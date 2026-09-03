<?php
if ( ! defined( 'ABSPATH' ) ) { exit; }

/**
 * The two REST routes under /wp-json/depicta/v1. Nothing here writes anything except a
 * single attachment's alt text, and every callback is gated by {@see authorize()}.
 */
class Depicta_REST {

	public function register_routes() {
		register_rest_route( DEPICTA_NS, '/media/missing-alt', array(
			'methods'             => WP_REST_Server::READABLE, // GET
			'callback'            => array( $this, 'list_missing_alt' ),
			'permission_callback' => array( $this, 'authorize' ),
			'args'                => array(
				'page'     => array( 'default' => 1,  'sanitize_callback' => 'absint' ),
				'per_page' => array( 'default' => 50, 'sanitize_callback' => 'absint' ),
			),
		) );

		register_rest_route( DEPICTA_NS, '/media/(?P<id>\d+)/alt', array(
			'methods'             => WP_REST_Server::CREATABLE, // POST
			'callback'            => array( $this, 'set_alt' ),
			'permission_callback' => array( $this, 'authorize' ),
			'args'                => array(
				'id'  => array(
					'required'          => true,
					'validate_callback' => static function ( $v ) { return is_numeric( $v ); },
					'sanitize_callback' => 'absint',
				),
				'alt' => array(
					'required'          => true,
					'type'              => 'string',
					// Enforce the alt-text contract server-side too: trim + cap length.
					'sanitize_callback' => static function ( $v ) {
						return mb_substr( sanitize_text_field( (string) $v ), 0, 125 );
					},
				),
			),
		) );
	}

	/**
	 * Four gates: HTTPS, an authenticated WP user (Application Password handles this before we
	 * run), the upload_files capability, and a signed request. The signature (HMAC-SHA256 over
	 * method + route + timestamp + nonce + body) proves the caller holds the shared secret
	 * without ever putting the secret on the wire, and the timestamp + one-time nonce make a
	 * captured request useless to replay.
	 */
	public function authorize( WP_REST_Request $request ) {
		// 1) Refuse plaintext transport in production.
		if ( ! is_ssl() && ( ! defined( 'WP_DEBUG' ) || ! WP_DEBUG ) ) {
			return new WP_Error( 'depicta_insecure', 'HTTPS is required.', array( 'status' => 403 ) );
		}

		// 2) Capability — the request's user must be allowed to manage media.
		if ( ! current_user_can( 'upload_files' ) ) {
			return new WP_Error( 'depicta_forbidden', 'Insufficient permissions.', array( 'status' => 403 ) );
		}

		// 3) Signed request (replaces the old plaintext-secret header).
		return $this->verify_signature( $request );
	}

	/**
	 * Verifies the HMAC signature and enforces the replay window. Returns true or a WP_Error.
	 */
	private function verify_signature( WP_REST_Request $request ) {
		$secret = (string) get_option( DEPICTA_SECRET_OPTION, '' );
		if ( '' === $secret ) {
			return new WP_Error( 'depicta_no_secret', 'Depicta is not configured.', array( 'status' => 401 ) );
		}

		$timestamp = (string) $request->get_header( 'x-depicta-timestamp' );
		$nonce     = (string) $request->get_header( 'x-depicta-nonce' );
		$signature = (string) $request->get_header( 'x-depicta-signature' );

		if ( '' === $timestamp || '' === $nonce || '' === $signature ) {
			return new WP_Error( 'depicta_unsigned', 'Missing request signature.', array( 'status' => 401 ) );
		}

		// Timestamp must be recent (defends against replay of old captures + clock nonsense).
		$tolerance = defined( 'DEPICTA_SIG_TOLERANCE' ) ? (int) DEPICTA_SIG_TOLERANCE : 300;
		if ( ! ctype_digit( $timestamp ) || abs( time() - (int) $timestamp ) > $tolerance ) {
			return new WP_Error( 'depicta_stale', 'Request timestamp outside allowed window.', array( 'status' => 401 ) );
		}

		// Nonce may be used only once within the tolerance window.
		if ( ! preg_match( '/^[a-f0-9]{16,64}$/', $nonce ) ) {
			return new WP_Error( 'depicta_bad_nonce', 'Malformed nonce.', array( 'status' => 401 ) );
		}
		$nonce_key = 'depicta_nonce_' . $nonce;
		if ( false !== get_transient( $nonce_key ) ) {
			return new WP_Error( 'depicta_replay', 'Request nonce already used.', array( 'status' => 401 ) );
		}

		// Reconstruct exactly what the backend signed. Route excludes the query string.
		$route = (string) $request->get_route();          // e.g. /depicta/v1/media/123/alt
		$prefix = '/' . DEPICTA_NS;                         // /depicta/v1
		if ( 0 === strpos( $route, $prefix ) ) {
			$route = substr( $route, strlen( $prefix ) );  // -> /media/123/alt
		}
		$body      = (string) $request->get_body();        // '' for GET
		$body_hash = base64_encode( hash( 'sha256', $body, true ) );
		$canonical = $request->get_method() . "\n" . $route . "\n" . $timestamp . "\n" . $nonce . "\n" . $body_hash;
		$expected  = base64_encode( hash_hmac( 'sha256', $canonical, $secret, true ) );

		if ( ! hash_equals( $expected, $signature ) ) {
			return new WP_Error( 'depicta_bad_signature', 'Invalid request signature.', array( 'status' => 401 ) );
		}

		// Accept — and burn the nonce so it can't be replayed within the window.
		set_transient( $nonce_key, 1, $tolerance );
		return true;
	}

	/** GET: attachments whose alt text is empty or missing. */
	public function list_missing_alt( WP_REST_Request $request ) {
		$page     = max( 1, (int) $request['page'] );
		$per_page = min( 100, max( 1, (int) $request['per_page'] ) );

		// Fetch every image attachment id, then filter for empty/missing alt in PHP.
		// This avoids a known WP_Query gotcha: two meta_query clauses on the SAME meta key
		// (NOT EXISTS OR = '') can generate incorrect SQL and miss rows.
		$query = new WP_Query( array(
			'post_type'      => 'attachment',
			'post_status'    => 'inherit',
			'post_mime_type' => 'image', // ALL image/* types (jpeg, png, gif, webp, avif, svg, bmp, …)
			'posts_per_page' => -1,
			'fields'         => 'ids',
			'no_found_rows'  => true,
			'orderby'        => 'ID',
			'order'          => 'ASC',
		) );

		$missing = array();
		foreach ( $query->posts as $id ) {
			if ( ! wp_attachment_is_image( $id ) ) {
				continue; // safety: skip non-images
			}
			$alt = get_post_meta( $id, '_wp_attachment_image_alt', true );
			if ( is_string( $alt ) && trim( $alt ) !== '' ) {
				continue; // already has alt text
			}
			$missing[] = (int) $id;
		}

		$total = count( $missing );
		$slice = array_slice( $missing, ( $page - 1 ) * $per_page, $per_page );

		$items = array();
		foreach ( $slice as $id ) {
			$meta   = wp_get_attachment_metadata( $id );
			$items[] = array(
				'attachmentId' => (int) $id,
				'url'          => wp_get_attachment_url( $id ),
				'filename'     => wp_basename( (string) get_attached_file( $id ) ),
				'mimeType'     => get_post_mime_type( $id ),
				'width'        => isset( $meta['width'] ) ? (int) $meta['width'] : null,
				'height'       => isset( $meta['height'] ) ? (int) $meta['height'] : null,
				'currentAlt'   => (string) get_post_meta( $id, '_wp_attachment_image_alt', true ),
			);
		}

		return new WP_REST_Response( array(
			'page'    => $page,
			'perPage' => $per_page,
			'total'   => $total,
			'items'   => $items,
		), 200 );
	}

	/** POST: set alt for one attachment, returning the value it had before (for revert). */
	public function set_alt( WP_REST_Request $request ) {
		$id  = (int) $request['id'];
		$alt = (string) $request['alt']; // already sanitized + capped by the arg schema

		$post = get_post( $id );
		if ( ! $post || 'attachment' !== $post->post_type || ! wp_attachment_is_image( $id ) ) {
			return new WP_Error( 'depicta_not_found', 'Attachment not found or not an image.', array( 'status' => 404 ) );
		}

		$previous = (string) get_post_meta( $id, '_wp_attachment_image_alt', true );
		update_post_meta( $id, '_wp_attachment_image_alt', $alt );

		return new WP_REST_Response( array(
			'applied'      => true,
			'attachmentId' => $id,
			'previousAlt'  => $previous,
		), 200 );
	}
}

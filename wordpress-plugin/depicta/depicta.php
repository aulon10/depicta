<?php
/**
 * Plugin Name:       Depicta Companion
 * Description:       Secure bridge that lets the Depicta backend read images missing alt
 *                    text and set alt text on a single attachment — nothing else.
 * Version:           0.2.0
 * Requires at least: 6.0
 * Requires PHP:      7.4
 * Author:            Aulon Murtezi
 * License:           GPL-2.0-or-later
 *
 * SECURITY MODEL (read before extending):
 *  - This plugin exposes EXACTLY two REST routes, both scoped to attachment alt metadata.
 *  - Every request must pass four checks: HTTPS, a valid WordPress login (Application
 *    Password), the `upload_files` capability, and a valid HMAC signature (timestamp + one-time
 *    nonce) that proves the caller holds the shared secret without ever sending it in the clear.
 *  - There is no route that runs code, changes options, or touches anything but a single
 *    attachment's `_wp_attachment_image_alt`. Keep it that way.
 */

if ( ! defined( 'ABSPATH' ) ) {
	exit; // No direct access.
}

define( 'DEPICTA_VERSION', '0.2.0' );
define( 'DEPICTA_NS', 'depicta/v1' );
define( 'DEPICTA_SECRET_OPTION', 'depicta_shared_secret' );
// How many seconds a signed request stays valid. Also the window a used nonce is remembered for.
define( 'DEPICTA_SIG_TOLERANCE', 300 );

require_once __DIR__ . '/includes/class-depicta-rest.php';
require_once __DIR__ . '/includes/class-depicta-admin.php';

// Register the REST routes.
add_action( 'rest_api_init', function () {
	( new Depicta_REST() )->register_routes();
} );

// Admin settings page (generate / view the shared secret).
if ( is_admin() ) {
	( new Depicta_Admin() )->init();
}

// On activation, generate a strong shared secret if one doesn't exist yet.
register_activation_hook( __FILE__, function () {
	if ( ! get_option( DEPICTA_SECRET_OPTION ) ) {
		add_option( DEPICTA_SECRET_OPTION, wp_generate_password( 48, false, false ) );
	}
} );

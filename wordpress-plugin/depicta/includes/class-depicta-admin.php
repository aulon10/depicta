<?php
if ( ! defined( 'ABSPATH' ) ) { exit; }

/**
 * Minimal settings screen under Settings → Depicta: shows the site's shared secret (which
 * the owner pastes into the Depicta dashboard) and lets them rotate it. Rotating instantly
 * invalidates the old secret, cutting the backend's access.
 */
class Depicta_Admin {

	public function init() {
		add_action( 'admin_menu', array( $this, 'menu' ) );
		add_action( 'admin_post_depicta_rotate_secret', array( $this, 'rotate_secret' ) );
	}

	public function menu() {
		add_options_page( 'Depicta', 'Depicta', 'manage_options', 'depicta', array( $this, 'render' ) );
	}

	public function render() {
		if ( ! current_user_can( 'manage_options' ) ) {
			return;
		}
		$secret = (string) get_option( DEPICTA_SECRET_OPTION, '' );
		?>
		<div class="wrap">
			<h1>Depicta</h1>
			<p>Paste this site's connection secret into your Depicta dashboard, together with a
			WordPress <strong>Application Password</strong> for a user who can manage media.</p>

			<table class="form-table" role="presentation">
				<tr>
					<th scope="row"><label for="depicta-secret">Shared secret</label></th>
					<td>
						<input id="depicta-secret" type="text" class="regular-text code" readonly
							value="<?php echo esc_attr( $secret ); ?>" onclick="this.select();" />
						<p class="description">Sent by the backend as the <code>X-Depicta-Secret</code> header on every request.</p>
					</td>
				</tr>
			</table>

			<form method="post" action="<?php echo esc_url( admin_url( 'admin-post.php' ) ); ?>">
				<input type="hidden" name="action" value="depicta_rotate_secret" />
				<?php wp_nonce_field( 'depicta_rotate_secret' ); ?>
				<?php submit_button( 'Rotate secret', 'secondary' ); ?>
				<p class="description">Rotating immediately revokes the old secret.</p>
			</form>
		</div>
		<?php
	}

	public function rotate_secret() {
		if ( ! current_user_can( 'manage_options' ) ) {
			wp_die( 'Forbidden', '', array( 'response' => 403 ) );
		}
		check_admin_referer( 'depicta_rotate_secret' );
		update_option( DEPICTA_SECRET_OPTION, wp_generate_password( 48, false, false ) );
		wp_safe_redirect( add_query_arg( array( 'page' => 'depicta', 'rotated' => '1' ), admin_url( 'options-general.php' ) ) );
		exit;
	}
}

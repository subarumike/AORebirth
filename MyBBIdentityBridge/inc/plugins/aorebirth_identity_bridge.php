<?php
/**
 * AORebirth Identity Bridge for MyBB.
 *
 * MyBB is a consumer of AORebirth identity. This plugin never receives,
 * validates, stores, or derives AO passwords or AO password hashes.
 */

if(!defined('IN_MYBB')){
	die('Direct initialization of this file is not allowed.');
}

$plugins->add_hook('member_register_start', 'aorebirth_identity_bridge_register_redirect');
$plugins->add_hook('member_do_register_start', 'aorebirth_identity_bridge_register_redirect');
$plugins->add_hook('member_login', 'aorebirth_identity_bridge_login_redirect');
$plugins->add_hook('member_do_login_start', 'aorebirth_identity_bridge_login_redirect');
$plugins->add_hook('member_lostpw', 'aorebirth_identity_bridge_login_redirect');
$plugins->add_hook('member_do_lostpw_start', 'aorebirth_identity_bridge_login_redirect');
$plugins->add_hook('member_resetpassword_start', 'aorebirth_identity_bridge_login_redirect');
$plugins->add_hook('usercp_password', 'aorebirth_identity_bridge_account_redirect');
$plugins->add_hook('usercp_do_password_start', 'aorebirth_identity_bridge_account_redirect');
$plugins->add_hook('usercp_email', 'aorebirth_identity_bridge_account_redirect');
$plugins->add_hook('usercp_do_email_start', 'aorebirth_identity_bridge_account_redirect');
$plugins->add_hook('usercp_changename_start', 'aorebirth_identity_bridge_account_redirect');
$plugins->add_hook('usercp_do_changename_start', 'aorebirth_identity_bridge_account_redirect');
$plugins->add_hook('misc_start', 'aorebirth_identity_bridge_misc_start');

function aorebirth_identity_bridge_info(){
	return array(
		'name' => 'AORebirth Identity Bridge',
		'description' => 'Routes MyBB login/registration through AORebirth Account Broker one-time-code SSO.',
		'website' => 'https://ao-rebirth.com',
		'author' => 'AORebirth',
		'authorsite' => 'https://ao-rebirth.com',
		'version' => '1.0.0',
		'compatibility' => '18*'
	);
}

function aorebirth_identity_bridge_activate(){
}

function aorebirth_identity_bridge_deactivate(){
}

function aorebirth_identity_bridge_register_redirect(){
	aorebirth_identity_bridge_redirect_to_website('https://ao-rebirth.com/register');
}

function aorebirth_identity_bridge_login_redirect(){
	$return = aorebirth_identity_bridge_forum_url('misc.php?action=aor_start');
	aorebirth_identity_bridge_redirect_to_website('https://ao-rebirth.com/login?return=' . rawurlencode($return));
}

function aorebirth_identity_bridge_account_redirect(){
	aorebirth_identity_bridge_redirect_to_website('https://ao-rebirth.com/account');
}

function aorebirth_identity_bridge_misc_start(){
	global $mybb;

	$action = isset($mybb->input['action']) ? (string)$mybb->input['action'] : '';
	if($action === 'aor_start'){
		aorebirth_identity_bridge_redirect_to_website('https://ao-rebirth.com/forum-login');
	}

	if($action !== 'aor_sso'){
		return;
	}

	$code = isset($mybb->input['code']) ? (string)$mybb->input['code'] : '';
	if($code === ''){
		aorebirth_identity_bridge_error('Missing AORebirth forum login code.');
	}

	$redeem = aorebirth_identity_bridge_broker_post('/api/forum/sso/redeem', array('code' => $code));
	if(!$redeem['ok'] || !isset($redeem['json']['identity']) || !is_array($redeem['json']['identity'])){
		aorebirth_identity_bridge_error('AORebirth forum login code is invalid or expired.');
	}

	$identity = $redeem['json']['identity'];
	if(!isset($identity['identityStatus']) || $identity['identityStatus'] !== 'Active'){
		aorebirth_identity_bridge_error('AORebirth identity is not active for forum login.');
	}

	$uid = 0;
	if(isset($identity['existingMybbUid']) && preg_match('/^[1-9][0-9]*$/', (string)$identity['existingMybbUid'])){
		$uid = (int)$identity['existingMybbUid'];
		if(!aorebirth_identity_bridge_user_exists($uid)){
			aorebirth_identity_bridge_error('Mapped forum account no longer exists.');
		}
	} else {
		$uid = aorebirth_identity_bridge_create_user($identity);
		$confirm = aorebirth_identity_bridge_broker_post(
			'/api/forum/mapping/confirm',
			array(
				'identityPublicId' => (string)$identity['identityPublicId'],
				'mybbUid' => (string)$uid
			)
		);
		if(!$confirm['ok']){
			aorebirth_identity_bridge_error('Forum account was created, but AORebirth mapping confirmation failed.');
		}
	}

	aorebirth_identity_bridge_login_uid($uid);
	$returnTo = isset($redeem['json']['returnTo']) ? (string)$redeem['json']['returnTo'] : '';
	if($returnTo === '' || strpos($returnTo, aorebirth_identity_bridge_forum_url('')) !== 0){
		$returnTo = aorebirth_identity_bridge_forum_url('index.php');
	}

	redirect($returnTo);
}

function aorebirth_identity_bridge_create_user($identity){
	$username = aorebirth_identity_bridge_username($identity);
	$email = isset($identity['email']) && filter_var($identity['email'], FILTER_VALIDATE_EMAIL)
		? (string)$identity['email']
		: strtolower($username) . '@forum.ao-rebirth.invalid';

	$existing = aorebirth_identity_bridge_find_user_by_username($username);
	if($existing !== null){
		aorebirth_identity_bridge_error('Forum username is already reserved.');
	}

	require_once MYBB_ROOT . 'inc/datahandlers/user.php';
	$password = aorebirth_identity_bridge_random_string(18);
	$userhandler = new UserDataHandler('insert');
	$userhandler->set_data(array(
		'username' => $username,
		'password' => $password,
		'password2' => $password,
		'email' => $email,
		'email2' => $email,
		'usergroup' => 2,
		'regdate' => TIME_NOW,
		'timezone' => 0,
		'language' => '',
		'profile_fields' => array()
	));

	if(!$userhandler->validate_user()){
		$errorCodes = array();
		foreach($userhandler->get_errors() as $error){
			if(isset($error['error_code'])){
				$errorCodes[] = preg_replace('/[^A-Za-z0-9_]/', '', (string)$error['error_code']);
			}
		}
		aorebirth_identity_bridge_error('Forum user validation failed: ' . implode(',', $errorCodes));
	}

	$inserted = $userhandler->insert_user();
	$uid = is_array($inserted) && isset($inserted['uid']) ? (int)$inserted['uid'] : (int)$inserted;
	if($uid < 1){
		aorebirth_identity_bridge_error('Forum user creation failed.');
	}

	return $uid;
}

function aorebirth_identity_bridge_login_uid($uid){
	global $db, $session;

	require_once MYBB_ROOT . 'inc/functions_user.php';
	$loginkey = function_exists('generate_loginkey') ? generate_loginkey() : aorebirth_identity_bridge_random_string(50);
	$db->update_query('users', array('loginkey' => $db->escape_string($loginkey)), 'uid=' . (int)$uid);
	my_setcookie('mybbuser', (int)$uid . '_' . $loginkey, null, true);
	if(isset($session) && isset($session->sid)){
		$db->update_query('sessions', array('uid' => (int)$uid), "sid='" . $db->escape_string($session->sid) . "'");
		my_setcookie('sid', $session->sid, -1, true);
	}
}

function aorebirth_identity_bridge_username($identity){
	$username = isset($identity['username']) ? (string)$identity['username'] : '';
	if(!preg_match('/^[A-Za-z0-9]{6,32}$/', $username)){
		aorebirth_identity_bridge_error('AORebirth username cannot be mapped to a forum username.');
	}

	return $username;
}

function aorebirth_identity_bridge_find_user_by_username($username){
	global $db;

	$query = $db->simple_select('users', 'uid,email', "username='" . $db->escape_string($username) . "'", array('limit' => 1));
	$row = $db->fetch_array($query);
	return $row ? $row : null;
}

function aorebirth_identity_bridge_user_exists($uid){
	global $db;

	$query = $db->simple_select('users', 'uid', 'uid=' . (int)$uid, array('limit' => 1));
	return (bool)$db->fetch_field($query, 'uid');
}

function aorebirth_identity_bridge_broker_post($path, $fields){
	$config = aorebirth_identity_bridge_config();
	$url = rtrim($config['broker_url'], '/') . '/' . ltrim($path, '/');
	$body = http_build_query($fields, '', '&');
	$headers = array(
		'Accept: application/json',
		'Content-Type: application/x-www-form-urlencoded',
		'X-AORebirth-Forum-SSO-Secret: ' . $config['secret']
	);

	if(function_exists('curl_init')){
		$ch = curl_init($url);
		curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
		curl_setopt($ch, CURLOPT_POST, true);
		curl_setopt($ch, CURLOPT_POSTFIELDS, $body);
		curl_setopt($ch, CURLOPT_HTTPHEADER, $headers);
		curl_setopt($ch, CURLOPT_TIMEOUT, 8);
		$response = curl_exec($ch);
		$status = $response === false ? 0 : (int)curl_getinfo($ch, CURLINFO_RESPONSE_CODE);
		curl_close($ch);
	} else {
		$context = stream_context_create(array(
			'http' => array(
				'method' => 'POST',
				'header' => implode("\r\n", $headers),
				'content' => $body,
				'timeout' => 8,
				'ignore_errors' => true
			)
		));
		$response = @file_get_contents($url, false, $context);
		$status = $response === false ? 0 : 200;
	}

	$json = json_decode($response ?: '', true);
	return array(
		'ok' => $status >= 200 && $status < 300 && is_array($json) && !empty($json['ok']),
		'status' => $status,
		'json' => is_array($json) ? $json : array()
	);
}

function aorebirth_identity_bridge_config(){
	$config = array(
		'broker_url' => 'http://172.18.0.1:7510',
		'secret_file' => '/run/secrets/forum_sso_secret'
	);
	$configFile = MYBB_ROOT . 'inc/aorebirth_identity_bridge_config.php';
	if(is_readable($configFile)){
		$loaded = include $configFile;
		if(is_array($loaded)){
			$config = array_merge($config, $loaded);
		}
	}

	$secret = '';
	if(!empty($config['secret_file']) && is_readable($config['secret_file'])){
		$secret = trim(file_get_contents($config['secret_file']));
	}
	if($secret === '' && !empty($config['secret'])){
		$secret = (string)$config['secret'];
	}
	if($secret === ''){
		aorebirth_identity_bridge_error('AORebirth forum bridge secret is not configured.');
	}

	$config['secret'] = $secret;
	return $config;
}

function aorebirth_identity_bridge_forum_url($path){
	global $mybb;

	$base = isset($mybb->settings['bburl']) ? rtrim($mybb->settings['bburl'], '/') . '/' : 'https://forum.ao-rebirth.com/';
	return $base . ltrim($path, '/');
}

function aorebirth_identity_bridge_random_string($bytes){
	return rtrim(strtr(base64_encode(random_bytes($bytes)), '+/', '-_'), '=');
}

function aorebirth_identity_bridge_redirect_to_website($url){
	redirect($url);
}

function aorebirth_identity_bridge_error($message){
	error($message);
	exit;
}

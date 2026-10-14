import { Redirect } from 'expo-router';

/**
 * Target of the OIDC redirect `fincore://auth/callback`. The system browser session hands the code to
 * expo-auth-session directly; when Android also delivers the deep link to the router, this sends it home instead of
 * showing "not found".
 */
export default function AuthCallback() {
  return <Redirect href="/" />;
}

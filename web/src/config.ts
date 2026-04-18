function req(name: keyof ImportMetaEnv): string {
  const v = import.meta.env[name];
  if (!v) throw new Error(`Missing ${String(name)} in environment (.env)`);
  return v;
}

export const config = {
  cognitoAuthority: req("VITE_COGNITO_AUTHORITY"),
  cognitoClientId: req("VITE_COGNITO_CLIENT_ID"),
  redirectUri: req("VITE_REDIRECT_URI"),
  postLogoutRedirectUri: req("VITE_POST_LOGOUT_REDIRECT_URI"),
  apiBaseUrl: req("VITE_API_BASE_URL").replace(/\/$/, ""),
};

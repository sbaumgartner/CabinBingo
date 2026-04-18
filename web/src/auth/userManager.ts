import { UserManager, WebStorageStateStore } from "oidc-client-ts";
import { config } from "../config";

export const userManager = new UserManager({
  authority: config.cognitoAuthority,
  client_id: config.cognitoClientId,
  redirect_uri: config.redirectUri,
  response_type: "code",
  scope: "openid email profile",
  userStore: new WebStorageStateStore({ store: window.localStorage }),
  automaticSilentRenew: false,
});

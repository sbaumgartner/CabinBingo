import { userManager } from "./auth/userManager";
import { config } from "./config";

let logoutInFlight: Promise<void> | null = null;

async function logoutOnUnauthorized(): Promise<void> {
  if (logoutInFlight) return logoutInFlight;

  logoutInFlight = (async () => {
    try {
      await userManager.removeUser();
      await userManager.signoutRedirect({
        post_logout_redirect_uri: config.postLogoutRedirectUri,
      });
    } finally {
      logoutInFlight = null;
    }
  })();

  return logoutInFlight;
}

async function authHeader(): Promise<HeadersInit> {
  const user = await userManager.getUser();
  if (user?.expired) {
    await logoutOnUnauthorized();
    return {};
  }

  const token = user?.access_token;
  if (!token) return {};
  return { Authorization: `Bearer ${token}` };
}

async function ensureOk(path: string, res: Response): Promise<void> {
  if (res.status === 401) {
    await logoutOnUnauthorized();
    throw new Error("Your session expired. Signing out.");
  }

  if (!res.ok) {
    throw new Error(`${path} failed: ${res.status} ${await res.text()}`);
  }
}

export async function apiGet<T>(path: string): Promise<T> {
  const res = await fetch(`${config.apiBaseUrl}${path}`, {
    headers: { ...(await authHeader()) },
  });
  await ensureOk(path, res);
  const text = await res.text();
  if (!text) return null as T;
  return JSON.parse(text) as T;
}

export async function apiPutJson(path: string, body: unknown): Promise<void> {
  const res = await fetch(`${config.apiBaseUrl}${path}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...(await authHeader()) },
    body: JSON.stringify(body),
  });
  await ensureOk(path, res);
}

export async function apiPostJson<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`${config.apiBaseUrl}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...(await authHeader()) },
    body: JSON.stringify(body),
  });
  await ensureOk(path, res);
  return (await res.json()) as T;
}

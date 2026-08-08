export const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5080";

export type AuthResponse = {
  accessToken: string;
  refreshToken: string;
  userId: string;
  email: string;
  fullName: string;
  credits: number;
  roles: string[];
};

export const TOKEN_KEY = "abs_access";
export const REFRESH_KEY = "abs_refresh";
export const USER_KEY = "abs_user";
export const ROLES_COOKIE = "abs_roles";

function setCookie(name: string, value: string, maxAgeSec: number) {
  if (typeof document === "undefined") return;
  document.cookie = `${name}=${encodeURIComponent(value)}; Path=/; Max-Age=${maxAgeSec}; SameSite=Lax`;
}

function clearCookie(name: string) {
  if (typeof document === "undefined") return;
  document.cookie = `${name}=; Path=/; Max-Age=0; SameSite=Lax`;
}

export function getAccessToken() {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(TOKEN_KEY);
}

export function getRefreshToken() {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(REFRESH_KEY);
}

export function getStoredUser(): AuthResponse | null {
  if (typeof window === "undefined") return null;
  const raw = localStorage.getItem(USER_KEY);
  return raw ? (JSON.parse(raw) as AuthResponse) : null;
}

export function setAuth(auth: AuthResponse) {
  localStorage.setItem(TOKEN_KEY, auth.accessToken);
  localStorage.setItem(REFRESH_KEY, auth.refreshToken);
  localStorage.setItem(USER_KEY, JSON.stringify(auth));
  // Middleware-readable session markers (token itself stays in localStorage for API calls).
  setCookie(TOKEN_KEY, "1", 60 * 60 * 24 * 14);
  setCookie(ROLES_COOKIE, auth.roles.join(","), 60 * 60 * 24 * 14);
  window.dispatchEvent(new Event("abs-auth-changed"));
}

export function patchStoredUser(partial: Partial<AuthResponse>) {
  const current = getStoredUser();
  if (!current) return;
  const next = { ...current, ...partial };
  localStorage.setItem(USER_KEY, JSON.stringify(next));
  if (next.roles) setCookie(ROLES_COOKIE, next.roles.join(","), 60 * 60 * 24 * 14);
  window.dispatchEvent(new Event("abs-auth-changed"));
}

export function clearAuth() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(REFRESH_KEY);
  localStorage.removeItem(USER_KEY);
  clearCookie(TOKEN_KEY);
  clearCookie(ROLES_COOKIE);
  window.dispatchEvent(new Event("abs-auth-changed"));
}

async function tryRefresh(): Promise<boolean> {
  const refreshToken = getRefreshToken();
  if (!refreshToken) return false;
  const res = await fetch(`${API_URL}/api/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  });
  if (!res.ok) return false;
  const auth = (await res.json()) as AuthResponse;
  setAuth(auth);
  return true;
}

export async function api<T>(path: string, options: RequestInit = {}, retry = true): Promise<T> {
  const headers = new Headers(options.headers);
  if (!headers.has("Content-Type") && options.body) {
    headers.set("Content-Type", "application/json");
  }
  const token = getAccessToken();
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const res = await fetch(`${API_URL}${path}`, { ...options, headers });
  if (res.status === 401 && retry) {
    const ok = await tryRefresh();
    if (ok) return api<T>(path, options, false);
    clearAuth();
    throw new Error("Session expired. Please sign in again.");
  }
  if (!res.ok) {
    let message = res.statusText;
    try {
      const body = await res.json();
      message = body.error || body.title || message;
    } catch {
      /* ignore */
    }
    throw new Error(message);
  }
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  if (!text) return undefined as T;
  return JSON.parse(text) as T;
}

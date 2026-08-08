import { api, AuthResponse, setAuth, clearAuth, getStoredUser, patchStoredUser } from "./client";

export type MeResponse = {
  userId: string;
  email: string;
  fullName: string;
  credits: number;
  roles: string[];
};

export async function login(email: string, password: string) {
  const auth = await api<AuthResponse>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password }),
  });
  setAuth(auth);
  return auth;
}

export async function register(fullName: string, email: string, password: string) {
  const auth = await api<AuthResponse>("/api/auth/register", {
    method: "POST",
    body: JSON.stringify({ fullName, email, password }),
  });
  setAuth(auth);
  return auth;
}

export async function refreshProfile(): Promise<AuthResponse | null> {
  try {
    const me = await api<MeResponse>("/api/auth/me");
    const current = getStoredUser();
    if (!current) return null;
    const next = {
      ...current,
      userId: me.userId,
      email: me.email,
      fullName: me.fullName,
      credits: me.credits,
      roles: me.roles,
    };
    patchStoredUser(next);
    return next;
  } catch {
    return null;
  }
}

export function signOut() {
  clearAuth();
}

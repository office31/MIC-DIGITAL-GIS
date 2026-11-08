import { createAuthClient } from "@neon/auth";

const authUrl = import.meta.env.VITE_NEON_AUTH_URL;
const authBaseUrl = authUrl ? authUrl.replace(/\/+$/, "") : null;

export const authClient = authBaseUrl ? createAuthClient(authBaseUrl) : null;

export async function getAuthToken() {
  if (!authBaseUrl) return null;
  try {
    const response = await fetch(`${authBaseUrl}/token`, {
      credentials: "include",
      headers: { accept: "application/json" },
    });
    if (!response.ok) return null;
    const payload = await response.json();
    return typeof payload?.token === "string" && payload.token.length > 0 ? payload.token : null;
  } catch (error) {
    console.error("Impossible de récupérer le jeton de session Neon Auth :", error);
    return null;
  }
}

export function notifyAuthChanged(user) {
  window.dispatchEvent(new CustomEvent("mic-auth-changed", {
    detail: { signedIn: Boolean(user), user: user || null },
  }));
}

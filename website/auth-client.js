import { createAuthClient } from "@neon/auth";

const authUrl = import.meta.env.VITE_NEON_AUTH_URL;

export const authClient = authUrl ? createAuthClient(authUrl) : null;

export async function getAuthToken() {
  if (!authClient) return null;
  try {
    const token = await authClient.getJWTToken?.();
    return typeof token === "string" && token.length > 0 ? token : null;
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

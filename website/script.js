import { createAuthClient } from "@neon/auth";
import { initializeWorldClock } from "./world-clock.js";
import "./world-clock.css";

const year = document.querySelector("[data-current-year]");
if (year) year.textContent = String(new Date().getFullYear());

const header = document.querySelector(".site-header");
const updateHeader = () => header?.classList.toggle("is-scrolled", window.scrollY > 12);
updateHeader();
window.addEventListener("scroll", updateHeader, { passive: true });

const targets = document.querySelectorAll(".hero-copy, .hero-panel, .section-heading, .card, .two-col > *, .step, .sponsor-box, .auth-intro, .auth-card");
targets.forEach((element, index) => {
  element.dataset.reveal = "";
  if (element.classList.contains("card") || element.classList.contains("step")) element.style.transitionDelay = `${(index % 4) * 70}ms`;
});
if ("IntersectionObserver" in window && !window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
  document.documentElement.classList.add("reveal-ready");
  const observer = new IntersectionObserver((entries, currentObserver) => {
    entries.forEach((entry) => {
      if (entry.isIntersecting) {
        entry.target.classList.add("is-visible");
        currentObserver.unobserve(entry.target);
      }
    });
  }, { threshold: 0.12 });
  targets.forEach((element) => observer.observe(element));
} else targets.forEach((element) => element.classList.add("is-visible"));

initializeWorldClock();

const status = document.querySelector("#auth-status");
const signupForm = document.querySelector("#signup-panel");
const signinForm = document.querySelector("#signin-panel");
const accountView = document.querySelector("#account-view");
const signupTab = document.querySelector("#signup-tab");
const signinTab = document.querySelector("#signin-tab");
let authClient;

function showMessage(message, kind = "info") {
  status.textContent = message;
  status.dataset.kind = kind;
}
function setBusy(form, busy) {
  const button = form.querySelector("button[type='submit']");
  if (button) {
    button.disabled = busy;
    button.setAttribute("aria-busy", String(busy));
  }
}
function getErrorMessage(result) {
  const error = result?.error || result?.err;
  return error?.message || error?.statusText || "La demande n’a pas abouti. Vérifiez les informations et réessayez.";
}
function updateSession(result) {
  const sessionData = result?.data;
  const user = sessionData?.user || sessionData?.session?.user;
  if (!user) return false;
  document.querySelector("#account-email").textContent = user.email || "Compte connecté";
  accountView.hidden = false;
  signupForm.hidden = true;
  signinForm.hidden = true;
  signupTab.hidden = true;
  signinTab.hidden = true;
  showMessage("Vous êtes connecté. Votre session est active sur cet appareil.", "success");
  return true;
}

function switchMode(mode) {
  const signup = mode === "signup";
  signupForm.hidden = !signup;
  signinForm.hidden = signup;
  signupTab.classList.toggle("is-active", signup);
  signinTab.classList.toggle("is-active", !signup);
  signupTab.setAttribute("aria-selected", String(signup));
  signinTab.setAttribute("aria-selected", String(!signup));
  showMessage(signup ? "Créez votre compte avec une adresse e-mail valide." : "Connectez-vous à votre compte.");
}
signupTab.addEventListener("click", () => switchMode("signup"));
signinTab.addEventListener("click", () => switchMode("signin"));

async function initializeAuth() {
  const authUrl = import.meta.env.VITE_NEON_AUTH_URL;
  if (!authUrl) {
    showMessage("Neon Auth n’est pas encore configuré pour le site. Ajoutez VITE_NEON_AUTH_URL aux variables Render après avoir activé Auth avec la CLI Neon.", "warning");
    signupForm.querySelectorAll("input, button").forEach((control) => { control.disabled = true; });
    signinForm.querySelectorAll("input, button").forEach((control) => { control.disabled = true; });
    return;
  }

  try {
    authClient = createAuthClient(authUrl);
    const result = await authClient.getSession();
    if (!result?.error && !result?.err && updateSession(result)) return;
    switchMode("signup");
  } catch (error) {
    console.error("Neon Auth initialization failed:", error);
    showMessage("Impossible de joindre le service d’authentification. Vérifiez l’URL Neon et les domaines autorisés.", "error");
  }
}

signupForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (!authClient) return;
  const form = new FormData(signupForm);
  const payload = { name: String(form.get("name")).trim(), email: String(form.get("email")).trim(), password: String(form.get("password")) };
  setBusy(signupForm, true);
  showMessage("Création du compte…");
  try {
    const result = await authClient.signUp.email(payload);
    if (result?.error || result?.err) throw new Error(getErrorMessage(result));
    if (!updateSession(result)) showMessage("Compte créé. Vérifiez votre boîte e-mail si une vérification est demandée, puis connectez-vous.", "success");
  } catch (error) {
    showMessage(error.message || getErrorMessage(error), "error");
  } finally { setBusy(signupForm, false); }
});

signinForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (!authClient) return;
  const form = new FormData(signinForm);
  setBusy(signinForm, true);
  showMessage("Connexion…");
  try {
    const result = await authClient.signIn.email({ email: String(form.get("email")).trim(), password: String(form.get("password")) });
    if (result?.error || result?.err) throw new Error(getErrorMessage(result));
    if (!updateSession(result)) {
      const session = await authClient.getSession();
      if (!updateSession(session)) showMessage("Connexion effectuée, mais la session n’a pas été retournée. Rechargez la page pour vérifier.", "warning");
    }
  } catch (error) {
    showMessage(error.message || getErrorMessage(error), "error");
  } finally { setBusy(signinForm, false); }
});

document.querySelector("#signout-button").addEventListener("click", async () => {
  try {
    const result = await authClient.signOut();
    if (result?.error || result?.err) throw new Error(getErrorMessage(result));
    accountView.hidden = true;
    signupTab.hidden = false;
    signinTab.hidden = false;
    switchMode("signup");
    showMessage("Vous êtes déconnecté.", "success");
  } catch (error) { showMessage(error.message || "La déconnexion a échoué.", "error"); }
});

initializeAuth();

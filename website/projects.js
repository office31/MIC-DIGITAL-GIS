import { API_BASE_URL } from "./api-config.js";
import { getAuthToken } from "./auth-client.js";

function messageFor(error) {
  if (error.code === "AUTH_REQUIRED") {
    return "Connectez-vous pour créer et consulter vos projets.";
  }
  if (error.status === 401) {
    return "Votre session a expiré. Reconnectez-vous pour continuer.";
  }
  if (error.status === 429) {
    return "Trop de projets créés récemment. Réessayez dans quelques minutes.";
  }
  if (error.name === "AbortError") {
    return "L’API met trop de temps à répondre. Elle est peut-être en veille : réessayez dans une minute.";
  }
  return error.message || "Impossible de joindre l’API. Vérifiez votre connexion et réessayez.";
}

async function request(path, options = {}) {
  const token = await getAuthToken();
  if (!token) {
    const error = new Error("Connectez-vous pour créer et consulter vos projets.");
    error.code = "AUTH_REQUIRED";
    throw error;
  }

  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), 75_000);
  try {
    const response = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      signal: controller.signal,
      headers: {
        ...(options.body ? { "Content-Type": "application/json" } : {}),
        ...options.headers,
        Authorization: `Bearer ${token}`,
      },
    });
    const payload = await response.json().catch(() => ({}));
    if (!response.ok) {
      const error = new Error(payload.error || `L’API a répondu avec le code ${response.status}.`);
      error.status = response.status;
      throw error;
    }
    return payload;
  } finally {
    window.clearTimeout(timeout);
  }
}

function createProjectSection() {
  const section = document.createElement("section");
  section.className = "section projects-section";
  section.id = "projects";
  section.innerHTML = `
    <div class="container">
      <div class="section-heading">
        <span class="eyebrow">Espace personnel</span>
        <h2>Vos projets géospatiaux</h2>
        <p class="projects-intro">Connectez-vous pour créer et retrouver vos propres projets enregistrés dans la base de données.</p>
      </div>
      <div class="projects-layout">
        <form class="projects-form wrap-panel" id="project-form">
          <h3>Nouveau projet</h3>
          <label for="project-name">Nom du projet</label>
          <input id="project-name" name="name" maxlength="200" required />
          <label for="project-description">Description <span>(facultative)</span></label>
          <textarea id="project-description" name="description" rows="4" maxlength="2000"></textarea>
          <button class="btn btn-primary" type="submit">Enregistrer le projet</button>
        </form>
        <div class="projects-results">
          <div class="projects-list-header"><h3>Mes projets récents</h3><button class="projects-refresh" id="projects-refresh" type="button">Actualiser</button></div>
          <p class="projects-status" id="projects-status" role="status" aria-live="polite">Vérification de votre session…</p>
          <div class="projects-list" id="projects-list"></div>
        </div>
      </div>
    </div>`;
  return section;
}

function renderProjects(container, projects) {
  container.replaceChildren();
  if (!projects.length) {
    const empty = document.createElement("p");
    empty.className = "projects-empty";
    empty.textContent = "Aucun projet pour le moment. Créez le premier avec le formulaire.";
    container.append(empty);
    return;
  }

  projects.forEach((project) => {
    const card = document.createElement("article");
    card.className = "project-item wrap-panel";
    const title = document.createElement("h4");
    title.textContent = project.name;
    const description = document.createElement("p");
    description.textContent = project.description || "Aucune description.";
    const created = document.createElement("time");
    if (project.created_at) {
      created.dateTime = project.created_at;
      created.textContent = new Intl.DateTimeFormat("fr-FR", { dateStyle: "medium", timeStyle: "short" }).format(new Date(project.created_at));
    }
    card.append(title, description);
    if (created.textContent) card.append(created);
    container.append(card);
  });
}

export function initializeProjects() {
  const main = document.querySelector("main");
  if (!main || document.querySelector("#projects")) return;

  const section = createProjectSection();
  const insertionPoint = main.querySelector("#account, #support");
  main.insertBefore(section, insertionPoint || null);

  const nav = document.querySelector(".nav");
  if (nav && !nav.querySelector('a[href="#projects"]')) {
    const link = document.createElement("a");
    link.href = "#projects";
    link.textContent = "Projets";
    nav.insertBefore(link, nav.querySelector('a[href="#account"], a[href="#support"]') || null);
  }

  const form = section.querySelector("#project-form");
  const status = section.querySelector("#projects-status");
  const list = section.querySelector("#projects-list");
  const submitButton = form.querySelector("button[type='submit']");
  const refreshButton = section.querySelector("#projects-refresh");
  let signedIn = false;

  function applyAuthState(isSignedIn) {
    signedIn = Boolean(isSignedIn);
    submitButton.disabled = !signedIn;
    refreshButton.disabled = !signedIn;
    form.querySelectorAll("input, textarea").forEach((control) => { control.disabled = !signedIn; });
    if (!signedIn) {
      list.replaceChildren();
      status.dataset.kind = "info";
      status.textContent = "Connectez-vous pour créer et consulter vos projets.";
      return;
    }
    loadProjects();
  }

  async function loadProjects() {
    status.dataset.kind = "info";
    status.textContent = "Chargement des projets… Si l’API est en veille, le premier démarrage peut prendre 30 à 60 secondes.";
    try {
      const payload = await request("/api/projects");
      const projects = Array.isArray(payload) ? payload : payload.projects;
      if (!Array.isArray(projects)) throw new Error("La réponse de l’API n’a pas le format attendu.");
      renderProjects(list, projects);
      status.dataset.kind = "success";
      status.textContent = `${projects.length} projet${projects.length === 1 ? "" : "s"} chargé${projects.length === 1 ? "" : "s"}.`;
    } catch (error) {
      status.dataset.kind = "error";
      status.textContent = messageFor(error);
      if (error.status === 401) {
        submitButton.disabled = true;
        form.querySelectorAll("input, textarea").forEach((control) => { control.disabled = true; });
      }
    }
  }

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!signedIn) return;
    submitButton.disabled = true;
    status.dataset.kind = "info";
    status.textContent = "Enregistrement du projet… Le premier réveil de l’API peut prendre jusqu’à 60 secondes.";
    const formData = new FormData(form);
    try {
      await request("/api/projects", {
        method: "POST",
        body: JSON.stringify({
          name: String(formData.get("name")).trim(),
          description: String(formData.get("description")).trim(),
        }),
      });
      form.reset();
      status.dataset.kind = "success";
      status.textContent = "Projet enregistré. Actualisation de la liste…";
      await loadProjects();
    } catch (error) {
      status.dataset.kind = "error";
      status.textContent = messageFor(error);
    } finally {
      submitButton.disabled = false;
    }
  });

  refreshButton.addEventListener("click", loadProjects);
  window.addEventListener("mic-auth-changed", (event) => applyAuthState(event.detail?.signedIn));
  applyAuthState(false);
  status.textContent = "Vérification de votre session…";
}

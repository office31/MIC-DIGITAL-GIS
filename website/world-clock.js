const DEFAULT_ZONES = [
  { city: "Paris", zone: "Europe/Paris" },
  { city: "Londres", zone: "Europe/London" },
  { city: "New York", zone: "America/New_York" },
  { city: "Los Angeles", zone: "America/Los_Angeles" },
  { city: "Dubaï", zone: "Asia/Dubai" },
  { city: "Tokyo", zone: "Asia/Tokyo" },
  { city: "Sydney", zone: "Australia/Sydney" },
];

const TIME_FORMAT = new Intl.DateTimeFormat("fr-FR", {
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
  hourCycle: "h23",
});

function formatTime(date, timeZone) {
  return new Intl.DateTimeFormat("fr-FR", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hourCycle: "h23",
    timeZone,
  }).format(date);
}

function formatDate(date, timeZone) {
  return new Intl.DateTimeFormat("fr-FR", {
    weekday: "long",
    day: "numeric",
    month: "long",
    year: "numeric",
    timeZone,
  }).format(date);
}

function zoneOffset(date, timeZone) {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone,
    timeZoneName: "shortOffset",
  }).formatToParts(date);
  return parts.find((part) => part.type === "timeZoneName")?.value || "UTC";
}

export function initializeWorldClock() {
  const main = document.querySelector("main");
  if (!main || document.querySelector("#world-clock")) return;

  const section = document.createElement("section");
  section.id = "world-clock";
  section.className = "section world-clock-section";
  section.innerHTML = `
    <div class="container">
      <div class="section-heading world-clock-heading">
        <span class="eyebrow">Heure mondiale</span>
        <h2>Un même instant, partout dans le monde.</h2>
        <p>Consultez l’heure locale en direct et comparez les fuseaux horaires utiles à vos équipes et projets internationaux.</p>
      </div>
      <div class="world-clock-toolbar">
        <label for="timezone-select">Ajouter une ville</label>
        <select id="timezone-select" aria-label="Choisir un fuseau horaire"></select>
        <button class="world-clock-add" id="timezone-add" type="button">Ajouter le fuseau</button>
        <span class="world-clock-live"><span class="live-dot" aria-hidden="true"></span>Mis à jour en direct</span>
      </div>
      <div class="world-clock-grid" id="world-clock-grid" aria-live="polite"></div>
      <p class="world-clock-footnote">Les heures sont calculées par le navigateur à partir de la base de fuseaux horaires de l’appareil.</p>
    </div>`;

  const accountSection = main.querySelector("#account");
  main.insertBefore(section, accountSection || null);

  const nav = document.querySelector(".nav");
  if (nav && !nav.querySelector('a[href="#world-clock"]')) {
    const link = document.createElement("a");
    link.href = "#world-clock";
    link.textContent = "Heure mondiale";
    nav.insertBefore(link, nav.querySelector('a[href="#account"]') || null);
  }

  const localZone = Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
  const zones = [{ city: "Votre heure locale", zone: localZone, local: true }, ...DEFAULT_ZONES.filter((item) => item.zone !== localZone)];
  const grid = section.querySelector("#world-clock-grid");
  const select = section.querySelector("#timezone-select");
  const addButton = section.querySelector("#timezone-add");

  function renderCards() {
    const now = new Date();
    grid.replaceChildren();
    zones.forEach(({ city, zone, local }) => {
      const card = document.createElement("article");
      card.className = "world-clock-card";
      const top = document.createElement("div");
      top.className = "world-clock-card-top";
      const cityLabel = document.createElement("h3");
      cityLabel.textContent = city;
      const offset = document.createElement("span");
      offset.className = "world-clock-offset";
      offset.textContent = zoneOffset(now, zone);
      top.append(cityLabel, offset);

      const time = document.createElement("time");
      time.className = "world-clock-time";
      time.dateTime = now.toISOString();
      time.textContent = formatTime(now, zone);
      time.setAttribute("aria-label", `Heure actuelle à ${city}: ${time.textContent}`);

      const details = document.createElement("div");
      details.className = "world-clock-details";
      const date = document.createElement("span");
      date.className = "world-clock-date";
      date.textContent = formatDate(now, zone);
      const zoneName = document.createElement("span");
      zoneName.className = "world-clock-zone";
      zoneName.textContent = zone;
      details.append(date, zoneName);
      card.append(top, time, details);

      if (!local) {
        const remove = document.createElement("button");
        remove.type = "button";
        remove.className = "world-clock-remove";
        remove.textContent = "Retirer";
        remove.setAttribute("aria-label", `Retirer le fuseau horaire ${city}`);
        remove.addEventListener("click", () => {
          const index = zones.findIndex((item) => item.zone === zone);
          if (index >= 0) zones.splice(index, 1);
          updateOptions();
          renderCards();
        });
        card.append(remove);
      }
      grid.append(card);
    });
  }

  function updateOptions() {
    const available = DEFAULT_ZONES.filter(({ zone }) => !zones.some((item) => item.zone === zone));
    select.replaceChildren();
    if (!available.length) {
      const option = document.createElement("option");
      option.textContent = "Toutes les villes sont affichées";
      option.value = "";
      select.append(option);
      addButton.disabled = true;
      return;
    }
    available.forEach(({ city, zone }) => {
      const option = document.createElement("option");
      option.value = zone;
      option.textContent = `${city} — ${zone}`;
      select.append(option);
    });
    addButton.disabled = false;
  }

  addButton.addEventListener("click", () => {
    const selected = DEFAULT_ZONES.find(({ zone }) => zone === select.value);
    if (!selected || zones.some((item) => item.zone === selected.zone)) return;
    zones.push(selected);
    updateOptions();
    renderCards();
  });

  updateOptions();
  renderCards();
  window.setInterval(renderCards, 1000);
}

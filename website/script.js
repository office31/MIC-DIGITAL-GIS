(() => {
  const year = document.querySelector("[data-current-year]");
  if (year) year.textContent = String(new Date().getFullYear());

  const header = document.querySelector(".site-header");
  const updateHeader = () => header?.classList.toggle("is-scrolled", window.scrollY > 12);
  updateHeader();
  window.addEventListener("scroll", updateHeader, { passive: true });

  const targets = document.querySelectorAll(
    ".hero-copy, .hero-panel, .section-heading, .card, .two-col > *, .step, .sponsor-box"
  );
  targets.forEach((element, index) => {
    element.dataset.reveal = "";
    if (element.classList.contains("card") || element.classList.contains("step")) {
      element.style.transitionDelay = `${(index % 4) * 70}ms`;
    }
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
  } else {
    targets.forEach((element) => element.classList.add("is-visible"));
  }
})();

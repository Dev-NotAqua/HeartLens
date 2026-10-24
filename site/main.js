(() => {
  "use strict";

  const root = document.documentElement;
  const header = document.querySelector(".masthead");
  const navToggle = document.querySelector(".nav-toggle");
  const primaryNav = document.querySelector("#primary-nav");

  /* Theme ----------------------------------------------------------------- */

  const themeToggle = document.querySelector(".theme-toggle");
  const themeMeta = document.querySelector('meta[name="theme-color"]');
  const themeLabel = themeToggle?.querySelector(".sr-only");

  const applyTheme = (theme, persist = false) => {
    const next = theme === "dark" ? "dark" : "light";
    root.dataset.theme = next;

    if (themeToggle) {
      themeToggle.setAttribute("aria-pressed", String(next === "dark"));
    }

    if (themeLabel) {
      themeLabel.textContent = next === "dark" ? "Switch to light theme" : "Switch to dark theme";
    }

    if (themeMeta) {
      themeMeta.setAttribute("content", next === "dark" ? "#101a16" : "#efe6d4");
    }

    if (persist) {
      try {
        localStorage.setItem("hearthlens-theme", next);
      } catch {
        /* The selected theme still applies for this visit. */
      }
    }
  };

  applyTheme(root.dataset.theme || "light");

  themeToggle?.addEventListener("click", () => {
    applyTheme(root.dataset.theme === "dark" ? "light" : "dark", true);
  });

  /* Masthead state -------------------------------------------------------- */

  const setHeaderState = () => {
    if (!header) return;
    header.classList.toggle("is-scrolled", window.scrollY > 12);
  };

  setHeaderState();
  window.addEventListener("scroll", setHeaderState, { passive: true });

  /* Mobile navigation ----------------------------------------------------- */

  const closeNav = () => {
    if (!navToggle || !primaryNav) return;
    navToggle.setAttribute("aria-expanded", "false");
    primaryNav.classList.remove("is-open");
    const label = navToggle.querySelector(".sr-only");
    if (label) label.textContent = "Menu";
  };

  if (navToggle && primaryNav) {
    navToggle.addEventListener("click", () => {
      const isOpen = navToggle.getAttribute("aria-expanded") === "true";
      navToggle.setAttribute("aria-expanded", String(!isOpen));
      primaryNav.classList.toggle("is-open", !isOpen);
      const label = navToggle.querySelector(".sr-only");
      if (label) label.textContent = isOpen ? "Menu" : "Close menu";
    });

    primaryNav.querySelectorAll("a").forEach((link) => {
      link.addEventListener("click", closeNav);
    });

    document.addEventListener("click", (event) => {
      if (navToggle.getAttribute("aria-expanded") !== "true") return;
      if (!header || header.contains(event.target)) return;
      closeNav();
    });

    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape") closeNav();
    });

    window.addEventListener("resize", () => {
      if (window.innerWidth > 920) closeNav();
    });
  }

  /* Feature tabs ---------------------------------------------------------- */

  const tabs = Array.from(document.querySelectorAll(".tabs [role='tab']"));
  const panels = Array.from(document.querySelectorAll(".feature-panel"));
  const shots = Array.from(document.querySelectorAll(".stage-img"));
  const caption = document.querySelector("#stage-caption");

  const captions = {
    comps: "Game plan, with the core cards you own",
    hero: "Hero pick, adjusted for this lobby",
    shop: "Shop scores on a real board",
    discover: "Dark Gift options, ranked",
  };

  const activateFeature = (target, moveFocus = false) => {
    if (!target) return;

    tabs.forEach((tab) => {
      const selected = tab.dataset.target === target;
      tab.setAttribute("aria-selected", String(selected));
      tab.tabIndex = selected ? 0 : -1;
      if (selected && moveFocus) tab.focus();
    });

    panels.forEach((panel) => {
      panel.hidden = panel.id !== `panel-${target}`;
    });

    shots.forEach((shot) => {
      shot.classList.toggle("is-active", shot.dataset.feature === target);
    });

    if (caption) caption.textContent = captions[target] || "";
  };

  tabs.forEach((tab, index) => {
    tab.addEventListener("click", () => activateFeature(tab.dataset.target));

    tab.addEventListener("keydown", (event) => {
      let nextIndex = null;

      if (event.key === "ArrowDown" || event.key === "ArrowRight") {
        nextIndex = (index + 1) % tabs.length;
      } else if (event.key === "ArrowUp" || event.key === "ArrowLeft") {
        nextIndex = (index - 1 + tabs.length) % tabs.length;
      } else if (event.key === "Home") {
        nextIndex = 0;
      } else if (event.key === "End") {
        nextIndex = tabs.length - 1;
      }

      if (nextIndex === null) return;
      event.preventDefault();
      activateFeature(tabs[nextIndex].dataset.target, true);
    });
  });

  /* Live pool figures ----------------------------------------------------- */

  const pool = "https://hearthlens-pool.nebula-auth-relay.workers.dev";
  const numberFormat = new Intl.NumberFormat("en-US");
  const shortDate = new Intl.DateTimeFormat("en-GB", {
    day: "2-digit",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone: "UTC",
  });

  const setStat = (name, value) => {
    document.querySelectorAll(`[data-stat="${name}"]`).forEach((node) => {
      node.textContent = value;
    });
  };

  const getJson = async (url, signal) => {
    const response = await fetch(url, {
      signal,
      cache: "no-store",
      headers: { Accept: "application/json" },
    });
    if (!response.ok) throw new Error(`Pool request failed: ${response.status}`);
    return response.json();
  };

  const loadPool = async () => {
    const controller = new AbortController();
    const timer = window.setTimeout(() => controller.abort(), 8000);

    try {
      const [metaResult, statsResult] = await Promise.allSettled([
        getJson(`${pool}/v1/meta`, controller.signal),
        getJson(`${pool}/v1/stats?mode=solo`, controller.signal),
      ]);

      if (metaResult.status === "fulfilled") {
        const meta = metaResult.value;
        const heroRows = Array.isArray(meta.heroes?.["100"]) ? meta.heroes["100"] : [];
        const heroGames = heroRows.reduce((total, row) => {
          const games = Number(Array.isArray(row) ? row[1] : 0);
          return total + (Number.isFinite(games) ? games : 0);
        }, 0);

        const counts = {
          heroes: numberFormat.format(heroRows.length || 0),
          comps: numberFormat.format(Array.isArray(meta.comps) ? meta.comps.length : 0),
          trinkets: numberFormat.format(Array.isArray(meta.trinkets) ? meta.trinkets.length : 0),
          games: numberFormat.format(heroGames),
        };

        Object.entries(counts).forEach(([name, value]) => {
          setStat(name, value);
        });

        const updated = new Date(meta.updated);
        if (!Number.isNaN(updated.valueOf())) {
          setStat("updated", `Pool updated ${shortDate.format(updated)} UTC`);
        }
      }

      if (statsResult.status === "fulfilled") {
        const games = Number(statsResult.value.games);
        if (Number.isFinite(games)) {
          setStat("shared", `shared games in this window: ${numberFormat.format(games)}`);
        }
      }
    } catch {
      /* The printed snapshot in the markup is the fallback. */
    } finally {
      window.clearTimeout(timer);
    }
  };

  loadPool();

  const year = document.querySelector("#year");
  if (year) year.textContent = String(new Date().getFullYear());
})();

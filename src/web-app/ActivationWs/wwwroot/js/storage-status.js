(() => {
    "use strict";

    const banner = document.getElementById("storageBanner");
    if (!banner) {
        return;
    }

    // Poll the health endpoint so the banner appears (and clears) without a page reload. A transient
    // fetch failure is ignored rather than flipping the banner, because if the server were truly down
    // the page itself would not be served.
    async function check() {
        try {
            const response = await fetch("/health", {
                headers: { "Accept": "application/json" },
                cache: "no-store"
            });
            banner.hidden = response.ok;
        } catch {
            // Network hiccup; keep the current banner state.
        }
    }

    check();
    setInterval(check, 30000);
})();

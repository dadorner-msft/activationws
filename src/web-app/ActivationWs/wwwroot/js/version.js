(() => {
    "use strict";

    const target = document.getElementById("appVersion");
    if (!target) {
        return;
    }

    // The footer stays usable when the version cannot be read, so failures are silent.
    fetch("/api/v1/version", { headers: { "Accept": "application/json" } })
        .then((response) => (response.ok ? response.json() : null))
        .then((data) => {
            if (data && data.version) {
                target.textContent = `Version ${data.version}`;
                target.hidden = false;
            }
        })
        .catch(() => { });
})();

(() => {
    "use strict";

    const confirmationIdRadio = document.getElementById("actionConfirmationId");
    const activationCountRadio = document.getElementById("actionActivationCount");
    const confirmationIdForm = document.getElementById("confirmationIdForm");
    const activationCountForm = document.getElementById("activationCountForm");
    const result = document.getElementById("result");

    function toggleForms() {
        const wantsConfirmationId = confirmationIdRadio.checked;
        confirmationIdForm.hidden = !wantsConfirmationId;
        activationCountForm.hidden = wantsConfirmationId;
        clearResult();
    }

    function clearResult() {
        result.hidden = true;
        result.replaceChildren();
        result.className = "result";
    }

    function showPending(message) {
        result.hidden = false;
        result.className = "result result--pending";

        const spinner = document.createElement("span");
        spinner.className = "spinner";
        result.replaceChildren(spinner, document.createTextNode(message));
    }

    function showSuccess(label, value, note) {
        result.hidden = false;
        result.className = "result result--success";

        const caption = document.createElement("strong");
        caption.textContent = label;

        const code = document.createElement("code");
        code.className = "result__value";
        code.textContent = value;

        result.replaceChildren(caption, code);

        if (note) {
            const hint = document.createElement("span");
            hint.className = "result__note";
            hint.textContent = note;
            result.append(hint);
        }
    }

    function showError(title, details) {
        result.hidden = false;
        result.className = "result result--error";

        const caption = document.createElement("strong");
        caption.textContent = title;
        result.replaceChildren(caption);

        if (details.length === 1) {
            result.append(document.createElement("br"), document.createTextNode(details[0]));
        } else if (details.length > 1) {
            const list = document.createElement("ul");
            list.className = "result__errors";
            for (const detail of details) {
                const item = document.createElement("li");
                item.textContent = detail;
                list.append(item);
            }
            result.append(list);
        }
    }

    /** Turns an RFC 9457 problem details payload into a title plus a list of messages. */
    async function describeFailure(response) {
        let payload = null;
        try {
            payload = await response.json();
        } catch {
            // Body was empty or not JSON.
        }

        if (!payload || typeof payload !== "object") {
            return [`Request failed (HTTP ${response.status})`, []];
        }

        const title = payload.title || `Request failed (HTTP ${response.status})`;
        const details = [];

        if (payload.errors && typeof payload.errors === "object") {
            for (const messages of Object.values(payload.errors)) {
                details.push(...(Array.isArray(messages) ? messages : [messages]));
            }
        }

        if (details.length === 0 && payload.detail) {
            details.push(payload.detail);
        }

        return [title, details];
    }

    async function submit(form, url, body, pendingMessage, onSuccess) {
        if (!form.reportValidity()) {
            return;
        }

        const button = form.querySelector("button[type=submit]");
        button.disabled = true;
        showPending(pendingMessage);

        try {
            const response = await fetch(url, {
                method: "POST",
                headers: { "Content-Type": "application/json", "Accept": "application/json" },
                body: JSON.stringify(body)
            });

            if (!response.ok) {
                const [title, details] = await describeFailure(response);
                showError(title, details);
                return;
            }

            onSuccess(await response.json());
        } catch {
            showError("The activation service could not be reached.", [
                "Check your network connection and try again."
            ]);
        } finally {
            button.disabled = false;
        }
    }

    confirmationIdRadio.addEventListener("change", toggleForms);
    activationCountRadio.addEventListener("change", toggleForms);

    confirmationIdForm.addEventListener("submit", (event) => {
        event.preventDefault();

        submit(
            confirmationIdForm,
            "/api/v1/activationservice/confirmation-id",
            {
                installationId: document.getElementById("installationId").value.trim(),
                extendedProductId: document.getElementById("extendedProductId").value.trim(),
                hostName: document.getElementById("hostName").value.trim()
            },
            "Requesting a Confirmation ID\u2026",
            (data) => showSuccess(
                "Confirmation ID",
                data.confirmationId,
                data.fromCache
                    ? "Served from cache."
                    : null)
        );
    });

    activationCountForm.addEventListener("submit", (event) => {
        event.preventDefault();

        submit(
            activationCountForm,
            "/api/v1/activationservice/count",
            { extendedProductId: document.getElementById("countExtendedProductId").value.trim() },
            "Retrieving the activation count\u2026",
            (data) => showSuccess(
                "Remaining activations",
                new Intl.NumberFormat().format(data.remainingActivations))
        );
    });

    toggleForms();
})();

(() => {
    "use strict";

    const PAGE_SIZE = 20;

    const searchForm = document.getElementById("searchForm");
    const hostSearch = document.getElementById("hostSearch");
    const resetSearch = document.getElementById("resetSearch");
    const exportCsv = document.getElementById("exportCsv");
    const status = document.getElementById("status");
    const table = document.getElementById("recordsTable");
    const tableCaption = document.getElementById("tableCaption");
    const body = document.getElementById("recordsBody");
    const previousPage = document.getElementById("previousPage");
    const nextPage = document.getElementById("nextPage");
    const pageInfo = document.getElementById("pageInfo");

    const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: "short", timeStyle: "medium" });
    const numberFormat = new Intl.NumberFormat();

    let state = { host: "", page: 1 };
    let requestId = 0;

    function showStatus(message, kind) {
        status.hidden = false;
        status.className = `result result--${kind}`;

        if (kind === "pending") {
            const spinner = document.createElement("span");
            spinner.className = "spinner";
            status.replaceChildren(spinner, document.createTextNode(message));
        } else {
            status.textContent = message;
        }
    }

    function clearStatus() {
        status.hidden = true;
        status.replaceChildren();
        status.className = "result";
    }

    function formatTimestamp(value) {
        const parsed = new Date(value);
        return Number.isNaN(parsed.valueOf()) ? value : dateFormat.format(parsed);
    }

    function addCell(row, text, className) {
        const cell = document.createElement("td");
        if (className) {
            cell.className = className;
        }
        cell.textContent = text;
        // The identifiers are long, so the untruncated value stays available on hover.
        cell.title = text;
        return row.appendChild(cell);
    }

    function renderRows(items) {
        body.replaceChildren();

        for (const item of items) {
            const row = document.createElement("tr");
            addCell(row, item.hostName);
            addCell(row, item.installationId, "table__mono");
            addCell(row, item.extendedProductId, "table__mono");
            addCell(row, item.confirmationId, "table__mono");
            addCell(row, numberFormat.format(item.requestCount), "table__number");
            addCell(row, formatTimestamp(item.createdAtUtc), "table__nowrap");
            addCell(row, formatTimestamp(item.lastRequestedAtUtc), "table__nowrap");
            body.append(row);
        }
    }

    // The export covers every matching record, not just the page on screen.
    function updateExport(totalCount) {
        exportCsv.hidden = totalCount === 0;
        if (totalCount === 0) {
            return;
        }

        const query = new URLSearchParams();
        if (state.host) {
            query.set("host", state.host);
        }

        const search = query.toString();
        exportCsv.href = `/api/v1/activationservice/records.csv${search ? `?${search}` : ""}`;

        const count = `${numberFormat.format(totalCount)} activation${totalCount === 1 ? "" : "s"}`;
        exportCsv.title = state.host
            ? `Downloads all ${count} for hosts containing "${state.host}" as CSV.`
            : `Downloads all ${count} as CSV.`;
    }

    function renderEmpty(host) {
        table.hidden = true;
        pageInfo.textContent = "";
        previousPage.disabled = true;
        nextPage.disabled = true;

        showStatus(
            host
                ? `No activation records found.`
                : "No activations have been recorded yet.",
            "empty");
    }

    async function describeFailure(response) {
        try {
            const payload = await response.json();
            if (payload && typeof payload === "object" && payload.title) {
                return payload.title;
            }
        } catch {
            // Body was empty or not JSON.
        }

        return `The report could not be loaded (HTTP ${response.status}).`;
    }

    async function load() {
        const current = ++requestId;

        showStatus("Loading activations\u2026", "pending");
        previousPage.disabled = true;
        nextPage.disabled = true;
        exportCsv.hidden = true;

        const query = new URLSearchParams({ page: String(state.page), pageSize: String(PAGE_SIZE) });
        if (state.host) {
            query.set("host", state.host);
        }

        try {
            const response = await fetch(`/api/v1/activationservice/records?${query}`, {
                headers: { "Accept": "application/json" }
            });

            if (current !== requestId) {
                return;
            }

            if (!response.ok) {
                table.hidden = true;
                previousPage.disabled = true;
                nextPage.disabled = true;
                exportCsv.hidden = true;

                // 503 is raised specifically when the activation database cannot be reached.
                showStatus(
                    response.status === 503
                        ? "The database is currently unavailable. Please try again later."
                        : await describeFailure(response),
                    "error");
                return;
            }

            const data = await response.json();
            if (current !== requestId) {
                return;
            }

            // A deleted or filtered-away page can leave the view past the end, so step back once.
            if (data.items.length === 0 && data.page > 1 && data.totalCount > 0) {
                state.page = data.totalPages;
                syncUrl();
                await load();
                return;
            }

            if (data.totalCount === 0) {
                renderEmpty(state.host);
                return;
            }

            clearStatus();
            table.hidden = false;
            renderRows(data.items);
            updateExport(data.totalCount);

            const first = (data.page - 1) * data.pageSize + 1;
            const last = first + data.items.length - 1;

            tableCaption.textContent = state.host
                ? `Activations for hosts containing "${state.host}"`
                : "All activations";
            pageInfo.textContent =
                `${numberFormat.format(first)}\u2013${numberFormat.format(last)} of ` +
                `${numberFormat.format(data.totalCount)} \u00b7 page ${numberFormat.format(data.page)} ` +
                `of ${numberFormat.format(data.totalPages)}`;

            previousPage.disabled = data.page <= 1;
            nextPage.disabled = data.page >= data.totalPages;
        } catch {
            if (current !== requestId) {
                return;
            }

            table.hidden = true;
            showStatus("The reporting service could not be reached.", "error");
        }
    }

    function syncUrl() {
        const query = new URLSearchParams();
        if (state.host) {
            query.set("host", state.host);
        }
        if (state.page > 1) {
            query.set("page", String(state.page));
        }

        const search = query.toString();
        history.replaceState(null, "", search ? `?${search}` : location.pathname);
    }

    function readUrl() {
        const query = new URLSearchParams(location.search);
        const page = Number.parseInt(query.get("page") ?? "1", 10);

        state = {
            host: query.get("host") ?? "",
            page: Number.isFinite(page) && page > 0 ? page : 1
        };

        hostSearch.value = state.host;
    }

    searchForm.addEventListener("submit", (event) => {
        event.preventDefault();
        state = { host: hostSearch.value.trim(), page: 1 };
        syncUrl();
        load();
    });

    resetSearch.addEventListener("click", () => {
        hostSearch.value = "";
        state = { host: "", page: 1 };
        syncUrl();
        load();
    });

    previousPage.addEventListener("click", () => {
        if (state.page > 1) {
            state.page -= 1;
            syncUrl();
            load();
        }
    });

    nextPage.addEventListener("click", () => {
        state.page += 1;
        syncUrl();
        load();
    });

    readUrl();
    load();
})();

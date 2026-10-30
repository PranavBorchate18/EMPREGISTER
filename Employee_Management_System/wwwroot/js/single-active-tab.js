
(() => {
    "use strict";

    const LOGIN_URL = "/Account/Login";
    const CLAIM_URL = "/Account/ClaimTab";
    const CHECK_URL = "/Account/CheckTab";

    const STORAGE_KEY = "ems-active-tab-id";
    const TRANSFER_STORAGE_KEY = "ems-tab-transfer-event";
    const CHANNEL_NAME = "ems-tab-presence";

    const CHECK_INTERVAL = 2000;
    const DUPLICATE_WAIT = 1200;

    const csrfToken = document.querySelector(
        'meta[name="tab-antiforgery-token"]'
    )?.content;

    if (!csrfToken) {
        console.error(
            "Antiforgery token missing. Check _Layout.cshtml."
        );
        return;
    }

    const instanceId = createId();
    const startedAt = Date.now() + Math.random();

    let tabId = sessionStorage.getItem(STORAGE_KEY);
    let isChecking = false;
    let isRedirecting = false;
    let channel = null;
    let checkTimer = null;
    let duplicateDetected = false;

    if (!tabId) {
        tabId = createId();
        sessionStorage.setItem(STORAGE_KEY, tabId);
    }

    function createId() {
        if (window.crypto?.randomUUID) {
            return window.crypto.randomUUID();
        }

        return "tab-" +
            Date.now().toString(36) +
            "-" +
            Math.random().toString(36).slice(2);
    }

    function currentPath() {
        return window.location.pathname.toLowerCase();
    }

    function isExcludedPage() {
        const path = currentPath();

        return path.endsWith("/account/login") ||
            path.endsWith("/account/logout") ||
            path.endsWith("/home/error");
    }

    function stopMonitoring() {
        if (checkTimer !== null) {
            clearInterval(checkTimer);
            checkTimer = null;
        }

        if (channel) {
            channel.close();
            channel = null;
        }
    }

    function redirectToLogin(reason) {
        if (isRedirecting || isExcludedPage()) {
            return;
        }

        isRedirecting = true;
        stopMonitoring();

        const url = new URL(
            LOGIN_URL,
            window.location.origin
        );

        if (reason === "duplicate") {
            url.searchParams.set("tabEntry", "true");
        } else {
            url.searchParams.set("tabLost", "true");
        }

        window.location.replace(url.toString());
    }

    function setNewTabId() {
        tabId = createId();
        sessionStorage.setItem(STORAGE_KEY, tabId);
    }

    function setupBroadcastChannel() {
        if (!("BroadcastChannel" in window)) {
            return;
        }

        if (channel) {
            channel.close();
        }

        channel = new BroadcastChannel(CHANNEL_NAME);

        channel.addEventListener("message", event => {
            const message = event.data;

            if (!message || message.instanceId === instanceId) {
                return;
            }

            // Another tab has taken over this tab's session.
            if (
                message.type === "tab-transfer-complete" &&
                message.oldTabId === tabId
            ) {
                redirectToLogin("lost");
                return;
            }

            // A newer tab announces the same tab ID.
            if (
                message.type === "hello" &&
                message.tabId === tabId &&
                message.startedAt > startedAt
            ) {
                channel?.postMessage({
                    type: "present",
                    tabId: tabId,
                    instanceId: instanceId,
                    targetInstanceId: message.instanceId,
                    startedAt: startedAt
                });
            }

            // Detect a duplicate tab instance.
            if (
                message.type === "present" &&
                message.targetInstanceId === instanceId &&
                message.tabId === tabId &&
                message.startedAt < startedAt
            ) {
                if (duplicateDetected || isRedirecting) {
                    return;
                }

                duplicateDetected = true;

                // Give the duplicate its own ID before redirecting.
                setNewTabId();
                redirectToLogin("duplicate");
            }
        });
    }

    function setupTransferStorageListener() {
        window.addEventListener("storage", event => {
            if (
                event.key !== TRANSFER_STORAGE_KEY ||
                !event.newValue ||
                isRedirecting
            ) {
                return;
            }

            try {
                const transfer = JSON.parse(event.newValue);

                if (transfer.oldTabId === tabId) {
                    redirectToLogin("lost");
                }
            } catch (error) {
                console.error(
                    "Invalid tab transfer event:",
                    error
                );
            }
        });
    }

    function announceTab() {
        if (!channel) {
            return;
        }

        channel.postMessage({
            type: "hello",
            tabId: tabId,
            instanceId: instanceId,
            startedAt: startedAt
        });
    }

    async function postForm(url, data) {
        const body = new URLSearchParams();

        Object.entries(data).forEach(([key, value]) => {
            body.set(key, value ?? "");
        });

        const response = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            cache: "no-store",
            headers: {
                "Content-Type":
                    "application/x-www-form-urlencoded; charset=UTF-8",
                "RequestVerificationToken": csrfToken,
                "X-Requested-With": "XMLHttpRequest"
            },
            body: body.toString()
        });

        if (
            response.status === 401 ||
            response.status === 403
        ) {
            redirectToLogin("lost");
            return null;
        }

        if (!response.ok) {
            throw new Error(
                url + " returned HTTP " + response.status
            );
        }

        return await response.json();
    }

    async function claimTab() {
        if (
            isChecking ||
            isRedirecting ||
            isExcludedPage() ||
            duplicateDetected
        ) {
            return;
        }

        isChecking = true;

        try {
            const result = await postForm(CLAIM_URL, {
                tabId: tabId
            });

            if (isRedirecting || result === null) {
                return;
            }

            if (result.active === true) {
                return;
            }

            redirectToLogin("duplicate");
        } catch (error) {
            console.error("ClaimTab error:", error);
        } finally {
            isChecking = false;
        }
    }

    async function checkTab() {
        if (
            isChecking ||
            isRedirecting ||
            isExcludedPage() ||
            duplicateDetected
        ) {
            return;
        }

        isChecking = true;

        try {
            const result = await postForm(CHECK_URL, {
                tabId: tabId,
                page: window.location.pathname
            });

            if (isRedirecting || result === null) {
                return;
            }

            if (result.active === false) {
                redirectToLogin("lost");
            }
        } catch (error) {
            console.error("CheckTab error:", error);
        } finally {
            isChecking = false;
        }
    }

    async function start() {
        if (isExcludedPage() || isRedirecting) {
            return;
        }

        setupBroadcastChannel();
        setupTransferStorageListener();
        announceTab();

        await new Promise(resolve => {
            setTimeout(resolve, DUPLICATE_WAIT);
        });

        if (isRedirecting || duplicateDetected) {
            return;
        }

        await claimTab();

        if (isRedirecting || duplicateDetected) {
            return;
        }

        if (checkTimer !== null) {
            clearInterval(checkTimer);
        }

        checkTimer = setInterval(
            checkTab,
            CHECK_INTERVAL
        );
    }

    window.addEventListener("pageshow", event => {
        if (event.persisted && !isExcludedPage()) {
            setupBroadcastChannel();
            setupTransferStorageListener();
            announceTab();
            checkTab();
        }
    });

    window.addEventListener("pagehide", () => {
        stopMonitoring();
    });

    start();
})();

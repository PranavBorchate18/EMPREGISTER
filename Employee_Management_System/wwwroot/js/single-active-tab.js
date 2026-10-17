
(() => {
    "use strict";

    const csrf = document.querySelector(
        'meta[name="tab-antiforgery-token"]'
    )?.content;

    if (!csrf) {
        console.error(
            "Single Active Tab: CSRF token missing in _Layout.cshtml"
        );
        return;
    }

    const tabKey = "ems-active-tab-id";
    const registeredKey = "ems-active-tab-registered";

    const channel = ("BroadcastChannel" in window)
        ? new BroadcastChannel("ems-tab-presence")
        : null;

    let tabId = sessionStorage.getItem(tabKey);
    let registered =
        sessionStorage.getItem(registeredKey) === "yes";

    let stopped = false;
    let checking = false;

    if (!tabId) {
        tabId = crypto.randomUUID();
        sessionStorage.setItem(tabKey, tabId);
        registered = false;
    }

    function makeInactive() {
        if (stopped) return;

        stopped = true;

        window.location.replace("/Account/TabInactive");
    }

    async function sendRequest(url) {
        const data = new URLSearchParams({
            tabId: tabId
        });

        const response = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            cache: "no-store",
            headers: {
                "Content-Type":
                    "application/x-www-form-urlencoded",
                "RequestVerificationToken": csrf,
                "X-Requested-With": "XMLHttpRequest"
            },
            body: data
        });

        if (!response.ok) {
            if (response.status === 401 ||
                response.status === 403) {
                makeInactive();
            }

            throw new Error(
                "Tab request failed: " + response.status
            );
        }

        return await response.json();
    }

    async function claimTab() {
        try {
            const result = await sendRequest(
                "/Account/ClaimTab"
            );

            if (result.active) {
                registered = true;

                sessionStorage.setItem(
                    registeredKey,
                    "yes"
                );
            } else {
                makeInactive();
            }
        } catch (error) {
            console.warn(
                "Unable to claim active tab:",
                error
            );
        }
    }

    async function verifyTab() {
        if (stopped || checking) return;

        checking = true;

        try {
            const result = await sendRequest(
                "/Account/CheckTab"
            );

            if (result.unclaimed && !registered) {
                await claimTab();
            } else if (!result.active) {
                makeInactive();
            } else {
                registered = true;

                sessionStorage.setItem(
                    registeredKey,
                    "yes"
                );
            }
        } catch (error) {
            console.warn(
                "Tab verification failed:",
                error
            );
        } finally {
            checking = false;
        }
    }

    // Detect duplicated tabs that share sessionStorage.
    let duplicate = false;

    if (channel) {
        channel.onmessage = function (event) {
            const message = event.data;

            if (!message ||
                message.tabId !== tabId) {
                return;
            }

            if (message.type === "hello") {
                channel.postMessage({
                    type: "present",
                    tabId: tabId
                });
            }

            if (message.type === "present") {
                duplicate = true;
            }
        };

        channel.postMessage({
            type: "hello",
            tabId: tabId
        });
    }

    setTimeout(async function () {
        if (duplicate) {
            tabId = crypto.randomUUID();

            sessionStorage.setItem(
                tabKey,
                tabId
            );

            sessionStorage.removeItem(
                registeredKey
            );

            registered = false;
        }

        if (!registered) {
            await claimTab();
        } else {
            await verifyTab();
        }
    }, 200);

    // Check active tab every 3 seconds.
    setInterval(verifyTab, 3000);

    // Check when user returns to the tab.
    window.addEventListener("focus", verifyTab);

    document.addEventListener(
        "visibilitychange",
        function () {
            if (!document.hidden) {
                verifyTab();
            }
        }
    );
})();

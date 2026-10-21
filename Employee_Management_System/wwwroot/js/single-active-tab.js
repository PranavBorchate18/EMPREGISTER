
(() => {
    "use strict";

    const csrf = document.querySelector(
        'meta[name="tab-antiforgery-token"]'
    )?.content;

    if (!csrf) {
        console.error("Tab antiforgery token missing.");
        return;
    }

    const STORAGE_KEY = "ems-active-tab-id";
    const CHANNEL_NAME = "ems-tab-presence";

    function createId() {
        return crypto.randomUUID
            ? crypto.randomUUID()
            : Date.now().toString(36) +
            "-" + Math.random().toString(36).slice(2);
    }

    let tabId = sessionStorage.getItem(STORAGE_KEY);

    if (!tabId) {
        tabId = createId();
        sessionStorage.setItem(STORAGE_KEY, tabId);
    }

   
    const instanceId = createId();

    const channel = "BroadcastChannel" in window
        ? new BroadcastChannel(CHANNEL_NAME)
        : null;

    let stopped = false;
    let initialized = false;
    let checking = false;
    let checkTimer = null;

    function redirectToLogin(reason) {
        if (stopped) return;

        stopped = true;

        if (checkTimer !== null) {
            clearInterval(checkTimer);
            checkTimer = null;
        }

        const url = new URL(
            "/Account/Login",
            window.location.origin
        );

        url.searchParams.set(reason, "1");

        window.location.replace(url.toString());
    }

    // -------------------------------------------
    // Duplicate Tab Detection
    // -------------------------------------------

    if (channel) {
        channel.onmessage = function (event) {
            const message = event.data;

            if (!message ||
                message.tabId !== tabId ||
                message.instanceId === instanceId) {
                return;
            }

         
            if (message.type === "hello") {
                channel.postMessage({
                    type: "present",
                    tabId: tabId,
                    instanceId: instanceId,
                    peerId: message.instanceId
                });
            }

 
            if (
                message.type === "present" &&
                message.peerId === instanceId
            ) {
                redirectToLogin("tabEntry");
            }
        };

    
        channel.postMessage({
            type: "hello",
            tabId: tabId,
            instanceId: instanceId
        });
    }

    // -------------------------------------------
    // Server Request
    // -------------------------------------------

    async function post(endpoint) {
        const response = await fetch(
            "/Account/" + endpoint,
            {
                method: "POST",
                credentials: "same-origin",
                cache: "no-store",
                headers: {
                    "Content-Type":
                        "application/x-www-form-urlencoded",
                    "RequestVerificationToken": csrf,
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: new URLSearchParams({
                    tabId: tabId,
                    page: document.title
                }).toString()
            }
        );

        if (response.status === 401) {
            redirectToLogin("tabLost");
            return null;
        }

        if (!response.ok) {
            throw new Error(
                endpoint + " returned HTTP " + response.status
            );
        }

        return await response.json();
    }

    // -------------------------------------------
    // Claim current tab
    // -------------------------------------------

    async function claimTab() {
        try {
            const result = await post("ClaimTab");

            if (!result || stopped) return;

            if (!result.active) {
                redirectToLogin("tabEntry");
                return;
            }

            initialized = true;

            await checkTab();

            if (!stopped) {
                checkTimer = setInterval(checkTab, 3000);
            }
        } catch (error) {
            console.error("ClaimTab failed:", error);
        }
    }

    // -------------------------------------------
    // Check current tab
    // -------------------------------------------

    async function checkTab() {
        if (stopped || !initialized || checking) return;

        checking = true;

        try {
            const result = await post("CheckTab");

            if (!result || stopped) return;

            if (!result.active) {
                redirectToLogin(
                    result.unclaimed ? "tabEntry" : "tabLost"
                );
            }
        } catch (error) {
            console.error("CheckTab failed:", error);
        } finally {
            checking = false;
        }
    }


    setTimeout(function () {
        if (!stopped) {
            claimTab();
        }
    }, 1200);

    window.addEventListener("focus", checkTab);

    document.addEventListener("visibilitychange", function () {
        if (!document.hidden) {
            checkTab();
        }
    });

    document.addEventListener("submit", function (event) {
        if (stopped) {
            event.preventDefault();
            event.stopImmediatePropagation();
        }
    }, true);

    window.addEventListener("pagehide", function () {
        if (checkTimer !== null) {
            clearInterval(checkTimer);
        }

        if (channel) {
            channel.close();
        }
    });
})();

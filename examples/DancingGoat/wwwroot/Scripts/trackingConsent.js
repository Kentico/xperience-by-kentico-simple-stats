/*
 * Tracking consent: agrees in the background instead of the form post + redirect, so the
 * page keeps its URL (including campaign UTM parameters) and is not reloaded.
 *
 * The activity logging script already ran when the page loaded, before consent, so the
 * landing page and page visit were not logged. Once the visitor agrees, the product's own
 * logging script (rendered by ActivityLoggingScriptV2) is loaded again to log them for the
 * current URL.
 *
 * Without JavaScript, or when the background request fails, the form posts as before.
 */
(() => {
    "use strict";

    const AGREE_HEADER = "X-Consent-Agree";
    const LOGGING_SCRIPT_SELECTOR = "script[src*='/KenticoActivityLogger/LoggerV2.js']";

    const consent = document.querySelector("[data-tracking-consent]");
    const form = consent?.querySelector("[data-consent-form]");
    if (!form || form.dataset.consentFormInitialized) {
        return;
    }
    form.dataset.consentFormInitialized = "true";

    const relogActivities = () => {
        const loggingScript = document.querySelector(LOGGING_SCRIPT_SELECTOR);
        if (!loggingScript) {
            return;
        }

        const script = document.createElement("script");
        script.src = loggingScript.src;
        script.async = true;
        document.head.appendChild(script);
    };

    form.addEventListener("submit", (event) => {
        event.preventDefault();

        const submitButton = form.querySelector("[type='submit']");
        if (submitButton) {
            submitButton.disabled = true;
        }

        fetch(form.action, {
            method: "POST",
            headers: { [AGREE_HEADER]: "1" },
            body: new FormData(form)
        })
            .then((response) => {
                const contentType = (response.headers.get("Content-Type") || "").split(";")[0].trim();
                if (!response.ok || response.redirected || contentType !== "text/html") {
                    throw new Error("Consent could not be saved.");
                }
                return response.text();
            })
            .then((html) => {
                const parsed = new DOMParser().parseFromString(html, "text/html");
                const agreedBody = parsed.querySelector("[data-consent-body]");
                if (!agreedBody?.querySelector("[data-consent-agreed]")) {
                    throw new Error("Unexpected consent response.");
                }

                const body = consent.querySelector("[data-consent-body]");
                body.replaceChildren(...agreedBody.childNodes);
                body.focus();

                const status = consent.querySelector("[data-consent-status]");
                if (status) {
                    status.textContent = body.textContent.trim().replace(/\s+/g, " ");
                }

                relogActivities();
            })
            .catch(() => {
                form.submit();
            });
    });
})();

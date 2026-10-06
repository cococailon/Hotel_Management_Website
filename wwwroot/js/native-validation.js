(function (window, document) {
    "use strict";

    const state = window.__nativeFormValidationState || {
        bound: false
    };
    window.__nativeFormValidationState = state;

    function getForms() {
        return document.querySelectorAll('form[data-native-validation="true"]');
    }

    function getInput(form, name) {
        if (!name) return null;
        const escaped = window.CSS?.escape ? CSS.escape(name) : name.replace(/(["\\])/g, "\\$1");
        return form.querySelector(`[name="${escaped}"]:not([type="hidden"])`) || form.querySelector(`[name="${escaped}"]`);
    }

    function getMessageElement(form, input) {
        if (!input?.name) return null;
        const candidates = form.querySelectorAll("[data-valmsg-for]");
        for (const candidate of candidates) {
            if (candidate.getAttribute("data-valmsg-for") === input.name) {
                return candidate;
            }
        }
        return null;
    }

    function setError(form, input, message) {
        if (!input || input.disabled || input.type === "hidden") {
            return;
        }

        const messageElement = getMessageElement(form, input);
        input.classList.add("is-invalid");
        input.setAttribute("aria-invalid", "true");

        if (messageElement) {
            messageElement.textContent = message || "Giá trị không hợp lệ.";
            messageElement.classList.remove("field-validation-valid");
            messageElement.classList.add("field-validation-error");
        }
    }

    function clearError(form, input) {
        if (!input) return;

        const messageElement = getMessageElement(form, input);
        input.classList.remove("is-invalid");
        input.removeAttribute("aria-invalid");

        if (messageElement) {
            messageElement.textContent = "";
            messageElement.classList.remove("field-validation-error");
            messageElement.classList.add("field-validation-valid");
        }
    }

    function getValue(input) {
        if (!input) return "";
        if (input.type === "checkbox" || input.type === "radio") {
            return input.checked ? "true" : "false";
        }
        return String(input.value ?? "");
    }

    function isEmpty(input, value) {
        if (input.type === "checkbox" || input.type === "radio") {
            return !input.checked;
        }
        return value.trim() === "";
    }

    function parseNumber(value) {
        if (value === null || value === undefined || String(value).trim() === "") {
            return null;
        }
        const number = Number(value);
        return Number.isFinite(number) ? number : NaN;
    }

    function validateElement(form, input) {
        if (!(input instanceof HTMLElement)) {
            return true;
        }

        if (input.disabled || input.type === "hidden") {
            clearError(form, input);
            return true;
        }

        const dataset = input.dataset;
        const value = getValue(input);

        if (dataset.valRequired !== undefined && isEmpty(input, value)) {
            setError(form, input, dataset.valRequired);
            return false;
        }

        if (value.trim() === "") {
            clearError(form, input);
            return true;
        }

        if (dataset.valNumber !== undefined) {
            const parsed = parseNumber(value);
            if (Number.isNaN(parsed)) {
                setError(form, input, dataset.valNumber);
                return false;
            }
        }

        const minLength = dataset.valLengthMin;
        const maxLength = dataset.valLengthMax;
        if (minLength !== undefined || maxLength !== undefined) {
            const length = value.length;
            if (minLength !== undefined && length < Number(minLength)) {
                setError(form, input, dataset.valLength || `Giá trị phải có ít nhất ${minLength} ký tự.`);
                return false;
            }
            if (maxLength !== undefined && length > Number(maxLength)) {
                setError(form, input, dataset.valLength || `Giá trị không được vượt quá ${maxLength} ký tự.`);
                return false;
            }
        }

        if (dataset.valRangeMin !== undefined || dataset.valRangeMax !== undefined) {
            const number = parseNumber(value);
            if (Number.isNaN(number)) {
                setError(form, input, dataset.valNumber || dataset.valRange || "Giá trị phải là số hợp lệ.");
                return false;
            }

            const min = dataset.valRangeMin !== undefined ? Number(dataset.valRangeMin) : null;
            const max = dataset.valRangeMax !== undefined ? Number(dataset.valRangeMax) : null;

            if ((min !== null && number < min) || (max !== null && number > max)) {
                setError(form, input, dataset.valRange || "Giá trị nằm ngoài khoảng cho phép.");
                return false;
            }
        }

        if (dataset.valRegexPattern !== undefined) {
            try {
                const regex = new RegExp(dataset.valRegexPattern);
                if (!regex.test(value)) {
                    setError(form, input, dataset.valRegex || "Giá trị không đúng định dạng.");
                    return false;
                }
            }
            catch (error) {
                console.warn("[NativeValidation] Regex DataAnnotation không hợp lệ:", error);
            }
        }

        clearError(form, input);
        return true;
    }

    function getControls(form) {
        return Array.from(form.querySelectorAll("input, select, textarea"))
            .filter(input => input instanceof HTMLElement);
    }

    function focusControl(input) {
        if (!input || input.disabled || input.type === "hidden") {
            return;
        }

        try {
            input.focus({ preventScroll: true });
        }
        catch {
            input.focus();
        }

        window.setTimeout(() => {
            input.scrollIntoView({
                behavior: "smooth",
                block: "center"
            });
        }, 0);
    }

    function validateForm(form, options = {}) {
        if (!form) return true;

        const controls = getControls(form);
        const firstInvalid = { input: null };
        let valid = true;

        for (const input of controls) {
            if (!validateElement(form, input)) {
                valid = false;
                if (!firstInvalid.input) {
                    firstInvalid.input = input;
                }
            }
        }

        if (!valid && options.focusFirst !== false) {
            focusControl(firstInvalid.input);
        }

        return valid;
    }

    function focusExistingServerError(form) {
        const messageElements = Array.from(form.querySelectorAll("[data-valmsg-for]"));

        for (const messageElement of messageElements) {
            const message = messageElement.textContent?.trim() || "";
            if (!message) continue;

            const fieldName = messageElement.getAttribute("data-valmsg-for");
            const input = getInput(form, fieldName);
            if (!input || input.disabled || input.type === "hidden") continue;

            input.classList.add("is-invalid");
            input.setAttribute("aria-invalid", "true");
            messageElement.classList.remove("field-validation-valid");
            messageElement.classList.add("field-validation-error");
            focusControl(input);
            return;
        }
    }

    function initializeForms() {
        for (const form of getForms()) {
            window.setTimeout(() => focusExistingServerError(form), 0);
        }
    }

    window.NativeFormValidation = {
        validateElement,
        validateForm,
        clearError,
        focusControl,
        initializeForms
    };

    if (!state.bound) {
        state.bound = true;

        document.addEventListener("focusout", function (event) {
            const target = event.target instanceof HTMLElement ? event.target : null;
            const form = target?.closest('form[data-native-validation="true"]');
            if (!form || !target) return;
            validateElement(form, target);
        }, true);

        document.addEventListener("change", function (event) {
            const target = event.target instanceof HTMLElement ? event.target : null;
            const form = target?.closest('form[data-native-validation="true"]');
            if (!form || !target) return;
            validateElement(form, target);
        }, true);

        document.addEventListener("input", function (event) {
            const target = event.target instanceof HTMLElement ? event.target : null;
            const form = target?.closest('form[data-native-validation="true"]');
            if (!form || !target || !target.classList.contains("is-invalid")) return;
            validateElement(form, target);
        }, true);
    }

    initializeForms();
})(window, document);

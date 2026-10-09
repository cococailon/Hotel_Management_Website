(() => {
    "use strict";
    if (window.__serviceFormBound) return;
    window.__serviceFormBound = true;

    const CODE_PATTERN = /^[A-Za-z0-9]+$/;

    function getFormFromEventTarget(target) {
        return target instanceof HTMLElement
            ? target.closest("form[data-service-form]")
            : null;
    }

    function getServiceCodeInput(form) {
        return form?.querySelector("[name=ServiceCode]") || null;
    }

    function getMessageElement(form, input) {
        if (!form || !input?.name) return null;
        return Array.from(form.querySelectorAll("[data-valmsg-for]"))
            .find(x => x.getAttribute("data-valmsg-for") === input.name) || null;
    }

    function setServerError(form, input, message) {
        if (!form || !input) return;
        const messageElement = getMessageElement(form, input);
        input.dataset.serviceServerError = "true";
        input.dataset.serviceServerErrorValue = input.value ?? "";
        input.classList.add("is-invalid");
        input.setAttribute("aria-invalid", "true");
        if (messageElement) {
            messageElement.textContent = message;
            messageElement.classList.remove("field-validation-valid");
            messageElement.classList.add("field-validation-error");
        }
    }

    function restoreServerError(form, input) {
        if (input?.dataset.serviceServerError !== "true") return false;
        const currentValue = input.value ?? "";
        const originalValue = input.dataset.serviceServerErrorValue ?? "";
        if (currentValue !== originalValue) {
            delete input.dataset.serviceServerError;
            delete input.dataset.serviceServerErrorValue;
            return false;
        }
        const messageElement = getMessageElement(form, input);
        const message = messageElement?.textContent?.trim() || "";
        if (!message) return false;
        input.classList.add("is-invalid");
        input.setAttribute("aria-invalid", "true");
        messageElement.classList.remove("field-validation-valid");
        messageElement.classList.add("field-validation-error");
        return true;
    }

    function captureServerErrors(form) {
        for (const messageElement of form.querySelectorAll("[data-valmsg-for]")) {
            const message = messageElement.textContent?.trim() || "";
            if (!message) continue;
            const fieldName = messageElement.getAttribute("data-valmsg-for");
            const input = form.querySelector(`[name="${CSS.escape(fieldName)}"]`);
            if (!input || input.type === "hidden" || input.disabled) continue;
            setServerError(form, input, message);
        }
    }

    function validate(form) {
        if (window.NativeFormValidation?.validateForm) {
            return window.NativeFormValidation.validateForm(form, { focusFirst: true });
        }
        return form.checkValidity();
    }

    async function submitByAjax(form, button) {
        if (!window.HotelAjax || typeof window.HotelAjax.navigate !== "function") {
            HTMLFormElement.prototype.submit.call(form);
            return true;
        }
        window.HotelUI?.setButtonLoading(button, true);
        try {
            return await window.HotelAjax.navigate(form.action, {
                method: "POST",
                body: new FormData(form),
                historyMode: "replace",
                preserveScroll: false,
                showErrorToast: false
            });
        } catch (error) {
            console.error("[Service] Form submit failed:", error);
            window.HotelUI?.toast("error", "Có lỗi xảy ra", "Không thể hoàn tất thao tác. Vui lòng thử lại.");
            return false;
        } finally {
            window.HotelUI?.setButtonLoading(button, false);
        }
    }

    async function generateServiceCode(button) {
        if (!button) return;
        const form = button.closest("form[data-service-form='create']");
        const input = getServiceCodeInput(form);
        if (!form || !input) return;

        const original = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>Đang sinh mã...';
        try {
            const response = await fetch("/Services/GenerateCode", {
                method: "GET",
                headers: { "X-Requested-With": "XMLHttpRequest" },
                credentials: "same-origin",
                cache: "no-store"
            });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            const data = await response.json();
            if (!data?.code || !CODE_PATTERN.test(data.code)) throw new Error("Mã sinh không hợp lệ");

            input.value = data.code;
            delete input.dataset.serviceServerError;
            delete input.dataset.serviceServerErrorValue;
            window.NativeFormValidation?.clearError(form, input);
        } catch (error) {
            console.error("[Service] Generate code failed:", error);
            window.HotelUI?.toast("error", "Không thể tự sinh mã", "Vui lòng thử lại.");
        } finally {
            button.disabled = false;
            button.innerHTML = original;
        }
    }

    function captureAllServiceServerErrors(root = document) {
        const scope = root?.querySelectorAll
            ? root
            : document;

        for (const form of scope.querySelectorAll("form[data-service-form]")) {
            captureServerErrors(form);
        }
    }

    document.addEventListener("DOMContentLoaded", () => {
        captureAllServiceServerErrors(document);
    });

    document.addEventListener("hotel:ajax-loaded", event => {
        const main = event.detail?.main;
        if (!main) return;

        window.setTimeout(() => {
            captureAllServiceServerErrors(main);
        }, 0);
    });

    document.addEventListener("focusout", event => {
        const form = getFormFromEventTarget(event.target);
        if (!form) return;
        const input = event.target;
        window.setTimeout(() => {
            restoreServerError(form, input);
        }, 0);
    }, true);

    document.addEventListener("input", event => {
        const form = getFormFromEventTarget(event.target);
        if (!form) return;
        const input = event.target;
        if (input.dataset.serviceServerError === "true" && (input.value ?? "") !== (input.dataset.serviceServerErrorValue ?? "")) {
            delete input.dataset.serviceServerError;
            delete input.dataset.serviceServerErrorValue;
        }
    }, true);

    document.addEventListener("click", event => {
        const button = event.target instanceof HTMLElement
            ? event.target.closest("#generateServiceCodeButton")
            : null;
        if (!button) return;
        event.preventDefault();
        generateServiceCode(button);
    }, true);

    document.addEventListener("submit", event => {
        const form = event.target instanceof HTMLFormElement
            ? event.target.closest("form[data-service-form]")
            : null;
        if (!form) return;
        event.preventDefault();
        event.stopPropagation();

        const button = form.querySelector("button[type='submit']");
        if (button?.disabled) return;
        if (!validate(form)) return;

        const edit = form.dataset.serviceForm === "edit";
        const activeBookings = form.dataset.hasActiveBookings === "true";

        window.HotelUI?.showConfirm({
            title: edit ? "Xác nhận sửa dịch vụ" : "Xác nhận tạo dịch vụ",
            message: edit ? "Bạn có chắc muốn lưu các thay đổi của dịch vụ này không?" : "Bạn có chắc muốn tạo dịch vụ này không?",
            note: edit && activeBookings ? "" : "",
            confirmText: edit ? "Lưu thay đổi" : "Tạo dịch vụ",
            variant: "primary",
            icon: edit ? "bi-pencil-square" : "bi-cone-striped",
            onConfirm: async () => submitByAjax(form, button)
        });
    }, true);
})();

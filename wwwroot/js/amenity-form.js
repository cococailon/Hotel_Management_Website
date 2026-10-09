(() => {
    "use strict";

    if (window.__amenityFormBound) {
        return;
    }

    window.__amenityFormBound = true;

    function getForm(element) {
        return element?.closest("form[data-amenity-form]")
            || document.querySelector('form[data-amenity-form]');
    }

    function getField(form, name) {
        if (!form) return null;
        return form.querySelector(`[name="${name}"]:not([type="hidden"])`)
            || form.querySelector(`[name="${name}"]`);
    }

    function getMessageElement(form, name) {
        return Array.from(form?.querySelectorAll("[data-valmsg-for]") || [])
            .find(x => x.getAttribute("data-valmsg-for") === name) || null;
    }

    function captureServerError(form, name) {
        const input = getField(form, name);
        const messageElement = getMessageElement(form, name);
        if (!input || !messageElement) return;

        const message = messageElement.textContent?.trim() || "";
        if (!message) return;

        input.dataset.serverValidationValue = input.value;
        input.dataset.serverValidationMessage = message;
        input.classList.add("is-invalid");
        input.setAttribute("aria-invalid", "true");
        messageElement.classList.remove("field-validation-valid");
        messageElement.classList.add("field-validation-error");
    }

    function restoreServerError(form, name) {
        const input = getField(form, name);
        const messageElement = getMessageElement(form, name);
        if (!input || !messageElement) return;

        const originalValue = input.dataset.serverValidationValue;
        const message = input.dataset.serverValidationMessage;
        if (originalValue === undefined || !message) return;

        if (input.value !== originalValue) return;

        input.classList.add("is-invalid");
        input.setAttribute("aria-invalid", "true");
        messageElement.textContent = message;
        messageElement.classList.remove("field-validation-valid");
        messageElement.classList.add("field-validation-error");
    }

    function clearServerError(form, name) {
        const input = getField(form, name);
        if (!input) return;

        delete input.dataset.serverValidationValue;
        delete input.dataset.serverValidationMessage;
    }

    function getCurrentFormMode() {
        return document.querySelector('form[data-amenity-form]');
    }

    function validate(form) {
        if (!window.NativeFormValidation?.validateForm) {
            return form.checkValidity();
        }

        return window.NativeFormValidation.validateForm(
            form,
            { focusFirst: true }
        );
    }

    async function submitByAjax(form, submitButton) {
        if (!window.HotelAjax || typeof window.HotelAjax.navigate !== "function") {
            HTMLFormElement.prototype.submit.call(form);
            return true;
        }

        window.HotelUI?.setButtonLoading(submitButton, true);

        try {
            const success = await window.HotelAjax.navigate(
                form.action,
                {
                    method: "POST",
                    body: new FormData(form),
                    historyMode: "replace",
                    preserveScroll: false,
                    showErrorToast: false
                });

            if (!success) {
                window.HotelUI?.toast(
                    "error",
                    "Có lỗi xảy ra",
                    "Không thể hoàn tất thao tác. Vui lòng thử lại."
                );
            }

            return success;
        }
        catch (error) {
            console.error("[Amenity] Form submit failed:", error);
            window.HotelUI?.toast(
                "error",
                "Có lỗi xảy ra",
                "Không thể hoàn tất thao tác. Vui lòng thử lại."
            );
            return false;
        }
        finally {
            window.HotelUI?.setButtonLoading(submitButton, false);
        }
    }

    async function generateAmenityCode(form) {
        const input = getField(form, "AmenityCode");
        const button = document.getElementById("generateAmenityCodeButton");
        if (!input || !button || form.dataset.amenityForm !== "create") return;

        const originalHtml = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>Đang sinh...';

        try {
            const endpoint = button.dataset.endpoint;
            if (!endpoint) throw new Error("Thiếu endpoint tự sinh mã tiện nghi.");

            const response = await fetch(endpoint, {
                method: "GET",
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    "Accept": "application/json"
                },
                credentials: "same-origin"
            });

            const payload = await response.json().catch(() => null);
            if (!response.ok || !payload?.code) {
                throw new Error(payload?.message || "Không thể tự sinh mã tiện nghi.");
            }

            input.value = payload.code;
            clearServerError(form, "AmenityCode");
            input.dispatchEvent(new Event("input", { bubbles: true }));
            input.dispatchEvent(new Event("change", { bubbles: true }));
            window.NativeFormValidation?.validateElement(form, input);
        }
        catch (error) {
            window.HotelUI?.toast(
                "error",
                "Không thể tự sinh mã tiện nghi",
                error?.message || "Vui lòng thử lại."
            );
        }
        finally {
            button.disabled = false;
            button.innerHTML = originalHtml;
        }
    }

    function bindServerErrorPersistence(form) {
        if (!form || form.dataset.serverErrorPersistenceBound === "true") return;
        form.dataset.serverErrorPersistenceBound = "true";

        captureServerError(form, "AmenityCode");

        const codeInput = getField(form, "AmenityCode");
        if (codeInput) {
            codeInput.addEventListener("input", () => {
                if (codeInput.dataset.serverValidationValue !== undefined &&
                    codeInput.value !== codeInput.dataset.serverValidationValue) {
                    clearServerError(form, "AmenityCode");
                }
            });

            // NativeValidation chạy focusout ở document capture; listener capture ở field
            // khôi phục lỗi server sau lượt validation chung.
            codeInput.addEventListener("focusout", () => {
                restoreServerError(form, "AmenityCode");
            }, true);
        }
    }

    function initializeForm(form) {
        bindServerErrorPersistence(form);

        const generateButton = document.getElementById("generateAmenityCodeButton");
        if (generateButton) {
            generateButton.addEventListener("click", () => {
                generateAmenityCode(form);
            });
        }
    }

    document.addEventListener("submit", event => {
        const form = event.target instanceof HTMLFormElement
            ? event.target.closest("form[data-amenity-form]")
            : null;

        if (!form) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();

        const submitButton = form.querySelector("[data-ui-submit], button[type='submit']");
        if (submitButton?.disabled) {
            return;
        }

        if (!validate(form)) {
            return;
        }

        const mode = form.dataset.amenityForm;
        const isEdit = mode === "edit";
        const activeBookings = form.dataset.hasActiveBookings === "true";

        const title = isEdit
            ? "Xác nhận sửa tiện nghi"
            : "Xác nhận tạo tiện nghi";

        const message = isEdit
            ? "Bạn có chắc muốn lưu các thay đổi của tiện nghi này không?"
            : "Bạn có chắc muốn tạo tiện nghi này không?";

        const note = isEdit && activeBookings
            ? "Tiện nghi đang thuộc loại phòng có Booking hoạt động. Mã và tên tiện nghi sẽ được giữ nguyên."
            : "";

        window.HotelUI?.showConfirm({
            title,
            message,
            note,
            confirmText: isEdit ? "Lưu thay đổi" : "Tạo tiện nghi",
            variant: "primary",
            icon: isEdit ? "bi-pencil-square" : "bi-stars",
            onConfirm: async () => {
                await submitByAjax(form, submitButton);
            }
        });
    }, true);

    const initialForm = getCurrentFormMode();
    if (initialForm) {
        initializeForm(initialForm);
    }
})();

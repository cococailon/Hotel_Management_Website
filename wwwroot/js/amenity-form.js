(() => {
    "use strict";

    if (window.__amenityFormBound) {
        return;
    }

    window.__amenityFormBound = true;

    function getForm(button) {
        return button?.closest("form[data-amenity-form]");
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
})();

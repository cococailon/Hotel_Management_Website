(() => {
    "use strict";
    if (window.__serviceFormBound) return;
    window.__serviceFormBound = true;

    function getForm(button) { return button?.closest("form[data-service-form]"); }
    function validate(form) {
        return window.NativeFormValidation?.validateForm
            ? window.NativeFormValidation.validateForm(form, { focusFirst: true })
            : form.checkValidity();
    }

    async function submitByAjax(form, button) {
        if (!window.HotelAjax || typeof window.HotelAjax.navigate !== "function") {
            HTMLFormElement.prototype.submit.call(form);
            return true;
        }
        window.HotelUI?.setButtonLoading(button, true);
        try {
            const ok = await window.HotelAjax.navigate(form.action, {
                method: "POST",
                body: new FormData(form),
                historyMode: "replace",
                preserveScroll: false,
                showErrorToast: false
            });
            if (!ok) window.HotelUI?.toast("error", "Có lỗi xảy ra", "Không thể hoàn tất thao tác. Vui lòng thử lại.");
            return ok;
        } catch (e) {
            console.error("[Service] Form submit failed:", e);
            window.HotelUI?.toast("error", "Có lỗi xảy ra", "Không thể hoàn tất thao tác. Vui lòng thử lại.");
            return false;
        } finally {
            window.HotelUI?.setButtonLoading(button, false);
        }
    }

    document.addEventListener("submit", event => {
        const form = event.target instanceof HTMLFormElement ? event.target.closest("form[data-service-form]") : null;
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
            note: edit && activeBookings ? "Dịch vụ đang thuộc loại phòng có Booking hoạt động. Mã, tên, đơn giá và đơn vị sẽ được giữ nguyên." : "",
            confirmText: edit ? "Lưu thay đổi" : "Tạo dịch vụ",
            variant: "primary",
            icon: edit ? "bi-pencil-square" : "bi-cone-striped",
            onConfirm: async () => submitByAjax(form, button)
        });
    }, true);
})();

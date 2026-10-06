(() => {
    "use strict";

    const init = () => {
        const form = document.querySelector(".guest-filter-form");
        if (form && form.dataset.ajaxFilterBound !== "true") {
            form.dataset.ajaxFilterBound = "true";
            form.addEventListener("submit", () => {
                const button = form.querySelector(".guest-filter-submit");
                if (button && window.HotelUI?.setButtonLoading) {
                    window.HotelUI.setButtonLoading(button, true, "Đang lọc...");
                }
            });
        }

        document.querySelectorAll(".guest-delete-form").forEach((deleteForm) => {
            if (deleteForm.dataset.confirmBound === "true") return;
            deleteForm.dataset.confirmBound = "true";

            deleteForm.addEventListener("submit", (event) => {
                event.preventDefault();
                const guestCode = deleteForm.dataset.guestCode || "khách hàng này";

                if (!window.HotelUI?.showConfirm) {
                    deleteForm.submit();
                    return;
                }

                window.HotelUI.showConfirm({
                    title: "Xác nhận xóa khách hàng",
                    message: `Bạn có chắc chắn muốn xóa khách hàng ${guestCode}?`,
                    note: "Thao tác này sẽ xóa khách hàng và toàn bộ Booking, BookingRoom, dịch vụ và phí phát sinh liên quan. Không thể khôi phục trên giao diện quản lý.",
                    variant: "danger",
                    confirmText: "Xóa khách hàng",
                    onConfirm: () => deleteForm.submit()
                });
            });
        });
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init, { once: true });
    } else {
        init();
    }
})();

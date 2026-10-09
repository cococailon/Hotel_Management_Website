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

                const confirmationMessage =
                    `Bạn có chắc chắn muốn xóa khách hàng ${guestCode}?\n\n` +
                    "Điều kiện xóa: khách hàng phải còn ít nhất một lịch đặt phòng chưa bị xóa và tất cả các lịch đặt phòng đó phải ở trạng thái Xác nhận thất bại, Check in thất bại, Khách hủy trước check in, Vắng mặt hoặc Khách sạn hủy.\n\n" +
                    "Sau khi xác nhận, máy chủ sẽ kiểm tra lại điều kiện xóa tại thời điểm thực hiện.\n\n" +
                    "Dữ liệu được xóa mềm kèm theo: Guests, Bookings, BookingRooms, BookingRoomServices và AdditionalFeeRoom. Bills/BillDetails không thuộc phạm vi xóa khách hàng hiện tại.";

                if (!window.HotelUI?.showConfirm) {
                    if (window.confirm(confirmationMessage)) {
                        deleteForm.submit();
                    }
                    return;
                }

                window.HotelUI.showConfirm({
                    title: "Xác nhận xóa khách hàng",
                    message: `Bạn có chắc chắn muốn xóa khách hàng ${guestCode}?`,
                    note: "Điều kiện xóa: tất cả Booking chưa bị xóa của khách hàng phải ở trạng thái Xác nhận thất bại, Check in thất bại, Khách hủy trước check in, Vắng mặt hoặc Khách sạn hủy; khách hàng phải còn ít nhất một Booking. Sau khi xác nhận, hệ thống sẽ kiểm tra lại điều kiện trên máy chủ.\n\nDữ liệu được xóa mềm: Guests, Bookings, BookingRooms, BookingRoomServices và AdditionalFeeRoom. Bills/BillDetails không thuộc phạm vi xóa khách hàng hiện tại.",
                    variant: "danger",
                    icon: "bi-trash3",
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

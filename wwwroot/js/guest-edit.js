(() => {
    "use strict";

    const init = () => {
        const form = document.getElementById("guestEditForm");
        if (!form || form.dataset.guestEditBound === "true") return;
        form.dataset.guestEditBound = "true";

        const submitButton = form.querySelector("[data-ui-submit], button[type='submit']");
        let isSubmitting = false;
        let confirmedSubmit = false;

        const detailsEditable = Array.from(form.querySelectorAll("#GuestCode, #Name"))
            .some((el) => !el.disabled);

        const phone = form.querySelector("#PhoneNumber");
        const email = form.querySelector("#Email");

        const validateContact = () => {
            if (!detailsEditable || !phone || !email) return true;

            const hasPhone = (phone.value || "").trim().length > 0;
            const hasEmail = (email.value || "").trim().length > 0;
            const valid = hasPhone || hasEmail;
            const message = valid ? "" : "Phải nhập ít nhất Số điện thoại hoặc Email.";

            [phone, email].forEach((input) => {
                input.setCustomValidity(message);
            });

            return valid;
        };

        phone?.addEventListener("input", validateContact);
        email?.addEventListener("input", validateContact);

        form.addEventListener("submit", (event) => {
            if (confirmedSubmit) {
                return;
            }

            event.preventDefault();

            if (isSubmitting) {
                return;
            }

            if (detailsEditable && !validateContact()) {
                email?.reportValidity();
                return;
            }

            const code = form.querySelector("#GuestCode");
            if (detailsEditable && code && !/^[^\s]+$/.test(code.value || "")) {
                code.setCustomValidity("Mã khách hàng không được chứa dấu cách hoặc khoảng trắng.");
                code.reportValidity();
                return;
            }
            code?.setCustomValidity("");

            if (window.jQuery && typeof window.jQuery.fn.validate === "function") {
                const valid = window.jQuery(form).valid();
                if (!valid) {
                    return;
                }
            } else if (typeof form.checkValidity === "function" && !form.checkValidity()) {
                form.reportValidity();
                return;
            }

            if (!window.HotelUI || typeof window.HotelUI.showConfirm !== "function") {
                console.error("HotelUI.showConfirm chưa sẵn sàng. Không thực hiện submit để tránh bỏ qua bước xác nhận.");
                return;
            }

            const guestName = form.querySelector("#Name")?.value?.trim() || "khách hàng này";

            window.HotelUI.showConfirm({
                title: "Xác nhận sửa khách hàng",
                message: `Bạn có chắc muốn lưu thay đổi thông tin của ${guestName}?`,
                note: "Hệ thống sẽ kiểm tra lại quyền sửa theo trạng thái Booking trước khi cập nhật dữ liệu.",
                confirmText: "Lưu thay đổi",
                icon: "bi-check-lg",
                variant: "primary",
                onConfirm: () => {
                    isSubmitting = true;
                    confirmedSubmit = true;

                    if (window.HotelUI?.setButtonLoading && submitButton) {
                        window.HotelUI.setButtonLoading(submitButton, true, "Đang lưu...");
                    }

                    HTMLFormElement.prototype.submit.call(form);
                }
            });
        });

        window.addEventListener("pageshow", () => {
            isSubmitting = false;
            confirmedSubmit = false;

            if (window.HotelUI?.setButtonLoading && submitButton) {
                window.HotelUI.setButtonLoading(submitButton, false);
            }
        });
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init, { once: true });
    } else {
        init();
    }
})();

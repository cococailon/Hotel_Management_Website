(function (window, document) {
    "use strict";

    if (window.__userIndexBound) {
        return;
    }

    window.__userIndexBound = true;

    const actionConfig = {
        lock: {
            title: "Xác nhận khóa tài khoản",
            message: 'Bạn có chắc muốn khóa tài khoản "{name}" không?',
            note: "Tài khoản sẽ không thể đăng nhập và toàn bộ quyền truy cập của tài khoản sẽ bị vô hiệu hóa.",
            confirmText: "Khóa tài khoản",
            variant: "warning",
            formId: "userLockForm",
            idInputId: "userLockId",
            returnUrlInputId: "userLockReturnUrl"
        },
        reopen: {
            title: "Xác nhận mở lại tài khoản",
            message: 'Bạn có chắc muốn mở lại tài khoản "{name}" không?',
            note: "Tài khoản sẽ được mở lại và có thể đăng nhập bình thường.",
            confirmText: "Mở lại tài khoản",
            variant: "success",
            formId: "userReopenForm",
            idInputId: "userReopenId",
            returnUrlInputId: "userReopenReturnUrl"
        },
        delete: {
            title: "Xác nhận xóa tài khoản",
            message: 'Bạn có chắc muốn xóa tài khoản "{name}" không?',
            note: "Tài khoản sẽ bị xóa và không hiện ở trang danh sách.",
            confirmText: "Xóa tài khoản",
            variant: "danger",
            formId: "userDeleteForm",
            idInputId: "userDeleteId",
            returnUrlInputId: "userDeleteReturnUrl"
        }
    };

    function getActionFromButton(button) {
        if (button.classList.contains("user-lock-btn")) {
            return "lock";
        }

        if (button.classList.contains("user-reopen-btn")) {
            return "reopen";
        }

        if (button.classList.contains("user-delete-btn")) {
            return "delete";
        }

        return null;
    }

    async function executeAction(button, action) {
        const config = actionConfig[action];
        if (!config) {
            throw new Error(`Không tồn tại action: ${action}`);
        }

        const form = document.getElementById(config.formId);
        const idInput = document.getElementById(config.idInputId);
        const returnUrlInput = document.getElementById(config.returnUrlInputId);

        if (!form || !idInput || !returnUrlInput) {
            throw new Error("Không tìm thấy form thao tác tài khoản.");
        }

        const userId = button.dataset.userId;
        const returnUrl = button.dataset.returnUrl || (
            window.location.pathname + window.location.search
        );

        if (!userId) {
            throw new Error("Không tìm thấy User ID.");
        }

        idInput.value = userId;
        returnUrlInput.value = returnUrl;

        const formData = new FormData(form);

        window.HotelUI?.setButtonLoading(button, true);

        try {
            if (!window.HotelAjax || typeof window.HotelAjax.navigate !== "function") {
                throw new Error("HotelAjax.navigate không tồn tại.");
            }

            const success = await window.HotelAjax.navigate(
                form.action,
                {
                    method: "POST",
                    body: formData,
                    historyMode: "replace",
                    preserveScroll: true,
                    showErrorToast: false
                }
            );

            if (!success) {
                window.HotelUI?.toast(
                    "error",
                    "Có lỗi xảy ra",
                    "Không thể hoàn tất thao tác. Vui lòng thử lại."
                );
                return false;
            }

            return true;
        }
        catch (error) {
            console.error("[User] Action failed:", error);
            window.HotelUI?.toast(
                "error",
                "Có lỗi xảy ra",
                "Không thể hoàn tất thao tác. Vui lòng thử lại."
            );
            return false;
        }
        finally {
            window.HotelUI?.setButtonLoading(button, false);
        }
    }

    function showConfirmation(button, action) {
        const config = actionConfig[action];
        if (!config) {
            return;
        }

        const userName = button.dataset.userName || "tài khoản này";

        window.HotelUI?.showConfirm({
            title: config.title,
            message: config.message.replace("{name}", userName),
            note: config.note,
            confirmText: config.confirmText,
            variant: config.variant,
            onConfirm: async function () {
                await executeAction(button, action);
            }
        });
    }

    document.addEventListener("click", function (event) {
        const target = event.target instanceof Element
            ? event.target
            : null;

        if (!target) {
            return;
        }

        const button = target.closest(
            ".user-lock-btn, .user-reopen-btn, .user-delete-btn"
        );

        if (!button) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();

        if (button.disabled) {
            return;
        }

        const action = getActionFromButton(button);
        if (!action) {
            return;
        }

        showConfirmation(button, action);
    }, true);

    // Giữ lại toggle trạng thái mật khẩu nếu có UI cũ được render trong page.
    document.addEventListener("click", function (event) {
        const target = event.target instanceof Element
            ? event.target.closest("[data-password-toggle]")
            : null;

        if (!target) {
            return;
        }

        const wrapper = target.closest(".user-password-cell");
        if (!wrapper) {
            return;
        }

        const value = wrapper.querySelector("[data-password-state]");
        const icon = target.querySelector("i");

        if (!value) {
            return;
        }

        const masked = value.dataset.passwordState !== "revealed";

        if (masked) {
            value.textContent = "Đã mã hóa";
            value.dataset.passwordState = "revealed";
            target.setAttribute("aria-label", "Ẩn thông tin mật khẩu");
            target.title = "Mật khẩu gốc không thể khôi phục từ hash";
            if (icon) {
                icon.className = "bi bi-eye";
            }
        } else {
            value.textContent = "********";
            value.dataset.passwordState = "masked";
            target.setAttribute("aria-label", "Hiện trạng thái mật khẩu");
            target.title = "Mật khẩu đang được lưu dạng mã hóa; không thể khôi phục mật khẩu gốc";
            if (icon) {
                icon.className = "bi bi-eye-slash";
            }
        }
    });
})(window, document);

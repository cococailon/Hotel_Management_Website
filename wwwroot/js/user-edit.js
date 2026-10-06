(function (window, document) {
    "use strict";

    const form = document.getElementById("userEditForm");
    const submitButton = document.getElementById("userEditSubmitButton");
    const roleSelect = document.getElementById("userEditRole");
    const employeeBlock = document.getElementById("userEditEmployeeAssignmentBlock");
    const employeeSelect = document.getElementById("userEditEmployeeId");
    const adminEmployeeNote = document.getElementById("userEditAdminEmployeeNote");
    const passwordInput = document.getElementById("userEditPassword");
    const passwordToggle = document.querySelector("[data-user-edit-password-toggle]");
    const passwordError = document.getElementById("userEditPasswordError");
    const userCodeInput = document.getElementById("UserCode");
    const confirmModal = document.getElementById("uiConfirmModal");

    if (!form || !submitButton || !roleSelect || !employeeBlock || !employeeSelect || !confirmModal) {
        return;
    }

    let confirmedSubmit = false;
    let isSubmitting = false;

    function getRole() {
        return Number(roleSelect.value);
    }

    function updateEmployeeOptions() {
        const role = getRole();
        const isAdmin = role === 0;

        employeeBlock.classList.toggle("d-none", isAdmin);
        adminEmployeeNote.classList.toggle("d-none", !isAdmin);

        Array.from(employeeSelect.options).forEach(function (option) {
            if (!option.value) {
                option.hidden = false;
                option.disabled = false;
                return;
            }

            const position = Number(option.dataset.position);
            const visible = !isAdmin &&
                ((role === 1 && position === 0) ||
                 (role === 2 && position === 1));

            option.hidden = !visible;
            option.disabled = !visible;
        });

        if (isAdmin) {
            employeeSelect.value = "";
            employeeSelect.disabled = true;
            return;
        }

        employeeSelect.disabled = false;

        const current = employeeSelect.options[employeeSelect.selectedIndex];
        if (current && current.value && current.disabled) {
            employeeSelect.value = "";
        }
    }

    function validateBasic() {
        if (window.jQuery && typeof window.jQuery.fn.validate === "function") {
            if (!window.jQuery(form).valid()) {
                return false;
            }
        }

        if (passwordError) {
            passwordError.textContent = "";
        }

        const userCode = userCodeInput?.value?.trim() || "";
        if (!userCode || /\s/.test(userCode)) {
            const message = document.querySelector('[data-valmsg-for="UserCode"]');
            if (message) message.textContent = !userCode
                ? "Mã tài khoản không được để trống."
                : "Mã tài khoản không được chứa dấu cách hoặc khoảng trắng.";
            userCodeInput?.classList.add("is-invalid");
            userCodeInput?.focus();
            return false;
        }

        const password = passwordInput?.value ?? "";
        if (password !== "" && password.length < 5) {
            if (passwordError) {
                passwordError.textContent = "Mật khẩu mới phải có ít nhất 5 ký tự.";
            }
            passwordInput?.focus();
            return false;
        }

        const role = getRole();
        if (![0, 1, 2].includes(role)) {
            roleSelect.focus();
            return false;
        }

        if (!employeeSelect.disabled && employeeSelect.value) {
            const option = employeeSelect.options[employeeSelect.selectedIndex];
            if (!option || option.disabled) {
                employeeSelect.focus();
                return false;
            }
        }

        return true;
    }

    function getConfirmationMessage() {
        const userName = document.getElementById("UserName")?.value?.trim() || "tài khoản này";
        const employeeOption = !employeeSelect.disabled && employeeSelect.value
            ? employeeSelect.options[employeeSelect.selectedIndex]
            : null;
        const employeeLabel = employeeOption?.textContent?.trim();

        if (employeeLabel && !employeeLabel.includes("(Đã nghỉ việc)")) {
            return `Bạn có chắc muốn lưu thay đổi cho tài khoản "${userName}" và gán cho ${employeeLabel}?`;
        }

        if (!employeeSelect.disabled && !employeeSelect.value) {
            return `Bạn có chắc muốn lưu thay đổi cho tài khoản "${userName}" và không gán nhân viên?`;
        }

        return `Bạn có chắc muốn lưu thay đổi cho tài khoản "${userName}"?`;
    }

    if (passwordToggle && passwordInput) {
        passwordToggle.addEventListener("click", function () {
            const passwordVisible = passwordInput.type === "text";
            passwordInput.type = passwordVisible ? "password" : "text";

            const icon = passwordToggle.querySelector("i");
            if (icon) {
                icon.className = passwordVisible ? "bi bi-eye-slash" : "bi bi-eye";
            }
        });
    }

    roleSelect.addEventListener("change", updateEmployeeOptions);
    updateEmployeeOptions();

    form.addEventListener("submit", function (event) {
        if (confirmedSubmit) {
            return;
        }

        event.preventDefault();

        if (isSubmitting) {
            return;
        }

        // Validate cơ bản trước, sau đó mới được mở modal xác nhận.
        if (!validateBasic()) {
            return;
        }

        window.HotelUI?.showConfirm({
            title: "Xác nhận sửa tài khoản",
            message: getConfirmationMessage(),
            note: "Mã tài khoản và trạng thái không thay đổi. Mật khẩu chỉ được cập nhật khi bạn nhập mật khẩu mới.",
            confirmText: "Lưu thay đổi",
            variant: "primary",
            icon: "bi-check-lg",
            onConfirm: function () {
                isSubmitting = true;
                confirmedSubmit = true;

                window.HotelUI?.setButtonLoading(
                    submitButton,
                    true,
                    "Đang lưu..."
                );

                if (typeof form.requestSubmit === "function") {
                    form.requestSubmit(submitButton);
                } else {
                    HTMLFormElement.prototype.submit.call(form);
                }
            }
        });
    });

    window.addEventListener("pageshow", function () {
        confirmedSubmit = false;
        isSubmitting = false;

        window.HotelUI?.setButtonLoading(
            submitButton,
            false
        );

        updateEmployeeOptions();
    });
})(window, document);

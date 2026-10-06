(function (window, document) {
    "use strict";

    function getForm() {
        return document.getElementById("userCreateForm");
    }

    function getRole() {
        const role = document.getElementById("userRole");
        return role ? Number(role.value) : 1;
    }

    function updateEmployeeOptions() {
        const block = document.getElementById("employeeAssignmentBlock");
        const select = document.getElementById("userEmployeeId");

        if (!block || !select) {
            return;
        }

        const role = getRole();
        const canAssignEmployee = role === 1 || role === 2;
        const currentValue = select.value;

        block.classList.toggle("d-none", !canAssignEmployee);
        select.disabled = !canAssignEmployee;

        Array.from(select.options).forEach(function (option) {
            if (!option.value) {
                option.hidden = false;
                option.disabled = false;
                return;
            }

            const position = Number(option.dataset.position);
            const visible = canAssignEmployee &&
                ((role === 1 && position === 0) ||
                 (role === 2 && position === 1));

            // Cho phép chọn cả nhân viên đã có tài khoản.
            // Kiểm tra trùng tài khoản được thực hiện sau submit ở backend.
            option.hidden = !visible;
            option.disabled = !visible;
        });

        if (!canAssignEmployee) {
            select.value = "";
            return;
        }

        if (currentValue) {
            const option = Array.from(select.options)
                .find(x => x.value === currentValue && !x.disabled);

            select.value = option ? currentValue : "";
        }
    }

    function bindPasswordToggle() {
        const button = document.querySelector("[data-password-input-toggle]");
        const input = document.getElementById("userPassword");

        if (!button || !input || button.dataset.bound === "true") {
            return;
        }

        button.dataset.bound = "true";
        button.addEventListener("click", function () {
            const hidden = input.type === "password";
            input.type = hidden ? "text" : "password";
            button.setAttribute("aria-label", hidden ? "Ẩn mật khẩu" : "Hiện mật khẩu");

            const icon = button.querySelector("i");
            if (icon) {
                icon.className = hidden ? "bi bi-eye" : "bi bi-eye-slash";
            }
        });
    }

    function bindControls() {
        const role = document.getElementById("userRole");

        if (role && role.dataset.bound !== "true") {
            role.dataset.bound = "true";
            role.addEventListener("change", updateEmployeeOptions);
        }

        updateEmployeeOptions();
        bindPasswordToggle();
    }

    function validateBasic(form) {
        // Ưu tiên ASP.NET Core unobtrusive validation để kiểm tra Required,
        // StringLength, RegularExpression, Range, MinLength, ...
        if (window.jQuery && typeof window.jQuery.fn.validate === "function") {
            const valid = window.jQuery(form).valid();

            if (!valid) {
                const firstInvalid = form.querySelector(".input-validation-error, .is-invalid");
                firstInvalid?.focus();
                return false;
            }
        }
        else if (!form.checkValidity()) {
            form.reportValidity();
            return false;
        }

        const userCode = document.getElementById("userCode");
        const userName = document.getElementById("UserName") || form.querySelector('[name="UserName"]');
        const password = document.getElementById("userPassword");
        const role = document.getElementById("userRole");
        const employeeSelect = document.getElementById("userEmployeeId");

        // Mã tài khoản để trống vẫn hợp lệ vì server sẽ tự sinh như Create Nhân viên.
        if (userCode && /\s/.test(userCode.value.trim())) {
            const message = "Mã tài khoản không được chứa dấu cách hoặc khoảng trắng.";
            const validator = document.querySelector('[data-valmsg-for="UserCode"]');
            if (validator) {
                validator.textContent = message;
                validator.classList.remove("field-validation-valid");
                validator.classList.add("field-validation-error");
            }
            userCode.classList.add("input-validation-error", "is-invalid");
            userCode.focus();
            return false;
        }

        if (!userName || userName.value.trim() === "") {
            userName?.focus();
            return false;
        }

        if (!password || password.value.length < 5) {
            password?.focus();
            return false;
        }

        const roleValue = Number(role?.value);
        if (![0, 1, 2].includes(roleValue)) {
            role?.focus();
            return false;
        }

        // Gán nhân viên là tùy chọn. Chỉ kiểm tra nếu người dùng đã chọn.
        if (employeeSelect && !employeeSelect.disabled && employeeSelect.value) {
            const option = employeeSelect.options[employeeSelect.selectedIndex];
            if (!option || option.disabled) {
                employeeSelect.focus();
                return false;
            }

            // Không kiểm tra "đã gán tài khoản" ở client.
            // Create phải cho phép submit; backend sẽ kiểm tra và trả lỗi tại EmployeeId.
        }

        return true;
    }

    function getConfirmationMessage() {
        const employeeSelect = document.getElementById("userEmployeeId");

        if (employeeSelect && !employeeSelect.disabled && employeeSelect.value) {
            const option = employeeSelect.options[employeeSelect.selectedIndex];
            const employeeLabel = option?.textContent?.trim() || "nhân viên đã chọn";
            return `Bạn có chắc muốn thêm tài khoản cho nhân viên ${employeeLabel} này không ?`;
        }

        return "Bạn có chắc muốn thêm tài khoản này không ?";
    }

    async function submitConfirmed(form, submitButton) {
        if (!window.HotelAjax || typeof window.HotelAjax.navigate !== "function") {
            form.submit();
            return;
        }

        const body = new FormData(form, submitButton || undefined);

        window.HotelUI?.setButtonLoading(
            submitButton,
            true,
            "Đang tạo..."
        );

        const success = await window.HotelAjax.navigate(
            form.action,
            {
                method: "POST",
                body,
                historyMode: "replace",
                preserveScroll: false,
                showErrorToast: false
            }
        );

        if (!success) {
            window.HotelUI?.toast(
                "error",
                "Không thể tạo tài khoản",
                "Vui lòng kiểm tra dữ liệu và thử lại."
            );
        }

        window.HotelUI?.setButtonLoading(
            submitButton,
            false
        );
    }

    function bindSubmitHandler() {
        if (window.__userCreateSubmitBound) {
            return;
        }

        window.__userCreateSubmitBound = true;

        document.addEventListener("submit", function (event) {
            const target = event.target instanceof Element
                ? event.target
                : null;

            const form = target?.closest("#userCreateForm");
            if (!form) {
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();

            // Không mở modal nếu validation cơ bản còn lỗi.
            if (!validateBasic(form)) {
                return;
            }

            const role = getRole();
            const canCreateAdmin = form.dataset.canCreateAdmin === "true";

            if (role === 0 && !canCreateAdmin) {
                window.HotelUI?.toast(
                    "error",
                    "Không có quyền",
                    "Chỉ Admin toàn quyền mới được tạo tài khoản Admin."
                );
                return;
            }

            const submitButton = document.getElementById("userCreateSubmitButton");
            const employeeSelect = document.getElementById("userEmployeeId");

            window.HotelUI?.showConfirm({
                title: "Xác nhận tạo tài khoản",
                message: getConfirmationMessage(),
                variant: "primary",
                confirmText: "Tạo tài khoản",
                onConfirm: function () {
                    return submitConfirmed(form, submitButton);
                }
            });
        }, true);
    }

    bindSubmitHandler();
    bindControls();
})(window, document);

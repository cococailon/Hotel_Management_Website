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
        const title = document.getElementById("roleAssignmentTitle");
        const description = document.getElementById("roleAssignmentDescription");
        const adminNote = document.getElementById("adminEmployeeNote");

        block.classList.toggle("d-none", !canAssignEmployee);
        select.disabled = !canAssignEmployee;
        adminNote?.classList.toggle("d-none", role !== 0);

        if (title) {
            title.textContent = role === 0 ? "Phân quyền Admin" : "Phân quyền và nhân viên";
        }
        if (description) {
            description.textContent = role === 0
                ? "Thiết lập trạng thái hoạt động cho tài khoản Admin. Tài khoản Admin không cần gán nhân viên."
                : "Gán tài khoản cho nhân viên phù hợp và xác định trạng thái hoạt động.";
        }

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

    function bindGenerateCode() {
        const button = document.getElementById("userGenerateCodeButton");
        const input = document.getElementById("UserCode");

        if (!button || !input || button.dataset.bound === "true") {
            return;
        }

        button.dataset.bound = "true";
        button.addEventListener("click", async function () {
            const url = button.dataset.generateUrl;
            if (!url) return;

            button.disabled = true;
            const originalHtml = button.innerHTML;
            button.innerHTML = '<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>Đang sinh...';

            try {
                const response = await fetch(url, {
                    method: "GET",
                    headers: { "X-Requested-With": "XMLHttpRequest" },
                    credentials: "same-origin"
                });

                if (!response.ok) {
                    throw new Error("Generate user code failed.");
                }

                const data = await response.json();
                input.value = data.userCode || "";
                input.classList.remove("input-validation-error", "is-invalid");

                const validator = document.querySelector('[data-valmsg-for="UserCode"]');
                if (validator) {
                    validator.textContent = "";
                }
            } catch {
                window.HotelUI?.toast(
                    "error",
                    "Không thể tự sinh mã tài khoản",
                    "Vui lòng thử lại."
                );
            } finally {
                button.disabled = false;
                button.innerHTML = originalHtml;
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

        const userCode = document.getElementById("UserCode") || form.querySelector('[name="UserCode"]');
        const userName = document.getElementById("UserName") || form.querySelector('[name="UserName"]');
        const password = document.getElementById("userPassword");
        const role = document.getElementById("userRole");
        const employeeSelect = document.getElementById("userEmployeeId");

        if (!userCode || !/^[A-Za-z0-9]+$/.test(userCode.value)) {
            userCode?.focus();
            return false;
        }

        if (!userName || userName.value.length < 3 || /\s/.test(userName.value)) {
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

    function scrollToEmployeeValidationError() {
        const validationMessage = document.querySelector(
            '[data-valmsg-for="EmployeeId"]'
        );

        if (!validationMessage) {
            return false;
        }

        const message = (validationMessage.textContent || "").trim();
        if (!message || !message.includes("Nhân viên này đã được gán cho một tài khoản khác.")) {
            return false;
        }

        requestAnimationFrame(function () {
            requestAnimationFrame(function () {
                validationMessage.scrollIntoView({
                    behavior: "smooth",
                    block: "center",
                    inline: "nearest"
                });
            });
        });

        return true;
    }

    function bindEmployeeValidationScroll() {
        if (window.__userCreateEmployeeValidationScrollBound) {
            return;
        }

        window.__userCreateEmployeeValidationScrollBound = true;

        document.addEventListener("hotel:ajax-loaded", function () {
            scrollToEmployeeValidationError();
        });

        scrollToEmployeeValidationError();
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
    bindGenerateCode();
    bindEmployeeValidationScroll();
})(window, document);

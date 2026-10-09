(() => {
    "use strict";

    const init = () => {
        const form = document.getElementById("guestEditForm");
        if (!form || form.dataset.guestEditBound === "true") return;
        form.dataset.guestEditBound = "true";

        const submitButton = form.querySelector("[data-ui-submit], button[type='submit']");
        let isSubmitting = false;
        let confirmedSubmit = false;

        const detailsEditable = form.dataset.detailsEditable === "true";
        const requireFullDetails = form.dataset.requireFullDetails === "true";

        const code = form.querySelector("#GuestCode");
        const name = form.querySelector("#Name");
        const dob = form.querySelector("#DateOfBirth");
        const sex = form.querySelector("#Sex");
        const address = form.querySelector("#Address");
        const phone = form.querySelector("#PhoneNumber");
        const email = form.querySelector("#Email");
        const identityNumber = form.querySelector("#IdentityNumber");
        const identityType = form.querySelector("#IdentityType");
        const note = form.querySelector("#Note");

        const getValidationMessage = (input) => {
            if (!input) return null;
            return form.querySelector(`[data-valmsg-for="${CSS.escape(input.name)}"]`);
        };

        const setFieldError = (input, message) => {
            if (!input) return false;

            input.classList.add("is-invalid");
            input.setAttribute("aria-invalid", "true");
            input.setCustomValidity(message || "Dữ liệu không hợp lệ.");

            const validationMessage = getValidationMessage(input);
            if (validationMessage) {
                validationMessage.textContent = message || "Dữ liệu không hợp lệ.";
                validationMessage.classList.add("field-validation-error");
            }

            return false;
        };

        const clearFieldError = (input) => {
            if (!input) return true;

            input.classList.remove("is-invalid");
            input.removeAttribute("aria-invalid");
            input.setCustomValidity("");

            const validationMessage = getValidationMessage(input);
            if (validationMessage) {
                validationMessage.textContent = "";
                validationMessage.classList.remove("field-validation-error");
            }

            return true;
        };

        const normalizePhone = (value) => {
            let normalized = (value || "").trim().replace(/[\s().-]/g, "");
            if (normalized.startsWith("00")) {
                normalized = `+${normalized.slice(2)}`;
            }
            return normalized;
        };

        const validateCode = () => {
            if (!detailsEditable) return true;

            const value = (code?.value || "").trim();
            if (!value) {
                return setFieldError(code, "Mã khách hàng không được để trống.");
            }

            if (value.length > 50) {
                return setFieldError(code, "Mã khách hàng không được vượt quá 50 ký tự.");
            }

            if (!/^[A-Za-z0-9]+$/.test(value)) {
                return setFieldError(
                    code,
                    "Mã khách hàng chỉ được bao gồm chữ hoa, chữ thường và số, không chứa dấu cách và ký tự đặc biệt."
                );
            }

            return clearFieldError(code);
        };

        const validateName = () => {
            if (!detailsEditable) return true;

            const value = (name?.value || "").trim();
            if (!value) {
                return setFieldError(name, "Tên khách hàng là bắt buộc.");
            }

            if (value.length > 100) {
                return setFieldError(name, "Tên khách hàng không được vượt quá 100 ký tự.");
            }

            return clearFieldError(name);
        };

        const validateDob = () => {
            if (!detailsEditable) return true;

            const value = dob?.value || "";
            if (!value) {
                if (requireFullDetails) {
                    return setFieldError(
                        dob,
                        "Ngày sinh là bắt buộc khi Booking đang ở trạng thái Đang ở."
                    );
                }
                return clearFieldError(dob);
            }

            const date = new Date(`${value}T00:00:00`);
            const today = new Date();
            today.setHours(0, 0, 0, 0);

            if (Number.isNaN(date.getTime()) || date > today) {
                return setFieldError(dob, "Ngày sinh không được lớn hơn ngày hiện tại.");
            }

            return clearFieldError(dob);
        };

        const validateSex = () => {
            if (!detailsEditable) return true;

            const value = sex?.value || "";
            if (!value) {
                if (requireFullDetails) {
                    return setFieldError(
                        sex,
                        "Giới tính là bắt buộc khi Booking đang ở trạng thái Đang ở."
                    );
                }
                return clearFieldError(sex);
            }

            if (!["0", "1", "2"].includes(value)) {
                return setFieldError(sex, "Giới tính không hợp lệ.");
            }

            return clearFieldError(sex);
        };

        const validateAddress = () => {
            if (!detailsEditable) return true;

            const value = (address?.value || "").trim();
            if (!value) {
                if (requireFullDetails) {
                    return setFieldError(
                        address,
                        "Địa chỉ là bắt buộc khi Booking đang ở trạng thái Đang ở."
                    );
                }
                return clearFieldError(address);
            }

            if (value.length > 255) {
                return setFieldError(address, "Địa chỉ không được vượt quá 255 ký tự.");
            }

            return clearFieldError(address);
        };

        const validatePhone = () => {
            if (!detailsEditable) return true;

            const value = (phone?.value || "").trim();
            const emailValue = (email?.value || "").trim();

            if (requireFullDetails && !value) {
                return setFieldError(
                    phone,
                    "Số điện thoại là bắt buộc khi Booking đang ở trạng thái Đang ở."
                );
            }

            if (!requireFullDetails && !value && !emailValue) {
                setFieldError(phone, "Phải nhập ít nhất Số điện thoại hoặc Email.");
                setFieldError(email, "Phải nhập ít nhất Số điện thoại hoặc Email.");
                return false;
            }

            if (!value) {
                return clearFieldError(phone);
            }

            if (value.length > 30) {
                return setFieldError(
                    phone,
                    "Số điện thoại nhập vào không được vượt quá 30 ký tự."
                );
            }

            const normalized = normalizePhone(value);
            const valid = normalized.startsWith("+")
                ? /^\+[1-9][0-9]{6,14}$/.test(normalized)
                : /^[0-9]{7,15}$/.test(normalized);

            if (!valid) {
                return setFieldError(
                    phone,
                    "Số điện thoại không hợp lệ. Có thể nhập số quốc tế; sau khi chuẩn hóa phải có 7-15 chữ số."
                );
            }

            return clearFieldError(phone);
        };

        const validateEmail = () => {
            if (!detailsEditable) return true;

            const value = (email?.value || "").trim();
            if (!value) {
                return clearFieldError(email);
            }

            if (value.length > 254 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) {
                return setFieldError(
                    email,
                    "Email không đúng định dạng hoặc vượt quá 254 ký tự."
                );
            }

            return clearFieldError(email);
        };

        const validateIdentity = () => {
            if (!detailsEditable) return true;

            const number = (identityNumber?.value || "").trim();
            const type = identityType?.value || "";

            if (!number) {
                clearFieldError(identityType);
                if (requireFullDetails) {
                    return setFieldError(
                        identityNumber,
                        "Số giấy tờ là bắt buộc khi Booking đang ở trạng thái Đang ở."
                    );
                }
                return clearFieldError(identityNumber);
            }

            if (number.length > 50) {
                return setFieldError(identityNumber, "Số giấy tờ không được vượt quá 50 ký tự.");
            }

            if (!["0", "1", "2"].includes(type)) {
                return setFieldError(
                    identityType,
                    "Vui lòng chọn loại giấy tờ khi nhập số giấy tờ."
                );
            }

            if (type === "0" && !/^\d{12}$/.test(number)) {
                return setFieldError(identityNumber, "CCCD phải gồm đúng 12 chữ số.");
            }

            if (type === "1" && !/^\d{12}$/.test(number)) {
                return setFieldError(identityNumber, "Giấy phép lái xe phải gồm đúng 12 chữ số.");
            }

            if (type === "2" && !/^[A-Za-z0-9]{5,20}$/.test(number)) {
                return setFieldError(
                    identityNumber,
                    "Hộ chiếu phải gồm 5-20 ký tự chữ và số, không chứa dấu cách hoặc ký tự đặc biệt."
                );
            }

            clearFieldError(identityType);
            return clearFieldError(identityNumber);
        };

        const validateNote = () => {
            const value = (note?.value || "").trim();
            if (value.length > 255) {
                return setFieldError(note, "Ghi chú khách hàng không được vượt quá 255 ký tự.");
            }
            return clearFieldError(note);
        };

        const validateAll = () => {
            if (!detailsEditable) {
                return validateNote();
            }

            const results = [
                validateCode(),
                validateName(),
                validateDob(),
                validateSex(),
                validateAddress(),
                validatePhone(),
                validateEmail(),
                validateIdentity(),
                validateNote()
            ];

            return results.every(Boolean);
        };

        [code, name, dob, sex, address, phone, email, identityNumber, identityType, note]
            .filter(Boolean)
            .forEach((input) => {
                const eventName = input.tagName === "SELECT" ? "change" : "input";
                input.addEventListener(eventName, () => {
                    const validators = new Map([
                        [code, validateCode],
                        [name, validateName],
                        [dob, validateDob],
                        [sex, validateSex],
                        [address, validateAddress],
                        [phone, validatePhone],
                        [email, validateEmail],
                        [identityNumber, validateIdentity],
                        [identityType, validateIdentity],
                        [note, validateNote]
                    ]);

                    validators.get(input)?.();

                    // Khi đổi SĐT/Email, điều kiện liên hệ của trạng thái 0/1
                    // phải được đánh giá lại ngay lập tức trên cả hai field.
                    if (input === phone || input === email) {
                        validatePhone();
                        validateEmail();
                    }
                });
            });

        form.addEventListener("submit", (event) => {
            if (confirmedSubmit) {
                return;
            }

            event.preventDefault();

            if (isSubmitting) {
                return;
            }

            if (!validateAll()) {
                const firstInvalid = form.querySelector(".is-invalid") || form.querySelector("[aria-invalid='true']");
                if (firstInvalid) {
                    firstInvalid.scrollIntoView({ behavior: "smooth", block: "center" });
                    window.setTimeout(() => {
                        try {
                            firstInvalid.focus({ preventScroll: true });
                        } catch {
                            firstInvalid.focus();
                        }
                    }, 150);
                }
                return;
            }

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

            const guestName = name?.value?.trim() || "khách hàng này";

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

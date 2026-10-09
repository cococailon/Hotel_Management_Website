(() => {
    "use strict";

    const form = document.getElementById("employeeEditForm");
    const submitButton = document.getElementById("employeeEditSubmitButton");
    const statusInput = document.getElementById("Status");
    const endDateInput = document.getElementById("employeeEditEndDate");
    const startDateInput = document.getElementById("employeeEditStartDate");
    const dateOfBirthInput = document.getElementById("employeeEditDateOfBirth");
    const positionSelect = document.getElementById("employeeEditPosition");
    const positionPreview = document.getElementById("employeeEditPositionPreview");
    const employeeCodeInput = document.getElementById("EmployeeCode");

    if (!form || !submitButton || !statusInput || !endDateInput ||
        !startDateInput || !dateOfBirthInput || !positionSelect || !employeeCodeInput) {
        return;
    }

    if (form.dataset.confirmationBound === "true") {
        return;
    }

    form.dataset.confirmationBound = "true";

    let confirmedSubmit = false;
    let isSubmitting = false;

    const dateValidationMessageIds = {
        DateOfBirth: "DateOfBirthBusinessValidationMessage",
        StartDate: "StartDateBusinessValidationMessage",
        EndDate: "EndDateBusinessValidationMessage"
    };

    const getLocalToday = () => {
        const now = new Date();
        const year = now.getFullYear();
        const month = String(now.getMonth() + 1).padStart(2, "0");
        const day = String(now.getDate()).padStart(2, "0");
        return `${year}-${month}-${day}`;
    };

    const setFieldError = (fieldName, message) => {
        const input = document.getElementById(fieldName);
        const messageElement = dateValidationMessageIds[fieldName]
            ? document.getElementById(dateValidationMessageIds[fieldName])
            : document.querySelector(`[data-valmsg-for="${fieldName}"]`);

        if (messageElement) {
            messageElement.textContent = message || "";
        }

        if (input) {
            input.classList.toggle("input-validation-error", Boolean(message));
            input.classList.toggle("is-invalid", Boolean(message));
        }
    };

    const clearBusinessErrors = () => {
        ["DateOfBirth", "StartDate", "EndDate"].forEach(fieldName => {
            setFieldError(fieldName, "");
        });
    };

    const syncEndDateState = () => {
        const isWorking = statusInput.value === "1";

        // Chỉ disable/enable, tuyệt đối không tự xóa giá trị người dùng nhập.
        endDateInput.disabled = isWorking;
    };

    const syncPositionPreview = () => {
        const isHousekeeping = positionSelect.value === "1";
        const icon = positionSelect.parentElement?.querySelector(".employee-edit-position-icon");

        if (positionPreview) {
            positionPreview.textContent = isHousekeeping
                ? "Buồng Phòng"
                : "Lễ Tân";
        }

        if (icon) {
            icon.className = `bi ${isHousekeeping ? "bi-house-check" : "bi-person-vcard"} employee-edit-position-icon`;
        }
    };

    const validateBusinessRules = () => {
        clearBusinessErrors();

        const dob = dateOfBirthInput.value || "";
        const start = startDateInput.value || "";
        const end = endDateInput.value || "";
        const today = getLocalToday();
        const status = statusInput.value;

        let isValid = true;
        let firstInvalidInput = null;

        const addError = (fieldName, input, message) => {
            setFieldError(fieldName, message);
            isValid = false;

            if (!firstInvalidInput && input) {
                firstInvalidInput = input;
            }
        };

        if (!dob) {
            addError("DateOfBirth", dateOfBirthInput, "Vui lòng chọn ngày sinh.");
        } else if (dob >= today) {
            addError(
                "DateOfBirth",
                dateOfBirthInput,
                "Ngày sinh phải bé hơn ngày hiện tại."
            );
        }

        if (!start) {
            addError("StartDate", startDateInput, "Vui lòng chọn ngày vào làm.");
        } else if (dob && dob >= start) {
            addError(
                "StartDate",
                startDateInput,
                "Ngày vào làm phải sau ngày sinh."
            );
        }

        if (status === "0" && !end) {
            addError(
                "EndDate",
                endDateInput,
                "Nhân viên đã nghỉ việc phải có ngày nghỉ việc."
            );
        }

        if (status === "1" && end) {
            addError(
                "EndDate",
                endDateInput,
                "Nhân viên đang làm không được có ngày nghỉ việc."
            );
        }

        if (start && end && end < start) {
            addError(
                "EndDate",
                endDateInput,
                "Ngày nghỉ việc không được trước ngày vào làm."
            );
        }

        if (firstInvalidInput) {
            firstInvalidInput.focus();
        }

        return isValid;
    };

    const validateDateFieldsOnChange = (changedField) => {
        const dob = dateOfBirthInput.value || "";
        const start = startDateInput.value || "";
        const end = endDateInput.value || "";
        const today = getLocalToday();
        const status = statusInput.value;

        if (changedField === "DateOfBirth") {
            if (!dob) {
                setFieldError("DateOfBirth", "Vui lòng chọn ngày sinh.");
            } else if (dob >= today) {
                setFieldError(
                    "DateOfBirth",
                    "Ngày sinh phải bé hơn ngày hiện tại."
                );
            } else {
                setFieldError("DateOfBirth", "");
            }

            // Chỉ cập nhật lỗi StartDate khi lỗi quan hệ thực sự đang xảy ra.
            // Không đụng tới lỗi trống/validation độc lập của StartDate.
            if (dob && start && dob >= start) {
                setFieldError(
                    "StartDate",
                    "Ngày vào làm phải sau ngày sinh."
                );
            }

            return;
        }

        if (changedField === "StartDate") {
            if (!start) {
                setFieldError("StartDate", "Vui lòng chọn ngày vào làm.");
            } else if (dob && dob >= start) {
                setFieldError(
                    "StartDate",
                    "Ngày vào làm phải sau ngày sinh."
                );
            } else if (start && end && end < start) {
                setFieldError(
                    "StartDate",
                    "Ngày vào làm không được sau ngày nghỉ việc."
                );
            } else {
                setFieldError("StartDate", "");
            }

            return;
        }

        if (changedField === "EndDate") {
            if (status === "0" && !end) {
                setFieldError(
                    "EndDate",
                    "Nhân viên đã nghỉ việc phải có ngày nghỉ việc."
                );
            } else if (status === "1" && end) {
                setFieldError(
                    "EndDate",
                    "Nhân viên đang làm không được có ngày nghỉ việc."
                );
            } else if (start && end && end < start) {
                setFieldError(
                    "EndDate",
                    "Ngày nghỉ việc không được trước ngày vào làm."
                );
            } else {
                setFieldError("EndDate", "");
            }
        }
    };

    employeeCodeInput.addEventListener("input", () => {
        if (/^[A-Za-z0-9]*$/.test(employeeCodeInput.value)) {
            setFieldError("EmployeeCode", "");
        }
    });

    dateOfBirthInput.addEventListener("change", () => {
        validateDateFieldsOnChange("DateOfBirth");
    });

    startDateInput.addEventListener("change", () => {
        validateDateFieldsOnChange("StartDate");
    });

    endDateInput.addEventListener("change", () => {
        validateDateFieldsOnChange("EndDate");
    });

    dateOfBirthInput.addEventListener("blur", () => {
        window.setTimeout(() => validateDateFieldsOnChange("DateOfBirth"), 0);
    });

    startDateInput.addEventListener("blur", () => {
        window.setTimeout(() => validateDateFieldsOnChange("StartDate"), 0);
    });

    endDateInput.addEventListener("blur", () => {
        window.setTimeout(() => validateDateFieldsOnChange("EndDate"), 0);
    });

    syncEndDateState();
    syncPositionPreview();
    positionSelect.addEventListener("change", syncPositionPreview);

    form.addEventListener("submit", function (event) {
        if (confirmedSubmit) {
            return;
        }

        event.preventDefault();

        if (isSubmitting) {
            return;
        }

        // Business validation phải chạy trước jQuery validation.
        if (!validateBusinessRules()) {
            return;
        }

        if (
            window.jQuery &&
            typeof window.jQuery.fn.validate === "function" &&
            !window.jQuery(form).valid()
        ) {
            return;
        }

        const employeeCode = employeeCodeInput.value.trim();

        if (!employeeCode) {
            setFieldError("EmployeeCode", "Vui lòng nhập mã nhân viên.");
            employeeCodeInput.focus();
            return;
        }

        if (!/^[A-Za-z0-9]+$/.test(employeeCode)) {
            setFieldError(
                "EmployeeCode",
                "Mã nhân viên chỉ được chứa chữ cái và chữ số, không chứa dấu cách hoặc ký tự đặc biệt."
            );
            employeeCodeInput.focus();
            return;
        }

        const employeeName =
            document.getElementById("EmployeeName")?.value?.trim()
            || "nhân viên này";

        if (!window.HotelUI || typeof window.HotelUI.showConfirm !== "function") {
            console.error("HotelUI.showConfirm chưa sẵn sàng.");
            return;
        }

        window.HotelUI.showConfirm({
            title: "Xác nhận lưu thay đổi",
            message: `Bạn có chắc muốn lưu thay đổi cho nhân viên "${employeeName}" không?`,
            note: "",
            confirmText: "Lưu thay đổi",
            icon: "bi-check-lg",
            variant: "primary",
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

        window.HotelUI?.setButtonLoading(submitButton, false);

        syncEndDateState();
        syncPositionPreview();
    });
})();

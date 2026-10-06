(() => {
            const form = document.getElementById('employeeEditForm');
            const submitButton = document.getElementById('employeeEditSubmitButton');
            const statusInput = document.getElementById('Status');
            const endDateInput = document.getElementById('employeeEditEndDate');
            const startDateInput = document.getElementById('employeeEditStartDate');
            const dateOfBirthInput = document.getElementById('employeeEditDateOfBirth');
            const positionSelect = document.getElementById('employeeEditPosition');
            const positionPreview = document.getElementById('employeeEditPositionPreview');

            // positionPreview là phần tử UI tùy chọn.
            // Không được return khỏi script chỉ vì preview không tồn tại,
            // nếu không toàn bộ submit/confirm của trang Edit sẽ không được bind.
            if (!form || !submitButton || !statusInput || !endDateInput ||
                !startDateInput || !dateOfBirthInput || !positionSelect) {
                return;
            }

            if (form.dataset.confirmationBound === 'true') {
                return;
            }

            form.dataset.confirmationBound = 'true';

            const codeInput = document.getElementById('EmployeeCode');

            let confirmedSubmit = false;
            let isSubmitting = false;

            const syncEndDateState = () => {
                const isWorking = statusInput.value === '1';

                if (isWorking) {
                    endDateInput.value = '';
                    endDateInput.disabled = true;
                } else {
                    endDateInput.disabled = false;
                }
            };

            const syncPositionPreview = () => {
                const isHousekeeping = positionSelect.value === '1';
                const icon = positionSelect.parentElement?.querySelector('.employee-edit-position-icon');

                if (positionPreview) {
                    positionPreview.textContent = isHousekeeping
                        ? 'Buồng Phòng'
                        : 'Lễ Tân';
                }

                if (icon) {
                    icon.className = `bi ${isHousekeeping ? 'bi-house-check' : 'bi-person-vcard'} employee-edit-position-icon`;
                }
            };

            const setFieldError = (fieldName, message) => {
                const input = document.getElementById(fieldName === 'DateOfBirth'
                    ? 'employeeEditDateOfBirth'
                    : fieldName === 'StartDate'
                        ? 'employeeEditStartDate'
                        : 'employeeEditEndDate');
                const messageElement = document.querySelector(`[data-valmsg-for="${fieldName}"]`);

                if (messageElement) {
                    messageElement.textContent = message || '';
                    messageElement.classList.toggle('field-validation-error', Boolean(message));
                    messageElement.classList.toggle('field-validation-valid', !message);
                }

                if (input) {
                    input.classList.toggle('input-validation-error', Boolean(message));
                    input.classList.toggle('is-invalid', Boolean(message));
                }
            };

            const clearBusinessErrors = () => {
                ['DateOfBirth', 'StartDate', 'EndDate'].forEach(fieldName => {
                    setFieldError(fieldName, '');
                });
            };

            const validateBusinessRules = () => {
                clearBusinessErrors();

                const dob = dateOfBirthInput.value;
                const start = startDateInput.value;
                const end = endDateInput.value;
                const today = new Date().toISOString().slice(0, 10);
                const status = statusInput.value;

                if (dob && dob > today) {
                    setFieldError('DateOfBirth', 'Ngày sinh không được lớn hơn ngày hiện tại.');
                    dateOfBirthInput.focus();
                    return false;
                }

                if (dob && start && dob >= start) {
                    setFieldError('StartDate', 'Ngày vào làm phải sau ngày sinh.');
                    startDateInput.focus();
                    return false;
                }

                if (status === '0' && !end) {
                    setFieldError('EndDate', 'Nhân viên đã nghỉ việc phải có ngày nghỉ việc.');
                    endDateInput.focus();
                    return false;
                }

                if (status === '1' && end) {
                    setFieldError('EndDate', 'Nhân viên đang làm không được có ngày nghỉ việc.');
                    endDateInput.focus();
                    return false;
                }

                if (start && end && end < start) {
                    setFieldError('EndDate', 'Ngày nghỉ việc không được trước ngày vào làm.');
                    endDateInput.focus();
                    return false;
                }

                return true;
            };

            syncEndDateState();
            syncPositionPreview();

            positionSelect.addEventListener('change', syncPositionPreview);
            dateOfBirthInput.addEventListener('change', () => {
                setFieldError('DateOfBirth', '');
                setFieldError('StartDate', '');
            });
            startDateInput.addEventListener('change', () => {
                setFieldError('StartDate', '');
            });
            endDateInput.addEventListener('change', () => {
                setFieldError('EndDate', '');
            });

            form.addEventListener('submit', function (event) {
                if (confirmedSubmit) {
                    return;
                }

                event.preventDefault();

                if (isSubmitting) {
                    return;
                }

                if (window.jQuery &&
                    typeof window.jQuery.fn.validate === 'function') {

                    const valid = window.jQuery(form).valid();

                    if (!valid) {
                        return;
                    }
                }

                if (!validateBusinessRules()) {
                    return;
                }

                const employeeName =
                    document.getElementById('EmployeeName')?.value?.trim()
                    || 'nhân viên này';

                if (!window.HotelUI || typeof window.HotelUI.showConfirm !== 'function') {
                    console.error('HotelUI.showConfirm chưa sẵn sàng. Không thực hiện submit để tránh bỏ qua bước xác nhận.');
                    return;
                }

                window.HotelUI.showConfirm({
                    title: 'Xác nhận lưu thay đổi',
                    message: `Bạn có chắc muốn lưu thay đổi cho ${employeeName}?`,
                    note: 'Mã nhân viên không thay đổi. Thông tin sau khi lưu sẽ được cập nhật vào hệ thống.',
                    confirmText: 'Lưu thay đổi',
                    icon: 'bi-check-lg',
                    variant: 'primary',
                    onConfirm: async function () {
                        isSubmitting = true;
                        confirmedSubmit = true;

                        // Không gọi HotelUI.lockForm() ở đây. Hàm này disable các <select>,
                        // khiến Position/Sex không được gửi khi dùng native form.submit().
                        // Chỉ khóa nút submit bằng loading state.

                        if (window.HotelUI?.setButtonLoading) {
                            window.HotelUI.setButtonLoading(
                                submitButton,
                                true,
                                'Đang lưu...'
                            );
                        }

                        // Dùng native submit để không chạy lại event submit.
                        HTMLFormElement.prototype.submit.call(form);
                    }
                });
            });

            window.addEventListener('pageshow', function () {
                isSubmitting = false;
                confirmedSubmit = false;

                if (window.HotelUI?.lockForm) {
                    window.HotelUI.lockForm(form, false);
                }

                if (window.HotelUI?.setButtonLoading) {
                    window.HotelUI.setButtonLoading(
                        submitButton,
                        false
                    );
                }

                syncEndDateState();
                syncPositionPreview();
            });
        })();

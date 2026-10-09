(() => {
    "use strict";

    const state = window.__roomTypeFormState || { eventsBound: false };
    window.__roomTypeFormState = state;

    function getForm() {
        return document.getElementById("roomTypeCreateForm") || document.getElementById("roomTypeEditForm");
    }

    function isEdit() {
        return !!document.getElementById("roomTypeEditForm");
    }

    function getRows(type) {
        return document.querySelectorAll(`#${type}Rows [data-association-row]`);
    }

    function updateEmptyState(type) {
        const empty = document.getElementById(`${type}Empty`);
        if (!empty) return;
        empty.classList.toggle("d-none", getRows(type).length > 0);
    }

    function reindexRows(type) {
        const rows = Array.from(getRows(type));
        rows.forEach((row, index) => {
            row.querySelectorAll("[name]").forEach(input => {
                input.name = input.name
                    .replace(new RegExp(`^${type === "amenity" ? "Amenities" : "Services"}\\[\\d+\\]\\.`), `${type === "amenity" ? "Amenities" : "Services"}[${index}].`);
            });
        });
        updateEmptyState(type);
    }

    function removeCurrentSelectionFromPicker(type, id) {
        const picker = document.getElementById(type === "amenity" ? "amenityPicker" : "servicePicker");
        if (!picker) return;
        const option = picker.querySelector(`option[value="${CSS.escape(id)}"]`);
        if (option) {
            option.hidden = true;
            option.disabled = true;
        }
    }

    function restoreSelectionToPicker(type, id) {
        const picker = document.getElementById(type === "amenity" ? "amenityPicker" : "servicePicker");
        if (!picker) return;
        const option = picker.querySelector(`option[value="${CSS.escape(id)}"]`);
        if (option) {
            option.hidden = false;
            option.disabled = false;
        }
    }

    function addRow(type) {
        const picker = document.getElementById(type === "amenity" ? "amenityPicker" : "servicePicker");
        const rowsContainer = document.getElementById(type === "amenity" ? "amenityRows" : "serviceRows");
        if (!picker || !rowsContainer || !picker.value) return;

        const option = picker.options[picker.selectedIndex];
        if (!option) return;

        const exists = Array.from(getRows(type)).some(row => row.querySelector('[data-association-item]')?.value === picker.value);
        if (exists) {
            window.HotelUI?.toast("warning", "Đã tồn tại", "Tiện nghi/dịch vụ này đã được thêm vào loại phòng.");
            return;
        }

        const collection = type === "amenity" ? "Amenities" : "Services";
        const index = getRows(type).length;
        const code = option.dataset.code || "";
        const name = option.dataset.name || option.textContent.trim();

        const priceHtml = type === "service"
            ? `<div class="small text-body-secondary">Dịch vụ được thêm theo cấu hình giá hiện tại.</div>`
            : "";

        const row = document.createElement("div");
        row.className = "roomtype-association-row";
        row.setAttribute("data-association-row", "");
        row.innerHTML = `
            <input type="hidden" name="${collection}[${index}].AssignmentId" value="" />
            <input type="hidden" name="${collection}[${index}].ItemId" value="${picker.value}" data-association-item />
            <div class="flex-grow-1 min-width-0">
                <div class="fw-semibold">${escapeHtml(code)} - ${escapeHtml(name)}</div>
                ${priceHtml}
            </div>
            <select class="form-select form-select-sm roomtype-association-status" name="${collection}[${index}].IsActive" data-association-status>
                <option value="true">Đang hoạt động</option>
                <option value="false">Dừng hoạt động</option>
            </select>
            <button type="button" class="btn btn-outline-danger btn-sm roomtype-remove-association" data-remove-association title="Xóa">
                <i class="bi bi-trash3" aria-hidden="true"></i>
            </button>
        `;

        rowsContainer.appendChild(row);
        removeCurrentSelectionFromPicker(type, picker.value);
        picker.value = "";
        updateEmptyState(type);
    }

    function escapeHtml(value) {
        return String(value).replace(/[&<>'"]/g, char => ({
            "&": "&amp;",
            "<": "&lt;",
            ">": "&gt;",
            "'": "&#39;",
            '"': "&quot;"
        }[char]));
    }

    function roomTypeCodeMessageElement(form) {
        if (!form) return null;
        return Array.from(form.querySelectorAll("[data-valmsg-for]"))
            .find(element => element.getAttribute("data-valmsg-for") === "RoomTypeCode") || null;
    }

    function getRoomTypeCodeInput(form) {
        return form?.querySelector('[name="RoomTypeCode"]') || null;
    }

    function captureServerValidationState(form) {
        const input = getRoomTypeCodeInput(form);
        const messageElement = roomTypeCodeMessageElement(form);
        if (!input || !messageElement) return;

        const message = messageElement.textContent?.trim() || "";
        if (!message) return;

        input.dataset.serverValidationValue = input.value;
        input.dataset.serverValidationMessage = message;
        input.classList.add("is-invalid");
        input.setAttribute("aria-invalid", "true");
        messageElement.classList.remove("field-validation-valid");
        messageElement.classList.add("field-validation-error");
    }

    function clearCapturedServerValidation(input, messageElement) {
        if (!input) return;

        delete input.dataset.serverValidationValue;
        delete input.dataset.serverValidationMessage;

        if (messageElement) {
            messageElement.textContent = "";
            messageElement.classList.remove("field-validation-error");
            messageElement.classList.add("field-validation-valid");
        }
    }

    function restoreCapturedServerValidation(form) {
        const input = getRoomTypeCodeInput(form);
        const messageElement = roomTypeCodeMessageElement(form);
        if (!input || !messageElement) return;

        const originalValue = input.dataset.serverValidationValue;
        const message = input.dataset.serverValidationMessage;
        if (!originalValue || !message || input.value !== originalValue) return;

        input.classList.add("is-invalid");
        input.setAttribute("aria-invalid", "true");
        messageElement.textContent = message;
        messageElement.classList.remove("field-validation-valid");
        messageElement.classList.add("field-validation-error");
    }

    function bindRoomTypeCodeServerErrorPersistence(form) {
        const input = getRoomTypeCodeInput(form);
        if (!input || input.dataset.serverErrorPersistenceBound === "true") return;

        input.dataset.serverErrorPersistenceBound = "true";
        captureServerValidationState(form);

        input.addEventListener("input", () => {
            const originalValue = input.dataset.serverValidationValue;
            if (originalValue !== undefined && input.value !== originalValue) {
                clearCapturedServerValidation(input, roomTypeCodeMessageElement(form));
            }
        });

        input.addEventListener("change", () => {
            restoreCapturedServerValidation(form);
        });

        input.addEventListener("blur", () => {
            window.setTimeout(() => restoreCapturedServerValidation(form), 0);
        });
    }

    async function generateRoomTypeCode(form) {
        const input = getRoomTypeCodeInput(form);
        const button = document.getElementById("generateRoomTypeCodeButton");
        if (!input || !button) return;

        const originalHtml = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>Đang sinh...';

        try {
            const endpoint = button.dataset.endpoint;
            if (!endpoint) throw new Error("Thiếu endpoint tự sinh mã.");

            const response = await fetch(endpoint, {
                method: "GET",
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    "Accept": "application/json"
                },
                credentials: "same-origin"
            });

            const payload = await response.json().catch(() => null);
            if (!response.ok || !payload?.code) {
                throw new Error(payload?.message || "Không thể tự sinh mã loại phòng.");
            }

            input.value = payload.code;
            input.dispatchEvent(new Event("input", { bubbles: true }));
            input.dispatchEvent(new Event("change", { bubbles: true }));
            window.NativeFormValidation?.validateElement(form, input);
        }
        catch (error) {
            window.HotelUI?.toast(
                "error",
                "Không thể tự sinh mã",
                error?.message || "Vui lòng thử lại."
            );
        }
        finally {
            button.disabled = false;
            button.innerHTML = originalHtml;
        }
    }

    function validateBasic(form) {
        const nativeValidation = window.NativeFormValidation;
        if (nativeValidation && !nativeValidation.validateForm(form, { focusFirst: true })) {
            return false;
        }

        for (const type of ["amenity", "service"]) {
            const ids = Array.from(getRows(type))
                .map(row => row.querySelector('[data-association-item]')?.value)
                .filter(Boolean);

            if (new Set(ids).size !== ids.length) {
                window.HotelUI?.toast(
                    "error",
                    "Dữ liệu không hợp lệ",
                    `Không được thêm trùng ${type === "amenity" ? "tiện nghi" : "dịch vụ"}.`
                );
                return false;
            }
        }

        return true;
    }

    function submitConfirmed(form, submitButton) {
        if (submitButton) {
            const content = submitButton.querySelector(".ui-button-content");
            const loading = submitButton.querySelector(".ui-button-loading");
            if (content) content.classList.add("d-none");
            if (loading) loading.classList.remove("d-none");
            submitButton.disabled = true;
        }

        // Bypass toàn bộ submit event / admin-ajax cho POST Edit.
        HTMLFormElement.prototype.submit.call(form);
    }

    function bindAddRemoveEvents() {
        if (state.eventsBound) return;

        state.eventsBound = true;
        document.addEventListener("click", function (event) {
            const addButton = event.target instanceof Element
                ? event.target.closest("[data-add-association]")
                : null;

            if (addButton) {
                event.preventDefault();
                addRow(addButton.dataset.addAssociation);
                return;
            }

            const removeButton = event.target instanceof Element
                ? event.target.closest("[data-remove-association]")
                : null;

            if (!removeButton || removeButton.disabled) return;

            const row = removeButton.closest("[data-association-row]");
            if (!row) return;

            const section = row.closest("[data-roomtype-association]");
            const type = section?.dataset.roomtypeAssociation;
            const id = row.querySelector('[data-association-item]')?.value;
            row.remove();

            if (type && id) restoreSelectionToPicker(type, id);
            if (type) reindexRows(type);
        });

        ["amenity", "service"].forEach(type => {
            Array.from(getRows(type)).forEach(row => {
                const id = row.querySelector('[data-association-item]')?.value;
                if (id) removeCurrentSelectionFromPicker(type, id);
            });
            updateEmptyState(type);
        });
    }

    function bindSubmitEvents() {
        const form = getForm();
        if (!form || form.dataset.roomTypeSubmitBound === "true") return;

        form.dataset.roomTypeSubmitBound = "true";

        form.addEventListener("submit", function (event) {
            if (form.dataset.confirmedSubmit === "true") {
                delete form.dataset.confirmedSubmit;
                return;
            }

            event.preventDefault();
            event.stopPropagation();

            if (!validateBasic(form)) return;

            const roomTypeName =
                document.querySelector('[name="RoomTypeName"]')?.value.trim() ||
                "loại phòng này";
            const isEditing = isEdit();
            const confirmAction = function () {
                form.dataset.confirmedSubmit = "true";
                submitConfirmed(form, form.querySelector("button[type='submit']"));
            };

            const modal = document.getElementById("uiConfirmModal");
            const okButton = document.getElementById("uiConfirmOkBtn");
            const cancelButton = document.getElementById("uiConfirmCancelBtn");
            const modalIsReady = !!(
                window.HotelUI &&
                typeof window.HotelUI.showConfirm === "function" &&
                window.bootstrap?.Modal &&
                modal &&
                okButton &&
                cancelButton &&
                okButton.dataset.hotelUiBound === "true"
            );

            if (modalIsReady) {
                window.HotelUI.showConfirm({
                    title: isEditing ? "Xác nhận sửa loại phòng" : "Xác nhận tạo loại phòng",
                    message: isEditing
                        ? `Bạn có chắc muốn lưu thay đổi cho ${roomTypeName} không?`
                        : `Bạn có chắc muốn tạo loại phòng ${roomTypeName} không?`,
                    note: isEditing
                        ? ""
                        : "",
                    confirmText: isEditing ? "Lưu thay đổi" : "Tạo loại phòng",
                    variant: "primary",
                    icon: isEditing ? "bi-check-lg" : "bi-plus-lg",
                    onConfirm: confirmAction
                });
                return;
            }

            // Nếu UI chung chưa sẵn sàng, không được nuốt submit.
            if (window.confirm(
                isEditing
                    ? `Bạn có chắc muốn lưu thay đổi cho ${roomTypeName} không?`
                    : `Bạn có chắc muốn tạo loại phòng ${roomTypeName} không?`
            )) {
                confirmAction();
            }
        });
    }

    function initializePageState() {
        ["amenity", "service"].forEach(type => {
            Array.from(getRows(type)).forEach(row => {
                const id = row.querySelector("[data-association-item]")?.value;
                if (id) removeCurrentSelectionFromPicker(type, id);
            });
            updateEmptyState(type);
        });
    }

    const form = getForm();
    if (form) {
        bindRoomTypeCodeServerErrorPersistence(form);

        const generateButton = document.getElementById("generateRoomTypeCodeButton");
        if (generateButton && generateButton.dataset.bound !== "true") {
            generateButton.dataset.bound = "true";
            generateButton.dataset.endpoint = generateButton.dataset.endpoint || generateButton.form?.dataset.generateCodeUrl || "";
            generateButton.addEventListener("click", () => generateRoomTypeCode(form));
        }
    }

    bindAddRemoveEvents();
    bindSubmitEvents();
    initializePageState();
})();

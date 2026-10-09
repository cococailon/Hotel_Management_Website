(function (window, document) {
    "use strict";

    const alreadyBound = window.__roomFormBound === true;
    window.__roomFormBound = true;

    let suggestionTimer = null;

    function getForm() {
        return document.getElementById("roomCreateForm") || document.getElementById("roomEditForm");
    }

    function isEdit(form) {
        return form?.id === "roomEditForm";
    }

    function isInUse(form) {
        return isEdit(form) && Number(form.querySelector('[name="Status"]')?.value) === 1;
    }

    function getField(name) {
        const form = getForm();
        return form?.querySelector(`[name="${name}"]`);
    }

    function setFieldError(name, message) {
        const input = getField(name);
        const span = document.querySelector(`[data-valmsg-for="${name}"]`);
        if (input) {
            input.classList.add("is-invalid");
        }
        if (span) {
            span.textContent = message;
            span.classList.remove("field-validation-valid");
            span.classList.add("field-validation-error");
        }
    }

    function clearFieldErrors(form) {
        form.querySelectorAll(".is-invalid").forEach(x => x.classList.remove("is-invalid"));
        form.querySelectorAll("[data-valmsg-for]").forEach(x => x.textContent = "");
    }

    function getMessageElement(form, name) {
        return Array.from(form?.querySelectorAll("[data-valmsg-for]") || [])
            .find(x => x.getAttribute("data-valmsg-for") === name) || null;
    }

    function captureServerError(form, name, relatedNames = []) {
        const input = getField(name);
        const messageElement = getMessageElement(form, name);
        if (!input || !messageElement) return;

        const message = messageElement.textContent?.trim() || "";
        if (!message) return;

        input.dataset.serverValidationValue = input.value;
        input.dataset.serverValidationMessage = message;
        input.dataset.serverValidationRelatedValues = JSON.stringify(
            relatedNames.map(relatedName => ({
                name: relatedName,
                value: getField(relatedName)?.value ?? ""
            }))
        );

        input.classList.add("is-invalid");
        input.setAttribute("aria-invalid", "true");
        messageElement.classList.remove("field-validation-valid");
        messageElement.classList.add("field-validation-error");
    }

    function restoreServerError(form, name) {
        const input = getField(name);
        const messageElement = getMessageElement(form, name);
        if (!input || !messageElement) return;

        const originalValue = input.dataset.serverValidationValue;
        const message = input.dataset.serverValidationMessage;
        if (originalValue === undefined || !message) return;

        let relatedValues = [];
        try {
            relatedValues = JSON.parse(input.dataset.serverValidationRelatedValues || "[]");
        }
        catch {
            relatedValues = [];
        }

        if (input.value !== originalValue) return;
        if (relatedValues.some(item => (getField(item.name)?.value ?? "") !== item.value)) return;

        input.classList.add("is-invalid");
        input.setAttribute("aria-invalid", "true");
        messageElement.textContent = message;
        messageElement.classList.remove("field-validation-valid");
        messageElement.classList.add("field-validation-error");
    }

    function clearServerError(name, relatedNames = []) {
        const input = getField(name);
        if (!input) return;

        delete input.dataset.serverValidationValue;
        delete input.dataset.serverValidationMessage;
        delete input.dataset.serverValidationRelatedValues;

        const messageElement = getMessageElement(getForm(), name);
        if (messageElement) {
            messageElement.textContent = "";
            messageElement.classList.remove("field-validation-error");
            messageElement.classList.add("field-validation-valid");
        }

        for (const relatedName of relatedNames) {
            const relatedInput = getField(relatedName);
            if (!relatedInput) continue;
            delete relatedInput.dataset.serverValidationValue;
            delete relatedInput.dataset.serverValidationMessage;
            delete relatedInput.dataset.serverValidationRelatedValues;
        }
    }

    function bindServerErrorPersistence(form) {
        if (!form || form.dataset.serverErrorPersistenceBound === "true") return;
        form.dataset.serverErrorPersistenceBound = "true";

        captureServerError(form, "RoomCode");
        captureServerError(form, "LeftToRightOrder", ["Floor", "Row"]);

        const codeInput = getField("RoomCode");
        if (codeInput) {
            codeInput.addEventListener("input", () => {
                if (codeInput.dataset.serverValidationValue !== undefined && codeInput.value !== codeInput.dataset.serverValidationValue) {
                    clearServerError("RoomCode");
                }
            });
            // native-validation xử lý focusout ở document capture trước;
            // listener capture tại field chạy ngay sau đó để khôi phục lỗi server.
            codeInput.addEventListener("focusout", () => {
                restoreServerError(form, "RoomCode");
            }, true);
        }

        for (const name of ["Floor", "Row", "LeftToRightOrder"]) {
            const input = getField(name);
            if (!input) continue;
            input.addEventListener("input", () => {
                const tracked = getField("LeftToRightOrder");
                if (tracked?.dataset.serverValidationValue !== undefined) {
                    clearServerError("LeftToRightOrder", ["Floor", "Row"]);
                }
            });
            input.addEventListener("focusout", () => {
                restoreServerError(form, "LeftToRightOrder");
            }, true);
        }
    }

    async function generateRoomCode(form) {
        const input = getField("RoomCode");
        const button = document.getElementById("generateRoomCodeButton");
        if (!input || !button || isEdit(form)) return;

        const originalHtml = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>Đang sinh...';

        try {
            const endpoint = button.dataset.endpoint;
            if (!endpoint) throw new Error("Thiếu endpoint tự sinh mã phòng.");

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
                throw new Error(payload?.message || "Không thể tự sinh mã phòng.");
            }

            input.value = payload.code;
            clearServerError("RoomCode");
            input.dispatchEvent(new Event("input", { bubbles: true }));
            input.dispatchEvent(new Event("change", { bubbles: true }));
            window.NativeFormValidation?.validateElement(form, input);
        }
        catch (error) {
            window.HotelUI?.toast("error", "Không thể tự sinh mã phòng", error?.message || "Vui lòng thử lại.");
        }
        finally {
            button.disabled = false;
            button.innerHTML = originalHtml;
        }
    }


    function getRoomIsActive(form) {
        if (!form) return true;

        if (isEdit(form)) {
            return form.dataset.roomIsActive === "true";
        }

        const activeInput = getField("IsActive");
        if (!activeInput) return true;

        return activeInput.value === "true";
    }

    function validateBasic(form) {
        const nativeValidation = window.NativeFormValidation;
        if (nativeValidation && !nativeValidation.validateForm(form, { focusFirst: true })) {
            return false;
        }

        if (isInUse(form)) {
            // Khi phòng đang sử dụng, các field khác ngoài Mô tả/Ghi chú
            // bị disabled và validator dùng chung sẽ tự bỏ qua chúng.
            return true;
        }

        const code = getField("RoomCode")?.value.trim() ?? "";
        const isEditForm = isEdit(form);

        if (!code) {
            setFieldError("RoomCode", "Vui lòng nhập mã phòng.");
            window.NativeFormValidation?.focusControl(getField("RoomCode"));
            return false;
        }

        const type = getField("RoomTypeId")?.value ?? "";
        const active = getRoomIsActive(form);
        const select = getField("RoomTypeId");
        const selected = select?.selectedOptions?.[0];

        if (!type) {
            setFieldError("RoomTypeId", "Vui lòng chọn loại phòng.");
            window.NativeFormValidation?.focusControl(select);
            return false;
        }

        if (!selected || selected.disabled) {
            setFieldError("RoomTypeId", "Loại phòng đã chọn không hợp lệ.");
            window.NativeFormValidation?.focusControl(select);
            return false;
        }

        if (active && selected.dataset.active !== "true") {
            setFieldError(
                "RoomTypeId",
                "Phòng đang hoạt động chỉ được chọn loại phòng đang hoạt động."
            );
            window.NativeFormValidation?.focusControl(select);
            return false;
        }

        return true;
    }

    function updateRoomTypeOptions() {
        const form = getForm();
        const select = document.getElementById("roomTypeId");

        if (!form || !select) {
            return;
        }

        const active = getRoomIsActive(form);

        Array.from(select.options).forEach(option => {
            if (!option.value) {
                option.disabled = false;
                option.hidden = false;
                return;
            }

            const isActiveType = option.dataset.active === "true";

            if (active) {
                option.disabled = !isActiveType || option.dataset.selectable === "false";
                option.hidden = !isActiveType;
            }
            else {
                option.disabled = option.dataset.selectable === "false";
                option.hidden = false;
            }
        });
    }

    function renderPositionSuggestion(data, row) {
        const floorSuggestion = document.getElementById("roomFloorSuggestion");
        const rowSuggestion = document.getElementById("roomRowSuggestion");

        if (!data) return;

        const rows = Array.isArray(data.rows) ? data.rows : [];
        if (floorSuggestion) {
            if (data.totalRows > 0) {
                const labels = rows.map(x => `Hàng ${x.row}: ${x.roomCount} phòng`).join(" · ");
                floorSuggestion.textContent = `Tầng ${data.floor} hiện có ${data.totalRows} hàng. ${labels}`;
            }
            else {
                floorSuggestion.textContent = `Tầng ${data.floor} hiện chưa có phòng; có thể bắt đầu từ Hàng 1.`;
            }
        }

        if (rowSuggestion) {
            if (row !== null && row !== undefined) {
                const current = rows.find(x => Number(x.row) === Number(row));
                if (current) {
                    rowSuggestion.textContent = `Hàng ${row} hiện có ${current.roomCount} phòng; thứ tự lớn nhất là ${current.maxLeftToRightOrder}.`;
                }
                else {
                    rowSuggestion.textContent = `Hàng ${row} hiện chưa có phòng; có thể bắt đầu từ Thứ tự 1.`;
                }
            }
            else {
                rowSuggestion.textContent = "";
            }
        }
    }

    async function loadPositionSuggestion() {
        const floorInput = document.getElementById("roomFloor");
        const rowInput = document.getElementById("roomRow");
        if (!floorInput) return;

        const floor = Number(floorInput.value);
        if (!Number.isInteger(floor)) return;

        const rowValue = rowInput?.value ?? "";
        const row = rowValue === "" ? null : Number(rowValue);
        const query = new URLSearchParams({ floor: String(floor) });
        if (row !== null && Number.isInteger(row)) query.set("row", String(row));

        try {
            const response = await fetch(`/Room/GetPositionSuggestions?${query.toString()}`, {
                method: "GET",
                headers: { "X-Requested-With": "XMLHttpRequest" },
                credentials: "same-origin"
            });

            if (!response.ok) return;
            renderPositionSuggestion(await response.json(), row);
        }
        catch (error) {
            console.warn("[Room] Không thể tải gợi ý vị trí:", error);
        }
    }

    function schedulePositionSuggestion() {
        window.clearTimeout(suggestionTimer);
        suggestionTimer = window.setTimeout(loadPositionSuggestion, 250);
    }

    function submitConfirmed(form, button) {
        window.HotelUI?.setButtonLoading(button, true, isEdit(form) ? "Đang lưu..." : "Đang tạo...");
        HTMLFormElement.prototype.submit.call(form);
    }

    function handleSubmit(event) {
        const target = event.target instanceof Element ? event.target : null;
        const form = target?.closest("#roomCreateForm, #roomEditForm");
        if (!form) return;

        event.preventDefault();
        event.stopImmediatePropagation();

        if (form.dataset.confirmedSubmit === "true") {
            delete form.dataset.confirmedSubmit;
            HTMLFormElement.prototype.submit.call(form);
            return;
        }

        if (!validateBasic(form)) return;

        const editing = isEdit(form);
        const roomName = getField("RoomName")?.value.trim() || "phòng này";

        const confirm = () => {
            form.dataset.confirmedSubmit = "true";
            submitConfirmed(
                form,
                document.getElementById(editing ? "roomEditSubmitButton" : "roomCreateSubmitButton")
            );
        };

        if (window.HotelUI && typeof window.HotelUI.showConfirm === "function") {
            window.HotelUI.showConfirm({
                title: editing ? "Xác nhận sửa phòng" : "Xác nhận tạo phòng",
                message: editing
                    ? `Bạn có chắc muốn lưu thay đổi cho ${roomName} không?`
                    : `Bạn có chắc muốn tạo phòng ${roomName} không?`,
                note: isInUse(form)
                    ? "Phòng đang sử dụng nên chỉ Mô tả và Ghi chú được phép thay đổi."
                    : "",
                confirmText: editing ? "Lưu thay đổi" : "Tạo phòng",
                variant: "primary",
                icon: editing ? "bi-check-lg" : "bi-plus-lg",
                onConfirm: confirm
            });
            return;
        }

        if (window.confirm(
            editing
                ? `Bạn có chắc muốn lưu thay đổi cho ${roomName} không?`
                : `Bạn có chắc muốn tạo phòng ${roomName} không?`
        )) {
            confirm();
        }
    }

    const currentForm = getForm();
    if (currentForm) {
        bindServerErrorPersistence(currentForm);
    }

    const generateButton = document.getElementById("generateRoomCodeButton");
    if (generateButton && !generateButton.dataset.bound) {
        generateButton.dataset.bound = "true";
        generateButton.addEventListener("click", () => generateRoomCode(getForm()));
    }

    if (!alreadyBound) {
        document.addEventListener("submit", handleSubmit, true);

        document.addEventListener("change", function (event) {
            const target = event.target instanceof Element ? event.target : null;
            if (target?.id === "roomIsActive") {
                updateRoomTypeOptions();
            }
            if (target?.id === "roomFloor" || target?.id === "roomRow") {
                schedulePositionSuggestion();
            }
        });

        document.addEventListener("input", function (event) {
            const target = event.target instanceof Element ? event.target : null;
            if (target?.id === "roomFloor" || target?.id === "roomRow") {
                schedulePositionSuggestion();
            }
        });
    }

    // Khởi tạo theo DOM hiện tại. Vì đây là event delegation, script có thể
    // được chạy nhiều lần sau AJAX navigation mà không bind trùng event.
    window.setTimeout(function () {
        updateRoomTypeOptions();
        loadPositionSuggestion();
    }, 0);
})(window, document);

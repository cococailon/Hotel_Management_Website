(function (window, document) {
    "use strict";

    if (window.__roomIndexBound) {
        return;
    }

    window.__roomIndexBound = true;

    function currentUrl() {
        return window.location.pathname + window.location.search;
    }

    async function postForm(form) {
        if (!form) {
            throw new Error("Không tìm thấy form thao tác phòng.");
        }

        const body = new FormData(form);

        if (!window.HotelAjax || typeof window.HotelAjax.navigate !== "function") {
            throw new Error("HotelAjax.navigate không tồn tại.");
        }

        return await window.HotelAjax.navigate(form.action, {
            method: "POST",
            body,
            historyMode: "replace",
            preserveScroll: true,
            showErrorToast: false
        });
    }

    function setFormValue(formId, inputId, value) {
        const form = document.getElementById(formId);
        const input = document.getElementById(inputId);
        if (input) {
            input.value = value || "";
        }

        const returnUrlInput = form?.querySelector('input[name="returnUrl"]');
        if (returnUrlInput) {
            returnUrlInput.value = currentUrl();
        }

        return form;
    }

    function showActionError(title, message) {
        window.HotelUI?.toast("warning", title, message);
    }

    function prepareMaintenanceOption(enabled) {
        const noteElement =
            document.getElementById("uiConfirmNote");

        if (!noteElement) {
            return null;
        }

        const oldOption =
            noteElement.querySelector(".room-maintenance-option");

        oldOption?.remove();

        const form =
            document.getElementById("roomDeactivateForm");

        const maintenanceInput =
            document.getElementById("roomDeactivateIsMaintenance");

        if (maintenanceInput) {
            maintenanceInput.value = "false";
        }

        if (!enabled) {
            return null;
        }

        const option = document.createElement("div");
        option.className = "room-maintenance-option mt-3";

        const wrapper = document.createElement("div");
        wrapper.className = "form-check mb-0";

        const checkbox = document.createElement("input");
        checkbox.type = "checkbox";
        checkbox.className = "form-check-input";
        checkbox.id = "roomMaintenanceCheckbox";
        checkbox.setAttribute("aria-describedby", "roomMaintenanceHelp");

        const label = document.createElement("label");
        label.className = "form-check-label fw-semibold";
        label.htmlFor = checkbox.id;
        label.textContent = "Đây có phải là trường hợp bảo trì không?";

        const help = document.createElement("div");
        help.className = "form-text mt-1";
        help.id = "roomMaintenanceHelp";
        help.textContent =
            "Nếu tích chọn, tình trạng phòng sẽ được chuyển sang Bảo trì.";

        checkbox.addEventListener("change", function () {
            if (maintenanceInput) {
                maintenanceInput.value = checkbox.checked ? "true" : "false";
            }
        });

        wrapper.appendChild(checkbox);
        wrapper.appendChild(label);
        option.appendChild(wrapper);
        option.appendChild(help);
        noteElement.appendChild(option);

        return checkbox;
    }

    async function executeAction(form, button) {
        try {
            window.HotelUI?.setButtonLoading(button, true);
            const success = await postForm(form);

            if (!success) {
                window.HotelUI?.toast(
                    "error",
                    "Không thể thực hiện",
                    "Máy chủ không thể hoàn tất thao tác phòng. Vui lòng thử lại."
                );
            }
        }
        catch (error) {
            console.error("[Room] Action failed:", error);
            window.HotelUI?.toast(
                "error",
                "Có lỗi xảy ra",
                "Không thể hoàn tất thao tác phòng. Vui lòng thử lại."
            );
        }
        finally {
            window.HotelUI?.setButtonLoading(button, false);
        }
    }

    function confirmAction(button, action) {
        const id = button.dataset.id;
        const name = button.dataset.name || "phòng này";
        const status = Number.parseInt(button.dataset.status || "-1", 10);
        const allowed = button.dataset.allowed !== "false";

        if (!id) {
            showActionError("Dữ liệu không hợp lệ", "Không xác định được phòng cần thao tác.");
            return;
        }

        if (!allowed) {
            if (action === "activate") {
                showActionError(
                    "Chưa thể mở hoạt động",
                    "Loại phòng đang dừng hoạt động. Hãy mở hoạt động loại phòng trước."
                );
            }
            return;
        }

        const config = {
            deactivate: {
                form: "roomDeactivateForm",
                input: "roomDeactivateId",
                title: "Xác nhận dừng hoạt động phòng",
                message: `Bạn có chắc muốn dừng hoạt động ${name} không?`,
                note: "Phòng đang được sử dụng sẽ không thể thực hiện thao tác này. Bạn có thể đánh dấu đây là trường hợp bảo trì; hệ thống vẫn kiểm tra lại điều kiện ở máy chủ.",
                confirmText: "Dừng hoạt động",
                variant: "warning",
                icon: "bi-pause-circle"
            },
            activate: {
                form: "roomActivateForm",
                input: "roomActivateId",
                title: "Xác nhận mở hoạt động phòng",
                message: `Bạn có chắc muốn mở hoạt động ${name} không?`,
                note: status === 4
                    ? "Phòng đang ở trạng thái Bảo trì. Khi mở hoạt động, hệ thống sẽ chuyển phòng về Trống."
                    : "Phòng chỉ được mở khi loại phòng đang hoạt động.",
                confirmText: "Mở hoạt động",
                variant: "success",
                icon: "bi-play-circle"
            },
            "reception-maintenance": {
                form: "roomReceptionMaintenanceForm",
                input: "roomReceptionMaintenanceId",
                title: "Xác nhận chuyển phòng sang Bảo trì",
                message: `Bạn có chắc muốn chuyển ${name} sang Bảo trì không?`,
                note: "Phòng sẽ được chuyển thành Status = 4 (Bảo trì) và IsActive = 0.",
                confirmText: "Chuyển Bảo trì",
                variant: "danger",
                icon: "bi-tools"
            },
            "reception-activate": {
                form: "roomReceptionActivateForm",
                input: "roomReceptionActivateId",
                title: "Xác nhận đưa phòng về Trống",
                message: `Bạn có chắc muốn đưa ${name} từ Bảo trì về Trống không?`,
                note: "Phòng sẽ được chuyển thành Status = 0 (Trống) và IsActive = 1.",
                confirmText: "Đưa về Trống",
                variant: "success",
                icon: "bi-play-circle"
            },
            "housekeeping-maintenance": {
                form: "housekeepingMaintenanceForm",
                input: "housekeepingMaintenanceId",
                title: "Xác nhận chuyển phòng sang Bảo trì",
                message: `Bạn có chắc muốn chuyển ${name} sang Bảo trì không?`,
                note: "Phòng sẽ được chuyển thành Status = 4 (Bảo trì) và IsActive = 0.",
                confirmText: "Chuyển Bảo trì",
                variant: "danger",
                icon: "bi-tools"
            },
            "housekeeping-clean": {
                form: "housekeepingCleanForm",
                input: "housekeepingCleanId",
                title: "Xác nhận dọn dẹp phòng",
                message: `Bạn có chắc muốn dọn dẹp ${name} không?`,
                note: "Nếu xác nhận, phòng sẽ chuyển từ Chờ dọn dẹp (Status = 2) sang Đang dọn dẹp (Status = 3).",
                confirmText: "Dọn dẹp phòng",
                variant: "primary",
                icon: "bi-stars"
            },
            "housekeeping-finished": {
                form: "housekeepingCompleteForm",
                input: "housekeepingCompleteId",
                title: "Xác nhận đã dọn xong phòng",
                message: `Bạn có chắc đã dọn dẹp xong ${name} không?`,
                note: "Nếu xác nhận, phòng sẽ chuyển từ Đang dọn dẹp (Status = 3) về Trống (Status = 0).",
                confirmText: "Đã dọn xong",
                variant: "success",
                icon: "bi-check2-circle"
            },
            delete: {
                form: "roomDeleteForm",
                input: "roomDeleteId",
                title: "Xác nhận xóa phòng",
                message: `Bạn có chắc muốn xóa ${name} không?`,
                note: "Phòng sẽ được soft-delete và không còn hiển thị trong danh sách quản lý.",
                confirmText: "Xóa phòng",
                variant: "danger",
                icon: "bi-trash3"
            }
        }[action];

        if (!config) {
            return;
        }

        const form = setFormValue(config.form, config.input, id);
        if (!form) {
            showActionError("Lỗi giao diện", "Không tìm thấy form thao tác phòng.");
            return;
        }

        const onConfirm = () => executeAction(form, button);

        if (window.HotelUI && typeof window.HotelUI.showConfirm === "function") {
            window.HotelUI.showConfirm({
                title: config.title,
                message: config.message,
                note: config.note,
                confirmText: config.confirmText,
                variant: config.variant,
                icon: config.icon,
                onConfirm
            });

            // showConfirm() đồng bộ nội dung note trước khi mở modal,
            // nên chèn checkbox ngay sau lời gọi này.
            prepareMaintenanceOption(action === "deactivate");
            return;
        }

        prepareMaintenanceOption(false);

        if (window.confirm(config.message)) {
            onConfirm();
        }
    }

    document.addEventListener("click", function (event) {
        const target = event.target instanceof Element ? event.target : null;
        const button = target?.closest("[data-room-action]");

        if (!button) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();

        if (button.disabled) {
            if (button.dataset.roomAction === "activate") {
                showActionError(
                    "Chưa thể mở hoạt động",
                    "Loại phòng đang dừng hoạt động. Hãy mở hoạt động loại phòng trước."
                );
            }
            return;
        }

        confirmAction(
            button,
            button.dataset.roomAction
        );
    }, true);
})(window, document);

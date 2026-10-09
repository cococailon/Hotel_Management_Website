(() => {
    "use strict";

    if (window.__amenityIndexBound) {
        return;
    }

    window.__amenityIndexBound = true;

    const currentUrl = () =>
        window.location.pathname + window.location.search;

    async function postAction(form) {
        if (!form) {
            throw new Error("Không tìm thấy form thao tác tiện nghi.");
        }

        if (!window.HotelAjax || typeof window.HotelAjax.navigate !== "function") {
            HTMLFormElement.prototype.submit.call(form);
            return true;
        }

        return await window.HotelAjax.navigate(
            form.action,
            {
                method: "POST",
                body: new FormData(form),
                historyMode: "replace",
                preserveScroll: true,
                showErrorToast: false
            });
    }

    function setId(formId, inputId, id) {
        const form = document.getElementById(formId);
        const input = document.getElementById(inputId);

        if (input) {
            input.value = id || "";
        }

        return form;
    }

    function showError(message) {
        window.HotelUI?.toast(
            "warning",
            "Không thể thực hiện",
            message
        );
    }

    function showActionConfirm(button, action) {
        const id = button.dataset.id;
        const name = button.dataset.name || "tiện nghi này";
        const allowed = button.dataset.allowed !== "false";

        if (!id) {
            showError("Không tìm thấy mã tiện nghi.");
            return;
        }

        if (!allowed) {
            showError(
                action === "deactivate"
                    ? "Không thể dừng hoạt động vì tiện nghi đang được sử dụng bởi loại phòng có Booking hoạt động."
                    : action === "activate"
                        ? "Không thể mở hoạt động vì tiện nghi đang được sử dụng bởi loại phòng có Booking hoạt động."
                        : "Không thể xóa vì tiện nghi đang được sử dụng bởi loại phòng có Booking hoạt động."
            );
            return;
        }

        const actionMap = {
            deactivate: {
                formId: "amenityDeactivateForm",
                inputId: "amenityDeactivateId",
                title: "Xác nhận dừng hoạt động",
                message: `Bạn có chắc muốn dừng hoạt động ${name} không?`,
                note: "Sau khi dừng, toàn bộ Tiện nghi của Loại phòng tương ứng đang hoạt động sẽ được chuyển sang dừng hoạt động.",
                confirmText: "Dừng hoạt động",
                variant: "warning"
            },
            delete: {
                formId: "amenityDeleteForm",
                inputId: "amenityDeleteId",
                title: "Xác nhận xóa tiện nghi",
                message: `Bạn có chắc muốn xóa ${name} không?`,
                note: "Chỉ tiện nghi đã dừng hoạt động và loại phòng của nó không nằm trong lịch đặt phòng có trạng thái là chờ xác nhận, chờ check in hoặc đang ở thì mới được xóa.",
                confirmText: "Xóa tiện nghi",
                variant: "danger"
            }
        };

        const config = actionMap[action];
        if (!config) {
            return;
        }

        const form = setId(config.formId, config.inputId, id);
        if (!form) {
            showError("Không tìm thấy form thao tác tiện nghi.");
            return;
        }

        window.HotelUI?.showConfirm({
            title: config.title,
            message: config.message,
            note: config.note,
            confirmText: config.confirmText,
            variant: config.variant,
            onConfirm: async () => {
                try {
                    const success = await postAction(form);
                    if (!success) {
                        showError("Không thể hoàn tất thao tác. Vui lòng thử lại.");
                    }
                }
                catch (error) {
                    console.error("[Amenity] Action failed:", error);
                    showError("Không thể hoàn tất thao tác. Vui lòng thử lại.");
                }
            }
        });
    }

    function showActivateModal(button) {
        const id = button.dataset.id;
        const name = button.dataset.name || "tiện nghi này";
        const allowed = button.dataset.allowed !== "false";

        if (!id) {
            showError("Không tìm thấy mã tiện nghi.");
            return;
        }

        if (!allowed) {
            showError("Không thể mở hoạt động vì tiện nghi đang được sử dụng bởi loại phòng có Booking hoạt động.");
            return;
        }

        const form = setId(
            "amenityActivateForm",
            "amenityActivateId",
            id
        );

        const modalElement = document.getElementById("amenityActivateModal");
        const message = document.getElementById("amenityActivateMessage");
        const checkbox = document.getElementById("amenityReactivateAssociationsCheckbox");

        if (!form || !modalElement || !message || !checkbox || !window.bootstrap?.Modal) {
            showError("Không thể mở hộp xác nhận. Vui lòng tải lại trang.");
            return;
        }

        message.textContent = `Bạn có chắc muốn mở hoạt động ${name} không?`;
        checkbox.checked = false;

        const modal = window.bootstrap.Modal.getOrCreateInstance(modalElement);
        modal.show();
    }

    document.addEventListener("click", event => {
        const target = event.target instanceof Element
            ? event.target.closest("[data-amenity-action]")
            : null;

        if (!target) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();

        const action = target.dataset.amenityAction;

        if (action === "activate") {
            showActivateModal(target);
            return;
        }

        showActionConfirm(target, action);
    }, true);

    document.addEventListener("click", async event => {
        const target = event.target instanceof Element
            ? event.target.closest("#amenityActivateConfirmButton")
            : null;

        if (!target) {
            return;
        }

        event.preventDefault();

        const form = document.getElementById("amenityActivateForm");
        const checkbox = document.getElementById("amenityReactivateAssociationsCheckbox");
        const hidden = document.getElementById("amenityReactivateAssociations");
        const modalElement = document.getElementById("amenityActivateModal");

        if (!form || !checkbox || !hidden) {
            showError("Không tìm thấy dữ liệu xác nhận mở hoạt động.");
            return;
        }

        hidden.value = checkbox.checked ? "true" : "false";

        const modal = modalElement && window.bootstrap?.Modal
            ? window.bootstrap.Modal.getOrCreateInstance(modalElement)
            : null;

        if (modal) {
            modal.hide();
        }

        try {
            const success = await postAction(form);
            if (!success) {
                showError("Không thể hoàn tất thao tác. Vui lòng thử lại.");
            }
        }
        catch (error) {
            console.error("[Amenity] Activate failed:", error);
            showError("Không thể hoàn tất thao tác. Vui lòng thử lại.");
        }
    }, true);
})();

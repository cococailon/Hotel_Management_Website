(() => {
    "use strict";
    if (window.__serviceIndexBound) return;
    window.__serviceIndexBound = true;

    function error(message) {
        window.HotelUI?.toast("warning", "Không thể thực hiện", message);
    }

    async function postAction(form) {
        if (!window.HotelAjax || typeof window.HotelAjax.navigate !== "function") {
            HTMLFormElement.prototype.submit.call(form);
            return true;
        }
        return await window.HotelAjax.navigate(form.action, {
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
        if (input) input.value = id || "";
        return form;
    }

    function confirmAction(button, action) {
        const id = button.dataset.id;
        const name = button.dataset.name || "dịch vụ này";
        if (!id) return error("Không tìm thấy mã dịch vụ.");
        if (button.dataset.allowed === "false") {
            return error(action === "delete" ? "Không thể xóa vì dịch vụ đang được sử dụng bởi loại phòng nằm trong lịch đặt phòng có trạng thái chờ xác nhận, chờ check in hoặc đang ở." : "Không thể thực hiện vì dịch vụ đang được sử dụng bởi loại phòng có Booking hoạt động.");
        }

        const map = {
            deactivate: { form: "serviceDeactivateForm", input: "serviceDeactivateId", title: "Xác nhận dừng hoạt động", message: `Bạn có chắc muốn dừng hoạt động ${name} không?`, note: "Toàn bộ Dịch vụ trong loại phòng tương ứng đang hoạt động sẽ được chuyển sang dừng hoạt động.", text: "Dừng hoạt động", variant: "warning" },
            delete: { form: "serviceDeleteForm", input: "serviceDeleteId", title: "Xác nhận xóa dịch vụ", message: `Bạn có chắc muốn xóa ${name} không?`, note: "Chỉ dịch vụ đã dừng hoạt động và loại phòng của nó không nằm trong các lịch đặt phòng có trạng thái chờ xác nhận, chờ check in hoặc đang ở thì mới được xóa.", text: "Xóa dịch vụ", variant: "danger" }
        };
        const cfg = map[action];
        if (!cfg) return;
        const form = setId(cfg.form, cfg.input, id);
        if (!form) return error("Không tìm thấy form thao tác dịch vụ.");
        window.HotelUI?.showConfirm({
            title: cfg.title,
            message: cfg.message,
            note: cfg.note,
            confirmText: cfg.text,
            variant: cfg.variant,
            onConfirm: async () => {
                try {
                    const ok = await postAction(form);
                    if (!ok) error("Không thể hoàn tất thao tác. Vui lòng thử lại.");
                } catch (e) {
                    console.error("[Service] Action failed:", e);
                    error("Không thể hoàn tất thao tác. Vui lòng thử lại.");
                }
            }
        });
    }

    function showActivate(button) {
        const id = button.dataset.id;
        const name = button.dataset.name || "dịch vụ này";
        if (!id) return error("Không tìm thấy mã dịch vụ.");
        if (button.dataset.allowed === "false") return error("Không thể mở hoạt động vì dịch vụ đang được sử dụng bởi loại phòng có Booking hoạt động.");
        const form = setId("serviceActivateForm", "serviceActivateId", id);
        const modalElement = document.getElementById("serviceActivateModal");
        const message = document.getElementById("serviceActivateMessage");
        const checkbox = document.getElementById("serviceReactivateAssociationsCheckbox");
        if (!form || !modalElement || !message || !checkbox || !window.bootstrap?.Modal) return error("Không thể mở hộp xác nhận. Vui lòng tải lại trang.");
        message.textContent = `Bạn có chắc muốn mở hoạt động ${name} không?`;
        checkbox.checked = false;
        window.bootstrap.Modal.getOrCreateInstance(modalElement).show();
    }

    document.addEventListener("click", event => {
        const target = event.target instanceof Element ? event.target.closest("[data-service-action]") : null;
        if (!target) return;
        event.preventDefault();
        event.stopPropagation();
        if (target.dataset.serviceAction === "activate") showActivate(target);
        else confirmAction(target, target.dataset.serviceAction);
    }, true);

    document.addEventListener("click", async event => {
        const target = event.target instanceof Element ? event.target.closest("#serviceActivateConfirmButton") : null;
        if (!target) return;
        event.preventDefault();
        const form = document.getElementById("serviceActivateForm");
        const checkbox = document.getElementById("serviceReactivateAssociationsCheckbox");
        const hidden = document.getElementById("serviceReactivateAssociations");
        const modalElement = document.getElementById("serviceActivateModal");
        if (!form || !checkbox || !hidden) return error("Không tìm thấy dữ liệu xác nhận mở hoạt động.");
        hidden.value = checkbox.checked ? "true" : "false";
        if (modalElement && window.bootstrap?.Modal) window.bootstrap.Modal.getOrCreateInstance(modalElement).hide();
        try {
            const ok = await postAction(form);
            if (!ok) error("Không thể hoàn tất thao tác. Vui lòng thử lại.");
        } catch (e) {
            console.error("[Service] Activate failed:", e);
            error("Không thể hoàn tất thao tác. Vui lòng thử lại.");
        }
    }, true);
})();

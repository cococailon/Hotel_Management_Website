(() => {
    "use strict";

    if (window.__roomTypeIndexBound) return;
    window.__roomTypeIndexBound = true;

    const getCurrentUrl = () => window.location.pathname + window.location.search;

    async function postForm(form) {
        if (!form) return false;

        if (!window.HotelAjax || typeof window.HotelAjax.navigate !== "function") {
            HTMLFormElement.prototype.submit.call(form);
            return true;
        }

        const body = new FormData(form);
        return await window.HotelAjax.navigate(form.action, {
            method: "POST",
            body,
            historyMode: "replace",
            preserveScroll: false,
            showErrorToast: false
        });
    }

    function resetActionState(form, id) {
        const input = form?.querySelector('input[name="id"]');
        if (input) input.value = id || "";
        const returnUrl = form?.querySelector('input[name="returnUrl"]');
        if (returnUrl) returnUrl.value = getCurrentUrl();
    }

    document.addEventListener("click", function (event) {
        const button = event.target instanceof Element
            ? event.target.closest("[data-roomtype-action]")
            : null;

        if (!button) return;

        const action = button.dataset.roomtypeAction;
        const id = button.dataset.id;
        const name = button.dataset.name || "loại phòng này";
        const allowed = button.dataset.allowed !== "false";

        if (action === "delete" && !allowed) {
            event.preventDefault();
            window.HotelUI?.toast(
                "warning",
                "Chưa thể xóa",
                "Loại phòng phải dừng hoạt động, không có Booking đang hoạt động và không còn phòng chưa xóa được gán."
            );
            return;
        }

        if (action === "deactivate" && !allowed) {
            event.preventDefault();
            window.HotelUI?.toast(
                "warning",
                "Chưa thể dừng hoạt động",
                "Không thể dừng loại phòng khi đang có Booking ở trạng thái Chờ xác nhận, Chờ check in hoặc Đang ở."
            );
            return;
        }

        if (action === "delete") {
            event.preventDefault();
            const form = document.getElementById("roomTypeDeleteForm");
            resetActionState(form, id);
            window.HotelUI?.showConfirm({
                title: "Xác nhận xóa loại phòng",
                message: `Bạn có chắc muốn xóa ${name} không?`,
                note: "Chỉ loại phòng đã dừng hoạt động và không còn Booking/phòng chưa xóa mới được xóa. Các quan hệ tiện nghi và dịch vụ của loại phòng sẽ được đánh dấu đã xóa.",
                confirmText: "Xóa loại phòng",
                variant: "danger",
                icon: "bi-trash3",
                onConfirm: () => postForm(form)
            });
            return;
        }

        if (action === "deactivate") {
            event.preventDefault();
            const form = document.getElementById("roomTypeDeactivateForm");
            resetActionState(form, id);
            window.HotelUI?.showConfirm({
                title: "Xác nhận dừng hoạt động",
                message: `Bạn có chắc muốn dừng hoạt động ${name} không?`,
                note: "Các phòng đang hoạt động thuộc loại phòng này cũng sẽ được chuyển sang dừng hoạt động.",
                confirmText: "Dừng hoạt động",
                variant: "warning",
                icon: "bi-pause-circle",
                onConfirm: () => postForm(form)
            });
            return;
        }

        if (action === "activate") {
            event.preventDefault();
            const form = document.getElementById("roomTypeActivateForm");
            resetActionState(form, id);

            const message = document.getElementById("roomTypeActivateMessage");
            if (message) {
                message.textContent = `Bạn có chắc muốn mở hoạt động ${name} không?`;
            }

            const checkbox = document.getElementById("roomTypeReopenRoomsCheckbox");
            if (checkbox) checkbox.checked = false;

            const modalElement = document.getElementById("roomTypeActivateModal");
            if (!modalElement || !window.bootstrap?.Modal) return;

            const modal = window.bootstrap.Modal.getOrCreateInstance(modalElement);
            modal.show();
        }
    });

    document.addEventListener("click", function (event) {
        const button = event.target instanceof Element
            ? event.target.closest("#roomTypeActivateConfirmButton")
            : null;

        if (!button) return;

        const form = document.getElementById("roomTypeActivateForm");
        const checkbox = document.getElementById("roomTypeReopenRoomsCheckbox");
        const hidden = document.getElementById("roomTypeReopenRooms");
        if (hidden) hidden.value = checkbox?.checked ? "true" : "false";

        const modalElement = document.getElementById("roomTypeActivateModal");
        const modal = modalElement && window.bootstrap?.Modal
            ? window.bootstrap.Modal.getOrCreateInstance(modalElement)
            : null;

        if (modal) modal.hide();
        postForm(form);
    });
})();

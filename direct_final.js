
(() => {
    "use strict";

    const form = document.getElementById("bookingDirectCreateForm");
    if (!form) return;

    const catalog = {};
    const assignmentContainer = document.getElementById("directRoomAssignmentContainer");
    const depositEl = document.getElementById("directDepositAmount");
    const summaryDepositEl = document.getElementById("directSummaryDeposit");
    const estimateEl = document.getElementById("directEstimatedAmount");
    const assignmentCountEl = document.getElementById("directAssignmentCount");

    const money = value => new Intl.NumberFormat("vi-VN").format(Math.round(Number(value || 0))) + " ₫";

    const getNights = () => {
        const end = document.getElementById("directCheckOutDate")?.value;
        if (!end) return 0;
        const start = new Date(new Date().toISOString().slice(0,10) + "T00:00:00");
        const checkout = new Date(end + "T00:00:00");
        const days = Math.round((checkout - start) / 86400000);
        return days > 0 ? days : 0;
    };



    const buildAssignments = () => {
        const current = [...assignmentContainer.querySelectorAll("[data-assignment-row]")].map(row => ({
            typeId: row.dataset.roomTypeId,
            roomId: row.querySelector(".direct-room-select")?.value || "",
            adults: row.querySelector(".direct-room-adults")?.value || "0",
            children: row.querySelector(".direct-room-children")?.value || "0",
            note: row.querySelector("textarea")?.value || ""
        }));

        assignmentContainer.replaceChildren();
        let index = 0;

        form.querySelectorAll("[data-direct-room-type-row]").forEach(typeRow => {
            const typeId = typeRow.dataset.directRoomTypeRow;
            const qty = Math.max(0, Number(typeRow.querySelector(".direct-quantity")?.value || 0));
            if (qty <= 0) return;

            const catalogRows = catalog[typeId] || [];
            const oldRows = current.filter(x => x.typeId === typeId);

            for (let i = 0; i < qty; i++) {
                const old = oldRows[i] || {};
                const optionHtml = [
                    '<option value="">-- Chọn phòng --</option>',
                    ...catalogRows.map(room => `<option value="${room.id}" ${String(old.roomId) === String(room.id) ? "selected" : ""}>${escapeHtml(room.code)} - ${escapeHtml(room.name)} - ${escapeHtml(room.typeName)}</option>`)
                ].join("");

                const card = document.createElement("div");
                card.className = "direct-assignment-card";
                card.dataset.assignmentRow = "true";
                card.dataset.roomTypeId = typeId;
                card.innerHTML = `
                    <input type="hidden" name="RoomAssignments[${index}].RoomTypeId" value="${typeId}">
                    <div class="d-flex flex-column flex-lg-row justify-content-between gap-2 mb-3">
                        <div><div class="fw-semibold">${escapeHtml(typeRow.querySelector("td:first-child .fw-semibold")?.textContent || "")}</div>
                        <div class="small text-body-secondary">BookingRoom ${i + 1} · Tối đa ${Number(typeRow.querySelector("td:first-child .small")?.textContent.match(/(\d+) NL/)?.[1] || 0)} người lớn</div></div>
                    </div>
                    <div class="row g-3">
                        <div class="col-12 col-lg-6">
                            <label class="form-label fw-semibold">Phòng thực tế</label>
                            <select class="form-select direct-room-select" name="RoomAssignments[${index}].RoomId" required>${optionHtml}</select>
                        </div>
                        <div class="col-6 col-lg-3">
                            <label class="form-label fw-semibold">Người lớn</label>
                            <input class="form-control direct-room-adults" type="number" min="0" max="${Number(typeRow.dataset.maxAdults || 9999)}" name="RoomAssignments[${index}].Adults" value="${escapeAttr(old.adults || 0)}">
                        </div>
                        <div class="col-6 col-lg-3">
                            <label class="form-label fw-semibold">Trẻ em</label>
                            <input class="form-control direct-room-children" type="number" min="0" max="${Number(typeRow.dataset.maxChildren || 9999)}" name="RoomAssignments[${index}].Children" value="${escapeAttr(old.children || 0)}">
                        </div>
                        <div class="col-12">
                            <label class="form-label fw-semibold">Ghi chú BookingRoom</label>
                            <textarea class="form-control" rows="2" maxlength="255" name="RoomAssignments[${index}].Note">${escapeHtml(old.note || "")}</textarea>
                        </div>
                    </div>`;
                assignmentContainer.appendChild(card);
                index++;
            }
        });

        if (index === 0) {
            const empty = document.createElement("div");
            empty.className = "direct-assignment-empty text-body-secondary";
            empty.textContent = "Chưa có BookingRoom. Hãy chọn số phòng ở bảng phía trên.";
            assignmentContainer.appendChild(empty);
        }
        assignmentCountEl.textContent = String(index);
    };

    const htmlEscapes = {"&":"&amp;","<":"&lt;",">":"&gt;","'":"&#39;",'"':"&quot;"};
    const escapeHtml = value => String(value ?? "").replace(/[&<>"']/g, c => htmlEscapes[c]);
    const escapeAttr = escapeHtml;

    const updateMoney = () => {
        const nights = getNights();
        let amount = 0;
        let deposit = 0;
        form.querySelectorAll("[data-direct-room-type-row]").forEach(row => {
            const qty = Math.max(0, Number(row.querySelector(".direct-quantity")?.value || 0));
            const price = Number(row.dataset.price || 0);
            const depositPercent = Number(row.dataset.deposit || 0);
            amount += qty * price * nights;
            deposit += qty * price * nights * depositPercent / 100;
        });
        estimateEl.textContent = money(amount);
        depositEl.textContent = money(deposit);
        summaryDepositEl.textContent = money(deposit);
    };


    const syncDepositFields = () => {
        const deposit = Number((summaryDepositEl?.textContent || "0").replace(/[^0-9]/g, ""));
        const method = document.getElementById("directPaymentMethod");
        const date = document.getElementById("directPaymentDate");
        const reference = document.getElementById("directReferenceCode");
        if (deposit <= 0) {
            if (method) method.value = "";
            if (date) date.value = "";
            if (reference) reference.value = "";
            const confirm = document.getElementById("directDepositConfirmed");
            if (confirm) confirm.checked = false;
        }
    };

    form.querySelectorAll(".direct-quantity").forEach(input => input.addEventListener("change", () => { buildAssignments(); updateMoney(); syncDepositFields(); }));
    document.getElementById("directCheckOutDate")?.addEventListener("change", () => { updateMoney(); syncDepositFields(); });
    updateMoney();
    syncDepositFields();
    const hasServerAssignments = false;
    if (!hasServerAssignments) {
        buildAssignments();
    } else {
        assignmentCountEl.textContent = String(assignmentContainer.querySelectorAll("[data-assignment-row]").length);
    }

    const guestSelect = document.getElementById("directGuestId");
    const setValue = (id, value) => { const el = document.getElementById(id); if (el) el.value = value ?? ""; };
    const clearInlineError = id => { const el = document.getElementById(id); if (el) el.textContent = ""; };
    const setInlineError = (id, message) => { const el = document.getElementById(id); if (el) el.textContent = message || ""; };
    const setDirectTopError = message => {
        const box = document.getElementById("directClientTopError");
        const list = document.getElementById("directClientTopErrorList");
        if (!box || !list) return;
        list.replaceChildren();
        const li = document.createElement("li");
        li.textContent = message;
        list.appendChild(li);
        box.classList.remove("d-none");
        box.scrollIntoView({ behavior: "smooth", block: "start" });
    };
    const clearDirectTopError = () => document.getElementById("directClientTopError")?.classList.add("d-none");
    const clearNewGuestFields = () => {
        const autoCode = guestSelect?.dataset.generatedCode || "";
        setValue("directGuestCode", autoCode);
        setValue("directGuestName", "");
        setValue("directGuestDob", "");
        setValue("directGuestSex", "");
        setValue("directGuestAddress", "");
        setValue("directGuestPhone", "");
        setValue("directGuestEmail", "");
        setValue("directGuestIdentityNumber", "");
        setValue("directGuestIdentityType", "");
        setValue("directGuestNote", "");
    };
    guestSelect?.addEventListener("change", () => {
        const option = guestSelect.selectedOptions[0];
        if (!option || !option.value) {
            clearNewGuestFields();
            return;
        }
        setValue("directGuestCode", option.dataset.code);
        setValue("directGuestName", option.dataset.name);
        setValue("directGuestDob", option.dataset.dob);
        setValue("directGuestSex", option.dataset.sex);
        setValue("directGuestAddress", option.dataset.address);
        setValue("directGuestPhone", option.dataset.phone);
        setValue("directGuestEmail", option.dataset.email);
        setValue("directGuestIdentityNumber", option.dataset.identityNumber);
        setValue("directGuestIdentityType", option.dataset.identityType);
        setValue("directGuestNote", option.dataset.note);
    });

    const syncReferenceVisibility = () => {
        const method = document.getElementById("directPaymentMethod");
        const reference = document.getElementById("directReferenceCode");
        const group = document.getElementById("directReferenceGroup");
        const isTransfer = method?.value === "1";
        if (group) group.classList.toggle("d-none", !isTransfer);
        if (reference) {
            reference.required = isTransfer;
            if (!isTransfer) reference.value = "";
        }
        if (!isTransfer) clearInlineError("directReferenceCodeClientError");
    };
    document.getElementById("directPaymentMethod")?.addEventListener("change", syncReferenceVisibility);
    form.querySelectorAll(".direct-quantity").forEach(input => input.addEventListener("change", syncReferenceVisibility));
    document.getElementById("directCheckOutDate")?.addEventListener("change", syncReferenceVisibility);
    document.getElementById("directPaymentDate")?.addEventListener("input", () => clearInlineError("directPaymentDateClientError"));
    document.getElementById("directPaymentMethod")?.addEventListener("change", () => clearInlineError("directPaymentMethodClientError"));
    document.getElementById("directReferenceCode")?.addEventListener("input", () => clearInlineError("directReferenceCodeClientError"));
    document.getElementById("directDepositConfirmed")?.addEventListener("change", () => clearInlineError("directDepositConfirmedClientError"));
    syncReferenceVisibility();

    const directCreateConfirmModalEl = document.getElementById("directCreateConfirmModal");
    const directCreateConfirmSubmit = document.getElementById("directCreateConfirmSubmit");

    let directClientValidationRunning = false;

    const openDirectConfirmModal = () => {
        if (!directCreateConfirmModalEl || typeof bootstrap === "undefined" || !bootstrap.Modal) {
            // Fallback cuối cùng: native confirm không phụ thuộc callback UI.
            if (window.confirm("Bạn có chắc chắn muốn tạo Booking và check in ngay cho khách hàng không?")) {
                const confirmationInput = document.getElementById("directFinalConfirmation");
                if (confirmationInput) confirmationInput.value = "true";
                form.dataset.directNativeConfirmed = "true";
                form.requestSubmit();
            }
            return;
        }

        bootstrap.Modal.getOrCreateInstance(directCreateConfirmModalEl).show();
    };

    const runDirectSubmitValidation = () => {
        if (directClientValidationRunning) return;
        directClientValidationRunning = true;

        try {
            clearDirectTopError();
            clearInlineError("directPaymentMethodClientError");
            clearInlineError("directPaymentDateClientError");
            clearInlineError("directReferenceCodeClientError");
            clearInlineError("directDepositConfirmedClientError");

            // 1) Validate annotation / required / format / range trước.
            const invalidField = form.querySelector(":invalid");
            if (invalidField) {
                invalidField.reportValidity();
                invalidField.scrollIntoView({ behavior: "smooth", block: "center" });
                invalidField.focus({ preventScroll: true });
                return;
            }

            // 2) Validate phân phòng / phân người.
            const totalAdults = Number(document.getElementById("directTotalAdults")?.value || 0);
            const totalChildren = Number(document.getElementById("directTotalChildren")?.value || 0);
            const assignmentRows = [...assignmentContainer.querySelectorAll("[data-assignment-row]")];
            const assignedAdults = assignmentRows.reduce((sum, row) => sum + Number(row.querySelector(".direct-room-adults")?.value || 0), 0);
            const assignedChildren = assignmentRows.reduce((sum, row) => sum + Number(row.querySelector(".direct-room-children")?.value || 0), 0);

            if (assignmentRows.length === 0) {
                setDirectTopError("Vui lòng chọn ít nhất một phòng.");
                return;
            }

            if (assignedAdults !== totalAdults || assignedChildren !== totalChildren) {
                setDirectTopError(`Phân bổ người chưa khớp: người lớn ${assignedAdults}/${totalAdults}; trẻ em ${assignedChildren}/${totalChildren}.`);
                return;
            }

            // 3) Validate tiền cọc sau khi các field cơ bản đã hợp lệ.
            const deposit = Number((summaryDepositEl?.textContent || "0").replace(/[^0-9]/g, ""));
            const paymentMethod = document.getElementById("directPaymentMethod");
            const paymentDate = document.getElementById("directPaymentDate");
            const referenceInput = document.getElementById("directReferenceCode");
            const depositConfirmed = document.getElementById("directDepositConfirmed");

            if (deposit > 0) {
                if (!paymentMethod?.value) {
                    setInlineError("directPaymentMethodClientError", "Vui lòng chọn phương thức thanh toán tiền cọc.");
                    paymentMethod?.focus();
                    return;
                }
                if (!paymentDate?.value) {
                    setInlineError("directPaymentDateClientError", "Vui lòng chọn thời gian thanh toán tiền cọc.");
                    paymentDate?.focus();
                    return;
                }
                const payment = new Date(paymentDate.value);
                if (payment > new Date()) {
                    setInlineError("directPaymentDateClientError", "Thời gian thanh toán không được lớn hơn thời điểm hiện tại.");
                    paymentDate?.focus();
                    return;
                }
                if (paymentMethod.value === "1" && !referenceInput?.value.trim()) {
                    setInlineError("directReferenceCodeClientError", "Mã giao dịch là bắt buộc khi thanh toán chuyển khoản.");
                    referenceInput?.focus();
                    return;
                }
                if (!depositConfirmed?.checked) {
                    setInlineError("directDepositConfirmedClientError", "Vui lòng xác nhận đã nhận tiền cọc.");
                    depositConfirmed?.focus();
                    return;
                }
            }

            // Chỉ sau khi mọi validation cơ bản/nghiệp vụ đều hợp lệ mới mở modal.
            openDirectConfirmModal();
        } finally {
            directClientValidationRunning = false;
        }
    };

    // Chỉ submit khi đã xác nhận ở modal. Nếu JS lỗi hoàn toàn, nút vẫn là submit button
    // và server sẽ trả lỗi xác nhận thay vì im lặng không làm gì.
    form.addEventListener("submit", event => {
        if (form.dataset.directNativeConfirmed === "true") {
            delete form.dataset.directNativeConfirmed;
            return;
        }

        event.preventDefault();
        runDirectSubmitValidation();
    });

    // Nếu người dùng Enter, form submit sẽ đi qua cùng handler ở trên.
    directCreateConfirmSubmit?.addEventListener("click", () => {
        const confirmationInput = document.getElementById("directFinalConfirmation");
        if (confirmationInput) confirmationInput.value = "true";
        form.dataset.directNativeConfirmed = "true";
    });
})();

/* =========================================================
   HOTEL MANAGEMENT UI SYSTEM v1.0
   ========================================================= */

(function (window, document) {

    "use strict";


    // =========================================================
    // HOTEL UI
    // =========================================================

    const HotelUI = {

        _confirmAction: null,


        // =====================================================
        // TOAST
        // =====================================================

        toast: function (
            type,
            title,
            message,
            delay = 4000
        ) {

            const host =
                document.getElementById(
                    "uiToastHost"
                );


            if (!host) {
                return;
            }


            const config =
                {
                    success: {
                        icon:
                            "bi-check-circle-fill",

                        className:
                            "text-bg-success"
                    },

                    error: {
                        icon:
                            "bi-x-circle-fill",

                        className:
                            "text-bg-danger"
                    },

                    warning: {
                        icon:
                            "bi-exclamation-triangle-fill",

                        className:
                            "text-bg-warning"
                    },

                    info: {
                        icon:
                            "bi-info-circle-fill",

                        className:
                            "text-bg-info"
                    }

                }[type]
                ??
                {
                    icon:
                        "bi-info-circle-fill",

                    className:
                        "text-bg-info"
                };


            const el =
                document.createElement(
                    "div"
                );


            el.className =
                `toast ${config.className} border-0`;


            el.setAttribute(
                "role",
                "alert"
            );


            el.setAttribute(
                "aria-live",
                "assertive"
            );


            el.setAttribute(
                "aria-atomic",
                "true"
            );


            el.innerHTML = `

                <div class="d-flex">

                    <div class="toast-body">

                        <div class="fw-semibold">

                            <i
                                class="bi ${config.icon} me-1"
                                aria-hidden="true">
                            </i>

                            ${HotelUI.escapeHtml(title)}

                        </div>

                        ${message
                    ? `
                                    <div class="mt-1">
                                        ${HotelUI.escapeHtml(message)}
                                    </div>
                                  `
                    : ""
                }

                    </div>


                    <button
                        type="button"
                        class="btn-close btn-close-white me-2 m-auto"
                        data-bs-dismiss="toast"
                        aria-label="Đóng">
                    </button>

                </div>
            `;


            host.appendChild(
                el
            );


            const toast =
                bootstrap.Toast.getOrCreateInstance(
                    el,
                    {
                        delay
                    }
                );


            el.addEventListener(
                "hidden.bs.toast",
                function () {

                    el.remove();

                }
            );


            toast.show();

        },


        // =====================================================
        // FLASH MESSAGES
        // =====================================================

        processFlashMessages: function (root = document) {

            const source = root?.getElementById
                ? root.getElementById("uiFlashMessages")
                : null;

            if (!source) {
                return;
            }

            const successMessage = source.dataset.successMessage?.trim();
            const errorMessage = source.dataset.errorMessage?.trim();

            if (successMessage) {
                HotelUI.toast(
                    "success",
                    "Thành công",
                    successMessage
                );
            }

            if (errorMessage) {
                HotelUI.toast(
                    "error",
                    "Có lỗi xảy ra",
                    errorMessage
                );
            }

        },


        // =====================================================
        // LOADING OVERLAY
        // =====================================================

        loadingOverlay: {

            show: function (
                text = "Đang xử lý..."
            ) {

                const el =
                    document.getElementById(
                        "uiLoadingOverlay"
                    );


                if (!el) {
                    return;
                }


                const label =
                    el.querySelector(
                        "[data-ui-loading-text]"
                    );


                if (label) {

                    label.textContent =
                        text;

                }


                el.classList.remove(
                    "d-none"
                );


                el.setAttribute(
                    "aria-hidden",
                    "false"
                );

            },


            hide: function () {

                const el =
                    document.getElementById(
                        "uiLoadingOverlay"
                    );


                if (!el) {
                    return;
                }


                el.classList.add(
                    "d-none"
                );


                el.setAttribute(
                    "aria-hidden",
                    "true"
                );

            }

        },


        // =====================================================
        // BUTTON LOADING
        // =====================================================

        setButtonLoading: function (
            button,
            loading,
            loadingText = "Đang xử lý..."
        ) {

            if (!button) {
                return;
            }


            const content =
                button.querySelector(
                    ".ui-button-content"
                );


            const spinner =
                button.querySelector(
                    ".ui-button-loading"
                );


            if (loading) {

                /*
                 * Lưu trạng thái disabled ban đầu.
                 */

                button.dataset.originalDisabled =
                    button.disabled
                        ? "true"
                        : "false";


                button.disabled =
                    true;


                if (content) {

                    content.classList.add(
                        "d-none"
                    );

                }


                if (spinner) {

                    spinner.classList.remove(
                        "d-none"
                    );


                    /*
                     * Cập nhật text loading nếu spinner
                     * có text node.
                     */

                    const textNode =
                        Array.from(
                            spinner.childNodes
                        )
                            .find(
                                node =>
                                    node.nodeType ===
                                    Node.TEXT_NODE &&
                                    node.textContent.trim()
                            );


                    if (textNode) {

                        textNode.textContent =
                            ` ${loadingText}`;

                    }

                }
                else {

                    /*
                     * Nếu không có spinner thì thay text
                     * trực tiếp.
                     */

                    button.dataset.originalText =
                        button.textContent.trim();


                    button.textContent =
                        loadingText;

                }

            }
            else {

                /*
                 * Khôi phục trạng thái disabled ban đầu.
                 */

                button.disabled =
                    button.dataset.originalDisabled ===
                    "true";


                if (content) {

                    content.classList.remove(
                        "d-none"
                    );

                }


                if (spinner) {

                    spinner.classList.add(
                        "d-none"
                    );

                }


                if (
                    !content &&
                    button.dataset.originalText
                ) {

                    button.textContent =
                        button.dataset.originalText;

                }


                delete button.dataset.originalDisabled;
                delete button.dataset.originalText;

            }

        },


        // =====================================================
        // LOCK FORM
        // =====================================================

        lockForm: function (
            form,
            lock = true
        ) {

            if (!form) {
                return;
            }


            form.dataset.uiBusy =
                lock
                    ? "true"
                    : "false";


            form
                .querySelectorAll(
                    "input, select, textarea"
                )
                .forEach(
                    control => {

                        if (lock) {

                            if (control.disabled) {
                                return;
                            }


                            control.dataset.uiWasReadonly =
                                control.readOnly
                                    ? "true"
                                    : "false";


                            if (
                                control.tagName === "INPUT" ||
                                control.tagName === "TEXTAREA"
                            ) {

                                control.readOnly =
                                    true;

                            }


                            if (
                                control.tagName === "SELECT"
                            ) {

                                control.dataset.uiWasDisabled =
                                    "false";

                                control.disabled =
                                    true;

                            }

                        }
                        else {

                            if (
                                control.dataset.uiWasReadonly ===
                                "false"
                            ) {

                                control.readOnly =
                                    false;

                            }


                            if (
                                control.dataset.uiWasDisabled ===
                                "false"
                            ) {

                                control.disabled =
                                    false;

                            }


                            delete control.dataset.uiWasReadonly;
                            delete control.dataset.uiWasDisabled;

                        }

                    }
                );

        },


        // =====================================================
        // SHOW CONFIRM
        // =====================================================

        showConfirm: function (
            options = {}
        ) {

            const modalElement =
                document.getElementById(
                    "uiConfirmModal"
                );


            const titleElement =
                document.getElementById(
                    "uiConfirmModalLabel"
                );


            const messageElement =
                document.getElementById(
                    "uiConfirmMessage"
                );


            const noteElement =
                document.getElementById(
                    "uiConfirmNote"
                );


            const confirmButton =
                document.getElementById(
                    "uiConfirmOkBtn"
                );


            const cancelButton =
                document.getElementById(
                    "uiConfirmCancelBtn"
                );


            if (
                !modalElement ||
                !titleElement ||
                !messageElement ||
                !noteElement ||
                !confirmButton ||
                !cancelButton
            ) {

                console.error(
                    "[HotelUI] Confirm modal elements not found."
                );

                return;

            }


            if (
                typeof bootstrap === "undefined" ||
                !bootstrap.Modal
            ) {

                console.error(
                    "[HotelUI] Bootstrap Modal is not available."
                );

                return;

            }


            // =================================================
            // RESET OLD STATE
            // =================================================

            HotelUI._confirmAction =
                null;


            HotelUI.setButtonLoading(
                confirmButton,
                false
            );


            cancelButton.disabled =
                false;


            // =================================================
            // CONTENT
            // =================================================

            titleElement.textContent =
                options.title ||
                "Xác nhận";


            messageElement.textContent =
                options.message ||
                "";


            if (options.note) {

                noteElement.textContent =
                    options.note;

                noteElement.classList.remove(
                    "d-none"
                );

            }
            else {

                noteElement.textContent =
                    "";

                noteElement.classList.add(
                    "d-none"
                );

            }


            // =================================================
            // CONFIRM BUTTON STYLE
            // =================================================

            confirmButton.classList.remove(
                "btn-danger",
                "btn-warning",
                "btn-primary",
                "btn-success",
                "btn-info",
                "btn-secondary"
            );


            const variant =
                options.variant ||
                "danger";


            confirmButton.classList.add(
                `btn-${variant}`
            );


            const content =
                confirmButton.querySelector(
                    ".ui-button-content"
                );


            if (content) {

                content.textContent =
                    options.confirmText ||
                    "Xác nhận";

            }


            // =================================================
            // SAVE ACTION
            // =================================================

            HotelUI._confirmAction =
                typeof options.onConfirm === "function"
                    ? options.onConfirm
                    : null;


            // =================================================
            // SHOW MODAL
            // =================================================

            const modal =
                bootstrap.Modal.getOrCreateInstance(
                    modalElement
                );


            modal.show();

        },


        // =====================================================
        // CLOSE CONFIRM
        // =====================================================

        closeConfirm: function () {

            const modalElement =
                document.getElementById(
                    "uiConfirmModal"
                );


            if (!modalElement) {

                HotelUI._confirmAction =
                    null;

                return;

            }


            if (
                typeof bootstrap === "undefined" ||
                !bootstrap.Modal
            ) {

                HotelUI._confirmAction =
                    null;

                return;

            }


            const modal =
                bootstrap.Modal.getInstance(
                    modalElement
                );


            if (modal) {

                modal.hide();

            }
            else {

                /*
                 * Trường hợp Bootstrap chưa tạo instance.
                 */

                modalElement.classList.remove(
                    "show"
                );

                modalElement.setAttribute(
                    "aria-hidden",
                    "true"
                );

                modalElement.style.display =
                    "none";

            }


            HotelUI._confirmAction =
                null;

        },


        // =====================================================
        // ESCAPE HTML
        // =====================================================

        escapeHtml: function (
            value
        ) {

            return String(
                value ?? ""
            )
                .replaceAll(
                    "&",
                    "&amp;"
                )
                .replaceAll(
                    "<",
                    "&lt;"
                )
                .replaceAll(
                    ">",
                    "&gt;"
                )
                .replaceAll(
                    '"',
                    "&quot;"
                )
                .replaceAll(
                    "'",
                    "&#039;"
                );

        }

    };


    // =========================================================
    // CONFIRM MODAL EVENTS
    // =========================================================
    //
    // CHỈ CÓ MỘT BỘ EVENT HANDLER.
    //
    // Không có document-level confirm handler thứ hai.
    // Không có DOMContentLoaded handler thứ hai.
    // =========================================================

    function bindConfirmModal() {

        const confirmButton =
            document.getElementById(
                "uiConfirmOkBtn"
            );


        const cancelButton =
            document.getElementById(
                "uiConfirmCancelBtn"
            );


        const modalElement =
            document.getElementById(
                "uiConfirmModal"
            );


        if (
            !confirmButton ||
            !cancelButton ||
            !modalElement
        ) {

            console.warn(
                "[HotelUI] Confirm modal could not be initialized."
            );

            return;

        }


        // =====================================================
        // CHỐNG BIND TRÙNG
        // =====================================================

        if (
            confirmButton.dataset.hotelUiBound ===
            "true"
        ) {

            return;

        }


        confirmButton.dataset.hotelUiBound =
            "true";


        // =====================================================
        // CONFIRM
        // =====================================================

        confirmButton.addEventListener(
            "click",
            async function (event) {

                event.preventDefault();
                event.stopPropagation();


                /*
                 * Lấy action hiện tại.
                 */

                const action =
                    HotelUI._confirmAction;


                if (
                    typeof action !== "function"
                ) {

                    return;

                }


                /*
                 * Chống double click.
                 */

                if (confirmButton.disabled) {

                    return;

                }


                /*
                 * XÓA ACTION NGAY.
                 *
                 * Không thể chạy lần thứ hai.
                 */

                HotelUI._confirmAction =
                    null;


                /*
                 * Hiển thị loading.
                 */

                HotelUI.setButtonLoading(
                    confirmButton,
                    true,
                    "Đang xử lý..."
                );


                cancelButton.disabled =
                    true;


                // =================================================
                // ĐÓNG MODAL
                // =================================================

                HotelUI.closeConfirm();


                // =================================================
                // CHẠY ACTION
                // =================================================

                try {

                    await action();

                }
                catch (error) {

                    console.error(
                        "[HotelUI] Confirm action failed:",
                        error
                    );

                }
                finally {

                    /*
                     * Khôi phục button.
                     *
                     * Nếu modal đã bị thay thế bởi Bootstrap,
                     * việc reset này vẫn an toàn.
                     */

                    HotelUI.setButtonLoading(
                        confirmButton,
                        false
                    );


                    cancelButton.disabled =
                        false;

                }

            }
        );


        // =====================================================
        // CANCEL
        // =====================================================

        cancelButton.addEventListener(
            "click",
            function () {

                HotelUI._confirmAction =
                    null;


                HotelUI.setButtonLoading(
                    confirmButton,
                    false
                );


                cancelButton.disabled =
                    false;

            }
        );


        // =====================================================
        // MODAL HIDDEN
        // =====================================================

        modalElement.addEventListener(
            "hidden.bs.modal",
            function () {

                /*
                 * Modal đã thực sự đóng.
                 */

                HotelUI._confirmAction =
                    null;


                HotelUI.setButtonLoading(
                    confirmButton,
                    false
                );


                cancelButton.disabled =
                    false;

            }
        );

    }


    // =========================================================
    // INIT
    // =========================================================

    if (
        document.readyState ===
        "loading"
    ) {

        document.addEventListener(
            "DOMContentLoaded",
            bindConfirmModal,
            {
                once: true
            }
        );

    }
    else {

        bindConfirmModal();

    }


    // =========================================================
    // GLOBAL
    // =========================================================

    window.HotelUI =
        HotelUI;


    // Xử lý flash message ở lần tải trang đầu tiên.
    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            () => {
                HotelUI.processFlashMessages(document);
            },
            { once: true }
        );
    }
    else {
        HotelUI.processFlashMessages(document);
    }


})(window, document);
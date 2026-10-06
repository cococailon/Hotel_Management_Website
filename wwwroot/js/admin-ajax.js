(() => {

    "use strict";


    // =========================================================
    // CONFIG
    // =========================================================

    const MAIN_CONTENT_ID =
        "app-main-content";


    const LOADING_ID =
        "admin-page-loading";


    const PAGE_STYLES_TEMPLATE_ID =
        "ajax-page-styles-template";


    const AJAX_HEADER =
        "X-Requested-With";


    const AJAX_HEADER_VALUE =
        "XMLHttpRequest";


    let isNavigating =
        false;



    // =========================================================
    // ELEMENTS
    // =========================================================

    function getMainContent() {

        return document.getElementById(
            MAIN_CONTENT_ID
        );

    }


    function getLoadingIndicator() {

        return document.getElementById(
            LOADING_ID
        );

    }



    // =========================================================
    // LOADING
    // =========================================================

    function setLoading(
        isLoading
    ) {

        const indicator =
            getLoadingIndicator();


        if (indicator) {

            indicator.classList.toggle(
                "is-visible",
                isLoading
            );

        }


        document.documentElement.classList.toggle(
            "ajax-navigation-loading",
            isLoading
        );

    }



    // =========================================================
    // PAGE SCROLL
    // =========================================================

    function getPageScrollY() {

        return (
            window.scrollY ||
            window.pageYOffset ||
            document.documentElement.scrollTop ||
            document.body.scrollTop ||
            0
        );

    }


    function setPageScrollY(
        scrollY
    ) {

        const y =
            Number.isFinite(scrollY)
                ? Math.max(
                    0,
                    scrollY
                )
                : 0;


        try {

            window.scrollTo(
                0,
                y
            );

        }
        catch {
            // Ignore.
        }


        try {

            if (document.scrollingElement) {

                document.scrollingElement.scrollTop =
                    y;

            }

        }
        catch {
            // Ignore.
        }


        try {

            document.documentElement.scrollTop =
                y;

        }
        catch {
            // Ignore.
        }


        try {

            document.body.scrollTop =
                y;

        }
        catch {
            // Ignore.
        }


        /*
         * Chỉ chỉnh .app-main nếu nó thực sự là
         * scroll container.
         */

        try {

            document
                .querySelectorAll(
                    ".app-main"
                )
                .forEach(
                    element => {

                        if (
                            element.scrollHeight >
                            element.clientHeight
                        ) {

                            element.scrollTop =
                                y;

                        }

                    }
                );

        }
        catch {
            // Ignore.
        }

    }


    function resetPageScroll() {

        setPageScrollY(0);

    }



    // =========================================================
    // URL
    // =========================================================

    function normalizeUrl(
        url
    ) {

        const result =
            new URL(
                url,
                window.location.href
            );


        result.hash =
            "";


        return result;

    }


    function isSameOrigin(
        url
    ) {

        try {

            const target =
                new URL(
                    url,
                    window.location.href
                );


            return (
                target.origin ===
                window.location.origin
            );

        }
        catch {

            return false;

        }

    }



    // =========================================================
    // HISTORY
    // =========================================================

    function updateHistory(
        url,
        historyMode
    ) {

        if (
            !historyMode ||
            historyMode === "none"
        ) {

            return;

        }


        const state = {

            ajax:
                true

        };


        if (
            historyMode === "push"
        ) {

            window.history.pushState(
                state,
                "",
                url
            );

            return;

        }


        if (
            historyMode === "replace"
        ) {

            window.history.replaceState(
                state,
                "",
                url
            );

        }

    }



    // =========================================================
    // LINK
    // =========================================================

    function shouldIgnoreLink(
        event,
        anchor
    ) {

        if (!anchor) {

            return true;

        }


        if (event.defaultPrevented) {

            return true;

        }


        if (event.button !== 0) {

            return true;

        }


        if (
            event.ctrlKey ||
            event.shiftKey ||
            event.altKey ||
            event.metaKey
        ) {

            return true;

        }


        if (
            anchor.dataset.ajax ===
            "false"
        ) {

            return true;

        }


        if (
            anchor.dataset.noAjax !==
            undefined
        ) {

            return true;

        }


        if (
            anchor.hasAttribute(
                "download"
            )
        ) {

            return true;

        }


        const target =
            (
                anchor.getAttribute(
                    "target"
                ) || ""
            ).toLowerCase();


        if (
            target &&
            target !== "_self"
        ) {

            return true;

        }


        const href =
            anchor.getAttribute(
                "href"
            );


        if (!href) {

            return true;

        }


        if (
            href === "#" ||
            href.startsWith("#") ||
            href.startsWith("javascript:") ||
            href.startsWith("mailto:") ||
            href.startsWith("tel:")
        ) {

            return true;

        }


        if (
            !isSameOrigin(
                anchor.href
            )
        ) {

            return true;

        }


        return false;

    }



    // =========================================================
    // FORM
    // =========================================================

    function shouldIgnoreForm(
        form
    ) {

        if (!form) {

            return true;

        }


        if (
            form.dataset.ajax ===
            "false"
        ) {

            return true;

        }


        if (
            form.dataset.noAjax !==
            undefined
        ) {

            return true;

        }


        const target =
            (
                form.getAttribute(
                    "target"
                ) || ""
            ).toLowerCase();


        if (
            target &&
            target !== "_self"
        ) {

            return true;

        }


        return false;

    }



    // =========================================================
    // ACTIVE SIDEBAR
    // =========================================================

    function updateActiveMenu(
        url
    ) {

        const currentUrl =
            normalizeUrl(
                url
            );


        document
            .querySelectorAll(
                ".app-sidebar a.nav-link[href]"
            )
            .forEach(
                link => {

                    const href =
                        link.getAttribute(
                            "href"
                        );


                    if (
                        !href ||
                        href === "#"
                    ) {

                        return;

                    }


                    let linkUrl;


                    try {

                        linkUrl =
                            normalizeUrl(
                                link.href
                            );

                    }
                    catch {

                        return;

                    }


                    const active =
                        linkUrl.pathname ===
                        currentUrl.pathname;


                    link.classList.toggle(
                        "active",
                        active
                    );


                    const parent =
                        link.closest(
                            ".nav-item"
                        );


                    if (active) {

                        parent?.classList.add(
                            "menu-open"
                        );

                    }
                    else {

                        parent?.classList.remove(
                            "menu-open"
                        );

                    }

                }
            );

    }



    // =========================================================
    // TITLE
    // =========================================================

    function updateDocumentTitle(
        nextDocument
    ) {

        if (
            !nextDocument ||
            !nextDocument.title
        ) {

            return;

        }


        document.title =
            nextDocument.title;

    }



    // =========================================================
    // PAGE STYLES
    // =========================================================

    function applyPageStyles(
        nextDocument
    ) {

        const currentTemplate =
            document.getElementById(
                PAGE_STYLES_TEMPLATE_ID
            );


        if (!currentTemplate) {

            return;

        }


        document.head
            .querySelectorAll(
                "[data-ajax-page-style]"
            )
            .forEach(
                element => {

                    element.remove();

                }
            );


        const nextTemplate =
            nextDocument.getElementById(
                PAGE_STYLES_TEMPLATE_ID
            );


        if (!nextTemplate) {

            currentTemplate.innerHTML =
                "";

            return;

        }


        const nodes =
            Array.from(

                nextTemplate.content
                    .cloneNode(true)
                    .childNodes

            );


        nodes.forEach(
            node => {

                if (
                    node.nodeType !==
                    Node.ELEMENT_NODE
                ) {

                    return;

                }


                node.setAttribute(
                    "data-ajax-page-style",
                    "true"
                );


                document.head.appendChild(
                    node
                );

            }
        );


        currentTemplate.innerHTML =
            nextTemplate.innerHTML;

    }



    // =========================================================
    // PAGE SCRIPTS
    // =========================================================

    async function executePageScripts(
        container
    ) {

        if (!container) {

            return;

        }


        const scripts =
            Array.from(
                container.querySelectorAll(
                    "script"
                )
            );


        for (
            const oldScript
            of scripts
        ) {

            try {

                await executeScript(
                    oldScript
                );

            }
            catch (error) {

                console.error(
                    "[HotelAjax] Lỗi page script:",
                    error
                );

            }

        }

    }


    function executeScript(
        oldScript
    ) {

        return new Promise(
            resolve => {

                if (!oldScript) {

                    resolve();

                    return;

                }


                const src =
                    oldScript.getAttribute(
                        "src"
                    );


                // =============================================
                // EXTERNAL SCRIPT
                // =============================================

                if (src) {

                    const script =
                        document.createElement(
                            "script"
                        );


                    for (
                        const attribute
                        of oldScript.attributes
                    ) {

                        if (
                            attribute.name ===
                            "src"
                        ) {

                            continue;

                        }


                        script.setAttribute(
                            attribute.name,
                            attribute.value
                        );

                    }


                    script.src =
                        new URL(
                            src,
                            window.location.href
                        ).href;


                    script.onload =
                        () => {

                            oldScript.remove();

                            resolve();

                        };


                    script.onerror =
                        error => {

                            console.error(
                                "[HotelAjax] Không thể tải script:",
                                src,
                                error
                            );


                            oldScript.remove();

                            resolve();

                        };


                    document.body.appendChild(
                        script
                    );


                    return;

                }


                // =============================================
                // INLINE SCRIPT
                // =============================================

                const newScript =
                    document.createElement(
                        "script"
                    );


                for (
                    const attribute
                    of oldScript.attributes
                ) {

                    newScript.setAttribute(
                        attribute.name,
                        attribute.value
                    );

                }


                newScript.textContent =
                    oldScript.textContent;


                oldScript.replaceWith(
                    newScript
                );


                resolve();

            }
        );

    }



    // =========================================================
    // LOGIN
    // =========================================================

    function isLoginResponse(
        documentObject
    ) {

        if (!documentObject) {

            return false;

        }


        return (
            documentObject.querySelector(
                'form[action*="/Account/Login"]'
            ) !== null
        );

    }



    // =========================================================
    // RESPONSE
    // =========================================================

    async function replaceMainContent(
        response,
        requestedUrl
    ) {

        if (!response) {

            throw new Error(
                "Không có AJAX response."
            );

        }


        if (!response.ok) {

            throw new Error(
                `Máy chủ trả về HTTP ${response.status}.`
            );

        }


        const contentType =
            response.headers.get(
                "content-type"
            ) || "";


        if (
            !contentType
                .toLowerCase()
                .includes(
                    "text/html"
                )
        ) {

            throw new Error(
                `AJAX response không phải HTML: ${contentType || "unknown"}`
            );

        }


        const html =
            await response.text();


        if (!html) {

            throw new Error(
                "AJAX response rỗng."
            );

        }


        const parser =
            new DOMParser();


        const nextDocument =
            parser.parseFromString(
                html,
                "text/html"
            );


        // =====================================================
        // LOGIN
        // =====================================================

        if (
            isLoginResponse(
                nextDocument
            )
        ) {

            window.location.href =
                response.url ||
                requestedUrl;


            return false;

        }


        // =====================================================
        // CURRENT MAIN
        // =====================================================

        const currentMain =
            getMainContent();


        if (!currentMain) {

            throw new Error(
                `Không tìm thấy #${MAIN_CONTENT_ID} trên trang hiện tại.`
            );

        }


        // =====================================================
        // NEXT MAIN
        // =====================================================

        const nextMain =
            nextDocument.getElementById(
                MAIN_CONTENT_ID
            );


        if (!nextMain) {

            throw new Error(
                `AJAX response không chứa #${MAIN_CONTENT_ID}.`
            );

        }


        const finalUrl =
            response.url ||
            requestedUrl;


        // =====================================================
        // TITLE
        // =====================================================

        try {

            updateDocumentTitle(
                nextDocument
            );

        }
        catch (error) {

            console.error(
                "[HotelAjax] Không thể cập nhật title:",
                error
            );

        }


        // =====================================================
        // CSS
        // =====================================================

        try {

            applyPageStyles(
                nextDocument
            );

        }
        catch (error) {

            console.error(
                "[HotelAjax] Không thể cập nhật page styles:",
                error
            );

        }


        // =====================================================
        // REPLACE MAIN
        // =====================================================

        currentMain.replaceWith(
            nextMain
        );


        /*
         * Từ đây main đã thay thành công.
         */


        // =====================================================
        // FLASH MESSAGES
        // =====================================================

        try {

            if (
                window.HotelUI &&
                typeof window.HotelUI.processFlashMessages === "function"
            ) {
                window.HotelUI.processFlashMessages(nextDocument);
            }

        }
        catch (error) {
            console.error(
                "[HotelAjax] Không thể xử lý flash message:",
                error
            );
        }


        // =====================================================
        // PAGE JS
        // =====================================================

        try {

            await executePageScripts(
                nextMain
            );

        }
        catch (error) {

            console.error(
                "[HotelAjax] Lỗi page scripts:",
                error
            );

        }


        // =====================================================
        // SIDEBAR
        // =====================================================

        try {

            updateActiveMenu(
                finalUrl
            );

        }
        catch (error) {

            console.error(
                "[HotelAjax] Lỗi cập nhật sidebar:",
                error
            );

        }


        // =====================================================
        // EVENT
        // =====================================================

        try {

            document.dispatchEvent(

                new CustomEvent(
                    "hotel:ajax-loaded",
                    {
                        detail: {

                            url:
                                finalUrl,

                            main:
                                nextMain

                        }

                    }
                )

            );

        }
        catch (error) {

            console.error(
                "[HotelAjax] Lỗi hotel:ajax-loaded:",
                error
            );

        }


        return true;

    }



    // =========================================================
    // NAVIGATION
    // =========================================================

    async function navigate(
        url,
        options = {}
    ) {

        if (isNavigating) {

            console.warn(
                "[HotelAjax] AJAX navigation đang được thực hiện."
            );


            return false;

        }


        const method =
            (
                options.method ||
                "GET"
            ).toUpperCase();


        const historyMode =
            options.historyMode ||
            "none";


        const preserveScroll =
            options.preserveScroll ===
            true;


        /*
         * Lấy scroll TRƯỚC khi fetch.
         */

        const preservedScrollY =
            preserveScroll
                ? getPageScrollY()
                : 0;


        isNavigating =
            true;


        setLoading(
            true
        );


        try {

            // =================================================
            // HEADERS
            // =================================================

            const headers =
                new Headers(
                    options.headers ||
                    {}
                );


            headers.set(
                AJAX_HEADER,
                AJAX_HEADER_VALUE
            );


            headers.set(
                "Accept",
                "text/html, application/xhtml+xml"
            );


            // =================================================
            // REQUEST INIT
            // =================================================

            const requestInit = {

                method,

                headers,

                credentials:
                    "same-origin",

                redirect:
                    "follow"

            };


            if (
                method !== "GET" &&
                method !== "HEAD"
            ) {

                if (
                    options.body !==
                    undefined
                ) {

                    requestInit.body =
                        options.body;

                }

            }


            // =================================================
            // FETCH
            // =================================================

            const response =
                await fetch(
                    url,
                    requestInit
                );


            // =================================================
            // REPLACE MAIN
            // =================================================

            const success =
                await replaceMainContent(
                    response,
                    url
                );


            if (!success) {

                return false;

            }


            // =================================================
            // SCROLL
            // =================================================

            if (preserveScroll) {

                setPageScrollY(
                    preservedScrollY
                );

            }
            else {

                resetPageScroll();

            }


            // =================================================
            // HISTORY
            // =================================================

            try {

                updateHistory(

                    response.url ||
                    url,

                    historyMode

                );

            }
            catch (error) {

                console.error(
                    "[HotelAjax] Không thể cập nhật history:",
                    error
                );

            }


            return true;

        }
        catch (error) {

            console.error(
                "[HotelAjax] Navigation failed:",
                error
            );


            if (
                options.showErrorToast !== false &&
                window.HotelUI &&
                typeof window.HotelUI.toast ===
                "function"
            ) {

                window.HotelUI.toast(

                    "error",

                    "Có lỗi xảy ra",

                    error?.message ||
                    "Không thể tải nội dung."

                );

            }


            return false;

        }
        finally {

            isNavigating =
                false;


            setLoading(
                false
            );

        }

    }



    // =========================================================
    // GET FORM URL
    // =========================================================

    function buildGetUrl(
        form,
        submitter
    ) {

        const action =
            submitter?.formAction ||
            form.action ||
            window.location.href;


        const url =
            new URL(
                action,
                window.location.href
            );


        const formData =
            new FormData(
                form,
                submitter ||
                undefined
            );


        url.search =
            "";


        for (
            const [key, value]
            of formData.entries()
        ) {

            if (
                value instanceof File
            ) {

                continue;

            }


            url.searchParams.append(

                key,

                String(value)

            );

        }


        return url.href;

    }



    // =========================================================
    // LINK CLICK
    // =========================================================

    document.addEventListener(

        "click",

        event => {

            const target =
                event.target instanceof Element
                    ? event.target
                    : null;


            if (!target) {

                return;

            }


            const anchor =
                target.closest(
                    "a[href]"
                );


            if (!anchor) {

                return;

            }


            // =================================================
            // PAGINATION
            // =================================================

            if (
                anchor.dataset.ajaxPagination ===
                "true"
            ) {

                event.preventDefault();

                event.stopPropagation();


                if (
                    window.HotelPagination &&
                    typeof window.HotelPagination.goTo ===
                    "function"
                ) {

                    window.HotelPagination.goTo(
                        anchor.href
                    );

                }


                return;

            }


            // =================================================
            // NORMAL LINK
            // =================================================

            if (
                shouldIgnoreLink(
                    event,
                    anchor
                )
            ) {

                return;

            }


            event.preventDefault();


            /*
             * QUAN TRỌNG:
             *
             * Normal link phải dùng anchor.href.
             *
             * Không được dùng buildGetUrl() ở đây vì
             * không có form/submitter.
             */

            navigate(

                anchor.href,

                {

                    method:
                        "GET",

                    historyMode:
                        "push",

                    /*
                     * Chuyển sang page mới:
                     * reset scroll.
                     */

                    preserveScroll:
                        false

                }

            );

        }

    );



    // =========================================================
    // FORM SUBMIT
    // =========================================================

    document.addEventListener(

        "submit",

        event => {

            const target =
                event.target instanceof Element
                    ? event.target
                    : null;


            if (!target) {

                return;

            }


            const form =
                target.closest(
                    "form"
                );


            if (!form) {

                return;

            }


            if (
                shouldIgnoreForm(
                    form
                )
            ) {

                return;

            }


            const submitter =
                event.submitter;


            // =================================================
            // CONFIRMATION
            // =================================================

            if (
                form.dataset.ajaxConfirm ===
                "true" &&
                form.dataset.ajaxConfirmed !==
                "true"
            ) {

                event.preventDefault();


                if (
                    showFormConfirmation(
                        form,
                        submitter
                    )
                ) {

                    return;

                }

            }


            // =================================================
            // RESET CONFIRM
            // =================================================

            if (
                form.dataset.ajaxConfirmed ===
                "true"
            ) {

                delete form.dataset.ajaxConfirmed;

            }


            if (
                event.defaultPrevented
            ) {

                return;

            }


            // =================================================
            // METHOD
            // =================================================

            const method =
                (
                    submitter?.formMethod ||
                    form.method ||
                    "GET"
                ).toUpperCase();


            // =================================================
            // ACTION
            // =================================================

            const action =
                submitter?.formAction ||
                form.action ||
                window.location.href;


            if (
                !isSameOrigin(
                    action
                )
            ) {

                return;

            }


            // =================================================
            // AJAX
            // =================================================

            event.preventDefault();


            // =================================================
            // GET
            // =================================================

            if (
                method ===
                "GET"
            ) {

                /*
                 * Employee filter:
                 * giữ scroll.
                 *
                 * Các GET form khác:
                 * mặc định về đầu trang.
                 */

                const preserveScroll =
                    form.dataset.preserveScroll === "true" ||
                    form.classList.contains("employee-filter-form");


                navigate(

                    buildGetUrl(
                        form,
                        submitter
                    ),

                    {

                        method:
                            "GET",

                        historyMode:
                            "push",

                        preserveScroll

                    }

                );


                return;

            }


            // =================================================
            // POST / PUT / PATCH / DELETE
            // =================================================

            const body =
                new FormData(
                    form,
                    submitter ||
                    undefined
                );


            /*
             * Chỉ các form được đánh dấu
             * data-preserve-scroll="true"
             * mới giữ scroll.
             *
             * Các form khác về đầu.
             */

            const preserveScroll =
                form.dataset.preserveScroll ===
                "true";


            navigate(

                action,

                {

                    method,

                    body,

                    historyMode:
                        "replace",

                    preserveScroll

                }

            );

        }

    );



    // =========================================================
    // BACK / FORWARD
    // =========================================================

    window.addEventListener(

        "popstate",

        () => {

            /*
             * Back / Forward:
             * không khôi phục scroll.
             */

            navigate(

                window.location.href,

                {

                    method:
                        "GET",

                    historyMode:
                        "none",

                    preserveScroll:
                        false

                }

            );

        }

    );



    // =========================================================
    // INITIALIZE
    // =========================================================

    document.addEventListener(

        "DOMContentLoaded",

        () => {

            // =================================================
            // INITIAL PAGE STYLES
            // =================================================

            const template =
                document.getElementById(
                    PAGE_STYLES_TEMPLATE_ID
                );


            if (template) {

                const existingStyles =
                    document.head.querySelectorAll(
                        "[data-ajax-page-style]"
                    );


                if (
                    existingStyles.length ===
                    0
                ) {

                    const nodes =
                        Array.from(

                            template.content
                                .cloneNode(true)
                                .childNodes

                        );


                    nodes.forEach(
                        node => {

                            if (
                                node.nodeType !==
                                Node.ELEMENT_NODE
                            ) {

                                return;

                            }


                            node.setAttribute(
                                "data-ajax-page-style",
                                "true"
                            );


                            document.head.appendChild(
                                node
                            );

                        }
                    );

                }

            }


            // =================================================
            // INITIAL HISTORY
            // =================================================

            window.history.replaceState(

                {

                    ajax:
                        true

                },

                "",

                window.location.href

            );


            // =================================================
            // ACTIVE MENU
            // =================================================

            try {

                updateActiveMenu(
                    window.location.href
                );

            }
            catch (error) {

                console.error(
                    "[HotelAjax] Không thể khởi tạo active menu:",
                    error
                );

            }


            // =================================================
            // INITIAL EVENT
            // =================================================

            try {

                document.dispatchEvent(

                    new CustomEvent(
                        "hotel:ajax-loaded",
                        {
                            detail: {

                                url:
                                    window.location.href,

                                main:
                                    getMainContent()

                            }

                        }
                    )

                );

            }
            catch (error) {

                console.error(
                    "[HotelAjax] Lỗi initial hotel:ajax-loaded:",
                    error
                );

            }

        }

    );



    // =========================================================
    // PUBLIC API
    // =========================================================

    window.HotelAjax = {

        navigate,


        reloadMain() {

            return navigate(

                window.location.href,

                {

                    method:
                        "GET",

                    historyMode:
                        "none",

                    preserveScroll:
                        false

                }

            );

        },


        executePageScripts,


        updateActiveMenu,


        setLoading

    };

})();
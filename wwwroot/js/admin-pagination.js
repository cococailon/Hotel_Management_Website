"use strict";

window.HotelPagination = {

    // =========================================================
    // PAGE LINK
    // =========================================================

    async goTo(url) {

        if (!url) {
            return;
        }

        if (
            !window.HotelAjax ||
            typeof window.HotelAjax.navigate !== "function"
        ) {

            // AJAX navigation chưa được load.
            // Fallback về navigation bình thường.

            window.location.href = url;

            return;
        }


        await window.HotelAjax.navigate(
            url,
            {
                method: "GET",
                historyMode: "push",
                preserveScroll: true
            }
        );

    },


    // =========================================================
    // PAGE SIZE
    // =========================================================

    async changePageSize(select) {

        if (!select) {
            return;
        }


        const url =
            select.value;


        if (!url) {
            return;
        }


        await this.goTo(url);

    }

};
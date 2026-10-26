(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        var catalogForm = document.getElementById("catalog-filter-form");
        if (catalogForm) {
            catalogForm.addEventListener("submit", function () {
                var pageInput = catalogForm.querySelector('input[name="Page"]');
                if (pageInput) {
                    pageInput.remove();
                }
            });

            var autoSubmitSelectors = ["#filter-category", "#filter-sort", "#filter-pagesize", "#filter-instock"];
            autoSubmitSelectors.forEach(function (selector) {
                var element = catalogForm.querySelector(selector);
                if (element) {
                    element.addEventListener("change", function () {
                        var pageInput = catalogForm.querySelector('input[name="Page"]');
                        if (pageInput) {
                            pageInput.remove();
                        }
                        catalogForm.submit();
                    });
                }
            });
        }
    });
})();

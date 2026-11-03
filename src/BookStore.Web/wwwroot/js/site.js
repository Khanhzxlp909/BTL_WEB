(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        var catalogForm = document.getElementById("catalog-filter-form");
        if (catalogForm) {
            var stripPage = function () {
                var pageInputs = catalogForm.querySelectorAll('input[name="Page"]');
                pageInputs.forEach(function (inp) {
                    inp.remove();
                });
            };

            catalogForm.addEventListener("submit", function () {
                stripPage();
            });

            var autoControls = document.querySelectorAll(
                '#catalog-filter-form #filter-category, #catalog-filter-form #filter-sort, #catalog-filter-form #filter-pagesize, #catalog-filter-form #filter-instock, #catalog-filter-form [data-auto-submit="true"], [form="catalog-filter-form"][data-auto-submit="true"], #filter-sort[form="catalog-filter-form"], #filter-pagesize[form="catalog-filter-form"]'
            );
            autoControls.forEach(function (element) {
                element.addEventListener("change", function () {
                    stripPage();
                    catalogForm.submit();
                });
            });
        }

        document.addEventListener("keydown", function (event) {
            if (event.key !== "/" || event.ctrlKey || event.metaKey || event.altKey) {
                return;
            }
            var active = document.activeElement;
            if (active && (active.tagName === "INPUT" || active.tagName === "TEXTAREA" || active.tagName === "SELECT" || active.isContentEditable)) {
                return;
            }
            var searchInput = document.getElementById("header-book-search");
            if (searchInput) {
                event.preventDefault();
                searchInput.focus();
                searchInput.select();
            }
        });
    });
})();

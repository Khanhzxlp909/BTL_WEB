(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        var catalogForm = document.getElementById("catalog-filter-form");
        if (catalogForm) {
            var stripPage = function () {
                var pageInputs = document.querySelectorAll(
                    '#catalog-filter-form input[name="Page"], input[name="Page"][form="catalog-filter-form"]'
                );
                pageInputs.forEach(function (inp) {
                    inp.remove();
                });
            };

            var disableEmptyFields = function () {
                var formElements = Array.prototype.slice.call(catalogForm.elements || []);
                var externalElements = Array.prototype.slice.call(
                    document.querySelectorAll('[form="catalog-filter-form"]')
                );
                var allControls = formElements.concat(externalElements);

                var disabledFields = [];
                var seen = [];
                allControls.forEach(function (el) {
                    if (!el || !el.name || el.disabled || seen.indexOf(el) !== -1) {
                        return;
                    }
                    seen.push(el);
                    var tag = el.tagName.toUpperCase();
                    if (tag === "INPUT" || tag === "SELECT") {
                        var type = (el.type || "").toLowerCase();
                        if (type === "checkbox" || type === "radio" || type === "submit" || type === "button" || type === "reset") {
                            return;
                        }
                        if (String(el.value || "").trim() === "") {
                            el.disabled = true;
                            disabledFields.push(el);
                        }
                    }
                });

                var restoreDisabled = function () {
                    disabledFields.forEach(function (el) {
                        el.disabled = false;
                    });
                };

                // Dữ liệu form được thu thập ngay khi sự kiện submit kết thúc; khôi phục ở tick kế tiếp.
                setTimeout(restoreDisabled, 0);
                window.addEventListener("pageshow", restoreDisabled, { once: true });
            };

            var prepareCatalogSubmit = function () {
                stripPage();
                disableEmptyFields();
            };

            catalogForm.addEventListener("submit", function () {
                prepareCatalogSubmit();
            });

            var autoControls = document.querySelectorAll(
                '#catalog-filter-form #filter-category, #catalog-filter-form #filter-sort, #catalog-filter-form #filter-pagesize, #catalog-filter-form #filter-instock, #catalog-filter-form [data-auto-submit="true"], [form="catalog-filter-form"][data-auto-submit="true"], #filter-sort[form="catalog-filter-form"], #filter-pagesize[form="catalog-filter-form"]'
            );
            autoControls.forEach(function (element) {
                element.addEventListener("change", function () {
                    prepareCatalogSubmit();
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

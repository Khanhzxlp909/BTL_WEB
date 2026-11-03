(function () {
    "use strict";

    if (window.__bsAdminJsInitialized) {
        return;
    }
    window.__bsAdminJsInitialized = true;

    document.addEventListener("DOMContentLoaded", function () {
        var filterForms = document.querySelectorAll('form[data-admin-filter-form="true"]');
        filterForms.forEach(function (form) {
            var stripPageInput = function () {
                var pageInputs = form.querySelectorAll('input[name="Page"]');
                pageInputs.forEach(function (input) {
                    input.remove();
                });
            };

            form.addEventListener("submit", function () {
                stripPageInput();
            });

            var autoSubmitControls = form.querySelectorAll('[data-auto-submit="true"]');
            autoSubmitControls.forEach(function (control) {
                control.addEventListener("change", function () {
                    stripPageInput();
                    form.submit();
                });
            });
        });

        var confirmForms = document.querySelectorAll("form[data-confirm], form[data-prevent-double-submit]");
        confirmForms.forEach(function (form) {
            form.addEventListener("submit", function (event) {
                if (form.dataset.submitting === "true") {
                    event.preventDefault();
                    return;
                }

                var message = form.getAttribute("data-confirm");
                if (message && !window.confirm(message)) {
                    event.preventDefault();
                    return;
                }

                if (form.getAttribute("data-prevent-double-submit") === "true") {
                    form.dataset.submitting = "true";
                    var submitButtons = form.querySelectorAll('button[type="submit"], input[type="submit"]');
                    submitButtons.forEach(function (btn) {
                        btn.disabled = true;
                    });
                }
            });
        });

        var coverInput = document.querySelector('input[type="file"][name="CoverFile"]');
        var coverStatus = document.getElementById("bs-cover-file-selected");
        if (coverInput && coverStatus) {
            coverInput.addEventListener("change", function () {
                if (coverInput.files && coverInput.files.length > 0) {
                    var file = coverInput.files[0];
                    var sizeKb = Math.max(1, Math.round(file.size / 1024));
                    coverStatus.textContent = file.name + " (" + sizeKb + " KB)";
                    coverStatus.classList.remove("d-none");
                } else {
                    coverStatus.textContent = "";
                    coverStatus.classList.add("d-none");
                }
            });
        }
    });
})();

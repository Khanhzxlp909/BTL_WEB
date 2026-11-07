(function ($) {
    "use strict";

    var isEn = (document.documentElement.getAttribute("lang") || "vi").toLowerCase().indexOf("en") === 0;

    function t(vi, en) {
        return isEn ? en : vi;
    }

    function moneyText(text) {
        return typeof text === "string" && text.trim().length > 0 ? text : "0 đ";
    }

    function getCsrfToken() {
        var el = document.querySelector('#bs-csrf-token input[name="__RequestVerificationToken"]')
            || document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : "";
    }

    function getLocalReturnUrl() {
        var path = window.location.pathname || "/";
        var search = window.location.search || "";
        var combined = path + search;
        if (combined.charAt(0) !== "/" || combined.indexOf("//") === 0) {
            return "/";
        }
        return combined;
    }

    function getLoginUrlWithReturn() {
        return "/tai-khoan/dang-nhap?ReturnUrl=" + encodeURIComponent(getLocalReturnUrl());
    }

    function parseErrorPayload(jqXHR) {
        if (jqXHR && jqXHR.responseJSON && typeof jqXHR.responseJSON === "object") {
            return jqXHR.responseJSON;
        }
        if (jqXHR && typeof jqXHR.responseText === "string" && jqXHR.responseText.trim().charAt(0) === "{") {
            try {
                return JSON.parse(jqXHR.responseText);
            } catch (_e) {
                return null;
            }
        }
        return null;
    }

    function normalizeCountText(count) {
        if (typeof count === "string" && /^\d+$/.test(count.trim())) {
            return count.trim();
        }
        if (typeof count === "number" && Number.isFinite(count) && count >= 0) {
            return String(Math.trunc(count));
        }
        return "0";
    }

    function normalizeNonNegativeIntText(value) {
        if (typeof value === "number" && Number.isFinite(value) && value >= 0) {
            return String(Math.trunc(value));
        }
        if (typeof value === "string") {
            var trimmed = value.trim();
            if (/^\d+$/.test(trimmed)) {
                return trimmed.replace(/^0+(?=\d)/, "");
            }
        }
        return null;
    }

    function readPreviousCartQty($inp) {
        if (!$inp || $inp.length === 0) {
            return "1";
        }
        var fromData = normalizeNonNegativeIntText($inp.attr("data-prev-qty"));
        if (fromData !== null) {
            return fromData;
        }
        var fromAttr = normalizeNonNegativeIntText($inp.attr("value"));
        if (fromAttr !== null) {
            return fromAttr;
        }
        var el = $inp[0];
        var fromDefault = el && typeof el.defaultValue === "string"
            ? normalizeNonNegativeIntText(el.defaultValue)
            : null;
        if (fromDefault !== null) {
            return fromDefault;
        }
        var fromVal = normalizeNonNegativeIntText($inp.val());
        if (fromVal !== null) {
            return fromVal;
        }
        return "1";
    }

    function setPreviousCartQty($inp, qty) {
        if (!$inp || $inp.length === 0) {
            return;
        }
        var normalized = normalizeNonNegativeIntText(qty);
        if (normalized !== null) {
            $inp.attr("data-prev-qty", normalized);
        }
    }

    function hasValidCartCount(cart) {
        if (!cart || typeof cart !== "object") {
            return false;
        }
        var raw = cart.itemCount !== undefined ? cart.itemCount : cart.ItemCount;
        if (typeof raw === "number") {
            return Number.isFinite(raw) && raw >= 0;
        }
        if (typeof raw === "string") {
            return /^\d+$/.test(raw.trim());
        }
        return false;
    }

    function buildHeaderCartSyncErrorMessage(jqXHR) {
        if (!jqXHR) {
            return t(
                "Không tải được số lượng giỏ hàng: Phản hồi /api/cart thiếu trường itemCount hợp lệ. Hãy tải lại trang hoặc mở Giỏ hàng để kiểm tra.",
                "Could not load cart item count: /api/cart response is missing a valid itemCount field. Reload the page or open Cart to verify."
            );
        }
        var status = jqXHR.status || 0;
        var payload = parseErrorPayload(jqXHR);
        var serverMsg = payload && (payload.message || payload.Message);
        var code = payload && (payload.code || payload.Code);
        var traceId = payload && (payload.traceId || payload.TraceId);
        var suffix = (code ? " [" + String(code) + "]" : "")
            + (traceId ? t(" (Mã tra cứu: ", " (TraceId: ") + String(traceId) + ")" : "");

        if (status === 401) {
            var msg401 = serverMsg || t(
                "Phiên đăng nhập đã hết hạn hoặc tài khoản bị khóa.",
                "Your session expired or the account is locked."
            );
            return t(
                "Không tải được số lượng giỏ hàng: " + msg401 + " Hãy đăng nhập lại để kiểm tra giỏ hàng.",
                "Could not load cart item count: " + msg401 + " Sign in again to inspect your cart."
            ) + suffix;
        }

        if (status === 0 || (!payload && status >= 502)) {
            return t(
                "Không tải được số lượng giỏ hàng do mất kết nối mạng hoặc máy chủ không phản hồi. Hãy tải lại trang hoặc mở Giỏ hàng để kiểm tra.",
                "Could not load cart item count due to a network error or unresponsive server. Reload the page or open Cart to verify."
            );
        }

        if (serverMsg) {
            return t(
                "Không tải được số lượng giỏ hàng: ",
                "Could not load cart item count: "
            ) + String(serverMsg) + suffix;
        }

        return t(
            "Không tải được số lượng giỏ hàng từ /api/cart (HTTP " + status + "). Hãy tải lại trang hoặc mở Giỏ hàng để kiểm tra.",
            "Could not load cart item count from /api/cart (HTTP " + status + "). Reload the page or open Cart to verify."
        ) + suffix;
    }

    function updateHeaderCartBadges(count) {
        var countText = normalizeCountText(count);
        $('[data-cart-badge="true"]').each(function () {
            var $badge = $(this);
            $badge
                .text(countText)
                .removeAttr("title")
                .removeAttr("aria-label")
                .removeAttr("data-cart-error")
                .removeClass("bg-danger text-white");

            var $link = $badge.closest("a.bs-header-cart-link");
            if ($link.length > 0) {
                $link.removeAttr("title");
                var defaultAria = $link.attr("data-default-aria-label");
                if (defaultAria) {
                    $link.attr("aria-label", defaultAria);
                }
            }
        });
    }

    function setHeaderCartBadgeError(jqXHR) {
        var errorText = buildHeaderCartSyncErrorMessage(jqXHR);
        $('[data-cart-badge="true"]').each(function () {
            var $badge = $(this);
            $badge
                .text("?")
                .attr("data-cart-error", "true")
                .attr("title", errorText)
                .attr("aria-label", errorText)
                .addClass("bg-danger text-white");

            var $link = $badge.closest("a.bs-header-cart-link");
            if ($link.length > 0) {
                if (!$link.attr("data-default-aria-label")) {
                    $link.attr("data-default-aria-label", $link.attr("aria-label") || t("Giỏ hàng", "Shopping cart"));
                }
                $link
                    .attr("title", errorText)
                    .attr("aria-label", $link.attr("data-default-aria-label") + " — " + errorText);
            }
        });
    }

    function buildErrorDom(jqXHR, isWriteOperation) {
        var status = jqXHR ? jqXHR.status : 0;
        var payload = parseErrorPayload(jqXHR);
        var $box = $("<div>").addClass("alert alert-danger small mb-0 bs-wrap-anywhere").attr("role", "alert");

        if (status === 401) {
            var authMsg = (payload && (payload.message || payload.Message))
                || t(
                    "Phiên đăng nhập chưa có, đã hết hạn hoặc tài khoản bị khóa. Hãy đăng nhập lại để tiếp tục mua hàng.",
                    "Your session is missing, expired, or locked. Please sign in to continue shopping."
                );
            var authCode = payload && (payload.code || payload.Code);
            var $top401 = $("<div>").addClass("d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2");
            $top401.append($("<strong>").text(authMsg));
            if (authCode) {
                $top401.append($("<code>").text(String(authCode)));
            }
            $box.append($top401);
            $box.append(
                $("<a>")
                    .addClass("btn btn-sm bs-btn-primary")
                    .attr("href", getLoginUrlWithReturn())
                    .text(t("Đăng nhập lại (giữ trang hiện tại)", "Sign in (keep current page)"))
            );
            return $box;
        }

        if (status === 0 || (!payload && status >= 502)) {
            var netMsg = isWriteOperation
                ? t(
                    "Không thể hoàn tất thao tác giỏ hàng do mất kết nối mạng hoặc máy chủ không phản hồi. Hệ thống không tự gửi lại để tránh ghi lặp; hãy tải lại trang để kiểm tra giỏ hàng thực tế.",
                    "The cart operation failed because the network connection dropped or the server did not respond. The request was not retried automatically to prevent duplicate writes; reload the page to inspect your actual cart."
                )
                : t(
                    "Không thể tải dữ liệu giỏ hàng do mất kết nối mạng hoặc máy chủ không phản hồi. Hãy tải lại trang để kiểm tra giỏ hàng.",
                    "Unable to load cart data due to a network error or unresponsive server. Reload the page to check your cart."
                );
            $box.append($("<div>").addClass("fw-semibold mb-2").text(netMsg));
            $box.append(
                $("<a>")
                    .addClass("btn btn-sm btn-outline-danger")
                    .attr("href", getLocalReturnUrl())
                    .text(t("Tải lại trang kiểm tra giỏ", "Reload page to verify cart"))
            );
            return $box;
        }

        var code = payload && (payload.code || payload.Code);
        var message = (payload && (payload.message || payload.Message))
            || t(
                "Không thể xử lý thao tác giỏ hàng (HTTP " + status + "). Hãy tải lại trang để kiểm tra dữ liệu giỏ hàng rồi thử lại.",
                "Could not process the cart operation (HTTP " + status + "). Reload the page to verify your cart and try again."
            );
        var details = payload && (payload.details || payload.Details);
        var traceId = payload && (payload.traceId || payload.TraceId);

        var $header = $("<div>").addClass("d-flex flex-wrap justify-content-between align-items-center gap-2 mb-1");
        $header.append($("<strong>").text(message));
        if (code) {
            $header.append($("<code>").text(String(code)));
        }
        $box.append($header);

        if (details && typeof details === "object") {
            var $ul = $("<ul>").addClass("mb-1 ps-3 mt-1");
            var detailCount = 0;
            Object.keys(details).forEach(function (field) {
                var arr = details[field];
                if (Array.isArray(arr)) {
                    arr.forEach(function (itemMsg) {
                        if (itemMsg && String(itemMsg) !== String(message)) {
                            $ul.append($("<li>").text(String(itemMsg)));
                            detailCount++;
                        }
                    });
                }
            });
            if (detailCount > 0) {
                $box.append($ul);
            }
        }

        if (traceId) {
            var $trace = $("<div>").addClass("text-secondary mt-1");
            $trace.append($("<span>").text(t("Mã tra cứu (TraceId): ", "Reference ID (TraceId): ")));
            $trace.append($("<code>").text(String(traceId)));
            $box.append($trace);
        }

        return $box;
    }

    function isSafeLocalImage(src) {
        return typeof src === "string" && src.length > 1 && src.charAt(0) === "/" && src.charAt(1) !== "/";
    }

    function renderObservationBanners($container, items) {
        $container.empty();
        if (!Array.isArray(items) || items.length === 0) {
            return;
        }

        var hasPriceChanged = false;
        var hasUnavailable = false;
        var warnings = [];

        items.forEach(function (line) {
            if (line.priceChanged) {
                hasPriceChanged = true;
            }
            if (!line.isAvailable) {
                hasUnavailable = true;
            }
            if (line.warning) {
                warnings.push({
                    title: line.title || "",
                    warning: String(line.warning)
                });
            }
        });

        if (hasPriceChanged || hasUnavailable || warnings.length > 0) {
            var $warnBox = $("<div>").addClass("alert alert-warning mb-3").attr("role", "status");
            var headingText = hasPriceChanged
                ? t(
                    "Cảnh báo cập nhật giá / tồn kho trong giỏ hàng:",
                    "Price or stock changes detected in your cart:"
                )
                : t(
                    "Cảnh báo tình trạng sách trong giỏ hàng:",
                    "Cart item availability notice:"
                );
            $warnBox.append($("<div>").addClass("fw-semibold mb-1").text(headingText));
            if (warnings.length > 0) {
                var $wList = $("<ul>").addClass("small mb-1 ps-3");
                warnings.forEach(function (w) {
                    var $li = $("<li>");
                    if (w.title) {
                        $li.append($("<strong>").text(w.title + ": "));
                    }
                    $li.append($("<span>").text(w.warning));
                    $wList.append($li);
                });
                $warnBox.append($wList);
            }
            $warnBox.append(
                $("<div>").addClass("small").text(
                    t(
                        "Vui lòng kiểm tra lại đơn giá, điều chỉnh số lượng hoặc xóa mục không khả dụng trước khi chuyển sang Đặt hàng.",
                        "Please review unit prices and adjust or remove unavailable items before proceeding to Checkout."
                    )
                )
            );
            $container.append($warnBox);
        }

        if (!hasPriceChanged) {
            var $refNote = $("<div>").addClass("alert alert-light border small text-secondary mb-3").attr("role", "note");
            $refNote.text(
                t(
                    "Lưu ý giá tham khảo: Đơn giá hiển thị là giá bán hiện tại (phiên chưa ghi nhận biến động giá trước đó). Báo giá cuối cùng và thanh toán COD sẽ được xác nhận lại tại bước Đặt hàng.",
                    "Reference price notice: Displayed unit prices reflect current live prices (no prior price change observed in this session). Final quotation and COD payment are re-verified at Checkout."
                )
            );
            $container.append($refNote);
        }
    }

    function buildCartRow(line) {
        var lineId = Number(line.id);
        var bookId = Number(line.bookId);
        var title = line.title || "";
        var sku = line.sku || line.SKU || "";
        var quantity = Math.max(0, Math.trunc(Number(line.quantity) || 0));
        var available = Math.max(0, Math.trunc(Number(line.availableQuantity) || 0));
        var isAvailable = Boolean(line.isAvailable);
        var priceChanged = Boolean(line.priceChanged);
        var warning = line.warning ? String(line.warning) : "";
        var detailHref = "/sach/" + encodeURIComponent(bookId);

        var $tr = $("<tr>").attr("data-cart-line-id", String(lineId));
        if (!isAvailable) {
            $tr.addClass("table-warning");
        }

        var $tdBook = $("<td>").addClass("bs-cart-cell-book");
        var $bookWrap = $("<div>").addClass("d-flex align-items-start gap-3 w-100");
        var $thumbLink = $("<a>").attr("href", detailHref).addClass("text-decoration-none");
        if (isSafeLocalImage(line.coverImage)) {
            $thumbLink.append(
                $("<img>")
                    .addClass("bs-cart-thumb")
                    .attr("src", line.coverImage)
                    .attr("alt", title)
            );
        } else {
            $thumbLink.append(
                $("<span>")
                    .addClass("bs-cart-thumb-placeholder")
                    .text(sku || "BOOK")
            );
        }
        $bookWrap.append($thumbLink);

        var $meta = $("<div>").addClass("min-w-0 flex-grow-1");
        $meta.append(
            $("<a>")
                .attr("href", detailHref)
                .addClass("fw-semibold text-decoration-none text-dark d-block bs-wrap-anywhere")
                .text(title)
        );
        var $skuRow = $("<div>").addClass("small text-muted mt-1 d-flex flex-wrap align-items-center gap-2");
        var $skuSpan = $("<span>").text("SKU: ");
        $skuSpan.append($("<code>").text(sku));
        $skuRow.append($skuSpan);

        if (isAvailable && available > 0) {
            $skuRow.append(
                $("<span>")
                    .addClass("badge bs-badge-instock")
                    .text(t("Khả dụng: " + available, "Available: " + available))
            );
        } else {
            $skuRow.append(
                $("<span>")
                    .addClass("badge bs-badge-outstock")
                    .text(t("Không đủ hàng / Ngừng bán", "Unavailable / Out of stock"))
            );
        }

        if (priceChanged) {
            $skuRow.append(
                $("<span>")
                    .addClass("badge bs-badge-price-changed")
                    .text(t("Giá vừa thay đổi", "Price changed"))
            );
        }
        $meta.append($skuRow);

        if (warning) {
            $meta.append(
                $("<div>")
                    .addClass("small text-danger fw-medium mt-1 bs-wrap-anywhere")
                    .text(warning)
            );
        }
        $bookWrap.append($meta);
        $tdBook.append($bookWrap);
        $tr.append($tdBook);

        var $tdUnit = $("<td>")
            .attr("data-label", t("Đơn giá", "Unit Price"))
            .addClass("text-md-end text-nowrap fw-medium")
            .text(moneyText(line.unitPriceText || line.UnitPriceText));
        $tr.append($tdUnit);

        var $tdQty = $("<td>")
            .attr("data-label", t("Số lượng", "Quantity"))
            .addClass("text-md-center");
        var $stepper = $("<div>").addClass("bs-qty-stepper");
        var $btnMinus = $("<button>")
            .attr("type", "button")
            .attr("data-cart-step", "-1")
            .attr("data-line-id", String(lineId))
            .attr("aria-label", t("Giảm số lượng", "Decrease quantity"))
            .addClass("btn btn-sm btn-outline-secondary")
            .prop("disabled", quantity <= 1)
            .text("−");
        var qtyText = String(quantity);
        var $qtyInput = $("<input>")
            .attr("type", "number")
            .attr("min", "0")
            .attr("step", "1")
            .attr("inputmode", "numeric")
            .attr("value", qtyText)
            .attr("data-cart-qty-input", "true")
            .attr("data-prev-qty", qtyText)
            .attr("data-line-id", String(lineId))
            .attr("aria-label", t("Số lượng của " + title, "Quantity for " + title))
            .addClass("form-control form-control-sm bs-qty-input")
            .val(qtyText);
        if (available > 0) {
            $qtyInput.attr("max", String(available));
        }
        var $btnPlus = $("<button>")
            .attr("type", "button")
            .attr("data-cart-step", "1")
            .attr("data-line-id", String(lineId))
            .attr("aria-label", t("Tăng số lượng", "Increase quantity"))
            .addClass("btn btn-sm btn-outline-secondary")
            .prop("disabled", available > 0 && quantity >= available)
            .text("+");

        $stepper.append($btnMinus, $qtyInput, $btnPlus);
        $tdQty.append($stepper);
        $tr.append($tdQty);

        var $tdTotal = $("<td>")
            .attr("data-label", t("Thành tiền", "Line Total"))
            .addClass("text-md-end text-nowrap fw-bold bs-price-current")
            .text(moneyText(line.lineTotalText || line.LineTotalText));
        $tr.append($tdTotal);

        var $tdAction = $("<td>")
            .attr("data-label", t("Thao tác", "Action"))
            .addClass("text-md-end text-nowrap");
        $tdAction.append(
            $("<button>")
                .attr("type", "button")
                .attr("data-cart-remove", "true")
                .attr("data-line-id", String(lineId))
                .addClass("btn btn-sm btn-outline-danger")
                .text(t("Xóa", "Remove"))
        );
        $tr.append($tdAction);

        return $tr;
    }

    function renderCartPage($root, cart) {
        if (!cart || !Array.isArray(cart.items)) {
            return;
        }
        var items = cart.items;
        var rawCount = cart.itemCount !== undefined ? cart.itemCount : cart.ItemCount;
        var itemCountText = normalizeCountText(rawCount);
        $root.attr("data-cart-item-count", itemCountText);
        updateHeaderCartBadges(itemCountText);

        var $banner = $("#bs-cart-observation-banner");
        renderObservationBanners($banner, items);

        if (items.length === 0) {
            $("#bs-cart-filled").addClass("d-none");
            $("#bs-cart-empty").removeClass("d-none");
            $("#bs-cart-tbody").empty();
            return;
        }

        $("#bs-cart-empty").addClass("d-none");
        $("#bs-cart-filled").removeClass("d-none");

        var $tbody = $("#bs-cart-tbody");
        $tbody.empty();
        items.forEach(function (line) {
            $tbody.append(buildCartRow(line));
        });

        $("#bs-cart-summary-count").text(itemCountText);
        $("#bs-cart-subtotal").text(moneyText(cart.subtotalText || cart.SubtotalText));
        $("#bs-cart-shipping").text(moneyText(cart.shippingFeeText || cart.ShippingFeeText));
        $("#bs-cart-total").text(moneyText(cart.totalAmountText || cart.TotalAmountText));

        var canCheckout = Boolean(cart.canCheckout);
        var $checkoutLink = $("#bs-cart-checkout-link");
        var $cannotNote = $("#bs-cart-cannot-checkout");
        if (canCheckout) {
            $checkoutLink
                .removeClass("disabled pe-none opacity-50")
                .attr("aria-disabled", "false")
                .removeAttr("tabindex");
            $cannotNote.addClass("d-none");
        } else {
            $checkoutLink
                .addClass("disabled pe-none opacity-50")
                .attr("aria-disabled", "true")
                .attr("tabindex", "-1");
            $cannotNote.removeClass("d-none");
        }
    }

    function initHeaderCartSync() {
        var $cartPage = $("#bs-cart-page");
        var $checkoutPage = $("#bs-checkout-page");

        if ($cartPage.length > 0 && $cartPage.attr("data-cart-item-count") !== undefined) {
            updateHeaderCartBadges($cartPage.attr("data-cart-item-count"));
            return;
        }
        if ($checkoutPage.length > 0 && $checkoutPage.attr("data-cart-item-count") !== undefined) {
            updateHeaderCartBadges($checkoutPage.attr("data-cart-item-count"));
            return;
        }

        var isAuth = document.body.getAttribute("data-authenticated") === "true";
        if (!isAuth) {
            return;
        }

        $.ajax({
            url: "/api/cart",
            method: "GET",
            dataType: "json",
            headers: { "Accept": "application/json" }
        }).done(function (cart) {
            if (hasValidCartCount(cart)) {
                updateHeaderCartBadges(cart.itemCount !== undefined ? cart.itemCount : cart.ItemCount);
            } else {
                setHeaderCartBadgeError(null);
            }
        }).fail(function (jqXHR) {
            setHeaderCartBadgeError(jqXHR);
        });
    }

    function initAddToCartForms() {
        $(document).on("click", 'form[data-cart-add-form="true"] [data-qty-step]', function () {
            var $btn = $(this);
            var step = parseInt($btn.attr("data-qty-step"), 10) || 0;
            var $form = $btn.closest("form");
            var $input = $form.find('input[name="quantity"]');
            var current = parseInt($input.val(), 10);
            if (!Number.isInteger(current) || current < 1) {
                current = 1;
            }
            var min = parseInt($input.attr("min"), 10) || 1;
            var max = parseInt($input.attr("max"), 10) || 9999;
            var next = Math.min(max, Math.max(min, current + step));
            $input.val(String(next));
        });

        $(document).on("submit", 'form[data-cart-add-form="true"]', function (event) {
            event.preventDefault();
            var $form = $(this);
            if ($form.attr("data-busy") === "true") {
                return;
            }

            var bookId = parseInt($form.attr("data-book-id"), 10);
            var $qtyInput = $form.find('input[name="quantity"]');
            var rawQty = String($qtyInput.val() || "").trim();
            var quantity = /^\d+$/.test(rawQty) ? parseInt(rawQty, 10) : NaN;
            var $feedback = $form.find("[data-cart-feedback]");
            if ($feedback.length === 0) {
                $feedback = $form.closest("[data-book-id]").find("[data-cart-feedback]");
            }

            $feedback.empty().removeClass("d-none");

            var maxQty = parseInt($qtyInput.attr("max"), 10);
            if (!Number.isInteger(bookId) || bookId < 1 || !Number.isInteger(quantity) || quantity < 1
                || (Number.isInteger(maxQty) && maxQty > 0 && quantity > maxQty)) {
                var over = Number.isInteger(quantity) && Number.isInteger(maxQty) && maxQty > 0 && quantity > maxQty;
                // Trả ô số lượng về giá trị hợp lệ để không giữ lại 0, số âm, số lẻ hoặc số vượt tồn.
                $qtyInput.val(String(over ? maxQty : 1));
                $feedback.append(
                    $("<div>")
                        .addClass("alert alert-danger py-1 px-2 small mb-0 mt-2")
                        .text(over
                            ? t("Chỉ còn " + maxQty + " cuốn khả dụng; đã đặt lại số lượng về " + maxQty + ".", "Only " + maxQty + " copies are available; quantity reset to " + maxQty + ".")
                            : t("Số lượng thêm vào giỏ phải là số nguyên từ 1 trở lên; đã đặt lại về 1.", "Quantity must be a whole number of at least 1; reset to 1."))
                );
                return;
            }

            $form.attr("data-busy", "true");
            var $submitBtn = $form.find('button[type="submit"]');
            $submitBtn.prop("disabled", true);

            $.ajax({
                url: "/api/cart/items",
                method: "POST",
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                headers: {
                    "X-CSRF-TOKEN": getCsrfToken(),
                    "Accept": "application/json"
                },
                data: JSON.stringify({ bookId: bookId, quantity: quantity })
            }).done(function (cart) {
                if (hasValidCartCount(cart)) {
                    updateHeaderCartBadges(cart.itemCount !== undefined ? cart.itemCount : cart.ItemCount);
                }
                var $ok = $("<div>")
                    .addClass("alert alert-success py-1 px-2 small mb-0 mt-2 d-flex flex-wrap justify-content-between align-items-center gap-2")
                    .attr("role", "status");
                $ok.append($("<span>").text(t("Đã thêm vào giỏ hàng.", "Added to cart.")));
                $ok.append(
                    $("<a>")
                        .attr("href", "/gio-hang")
                        .addClass("fw-semibold text-decoration-none")
                        .text(t("Xem giỏ hàng", "View cart"))
                );
                $feedback.empty().append($ok);
                // Thêm xong đưa ô về 1 để lần bấm sau không vô tình thêm lại số lượng lớn.
                $qtyInput.val("1");
            }).fail(function (jqXHR) {
                var $err = buildErrorDom(jqXHR, true);
                $err.addClass("mt-2 py-2 px-2");
                $feedback.empty().append($err);
            }).always(function () {
                $form.removeAttr("data-busy");
                $submitBtn.prop("disabled", false);
            });
        });
    }

    function initCartPage() {
        var $root = $("#bs-cart-page");
        if ($root.length === 0) {
            return;
        }

        var $alertArea = $("#bs-cart-ajax-alert");
        var busy = false;

        function syncInitialCartQuantities() {
            $root.find("input[data-cart-qty-input]").each(function () {
                var $inp = $(this);
                setPreviousCartQty($inp, readPreviousCartQty($inp));
            });
        }

        function syncRowStepperButtons($row) {
            var $inp = $row.find("input[data-cart-qty-input]");
            if ($inp.length === 0) {
                return;
            }
            var valText = normalizeNonNegativeIntText($inp.val());
            var val = valText !== null ? parseInt(valText, 10) : (parseInt(readPreviousCartQty($inp), 10) || 0);
            var max = parseInt($inp.attr("max"), 10);
            $row.find('[data-cart-step="-1"]').prop("disabled", val <= 1);
            if (Number.isInteger(max) && max > 0) {
                $row.find('[data-cart-step="1"]').prop("disabled", val >= max);
            } else {
                $row.find('[data-cart-step="1"]').prop("disabled", false);
            }
        }

        function setBusy(state) {
            busy = state;
            $root.find("button, input[data-cart-qty-input]").prop("disabled", state);
            if (!state) {
                $root.find("tr[data-cart-line-id]").each(function () {
                    syncRowStepperButtons($(this));
                });
            }
        }

        function findLineQtyInput(lineId) {
            var targetId = String(lineId);
            return $root.find("input[data-cart-qty-input]").filter(function () {
                var $el = $(this);
                return $el.attr("data-line-id") === targetId
                    || $el.closest("tr[data-cart-line-id]").attr("data-cart-line-id") === targetId;
            });
        }

        function rollbackQtyInput($inp) {
            if (!$inp || $inp.length === 0) {
                return;
            }
            var prev = readPreviousCartQty($inp);
            setPreviousCartQty($inp, prev);
            $inp.val(prev);
            if (!busy) {
                var $row = $inp.closest("tr[data-cart-line-id]");
                if ($row.length > 0) {
                    syncRowStepperButtons($row);
                }
            }
        }

        function hasBadNumberInput($inp) {
            var el = $inp && $inp[0];
            return Boolean(el && el.validity && el.validity.badInput);
        }

        function showInvalidQuantityAlert() {
            var $invalid = $("<div>")
                .addClass("alert alert-danger py-2 px-3 small mb-0")
                .attr("role", "alert")
                .text(t(
                    "Không thể cập nhật số lượng giỏ hàng: Số lượng giỏ phải là số nguyên không âm (nhập 0 để xóa dòng). Giao diện đã khôi phục số lượng hợp lệ trước đó; hãy nhập lại số nguyên từ 0 trở lên.",
                    "Cannot update cart quantity: Quantity must be a non-negative integer (enter 0 to remove the item). Previous valid quantity was restored; enter a whole number of 0 or greater."
                ));
            $alertArea.empty().removeClass("d-none").append($invalid);
        }

        syncInitialCartQuantities();

        function clearAlert() {
            $alertArea.empty().addClass("d-none");
        }

        function showError(jqXHR, isWrite) {
            $alertArea.empty().removeClass("d-none").append(buildErrorDom(jqXHR, isWrite));
        }

        function showStatusMessage(msg) {
            var $ok = $("<div>")
                .addClass("alert alert-success py-2 px-3 small mb-0")
                .attr("role", "status")
                .text(msg);
            $alertArea.empty().removeClass("d-none").append($ok);
        }

        function applyCartResponse(cart, successMsg) {
            if (cart && Array.isArray(cart.items)) {
                renderCartPage($root, cart);
                if (successMsg) {
                    showStatusMessage(successMsg);
                } else {
                    clearAlert();
                }
                return;
            }
            $.ajax({
                url: "/api/cart",
                method: "GET",
                dataType: "json",
                headers: { "Accept": "application/json" }
            }).done(function (freshCart) {
                renderCartPage($root, freshCart);
                if (successMsg) {
                    showStatusMessage(successMsg);
                }
            }).fail(function (jqXHR) {
                showError(jqXHR, false);
            });
        }

        function updateLineQuantity(lineId, newQuantity) {
            var $inp = findLineQtyInput(lineId);
            if (busy) {
                rollbackQtyInput($inp);
                return;
            }
            if (!Number.isInteger(lineId) || lineId < 1 || !Number.isSafeInteger(newQuantity) || newQuantity < 0) {
                rollbackQtyInput($inp);
                showInvalidQuantityAlert();
                return;
            }
            setBusy(true);
            clearAlert();

            $.ajax({
                url: "/api/cart/items/" + encodeURIComponent(lineId),
                method: "PUT",
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                headers: {
                    "X-CSRF-TOKEN": getCsrfToken(),
                    "Accept": "application/json"
                },
                data: JSON.stringify({ quantity: newQuantity })
            }).done(function (cart) {
                if (newQuantity > 0) {
                    setPreviousCartQty($inp, newQuantity);
                    $inp.attr("value", String(newQuantity)).val(String(newQuantity));
                }
                setBusy(false);
                var msg = newQuantity === 0
                    ? t("Đã xóa đầu sách khỏi giỏ hàng.", "Removed item from cart.")
                    : t("Đã cập nhật số lượng giỏ hàng.", "Updated cart quantity.");
                applyCartResponse(cart, msg);
            }).fail(function (jqXHR) {
                rollbackQtyInput($inp);
                setBusy(false);
                showError(jqXHR, true);
            });
        }

        $root.on("click", "[data-cart-step]", function () {
            if (busy) {
                return;
            }
            var $btn = $(this);
            var lineId = parseInt($btn.attr("data-line-id"), 10);
            var step = parseInt($btn.attr("data-cart-step"), 10) || 0;
            var $inp = $btn.closest("tr").find("input[data-cart-qty-input]");
            var current = parseInt(readPreviousCartQty($inp), 10) || 1;
            var next = current + step;
            if (next < 1) {
                return;
            }
            updateLineQuantity(lineId, next);
        });

        $root.on("change", "input[data-cart-qty-input]", function () {
            var $inp = $(this);
            if (busy) {
                rollbackQtyInput($inp);
                return;
            }
            var lineId = parseInt($inp.attr("data-line-id"), 10);
            var raw = String($inp.val() || "").trim();
            if (hasBadNumberInput($inp) || !/^\d+$/.test(raw)) {
                rollbackQtyInput($inp);
                showInvalidQuantityAlert();
                return;
            }
            var parsed = parseInt(raw, 10);
            if (!Number.isSafeInteger(parsed) || parsed < 0) {
                rollbackQtyInput($inp);
                showInvalidQuantityAlert();
                return;
            }
            updateLineQuantity(lineId, parsed);
        });

        $root.on("focusout", "input[data-cart-qty-input]", function () {
            if (busy) {
                return;
            }
            var $inp = $(this);
            var raw = String($inp.val() || "").trim();
            if (hasBadNumberInput($inp) || !/^\d+$/.test(raw)) {
                rollbackQtyInput($inp);
                showInvalidQuantityAlert();
            }
        });

        $root.on("click", "button[data-cart-remove]", function () {
            if (busy) {
                return;
            }
            var lineId = parseInt($(this).attr("data-line-id"), 10);
            setBusy(true);
            clearAlert();

            $.ajax({
                url: "/api/cart/items/" + encodeURIComponent(lineId),
                method: "DELETE",
                headers: {
                    "X-CSRF-TOKEN": getCsrfToken(),
                    "Accept": "application/json"
                }
            }).done(function (cart) {
                setBusy(false);
                applyCartResponse(cart, t("Đã xóa đầu sách khỏi giỏ hàng.", "Removed item from cart."));
            }).fail(function (jqXHR) {
                setBusy(false);
                showError(jqXHR, true);
            });
        });

        $root.on("click", "#bs-cart-clear-btn", function () {
            if (busy) {
                return;
            }
            var confirmMsg = $(this).attr("data-confirm");
            if (confirmMsg && !window.confirm(confirmMsg)) {
                return;
            }
            setBusy(true);
            clearAlert();

            $.ajax({
                url: "/api/cart",
                method: "DELETE",
                headers: {
                    "X-CSRF-TOKEN": getCsrfToken(),
                    "Accept": "application/json"
                }
            }).done(function (cart) {
                setBusy(false);
                applyCartResponse(cart, t("Đã làm trống toàn bộ giỏ hàng.", "Cleared all items from your cart."));
            }).fail(function (jqXHR) {
                setBusy(false);
                showError(jqXHR, true);
            });
        });

        $root.on("click", "[data-cart-refresh]", function () {
            if (busy) {
                return;
            }
            setBusy(true);
            clearAlert();

            $.ajax({
                url: "/api/cart",
                method: "GET",
                dataType: "json",
                headers: { "Accept": "application/json" }
            }).done(function (cart) {
                setBusy(false);
                applyCartResponse(cart, t("Đã đồng bộ lại dữ liệu giỏ hàng mới nhất.", "Refreshed latest cart state."));
            }).fail(function (jqXHR) {
                setBusy(false);
                showError(jqXHR, false);
            });
        });
    }

    function initCustomerFormsAndFilters() {

        var customerFilterForms = document.querySelectorAll('form[data-customer-filter-form="true"]');
        customerFilterForms.forEach(function (form) {
            var stripPage = function () {
                form.querySelectorAll('input[name="Page"]').forEach(function (inp) {
                    inp.remove();
                });
            };
            form.addEventListener("submit", stripPage);
            form.querySelectorAll('[data-auto-submit="true"]').forEach(function (ctrl) {
                ctrl.addEventListener("change", function () {
                    stripPage();
                    form.submit();
                });
            });
        });

        bindGuardedForms();
    }

    // Xác nhận + chống gửi lặp dùng chung cho layout khách và quản trị. Mỗi form chỉ gắn một lần
    // (cờ data-guard-bound), tránh hai bộ lắng nghe chặn nhau khiến form không gửi được.
    function bindGuardedForms() {
        document.querySelectorAll("form[data-confirm], form[data-prevent-double-submit]").forEach(function (form) {
            if (form.dataset.guardBound === "true") {
                return;
            }
            form.dataset.guardBound = "true";
            form.addEventListener("submit", function (event) {
                if (form.dataset.submitting === "true") {
                    event.preventDefault();
                    return;
                }
                var msg = form.getAttribute("data-confirm");
                if (msg && !window.confirm(msg)) {
                    event.preventDefault();
                    return;
                }
                if (form.getAttribute("data-prevent-double-submit") === "true") {
                    // Chặn bấm lặp ngay lập tức; chỉ khóa nút khi trình duyệt thực sự gửi form. Nếu một listener
                    // khác (chạy sau) đã chặn gửi, mở cờ lại để form không bị khóa vĩnh viễn.
                    form.dataset.submitting = "true";
                    window.setTimeout(function () {
                        if (event.defaultPrevented) {
                            delete form.dataset.submitting;
                            return;
                        }
                        form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach(function (btn) {
                            btn.disabled = true;
                        });
                    }, 0);
                }
            });
        });
    }

    // Trang được khôi phục từ bfcache (nút Back) giữ trạng thái "đang gửi": mở khóa lại để gửi được.
    window.addEventListener("pageshow", function (event) {
        if (!event.persisted) {
            return;
        }
        document.querySelectorAll('form[data-submitting="true"]').forEach(function (form) {
            delete form.dataset.submitting;
            form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach(function (btn) {
                btn.disabled = false;
            });
        });
    });

    $(function () {
        initHeaderCartSync();
        initAddToCartForms();
        initCartPage();
        initCustomerFormsAndFilters();
    });
})(window.jQuery);

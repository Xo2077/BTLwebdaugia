$(function () {

    // Chỉ chạy AJAX ở các trang My Account.
    if (!$(".account[data-account-page]").length) {
        return;
    }

    let currentRequest = null;
    let requestVersion = 0;

    const $account = $(".account[data-account-page]");
    const $content = $("#account-content");

    // Lưu nội dung ban đầu để có thể phục hồi khi Back.
    const initialUrl = window.location.href;

    // Tạo thông báo tải/lỗi.
    const $status = $('<div>', {
        id: 'account-ajax-status',
        role: 'status',
        'aria-live': 'polite'
    });

    $content.wrap('<div class="account-body"></div>');
    $content.before($status);

    function setActiveMenu(url) {

        const path = new URL(url, window.location.href)
            .pathname
            .replace(/\/+$/, "")
            .toLowerCase();

        $(".sidebar-nav a[data-account-ajax]")
            .each(function () {

                const linkPath = new URL(
                    this.href,
                    window.location.href
                ).pathname.replace(/\/+$/, "").toLowerCase();

                $(this).toggleClass(
                    "active",
                    path === linkPath
                );
            });
    }

    function updateBreadcrumb(url) {
        function normalizePath(value) {
            return new URL(value, window.location.href).pathname
                .replace(/\/+$/, '').replace(/\/index$/i, '').toLowerCase();
        }
        const path = normalizePath(url);
        const $link = $('.sidebar-nav a[data-account-ajax]').filter(function () {
            return normalizePath(this.href) === path;
        }).first();
        const title = $link.text().trim();
        if (title) {
            $('.crumb [aria-current="page"]').first().text(title);
            document.title = title + ' | BidHub';
        }
    }

    function loadAccountPage(url, addHistory) {

        const normalizedUrl = new URL(
            url,
            window.location.href
        ).href;

        // Hủy request cũ nếu chuyển trang liên tục.
        if (currentRequest) {
            currentRequest.abort();
        }

        const myVersion = ++requestVersion;

        $status.text("Loading account information...");
        $content.attr("aria-busy", "true");

        const request = $.ajax({
            url: normalizedUrl,
            type: "GET",
            dataType: "html",
            cache: false,
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            }
        });

        currentRequest = request;

        request.done(function (html, textStatus, xhr) {

            if (myVersion !== requestVersion) {
                return;
            }

            // Nếu phiên đăng nhập đã hết hạn và server
            // điều hướng đến trang Sign In, mở trang đó.
            const responseUrl =
                xhr.responseURL || normalizedUrl;

            const expectedPath =
                new URL(normalizedUrl).pathname.toLowerCase();

            const actualPath =
                new URL(responseUrl, window.location.href)
                    .pathname.toLowerCase();

            if (
                actualPath !== expectedPath ||
                html.includes('<!DOCTYPE html>')
            ) {
                window.location.assign(responseUrl);
                return;
            }

            // Thay nội dung bên phải bằng PartialView.
            $content.html(html);

            setActiveMenu(normalizedUrl);
            updateBreadcrumb(normalizedUrl);

            $status.text("");

            // Cập nhật URL mà không reload.
            if (addHistory) {
                window.history.pushState(
                    { accountAjax: true },
                    "",
                    normalizedUrl
                );
            }
        });

        request.fail(function (xhr, status) {

            if (
                status === "abort" ||
                myVersion !== requestVersion
            ) {
                return;
            }

            // Phản hồi Unauthorized/Forbidden.
            if (xhr.status === 401 || xhr.status === 403) {
                window.location.assign(normalizedUrl);
                return;
            }

            $status.text(
                "Unable to load this page. Please try again."
            );

            console.error("Account AJAX error:", xhr.status);
        });

        request.always(function () {

            if (myVersion !== requestVersion) {
                return;
            }

            $content.removeAttr("aria-busy");

            if (currentRequest === request) {
                currentRequest = null;
            }
        });
    }

    // Bắt sự kiện click sidebar dùng chung.
    $(document).on(
        "click",
        ".sidebar-nav a[data-account-ajax]",
        function (event) {

            // Giữ hành vi mở tab mới hoặc Ctrl+Click.
            if (
                event.ctrlKey ||
                event.metaKey ||
                event.shiftKey ||
                event.altKey ||
                event.button !== 0
            ) {
                return;
            }

            event.preventDefault();

            const url = this.href;

            if (url === window.location.href) {
                return;
            }

            loadAccountPage(url, true);
        }
    );

    // Xử lý nút Back / Forward.
    window.addEventListener("popstate", function () {

        if (
            !document.querySelector(
                ".account[data-account-page]"
            )
        ) {
            window.location.reload();
            return;
        }

        loadAccountPage(window.location.href, false);
    });

    // Đồng bộ tiêu đề và breadcrumb lần đầu.
    updateBreadcrumb(initialUrl);
});

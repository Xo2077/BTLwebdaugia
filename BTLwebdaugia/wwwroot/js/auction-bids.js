$(function () {
    const $auction = $('.auction-page');
    if (!$auction.length) return;

    const $feedback = $('<p>', { role: 'status', class: 'notice text-danger', hidden: true });
    const pageUrl = window.location.href;
    const pollIntervalMs = 5000;
    let busy = false;

    function showError(message) {
        $feedback.text(message).prop('hidden', false);
        $('#auction-bid-panel').prepend($feedback);
    }

    function updateAuction($page, replacePanel) {
        const $nextAuction = $page.find('.auction-page');
        const status = $nextAuction.attr('data-auction-status');
        $('#auction-history').html($page.find('#auction-history').html());

        if (replacePanel || status !== $auction.attr('data-auction-status')) {
            $('#auction-bid-panel').html($page.find('#auction-bid-panel').html());
        } else {
            // Leave the user's typed bid and focus intact during background refreshes.
            ['#auction-price-label', '#auction-price', '#auction-minimum-bid'].forEach(function (selector) {
                $(selector).text($page.find(selector).text());
            });
            const minimum = $page.find('#bid-value').attr('min');
            if (minimum) $('#bid-value').attr('min', minimum);
        }
        $auction.attr('data-auction-status', status);
        $('.auction-status-row').html($page.find('.auction-status-row').html());
        if (!$feedback.prop('hidden')) $('#auction-bid-panel').prepend($feedback);
    }

    function refresh(replacePanel) {
        return $.get(pageUrl, null, null, 'html').then(function (html) {
            const $page = $('<div>').append($.parseHTML(html));
            if (!$page.find('#auction-history').length || !$page.find('#auction-bid-panel').length) {
                return $.Deferred().reject().promise();
            }
            updateAuction($page, replacePanel);
        });
    }

    $(document).on('submit', '#auction-bid-form', function (event) {
        event.preventDefault();
        if (busy) return;
        busy = true;
        $feedback.prop('hidden', true);
        const $button = $(this).find(':submit').prop('disabled', true);
        $.ajax({ url: this.action, method: 'POST', data: $(this).serialize(), dataType: 'json' })
            .fail(function (xhr) {
                showError(xhr.responseJSON?.message || $auction.attr('data-bid-error'));
            }).always(function () {
                refresh(true).fail(function () {
                    if ($feedback.prop('hidden')) showError($auction.attr('data-load-error'));
                }).always(function () {
                    busy = false;
                    $button.prop('disabled', false);
                });
            });
    });

    if ($auction.attr('data-auction-status') !== 'Ended') {
        const timer = setInterval(function () {
            if ($auction.attr('data-auction-status') === 'Ended') {
                clearInterval(timer);
                return;
            }
            if (busy || document.hidden) return;
            busy = true;
            refresh(false).fail(function () {
                showError($auction.attr('data-load-error'));
            }).always(function () { busy = false; });
        }, pollIntervalMs);
    }
});

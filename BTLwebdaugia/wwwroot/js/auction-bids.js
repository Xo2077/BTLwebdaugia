$(function () {
    const $feedback = $('<p>', { role: 'status', class: 'notice text-danger', hidden: true });
    const pageUrl = window.location.href;
    let busy = false;

    function refresh(updatePanel) {
        return $.get(pageUrl, function (html) {
            const $page = $('<div>').append($.parseHTML(html));
            const $history = $page.find('#auction-history');
            if (!$history.length) return;

            $('#auction-history').html($history.html());
            if (updatePanel) {
                const $panel = $page.find('#auction-bid-panel');
                if ($panel.length) $('#auction-bid-panel').html($panel.html());
            }
        }, 'html');
    }

    $(document).on('submit', '#auction-bid-form', function (event) {
        event.preventDefault();
        if (busy) return;
        busy = true;

        const $form = $(this);
        $feedback.prop("hidden", true);
        const $button = $form.find(':submit').prop('disabled', true);
        $.ajax({
            url: this.action,
            method: 'POST',
            data: $form.serialize(),
            dataType: 'json'
        }).fail(function (xhr) {
            $feedback.text(xhr.responseJSON?.message || 'Unable to submit your bid.').prop('hidden', false);
        }).always(function () {
            refresh(true).always(function () {
                busy = false;
                $("#auction-bid-panel").prepend($feedback);
                $button.prop('disabled', false);
            });
        });
    });

    if ($('.countdown').length) {
        const timer = setInterval(function () {
            if (!$('.countdown').length) clearInterval(timer);
            if (busy || document.hidden) return;
            busy = true;
            refresh(false).always(function () { busy = false; });
        }, 5000);
    }
});

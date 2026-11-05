$(function () {
    const $root = $('.notification-dropdown');
    if (!$root.length) return;
    const $toggle = $('#notification-toggle');
    const $menu = $('#notification-menu');
    const $count = $('#notification-count');
    let request;
    let opening = false;

    function updateCount() {
        const unread = Number($menu.find('.notification-list').attr('data-unread')) || 0;
        $count.text(unread > 99 ? '99+' : unread).prop('hidden', unread === 0);
    }

    function refresh(page) {
        if (request || opening) return;
        request = $.ajax({
            url: $root.attr('data-list-url'),
            data: { page: page },
            dataType: 'html',
            cache: false
        }).done(function (html) {
            const $list = $(html).filter('.notification-list');
            if (!$list.length) return;
            $menu.empty().append($list);
            updateCount();
        }).always(function () { request = null; });
    }

    $toggle.on('click', function () {
        const show = $menu.prop('hidden');
        $menu.prop('hidden', !show);
        $toggle.attr('aria-expanded', String(show));
        if (show) refresh(1);
    });

    $(document).on('click', function (event) {
        if (!$(event.target).closest('.notification-dropdown').length) {
            $menu.prop('hidden', true);
            $toggle.attr('aria-expanded', 'false');
        }
    }).on('keydown', function (event) {
        if (event.key === 'Escape' && !$menu.prop('hidden')) {
            $menu.prop('hidden', true);
            $toggle.attr('aria-expanded', 'false').trigger('focus');
        }
    });

    $menu.on('click', '[data-notification-page]', function () {
        refresh(Number($(this).attr('data-notification-page')));
    }).on('submit', '.notification-item', function (event) {
        event.preventDefault();
        if (opening) return;
        opening = true;
        if (request) request.abort();
        const $form = $(this);
        const $button = $form.find('button').prop('disabled', true);
        $.ajax({
            url: this.action,
            method: 'POST',
            data: $form.serialize(),
            dataType: 'json'
        }).done(function (result) {
            const target = new URL(result.url, location.origin);
            if (target.origin === location.origin) window.location.assign(target.href);
        }).fail(function () {
            $menu.find('.notification-error').remove();
            $('<p>', { class: 'notification-error text-danger', role: 'alert' })
                .text($root.attr('data-error')).prependTo($menu);
        }).always(function () {
            opening = false;
            $button.prop('disabled', false);
        });
    });

    updateCount();
    setInterval(function () {
        if (document.hidden) return;
        refresh(Number($menu.find('.notification-list').attr('data-page')) || 1);
    }, 15000);
});

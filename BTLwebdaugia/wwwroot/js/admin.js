$(function () {
    const $content = $('#admin-content');
    const $feedback = $('#admin-feedback');
    let navigation;
    let version = 0;
    let saving = false;

    function message(text, error) {
        $feedback.text(text || '').prop('hidden', !text).toggleClass('text-danger', !!error);
    }

    function load(url, pushHistory) {
        if (navigation) navigation.abort();
        const current = ++version;
        $content.attr('aria-busy', 'true');
        message($feedback.attr('data-loading'), false);
        navigation = $.ajax({ url: url, dataType: 'html', cache: false })
            .done(function (html) {
                if (current !== version) return;
                const $page = $('<div>').append($.parseHTML(html));
                const $next = $page.find('#admin-content');
                if (!$next.length) {
                    window.location.assign(url);
                    return;
                }
                $content.html($next.html());
                document.title = $page.find('title').text() || document.title;
                if (pushHistory) window.history.pushState({}, '', url);
                $('.admin-sidebar a').each(function () {
                    $(this).toggleClass('active',
                        new URL(this.href).pathname.toLowerCase() === new URL(url, location.href).pathname.toLowerCase());
                });
                if ($.validator && $.validator.unobtrusive) $.validator.unobtrusive.parse($content);
                message('', false);
            })
            .fail(function (xhr, status) {
                if (status !== 'abort' && current === version)
                    message($feedback.attr('data-error'), true);
            })
            .always(function () {
                if (current === version) {
                    navigation = null;
                    $content.removeAttr('aria-busy');
                }
            });
        return navigation;
    }

    $(document).on('click', '.admin-pager a, .admin-sidebar a', function (event) {
        if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0 || saving) return;
        event.preventDefault();
        load(this.href, true);
    });

    $(document).on('submit', '.admin-filter-form', function (event) {
        event.preventDefault();
        if (saving) return;
        const url = new URL(window.location.href);
        url.search = $(this).serialize();
        load(url.href, true);
    });

    $(document).on('submit', '#admin-content form[method="post"]', function (event) {
        event.preventDefault();
        if (saving || navigation) return;
        const $form = $(this);
        const confirmation = $form.find('[data-confirm]').attr('data-confirm') ||
            $(document.activeElement).attr('data-confirm');
        if (confirmation && !window.confirm(confirmation)) return;
        if ($.validator && !$form.valid()) return;
        saving = true;
        $form.find('[data-valmsg-for]').text('');
        const formId = this.id;
        const $externalButtons = formId ? $('[form]').filter(function () {
            return this.getAttribute('form') === formId;
        }) : $();
        const $buttons = $form.find(':submit').add($externalButtons).prop('disabled', true);
        const data = $form.serializeArray();
        data.push({ name: 'returnUrl', value: location.pathname + location.search });
        $.ajax({ url: this.action, method: 'POST', data: $.param(data), dataType: 'json' })
            .done(function (result) {
                load(location.href, false).done(function () {
                    message(result.message, false);
                });
            })
            .fail(function (xhr) {
                const result = xhr.responseJSON;
                message(result?.message || $feedback.attr(xhr.status === 401 || xhr.status === 403 ? 'data-access' : 'data-error'), true);
                if (result?.errors) {
                    Object.entries(result.errors).forEach(function ([field, errors]) {
                        $form.find('[data-valmsg-for]').filter(function () {
                            return $(this).attr('data-valmsg-for') === field;
                        }).text(errors.join(' '));
                    });
                }
            })
            .always(function () {
                saving = false;
                $buttons.prop('disabled', false);
            });
    });

    window.addEventListener('popstate', function () {
        load(location.href, false);
    });
    if ($.validator && $.validator.unobtrusive) $.validator.unobtrusive.parse($content);
});

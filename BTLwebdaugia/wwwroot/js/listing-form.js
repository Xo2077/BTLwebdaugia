document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('create-product-form');
    if (!form) return;

    const textarea = document.getElementById('product-description');
    if (textarea && typeof EasyMDE !== 'undefined') {
        const editPermission = document.getElementById('CanEditProduct');
        const editor = new EasyMDE({
            element: textarea,
            readOnly: editPermission ? editPermission.value !== 'true' : false,
            spellChecker: false,
            status: false,
            autoDownloadFontAwesome: false,
            toolbar: ['bold', 'italic', 'heading', '|', 'quote', 'unordered-list',
                'ordered-list', '|', 'link', 'preview', 'guide']
        });
        form.addEventListener('submit', function () {
            editor.codemirror.save();
        });
    }

    const endMode = document.getElementById('end-time-mode');
    const durationGroup = document.getElementById('duration-group');
    const specificEndGroup = document.getElementById('specific-end-group');
    if (!endMode || !durationGroup || !specificEndGroup) return;

    function updateEndMode() {
        const isDuration = endMode.value === 'Duration';
        durationGroup.hidden = !isDuration;
        specificEndGroup.hidden = isDuration;
    }
    endMode.addEventListener('change', updateEndMode);
    updateEndMode();
});

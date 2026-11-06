$(function () {
    $('.project-select').on('select2:select', function (e) {
        if (typeof gtag === 'function') {
            gtag('event', 'project_selected', {
                project_id: e.params.data.id,
                project_name: e.params.data.text
            });
        }
    });
});
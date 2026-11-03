$(function () {
    $('.project-select').select2({
        width: '100%',
        placeholder: '— оберіть проєкт —',
        minimumInputLength: 3,
        ajax: {
            url: '/api/projects/search',
            delay: 300,  
            dataType: 'json',
            data: function (params) { return { term: params.term }; }
        },
        language: {
            inputTooShort: function () { return 'Введіть щонайменше 3 символи'; },
            noResults: function () { return 'Нічого не знайдено'; },
            searching: function () { return 'Пошук…'; }
        }
    });
});
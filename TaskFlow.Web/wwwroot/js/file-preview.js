$(function () {
    var input = document.getElementById('Attachment');
    var box = document.getElementById('attachment-preview');
    if (!input || !box) return;

    var MAX_SIZE = 5 * 1024 * 1024; 
    var ALLOWED = ['pdf', 'png', 'jpg', 'jpeg', 'gif', 'txt', 'docx', 'xlsx', 'zip'];

    function message(text, cssClass) {
        var p = document.createElement('div');
        p.className = cssClass;
        p.textContent = text;
        box.appendChild(p);
    }

    input.addEventListener('change', function () {
        box.innerHTML = '';
        var file = input.files[0];
        if (!file) return;

        var ext = file.name.split('.').pop().toLowerCase();

        if (ALLOWED.indexOf(ext) === -1) {
            message('Тип файлу ".' + ext + '" не підтримується.', 'text-danger');
            input.value = '';
            return;
        }
        if (file.size > MAX_SIZE) {
            message('Файл завеликий (' + (file.size / 1024 / 1024).toFixed(1) + ' МБ). Максимум 5 МБ.', 'text-danger');
            input.value = '';
            return;
        }

        message(file.name + ' — ' + (file.size / 1024).toFixed(1) + ' КБ, тип: ' + (file.type || 'невідомий'), 'form-text');

        var reader = new FileReader();

        if (file.type.indexOf('image/') === 0) {
            reader.onload = function (e) {
                var img = document.createElement('img');
                img.src = e.target.result;  
                img.style.maxHeight = '150px';
                img.className = 'img-thumbnail';
                box.appendChild(img);
            };
            reader.readAsDataURL(file);
        } else if (ext === 'txt') {
            reader.onload = function (e) {
                var pre = document.createElement('pre');
                pre.className = 'border p-2 small';
                pre.textContent = e.target.result.slice(0, 300);
                box.appendChild(pre);
            };
            reader.readAsText(file);
        }
    });
});
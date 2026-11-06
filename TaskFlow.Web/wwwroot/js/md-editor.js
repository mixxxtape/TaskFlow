(function () {
    var root = document.getElementById('md-editor');
    if (!root) return;

    var KEY = 'taskflow-md-editor';
    var output = document.getElementById('md-output');
    var TYPES = { h1: 'Заголовок 1', h2: 'Заголовок 2', p: 'Параграф', li: 'Список' };

    var blocks = load();
    var dragFrom = null;

    function newBlock(type, text) { return { type: type, text: text || '' }; }

    //відновлення документа з localStorage
    function load() {
        try {
            var saved = JSON.parse(localStorage.getItem(KEY));
            if (Array.isArray(saved) && saved.length) return saved;
        } catch (e) { }
        return [newBlock('h1', 'Мій документ'), newBlock('p', '')];
    }

    //автозбереження
    function save() {
        try { localStorage.setItem(KEY, JSON.stringify(blocks)); } catch (e) { }
        output.textContent = toMarkdown();
    }

    function toMarkdown() {
        var out = '';
        blocks.forEach(function (b, i) {
            var line = b.type === 'h1' ? '# ' + b.text
                : b.type === 'h2' ? '## ' + b.text
                    : b.type === 'li' ? '- ' + b.text
                        : b.text;
            if (i > 0) out += (b.type === 'li' && blocks[i - 1].type === 'li') ? '\n' : '\n\n';
            out += line;
        });
        return out;
    }

    function resize(el) {
        el.style.height = 'auto';
        el.style.height = el.scrollHeight + 'px';
    }

    function render(focusIndex, flashIndex) {
        root.innerHTML = '';

        blocks.forEach(function (b, i) {
            var row = document.createElement('div');
            row.className = 'md-row md-' + b.type;

            var handle = document.createElement('span');
            handle.className = 'md-handle';
            handle.textContent = '⋮⋮';
            handle.title = 'Перетягни, щоб змінити порядок';
            row.appendChild(handle);

            if (b.type === 'li') {
                var bullet = document.createElement('span');
                bullet.className = 'md-bullet';
                bullet.textContent = '•';
                row.appendChild(bullet);
            }

            var text = document.createElement('textarea');
            text.className = 'form-control md-text';
            text.rows = 1;
            text.value = b.text;
            text.placeholder = TYPES[b.type];
            row.appendChild(text);

            var select = document.createElement('select');
            select.className = 'form-select form-select-sm md-type';
            Object.keys(TYPES).forEach(function (t) {
                var o = document.createElement('option');
                o.value = t;
                o.textContent = TYPES[t];
                if (t === b.type) o.selected = true;
                select.appendChild(o);
            });
            row.appendChild(select);

            var del = document.createElement('button');
            del.type = 'button';
            del.className = 'btn btn-sm btn-outline-secondary';
            del.textContent = '×';
            row.appendChild(del);

            //редагування 
            text.addEventListener('input', function () {
                var v = text.value;
                if (b.type === 'p') {
                    if (v.indexOf('## ') === 0) { b.type = 'h2'; b.text = v.slice(3); save(); render(i); return; }
                    if (v.indexOf('# ') === 0) { b.type = 'h1'; b.text = v.slice(2); save(); render(i); return; }
                    if (v.indexOf('- ') === 0) { b.type = 'li'; b.text = v.slice(2); save(); render(i); return; }
                }
                b.text = v;
                resize(text);
                save();
            });

            text.addEventListener('keydown', function (e) {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    if (b.type === 'li' && b.text === '') { b.type = 'p'; save(); render(i); return; }
                    blocks.splice(i + 1, 0, newBlock(b.type === 'li' ? 'li' : 'p'));
                    save();
                    render(i + 1);
                } else if (e.key === 'Backspace' && b.text === '' && blocks.length > 1) {
                    e.preventDefault();
                    blocks.splice(i, 1);
                    save();
                    render(Math.max(0, i - 1));
                }
            });

            select.addEventListener('change', function () {
                b.type = select.value;
                save();
                render(i);
            });

            del.addEventListener('click', function () {
                if (blocks.length > 1) blocks.splice(i, 1); else blocks[0].text = '';
                save();
                render();
            });

            //drag-and-drop (F3) 
            handle.addEventListener('mousedown', function () { row.draggable = true; });

            row.addEventListener('dragstart', function (e) {
                dragFrom = i;
                e.dataTransfer.effectAllowed = 'move';
                e.dataTransfer.setData('text/plain', String(i));
                row.classList.add('md-dragging');
            });
            row.addEventListener('dragend', function () {
                row.draggable = false;
                row.classList.remove('md-dragging');
            });
            row.addEventListener('dragover', function (e) {
                e.preventDefault();
                row.classList.add('md-over');
            });
            row.addEventListener('dragleave', function () { row.classList.remove('md-over'); });
            row.addEventListener('drop', function (e) {
                e.preventDefault();
                if (dragFrom === null || dragFrom === i) { render(); return; }
                var moved = blocks.splice(dragFrom, 1)[0];
                blocks.splice(i, 0, moved);
                dragFrom = null;
                save();
                render(null, i);
            });

            root.appendChild(row);
        });

        Array.prototype.forEach.call(root.querySelectorAll('textarea'), resize);

        var rows = root.querySelectorAll('.md-row');
        if (focusIndex != null && rows[focusIndex]) rows[focusIndex].querySelector('textarea').focus();
        if (flashIndex != null && rows[flashIndex]) rows[flashIndex].classList.add('md-flash');
    }

    document.getElementById('md-add').addEventListener('click', function () {
        blocks.push(newBlock('p'));
        save();
        render(blocks.length - 1);
    });

    document.getElementById('md-clear').addEventListener('click', function () {
        if (!confirm('Очистити документ?')) return;
        blocks = [newBlock('p')];
        save();
        render(0);
    });

    save();
    render();
})();
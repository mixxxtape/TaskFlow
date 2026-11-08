(function () {
    var root = document.getElementById('md-editor');
    if (!root) return;

    var KEY = 'taskflow-md-editor';
    var output = document.getElementById('md-output');
    var syncStatus = document.getElementById('md-sync-status'); // C13/C14: індикатор стану синхронізації
    var TYPES = { h1: 'Заголовок 1', h2: 'Заголовок 2', p: 'Параграф', li: 'Список' };

    var blocks = load();
    var dragFrom = null;
    var connection = null; // C13/C14: з'єднання SignalR

    function newBlock(type, text) { return { type: type, text: text || '' }; }

    function load() {
        try {
            var saved = JSON.parse(localStorage.getItem(KEY));
            if (Array.isArray(saved) && saved.length) return saved;
        } catch (e) { }
        return [newBlock('h1', 'Мій документ'), newBlock('p', '')];
    }

    // C13/C14: локальне збереження документа без мережевої синхронізації
    function persist() {
        try { localStorage.setItem(KEY, JSON.stringify(blocks)); } catch (e) { }
        output.textContent = toMarkdown();
    }

    // C13/C14: після локального збереження відправляємо зміни іншим клієнтам
    function save() {
        persist();
        broadcast();
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

    // C13/C14: додано можливість підсвічувати декілька змінених блоків
    function render(focusIndex, flashIndexes, flashClass) {
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
                render(null, [i]);
            });

            root.appendChild(row);
        });

        Array.prototype.forEach.call(root.querySelectorAll('textarea'), resize);

        var rows = root.querySelectorAll('.md-row');
        if (focusIndex != null && rows[focusIndex]) rows[focusIndex].querySelector('textarea').focus();
        // C13/C14: підсвічування блоків, які змінилися на іншому пристрої
        if (flashIndexes) {
            flashIndexes.forEach(function (i) {
                if (rows[i]) rows[i].classList.add(flashClass || 'md-flash');
            });
        }
    }

    // C13/C14: відображення стану синхронізації
    function setSync(text, cls) {
        if (!syncStatus) return;
        syncStatus.textContent = text;
        syncStatus.className = 'badge mb-2 text-bg-' + (cls || 'secondary');
    }

    // C13/C14: відправлення документа через SignalR
    function broadcast() {
        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            connection.invoke('SendDocument', JSON.stringify(blocks)).catch(function () { });
        }
    }

    // C13/C14: перевірка отриманого документа перед застосуванням
    function clean(incoming) {
        if (!Array.isArray(incoming) || !incoming.length) return null;
        return incoming.map(function (b) {
            return { type: TYPES[b && b.type] ? b.type : 'p', text: String((b && b.text) || '') };
        });
    }

    // C13/C14: отримання документа, зміненого іншим клієнтом
    function applyRemote(json) {
        var incoming;
        try { incoming = clean(JSON.parse(json)); } catch (e) { return; }
        if (!incoming) return;

        // які блоки змінилися (для анімації)
        var changed = [];
        incoming.forEach(function (b, i) {
            var old = blocks[i];
            if (!old || old.type !== b.type || old.text !== b.text) changed.push(i);
        });
        if (!changed.length && incoming.length === blocks.length) return;

        // запам'ятовуємо, де стояв курсор, щоб не збити друк
        var active = document.activeElement;
        var row = active && active.closest ? active.closest('.md-row') : null;
        var focusIndex = row ? Array.prototype.indexOf.call(root.children, row) : null;
        var caret = active && active.selectionStart != null ? active.selectionStart : null;

        blocks = incoming;
        persist();  
        render(focusIndex, changed, 'md-remote');

        if (focusIndex != null && caret != null && root.children[focusIndex]) {
            var ta = root.children[focusIndex].querySelector('textarea');
            if (ta) ta.setSelectionRange(caret, caret);
        }
        setSync('Отримано зміни з іншого пристрою', 'primary');
        setTimeout(function () { setSync('Синхронізація: увімкнена', 'success'); }, 2000);
    }

    if (window.signalR) {
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/live')
            .withAutomaticReconnect()
            .build();
        connection.on('DocumentChanged', applyRemote);
        connection.onreconnected(function () { setSync('Синхронізація: увімкнена', 'success'); });
        connection.onclose(function () { setSync('Синхронізація: відключено', 'danger'); });
        connection.start()
            .then(function () { setSync('Синхронізація: увімкнена', 'success'); })
            .catch(function () { setSync('Синхронізація: недоступна', 'danger'); });
    } else {
        setSync('Синхронізація: недоступна', 'danger');
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

    persist();
    render();
})();
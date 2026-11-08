(function () {
    function $(id) { return document.getElementById(id); }
    if (!$('live-start')) return;

    var connection = null, timer = null, observer = null;
    var lastId = 0, startedAt = 0;
    var stats = { requests: 0, bytes: 0, events: 0, latency: 0 };

    function showStats() {
        $('st-requests').textContent = stats.requests;
        $('st-bytes').textContent = stats.bytes;
        $('st-events').textContent = stats.events;
        $('st-latency').textContent = stats.events ? Math.round(stats.latency / stats.events) + ' мс' : '—';
    }

    function setStatus(text) { $('live-status').textContent = text; }

    // подія прийшла (будь-яким способом)
    function onEvent(e) {
        lastId = Math.max(lastId, e.id);
        if (e.sentAtMs < startedAt) return;          // стара подія, яка була ще до старту

        var delay = Math.max(0, Date.now() - e.sentAtMs);
        stats.events++;
        stats.latency += delay;

        var li = document.createElement('li');
        li.className = 'list-group-item py-1';
        li.textContent = '#' + e.id + ' ' + e.message + ' — затримка ' + delay + ' мс';
        $('live-log').prepend(li);
        showStats();
    }

    // рахуємо HTTP-запити до нашого сервера за даними браузера (Resource Timing)
    function startObserver() {
        observer = new PerformanceObserver(function (list) {
            list.getEntries().forEach(function (en) {
                if (en.name.indexOf('/hubs/live') !== -1 || en.name.indexOf('/api/live/latest') !== -1) {
                    stats.requests++;
                    stats.bytes += en.transferSize || 0;
                }
            });
            showStats();
        });
        observer.observe({ type: 'resource', buffered: false });
    }

    function poll() {
        fetch('/api/live/latest?afterId=' + lastId).then(function (r) {
            if (r.status === 200) return r.json().then(onEvent);   // 204 = нічого нового
        });
    }

    function stop() {
        if (timer) { clearInterval(timer); timer = null; }
        if (connection) { connection.stop(); connection = null; }
        if (observer) { observer.disconnect(); observer = null; }
        setStatus('Зупинено');
    }

    function start() {
        stop();
        var mode = document.querySelector('input[name=mode]:checked').value;
        stats = { requests: 0, bytes: 0, events: 0, latency: 0 };
        $('live-log').innerHTML = '';
        showStats();
        startedAt = Date.now();
        startObserver();

        if (mode === 'polling') {
            timer = setInterval(poll, 2000);
            setStatus('Працює: Polling');
            return;
        }

        var options = mode === 'longpolling'
            ? { transport: signalR.HttpTransportType.LongPolling }
            : { skipNegotiation: true, transport: signalR.HttpTransportType.WebSockets };

        connection = new signalR.HubConnectionBuilder().withUrl('/hubs/live', options).build();
        connection.on('LiveEvent', onEvent);
        connection.start()
            .then(function () { setStatus('Працює: ' + (mode === 'longpolling' ? 'Long Polling' : 'WebSocket')); })
            .catch(function (err) { setStatus('Помилка: ' + err); });
    }

    $('live-start').addEventListener('click', start);
    $('live-stop').addEventListener('click', stop);
    $('live-ping').addEventListener('click', function () {
        fetch('/api/live/ping', { method: 'POST' });
    });
})();
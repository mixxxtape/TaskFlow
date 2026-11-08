(function () {
    var canvas = document.getElementById('board');
    if (!canvas) return;

    var ctx = canvas.getContext('2d');
    ctx.lineCap = 'round';

    var colorInput = document.getElementById('board-color');
    var sizeInput = document.getElementById('board-size');
    var statusEl = document.getElementById('board-status');
    var usersEl = document.getElementById('board-users');

    var drawing = false;
    var last = null;

    function setStatus(text, cls) {
        statusEl.textContent = text;
        statusEl.className = 'badge text-bg-' + cls;
    }

    function pos(e) {
        var r = canvas.getBoundingClientRect();
        return {
            x: (e.clientX - r.left) * canvas.width / r.width,
            y: (e.clientY - r.top) * canvas.height / r.height
        };
    }

    function drawSegment(s) {
        ctx.strokeStyle = s.color;
        ctx.lineWidth = s.size;
        ctx.beginPath();
        ctx.moveTo(s.x0, s.y0);
        ctx.lineTo(s.x1, s.y1);
        ctx.stroke();
    }

    function clearCanvas() { ctx.clearRect(0, 0, canvas.width, canvas.height); }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/board')
        .withAutomaticReconnect()
        .build();

    connection.on('Stroke', drawSegment);   //чужий штрих
    connection.on('Cleared', clearCanvas);   //хтось очистив дошку
    connection.on('Users', function (n) { usersEl.textContent = n; });
    connection.on('History', function (strokes) {  //повна історія при підключенні
        clearCanvas();
        strokes.forEach(drawSegment);
    });

    connection.onreconnecting(function () { setStatus('Перепідключення…', 'warning'); });
    connection.onreconnected(function () { setStatus('Підключено', 'success'); });
    connection.onclose(function () { setStatus('Відключено', 'danger'); });

    connection.start()
        .then(function () { setStatus('Підключено', 'success'); })
        .catch(function () { setStatus('Недоступно', 'danger'); });

    function send(method, arg) {
        if (connection.state === signalR.HubConnectionState.Connected) {
            connection.invoke(method, arg).catch(function () { });
        }
    }

    //малювання
    function segment(from, to) {
        return {
            x0: from.x, y0: from.y, x1: to.x, y1: to.y,
            color: colorInput.value, size: parseInt(sizeInput.value, 10)
        };
    }

    canvas.addEventListener('pointerdown', function (e) {
        drawing = true;
        last = pos(e);
        canvas.setPointerCapture(e.pointerId);
        var dot = segment(last, last);  //крапка, якщо користувач просто клацнув
        drawSegment(dot);
        send('Draw', dot);
    });

    canvas.addEventListener('pointermove', function (e) {
        if (!drawing) return;
        var p = pos(e);
        var s = segment(last, p);
        drawSegment(s);
        send('Draw', s);
        last = p;
    });

    function stopDrawing() { drawing = false; }
    canvas.addEventListener('pointerup', stopDrawing);
    canvas.addEventListener('pointercancel', stopDrawing);

    document.getElementById('board-clear').addEventListener('click', function () {
        if (!confirm('Очистити дошку для всіх?')) return;
        clearCanvas();
        send('Clear');
    });
})();
(function () {
    var canvas = document.getElementById('game');
    if (!canvas) return;

    var ctx = canvas.getContext('2d');
    var W = canvas.width, H = canvas.height;
    var player, items, score, lives, over, spawnTimer, best = 0;
    var keys = {};

    function reset() {
        player = { x: W / 2 - 30, y: H - 24, w: 60, h: 12 };
        items = [];
        score = 0;
        lives = 3;
        over = false;
        spawnTimer = 0;
    }

    document.addEventListener('keydown', function (e) {
        if (e.key === 'ArrowLeft' || e.key === 'ArrowRight' || e.key === ' ') e.preventDefault();
        keys[e.key] = true;
        if (over && e.key === ' ') reset();
    });
    document.addEventListener('keyup', function (e) { keys[e.key] = false; });

    canvas.addEventListener('mousemove', function (e) {
        var rect = canvas.getBoundingClientRect();
        player.x = e.clientX - rect.left - player.w / 2;
    });
    canvas.addEventListener('click', function () { if (over) reset(); });

    function update() {
        if (over) return;

        if (keys['ArrowLeft']) player.x -= 6;
        if (keys['ArrowRight']) player.x += 6;
        player.x = Math.max(0, Math.min(W - player.w, player.x));

        //кожні ~80 кадрів з'являється нове завдання у випадковому місці
        spawnTimer++;
        if (spawnTimer > 80) {
            spawnTimer = 0;
            items.push({ x: Math.random() * (W - 24), y: -24, s: 24, speed: 1 + score * 0.05 });
        }

        items.forEach(function (i) { i.y += i.speed; });

        items = items.filter(function (i) {
            var caught = i.y + i.s >= player.y && i.y <= player.y + player.h &&
                i.x + i.s >= player.x && i.x <= player.x + player.w;
            if (caught) { score++; return false; }
            if (i.y > H) { lives--; return false; }
            return true;
        });

        if (lives <= 0) {
            over = true;
            best = Math.max(best, score);
        }
    }

    function draw() {
        ctx.clearRect(0, 0, W, H);

        ctx.fillStyle = '#0d6efd';
        items.forEach(function (i) { ctx.fillRect(i.x, i.y, i.s, i.s); });

        ctx.fillStyle = '#212529';
        ctx.fillRect(player.x, player.y, player.w, player.h);

        ctx.fillStyle = '#212529';
        ctx.font = '16px sans-serif';
        ctx.fillText('Рахунок: ' + score, 10, 22);
        ctx.fillText('Життя: ' + lives, W - 90, 22);

        if (over) {
            ctx.fillStyle = 'rgba(0,0,0,0.6)';
            ctx.fillRect(0, 0, W, H);
            ctx.fillStyle = '#fff';
            ctx.font = '28px sans-serif';
            ctx.fillText('Гру закінчено', 110, H / 2 - 20);
            ctx.font = '16px sans-serif';
            ctx.fillText('Рахунок: ' + score + '   Рекорд: ' + best, 110, H / 2 + 10);
            ctx.fillText('Пробіл або клік, щоб почати знову', 80, H / 2 + 40);
        }
    }

    function loop() {
        update();
        draw();
        requestAnimationFrame(loop);
    }

    reset();
    loop();
})();
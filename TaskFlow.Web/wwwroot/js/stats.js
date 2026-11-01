// F1: завантажує готові числа з /Stats/Data і малює їх Chart.js
(async function () {
    const root = document.getElementById('stats-root');
    if (!root) return;

    const response = await fetch(root.dataset.url);
    if (!response.ok) {
        document.getElementById('stats-total').textContent = 'Не вдалося завантажити статистику.';
        return;
    }
    const data = await response.json();
    document.getElementById('stats-total').textContent = 'Усього завдань: ' + data.total;

    const labels = items => items.map(x => x.label);
    const values = items => items.map(x => x.value);

    new Chart(document.getElementById('chartStatus'), {
        type: 'doughnut',
        data: {
            labels: labels(data.byStatus),
            datasets: [{ data: values(data.byStatus), backgroundColor: ['#6c757d', '#0d6efd', '#198754'] }]
        },
        options: { plugins: { legend: { position: 'bottom' } } }
    });

    const bar = (id, items, horizontal) => new Chart(document.getElementById(id), {
        type: 'bar',
        data: { labels: labels(items), datasets: [{ data: values(items), backgroundColor: '#0d6efd' }] },
        options: {
            indexAxis: horizontal ? 'y' : 'x',
            plugins: { legend: { display: false } },
            scales: { [horizontal ? 'x' : 'y']: { beginAtZero: true, ticks: { precision: 0 } } }
        }
    });

    bar('chartProject', data.byProject, true);
    bar('chartAssignee', data.byAssignee, false);
})();

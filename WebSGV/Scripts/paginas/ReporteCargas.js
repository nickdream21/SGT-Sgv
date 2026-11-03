// Reporte de cargas: pestañas (Resumen / Por pedido), gráficos del resumen y expandir / contraer pedidos.
(function () {
    // ---------- Por pedido: expandir / contraer ----------
    function marcar(abrir) {
        document.querySelectorAll('details.rc-grupo').forEach(function (d) { d.open = abrir; });
    }
    var expandir = document.getElementById('rcExpandir');
    var contraer = document.getElementById('rcContraer');
    if (expandir) expandir.addEventListener('click', function () { marcar(true); });
    if (contraer) contraer.addEventListener('click', function () { marcar(false); });

    // ---------- Gráficos ----------
    var datos = SGV.carga;
    var charts = {};
    var REDUCIR_MOVIMIENTO = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var AZUL = '#2563eb', AZUL_SUAVE = 'rgba(37, 99, 235, .78)';
    var fmtT = new Intl.NumberFormat('es-PE', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

    // Callbacks a prueba de listas vacías (mismo arreglo que el dashboard de exportación).
    function primero(it) { return it && it.length ? it[0] : null; }

    function crear(id, config) {
        if (charts[id]) charts[id].destroy();
        var canvas = document.getElementById(id);
        if (!canvas || !window.Chart) return;
        charts[id] = new Chart(canvas, config);
    }

    function base(extra) {
        return Object.assign({
            responsive: true,
            maintainAspectRatio: false,
            animation: REDUCIR_MOVIMIENTO ? false : { duration: 500 },
            plugins: {
                legend: { display: false },
                tooltip: { backgroundColor: '#1f2937', padding: 10, cornerRadius: 6, titleFont: { weight: '600' } }
            }
        }, extra);
    }

    function dibujar() {
        if (!datos || !window.Chart) return;

        var periodo = datos.periodo || [];
        crear('rcChartPeriodo', {
            type: 'bar',
            data: {
                labels: periodo.map(function (p) { return p.e; }),
                datasets: [{ data: periodo.map(function (p) { return p.t; }), backgroundColor: AZUL_SUAVE, hoverBackgroundColor: AZUL, borderRadius: 3, maxBarThickness: 28 }]
            },
            options: base({
                interaction: { mode: 'index', intersect: false },
                scales: {
                    x: { grid: { display: false }, ticks: { color: '#6b7280', font: { size: 11 }, autoSkip: true, maxRotation: 0 } },
                    y: { beginAtZero: true, border: { display: false }, grid: { color: '#eef1f5' },
                         ticks: { color: '#6b7280', font: { size: 11 }, maxTicksLimit: 6 },
                         title: { display: true, text: 'Toneladas', color: '#6b7280', font: { size: 11 } } }
                },
                plugins: Object.assign({}, base().plugins, {
                    tooltip: Object.assign({}, base().plugins.tooltip, {
                        callbacks: {
                            label: function (it) {
                                var p = periodo[it.dataIndex];
                                return p ? fmtT.format(p.t) + ' t · ' + p.v + (p.v === 1 ? ' viaje' : ' viajes') : '';
                            }
                        }
                    })
                })
            })
        });

        var clientes = datos.clientes || [];
        crear('rcChartClientes', {
            type: 'bar',
            data: {
                labels: clientes.map(function (c) { return c.c.length > 28 ? c.c.substring(0, 27) + '…' : c.c; }),
                datasets: [{ data: clientes.map(function (c) { return c.t; }), backgroundColor: AZUL_SUAVE, hoverBackgroundColor: AZUL, borderRadius: 3, maxBarThickness: 22 }]
            },
            options: base({
                indexAxis: 'y',
                scales: {
                    x: { beginAtZero: true, border: { display: false }, grid: { color: '#eef1f5' }, ticks: { color: '#6b7280', font: { size: 11 }, maxTicksLimit: 5 } },
                    y: { grid: { display: false }, ticks: { color: '#374151', font: { size: 11 } } }
                },
                plugins: Object.assign({}, base().plugins, {
                    tooltip: Object.assign({}, base().plugins.tooltip, {
                        callbacks: {
                            title: function (it) { var p = primero(it); return p ? clientes[p.dataIndex].c : ''; },
                            label: function (it) {
                                var c = clientes[it.dataIndex];
                                return c ? fmtT.format(c.t) + ' t · ' + c.v + (c.v === 1 ? ' viaje' : ' viajes') : '';
                            }
                        }
                    })
                })
            })
        });
    }

    // ---------- Pestañas: abrir la guardada y recordarla en el postback ----------
    var hf = document.getElementById(SGV.hfPestana);
    var pestana = (hf && hf.value) || 'resumen';
    var enlace = document.querySelector('.rc-pestanas a[data-pestana="' + pestana + '"]') ||
                 document.querySelector('.rc-pestanas a[data-pestana="resumen"]');

    if (window.jQuery && enlace) {
        jQuery('.rc-pestanas a[data-toggle="tab"]').on('shown.bs.tab', function (e) {
            if (hf) hf.value = e.target.getAttribute('data-pestana');
            // Un canvas dibujado en una pestaña oculta mide 0: se dibuja al mostrarse.
            if (e.target.getAttribute('data-pestana') === 'resumen') dibujar();
        });
        jQuery(enlace).tab('show');
    } else {
        dibujar();
    }
})();

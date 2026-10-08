(function () {
    // Tabs
    document.querySelectorAll('.de-tab').forEach(function (tab) {
        tab.addEventListener('click', function () {
            var target = this.getAttribute('data-target');
            document.querySelectorAll('.de-tab').forEach(function (t) { t.classList.remove('active'); });
            document.querySelectorAll('.de-tab-panel').forEach(function (p) { p.classList.remove('active'); });
            this.classList.add('active');
            var panel = document.getElementById(target);
            if (panel) {
                panel.classList.add('active');
                // Forzar resize de chart cuando se muestra
                setTimeout(function () {
                    for (var k in window.deCharts) {
                        if (window.deCharts[k] && typeof window.deCharts[k].resize === 'function') {
                            window.deCharts[k].resize();
                        }
                    }
                }, 60);
            }
        });
    });
})();

function deRenderCharts() {
    var hdn = document.getElementById(SGV.hdnDatos);
    if (!hdn || !hdn.value) return;
    var d;
    try { d = JSON.parse(hdn.value); } catch (e) { console.error('JSON dashboard inválido', e); return; }

    window.deCharts = window.deCharts || {};
    // Destruir previos
    Object.keys(window.deCharts).forEach(function (k) {
        if (window.deCharts[k]) { window.deCharts[k].destroy(); window.deCharts[k] = null; }
    });

    var palette = {
        primary:  '#2563EB',
        accent:   '#00D4AA',
        warning:  '#F59E0B',
        danger:   '#EF4444',
        violet:   '#8B5CF6',
        ink:      '#0B1426',
        soft:     '#94A3B8'
    };

    // Registrar plugin annotation si está disponible
    if (window['chartjs-plugin-annotation'] && Chart.registry && !Chart.registry.plugins.get('annotation')) {
        try { Chart.register(window['chartjs-plugin-annotation']); } catch(e) {}
    }

    // Helper: crea un gradiente vertical para áreas
    function gradientFor(ctx, area, hex) {
        if (!area) return hex + '33';
        var g = ctx.createLinearGradient(0, area.top, 0, area.bottom);
        g.addColorStop(0, hex + 'CC');
        g.addColorStop(1, hex + '08');
        return g;
    }

    // Plugin: dibuja valor + label en el centro de un doughnut
    var centerTextPlugin = {
        id: 'centerText',
        afterDraw: function (chart, args, opts) {
            if (!opts || !opts.text) return;
            var ctx2 = chart.ctx;
            var x = (chart.chartArea.left + chart.chartArea.right) / 2;
            var y = (chart.chartArea.top + chart.chartArea.bottom) / 2;
            ctx2.save();
            ctx2.textAlign = 'center';
            ctx2.textBaseline = 'middle';
            ctx2.fillStyle = palette.ink;
            ctx2.font = '800 24px "Plus Jakarta Sans"';
            ctx2.fillText(opts.text, x, y - 8);
            if (opts.subtext) {
                ctx2.fillStyle = palette.soft;
                ctx2.font = '600 11px "Plus Jakarta Sans"';
                ctx2.fillText(opts.subtext, x, y + 14);
            }
            ctx2.restore();
        }
    };

    var commonOpts = {
        responsive: true,
        maintainAspectRatio: false,
        layout: { padding: { top: 10, left: 0, right: 0, bottom: 0 } },
        plugins: {
            legend: { position: 'bottom', labels: { font: { family: "'Plus Jakarta Sans'", size: 12, weight: '600' }, color: palette.ink, boxWidth: 12, usePointStyle: true } },
            tooltip: { backgroundColor: palette.ink, padding: 10, titleFont: { weight: '700' }, cornerRadius: 8 }
        },
        scales: {
            x: { ticks: { color: palette.soft, font: { family: "'Plus Jakarta Sans'", size: 11, weight: '500' } }, grid: { display:false, drawBorder:false } },
            y: { beginAtZero: true, border: { dash: [4, 4] }, ticks: { color: palette.soft, font: { family: "'Plus Jakarta Sans'", size: 11, weight: '500' }, maxTicksLimit: 6 }, grid: { color: '#EEF2F7', drawBorder:false } }
        }
    };

    function bar(id, labels, values, color, suffix) {
        var ctx = document.getElementById(id); if (!ctx) return;
        window.deCharts[id] = new Chart(ctx, {
            type: 'bar',
            data: { labels: labels, datasets: [{ data: values, backgroundColor: color, borderRadius: 8, maxBarThickness: 48 }] },
            options: Object.assign({}, commonOpts, {
                plugins: Object.assign({}, commonOpts.plugins, {
                    legend: { display:false },
                    tooltip: { callbacks: { label: function(c){ return c.parsed.y + (suffix || ''); } } }
                })
            })
        });
    }

    function hbar(id, labels, values, color, suffix) {
        var ctx = document.getElementById(id); if (!ctx) return;
        window.deCharts[id] = new Chart(ctx, {
            type: 'bar',
            data: { labels: labels, datasets: [{ data: values, backgroundColor: color, borderRadius: 8, maxBarThickness: 26 }] },
            options: Object.assign({}, commonOpts, {
                indexAxis: 'y',
                plugins: Object.assign({}, commonOpts.plugins, {
                    legend: { display:false },
                    tooltip: { callbacks: { label: function(c){ return c.parsed.x + (suffix || ''); } } }
                }),
                scales: { x: { beginAtZero: true, ticks: { color: palette.soft }, grid: { color: '#EEF2F7' } }, y: { ticks: { color: palette.ink, font: { weight:'600' } }, grid: { display:false } } }
            })
        });
    }

    function doughnut(id, labels, values, colors, centerOpts) {
        var ctx = document.getElementById(id); if (!ctx) return;
        var cfg = {
            type: 'doughnut',
            data: { labels: labels, datasets: [{ data: values, backgroundColor: colors, borderWidth: 0, hoverOffset: 8 }] },
            options: { responsive:true, maintainAspectRatio:false, cutout: '62%',
                plugins: Object.assign({}, commonOpts.plugins, { centerText: centerOpts || false }),
                animation: { animateScale: true, animateRotate: true }
            },
            plugins: [centerTextPlugin]
        };
        window.deCharts[id] = new Chart(ctx, cfg);
    }

    function line(id, labels, values, color, opts) {
        var ctx = document.getElementById(id); if (!ctx) return;
        opts = opts || {};
        window.deCharts[id] = new Chart(ctx, {
            type: 'line',
            data: { labels: labels, datasets: [{
                label: opts.label || 'Camiones',
                data: values,
                borderColor: color,
                backgroundColor: function (c) { return gradientFor(c.chart.ctx, c.chart.chartArea, color); },
                tension: .4,
                fill: true,
                pointRadius: function (c) {
                    var max = Math.max.apply(null, values);
                    return c.parsed && c.parsed.y === max ? 6 : 3;
                },
                pointHoverRadius: 7,
                pointBackgroundColor: color,
                pointBorderColor: '#fff',
                pointBorderWidth: 2,
                borderWidth: 2.5
            }]},
            options: Object.assign({}, commonOpts, {
                onClick: opts.onClick || null,
                interaction: { mode: 'index', intersect: false },
                plugins: Object.assign({}, commonOpts.plugins, {
                    tooltip: {
                        backgroundColor: palette.ink, padding: 10, cornerRadius: 8,
                        callbacks: {
                            label: function (c) {
                                return ' ' + (opts.label || 'Valor') + ': ' + c.parsed.y + (opts.suffix || '');
                            }
                        }
                    }
                })
            })
        });
        if (opts.onClick) ctx.classList.add('de-clickable');
    }

    function gauge(id, value, max, color, suffix) {
        var ctx = document.getElementById(id); if (!ctx) return;
        var v = Math.max(0, Math.min(value, max));
        var rest = Math.max(0, max - v);
        window.deCharts[id] = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: ['Cumple', 'Pendiente'],
                datasets: [{ data: [v, rest], backgroundColor: [color, '#EEF2F7'], borderWidth: 0 }]
            },
            options: {
                responsive: true, maintainAspectRatio: false,
                cutout: '72%',
                rotation: -90, circumference: 180,
                plugins: {
                    legend: { display: false },
                    tooltip: { callbacks: { label: function(c){ return c.label + ': ' + c.parsed.toFixed(2) + (suffix || ''); } } }
                }
            },
            plugins: [{
                id: 'gaugeText',
                afterDraw: function (chart) {
                    var ctx2 = chart.ctx;
                    var w = chart.width, h = chart.height;
                    ctx2.save();
                    ctx2.font = '800 28px "Plus Jakarta Sans"';
                    ctx2.textAlign = 'center';
                    ctx2.fillStyle = palette.ink;
                    ctx2.fillText(value.toFixed(1) + (suffix || ''), w / 2, h * 0.72);
                    ctx2.restore();
                }
            }]
        });
    }

    function stackedBar(id, labels, datasets) {
        var ctx = document.getElementById(id); if (!ctx) return;
        window.deCharts[id] = new Chart(ctx, {
            type: 'bar',
            data: { labels: labels, datasets: datasets },
            options: Object.assign({}, commonOpts, {
                scales: {
                    x: { stacked: true, ticks: { color: palette.soft }, grid: { display: false } },
                    y: { stacked: true, beginAtZero: true, ticks: { color: palette.soft }, grid: { color: '#EEF2F7' } }
                }
            })
        });
    }

    // Horizontal stacked bar — igual al visual del Power BI de Tiempos Trujillo
    function hStackedBar(id, labels, datasets, suffix) {
        var ctx = document.getElementById(id); if (!ctx) return;
        window.deCharts[id] = new Chart(ctx, {
            type: 'bar',
            data: { labels: labels, datasets: datasets },
            options: {
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: true,
                        position: 'right',
                        labels: { color: palette.ink, font: { size: 11 }, boxWidth: 14 }
                    },
                    tooltip: {
                        callbacks: {
                            label: function(c) {
                                var v = c.parsed.x;
                                var base = ' ' + c.dataset.label + ': ' + (v != null ? v.toFixed(1) : '0') + (suffix || '');
                                var counts = c.dataset.counts;
                                if (counts && counts[c.dataIndex] != null) {
                                    base += '  (n=' + counts[c.dataIndex] + ')';
                                }
                                return base;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        stacked: true,
                        beginAtZero: true,
                        ticks: { color: palette.soft, callback: function(v){ return v + (suffix || ''); } },
                        grid: { color: '#EEF2F7' }
                    },
                    y: {
                        stacked: true,
                        ticks: { color: palette.ink, font: { weight: '600' } },
                        grid: { display: false }
                    }
                }
            }
        });
    }

    function multiBar(id, labels, datasets, suffix) {
        var ctx = document.getElementById(id); if (!ctx) return;
        window.deCharts[id] = new Chart(ctx, {
            type: 'bar',
            data: { labels: labels, datasets: datasets },
            options: Object.assign({}, commonOpts, {
                plugins: Object.assign({}, commonOpts.plugins, {
                    tooltip: { callbacks: { label: function(c){ return c.dataset.label + ': ' + c.parsed.y + (suffix || ''); } } }
                })
            })
        });
    }

    // ============= Render =============

    // Trujillo — stacked bar horizontal por mes (igual al Power BI)
    var mesesNombresT = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
    var trujMeses = d.trujillo || [];

    // Detectar si hay algún dato real cargado en planta
    var totalConDatosPlanta = trujMeses.reduce(function(acc, x){
        return acc + (x.nEsperaIngreso||0) + (x.nEsperaInicio||0) + (x.nCarga||0) + (x.nPermanencia||0);
    }, 0);

    var canvasTruj = document.getElementById('chartTrujillo');
    if (canvasTruj && totalConDatosPlanta === 0 && trujMeses.length > 0) {
        // Mostrar mensaje claro en lugar de un chart vacío
        var wrap = canvasTruj.parentElement;
        wrap.innerHTML =
            '<div style="display:flex;flex-direction:column;align-items:center;justify-content:center;height:100%;min-height:220px;color:#888;text-align:center;padding:20px;">' +
                '<i class="fas fa-database" style="font-size:42px;color:#ddd;margin-bottom:12px;"></i>' +
                '<div style="font-weight:600;color:#555;margin-bottom:4px;">Sin datos de planta cargados</div>' +
                '<div style="font-size:12px;">Hay <b>' + trujMeses.reduce(function(a,x){return a+(x.totalRegistros||0);},0) + '</b> registros en el per&iacute;odo,<br/>' +
                'pero ninguno tiene <b>F.H.I PLANTA</b>, <b>INICIO/TERMINO DE CARGA</b> ni <b>F.H.S PLANTA</b>.</div>' +
                '<div style="font-size:11px;margin-top:10px;color:#aaa;">Completa esas columnas en el registro o en el Excel de importaci&oacute;n.</div>' +
            '</div>';
    } else {
        var trujLabels = trujMeses.map(function(x){ return mesesNombresT[x.mes - 1]; });
        hStackedBar('chartTrujillo', trujLabels, [
            {
                label: 'Espera a ingreso (Hrs)',
                data: trujMeses.map(function(x){ return x.esperaIngresoTrujillo; }),
                counts: trujMeses.map(function(x){ return x.nEsperaIngreso; }),
                backgroundColor: '#D4A0A0',
                borderWidth: 0
            },
            {
                label: 'Espera inicio carga (Hrs)',
                data: trujMeses.map(function(x){ return x.esperaInicioCarga; }),
                counts: trujMeses.map(function(x){ return x.nEsperaInicio; }),
                backgroundColor: '#A8BCA1',
                borderWidth: 0
            },
            {
                label: 'Carga (Hrs)',
                data: trujMeses.map(function(x){ return x.cargaHoras; }),
                counts: trujMeses.map(function(x){ return x.nCarga; }),
                backgroundColor: '#6C7A9C',
                borderWidth: 0
            },
            {
                label: 'Permanencia total (Hrs)',
                data: trujMeses.map(function(x){ return x.permanenciaPlanta; }),
                counts: trujMeses.map(function(x){ return x.nPermanencia; }),
                backgroundColor: '#E8A838',
                borderWidth: 0
            }
        ], ' hrs');
    }

    // Trujillo → Planta Ecuador
    bar('chartTrujilloEcu', ['Días promedio'], [d.diasTrujilloPlantaEcu], palette.violet, ' d');

    // Base
    bar('chartBase', ['Tiempo Base'], [d.tiempoBaseHoras], palette.accent, ' hrs');


    // Estado
    var estadoLabels = (d.estados || []).map(function (x) { return x.estado; });
    var estadoValues = (d.estados || []).map(function (x) { return x.total; });
    var totalEstado = estadoValues.reduce(function(a,b){return a+(b||0);},0);
    doughnut('chartEstado', estadoLabels, estadoValues,
        [palette.primary, palette.accent, palette.warning, palette.danger, palette.violet],
        { text: totalEstado.toString(), subtext: 'viajes' });

    // Incidencias
    var totalInc = (d.incidencias.robados||0) + (d.incidencias.rotos||0) + (d.incidencias.mojados||0);
    doughnut('chartIncidencias', ['Robados', 'Rotos', 'Mojados'],
        [d.incidencias.robados, d.incidencias.rotos, d.incidencias.mojados],
        [palette.danger, palette.warning, palette.primary],
        { text: totalInc.toString(), subtext: 'sacos' });

    // TCI
    bar('chartTCI',
        ['Tiempo TCI', 'Espera nacionalización'],
        [d.tci.tiempoTCIHoras, d.tci.esperaNacionalizacion],
        palette.primary, ' hrs');

    // CEBAF
    bar('chartCEBAF', ['CEBAF'], [d.tci.tiempoCEBAFMin], palette.warning, ' min');

    // DEPSA consolidado
    bar('chartDepsa',
        ['Tiempo en bodega', 'Espera para ingresar'],
        [d.depsa.tiempoDepsa, d.depsa.esperaIngresoDepsa],
        palette.violet, ' hrs');

    // DEPSA puro
    bar('chartDepsaPuro',
        ['Tiempo en DEPSA', 'Espera ingreso DEPSA'],
        [d.depsaPuro.tiempo, d.depsaPuro.espera],
        palette.violet, ' hrs');

    // COMPLEX
    bar('chartComplex',
        ['Tiempo en COMPLEX', 'Espera ingreso COMPLEX'],
        [d.complex.tiempo, d.complex.espera],
        palette.accent, ' hrs');

    // Inbalnor
    bar('chartInbalnor',
        ['Descarga', 'Espera para descarga'],
        [d.inbalnor.descarga, d.inbalnor.espera],
        palette.accent, ' hrs');

    // Jave
    bar('chartJave',
        ['Descarga', 'Espera para descarga'],
        [d.jave.descarga, d.jave.espera],
        palette.primary, ' hrs');

    // DEPSA -> Inbalnor / Jave
    multiBar('chartDepsaSplit',
        ['Hacia bodega'],
        [
            { label: 'Inbalnor', data: [d.depsaSplit.aInbalnor], backgroundColor: palette.accent, borderRadius: 8, maxBarThickness: 48 },
            { label: 'Jave',     data: [d.depsaSplit.aJave],     backgroundColor: palette.primary, borderRadius: 8, maxBarThickness: 48 }
        ], ' hrs');

    // TCI -> Inbalnor / Jave
    multiBar('chartTciSplit',
        ['Hacia bodega'],
        [
            { label: 'Inbalnor', data: [d.tciSplit.aInbalnor], backgroundColor: palette.accent, borderRadius: 8, maxBarThickness: 48 },
            { label: 'Jave',     data: [d.tciSplit.aJave],     backgroundColor: palette.primary, borderRadius: 8, maxBarThickness: 48 }
        ], ' hrs');

    // Tendencia mensual — click navega al mes
    var mesesNombres = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
    var tendData = (d.tendencia || []);
    var tendLabels = tendData.map(function (x) { return mesesNombres[x.mes - 1]; });
    var tendValues = tendData.map(function (x) { return x.totalCamiones; });
    line('chartTendencia', tendLabels, tendValues, palette.primary, {
        label: 'Camiones', suffix: '',
        onClick: function (evt, elements) {
            if (!elements || !elements.length) return;
            var idx = elements[0].index;
            var mes = tendData[idx] ? tendData[idx].mes : null;
            if (!mes) return;
            var ddl = document.getElementById(SGV.ddlMes);
            if (ddl) {
                ddl.value = mes.toString();
                var btn = document.getElementById(SGV.btnFiltrar);
                if (btn) btn.click();
            }
        }
    });

    // Cumplimiento (gauge - usa valor del KPI literal)
    var pctCumpl = parseFloat((SGV.litCumplimientoText || '0').replace(',', '.')) || 0;
    gauge('chartCumplGauge', pctCumpl, 100, palette.accent, '%');

    // Leer detalle de cumplimiento desde hidden field
    (function () {
        var hdnCumpl = document.getElementById(SGV.hdnCumplDetalle);
        if (!hdnCumpl || !hdnCumpl.value) return;
        try {
            var det = JSON.parse(hdnCumpl.value);
            // Texto de detalle: A tiempo / Con llegada registrada
            var detEl = document.getElementById('kpiCumplDetalle');
            if (detEl) {
                detEl.innerHTML =
                    '<i class="fas fa-check-circle" style="color:#27ae60"></i> ' + det.aTiempo + ' a tiempo &nbsp;|&nbsp; ' +
                    '<i class="fas fa-clock" style="color:#e67e22"></i> ' + det.tardios + ' tard&iacute;os &nbsp;|&nbsp; ' +
                    '<i class="fas fa-question-circle" style="color:#bbb"></i> ' + det.sinDato + ' sin dato';
            }
            // Si no hay llegadas registradas, mostrar "S/D" en lugar de %
            if (det.conLlegada === 0) {
                var sufijo = document.getElementById('kpiCumplSufijo');
                if (sufijo) sufijo.textContent = '';
            }
        } catch(e) {}
    })();

    // Cumplimiento buckets (doughnut segmentado con valor central)
    var b = d.cumplimientoBuckets || { aTiempo:0, retraso6h:0, retraso24h:0, retrasoMas24h:0, sinDato:0 };
    var bLabels = ['A tiempo', '≤ 6 h', '6 - 24 h', '> 24 h', 'Sin dato'];
    var bValues = [b.aTiempo, b.retraso6h, b.retraso24h, b.retrasoMas24h, b.sinDato];
    var bColors = [palette.accent, palette.primary, palette.warning, palette.danger, palette.soft];
    var totalCu = bValues.reduce(function(a,c){return a+(c||0);},0);
    var pctATiempoCalc = totalCu ? (bValues[0] * 100 / totalCu).toFixed(0) : '0';
    doughnut('chartCumplBuckets', bLabels, bValues, bColors,
        { text: pctATiempoCalc + '%', subtext: 'a tiempo' });

    // Resumen del cumplimiento
    var totalCumpl = bValues.reduce(function (a, c) { return a + (c || 0); }, 0) || 1;
    var resumenHtml = bLabels.map(function (lbl, i) {
        var v = bValues[i] || 0;
        var pct = (v * 100 / totalCumpl).toFixed(1);
        return '<div class="de-resumen-item">' +
            '<span class="lbl"><span class="dot" style="background:' + bColors[i] + '"></span>' + lbl + '</span>' +
            '<span class="val">' + v + '<span class="pct">' + pct + '%</span></span>' +
            '</div>';
    }).join('');
    var elResumen = document.getElementById('cumplResumen');
    if (elResumen) elResumen.innerHTML = resumenHtml;

    // Top 10 pedidos (clientes) — click filtra cliente activo
    var cliLabels = (d.clientes || []).map(function (x) { return x.cliente; });
    var cliValues = (d.clientes || []).map(function (x) { return x.totalCamiones; });
    var ctxCli = document.getElementById('chartClientes');
    if (ctxCli) {
        ctxCli.classList.add('de-clickable');
        window.deCharts['chartClientes'] = new Chart(ctxCli, {
            type: 'bar',
            data: { labels: cliLabels, datasets: [{
                data: cliValues,
                backgroundColor: cliLabels.map(function (lbl) {
                    return (window._deClienteActivo && window._deClienteActivo !== lbl) ? palette.soft : palette.primary;
                }),
                borderRadius: 8, maxBarThickness: 26
            }] },
            options: Object.assign({}, commonOpts, {
                indexAxis: 'y',
                onClick: function (evt, elements) {
                    if (!elements || !elements.length) return;
                    var lbl = cliLabels[elements[0].index];
                    window._deClienteActivo = (window._deClienteActivo === lbl) ? null : lbl;
                    deRefrescarChips();
                    deRefrescarClienteUI();
                },
                plugins: Object.assign({}, commonOpts.plugins, {
                    legend: { display:false },
                    tooltip: { callbacks: {
                        label: function(c){ return c.parsed.x + ' camiones'; },
                        afterLabel: function(){ return 'Click para filtrar / quitar filtro'; }
                    } }
                }),
                scales: { x: { beginAtZero: true, ticks: { color: palette.soft }, grid: { color: '#EEF2F7' } }, y: { ticks: { color: palette.ink, font: { weight:'600' } }, grid: { display:false } } }
            })
        });
    }

    // Tendencia por cliente (stacked bar 5 clientes x 12 meses)
    var clientesUnicos = [];
    (d.tendenciaCliente || []).forEach(function (x) {
        if (clientesUnicos.indexOf(x.cliente) === -1) clientesUnicos.push(x.cliente);
    });
    var stackColors = [palette.primary, palette.accent, palette.warning, palette.violet, palette.danger];
    var datasetsTC = clientesUnicos.map(function (cli, idx) {
        var serie = mesesNombres.map(function (_, m) {
            var item = (d.tendenciaCliente || []).find(function (x) {
                return x.cliente === cli && x.mes === (m + 1);
            });
            return item ? item.totalCamiones : 0;
        });
        return {
            label: cli,
            data: serie,
            backgroundColor: stackColors[idx % stackColors.length],
            borderRadius: 6,
            maxBarThickness: 32
        };
    });
    stackedBar('chartClienteMes', mesesNombres, datasetsTC);

    // ===== Vista diaria (drill-down) =====
    deRenderDaily(d, palette);

    // Guardar referencias para re-render local
    window._deDatos = d;
    deRefrescarChips();
    deRefrescarClienteUI();
}

// ===== Vista diaria: trazabilidad de tiempos por etapa =====
// Catálogo de etapas. Cada una mapea a un campo del payload serieDiaria.
// 'n' = campo de conteo (para saber cuántos camiones aportaron al promedio)
var DAILY_STAGES = [
    { group: 'Trujillo (planta)',
      items: [
          { id: 'horasTrujilloProm', n: 'nTrujillo',     label: 'Permanencia en Trujillo',          unit: 'h',   color: 'primary' },
          { id: 'horasViajeProm',    n: 'nViaje',        label: 'Viaje Trujillo → destino',         unit: 'h',   color: 'violet' }
      ] },
    { group: 'Tránsito a Ecuador',
      items: [
          { id: 'tBNInbalnor',  n: 'nBNInbalnor',  label: 'Bodega Nacional → INBALNOR',        unit: 'h', color: 'primary' },
          { id: 'tBNJave',      n: 'nBNJave',      label: 'Bodega Nacional → JAVE',            unit: 'h', color: 'primary' },
          { id: 'tTciInbalnor', n: 'nTciInbalnor', label: 'TCI → INBALNOR',                    unit: 'h', color: 'accent' },
          { id: 'tTciJave',     n: 'nTciJave',     label: 'TCI → JAVE',                        unit: 'h', color: 'accent' }
      ] },
    { group: 'Trámite aduanero',
      items: [
          { id: 'tTci',           n: 'nTci',           label: 'Tiempo en TCI',                       unit: 'h',   color: 'primary' },
          { id: 'tEsperaNac',     n: 'nEsperaNac',     label: 'Espera nacionalización',              unit: 'h',   color: 'warning' },
          { id: 'tEsperaDepsa',   n: 'nEsperaDepsa',   label: 'Espera para ingresar a DEPSA',        unit: 'h',   color: 'warning' },
          { id: 'tDepsa',         n: 'nDepsa',         label: 'Tiempo dentro de DEPSA',              unit: 'h',   color: 'violet' },
          { id: 'tEsperaComplex', n: 'nEsperaComplex', label: 'Espera para ingresar a COMPLEX',      unit: 'h',   color: 'warning' },
          { id: 'tComplex',       n: 'nComplex',       label: 'Tiempo dentro de COMPLEX',            unit: 'h',   color: 'violet' },
          { id: 'tCebafMin',      n: 'nCebaf',         label: 'Tiempo en CEBAF',                     unit: 'min', color: 'accent' }
      ] }
];

function deRellenarSelectorEtapas() {
    var sel = document.getElementById('dailyStage');
    if (!sel || sel.dataset.filled === '1') return;
    var html = '';
    DAILY_STAGES.forEach(function (g) {
        html += '<optgroup label="' + g.group + '">';
        g.items.forEach(function (it) {
            html += '<option value="' + it.id + '">' + it.label + ' (' + it.unit + ')</option>';
        });
        html += '</optgroup>';
    });
    sel.innerHTML = html;
    sel.dataset.filled = '1';
    // valor por defecto: la primera de tránsito a Ecuador (más representativo)
    sel.value = 'tBNInbalnor';
}

function deBuscarEtapa(id) {
    for (var i = 0; i < DAILY_STAGES.length; i++) {
        for (var j = 0; j < DAILY_STAGES[i].items.length; j++) {
            if (DAILY_STAGES[i].items[j].id === id) return DAILY_STAGES[i].items[j];
        }
    }
    return null;
}

function deRenderDaily(d, palette) {
    var panel  = document.getElementById('dailyPanel');
    var hint   = document.getElementById('dailyHint');
    var picoEl = document.getElementById('dailyPico');
    var stats  = document.getElementById('dailyStats');
    if (!panel) return;
    deRellenarSelectorEtapas();

    var serie = (d && d.serieDiaria) || [];
    var hayMes = serie.length > 0;
    panel.classList.toggle('disabled', !hayMes);
    if (!hayMes) {
        hint.textContent = '— selecciona un mes específico para activar';
        picoEl.innerHTML = '';
        stats.innerHTML  = '';
        panel.dataset.collapsed = 'true';
        if (window.deCharts && window.deCharts.chartDaily) {
            window.deCharts.chartDaily.destroy();
            window.deCharts.chartDaily = null;
        }
        return;
    }
    hint.textContent = '— ' + serie.length + ' día(s) con registros';

    // Etapa activa
    var sel    = document.getElementById('dailyStage');
    var etapa  = deBuscarEtapa(sel.value) || DAILY_STAGES[1].items[0];
    var unit   = etapa.unit === 'min' ? ' min' : ' h';
    var label  = etapa.label;

    // Sólo incluyo días donde la etapa tiene al menos un camión que aportó (n > 0).
    // Eso evita que un valor 0 "sin dato" baje el promedio del gráfico.
    var puntos = serie
        .filter(function (x) { return (x[etapa.n] || 0) > 0; })
        .map(function (x) { return { dia: x.dia, value: Number(x[etapa.id] || 0), row: x }; });

    if (!puntos.length) {
        stats.innerHTML  = '<div class="de-daily-stat warn">Sin datos para esta etapa<b style="font-size:14px;">— intenta otra etapa</b></div>';
        picoEl.innerHTML = '';
        if (window.deCharts && window.deCharts.chartDaily) {
            window.deCharts.chartDaily.destroy();
            window.deCharts.chartDaily = null;
        }
        return;
    }

    var labels = puntos.map(function (p) { return 'Día ' + p.dia; });
    var values = puntos.map(function (p) { return p.value; });

    // Estadísticos del mes en esta etapa
    var maxVal = -Infinity, minVal = Infinity, idxMax = -1, idxMin = -1;
    var suma   = 0, nAporte = 0;
    puntos.forEach(function (p, i) {
        suma    += p.value;
        nAporte += (p.row[etapa.n] || 0);
        if (p.value > maxVal) { maxVal = p.value; idxMax = i; }
        if (p.value < minVal) { minVal = p.value; idxMin = i; }
    });
    var promedio = suma / values.length;
    var fmt = function (v) { return (Math.round(v * 10) / 10).toLocaleString('es-PE'); };

    picoEl.innerHTML = '<i class="fas fa-arrow-up" style="margin-right:4px;color:#F59E0B;"></i>Pico en <b>Día ' + puntos[idxMax].dia + '</b> · ' + fmt(maxVal) + unit;

    // Cuántos días superan el promedio (útil para detectar desviaciones)
    var sobrePromedio = values.filter(function (v) { return v > promedio; }).length;

    stats.innerHTML =
        '<div class="de-daily-stat acc">Promedio del mes<b>'   + fmt(promedio)   + unit + '</b></div>' +
        '<div class="de-daily-stat warn">Día pico<b>Día '       + puntos[idxMax].dia + ' · ' + fmt(maxVal) + unit + '</b></div>' +
        '<div class="de-daily-stat">Día mínimo<b>Día '          + puntos[idxMin].dia + ' · ' + fmt(minVal) + unit + '</b></div>' +
        '<div class="de-daily-stat">Días sobre el promedio<b>'  + sobrePromedio + ' / ' + values.length + '</b></div>' +
        '<div class="de-daily-stat">Camiones que aportaron<b>'  + nAporte.toLocaleString('es-PE') + '</b></div>';

    // Render Chart
    var canvas = document.getElementById('chartDaily');
    if (!canvas) return;
    var ctx = canvas.getContext('2d');
    if (window.deCharts && window.deCharts.chartDaily) {
        window.deCharts.chartDaily.destroy();
        window.deCharts.chartDaily = null;
    }

    var colorMap = { primary: palette.primary, accent: palette.accent, warning: palette.warning, danger: palette.danger, violet: palette.violet };
    var color    = colorMap[etapa.color] || palette.primary;

    var pointBg  = values.map(function (_, i) { return i === idxMax ? palette.warning : color; });
    var pointRad = values.map(function (_, i) { return i === idxMax ? 8 : 3.5; });

    window.deCharts = window.deCharts || {};
    window.deCharts.chartDaily = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: label,
                data: values,
                borderColor: color,
                backgroundColor: function (c) {
                    var area = c.chart.chartArea;
                    if (!area) return color + '22';
                    var g = c.chart.ctx.createLinearGradient(0, area.top, 0, area.bottom);
                    g.addColorStop(0, color + 'AA');
                    g.addColorStop(1, color + '08');
                    return g;
                },
                borderWidth: 2.5,
                tension: 0.35,
                fill: true,
                pointBackgroundColor: pointBg,
                pointBorderColor: '#fff',
                pointBorderWidth: 1.5,
                pointRadius: pointRad,
                pointHoverRadius: 8
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            onClick: function (evt, els) {
                if (!els || !els.length) return;
                var i  = els[0].index;
                var pt = puntos[i];
                if (!pt) return;
                picoEl.innerHTML = '<i class="fas fa-crosshairs" style="margin-right:4px;"></i>Día seleccionado: <b>Día ' + pt.dia + '</b> · ' + fmt(pt.value) + unit;
            },
            plugins: {
                legend: { display: false },
                tooltip: {
                    backgroundColor: 'rgba(11,20,38,.95)',
                    padding: 12, cornerRadius: 8,
                    titleFont: { weight: 'bold' },
                    callbacks: {
                        title: function (items) {
                            if (!items || !items.length) return '';
                            return 'Día ' + puntos[items[0].dataIndex].dia;
                        },
                        label: function (item) {
                            var pt   = puntos[item.dataIndex] || { row: {} };
                            var row  = pt.row || {};
                            var diff = pt.value - promedio;
                            var arr  = (diff >= 0 ? '▲ ' : '▼ ') + Math.abs(Math.round(diff * 10) / 10).toLocaleString('es-PE') + unit + ' vs prom';
                            return [
                                label + ': ' + fmt(pt.value) + unit + '  (' + arr + ')',
                                '— Camiones del día: '  + (row.totalCamiones || 0),
                                '— Aportaron a esta etapa: ' + (row[etapa.n] || 0),
                                '— A tiempo / Tardíos: ' + (row.aTiempo || 0) + ' / ' + (row.tardios || 0)
                            ];
                        }
                    }
                },
                annotation: {
                    annotations: {
                        promedio: {
                            type: 'line',
                            yMin: promedio, yMax: promedio,
                            borderColor: palette.soft,
                            borderWidth: 1.5,
                            borderDash: [6, 4],
                            label: {
                                enabled: true, display: true,
                                content: 'Promedio ' + fmt(promedio) + unit,
                                position: 'end',
                                backgroundColor: 'rgba(148,163,184,.85)',
                                color: '#fff', font: { size: 10, weight: 'bold' },
                                padding: 4
                            }
                        },
                        pico: {
                            type: 'point',
                            xValue: idxMax, yValue: values[idxMax],
                            backgroundColor: 'rgba(245,158,11,.15)',
                            borderColor: palette.warning,
                            borderWidth: 2,
                            radius: 14
                        }
                    }
                }
            },
            scales: {
                x: { grid: { display: false }, ticks: { color: palette.soft, font: { size: 11 } } },
                y: { beginAtZero: true,
                     grid: { color: 'rgba(0,0,0,.05)' },
                     ticks: { color: palette.soft, font: { size: 11 },
                              callback: function (v) { return v + (unit === ' min' ? '' : ''); } },
                     title: { display: true, text: etapa.unit === 'min' ? 'Minutos' : 'Horas',
                              color: palette.soft, font: { size: 11, weight: 'bold' } } }
            }
        }
    });
}

// Toggle + cambio de etapa
(function () {
    var DEF_PAL = { primary:'#2563EB', accent:'#00D4AA', warning:'#F59E0B',
                    danger:'#EF4444', violet:'#8B5CF6', ink:'#0B1426', soft:'#94A3B8' };
    document.addEventListener('click', function (ev) {
        var t = ev.target.closest && ev.target.closest('#dailyToggle');
        if (!t) return;
        var p = document.getElementById('dailyPanel');
        if (p.classList.contains('disabled')) return;
        p.dataset.collapsed = (p.dataset.collapsed === 'true') ? 'false' : 'true';
        if (p.dataset.collapsed === 'false' && window._deDatos) {
            setTimeout(function () { deRenderDaily(window._deDatos, DEF_PAL); }, 200);
        }
    });
    document.addEventListener('change', function (ev) {
        if (ev.target && ev.target.id === 'dailyStage' && window._deDatos) {
            deRenderDaily(window._deDatos, DEF_PAL);
        }
    });
})();

// ============= Interacción cliente-side =============
function deRefrescarChips() {
    var cont = document.getElementById('deChips');
    if (!cont) return;
    cont.innerHTML = '';
    if (window._deClienteActivo) {
        var chip = document.createElement('span');
        chip.className = 'de-chip';
        chip.innerHTML = '<i class="fas fa-filter"></i> Pedido: ' + window._deClienteActivo +
                         ' <span class="x" title="Quitar filtro">&times;</span>';
        chip.querySelector('.x').addEventListener('click', function () {
            window._deClienteActivo = null;
            deRefrescarChips();
            deRefrescarClienteUI();
        });
        cont.appendChild(chip);
    }
}

// Recolorea las barras del Top 10 según el cliente activo
function deRefrescarClienteUI() {
    var ch = window.deCharts && window.deCharts['chartClientes'];
    if (!ch) return;
    var lbls = ch.data.labels;
    ch.data.datasets[0].backgroundColor = lbls.map(function (lbl) {
        if (!window._deClienteActivo) return '#2563EB';
        return (window._deClienteActivo === lbl) ? '#F59E0B' : '#94A3B8';
    });
    ch.update('none');

    // Resaltar la serie del cliente activo en tendencia por cliente (panel operativo)
    var tc = window.deCharts && window.deCharts['chartClienteMes'];
    if (tc) {
        tc.data.datasets.forEach(function (ds) {
            var match = !window._deClienteActivo || ds.label === window._deClienteActivo;
            ds.backgroundColor = match ? ds._origColor || ds.backgroundColor : '#E4E8F0';
            if (!ds._origColor) ds._origColor = ds.backgroundColor;
        });
        tc.update('none');
    }
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', deRenderCharts);
} else {
    deRenderCharts();
}
// Re-render tras postback (UpdatePanel no se usa aquí, pero por si acaso)
if (typeof Sys !== 'undefined' && Sys.WebForms && Sys.WebForms.PageRequestManager) {
    Sys.WebForms.PageRequestManager.getInstance().add_endRequest(deRenderCharts);
}

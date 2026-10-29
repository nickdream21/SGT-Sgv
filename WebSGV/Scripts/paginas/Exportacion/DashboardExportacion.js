// Dashboard de exportación: todo se dibuja desde el JSON que deja el servidor en hdnDatos
// ({ periodo: ResumenPeriodo, operacion: OperacionActual }).
(function () {
    'use strict';

    var FASES = [
        { id: 'carga', nombre: 'Carga en Trujillo' },
        { id: 'base', nombre: 'Base Sullana' },
        { id: 'frontera', nombre: 'Frontera Perú' },
        { id: 'ecuador', nombre: 'Ecuador' },
        { id: 'retorno', nombre: 'Retorno' }
    ];
    var EXTRA = {
        transito: 'Tránsito al cliente (Trujillo → planta Ecuador)',
        ciclo: 'Ciclo completo (salida a cargar → regreso a base)'
    };
    // Accesos rápidos de la evolución: los totales y las esperas que más pesan.
    var CHIPS = [
        { id: 'transito', nombre: 'Tránsito al cliente' },
        { id: 'ciclo', nombre: 'Ciclo completo' },
        { id: 'colaTrujillo', nombre: 'Cola Trujillo' },
        { id: 'base', nombre: 'Espera en base' },
        { id: 'complex', nombre: 'Complex' },
        { id: 'tci', nombre: 'TCI' },
        { id: 'descarga', nombre: 'Descarga' }
    ];
    var PLANTA_COLOR = ['#6250d6', '#64748b', '#a3a3a3'];
    var ESTADO = { ok: '#16a34a', tarde: '#d97706', sin: '#cbd5e1' };
    var REDUCIR_MOVIMIENTO = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    var charts = {};
    var datos = null;
    var tramoSel = 'transito';

    function css(nombre) { return getComputedStyle(document.documentElement).getPropertyValue(nombre).trim(); }
    function colorFase(f) { return css('--de-f-' + f) || '#2a78d6'; }
    function nombreFase(id) { var f = FASES.filter(function (x) { return x.id === id; })[0]; return f ? f.nombre : id; }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function num(v, dec) {
        return Number(v).toLocaleString('es-PE', { minimumFractionDigits: dec || 0, maximumFractionDigits: dec || 0 });
    }

    // Duración legible: "45 min", "12,3 h", "2 d 6 h".
    function dur(h) {
        if (h == null || isNaN(h)) return '—';
        if (h < 1) return Math.round(h * 60) + ' min';
        if (h < 48) return num(h, h < 10 ? 1 : 0) + ' h';
        var d = Math.floor(h / 24);
        return d + ' d ' + Math.round(h - d * 24) + ' h';
    }

    function fecha(iso, conHora) {
        if (!iso) return '';
        var p = iso.split('T'), f = p[0].split('-');
        return f[2] + '/' + f[1] + (conHora && p[1] && p[1] !== '00:00' ? ' ' + p[1] : '');
    }

    // Diferencia contra el período anterior; menorEsMejor para tiempos.
    function delta(actual, anterior, opts) {
        opts = opts || {};
        if (actual == null || anterior == null)
            return '<span class="de-delta igual" title="El período anterior no tiene al menos 10 viajes con este dato">sin comparación</span>';
        var d = actual - anterior;
        var umbral = opts.umbral != null ? opts.umbral : Math.max(Math.abs(anterior) * 0.03, 0.05);
        var cls = Math.abs(d) < umbral || opts.neutro ? 'igual' : ((d < 0) === !!opts.menorEsMejor ? 'mejor' : 'peor');
        var flecha = Math.abs(d) < umbral ? '=' : (d > 0 ? '▲' : '▼');
        var txt = opts.formato ? opts.formato(Math.abs(d)) : num(Math.abs(d), 1);
        return '<span class="de-delta ' + cls + '">' + flecha + ' ' + txt + '</span>';
    }

    // Línea mínima (SVG) de una serie; los huecos (null) cortan la línea.
    function sparkline(valores, color) {
        var pts = valores.map(function (v, i) { return v == null ? null : { i: i, v: v }; }).filter(Boolean);
        if (pts.length < 2) return '';
        var w = 120, h = 30, max = Math.max.apply(null, pts.map(function (p) { return p.v; }));
        var min = Math.min.apply(null, pts.map(function (p) { return p.v; }));
        var rango = max - min || 1, n = Math.max(valores.length - 1, 1);
        function x(i) { return (i / n * (w - 4) + 2).toFixed(1); }
        function y(v) { return (h - 3 - (v - min) / rango * (h - 8)).toFixed(1); }
        var linea = pts.map(function (p) { return x(p.i) + ',' + y(p.v); }).join(' ');
        var area = x(pts[0].i) + ',' + h + ' ' + linea + ' ' + x(pts[pts.length - 1].i) + ',' + h;
        var ult = pts[pts.length - 1];
        return '<svg class="de-spark" viewBox="0 0 ' + w + ' ' + h + '" preserveAspectRatio="none" aria-hidden="true">' +
            '<polygon points="' + area + '" fill="' + color + '" opacity=".12"/>' +
            '<polyline points="' + linea + '" fill="none" stroke="' + color + '" stroke-width="1.6" vector-effect="non-scaling-stroke"/>' +
            '<circle cx="' + x(ult.i) + '" cy="' + y(ult.v) + '" r="2.4" fill="' + color + '"/></svg>';
    }

    // Números que suben desde 0 al cargar (data-contar = valor, data-dec = decimales).
    function animarNumeros(raiz) {
        raiz.querySelectorAll('[data-contar]').forEach(function (el) {
            var fin = parseFloat(el.getAttribute('data-contar')), dec = parseInt(el.getAttribute('data-dec') || '0', 10);
            if (REDUCIR_MOVIMIENTO || !isFinite(fin)) { el.textContent = num(fin, dec); return; }
            var t0 = null;
            function paso(t) {
                if (!t0) t0 = t;
                var k = Math.min((t - t0) / 700, 1), e = 1 - Math.pow(1 - k, 3);
                el.textContent = num(fin * e, dec);
                if (k < 1) requestAnimationFrame(paso);
            }
            requestAnimationFrame(paso);
        });
    }

    function vacio(cont, cols, texto) {
        if (cols) cont.innerHTML = '<tr><td colspan="' + cols + '" class="de-empty">' + esc(texto) + '</td></tr>';
        else cont.innerHTML = '<div class="de-empty">' + esc(texto) + '</div>';
    }

    // ================= Operación ahora =================
    function renderOperacion(op) {
        var meta = document.getElementById('opMeta');
        var ruta = document.getElementById('opRuta');
        if (!op) { meta.textContent = 'No se pudo cargar la operación de ahora.'; return; }

        var partes = ['<b>' + op.unidades + '</b> unidades activas'];
        if (op.ultimaConsultaGps) {
            var min = Math.round((new Date(op.ahora) - new Date(op.ultimaConsultaGps)) / 60000);
            var txt = min < 60 ? 'hace ' + Math.max(min, 0) + ' min' : 'el ' + fecha(op.ultimaConsultaGps, true);
            partes.push('GPS consultado ' + txt + (min >= 30 ? ' <span class="de-pill de-pill-warn">desactualizado</span>' : ''));
        }
        if (op.gpsSinReportar > 0)
            partes.push('<span class="de-pill de-pill-warn">' + op.gpsSinReportar + ' sin señal del GPS</span>');
        meta.innerHTML = partes.join(' &middot; ');

        // Ruta: una estación por fase; el círculo crece con las unidades que hay en ella.
        var max = Math.max.apply(null, op.fases.map(function (f) { return f.unidades; })) || 1;
        ruta.innerHTML = op.fases.map(function (f, i) {
            var tam = f.unidades ? Math.round(30 + 26 * Math.sqrt(f.unidades / max)) : 14;
            var grupoNuevo = i === 0 || op.fases[i - 1].grupo !== f.grupo;
            return '<a class="de-estacion' + (f.unidades ? '' : ' vacia') + '" href="RegistroSeguimiento.aspx"' +
                ' style="--fase:' + colorFase(f.grupo) + ';--tam:' + tam + 'px" title="' + esc(f.nombre) + ': ' + f.unidades + ' unidad(es)">' +
                '<span class="grupo">' + (grupoNuevo ? esc(nombreFase(f.grupo)) : '&nbsp;') + '</span>' +
                '<span class="punto"><span class="burbuja">' + (f.unidades || '') + '</span></span>' +
                '<span class="t">' + esc(f.nombre) + '</span></a>';
        }).join('');

        // Flota: la misma foto en una barra por fase.
        var porGrupo = FASES.map(function (g) {
            return { g: g, n: op.fases.filter(function (f) { return f.grupo === g.id; }).reduce(function (a, f) { return a + f.unidades; }, 0) };
        });
        var total = op.unidades || 1;
        document.getElementById('opFlota').innerHTML = op.unidades
            ? '<div class="de-flota-barra">' + porGrupo.filter(function (x) { return x.n; }).map(function (x) {
                return '<span style="flex:' + x.n + ';background:' + colorFase(x.g.id) + '" title="' + esc(x.g.nombre) + ': ' + x.n + '">' +
                    (x.n / total >= 0.08 ? x.n : '') + '</span>';
            }).join('') + '</div><div class="de-legend">' + porGrupo.map(function (x) {
                return '<span style="--fase:' + colorFase(x.g.id) + '"><i></i>' + esc(x.g.nombre) + ' <b>' + x.n + '</b></span>';
            }).join('') + '</div>'
            : '';

        var alertas = document.getElementById('opAlertas');
        alertas.innerHTML = op.alertas && op.alertas.length
            ? '<div class="de-alertas"><div class="de-alertas-title"><i class="fas fa-exclamation-triangle"></i> ' +
                op.alertas.length + ' espera(s) que siguen en curso</div><ul>' +
                op.alertas.map(function (a) {
                    return '<li><a href="' + SGV.urlViaje + '?id=' + a.id + '">' + esc(a.placa) + '</a>' +
                        '<span>' + esc(a.texto) + (a.pedido ? ' · ' + esc(a.pedido) : '') + '</span></li>';
                }).join('') + '</ul></div>'
            : '';
    }

    // ================= KPIs =================
    function renderKpis(p) {
        var k = p.kpi, serie = p.serie;
        document.getElementById('periodoNota').innerHTML =
            'Per&iacute;odo: <b>' + esc(p.periodo) + '</b> &middot; comparado con ' + esc(p.periodoAnterior);
        document.getElementById('thAnterior').textContent = 'vs. ' + p.periodoAnterior;

        function vsAnt(html) { return html + ' <span>vs. ' + esc(p.periodoAnterior) + '</span>'; }
        function tile(o) {
            return '<div class="de-kpi' + (o.clic ? ' clic' : '') + '"' + (o.clic ? ' data-tramo="' + o.clic + '" title="Ver su evolución"' : '') + '>' +
                '<div class="l">' + o.label + '</div><div class="v">' + o.valor + '</div>' +
                (o.delta ? '<div class="d">' + o.delta + '</div>' : '') +
                (o.sub ? '<div class="s">' + o.sub + '</div>' : '') + (o.extra || '') + '</div>';
        }

        var pctDia = serie.map(function (s) { var c = s.aTiempo + s.tardios; return c ? 100 * s.aTiempo / c : null; });
        var evaluados = k.aTiempo + k.tardios + k.sinLlegada || 1;
        var meter = '<div class="de-meter" title="A tiempo / tarde / sin llegada">' +
            '<span style="width:' + (100 * k.aTiempo / evaluados) + '%;background:' + ESTADO.ok + '"></span>' +
            '<span style="width:' + (100 * k.tardios / evaluados) + '%;background:' + ESTADO.tarde + '"></span></div>';
        var pctGps = k.hitosRegistrados ? 100 * k.hitosGps / k.hitosRegistrados : null;
        var pctDescargados = k.viajes ? 100 * k.descargados / k.viajes : 0;
        var sacos = k.sacosRobados + k.sacosRotos + k.sacosMojados;

        var cont = document.getElementById('kpis');
        cont.innerHTML = [
            tile({
                label: 'Viajes programados', valor: '<span data-contar="' + k.viajes + '">0</span>',
                delta: k.viajesAnterior != null ? vsAnt(delta(k.viajes, k.viajesAnterior, { neutro: true, formato: function (d) { return num(d); } })) : '',
                sub: num(k.pedidos) + ' pedido(s)',
                extra: sparkline(serie.map(function (s) { return s.viajes; }), '#1d4ed8')
            }),
            tile({
                label: 'Llegada a Trujillo a tiempo',
                valor: k.cumplimiento == null ? '—' : '<span data-contar="' + k.cumplimiento + '" data-dec="1">0</span><small>%</small>',
                delta: vsAnt(delta(k.cumplimiento, k.cumplimientoAnterior, { umbral: 1, formato: function (d) { return num(d, 1) + ' pts'; } })),
                sub: k.aTiempo + ' a tiempo · ' + k.tardios + ' tarde' + (k.retrasoMediano != null ? ' (retraso típico ' + dur(k.retrasoMediano) + ')' : ''),
                extra: meter + sparkline(pctDia, ESTADO.ok)
            }),
            tile({
                label: 'Tránsito al cliente', valor: dur(k.transito.mediana), clic: 'transito',
                delta: vsAnt(delta(k.transito.mediana, k.transitoAnterior, { menorEsMejor: true, formato: dur })),
                sub: 'Trujillo → planta Ecuador · P90 ' + dur(k.transito.p90),
                extra: sparkline(serie.map(function (s) { return s.tramos.transito; }), colorFase('ecuador'))
            }),
            tile({
                label: 'Ciclo completo', valor: dur(k.ciclo.mediana), clic: 'ciclo',
                delta: vsAnt(delta(k.ciclo.mediana, k.cicloAnterior, { menorEsMejor: true, formato: dur })),
                sub: k.ciclo.n + ' viaje(s) cerrados · P90 ' + dur(k.ciclo.p90),
                extra: sparkline(serie.map(function (s) { return s.tramos.ciclo; }), colorFase('retorno'))
            }),
            tile({
                label: 'Avance de los viajes',
                valor: '<span data-contar="' + k.descargados + '">0</span><small>/ ' + num(k.viajes) + '</small>',
                sub: 'descargados en Ecuador · ' + num(k.cerrados) + ' de vuelta en base',
                extra: '<div class="de-anillo" style="--pct:' + pctDescargados.toFixed(1) + '"><span>' + num(pctDescargados, 0) + '%</span></div>'
            }),
            tile({
                label: 'Horas registradas por GPS',
                valor: pctGps == null ? '—' : '<span data-contar="' + pctGps + '">0</span><small>%</small>',
                sub: num(k.hitosGps) + ' de ' + num(k.hitosRegistrados) + ' hitos',
                extra: sacos > 0 ? '<div class="s"><span class="de-pill de-pill-bad">' + sacos + ' sacos con incidencia</span></div>'
                                 : '<div class="s"><span class="de-pill de-pill-ok">Sin sacos con incidencia</span></div>'
            })
        ].join('');
        animarNumeros(cont);
        cont.querySelectorAll('.de-kpi.clic').forEach(function (el) {
            el.addEventListener('click', function () { seleccionarTramo(el.getAttribute('data-tramo'), true); });
        });
    }

    // ================= Gráficos =================
    var base = {
        responsive: true,
        maintainAspectRatio: false,
        animation: REDUCIR_MOVIMIENTO ? false : { duration: 600, easing: 'easeOutQuart' },
        interaction: { mode: 'index', intersect: false },
        plugins: {
            legend: { position: 'bottom', labels: { boxWidth: 10, boxHeight: 10, color: '#4b5563', font: { size: 12 } } },
            tooltip: { backgroundColor: '#1f2937', padding: 10, cornerRadius: 6, titleFont: { weight: '600' }, boxPadding: 4 }
        }
    };
    function opciones(extra) {
        var o = Object.assign({}, base, extra || {});
        o.plugins = Object.assign({}, base.plugins, (extra && extra.plugins) || {});
        return o;
    }
    // Callbacks del tooltip a prueba de listas vacías: con el filtro, un día sin datos no trae elementos
    // y leer it[0] rompía el dibujo de todas las gráficas.
    function primero(it) { return it && it.length ? it[0] : null; }

    function ejes(titulo, apilado, formatoY) {
        return {
            x: { stacked: !!apilado, grid: { display: false }, ticks: { color: '#6b7280', font: { size: 11 }, autoSkip: true, maxRotation: 0 } },
            y: {
                stacked: !!apilado, beginAtZero: true, border: { display: false },
                grid: { color: '#eef1f5' },
                ticks: { color: '#6b7280', font: { size: 11 }, maxTicksLimit: 6, callback: formatoY || undefined },
                title: { display: !!titulo, text: titulo, color: '#6b7280', font: { size: 11 } }
            }
        };
    }

    function crear(id, config) {
        if (charts[id]) charts[id].destroy();
        var canvas = document.getElementById(id);
        if (!canvas || !window.Chart) return null;
        charts[id] = new Chart(canvas, config);
        return charts[id];
    }

    // Texto en el centro de la dona.
    var textoCentro = {
        id: 'textoCentro',
        afterDraw: function (chart, args, o) {
            if (!o || !o.valor) return;
            var a = chart.chartArea, ctx = chart.ctx, x = (a.left + a.right) / 2, y = (a.top + a.bottom) / 2;
            ctx.save();
            ctx.textAlign = 'center';
            ctx.fillStyle = '#1f2937';
            ctx.font = '600 20px ' + Chart.defaults.font.family;
            ctx.fillText(o.valor, x, y + 2);
            ctx.fillStyle = '#6b7280';
            ctx.font = '12px ' + Chart.defaults.font.family;
            ctx.fillText(o.texto, x, y + 20);
            ctx.restore();
        }
    };

    function renderFases(p) {
        var horas = FASES.map(function (f) {
            return p.tramos.filter(function (t) { return t.fase === f.id; }).reduce(function (a, t) { return a + (t.actual.mediana || 0); }, 0);
        });
        var total = horas.reduce(function (a, b) { return a + b; }, 0);
        var leyenda = document.getElementById('fasesLeyenda');
        if (!total) { vacio(leyenda, 0, 'Sin hitos en el período.'); return; }
        crear('chartFases', {
            type: 'doughnut',
            data: {
                labels: FASES.map(function (f) { return f.nombre; }),
                datasets: [{ data: horas, backgroundColor: FASES.map(function (f) { return colorFase(f.id); }), borderColor: '#fff', borderWidth: 2, hoverOffset: 6 }]
            },
            options: opciones({
                cutout: '68%',
                interaction: { mode: 'nearest', intersect: true },
                plugins: {
                    legend: { display: false },
                    textoCentro: { valor: dur(total), texto: 'suma de tramos' },
                    tooltip: Object.assign({}, base.plugins.tooltip, {
                        callbacks: { label: function (c) { return ' ' + c.label + ': ' + dur(c.parsed) + ' (' + num(100 * c.parsed / total, 0) + '%)'; } }
                    })
                }
            }),
            plugins: [textoCentro]
        });
        leyenda.innerHTML = FASES.map(function (f, i) {
            return '<div class="fila" style="--fase:' + colorFase(f.id) + '"><i></i><span>' + esc(f.nombre) + '</span>' +
                '<b>' + dur(horas[i]) + '</b><span class="pct">' + num(100 * horas[i] / total, 0) + '%</span></div>';
        }).join('');
    }

    function renderTramos(p) {
        var tramos = p.tramos.filter(function (t) { return t.actual.n > 0; });
        var colores = tramos.map(function (t) { return colorFase(t.fase); });
        crear('chartTramos', {
            type: 'bar',
            data: {
                labels: tramos.map(function (t) { return t.nombre; }),
                datasets: [{
                    label: 'Mediana', data: tramos.map(function (t) { return t.actual.mediana; }),
                    backgroundColor: colores, borderRadius: 3, barPercentage: 0.62, grouped: false, order: 1
                }, {
                    label: 'P90', data: tramos.map(function (t) { return t.actual.p90; }),
                    backgroundColor: colores.map(function (c) { return c + '38'; }), borderRadius: 3, barPercentage: 0.62, grouped: false, order: 2
                }]
            },
            options: opciones({
                indexAxis: 'y',
                interaction: { mode: 'index', axis: 'y', intersect: false },
                onClick: function (e, els) { if (els.length) seleccionarTramo(tramos[els[0].index].id, true); },
                onHover: function (e, els) { e.native.target.style.cursor = els.length ? 'pointer' : 'default'; },
                scales: {
                    x: { beginAtZero: true, grid: { color: '#eef1f5' }, border: { display: false },
                         ticks: { color: '#6b7280', font: { size: 11 }, callback: function (v) { return v + ' h'; } } },
                    y: { grid: { display: false }, ticks: { color: '#374151', font: { size: 11.5 }, autoSkip: false } }
                },
                plugins: {
                    legend: { display: false },
                    tooltip: Object.assign({}, base.plugins.tooltip, {
                        filter: function (c) { return c.datasetIndex === 0; },
                        callbacks: {
                            label: function (c) { return ' Mediana: ' + dur(c.parsed.x); },
                            afterBody: function (it) {
                                var c = primero(it); if (!c) return [];
                                var t = tramos[c.dataIndex], a = t.actual;
                                return ['Rango típico: ' + dur(a.p25) + ' – ' + dur(a.p75),
                                        'P90: ' + dur(a.p90), 'Viajes: ' + a.n,
                                        t.medianaAnterior != null ? 'Mes anterior: ' + dur(t.medianaAnterior) : 'Sin comparación'];
                            },
                            footer: function () { return 'Clic para ver su evolución'; }
                        }
                    })
                }
            })
        });
    }

    function nombreTramo(id) {
        if (EXTRA[id]) return EXTRA[id];
        var t = datos.periodo.tramos.filter(function (x) { return x.id === id; })[0];
        return t ? t.nombre : id;
    }

    function estadisticaTramo(id) {
        var p = datos.periodo;
        if (id === 'transito') return { e: p.kpi.transito, fase: 'ecuador' };
        if (id === 'ciclo') return { e: p.kpi.ciclo, fase: 'retorno' };
        var t = p.tramos.filter(function (x) { return x.id === id; })[0];
        return { e: t.actual, fase: t.fase };
    }

    function seleccionarTramo(id, desplazar) {
        tramoSel = id;
        document.querySelectorAll('#tablaTramos tbody tr[data-tramo]').forEach(function (tr) {
            tr.classList.toggle('sel', tr.getAttribute('data-tramo') === id);
        });
        renderEvolucion(datos.periodo);
        if (desplazar) document.getElementById('evolTitulo').scrollIntoView({ behavior: REDUCIR_MOVIMIENTO ? 'auto' : 'smooth', block: 'center' });
    }

    function renderEvolucion(p) {
        var info = estadisticaTramo(tramoSel);
        var color = colorFase(info.fase);
        var serie = p.serie;
        var valores = serie.map(function (s) { return s.tramos[tramoSel] != null ? s.tramos[tramoSel] : null; });
        var hayDatos = valores.some(function (v) { return v != null; });

        document.getElementById('evolTitulo').textContent = nombreTramo(tramoSel);
        document.getElementById('evolMeta').innerHTML = hayDatos
            ? 'Mediana ' + (p.porDia ? 'por d&iacute;a de programaci&oacute;n' : 'por mes') + ' &middot; per&iacute;odo <b>' + dur(info.e.mediana) + '</b>'
            : 'Sin datos de este tramo en el per&iacute;odo';

        var chips = CHIPS.slice();
        if (!chips.some(function (c) { return c.id === tramoSel; })) chips.push({ id: tramoSel, nombre: nombreTramo(tramoSel) });
        document.getElementById('evolChips').innerHTML = chips.map(function (c) {
            return '<button type="button" class="de-chip' + (c.id === tramoSel ? ' sel' : '') + '" data-tramo="' + c.id + '">' + esc(c.nombre) + '</button>';
        }).join('');

        crear('chartEvolucion', {
            type: 'line',
            data: {
                labels: serie.map(function (s) { return s.etiqueta; }),
                datasets: [{
                    label: 'Mediana ' + (p.porDia ? 'del día' : 'del mes'),
                    data: valores,
                    borderColor: color,
                    backgroundColor: function (c) {
                        var a = c.chart.chartArea;
                        if (!a) return color + '22';
                        var g = c.chart.ctx.createLinearGradient(0, a.top, 0, a.bottom);
                        g.addColorStop(0, color + '40');
                        g.addColorStop(1, color + '00');
                        return g;
                    },
                    fill: 'origin', borderWidth: 2, tension: 0.3, spanGaps: true,
                    pointRadius: 3, pointHoverRadius: 6, pointBackgroundColor: color, pointBorderColor: '#fff', pointBorderWidth: 1.5
                }, {
                    label: 'Mediana del período',
                    data: serie.map(function () { return info.e.mediana; }),
                    borderColor: '#9ca3af', borderDash: [5, 4], borderWidth: 1.5, pointRadius: 0, pointHoverRadius: 0, fill: false
                }]
            },
            options: opciones({
                scales: ejes(null, false, function (v) { return v + ' h'; }),
                plugins: {
                    tooltip: Object.assign({}, base.plugins.tooltip, {
                        filter: function (c) { return c.parsed.y != null; },
                        callbacks: {
                            title: function (it) { var c = primero(it); return c ? (p.porDia ? 'Día ' : '') + c.label : ''; },
                            label: function (c) {
                                if (c.datasetIndex === 1) return ' Mediana del período: ' + dur(c.parsed.y);
                                var n = serie[c.dataIndex].tramosN[tramoSel] || 0;
                                return ' ' + dur(c.parsed.y) + '  (' + n + ' viaje' + (n === 1 ? '' : 's') + ')';
                            }
                        }
                    })
                }
            })
        });
    }

    function renderCumplimiento(p) {
        var k = p.kpi, serie = p.serie;
        document.getElementById('cumplMeta').innerHTML = k.conLlegada
            ? '<b>' + num(k.cumplimiento, 1) + '%</b> a tiempo de ' + k.conLlegada + ' llegada(s)'
            : 'Sin llegadas registradas';
        function ds(label, campo, color) {
            return { label: label, data: serie.map(function (s) { return s[campo]; }), backgroundColor: color,
                     borderColor: '#fff', borderWidth: { top: 2 }, borderSkipped: false, borderRadius: 2, maxBarThickness: 26 };
        }
        crear('chartCumplimiento', {
            type: 'bar',
            data: {
                labels: serie.map(function (s) { return s.etiqueta; }),
                datasets: [ds('A tiempo', 'aTiempo', ESTADO.ok), ds('Tarde', 'tardios', ESTADO.tarde), ds('Sin llegada', 'sinLlegada', ESTADO.sin)]
            },
            options: opciones({
                scales: ejes('Viajes', true),
                plugins: {
                    tooltip: Object.assign({}, base.plugins.tooltip, {
                        filter: function (c) { return c.parsed.y > 0; },
                        callbacks: {
                            title: function (it) { var c = primero(it); return c ? (p.porDia ? 'Día ' : '') + c.label : ''; },
                            footer: function (it) {
                                var c = primero(it); if (!c) return '';
                                var s = serie[c.dataIndex], con = s.aTiempo + s.tardios;
                                return con ? num(100 * s.aTiempo / con, 0) + '% a tiempo' : '';
                            }
                        }
                    })
                }
            })
        });
    }

    function renderPlantas(p) {
        var plantas = p.plantas.filter(function (x) { return x.planta !== 'Sin planta'; }).slice(0, 3);
        var resumen = document.getElementById('plantasResumen');
        if (!plantas.length) { vacio(resumen, 0, 'Sin viajes con planta en el período.'); return; }
        var sinPlanta = p.plantas.filter(function (x) { return x.planta === 'Sin planta'; })[0];
        resumen.innerHTML = plantas.map(function (x, i) {
            return '<div class="de-planta" style="--planta:' + PLANTA_COLOR[i] + '"><div class="nombre"><i></i>' + esc(x.planta) + '</div>' +
                '<div class="cifra"><b>' + x.viajes + '</b> viajes &middot; ' + x.descargados + ' descargados</div>' +
                '<div class="cifra">Tr&aacute;nsito <b>' + dur(x.transito) + '</b></div></div>';
        }).join('') + (sinPlanta ? '<div class="de-note">' + sinPlanta.viajes + ' viaje(s) a&uacute;n sin planta asignada.</div>' : '');

        var etapas = [['tciPlanta', 'TCI → planta'], ['colaPlanta', 'Cola'], ['descarga', 'Descarga'], ['retorno', 'Retorno a base']];
        crear('chartPlantas', {
            type: 'bar',
            data: {
                labels: etapas.map(function (e) { return e[1]; }),
                datasets: plantas.map(function (x, i) {
                    return { label: x.planta, data: etapas.map(function (e) { return x.tramos[e[0]]; }),
                             backgroundColor: PLANTA_COLOR[i], borderRadius: 3, maxBarThickness: 28 };
                })
            },
            options: opciones({
                scales: ejes(null, false, function (v) { return v + ' h'; }),
                plugins: {
                    tooltip: Object.assign({}, base.plugins.tooltip, {
                        callbacks: { label: function (c) { return ' ' + c.dataset.label + ': ' + dur(c.parsed.y); } }
                    })
                }
            })
        });
    }

    // ================= Viajes con mayor tránsito =================
    function renderLentos(p) {
        var cont = document.getElementById('listaLentos');
        if (!p.masLentos.length) { vacio(cont, 0, 'Aún no hay viajes que hayan llegado a Ecuador en el período.'); return; }
        var fases = ['carga', 'base', 'frontera', 'ecuador'];
        var max = Math.max.apply(null, p.masLentos.map(function (v) { return v.transito; }));
        var tipico = p.kpi.transito.mediana;
        cont.innerHTML = p.masLentos.map(function (v) {
            var segs = fases.map(function (f) {
                var h = v.fases[f] || 0;
                return h ? '<span style="width:' + (100 * h / max) + '%;background:' + colorFase(f) + '" title="' + esc(nombreFase(f)) + ': ' + dur(h) + '"></span>' : '';
            }).join('');
            return '<a class="de-lento" href="' + SGV.urlViaje + '?id=' + v.id + '">' +
                '<div class="info"><b>' + esc(v.pedido || 'Viaje ' + v.id) + '</b> <span class="muted">' + esc(v.carreta || '') + ' · ' + esc(v.tracto || '') +
                ' · ' + fecha(v.programacion) + ' · ' + esc(v.planta) + '</span></div>' +
                '<div class="barra">' + segs + (tipico ? '<em style="left:' + (100 * tipico / max) + '%" title="Tránsito típico ' + dur(tipico) + '"></em>' : '') + '</div>' +
                '<div class="total"><b>' + dur(v.transito) + '</b>' +
                (v.tramoCritico ? '<span class="critico">' + esc(v.tramoCritico) + ' +' + dur(v.exceso) + '</span>' : '') + '</div></a>';
        }).join('') + '<div class="de-legend de-legend-sm">' + fases.map(function (f) {
            return '<span style="--fase:' + colorFase(f) + '"><i></i>' + esc(nombreFase(f)) + '</span>';
        }).join('') + '<span class="tipico"><em></em>Tránsito típico ' + dur(tipico) + '</span></div>';
    }

    // ================= Tablas =================
    function renderTabla(p) {
        var tramos = p.tramos;
        var tbody = document.querySelector('#tablaTramos tbody');
        var maxEscala = Math.max.apply(null, tramos.map(function (t) { return t.actual.p90 || 0; })) || 1;
        function pos(v) { return Math.min(100, 100 * v / maxEscala) + '%'; }

        var html = '', faseActual = null;
        tramos.forEach(function (t) {
            if (t.fase !== faseActual) {
                faseActual = t.fase;
                html += '<tr class="grupo"><td colspan="6">' + esc(nombreFase(t.fase)) + '</td></tr>';
            }
            var a = t.actual;
            var rango = a.n
                ? '<div class="de-rango" style="--fase:' + colorFase(t.fase) + '"><div class="eje"></div>' +
                  '<div class="iqr" style="left:' + pos(a.p25) + ';width:calc(' + pos(a.p75) + ' - ' + pos(a.p25) + ')"></div>' +
                  '<div class="med" style="left:' + pos(a.mediana) + '"></div>' +
                  '<div class="p90" style="left:' + pos(a.p90) + '"></div></div>'
                : '';
            html += '<tr data-tramo="' + t.id + '"' + (tramoSel === t.id ? ' class="sel"' : '') + '>' +
                '<td><span class="nombre" style="--fase:' + colorFase(t.fase) + '"><i></i>' + esc(t.nombre) +
                (t.espera ? '<span class="espera">espera</span>' : '') + '</span></td>' +
                '<td class="num"><b>' + dur(a.mediana) + '</b></td>' +
                '<td title="P25 ' + dur(a.p25) + ' · P75 ' + dur(a.p75) + '">' + rango + '</td>' +
                '<td class="num muted">' + dur(a.p90) + '</td>' +
                '<td class="num muted">' + num(a.n) + '</td>' +
                '<td class="num">' + (a.mediana == null ? '' : delta(a.mediana, t.medianaAnterior, { menorEsMejor: true, formato: dur })) + '</td></tr>';
        });
        var k = p.kpi;
        html += '<tr class="grupo"><td colspan="6">Totales</td></tr>' +
            filaTotal('transito', EXTRA.transito, k.transito, k.transitoAnterior) +
            filaTotal('ciclo', EXTRA.ciclo, k.ciclo, k.cicloAnterior);
        tbody.innerHTML = html;

        function filaTotal(id, nombre, e, ant) {
            return '<tr data-tramo="' + id + '"' + (tramoSel === id ? ' class="sel"' : '') + '>' +
                '<td><b>' + esc(nombre) + '</b></td><td class="num"><b>' + dur(e.mediana) + '</b></td>' +
                '<td class="muted">P25 ' + dur(e.p25) + ' · P75 ' + dur(e.p75) + '</td>' +
                '<td class="num muted">' + dur(e.p90) + '</td><td class="num muted">' + num(e.n) + '</td>' +
                '<td class="num">' + (e.mediana == null ? '' : delta(e.mediana, ant, { menorEsMejor: true, formato: dur })) + '</td></tr>';
        }
    }

    function renderPedidos(p) {
        var tbody = document.querySelector('#tablaPedidos tbody');
        document.getElementById('pedidosMeta').textContent = p.pedidos.length + ' pedido(s)';
        if (!p.pedidos.length) return vacio(tbody, 6, 'Sin pedidos en el período.');
        tbody.innerHTML = p.pedidos.map(function (x) {
            var pct = x.viajes ? 100 * x.descargados / x.viajes : 0;
            var rango = fecha(x.desde) === fecha(x.hasta) ? fecha(x.desde) : fecha(x.desde) + ' – ' + fecha(x.hasta);
            return '<tr><td><b>' + esc(x.pedido) + '</b></td>' +
                '<td>' + esc(x.planta || '—') + '</td>' +
                '<td class="muted">' + rango + '</td>' +
                '<td class="num">' + x.viajes + '</td>' +
                '<td><div class="de-avance"><div class="barra"><span style="width:' + pct + '%"></span></div>' +
                '<span class="txt">' + x.descargados + '/' + x.viajes + '</span></div></td>' +
                '<td class="num">' + dur(x.transito) + '</td></tr>';
        }).join('');
    }

    // ================= Navegación del período =================
    function moverMes(paso) {
        var ddlMes = document.getElementById(SGV.ddlMes), ddlAnio = document.getElementById(SGV.ddlAnio);
        var mes = parseInt(ddlMes.value, 10), anio = parseInt(ddlAnio.value, 10);
        if (mes === 0) {
            anio += paso;
        } else {
            mes += paso;
            if (mes < 1) { mes = 12; anio--; }
            if (mes > 12) { mes = 1; anio++; }
        }
        if (!Array.prototype.some.call(ddlAnio.options, function (o) { return o.value === String(anio); })) {
            if (SGV.avisar) SGV.avisar('No hay viajes programados en ' + anio + '.', { tipo: 'info' });
            return;
        }
        ddlMes.value = String(mes);
        ddlAnio.value = String(anio);
        document.getElementById(SGV.btnFiltrar).click();
    }

    // ================= Inicio =================
    function iniciar() {
        document.getElementById('btnMesAnterior').addEventListener('click', function () { moverMes(-1); });
        document.getElementById('btnMesSiguiente').addEventListener('click', function () { moverMes(1); });
        document.getElementById('btnDetalle').addEventListener('click', function () {
            var panel = document.getElementById('detalleTramos'), abierto = panel.hasAttribute('hidden');
            if (abierto) panel.removeAttribute('hidden'); else panel.setAttribute('hidden', '');
            this.setAttribute('aria-expanded', abierto ? 'true' : 'false');
        });

        var hdn = document.getElementById(SGV.hdnDatos);
        if (!hdn || !hdn.value) return;
        try { datos = JSON.parse(hdn.value); } catch (e) { console.error('Datos del dashboard inválidos', e); return; }

        if (window.Chart) {
            Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
            Chart.defaults.color = '#4b5563';
        }
        renderOperacion(datos.operacion);
        var p = datos.periodo;
        if (!p) return;
        renderKpis(p);
        renderTabla(p);
        renderPedidos(p);
        renderLentos(p);
        if (window.Chart) {
            renderFases(p);
            renderTramos(p);
            renderEvolucion(p);
            renderCumplimiento(p);
            renderPlantas(p);
        }

        document.getElementById('tablaTramos').addEventListener('click', function (ev) {
            var tr = ev.target.closest('tr[data-tramo]');
            if (tr) seleccionarTramo(tr.getAttribute('data-tramo'), true);
        });
        document.getElementById('evolChips').addEventListener('click', function (ev) {
            var b = ev.target.closest('.de-chip[data-tramo]');
            if (b) seleccionarTramo(b.getAttribute('data-tramo'), false);
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar);
    else iniciar();
})();

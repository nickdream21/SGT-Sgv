// Detalle del viaje: mapa con la ruta del GPS de cada tramo (carga y descarga), sus hitos,
// las geocercas y una reproducción en el tiempo con el camión moviéndose por el recorrido.
(function () {
    var contenedor = document.getElementById('rutaMapa');
    if (!contenedor || !SGV.idSeguimiento || typeof L === 'undefined') return;

    var COLORES = { carga: '#1d4ed8', descarga: '#c2410c' };
    var estado = document.getElementById('rutaEstado');
    var reloj = document.getElementById('rutaReloj');
    var control = document.getElementById('rutaControl');
    var deslizador = document.getElementById('rutaTiempo');
    var botonPlay = document.getElementById('rutaPlay');
    var velocidad = document.getElementById('rutaVelocidad');
    var avisos = document.getElementById('rutaAvisos');
    var resumen = document.getElementById('rutaResumen');
    var linea = document.getElementById('rutaLinea');
    var pista = document.getElementById('rutaPista');
    var dias = document.getElementById('rutaDias');
    var seguir = document.getElementById('rutaSeguir');
    var NOMBRES = { carga: 'Carga', descarga: 'Descarga' };
    var PARADA_MIN_MS = 30 * 60000, PARADA_RADIO_M = 250;

    var mapa = L.map(contenedor, { scrollWheelZoom: false, zoomSnap: 0.5 }).setView([-6.5, -79.8], 6);
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
    }).addTo(mapa);
    // La rueda del ratón solo hace zoom después de hacer clic en el mapa (no secuestra el scroll de la página).
    mapa.on('click', function () { mapa.scrollWheelZoom.enable(); });
    mapa.on('mouseout', function () { mapa.scrollWheelZoom.disable(); });

    // dd/MM HH:mm en hora de Perú (es-PE no respeta day: '2-digit', se arma con las partes).
    var partesLima = new Intl.DateTimeFormat('en-GB', {
        timeZone: 'America/Lima', day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', hour12: false
    });
    var horaLima = {
        format: function (fecha) {
            var p = {};
            partesLima.formatToParts(fecha).forEach(function (x) { p[x.type] = x.value; });
            return p.day + '/' + p.month + ' ' + p.hour + ':' + p.minute;
        }
    };

    var tramos = [];      // { clave, datos, latlngs, completa, avance, camion, hitos: [{t, marcador}] }
    var capas = {};       // clave -> L.layerGroup
    var tMin = Infinity, tMax = -Infinity, tActual = 0;
    var reproduciendo = false, ultimoCuadro = 0;

    function mostrarEstado(texto, error) {
        estado.innerHTML = '';
        if (!error) {
            var g = document.createElement('span');
            g.className = 'sx-ruta-girando'; g.setAttribute('aria-hidden', 'true');
            estado.appendChild(g);
            estado.appendChild(document.createTextNode(' '));
        }
        estado.appendChild(document.createTextNode(texto));
        estado.classList.toggle('sx-ruta-estado-error', !!error);
        estado.hidden = false;
    }

    function aviso(texto) {
        var li = document.createElement('li');
        li.textContent = texto;
        avisos.appendChild(li);
    }

    function iconoCamion(clave, placa) {
        return L.divIcon({
            className: 'sx-camion-icono',
            html: '<span class="sx-camion sx-camion-' + clave + '"><i class="fas fa-truck" aria-hidden="true"></i></span>' +
                  '<span class="sx-camion-placa">' + escapar(placa) + '</span>',
            iconSize: [34, 34], iconAnchor: [17, 17]
        });
    }

    function iconoHito(clave, n) {
        return L.divIcon({
            className: 'sx-hito-icono',
            html: '<span class="sx-mk sx-mk-' + clave + '">' + n + '</span>',
            iconSize: [22, 22], iconAnchor: [11, 11]
        });
    }

    function escapar(s) {
        var d = document.createElement('div');
        d.textContent = s == null ? '' : String(s);
        return d.innerHTML;
    }

    function duracion(ms) {
        var min = Math.round(ms / 60000), h = Math.floor(min / 60), d = Math.floor(h / 24);
        if (d > 0) return d + ' d ' + (h % 24) + ' h';
        if (h > 0) return h + ' h ' + (min % 60 < 10 ? '0' : '') + (min % 60) + ' min';
        return min + ' min';
    }

    function metros(a, b) {
        var kLat = 111320, kLng = 111320 * Math.cos(a[0] * Math.PI / 180);
        var dy = (a[0] - b[0]) * kLat, dx = (a[1] - b[1]) * kLng;
        return Math.sqrt(dx * dx + dy * dy);
    }

    // Paradas: la unidad se queda dentro de un radio corto durante 30 minutos o más.
    function detectarParadas(p) {
        var paradas = [], i = 0;
        while (i < p.length - 1) {
            var j = i;
            while (j + 1 < p.length && metros(p[i], p[j + 1]) <= PARADA_RADIO_M) j++;
            if (j > i && p[j][2] - p[i][2] >= PARADA_MIN_MS) {
                paradas.push({ desde: p[i][2], hasta: p[j][2], latlng: [p[i][0], p[i][1]] });
                i = j;
            } else i++;
        }
        return paradas;
    }

    function iconoParada() {
        return L.divIcon({ className: 'sx-hito-icono', html: '<span class="sx-parada"></span>', iconSize: [12, 12], iconAnchor: [6, 6] });
    }

    function dibujarZonas(zonas) {
        var grupo = L.layerGroup();
        (zonas || []).forEach(function (z) {
            L.polygon(z.Vertices, { color: '#7c3aed', weight: 1.5, dashArray: '5 4', fillColor: '#8b5cf6', fillOpacity: 0.16, interactive: true })
                .bindTooltip(escapar(z.Nombre), { sticky: true, className: 'sx-ruta-tip' })
                .addTo(grupo);
        });
        capas.zonas = grupo.addTo(mapa);
    }

    function dibujarTramo(datos) {
        var clave = datos.Clave;
        var km = document.getElementById('rutaKm' + clave);
        if (datos.Aviso) aviso(datos.Titulo + ': ' + datos.Aviso);
        if (datos.Inicio) aviso(datos.Titulo + ': ' + datos.Inicio);
        if (!datos.Puntos || datos.Puntos.length < 2) {
            if (km) km.textContent = '· sin recorrido';
            return;
        }
        if (km) km.textContent = '· ' + datos.Km.toLocaleString('es-PE') + ' km';
        if (datos.EnCurso) aviso(datos.Titulo + ': todavía en curso, el recorrido llega hasta la última posición del GPS.');

        var color = COLORES[clave];
        var latlngs = datos.Puntos.map(function (p) { return [p[0], p[1]]; });
        var grupo = L.layerGroup();
        var completa = L.polyline(latlngs, { color: color, weight: 4, opacity: 0.28, lineJoin: 'round' }).addTo(grupo);
        var avance = L.polyline([], { color: color, weight: 5, opacity: 0.95, lineJoin: 'round' }).addTo(grupo);
        completa.bindTooltip(escapar(datos.Titulo + ' · ' + datos.Tracto), { sticky: true, className: 'sx-ruta-tip' });

        var hitos = datos.Hitos.map(function (h, i) {
            var m = L.marker([h.Lat, h.Lng], { icon: iconoHito(clave, i + 1), keyboard: false, zIndexOffset: 500 })
                .bindTooltip('<strong>' + (i + 1) + '. ' + escapar(h.Nombre) + '</strong><br>' + escapar(h.Hora) +
                             (h.PuntoGps ? '' : '<br><em>ubicado por la hora</em>'), { className: 'sx-ruta-tip', direction: 'top', offset: [0, -8] })
                .addTo(grupo);
            m.on('click', function () { irA(h.T, false); });
            return { t: h.T, nombre: h.Nombre, marcador: m };
        });

        var camion = L.marker(latlngs[0], { icon: iconoCamion(clave, datos.Tracto), zIndexOffset: 1000, keyboard: false }).addTo(grupo);
        capas[clave] = grupo.addTo(mapa);

        var paradas = detectarParadas(datos.Puntos);
        if (!capas.paradas) capas.paradas = L.layerGroup().addTo(mapa);
        paradas.forEach(function (pa) {
            L.marker(pa.latlng, { icon: iconoParada(), keyboard: false, zIndexOffset: 100 })
                .bindTooltip('<strong>Parada de ' + duracion(pa.hasta - pa.desde) + '</strong><br>' + escapar(NOMBRES[clave] + ' ' + datos.Tracto) + '<br>' +
                             horaLima.format(new Date(pa.desde)) + ' → ' + horaLima.format(new Date(pa.hasta)), { className: 'sx-ruta-tip', direction: 'top', offset: [0, -6] })
                .on('click', function () { irA(pa.desde, false); })
                .addTo(capas.paradas);
        });

        var t0 = datos.Puntos[0][2], t1 = datos.Puntos[datos.Puntos.length - 1][2];
        tMin = Math.min(tMin, t0); tMax = Math.max(tMax, t1);
        tramos.push({ clave: clave, datos: datos, latlngs: latlngs, completa: completa, avance: avance, camion: camion, hitos: hitos,
                      paradas: paradas, t0: t0, t1: t1 });
    }

    // Posición del tramo en el instante t: búsqueda binaria e interpolación entre dos puntos.
    function posicion(tramo, t) {
        var p = tramo.datos.Puntos, lo = 0, hi = p.length - 1;
        if (t <= p[0][2]) return { i: 0, latlng: tramo.latlngs[0], vel: 0, antes: true };
        if (t >= p[hi][2]) return { i: hi, latlng: tramo.latlngs[hi], vel: 0, despues: true };
        while (hi - lo > 1) {
            var mid = (lo + hi) >> 1;
            if (p[mid][2] <= t) lo = mid; else hi = mid;
        }
        var f = (t - p[lo][2]) / Math.max(1, p[hi][2] - p[lo][2]);
        return {
            i: lo,
            latlng: [p[lo][0] + (p[hi][0] - p[lo][0]) * f, p[lo][1] + (p[hi][1] - p[lo][1]) * f],
            vel: p[hi][3]
        };
    }

    function pintar(t) {
        tActual = t;
        var partes = [], foco = null;
        var posiciones = tramos.map(function (tr) { return posicion(tr, t); });
        var algunoSalio = posiciones.some(function (pos) { return !pos.antes; });
        tramos.forEach(function (tr, n) {
            var pos = posiciones[n];
            // Un solo camión a la vez: el tramo que ya terminó cede el sitio al que salió después
            // (carga y descarga suelen ser el mismo tracto) y el que aún no sale no se muestra.
            var relevado = pos.despues && tramos.some(function (otro, m) { return otro.t0 > tr.t0 && !posiciones[m].antes; });
            var visible = pos.antes ? (!algunoSalio && n === 0) : !relevado;
            tr.camion.setLatLng(pos.latlng);
            tr.camion.setOpacity(!visible ? 0 : (pos.antes ? 0.45 : 1));
            if (visible && !pos.antes) foco = tr.camion;
            tr.avance.setLatLngs(pos.antes ? [] : tr.latlngs.slice(0, pos.i + 1).concat([pos.latlng]));
            tr.hitos.forEach(function (h) {
                var el = h.marcador.getElement();
                if (el) el.classList.toggle('sx-mk-pasado', h.t <= t);
            });
            if (!visible) return;
            var placa = NOMBRES[tr.clave] + ' ' + tr.datos.Tracto;
            if (pos.antes) partes.push(placa + ': aún no sale');
            else if (pos.despues) partes.push(placa + (tr.datos.EnCurso ? ': última posición' : ': en base'));
            else {
                var ultimo = null;
                tr.hitos.forEach(function (h) { if (h.t <= t) ultimo = h.nombre; });
                partes.push(placa + ' · ' + Math.round(pos.vel) + ' km/h' + (ultimo ? ' · después de: ' + ultimo : ''));
            }
        });
        document.getElementById('rutaHora').textContent = horaLima.format(new Date(t));
        document.getElementById('rutaDetalle').textContent = partes.join('   |   ');
        deslizador.value = Math.round((t - tMin) / Math.max(1, tMax - tMin) * 1000);
        deslizador.setAttribute('aria-valuetext', horaLima.format(new Date(t)));
        if (cursor) cursor.style.left = pct(t) + '%';
        if (reproduciendo && seguir.checked && foco && !mapa.getBounds().pad(-0.2).contains(foco.getLatLng()))
            mapa.panTo(foco.getLatLng(), { animate: false }); // sin animación: una por cuadro frenaba la reproducción
    }

    // ----- Línea de tiempo: un carril por tramo con sus hitos y paradas, y el cursor del momento actual -----
    var cursor = null;
    function pct(t) { return (t - tMin) / Math.max(1, tMax - tMin) * 100; }

    function dibujarLinea() {
        pista.innerHTML = ''; dias.innerHTML = '';
        tramos.forEach(function (tr) {
            var carril = document.createElement('div');
            carril.className = 'sx-carril sx-carril-' + tr.clave;
            var nombre = document.createElement('span');
            nombre.className = 'sx-carril-nombre'; nombre.textContent = NOMBRES[tr.clave];
            carril.appendChild(nombre);
            var barra = document.createElement('span');
            barra.className = 'sx-carril-barra';
            barra.style.left = pct(tr.t0) + '%'; barra.style.width = Math.max(0.3, pct(tr.t1) - pct(tr.t0)) + '%';
            carril.appendChild(barra);
            tr.paradas.forEach(function (pa) {
                var b = document.createElement('span');
                b.className = 'sx-carril-parada';
                b.style.left = pct(pa.desde) + '%'; b.style.width = Math.max(0.25, pct(pa.hasta) - pct(pa.desde)) + '%';
                b.title = 'Parada de ' + duracion(pa.hasta - pa.desde) + ' · ' + horaLima.format(new Date(pa.desde)) + ' → ' + horaLima.format(new Date(pa.hasta));
                carril.appendChild(b);
            });
            tr.hitos.forEach(function (h, i) {
                var b = document.createElement('button');
                b.type = 'button'; b.className = 'sx-carril-hito';
                b.style.left = pct(h.t) + '%'; b.textContent = i + 1;
                b.title = (i + 1) + '. ' + h.nombre + ' · ' + horaLima.format(new Date(h.t));
                b.setAttribute('aria-label', b.title);
                b.addEventListener('pointerdown', function (e) { e.stopPropagation(); });
                b.addEventListener('click', function () {
                    irA(h.t, false);
                    mapa.flyTo(h.marcador.getLatLng(), Math.max(mapa.getZoom(), 12), { duration: 0.8 });
                });
                carril.appendChild(b);
            });
            pista.appendChild(carril);
        });
        cursor = document.createElement('span');
        cursor.className = 'sx-carril-cursor';
        pista.appendChild(cursor);

        // Marcas de cada medianoche (hora de Perú, UTC-5).
        var DIA = 86400000, LIMA = 5 * 3600000;
        var saltar = Math.max(1, Math.ceil((tMax - tMin) / DIA / 10));
        for (var k = Math.ceil((tMin - LIMA) / DIA), n = 0; k * DIA + LIMA <= tMax; k++, n++) {
            if (n % saltar) continue;
            var m = document.createElement('span');
            m.style.left = pct(k * DIA + LIMA) + '%';
            m.textContent = horaLima.format(new Date(k * DIA + LIMA)).slice(0, 5);
            dias.appendChild(m);
        }
    }

    function buscarEnPista(e) {
        var r = pista.getBoundingClientRect();
        var f = Math.min(1, Math.max(0, (e.clientX - r.left) / Math.max(1, r.width)));
        detener();
        pintar(tMin + (tMax - tMin) * f);
    }
    pista.addEventListener('pointerdown', function (e) {
        try { pista.setPointerCapture(e.pointerId); } catch (x) { /* puntero sintético o ya liberado */ }
        buscarEnPista(e);
        pista.onpointermove = buscarEnPista;
    });
    pista.addEventListener('pointerup', function () { pista.onpointermove = null; });
    pista.addEventListener('pointercancel', function () { pista.onpointermove = null; });

    function dibujarResumen() {
        resumen.innerHTML = '';
        tramos.forEach(function (tr) {
            var detenido = tr.paradas.reduce(function (s, pa) { return s + (pa.hasta - pa.desde); }, 0);
            var total = tr.t1 - tr.t0, vmax = 0;
            tr.datos.Puntos.forEach(function (p) { if (p[3] > vmax && p[3] < 130) vmax = p[3]; });
            var caja = document.createElement('div');
            caja.className = 'sx-resumen sx-resumen-' + tr.clave;
            var datos = [
                [tr.datos.Km.toLocaleString('es-PE') + ' km', 'recorridos'],
                [duracion(total), tr.datos.EnCurso ? 'en ruta (en curso)' : 'de inicio a fin'],
                [duracion(Math.max(0, total - detenido)), 'en marcha'],
                [duracion(detenido), tr.paradas.length + (tr.paradas.length === 1 ? ' parada' : ' paradas')],
                [Math.round(vmax) + ' km/h', 'velocidad máxima']
            ];
            var html = '<div class="sx-resumen-titulo">' + NOMBRES[tr.clave] + ' <span class="sx-mono">' + escapar(tr.datos.Tracto) + '</span></div><div class="sx-resumen-datos">';
            datos.forEach(function (d) { html += '<span><strong>' + escapar(d[0]) + '</strong>' + escapar(d[1]) + '</span>'; });
            caja.innerHTML = html + '</div>';
            resumen.appendChild(caja);
        });
    }

    // Hitos que caen en el mismo punto a este zoom (p. ej. los de Trujillo): se abren en fila
    // para que se vean todos los números en vez de uno encima del otro.
    function separarHitos() {
        var grupos = [];
        tramos.forEach(function (tr) {
            if (!mapa.hasLayer(capas[tr.clave])) return;
            tr.hitos.forEach(function (h) {
                var pt = mapa.latLngToLayerPoint(h.marcador.getLatLng());
                var g = grupos.filter(function (x) { return Math.abs(x.pt.x - pt.x) < 18 && Math.abs(x.pt.y - pt.y) < 18; })[0];
                if (g) g.items.push(h); else grupos.push({ pt: pt, items: [h] });
            });
        });
        grupos.forEach(function (g) {
            var n = g.items.length, porFila = Math.min(n, 6);
            g.items.forEach(function (h, k) {
                var el = h.marcador.getElement();
                if (!el) return;
                var fila = Math.floor(k / porFila), col = k % porFila;
                var enFila = Math.min(porFila, n - fila * porFila);
                el.style.marginLeft = (-11 + (n > 1 ? (col - (enFila - 1) / 2) * 23 : 0)) + 'px';
                el.style.marginTop = (-11 + (n > 1 ? fila * 23 : 0)) + 'px';
            });
        });
    }
    mapa.on('zoomend', separarHitos);

    // Número del hito en la lista, igual al del mapa y al de la línea de tiempo.
    function numerarLista() {
        document.querySelectorAll('.sx-tramo').forEach(function (seccion, indice) {
            var clave = indice === 0 ? 'carga' : 'descarga';
            var tr = tramos.filter(function (x) { return x.clave === clave; })[0];
            if (!tr) return;
            seccion.querySelectorAll('.sx-hito').forEach(function (li) {
                var n = -1;
                tr.hitos.forEach(function (h, i) { if (h.nombre === li.dataset.nombre) n = i; });
                var texto = li.querySelector('.sx-hito-texto > span');
                if (n < 0 || !texto || texto.querySelector('.sx-hito-num')) return;
                var s = document.createElement('span');
                s.className = 'sx-hito-num sx-hito-num-' + clave; s.textContent = n + 1;
                s.title = 'Hito ' + (n + 1) + ' en el mapa';
                texto.insertBefore(s, texto.firstChild);
            });
        });
    }

    function cuadro(ahora) {
        if (!reproduciendo) return;
        var dt = ultimoCuadro ? ahora - ultimoCuadro : 0;
        ultimoCuadro = ahora;
        var t = tActual + dt * Number(velocidad.value);
        if (t >= tMax) { pintar(tMax); detener(); return; }
        pintar(t);
        requestAnimationFrame(cuadro);
    }

    function reproducir() {
        if (tActual >= tMax) pintar(tMin);
        reproduciendo = true; ultimoCuadro = 0;
        botonPlay.querySelector('i').className = 'fas fa-pause';
        botonPlay.querySelector('span').textContent = 'Pausar';
        requestAnimationFrame(cuadro);
    }

    function detener() {
        reproduciendo = false;
        botonPlay.querySelector('i').className = 'fas fa-play';
        botonPlay.querySelector('span').textContent = 'Reproducir';
    }

    function verTodo() {
        var limites = null;
        tramos.forEach(function (tr) {
            if (!mapa.hasLayer(capas[tr.clave])) return;
            limites = limites ? limites.extend(tr.completa.getBounds()) : tr.completa.getBounds();
        });
        if (limites) mapa.fitBounds(limites, { padding: [30, 30] });
    }

    // Lleva la animación a un momento y centra el camión que está en movimiento.
    function irA(t, centrar) {
        detener();
        pintar(Math.min(tMax, Math.max(tMin, t)));
        if (centrar) {
            var activo = tramos.filter(function (tr) { return !posicion(tr, t).antes; }).pop();
            if (activo) mapa.flyTo(activo.camion.getLatLng(), Math.max(mapa.getZoom(), 11), { duration: 0.8 });
        }
    }

    botonPlay.addEventListener('click', function () { if (reproduciendo) detener(); else reproducir(); });
    deslizador.addEventListener('input', function () {
        detener();
        pintar(tMin + (tMax - tMin) * Number(deslizador.value) / 1000);
    });
    document.getElementById('rutaAjustar').addEventListener('click', verTodo);
    document.querySelectorAll('.sx-ruta-capas input[data-capa]').forEach(function (chk) {
        chk.addEventListener('change', function () {
            var capa = capas[chk.dataset.capa];
            if (!capa) return;
            if (chk.checked) capa.addTo(mapa); else mapa.removeLayer(capa);
            separarHitos();
        });
    });

    // "Evidencia" de un hito en la línea de tiempo: el mapa va a ese momento.
    document.querySelectorAll('.sx-tramo').forEach(function (seccion, indice) {
        var clave = indice === 0 ? 'carga' : 'descarga';
        seccion.querySelectorAll('.sx-hito').forEach(function (li) {
            li.querySelector('.sx-ver').addEventListener('click', function () {
                var tr = tramos.filter(function (x) { return x.clave === clave; })[0];
                if (!tr) return;
                var h = tr.hitos.filter(function (x) { return x.nombre === li.dataset.nombre; })[0];
                if (h) {
                    irA(h.t, false);
                    mapa.flyTo(h.marcador.getLatLng(), Math.max(mapa.getZoom(), 13), { duration: 0.8 });
                }
            });
        });
    });

    mostrarEstado('Cargando el recorrido del GPS… (la primera vez puede tardar unos segundos)');
    $.ajax({
        type: 'POST',
        url: 'SeguimientoViaje.aspx/ObtenerRuta',
        data: JSON.stringify({ idSeguimiento: SGV.idSeguimiento }),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        timeout: 180000
    }).done(function (r) {
        var res = r && r.d;
        if (!res || !res.ok) { mostrarEstado((res && res.mensaje) || 'No se pudo cargar la ruta.', true); return; }
        dibujarZonas(res.ruta.Zonas);
        res.ruta.Tramos.forEach(dibujarTramo);
        if (!tramos.length) { mostrarEstado('Todavía no hay recorrido del GPS para este viaje.', true); return; }

        var sub = tramos.map(function (tr) {
            return (tr.clave === 'carga' ? 'Carga ' : 'Descarga ') + tr.datos.Tracto + ': ' + tr.datos.Desde + ' → ' + tr.datos.Hasta;
        });
        document.getElementById('rutaSub').textContent = sub.join(' · ');
        estado.hidden = true;
        reloj.hidden = false;
        control.hidden = false;
        resumen.hidden = false;
        linea.hidden = false;
        tramos.sort(function (a, b) { return a.t0 - b.t0; });
        dibujarResumen();
        dibujarLinea();
        numerarLista();
        mapa.invalidateSize();
        verTodo();
        separarHitos();
        pintar(tMax); // abre con el recorrido completo; "Reproducir" lo anima desde el inicio
    }).fail(function (x, tipo) {
        mostrarEstado(tipo === 'timeout' ? 'El GPS tardó demasiado en responder. Recarga la página para intentarlo de nuevo.'
                                         : 'No se pudo cargar la ruta del GPS.', true);
    });
})();

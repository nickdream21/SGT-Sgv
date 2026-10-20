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

        var t0 = datos.Puntos[0][2], t1 = datos.Puntos[datos.Puntos.length - 1][2];
        tMin = Math.min(tMin, t0); tMax = Math.max(tMax, t1);
        tramos.push({ clave: clave, datos: datos, latlngs: latlngs, completa: completa, avance: avance, camion: camion, hitos: hitos });
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
        var partes = [];
        tramos.forEach(function (tr) {
            var pos = posicion(tr, t);
            tr.camion.setLatLng(pos.latlng);
            tr.camion.setOpacity(pos.antes ? 0.45 : 1);
            tr.avance.setLatLngs(pos.antes ? [] : tr.latlngs.slice(0, pos.i + 1).concat([pos.latlng]));
            tr.hitos.forEach(function (h) {
                var el = h.marcador.getElement();
                if (el) el.classList.toggle('sx-mk-pasado', h.t <= t);
            });
            var placa = tr.datos.Tracto;
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
        mapa.invalidateSize();
        verTodo();
        pintar(tMax); // abre con el recorrido completo; "Reproducir" lo anima desde el inicio
    }).fail(function (x, tipo) {
        mostrarEstado(tipo === 'timeout' ? 'El GPS tardó demasiado en responder. Recarga la página para intentarlo de nuevo.'
                                         : 'No se pudo cargar la ruta del GPS.', true);
    });
})();

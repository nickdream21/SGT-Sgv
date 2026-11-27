// Tablero de seguimiento: copiar el STATUS de la unidad (fila por fila o toda la tabla) para
// pegarlo en el Excel STATUS GENERAL o enviarlo.
(function () {
    'use strict';

    function copiar(texto, mensaje) {
        function listo() { SGV.avisar(mensaje); }
        function respaldo() {
            var area = document.createElement('textarea');
            area.value = texto;
            area.setAttribute('readonly', '');
            area.style.position = 'fixed';
            area.style.opacity = '0';
            document.body.appendChild(area);
            area.select();
            var ok = false;
            try { ok = document.execCommand('copy'); } catch (e) { ok = false; }
            document.body.removeChild(area);
            if (ok) listo(); else SGV.avisar('No se pudo copiar: selecciona el texto y usa Ctrl+C.');
        }
        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(texto).then(listo, respaldo);
        } else {
            respaldo();
        }
    }

    // "Actualizar GPS": la consulta corre en el servidor en segundo plano y aquí solo se espera a que termine.
    // Como postback tardaba hasta 80 s con la sesión tomada y cualquier otra pantalla abierta mientras tanto
    // (entrar a un viaje, otra pestaña) quedaba esperando. Si el servicio no responde, se hace como antes.
    var consultandoGps = false, ESPERA_MAXIMA_GPS = 6 * 60000;
    SGV.actualizarGps = function (boton) {
        if (consultandoGps) return;
        consultandoGps = true;
        var texto = boton.value, inicio = Date.now();
        boton.value = 'Consultando el GPS…';
        boton.disabled = true;

        function pedir(url, metodo) {
            var opciones = { method: metodo, credentials: 'same-origin', cache: 'no-store' };
            // El servidor exige JSON para lanzarla (415 si no): un formulario de otro sitio no puede mandarlo.
            if (metodo === 'POST') { opciones.headers = { 'Content-Type': 'application/json' }; opciones.body = '{}'; }
            return fetch(url, opciones)
                .then(function (r) {
                    if (r.ok) return r.json();
                    var error = new Error('GPS ' + r.status);
                    error.estado = r.status;
                    // 401/403 de Global.asax (sesión cerrada o cambio de contraseña pendiente): el JSON trae el motivo.
                    return r.json().catch(function () { return {}; }).then(function (j) { error.respuesta = j; throw error; });
                });
        }
        function terminar(d) {
            consultandoGps = false;
            boton.value = texto;
            boton.disabled = false;
            if (d && d.mensaje) SGV.avisar(d.mensaje, { tipo: d.conFallas ? 'warning' : 'success', trasRecargar: true });
            var filtrar = document.getElementById(SGV.btnFiltrar);
            if (filtrar) filtrar.click(); else location.reload();
        }
        function comoAntes(error) {
            consultandoGps = false;
            boton.disabled = false;
            // 401/403 de Global.asax: no volver al postback. Con el cambio de contraseña pendiente, el postback se descarta
            // recargando la página, que volvía a lanzar la consulta automática: recargas en bucle.
            var r = (error && error.respuesta) || {};
            if (r.sesionCerrada || r.requiereCambioContrasena) {
                boton.value = texto;
                if (r.sesionCerrada) {
                    SGV.avisar(r.Message, { tipo: 'danger' });
                    setTimeout(function () {
                        window.location.href = (SGV.urlLogin || '/Views/Login.aspx') + '?error=' + (r.codigoLogin === 'sesion_vencida' ? 'sesion_vencida' : 'sesion_cerrada');
                    }, 2500);
                }
                return;
            }
            window.__doPostBack(boton.name, '');
        }
        function esperar() {
            pedir(SGV.urlEstadoGps, 'GET').then(function (d) {
                if (!d.enCurso || Date.now() - inicio > ESPERA_MAXIMA_GPS) terminar(d);
                else setTimeout(esperar, 3000);
            }).catch(function (error) {
                var r = (error && error.respuesta) || {};
                if (r.sesionCerrada || r.requiereCambioContrasena) comoAntes(error);
                else if (Date.now() - inicio > ESPERA_MAXIMA_GPS) terminar(null); else setTimeout(esperar, 5000);
            });
        }
        pedir(SGV.urlIniciarGps, 'POST')
            .then(function (d) { if (d.iniciada || d.enCurso) setTimeout(esperar, 2000); else comoAntes(); })
            .catch(comoAntes);
    };

    // La última consulta al GPS tiene más de 15 minutos: se consulta sola al abrir (el servidor decide;
    // tras el postback ya no se repite). Con ?auto=0 en la dirección no se lanza.
    if (SGV.autoActualizarGps) {
        var boton = document.getElementById(SGV.btnActualizarGps);
        if (boton) {
            // Al cargar este script SiteMaster.js todavía no definió SGV.avisar: se espera a que la página termine.
            $(function () {
                SGV.avisar('La última consulta al GPS tiene más de 15 minutos: se actualiza sola ahora. Puede seguir trabajando.');
                setTimeout(function () { SGV.actualizarGps(boton); }, 600);
            });
        }
    }

    // La actualización automática del GPS corre en el servidor cada 10–15 min: con la página abierta se
    // pregunta cada 2 min si hay una consulta más nueva que la mostrada y se avisa (sin recargar sola:
    // "Ver datos nuevos" vuelve a buscar con los mismos filtros).
    var aviso = document.getElementById('avisoGpsNuevo');
    if (aviso && SGV.urlEstadoGps) {
        var revisar = function () {
            if (document.hidden) return;
            fetch(SGV.urlEstadoGps, { credentials: 'same-origin', cache: 'no-store' })
                .then(function (r) { return r.ok ? r.json() : null; })
                .then(function (d) {
                    if (!d || !d.ultima || (SGV.ultimaConsultaGps && d.ultima <= SGV.ultimaConsultaGps)) return;
                    document.getElementById('avisoGpsHora').textContent = d.ultima.substr(11, 5);
                    aviso.hidden = false;
                })
                .catch(function () { /* sin red: se reintenta en la próxima vuelta */ });
        };
        setInterval(revisar, 120000);
        document.addEventListener('visibilitychange', function () { if (!document.hidden) revisar(); });
        document.getElementById('btnVerDatosNuevos').addEventListener('click', function () {
            var filtrar = document.getElementById(SGV.btnFiltrar);
            if (filtrar) filtrar.click(); else location.reload();
        });
    }

    // Las unidades de un mismo pedido van juntas bajo una cabecera (pedido, programación y cuántas son),
    // y lo que se escribe en "Buscar" filtra la tabla al instante, sin esperar al servidor.
    (function () {
        var filas = Array.prototype.slice.call(document.querySelectorAll('tr.sx-fila'));
        if (!filas.length) return;
        var columnas = filas[0].children.length, grupos = [], actual = null;
        filas.forEach(function (tr) {
            var clave = tr.dataset.pedido + '|' + tr.dataset.prog;
            if (!actual || actual.clave !== clave) {
                var cab = document.createElement('tr');
                cab.className = 'sx-grupo';
                var td = document.createElement('td');
                td.colSpan = columnas;
                cab.appendChild(td);
                tr.parentNode.insertBefore(cab, tr);
                actual = { clave: clave, cab: cab, td: td, filas: [], pedido: tr.dataset.pedido, prog: tr.dataset.prog };
                grupos.push(actual);
            }
            actual.filas.push(tr);
            tr.classList.add('sx-fila-agrupada');
        });

        function plano(t) { return t.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, ''); }
        filas.forEach(function (tr) { tr._texto = plano(tr.textContent); });

        var caja = document.getElementById(SGV.txtBuscar), cuenta = document.getElementById('sxFiltroCuenta');
        function aplicar() {
            var palabras = caja ? plano(caja.value).split(/\s+/).filter(Boolean) : [], visibles = 0;
            grupos.forEach(function (g) {
                var n = 0;
                g.filas.forEach(function (tr) {
                    var ok = palabras.every(function (p) { return tr._texto.indexOf(p) >= 0; });
                    tr.hidden = !ok;
                    if (ok) n++;
                });
                g.n = n;
                g.cab.hidden = n === 0;
                g.td.innerHTML = '';
                var b = document.createElement('strong');
                b.className = 'sx-mono'; b.textContent = g.pedido || 'Sin pedido';
                g.td.appendChild(b);
                g.td.appendChild(document.createTextNode('  ·  Prog. ' + g.prog + '  ·  ' +
                    (n === g.filas.length ? n : n + ' de ' + g.filas.length) + (g.filas.length === 1 ? ' unidad' : ' unidades')));
                visibles += n;
            });
            if (cuenta) cuenta.textContent = palabras.length ? visibles + ' de ' + filas.length + ' viajes en esta pestaña' : '';
        }

        // Paginación: POR_PAGINA viajes por página sin partir un pedido (un grupo más grande que la página va solo).
        // Pagina lo que deja la búsqueda; las filas de otra página se ocultan con una clase y no con [hidden], así
        // "Copiar STATUS" sigue copiando todas las de la pestaña. Cambiar de pestaña recarga y vuelve a la página 1.
        var POR_PAGINA = 25, pagina = 0, paginas = [];
        var nav = document.getElementById('sxPaginador'), textoNav = document.getElementById('sxPaginadorTexto'),
            anterior = document.getElementById('sxPaginaAnterior'), siguiente = document.getElementById('sxPaginaSiguiente');
        function paginar() {
            paginarSinMarcar();
            if (SGV.cierreVencidos) SGV.cierreVencidos.contarVencidosMarcados();
        }
        function paginarSinMarcar() {
            var conFilas = grupos.filter(function (g) { return g.n > 0; });
            var total = conFilas.reduce(function (a, g) { return a + g.n; }, 0);
            paginas = [];
            var actualPag = null;
            conFilas.forEach(function (g) {
                if (!actualPag || (actualPag.n > 0 && actualPag.n + g.n > POR_PAGINA)) {
                    actualPag = { grupos: [], n: 0 };
                    paginas.push(actualPag);
                }
                actualPag.grupos.push(g);
                actualPag.n += g.n;
            });
            var hayPaginas = total > POR_PAGINA && paginas.length > 1;
            if (pagina >= paginas.length) pagina = Math.max(paginas.length - 1, 0);
            grupos.forEach(function (g) {
                var fuera = hayPaginas && paginas[pagina].grupos.indexOf(g) < 0;
                g.cab.classList.toggle('sx-fuera-pagina', fuera);
                g.filas.forEach(function (tr) { tr.classList.toggle('sx-fuera-pagina', fuera); });
            });
            if (!nav) return;
            nav.hidden = !hayPaginas;
            if (!hayPaginas) return;
            var inicio = 0;
            for (var i = 0; i < pagina; i++) inicio += paginas[i].n;
            var fin = inicio + paginas[pagina].n;
            textoNav.textContent = (fin === inicio + 1 ? fin : (inicio + 1) + '–' + fin) + ' de ' + total;
            anterior.disabled = pagina === 0;
            siguiente.disabled = pagina >= paginas.length - 1;
        }
        function irA(nueva) {
            pagina = nueva;
            paginar();
            var tabs = document.querySelector('.sgv-tabs');
            if (tabs && tabs.scrollIntoView) tabs.scrollIntoView({ block: 'start' });
        }
        if (anterior) anterior.addEventListener('click', function () { if (pagina > 0) irA(pagina - 1); });
        if (siguiente) siguiente.addEventListener('click', function () { if (pagina < paginas.length - 1) irA(pagina + 1); });

        aplicar();
        paginar();
        if (caja) caja.addEventListener('input', function () { aplicar(); pagina = 0; paginar(); });
    })();

    // Cierre administrativo de los vencidos (pestaña Vencidos, solo administradores): "Otro" pide escribir el motivo y
    // el botón dice cuántos hay marcados (también en la pregunta de confirmación).
    var motivo = SGV.ddlMotivoCierre && document.getElementById(SGV.ddlMotivoCierre);
    var botonCerrar = SGV.btnCerrarSeleccionados && document.getElementById(SGV.btnCerrarSeleccionados);
    function mostrarMotivoOtro() {
        var campo = document.getElementById('campoMotivoOtro');
        if (campo && motivo) campo.hidden = motivo.value !== SGV.motivoOtro;
    }
    // "Marcar los de esta página" queda marcada solo si lo están todas las casillas a la vista (cambia al paginar o buscar).
    function casillasALaVista() {
        return document.querySelectorAll('tr.sx-fila:not([hidden]):not(.sx-fuera-pagina) input[name="cierreSel"]');
    }
    function contarVencidosMarcados() {
        var todos = document.getElementById('chkCierreTodos');
        if (todos) {
            var vista = casillasALaVista();
            todos.checked = vista.length > 0 && Array.prototype.every.call(vista, function (c) { return c.checked; });
        }
        if (!botonCerrar) return;
        var n = document.querySelectorAll('input[name="cierreSel"]:checked').length;
        botonCerrar.value = n > 0 ? 'Cerrar los seleccionados (' + n + ')' : 'Cerrar los seleccionados';
        botonCerrar.setAttribute('data-confirmar', n > 0
            ? '¿Cerrar administrativamente ' + (n === 1 ? 'el viaje vencido marcado' : 'los ' + n + ' viajes vencidos marcados') +
              '?\\nPasan a FINALIZADO con el motivo elegido y queda en la auditoría.'
            : '¿Cerrar administrativamente los viajes vencidos marcados?\\nPasan a FINALIZADO con el motivo elegido y queda en la auditoría.');
    }
    mostrarMotivoOtro();
    contarVencidosMarcados();
    SGV.cierreVencidos = { mostrarMotivoOtro: mostrarMotivoOtro, contarVencidosMarcados: contarVencidosMarcados, casillasALaVista: casillasALaVista };

    // "Cerrar los seleccionados" sin ninguna marcada: se avisa sin abrir la confirmación. Va en la captura de window,
    // antes que el data-confirmar de SiteMaster.js (captura de document). El servidor también lo rechaza.
    window.addEventListener('click', function (e) {
        if (!botonCerrar || !e.target || !e.target.closest || e.target.closest('#' + SGV.btnCerrarSeleccionados) !== botonCerrar) return;
        if (botonCerrar.sgvConfirmado || document.querySelectorAll('input[name="cierreSel"]:checked').length > 0) return;
        e.preventDefault();
        e.stopPropagation();
        SGV.avisar('Marca al menos un viaje vencido.', { tipo: 'warning' });
    }, true);

    document.addEventListener('click', function (e) {
        var uno = e.target.closest('[data-copiar]');
        if (uno) {
            var celda = uno.closest('.sx-status');
            var texto = celda.querySelector('.sx-status-texto').textContent.trim();
            copiar(texto, 'STATUS copiado: ' + texto);
            return;
        }
        if (e.target.closest('#btnCopiarStatus')) {
            // Las filas que deja el filtro al escribir, de todas las páginas (la paginación no las marca [hidden]).
            var filas = Array.prototype.map.call(document.querySelectorAll('tr.sx-fila:not([hidden]) .sx-status-texto'), function (s) { return s.textContent.trim(); });
            if (!filas.length) { SGV.avisar('No hay filas para copiar.'); return; }
            // Una fila por línea, en el orden de la tabla: se pega directo en la columna STATUS del Excel.
            copiar(filas.join('\r\n'), 'STATUS de ' + filas.length + ' unidad(es) copiado, en el orden de la tabla.');
            return;
        }
        if (e.target.closest('#btnReporteLlegadas')) {
            // El servidor arma el reporte con todas las unidades en camino a planta (no solo la pestaña elegida).
            var reporte = document.getElementById('txtReporteLlegadas').value.trim();
            if (!reporte) { SGV.avisar('No hay unidades en camino a cargar a Trujillo ni a descargar a Jave/Inbalnor con posición del GPS.'); return; }
            copiar(reporte, 'Reporte de llegadas copiado: pégalo en el mensaje.');
            return;
        }
        if (e.target.closest('#btnReporteGrifo')) {
            var grifo = document.getElementById('txtReporteGrifo').value.trim();
            if (!grifo) { SGV.avisar('No hay carros de vuelta de Ecuador camino al grifo Santa Rosa (ni descargando).'); return; }
            copiar(grifo, 'Reporte del grifo Santa Rosa copiado: pégalo en el mensaje al grifo.');
        }
    });
})();

// Acciones del marcado (data-accion / data-accion-input / data-accion-change): las despacha
// SiteMaster.js. Devolver false cancela el clic (y el postback del botón).
window.SGV = window.SGV || {};
SGV.acciones = Object.assign(SGV.acciones || {}, {
    mostrarMotivoOtro: function () { if (SGV.cierreVencidos) SGV.cierreVencidos.mostrarMotivoOtro(); },
    contarVencidosMarcados: function () { if (SGV.cierreVencidos) SGV.cierreVencidos.contarVencidosMarcados(); },
    // "Marcar los de esta página": solo las filas a la vista (la búsqueda y la página).
    marcarVencidosPagina: function (el) {
        if (!SGV.cierreVencidos) return;
        Array.prototype.forEach.call(SGV.cierreVencidos.casillasALaVista(), function (c) { c.checked = el.checked; });
        if (SGV.cierreVencidos) SGV.cierreVencidos.contarVencidosMarcados();
    },
    // La consulta corre en segundo plano (SGV.actualizarGps) y el postback del botón no se hace.
    actualizarGps: function (el) {
        if (SGV.actualizarGps) { SGV.actualizarGps(el); return false; }
        el.value = 'Consultando el GPS…';   // sin el script de arriba: postback como antes
    }
});

// Validador del motivo del cierre (cvMotivoCierre, grupo "cierre"): uno de la lista y, con "Otro", el texto escrito.
// Lo llama WebForms por su nombre (ClientValidationFunction), por eso es global.
window.sxValidarMotivoCierre = function (origen, args) {
    var otro = SGV.txtMotivoOtro && document.getElementById(SGV.txtMotivoOtro);
    args.IsValid = !!args.Value && (args.Value !== SGV.motivoOtro || !!(otro && otro.value.trim()));
};

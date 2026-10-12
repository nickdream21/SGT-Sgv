// ── Escudo anti-autocompletado ──────────────────────────────────────
// El formulario único de WebForms envuelve TODA la página, incluido el
// modal "Cambiar Contraseña" (con campos type=password reales). Eso hace
// que Chrome/Edge traten la página entera como un formulario de login y
// autocompleten campos de negocio (N° Pedido, Conductor, etc.) con
// usuario/contraseña guardados, sin que el usuario haga clic.
//
// Truco: los campos de texto quedan [readonly] hasta el primer focus, con
// lo que el navegador no tiene nada editable que autocompletar. Al hacer
// clic o tabular hasta el campo, se libera al instante (ver CSS .af-shield
// en <head> para que no se vea "deshabilitado" mientras tanto).
// No se aplica en Login.aspx: ahí SÍ queremos el autocompletado normal.
(function () {
    if (/Login\.aspx/i.test(window.location.pathname)) return;

    function protegerCampo(input) {
        if (input.dataset.afShielded === '1') return;
        input.dataset.afShielded = '1';
        input.classList.add('af-shield');
        input.setAttribute('autocomplete', 'off');
        input.setAttribute('readonly', 'readonly');
        input.addEventListener('focus', function () {
            input.removeAttribute('readonly');
        }, { once: true });
    }

    function protegerFormulario() {
        var form = document.forms[0];
        if (!form) return;
        var campos = form.querySelectorAll('input[type="text"]:not([readonly]), input:not([type]):not([readonly])');
        for (var i = 0; i < campos.length; i++) protegerCampo(campos[i]);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', protegerFormulario);
    } else {
        protegerFormulario();
    }

    // Reaplicar sobre los campos nuevos que trae cada postback parcial de UpdatePanel.
    if (window.Sys && Sys.WebForms && Sys.WebForms.PageRequestManager) {
        Sys.WebForms.PageRequestManager.getInstance().add_endRequest(protegerFormulario);
    }
})();

// ── Avisos no bloqueantes ───────────────────────────────────────────
// Reemplazo de alert(): un alert() detiene la página hasta que se cierra (y bloquea la
// automatización de pruebas). SGV.avisar(mensaje) muestra el aviso arriba a la derecha y se
// cierra solo; el tipo sale del emoji inicial (✅ éxito, ❌ error, ⚠️ advertencia; sin emoji:
// "Error..." es error y lo demás advertencia) o de opciones.tipo. Con { trasRecargar: true } el aviso se guarda y aparece después de un
// location.reload().
(function () {
    var SGV = window.SGV = window.SGV || {};
    var CLAVE = 'sgvAvisoPendiente';

    function tipoDe(mensaje) {
        if (/^\s*✅/.test(mensaje)) return 'success';
        if (/^\s*❌/.test(mensaje)) return 'danger';
        if (/^\s*⚠/.test(mensaje)) return 'warning';
        if (/^\s*Error/i.test(mensaje)) return 'danger';
        return 'warning';   // sin emoji: validaciones de formulario
    }

    function contenedor() {
        var c = document.getElementById('sgvAvisos');
        if (!c) {
            c = document.createElement('div');
            c.id = 'sgvAvisos';
            c.className = 'sgv-avisos';
            c.setAttribute('aria-live', 'polite');
            document.body.appendChild(c);
        }
        return c;
    }

    function mostrar(mensaje, tipo) {
        var aviso = document.createElement('div');
        aviso.className = 'alert alert-' + tipo + ' alert-dismissible fade show shadow-sm sgv-aviso';
        aviso.setAttribute('role', tipo === 'danger' ? 'alert' : 'status');
        var texto = document.createElement('div');
        texto.className = 'sgv-aviso-texto';
        texto.textContent = mensaje;   // texto plano: nunca interpretar HTML del mensaje
        var cerrar = document.createElement('button');
        cerrar.type = 'button';
        cerrar.className = 'close';
        cerrar.setAttribute('aria-label', 'Cerrar');
        cerrar.innerHTML = '<span aria-hidden="true">&times;</span>';
        cerrar.onclick = function () { aviso.remove(); };
        aviso.appendChild(cerrar);
        aviso.appendChild(texto);
        contenedor().appendChild(aviso);
        // Los errores se quedan más tiempo para que se alcancen a leer.
        setTimeout(function () { aviso.remove(); }, tipo === 'danger' ? 12000 : 6000);
    }

    SGV.avisar = function (mensaje, opciones) {
        mensaje = String(mensaje == null ? '' : mensaje);
        opciones = opciones || {};
        var tipo = opciones.tipo || tipoDe(mensaje);
        if (opciones.trasRecargar) {
            try {
                sessionStorage.setItem(CLAVE, JSON.stringify({ m: mensaje, t: tipo }));
                return;
            } catch (e) { /* sin sessionStorage: mostrarlo ahora */ }
        }
        if (document.body) mostrar(mensaje, tipo);
        else document.addEventListener('DOMContentLoaded', function () { mostrar(mensaje, tipo); });
    };

    function mostrarPendiente() {
        var guardado = null;
        try {
            guardado = sessionStorage.getItem(CLAVE);
            sessionStorage.removeItem(CLAVE);
        } catch (e) { }
        if (!guardado) return;
        try {
            var a = JSON.parse(guardado);
            mostrar(a.m, a.t);
        } catch (e) { }
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', mostrarPendiente);
    else mostrarPendiente();
})();

// ── Confirmaciones no bloqueantes ───────────────────────────────────
// Reemplazo de confirm(): el cuadro nativo detiene la página y no se puede estilar.
//   SGV.confirmar(mensaje, alAceptar[, { titulo, textoAceptar, peligro }])
// "peligro" pinta el botón en rojo (eliminar, rechazar, revertir). El mensaje respeta los saltos de línea.
// Además, todo elemento con onclick="return confirm('texto');" (los OnClientClick simples de los
// botones de servidor) usa este modal sin tocar su marcado: se intercepta el clic, se pregunta y,
// al aceptar, se repite el clic con el onclick retirado un instante.
(function () {
    var SGV = window.SGV = window.SGV || {};
    var $modal = null;
    var alAceptarActual = null;

    function construir() {
        if ($modal) return $modal;
        $modal = $(
            '<div class="modal fade" id="sgvConfirmarModal" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="sgvConfirmarTitulo">' +
              '<div class="modal-dialog modal-dialog-centered" role="document">' +
                '<div class="modal-content">' +
                  '<div class="modal-header">' +
                    '<h5 class="modal-title" id="sgvConfirmarTitulo">Confirmar</h5>' +
                    '<button type="button" class="close" data-dismiss="modal" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>' +
                  '</div>' +
                  '<div class="modal-body"><p class="mb-0" id="sgvConfirmarMensaje" style="white-space:pre-line"></p></div>' +
                  '<div class="modal-footer">' +
                    '<button type="button" class="btn btn-outline-secondary" data-dismiss="modal">Cancelar</button>' +
                    '<button type="button" class="btn btn-primary" id="sgvConfirmarAceptar">Aceptar</button>' +
                  '</div>' +
                '</div>' +
              '</div>' +
            '</div>').appendTo(document.body);

        $modal.on('click', '#sgvConfirmarAceptar', function () {
            var fn = alAceptarActual;
            alAceptarActual = null;
            $modal.modal('hide');
            if (typeof fn === 'function') fn();
        });
        // Suele abrirse encima de otro modal (detalle de liquidación): Bootstrap 4 deja el segundo fondo
        // detrás del primero y, al cerrarse, quita el bloqueo de scroll del que sigue abierto.
        $modal.on('show.bs.modal', function () { $modal.css('z-index', 1060); });
        $modal.on('shown.bs.modal', function () { $('.modal-backdrop').last().css('z-index', 1055); });
        $modal.on('hidden.bs.modal', function () {
            alAceptarActual = null;
            if ($('.modal.show').not($modal).length) $(document.body).addClass('modal-open');
        });
        $modal.on('shown.bs.modal', function () { $modal.find('#sgvConfirmarAceptar').trigger('focus'); });
        return $modal;
    }

    SGV.confirmar = function (mensaje, alAceptar, opciones) {
        opciones = opciones || {};
        var $m = construir();
        $m.find('#sgvConfirmarTitulo').text(opciones.titulo || 'Confirmar');
        $m.find('#sgvConfirmarMensaje').text(String(mensaje == null ? '' : mensaje));
        $m.find('#sgvConfirmarAceptar')
            .text(opciones.textoAceptar || 'Aceptar')
            .toggleClass('btn-danger', !!opciones.peligro)
            .toggleClass('btn-primary', !opciones.peligro);
        alAceptarActual = alAceptar;
        $m.modal('show');
    };

    // onclick="return confirm('...');" exacto, con el texto como literal de cadena simple.
    var PATRON = /^\s*return\s+confirm\(\s*(['"])((?:\\.|(?!\1)[^\\])*)\1\s*\)\s*;?\s*$/;

    function textoDe(literal) {
        // Solo se decodifican los escapes de cadena; no se evalúa nada.
        var mapa = { n: '\n', r: '', t: ' ' };
        return literal.replace(/\\(.)/g, function (_, c) { return mapa.hasOwnProperty(c) ? mapa[c] : c; });
    }

    document.addEventListener('click', function (e) {
        var el = e.target && e.target.closest ? e.target.closest('[onclick]') : null;
        if (!el) return;
        var m = PATRON.exec(el.getAttribute('onclick') || '');
        if (!m) return;

        e.preventDefault();
        e.stopImmediatePropagation();
        var onclick = el.getAttribute('onclick');
        var esPeligro = /elimin|rechaz|revert|borrar|anular|desactiv/i.test(m[2]);
        SGV.confirmar(textoDe(m[2]), function () {
            el.removeAttribute('onclick');
            try { el.click(); }
            finally { setTimeout(function () { el.setAttribute('onclick', onclick); }, 0); }
        }, { peligro: esPeligro, textoAceptar: esPeligro ? 'Continuar' : 'Aceptar' });
    }, true);
})();

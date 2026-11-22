// ── Campos del modal "Cambiar contraseña" ──────────────────────────
// El formulario único de WebForms envuelve toda la página. Si los campos type=password del
// modal estuvieran siempre en el DOM, Edge/Chrome tratarían la página entera como un login y
// ofrecerían "Contraseñas guardadas" en cualquier buscador. Por eso viven en
// <template id="tplCamposContrasena"> (Site.Master) y solo se clonan dentro del modal mientras
// está abierto. El handler es delegado y este script carga antes que los startup scripts que
// abren el modal (AbrirModalContrasena / cambio obligatorio), así que el clon siempre ocurre.
(function () {
    function llenar() {
        var destino = document.getElementById('camposContrasena');
        var plantilla = document.getElementById('tplCamposContrasena');
        if (!destino || !plantilla || destino.children.length) return;
        destino.appendChild(document.importNode(plantilla.content, true));
    }
    function vaciar() {
        var destino = document.getElementById('camposContrasena');
        if (destino) destino.innerHTML = '';
    }
    window.SGV = window.SGV || {};
    SGV.llenarCamposContrasena = llenar;
    if (window.jQuery) {
        jQuery(document)
            .on('show.bs.modal', '#modalCambiarContrasena', llenar)
            .on('hidden.bs.modal', '#modalCambiarContrasena', vaciar);
    }
})();

// ── Escudo anti-autocompletado (solo páginas con contraseñas a la vista) ──
// En las páginas que sí tienen campos type=password en el formulario (p. ej. los modales de
// Gestión de usuarios), los campos de texto quedan [readonly] hasta el primer focus, para que el
// navegador no los rellene con usuario/contraseña guardados. Las demás páginas ya no lo necesitan:
// las contraseñas del modal del Site.Master solo existen mientras está abierto.
// No se aplica en Login.aspx: ahí sí queremos el autocompletado normal.
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
        if (!form || !form.querySelector('input[type="password"]')) return;
        var campos = form.querySelectorAll('input[type="text"]:not([readonly]):not([autocomplete="username"]), input:not([type]):not([readonly])');
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

// ── Enter en los campos ─────────────────────────────────────────────
// En WebForms todo está en un solo <form>: Enter en cualquier campo enviaba el formulario con el
// PRIMER botón submit de la página (descargaba el Excel en vez de filtrar, o enviaba la
// liquidación del conductor sin revisión). Ahora Enter nunca envía el formulario por su cuenta:
//   - dentro de un contenedor con data-enter="<id de botón>" hace clic en ese botón (corren su
//     OnClientClick, los validadores, __doPostBack y el UpdatePanel como con el mouse);
//   - data-enter="" o fuera de toda zona, no hace nada (los formularios de alta se guardan
//     solo con su botón);
//   - los asp:Panel con DefaultButton (onkeypress="WebForm_FireDefaultButton") siguen igual;
//   - si un script de la página ya atendió el Enter (sugerencias, autocompletar) y llamó a
//     preventDefault, no se toca.
(function () {
    var TIPOS = /^(text|search|number|date|time|datetime-local|month|week|email|tel|url|password|checkbox|radio)$/;

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter' || e.defaultPrevented || e.isComposing) return;
        var el = e.target;
        if (!el || el.tagName !== 'INPUT') return;
        if (!TIPOS.test((el.getAttribute('type') || 'text').toLowerCase())) return;
        if (el.closest('[onkeypress*="WebForm_FireDefaultButton"]')) return;

        e.preventDefault();
        // Enter sostenido: una sola acción (si no, cada repetición hace otro postback).
        if (e.repeat) return;
        var zona = el.closest('[data-enter]');
        var id = zona ? zona.getAttribute('data-enter') : '';
        if (!id) return;
        var boton = document.getElementById(id);
        if (!boton || boton.disabled || boton.classList.contains('disabled')) return;
        boton.click();
    });
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
// Reemplazo del confirm nativo: el cuadro detiene la página y no se puede estilar.
//   SGV.confirmar(mensaje[, alAceptar][, { titulo, textoAceptar, peligro }]) → Promise<bool>
// "peligro" pinta el botón en rojo (eliminar, rechazar, revertir) y deja el foco en Cancelar.
// El mensaje respeta los saltos de línea; Esc, la X o Cancelar responden false.
// Botones de servidor: data-confirmar="texto" (\n = salto de línea), y opcionales
// data-confirmar-titulo, data-confirmar-aceptar y data-confirmar-peligro="true". Se cancela el
// clic, se pregunta y, al aceptar, se repite el mismo clic: su onclick (validación, postback) y el
// UpdatePanel siguen igual. Úsalo en vez de un OnClientClick con el confirm nativo.
(function () {
    var SGV = window.SGV = window.SGV || {};
    var $modal = null;
    var alAceptarActual = null;
    var resolverActual = null;
    var peligroActual = false;

    function responder(valor) {
        var resolver = resolverActual;
        resolverActual = null;
        if (typeof resolver === 'function') resolver(valor);
    }

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
                  '<div class="modal-body"><p class="mb-0 sgv-confirmar-mensaje" id="sgvConfirmarMensaje"></p></div>' +
                  '<div class="modal-footer">' +
                    '<button type="button" class="btn btn-outline-secondary" id="sgvConfirmarCancelar" data-dismiss="modal">Cancelar</button>' +
                    '<button type="button" class="btn btn-primary" id="sgvConfirmarAceptar">Aceptar</button>' +
                  '</div>' +
                '</div>' +
              '</div>' +
            '</div>').appendTo(document.body);

        $modal.on('click', '#sgvConfirmarAceptar', function () {
            var fn = alAceptarActual;
            alAceptarActual = null;
            $modal.modal('hide');
            responder(true);
            if (typeof fn === 'function') fn();
        });
        // Suele abrirse encima de otro modal (detalle de liquidación): Bootstrap 4 deja el segundo fondo
        // detrás del primero y, al cerrarse, quita el bloqueo de scroll del que sigue abierto.
        $modal.on('show.bs.modal', function () { $modal.css('z-index', 1060); });
        $modal.on('shown.bs.modal', function () { $('.modal-backdrop').last().css('z-index', 1055); });
        $modal.on('hidden.bs.modal', function () {
            alAceptarActual = null;
            responder(false);   // Cancelar, X o Esc (tras Aceptar ya no queda nada por responder)
            if ($('.modal.show').not($modal).length) $(document.body).addClass('modal-open');
        });
        // En una acción peligrosa el foco queda en Cancelar: un Enter distraído no la ejecuta.
        $modal.on('shown.bs.modal', function () {
            $modal.find(peligroActual ? '#sgvConfirmarCancelar' : '#sgvConfirmarAceptar').trigger('focus');
        });
        return $modal;
    }

    SGV.confirmar = function (mensaje, alAceptar, opciones) {
        if (alAceptar && typeof alAceptar === 'object') { opciones = alAceptar; alAceptar = null; }
        opciones = opciones || {};
        var $m = construir();
        responder(false);   // una pregunta anterior sin contestar se da por cancelada
        peligroActual = !!opciones.peligro;
        $m.find('#sgvConfirmarTitulo').text(opciones.titulo || 'Confirmar');
        $m.find('#sgvConfirmarMensaje').text(String(mensaje == null ? '' : mensaje));
        $m.find('#sgvConfirmarAceptar')
            .text(opciones.textoAceptar || 'Aceptar')
            .toggleClass('btn-danger', peligroActual)
            .toggleClass('btn-primary', !peligroActual);
        alAceptarActual = alAceptar;
        var promesa = window.Promise ? new Promise(function (resolver) { resolverActual = resolver; }) : null;
        $m.modal('show');
        return promesa;
    };

    // data-confirmar en botones y enlaces (ver arriba). La marca deja pasar el clic repetido.
    document.addEventListener('click', function (e) {
        var el = e.target && e.target.closest ? e.target.closest('[data-confirmar]') : null;
        if (!el) return;
        if (el.sgvConfirmado) { el.sgvConfirmado = false; return; }
        if (el.disabled) return;

        e.preventDefault();
        e.stopImmediatePropagation();
        var esPeligro = (el.getAttribute('data-confirmar-peligro') || '').toLowerCase() === 'true';
        SGV.confirmar(el.getAttribute('data-confirmar').replace(/\\n/g, '\n'), function () {
            el.sgvConfirmado = true;
            try { el.click(); }
            finally { el.sgvConfirmado = false; }
        }, {
            titulo: el.getAttribute('data-confirmar-titulo') || undefined,
            textoAceptar: el.getAttribute('data-confirmar-aceptar') || (esPeligro ? 'Continuar' : 'Aceptar'),
            peligro: esPeligro
        });
    }, true);
})();

// ── Cerrar sesión: borradores del navegador fuera ──────────────────
// DashboardConductor guarda en localStorage lo que el conductor va escribiendo (por id de conductor,
// hasta 45 días). En un celular o PC compartido no debe quedar tras cerrar sesión, pero tampoco debe
// perderse si el conductor pulsa "Cerrar sesión" y luego cancela el aviso de salir del navegador.
//  1. Clic en [data-cerrar-sesion]: solo se marca (SGV.sesionCerrandose y una marca en sessionStorage,
//     que vive lo que la pestaña). No se borra nada: el LinkButton hace theForm.submit(), el navegador
//     puede preguntar "¿salir del sitio?" (beforeunload) y el conductor puede cancelar.
//  2. Cancelar no genera ningún evento en la página: la marca se apaga con la siguiente interacción
//     (pointerdown / keydown fuera del botón) o al volver a la pestaña (visibilitychange → visible).
//     Si cierra la pestaña sin más, sessionStorage desaparece con ella y el borrador queda intacto.
//  3. Si sale de verdad, el servidor cierra la sesión y redirige a Login.aspx (sin "error=" en la
//     dirección): con la marca presente, ahí se borran las claves y la marca. Mientras tanto, el
//     pagehide / visibilitychange de la página guardan el borrador como siempre.
// Un vencimiento de sesión (Login.aspx?error=...) no los borra: el borrador existe para no perder lo escrito.
(function () {
    var SGV = window.SGV = window.SGV || {};
    var PREFIJOS = ['sgv-borrador-liquidacion-'];
    var MARCA = 'sgv-cerrando-sesion';

    function quitarMarca() {
        SGV.sesionCerrandose = false;
        try { window.sessionStorage.removeItem(MARCA); } catch (e) { /* sin almacenamiento */ }
    }

    SGV.borrarBorradoresLocales = function () {
        try {
            var claves = [];
            for (var i = 0; i < window.localStorage.length; i++) {
                var k = window.localStorage.key(i);
                if (k && PREFIJOS.some(function (p) { return k.indexOf(p) === 0; })) claves.push(k);
            }
            claves.forEach(function (k) { window.localStorage.removeItem(k); });
        } catch (e) { /* sin almacenamiento: no hay nada que borrar */ }
    };

    var marcada = false;
    try { marcada = window.sessionStorage.getItem(MARCA) === '1'; } catch (e) { }
    if (marcada && /\/Login\.aspx$/i.test(window.location.pathname) && !/[?&]error=/i.test(window.location.search)) {
        SGV.borrarBorradoresLocales();
    }
    quitarMarca();   // en cualquier otra página, una marca que quedó de una salida cancelada

    document.addEventListener('click', function (e) {
        var el = e.target && e.target.closest ? e.target.closest('[data-cerrar-sesion]') : null;
        if (!el) return;
        SGV.sesionCerrandose = true;
        try { window.sessionStorage.setItem(MARCA, '1'); } catch (err) { /* sin almacenamiento */ }
    }, true);

    function alInteractuar(e) {
        if (!SGV.sesionCerrandose) return;
        if (e.target && e.target.closest && e.target.closest('[data-cerrar-sesion]')) return;
        quitarMarca();
    }
    document.addEventListener('pointerdown', alInteractuar, true);
    document.addEventListener('keydown', alInteractuar, true);
    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'visible' && SGV.sesionCerrandose) quitarMarca();
    });
})();

// ── Texto seguro dentro de HTML ─────────────────────────────────────
// SGV.escaparHtml(valor): para interpolar datos (nombres, observaciones, comprobantes que
// escribe el usuario) en HTML armado con cadenas (.html(), .append(), innerHTML, plantillas
// `...${x}...`). Escapa también las comillas, así sirve dentro de atributos value='...'.
(function () {
    var SGV = window.SGV = window.SGV || {};
    var TABLA = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
    SGV.escaparHtml = function (valor) {
        if (valor === null || valor === undefined) return '';
        return String(valor).replace(/[&<>"']/g, function (c) { return TABLA[c]; });
    };
})();

// ── Sesión cerrada o cambio de contraseña pendiente en llamadas AJAX ──
// Global.asax responde 401 { sesionCerrada } cuando cambiaron los datos de acceso del usuario
// (desactivado, otro rol, otra contraseña) y 403 { requiereCambioContrasena } si aún debe cambiar
// la contraseña. Se corre después de los .fail() de cada página, así este aviso es el que queda.
(function () {
    if (!window.jQuery) return;
    function leer(xhr) {
        try { return JSON.parse(xhr.responseText) || {}; } catch (e) { return {}; }
    }
    $(document).ajaxError(function (evento, xhr) {
        if (!xhr || (xhr.status !== 401 && xhr.status !== 403)) return;
        var r = leer(xhr);
        var SGV = window.SGV || {};
        if (xhr.status === 401 && r.sesionCerrada) {
            if (SGV.avisar) SGV.avisar(r.Message, { tipo: 'danger' });
            setTimeout(function () {
                // codigoLogin: 'sesion_vencida' si llegó a su duración máxima (E4-12).
                window.location.href = (SGV.urlLogin || '/Views/Login.aspx') + '?error=' + (r.codigoLogin === 'sesion_vencida' ? 'sesion_vencida' : 'sesion_cerrada');
            }, 2500);
        } else if (xhr.status === 403 && r.requiereCambioContrasena) {
            if (SGV.avisar) SGV.avisar(r.Message, { tipo: 'warning' });
        }
    });
})();

// ── Tamaño de los archivos antes de enviarlos ───────────────────────
// <input type=file data-max-mb="10" data-max-envio-mb="24">: al elegir archivos se suman sus
// tamaños y, si pasan data-max-mb, se avisa y se vacía la selección; al enviar se revisa otra vez
// y además que todos los archivos del formulario juntos no pasen el data-max-envio-mb más bajo
// (el límite del servidor para la página). Solo ayuda al usuario: el servidor valida igual
// (ArchivoSubidoHelper) y un envío demasiado grande vuelve a la página con un aviso.
(function () {
    var MB = 1024 * 1024;
    function numero(el, atributo) {
        var v = parseFloat(el.getAttribute(atributo));
        return v > 0 ? v : 0;
    }
    function suma(input) {
        var total = 0, archivos = input.files || [];
        for (var i = 0; i < archivos.length; i++) total += archivos[i].size || 0;
        return total;
    }
    function enMb(bytes) {
        return (bytes / MB).toLocaleString('es-PE', { maximumFractionDigits: 1 }) + ' MB';
    }
    function avisar(texto) {
        var SGV = window.SGV || {};
        if (SGV.avisar) SGV.avisar(texto, { tipo: 'warning' });
    }
    function excede(input) {
        var max = numero(input, 'data-max-mb'), total = suma(input);
        if (!max || total <= max * MB) return null;
        var archivos = input.files || [];
        return archivos.length === 1
            ? 'El archivo ' + archivos[0].name + ' pesa ' + enMb(total) + '; el máximo es ' + max + ' MB.'
            : 'Los archivos elegidos suman ' + enMb(total) + '; el máximo es ' + max + ' MB. Súbalos por separado o más livianos.';
    }

    document.addEventListener('change', function (e) {
        var input = e.target;
        if (!input || input.tagName !== 'INPUT' || input.type !== 'file' || !input.hasAttribute('data-max-mb')) return;
        var mensaje = excede(input);
        if (!mensaje) return;
        avisar(mensaje);
        try { input.value = ''; } catch (err) { }
    }, true);

    // Postback parcial de un UpdatePanel: el PageRequestManager ya decidió (al hacer clic) que
    // el envío es asíncrono, y esos envíos no llevan archivos. Sin MS Ajax en la página: false.
    function esPostbackAsincrono() {
        try {
            var prm = window.Sys && Sys.WebForms && Sys.WebForms.PageRequestManager
                ? Sys.WebForms.PageRequestManager.getInstance() : null;
            return !!(prm && prm._postBackSettings && prm._postBackSettings.async);
        } catch (err) { return false; }
    }

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!form || !form.querySelectorAll || esPostbackAsincrono()) return;
        var inputs = form.querySelectorAll('input[type=file][data-max-mb]');
        var total = 0, maxEnvio = 0;
        for (var i = 0; i < inputs.length; i++) {
            var mensaje = excede(inputs[i]);
            if (mensaje) {
                e.preventDefault();
                avisar(mensaje);
                return;
            }
            var bytes = suma(inputs[i]);
            if (!bytes) continue;
            total += bytes;
            var envio = numero(inputs[i], 'data-max-envio-mb');
            if (envio && (!maxEnvio || envio < maxEnvio)) maxEnvio = envio;
        }
        if (maxEnvio && total > maxEnvio * MB) {
            e.preventDefault();
            avisar('Los archivos elegidos en la página suman ' + enMb(total) + '; el máximo por envío es ' + maxEnvio +
                ' MB. Súbalos por separado o más livianos.');
        }
    }, true);
})();

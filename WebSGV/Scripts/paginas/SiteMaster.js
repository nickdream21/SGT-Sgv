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
//     data-accion, los validadores, __doPostBack y el UpdatePanel como con el mouse);
//   - data-enter="" o fuera de toda zona, no hace nada (los formularios de alta se guardan
//     solo con su botón);
//   - los asp:Panel con DefaultButton (WebForms les genera un onkeypress con
//     WebForm_FireDefaultButton) siguen igual;
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

// ── Un solo envío a la vez: "Procesando…" (E5-2) ────────────────────
// Un doble clic en un botón de servidor mandaba dos postbacks (el servidor atendía los dos: dos
// búsquedas, y en un alta, dos registros). Mientras un envío está en curso la página queda
// "ocupada": body.sgv-procesando, aria-busy en el formulario (no en body: la región que anuncia
// "Procesando…" está fuera del formulario), cursor de espera, se anulan todos los clics (salvo los
// de los avisos de SGV.avisar, para poder cerrarlos) y el Enter, y a los 300 ms aparece el velo
// "Procesando…" (lo rápido no parpadea). No se deshabilita el botón: un submit deshabilitado no
// envía su nombre y el servidor perdería el evento.
// Cuándo se bloquea:
//   - submit del formulario que nadie canceló (validador que falla, archivo grande, monto ilegible
//     y el postback parcial que intercepta el PageRequestManager ya lo cancelaron: no bloquean);
//   - theForm.submit() (lo llaman __doPostBack, LinkButton, UseSubmitBehavior=false y AutoPostBack);
//   - postback parcial (UpdatePanel): beginRequest. Un segundo postback parcial mientras corre el
//     primero se cancela en initializeRequest (sin esto MS Ajax abortaba el primero y mandaba el otro).
// Cuándo se libera (lo primero que ocurra):
//   - endRequest del parcial (también con error o sin conexión: el aviso lo da SiteMaster-2.js);
//   - pageshow desde la caché (Atrás);
//   - data-espera="segundos" en el botón: ese tope (envíos largos: subidas, GPS, firma del conductor;
//     y Cerrar sesión, 2 s, por si el usuario cancela el "¿salir del sitio?");
//   - data-descarga en el botón (devuelve un archivo: la página no se recarga): 3 s;
//   - postback completo con archivos elegidos en el formulario: 300 s (executionTimeout de las
//     páginas de subida);
//   - cualquier otro caso: 20 s (descarga sin marcar, "¿salir del sitio?" cancelado: el navegador no
//     avisa a la página si el usuario se queda).
// data-procesando="Leyendo…" en un botón cambia su texto mientras dura el envío.
// SGV.envioEnCurso() dice si hay un envío en curso (lo usan los beforeunload de las páginas: un
// LinkButton envía con form.submit(), que no dispara el evento submit).
// No cubre fetch ni PageMethods ($.ajax): tienen su propio bloqueo y no pasan por aquí.
// SGV.avisar sigue mostrándose encima del velo; SGV.confirmar no se abre por clic durante el envío
// (los clics no llegan) y, si un script lo abre, queda detrás del velo hasta que se libere.
(function () {
    var SGV = window.SGV = window.SGV || {};
    var RETRASO_VELO = 300, ESPERA_DESCARGA = 3000, ESPERA_SUBIDA = 300000, ESPERA_SEGURIDAD = 20000;
    var TEXTO = 'Procesando…';
    var bloqueado = false, temporizador = null, temporizadorVelo = null, velo = null, estado = null;
    var ultimoElemento = null, ultimoMomento = 0;
    var textoCambiado = null;

    // El velo (visible, aria-hidden) y la región que lo anuncia (sr-only, role=status) están siempre en
    // el documento: una región aria-live que aparece junto con su texto no siempre se anuncia. El texto
    // de la región se pone a los 300 ms, con el velo.
    function crear() {
        if (velo || !document.body) return;
        velo = document.createElement('div');
        velo.id = 'sgvProcesando';
        velo.className = 'sgv-procesando-velo';
        velo.setAttribute('aria-hidden', 'true');
        velo.innerHTML = '<div class="sgv-procesando-caja"><span class="spinner-border spinner-border-sm"></span>' +
            '<span class="sgv-procesando-texto">' + TEXTO + '</span></div>';
        estado = document.createElement('div');
        estado.id = 'sgvProcesandoEstado';
        estado.className = 'sr-only';
        estado.setAttribute('role', 'status');
        estado.setAttribute('aria-live', 'polite');
        document.body.appendChild(velo);
        document.body.appendChild(estado);
    }
    if (document.body) crear();
    else document.addEventListener('DOMContentLoaded', crear);

    function mostrarVelo() {
        if (!bloqueado) return;
        crear();
        document.body.classList.add('sgv-procesando-velo-visible');
        estado.textContent = TEXTO;
    }

    // Elemento que pidió el envío: el submitter, el último clic (menos de 1,5 s) o el __EVENTTARGET.
    function origen(candidato, form) {
        if (candidato && candidato.nodeType === 1) return candidato;
        if (ultimoElemento && Date.now() - ultimoMomento < 1500) return ultimoElemento;
        var destino = form && form.__EVENTTARGET ? form.__EVENTTARGET.value : '';
        if (!destino) return null;
        return document.getElementsByName(destino)[0] || document.getElementById(destino.replace(/\$/g, '_'));
    }

    function hayArchivos(form) {
        var campos = form && form.querySelectorAll ? form.querySelectorAll('input[type=file]') : [];
        for (var i = 0; i < campos.length; i++) if (campos[i].files && campos[i].files.length) return true;
        return false;
    }

    function esperaDe(el, asincrono) {
        var marcado = el && el.closest ? el.closest('[data-descarga], [data-espera]') : null;
        var segundos = marcado ? parseFloat(marcado.getAttribute('data-espera')) : 0;
        if (segundos > 0) return segundos * 1000;
        if (marcado && marcado.hasAttribute('data-descarga')) return ESPERA_DESCARGA;
        // Un postback parcial no lleva archivos (MS Ajax no los envía).
        if (!asincrono && hayArchivos(document.forms[0])) return ESPERA_SUBIDA;
        return ESPERA_SEGURIDAD;
    }

    function cambiarTexto(el) {
        var boton = el && el.closest ? el.closest('[data-procesando]') : null;
        if (!boton) return;
        var nuevo = boton.getAttribute('data-procesando');
        if (boton.tagName === 'INPUT') {
            textoCambiado = { el: boton, valor: boton.value };
            boton.value = nuevo;
        } else {
            textoCambiado = { el: boton, html: boton.innerHTML };
            boton.textContent = nuevo;
        }
    }

    function restaurarTexto() {
        var t = textoCambiado;
        textoCambiado = null;
        if (!t) return;
        if ('valor' in t) t.el.value = t.valor;
        else t.el.innerHTML = t.html;
    }

    function bloquear(el, asincrono) {
        if (bloqueado || !document.body) return;
        bloqueado = true;
        crear();
        document.body.classList.add('sgv-procesando');
        if (document.forms[0]) document.forms[0].setAttribute('aria-busy', 'true');
        cambiarTexto(el);
        temporizadorVelo = setTimeout(mostrarVelo, RETRASO_VELO);
        temporizador = setTimeout(liberar, esperaDe(el, asincrono));
    }

    function liberar() {
        if (!bloqueado) return;
        bloqueado = false;
        clearTimeout(temporizador);
        clearTimeout(temporizadorVelo);
        document.body.classList.remove('sgv-procesando', 'sgv-procesando-velo-visible');
        if (document.forms[0]) document.forms[0].removeAttribute('aria-busy');
        if (estado) estado.textContent = '';
        restaurarTexto();
    }

    SGV.envioEnCurso = function () { return bloqueado; };

    // Va antes que todos los demás listeners de clic (window, captura, registrado primero): durante el
    // envío ningún clic llega a data-confirmar, data-accion, los montos, WebForms ni la página. Los
    // avisos (#sgvAvisos, encima del velo) sí se pueden cerrar.
    function anular(e) {
        e.preventDefault();
        e.stopImmediatePropagation();
    }
    function enAvisos(e) {
        return !!(e.target && e.target.closest && e.target.closest('#sgvAvisos'));
    }
    window.addEventListener('click', function (e) {
        if (bloqueado) { if (!enAvisos(e)) anular(e); return; }
        ultimoElemento = e.target && e.target.closest ? e.target.closest('input, button, a, [data-descarga]') : null;
        ultimoMomento = Date.now();
    }, true);
    window.addEventListener('keydown', function (e) {
        if (bloqueado && e.key === 'Enter' && !enAvisos(e)) anular(e);
    }, true);
    // Un requestSubmit() de un script durante el envío.
    window.addEventListener('submit', function (e) {
        if (bloqueado) anular(e);
    }, true);

    // Burbuja: corre después de los validadores (onsubmit), del PageRequestManager y de los avisos
    // de tamaño y montos, que cancelan en captura o en el propio formulario.
    document.addEventListener('submit', function (e) {
        if (e.defaultPrevented) return;
        bloquear(origen(e.submitter, e.target), false);
        // Un listener de la página registrado después de este que cancele el envío: se libera.
        setTimeout(function () { if (e.defaultPrevented) liberar(); }, 0);
    });

    // theForm.submit() no dispara el evento submit.
    var form = document.forms[0];
    if (form && typeof form.submit === 'function') {
        var enviar = form.submit;
        form.submit = function () {
            if (bloqueado) return;   // segundo envío mientras corre el primero
            bloquear(origen(null, form), false);
            try { return enviar.apply(form, arguments); }
            catch (err) { liberar(); throw err; }
        };
    }

    if (window.Sys && Sys.WebForms && Sys.WebForms.PageRequestManager) {
        var prm = Sys.WebForms.PageRequestManager.getInstance();
        prm.add_initializeRequest(function (sender, args) {
            if (prm.get_isInAsyncPostBack()) args.set_cancel(true);
        });
        prm.add_beginRequest(function (sender, args) {
            bloquear(args && args.get_postBackElement ? args.get_postBackElement() : null, true);
        });
        prm.add_endRequest(function () {
            if (!prm.get_isInAsyncPostBack()) liberar();
        });
    }

    window.addEventListener('pageshow', function (e) { if (e.persisted) liberar(); });
})();

// ── Avisos no bloqueantes ───────────────────────────────────────────
// Reemplazo de alert(): un alert() detiene la página hasta que se cierra (y bloquea la
// automatización de pruebas). SGV.avisar(mensaje) muestra el aviso arriba a la derecha; se
// cierra solo a los 10 s (pausa con el cursor o el foco encima), salvo los errores (danger), que
// esperan a que el usuario los cierre. Desde el servidor: AvisoHelper.Avisar. El tipo sale de opciones.tipo
// (success, danger, warning, info); sin él, un mensaje que empieza con "Error" es error y lo demás advertencia.
// Con { trasRecargar: true } el aviso se guarda y aparece después de un location.reload().
(function () {
    var SGV = window.SGV = window.SGV || {};
    var CLAVE = 'sgvAvisoPendiente';

    function tipoDe(mensaje) {
        if (/^\s*Error/i.test(mensaje)) return 'danger';
        return 'warning';   // validaciones de formulario
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
        // E5-14: un error no se cierra solo (se cierra con la X). Los demás duran 10 s y el tiempo se
        // detiene mientras el cursor o el foco están encima; al salir corre lo que faltaba.
        if (tipo !== 'danger') cerrarSolo(aviso, 10000);
    }

    function cerrarSolo(aviso, ms) {
        var restante = ms, inicio = 0, reloj = null, cursor = false, foco = false;
        function correr() {
            if (reloj || cursor || foco) return;
            inicio = Date.now();
            reloj = setTimeout(function () { aviso.remove(); }, restante);
        }
        function pausar() {
            if (!reloj) return;
            clearTimeout(reloj);
            reloj = null;
            restante = Math.max(1000, restante - (Date.now() - inicio));
        }
        aviso.addEventListener('mouseenter', function () { cursor = true; pausar(); });
        aviso.addEventListener('mouseleave', function () { cursor = false; correr(); });
        aviso.addEventListener('focusin', function () { foco = true; pausar(); });
        aviso.addEventListener('focusout', function (e) {
            if (e.relatedTarget && aviso.contains(e.relatedTarget)) return;
            foco = false; correr();
        });
        correr();
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

// ── Filtros plegables (details.sgv-filtros, E5-14) ─────────────────
// El rótulo dice cuántos filtros tienen valor ("Filtros (2)") en <span class="sgv-filtros-cuenta">.
// En el celular (≤ 767.98 px), tras enviar con un botón de los filtros (submit o data-filtros-buscar;
// cuenta el envío real, no el clic), al volver la página (postback completo o parcial) los filtros quedan plegados
// para que el primer resultado se vea sin desplazarse. Un campo con data-filtro-ignorar no cuenta.
(function () {
    var CLAVE = 'sgvFiltrosPlegar';
    var MOVIL = '(max-width: 767.98px)';

    function cuenta(d) {
        var n = 0;
        d.querySelectorAll('input, select, textarea').forEach(function (c) {
            if (c.disabled || c.hasAttribute('data-filtro-ignorar')) return;
            var tipo = (c.type || '').toLowerCase();
            if (tipo === 'hidden' || tipo === 'submit' || tipo === 'button' || tipo === 'reset' || tipo === 'image') return;
            if (tipo === 'checkbox' || tipo === 'radio') { if (c.checked && c.value !== '') n++; return; }
            if (c.tagName === 'SELECT') { if (c.selectedIndex > 0 && c.value !== '') n++; return; }
            if (String(c.value || '').trim() !== '') n++;
        });
        return n;
    }

    function rotular(d) {
        var r = d.querySelector('.sgv-filtros-cuenta');
        if (!r) return;
        var n = cuenta(d);
        r.textContent = n > 0 ? '(' + n + ')' : '';
    }

    function preparar() {
        var plegar = false;
        try { plegar = sessionStorage.getItem(CLAVE) === '1'; sessionStorage.removeItem(CLAVE); } catch (e) { }
        var movil = window.matchMedia && window.matchMedia(MOVIL).matches;
        document.querySelectorAll('details.sgv-filtros').forEach(function (d) {
            rotular(d);
            if (plegar && movil) d.open = false;
        });
    }

    var BUSCAR = 'details.sgv-filtros [data-filtros-buscar], details.sgv-filtros input[type=submit], details.sgv-filtros button[type=submit]';
    function esBuscar(el) { return !!(el && el.closest && el.closest(BUSCAR)); }
    function marcar() { try { sessionStorage.setItem(CLAVE, '1'); } catch (err) { } }

    // La marca se guarda solo cuando el envío sale de verdad: un clic que cancelan los validadores o
    // SGV.confirmar no envía el formulario ni dispara beginRequest. El clic solo recuerda el botón para los
    // navegadores sin e.submitter.
    var pulsado = null;
    document.addEventListener('click', function (e) {
        var b = e.target.closest && e.target.closest(BUSCAR);
        pulsado = b || null;
        if (b) setTimeout(function () { if (pulsado === b) pulsado = null; }, 0);
    }, true);

    // Postback completo. En uno parcial el PageRequestManager cancela el submit (defaultPrevented) y avisa en beginRequest.
    document.addEventListener('submit', function (e) {
        if (e.defaultPrevented) return;
        if (esBuscar(e.submitter || pulsado)) marcar();
    });

    ['input', 'change'].forEach(function (ev) {
        document.addEventListener(ev, function (e) {
            var d = e.target.closest && e.target.closest('details.sgv-filtros');
            if (d) rotular(d);
        });
    });

    // E5-17: details.sgv-filtros y details.sgv-plegable con data-plegar-movil arrancan plegados en el celular
    // (se cierran al cargar la página; no tras un parcial, para no cerrar lo que el usuario abrió). El marcado
    // los deja con open, así en escritorio y sin JS se ven abiertos.
    function plegarAlCargar() {
        if (!(window.matchMedia && window.matchMedia(MOVIL).matches)) return;
        document.querySelectorAll('details.sgv-filtros[data-plegar-movil], details.sgv-plegable[data-plegar-movil]')
            .forEach(function (d) { d.open = false; });
    }

    // SGV.plegarFiltros(el): para las búsquedas por fetch/AJAX (sin postback). Pliega en el celular los
    // filtros que contienen a el (o el mismo details) y actualiza su "(n)".
    var SGV = window.SGV = window.SGV || {};
    SGV.plegarFiltros = function (el) {
        var d = el && el.closest ? el.closest('details.sgv-filtros') : null;
        if (!d) return;
        rotular(d);
        if (window.matchMedia && window.matchMedia(MOVIL).matches) d.open = false;
    };

    function alCargar() { plegarAlCargar(); preparar(); }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', alCargar);
    else alCargar();
    if (window.Sys && Sys.WebForms && Sys.WebForms.PageRequestManager) {
        var prm = Sys.WebForms.PageRequestManager.getInstance();
        prm.add_beginRequest(function (s, a) { if (esBuscar(a.get_postBackElement())) marcar(); });
        prm.add_endRequest(preparar);
    }
})();

// ── Cargando (E5-17) ───────────────────────────────────────────────
// SGV.htmlCargando(texto): el bloque .sgv-cargando de sgv-base.css (giratorio y texto, role="status") para
// pintarlo con .html() mientras llega una respuesta. El texto se escapa.
(function () {
    var SGV = window.SGV = window.SGV || {};
    SGV.htmlCargando = function (texto) {
        var t = String(texto === null || texto === undefined || texto === '' ? 'Cargando…' : texto)
            .replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; });
        return '<div class="sgv-cargando" role="status">' + t + '</div>';
    };
})();

// ── Confirmaciones no bloqueantes ───────────────────────────────────
// Reemplazo del confirm nativo: el cuadro detiene la página y no se puede estilar.
//   SGV.confirmar(mensaje[, alAceptar][, { titulo, textoAceptar, peligro }]) → Promise<bool>
// "peligro" pinta el botón en rojo (eliminar, rechazar, revertir) y deja el foco en Cancelar.
// El mensaje respeta los saltos de línea; Esc, la X o Cancelar responden false.
// Botones de servidor: data-confirmar="texto" (\n = salto de línea), y opcionales
// data-confirmar-titulo, data-confirmar-aceptar y data-confirmar-peligro="true". Se cancela el
// clic, se pregunta y, al aceptar, se repite el mismo clic: su onclick (validación, postback) y el
// UpdatePanel siguen igual. Úsalo en vez de un data-accion que pregunte con el confirm nativo.
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
        // Botón con grupo de validación (data-validar-grupo="grupo", E5-20b): valida antes de preguntar; si
        // falta un dato no se confirma algo que luego no se envía (los mensajes quedan junto a los campos).
        // Page_ClientValidate deja Page_BlockSubmit = true para el submit que lo consume (WebForm_OnSubmit); aquí el
        // clic se cancela y no hay submit, así que se repone: si no, el siguiente envío de la página se perdería.
        var grupoValidar = el.getAttribute('data-validar-grupo');
        if (grupoValidar !== null && typeof window.Page_ClientValidate === 'function' && !window.Page_ClientValidate(grupoValidar)) {
            window.Page_BlockSubmit = false;
            return;
        }
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

// ── Acciones delegadas (sin onclick= ni otros on*= en el marcado) ──
// Prepara la política de contenido (CSP) sin 'unsafe-inline': el marcado dice qué hacer y el
// script de la página dice cómo, en vez de llevar JavaScript dentro de los atributos.
//   <button type="button" data-accion="verDetalle" data-id="5">       → al hacer clic
//   <input data-accion-input="filtrar" data-contenedor="tabla">       → al escribir (input)
//   <select data-accion-change="mostrarCampo">                        → al cambiar (change)
//   SGV.acciones = Object.assign(SGV.acciones || {}, { verDetalle: function (el, e) { ... } });
// El manejador recibe el elemento que lleva el atributo (el antiguo "this") y el evento. Los
// scripts de página pueden registrarse antes o después de este archivo: se buscan al disparar.
// Clic: se atiende en captura desde document, después de data-confirmar (registrado arriba) y
// antes del onclick que WebForms genera en sus botones (validación, __doPostBack). Si el manejador
// devuelve false se cancela el clic y ese onclick ya no corre, como el antiguo
// "return x();" de un OnClientClick. Diferencia con aquel "return false": aquí además se detiene la
// propagación, así que los listeners de document en burbuja (los de clic de otras páginas o
// bibliotecas) ya no reciben ese clic; hace falta para que no corra el onclick de WebForms.
// Al ser delegado sirve para filas que llegan por UpdatePanel o se arman en JS.
(function () {
    var SGV = window.SGV = window.SGV || {};
    SGV.acciones = SGV.acciones || {};

    function despachar(e, atributo) {
        var el = e.target && e.target.closest ? e.target.closest('[' + atributo + ']') : null;
        if (!el || el.disabled) return;
        var nombre = el.getAttribute(atributo);
        var fn = SGV.acciones[nombre];
        if (typeof fn !== 'function') {
            if (window.console) console.warn('SGV: la acción "' + nombre + '" no tiene manejador.');
            return;
        }
        if (fn.call(el, el, e) === false) {
            e.preventDefault();
            e.stopPropagation();
        }
    }

    document.addEventListener('click', function (e) { despachar(e, 'data-accion'); }, true);
    document.addEventListener('input', function (e) { despachar(e, 'data-accion-input'); });
    document.addEventListener('change', function (e) { despachar(e, 'data-accion-change'); });

    // Compartidas por varias páginas.
    Object.assign(SGV.acciones, {
        // X de un mensaje del servidor: oculta el bloque .mb-3 que lo envuelve.
        cerrarBloqueMensaje: function (el) {
            var bloque = el.closest('.mb-3');
            if (bloque) bloque.style.display = 'none';
        },
        recargarPagina: function () { location.reload(); },
        // Botón de una fila de grilla sin ViewState (E5-16, catálogos): copia data-argumento al campo
        // oculto de la página (SGV.hfArgumentoFila) y pulsa su botón oculto (SGV.btnAccionFila), que hace
        // el postback. Con data-confirmar en el mismo botón, la pregunta va antes (su listener se registra
        // arriba) y esto solo corre si el usuario acepta. El servidor revalida el argumento.
        enviarFila: function (el) {
            var campo = SGV.hfArgumentoFila && document.getElementById(SGV.hfArgumentoFila);
            var boton = SGV.btnAccionFila && document.getElementById(SGV.btnAccionFila);
            if (!campo || !boton) return false;
            campo.value = el.getAttribute('data-argumento') || '';
            boton.click();
            return false;
        }
    });
})();

// ── Validación: marca en el campo y foco al primer error (E5-20) ───────
// Forma común de los formularios: el asterisco es <span class="sgv-obligatorio" aria-hidden="true">*</span> y el
// control lleva aria-required; cada validador de WebForms lleva la frase completa en ErrorMessage (sin Text),
// Display="Dynamic", CssClass="sgv-campo-error", SetFocusOnError y su ValidationGroup. No hay ValidationSummary:
// el mensaje va una sola vez, debajo del campo.
// Aquí hay UN envoltorio de ValidatorUpdateDisplay (WebUIValidation.js; ninguna página lo envuelve aparte):
//   - pone is-invalid y aria-invalid en el control si ALGÚN validador activo suyo falla (no solo el último
//     que se evaluó: un campo con obligatorio y formato no se "limpia" porque el segundo pase);
//   - añade el id del mensaje a aria-describedby mientras falla y lo quita cuando pasa;
//   - quita el display:inline que Display="Dynamic" pone al mostrar el mensaje (vale el display:block de
//     .sgv-campo-error, sgv-base.css).
// Page_ClientValidate se envuelve para llevar la vista y el foco al primer campo inválido del grupo, sin
// subir la página (en el celular el formulario de un catálogo va debajo de la lista, E5-16a). Si la validación
// cancela el envío, el submit queda cancelado (WebForm_OnSubmit devuelve false) y el bloqueo de doble envío
// (E5-2) no se activa.
// SGV.marcarError(campo, mensaje) / SGV.limpiarError(campo): el mismo aspecto para lo que valida un script
// (un small.sgv-campo-error tras el campo o su .input-group). Al corregir un campo marcado, su marca se va (alCorregir).
// Al cerrarse un modal, sus mensajes se borran (el próximo que se abra empieza limpio); si el servidor lo
// reabre con errores (B5-7), sus validadores ya vienen marcados.
(function () {
    var SGV = window.SGV = window.SGV || {};
    var contador = 0;

    function porId(campo) { return typeof campo === 'string' ? document.getElementById(campo) : campo; }

    function describir(el, id, incluir) {
        var ids = (el.getAttribute('aria-describedby') || '').split(/\s+/).filter(function (x) { return x && x !== id; });
        if (incluir) ids.push(id);
        if (ids.length) el.setAttribute('aria-describedby', ids.join(' '));
        else el.removeAttribute('aria-describedby');
    }

    function fallaAlgunValidador(el) {
        var lista = window.Page_Validators || [];
        for (var i = 0; i < lista.length; i++) {
            var v = lista[i];
            if (v && v.controltovalidate === el.id && v.enabled !== false && v.isvalid === false) return true;
        }
        return false;
    }

    // Un monto ilegible (data-monto-invalido, más abajo) o un error de script (data-sgv-error) también cuentan.
    function pintar(el) {
        var invalido = fallaAlgunValidador(el) || el.hasAttribute('data-sgv-error') || el.hasAttribute('data-monto-invalido');
        el.classList.toggle('is-invalid', invalido);
        if (invalido) el.setAttribute('aria-invalid', 'true');
        else el.removeAttribute('aria-invalid');
    }

    function alActualizar(val) {
        if (!val || !val.style) return;
        var falla = val.isvalid === false && val.enabled !== false;
        if (falla && val.style.display === 'inline' && val.classList && val.classList.contains('sgv-campo-error')) val.style.display = '';
        var el = val.controltovalidate ? document.getElementById(val.controltovalidate) : null;
        if (!el) return;
        describir(el, val.id, falla);
        // Ya marcado una vez: se sigue revalidando al escribir (alCorregir), también si se vuelve a vaciar.
        if (falla) el.setAttribute('data-sgv-marcado', '');
        pintar(el);
    }

    // Quita data-sgv-marcado de los campos de los validadores de un grupo (o de un contenedor).
    function desmarcar(grupo, contenedor) {
        var lista = window.Page_Validators || [];
        for (var i = 0; i < lista.length; i++) {
            var v = lista[i];
            if (!v || !v.controltovalidate) continue;
            if (contenedor ? !contenedor.contains(v) :
                (typeof window.IsValidationGroupMatch === 'function' && !window.IsValidationGroupMatch(v, grupo))) continue;
            var el = document.getElementById(v.controltovalidate);
            if (el) el.removeAttribute('data-sgv-marcado');
        }
    }

    // El foco al primer error solo al enviar (dentro de Page_ClientValidate). WebUIValidation.js también lo
    // movía en la validación por change (SetFocusOnError): escribir mal la placa y pasar a Marca devolvía el
    // foco a Placa y lo que se tecleaba caía ahí. El foco tras un postback con error lo pone el servidor
    // (WebForm_AutoFocus), no ValidatorSetFocus.
    var enviando = 0;

    function aLaVista(el) {
        var r = el.getBoundingClientRect();
        return r.top >= 0 && r.bottom <= (window.innerHeight || document.documentElement.clientHeight);
    }

    function llevarAlError(grupo) {
        var lista = window.Page_Validators || [];
        var campo = null;
        for (var i = 0; i < lista.length && !campo; i++) {
            var v = lista[i];
            if (!v || v.isvalid !== false || v.enabled === false) continue;
            if (typeof window.IsValidationGroupMatch === 'function' && !window.IsValidationGroupMatch(v, grupo)) continue;
            campo = v.controltovalidate ? document.getElementById(v.controltovalidate) : null;
        }
        if (!campo) return;
        // Un select con select2 está oculto: la vista y el foco van a su caja.
        var caja = campo.classList.contains('select2-hidden-accessible') && campo.nextElementSibling;
        var destino = caja && caja.classList.contains('select2-container') ? caja : campo;
        if (!aLaVista(destino)) destino.scrollIntoView({ block: 'center' });
        var enfocable = destino === campo ? campo : (destino.querySelector('.select2-selection') || campo);
        if (typeof enfocable.focus === 'function') {
            try { enfocable.focus({ preventScroll: true }); } catch (e) { enfocable.focus(); }
        }
    }

    function envolver() {
        var mostrar = window.ValidatorUpdateDisplay;
        if (typeof mostrar === 'function' && !mostrar.sgvEnvuelto) {
            var mostrarEnvuelto = function (val) {
                var r = mostrar.apply(this, arguments);
                alActualizar(val);
                return r;
            };
            mostrarEnvuelto.sgvEnvuelto = true;
            window.ValidatorUpdateDisplay = mostrarEnvuelto;
        }
        var enfocar = window.ValidatorSetFocus;
        if (typeof enfocar === 'function' && !enfocar.sgvEnvuelto) {
            var enfocarEnvuelto = function () {
                if (enviando > 0) return enfocar.apply(this, arguments);
            };
            enfocarEnvuelto.sgvEnvuelto = true;
            window.ValidatorSetFocus = enfocarEnvuelto;
        }
        var validar = window.Page_ClientValidate;
        if (typeof validar === 'function' && !validar.sgvEnvuelto) {
            var validarEnvuelto = function (grupo) {
                var valido;
                enviando++;
                try { valido = validar.apply(this, arguments); }
                finally { enviando--; }
                if (!valido) llevarAlError(grupo);
                else desmarcar(grupo);
                return valido;
            };
            validarEnvuelto.sgvEnvuelto = true;
            window.Page_ClientValidate = validarEnvuelto;
        }
        // Validadores que el servidor devolvió en error (Page.IsValid falso): su campo también se marca.
        var lista = window.Page_Validators || [];
        for (var i = 0; i < lista.length; i++) if (lista[i] && lista[i].isvalid === false) alActualizar(lista[i]);
        // Campos que el servidor marca is-invalid sin validador (RegistroDespacho): también aria-invalid.
        var marcadosServidor = document.querySelectorAll('.is-invalid:not([aria-invalid])');
        for (var j = 0; j < marcadosServidor.length; j++) marcadosServidor[j].setAttribute('aria-invalid', 'true');
    }

    SGV.marcarError = function (campo, mensaje) {
        var el = porId(campo);
        if (!el || !el.parentNode) return null;
        var id = el.getAttribute('data-sgv-error');
        var ayuda = id ? document.getElementById(id) : null;
        if (!ayuda) {
            var ancla = (el.closest && el.closest('.input-group')) || el;
            ayuda = document.createElement('small');
            ayuda.id = 'sgvCampoError' + (++contador);
            ayuda.className = 'sgv-campo-error';
            ancla.parentNode.insertBefore(ayuda, ancla.nextSibling);
        }
        ayuda.textContent = mensaje;
        el.setAttribute('data-sgv-error', ayuda.id);
        describir(el, ayuda.id, true);
        pintar(el);
        return ayuda;
    };

    SGV.limpiarError = function (campo) {
        var el = porId(campo);
        if (!el) return;
        var id = el.getAttribute('data-sgv-error');
        if (id) {
            var ayuda = document.getElementById(id);
            if (ayuda && ayuda.parentNode) ayuda.parentNode.removeChild(ayuda);
            el.removeAttribute('data-sgv-error');
            describir(el, id, false);
        }
        pintar(el);
    };

    function validadoresDe(el) {
        var lista = window.Page_Validators || [], propios = [];
        for (var i = 0; i < lista.length; i++) if (lista[i] && lista[i].controltovalidate === el.id) propios.push(lista[i]);
        return propios;
    }

    // Al corregir un campo ya marcado:
    //   - error de script (data-sgv-error): se va al escribir;
    //   - marcado por sus validadores: al escribir se vuelven a evaluar, y el mensaje se va en cuanto el
    //     valor es válido (sin esperar a salir del campo). Un campo aún sin marcar no se valida mientras se
    //     escribe por primera vez (el error aparece al salir o al enviar, como en WebForms);
    //   - marcado por el servidor sin validador (RegistroDespacho: conductor, tracto, planta... al leer
    //     documentos; antes RegistroDespacho-2.js): se desmarca al tener valor, también al elegir en un select2.
    // Va por jQuery: select2 avisa el cambio con .trigger('change'), que no llega a addEventListener.
    function alCorregir(e) {
        var el = e.target;
        if (!el || !el.classList || !el.hasAttribute) return;
        if (el.hasAttribute('data-sgv-error')) {
            if (e.type === 'input') SGV.limpiarError(el);
            return;
        }
        if (el.hasAttribute('data-monto-invalido')) return;
        var propios = validadoresDe(el);
        // Con validadores: se revalida mientras está marcado o ya lo estuvo (data-sgv-marcado).
        if (!el.classList.contains('is-invalid') && !(propios.length && el.hasAttribute('data-sgv-marcado'))) return;
        if (propios.length) {
            // En change ya revalida WebUIValidation (ValidatorOnChange).
            if (e.type !== 'input' || typeof window.ValidatorValidate !== 'function') return;
            for (var i = 0; i < propios.length; i++) window.ValidatorValidate(propios[i]);
            if (typeof window.ValidatorUpdateIsValid === 'function') window.ValidatorUpdateIsValid();
            pintar(el);
            return;
        }
        if (String(el.value || '').trim() !== '') {
            el.classList.remove('is-invalid');
            el.removeAttribute('aria-invalid');
        }
    }
    if (window.jQuery) window.jQuery(document).on('input change', alCorregir);
    else {
        document.addEventListener('input', alCorregir);
        document.addEventListener('change', alCorregir);
    }

    if (window.jQuery) {
        window.jQuery(document).on('hidden.bs.modal', '.modal', function () {
            var modal = this;
            var lista = window.Page_Validators || [];
            for (var i = 0; i < lista.length; i++) {
                var v = lista[i];
                if (!v || v.isvalid !== false || !modal.contains(v)) continue;
                v.isvalid = true;
                if (typeof window.ValidatorUpdateDisplay === 'function') window.ValidatorUpdateDisplay(v);
            }
            desmarcar(null, modal);
            var marcados = modal.querySelectorAll('[data-sgv-error]');
            for (var j = 0; j < marcados.length; j++) SGV.limpiarError(marcados[j]);
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', envolver);
    else envolver();
    window.addEventListener('load', envolver);
    if (window.Sys && Sys.WebForms && Sys.WebForms.PageRequestManager) {
        Sys.WebForms.PageRequestManager.getInstance().add_endRequest(envolver);
    }
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

// ── Nombre accesible de un Select2 ──────────────────────────────────
// Select2 oculta el <select> y muestra un combobox nombrado solo por la opción elegida: el lector
// decía "Seleccione…" sin saber de qué campo. SGV.nombrarSelect2(select) toma el aria-label del
// <select> (o el texto de su <label for>) y lo antepone: "Pedido a descargar, 4500123". Llamarlo
// tras cada .select2(...).
(function () {
    var SGV = window.SGV = window.SGV || {};
    SGV.nombrarSelect2 = function (select) {
        if (!select || !select.id || !window.jQuery) return;
        var etiqueta = document.querySelector('label[for="' + select.id + '"]');
        var nombre = (select.getAttribute('aria-label') || (etiqueta ? etiqueta.textContent : '')).replace(/\s+/g, ' ').trim();
        if (!nombre) return;
        var $cont = jQuery(select).next('.select2');
        var combo = $cont.find('.select2-selection')[0], visto = $cont.find('.select2-selection__rendered')[0];
        if (!combo || !visto || !visto.id) return;
        var idNombre = select.id + '_nombreSelect2';
        var span = document.getElementById(idNombre);
        if (!span) {
            span = document.createElement('span');
            span.id = idNombre;
            span.className = 'sr-only';
            select.parentNode.insertBefore(span, select);
        }
        span.textContent = nombre;
        var ids = idNombre + ' ' + visto.id;
        combo.setAttribute('aria-labelledby', ids);
        visto.setAttribute('aria-labelledby', ids);
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

// ── Montos con coma o punto (E5-1) ──────────────────────────────────
// Los campos de dinero son de texto (type=text, inputmode=decimal, data-monto; no type=number: en un
// navegador es-PE "12,50" llegaba como 1250). SGV.leerMonto aplica la misma regla que
// MontoHelper.LeerMonto en el servidor: signo opcional al inicio, dígitos y como máximo un separador
// decimal (coma o punto) con 0 a 2 decimales; sin separador de miles, sin exponente ni espacios
// internos. Vacío = 0; lo que no cumple la regla = null. "1,250" y "1.250" (¿miles o 3 decimales?)
// no se leen. Al salir de un [data-monto] se escribe con punto ("12,5" → "12.5") o, si no se puede
// leer, se marca (is-invalid, aria-invalid y un solo mensaje por campo, ver marcar). El servidor
// decide igual: esto solo evita sorpresas.
//
// Envío con un monto ilegible: al pulsar algo que envía (submit, input image, enlace __doPostBack) se
// normalizan los montos y, si en el ámbito del botón queda uno visible que no se lee, se cancela el
// clic, se enfoca el campo y se avisa. Ámbito = el contenedor más cercano del botón entre
// .modal, fieldset, .card y [data-montos-grupo]; sin ninguno, el formulario. Un monto que está dentro
// de un [data-montos-grupo] solo cuenta para los botones de ese mismo grupo (páginas con una sola
// tarjeta grande, como ListaDespachos). Así un "Buscar" de otra tarjeta o grupo no queda bloqueado. No se revisan los botones con data-dismiss, .close, formnovalidate o
// data-montos-libre, ni los de Cancelar/Cerrar/Volver/Limpiar (un botón nuevo que no use montos debe llevar
// data-montos-libre="true": no confiar en su texto). Se escucha en window (captura): corre
// antes que data-confirmar (no se abre la confirmación), data-accion, los validadores y __doPostBack.
(function () {
    var SGV = window.SGV = window.SGV || {};
    var REGLA = /^[+-]?(?:[0-9]+(?:[.,][0-9]{0,2})?|[.,][0-9]{1,2})$/;
    var contador = 0;

    SGV.mensajeFormatoMonto = 'Escriba el monto sin separador de miles, con coma o punto para los decimales (por ejemplo 1250,50).';

    SGV.leerMonto = function (texto) {
        var t = texto === null || texto === undefined ? '' : String(texto).trim();
        if (!t) return 0;
        if (!REGLA.test(t)) return null;
        return parseFloat(t.replace(',', '.'));
    };

    // Texto ya válido escrito con punto: "12,5" → "12.5", ",5" → "0.5", "12," → "12". No rellena ceros.
    function conPunto(t) {
        t = t.replace(',', '.').replace(/^([+-]?)\./, function (m, signo) { return signo + '0.'; });
        return t.replace(/\.$/, '');
    }

    // ── Validadores de WebForms sobre el campo (RangeValidator, CompareValidator, ...) ──
    // Un campo con validador propio muestra UN mensaje: el del validador. Si el texto no se puede leer,
    // el validador da el de formato (en vez de "Ingrese un monto válido (mayor a 0)") y frena el envío
    // como siempre; si se lee, da su propio mensaje (rango). La ayuda de abajo no se agrega.
    function validadoresDe(el) {
        var lista = window.Page_Validators || [];
        var propios = [];
        for (var i = 0; i < lista.length; i++) if (lista[i] && lista[i].controltovalidate === el.id) propios.push(lista[i]);
        return propios;
    }

    function adaptarValidador(v) {
        if (v.sgvMonto || typeof v.evaluationfunction !== 'function') return;
        // RequiredFieldValidator: vacío es "obligatorio", no un formato malo; se deja como está.
        if (/RequiredField/.test(String(v.evaluationfunction.name || v.evaluationfunction))) { v.sgvMonto = true; return; }
        var original = v.evaluationfunction;
        var texto = v.innerHTML, mensaje = v.errormessage;
        v.sgvMonto = true;
        v.sgvMontoFormato = true;
        v.evaluationfunction = function (val) {
            var campo = document.getElementById(val.controltovalidate);
            var ilegible = campo && SGV.leerMonto(campo.value) === null;
            if (ilegible) val.textContent = SGV.mensajeFormatoMonto;
            else val.innerHTML = texto;
            val.errormessage = ilegible ? SGV.mensajeFormatoMonto : mensaje;
            return ilegible ? false : original(val);
        };
    }

    function prepararValidadores(el) {
        var propios = validadoresDe(el);
        for (var i = 0; i < propios.length; i++) adaptarValidador(propios[i]);
        return propios;
    }

    // ── Marca y ayuda ──
    function anclaDe(el) { return el.closest('.input-group, .dc-montos') || el; }

    function quitarMarca(el) {
        if (!el.hasAttribute('data-monto-invalido')) return;
        el.removeAttribute('data-monto-invalido');
        el.classList.remove('is-invalid');
        el.removeAttribute('aria-invalid');
        var id = el.getAttribute('data-monto-ayuda');
        el.removeAttribute('data-monto-ayuda');
        var descrita = (el.getAttribute('aria-describedby') || '').split(/\s+/).filter(function (x) { return x && x !== id; });
        if (descrita.length) el.setAttribute('aria-describedby', descrita.join(' '));
        else el.removeAttribute('aria-describedby');
        // La ayuda puede ser compartida (S/ y $ del conductor): se va cuando ya no la usa nadie.
        var ayuda = id ? document.getElementById(id) : null;
        if (ayuda && !document.querySelector('[data-monto-ayuda="' + id + '"]')) ayuda.parentNode.removeChild(ayuda);
    }

    function marcar(el) {
        el.setAttribute('data-monto-invalido', '');
        el.classList.add('is-invalid');
        el.setAttribute('aria-invalid', 'true');
        // Con validador propio, el mensaje lo da el validador (un solo mensaje por campo).
        if (validadoresDe(el).some(function (v) { return v.sgvMontoFormato && v.enabled !== false; })) return;
        if (el.getAttribute('data-monto-ayuda') && document.getElementById(el.getAttribute('data-monto-ayuda'))) return;
        // Debajo del grupo (input-group de Bootstrap, montos del conductor), no dentro de él; si el grupo
        // ya tiene la ayuda (otro campo del mismo grupo), se reutiliza.
        var ancla = anclaDe(el);
        var ayuda = ancla.nextElementSibling;
        if (!ayuda || !ayuda.classList.contains('sgv-monto-ayuda')) {
            ayuda = document.createElement('small');
            ayuda.id = 'sgvMontoAyuda' + (++contador);
            ayuda.className = 'sgv-monto-ayuda';
            ayuda.textContent = SGV.mensajeFormatoMonto;
            ancla.parentNode.insertBefore(ayuda, ancla.nextSibling);
        }
        el.setAttribute('data-monto-ayuda', ayuda.id);
        el.setAttribute('aria-describedby', ((el.getAttribute('aria-describedby') || '') + ' ' + ayuda.id).trim());
    }

    // Normaliza un campo y devuelve su monto (0 si está vacío) o null si no se puede leer.
    SGV.normalizarMonto = function (el) {
        var t = (el.value || '').trim();
        var n = SGV.leerMonto(t);
        var propios = prepararValidadores(el);
        if (n === null) {
            marcar(el);
            // El validador muestra ya el mensaje de formato (sin esperar al clic en Guardar).
            if (window.ValidatorValidate) for (var i = 0; i < propios.length; i++) window.ValidatorValidate(propios[i]);
            return null;
        }
        quitarMarca(el);
        if (t && conPunto(t) !== el.value) el.value = conPunto(t);
        return n;
    };

    // Normaliza todos los [data-monto] del contenedor (o de la página); devuelve el primero que no se lee.
    SGV.normalizarMontos = function (contenedor) {
        var campos = (contenedor && contenedor.querySelectorAll ? contenedor : document).querySelectorAll('[data-monto]');
        var primero = null;
        for (var i = 0; i < campos.length; i++) {
            if (SGV.normalizarMonto(campos[i]) === null && !primero) primero = campos[i];
        }
        return primero;
    };

    function esMonto(el) { return el && el.nodeType === 1 && el.hasAttribute('data-monto'); }
    function visible(el) { return !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length); }
    function alSalir(e) { if (esMonto(e.target)) SGV.normalizarMonto(e.target); }
    document.addEventListener('change', alSalir, true);
    document.addEventListener('focusout', alSalir, true);
    // Mientras corrige, la marca se quita apenas el texto vuelve a ser un monto.
    document.addEventListener('input', function (e) {
        if (esMonto(e.target) && SGV.leerMonto(e.target.value) !== null) quitarMarca(e.target);
    }, true);

    // ── Envío con un monto ilegible ──
    var AMBITO = '.modal, fieldset, .card, [data-montos-grupo]';
    var NO_REVISA = /^\s*(cancelar|cerrar|volver|limpiar)\b/i;

    function botonQueEnvia(e) {
        var el = e.target && e.target.closest ? e.target.closest('input[type=submit], input[type=image], button, a[href]') : null;
        if (!el || el.disabled) return null;
        if (el.tagName === 'BUTTON' && (el.getAttribute('type') || 'submit').toLowerCase() !== 'submit') return null;
        if (el.tagName === 'A' && !/__doPostBack|WebForm_DoPostBack/.test(el.getAttribute('href') || '')) return null;
        return el;
    }

    SGV.revisarMontosAntesDeEnviar = function (boton) {
        var ambito = boton.closest(AMBITO) || boton.form || document;
        SGV.normalizarMontos(ambito);
        var campos = ambito.querySelectorAll('[data-monto]');
        for (var i = 0; i < campos.length; i++) {
            // Un monto dentro de un [data-montos-grupo] solo cuenta para los botones de ese grupo.
            var grupo = campos[i].closest('[data-montos-grupo]');
            if (grupo && !grupo.contains(boton)) continue;
            if (visible(campos[i]) && SGV.leerMonto(campos[i].value) === null) return campos[i];
        }
        return null;
    };

    window.addEventListener('click', function (e) {
        var boton = botonQueEnvia(e);
        if (!boton) {
            // Otros clics en botones (type=button con data-accion, etc.): solo se normaliza.
            var otro = e.target && e.target.closest ? e.target.closest('button, input[type=button]') : null;
            if (otro) SGV.normalizarMontos(otro.form || document);
            return;
        }
        var etiqueta = boton.value || boton.textContent || '';
        if (boton.matches('[data-dismiss], .close, [formnovalidate], [data-montos-libre]') || NO_REVISA.test(etiqueta)) return;
        var malo = SGV.revisarMontosAntesDeEnviar(boton);
        if (!malo) return;
        e.preventDefault();
        e.stopImmediatePropagation();
        e.stopPropagation();
        try { malo.focus(); } catch (err) { }
        if (SGV.avisar) SGV.avisar('Revise el monto marcado. ' + SGV.mensajeFormatoMonto, { tipo: 'warning' });
    }, true);
    document.addEventListener('submit', function (e) { SGV.normalizarMontos(e.target); }, true);
})();

// ── Ayuda de la pantalla (E5-21) ────────────────────────────────────
// El "?" del encabezado es <a class="sgv-ayuda" href=".../Ayuda.aspx?tema=X" target="_blank" data-ayuda="X">.
// Con JS, el clic trae esa página, toma su #ayudaContenido (y su data-titulo) y lo muestra en #modalAyuda
// de Site.Master: no se sale de la pantalla ni se pierde un formulario a medio llenar. Los enlaces a otro
// tema dentro del fragmento también llevan data-ayuda y se abren en el mismo modal. Ctrl/Mayús/clic central
// siguen abriendo la pestaña, y en la propia Ayuda.aspx los enlaces navegan normalmente. Si la página no trae
// el tema (otro rol, sesión vencida, sin conexión), el modal lo dice y ofrece abrirlo en otra pestaña.
(function () {
    if (/\/Ayuda\.aspx$/i.test(window.location.pathname)) return;

    function mostrar(titulo, nodos, href) {
        var cuerpo = document.getElementById('modalAyudaCuerpo');
        var tit = document.getElementById('modalAyudaTitulo');
        var pestana = document.getElementById('modalAyudaPestana');
        if (!cuerpo || !tit) return false;
        tit.textContent = titulo || 'Ayuda';
        while (cuerpo.firstChild) cuerpo.removeChild(cuerpo.firstChild);
        for (var i = 0; i < nodos.length; i++) cuerpo.appendChild(nodos[i]);
        if (pestana) pestana.setAttribute('href', href);
        cuerpo.scrollTop = 0;
        return true;
    }

    function mensaje(texto) {
        var p = document.createElement('p');
        p.className = 'sgv-suave mb-0';
        p.textContent = texto;
        return p;
    }

    function abrir(href) {
        var $m = window.jQuery ? jQuery('#modalAyuda') : null;
        if (!$m || !$m.length || typeof $m.modal !== 'function' || !window.fetch || !window.DOMParser) return false;
        mostrar('Ayuda', [mensaje('Cargando la ayuda…')], href);
        if (!$m.hasClass('show')) $m.modal('show');
        fetch(href, { credentials: 'same-origin', headers: { 'Accept': 'text/html' } })
            .then(function (r) { return r.ok ? r.text() : Promise.reject(r.status); })
            .then(function (html) {
                var doc = new DOMParser().parseFromString(html, 'text/html');
                var art = doc.getElementById('ayudaContenido');
                if (art) {
                    var nodos = [];
                    for (var n = art.firstChild; n; n = n.nextSibling) nodos.push(document.importNode(n, true));
                    mostrar(art.getAttribute('data-titulo'), nodos, href);
                    return;
                }
                var aviso = doc.getElementById('ayudaAviso');
                mostrar('Ayuda', [mensaje(aviso ? aviso.textContent : 'No se pudo abrir este tema. Pruebe abrirlo en otra pestaña.')], href);
            })
            .catch(function () {
                mostrar('Ayuda', [mensaje('No se pudo cargar la ayuda. Revise la conexión o ábrala en otra pestaña.')], href);
            });
        return true;
    }

    document.addEventListener('click', function (e) {
        if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
        var a = e.target && e.target.closest ? e.target.closest('a[data-ayuda]') : null;
        if (!a || !a.href) return;
        if (abrir(a.href)) e.preventDefault();
    });

    window.SGV = window.SGV || {};
    SGV.abrirAyuda = abrir;
})();

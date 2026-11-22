// Los campos del modal se clonan desde <template id="tplCamposContrasena"> al abrirlo
// (SiteMaster.js): pueden no estar en el DOM, así que se leen con tolerancia a null.
function validarFormContrasena() {
    function valor(id) {
        var el = id ? document.getElementById(id) : null;
        return el ? el.value : '';
    }
    var actual = valor(SGV.txtContrasenaActual);
    var nueva = valor(SGV.txtNuevaContrasena);
    var confirmar = valor(SGV.txtConfirmarContrasena);

    if (!actual) {
        SGV.avisar('Por favor ingresa tu contraseña actual.');
        return false;
    }
    // Mismo mínimo que el servidor (PasswordHelper.LargoMinimoNueva), que la guarda recortada.
    // Las demás reglas (nombre, DNI, secuencias...) las revisa el servidor (PoliticaContrasena).
    var minimo = SGV.largoMinimoContrasena || 10;
    if (!nueva || nueva.trim().length < minimo) {
        SGV.avisar('La nueva contraseña debe tener al menos ' + minimo + ' caracteres.');
        return false;
    }
    if (nueva !== confirmar) {
        SGV.avisar('Las contraseñas nuevas no coinciden.');
        return false;
    }
    return true;
}

function deshabilitarAutocomplete() {
    $('input[type="text"], input[type="number"], input[type="search"], input[type="email"], input[type="tel"], input[type="date"], input[type="time"], textarea')
        .not('[autocomplete]')
        .attr('autocomplete', 'off');
}

// Error en un postback parcial (UpdatePanel). El servidor (Site.Master.cs,
// ScriptManager1_AsyncPostBackError) manda un código o un mensaje ya apto para el usuario.
// Sin esto MS Ajax lanzaba PageRequestManagerServerErrorException y el usuario no veía nada.
function manejarErrorPostbackParcial(args) {
    var error = args.get_error();
    args.set_errorHandled(true);

    var mensaje = String(error.message || '').replace(/^Sys\.WebForms\.PageRequestManager\w*Exception:\s*/, '');
    var esTimeout = error.name === 'Sys.WebForms.PageRequestManagerTimeoutException';
    // Solo el texto que manda el servidor (AsyncPostBackErrorMessage) es apto para el usuario; los
    // demás (respuesta de IIS 413/500, ParserError, "An unknown error...") son técnicos.
    // El texto propio de MS Ajax para un estado HTTP distinto de 200 (Sys.WebForms.Res.PRM_ServerError,
    // localizado: "Error desconocido al procesar la solicitud..." / "An unknown error...") no se muestra.
    var plantilla = (Sys.WebForms && Sys.WebForms.Res && Sys.WebForms.Res.PRM_ServerError) || '';
    var inicioGenerico = plantilla.split('{0}')[0];
    var esTextoDeMsAjax = /^An unknown error/i.test(mensaje) || (inicioGenerico && mensaje.indexOf(inicioGenerico) === 0);
    var esDelServidor = error.name === 'Sys.WebForms.PageRequestManagerServerErrorException'
        && error.httpStatusCode !== 0 && mensaje && !esTextoDeMsAjax;

    if (mensaje === 'SGV:SESION_VENCIDA') {
        location.href = SGV.urlLogin + '?error=sesion';
    } else if (mensaje === 'SGV:PAGINA_VENCIDA') {
        SGV.avisar(SGV.textoPaginaVencida, { tipo: 'warning', trasRecargar: true });
        location.href = location.href.split('#')[0];
    } else if (esTimeout || error.httpStatusCode === 0) {
        SGV.avisar('No hay conexión con el servidor. Intente de nuevo.', { tipo: 'warning' });
    } else {
        SGV.avisar(esDelServidor ? mensaje : 'Ocurrió un error inesperado. Intente nuevamente; si el problema continúa, contacte al administrador.', { tipo: 'danger' });
    }
}

$(document).ready(function () {
    console.log("Inicializando scripts maestros del SGV...");

    deshabilitarAutocomplete();

    // Inicializar componentes
    $('.dropdown-toggle').dropdown();

    // Obtener instancia del PageRequestManager
    if (typeof Sys !== 'undefined' && Sys.WebForms && Sys.WebForms.PageRequestManager) {
        var prm = Sys.WebForms.PageRequestManager.getInstance();

        // Sin conexión: MS Ajax 4 ignora la respuesta con estado 0 y esperaría hasta el timeout del
        // ScriptManager (90 s) con la capa "Procesando..." encima. Se escucha el evento "error" del XHR
        // del postback y se cancela; el endRequest (sin error, abortado) muestra el aviso.
        var sinConexion = false, peticionPostback = null;
        prm.add_beginRequest(function (sender, args) {
            sinConexion = false;
            peticionPostback = args && args.get_request ? args.get_request() : null;
        });
        if (Sys.Net && Sys.Net.WebRequestManager) {
            // El ejecutor (XHR) se crea después del beginRequest; invokingRequest llega justo antes de enviarlo.
            Sys.Net.WebRequestManager.add_invokingRequest(function (sender, args) {
                var peticion = args.get_webRequest();
                var ejecutor = peticion === peticionPostback && peticion.get_executor();
                if (!ejecutor || !ejecutor.executeRequest) return;
                var ejecutar = ejecutor.executeRequest;
                ejecutor.executeRequest = function () {
                    var resultado = ejecutar.apply(this, arguments);
                    var xhr = this._xmlHttpRequest;
                    if (xhr && xhr.addEventListener) {
                        xhr.addEventListener('error', function () {
                            sinConexion = true;
                            prm.abortPostBack();
                        });
                    }
                    return resultado;
                };
            });
        }

        // Al recibir una respuesta AJAX
        prm.add_endRequest(function (sender, args) {
            console.log("AJAX completado en Site.Master");

            if (sinConexion) {
                sinConexion = false;
                if (args && args.get_error && args.get_error()) args.set_errorHandled(true);
                SGV.avisar('No hay conexión con el servidor. Intente de nuevo.', { tipo: 'warning' });
                return;
            }

            if (args && args.get_error && args.get_error()) {
                manejarErrorPostbackParcial(args);
                return;
            }

            // Reinicializar dropdowns después de AJAX
            $('.dropdown-toggle').dropdown();

            deshabilitarAutocomplete();
        });
    }

    // Highlight del menú activo
    var paginaActual = window.location.pathname.split('/').pop().toLowerCase();
    $('.navbar-nav a').each(function () {
        var href = $(this).attr('href');
        if (href && href.toLowerCase() === paginaActual) {
            $(this).closest('.nav-item').addClass('active');
            $(this).closest('.dropdown').addClass('active');
        }
    });
});

// Aviso que manda el servidor en la URL (?aviso=pagina_vencida tras recargar una página con el
// ViewState vencido; Site.Master solo deja pasar valores conocidos). Se muestra una vez y se quita
// de la URL para que F5 no lo repita.
(function () {
    if (!SGV.avisoInicial) return;
    SGV.avisar(SGV.avisoInicial, { tipo: 'warning' });
    try {
        var url = new URL(window.location.href);
        url.searchParams.delete('aviso');
        history.replaceState(history.state, document.title, url.pathname + url.search + url.hash);
    } catch (e) { }
})();

// Atrás sin pantalla gris: las páginas protegidas no se guardan en caché (no-store), así que si
// una página llegó por POST (un botón del servidor) y el usuario vuelve a ella con Atrás, el
// navegador muestra "Confirmar reenvío del formulario" (ERR_CACHE_MISS). Se cambia esa entrada
// del historial por la misma URL como GET: Atrás y F5 vuelven a cargar la página limpia y nunca
// reenvían un registro.
(function () {
    if (!SGV.esRespuestaPost || !window.history || !history.replaceState) return;
    try { history.replaceState(history.state, document.title, window.location.href); } catch (e) { }
})();

// Textos con "<" pegado a una letra (o "&#"): ASP.NET los toma por HTML y rechaza el formulario
// entero ("valor Request.Form peligroso"), con lo que el usuario pierde lo que había llenado.
// Se separan con un espacio al salir del campo y otra vez al enviar; el texto se lee igual.
// Las contraseñas no se tocan.
(function () {
    function esTexto(el) {
        if (!el || !el.tagName) return false;
        if (el.tagName === 'TEXTAREA') return true;
        return el.tagName === 'INPUT' && /^(text|search|email|tel|url)?$/.test((el.getAttribute('type') || '').toLowerCase());
    }
    function limpiar(el) {
        var v = el.value;
        if (typeof v !== 'string' || (v.indexOf('<') < 0 && v.indexOf('&#') < 0)) return;
        el.value = v.replace(/<(?=[A-Za-z!\/?])/g, '< ').replace(/&#/g, '& #');
    }
    document.addEventListener('change', function (e) { if (esTexto(e.target)) limpiar(e.target); }, true);
    document.addEventListener('submit', function (e) {
        var campos = e.target && e.target.querySelectorAll ? e.target.querySelectorAll('input, textarea') : [];
        for (var i = 0; i < campos.length; i++) if (esTexto(campos[i])) limpiar(campos[i]);
    }, true);
})();

// Acciones del marcado (data-accion / data-accion-input / data-accion-change): las despacha
// SiteMaster.js. Devolver false cancela el clic (y el postback del botón).
window.SGV = window.SGV || {};
SGV.acciones = Object.assign(SGV.acciones || {}, {
    validarFormContrasena: function () { return validarFormContrasena(); }
});

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
    var minimo = SGV.largoMinimoContrasena || 8;
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

$(document).ready(function () {
    console.log("Inicializando scripts maestros del SGV...");

    deshabilitarAutocomplete();

    // Inicializar componentes
    $('.dropdown-toggle').dropdown();

    // Obtener instancia del PageRequestManager
    if (typeof Sys !== 'undefined' && Sys.WebForms && Sys.WebForms.PageRequestManager) {
        var prm = Sys.WebForms.PageRequestManager.getInstance();

        // Al recibir una respuesta AJAX
        prm.add_endRequest(function (sender, args) {
            console.log("AJAX completado en Site.Master");

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

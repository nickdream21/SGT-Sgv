function validarFormContrasena() {
    var actual = document.getElementById(SGV.txtContrasenaActual).value;
    var nueva = document.getElementById(SGV.txtNuevaContrasena).value;
    var confirmar = document.getElementById(SGV.txtConfirmarContrasena).value;

    if (!actual) {
        SGV.avisar('Por favor ingresa tu contraseña actual.');
        return false;
    }
    if (!nueva || nueva.length < 6) {
        SGV.avisar('La nueva contraseña debe tener al menos 6 caracteres.');
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

// ============================================================
// Anti-autorrelleno de credenciales en campos de búsqueda.
//
// Chrome/Edge IGNORAN autocomplete="off" para su gestor de
// contraseñas: ofrecen los usuarios guardados en cualquier campo
// de texto del sitio. La única técnica fiable es mantener el campo
// en modo readonly hasta que el usuario lo enfoca (un campo readonly
// no es candidato al autorrelleno del navegador) y restaurar el
// readonly al salir. El value sigue asignándose por JS y se envía
// en el postback con normalidad; para el usuario es transparente.
//
// Se aplica automáticamente a todo input cuyo placeholder contenga
// "buscar" (los campos de filtro del sistema) y a cualquier input
// marcado con la clase .no-autofill.
// ============================================================
function protegerCamposDeBusqueda(contexto) {
    var raiz = contexto || document;
    var inputs = raiz.querySelectorAll('input[type="text"], input:not([type]), input[type="search"]');
    Array.prototype.forEach.call(inputs, function (el) {
        if (el.dataset.noAutofill === '1') return;

        var placeholder = el.getAttribute('placeholder') || '';
        var esBusqueda = /buscar/i.test(placeholder) || el.classList.contains('no-autofill');
        if (!esBusqueda) return;

        el.dataset.noAutofill = '1';
        el.setAttribute('autocomplete', 'off');
        el.setAttribute('readonly', 'readonly');

        el.addEventListener('focus', function () { el.removeAttribute('readonly'); });
        el.addEventListener('blur', function () { el.setAttribute('readonly', 'readonly'); });
    });
}

// Oculta los campos de contraseña del modal convirtiéndolos a type="text"
// mientras el modal está cerrado. Chrome no ofrece credenciales guardadas
// en páginas que no tienen campos type="password" visibles.
var _passIds = [
    SGV.txtContrasenaActual,
    SGV.txtNuevaContrasena,
    SGV.txtConfirmarContrasena
];

function ocultarCamposPassword() {
    _passIds.forEach(function (id) {
        var el = document.getElementById(id);
        if (el) { el.type = 'text'; el.value = ''; }
    });
}

function mostrarCamposPassword() {
    _passIds.forEach(function (id) {
        var el = document.getElementById(id);
        if (el) { el.type = 'password'; }
    });
}

$(document).ready(function () {
    console.log("Inicializando scripts maestros del SGV...");

    deshabilitarAutocomplete();
    protegerCamposDeBusqueda();
    ocultarCamposPassword();

    $('#modalCambiarContrasena')
        .on('show.bs.modal', function () { mostrarCamposPassword(); })
        .on('hidden.bs.modal', function () { ocultarCamposPassword(); });

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
            protegerCamposDeBusqueda();
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

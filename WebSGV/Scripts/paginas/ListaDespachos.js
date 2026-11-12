// Inicializar Select2 en los dropdowns de conductores
function inicializarSelect2Conductores() {
    $('.conductor-select').select2({
        width: '100%',
        placeholder: 'Buscar conductor...',
        allowClear: false,
        language: {
            noResults: function() {
                return "No se encontraron conductores";
            },
            searching: function() {
                return "Buscando...";
            }
        }
    });
}

$(document).ready(function () {
    inicializarSelect2Conductores();
    autoHideMessages();
    establecerFechasPorDefecto();
});

var prm = Sys.WebForms.PageRequestManager.getInstance();
prm.add_endRequest(function () {
    inicializarSelect2Conductores();
    autoHideMessages();
});

function autoHideMessages() {
    setTimeout(function () {
        $('.alert').fadeOut('slow');
    }, 5000);
}

function establecerFechasPorDefecto() {
    var hoy = new Date();
    var primerDiaMes = new Date(hoy.getFullYear(), hoy.getMonth(), 1);

    var fechaDesde = document.getElementById(SGV.txtFechaDesde);
    var fechaHasta = document.getElementById(SGV.txtFechaHasta);

    if (fechaDesde && fechaDesde.value === '') {
        fechaDesde.value = primerDiaMes.toISOString().substr(0, 10);
    }

    if (fechaHasta && fechaHasta.value === '') {
        fechaHasta.value = hoy.toISOString().substr(0, 10);
    }
}

function confirmarAccion(mensaje) {
    return confirm(mensaje);
}

function validarNumeroPedido(input) {
    var valor = input.value.replace(/[^0-9]/g, '');
    input.value = valor;

    if (valor.length > 0 && valor.length !== 10) {
        input.style.borderColor = '#dc3545';
        return false;
    } else {
        input.style.borderColor = '#ced4da';
        return true;
    }
}

$(document).on('input', '#' + SGV.txtNumeroPedidoEdit, function () {
    validarNumeroPedido(this);
});

// ---- Documentos del lote: nombre del archivo elegido, botón Adjuntar y aviso de guías sin guardar ----
function iniciarDocumentosLote() {
    $('.ld-doc-subir').each(function () {
        var zona = $(this);
        var input = zona.find('input[type=file]');
        var etiqueta = zona.find('.ld-doc-elegir');
        var texto = zona.find('.ld-doc-elegir-texto');
        var boton = zona.find('.ld-doc-btn');
        if (!input.length) return;

        function actualizar() {
            var f = input[0].files && input[0].files[0];
            etiqueta.toggleClass('ld-con-archivo', !!f);
            texto.text(f ? f.name : 'Elegir PDF o foto');
            boton.toggleClass('ld-desactivado', !f);
        }
        input.off('change.ld').on('change.ld', actualizar);
        actualizar();
    });

    $('.ld-guia-input').each(function () {
        var campo = $(this);
        campo.data('original', campo.val());
        campo.off('input.ld').on('input.ld', function () {
            campo.toggleClass('ld-modificado', campo.val() !== campo.data('original'));
        });
    });
}

$(document).ready(iniciarDocumentosLote);
Sys.WebForms.PageRequestManager.getInstance().add_endRequest(function () {
    iniciarDocumentosLote();
    var panel = document.querySelector('.ld-docs-page');
    if (panel && panel.getBoundingClientRect().top < 0) panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
});

// "Más filtros" de los lotes: el UpdatePanel vuelve a dibujar la barra en cada búsqueda, así que se
// reabre solo si estaba abierto o si alguno de sus campos tiene un valor que está filtrando la lista.
(function () {
    var abierto = false;
    function ajustar() {
        var d = document.getElementById('ldFiltrosMas');
        if (!d) return;
        var conValor = Array.prototype.some.call(d.querySelectorAll('input[type=text]'), function (i) { return i.value.trim() !== ''; }) ||
            Array.prototype.some.call(d.querySelectorAll('select'), function (s) {
                var porDefecto = Array.prototype.filter.call(s.options, function (o) { return o.defaultSelected; })[0] || s.options[0];
                return s.value !== porDefecto.value;
            });
        if (abierto || conValor) d.open = true;
        d.addEventListener('toggle', function () { abierto = d.open; });
    }
    ajustar();
    if (window.Sys && Sys.WebForms) Sys.WebForms.PageRequestManager.getInstance().add_endRequest(ajustar);
})();

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
    $('.conductor-select').each(function () { SGV.nombrarSelect2(this); });
}

$(document).ready(function () {
    inicializarSelect2Conductores();
});

var prm = Sys.WebForms.PageRequestManager.getInstance();
prm.add_endRequest(function () {
    inicializarSelect2Conductores();
});

function validarNumeroPedido(input) {
    var valor = input.value.replace(/[^0-9]/g, '');
    input.value = valor;

    // Clase de Bootstrap en vez de colores fijos (M-143): respeta el tema y el foco.
    var valido = valor.length === 0 || valor.length === 10;
    input.classList.toggle('is-invalid', !valido);
    return valido;
}

$(document).on('input', '#' + SGV.txtNumeroPedidoEdit, function () {
    validarNumeroPedido(this);
});

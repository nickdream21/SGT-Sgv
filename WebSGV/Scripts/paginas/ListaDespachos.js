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

// Las fechas Desde/Hasta las pone el servidor (ConfigurarFechasPorDefecto). No se rellenan aquí: "?documentos=pendientes"
// y "Ver los lotes con pendientes" las dejan vacías a propósito (lotes de cualquier fecha) y rellenarlas mostraba un
// rango que no se había aplicado.
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

// Pestañas y vistas por postback parcial (E5-18a): el UpdatePanel vuelve a dibujar los filtros abiertos. En el
// celular se pliegan, salvo que el postback haya salido de los propios filtros (un combo que filtra o Buscar,
// que ya pliega SiteMaster.js): así al cambiar de pestaña o volver de un lote se ve la lista sin desplazarse.
(function () {
    if (!(window.Sys && Sys.WebForms)) return;
    var desdeFiltros = false;
    var prm = Sys.WebForms.PageRequestManager.getInstance();
    prm.add_beginRequest(function (s, a) {
        var el = a.get_postBackElement();
        desdeFiltros = !!(el && el.closest && el.closest('details.sgv-filtros'));
    });
    prm.add_endRequest(function () {
        if (desdeFiltros || !(window.matchMedia && window.matchMedia('(max-width: 767.98px)').matches)) return;
        document.querySelectorAll('.ld-pagina details.sgv-filtros[data-plegar-movil]').forEach(function (d) { d.open = false; });
    });
})();

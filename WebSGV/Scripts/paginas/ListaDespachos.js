// Fechas Desde/Hasta: las pone el servidor (ConfigurarFechasPorDefecto). No se rellenan aquí: "?documentos=pendientes"
// y "Ver los lotes con pendientes" las dejan vacías a propósito (lotes de cualquier fecha). La edición de un lote
// (combos de conductor y N° de pedido) está en EditarLote.js desde E6-28.

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

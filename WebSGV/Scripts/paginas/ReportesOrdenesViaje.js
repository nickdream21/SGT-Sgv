function cambiarTab(tab) {
    $('#seccionLiquidaciones').hide();
    $('#seccionViajesActivos').hide();
    $('#seccionPersonalizado').hide();
    $('#tabLiquidaciones, #tabViajesActivos, #tabPersonalizado').removeClass('tab-btn-active');

    if (tab === 'liquidaciones') {
        $('#seccionLiquidaciones').show();
        $('#tabLiquidaciones').addClass('tab-btn-active');
    } else if (tab === 'viajesActivos') {
        $('#seccionViajesActivos').show();
        $('#tabViajesActivos').addClass('tab-btn-active');
    } else if (tab === 'personalizado') {
        $('#seccionPersonalizado').show();
        $('#tabPersonalizado').addClass('tab-btn-active');
    }
}

$(document).ready(function () {
    establecerFechasPorDefecto();
});

function establecerFechasPorDefecto() {
    var hoy = new Date();
    var primerDia = new Date(hoy.getFullYear(), hoy.getMonth(), 1);

    if (!$('#' + SGV.txtFechaDesde).val()) {
        $('#' + SGV.txtFechaDesde).val(primerDia.toISOString().split('T')[0]);
    }

    if (!$('#' + SGV.txtFechaHasta).val()) {
        $('#' + SGV.txtFechaHasta).val(hoy.toISOString().split('T')[0]);
    }
}

function verDetalleOrden(idOrden) {
    $('#modalDetalleOrden').modal('show');
    $('#detalleOrdenContent').html('<div class="text-center py-5"><div class="spinner-border text-primary" role="status"><span class="sr-only">Cargando...</span></div><p class="mt-3 text-muted">Cargando información...</p></div>');

    $.ajax({
        url: 'ReportesOrdenesViaje.aspx/ObtenerDetalleOrden',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ idOrden: idOrden }),
        success: function (response) {
            $('#detalleOrdenContent').html(response.d);
        },
        error: function (xhr, status, error) {
            $('#detalleOrdenContent').html('<div class="alert alert-danger"><i class="fas fa-exclamation-triangle mr-2"></i>Error al cargar el detalle: ' + error + '</div>');
        }
    });
}

function exportarLiquidaciones() {
    limpiarErroresValidacion();
    var fechaDesde = $('#' + SGV.txtFechaDesde).val();
    var fechaHasta = $('#' + SGV.txtFechaHasta).val();
    var factorConversion = $('#' + SGV.txtFactorConversion).val() || '3.75';

    if (!validarFiltrosLiquidaciones(fechaDesde, fechaHasta, factorConversion)) {
        return;
    }

    window.location.href = 'ReportesOrdenesViaje.aspx?action=exportarLiquidaciones&fechaDesde=' + fechaDesde + '&fechaHasta=' + fechaHasta + '&factor=' + factorConversion;
}

function exportarViajesActivos() {
    limpiarErroresValidacion();
    var buscarConductor = $('#' + SGV.txtBuscarConductor).val();
    var estadoViaje = $('#' + SGV.ddlEstadoViaje).val();

    if (!validarFiltroViajesActivos(buscarConductor, estadoViaje)) {
        return;
    }

    window.location.href = 'ReportesOrdenesViaje.aspx?action=exportarViajesActivos&buscarConductor=' + encodeURIComponent(buscarConductor) + '&estadoViaje=' + estadoViaje;
}

function generarPDFLiquidaciones() {
    limpiarErroresValidacion();
    var fechaDesde = $('#' + SGV.txtFechaDesde).val();
    var fechaHasta = $('#' + SGV.txtFechaHasta).val();
    var factorConversion = $('#' + SGV.txtFactorConversion).val() || '3.75';

    if (!validarFiltrosLiquidaciones(fechaDesde, fechaHasta, factorConversion)) {
        return;
    }

    window.location.href = 'ReportesOrdenesViaje.aspx?action=generarPDF&fechaDesde=' + fechaDesde + '&fechaHasta=' + fechaHasta + '&factor=' + factorConversion;
}

function marcarTodasColumnas(checked) {
    $('.col-pers').prop('checked', checked);
}

function generarPersonalizado(formato) {
    limpiarErroresValidacion();
    var fechaDesde = $('#' + SGV.txtPersFechaDesde).val();
    var fechaHasta = $('#' + SGV.txtPersFechaHasta).val();
    var estado = $('#' + SGV.ddlPersEstado).val();
    var idConductor = $('#' + SGV.ddlPersConductor).val();
    var idCliente = $('#' + SGV.ddlPersCliente).val();
    var placaTracto = $('#' + SGV.txtPersPlacaTracto).val();
    var categoria = $('#' + SGV.txtPersCategoria).val();
    var orden = $('#' + SGV.ddlPersOrden).val();
    var factor = $('#' + SGV.txtPersFactor).val() || '3.75';
    var titulo = $('#' + SGV.txtPersTitulo).val() || 'Reporte Personalizado';
    var incluirTotales = $('#chkIncluirTotales').is(':checked') ? '1' : '0';
    var incluirResumen = $('#chkIncluirResumen').is(':checked') ? '1' : '0';

    if (!validarFiltrosPersonalizados(fechaDesde, fechaHasta, estado, idConductor, idCliente, placaTracto, categoria, orden, factor, titulo)) {
        return;
    }

    var columnas = [];
    $('.col-pers:checked').each(function () { columnas.push($(this).val()); });

    if (columnas.length === 0) {
        mostrarErrorCampo('errPersTitulo', 'Debe seleccionar al menos una columna para el reporte.');
        return;
    }

    var qs = 'action=reportePersonalizado'
        + '&formato=' + formato
        + '&fechaDesde=' + encodeURIComponent(fechaDesde)
        + '&fechaHasta=' + encodeURIComponent(fechaHasta)
        + '&estado=' + encodeURIComponent(estado)
        + '&idConductor=' + encodeURIComponent(idConductor)
        + '&idCliente=' + encodeURIComponent(idCliente)
        + '&placaTracto=' + encodeURIComponent(placaTracto)
        + '&categoria=' + encodeURIComponent(categoria)
        + '&orden=' + encodeURIComponent(orden)
        + '&factor=' + encodeURIComponent(factor)
        + '&titulo=' + encodeURIComponent(titulo)
        + '&totales=' + incluirTotales
        + '&resumen=' + incluirResumen
        + '&cols=' + encodeURIComponent(columnas.join(','));

    window.location.href = 'ReportesOrdenesViaje.aspx?' + qs;
}

function limpiarErroresValidacion() {
    $('.text-danger[id^="err"]').each(function () {
        $(this).addClass('d-none').text('');
    });
}

function mostrarErrorCampo(id, mensaje) {
    $('#' + id).removeClass('d-none').text(mensaje);
}

function validarFechaIso(valor) {
    return /^\d{4}-\d{2}-\d{2}$/.test(valor);
}

function validarFiltrosLiquidaciones(fechaDesde, fechaHasta, factor) {
    var ok = true;
    if (!fechaDesde || !validarFechaIso(fechaDesde)) { mostrarErrorCampo('errFechaDesde', 'Ingrese una fecha inicial válida (aaaa-mm-dd).'); ok = false; }
    if (!fechaHasta || !validarFechaIso(fechaHasta)) { mostrarErrorCampo('errFechaHasta', 'Ingrese una fecha final válida (aaaa-mm-dd).'); ok = false; }
    if (ok && new Date(fechaDesde) > new Date(fechaHasta)) { mostrarErrorCampo('errFechaHasta', 'La fecha final debe ser mayor o igual a la fecha inicial.'); ok = false; }

    var factorNumero = parseFloat((factor || '').replace(',', '.'));
    if (isNaN(factorNumero) || factorNumero < 0.01 || factorNumero > 20) {
        mostrarErrorCampo('errFactorConversion', 'Ingrese un factor entre 0.01 y 20.');
        ok = false;
    }
    return ok;
}

function validarFiltroViajesActivos(buscarConductor, estadoViaje) {
    var ok = true;
    if (buscarConductor && buscarConductor.length > 100) {
        mostrarErrorCampo('errBuscarConductor', 'La búsqueda no debe superar 100 caracteres.');
        ok = false;
    }
    if (buscarConductor && !/^[a-zA-ZáéíóúÁÉÍÓÚñÑ0-9\s\-\.]+$/.test(buscarConductor)) {
        mostrarErrorCampo('errBuscarConductor', 'Solo se permiten letras, números, espacios, punto y guion.');
        ok = false;
    }
    if (['TODOS', 'ABIERTO', 'CERRADO'].indexOf(estadoViaje) === -1) {
        mostrarErrorCampo('errEstadoViaje', 'Seleccione un estado de viaje válido.');
        ok = false;
    }
    return ok;
}

function validarFiltrosPersonalizados(fechaDesde, fechaHasta, estado, idConductor, idCliente, placaTracto, categoria, orden, factor, titulo) {
    var ok = true;
    if (!fechaDesde || !validarFechaIso(fechaDesde)) { mostrarErrorCampo('errPersFechaDesde', 'Ingrese una fecha inicial válida (aaaa-mm-dd).'); ok = false; }
    if (!fechaHasta || !validarFechaIso(fechaHasta)) { mostrarErrorCampo('errPersFechaHasta', 'Ingrese una fecha final válida (aaaa-mm-dd).'); ok = false; }
    if (ok && new Date(fechaDesde) > new Date(fechaHasta)) { mostrarErrorCampo('errPersFechaHasta', 'La fecha final debe ser mayor o igual a la fecha inicial.'); ok = false; }
    if (['TODOS', 'COMPLETADO', 'PENDIENTE', 'RECHAZADO'].indexOf(estado) === -1) { mostrarErrorCampo('errPersEstado', 'Seleccione un estado válido.'); ok = false; }
    if (!/^\d+$/.test(idConductor) || parseInt(idConductor, 10) < 0) { mostrarErrorCampo('errPersConductor', 'Seleccione un conductor válido.'); ok = false; }
    if (!/^\d+$/.test(idCliente) || parseInt(idCliente, 10) < 0) { mostrarErrorCampo('errPersCliente', 'Seleccione un cliente válido.'); ok = false; }
    if (placaTracto && !/^[a-zA-Z0-9\-\s]{0,15}$/.test(placaTracto)) { mostrarErrorCampo('errPersPlaca', 'La placa permite letras, números, espacios y guion (máx. 15).'); ok = false; }
    if (categoria && !/^[a-zA-ZáéíóúÁÉÍÓÚñÑ0-9\s\-\.]{0,60}$/.test(categoria)) { mostrarErrorCampo('errPersCategoria', 'Categoría inválida. Máximo 60 caracteres.'); ok = false; }
    if (['fecha_desc', 'fecha_asc', 'conductor', 'cliente'].indexOf(orden) === -1) { mostrarErrorCampo('errPersOrden', 'Seleccione un orden válido.'); ok = false; }

    var factorNumero = parseFloat((factor || '').replace(',', '.'));
    if (isNaN(factorNumero) || factorNumero < 0.01 || factorNumero > 20) { mostrarErrorCampo('errPersFactor', 'Ingrese un factor entre 0.01 y 20.'); ok = false; }
    if (!titulo || titulo.trim().length < 5 || titulo.trim().length > 120) { mostrarErrorCampo('errPersTitulo', 'El título debe tener entre 5 y 120 caracteres.'); ok = false; }
    return ok;
}

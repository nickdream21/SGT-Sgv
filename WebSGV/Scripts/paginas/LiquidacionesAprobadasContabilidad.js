var _urlPdfActual = '';

function htmlEncode(value) {
    return $('<div/>').text(value == null ? '' : value).html();
}

function valorTexto(value) {
    var v = (value || '').toString().trim();
    return v ? htmlEncode(v) : '—';
}

function money(value) {
    return Number(value || 0).toFixed(2);
}

function renderFilaConcepto(nombre, soles, dolares, detalle) {
    return '<tr>' +
        '<td>' + htmlEncode(nombre) + '</td>' +
        '<td class="text-right">' + money(soles) + '</td>' +
        '<td class="text-right">' + money(dolares) + '</td>' +
        '<td>' + valorTexto(detalle) + '</td>' +
        '</tr>';
}

function renderDetalleIngresos(d) {
    var html = '';
    html += renderFilaConcepto('Despacho', d.DespachoSoles, d.DespachoDolares, d.DescDespacho);
    html += renderFilaConcepto('Préstamo', d.PrestamoSoles, d.PrestamoDolares, d.DescPrestamo);
    html += renderFilaConcepto('Mensualidad', d.MensualidadSoles, d.MensualidadDolares, d.DescMensualidad);
    html += renderFilaConcepto('Otros autorizados', d.OtrosIngresosSoles, d.OtrosIngresosDolares, d.DescOtros);

    var adicionales = d.DetallesIngresosAdicionales || [];
    for (var i = 0; i < adicionales.length; i++) {
        var item = adicionales[i];
        html += renderFilaConcepto(item.Nombre || ('Ingreso adicional ' + (i + 1)), item.Soles, item.Dolares, item.Descripcion);
    }

    $('#tbodyDetalleIngresos').html(html || '<tr><td colspan="4" class="text-center text-muted">Sin ingresos registrados.</td></tr>');
}

function renderDetalleGastos(d) {
    var html = '';
    html += renderFilaConcepto('Peajes', d.GastosPeajesSoles, d.GastosPeajesDolares, d.DescPeajes);
    html += renderFilaConcepto('Alimentación', d.GastosAlimentacionSoles, d.GastosAlimentacionDolares, d.DescAlimentacion);
    html += renderFilaConcepto('Apoyo seguridad', d.GastosApoyoSeguridadSoles, d.GastosApoyoSeguridadDolares, d.DescApoyoSeguridad);
    html += renderFilaConcepto('Reparaciones/Varios', d.GastosReparacionesSoles, d.GastosReparacionesDolares, d.DescReparaciones);
    html += renderFilaConcepto('Movilidad', d.GastosMovilidadSoles, d.GastosMovilidadDolares, d.DescMovilidad);
    html += renderFilaConcepto('Encarpada/Desencarpada', d.GastosEncarpadaSoles, d.GastosEncarpadaDolares, d.DescEncarpada);
    html += renderFilaConcepto('Hospedaje', d.GastosHospedajeSoles, d.GastosHospedajeDolares, d.DescHospedaje);
    html += renderFilaConcepto('Combustible', d.GastosCombustibleSoles, d.GastosCombustibleDolares, d.DescCombustible);

    var adicionales = d.DetallesGastosAdicionales || [];
    for (var i = 0; i < adicionales.length; i++) {
        var item = adicionales[i];
        html += renderFilaConcepto(item.Nombre || ('Gasto adicional ' + (i + 1)), item.Soles, item.Dolares, item.Descripcion);
    }

    $('#tbodyDetalleGastos').html(html || '<tr><td colspan="4" class="text-center text-muted">Sin gastos registrados.</td></tr>');
}

function renderDetallePeajes(d) {
    var peajes = d.DetallesPeajes || [];
    if (!peajes.length) {
        $('#tbodyDetallePeajes').html('<tr><td colspan="6" class="text-center text-muted">Sin peajes detallados.</td></tr>');
        return;
    }

    var html = '';
    for (var i = 0; i < peajes.length; i++) {
        var p = peajes[i];
        html += '<tr>' +
            '<td>' + valorTexto(p.Estacion) + '</td>' +
            '<td>' + valorTexto(p.Fecha) + '</td>' +
            '<td>' + valorTexto(p.Comprobante) + '</td>' +
            '<td class="text-right">' + money(p.Soles) + '</td>' +
            '<td class="text-right">' + money(p.Dolares) + '</td>' +
            '<td>' + valorTexto(p.Observaciones) + '</td>' +
            '</tr>';
    }
    $('#tbodyDetallePeajes').html(html);
}

var _timerBusquedaProgresiva = null;
var _timerConductor = null;
var _ultimaClaveBusqueda = '';

function obtenerFiltrosBusqueda() {
    return {
        idConductor: parseInt($('#hfConductorId').val() || '0', 10),
        conductor: ($('#txtConductor').val() || '').trim(),
        numero: ($('#txtNumeroLiquidacion').val() || '').trim()
    };
}

function puedeBuscarAutomaticamente(filtros) {
    if (!filtros) {
        return false;
    }

    if (!filtros.conductor && !filtros.numero && filtros.idConductor <= 0) {
        return true;
    }

    if (filtros.idConductor > 0) {
        return true;
    }

    return filtros.conductor.length >= 2 || filtros.numero.length >= 2;
}

function programarBusquedaProgresiva() {
    clearTimeout(_timerBusquedaProgresiva);
    clearTimeout(_timerConductor);
    _timerBusquedaProgresiva = setTimeout(function () {
        var filtros = obtenerFiltrosBusqueda();
        if (!puedeBuscarAutomaticamente(filtros)) {
            return;
        }

        buscarLiquidaciones(false);
    }, 400);
}

function limpiarFiltros() {
    $('#txtConductor').val('');
    $('#hfConductorId').val('');
    $('#txtNumeroLiquidacion').val('');
    _ultimaClaveBusqueda = '';
    buscarLiquidaciones(false);
}

function buscarLiquidaciones(forzarManual) {
    var filtros = obtenerFiltrosBusqueda();
    var esManual = !!forzarManual;

    if (!esManual && !puedeBuscarAutomaticamente(filtros)) {
        return;
    }

    var clave = [filtros.idConductor, filtros.conductor.toLowerCase(), filtros.numero.toLowerCase()].join('|');
    if (!esManual && clave === _ultimaClaveBusqueda) {
        return;
    }
    _ultimaClaveBusqueda = clave;

    $.ajax({
        type: 'POST',
        url: 'LiquidacionesAprobadasContabilidad.aspx/ObtenerLiquidacionesAprobadasContabilidad',
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        data: JSON.stringify({ idConductor: filtros.idConductor, numeroOrden: filtros.numero, nombreConductor: filtros.idConductor > 0 ? '' : filtros.conductor }),
        success: function (response) {
            var data = response && response.d ? response.d : [];
            $('#lblTotalResultados').text(data.length);

            if (!data.length) {
                $('#tbodyLiquidaciones').html('<tr><td colspan="6" class="text-center text-muted py-4">No se encontraron liquidaciones aprobadas.</td></tr>');
                return;
            }

            var html = '';
            for (var i = 0; i < data.length; i++) {
                var item = data[i];
                html += '<tr>' +
                    '<td><strong>' + htmlEncode(item.NumeroOrdenViaje) + '</strong></td>' +
                    '<td>' + htmlEncode(item.NombreConductor) + '</td>' +
                    '<td>' + htmlEncode(item.FechaSalida) + ' al ' + htmlEncode(item.FechaLlegada) + '</td>' +
                    '<td class="text-right">' + Number(item.BalanceSoles || 0).toFixed(2) + '</td>' +
                    '<td class="text-right">' + Number(item.BalanceDolares || 0).toFixed(2) + '</td>' +
                    '<td class="text-center">' +
                        '<button type="button" class="btn btn-info btn-sm mr-1" onclick="verPdfLiquidacion(' + item.IdOrdenViaje + ')"><i class="fas fa-eye"></i> Ver</button>' +
                        '<button type="button" class="btn btn-outline-secondary btn-sm" onclick="verDetalle(' + item.IdOrdenViaje + ')"><i class="fas fa-list"></i> Detalle</button>' +
                    '</td>' +
                    '</tr>';
            }
            $('#tbodyLiquidaciones').html(html);
        },
        error: function () {
            $('#tbodyLiquidaciones').html('<tr><td colspan="6" class="text-center text-danger py-4">Error al obtener liquidaciones.</td></tr>');
        }
    });
}

function verDetalle(idOrdenViaje) {
    $.ajax({
        type: 'POST',
        url: 'LiquidacionesPendientes.aspx/ObtenerDetalleLiquidacion',
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        data: JSON.stringify({ idOrdenViaje: idOrdenViaje }),
        success: function (response) {
            var d = response && response.d ? response.d : null;
            if (!d) return;

            $('#detalleNumero').text(d.NumeroOrdenViaje || '');
            $('#detalleConductor').text(d.NombreConductor || '');
            $('#detalleTracto').text(d.PlacaTracto || '');
            $('#detalleCarreta').text(d.PlacaCarreta || '');
            $('#detalleFechaSalida').text(d.FechaSalida || '');
            $('#detalleFechaLlegada').text(d.FechaLlegada || '');
            $('#detalleIngresosSoles').text(money(d.TotalIngresosSoles));
            $('#detalleIngresosDolares').text(money(d.TotalIngresosDolares));
            $('#detalleGastosSoles').text(money(d.TotalGastosSoles));
            $('#detalleGastosDolares').text(money(d.TotalGastosDolares));

            renderDetalleIngresos(d);
            renderDetalleGastos(d);
            renderDetallePeajes(d);

            $('#modalDetalleLiquidacion').modal('show');
        }
    });
}

function verPdfLiquidacion(idOrdenViaje) {
    if (!idOrdenViaje) {
        return;
    }

    $.ajax({
        type: 'POST',
        url: 'LiquidacionesPendientes.aspx/ObtenerUrlPdfOrdenViaje',
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        data: JSON.stringify({ idOrdenViaje: idOrdenViaje }),
        success: function (response) {
            var r = response && response.d ? response.d : null;
            if (r && r.success && r.url) {
                abrirModalPdfLiquidacion(r.url);
                return;
            }

            alert(r && r.message
                ? r.message
                : 'No se encontró el PDF firmado para esta liquidación.');
        },
        error: function () {
            alert('Error al abrir el PDF de la liquidación.');
        }
    });
}

function abrirModalPdfLiquidacion(url) {
    _urlPdfActual = url || '';
    if (!_urlPdfActual) {
        return;
    }

    var $contenedor = $('#pdfContenedor');
    var $iframe = $('#iframePdfLiquidacion');

    $contenedor.removeClass('cargado');
    $iframe.attr('src', 'about:blank');
    $iframe.attr('src', _urlPdfActual);
    $('#lblEstadoPdfLiquidacion').text('Vista previa en línea del documento oficial.');
    $('#modalPdfLiquidacion').modal('show');
}

$('#txtConductor').on('input', function () {
    clearTimeout(_timerBusquedaProgresiva);
    var termino = ($(this).val() || '').trim();
    $('#hfConductorId').val('');

    if (!termino.length) {
        $('#conductorSugg').hide().empty();
        programarBusquedaProgresiva();
        return;
    }

    if (termino.length < 2) {
        $('#conductorSugg').hide().empty();
        return;
    }

    var terminoSolicitud = termino;
    _timerConductor = setTimeout(function () {
        $.ajax({
            type: 'POST',
            url: 'LiquidacionesPendientes.aspx/BuscarConductores',
            contentType: 'application/json; charset=utf-8',
            dataType: 'json',
            data: JSON.stringify({ termino: terminoSolicitud }),
            success: function (response) {
                var data = response && response.d ? response.d : [];
                if (!data.length) {
                    $('#conductorSugg').hide().empty();
                    return;
                }

                var html = '';
                for (var i = 0; i < data.length; i++) {
                    var item = data[i];
                    html += '<div class="autocomplete-item" data-id="' + item.IdConductor + '" data-text="' + htmlEncode(item.NombreCompleto) + '">' + htmlEncode(item.NombreCompleto) + '</div>';
                }
                $('#conductorSugg').html(html).show();
            }
        });

        programarBusquedaProgresiva();
    }, 250);
});

$(document).on('click', '#conductorSugg .autocomplete-item', function () {
    $('#txtConductor').val($(this).attr('data-text'));
    $('#hfConductorId').val($(this).attr('data-id'));
    $('#conductorSugg').hide().empty();
    buscarLiquidaciones(false);
});

$('#txtNumeroLiquidacion').on('input', function () {
    programarBusquedaProgresiva();
});

$(document).ready(function () {
    $('#iframePdfLiquidacion').on('load', function () {
        if (!_urlPdfActual) {
            return;
        }
        $('#pdfContenedor').addClass('cargado');
    });

    $('#btnAbrirPdfNuevaPestana').on('click', function () {
        if (!_urlPdfActual) {
            return;
        }
        window.open(_urlPdfActual, '_blank');
    });

    $('#btnDescargarPdfLiquidacion').on('click', function () {
        if (!_urlPdfActual) {
            return;
        }

        var separador = _urlPdfActual.indexOf('?') >= 0 ? '&' : '?';
        window.open(_urlPdfActual + separador + 'download=1', '_blank');
    });

    $('#modalPdfLiquidacion').on('hidden.bs.modal', function () {
        _urlPdfActual = '';
        $('#pdfContenedor').removeClass('cargado');
        $('#iframePdfLiquidacion').attr('src', 'about:blank');
    });

    buscarLiquidaciones(false);
});

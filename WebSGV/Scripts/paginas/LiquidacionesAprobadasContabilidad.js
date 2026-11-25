var _urlPdfActual = '';

function valorTexto(value) {
    var v = (value || '').toString().trim();
    return v ? SGV.escaparHtml(v) : '—';
}

function money(value) {
    return Number(value || 0).toFixed(2);
}

function renderFilaConcepto(nombre, soles, dolares, detalle) {
    return '<tr>' +
        '<td>' + SGV.escaparHtml(nombre) + '</td>' +
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
var _paginaActual = 0;
// Filtros de la última búsqueda enviada: el paginador pide otra página de ESA búsqueda, aunque el
// usuario haya escrito otra cosa en los filtros sin buscar todavía.
var _ultimosFiltros = null;

// U-095: la pantalla abre con los últimos 90 días por fecha de salida; "Todo el historial" quita el rango.
var DIAS_RANGO_POR_DEFECTO = 90;

function fechaIso(d) {
    var mm = ('0' + (d.getMonth() + 1)).slice(-2);
    var dd = ('0' + d.getDate()).slice(-2);
    return d.getFullYear() + '-' + mm + '-' + dd;
}

function fechaLegible(iso) {
    var p = (iso || '').split('-');
    return p.length === 3 ? p[2] + '/' + p[1] + '/' + p[0] : iso;
}

function ponerRangoPorDefecto() {
    var hoy = new Date();
    var desde = new Date(hoy.getFullYear(), hoy.getMonth(), hoy.getDate() - DIAS_RANGO_POR_DEFECTO);
    $('#txtFechaDesde').val(fechaIso(desde));
    $('#txtFechaHasta').val(fechaIso(hoy));
}

function describirRango(filtros) {
    var texto;
    if (filtros.desde && filtros.hasta) {
        texto = 'Liquidaciones con salida del ' + fechaLegible(filtros.desde) + ' al ' + fechaLegible(filtros.hasta) + '.';
    } else if (filtros.desde) {
        texto = 'Liquidaciones con salida desde el ' + fechaLegible(filtros.desde) + '.';
    } else if (filtros.hasta) {
        texto = 'Liquidaciones con salida hasta el ' + fechaLegible(filtros.hasta) + '.';
    } else {
        texto = 'Todo el historial (sin rango de fechas).';
    }
    $('#lblRangoFechas').text(texto);
}

function obtenerFiltrosBusqueda() {
    return {
        idConductor: parseInt($('#hfConductorId').val() || '0', 10),
        conductor: ($('#txtConductor').val() || '').trim(),
        numero: ($('#txtNumeroLiquidacion').val() || '').trim(),
        desde: ($('#txtFechaDesde').val() || '').trim(),
        hasta: ($('#txtFechaHasta').val() || '').trim()
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
    ponerRangoPorDefecto();
    _ultimaClaveBusqueda = '';
    buscarLiquidaciones(false);
}

function verTodoHistorial() {
    $('#txtFechaDesde').val('');
    $('#txtFechaHasta').val('');
    buscarLiquidaciones(true);
}

function limpiarResultados(mensajeHtml) {
    $('#lblTotalResultados').text('0');
    $('#lblMostrando').text('');
    $('#paginadorLiquidaciones').empty();
    $('#tbodyLiquidaciones').html(mensajeHtml);
}

// Paginador sgv-paginador (E5-17, M-153): la página actual es un <span>; las demás, botones que piden esa
// página de la última búsqueda. "Anterior" y "Siguiente" solo aparecen cuando hay adónde ir.
function itemPaginador(texto, pagina, opciones) {
    opciones = opciones || {};
    var etiqueta = opciones.etiqueta ? ' aria-label="' + SGV.escaparHtml(opciones.etiqueta) + '"' : '';
    if (opciones.activa) {
        // aria-label no vale en un <span> sin rol: el texto largo va en sr-only.
        var largo = opciones.etiqueta ? '<span class="sr-only">' + SGV.escaparHtml(opciones.etiqueta) + '</span>' : '';
        return '<span aria-current="page"><span aria-hidden="' + (largo ? 'true' : 'false') + '">' +
            SGV.escaparHtml(texto) + '</span>' + largo + '</span>';
    }
    return '<button type="button" data-accion="irPagina" data-pagina="' + pagina + '"' + etiqueta + '>' +
        SGV.escaparHtml(texto) + '</button>';
}

function pintarPaginador(pagina, total, tamano) {
    var paginas = Math.max(1, Math.ceil(total / tamano));
    var $pag = $('#paginadorLiquidaciones');
    if (paginas <= 1) {
        $pag.empty();
        return;
    }

    var html = pagina > 0 ? itemPaginador('Anterior', pagina - 1, { etiqueta: 'Página anterior' }) : '';
    var inicio = Math.max(0, Math.min(pagina - 2, paginas - 5));
    var fin = Math.min(paginas - 1, inicio + 4);
    for (var p = inicio; p <= fin; p++) {
        html += itemPaginador(String(p + 1), p, { activa: p === pagina, etiqueta: 'Página ' + (p + 1) + ' de ' + paginas });
    }
    if (pagina < paginas - 1) html += itemPaginador('Siguiente', pagina + 1, { etiqueta: 'Página siguiente' });
    $pag.html(html);
}

function irPagina(pagina) {
    if (isNaN(pagina) || pagina < 0 || !_ultimosFiltros) {
        return;
    }
    buscarLiquidaciones(true, pagina, _ultimosFiltros);
}

function buscarLiquidaciones(forzarManual, pagina, filtrosGuardados) {
    var filtros = filtrosGuardados || obtenerFiltrosBusqueda();
    var esManual = !!forzarManual;

    if (!esManual && !puedeBuscarAutomaticamente(filtros)) {
        return;
    }

    if (filtros.desde && filtros.hasta && filtros.desde > filtros.hasta) {
        SGV.avisar('La fecha "desde" es posterior a la fecha "hasta".', { tipo: 'warning' });
        return;
    }

    // Cualquier cambio de filtros vuelve a la primera página; solo el paginador pide otra.
    var paginaPedida = typeof pagina === 'number' ? pagina : 0;

    var clave = [filtros.idConductor, filtros.conductor.toLowerCase(), filtros.numero.toLowerCase(), filtros.desde, filtros.hasta].join('|');
    if (!esManual && clave === _ultimaClaveBusqueda) {
        return;
    }
    _ultimaClaveBusqueda = clave;
    _ultimosFiltros = filtros;
    describirRango(filtros);

    $.ajax({
        type: 'POST',
        url: 'LiquidacionesAprobadasContabilidad.aspx/ObtenerLiquidacionesAprobadasContabilidad',
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        data: JSON.stringify({
            idConductor: filtros.idConductor,
            numeroOrden: filtros.numero,
            nombreConductor: filtros.idConductor > 0 ? '' : filtros.conductor,
            fechaDesde: filtros.desde,
            fechaHasta: filtros.hasta,
            pagina: paginaPedida
        }),
        success: function (response) {
            var r = response && response.d ? response.d : null;
            if (!r) {
                limpiarResultados('<tr><td colspan="6" class="text-center text-danger py-4">Error al obtener liquidaciones.</td></tr>');
                return;
            }
            if (r.Mensaje) {
                limpiarResultados('<tr><td colspan="6" class="text-center text-danger py-4">' + SGV.escaparHtml(r.Mensaje) + '</td></tr>');
                return;
            }

            // Búsqueda pedida con un botón (no la que corre mientras se escribe): en el celular se pliegan
            // los filtros para dejar a la vista los resultados.
            if (esManual) SGV.plegarFiltros(document.getElementById('btnBuscarLiquidaciones'));

            var data = r.Items || [];
            var total = r.Total || 0;
            var tamano = r.TamanoPagina || 50;
            _paginaActual = r.Pagina || 0;

            if (!data.length) {
                limpiarResultados('<tr><td colspan="6" class="text-center text-muted py-4">No se encontraron liquidaciones aprobadas.</td></tr>');
                $('#lblMostrando').text('Mostrando 0 de 0');
                return;
            }

            $('#lblTotalResultados').text(total);
            var paginas = Math.max(1, Math.ceil(total / tamano));
            $('#lblMostrando').text('Mostrando ' + data.length + ' de ' + total +
                (paginas > 1 ? ' · página ' + (_paginaActual + 1) + ' de ' + paginas : ''));
            pintarPaginador(_paginaActual, total, tamano);

            var html = '';
            for (var i = 0; i < data.length; i++) {
                var item = data[i];
                html += '<tr>' +
                    '<td><strong>' + SGV.escaparHtml(item.NumeroOrdenViaje) + '</strong></td>' +
                    '<td>' + SGV.escaparHtml(item.NombreConductor) + '</td>' +
                    '<td>' + SGV.escaparHtml(item.FechaSalida) + ' al ' + SGV.escaparHtml(item.FechaLlegada) + '</td>' +
                    '<td class="text-right">' + Number(item.BalanceSoles || 0).toFixed(2) + '</td>' +
                    '<td class="text-right">' + Number(item.BalanceDolares || 0).toFixed(2) + '</td>' +
                    '<td class="text-center">' +
                        '<button type="button" class="btn btn-outline-primary btn-sm mr-1" data-accion="verPdfLiquidacion" data-id="' + item.IdOrdenViaje + '">Ver</button>' +
                        '<button type="button" class="btn btn-outline-secondary btn-sm" data-accion="verDetalle" data-id="' + item.IdOrdenViaje + '">Detalle</button>' +
                    '</td>' +
                    '</tr>';
            }
            $('#tbodyLiquidaciones').html(html);
        },
        error: function () {
            limpiarResultados('<tr><td colspan="6" class="text-center text-danger py-4">Error al obtener liquidaciones.</td></tr>');
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
            if (!d) { SGV.avisar('Error al cargar el detalle de la liquidación. Intente de nuevo.', { tipo: 'danger' }); return; }

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
        },
        error: function () {
            SGV.avisar('Error al cargar el detalle de la liquidación. Intente de nuevo.', { tipo: 'danger' });
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

            SGV.avisar(r && r.message
                ? r.message
                : 'No se encontró el PDF firmado para esta liquidación.');
        },
        error: function () {
            SGV.avisar('Error al abrir el PDF de la liquidación.');
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
            url: 'LiquidacionesAprobadasContabilidad.aspx/BuscarConductores',
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
                    html += '<div class="autocomplete-item" data-id="' + item.IdConductor + '" data-text="' + SGV.escaparHtml(item.NombreCompleto) + '">' + SGV.escaparHtml(item.NombreCompleto) + '</div>';
                }
                $('#conductorSugg').html(html).show();
            },
            error: function () {
                // Sin elemento seleccionable: que no parezca "sin resultados".
                $('#conductorSugg').html('<div class="px-3 py-2 small text-danger">No se pudo buscar conductores. Intente de nuevo.</div>').show();
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

$('#txtFechaDesde, #txtFechaHasta').on('change', function () {
    programarBusquedaProgresiva();
});

$(document).ready(function () {
    ponerRangoPorDefecto();

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

// Acciones del marcado (data-accion / data-accion-input / data-accion-change): las despacha
// SiteMaster.js. Devolver false cancela el clic (y el postback del botón).
window.SGV = window.SGV || {};
SGV.acciones = Object.assign(SGV.acciones || {}, {
    buscarLiquidaciones: function () { buscarLiquidaciones(true); },
    limpiarFiltros: function () { limpiarFiltros(); },
    verTodoHistorial: function () { verTodoHistorial(); },
    irPagina: function (el) { irPagina(Number(el.getAttribute('data-pagina'))); },
    verPdfLiquidacion: function (el) { verPdfLiquidacion(Number(el.getAttribute('data-id'))); },
    verDetalle: function (el) { verDetalle(Number(el.getAttribute('data-id'))); }
});

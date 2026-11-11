let idOrdenParaAprobar = 0;
let _idOrdenDetalleActual = 0;
let modoAprobacion = false;
let _balanceSoles = 0;
let _balanceDolares = 0;
// Para recalcular el balance mientras se registra el dinero entregado al conductor.
let _ingresosFijosSoles = 0, _ingresosFijosDolares = 0, _gastosSoles = 0, _gastosDolares = 0;

function verDetalleLiquidacion(idOrdenViaje) {
    console.log('Ver detalle:', idOrdenViaje);

    $('#btnAprobarDesdeModal').hide().prop('disabled', false).html('<i class="fas fa-check mr-1"></i>Aprobar liquidación');
    $('#detalleLoading').show();
    $('#detalleError').hide();
    $('#approvalBanner').hide();
    $('#modalModeBadge').hide();
    $('#modalDetalleLiquidacionHeader').removeClass('mode-aprobacion');
    $('#modalDetalleLiquidacion .detail-section').hide();
    $('#modalNumeroOrden').text('');
    $('#modalDetalleLiquidacion').modal('show');

    $.ajax({
        type: "POST",
        url: "LiquidacionesPendientes.aspx/ObtenerDetalleLiquidacion",
        data: JSON.stringify({ idOrdenViaje: idOrdenViaje }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            mostrarDetalle(response.d);
        },
        error: function (xhr, status, error) {
            console.error('Error:', error);
            $('#detalleLoading').hide();
            $('#detalleError').show();
        }
    });
}

function toggleDetail(btn) {
    var $btn = $(btn);
    var $subRow = $btn.closest('tr').next('.detail-sub-row');
    $subRow.toggle();
    $btn.find('i').toggleClass('fa-chevron-down fa-chevron-up');
}

function buildSubTablePeajes(items) {
    if (!items || items.length === 0) return '';
    var html = '<table class="table table-sub table-bordered mb-0">' +
        '<thead><tr><th>Estaci&oacute;n</th><th>Fecha</th><th>Comprobante</th>' +
        '<th class="text-right">S/</th><th class="text-right">$</th><th>Observaciones</th></tr></thead><tbody>';
    items.forEach(function (item) {
        html += '<tr>' +
            '<td>' + (item.Estacion || '-') + '</td>' +
            '<td>' + (item.Fecha || '-') + '</td>' +
            '<td>' + (item.Comprobante || '-') + '</td>' +
            '<td class="text-right">' + parseFloat(item.Soles || 0).toFixed(2) + '</td>' +
            '<td class="text-right">' + parseFloat(item.Dolares || 0).toFixed(2) + '</td>' +
            '<td>' + (item.Observaciones || '-') + '</td>' +
            '</tr>';
    });
    html += '</tbody></table>';
    return html;
}

function buildSubTableGenerica(items) {
    if (!items || items.length === 0) return '';
    var html = '<table class="table table-sub table-bordered mb-0">' +
        '<thead><tr><th>Fecha</th><th>Comprobante</th>' +
        '<th class="text-right">S/</th><th class="text-right">$</th><th>Observaciones</th></tr></thead><tbody>';
    items.forEach(function (item) {
        html += '<tr>' +
            '<td>' + (item.Fecha || '-') + '</td>' +
            '<td>' + (item.Comprobante || '-') + '</td>' +
            '<td class="text-right">' + parseFloat(item.Soles || 0).toFixed(2) + '</td>' +
            '<td class="text-right">' + parseFloat(item.Dolares || 0).toFixed(2) + '</td>' +
            '<td>' + (item.Observaciones || '-') + '</td>' +
            '</tr>';
    });
    html += '</tbody></table>';
    return html;
}

function makeRow(icon, iconClass, label, soles, dolares, desc, subTable) {
    var hasDetail = (desc && desc.trim()) || (subTable && subTable.trim());
    var expandBtn = hasDetail
        ? ' <button type="button" class="btn btn-expand ml-1" onclick="toggleDetail(this)" title="Ver detalle">' +
          '<i class="fas fa-chevron-down"></i></button>'
        : '';

    var mainRow = '<tr>' +
        '<td>' + label + expandBtn + '</td>' +
        '<td class="text-right">S/ ' + parseFloat(soles || 0).toFixed(2) + '</td>' +
        '<td class="text-right">$ ' + parseFloat(dolares || 0).toFixed(2) + '</td>' +
        '</tr>';

    if (!hasDetail) return mainRow;

    var subContent = '';
    if (desc && desc.trim()) {
        subContent += '<div class="detail-desc"><strong>Descripci&oacute;n:</strong> ' +
            $('<span>').text(desc).html() + '</div>';
    }
    if (subTable && subTable.trim()) {
        subContent += '<div class="detail-sub-content">' + subTable + '</div>';
    }

    var subRow = '<tr class="detail-sub-row" style="display:none;">' +
        '<td colspan="3" class="p-0 border-top-0">' + subContent + '</td>' +
        '</tr>';

    return mainRow + subRow;
}

function actualizarTextoHoraLlegada(horaDeclarada, horaSistema, horaGps) {
    var partes = [];
    if (horaGps) {
        // Desde 2026-11 la llegada la pone el GPS; si el conductor declaró otra hora, se muestra aparte.
        partes.push(horaGps + ' h (GPS)');
        if (horaDeclarada && horaDeclarada !== horaGps) partes.push(horaDeclarada + ' h (declarada por el conductor)');
    } else {
        partes.push(horaDeclarada ? horaDeclarada + ' h (declarada por el conductor)' : 'sin declarar');
    }
    partes.push(horaSistema ? horaSistema + ' h (envío al sistema)' : 'sin registrar');
    $('#detalleHoraLlegada').text(partes.join(' · '));
}

function consultarHoraLlegadaGps() {
    if (!_idOrdenDetalleActual) return;

    var $btn = $('#btnConsultarHoraGps');
    $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i> Consultando...');

    $.ajax({
        type: "POST",
        url: "LiquidacionesPendientes.aspx/ConsultarHoraLlegadaGps",
        data: JSON.stringify({ idOrdenViaje: _idOrdenDetalleActual }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            var result = response.d;
            $btn.prop('disabled', false).html('<i class="fas fa-satellite-dish"></i> Verificar GPS');
            if (result.success) {
                var textoActual = $('#detalleHoraLlegada').text();
                $('#detalleHoraLlegada').text(textoActual + ' · ' + result.horaLlegadaGps + ' h (verificada por GPS)');
            } else {
                SGV.avisar('❌ ' + result.message);
            }
        },
        error: function (xhr) {
            console.error('Error:', xhr.responseText);
            $btn.prop('disabled', false).html('<i class="fas fa-satellite-dish"></i> Verificar GPS');
            SGV.avisar('Error al consultar el GPS. Por favor intente de nuevo.');
        }
    });
}

$(document).on('click', '#btnConsultarHoraGps', consultarHoraLlegadaGps);

function mostrarDetalle(datos) {
    if (!datos) {
        $('#detalleLoading').hide();
        $('#detalleError').show();
        return;
    }

    $('#modalNumeroOrden').text(datos.NumeroOrdenViaje);
    $('#detalleConductor').text(datos.NombreConductor);
    $('#detalleTracto').text(datos.PlacaTracto);
    $('#detalleCarreta').text(datos.PlacaCarreta);
    $('#detallePeriodo').text(datos.FechaSalida + ' al ' + datos.FechaLlegada);
    actualizarTextoHoraLlegada(datos.HoraLlegadaDeclarada, datos.HoraLlegadaSistema, datos.HoraLlegadaGps);

    // Corrección de salida: solo disponible mientras la liquidación esté PENDIENTE.
    _idOrdenDetalleActual = datos.IdOrdenViaje;
    $('#panelCorregirSalida').hide();
    $('#corregirSalidaMotivo').val('');
    $('#errorCorregirSalidaMotivo').hide();
    if (datos.EstadoAprobacion === 'PENDIENTE') {
        var horaTxt = datos.HoraSalida ? datos.HoraSalida + ' h' : 'sin hora';
        $('#detalleSalidaValor').text(datos.FechaSalida + '  ·  ' + horaTxt);
        $('#corregirSalidaFecha').val(datos.FechaSalidaISO || '');
        $('#corregirSalidaHora').val(datos.HoraSalida || '');
        $('#seccionCorregirSalida').show();
    } else {
        $('#seccionCorregirSalida').hide();
    }

    if (datos.Observaciones) {
        $('#detalleObservaciones').text(datos.Observaciones);
        $('#detalleObservacionesRow').show();
    } else {
        $('#detalleObservacionesRow').hide();
    }

    $('#detalleIngresosSoles').text(parseFloat(datos.TotalIngresosSoles).toFixed(2));
    $('#detalleIngresosDolares').text(parseFloat(datos.TotalIngresosDolares).toFixed(2));
    $('#detalleGastosSoles').text(parseFloat(datos.TotalGastosSoles).toFixed(2));
    $('#detalleGastosDolares').text(parseFloat(datos.TotalGastosDolares).toFixed(2));

    var balanceSoles = parseFloat(datos.TotalIngresosSoles) - parseFloat(datos.TotalGastosSoles);
    var balanceDolares = parseFloat(datos.TotalIngresosDolares) - parseFloat(datos.TotalGastosDolares);

    _balanceSoles = balanceSoles;
    _balanceDolares = balanceDolares;

    // Barra de balance con tinte suave: verde si cuadra o es a favor, rojo si hay saldo negativo.
    $('#detalleBalanceSoles').text(balanceSoles.toFixed(2))
        .toggleClass('sgv-negativo', balanceSoles < 0);
    $('#detalleBalanceDolares').text(balanceDolares.toFixed(2))
        .toggleClass('sgv-negativo', balanceDolares < 0);
    $('#rfBalanceBar').toggleClass('es-negativo', balanceSoles < 0 || balanceDolares < 0);

    // --- Desglose de Ingresos ---
    var ingresosItems = [
        { key: 'Despacho',      label: 'Adelanto para el viaje', descKey: 'DescDespacho' },
        { key: 'Prestamo',      label: 'Pr&eacute;stamo',  descKey: 'DescPrestamo' },
        { key: 'Mensualidad',   label: 'Mensualidad',      descKey: 'DescMensualidad' },
        { key: 'OtrosIngresos', label: 'Otros Ingresos',   descKey: 'DescOtros' }
    ];

    var ingresosHtml = '';
    ingresosItems.forEach(function (item) {
        var soles = parseFloat(datos[item.key + 'Soles'] || 0);
        var dolares = parseFloat(datos[item.key + 'Dolares'] || 0);
        if (soles > 0 || dolares > 0) {
            var desc = datos[item.descKey] || '';
            ingresosHtml += makeRow('fa-plus-circle', 'text-success', item.label, soles, dolares, desc, '');
        }
    });

    if (datos.DetallesIngresosAdicionales && datos.DetallesIngresosAdicionales.length > 0) {
        datos.DetallesIngresosAdicionales.forEach(function (item) {
            var nombre = item.Nombre || 'Ingreso Adicional';
            ingresosHtml += makeRow('fa-plus-circle', 'text-success', nombre,
                item.Soles, item.Dolares, item.Descripcion || '', '');
        });
    }

    if (!ingresosHtml) {
        ingresosHtml = '<tr><td colspan="3" class="text-center text-muted">No hay ingresos registrados</td></tr>';
    }
    $('#detalleIngresosBody').html(ingresosHtml);

    // --- Desglose de Gastos ---
    var gastosItems = [
        { key: 'Peajes',         label: 'Peajes',                   descKey: 'DescPeajes',         detailKey: 'DetallesPeajes',       detailType: 'peajes' },
        { key: 'Alimentacion',   label: 'Alimentaci&oacute;n',      descKey: 'DescAlimentacion',   detailKey: '',                     detailType: '' },
        { key: 'ApoyoSeguridad', label: 'Apoyo de Seguridad',       descKey: 'DescApoyoSeguridad', detailKey: '',                     detailType: '' },
        { key: 'Reparaciones',   label: 'Reparaciones y Varios',    descKey: 'DescReparaciones',   detailKey: 'DetallesReparaciones', detailType: 'generica' },
        { key: 'Movilidad',      label: 'Movilidad',                descKey: 'DescMovilidad',      detailKey: '',                     detailType: '' },
        { key: 'Encarpada',      label: 'Encarpada / Desencarpada', descKey: 'DescEncarpada',      detailKey: '',                     detailType: '' },
        { key: 'Hospedaje',      label: 'Hospedaje',                descKey: 'DescHospedaje',      detailKey: 'DetallesHospedaje',    detailType: 'generica' },
        { key: 'Combustible',    label: 'Combustible',              descKey: 'DescCombustible',    detailKey: 'DetallesCombustible',  detailType: 'generica' }
    ];

    var gastosHtml = '';
    gastosItems.forEach(function (item) {
        var soles = parseFloat(datos['Gastos' + item.key + 'Soles'] || 0);
        var dolares = parseFloat(datos['Gastos' + item.key + 'Dolares'] || 0);
        if (soles > 0 || dolares > 0) {
            var desc = datos[item.descKey] || '';
            var subTable = '';
            if (item.detailKey && datos[item.detailKey] && datos[item.detailKey].length > 0) {
                subTable = item.detailType === 'peajes'
                    ? buildSubTablePeajes(datos[item.detailKey])
                    : buildSubTableGenerica(datos[item.detailKey]);
            }
            gastosHtml += makeRow('fa-minus-circle', 'text-danger', item.label, soles, dolares, desc, subTable);
        }
    });

    if (datos.DetallesGastosAdicionales && datos.DetallesGastosAdicionales.length > 0) {
        datos.DetallesGastosAdicionales.forEach(function (item) {
            var nombre = item.Nombre || 'Gasto Adicional';
            gastosHtml += makeRow('fa-minus-circle', 'text-danger', nombre,
                item.Soles, item.Dolares, item.Descripcion || '', '');
        });
    }

    if (!gastosHtml) {
        gastosHtml = '<tr><td colspan="3" class="text-center text-muted">No hay gastos registrados</td></tr>';
    }
    $('#detalleGastosBody').html(gastosHtml);

    // --- Ajustes Administrativos: pre-poblar inputs ---
    var descSoles = parseFloat(datos.DescuentoSoles || 0);
    var descDolares = parseFloat(datos.DescuentoDolares || 0);
    var reintSoles = parseFloat(datos.ReintegroSoles || 0);
    var reintDolares = parseFloat(datos.ReintegroDolares || 0);

    $('#ajusteDescuentoSoles').val(descSoles > 0 ? descSoles.toFixed(2) : '');
    $('#ajusteDescuentoDolares').val(descDolares > 0 ? descDolares.toFixed(2) : '');
    $('#ajusteReintegroSoles').val(reintSoles > 0 ? reintSoles.toFixed(2) : '');
    $('#ajusteReintegroDolares').val(reintDolares > 0 ? reintDolares.toFixed(2) : '');
    $('#ajusteMotivoDescuento').val(datos.DescuentoMotivo || '');
    $('#ajusteMotivoReintegro').val(datos.ReintegroMotivo || '');

    // Desacuerdo del conductor con una revisión anterior: lo primero que hay que atender.
    if (datos.ObservacionConductor) {
        $('#detalleObservacionConductor').text(datos.ObservacionConductor);
        $('#detalleFechaObservacion').text(datos.FechaObservacionConductor ? ' el ' + datos.FechaObservacionConductor : '');
        $('#detalleObservacionConductorRow').show();
    } else {
        $('#detalleObservacionConductorRow').hide();
    }

    // --- Dinero entregado al conductor: pre-poblar y separar lo que no se edita aquí ---
    // (ingresos adicionales de liquidaciones antiguas, que siguen sumando al balance).
    var principalS = 0, principalD = 0;
    $('.tabla-entregado tr[data-ingreso]').each(function () {
        var k = $(this).data('ingreso');
        var s = parseFloat(datos[k + 'Soles'] || 0);
        var d = parseFloat(datos[k + 'Dolares'] || 0);
        var descKey = { Despacho: 'DescDespacho', Mensualidad: 'DescMensualidad', Prestamo: 'DescPrestamo', OtrosIngresos: 'DescOtros' }[k];
        principalS += s; principalD += d;
        $(this).find('.ing-soles').val(s > 0 ? s.toFixed(2) : '');
        $(this).find('.ing-dolares').val(d > 0 ? d.toFixed(2) : '');
        $(this).find('.ing-desc').val(datos[descKey] || '');
    });
    _ingresosFijosSoles = parseFloat(datos.TotalIngresosSoles || 0) - principalS;
    _ingresosFijosDolares = parseFloat(datos.TotalIngresosDolares || 0) - principalD;
    _gastosSoles = parseFloat(datos.TotalGastosSoles || 0);
    _gastosDolares = parseFloat(datos.TotalGastosDolares || 0);
    actualizarBalanceAjustado();

    if (modoAprobacion) cargarMovimientosRevision(datos.IdOrdenViaje);

    $('#detalleLoading').hide();
    $('#modalDetalleLiquidacion .detail-section').show();
    $('#sectionAjustes').toggle(modoAprobacion);
    $('#approvalBanner').toggle(modoAprobacion);
    $('#modalModeBadge').toggle(modoAprobacion);
    $('#modalDetalleLiquidacionHeader').toggleClass('mode-aprobacion', modoAprobacion);

    if (modoAprobacion) {
        $('#btnAprobarDesdeModal').show();
        // Scroll modal to top so admin sees the guidance banner first
        setTimeout(function () {
            $('#modalDetalleLiquidacion .modal-body').scrollTop(0);
        }, 50);
    }
}

function scrollToAjustes() {
    var $body = $('#modalDetalleLiquidacion .modal-body');
    var $target = $('#sectionAjustes');
    if ($body.length && $target.length) {
        $body.animate({ scrollTop: $target.position().top + $body.scrollTop() - 16 }, 350);
    }
}

function abrirModalRechazar(idOrden, numeroOrden) {
    $('#rechazarIdOrden').val(idOrden);
    $('#rechazarNumeroOrden').val(numeroOrden);
    $('#rechazarObservaciones').val('');
    $('#hfIdOrdenSeleccionada').val(idOrden);
    $('#modalRechazar').modal('show');
}

function validarRechazo() {
    const observaciones = $('#rechazarObservaciones').val().trim();
    limpiarErroresFormulario();

    if (!observaciones) {
        mostrarErrorCampo('#rechazarObservaciones', '#errorRechazoObservaciones', 'Debe ingresar el motivo del rechazo.');
        $('#rechazarObservaciones').focus();
        return false;
    }

    if (observaciones.length < 10) {
        mostrarErrorCampo('#rechazarObservaciones', '#errorRechazoObservaciones', 'El motivo del rechazo debe tener al menos 10 caracteres.');
        $('#rechazarObservaciones').focus();
        return false;
    }

    if (observaciones.length > 500) {
        mostrarErrorCampo('#rechazarObservaciones', '#errorRechazoObservaciones', 'El motivo del rechazo no puede superar 500 caracteres.');
        $('#rechazarObservaciones').focus();
        return false;
    }

    $('[name=observacionesRechazo]').remove();
    $('<input>').attr({
        type: 'hidden',
        name: 'observacionesRechazo',
        value: observaciones
    }).appendTo('form');

    // OnClientClick="return validarRechazo();": se frena el envío mientras se muestra el modal y, al
    // aceptar, se repite el clic dejándolo pasar una vez.
    if (rechazoConfirmado) {
        rechazoConfirmado = false;
        return true;
    }
    SGV.confirmar('¿Está seguro de RECHAZAR esta liquidación?\n\nEl viaje se reabrirá para correcciones.',
        function () {
            rechazoConfirmado = true;
            document.querySelector('[id$="btnConfirmarRechazo"]').click();
        },
        { titulo: 'Rechazar liquidación', textoAceptar: 'Rechazar', peligro: true });
    return false;
}

var rechazoConfirmado = false;

// Imprime el documento oficial (PDF SGV-CDF-F-05, el mismo que se archiva al firmar y aprobar) en
// lugar de una copia HTML: así lleva la firma del conductor, los ajustes y la paginación reales.
function imprimirDetalle() {
    descargarPdfOrdenViaje(_idOrdenDetalleActual);
}

function abrirModalAprobar(idOrdenViaje) {
    modoAprobacion = true;
    idOrdenParaAprobar = idOrdenViaje;
    verDetalleLiquidacion(idOrdenViaje);
}

function actualizarBalanceAjustado() {
    var descS = parseFloat($('#ajusteDescuentoSoles').val()) || 0;
    var descD = parseFloat($('#ajusteDescuentoDolares').val()) || 0;
    var reintS = parseFloat($('#ajusteReintegroSoles').val()) || 0;
    var reintD = parseFloat($('#ajusteReintegroDolares').val()) || 0;
    var ent = leerIngresosEntregados();

    // En aprobación el balance se recalcula con lo entregado que se está registrando.
    if (modoAprobacion || descS > 0 || descD > 0 || reintS > 0 || reintD > 0) {
        var baseS = modoAprobacion ? (_ingresosFijosSoles + ent.totalSoles - _gastosSoles) : _balanceSoles;
        var baseD = modoAprobacion ? (_ingresosFijosDolares + ent.totalDolares - _gastosDolares) : _balanceDolares;
        // Saldo: negativo = la empresa paga al conductor; positivo = él devuelve.
        var balS = baseS + descS - reintS;
        var balD = baseD + descD - reintD;
        $('#balanceAjustadoSoles').text(balS.toFixed(2))
            .toggleClass('sgv-negativo', balS < 0).toggleClass('sgv-positivo', balS > 0);
        $('#balanceAjustadoDolares').text(balD.toFixed(2))
            .toggleClass('sgv-negativo', balD < 0).toggleClass('sgv-positivo', balD > 0);
        $('#balanceAjustadoPanel').show();
    } else {
        $('#balanceAjustadoPanel').hide();
    }
}

// Dinero entregado al conductor, tal como lo espera el WebMethod (IngresosEntregados).
function leerIngresosEntregados() {
    var r = { totalSoles: 0, totalDolares: 0, negativo: false, datos: {} };
    $('.tabla-entregado tr[data-ingreso]').each(function () {
        var k = $(this).data('ingreso');
        var campo = k === 'OtrosIngresos' ? 'Otros' : k;
        var s = parseFloat($(this).find('.ing-soles').val()) || 0;
        var d = parseFloat($(this).find('.ing-dolares').val()) || 0;
        if (s < 0 || d < 0) r.negativo = true;
        r.totalSoles += s; r.totalDolares += d;
        r.datos[campo + 'Soles'] = s;
        r.datos[campo + 'Dolares'] = d;
        r.datos['Desc' + campo] = $(this).find('.ing-desc').val().trim() || null;
    });
    return r;
}

function toggleCorregirSalida() {
    $('#errorCorregirSalidaMotivo').hide();
    $('#panelCorregirSalida').slideToggle(150);
}

function corregirSalidaDesdeModal() {
    if (!_idOrdenDetalleActual) return;

    var fecha = $('#corregirSalidaFecha').val();
    var hora = $('#corregirSalidaHora').val();
    var motivo = $('#corregirSalidaMotivo').val().trim();

    $('#errorCorregirSalidaMotivo').hide();

    if (!fecha || !hora) {
        mostrarErrorCampo('#corregirSalidaMotivo', '#errorCorregirSalidaMotivo', 'Debe indicar la fecha y la hora de salida.');
        return;
    }
    if (motivo.length < 10) {
        mostrarErrorCampo('#corregirSalidaMotivo', '#errorCorregirSalidaMotivo', 'El motivo debe tener al menos 10 caracteres.');
        return;
    }
    if (motivo.length > 500) {
        mostrarErrorCampo('#corregirSalidaMotivo', '#errorCorregirSalidaMotivo', 'El motivo no puede superar 500 caracteres.');
        return;
    }

    SGV.confirmar('¿Confirma corregir la fecha/hora de salida de esta liquidación?\n\nEl cambio quedará auditado.',
        function () { enviarCorreccionSalida(fecha, hora, motivo); },
        { titulo: 'Corregir salida', textoAceptar: 'Corregir' });
}

function enviarCorreccionSalida(fecha, hora, motivo) {
    var $btn = $('#btnGuardarCorregirSalida');
    $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-1"></i>Guardando...');

    $.ajax({
        type: "POST",
        url: "LiquidacionesPendientes.aspx/CorregirSalidaPendiente",
        data: JSON.stringify({ idOrdenViaje: _idOrdenDetalleActual, fechaSalida: fecha, horaSalida: hora, motivo: motivo }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            var result = response.d;
            if (result.success) {
                $('#modalDetalleLiquidacion').modal('hide');
                SGV.avisar('✅ ' + result.message, { trasRecargar: true });
                location.reload();
            } else {
                SGV.avisar('❌ ' + result.message);
                $btn.prop('disabled', false).html('<i class="fas fa-save mr-1"></i>Guardar corrección');
            }
        },
        error: function (xhr) {
            console.error('Error:', xhr.responseText);
            SGV.avisar('Error al corregir la salida. Por favor intente de nuevo.');
            $btn.prop('disabled', false).html('<i class="fas fa-save mr-1"></i>Guardar corrección');
        }
    });
}

var MINIMO_MOTIVO = 10;

// ============================================================
// === Entregas y pagos directos registrados durante el viaje ===
// ============================================================

// null = la revisión no cargó movimientos (no se tocan los ya unidos a la liquidación).
var _movimientosRevision = null;

function cargarMovimientosRevision(idOrden) {
    _movimientosRevision = null;
    $('#panelMovimientos').hide();
    $('#tbodyMovimientos').html('');
    $.ajax({
        type: "POST",
        url: "LiquidacionesPendientes.aspx/ObtenerMovimientosParaRevision",
        data: JSON.stringify({ idOrdenViaje: idOrden }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            var r = response.d;
            if (!r || !r.success) return;
            _movimientosRevision = r.movimientos || [];
            if (_movimientosRevision.length === 0) return;

            var html = '';
            _movimientosRevision.forEach(function (m) {
                var esPago = m.Tipo === 'PAGO_DIRECTO';
                var sub = [];
                if (m.NombrePortador) sub.push('lleva: ' + m.NombrePortador);
                if (m.NumeroOperacion) sub.push('op. ' + m.NumeroOperacion);
                if (esPago && m.NombreProveedor) sub.push(m.NombreProveedor + (m.Comprobante ? ' · ' + m.Comprobante : ''));
                if (m.Descripcion) sub.push(m.Descripcion);
                html += '<tr class="' + (esPago ? 'mov-pago' : '') + '">';
                html += '<td><input type="checkbox" class="chk-mov" data-id="' + m.IdMovimiento + '" checked onchange="aplicarEntregasALaTabla()"></td>';
                html += '<td class="text-nowrap">' + htmlEncode(m.Fecha) + '</td>';
                html += '<td>' + (esPago ? 'Pago directo' : 'Entrega') + '</td>';
                html += '<td>' + htmlEncode(m.Concepto) + (sub.length ? '<span class="mov-sub">' + htmlEncode(sub.join(' · ')) + '</span>' : '') + '</td>';
                html += '<td>' + htmlEncode(m.Medio) + '</td>';
                html += '<td class="text-right">' + (m.MontoSoles ? parseFloat(m.MontoSoles).toFixed(2) : '—') + '</td>';
                html += '<td class="text-right">' + (m.MontoDolares ? parseFloat(m.MontoDolares).toFixed(2) : '—') + '</td>';
                html += '</tr>';
            });
            $('#tbodyMovimientos').html(html);
            $('#panelMovimientos').show();
            aplicarEntregasALaTabla();
        }
    });
}

function idsMovimientosMarcados() {
    if (_movimientosRevision === null) return null;
    var ids = [];
    $('#tbodyMovimientos .chk-mov:checked').each(function () { ids.push(parseInt($(this).data('id'), 10)); });
    return ids;
}

// Las entregas marcadas llenan la tabla "Dinero entregado" (adelanto y segundo despacho van a Adelanto).
function aplicarEntregasALaTabla() {
    if (!_movimientosRevision) return;
    var marcados = idsMovimientosMarcados();
    var filas = { Despacho: { s: 0, d: 0, det: [] }, Mensualidad: { s: 0, d: 0, det: [] }, Prestamo: { s: 0, d: 0, det: [] }, OtrosIngresos: { s: 0, d: 0, det: [] } };
    var hayEntregas = false;
    _movimientosRevision.forEach(function (m) {
        if (m.Tipo !== 'ENTREGA' || marcados.indexOf(m.IdMovimiento) < 0) return;
        hayEntregas = true;
        var k = m.ConceptoCodigo === 'MENSUALIDAD' ? 'Mensualidad' : m.ConceptoCodigo === 'PRESTAMO' ? 'Prestamo'
              : m.ConceptoCodigo === 'OTROS' ? 'OtrosIngresos' : 'Despacho';
        filas[k].s += parseFloat(m.MontoSoles || 0);
        filas[k].d += parseFloat(m.MontoDolares || 0);
        filas[k].det.push(m.Fecha.substr(0, 5) + ' ' + m.Medio.toLowerCase() + (m.NombrePortador ? ' (' + m.NombrePortador + ')' : ''));
    });
    if (!hayEntregas && _movimientosRevision.every(function (m) { return m.Tipo !== 'ENTREGA'; })) return;
    $('.tabla-entregado tr[data-ingreso]').each(function () {
        var f = filas[$(this).data('ingreso')];
        $(this).find('.ing-soles').val(f.s > 0 ? f.s.toFixed(2) : '');
        $(this).find('.ing-dolares').val(f.d > 0 ? f.d.toFixed(2) : '');
        $(this).find('.ing-desc').val(f.det.join('; ').substr(0, 250));
    });
    actualizarBalanceAjustado();
}

// Un descuento o reintegro sin motivo no se puede enviar: el conductor lo verá en la liquidación
// final antes de firmar su conformidad y es lo que sustenta el ajuste ante una auditoría.
function validarMotivoAjuste(hayMonto, selector, selectorError, tipo) {
    var motivo = $(selector).val().trim();
    $(selector).removeClass('campo-con-error');
    $(selectorError).addClass('d-none').text('');
    if (hayMonto && motivo.length < MINIMO_MOTIVO) {
        $(selector).addClass('campo-con-error');
        $(selectorError).removeClass('d-none')
            .text('Indique el motivo del ' + tipo + ' (al menos ' + MINIMO_MOTIVO + ' caracteres).');
        return false;
    }
    return true;
}

function aprobarDesdeModal() {
    if (!idOrdenParaAprobar) return;
    limpiarErroresFormulario();

    var ajustes = {
        DescuentoSoles: parseFloat($('#ajusteDescuentoSoles').val()) || 0,
        DescuentoDolares: parseFloat($('#ajusteDescuentoDolares').val()) || 0,
        MotivoDescuento: $('#ajusteMotivoDescuento').val().trim(),
        ReintegroSoles: parseFloat($('#ajusteReintegroSoles').val()) || 0,
        ReintegroDolares: parseFloat($('#ajusteReintegroDolares').val()) || 0,
        MotivoReintegro: $('#ajusteMotivoReintegro').val().trim()
    };
    var hayDescuento = ajustes.DescuentoSoles > 0 || ajustes.DescuentoDolares > 0;
    var hayReintegro = ajustes.ReintegroSoles > 0 || ajustes.ReintegroDolares > 0;
    var nota = $('#notaAprobacion').val().trim();

    if (ajustes.DescuentoSoles < 0 || ajustes.DescuentoDolares < 0 || ajustes.ReintegroSoles < 0 || ajustes.ReintegroDolares < 0) {
        SGV.avisar('⚠️ Los montos de ajuste no pueden ser negativos.');
        return;
    }

    var okDesc = validarMotivoAjuste(hayDescuento, '#ajusteMotivoDescuento', '#errorMotivoDescuento', 'descuento');
    var okReint = validarMotivoAjuste(hayReintegro, '#ajusteMotivoReintegro', '#errorMotivoReintegro', 'reintegro');
    if (!okDesc || !okReint) {
        scrollToAjustes();
        return;
    }
    if (!hayDescuento) ajustes.MotivoDescuento = null;
    if (!hayReintegro) ajustes.MotivoReintegro = null;

    var ent = leerIngresosEntregados();
    if (ent.negativo) {
        SGV.avisar('⚠️ Los montos entregados al conductor no pueden ser negativos.');
        return;
    }

    if (nota.length > 500) {
        SGV.avisar('La nota de aprobación no puede superar 500 caracteres.');
        $('#notaAprobacion').focus();
        return;
    }

    var confirmMsg = '¿Aprobar esta liquidación y enviarla al conductor?\n\n';
    if (ent.totalSoles > 0 || ent.totalDolares > 0) {
        confirmMsg += 'Entregado al conductor: S/ ' + ent.totalSoles.toFixed(2) + ' · $ ' + ent.totalDolares.toFixed(2) + '\n\n';
    } else {
        confirmMsg += 'No se registra dinero entregado al conductor.\n\n';
    }
    if (hayDescuento || hayReintegro) {
        confirmMsg += 'Ajustes:\n';
        if (hayDescuento) confirmMsg += '  • Descuento S/ ' + ajustes.DescuentoSoles.toFixed(2) + ' · $ ' + ajustes.DescuentoDolares.toFixed(2) + ': ' + ajustes.MotivoDescuento + '\n';
        if (hayReintegro) confirmMsg += '  • Reintegro S/ ' + ajustes.ReintegroSoles.toFixed(2) + ' · $ ' + ajustes.ReintegroDolares.toFixed(2) + ': ' + ajustes.MotivoReintegro + '\n';
        confirmMsg += '\n';
    }
    if (nota) confirmMsg += 'Nota: ' + nota + '\n\n';
    confirmMsg += 'Se registrará su firma de revisión y el conductor recibirá la liquidación final para firmar su conformidad. ' +
                  'El viaje se completará cuando él firme.';

    SGV.confirmar(confirmMsg,
        function () { enviarAprobacion(ajustes, nota, ent.datos); },
        { titulo: 'Aprobar y enviar al conductor', textoAceptar: 'Aprobar y enviar' });
}

function enviarAprobacion(ajustes, nota, ingresos) {
    var $btn = $('#btnAprobarDesdeModal');
    $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-1"></i>Enviando...');

    $.ajax({
        type: "POST",
        url: "LiquidacionesPendientes.aspx/EnviarAConformidad",
        data: JSON.stringify({
            idOrdenViaje: idOrdenParaAprobar,
            ajustes: ajustes,
            notaAprobacion: nota || null,
            ingresos: ingresos || null,
            idsMovimientos: idsMovimientosMarcados()
        }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            var result = response.d;
            if (result.success) {
                $('#modalDetalleLiquidacion').modal('hide');
                SGV.avisar('✅ ' + result.message, { trasRecargar: true });
                location.reload();
            } else {
                SGV.avisar('❌ ' + result.message);
                $btn.prop('disabled', false).html('<i class="fas fa-paper-plane mr-1"></i>Aprobar y enviar al conductor');
            }
        },
        error: function (xhr) {
            console.error('Error:', xhr.responseText);
            SGV.avisar('Error al aprobar la liquidación. Por favor intente de nuevo.');
            $btn.prop('disabled', false).html('<i class="fas fa-paper-plane mr-1"></i>Aprobar y enviar al conductor');
        }
    });
}

$(document).ready(function () {
    console.log('LiquidacionesPendientes cargado');
    $('#modalDetalleLiquidacion').on('hidden.bs.modal', function () {
        modoAprobacion = false;
        idOrdenParaAprobar = 0;
        $('#btnAprobarDesdeModal').hide();
        $('#approvalBanner').hide();
        $('#modalModeBadge').hide();
        $('#modalDetalleLiquidacionHeader').removeClass('mode-aprobacion');
        $('#notaAprobacion').val('');
        $('#ajusteDescuentoSoles, #ajusteDescuentoDolares, #ajusteReintegroSoles, #ajusteReintegroDolares').val('');
        $('#ajusteMotivoDescuento, #ajusteMotivoReintegro').val('').removeClass('campo-con-error');
        $('#errorMotivoDescuento, #errorMotivoReintegro').addClass('d-none').text('');
        $('#balanceAjustadoPanel').hide();
    });
});

// ============================================================
// === SECCIÓN CONTROL DE APROBADAS ===
// ============================================================

var _aprobadasData = {};
var _aprobadasCargadas = false;

// Tras revertir una aprobación la página se recarga; volver a la pestaña de aprobadas.
$(function () {
    var tab = null;
    try { tab = sessionStorage.getItem('sgvLiqTab'); sessionStorage.removeItem('sgvLiqTab'); } catch (e) { }
    if (tab === 'aprobadas') cambiarTab('aprobadas');
});

function cambiarTab(tab) {
    $('#seccionPendientes').toggle(tab === 'pendientes');
    $('#seccionPorConformar').toggle(tab === 'porConformar');
    $('#seccionAprobadas').toggle(tab === 'aprobadas');
    $('#tabPendientes').toggleClass('activo', tab === 'pendientes');
    $('#tabPorConformar').toggleClass('activo', tab === 'porConformar');
    $('#tabAprobadas').toggleClass('activo', tab === 'aprobadas');

    if (tab === 'porConformar') {
        cargarLiquidacionesPorConformar();
    } else if (tab === 'aprobadas' && !_aprobadasCargadas) {
        inicializarFiltrosAprobadas();
        cargarLiquidacionesAprobadas();
        _aprobadasCargadas = true;
    }
}

// ============================================================
// === SECCIÓN ESPERANDO CONFORMIDAD DEL CONDUCTOR ===
// ============================================================

var _porConformarData = {};

// El contador de la pestaña se carga al abrir la página.
$(function () { cargarLiquidacionesPorConformar(true); });

function cargarLiquidacionesPorConformar(soloContador) {
    $.ajax({
        type: "POST",
        url: "LiquidacionesPendientes.aspx/ObtenerLiquidacionesPorConformar",
        data: JSON.stringify({ idConductor: 0 }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            var datos = response.d || [];
            $('#lblTotalPorConformar').text(datos.length > 0 ? datos.length : '');
            $('#lblResultadosPorConformar').text(datos.length);
            if (soloContador) return;

            _porConformarData = {};
            if (datos.length === 0) {
                $('#tbodyPorConformar').html('<tr><td colspan="7" class="sgv-vacio">No hay liquidaciones esperando la conformidad del conductor.</td></tr>');
                return;
            }

            var html = '';
            datos.forEach(function (item) {
                _porConformarData[item.IdOrdenViaje] = item;
                var espera = item.HorasEsperando >= 48 ? 'sgv-estado-peligro' : item.HorasEsperando >= 24 ? 'sgv-estado-alerta' : 'sgv-estado-ok';
                html += '<tr>';
                html += '<td><div class="orden-numero">' + htmlEncode(item.NumeroOrdenViaje) + '</div>';
                html += '<div class="sgv-suave">' + htmlEncode(item.FechaSalida) + ' – ' + htmlEncode(item.FechaLlegada) + '</div></td>';
                html += '<td><div>' + htmlEncode(item.NombreConductor) + '</div>';
                html += '<div class="sgv-suave">' + htmlEncode(item.PlacaTracto) + ' / ' + htmlEncode(item.PlacaCarreta) + '</div></td>';
                html += '<td class="text-nowrap"><div>' + htmlEncode(item.FechaEnvio) + '</div>';
                html += '<span class="sgv-estado ' + espera + '">' + (item.HorasEsperando < 1 ? 'hace menos de 1 h' : 'hace ' + item.HorasEsperando + ' h') + '</span></td>';
                html += celdaMontos(parseFloat(item.DescuentoSoles || 0), parseFloat(item.DescuentoDolares || 0), 'sgv-negativo');
                html += celdaMontos(parseFloat(item.ReintegroSoles || 0), parseFloat(item.ReintegroDolares || 0), 'sgv-positivo');
                html += celdaMontos(parseFloat(item.SaldoSoles || 0), parseFloat(item.SaldoDolares || 0));
                html += '<td class="text-right"><div class="sgv-acciones">';
                html += '<button type="button" class="sgv-btn-icono info" onclick="verDetalleLiquidacion(' + item.IdOrdenViaje + ')" title="Ver detalle"><i class="fas fa-eye"></i></button>';
                html += '<button type="button" class="sgv-btn-icono" onclick="descargarPdfOrdenViaje(' + item.IdOrdenViaje + ')" title="PDF de la liquidación enviada"><i class="fas fa-file-pdf"></i></button>';
                html += '<button type="button" class="sgv-btn-icono alerta" onclick="abrirModalRetirarEnvio(' + item.IdOrdenViaje + ')" title="Retirar envío (para corregir antes de que firme)"><i class="fas fa-undo"></i></button>';
                html += '</div></td></tr>';
            });
            $('#tbodyPorConformar').html(html);
        },
        error: function (xhr) {
            console.error('Error cargando por conformar:', xhr.responseText);
            if (!soloContador)
                $('#tbodyPorConformar').html('<tr><td colspan="7" class="sgv-vacio text-danger">Error al cargar las liquidaciones.</td></tr>');
        }
    });
}

function abrirModalRetirarEnvio(idOrden) {
    var item = _porConformarData[idOrden];
    if (!item) { SGV.avisar('No se encontraron datos de la liquidación.'); return; }
    $('#retirarIdOrden').val(idOrden);
    $('#retirarNumeroOrden').val(item.NumeroOrdenViaje);
    $('#retirarMotivo').val('');
    $('#errorRetirarMotivo').addClass('d-none').text('');
    $('#modalRetirarEnvio').modal('show');
}

function confirmarRetiroEnvio() {
    var idOrden = parseInt($('#retirarIdOrden').val());
    var motivo = $('#retirarMotivo').val().trim();
    limpiarErroresFormulario();
    if (motivo.length < MINIMO_MOTIVO) {
        mostrarErrorCampo('#retirarMotivo', '#errorRetirarMotivo', 'El motivo debe tener al menos ' + MINIMO_MOTIVO + ' caracteres.');
        $('#retirarMotivo').focus();
        return;
    }

    var $btn = $('#btnConfirmarRetiro');
    $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-1"></i>Procesando...');
    $.ajax({
        type: "POST",
        url: "LiquidacionesPendientes.aspx/RetirarEnvioConformidad",
        data: JSON.stringify({ idOrdenViaje: idOrden, motivo: motivo }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            var r = response.d;
            $btn.prop('disabled', false).html('<i class="fas fa-undo mr-1"></i>Retirar envío');
            if (r.success) {
                $('#modalRetirarEnvio').modal('hide');
                SGV.avisar('✅ ' + r.message, { trasRecargar: true });
                location.reload();
            } else {
                SGV.avisar('❌ ' + r.message);
            }
        },
        error: function (xhr) {
            console.error('Error:', xhr.responseText);
            $btn.prop('disabled', false).html('<i class="fas fa-undo mr-1"></i>Retirar envío');
            SGV.avisar('Error al retirar el envío.');
        }
    });
}

function inicializarFiltrosAprobadas() {
    var hoy = new Date();
    var hace30 = new Date(hoy);
    hace30.setDate(hace30.getDate() - 30);
    $('#filtroDesdeAprobadas').val(hace30.toISOString().substr(0, 10));
    $('#filtroHastaAprobadas').val(hoy.toISOString().substr(0, 10));
}

function limpiarFiltrosAprobadas() {
    $('#txtConductorAprobadas').val('');
    $('#filtroCondAprobadasId').val('');
    $('#filtroOrdenAprobadas').val('');
    var hoy = new Date();
    var hace30 = new Date(hoy);
    hace30.setDate(hace30.getDate() - 30);
    $('#filtroDesdeAprobadas').val(hace30.toISOString().substr(0, 10));
    $('#filtroHastaAprobadas').val(hoy.toISOString().substr(0, 10));
    cargarLiquidacionesAprobadas();
}

function cargarLiquidacionesAprobadas() {
    limpiarErroresFormulario();
    if (!validarFiltrosAprobadasCliente()) return;

    var filtros = {
        idConductor: parseInt($('#filtroCondAprobadasId').val()) || 0,
        fechaDesde: $('#filtroDesdeAprobadas').val() || '',
        fechaHasta: $('#filtroHastaAprobadas').val() || '',
        numeroOrden: $('#filtroOrdenAprobadas').val() || ''
    };

    $('#loadingAprobadas').show();
    $('#tbodyAprobadas').html('');

    $.ajax({
        type: "POST",
        url: "LiquidacionesPendientes.aspx/ObtenerLiquidacionesAprobadas",
        data: JSON.stringify(filtros),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            $('#loadingAprobadas').hide();
            var datos = response.d;
            if (!datos || datos.length === 0) {
                $('#tbodyAprobadas').html(
                    '<tr><td colspan="7" class="sgv-vacio">' +
                    'No se encontraron liquidaciones aprobadas en el rango seleccionado.</td></tr>'
                );
                $('#lblTotalAprobadas').text('0');
                return;
            }

            _aprobadasData = {};
            $('#lblTotalAprobadas').text(datos.length);
            var html = '';

            datos.forEach(function (item) {
                _aprobadasData[item.IdOrdenViaje] = item;

                html += '<tr>';

                // N° Orden
                html += '<td><div class="orden-numero">' + htmlEncode(item.NumeroOrdenViaje) + '</div>';
                html += '<div class="sgv-suave">' + htmlEncode(item.FechaSalida) + '</div></td>';
                if (item.FechaConformidad) html += '<div class="sgv-suave" title="Conformidad firmada por el conductor"><i class="fas fa-signature mr-1"></i>' + htmlEncode(item.FechaConformidad) + '</div>';

                // Conductor
                html += '<td><div>' + htmlEncode(item.NombreConductor) + '</div>';
                html += '<div class="sgv-suave">' + htmlEncode(item.PlacaTracto) + ' / ' + htmlEncode(item.PlacaCarreta) + '</div></td>';

                // Periodo
                html += '<td class="text-nowrap">' + htmlEncode(item.FechaSalida) + ' – ' + htmlEncode(item.FechaLlegada) + '</td>';

                // Descuento en rojo, reintegro en verde, balance según su signo (S/ arriba, $ abajo)
                html += celdaMontos(parseFloat(item.DescuentoSoles || 0), parseFloat(item.DescuentoDolares || 0), 'sgv-negativo');
                html += celdaMontos(parseFloat(item.ReintegroSoles || 0), parseFloat(item.ReintegroDolares || 0), 'sgv-positivo');
                html += celdaMontos(parseFloat(item.BalanceSoles || 0), parseFloat(item.BalanceDolares || 0));

                // Acciones
                html += '<td class="text-right"><div class="sgv-acciones">';
                html += '<button type="button" class="sgv-btn-icono info" onclick="verDetalleLiquidacion(' + item.IdOrdenViaje + ')" title="Ver detalle"><i class="fas fa-eye"></i></button>';
                html += '<button type="button" class="sgv-btn-icono" onclick="descargarPdfOrdenViaje(' + item.IdOrdenViaje + ')" title="Descargar PDF SGV-CDF-F-05"><i class="fas fa-file-pdf"></i></button>';
                html += '<button type="button" class="sgv-btn-icono peligro" onclick="abrirModalRevertir(' + item.IdOrdenViaje + ')" title="Revertir aprobación"><i class="fas fa-undo"></i></button>';
                html += '</div></td>';

                html += '</tr>';
            });

            $('#tbodyAprobadas').html(html);
        },
        error: function (xhr) {
            $('#loadingAprobadas').hide();
            console.error('Error cargando aprobadas:', xhr.responseText);
            $('#tbodyAprobadas').html(
                '<tr><td colspan="7" class="sgv-vacio text-danger">' +
                'Error al cargar las liquidaciones aprobadas</td></tr>'
            );
        }
    });
}

// Celda con S/ y $ alineados a la derecha. claseFija: color fijo de un ajuste ("—" si ambos son 0);
// sin claseFija, cada monto se colorea según su signo.
function celdaMontos(soles, dolares, claseFija) {
    if (claseFija && soles === 0 && dolares === 0) {
        return '<td class="sgv-num sgv-suave">—</td>';
    }
    function monto(simbolo, valor) {
        var clase = claseFija || (valor > 0 ? 'sgv-positivo' : valor < 0 ? 'sgv-negativo' : '');
        return '<div' + (clase ? ' class="' + clase + '"' : '') + '>' + simbolo + ' ' + valor.toFixed(2) + '</div>';
    }
    return '<td class="sgv-num">' + monto('S/', soles) + monto('$', dolares) + '</td>';
}

function htmlEncode(text) {
    if (!text) return '';
    var div = document.createElement('div');
    div.appendChild(document.createTextNode(text));
    return div.innerHTML;
}

// --- Descargar PDF SGV-CDF-F-05 ---
function descargarPdfOrdenViaje(idOrden) {
    if (!idOrden) return;
    $.ajax({
        type: "POST",
        url: "LiquidacionesPendientes.aspx/ObtenerUrlPdfOrdenViaje",
        data: JSON.stringify({ idOrdenViaje: idOrden }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            var r = response.d;
            if (r && r.success && r.url) {
                // Abrir en pestaña nueva (vista previa). Para forzar descarga: &download=1
                window.open(r.url, '_blank');
            } else {
                SGV.avisar('\u274c ' + (r && r.message ? r.message : 'No se pudo obtener el PDF.'));
            }
        },
        error: function (xhr) {
            console.error('Error PDF:', xhr.responseText);
            SGV.avisar('Error al solicitar el PDF.');
        }
    });
}

// --- Modal Revertir ---
function abrirModalRevertir(idOrden) {
    var item = _aprobadasData[idOrden];
    if (!item) { SGV.avisar('No se encontraron datos de la liquidaci\u00f3n.'); return; }
    $('#revertirIdOrden').val(idOrden);
    $('#revertirNumeroOrden').val(item.NumeroOrdenViaje);
    $('#revertirConductor').val(item.NombreConductor);
    $('#revertirMotivo').val('');
    $('#modalRevertir').modal('show');
}

function confirmarReversion() {
    var idOrden = parseInt($('#revertirIdOrden').val());
    var motivo = $('#revertirMotivo').val().trim();
    limpiarErroresFormulario();

    if (!motivo) {
        mostrarErrorCampo('#revertirMotivo', '#errorRevertirMotivo', 'Debe ingresar el motivo de la reversión.');
        $('#revertirMotivo').focus();
        return;
    }
    if (motivo.length < 10) {
        mostrarErrorCampo('#revertirMotivo', '#errorRevertirMotivo', 'El motivo debe tener al menos 10 caracteres.');
        $('#revertirMotivo').focus();
        return;
    }
    if (motivo.length > 500) {
        mostrarErrorCampo('#revertirMotivo', '#errorRevertirMotivo', 'El motivo no puede superar 500 caracteres.');
        $('#revertirMotivo').focus();
        return;
    }

    SGV.confirmar('\u00bfEst\u00e1 seguro de REVERTIR esta aprobaci\u00f3n?\n\nLa liquidaci\u00f3n volver\u00e1 al estado PENDIENTE y deber\u00e1 ser revisada nuevamente.',
        function () { enviarReversion(idOrden, motivo); },
        { titulo: 'Revertir aprobaci\u00f3n', textoAceptar: 'Revertir', peligro: true });
}

function enviarReversion(idOrden, motivo) {
    var $btn = $('#btnConfirmarReversion');
    $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-1"></i>Procesando...');

    $.ajax({
        type: "POST",
        url: "LiquidacionesPendientes.aspx/RevertirAprobacion",
        data: JSON.stringify({ idOrdenViaje: idOrden, motivo: motivo }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            var result = response.d;
            if (result.success) {
                $('#modalRevertir').modal('hide');
                SGV.avisar('\u2705 ' + result.message, { trasRecargar: true });
                // La orden vuelve a pendientes: recargar para refrescar esa lista (se genera en el
                // servidor) y los contadores, regresando a la pestaña de aprobadas.
                try { sessionStorage.setItem('sgvLiqTab', 'aprobadas'); } catch (e) { }
                location.reload();
            } else {
                SGV.avisar('\u274c ' + result.message);
            }
            $btn.prop('disabled', false).html('<i class="fas fa-undo mr-1"></i>Confirmar Reversi\u00f3n');
        },
        error: function (xhr) {
            console.error('Error:', xhr.responseText);
            SGV.avisar('Error al revertir la aprobaci\u00f3n.');
            $btn.prop('disabled', false).html('<i class="fas fa-undo mr-1"></i>Confirmar Reversi\u00f3n');
        }
    });
}

// ============================================================
// === AUTOCOMPLETE DE CONDUCTOR ===
// ============================================================

function initConductorAutocomplete(inputId, suggestionsId, hiddenId) {
    var timer = null;
    var activeIndex = -1;

    var $input = $('#' + inputId);
    var $sugg = $('#' + suggestionsId);
    var $hidden = $('#' + hiddenId);

    $input.on('input', function () {
        var term = $(this).val().trim();
        activeIndex = -1;
        $hidden.val('');

        if (term.length < 2) {
            $sugg.hide().empty();
            return;
        }

        clearTimeout(timer);
        timer = setTimeout(function () {
            $.ajax({
                type: 'POST',
                url: 'LiquidacionesPendientes.aspx/BuscarConductores',
                data: JSON.stringify({ term: term }),
                contentType: 'application/json; charset=utf-8',
                dataType: 'json',
                success: function (response) {
                    var items = JSON.parse(response.d);
                    $sugg.empty();
                    if (items.length === 0) {
                        $sugg.append('<div class="autocomplete-no-results">Sin resultados</div>').show();
                        return;
                    }
                    items.forEach(function (item) {
                        var $item = $('<div class="autocomplete-item"></div>')
                            .text(item.text)
                            .attr('data-id', item.id)
                            .attr('data-text', item.text);
                        $sugg.append($item);
                    });
                    $sugg.show();
                }
            });
        }, 280);
    });

    $input.on('keydown', function (e) {
        var $items = $sugg.find('.autocomplete-item');
        if (!$sugg.is(':visible') || $items.length === 0) return;

        if (e.key === 'ArrowDown') {
            e.preventDefault();
            activeIndex = Math.min(activeIndex + 1, $items.length - 1);
            $items.removeClass('active').eq(activeIndex).addClass('active');
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            activeIndex = Math.max(activeIndex - 1, 0);
            $items.removeClass('active').eq(activeIndex).addClass('active');
        } else if (e.key === 'Enter') {
            e.preventDefault();
            if (activeIndex >= 0) {
                $items.eq(activeIndex).trigger('click');
            }
        } else if (e.key === 'Escape') {
            $sugg.hide().empty();
            $hidden.val('');
        }
    });

    $(document).on('click', '#' + suggestionsId + ' .autocomplete-item', function () {
        $input.val($(this).attr('data-text'));
        $hidden.val($(this).attr('data-id'));
        $sugg.hide().empty();
        activeIndex = -1;
    });

    // Limpiar selección si el usuario borra el texto manualmente
    $input.on('change', function () {
        if ($(this).val().trim() === '') {
            $hidden.val('');
        }
    });

    // Cerrar al hacer clic fuera
    $(document).on('click', function (e) {
        if (!$(e.target).closest('#' + inputId + ', #' + suggestionsId).length) {
            $sugg.hide();
        }
    });
}

$(document).ready(function () {
    initConductorAutocomplete('txtConductorBuscar', 'conductorPendientesSugg', 'hfConductorId');
    initConductorAutocomplete('txtConductorAprobadas', 'conductorAprobadasSugg', 'filtroCondAprobadasId');
    $('#' + SGV.btnFiltrar).on('click', function () {
        return validarFiltrosPendientesCliente();
    });
});

function mostrarErrorCampo(selectorInput, selectorError, mensaje) {
    $(selectorInput).addClass('is-invalid');
    $(selectorError).text(mensaje).removeClass('d-none');
}

function limpiarErroresFormulario() {
    $('.is-invalid').removeClass('is-invalid');
    $('#errorConductorBuscar,#errorFechaDesde,#errorFechaHasta,#errorPrioridad,#errorRechazoObservaciones,#errorRevertirMotivo,#errorRetirarMotivo').addClass('d-none').text('');
}

function validarFiltrosPendientesCliente() {
    limpiarErroresFormulario();
    var valido = true;
    var conductorTexto = $('#txtConductorBuscar').val().trim();
    var conductorId = $('#hfConductorId').val().trim();
    var fechaDesde = $('#' + SGV.txtFechaDesde).val();
    var fechaHasta = $('#' + SGV.txtFechaHasta).val();
    var prioridad = $('#' + SGV.ddlPrioridad).val();

    if (conductorTexto && !conductorId) {
        mostrarErrorCampo('#txtConductorBuscar', '#errorConductorBuscar', 'Seleccione un conductor de la lista sugerida.');
        valido = false;
    }
    if (fechaDesde && fechaHasta && fechaDesde > fechaHasta) {
        mostrarErrorCampo('#' + SGV.txtFechaHasta, '#errorFechaHasta', 'La fecha "Hasta" debe ser mayor o igual a "Desde".');
        valido = false;
    }
    if (prioridad && ['URGENTE', 'ALTA', 'NORMAL'].indexOf(prioridad) === -1) {
        mostrarErrorCampo('#' + SGV.ddlPrioridad, '#errorPrioridad', 'La prioridad seleccionada no es válida.');
        valido = false;
    }
    return valido;
}

function validarFiltrosAprobadasCliente() {
    var fechaDesde = $('#filtroDesdeAprobadas').val();
    var fechaHasta = $('#filtroHastaAprobadas').val();
    var numeroOrden = ($('#filtroOrdenAprobadas').val() || '').trim();

    if (fechaDesde && fechaHasta && fechaDesde > fechaHasta) {
        SGV.avisar('En filtros de aprobadas, la fecha "Hasta" debe ser mayor o igual a "Desde".');
        return false;
    }
    if (numeroOrden && !/^[A-Za-z0-9\-_/]{1,30}$/.test(numeroOrden)) {
        SGV.avisar('El filtro N° Orden solo permite letras, números, guion (-), guion bajo (_) y barra (/), máximo 30 caracteres.');
        $('#filtroOrdenAprobadas').focus();
        return false;
    }
    return true;
}

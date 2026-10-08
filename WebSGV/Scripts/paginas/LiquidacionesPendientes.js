let idOrdenParaAprobar = 0;
let _idOrdenDetalleActual = 0;
let modoAprobacion = false;
let _balanceSoles = 0;
let _balanceDolares = 0;

function verDetalleLiquidacion(idOrdenViaje) {
    console.log('Ver detalle:', idOrdenViaje);

    $('#btnAprobarDesdeModal').hide().prop('disabled', false).html('<i class="fas fa-check mr-1"></i>Aprobar Liquidación');
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
        '<td><i class="fas ' + icon + ' ' + iconClass + ' mr-2"></i>' + label + expandBtn + '</td>' +
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
    var partes = [
        horaDeclarada ? horaDeclarada + ' h (declarada por el conductor)' : 'sin declarar',
        horaSistema ? horaSistema + ' h (envío al sistema)' : 'sin registrar'
    ];
    if (horaGps) {
        partes.push(horaGps + ' h (verificada por GPS)');
    }
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
                alert('❌ ' + result.message);
            }
        },
        error: function (xhr) {
            console.error('Error:', xhr.responseText);
            $btn.prop('disabled', false).html('<i class="fas fa-satellite-dish"></i> Verificar GPS');
            alert('Error al consultar el GPS. Por favor intente de nuevo.');
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

    var balPositivo = balanceSoles >= 0 && balanceDolares >= 0;
    var balNegativo = balanceSoles < 0 || balanceDolares < 0;
    var balColor = balNegativo ? '#7f1d1d' : '#064e3b';
    var balBg = balNegativo
        ? 'linear-gradient(135deg, #7f1d1d 0%, #991b1b 100%)'
        : 'linear-gradient(135deg, #064e3b 0%, #065f46 100%)';

    $('#detalleBalanceSoles').text(balanceSoles.toFixed(2))
        .css('color', balanceSoles >= 0 ? '#6ee7b7' : '#fca5a5');
    $('#detalleBalanceDolares').text(balanceDolares.toFixed(2))
        .css('color', balanceDolares >= 0 ? '#6ee7b7' : '#fca5a5');
    $('#rfBalanceBar').css('background', balBg);

    // --- Desglose de Ingresos ---
    var ingresosItems = [
        { key: 'Despacho',      label: 'Flete / Despacho', descKey: 'DescDespacho' },
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
    actualizarBalanceAjustado();

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

    return confirm('¿Está seguro de RECHAZAR esta liquidación?\n\nEl viaje se reabrirá para correcciones.');
}

function imprimirDetalle() {
    // Recopilar datos del modal actual
    var numeroOrden = $('#modalNumeroOrden').text();
    var conductor = $('#detalleConductor').text();
    var tracto = $('#detalleTracto').text();
    var carreta = $('#detalleCarreta').text();
    var periodo = $('#detallePeriodo').text();
    var observaciones = $('#detalleObservaciones').text() || '';

    var ingresosSoles = $('#detalleIngresosSoles').text();
    var ingresosDolares = $('#detalleIngresosDolares').text();
    var gastosSoles = $('#detalleGastosSoles').text();
    var gastosDolares = $('#detalleGastosDolares').text();
    var balanceSoles = $('#detalleBalanceSoles').text();
    var balanceDolares = $('#detalleBalanceDolares').text();
    var balSolesColor = parseFloat(balanceSoles) >= 0 ? '#059669' : '#dc2626';
    var balDolaresColor = parseFloat(balanceDolares) >= 0 ? '#059669' : '#dc2626';

    // Recopilar filas de ingresos
    var ingresosRows = '';
    $('#detalleIngresosBody tr:not(.detail-sub-row)').each(function () {
        var cols = $(this).find('td');
        if (cols.length >= 3) {
            var concepto = $(cols[0]).clone();
            concepto.find('.btn-expand').remove();
            ingresosRows += '<tr>' +
                '<td style="padding:8px 10px;border-bottom:1px solid #e2e8f0;font-size:13px;">' + concepto.html() + '</td>' +
                '<td style="padding:8px 10px;border-bottom:1px solid #e2e8f0;text-align:right;font-size:13px;">' + $(cols[1]).text() + '</td>' +
                '<td style="padding:8px 10px;border-bottom:1px solid #e2e8f0;text-align:right;font-size:13px;">' + $(cols[2]).text() + '</td></tr>';

            // Incluir sub-detalle si existe
            var $subRow = $(this).next('.detail-sub-row');
            if ($subRow.length) {
                ingresosRows += '<tr><td colspan="3" style="padding:4px 20px 10px;border-bottom:1px solid #e2e8f0;background:#f8fafc;font-size:12px;">' + $subRow.find('td').html() + '</td></tr>';
            }
        }
    });

    // Recopilar filas de gastos
    var gastosRows = '';
    $('#detalleGastosBody tr:not(.detail-sub-row)').each(function () {
        var cols = $(this).find('td');
        if (cols.length >= 3) {
            var concepto = $(cols[0]).clone();
            concepto.find('.btn-expand').remove();
            gastosRows += '<tr>' +
                '<td style="padding:8px 10px;border-bottom:1px solid #e2e8f0;font-size:13px;">' + concepto.html() + '</td>' +
                '<td style="padding:8px 10px;border-bottom:1px solid #e2e8f0;text-align:right;font-size:13px;">' + $(cols[1]).text() + '</td>' +
                '<td style="padding:8px 10px;border-bottom:1px solid #e2e8f0;text-align:right;font-size:13px;">' + $(cols[2]).text() + '</td></tr>';

            var $subRow = $(this).next('.detail-sub-row');
            if ($subRow.length) {
                gastosRows += '<tr><td colspan="3" style="padding:4px 20px 10px;border-bottom:1px solid #e2e8f0;background:#f8fafc;font-size:12px;">' + $subRow.find('td').html() + '</td></tr>';
            }
        }
    });

    var fechaImpresion = new Date().toLocaleString('es-PE', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });

    var logoUrl = window.location.origin + '/Content/favicon.png';

    // === DATOS CORPORATIVOS (Empresa + Formato Controlado ISO/BASC) ===
    var EMPRESA_NOMBRE = 'SERVICIOS GENERALES VIVIANA E.I.R.L.';
    var EMPRESA_RUBRO = 'Transporte y Construcci\u00f3n';
    var EMPRESA_RUC = '20483851171';
    var EMPRESA_DIRECCION = 'Jr. Ca\u00f1ete Nro. 416 Dpto. 100 - Cercado de Lima, Lima';
    var EMPRESA_WEB = 'www.serviciosgviviana.somee.com';
    var FORMATO_CODIGO = 'SGV-CDF-F-05';
    var FORMATO_VERSION = '01';
    var FORMATO_VIGENCIA = '01/01/2025';

    // === Conversi\u00f3n monto a letras (peruano) ===
    function numeroALetras(num, moneda) {
        num = parseFloat(num) || 0;
        var negativo = num < 0;
        num = Math.abs(num);
        var entero = Math.floor(num);
        var decimal = Math.round((num - entero) * 100);
        if (decimal === 100) { entero += 1; decimal = 0; }

        var UNIDADES = ['', 'UNO', 'DOS', 'TRES', 'CUATRO', 'CINCO', 'SEIS', 'SIETE', 'OCHO', 'NUEVE', 'DIEZ', 'ONCE', 'DOCE', 'TRECE', 'CATORCE', 'QUINCE', 'DIECIS\u00c9IS', 'DIECISIETE', 'DIECIOCHO', 'DIECINUEVE', 'VEINTE'];
        var DECENAS = ['', '', 'VEINTI', 'TREINTA', 'CUARENTA', 'CINCUENTA', 'SESENTA', 'SETENTA', 'OCHENTA', 'NOVENTA'];
        var CENTENAS = ['', 'CIENTO', 'DOSCIENTOS', 'TRESCIENTOS', 'CUATROCIENTOS', 'QUINIENTOS', 'SEISCIENTOS', 'SETECIENTOS', 'OCHOCIENTOS', 'NOVECIENTOS'];

        function seccion(n) {
            if (n === 0) return '';
            if (n <= 20) return UNIDADES[n];
            if (n < 30) return 'VEINTI' + UNIDADES[n - 20].toLowerCase();
            if (n < 100) {
                var d = Math.floor(n / 10), u = n % 10;
                return DECENAS[d] + (u ? ' Y ' + UNIDADES[u] : '');
            }
            if (n === 100) return 'CIEN';
            if (n < 1000) {
                var c = Math.floor(n / 100), r = n % 100;
                return CENTENAS[c] + (r ? ' ' + seccion(r) : '');
            }
            if (n < 1000000) {
                var miles = Math.floor(n / 1000), resto = n % 1000;
                var pref = (miles === 1) ? 'MIL' : seccion(miles) + ' MIL';
                return pref + (resto ? ' ' + seccion(resto) : '');
            }
            var mill = Math.floor(n / 1000000), rest2 = n % 1000000;
            var pref2 = (mill === 1) ? 'UN MILL\u00d3N' : seccion(mill) + ' MILLONES';
            return pref2 + (rest2 ? ' ' + seccion(rest2) : '');
        }

        var letras = seccion(entero);
        if (!letras) letras = 'CERO';
        letras = letras.replace('VEINTIuno', 'VEINTIUNO').replace('VEINTIdos', 'VEINTID\u00d3S').replace('VEINTItres', 'VEINTITR\u00c9S').replace('VEINTIcuatro', 'VEINTICUATRO').replace('VEINTIcinco', 'VEINTICINCO').replace('VEINTIseis', 'VEINTIS\u00c9IS').replace('VEINTIsiete', 'VEINTISIETE').replace('VEINTIocho', 'VEINTIOCHO').replace('VEINTInueve', 'VEINTINUEVE');
        var dec = (decimal < 10 ? '0' : '') + decimal;
        var signo = negativo ? 'MENOS ' : '';
        return signo + letras + ' CON ' + dec + '/100 ' + moneda;
    }

    var balSolesNum = parseFloat(balanceSoles) || 0;
    var balDolaresNum = parseFloat(balanceDolares) || 0;
    var balSolesLetras = numeroALetras(balSolesNum, 'SOLES');
    var balDolaresLetras = numeroALetras(balDolaresNum, 'D\u00d3LARES AMERICANOS');

    var printHtml = '<!DOCTYPE html><html lang="es"><head><meta charset="utf-8"/>' +
        '<title>Orden de Viaje ' + htmlEncode(numeroOrden) + '</title>' +
        '<link href="https://fonts.googleapis.com/css2?family=Montserrat:wght@500;600;700;800&family=Open+Sans:wght@400;600;700&display=swap" rel="stylesheet"/>' +
        '<link href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/5.15.4/css/all.min.css" rel="stylesheet"/>' +
        '<style>' +
        // --- Variables y reset ---
        '@page { size: A4; margin: 12mm 12mm 14mm 12mm; }' +
        '*, *::before, *::after { box-sizing: border-box; }' +
        'body { font-family: "Open Sans", "Segoe UI", Arial, sans-serif; color: #1F2937; margin: 0; padding: 0; font-size: 12px; line-height: 1.45; -webkit-print-color-adjust: exact; print-color-adjust: exact; }' +
        '.print-page { max-width: 210mm; margin: 0 auto; padding: 0; }' +
        'h1, h2, h3, h4 { font-family: "Montserrat", "Segoe UI", Arial, sans-serif; margin: 0; }' +

        // --- Header controlado (3 columnas: logo | empresa | formato) ---
        '.doc-header { display: table; width: 100%; border: 1.5px solid #0B3D91; border-collapse: collapse; margin-bottom: 10px; }' +
        '.doc-header > div { display: table-cell; vertical-align: middle; padding: 8px 10px; border-right: 1px solid #0B3D91; }' +
        '.doc-header > div:last-child { border-right: none; }' +
        '.dh-logo { width: 90px; text-align: center; }' +
        '.dh-logo img { width: 70px; height: 70px; object-fit: contain; }' +
        '.dh-empresa { text-align: center; }' +
        '.dh-empresa .razon { font-family: "Montserrat"; font-size: 13px; font-weight: 800; color: #0B3D91; text-transform: uppercase; letter-spacing: 0.5px; }' +
        '.dh-empresa .rubro { font-size: 10.5px; color: #6B7280; font-weight: 600; margin-top: 1px; }' +
        '.dh-empresa .titulo-doc { font-family: "Montserrat"; font-size: 15px; font-weight: 700; color: #1F2937; letter-spacing: 3px; margin-top: 6px; text-transform: uppercase; }' +
        '.dh-formato { width: 160px; font-size: 10px; color: #1F2937; }' +
        '.dh-formato table { width: 100%; border-collapse: collapse; }' +
        '.dh-formato td { padding: 2px 4px; border-bottom: 1px dotted #D1D5DB; }' +
        '.dh-formato td.lbl { font-weight: 700; color: #6B7280; text-transform: uppercase; font-size: 9px; letter-spacing: 0.3px; }' +
        '.dh-formato td.val { font-family: "Montserrat"; font-weight: 700; color: #0B3D91; text-align: right; font-size: 10px; }' +
        '.dh-formato tr:last-child td { border-bottom: none; }' +

        // --- Sub-cabecera: datos empresa + n\u00famero doc ---
        '.sub-header { display: table; width: 100%; margin-bottom: 12px; font-size: 10.5px; }' +
        '.sub-header > div { display: table-cell; vertical-align: top; }' +
        '.sub-header .emp-data { color: #374151; line-height: 1.55; }' +
        '.sub-header .emp-data strong { color: #0B3D91; font-weight: 700; }' +
        '.sub-header .doc-num { text-align: right; width: 200px; }' +
        '.sub-header .doc-num .nro-label { font-size: 9.5px; font-weight: 700; color: #6B7280; letter-spacing: 0.4px; text-transform: uppercase; }' +
        '.sub-header .doc-num .nro-value { font-family: "Montserrat"; font-size: 17px; font-weight: 800; color: #C8102E; border: 1.5px solid #C8102E; padding: 4px 12px; display: inline-block; margin-top: 3px; letter-spacing: 0.8px; }' +
        '.sub-header .doc-num .emitido { font-size: 9.5px; color: #6B7280; margin-top: 5px; }' +

        // --- Secciones numeradas ---
        '.section { margin-bottom: 10px; page-break-inside: avoid; }' +
        '.section-title { font-family: "Montserrat"; font-size: 11px; font-weight: 700; color: #FFFFFF; background: #0B3D91; padding: 5px 10px; text-transform: uppercase; letter-spacing: 0.6px; }' +
        '.section-title .num { display: inline-block; background: #FFFFFF; color: #0B3D91; width: 18px; height: 18px; line-height: 18px; text-align: center; border-radius: 2px; margin-right: 8px; font-weight: 800; }' +

        // --- Grid de info ---
        '.info-grid { display: table; width: 100%; border-collapse: collapse; }' +
        '.info-grid .info-row { display: table-row; }' +
        '.info-item { display: table-cell; border: 1px solid #D1D5DB; padding: 6px 10px; width: 25%; }' +
        '.info-item .label { font-size: 9px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.4px; color: #6B7280; }' +
        '.info-item .value { font-family: "Montserrat"; font-size: 12px; font-weight: 700; color: #1F2937; margin-top: 2px; }' +
        '.obs-box { border: 1px solid #D1D5DB; border-left: 3px solid #C8102E; padding: 7px 10px; font-size: 11px; color: #374151; margin-top: 6px; background: #FAFAFA; }' +

        // --- Tablas ---
        'table.report-table { width: 100%; border-collapse: collapse; font-size: 11.5px; border: 1px solid #D1D5DB; }' +
        'table.report-table thead th { background: #0B3D91; color: #FFFFFF; padding: 6px 10px; font-family: "Montserrat"; font-weight: 600; font-size: 10.5px; text-transform: uppercase; letter-spacing: 0.4px; border: 1px solid #0B3D91; }' +
        'table.report-table thead th:first-child { text-align: left; }' +
        'table.report-table thead th.text-right { text-align: right; }' +
        'table.report-table tbody td { padding: 5px 10px; border-bottom: 1px solid #E5E7EB; }' +
        'table.report-table tbody tr:nth-child(odd) td { background: #E8EEF7; }' +
        'table.report-table tbody tr:nth-child(even) td { background: #FFFFFF; }' +
        'table.report-table tfoot td { background: #F3F4F6; font-family: "Montserrat"; font-weight: 700; padding: 7px 10px; border-top: 1.5px solid #0B3D91; font-size: 12px; color: #0B3D91; }' +

        // --- Resumen financiero (tabla sobria) ---
        'table.resumen-table { width: 100%; border-collapse: collapse; font-size: 11.5px; border: 1px solid #D1D5DB; }' +
        'table.resumen-table th { background: #F3F4F6; color: #1F2937; padding: 6px 10px; font-family: "Montserrat"; font-weight: 700; font-size: 10.5px; text-transform: uppercase; border: 1px solid #D1D5DB; }' +
        'table.resumen-table td { padding: 6px 10px; border: 1px solid #D1D5DB; }' +
        'table.resumen-table td.concepto { font-weight: 600; color: #1F2937; }' +
        'table.resumen-table td.num { text-align: right; font-family: "Montserrat"; font-weight: 600; }' +
        'table.resumen-table tr.total td { background: #E8EEF7; font-weight: 800; color: #0B3D91; }' +
        'table.resumen-table tr.balance td { background: #0B3D91; color: #FFFFFF; font-family: "Montserrat"; font-weight: 800; font-size: 13px; }' +
        'table.resumen-table tr.balance td.num.neg { color: #FCA5A5; }' +
        '.monto-letras { background: #F9FAFB; border: 1px dashed #9CA3AF; padding: 6px 10px; font-size: 10.5px; color: #374151; margin-top: 6px; }' +
        '.monto-letras strong { font-family: "Montserrat"; font-weight: 700; color: #0B3D91; display: inline-block; min-width: 60px; }' +

        // --- Constancia y firma ---
        '.constancia { border: 1px solid #D1D5DB; padding: 10px 12px; margin-top: 10px; page-break-inside: avoid; }' +
        '.constancia .const-title { font-family: "Montserrat"; font-size: 11px; font-weight: 700; color: #0B3D91; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 6px; }' +
        '.constancia .const-text { font-size: 11px; text-align: justify; color: #374151; line-height: 1.55; margin-bottom: 10px; }' +
        '.firma-box { width: 280px; margin: 0 auto; text-align: center; }' +
        '.firma-box .firma-area { border: 1px dashed #9CA3AF; height: 70px; margin-bottom: 4px; background: #FAFAFA; display: flex; align-items: center; justify-content: center; font-size: 9px; color: #9CA3AF; font-style: italic; }' +
        '.firma-box .firma-line { border-top: 1px solid #1F2937; padding-top: 3px; }' +
        '.firma-box .firma-nombre { font-family: "Montserrat"; font-size: 11px; font-weight: 700; color: #1F2937; }' +
        '.firma-box .firma-rol { font-size: 10px; color: #6B7280; margin-top: 1px; }' +
        '.firma-meta { margin-top: 8px; font-size: 9px; color: #6B7280; text-align: center; font-family: "Consolas", monospace; }' +

        // --- Pie ---
        '.doc-footer { margin-top: 14px; border-top: 1.5px solid #0B3D91; padding-top: 8px; font-size: 9px; color: #6B7280; display: table; width: 100%; }' +
        '.doc-footer > div { display: table-cell; vertical-align: top; }' +
        '.doc-footer .foot-legal { width: 75%; line-height: 1.5; }' +
        '.doc-footer .foot-legal strong { color: #0B3D91; }' +
        '.doc-footer .foot-pag { width: 25%; text-align: right; font-family: "Consolas", monospace; }' +
        '.doc-footer .hash { font-family: "Consolas", monospace; color: #374151; word-break: break-all; }' +

        // --- Detalles anidados / tablas hijas ---
        '.detail-sub-content { padding: 4px 8px; font-size: 10.5px; }' +
        '.detail-desc { font-size: 10.5px; color: #6B7280; font-style: italic; margin-bottom: 4px; }' +
        '.table-sub { font-size: 10px !important; }' +
        '.table-sub thead th { background: #E5E7EB !important; color: #1F2937 !important; font-size: 9.5px !important; padding: 3px 6px !important; }' +
        '.table-sub tbody td { padding: 3px 6px !important; font-size: 10px !important; background: #FFFFFF !important; }' +

        '</style></head><body>' +
        '<div class="print-page">' +

        // === 1. ENCABEZADO CONTROLADO (ISO/BASC) ===
        '<div class="doc-header">' +
        '<div class="dh-logo"><img src="' + logoUrl + '" alt="Logo Viviana"/></div>' +
        '<div class="dh-empresa">' +
        '<div class="razon">' + EMPRESA_NOMBRE + '</div>' +
        '<div class="rubro">' + EMPRESA_RUBRO + '</div>' +
        '<div class="titulo-doc">Orden de Viaje</div>' +
        '</div>' +
        '<div class="dh-formato">' +
        '<table>' +
        '<tr><td class="lbl">C\u00f3digo</td><td class="val">' + FORMATO_CODIGO + '</td></tr>' +
        '<tr><td class="lbl">Versi\u00f3n</td><td class="val">' + FORMATO_VERSION + '</td></tr>' +
        '<tr><td class="lbl">Vigencia</td><td class="val">' + FORMATO_VIGENCIA + '</td></tr>' +
        '<tr><td class="lbl">P\u00e1gina</td><td class="val">1 de 1</td></tr>' +
        '</table>' +
        '</div>' +
        '</div>' +

        // === SUB-HEADER: datos empresa + n\u00famero de documento ===
        '<div class="sub-header">' +
        '<div class="emp-data">' +
        '<strong>RUC:</strong> ' + EMPRESA_RUC + '<br/>' +
        '<strong>Domicilio Fiscal:</strong> ' + EMPRESA_DIRECCION + '<br/>' +
        '<strong>Web:</strong> ' + EMPRESA_WEB +
        '</div>' +
        '<div class="doc-num">' +
        '<div class="nro-label">N.\u00b0 de Orden</div>' +
        '<div class="nro-value">' + htmlEncode(numeroOrden) + '</div>' +
        '<div class="emitido">Emitido: ' + fechaImpresion + '</div>' +
        '</div>' +
        '</div>' +

        // === 1. INFORMACI\u00d3N DEL VIAJE ===
        '<div class="section">' +
        '<div class="section-title"><span class="num">1</span>Informaci\u00f3n del Viaje</div>' +
        '<div class="info-grid"><div class="info-row">' +
        '<div class="info-item"><div class="label">Conductor</div><div class="value">' + htmlEncode(conductor) + '</div></div>' +
        '<div class="info-item"><div class="label">Tracto</div><div class="value">' + htmlEncode(tracto) + '</div></div>' +
        '<div class="info-item"><div class="label">Carreta</div><div class="value">' + htmlEncode(carreta) + '</div></div>' +
        '<div class="info-item"><div class="label">Periodo</div><div class="value">' + htmlEncode(periodo) + '</div></div>' +
        '</div></div>' +
        '</div>' +

        // === 2. RESUMEN FINANCIERO (tabla sobria) ===
        '<div class="section">' +
        '<div class="section-title"><span class="num">2</span>Resumen Financiero</div>' +
        '<table class="resumen-table">' +
        '<thead><tr><th style="text-align:left;">Concepto</th><th style="text-align:right;">Soles (S/)</th><th style="text-align:right;">D\u00f3lares ($)</th></tr></thead>' +
        '<tbody>' +
        '<tr><td class="concepto">Total Ingresos</td><td class="num">S/ ' + ingresosSoles + '</td><td class="num">$ ' + ingresosDolares + '</td></tr>' +
        '<tr><td class="concepto">Total Gastos</td><td class="num">S/ ' + gastosSoles + '</td><td class="num">$ ' + gastosDolares + '</td></tr>' +
        '<tr class="balance"><td>BALANCE FINAL DEL VIAJE</td><td class="num' + (balSolesNum < 0 ? ' neg' : '') + '">S/ ' + balanceSoles + '</td><td class="num' + (balDolaresNum < 0 ? ' neg' : '') + '">$ ' + balanceDolares + '</td></tr>' +
        '</tbody></table>' +
        '<div class="monto-letras"><strong>SOLES:</strong> ' + balSolesLetras + '</div>' +
        '<div class="monto-letras"><strong>D\u00d3LARES:</strong> ' + balDolaresLetras + '</div>' +
        '</div>' +

        // === 3. DESGLOSE DE INGRESOS ===
        '<div class="section">' +
        '<div class="section-title"><span class="num">3</span>Desglose de Ingresos</div>' +
        '<table class="report-table">' +
        '<thead><tr><th>Concepto</th><th class="text-right">Soles (S/)</th><th class="text-right">D\u00f3lares ($)</th></tr></thead>' +
        '<tbody>' + ingresosRows + '</tbody>' +
        '<tfoot><tr><td>Total Ingresos</td><td style="text-align:right;">S/ ' + ingresosSoles + '</td><td style="text-align:right;">$ ' + ingresosDolares + '</td></tr></tfoot>' +
        '</table></div>' +

        // === 4. DESGLOSE DE GASTOS ===
        '<div class="section">' +
        '<div class="section-title"><span class="num">4</span>Desglose de Gastos</div>' +
        '<table class="report-table">' +
        '<thead><tr><th>Concepto</th><th class="text-right">Soles (S/)</th><th class="text-right">D\u00f3lares ($)</th></tr></thead>' +
        '<tbody>' + gastosRows + '</tbody>' +
        '<tfoot><tr><td>Total Gastos</td><td style="text-align:right;">S/ ' + gastosSoles + '</td><td style="text-align:right;">$ ' + gastosDolares + '</td></tr></tfoot>' +
        '</table></div>' +

        // === 5. OBSERVACIONES ===
        (observaciones ?
            '<div class="section">' +
            '<div class="section-title"><span class="num">5</span>Observaciones</div>' +
            '<div class="obs-box">' + htmlEncode(observaciones) + '</div>' +
            '</div>'
            : '') +

        // === 6. CONSTANCIA DE CONFORMIDAD Y FIRMA ===
        '<div class="constancia">' +
        '<div class="const-title">Constancia de Conformidad</div>' +
        '<div class="const-text">Yo, <strong>' + htmlEncode(conductor) + '</strong>, en mi condici\u00f3n de conductor del viaje ' +
        '<strong>' + htmlEncode(numeroOrden) + '</strong>, declaro haber revisado el detalle de ingresos, gastos y balance ' +
        'consignados en el presente documento, y manifiesto mi conformidad con la informaci\u00f3n registrada. ' +
        'Firmo en se\u00f1al de aceptaci\u00f3n y asumo las obligaciones derivadas de la presente liquidaci\u00f3n.</div>' +
        '<div class="firma-box">' +
        '<div class="firma-area">[Espacio reservado para firma del conductor]</div>' +
        '<div class="firma-line">' +
        '<div class="firma-nombre">' + htmlEncode(conductor) + '</div>' +
        '<div class="firma-rol">Conductor</div>' +
        '</div>' +
        '</div>' +
        '<div class="firma-meta">Firma pendiente de registro digital</div>' +
        '</div>' +

        // === PIE CONTROLADO ===
        '<div class="doc-footer">' +
        '<div class="foot-legal">' +
        '<strong>Documento interno controlado.</strong> Generado electr\u00f3nicamente por el Sistema SGV. ' +
        'Los comprobantes tributarios (facturas, boletas, guias, tickets) que respaldan los importes aqu\u00ed consignados ' +
        'se conservan digitalmente vinculados a este documento conforme al art. 87 del C\u00f3digo Tributario (plazo m\u00ednimo 5 a\u00f1os).' +
        '<br/><span class="hash">Hash documento: [pendiente de firma digital]</span>' +
        '</div>' +
        '<div class="foot-pag">' +
        FORMATO_CODIGO + ' v.' + FORMATO_VERSION + '<br/>' +
        'P\u00e1gina 1 de 1' +
        '</div>' +
        '</div>' +

        '</div></body></html>';

    var printWindow = window.open('', '_blank', 'width=900,height=700');
    if (printWindow) {
        printWindow.document.write(printHtml);
        printWindow.document.close();
        printWindow.onload = function () {
            setTimeout(function () {
                printWindow.print();
            }, 400);
        };
    } else {
        alert('El navegador bloque\u00f3 la ventana emergente. Por favor, permita las ventanas emergentes para este sitio.');
    }
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

    if (descS > 0 || descD > 0 || reintS > 0 || reintD > 0) {
        var balS = _balanceSoles - descS + reintS;
        var balD = _balanceDolares - descD + reintD;
        $('#balanceAjustadoSoles').text(balS.toFixed(2))
            .css('color', balS >= 0 ? '#059669' : '#dc2626');
        $('#balanceAjustadoDolares').text(balD.toFixed(2))
            .css('color', balD >= 0 ? '#059669' : '#dc2626');
        $('#balanceAjustadoPanel').show();
    } else {
        $('#balanceAjustadoPanel').hide();
    }
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

    if (!confirm('¿Confirma corregir la fecha/hora de salida de esta liquidación?\n\nEl cambio quedará auditado.')) return;

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
                alert('✅ ' + result.message);
                location.reload();
            } else {
                alert('❌ ' + result.message);
                $btn.prop('disabled', false).html('<i class="fas fa-save mr-1"></i>Guardar corrección');
            }
        },
        error: function (xhr) {
            console.error('Error:', xhr.responseText);
            alert('Error al corregir la salida. Por favor intente de nuevo.');
            $btn.prop('disabled', false).html('<i class="fas fa-save mr-1"></i>Guardar corrección');
        }
    });
}

function aprobarDesdeModal() {
    if (!idOrdenParaAprobar) return;
    limpiarErroresFormulario();

    var descS = parseFloat($('#ajusteDescuentoSoles').val()) || 0;
    var descD = parseFloat($('#ajusteDescuentoDolares').val()) || 0;
    var reintS = parseFloat($('#ajusteReintegroSoles').val()) || 0;
    var reintD = parseFloat($('#ajusteReintegroDolares').val()) || 0;
    var nota = $('#notaAprobacion').val().trim();

    if (descS < 0 || descD < 0 || reintS < 0 || reintD < 0) {
        alert('⚠️ Los montos de ajuste no pueden ser negativos.');
        return;
    }

    if (nota.length > 500) {
        alert('La nota de aprobación no puede superar 500 caracteres.');
        $('#notaAprobacion').focus();
        return;
    }

    var confirmMsg = '¿Está seguro de APROBAR esta liquidación?\n\n';
    if (descS > 0 || descD > 0 || reintS > 0 || reintD > 0) {
        confirmMsg += 'Se aplicarán los siguientes ajustes:\n';
        if (descS > 0) confirmMsg += '  \u2022 Descuento S/: ' + descS.toFixed(2) + '\n';
        if (descD > 0) confirmMsg += '  \u2022 Descuento $: ' + descD.toFixed(2) + '\n';
        if (reintS > 0) confirmMsg += '  \u2022 Reintegro S/: ' + reintS.toFixed(2) + '\n';
        if (reintD > 0) confirmMsg += '  \u2022 Reintegro $: ' + reintD.toFixed(2) + '\n';
        confirmMsg += '\n';
    }
    if (nota) confirmMsg += 'Nota: ' + nota + '\n\n';
    confirmMsg += 'Esta acción completará el viaje y no se podrá deshacer.';

    if (!confirm(confirmMsg)) return;

    var $btn = $('#btnAprobarDesdeModal');
    $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-1"></i>Aprobando...');

    $.ajax({
        type: "POST",
        url: "LiquidacionesPendientes.aspx/AprobarLiquidacionConFirma",
        data: JSON.stringify({
            idOrdenViaje: idOrdenParaAprobar,
            descuentoSoles: descS,
            descuentoDolares: descD,
            reintegroSoles: reintS,
            reintegroDolares: reintD,
            notaAprobacion: nota || null
        }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            var result = response.d;
            if (result.success) {
                $('#modalDetalleLiquidacion').modal('hide');
                alert('\u2705 ' + result.message);
                location.reload();
            } else {
                alert('\u274c ' + result.message);
                $btn.prop('disabled', false).html('<i class="fas fa-check mr-1"></i>Aprobar Liquidación');
            }
        },
        error: function (xhr) {
            console.error('Error:', xhr.responseText);
            alert('Error al aprobar la liquidación. Por favor intente de nuevo.');
            $btn.prop('disabled', false).html('<i class="fas fa-check mr-1"></i>Aprobar Liquidación');
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
        $('#balanceAjustadoPanel').hide();
    });
});

// ============================================================
// === SECCIÓN CONTROL DE APROBADAS ===
// ============================================================

var _aprobadasData = {};
var _aprobadasCargadas = false;

function cambiarTab(tab) {
    if (tab === 'pendientes') {
        $('#seccionPendientes').show();
        $('#seccionAprobadas').hide();
        $('#tabPendientes').addClass('tab-btn-active');
        $('#tabAprobadas').removeClass('tab-btn-active');
    } else {
        $('#seccionPendientes').hide();
        $('#seccionAprobadas').show();
        $('#tabPendientes').removeClass('tab-btn-active');
        $('#tabAprobadas').addClass('tab-btn-active');
        if (!_aprobadasCargadas) {
            inicializarFiltrosAprobadas();
            cargarLiquidacionesAprobadas();
            _aprobadasCargadas = true;
        }
    }
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
                    '<tr><td colspan="7" class="text-center py-4">' +
                    '<div class="empty-state-table" style="padding:2rem;">' +
                    '<i class="fas fa-check-circle" style="font-size:2.5rem;color:#059669;display:block;margin-bottom:0.5rem;"></i>' +
                    '<p class="text-muted mb-0">No se encontraron liquidaciones aprobadas en el rango seleccionado.</p>' +
                    '</div></td></tr>'
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
                html += '<td><div class="orden-info">';
                html += '<span class="orden-numero">' + htmlEncode(item.NumeroOrdenViaje) + '</span>';
                html += '<span class="orden-fecha">' + htmlEncode(item.FechaSalida) + '</span>';
                html += '</div></td>';

                // Conductor
                html += '<td><div class="conductor-info">';
                html += '<i class="fas fa-user-circle mr-2 text-success"></i><div>';
                html += '<strong>' + htmlEncode(item.NombreConductor) + '</strong><br/>';
                html += '<small class="text-muted">' + htmlEncode(item.PlacaTracto) + ' / ' + htmlEncode(item.PlacaCarreta) + '</small>';
                html += '</div></div></td>';

                // Periodo
                html += '<td class="text-center"><div class="viaje-periodo">';
                html += '<div class="fecha-item"><i class="fas fa-calendar-check text-success"></i><span>' + htmlEncode(item.FechaSalida) + '</span></div>';
                html += '<div class="fecha-item"><i class="fas fa-calendar-times text-danger"></i><span>' + htmlEncode(item.FechaLlegada) + '</span></div>';
                html += '</div></td>';

                // Descuento
                var descS = parseFloat(item.DescuentoSoles || 0);
                var descD = parseFloat(item.DescuentoDolares || 0);
                html += '<td class="text-right">';
                if (descS > 0 || descD > 0) {
                    html += '<div class="balance-preview">';
                    html += '<div class="balance-row"><span class="balance-moneda">S/</span><span class="balance-monto balance-negativo">' + descS.toFixed(2) + '</span></div>';
                    html += '<div class="balance-row"><span class="balance-moneda">$</span><span class="balance-monto balance-negativo">' + descD.toFixed(2) + '</span></div>';
                    html += '</div>';
                } else {
                    html += '<span class="text-muted">-</span>';
                }
                html += '</td>';

                // Reintegro
                var reintS = parseFloat(item.ReintegroSoles || 0);
                var reintD = parseFloat(item.ReintegroDolares || 0);
                html += '<td class="text-right">';
                if (reintS > 0 || reintD > 0) {
                    html += '<div class="balance-preview">';
                    html += '<div class="balance-row"><span class="balance-moneda">S/</span><span class="balance-monto balance-positivo">' + reintS.toFixed(2) + '</span></div>';
                    html += '<div class="balance-row"><span class="balance-moneda">$</span><span class="balance-monto balance-positivo">' + reintD.toFixed(2) + '</span></div>';
                    html += '</div>';
                } else {
                    html += '<span class="text-muted">-</span>';
                }
                html += '</td>';

                // Balance Final
                var bfS = parseFloat(item.BalanceSoles || 0);
                var bfD = parseFloat(item.BalanceDolares || 0);
                html += '<td class="text-right"><div class="balance-preview">';
                html += '<div class="balance-row"><span class="balance-moneda">S/</span><span class="balance-monto ' + (bfS >= 0 ? 'balance-positivo' : 'balance-negativo') + '">' + bfS.toFixed(2) + '</span></div>';
                html += '<div class="balance-row"><span class="balance-moneda">$</span><span class="balance-monto ' + (bfD >= 0 ? 'balance-positivo' : 'balance-negativo') + '">' + bfD.toFixed(2) + '</span></div>';
                html += '</div></td>';

                // Acciones
                html += '<td class="text-center"><div class="acciones-grupo">';
                html += '<button type="button" class="btn btn-info-action" onclick="verDetalleLiquidacion(' + item.IdOrdenViaje + ')" title="Ver Detalle"><i class="fas fa-eye"></i></button>';
                html += '<button type="button" class="btn btn-success-action" onclick="descargarPdfOrdenViaje(' + item.IdOrdenViaje + ')" title="Descargar PDF SGV-CDF-F-05"><i class="fas fa-file-pdf"></i></button>';
                html += '<button type="button" class="btn btn-warning-action" onclick="abrirModalCorregir(' + item.IdOrdenViaje + ')" title="Corregir Ajustes"><i class="fas fa-edit"></i></button>';
                html += '<button type="button" class="btn btn-danger-action" onclick="abrirModalRevertir(' + item.IdOrdenViaje + ')" title="Revertir Aprobaci\u00f3n"><i class="fas fa-undo"></i></button>';
                html += '</div></td>';

                html += '</tr>';
            });

            $('#tbodyAprobadas').html(html);
        },
        error: function (xhr) {
            $('#loadingAprobadas').hide();
            console.error('Error cargando aprobadas:', xhr.responseText);
            $('#tbodyAprobadas').html(
                '<tr><td colspan="7" class="text-center text-danger py-4">' +
                '<i class="fas fa-exclamation-triangle mr-2"></i>Error al cargar las liquidaciones aprobadas</td></tr>'
            );
        }
    });
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
                alert('\u274c ' + (r && r.message ? r.message : 'No se pudo obtener el PDF.'));
            }
        },
        error: function (xhr) {
            console.error('Error PDF:', xhr.responseText);
            alert('Error al solicitar el PDF.');
        }
    });
}

// --- Modal Revertir ---
function abrirModalRevertir(idOrden) {
    var item = _aprobadasData[idOrden];
    if (!item) { alert('No se encontraron datos de la liquidaci\u00f3n.'); return; }
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

    if (!confirm('\u26a0\ufe0f \u00bfEst\u00e1 seguro de REVERTIR esta aprobaci\u00f3n?\n\nLa liquidaci\u00f3n volver\u00e1 al estado PENDIENTE y deber\u00e1 ser revisada nuevamente.')) return;

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
                alert('\u2705 ' + result.message);
                cargarLiquidacionesAprobadas();
            } else {
                alert('\u274c ' + result.message);
            }
            $btn.prop('disabled', false).html('<i class="fas fa-undo mr-1"></i>Confirmar Reversi\u00f3n');
        },
        error: function (xhr) {
            console.error('Error:', xhr.responseText);
            alert('Error al revertir la aprobaci\u00f3n.');
            $btn.prop('disabled', false).html('<i class="fas fa-undo mr-1"></i>Confirmar Reversi\u00f3n');
        }
    });
}

// --- Modal Corregir Ajustes ---
function abrirModalCorregir(idOrden) {
    var item = _aprobadasData[idOrden];
    if (!item) { alert('No se encontraron datos de la liquidaci\u00f3n.'); return; }

    $('#corregirIdOrden').val(idOrden);
    $('#corregirNumeroOrden').val(item.NumeroOrdenViaje);
    $('#corregirConductor').val(item.NombreConductor);

    var descS = parseFloat(item.DescuentoSoles || 0);
    var descD = parseFloat(item.DescuentoDolares || 0);
    var reintS = parseFloat(item.ReintegroSoles || 0);
    var reintD = parseFloat(item.ReintegroDolares || 0);

    $('#corregirDescSolesActual').text(descS.toFixed(2));
    $('#corregirDescDolaresActual').text(descD.toFixed(2));
    $('#corregirReintSolesActual').text(reintS.toFixed(2));
    $('#corregirReintDolaresActual').text(reintD.toFixed(2));

    $('#corregirDescSoles').val(descS > 0 ? descS.toFixed(2) : '');
    $('#corregirDescDolares').val(descD > 0 ? descD.toFixed(2) : '');
    $('#corregirReintSoles').val(reintS > 0 ? reintS.toFixed(2) : '');
    $('#corregirReintDolares').val(reintD > 0 ? reintD.toFixed(2) : '');
    $('#corregirMotivo').val('');

    $('#modalCorregirAjustes').modal('show');
}

function confirmarCorreccion() {
    var idOrden = parseInt($('#corregirIdOrden').val());
    var motivo = $('#corregirMotivo').val().trim();
    var descS = parseFloat($('#corregirDescSoles').val()) || 0;
    var descD = parseFloat($('#corregirDescDolares').val()) || 0;
    var reintS = parseFloat($('#corregirReintSoles').val()) || 0;
    var reintD = parseFloat($('#corregirReintDolares').val()) || 0;
    limpiarErroresFormulario();

    if (!motivo) {
        mostrarErrorCampo('#corregirMotivo', '#errorCorregirMotivo', 'Debe ingresar el motivo de la corrección.');
        $('#corregirMotivo').focus();
        return;
    }
    if (motivo.length < 10) {
        mostrarErrorCampo('#corregirMotivo', '#errorCorregirMotivo', 'El motivo debe tener al menos 10 caracteres.');
        $('#corregirMotivo').focus();
        return;
    }
    if (motivo.length > 500) {
        mostrarErrorCampo('#corregirMotivo', '#errorCorregirMotivo', 'El motivo no puede superar 500 caracteres.');
        $('#corregirMotivo').focus();
        return;
    }

    if (descS < 0 || descD < 0 || reintS < 0 || reintD < 0) {
        alert('Los montos de descuento/reintegro no pueden ser negativos.');
        return;
    }

    var msg = '\u00bfEst\u00e1 seguro de CORREGIR los ajustes?\n\n';
    msg += 'Nuevos valores:\n';
    msg += '  \u2022 Descuento S/: ' + descS.toFixed(2) + '\n';
    msg += '  \u2022 Descuento $: ' + descD.toFixed(2) + '\n';
    msg += '  \u2022 Reintegro S/: ' + reintS.toFixed(2) + '\n';
    msg += '  \u2022 Reintegro $: ' + reintD.toFixed(2) + '\n';

    if (!confirm(msg)) return;

    var $btn = $('#btnConfirmarCorreccion');
    $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-1"></i>Guardando...');

    $.ajax({
        type: "POST",
        url: "LiquidacionesPendientes.aspx/CorregirAjustesAprobada",
        data: JSON.stringify({
            idOrdenViaje: idOrden,
            descuentoSoles: descS,
            descuentoDolares: descD,
            reintegroSoles: reintS,
            reintegroDolares: reintD,
            motivo: motivo
        }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            var result = response.d;
            if (result.success) {
                $('#modalCorregirAjustes').modal('hide');
                alert('\u2705 ' + result.message);
                _aprobadasCargadas = false;
                cargarLiquidacionesAprobadas();
            } else {
                alert('\u274c ' + result.message);
            }
            $btn.prop('disabled', false).html('<i class="fas fa-save mr-1"></i>Guardar Correcci\u00f3n');
        },
        error: function (xhr) {
            console.error('Error:', xhr.responseText);
            alert('Error al guardar la correcci\u00f3n.');
            $btn.prop('disabled', false).html('<i class="fas fa-save mr-1"></i>Guardar Correcci\u00f3n');
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
    $('#errorConductorBuscar,#errorFechaDesde,#errorFechaHasta,#errorPrioridad,#errorRechazoObservaciones,#errorRevertirMotivo,#errorCorregirMotivo').addClass('d-none').text('');
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
        alert('En filtros de aprobadas, la fecha "Hasta" debe ser mayor o igual a "Desde".');
        return false;
    }
    if (numeroOrden && !/^[A-Za-z0-9\-_/]{1,30}$/.test(numeroOrden)) {
        alert('El filtro N° Orden solo permite letras, números, guion (-), guion bajo (_) y barra (/), máximo 30 caracteres.');
        $('#filtroOrdenAprobadas').focus();
        return false;
    }
    return true;
}

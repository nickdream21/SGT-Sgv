// === SEGURIDAD: escapado HTML para prevenir XSS en renderizado dinámico ===
function escHtml(str) {
    if (str == null) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

// === VARIABLES GLOBALES ===
let contadorIngresosAdicionales = 0;
let contadorGastosAdicionales = 0;

// Datos de modales
let reparacionesData = [];
let hospedajesData = [];
let combustiblesData = [];

let contadorReparaciones = 0;
let contadorHospedajes = 0;
let contadorCombustibles = 0;
let liquidacionModificada = false;
let formularioEnviandose = false;

// === INICIALIZACIÓN ===
$(document).ready(function () {
    console.log('✅ Dashboard Conductor iniciado');
    configurarFechasPorDefecto();
    aplicarVisibilidadPeajes();
    calcularTotales();

    const formularioLiquidacion = $('#' + SGV.pnlFormularioLiquidacion);
    formularioLiquidacion.on('input change', 'input:not([readonly]), textarea, select', function () {
        liquidacionModificada = true;
    });

    $('form').on('submit', function () {
        formularioEnviandose = true;
    });

    // Renueva la sesión mientras el conductor completa una liquidación extensa.
    window.setInterval(function () {
        if (!formularioLiquidacion.is(':visible')) return;

        $.ajax({
            type: 'POST',
            url: 'DashboardConductor.aspx/MantenerSesionActiva',
            data: '{}',
            contentType: 'application/json; charset=utf-8',
            dataType: 'json'
        });
    }, 5 * 60 * 1000);
});

window.addEventListener('beforeunload', function (event) {
    if (!liquidacionModificada || formularioEnviandose) return;
    event.preventDefault();
    event.returnValue = '';
});

// Los peajes (nacionales y extranjeros) los paga directamente la empresa a la
// concesionaria. En viajes nacionales el conductor NO debe registrarlos, por lo
// que ocultamos ambas filas y limpiamos sus montos para que no entren en la liquidación.
function aplicarVisibilidadPeajes() {
    const esInternacional = $('#hfEsInternacional').val() === '1';
    if (!esInternacional) {
        $('#filaPeajesNacionales, #filaPeajesExtranjeros').hide();
        $('#peajesNacSoles, #peajesExtDolares').val('');
        $('#descPeajesNacionales, #descPeajesExtranjeros').val('');
    } else {
        $('#filaPeajesNacionales, #filaPeajesExtranjeros').show();
    }
}

function configurarFechasPorDefecto() {
    const hoy = new Date();
    const fechaHoy = hoy.toISOString().split('T')[0];

    $('#nuevaReparacionFecha, #nuevoHospedajeFecha, #nuevoCombustibleFecha').val(fechaHoy);

    if (!$('#' + SGV.txtFechaSalida).val()) {
        $('#' + SGV.txtFechaSalida).val(fechaHoy);
    }

    if (!$('#' + SGV.txtFechaLlegada).val()) {
        $('#' + SGV.txtFechaLlegada).val(fechaHoy);
    }
}

// === GESTIÓN DE INGRESOS Y GASTOS
function agregarIngreso() {
    contadorIngresosAdicionales++;
    const numeroFila = 4 + contadorIngresosAdicionales;

    $('#ingresosAdicionalesBody').append(`
    <tr id="ingresoAdicional_${contadorIngresosAdicionales}">
        <td class="text-center">${numeroFila}</td>
        <td>
            <input type="text" class="form-control form-control-sm" name="conceptoIngreso_${contadorIngresosAdicionales}"
                   placeholder="Concepto de ingreso" required>
        </td>
        <td>
            <input type="text" class="form-control form-control-sm" name="descIngreso_${contadorIngresosAdicionales}"
                   placeholder="Descripción del ingreso">
        </td>
        <td>
            <input type="number" class="form-control form-control-sm ingreso-soles"
                   name="ingresoSoles_${contadorIngresosAdicionales}"
                   placeholder="0.00" step="0.01" onchange="calcularTotales()">
        </td>
        <td>
            <input type="number" class="form-control form-control-sm ingreso-dolares"
                   name="ingresoDolares_${contadorIngresosAdicionales}"
                   placeholder="0.00" step="0.01" onchange="calcularTotales()">
        </td>
        <td class="text-center">
            <button type="button" class="btn btn-danger btn-sm" onclick="eliminarIngreso(${contadorIngresosAdicionales})">
                <i class="fas fa-trash"></i>
            </button>
        </td>
    </tr>
`);
}

function eliminarIngreso(id) {
    if (confirm('¿Está seguro de eliminar este ingreso?')) {
        $(`#ingresoAdicional_${id}`).remove();
        calcularTotales();
    }
}

function agregarGasto() {
    contadorGastosAdicionales++;
    const numeroFila = 8 + contadorGastosAdicionales;

    $('#gastosAdicionalesBody').append(`
    <tr id="gastoAdicional_${contadorGastosAdicionales}">
        <td class="text-center">${numeroFila}</td>
        <td>
            <input type="text" class="form-control form-control-sm" name="conceptoGasto_${contadorGastosAdicionales}"
                   placeholder="Concepto de gasto" required>
        </td>
        <td>
            <input type="text" class="form-control form-control-sm" name="descGasto_${contadorGastosAdicionales}"
                   placeholder="Descripción del gasto">
        </td>
        <td>
            <input type="number" class="form-control form-control-sm gasto-soles"
                   name="gastoSoles_${contadorGastosAdicionales}"
                   placeholder="0.00" step="0.01" onchange="calcularTotales()">
        </td>
        <td>
            <input type="number" class="form-control form-control-sm gasto-dolares"
                   name="gastoDolares_${contadorGastosAdicionales}"
                   placeholder="0.00" step="0.01" onchange="calcularTotales()">
        </td>
        <td class="text-center">
            <button type="button" class="btn btn-danger btn-sm" onclick="eliminarGasto(${contadorGastosAdicionales})">
                <i class="fas fa-trash"></i>
            </button>
        </td>
    </tr>
`);
}

function eliminarGasto(id) {
    if (confirm('¿Está seguro de eliminar este gasto?')) {
        $(`#gastoAdicional_${id}`).remove();
        calcularTotales();
    }
}

function calcularTotales() {
    let totalIngresosSoles = 0;
    let totalIngresosDolares = 0;
    let totalGastosSoles = 0;
    let totalGastosDolares = 0;

    $('.ingreso-soles').each(function () {
        totalIngresosSoles += parseFloat($(this).val()) || 0;
    });

    $('.ingreso-dolares').each(function () {
        totalIngresosDolares += parseFloat($(this).val()) || 0;
    });

    $('.gasto-soles').each(function () {
        totalGastosSoles += parseFloat($(this).val()) || 0;
    });

    $('.gasto-dolares').each(function () {
        totalGastosDolares += parseFloat($(this).val()) || 0;
    });

    const diferenciaSoles = totalIngresosSoles - totalGastosSoles;
    const diferenciaDolares = totalIngresosDolares - totalGastosDolares;

    $('#totalIngresosSoles').text(totalIngresosSoles.toFixed(2));
    $('#totalIngresosDolares').text(totalIngresosDolares.toFixed(2));
    $('#totalGastosSoles').text(totalGastosSoles.toFixed(2));
    $('#totalGastosDolares').text(totalGastosDolares.toFixed(2));

    $('#diferenciaSoles').text('S/ ' + diferenciaSoles.toFixed(2));
    $('#diferenciaDolares').text('$ ' + diferenciaDolares.toFixed(2));

    $('#diferenciaSoles').css('color', diferenciaSoles >= 0 ? '#059669' : '#dc2626');
    $('#diferenciaDolares').css('color', diferenciaDolares >= 0 ? '#059669' : '#dc2626');
}

// === GESTIÓN DE PEAJES (Simplificado: Nacionales/Extranjeros) ===
// Ya no se usa modal ni detalle individual, los campos son directos en la tabla de egresos.

// === GESTIÓN DE REPARACIONES ===

function abrirModalReparaciones() {
    $('#modalReparaciones').modal('show');
    actualizarTablaReparaciones();
}

function agregarReparacion() {
    const tipo = $('#nuevaReparacionTipo').val().trim();
    const fecha = $('#nuevaReparacionFecha').val();
    const comprobante = $('#nuevaReparacionComprobante').val().trim();
    const soles = parseFloat($('#nuevaReparacionSoles').val()) || 0;
    const dolares = parseFloat($('#nuevaReparacionDolares').val()) || 0;
    const observaciones = $('#nuevaReparacionObservaciones').val().trim();

    if (!tipo) { alert('Ingrese el tipo de reparación'); $('#nuevaReparacionTipo').focus(); return; }
    if (!fecha) { alert('Seleccione una fecha'); $('#nuevaReparacionFecha').focus(); return; }
    if (soles <= 0 && dolares <= 0) { alert('Ingrese al menos un monto'); $('#nuevaReparacionSoles').focus(); return; }

    reparacionesData.push({
        id: ++contadorReparaciones,
        tipo, fecha, comprobante, soles, dolares, observaciones
    });

    $('#nuevaReparacionTipo, #nuevaReparacionComprobante, #nuevaReparacionObservaciones').val('');
    $('#nuevaReparacionSoles, #nuevaReparacionDolares').val('');

    actualizarTablaReparaciones();
    actualizarTotalesReparaciones();
}

function eliminarReparacion(id) {
    if (confirm('¿Eliminar esta reparación?')) {
        reparacionesData = reparacionesData.filter(r => r.id !== id);
        actualizarTablaReparaciones();
        actualizarTotalesReparaciones();
    }
}

function actualizarTablaReparaciones() {
    const tbody = $('#tablaReparaciones');
    tbody.empty();
    reparacionesData.forEach(r => {
        tbody.append(`
        <tr>
            <td>${escHtml(r.tipo)}</td>
            <td>${escHtml(r.fecha)}</td>
            <td>${escHtml(r.comprobante) || 'N/A'}</td>
            <td>S/ ${r.soles.toFixed(2)}</td>
            <td>$ ${r.dolares.toFixed(2)}</td>
            <td class="text-center">
                <button type="button" class="btn btn-danger btn-sm" onclick="eliminarReparacion(${r.id})">
                    <i class="fas fa-trash"></i>
                </button>
            </td>
        </tr>
    `);
    });
    $('#totalReparaciones').text(`${reparacionesData.length} reparaciones`);
}

function actualizarTotalesReparaciones() {
    const totalSoles = reparacionesData.reduce((sum, r) => sum + r.soles, 0);
    const totalDolares = reparacionesData.reduce((sum, r) => sum + r.dolares, 0);

    $('#totalReparacionesSoles').text(`S/ ${totalSoles.toFixed(2)}`);
    $('#totalReparacionesDolares').text(`$ ${totalDolares.toFixed(2)}`);
    $('#reparacionesSoles').val(totalSoles > 0 ? totalSoles.toFixed(2) : '');
    $('#reparacionesDolares').val(totalDolares > 0 ? totalDolares.toFixed(2) : '');
    $('#descReparaciones').val(reparacionesData.length > 0 ? `${reparacionesData.length} reparaciones` : 'Sin reparaciones');
    $('#contadorReparaciones').text(reparacionesData.length);

    calcularTotales();
}

function aplicarReparaciones() {
    $('#modalReparaciones').modal('hide');
}

// === GESTIÓN DE HOSPEDAJE ===

function abrirModalHospedaje() {
    $('#modalHospedaje').modal('show');
    actualizarTablaHospedajes();
}

function agregarHospedaje() {
    const lugar = $('#nuevoHospedajeLugar').val().trim();
    const fecha = $('#nuevoHospedajeFecha').val();
    const comprobante = $('#nuevoHospedajeComprobante').val().trim();
    const soles = parseFloat($('#nuevoHospedajeSoles').val()) || 0;
    const dolares = parseFloat($('#nuevoHospedajeDolares').val()) || 0;
    const observaciones = $('#nuevoHospedajeObservaciones').val().trim();

    if (!lugar) { alert('Ingrese el lugar'); $('#nuevoHospedajeLugar').focus(); return; }
    if (!fecha) { alert('Seleccione una fecha'); $('#nuevoHospedajeFecha').focus(); return; }
    if (soles <= 0 && dolares <= 0) { alert('Ingrese al menos un monto'); $('#nuevoHospedajeSoles').focus(); return; }

    hospedajesData.push({
        id: ++contadorHospedajes,
        lugar, fecha, comprobante, soles, dolares, observaciones
    });

    $('#nuevoHospedajeLugar, #nuevoHospedajeComprobante, #nuevoHospedajeObservaciones').val('');
    $('#nuevoHospedajeSoles, #nuevoHospedajeDolares').val('');

    actualizarTablaHospedajes();
    actualizarTotalesHospedajes();
}

function eliminarHospedaje(id) {
    if (confirm('¿Eliminar este hospedaje?')) {
        hospedajesData = hospedajesData.filter(h => h.id !== id);
        actualizarTablaHospedajes();
        actualizarTotalesHospedajes();
    }
}

function actualizarTablaHospedajes() {
    const tbody = $('#tablaHospedajes');
    tbody.empty();
    hospedajesData.forEach(h => {
        tbody.append(`
        <tr>
            <td>${escHtml(h.lugar)}</td>
            <td>${escHtml(h.fecha)}</td>
            <td>${escHtml(h.comprobante) || 'N/A'}</td>
            <td>S/ ${h.soles.toFixed(2)}</td>
            <td>$ ${h.dolares.toFixed(2)}</td>
            <td class="text-center">
                <button type="button" class="btn btn-danger btn-sm" onclick="eliminarHospedaje(${h.id})">
                    <i class="fas fa-trash"></i>
                </button>
            </td>
        </tr>
    `);
    });
    $('#totalHospedajes').text(`${hospedajesData.length} hospedajes`);
}

function actualizarTotalesHospedajes() {
    const totalSoles = hospedajesData.reduce((sum, h) => sum + h.soles, 0);
    const totalDolares = hospedajesData.reduce((sum, h) => sum + h.dolares, 0);

    $('#totalHospedajesSoles').text(`S/ ${totalSoles.toFixed(2)}`);
    $('#totalHospedajesDolares').text(`$ ${totalDolares.toFixed(2)}`);
    $('#hospedajeSoles').val(totalSoles > 0 ? totalSoles.toFixed(2) : '');
    $('#hospedajeDolares').val(totalDolares > 0 ? totalDolares.toFixed(2) : '');
    $('#descHospedaje').val(hospedajesData.length > 0 ? `${hospedajesData.length} hospedajes` : 'Sin hospedajes');
    $('#contadorHospedaje').text(hospedajesData.length);

    calcularTotales();
}

function aplicarHospedajes() {
    $('#modalHospedaje').modal('hide');
}

// === GESTIÓN DE COMBUSTIBLE ===

function abrirModalCombustible() {
    $('#modalCombustible').modal('show');
    actualizarTablaCombustibles();
}

function agregarCombustible() {
    const lugar = $('#nuevoCombustibleLugar').val().trim();
    const fecha = $('#nuevoCombustibleFecha').val();
    const comprobante = $('#nuevoCombustibleComprobante').val().trim();
    const soles = parseFloat($('#nuevoCombustibleSoles').val()) || 0;
    const dolares = parseFloat($('#nuevoCombustibleDolares').val()) || 0;
    const observaciones = $('#nuevoCombustibleObservaciones').val().trim();

    if (!lugar) { alert('Ingrese el lugar'); $('#nuevoCombustibleLugar').focus(); return; }
    if (!fecha) { alert('Seleccione una fecha'); $('#nuevoCombustibleFecha').focus(); return; }
    if (soles <= 0 && dolares <= 0) { alert('Ingrese al menos un monto'); $('#nuevoCombustibleSoles').focus(); return; }

    combustiblesData.push({
        id: ++contadorCombustibles,
        lugar, fecha, comprobante, soles, dolares, observaciones
    });

    $('#nuevoCombustibleLugar, #nuevoCombustibleComprobante, #nuevoCombustibleObservaciones').val('');
    $('#nuevoCombustibleSoles, #nuevoCombustibleDolares').val('');

    actualizarTablaCombustibles();
    actualizarTotalesCombustibles();
}

function eliminarCombustible(id) {
    if (confirm('¿Eliminar este combustible?')) {
        combustiblesData = combustiblesData.filter(c => c.id !== id);
        actualizarTablaCombustibles();
        actualizarTotalesCombustibles();
    }
}

function actualizarTablaCombustibles() {
    const tbody = $('#tablaCombustibles');
    tbody.empty();
    combustiblesData.forEach(c => {
        tbody.append(`
        <tr>
            <td>${escHtml(c.lugar)}</td>
            <td>${escHtml(c.fecha)}</td>
            <td>${escHtml(c.comprobante) || 'N/A'}</td>
            <td>S/ ${c.soles.toFixed(2)}</td>
            <td>$ ${c.dolares.toFixed(2)}</td>
            <td class="text-center">
                <button type="button" class="btn btn-danger btn-sm" onclick="eliminarCombustible(${c.id})">
                    <i class="fas fa-trash"></i>
                </button>
            </td>
        </tr>
    `);
    });
    $('#totalCombustibles').text(`${combustiblesData.length} combustibles`);
}

function actualizarTotalesCombustibles() {
    const totalSoles = combustiblesData.reduce((sum, c) => sum + c.soles, 0);
    const totalDolares = combustiblesData.reduce((sum, c) => sum + c.dolares, 0);

    $('#totalCombustiblesSoles').text(`S/ ${totalSoles.toFixed(2)}`);
    $('#totalCombustiblesDolares').text(`$ ${totalDolares.toFixed(2)}`);
    $('#combustibleSoles').val(totalSoles > 0 ? totalSoles.toFixed(2) : '');
    $('#combustibleDolares').val(totalDolares > 0 ? totalDolares.toFixed(2) : '');
    $('#descCombustible').val(combustiblesData.length > 0 ? `${combustiblesData.length} combustibles` : 'Sin combustibles');
    $('#contadorCombustible').text(combustiblesData.length);

    calcularTotales();
}

function aplicarCombustibles() {
    $('#modalCombustible').modal('hide');
}

// === PREPARAR DATOS PARA ENVÍO ===

function prepararDatosFinancieros() {
    const gastosFinancieros = [];

    // Peajes Nacionales (Perú) - solo Soles
    const pnSoles = parseFloat($('#peajesNacSoles').val()) || 0;
    if (pnSoles > 0) {
        gastosFinancieros.push({
            categoria: 'Peajes',
            estacion: 'Peajes Nacionales (Perú)',
            soles: pnSoles,
            dolares: 0,
            observaciones: $('#descPeajesNacionales').val() || ''
        });
    }

    // Peajes Extranjeros (Ecuador) - solo Dólares
    const peDolares = parseFloat($('#peajesExtDolares').val()) || 0;
    if (peDolares > 0) {
        gastosFinancieros.push({
            categoria: 'Peajes',
            estacion: 'Peajes Extranjeros (Ecuador)',
            soles: 0,
            dolares: peDolares,
            observaciones: $('#descPeajesExtranjeros').val() || ''
        });
    }

    // Otros gastos detallados
    gastosFinancieros.push(
        ...reparacionesData.map(r => ({ categoria: 'Reparaciones', ...r })),
        ...hospedajesData.map(h => ({ categoria: 'Hospedaje', ...h })),
        ...combustiblesData.map(c => ({ categoria: 'Combustible', ...c }))
    );
    $('#hfGastosFinancieros').val(JSON.stringify(gastosFinancieros));

    const ingresosAdicionales = [];
    $('#ingresosAdicionalesBody tr').each(function () {
        const concepto = $(this).find('input[name^="conceptoIngreso_"]').val()?.trim() || '';
        const desc = $(this).find('input[name^="descIngreso_"]').val()?.trim() || '';
        const soles = parseFloat($(this).find('input[name^="ingresoSoles_"]').val()) || 0;
        const dolares = parseFloat($(this).find('input[name^="ingresoDolares_"]').val()) || 0;
        if (concepto && (soles > 0 || dolares > 0)) {
            ingresosAdicionales.push({ categoria: concepto, nombreCategoria: concepto, descripcion: desc, soles, dolares });
        }
    });
    $('#hfIngresosAdicionales').val(JSON.stringify(ingresosAdicionales));

    const gastosAdicionales = [];
    $('#gastosAdicionalesBody tr').each(function () {
        const concepto = $(this).find('input[name^="conceptoGasto_"]').val()?.trim() || '';
        const desc = $(this).find('input[name^="descGasto_"]').val()?.trim() || '';
        const soles = parseFloat($(this).find('input[name^="gastoSoles_"]').val()) || 0;
        const dolares = parseFloat($(this).find('input[name^="gastoDolares_"]').val()) || 0;
        if (concepto && (soles > 0 || dolares > 0)) {
            gastosAdicionales.push({ categoria: concepto, nombreCategoria: concepto, descripcion: desc, soles, dolares });
        }
    });
    $('#hfGastosAdicionales').val(JSON.stringify(gastosAdicionales));
}

// === FUNCIONES AUXILIARES ===

function confirmarEnvioLiquidacion() {
    return confirm(
        '¿Está seguro de enviar esta liquidación?\n\n' +
        'Una vez enviada, no podrá modificarla y quedará pendiente de revisión por la administración.\n\n' +
        'Asegúrese de que todos los datos son correctos.'
    );
}

function limpiarFormulario() {
    if (confirm('¿Está seguro de limpiar el formulario? Se perderán todos los datos ingresados.')) {
        liquidacionModificada = false;
        location.reload();
    }
}

function verDetalleLiquidacion(idOrdenViaje) {
    window.location.href = `DetalleOrdenViaje.aspx?id=${idOrdenViaje}`;
}

// === Retirar Liquidación ===
var idOrdenViajeARetirar = 0;

function abrirModalRetirar(idOrdenViaje) {
    idOrdenViajeARetirar = idOrdenViaje;
    $('#modalRetirarLiquidacion').modal('show');
}

function confirmarRetirar() {
    if (idOrdenViajeARetirar === 0) return;

    var btnConfirmar = $('#btnConfirmarRetirar');
    btnConfirmar.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-1"></i>Procesando...');

    $.ajax({
        type: 'POST',
        url: 'DashboardConductor.aspx/RetirarLiquidacion',
        data: JSON.stringify({ idOrdenViaje: idOrdenViajeARetirar }),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            var data = response.d;
            $('#modalRetirarLiquidacion').modal('hide');

            if (data.success) {
                alert('✅ ' + data.message);
                location.reload();
            } else {
                alert('⚠️ ' + data.message);
            }
        },
        error: function (xhr) {
            $('#modalRetirarLiquidacion').modal('hide');
            alert('❌ Error de comunicación. Intente nuevamente.');
            console.error(xhr.responseText);
        },
        complete: function () {
            btnConfirmar.prop('disabled', false).html('<i class="fas fa-undo mr-1"></i>Sí, Retirar');
            idOrdenViajeARetirar = 0;
        }
    });
}

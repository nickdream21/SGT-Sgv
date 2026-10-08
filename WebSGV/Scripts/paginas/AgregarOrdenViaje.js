// Variables globales
let contadorIngresosAdicionales = 0;
let contadorGastosAdicionales = 0;

// Datos de modales
let peajesData = [];
let reparacionesData = [];
let hospedajesData = [];
let combustiblesData = [];

let contadorPeajes = 0;
let contadorReparaciones = 0;
let contadorHospedajes = 0;
let contadorCombustibles = 0;

let estacionesPeaje = [];

// Inicialización
$(document).ready(function () {
    console.log('✅ Sistema iniciado');
    actualizarTituloPagina();
    detectarOrigenViaje();
    configurarFechasPorDefecto();
    cargarEstacionesPeaje();
    calcularTotales();
});

function actualizarTituloPagina() {
    var idOrden = parseInt($('#hfIdOrdenViaje').val()) || 0;
    if (idOrden > 0) {
        $('#pageTitleText').text('Editar Orden de Viaje');
        $('#pageTitleSub').text('Modifique los datos del viaje y guarde los cambios');
    }
}

function detectarOrigenViaje() {
    const urlParams = new URLSearchParams(window.location.search);
    const origen = urlParams.get('origen');
    if (origen === 'viajeFinalizado') {
        $('#hfOrigenViaje').val('viajeFinalizado');
    }
}

function configurarFechasPorDefecto() {
    const hoy = new Date().toISOString().split('T')[0];
    $('#nuevoPeajeFecha, #nuevaReparacionFecha, #nuevoHospedajeFecha, #nuevoCombustibleFecha').val(hoy);

    if (!$('#' + SGV.txtFechaSalida).val()) {
        $('#' + SGV.txtFechaSalida).val(hoy);
    }
    if (!$('#' + SGV.txtFechaLlegada).val()) {
        const manana = new Date();
        manana.setDate(manana.getDate() + 1);
        $('#' + SGV.txtFechaLlegada).val(manana.toISOString().split('T')[0]);
    }
}

function mostrarDetallesViajeOrigen() {
    $('#pnlDetallesViajeOrigen').collapse('toggle');
}

// INGRESOS
function agregarIngreso() {
    contadorIngresosAdicionales++;
    const numeroFila = 4 + contadorIngresosAdicionales;
    $('#ingresosAdicionalesBody').append(`
        <tr id="ingresoAdicional_${contadorIngresosAdicionales}">
            <td class="text-center">${numeroFila}</td>
            <td><input type="text" class="form-control form-control-sm" name="conceptoIngreso_${contadorIngresosAdicionales}" placeholder="Concepto" required></td>
            <td><input type="text" class="form-control form-control-sm" name="descIngreso_${contadorIngresosAdicionales}" placeholder="Descripción"></td>
            <td><input type="number" class="form-control form-control-sm ingreso-soles" name="ingresoSoles_${contadorIngresosAdicionales}" placeholder="0.00" step="0.01" onchange="calcularTotales()"></td>
            <td><input type="number" class="form-control form-control-sm ingreso-dolares" name="ingresoDolares_${contadorIngresosAdicionales}" placeholder="0.00" step="0.01" onchange="calcularTotales()"></td>
            <td class="text-center"><button type="button" class="btn btn-danger btn-sm" onclick="eliminarIngreso(${contadorIngresosAdicionales})"><i class="fas fa-trash"></i></button></td>
        </tr>
    `);
}

function eliminarIngreso(id) {
    if (confirm('¿Eliminar este ingreso?')) {
        $(`#ingresoAdicional_${id}`).remove();
        calcularTotales();
    }
}

// GASTOS
function agregarGasto() {
    contadorGastosAdicionales++;
    const numeroFila = 8 + contadorGastosAdicionales;
    $('#gastosAdicionalesBody').append(`
        <tr id="gastoAdicional_${contadorGastosAdicionales}">
            <td class="text-center">${numeroFila}</td>
            <td><input type="text" class="form-control form-control-sm" name="conceptoGasto_${contadorGastosAdicionales}" placeholder="Concepto" required></td>
            <td><input type="text" class="form-control form-control-sm" name="descGasto_${contadorGastosAdicionales}" placeholder="Descripción"></td>
            <td><input type="number" class="form-control form-control-sm gasto-soles" name="gastoSoles_${contadorGastosAdicionales}" placeholder="0.00" step="0.01" onchange="calcularTotales()"></td>
            <td><input type="number" class="form-control form-control-sm gasto-dolares" name="gastoDolares_${contadorGastosAdicionales}" placeholder="0.00" step="0.01" onchange="calcularTotales()"></td>
            <td class="text-center"><button type="button" class="btn btn-danger btn-sm" onclick="eliminarGasto(${contadorGastosAdicionales})"><i class="fas fa-trash"></i></button></td>
        </tr>
    `);
}

function eliminarGasto(id) {
    if (confirm('¿Eliminar este gasto?')) {
        $(`#gastoAdicional_${id}`).remove();
        calcularTotales();
    }
}

// CALCULAR TOTALES
function calcularTotales() {
    let totalIngresosSoles = 0, totalIngresosDolares = 0;
    let totalGastosSoles = 0, totalGastosDolares = 0;

    $('.ingreso-soles').each(function () { totalIngresosSoles += parseFloat($(this).val()) || 0; });
    $('.ingreso-dolares').each(function () { totalIngresosDolares += parseFloat($(this).val()) || 0; });
    $('.gasto-soles').each(function () { totalGastosSoles += parseFloat($(this).val()) || 0; });
    $('.gasto-dolares').each(function () { totalGastosDolares += parseFloat($(this).val()) || 0; });

    const descuentoSoles = parseFloat($('#descuentoSoles').val()) || 0;
    const descuentoDolares = parseFloat($('#descuentoDolares').val()) || 0;
    const reintegroSoles = parseFloat($('#reintegroSoles').val()) || 0;
    const reintegroDolares = parseFloat($('#reintegroDolares').val()) || 0;

    const diferenciaSoles = totalIngresosSoles - totalGastosSoles - descuentoSoles + reintegroSoles;
    const diferenciaDolares = totalIngresosDolares - totalGastosDolares - descuentoDolares + reintegroDolares;

    $('#totalIngresosSoles').text(totalIngresosSoles.toFixed(2));
    $('#totalIngresosDolares').text(totalIngresosDolares.toFixed(2));
    $('#totalGastosSoles').text(totalGastosSoles.toFixed(2));
    $('#totalGastosDolares').text(totalGastosDolares.toFixed(2));
    var solPos = diferenciaSoles >= 0, dolPos = diferenciaDolares >= 0;
    $('#diferenciaSoles').text(diferenciaSoles.toFixed(2));
    $('#diferenciaDolares').text(diferenciaDolares.toFixed(2));
    var $bar = $('#balanceFinalBar');
    $bar.removeClass('balance-positivo balance-negativo');
    if (!solPos || !dolPos) {
        $bar.addClass('balance-negativo');
    } else {
        $bar.addClass('balance-positivo');
    }
}

// PEAJES
function cargarEstacionesPeaje() {
    try {
        const json = SGV.ObtenerEstacionesPeajeJSON;
        if (json && json !== '[]') {
            estacionesPeaje = JSON.parse(json);
            llenarDatalistEstaciones();
        }
    } catch (e) {
        console.error('Error cargando estaciones:', e);
    }
}

function llenarDatalistEstaciones() {
    const datalist = $('#listaEstaciones');
    datalist.empty();
    estacionesPeaje.forEach(est => {
        datalist.append(`<option value="${est.nombre}">`);
    });
}

function abrirModalPeajes() {
    $('#modalPeajes').modal('show');
    actualizarTablaPeajes();
}

function agregarPeaje() {
    const estacion = $('#nuevoPeajeEstacion').val().trim();
    const fecha = $('#nuevoPeajeFecha').val();
    const soles = parseFloat($('#nuevoPeajeSoles').val()) || 0;
    const dolares = parseFloat($('#nuevoPeajeDolares').val()) || 0;

    if (!estacion) { alert('⚠️ Seleccione una estación'); $('#nuevoPeajeEstacion').focus(); return; }
    if (!fecha) { alert('⚠️ Seleccione una fecha'); $('#nuevoPeajeFecha').focus(); return; }
    if (soles <= 0 && dolares <= 0) { alert('⚠️ Ingrese al menos un monto'); $('#nuevoPeajeSoles').focus(); return; }

    peajesData.push({
        id: ++contadorPeajes,
        estacion: estacion,
        fecha: fecha,
        comprobante: $('#nuevoPeajeComprobante').val().trim(),
        soles: soles,
        dolares: dolares,
        observaciones: $('#nuevoPeajeObservaciones').val().trim()
    });

    $('#nuevoPeajeEstacion, #nuevoPeajeComprobante, #nuevoPeajeObservaciones').val('');
    $('#nuevoPeajeSoles, #nuevoPeajeDolares').val('');
    actualizarTablaPeajes();
    actualizarTotalesPeajes();
}

function eliminarPeaje(id) {
    if (confirm('¿Eliminar este peaje?')) {
        peajesData = peajesData.filter(p => p.id !== id);
        actualizarTablaPeajes();
        actualizarTotalesPeajes();
    }
}

function actualizarTablaPeajes() {
    const tbody = $('#tablaPeajes');
    tbody.empty();
    peajesData.forEach(p => {
        tbody.append(`
            <tr>
                <td>${p.estacion}</td>
                <td>${p.fecha}</td>
                <td>${p.comprobante || 'N/A'}</td>
                <td>S/ ${p.soles.toFixed(2)}</td>
                <td>$ ${p.dolares.toFixed(2)}</td>
                <td class="text-center"><button type="button" class="btn btn-danger btn-sm" onclick="eliminarPeaje(${p.id})"><i class="fas fa-trash"></i></button></td>
            </tr>
        `);
    });
    $('#totalPeajes').text(`${peajesData.length} peajes`);
}

function actualizarTotalesPeajes() {
    const totalS = peajesData.reduce((sum, p) => sum + p.soles, 0);
    const totalD = peajesData.reduce((sum, p) => sum + p.dolares, 0);
    $('#totalPeajesSoles').text(`S/ ${totalS.toFixed(2)}`);
    $('#totalPeajesDolares').text(`$ ${totalD.toFixed(2)}`);
    $('#peajesSoles').val(totalS > 0 ? totalS.toFixed(2) : '');
    $('#peajesDolares').val(totalD > 0 ? totalD.toFixed(2) : '');
    $('#descPeajes').val(peajesData.length > 0 ? `${peajesData.length} peajes registrados` : '');
    $('#contadorPeajes').text(peajesData.length);
    calcularTotales();
}

function aplicarPeajes() {
    $('#modalPeajes').modal('hide');
}

// REPARACIONES
function abrirModalReparaciones() {
    $('#modalReparaciones').modal('show');
    actualizarTablaReparaciones();
}

function agregarReparacion() {
    const tipo = $('#nuevaReparacionTipo').val().trim();
    const fecha = $('#nuevaReparacionFecha').val();
    const soles = parseFloat($('#nuevaReparacionSoles').val()) || 0;
    const dolares = parseFloat($('#nuevaReparacionDolares').val()) || 0;

    if (!tipo) { alert('Ingrese el tipo de reparación'); return; }
    if (!fecha) { alert('Seleccione una fecha'); return; }
    if (soles <= 0 && dolares <= 0) { alert('Ingrese al menos un monto'); return; }

    reparacionesData.push({
        id: ++contadorReparaciones,
        tipo: tipo,
        fecha: fecha,
        comprobante: $('#nuevaReparacionComprobante').val().trim(),
        soles: soles,
        dolares: dolares,
        observaciones: $('#nuevaReparacionObservaciones').val().trim()
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
                <td>${r.tipo}</td>
                <td>${r.fecha}</td>
                <td>${r.comprobante || 'N/A'}</td>
                <td>S/ ${r.soles.toFixed(2)}</td>
                <td>$ ${r.dolares.toFixed(2)}</td>
                <td class="text-center"><button type="button" class="btn btn-danger btn-sm" onclick="eliminarReparacion(${r.id})"><i class="fas fa-trash"></i></button></td>
            </tr>
        `);
    });
    $('#totalReparaciones').text(`${reparacionesData.length} reparaciones`);
}

function actualizarTotalesReparaciones() {
    const totalS = reparacionesData.reduce((sum, r) => sum + r.soles, 0);
    const totalD = reparacionesData.reduce((sum, r) => sum + r.dolares, 0);
    $('#totalReparacionesSoles').text(`S/ ${totalS.toFixed(2)}`);
    $('#totalReparacionesDolares').text(`$ ${totalD.toFixed(2)}`);
    $('#reparacionesSoles').val(totalS > 0 ? totalS.toFixed(2) : '');
    $('#reparacionesDolares').val(totalD > 0 ? totalD.toFixed(2) : '');
    $('#descReparaciones').val(reparacionesData.length > 0 ? `${reparacionesData.length} reparaciones` : '');
    $('#contadorReparaciones').text(reparacionesData.length);
    calcularTotales();
}

function aplicarReparaciones() {
    $('#modalReparaciones').modal('hide');
}

// HOSPEDAJE
function abrirModalHospedaje() {
    $('#modalHospedaje').modal('show');
    actualizarTablaHospedajes();
}

function agregarHospedaje() {
    const lugar = $('#nuevoHospedajeLugar').val().trim();
    const fecha = $('#nuevoHospedajeFecha').val();
    const soles = parseFloat($('#nuevoHospedajeSoles').val()) || 0;
    const dolares = parseFloat($('#nuevoHospedajeDolares').val()) || 0;

    if (!lugar) { alert('Ingrese el lugar'); return; }
    if (!fecha) { alert('Seleccione una fecha'); return; }
    if (soles <= 0 && dolares <= 0) { alert('Ingrese al menos un monto'); return; }

    hospedajesData.push({
        id: ++contadorHospedajes,
        lugar: lugar,
        fecha: fecha,
        comprobante: $('#nuevoHospedajeComprobante').val().trim(),
        soles: soles,
        dolares: dolares,
        observaciones: $('#nuevoHospedajeObservaciones').val().trim()
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
                <td>${h.lugar}</td>
                <td>${h.fecha}</td>
                <td>${h.comprobante || 'N/A'}</td>
                <td>S/ ${h.soles.toFixed(2)}</td>
                <td>$ ${h.dolares.toFixed(2)}</td>
                <td class="text-center"><button type="button" class="btn btn-danger btn-sm" onclick="eliminarHospedaje(${h.id})"><i class="fas fa-trash"></i></button></td>
            </tr>
        `);
    });
    $('#totalHospedajes').text(`${hospedajesData.length} hospedajes`);
}

function actualizarTotalesHospedajes() {
    const totalS = hospedajesData.reduce((sum, h) => sum + h.soles, 0);
    const totalD = hospedajesData.reduce((sum, h) => sum + h.dolares, 0);
    $('#totalHospedajesSoles').text(`S/ ${totalS.toFixed(2)}`);
    $('#totalHospedajesDolares').text(`$ ${totalD.toFixed(2)}`);
    $('#hospedajeSoles').val(totalS > 0 ? totalS.toFixed(2) : '');
    $('#hospedajeDolares').val(totalD > 0 ? totalD.toFixed(2) : '');
    $('#descHospedaje').val(hospedajesData.length > 0 ? `${hospedajesData.length} hospedajes` : '');
    $('#contadorHospedaje').text(hospedajesData.length);
    calcularTotales();
}

function aplicarHospedajes() {
    $('#modalHospedaje').modal('hide');
}

// COMBUSTIBLE
function abrirModalCombustible() {
    $('#modalCombustible').modal('show');
    actualizarTablaCombustibles();
}

function agregarCombustible() {
    const lugar = $('#nuevoCombustibleLugar').val().trim();
    const fecha = $('#nuevoCombustibleFecha').val();
    const soles = parseFloat($('#nuevoCombustibleSoles').val()) || 0;
    const dolares = parseFloat($('#nuevoCombustibleDolares').val()) || 0;

    if (!lugar) { alert('Ingrese el lugar'); return; }
    if (!fecha) { alert('Seleccione una fecha'); return; }
    if (soles <= 0 && dolares <= 0) { alert('Ingrese al menos un monto'); return; }

    combustiblesData.push({
        id: ++contadorCombustibles,
        lugar: lugar,
        fecha: fecha,
        comprobante: $('#nuevoCombustibleComprobante').val().trim(),
        soles: soles,
        dolares: dolares,
        observaciones: $('#nuevoCombustibleObservaciones').val().trim()
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
                <td>${c.lugar}</td>
                <td>${c.fecha}</td>
                <td>${c.comprobante || 'N/A'}</td>
                <td>S/ ${c.soles.toFixed(2)}</td>
                <td>$ ${c.dolares.toFixed(2)}</td>
                <td class="text-center"><button type="button" class="btn btn-danger btn-sm" onclick="eliminarCombustible(${c.id})"><i class="fas fa-trash"></i></button></td>
            </tr>
        `);
    });
    $('#totalCombustibles').text(`${combustiblesData.length} combustibles`);
}

function actualizarTotalesCombustibles() {
    const totalS = combustiblesData.reduce((sum, c) => sum + c.soles, 0);
    const totalD = combustiblesData.reduce((sum, c) => sum + c.dolares, 0);
    $('#totalCombustiblesSoles').text(`S/ ${totalS.toFixed(2)}`);
    $('#totalCombustiblesDolares').text(`$ ${totalD.toFixed(2)}`);
    $('#combustibleSoles').val(totalS > 0 ? totalS.toFixed(2) : '');
    $('#combustibleDolares').val(totalD > 0 ? totalD.toFixed(2) : '');
    $('#descCombustible').val(combustiblesData.length > 0 ? `${combustiblesData.length} combustibles` : '');
    $('#contadorCombustible').text(combustiblesData.length);
    calcularTotales();
}

function aplicarCombustibles() {
    $('#modalCombustible').modal('hide');
}

// PREPARAR DATOS
function prepararDatosFinancieros() {
    const gastosFinancieros = [
        ...peajesData.map(p => ({ categoria: 'Peajes', ...p })),
        ...reparacionesData.map(r => ({ categoria: 'Reparaciones', ...r })),
        ...hospedajesData.map(h => ({ categoria: 'Hospedaje', ...h })),
        ...combustiblesData.map(c => ({ categoria: 'Combustible', ...c }))
    ];
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

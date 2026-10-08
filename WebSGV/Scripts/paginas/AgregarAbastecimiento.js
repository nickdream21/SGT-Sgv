let contadorTickets = 0;
var esModoViaje = SGV.hdnModoViajeValue === '1';

function agregarTicket() {
    contadorTickets++;
    const tableBody = document.getElementById('ticketsTableBody');
    const newRow = document.createElement('tr');
    newRow.id = 'ticket_' + contadorTickets;
    newRow.innerHTML =
        '<td>' + contadorTickets + '</td>' +
        '<td><input type="number" id="costo_' + contadorTickets + '" step="0.01" placeholder="548.54" onchange="actualizarTotalesTickets()"></td>' +
        '<td><input type="number" id="galones_' + contadorTickets + '" step="0.01" placeholder="305.2" onchange="actualizarTotalesTickets()"></td>' +
        '<td><button type="button" class="btn-remove-ticket" onclick="eliminarTicket(' + contadorTickets + ')"><i class="fas fa-trash"></i></button></td>';
    tableBody.appendChild(newRow);
    actualizarTotalesTickets();
}

function eliminarTicket(id) {
    var row = document.getElementById('ticket_' + id);
    if (row) { row.remove(); actualizarTotalesTickets(); }
}

function actualizarTotalesTickets() {
    var totalTickets = 0, costoTotal = 0, galonesTotal = 0;
    var rows = document.querySelectorAll('#ticketsTableBody tr');
    rows.forEach(function (row) {
        var costoInput = row.querySelector('input[id^="costo_"]');
        var galonesInput = row.querySelector('input[id^="galones_"]');
        if (costoInput && galonesInput) {
            var costo = parseFloat(costoInput.value) || 0;
            var galones = parseFloat(galonesInput.value) || 0;
            if (costo > 0 || galones > 0) { totalTickets++; costoTotal += costo; galonesTotal += galones; }
        }
    });
    document.getElementById('totalTickets').textContent = totalTickets;
    document.getElementById('costoTotalTickets').textContent = '$ ' + costoTotal.toFixed(2);
    document.getElementById('galonesTotalesTickets').textContent = galonesTotal.toFixed(2) + ' GL';
    sincronizarCamposPrincipales(costoTotal, galonesTotal);
    guardarTicketsData();
}

function sincronizarCamposPrincipales(costoTotal, galonesTotal) {
    var elGL = document.getElementById(SGV.txtGLComprados);
    var elMonto = document.getElementById(SGV.txtMontoTotal);
    if (elGL) elGL.value = galonesTotal.toFixed(2);
    if (elMonto) elMonto.value = costoTotal.toFixed(2);
    calcularTotales();
}

function guardarTicketsData() {
    var tickets = [];
    document.querySelectorAll('#ticketsTableBody tr').forEach(function (row) {
        var c = row.querySelector('input[id^="costo_"]');
        var g = row.querySelector('input[id^="galones_"]');
        if (c && g) {
            var cv = parseFloat(c.value) || 0, gv = parseFloat(g.value) || 0;
            if (cv > 0 || gv > 0) tickets.push({ costo: cv, galones: gv });
        }
    });
    var hdn = document.getElementById(SGV.hdnTicketsData);
    if (hdn) hdn.value = JSON.stringify(tickets);
}

function calcularTotales() {
    var glRuta = parseFloat((document.getElementById(SGV.txtGLRuta).value || "0").replace(",", ".")) || 0;
    var glComprados = parseFloat((document.getElementById(SGV.txtGLComprados).value || "0").replace(",", ".")) || 0;
    var glFinal = parseFloat((document.getElementById(SGV.txtGLFinal).value || "0").replace(",", ".")) || 0;
    var totalAbastecido = glRuta + glComprados;
    document.getElementById(SGV.txtTotalGL).value = totalAbastecido.toFixed(2);
    var totalConsumido = totalAbastecido - glFinal;
    if (totalConsumido < 0) totalConsumido = 0;
    document.getElementById(SGV.txtGLConsumidos).value = totalConsumido.toFixed(2);
    actualizarNivelCombustible(glFinal, totalAbastecido);
    calcularRendimiento();
}

function calcularRendimiento() {
    var distancia = parseFloat((document.getElementById(SGV.txtDistancia).value || "0").replace(",", ".")) || 0;
    var consumido = parseFloat((document.getElementById(SGV.txtGLConsumidos).value || "0").replace(",", ".")) || 0;
    document.getElementById('rendimientoPromedio').textContent = (distancia > 0 && consumido > 0) ? (distancia / consumido).toFixed(2) : "0.00";
}

function actualizarNivelCombustible(actual, total) {
    var el = document.getElementById('fuelLevelVisual');
    if (el) {
        if (total > 0) { var p = (actual / total) * 100; el.style.width = Math.min(p, 100) + '%'; }
        else { el.style.width = '0%'; }
    }
}

document.addEventListener('DOMContentLoaded', function () {
    var fechaInput = document.getElementById(SGV.txtFecha);
    var horaInput = document.getElementById(SGV.txtHora);
    if (!fechaInput.value) fechaInput.value = new Date().toISOString().split('T')[0];
    if (!horaInput.value) {
        var ahora = new Date();
        horaInput.value = ahora.getHours().toString().padStart(2, '0') + ":" + ahora.getMinutes().toString().padStart(2, '0');
    }
    agregarTicket();
    calcularTotales();
    onTipoVehiculoChange();
});

function onTipoVehiculoChange() {
    var ddl = document.getElementById(SGV.tipoVehiculo);
    if (!ddl) return;
    var txt = (ddl.options[ddl.selectedIndex] ? ddl.options[ddl.selectedIndex].text : '').toUpperCase();

    var divTracto    = document.getElementById('divPlacaTracto');
    var divVolquete  = document.getElementById('divPlacaVolquete');
    var divCamioneta = document.getElementById('divPlacaCamioneta');
    var divOtro      = document.getElementById('divPlacaOtro');
    var divCarreta   = document.getElementById('divCarreta');

    // Ocultar todos
    if (divTracto) divTracto.style.display = 'none';
    if (divVolquete) divVolquete.style.display = 'none';
    if (divCamioneta) divCamioneta.style.display = 'none';
    if (divOtro) divOtro.style.display = 'none';
    if (divCarreta) divCarreta.style.display = 'none';

    if (txt.indexOf('VOLQUETE') >= 0) {
        if (divVolquete) divVolquete.style.display = '';
    } else if (txt.indexOf('CAMIONETA') >= 0) {
        if (divCamioneta) divCamioneta.style.display = '';
    } else if (txt.indexOf('TRAILER') >= 0 || txt.indexOf('TRÁILER') >= 0 ||
               txt.indexOf('TRACTO') >= 0 || txt.indexOf('CAMIÓN') >= 0 || txt.indexOf('CAMION') >= 0) {
        if (divTracto) divTracto.style.display = '';
        if (divCarreta) divCarreta.style.display = '';
    } else {
        if (divOtro) divOtro.style.display = '';
    }
}

function onMotivoChange() {
    var ddl = document.getElementById(SGV.ddlMotivoSalida);
    var hint = document.getElementById('motivoHintText');
    if (!ddl || !hint) return;
    var val = ddl.value;
    var esFlexible = (val === 'MANTENIMIENTO' || val === 'OTRO');

    // Actualizar hint descriptivo según motivo
    if (val === 'MANTENIMIENTO') {
        hint.textContent = 'Salida de combustible para mantenimiento: filtros, limpieza, servicio técnico. Conductor, placa, producto y tickets son opcionales.';
    } else if (val === 'OTRO') {
        hint.textContent = 'Uso especial: generadores, equipos, pruebas u otros. Conductor, placa, producto y tickets son opcionales. Detalle en Observaciones.';
    } else {
        hint.textContent = 'Rellenado de combustible para viaje corto o rutina. Producto, tickets y GL Ruta son opcionales.';
    }

    // Campos opcionales según tipo:
    // MANTENIMIENTO/OTRO: placa, conductor, producto, GL Ruta son opcionales
    // ABASTECIMIENTO: producto y GL Ruta son opcionales (placa y conductor siguen siendo requeridos)
    var camposTodosOpcionales = [SGV.ddlPlaca, SGV.ddlCarreta, SGV.ddlConductor, SGV.txtProducto, SGV.txtGLRuta];
    var camposProductoOpcional = [SGV.txtProducto, SGV.txtGLRuta];

    // Primero resetear todos
    camposTodosOpcionales.forEach(function (id) {
        var el = document.getElementById(id);
        if (el) { el.style.borderColor = ''; el.style.backgroundColor = ''; }
    });

    // Luego marcar los opcionales según el motivo
    var camposAMarcar = esFlexible ? camposTodosOpcionales : camposProductoOpcional;
    camposAMarcar.forEach(function (id) {
        var el = document.getElementById(id);
        if (el) {
            el.style.borderColor = '#ffc107';
            el.style.backgroundColor = '#fffdf5';
        }
    });
}

function limpiarFormulario() {
    if (!esModoViaje) {
        try {
            if ($.fn.select2) {
                $('#' + SGV.ddlPlaca).val(null).trigger('change');
                $('#' + SGV.ddlCarreta).val(null).trigger('change');
                $('#' + SGV.ddlConductor).val(null).trigger('change');
            }
        } catch (e) { }
        var txtRuta = document.getElementById(SGV.txtRutaManual);
        if (txtRuta) txtRuta.value = '';
        var ddlMotivo = document.getElementById(SGV.ddlMotivoSalida);
        if (ddlMotivo) { ddlMotivo.value = 'ABASTECIMIENTO'; onMotivoChange(); }
        var txtProd = document.getElementById(SGV.txtProducto);
        if (txtProd) txtProd.value = '';
    }
    var txtProdViaje = document.getElementById(SGV.txtProductoViaje);
    if (txtProdViaje) txtProdViaje.value = '';
    document.getElementById('ticketsTableBody').innerHTML = '';
    contadorTickets = 0;
    agregarTicket();
    var campos = [SGV.txtGLRuta, SGV.txtGLComprados, SGV.txtTotalGL,
        SGV.txtGLFinal, SGV.txtGLConsumidos, SGV.txtPrecioDolar,
        SGV.txtMontoTotal, SGV.txtDistancia, SGV.txtConsumoComputador,
        SGV.txtObservaciones];
    campos.forEach(function (id) { var el = document.getElementById(id); if (el) el.value = ''; });
    document.getElementById('rendimientoPromedio').textContent = '0.00';
    document.getElementById('fuelLevelVisual').style.width = '0%';
    document.getElementById('alertMessage').style.display = 'none';
}

function mostrarMensaje(mensaje, tipo) {
    var alertDiv = document.getElementById('alertMessage');
    alertDiv.classList.remove('alert-success', 'alert-danger');
    alertDiv.classList.add(tipo === 'success' ? 'alert-success' : 'alert-danger');
    alertDiv.innerHTML = mensaje;
    alertDiv.style.display = 'block';
    setTimeout(function () { alertDiv.style.display = 'none'; }, 5000);
}

$(document).ready(function () {
    // Aplicar readonly via JS para que ASP.NET no ignore los valores en postback
    $('#' + SGV.txtGLComprados).prop('readonly', true);
    $('#' + SGV.txtTotalGL).prop('readonly', true);
    $('#' + SGV.txtGLConsumidos).prop('readonly', true);
    $('#' + SGV.txtMontoTotal).prop('readonly', true);

    if (!esModoViaje) {
        try {
            var select2Config = { allowClear: true, width: '100%', closeOnSelect: true, language: { noResults: function () { return "Sin resultados"; }, searching: function () { return "Buscando..."; } } };
            $('#' + SGV.ddlPlaca).select2($.extend({}, select2Config, { placeholder: "Buscar placa..." }));
            $('#' + SGV.ddlCarreta).select2($.extend({}, select2Config, { placeholder: "Buscar carreta..." }));
            $('#' + SGV.ddlConductor).select2($.extend({}, select2Config, { placeholder: "Buscar conductor..." }));
        } catch (e) { console.error("Error Select2: ", e); }
    }

    $('#' + SGV.btnGuardar).on('click', function () { guardarTicketsData(); });
});

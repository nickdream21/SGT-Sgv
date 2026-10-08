var contador = 0;

function agregarTicket() {
    contador++;
    var cont = document.getElementById('ticketsContainer');
    var row = document.createElement('div');
    row.className = 'ticket-row';
    row.id = 'row_' + contador;
    row.innerHTML =
        '<input type="text" class="form-control ticket-numero" maxlength="50" placeholder="Nº ticket" />' +
        '<input type="text" class="form-control ticket-proveedor" maxlength="150" placeholder="Proveedor / estación" />' +
        '<input type="number" step="0.01" min="0" class="form-control ticket-gal" placeholder="0.00" oninput="recalc()" />' +
        '<input type="number" step="0.01" min="0" class="form-control ticket-usd" placeholder="0.00" oninput="recalc()" />' +
        '<button type="button" class="btn-remove" onclick="quitar(\'row_' + contador + '\')"><i class="fas fa-trash"></i></button>';
    cont.appendChild(row);
}

function quitar(id) {
    var r = document.getElementById(id);
    if (r) r.parentNode.removeChild(r);
    recalc();
}

function recalc() {
    var gal = 0, usd = 0;
    document.querySelectorAll('.ticket-gal').forEach(function (i) { gal += parseFloat(i.value) || 0; });
    document.querySelectorAll('.ticket-usd').forEach(function (i) { usd += parseFloat(i.value) || 0; });
    document.getElementById('totalGal').innerText = gal.toFixed(2);
    document.getElementById('totalUsd').innerText = usd.toFixed(2);
}

function prepararEnvio() {
    var filas = document.querySelectorAll('#ticketsContainer .ticket-row');
    if (filas.length === 0) {
        alert('Agregue al menos un ticket.');
        return false;
    }
    var lista = [];
    for (var i = 0; i < filas.length; i++) {
        var f = filas[i];
        var num = f.querySelector('.ticket-numero').value.trim();
        var prov = f.querySelector('.ticket-proveedor').value.trim();
        var gal = parseFloat(f.querySelector('.ticket-gal').value) || 0;
        var usd = parseFloat(f.querySelector('.ticket-usd').value) || 0;
        if (gal <= 0) {
            alert('Cada ticket debe tener galones > 0.');
            return false;
        }
        lista.push({ numeroTicket: num, proveedor: prov, galones: gal, precioUSD: usd });
    }
    document.getElementById(SGV.hfTicketsJson).value = JSON.stringify(lista);
    return true;
}

// Primer ticket por defecto al cargar
(function () { agregarTicket(); })();

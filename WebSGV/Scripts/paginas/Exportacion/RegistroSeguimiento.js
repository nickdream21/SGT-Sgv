// ============================================================
//  Tabs
// ============================================================
document.querySelectorAll('.se-tab').forEach(function (tab) {
    tab.addEventListener('click', function () {
        var target = this.getAttribute('data-target');
        document.querySelectorAll('.se-tab').forEach(function (t) { t.classList.remove('active'); });
        document.querySelectorAll('.se-tab-panel').forEach(function (p) { p.classList.remove('active'); });
        this.classList.add('active');
        var panel = document.getElementById(target);
        if (panel) panel.classList.add('active');
    });
});

// ============================================================
//  Autocompletado de despachos (Nacional / Internacional)
// ============================================================
var seAcTimer = null;
function seAutocompletarDespacho(input, esInternacional) {
    clearTimeout(seAcTimer);
    var texto = input.value;
    var list = document.getElementById(esInternacional ? 'acDespachoInternacional' : 'acDespachoNacional');
    if (texto.length < 2) { list.classList.remove('show'); list.innerHTML = ''; return; }

    seAcTimer = setTimeout(function () {
        var idSeguimientoActual = parseInt(document.getElementById(SGV.hdnIdSeguimiento).value, 10) || 0;
        fetch('RegistroSeguimiento.aspx/BuscarDespachosAjax', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json; charset=utf-8' },
            body: JSON.stringify({ esInternacional: esInternacional, texto: texto, idSeguimientoActual: idSeguimientoActual })
        })
        .then(function (r) { return r.json(); })
        .then(function (data) {
            var items = data.d;
            list.innerHTML = '';
            if (!items.length) {
                var vacio = document.createElement('div');
                vacio.className = 'se-ac-item se-ac-empty';
                vacio.textContent = 'Sin despachos disponibles con ese texto';
                list.appendChild(vacio);
            } else {
                items.forEach(function (it) {
                    var div = document.createElement('div');
                    div.className = 'se-ac-item';
                    div.textContent = it.Etiqueta;
                    div.addEventListener('click', function () { seSeleccionarDespacho(it.IdDespacho, esInternacional, it.Etiqueta); });
                    list.appendChild(div);
                });
            }
            list.classList.add('show');
        })
        .catch(function (err) { console.error('Error buscando despachos:', err); });
    }, 300);
}

function seSeleccionarDespacho(idDespacho, esInternacional, etiqueta) {
    document.getElementById(esInternacional ? 'acDespachoInternacional' : 'acDespachoNacional').classList.remove('show');
    if (esInternacional) {
        document.getElementById(SGV.txtBuscarDespachoInternacional).value = etiqueta;
        document.getElementById(SGV.hdnIdDespachoDestino).value = idDespacho;
        __doPostBack(SGV.lnkAplicarDespachoInternacionalUniqueID, '');
    } else {
        document.getElementById(SGV.txtBuscarDespachoNacional).value = etiqueta;
        document.getElementById(SGV.hdnIdDespachoOrigen).value = idDespacho;
        __doPostBack(SGV.lnkAplicarDespachoNacionalUniqueID, '');
    }
}

document.addEventListener('click', function (e) {
    if (!e.target.closest('.se-field')) {
        document.querySelectorAll('.se-ac-list').forEach(function (l) { l.classList.remove('show'); });
    }
});

// ============================================================
//  File upload preview
// ============================================================
function seUpdateFileName(input) {
    var info = document.getElementById('fileInfo');
    if (input.files && input.files[0]) {
        info.innerHTML = 'Archivo: <strong>' + input.files[0].name + '</strong> (' + (input.files[0].size / 1024 / 1024).toFixed(2) + ' MB)';
    } else {
        info.innerHTML = 'Ningún archivo seleccionado';
    }
}
var dropZone = document.getElementById('dropZone');
if (dropZone) {
    ['dragover','dragenter'].forEach(function (ev) {
        dropZone.addEventListener(ev, function (e) { e.preventDefault(); e.stopPropagation(); this.classList.add('drag-over'); });
    });
    ['dragleave','drop'].forEach(function (ev) {
        dropZone.addEventListener(ev, function (e) { e.preventDefault(); e.stopPropagation(); this.classList.remove('drag-over'); });
    });
    dropZone.addEventListener('drop', function (e) {
        var fu = document.getElementById(SGV.fileExcel);
        if (fu && e.dataTransfer.files.length) { fu.files = e.dataTransfer.files; seUpdateFileName(fu); }
    });
}

// Restaurar tab activo si viene en hash
(function restoreTab() {
    var hash = window.location.hash;
    if (hash && hash.indexOf('#tab=') === 0) {
        var t = hash.replace('#tab=', '');
        var btn = document.querySelector('.se-tab[data-target="' + t + '"]');
        if (btn) btn.click();
    }
})();

// ============================================================
//  Datalist: poblar desde window.SE_AC (emitido por code-behind)
// ============================================================
function sePopulateDatalist(id, items) {
    var dl = document.getElementById(id);
    if (!dl || !items) return;
    var html = '';
    for (var i = 0; i < items.length; i++) {
        var v = items[i].replace(/&/g,'&amp;').replace(/"/g,'&quot;').replace(/</g,'&lt;');
        html += '<option value="' + v + '">';
    }
    dl.innerHTML = html;
}

document.addEventListener('DOMContentLoaded', function () {
    if (!window.SE_AC) return;
    sePopulateDatalist('seListClientes',    window.SE_AC.clientes);
    sePopulateDatalist('seListConductores', window.SE_AC.conductores);
    sePopulateDatalist('seListTractos',     window.SE_AC.tractos);
    sePopulateDatalist('seListCarretas',    window.SE_AC.carretas);
});

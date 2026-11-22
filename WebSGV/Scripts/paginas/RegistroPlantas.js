document.addEventListener('click', function (e) {
    var btn = e.target.closest('.btn-editar');
    if (!btn) return;
    // Al abrir otra edición, el error anterior (B5-7) ya no corresponde
    var errorModal = document.getElementById(SGV.pnlErrorModal);
    if (errorModal) errorModal.style.display = 'none';
    document.getElementById(SGV.hfIdPlanta).value = btn.dataset.id;
    document.getElementById(SGV.txtEditarNombre).value = btn.dataset.nombre;
    var ddl = document.getElementById(SGV.ddlEditarAmbito);
    ddl.value = (btn.dataset.internacional === 'True' || btn.dataset.internacional === 'true') ? '1' : '0';
    $('#modalEditar').modal('show');
});

document.addEventListener('DOMContentLoaded', function () {
    var msgPanel = document.getElementById(SGV.pnlMensaje);
    // Edición rechazada (B5-7): el error está dentro del modal, que se reabre con lo escrito
    if (document.getElementById(SGV.pnlErrorModal))
        $('#modalEditar').modal('show');
    if (msgPanel && msgPanel.querySelector('.alert-success')) {
        var ok = msgPanel.querySelector('.alert-success');
        setTimeout(function () {
            ok.style.transition = 'opacity .5s'; ok.style.opacity = '0';
            setTimeout(function () { msgPanel.style.display = 'none'; }, 500);
        }, 4000);
    }
});

function filtrarTabla(valor, id) {
    var filas = document.querySelectorAll('#' + id + ' table tr');
    valor = valor.toLowerCase();
    filas.forEach(function (f, i) { if (i > 0) f.style.display = f.textContent.toLowerCase().includes(valor) ? '' : 'none'; });
}

// Acciones del marcado (data-accion / data-accion-input / data-accion-change): las despacha
// SiteMaster.js. Devolver false cancela el clic (y el postback del botón).
window.SGV = window.SGV || {};
SGV.acciones = Object.assign(SGV.acciones || {}, {
    filtrarTabla: function (el) { filtrarTabla(el.value, el.getAttribute('data-contenedor')); }
});

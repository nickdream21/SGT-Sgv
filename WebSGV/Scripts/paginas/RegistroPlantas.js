document.addEventListener('click', function (e) {
    var btn = e.target.closest('.btn-editar');
    if (!btn) return;
    document.getElementById(SGV.hfIdPlanta).value = btn.dataset.id;
    document.getElementById(SGV.txtEditarNombre).value = btn.dataset.nombre;
    var ddl = document.getElementById(SGV.ddlEditarAmbito);
    ddl.value = (btn.dataset.internacional === 'True' || btn.dataset.internacional === 'true') ? '1' : '0';
    $('#modalEditar').modal('show');
});

document.addEventListener('DOMContentLoaded', function () {
    var hf = document.getElementById(SGV.hfIdPlanta);
    var msgPanel = document.getElementById(SGV.pnlMensaje);
    if (hf && hf.value > 0 && msgPanel && msgPanel.querySelector('.alert-danger'))
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

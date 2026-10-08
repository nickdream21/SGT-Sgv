document.addEventListener('click', function (e) {
    var btn = e.target.closest('.btn-editar');
    if (!btn) return;
    document.getElementById(SGV.hfIdItem).value = btn.dataset.id;
    document.getElementById(SGV.txtEditarNombre).value = btn.dataset.nombre || '';
    document.getElementById(SGV.txtEditarDetalle).value = btn.dataset.detalle || '';
    $('#modalEditar').modal('show');
});

document.addEventListener('DOMContentLoaded', function () {
    var hf = document.getElementById(SGV.hfIdItem);
    var msgPanel = document.getElementById(SGV.pnlMensaje);
    if (hf && hf.value > 0 && msgPanel && msgPanel.querySelector('.alert-danger'))
        $('#modalEditar').modal('show');
});

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
    // Edición rechazada (B5-7): el error está dentro del modal, que se reabre con lo escrito
    if (document.getElementById(SGV.pnlErrorModal))
        $('#modalEditar').modal('show');
});

// La búsqueda va al servidor (E5-16: txtBuscar + Buscar, grilla paginada); Activar/Desactivar usa la acción
// compartida enviarFila de SiteMaster.js.

document.addEventListener('click', function (e) {
    var btn = e.target.closest('.btn-editar');
    if (!btn) return;
    // Al abrir otra edición, el error anterior (B5-7) ya no corresponde
    var errorModal = document.getElementById(SGV.pnlErrorModal);
    if (errorModal) errorModal.style.display = 'none';
    document.getElementById(SGV.hfIdTracto).value = btn.dataset.id;
    document.getElementById(SGV.txtEditarPlaca).value = btn.dataset.placa;
    document.getElementById(SGV.txtEditarMarca).value = btn.dataset.marca;
    document.getElementById(SGV.txtEditarModelo).value = btn.dataset.modelo;
    aplicarPlacaConHistorial(btn);
    $('#modalEditar').modal('show');
});

// B5-4: con despachos u otro historial la placa no se puede cambiar (el servidor lo valida igual);
// se muestra de solo lectura con una nota.
function aplicarPlacaConHistorial(btn) {
    var conHistorial = !!btn && btn.dataset.conHistorial === '1';
    document.getElementById(SGV.txtEditarPlaca).readOnly = conHistorial;
    var nota = document.getElementById('notaPlacaHistorial');
    if (nota) nota.classList.toggle('d-none', !conHistorial);
}

document.addEventListener('DOMContentLoaded', function () {
    // Edición rechazada (B5-7): el error está dentro del modal, que se reabre con lo escrito
    if (document.getElementById(SGV.pnlErrorModal)) {
        var id = document.getElementById(SGV.hfIdTracto).value;
        var btn = Array.prototype.find.call(document.querySelectorAll('.btn-editar'),
            function (b) { return b.dataset.id === id; });
        // Si la fila no está en la grilla (otra página u otro filtro), la placa queda como la dejó el servidor.
        if (btn) aplicarPlacaConHistorial(btn);
        $('#modalEditar').modal('show');
    }
});

// La búsqueda va al servidor (E5-16: txtBuscar + Buscar, grilla paginada); Activar/Desactivar usa la acción
// compartida enviarFila de SiteMaster.js.

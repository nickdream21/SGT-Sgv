document.addEventListener('click', function (e) {
    var btn = e.target.closest('.btn-editar');
    if (!btn) return;
    // Al abrir otra edición, el error anterior (B5-7) ya no corresponde
    var errorModal = document.getElementById(SGV.pnlErrorModal);
    if (errorModal) errorModal.style.display = 'none';
    document.getElementById(SGV.hfIdCliente).value = btn.dataset.id;
    document.getElementById(SGV.txtEditarRUC).value = btn.dataset.ruc;
    document.getElementById(SGV.txtEditarNombre).value = btn.dataset.nombre;
    document.getElementById(SGV.txtEditarDireccion).value = btn.dataset.direccion || '';
    document.getElementById(SGV.txtEditarContacto).value = btn.dataset.contacto || '';
    document.getElementById(SGV.txtEditarTelefono).value = btn.dataset.telefono || '';
    document.getElementById(SGV.txtEditarCorreo).value = btn.dataset.correo || '';
    document.getElementById(SGV.txtEditarObservaciones).value = btn.dataset.observaciones || '';
    document.getElementById(SGV.ddlEditarMoneda).value = btn.dataset.moneda || 'PEN';
    document.getElementById(SGV.chkEditarEsExportador).checked = btn.dataset.exportador === '1';
    $('#modalEditar').modal('show');
});

document.addEventListener('DOMContentLoaded', function () {
    // Edición rechazada (B5-7): el error está dentro del modal, que se reabre con lo escrito
    if (document.getElementById(SGV.pnlErrorModal))
        $('#modalEditar').modal('show');
});

// B5-6: la consulta la hace el servidor (BuscarPorRuc); el token de la API ya no está en este archivo.
var consultandoRuc = false;
function verificarRUC() {
    var ruc = document.getElementById(SGV.txtRUC).value.trim();
    if (!ruc) { SGV.avisar('Debe ingresar un RUC para verificar.'); return; }
    if (!/^\d{11}$/.test(ruc)) { SGV.avisar('El RUC debe tener 11 dígitos.'); return; }
    if (consultandoRuc) return;
    consultandoRuc = true;
    var boton = document.getElementById(SGV.btnBuscarRUC);
    if (boton) boton.disabled = true;
    $.ajax({
        type: 'POST',
        url: 'RegistroClientes.aspx/BuscarPorRuc',
        data: JSON.stringify({ numero: ruc }),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json'
    }).done(function (resp) {
        var r = resp && resp.d;
        if (r && r.exito) {
            document.getElementById(SGV.txtNombre).value = r.razonSocial || '';
            // La dirección fiscal solo se propone si el campo está vacío: no pisa lo que ya escribió el usuario.
            var direccion = document.getElementById(SGV.txtDireccion);
            if (direccion && !direccion.value.trim() && r.direccion)
                direccion.value = r.direccion.substring(0, direccion.maxLength > 0 ? direccion.maxLength : r.direccion.length);
            SGV.avisar('Datos del RUC cargados; revíselos antes de registrar.', { tipo: 'success' });
        } else {
            SGV.avisar((r && r.mensaje) || 'No se pudo consultar el RUC; complete los datos a mano.');
        }
    }).fail(function () {
        SGV.avisar('No se pudo consultar el RUC; complete los datos a mano.');
    }).always(function () {
        consultandoRuc = false;
        if (boton) boton.disabled = false;
    });
}

// Acciones del marcado (data-accion / data-accion-input / data-accion-change): las despacha
// SiteMaster.js. Devolver false cancela el clic (y el postback del botón).
window.SGV = window.SGV || {};
SGV.acciones = Object.assign(SGV.acciones || {}, {
    verificarRUC: function () { verificarRUC(); return false; }
});

// La búsqueda va al servidor (E5-16: txtBuscar + Buscar, grilla paginada); Activar/Desactivar usa la acción
// compartida enviarFila de SiteMaster.js.

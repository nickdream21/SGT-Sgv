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
            SGV.avisar('✅ Datos del RUC cargados; revíselos antes de registrar.');
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

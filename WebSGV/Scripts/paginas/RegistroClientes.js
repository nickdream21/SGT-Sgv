document.addEventListener('click', function (e) {
    var btn = e.target.closest('.btn-editar');
    if (!btn) return;
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
    var hf = document.getElementById(SGV.hfIdCliente);
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

async function verificarRUC() {
    const ruc = document.getElementById(SGV.txtRUC).value.trim();
    const token = 'eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJlbWFpbCI6Im1vcmFucGFsYWNpb3NhbGVtYmVydEBnbWFpbC5jb20ifQ.-nOvFy3s-JXWGF6IoEeJU1NtSrGXhM6sL3msay8eKRI';
    if (!ruc) { alert('Debe ingresar un RUC para verificar.'); return; }
    if (ruc.length !== 11) { alert('El RUC debe tener 11 dígitos.'); return; }
    try {
        const resp = await fetch('https://dniruc.apisperu.com/api/v1/ruc/' + ruc + '?token=' + token);
        if (!resp.ok) throw new Error();
        const data = await resp.json();
        if (data.razonSocial)
            document.getElementById(SGV.txtNombre).value = data.razonSocial;
        else
            alert('No se encontró información para el RUC ingresado. Ingrese el nombre manualmente.');
    } catch { alert('Error al consultar el RUC.'); }
}

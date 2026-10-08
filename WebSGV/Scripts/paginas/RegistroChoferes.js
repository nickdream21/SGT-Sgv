document.addEventListener('click', function (e) {
    var btn = e.target.closest('.btn-editar');
    if (!btn) return;
    document.getElementById(SGV.hfIdConductor).value = btn.dataset.id;
    document.getElementById(SGV.txtEditarNombres).value = btn.dataset.nombres;
    document.getElementById(SGV.txtEditarApellidoPaterno).value = btn.dataset.apPaterno;
    document.getElementById(SGV.txtEditarApellidoMaterno).value = btn.dataset.apMaterno;
    document.getElementById(SGV.txtEditarTelefono).value = btn.dataset.telefono;
    $('#modalEditar').modal('show');
});

document.addEventListener('DOMContentLoaded', function () {
    var hf = document.getElementById(SGV.hfIdConductor);
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

async function buscarPorDNI() {
    const dni = document.getElementById(SGV.txtDNI).value.trim();
    const token = 'eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJlbWFpbCI6Im1vcmFucGFsYWNpb3NhbGVtYmVydEBnbWFpbC5jb20ifQ.-nOvFy3s-JXWGF6IoEeJU1NtSrGXhM6sL3msay8eKRI';
    if (dni.length !== 8) { alert('El DNI debe tener 8 dígitos.'); return; }
    try {
        const resp = await fetch('https://dniruc.apisperu.com/api/v1/dni/' + dni + '?token=' + token);
        if (!resp.ok) throw new Error();
        const data = await resp.json();
        if (data.nombres) {
            document.getElementById(SGV.txtNombres).value = data.nombres;
            document.getElementById(SGV.txtApellidoPaterno).value = data.apellidoPaterno;
            document.getElementById(SGV.txtApellidoMaterno).value = data.apellidoMaterno;
        } else {
            alert('No se encontró información para el DNI ingresado. Ingréselo manualmente.');
        }
    } catch { alert('Error al consultar el DNI.'); }
}

function mostrarCampoDocumento() {
    const tipo = document.getElementById(SGV.ddlTipoDocumento).value;
    document.getElementById('grupoDNI').style.display = 'none';
    document.getElementById('grupoCarnet').style.display = 'none';
    document.getElementById('grupoPasaporte').style.display = 'none';
    if (tipo === 'DNI') document.getElementById('grupoDNI').style.display = 'block';
    else if (tipo === 'Carnet de Extranjería') document.getElementById('grupoCarnet').style.display = 'block';
    else if (tipo === 'Pasaporte') document.getElementById('grupoPasaporte').style.display = 'block';
}

document.addEventListener('DOMContentLoaded', function () { mostrarCampoDocumento(); });

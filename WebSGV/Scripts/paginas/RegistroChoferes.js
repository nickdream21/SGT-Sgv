document.addEventListener('click', function (e) {
    var btn = e.target.closest('.btn-editar');
    if (!btn) return;
    // Al abrir otra edición, el error anterior (B5-7) ya no corresponde
    var errorModal = document.getElementById(SGV.pnlErrorModal);
    if (errorModal) errorModal.style.display = 'none';
    document.getElementById(SGV.hfIdConductor).value = btn.dataset.id;
    document.getElementById(SGV.txtEditarNombres).value = btn.dataset.nombres;
    document.getElementById(SGV.txtEditarApellidoPaterno).value = btn.dataset.apPaterno;
    document.getElementById(SGV.txtEditarApellidoMaterno).value = btn.dataset.apMaterno;
    document.getElementById(SGV.txtEditarTelefono).value = btn.dataset.telefono;
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

// B5-6: la consulta la hace el servidor (BuscarPorDNI); el token de la API ya no está en este archivo.
var consultandoDni = false;
function buscarPorDNI() {
    var dni = document.getElementById(SGV.txtDNI).value.trim();
    if (!/^\d{8}$/.test(dni)) { SGV.avisar('El DNI debe tener 8 dígitos.'); return; }
    if (consultandoDni) return;
    consultandoDni = true;
    var boton = document.getElementById(SGV.btnBuscarDNI);
    if (boton) boton.disabled = true;
    $.ajax({
        type: 'POST',
        url: 'RegistroChoferes.aspx/BuscarPorDNI',
        data: JSON.stringify({ numero: dni }),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json'
    }).done(function (resp) {
        var r = resp && resp.d;
        if (r && r.exito) {
            document.getElementById(SGV.txtNombres).value = r.nombres || '';
            document.getElementById(SGV.txtApellidoPaterno).value = r.apellidoPaterno || '';
            document.getElementById(SGV.txtApellidoMaterno).value = r.apellidoMaterno || '';
            SGV.avisar('✅ Datos del DNI cargados; revíselos antes de registrar.');
        } else {
            SGV.avisar((r && r.mensaje) || 'No se pudo consultar el DNI; complete los datos a mano.');
        }
    }).fail(function () {
        SGV.avisar('No se pudo consultar el DNI; complete los datos a mano.');
    }).always(function () {
        consultandoDni = false;
        if (boton) boton.disabled = false;
    });
}

function mostrarCampoDocumento() {
    const tipo = document.getElementById(SGV.ddlTipoDocumento).value;
    document.getElementById('grupoDNI').style.display = 'none';
    document.getElementById('grupoCarnet').style.display = 'none';
    if (tipo === 'DNI') document.getElementById('grupoDNI').style.display = 'block';
    else if (tipo === 'Carnet de Extranjería') document.getElementById('grupoCarnet').style.display = 'block';
}

document.addEventListener('DOMContentLoaded', function () { mostrarCampoDocumento(); });

// Acciones del marcado (data-accion / data-accion-input / data-accion-change): las despacha
// SiteMaster.js. Devolver false cancela el clic (y el postback del botón).
window.SGV = window.SGV || {};
SGV.acciones = Object.assign(SGV.acciones || {}, {
    filtrarTabla: function (el) { filtrarTabla(el.value, el.getAttribute('data-contenedor')); },
    mostrarCampoDocumento: function () { mostrarCampoDocumento(); },
    buscarPorDNI: function () { buscarPorDNI(); return false; }
});

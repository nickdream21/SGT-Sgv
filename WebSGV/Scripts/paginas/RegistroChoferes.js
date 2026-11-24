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
    // Edición rechazada (B5-7): el error está dentro del modal, que se reabre con lo escrito
    if (document.getElementById(SGV.pnlErrorModal))
        $('#modalEditar').modal('show');
});

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

// Muestra el campo del tipo elegido con la clase d-none de Bootstrap (lleva !important: style.display no
// podría mostrar un grupo que la tiene en el marcado, como grupoCarnet).
// El validador del campo oculto se apaga (ValidatorEnable de WebUIValidation.js): si no, un carnet mal
// escrito y luego cambiado a DNI bloquearía Registrar con un error que no se ve. El servidor hace lo mismo
// (AjustarValidadoresDocumento).
function mostrarCampoDocumento() {
    const tipo = document.getElementById(SGV.ddlTipoDocumento).value;
    const esDni = tipo === 'DNI', esCarnet = tipo === 'Carnet de Extranjería';
    document.getElementById('grupoDNI').classList.toggle('d-none', !esDni);
    document.getElementById('grupoCarnet').classList.toggle('d-none', !esCarnet);
    activarValidador(SGV.revDni, esDni);
    activarValidador(SGV.revCarnet, esCarnet);
}

function activarValidador(id, activo) {
    var val = id && document.getElementById(id);
    // Al cargar, la validación discreta aún no armó Page_Validators: el servidor ya dejó cada validador
    // encendido o apagado según el tipo elegido (AjustarValidadoresDocumento).
    if (!val || typeof ValidatorEnable !== 'function' || typeof Page_Validators === 'undefined') return;
    // Sin ValidatorValidate al encender: el campo recién mostrado se valida al escribir o al registrar.
    if (activo) val.enabled = true;
    else ValidatorEnable(val, false);   // apaga y borra el mensaje que hubiera
}

document.addEventListener('DOMContentLoaded', function () { mostrarCampoDocumento(); });

// Acciones del marcado (data-accion / data-accion-input / data-accion-change): las despacha
// SiteMaster.js. Devolver false cancela el clic (y el postback del botón).
window.SGV = window.SGV || {};
SGV.acciones = Object.assign(SGV.acciones || {}, {
    mostrarCampoDocumento: function () { mostrarCampoDocumento(); },
    buscarPorDNI: function () { buscarPorDNI(); return false; }
});

// La búsqueda va al servidor (E5-16: txtBuscar + Buscar, grilla paginada); Activar/Desactivar usa la acción
// compartida enviarFila de SiteMaster.js.

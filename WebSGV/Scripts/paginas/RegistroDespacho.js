$(document).ready(function () {
    initializeSelect2();
    setDefaultDate();
    autoHideMessages();
    actualizarGuardiaLote();
    actualizarStepper();
    inicializarValidacionNumeroPedido();
});

function initializeSelect2() {
    $('.select2-searchable').each(function () {
        var $sel = $(this);
        // Destruir la instancia previa antes de re-inicializar: tras un postback
        // parcial quedaban contenedores huérfanos y el desplegable se comportaba
        // como una lista larga sin buscador.
        if ($sel.hasClass('select2-hidden-accessible')) {
            $sel.select2('destroy');
        }
        $sel.select2({
            theme: 'bootstrap-5',
            placeholder: $sel.find('option:first-child').text(),
            allowClear: false,
            width: '100%',
            minimumResultsForSearch: 0   // el buscador siempre visible
        });
    });
}

function setDefaultDate() {
    var today = new Date();
    var dd = String(today.getDate()).padStart(2, '0');
    var mm = String(today.getMonth() + 1).padStart(2, '0');
    var yyyy = today.getFullYear();
    var todayString = yyyy + '-' + mm + '-' + dd;

    var fechaInputs = [
        SGV.txtFechaDespachoBase,
        SGV.txtFechaEmisionFacturaBase,
        SGV.txtFechaEmisionCPICBase
    ];

    fechaInputs.forEach(function (inputId) {
        var input = document.getElementById(inputId);
        if (input && input.value === '') {
            input.value = todayString;
        }
    });
}

// Función para mostrar el modal de historial
function showHistorialModal() {
    var modal = new bootstrap.Modal(document.getElementById('modalHistorialViajes'));
    modal.show();
}

// Re-inicializar después de un postback parcial del UpdatePanel
var prm = Sys.WebForms.PageRequestManager.getInstance();
prm.add_endRequest(function () {
    initializeSelect2();
    setDefaultDate();
    autoHideMessages();
    actualizarGuardiaLote();
    actualizarStepper();
});

// Función para auto-ocultar mensajes después de 5 segundos
function autoHideMessages() {
    setTimeout(function () {
        $('.alert-dismissible').fadeOut('slow');
    }, 5000);
}

// Función para scroll automático a la sección de conductores
function scrollToConductores() {
    setTimeout(function () {
        var conductoresPanel = document.getElementById(SGV.pnlAdicionConductores);
        if (conductoresPanel) {
            conductoresPanel.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }, 100);
}

// ── Guardia de lote activo ──────────────────────────────────────────
// Avisa si el usuario abandona la página con un lote a medio armar.
//
// `_postbackEnCurso` evita el falso positivo: un postback del propio formulario
// también descarga la página y disparaba el "¿Desea salir del sitio?" aunque el
// usuario no se estuviera yendo a ningún lado (pasaba al agregar un conductor,
// que hacía postback completo).
var _postbackEnCurso = false;

document.addEventListener('submit', function () {
    _postbackEnCurso = true;
    // Si el postback no llega a navegar (validación de cliente que falla), se
    // rearma el guard para no dejar la página desprotegida.
    setTimeout(function () { _postbackEnCurso = false; }, 4000);
}, true);

function _guardiaLote(e) {
    if (_postbackEnCurso) return;
    e.preventDefault();
    e.returnValue = '';
    return '';
}

// Lee el HiddenField que el servidor actualiza en cada postback
// y registra/quita el beforeunload según corresponda.
function actualizarGuardiaLote() {
    var hf = document.getElementById(SGV.hfLoteActivo);
    if (!hf) return;
    if (hf.value === '1') {
        window.addEventListener('beforeunload', _guardiaLote);
    } else {
        window.removeEventListener('beforeunload', _guardiaLote);
    }
}

// Actualiza el indicador de progreso según el estado del lote
function actualizarStepper() {
    var hf = document.getElementById(SGV.hfLoteActivo);
    var loteActivo = hf && hf.value === '1';
    var step1 = document.getElementById('rdStep1');
    var step2 = document.getElementById('rdStep2');
    if (!step1 || !step2) return;
    step1.className = 'rd-stepper-item' + (loteActivo ? ' done' : ' active');
    step2.className = 'rd-stepper-item' + (loteActivo ? ' active' : '');
}

// Validación en tiempo real del N° de Pedido (solo dígitos, máximo 10)
function inicializarValidacionNumeroPedido() {
    var inputPedido = document.getElementById(SGV.txtNumeroPedidoBase);
    if (!inputPedido) return;
    inputPedido.addEventListener('input', function () {
        this.value = this.value.replace(/[^0-9]/g, '').substring(0, 10);
        var len = this.value.length;
        if (len === 0) {
            this.classList.remove('is-invalid', 'is-valid');
        } else if (len === 10) {
            this.classList.remove('is-invalid');
            this.classList.add('is-valid');
        } else {
            this.classList.remove('is-valid');
            this.classList.add('is-invalid');
        }
    });
}

function validarFechaProgramacionCliente(source, args) {
    if (!args.Value) {
        args.IsValid = true;
        return;
    }

    var fecha = new Date(args.Value + 'T00:00:00');
    if (isNaN(fecha.getTime())) {
        args.IsValid = false;
        return;
    }

    var anio = fecha.getFullYear();
    if (anio < 2000 || anio > 2100) {
        args.IsValid = false;
        return;
    }

    var hoy = new Date();
    hoy.setHours(0, 0, 0, 0);
    var min = new Date(hoy);
    min.setDate(hoy.getDate() - 365);
    var max = new Date(hoy);
    max.setDate(hoy.getDate() + 30);

    args.IsValid = fecha >= min && fecha <= max;
}

function validarFechaEmisionCliente(source, args) {
    if (!args.Value) {
        args.IsValid = true;
        return;
    }

    var fecha = new Date(args.Value + 'T00:00:00');
    if (isNaN(fecha.getTime())) {
        args.IsValid = false;
        return;
    }

    var anio = fecha.getFullYear();
    if (anio < 2000 || anio > 2100) {
        args.IsValid = false;
        return;
    }

    var hoy = new Date();
    hoy.setHours(0, 0, 0, 0);
    args.IsValid = fecha <= hoy;
}

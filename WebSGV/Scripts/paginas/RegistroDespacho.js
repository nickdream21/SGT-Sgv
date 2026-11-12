$(document).ready(function () {
    initializeSelect2();
    setDefaultDate();
    autoHideMessages();
    actualizarGuardiaLote();
    actualizarStepper();
    inicializarValidacionNumeroPedido();
});

// Select2 4.0 con jQuery 3.6 no pone el foco en el buscador al abrir: el usuario tenía
// que hacer clic en la cajita antes de escribir. Se enfoca a mano (delegado: vale
// también para los desplegables que aparecen tras un postback parcial).
$(document).on('select2:open', function () {
    setTimeout(function () {
        var buscador = document.querySelector('.select2-container--open .select2-search__field');
        if (buscador) buscador.focus();
    }, 0);
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

// Formulario manual (Configuración base) plegable: se recuerda en un campo oculto para
// que el servidor lo vuelva a pintar igual tras cada postback.
$(document).on('shown.bs.collapse hidden.bs.collapse', '#rdManualCuerpo', function (e) {
    if (e.target.id !== 'rdManualCuerpo') return;
    $('#' + SGV.hfManualAbierto).val(e.type === 'shown' ? '1' : '0');
    actualizarStepper();
    if (e.type === 'shown') initializeSelect2();
});

// Re-inicializar después de un postback parcial del UpdatePanel
var prm = Sys.WebForms.PageRequestManager.getInstance();
prm.add_endRequest(function () {
    initializeSelect2();
    setDefaultDate();
    autoHideMessages();
    actualizarGuardiaLote();
    actualizarStepper();
});

// Auto-oculta los avisos de éxito o información a los 5 segundos. Los errores y
// advertencias se quedan hasta que el usuario los cierra (si no, se perdían sin leerlos).
function autoHideMessages() {
    setTimeout(function () {
        $('.alert-dismissible.alert-success, .alert-dismissible.alert-info').closest('.mb-3').fadeOut('slow');
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
    // Los dos pasos son del lote a mano: sin ese formulario abierto ni un lote en curso no se muestran.
    var caja = document.getElementById('rdStepper');
    var manual = document.getElementById(SGV.hfManualAbierto);
    if (caja) caja.style.display = (loteActivo || (manual && manual.value === '1')) ? '' : 'none';
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

// Zona para soltar el Word, el ZIP o los PDF: muestra los archivos elegidos. Delegado en el documento
// porque el UpdatePanel vuelve a dibujar la zona en cada postback parcial.
(function () {
    function mostrar(input) {
        var zona = input.closest('.rd-zona');
        var lista = zona && zona.querySelector('.rd-zona-archivos');
        if (!lista) return;
        lista.innerHTML = '';
        Array.prototype.forEach.call(input.files || [], function (f) {
            var s = document.createElement('span');
            s.textContent = f.name;
            lista.appendChild(s);
        });
    }
    document.addEventListener('change', function (e) {
        if (e.target.classList && e.target.classList.contains('rd-zona-input')) mostrar(e.target);
    });
    ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(function (ev) {
        document.addEventListener(ev, function (e) {
            var zona = e.target.closest && e.target.closest('.rd-zona');
            if (!zona) return;
            zona.classList.toggle('rd-zona-activa', ev === 'dragenter' || ev === 'dragover');
            if (ev === 'drop') setTimeout(function () { mostrar(zona.querySelector('.rd-zona-input')); }, 0);
        });
    });
})();

// Vista previa del Word: menos ruido.
//  - "Word: ABC-123" sobre el tracto o la carreta solo se deja cuando difiere de la placa elegida en la lista.
//  - La tabla de descargas y entregas se pliega cuando todas sus filas ya están registradas.
function ordenarVistaPreviaWord() {
    document.querySelectorAll('td.rd-col-placa small.rd-doc-dato').forEach(function (s) {
        var lista = s.parentNode.querySelector('select');
        if (!lista || lista.selectedIndex < 0) return;
        var delWord = s.textContent.replace(/^\s*Word:\s*/, '').trim().toUpperCase();
        var elegido = lista.options[lista.selectedIndex].text.trim().toUpperCase();
        s.style.display = delWord && delWord === elegido ? 'none' : '';
    });
    document.querySelectorAll('details.rd-entregas-detalle').forEach(function (d) {
        var chips = d.querySelectorAll('tbody .rd-chip');
        var hechas = Array.prototype.filter.call(chips, function (c) { return /ya registrad/i.test(c.textContent); }).length;
        var cuenta = d.querySelector('.rd-entregas-cuenta');
        if (cuenta) cuenta.textContent = chips.length ? '· ' + hechas + ' de ' + chips.length + ' ya registradas' : '';
        if (chips.length && hechas === chips.length && !d.dataset.visto) d.open = false;
        d.dataset.visto = '1';
    });
}
$(ordenarVistaPreviaWord);
$(document).on('change', 'td.rd-col-placa select', ordenarVistaPreviaWord);
if (window.Sys && Sys.WebForms) Sys.WebForms.PageRequestManager.getInstance().add_endRequest(ordenarVistaPreviaWord);

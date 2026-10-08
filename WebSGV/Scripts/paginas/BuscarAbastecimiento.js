// Descargar PDF SGV-CDF-F-06 del abastecimiento cargado.
function descargarPdfAbastecimiento() {
    var inp = document.getElementById(SGV.txtNumAbastecimiento);
    var num = inp ? (inp.value || '').trim() : '';
    if (!num) {
        alert('No hay un abastecimiento cargado para generar el PDF.');
        return;
    }
    var url = 'DescargarPdfAbastecimiento.aspx?num=' + encodeURIComponent(num);
    window.open(url, '_blank');
}
// Función para actualizar la visualización del nivel de combustible
function actualizarNivelCombustible(actual, total) {
    if (total > 0) {
        var porcentaje = (actual / total) * 100;
        document.getElementById('fuelLevelVisual').style.width = porcentaje + '%';
    } else {
        document.getElementById('fuelLevelVisual').style.width = '0%';
    }
}

// Función para calcular totales automáticamente (para cuando está en modo edición)
function calcularTotales() {
    var glRuta = parseFloat(document.getElementById(SGV.txtGLRuta).value) || 0;
    var glComprados = parseFloat(document.getElementById(SGV.txtGLComprados).value) || 0;
    var glFinal = parseFloat(document.getElementById(SGV.txtGLFinal).value) || 0;

    // Calcular total abastecido
    var totalAbastecido = glRuta + glComprados;
    document.getElementById(SGV.txtTotalGL).value = totalAbastecido.toFixed(2);

    // Calcular total consumido
    var totalConsumido = totalAbastecido - glFinal;
    document.getElementById(SGV.txtGLConsumidos).value = totalConsumido.toFixed(2);

    // Actualizar visualización del nivel de combustible
    actualizarNivelCombustible(glFinal, totalAbastecido);

    // Calcular rendimiento
    calcularRendimiento();
}

// Función para calcular rendimiento
function calcularRendimiento() {
    var distancia = parseFloat(document.getElementById(SGV.txtDistancia).value) || 0;
    var consumido = parseFloat(document.getElementById(SGV.txtGLConsumidos).value) || 0;

    if (distancia > 0 && consumido > 0) {
        var rendimiento = distancia / consumido;
        document.getElementById(SGV.lblRendimientoPromedio).textContent = rendimiento.toFixed(2);
        return rendimiento.toFixed(2);
    } else {
        document.getElementById(SGV.lblRendimientoPromedio).textContent = "0.00";
        return "0.00";
    }
}

// Función para mostrar indicadores de campos opcionales en modo edición
function aplicarIndicadoresEdicion() {
    var ddlTipo = document.getElementById(SGV.ddlTipoAbastecimientoEdit);
    var hdnTipo = document.getElementById(SGV.hdnTipoAbastecimiento);
    var tipo = ddlTipo ? ddlTipo.value : (hdnTipo ? hdnTipo.value : 'ABASTECIMIENTO');
    var esViajeProgramado = (tipo === 'VIAJE PROGRAMADO');
    var esFlexible = (tipo === 'MANTENIMIENTO' || tipo === 'OTRO');

    // Sincronizar hidden field
    if (hdnTipo) hdnTipo.value = tipo;

    var hintDiv = document.getElementById('divMotivoHint');
    if (hintDiv) {
        hintDiv.style.display = 'block';
        if (esFlexible) {
            hintDiv.innerHTML = '<i class="fas fa-info-circle mr-1"></i>Tipo <strong>' + tipo + '</strong>: Producto, GL Ruta, precio y distancia son opcionales.';
        } else if (esViajeProgramado) {
            hintDiv.innerHTML = '<i class="fas fa-info-circle mr-1"></i><strong>Viaje Programado</strong>: Todos los campos de combustible aplican.';
        } else {
            hintDiv.innerHTML = '<i class="fas fa-info-circle mr-1"></i><strong>Abastecimiento</strong>: Producto y GL Ruta son opcionales.';
        }
    }
}

// Vincular cambio de tipo dropdown a actualización de indicadores
function initTipoDropdownChange() {
    var ddlTipo = document.getElementById(SGV.ddlTipoAbastecimientoEdit);
    if (ddlTipo) {
        ddlTipo.addEventListener('change', function () {
            aplicarIndicadoresEdicion();
        });
    }
}

// Inicializar visualización al cargar la página
window.onload = function () {
    try {
        var txtTotalGLElement = document.getElementById(SGV.txtTotalGL);
        var txtGLFinalElement = document.getElementById(SGV.txtGLFinal);

        if (txtTotalGLElement && txtGLFinalElement) {
            var totalGL = parseFloat(txtTotalGLElement.value) || 0;
            var glFinal = parseFloat(txtGLFinalElement.value) || 0;

            actualizarNivelCombustible(glFinal, totalGL);
        } else {
            console.log("Elementos no encontrados: txtTotalGL o txtGLFinal");
        }

        initTipoDropdownChange();
    } catch (error) {
        console.error("Error en window.onload:", error);
    }
};

// BuscarOrdenViaje.js - Script para la página de búsqueda y edición de órdenes de viaje
$(document).ready(function () {
    // Inicializar Select2 solo si está disponible
    if ($.fn.select2) {
        $('.select2').select2({
            width: '100%',
            placeholder: 'Seleccione una opción',
            allowClear: true
        });
    } else {
        console.error("Select2 no está disponible. Verifica que se haya cargado correctamente.");
    }

    // Manejar cambio en la ruta seleccionada
    $('#ddlRuta').on('change', function () {
        console.log("Cambio en ruta detectado:", $(this).val());
        toggleRutaDetails();
    });

    // Configurar los campos numéricos para validación
    $('input[type="number"]').on('input', function () {
        // Remover clases de error si se ingresan valores correctos
        if (parseFloat($(this).val()) >= 0 || $(this).val() === '') {
            $(this).removeClass('is-invalid');
        } else {
            $(this).addClass('is-invalid');
        }
    });

    // Agregar evento para el botón de habilitar edición
    $('#btnHabilitarEdicion').on('click', function () {
        console.log("Botón Habilitar Edición presionado");
        // Dar tiempo a los controles para habilitarse
        setTimeout(function () {
            toggleRutaDetails();
        }, 200);
    });

    // También ejecutar cuando se guarden cambios o se cancele
    $('#btnGuardarCambios, #btnCancelar').on('click', function () {
        setTimeout(function () {
            toggleRutaDetails();
        }, 200);
    });

    // Llamar a toggleRutaDetails() al cargar la página
    setTimeout(function () {
        toggleRutaDetails();
    }, 100);

    // Función para calcular y actualizar los totales de ingresos y gastos
    function calcularTotales() {
        // Totales para ingresos
        let totalIngresosSoles = 0;
        let totalIngresosDolares = 0;

        // Sumar ingresos fijos
        totalIngresosSoles += parseFloat($('input[name="txtDespachoSoles"]').val() || 0);
        totalIngresosDolares += parseFloat($('input[name="txtDespachoDolares"]').val() || 0);

        totalIngresosSoles += parseFloat($('input[name="txtMensualidadSoles"]').val() || 0);
        totalIngresosDolares += parseFloat($('input[name="txtMensualidadDolares"]').val() || 0);

        totalIngresosSoles += parseFloat($('input[name="txtOtrosSoles"]').val() || 0);
        totalIngresosDolares += parseFloat($('input[name="txtOtrosDolares"]').val() || 0);

        totalIngresosSoles += parseFloat($('input[name="txtPrestamoSoles"]').val() || 0);
        totalIngresosDolares += parseFloat($('input[name="txtPrestamoDolares"]').val() || 0);

        // Sumar ingresos adicionales
        $('#rptIngresosAdicionales input[id$="txtIngresoSoles"]').each(function () {
            totalIngresosSoles += parseFloat($(this).val() || 0);
        });

        $('#rptIngresosAdicionales input[id$="txtIngresoDolares"]').each(function () {
            totalIngresosDolares += parseFloat($(this).val() || 0);
        });

        // Totales para gastos
        let totalGastosSoles = 0;
        let totalGastosDolares = 0;

        // Sumar gastos fijos
        totalGastosSoles += parseFloat($('input[name="txtPeajesSoles"]').val() || 0);
        totalGastosDolares += parseFloat($('input[name="txtPeajesDolares"]').val() || 0);

        totalGastosSoles += parseFloat($('input[name="txtAlimentacionSoles"]').val() || 0);
        totalGastosDolares += parseFloat($('input[name="txtAlimentacionDolares"]').val() || 0);

        totalGastosSoles += parseFloat($('input[name="txtApoyoSeguridadSoles"]').val() || 0);
        totalGastosDolares += parseFloat($('input[name="txtApoyoSeguridadDolares"]').val() || 0);

        totalGastosSoles += parseFloat($('input[name="txtReparacionesSoles"]').val() || 0);
        totalGastosDolares += parseFloat($('input[name="txtReparacionesDolares"]').val() || 0);

        totalGastosSoles += parseFloat($('input[name="txtMovilidadSoles"]').val() || 0);
        totalGastosDolares += parseFloat($('input[name="txtMovilidadDolares"]').val() || 0);

        totalGastosSoles += parseFloat($('input[name="txtEncapadaSoles"]').val() || 0);
        totalGastosDolares += parseFloat($('input[name="txtEncapadaDolares"]').val() || 0);

        totalGastosSoles += parseFloat($('input[name="txtHospedajeSoles"]').val() || 0);
        totalGastosDolares += parseFloat($('input[name="txtHospedajeDolares"]').val() || 0);

        totalGastosSoles += parseFloat($('input[name="txtCombustibleSoles"]').val() || 0);
        totalGastosDolares += parseFloat($('input[name="txtCombustibleDolares"]').val() || 0);

        // Sumar gastos adicionales
        $('#rptGastosAdicionales input[id$="txtGastoSoles"]').each(function () {
            totalGastosSoles += parseFloat($(this).val() || 0);
        });

        $('#rptGastosAdicionales input[id$="txtGastoDolares"]').each(function () {
            totalGastosDolares += parseFloat($(this).val() || 0);
        });

        // Calcular diferencia de saldo
        let diferenciaSaldoSoles = totalIngresosSoles - totalGastosSoles;
        let diferenciaSaldoDolares = totalIngresosDolares - totalGastosDolares;

        // Actualizar los elementos HTML con los totales
        $('#totalIngresosSoles').text(totalIngresosSoles.toFixed(2));
        $('#totalIngresosDolares').text(totalIngresosDolares.toFixed(2));

        $('#totalGastosSoles').text(totalGastosSoles.toFixed(2));
        $('#totalGastosDolares').text(totalGastosDolares.toFixed(2));

        $('#diferenciaSaldoSoles').text(diferenciaSaldoSoles.toFixed(2));
        $('#diferenciaSaldoDolares').text(diferenciaSaldoDolares.toFixed(2));
    }

    // Agregar listener para recalcular totales cuando los valores cambian
    $('input[type="number"]').on('input', function () {
        calcularTotales();
    });

    // Calcular totales al cargar la página
    calcularTotales();
});

// Función para mostrar/ocultar los campos adicionales según la ruta seleccionada
function toggleRutaDetails() {
    var rutaSeleccionada = $('#ddlRuta').val();
    console.log("Ejecutando toggleRutaDetails(), ruta seleccionada:", rutaSeleccionada);

    // Para rutas específicas se muestran los detalles adicionales
    if (rutaSeleccionada === '2') { // Sullana-Guayaquil-Sullana
        console.log("Mostrando detalles de ruta para Sullana-Guayaquil-Sullana");
        $('#rutaDetails').show();
    } else {
        console.log("Ocultando detalles de ruta");
        $('#rutaDetails').hide();
    }
}

// Detectar cuando se cambia de pestaña y asegurar que la funcionalidad de ruta funcione
$(document).on('shown.bs.tab', 'a[data-toggle="tab"]', function (e) {
    console.log("Cambio de pestaña detectado");
    if ($(e.target).attr('href') === "#guias") {
        setTimeout(function () {
            toggleRutaDetails();
        }, 100);
    }
});
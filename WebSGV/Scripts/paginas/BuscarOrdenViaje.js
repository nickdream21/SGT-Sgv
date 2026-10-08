// Script específico para manejar los campos de ruta en esta página
$(document).ready(function () {
    // Verificar ruta inicial y mostrar/ocultar campos
    setTimeout(function () {
        checkRutaAndShowFields();
    }, 200);

    // Manejar cambio en la ruta
    $('#ddlRuta').on('change', function () {
        checkRutaAndShowFields();
    });

    // Manejar clic en botón de habilitar edición
    $('#btnHabilitarEdicion').on('click', function () {
        setTimeout(function () {
            checkRutaAndShowFields();
        }, 300);
    });

    // Manejar cambio de pestaña
    $('a[data-toggle="tab"]').on('shown.bs.tab', function (e) {
        if ($(e.target).attr('href') === "#guias") {
            setTimeout(function () {
                checkRutaAndShowFields();
            }, 300);
        }
    });
});

// Función específica para verificar la ruta y mostrar los campos
function checkRutaAndShowFields() {
    var rutaValue = $('#ddlRuta').val();
    var rutaText = $('#ddlRuta option:selected').text();

    console.log("Ruta seleccionada:", rutaValue, rutaText);

    if (rutaValue === '2' || rutaText.toLowerCase().indexOf('guayaquil') > -1) {
        $('#rutaDetails').show();
        console.log("Mostrando campos de Planta y Manifiesto");
    } else {
        $('#rutaDetails').hide();
        console.log("Ocultando campos de Planta y Manifiesto");
    }
}

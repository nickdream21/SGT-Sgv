document.addEventListener('DOMContentLoaded', function () {
    // Configurar Select2 SOLO para los 4 campos específicos
    $('.searchable-select').select2({
        placeholder: function () {
            return $(this).find('option').first().text();
        },
        allowClear: false,
        width: '100%',
        language: {
            noResults: function () {
                return "No se encontraron resultados";
            },
            searching: function () {
                return "Buscando...";
            },
            inputTooShort: function () {
                return "Escribe para buscar";
            }
        },
        templateResult: function (option) {
            if (!option.id) {
                return option.text;
            }

            // Resaltar texto coincidente
            var term = $('.select2-search__field').val();
            if (term && term.length > 0) {
                var regex = new RegExp('(' + term + ')', 'gi');
                var highlighted = option.text.replace(regex, '<strong>$1</strong>');
                return $('<span>' + highlighted + '</span>');
            }

            return option.text;
        },
        escapeMarkup: function (markup) {
            return markup;
        }
    });
});

// Auto-hide alerts
document.addEventListener('DOMContentLoaded', function () {
    setTimeout(function () {
        const alerts = document.querySelectorAll('.alert');
        alerts.forEach(function (alert) {
            alert.style.transition = 'opacity 0.5s';
            alert.style.opacity = '0';
            setTimeout(function () {
                alert.style.display = 'none';
            }, 500);
        });
    }, 8000);
});

// Confirmación antes de cancelar si hay cambios
document.addEventListener('DOMContentLoaded', function () {
    const form = document.querySelector('form');
    const cancelBtn = document.getElementById(SGV.btnCancelar);
    let formChanged = false;

    // Detectar cambios en el formulario
    const inputs = form.querySelectorAll('input, select, textarea');
    inputs.forEach(function (input) {
        input.addEventListener('change', function () {
            formChanged = true;
        });
    });

    // Detectar cambios en Select2
    $('.searchable-select').on('change', function () {
        formChanged = true;
    });

    // Confirmar cancelación si hay cambios
    if (cancelBtn) {
        cancelBtn.addEventListener('click', function (e) {
            if (formChanged) {
                if (!confirm('¿Está seguro de que desea cancelar? Se perderán los cambios no guardados.')) {
                    e.preventDefault();
                    return false;
                }
            }
        });
    }
});

// Validaciones en el cliente
function validarFormulario() {
    let esValido = true;
    let mensajes = [];

    // Validar fecha
    const fecha = document.getElementById(SGV.txtFechaDespacho).value;
    if (!fecha) {
        esValido = false;
        mensajes.push('La fecha de despacho es obligatoria');
    }

    // Validar conductor
    const conductor = $('#' + SGV.ddlConductor).val();
    if (!conductor) {
        esValido = false;
        mensajes.push('Debe seleccionar un conductor');
    }

    // Validar cliente
    const cliente = $('#' + SGV.ddlCliente).val();
    if (!cliente) {
        esValido = false;
        mensajes.push('Debe seleccionar un cliente');
    }

    // Validar tracto
    const tracto = $('#' + SGV.ddlTracto).val();
    if (!tracto) {
        esValido = false;
        mensajes.push('Debe seleccionar un tracto');
    }

    // Validar carreta
    const carreta = $('#' + SGV.ddlCarreta).val();
    if (!carreta) {
        esValido = false;
        mensajes.push('Debe seleccionar una carreta');
    }

    // Validar lugar
    const lugar = document.getElementById(SGV.ddlLugar).value;
    if (!lugar) {
        esValido = false;
        mensajes.push('Debe seleccionar un lugar de operación');
    }

    // Validar tipo de operación
    const tipoOperacion = document.getElementById(SGV.ddlTipoOperacion).value;
    if (!tipoOperacion) {
        esValido = false;
        mensajes.push('Debe seleccionar un tipo de operación');
    }

    if (!esValido) {
        alert('Por favor, corrija los siguientes errores:\n\n• ' + mensajes.join('\n• '));
    }

    return esValido;
}

// Asociar validación al botón guardar
document.addEventListener('DOMContentLoaded', function () {
    const saveBtn = document.getElementById(SGV.btnGuardar);
    if (saveBtn) {
        saveBtn.addEventListener('click', function (e) {
            if (!validarFormulario()) {
                e.preventDefault();
                return false;
            }
        });
    }
});

// Control de navegación entre pestañas y validación
// Archivo: OrdenViaje.js

$(document).ready(function () {
    // Inicializar Select2
    $('.select2').select2();

    // Deshabilitar navegación directa con clic en las pestañas
    $('.nav-tabs a').on('click', function (e) {
        e.preventDefault();
        var targetTab = $(this).attr('href').substring(1);
        var currentTab = $('.tab-pane.active').attr('id');

        // Solo permitir navegación controlada
        if (canNavigateToTab(currentTab, targetTab)) {
            showTab(targetTab);
        }
    });

    // Prevenir la letra 'e' en campos numéricos
    $(document).on('keydown', 'input[type=number]', function (e) {
        if (e.key === 'e' || e.key === 'E') {
            e.preventDefault();
        }
    });

    // Escuchar cambios en los campos de las tablas para actualizar totales
    $(document).on('input change', '#ingresosBody input, #gastosFijosBody input, #gastosAdicionalesBody input', function () {
        actualizarResumen();
        actualizarGastosAdicionales();
    });

    // Escuchar cambios específicamente en los campos de gastos adicionales
    $(document).on('change input', ".nombreCategoria, .descripcion, .soles, .dolares", function () {
        actualizarGastosAdicionales();

        // Validar que el nombre de categoría no esté vacío si hay valores
        let row = $(this).closest('tr');
        let nombreCategoria = row.find(".nombreCategoria").val().trim();
        let soles = parseFloat(row.find(".soles").val()) || 0;
        let dolares = parseFloat(row.find(".dolares").val()) || 0;

        if ((soles > 0 || dolares > 0) && nombreCategoria === "") {
            row.find(".nombreCategoria").addClass('is-invalid');
        } else {
            row.find(".nombreCategoria").removeClass('is-invalid');
        }
    });

    // Limpiar campos y calcular los totales iniciales
    limpiarCamposLiquidacion();
    actualizarResumen();

    // Inicializar tabla de productos
    $("#tablaProductos tbody").empty();
    agregarFilaProducto();

    // Verificar si hay mensajes de error del servidor
    verificarMensajesServidor();
});

// Función para determinar si se puede navegar a la pestaña destino
function canNavigateToTab(currentTab, targetTab) {
    // Permitir navegación libre al hacer clic en la misma pestaña
    if (currentTab === targetTab) {
        return true;
    }

    // Navegación desde "datos" a "liquidacion"
    if (currentTab === "datos" && targetTab === "liquidacion") {
        return validarDatosViaje();
    }

    // Navegación desde "liquidacion" a "guias"
    if (currentTab === "liquidacion" && targetTab === "guias") {
        return validarLiquidacion();
    }

    // Siempre permitir retroceder a pestañas anteriores
    if ((currentTab === "liquidacion" && targetTab === "datos") ||
        (currentTab === "guias" && (targetTab === "datos" || targetTab === "liquidacion"))) {
        return true;
    }

    return false;
}

// Función para mostrar una pestaña específica
function showTab(tabId) {
    $('.nav-tabs .nav-link').removeClass('active').attr('aria-selected', 'false');
    $('.tab-content .tab-pane').removeClass('show active');

    $('#' + tabId + '-tab').addClass('active').attr('aria-selected', 'true');
    $('#' + tabId).addClass('show active');

    // Si cambiamos a la pestaña de liquidación, actualizar cálculos
    if (tabId === "liquidacion") {
        actualizarResumen();
    }
}

// Función para verificar mensajes de error del servidor
function verificarMensajesServidor() {
    // Mostrar mensaje de error del servidor en un alert
    var errorMessage = $('[id$=lblErrores]').text();
    if (errorMessage !== "") {
        alert(errorMessage.replace(/<br\/>/g, "\n"));
    }

    // Verificar si hay un mensaje de éxito desde el servidor para cambiar de pestaña
    if ($('[id$=lblErrores]').text() === "") {
        var validationSuccess = $('[id$=hfValidationError]').val();
        if (validationSuccess === "true") {
            showTab('liquidacion');
        }
    }
}

// Validar datos de la pestaña "Datos del Viaje"
function validarDatosViaje() {
    let isValid = true;
    let errores = [];

    // Validar campos de texto - Usando selectores de atributos para IDs generados por ASP.NET
    if ($('[id$=txtCPI]').val().trim() === "") {
        errores.push("El campo 'N° CPI' es obligatorio.");
        $('[id$=txtCPI]').addClass('is-invalid');
        isValid = false;
    } else {
        $('[id$=txtCPI]').removeClass('is-invalid');
    }

    if ($('[id$=txtOrdenViaje]').val().trim() === "") {
        errores.push("El campo 'N° Orden Viaje' es obligatorio.");
        $('[id$=txtOrdenViaje]').addClass('is-invalid');
        isValid = false;
    } else {
        $('[id$=txtOrdenViaje]').removeClass('is-invalid');
    }

    // Validar fechas
    let fechaSalida = $('[id$=txtFechaSalida]').val();
    let fechaLlegada = $('[id$=txtFechaLlegada]').val();
    let horaSalida = $('[id$=txtHoraSalida]').val();
    let horaLlegada = $('[id$=txtHoraLlegada]').val();
    let fechaActual = new Date();
    fechaActual.setHours(0, 0, 0, 0);

    if (!fechaSalida) {
        errores.push("La 'Fecha de Salida' es obligatoria.");
        $('[id$=txtFechaSalida]').addClass('is-invalid');
        isValid = false;
    } else {
        $('[id$=txtFechaSalida]').removeClass('is-invalid');
    }

    if (!horaSalida) {
        errores.push("La 'Hora de Salida' es obligatoria.");
        $('[id$=txtHoraSalida]').addClass('is-invalid');
        isValid = false;
    } else {
        $('[id$=txtHoraSalida]').removeClass('is-invalid');
    }

    if (!fechaLlegada) {
        errores.push("La 'Fecha de Llegada' es obligatoria.");
        $('[id$=txtFechaLlegada]').addClass('is-invalid');
        isValid = false;
    } else {
        $('[id$=txtFechaLlegada]').removeClass('is-invalid');
    }

    if (!horaLlegada) {
        errores.push("La 'Hora de Llegada' es obligatoria.");
        $('[id$=txtHoraLlegada]').addClass('is-invalid');
        isValid = false;
    } else {
        $('[id$=txtHoraLlegada]').removeClass('is-invalid');
    }

    if (fechaSalida && fechaLlegada) {
        let fechaSalidaDate = new Date(fechaSalida);
        let fechaLlegadaDate = new Date(fechaLlegada);

        if (fechaSalidaDate > fechaLlegadaDate) {
            errores.push("La 'Fecha de Salida' no puede ser mayor a la 'Fecha de Llegada'.");
            $('[id$=txtFechaSalida]').addClass('is-invalid');
            $('[id$=txtFechaLlegada]').addClass('is-invalid');
            isValid = false;
        }

        if (fechaLlegadaDate > fechaActual) {
            errores.push("La 'Fecha de Llegada' no puede ser mayor a la fecha actual (" + fechaActual.toLocaleDateString() + ").");
            $('[id$=txtFechaLlegada]').addClass('is-invalid');
            isValid = false;
        }
    }

    // Validar dropdowns
    if ($('[id$=ddlCliente]').val() === "") {
        errores.push("Debe seleccionar un 'Cliente'.");
        $('[id$=ddlCliente]').addClass('is-invalid');
        isValid = false;
    } else {
        $('[id$=ddlCliente]').removeClass('is-invalid');
    }

    if ($('[id$=ddlPlacaTracto]').val() === "") {
        errores.push("Debe seleccionar una 'Placa Tracto'.");
        $('[id$=ddlPlacaTracto]').addClass('is-invalid');
        isValid = false;
    } else {
        $('[id$=ddlPlacaTracto]').removeClass('is-invalid');
    }

    if ($('[id$=ddlPlacaCarreta]').val() === "") {
        errores.push("Debe seleccionar una 'Placa Carreta'.");
        $('[id$=ddlPlacaCarreta]').addClass('is-invalid');
        isValid = false;
    } else {
        $('[id$=ddlPlacaCarreta]').removeClass('is-invalid');
    }

    if ($('[id$=ddlConductor]').val() === "") {
        errores.push("Debe seleccionar un 'Conductor'.");
        $('[id$=ddlConductor]').addClass('is-invalid');
        isValid = false;
    } else {
        $('[id$=ddlConductor]').removeClass('is-invalid');
    }

    // Mostrar errores si los hay
    if (errores.length > 0) {
        alert(errores.join("\n"));
        return false;
    }

    // Limpiar el campo oculto antes de enviar al servidor
    $('[id$=hfValidationError]').val("");
    return isValid;
}

// Validar campos de Liquidación
function validarLiquidacion() {
    let isValid = true;
    let errores = [];

    // Validar que los valores no sean negativos
    $('#ingresosBody input[type="number"], #gastosFijosBody input[type="number"]').each(function () {
        let valor = parseFloat($(this).val()) || 0;
        if (valor < 0) {
            isValid = false;
            let nombreCampo = $(this).closest('tr').find('td:nth-child(2)').text();
            errores.push(`El valor para '${nombreCampo}' no puede ser negativo.`);
            $(this).addClass('is-invalid');
        } else {
            $(this).removeClass('is-invalid');
        }
    });

    // Validar que los gastos adicionales tengan nombre si tienen valores
    $("#gastosAdicionalesBody tr").each(function () {
        let nombreCategoria = $(this).find(".nombreCategoria").val().trim();
        let soles = parseFloat($(this).find(".soles").val()) || 0;
        let dolares = parseFloat($(this).find(".dolares").val()) || 0;

        // Si hay valor en soles o dólares, el nombre debe estar presente
        if ((soles > 0 || dolares > 0) && nombreCategoria === "") {
            isValid = false;
            errores.push("Debe ingresar un nombre para cada gasto adicional que tenga valores.");
            $(this).find(".nombreCategoria").addClass('is-invalid');
        } else {
            $(this).find(".nombreCategoria").removeClass('is-invalid');
        }

        // Verificar que no haya valores negativos
        if (soles < 0 || dolares < 0) {
            isValid = false;
            errores.push(`Los valores para el gasto '${nombreCategoria || "sin nombre"}' no pueden ser negativos.`);
            if (soles < 0) $(this).find(".soles").addClass('is-invalid');
            if (dolares < 0) $(this).find(".dolares").addClass('is-invalid');
        } else {
            $(this).find(".soles").removeClass('is-invalid');
            $(this).find(".dolares").removeClass('is-invalid');
        }
    });

    if (!isValid) {
        alert(errores.join("\n"));
    }

    return isValid;
}

// Función para agregar una fila de gasto adicional mejorada
// Función para agregar una fila de gasto adicional mejorada
function agregarFila() {
    try {
        const nuevaFila = `
            <tr>
                <td class="numeroFila"></td>
                <td><input type="text" class="form-control nombreCategoria" placeholder="Gasto Adicional" required></td>
                <td><input type="text" class="form-control descripcion" placeholder="Descripción"></td>
                <td><input type="number" class="form-control soles" placeholder="Soles" min="0" value="0" step="0.01"></td>
                <td><input type="number" class="form-control dolares" placeholder="Dólares" min="0" value="0" step="0.01"></td>
                <td class="text-center">
                    <button type="button" class="btn btn-danger btnEliminarFila">Eliminar</button>
                </td>
            </tr>
        `;

        $("#gastosAdicionalesBody").append(nuevaFila);
        recalcularNumeros();
        actualizarResumen();
        actualizarGastosAdicionales();

        // Aplicar validación al cambiar los valores
        const ultimaFila = $("#gastosAdicionalesBody tr:last");

        ultimaFila.find('.nombreCategoria, .descripcion, .soles, .dolares').on('change input blur', function () {
            let row = $(this).closest('tr');
            let nombreCategoria = row.find(".nombreCategoria").val().trim();
            let soles = parseFloat(row.find(".soles").val()) || 0;
            let dolares = parseFloat(row.find(".dolares").val()) || 0;

            if ((soles > 0 || dolares > 0) && nombreCategoria === "") {
                row.find(".nombreCategoria").addClass('is-invalid');
            } else {
                row.find(".nombreCategoria").removeClass('is-invalid');
            }

            // Actualizar el campo oculto cada vez que cambia un valor
            actualizarGastosAdicionales();
        });

        console.log("Fila de gasto adicional agregada");
    } catch (error) {
        console.error("Error en agregarFila: ", error);
    }
}


// Gestionar la eliminación de filas de gastos adicionales
$(document).on("click", ".btnEliminarFila", function () {
    $(this).closest("tr").remove();
    recalcularNumeros();
    actualizarResumen();
    actualizarGastosAdicionales();
});

// Función para recalcular los números de las filas
function recalcularNumeros() {
    $("#gastosAdicionalesBody tr").each(function (index) {
        $(this).find(".numeroFila").text(index + 9); // Comienza en 9 porque los gastos fijos terminan en 8
    });
}

// Función para limpiar los campos numéricos de las tablas Ingresos y Gastos
function limpiarCamposLiquidacion() {
    // Limpiar campos de Ingresos (Soles y Dólares)
    $("#ingresosBody tr").each(function () {
        $(this).find("td:eq(3) input").val(""); // Soles
        $(this).find("td:eq(4) input").val(""); // Dólares
    });

    // Limpiar campos de Gastos Fijos (Soles y Dólares)
    $("#gastosFijosBody tr").each(function () {
        $(this).find("td:eq(3) input").val(""); // Soles
        $(this).find("td:eq(4) input").val(""); // Dólares
    });

    // Limpiar campos de Gastos Adicionales (Soles y Dólares)
    $("#gastosAdicionalesBody tr").remove();

    actualizarGastosAdicionales();
}

// Función para actualizar el resumen de totales
// Función para actualizar el resumen de totales
function actualizarResumen() {
    // Calcular totales de Ingresos fijos
    let totalIngresosSoles = 0;
    let totalIngresosDolares = 0;

    // Ingresos fijos
    $("#ingresosFijosBody tr").each(function () {
        let soles = parseFloat($(this).find("td:eq(3) input").val()) || 0;
        let dolares = parseFloat($(this).find("td:eq(4) input").val()) || 0;
        totalIngresosSoles += soles;
        totalIngresosDolares += dolares;
    });

    // Ingresos adicionales
    $("#ingresosAdicionalesBody tr").each(function () {
        let soles = parseFloat($(this).find(".solesIngreso").val()) || 0;
        let dolares = parseFloat($(this).find(".dolaresIngreso").val()) || 0;
        totalIngresosSoles += soles;
        totalIngresosDolares += dolares;
    });

    // Calcular totales de Gastos (gastos fijos + adicionales)
    let totalGastosSoles = 0;
    let totalGastosDolares = 0;

    // Gastos fijos
    $("#gastosFijosBody tr").each(function () {
        let soles = parseFloat($(this).find("td:eq(3) input").val()) || 0;
        let dolares = parseFloat($(this).find("td:eq(4) input").val()) || 0;
        totalGastosSoles += soles;
        totalGastosDolares += dolares;
    });

    // Gastos adicionales
    $("#gastosAdicionalesBody tr").each(function () {
        let soles = parseFloat($(this).find(".soles").val()) || 0;
        let dolares = parseFloat($(this).find(".dolares").val()) || 0;
        totalGastosSoles += soles;
        totalGastosDolares += dolares;
    });

    // Calcular diferencia de saldo
    let diferenciaSaldoSoles = totalIngresosSoles - totalGastosSoles;
    let diferenciaSaldoDolares = totalIngresosDolares - totalGastosDolares;

    // Actualizar los valores en la tabla de Resumen
    $("#totalIngresosSoles").text(totalIngresosSoles.toFixed(2));
    $("#totalIngresosDolares").text(totalIngresosDolares.toFixed(2));
    $("#totalGastosSoles").text(totalGastosSoles.toFixed(2));
    $("#totalGastosDolares").text(totalGastosDolares.toFixed(2));
    $("#diferenciaSaldoSoles").text(diferenciaSaldoSoles.toFixed(2));
    $("#diferenciaSaldoDolares").text(diferenciaSaldoDolares.toFixed(2));
}

// Función para actualizar el campo oculto con los datos de gastos adicionales
// Función para actualizar el campo oculto con los datos de gastos adicionales
function actualizarGastosAdicionales() {
    try {
        // Array para almacenar los datos de gastos adicionales
        let gastosAdicionales = [];

        // Recorrer cada fila en la tabla de gastos adicionales
        $("#gastosAdicionalesBody tr").each(function () {
            // Obtener los valores de cada campo y sanitizarlos
            let nombreCategoria = $(this).find(".nombreCategoria").val() || "";
            nombreCategoria = nombreCategoria.trim().replace(/[\u201C\u201D\u201E\u201F\u2033\u2036]/g, '"'); // Reemplazar comillas especiales

            let descripcion = $(this).find(".descripcion").val() || "";
            descripcion = descripcion.trim().replace(/[\u201C\u201D\u201E\u201F\u2033\u2036]/g, '"');

            // Convertir a número para evitar problemas con formatos de números
            let soles = 0;
            let solesVal = $(this).find(".soles").val();
            if (solesVal && solesVal.trim() !== '') {
                soles = parseFloat(solesVal.replace(',', '.')) || 0;
            }

            let dolares = 0;
            let dolaresVal = $(this).find(".dolares").val();
            if (dolaresVal && dolaresVal.trim() !== '') {
                dolares = parseFloat(dolaresVal.replace(',', '.')) || 0;
            }

            // Añadir al array solo si hay un nombre de categoría o valores
            if (nombreCategoria !== "" || soles > 0 || dolares > 0) {
                gastosAdicionales.push({
                    nombreCategoria: nombreCategoria,
                    descripcion: descripcion,
                    soles: soles,
                    dolares: dolares
                });
            }
        });

        // Convertir el array a JSON y guardarlo en el campo oculto
        const jsonString = JSON.stringify(gastosAdicionales);

        // Verificar que el JSON sea válido antes de asignarlo
        try {
            JSON.parse(jsonString); // Esto lanzará una excepción si el JSON no es válido
            $('#hiddenGastosAdicionales').val(jsonString);
            console.log("JSON válido generado: ", jsonString);
            return true;
        } catch (jsonError) {
            console.error("JSON inválido generado: ", jsonString, jsonError);
            // Usar un array vacío en caso de error
            $('#hiddenGastosAdicionales').val("[]");
            return false;
        }
    } catch (error) {
        console.error("Error en actualizarGastosAdicionales: ", error);
        // Usar un array vacío en caso de error
        $('#hiddenGastosAdicionales').val("[]");
        return false;
    }
}


// Validar formulario completo antes de enviar
function validarFormulario() {
    try {
        let isValid = true;
        let errores = [];

        // Validar N° Guía Transportista
        const guiaTransportista = $('[id$=txtGuiaTransportista]').val().trim();
        if (guiaTransportista === "") {
            errores.push("El campo 'N° Guía Transportista' es obligatorio.");
            $('[id$=txtGuiaTransportista]').addClass('is-invalid');
            isValid = false;
        } else {
            $('[id$=txtGuiaTransportista]').removeClass('is-invalid');
        }

        // Validar N° Guía Cliente
        const guiaCliente = $('[id$=txtGuiaCliente]').val().trim();
        if (guiaCliente === "") {
            errores.push("El campo 'N° Guía Cliente' es obligatorio.");
            $('[id$=txtGuiaCliente]').addClass('is-invalid');
            isValid = false;
        } else {
            $('[id$=txtGuiaCliente]').removeClass('is-invalid');
        }

        // Validar Ruta
        const ruta = $('[id$=ddlRuta]').val();
        if (ruta === "") {
            errores.push("Debe seleccionar una 'Ruta'.");
            $('[id$=ddlRuta]').addClass('is-invalid');
            isValid = false;
        } else {
            $('[id$=ddlRuta]').removeClass('is-invalid');
        }

        // Validar Planta de Descarga y N° Manifiesto si la ruta es "Sullana-Guayaquil-Sullana"
        if (ruta === "2") { // idRuta = 2 para Sullana-Guayaquil-Sullana
            const plantaDescarga = $('[id$=ddlPlantaDescarga]').val();
            if (plantaDescarga === "") {
                errores.push("Debe seleccionar una 'Planta de Descarga' para la ruta Sullana-Guayaquil-Sullana.");
                $('[id$=ddlPlantaDescarga]').addClass('is-invalid');
                isValid = false;
            } else {
                $('[id$=ddlPlantaDescarga]').removeClass('is-invalid');
            }

            const manifiesto = $('[id$=txtManifiesto]').val().trim();
            if (manifiesto === "") {
                errores.push("El campo 'N° Manifiesto' es obligatorio para la ruta Sullana-Guayaquil-Sullana.");
                $('[id$=txtManifiesto]').addClass('is-invalid');
                isValid = false;
            } else {
                $('[id$=txtManifiesto]').removeClass('is-invalid');
            }
        }

        // Validar que la tabla de productos tenga al menos una fila con datos válidos
        let productosValidos = false;
        let productosData = [];

        $("#tablaProductos tbody tr").each(function () {
            const producto = $(this).find(".producto-dropdown").val();
            const cantidad = $(this).find("input[name='cantidad']").val();

            if (producto && producto !== "0" && cantidad && parseInt(cantidad) > 0) {
                productosValidos = true;
                productosData.push({
                    idProducto: producto,
                    cantidad: parseInt(cantidad)
                });
            }
        });

        if (!productosValidos) {
            errores.push("Debe agregar al menos un producto con cantidad válida.");
            $("#tablaProductos").addClass('is-invalid');
            isValid = false;
        } else {
            $("#tablaProductos").removeClass('is-invalid');
        }

        // Mostrar errores si los hay
        if (errores.length > 0) {
            alert(errores.join("\n"));
            return false;
        }

        // CRÍTICO: Actualizar SIEMPRE el campo oculto con los datos de gastos adicionales antes de enviar
        // Actualizar en validarFormulario()
        if (!actualizarGastosAdicionales() || !actualizarIngresosAdicionales()) {
            console.error("Error al actualizar gastos o ingresos adicionales");
            return false;
        }

        // Verificar que el campo tenga un valor válido
        let gastosValue = $('#hiddenGastosAdicionales').val();
        console.log("Valor final de hiddenGastosAdicionales antes de enviar:", gastosValue);

        if (!gastosValue || gastosValue === '[]') {
            console.warn("El campo hiddenGastosAdicionales está vacío o solo contiene []");
            // Esto no es un error, puede ser que no haya gastos adicionales
        }

        // Enviar los datos de los productos al servidor
        $('<input>').attr({
            type: 'hidden',
            name: 'productosData',
            value: JSON.stringify(productosData)
        }).appendTo('#formGuias');

        return isValid;
    } catch (error) {
        console.error("Error en validarFormulario: ", error);
        return false;
    }
}


// Función para mostrar/ocultar campos adicionales al seleccionar ruta
function toggleRutaDetails() {
    const selectedRouteValue = $('#ddlRuta').val();
    if (selectedRouteValue === "2") { // Ruta "Sullana-Guayaquil-Sullana"
        $('#rutaDetails').slideDown();
        // Hacer que los campos sean requeridos
        $('[id$=ddlPlantaDescarga]').prop('required', true);
        $('[id$=txtManifiesto]').prop('required', true);
    } else {
        $('#rutaDetails').slideUp();
        // Quitar el requerido de los campos
        $('[id$=ddlPlantaDescarga]').prop('required', false);
        $('[id$=txtManifiesto]').prop('required', false);
    }
}

// Función para agregar fila de producto
function agregarFilaProducto() {
    const nuevaFila = `
        <tr>
            <td>
                <select class="form-control producto-dropdown" onchange="actualizarSeleccion(this)">
                    <option value="0">Seleccione un producto</option>
                    ${productos.map(p => `<option value="${p.idProducto}">${p.nombre}</option>`).join('')}
                </select>
            </td>
            <td>
                <input type="number" class="form-control" placeholder="Cantidad" name="cantidad" min="1">
            </td>
            <td class="text-center">
                <button type="button" class="btn btn-primary btn-add-row">Añadir</button>
            </td>
            <td class="text-center">
                <button type="button" class="btn btn-danger btn-remove-row">Eliminar</button>
            </td>
        </tr>
    `;

    $("#tablaProductos tbody").append(nuevaFila);
    actualizarDropdowns();
}

// Actualizar selección de productos
function actualizarSeleccion(dropdown) {
    const valorAnterior = dropdown.dataset.valorAnterior || "0";
    const valorNuevo = dropdown.value;

    // Liberar el producto previamente seleccionado
    if (valorAnterior !== "0") {
        productosSeleccionados.delete(valorAnterior);
    }

    // Validar la nueva selección
    if (valorNuevo !== "0") {
        if (productosSeleccionados.has(valorNuevo)) {
            alert("Este producto ya está seleccionado.");
            dropdown.value = "0"; // Restablecer selección
            return;
        }
        productosSeleccionados.add(valorNuevo);
    }

    dropdown.dataset.valorAnterior = valorNuevo; // Guardar la nueva selección
    actualizarDropdowns();
}

// Deshabilitar productos seleccionados en otros dropdowns
function actualizarDropdowns() {
    $("#tablaProductos .producto-dropdown").each(function () {
        const dropdown = $(this);
        const opciones = dropdown.find("option");
        opciones.each(function () {
            const opcion = $(this);
            if (productosSeleccionados.has(opcion.val()) && opcion.val() !== dropdown.val()) {
                opcion.prop("disabled", true); // Deshabilitar producto seleccionado
            } else {
                opcion.prop("disabled", false); // Habilitar si está disponible
            }
        });
    });
}

// Eventos para gestionar productos
$(document).on("click", ".btn-add-row", function () {
    const ultimaFila = $("#tablaProductos tbody tr:last");
    if (ultimaFila.length && !validarFila(ultimaFila)) {
        alert("Complete todos los datos de la fila actual antes de agregar una nueva.");
        return;
    }
    agregarFilaProducto();
});

// Eliminar una fila de producto
$(document).on("click", ".btn-remove-row", function () {
    if ($("#tablaProductos tbody tr").length > 1) {
        const fila = $(this).closest("tr");
        const dropdown = fila.find(".producto-dropdown");
        const valorSeleccionado = dropdown.val();

        // Liberar el producto seleccionado
        if (valorSeleccionado !== "0") {
            productosSeleccionados.delete(valorSeleccionado);
        }

        fila.remove();
        actualizarDropdowns();
    } else {
        alert("No puede eliminar todas las filas. Debe haber al menos una.");
    }
});

// Validar una fila de producto antes de agregar otra
function validarFila(fila) {
    const producto = fila.find(".producto-dropdown").val();
    const cantidad = fila.find("input[name='cantidad']").val();

    if (producto === "0" || !cantidad || cantidad <= 0) {
        return false;
    }
    return true;
}

// Agregar funciones para manejar navegación entre pestañas
function showPreviousTab(tabId) {
    showTab(tabId);
}

function showNextTab(tabId) {
    if (tabId === 'liquidacion' && !validarDatosViaje()) {
        return false;
    }

    if (tabId === 'guias' && !validarLiquidacion()) {
        return false;
    }

    showTab(tabId);
    return true;
}



// Función para agregar una fila de ingreso adicional
function agregarFilaIngreso() {
    try {
        const nuevaFila = `
            <tr>
                <td class="numeroFilaIngreso"></td>
                <td><input type="text" class="form-control nombreCategoriaIngreso" placeholder="Ingreso Adicional" required></td>
                <td><input type="text" class="form-control descripcionIngreso" placeholder="Descripción"></td>
                <td><input type="number" class="form-control solesIngreso" placeholder="Soles" min="0" value="0" step="0.01"></td>
                <td><input type="number" class="form-control dolaresIngreso" placeholder="Dólares" min="0" value="0" step="0.01"></td>
                <td class="text-center">
                    <button type="button" class="btn btn-danger btnEliminarFilaIngreso">Eliminar</button>
                </td>
            </tr>
        `;

        $("#ingresosAdicionalesBody").append(nuevaFila);
        recalcularNumerosIngreso();
        actualizarResumen();
        actualizarIngresosAdicionales();

        // Aplicar validación al cambiar los valores
        const ultimaFila = $("#ingresosAdicionalesBody tr:last");

        ultimaFila.find('.nombreCategoriaIngreso, .descripcionIngreso, .solesIngreso, .dolaresIngreso').on('change input blur', function () {
            let row = $(this).closest('tr');
            let nombreCategoria = row.find(".nombreCategoriaIngreso").val().trim();
            let soles = parseFloat(row.find(".solesIngreso").val()) || 0;
            let dolares = parseFloat(row.find(".dolaresIngreso").val()) || 0;

            if ((soles > 0 || dolares > 0) && nombreCategoria === "") {
                row.find(".nombreCategoriaIngreso").addClass('is-invalid');
            } else {
                row.find(".nombreCategoriaIngreso").removeClass('is-invalid');
            }

            // Actualizar el campo oculto cada vez que cambia un valor
            actualizarIngresosAdicionales();
        });

        console.log("Fila de ingreso adicional agregada");
    } catch (error) {
        console.error("Error en agregarFilaIngreso: ", error);
    }
}

// Gestionar la eliminación de filas de ingresos adicionales
$(document).on("click", ".btnEliminarFilaIngreso", function () {
    $(this).closest("tr").remove();
    recalcularNumerosIngreso();
    actualizarResumen();
    actualizarIngresosAdicionales();
});

// Función para recalcular los números de las filas de ingresos
function recalcularNumerosIngreso() {
    $("#ingresosAdicionalesBody tr").each(function (index) {
        $(this).find(".numeroFilaIngreso").text(index + 5); // Comienza en 5 porque los ingresos fijos terminan en 4
    });
}

// Función para actualizar el campo oculto con los datos de ingresos adicionales
function actualizarIngresosAdicionales() {
    try {
        // Array para almacenar los datos de ingresos adicionales
        let ingresosAdicionales = [];

        // Recorrer cada fila en la tabla de ingresos adicionales
        $("#ingresosAdicionalesBody tr").each(function () {
            // Obtener los valores de cada campo y sanitizarlos
            let nombreCategoria = $(this).find(".nombreCategoriaIngreso").val() || "";
            nombreCategoria = nombreCategoria.trim().replace(/[\u201C\u201D\u201E\u201F\u2033\u2036]/g, '"');

            let descripcion = $(this).find(".descripcionIngreso").val() || "";
            descripcion = descripcion.trim().replace(/[\u201C\u201D\u201E\u201F\u2033\u2036]/g, '"');

            // Convertir a número para evitar problemas con formatos
            let soles = 0;
            let solesVal = $(this).find(".solesIngreso").val();
            if (solesVal && solesVal.trim() !== '') {
                soles = parseFloat(solesVal.replace(',', '.')) || 0;
            }

            let dolares = 0;
            let dolaresVal = $(this).find(".dolaresIngreso").val();
            if (dolaresVal && dolaresVal.trim() !== '') {
                dolares = parseFloat(dolaresVal.replace(',', '.')) || 0;
            }

            // Añadir al array solo si hay un nombre de categoría o valores
            if (nombreCategoria !== "" || soles > 0 || dolares > 0) {
                ingresosAdicionales.push({
                    nombreCategoria: nombreCategoria,
                    descripcion: descripcion,
                    soles: soles,
                    dolares: dolares
                });
            }
        });

        // Convertir el array a JSON y guardarlo en el campo oculto
        const jsonString = JSON.stringify(ingresosAdicionales);

        // Verificar que el JSON sea válido antes de asignarlo
        try {
            JSON.parse(jsonString); // Esto lanzará una excepción si el JSON no es válido
            $('#hiddenIngresosAdicionales').val(jsonString);
            console.log("JSON de ingresos válido generado: ", jsonString);
            return true;
        } catch (jsonError) {
            console.error("JSON de ingresos inválido generado: ", jsonString, jsonError);
            // Usar un array vacío en caso de error
            $('#hiddenIngresosAdicionales').val("[]");
            return false;
        }
    } catch (error) {
        console.error("Error en actualizarIngresosAdicionales: ", error);
        // Usar un array vacío en caso de error
        $('#hiddenIngresosAdicionales').val("[]");
        return false;
    }
}
/* =============================================
   JAVASCRIPT RESPONSIVE - CONVERSIÓN TABLA A CARDS
   ============================================= */

// Detectar si es dispositivo móvil
function esMobile() {
    return window.innerWidth <= 768;
}

// Inicializar vista responsive
function inicializarVistaResponsive() {
    if (esMobile()) {
        convertirIngresosACards();
        convertirGastosACards();
    }

    // Escuchar cambios de tamaño de ventana
    let resizeTimer;
    window.addEventListener('resize', function () {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(function () {
            if (esMobile()) {
                convertirIngresosACards();
                convertirGastosACards();
            }
        }, 250);
    });
}

// Convertir tabla de ingresos a cards
function convertirIngresosACards() {
    const tbody = document.getElementById('ingresosAdicionalesBody');
    if (!tbody) return;

    const rows = tbody.querySelectorAll('tr');
    let cardsHTML = '';

    rows.forEach((row, index) => {
        const concepto = row.querySelector('[name^="conceptoIngreso_"]');
        const descripcion = row.querySelector('[name^="descIngreso_"]');
        const soles = row.querySelector('[name^="ingresoSoles_"]');
        const dolares = row.querySelector('[name^="ingresoDolares_"]');

        if (concepto && descripcion && soles && dolares) {
            cardsHTML += `
                <div class="financial-card" data-row-index="${index}">
                    <button type="button" class="btn btn-danger btn-sm card-delete-btn" 
                            onclick="eliminarFilaIngreso(this)">
                        <i class="fas fa-times"></i>
                    </button>
                    
                    <div class="financial-card-header">
                        <span class="financial-card-title">Ingreso #${index + 1}</span>
                    </div>
                    
                    <div class="financial-card-body">
                        <div class="card-field">
                            <label class="card-field-label">Concepto</label>
                            <div class="card-field-input">
                                <input type="text" class="form-control" 
                                       name="${concepto.name}" 
                                       value="${concepto.value}"
                                       placeholder="Ej: Adelanto">
                            </div>
                        </div>
                        
                        <div class="card-field">
                            <label class="card-field-label">Descripción</label>
                            <div class="card-field-input">
                                <textarea class="form-control" 
                                          name="${descripcion.name}" 
                                          rows="2"
                                          placeholder="Detalles adicionales">${descripcion.value}</textarea>
                            </div>
                        </div>
                        
                        <div class="card-field">
                            <label class="card-field-label">Montos</label>
                            <div class="currency-input-group">
                                <div class="currency-input">
                                    <span class="currency-symbol">S/</span>
                                    <input type="number" class="form-control" 
                                           name="${soles.name}" 
                                           value="${soles.value}"
                                           placeholder="0.00" 
                                           step="0.01"
                                           onchange="calcularTotales()">
                                </div>
                                <div class="currency-input">
                                    <span class="currency-symbol">$</span>
                                    <input type="number" class="form-control" 
                                           name="${dolares.name}" 
                                           value="${dolares.value}"
                                           placeholder="0.00" 
                                           step="0.01"
                                           onchange="calcularTotales()">
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            `;
        }
    });

    // Crear o actualizar contenedor de cards
    let mobileContainer = document.getElementById('ingresosCardsMobile');
    if (!mobileContainer) {
        mobileContainer = document.createElement('div');
        mobileContainer.id = 'ingresosCardsMobile';
        mobileContainer.className = 'mobile-card-view';
        tbody.parentElement.parentElement.appendChild(mobileContainer);
    }
    mobileContainer.innerHTML = cardsHTML;
}

// Convertir tabla de gastos a cards
function convertirGastosACards() {
    const tbody = document.getElementById('gastosAdicionalesBody');
    if (!tbody) return;

    const rows = tbody.querySelectorAll('tr');
    let cardsHTML = '';

    rows.forEach((row, index) => {
        const concepto = row.querySelector('[name^="conceptoGasto_"]');
        const descripcion = row.querySelector('[name^="descGasto_"]');
        const soles = row.querySelector('[name^="gastoSoles_"]');
        const dolares = row.querySelector('[name^="gastoDolares_"]');

        if (concepto && descripcion && soles && dolares) {
            cardsHTML += `
                <div class="financial-card" data-row-index="${index}">
                    <button type="button" class="btn btn-danger btn-sm card-delete-btn" 
                            onclick="eliminarFilaGasto(this)">
                        <i class="fas fa-times"></i>
                    </button>
                    
                    <div class="financial-card-header">
                        <span class="financial-card-title">Gasto #${index + 1}</span>
                    </div>
                    
                    <div class="financial-card-body">
                        <div class="card-field">
                            <label class="card-field-label">Concepto</label>
                            <div class="card-field-input">
                                <input type="text" class="form-control" 
                                       name="${concepto.name}" 
                                       value="${concepto.value}"
                                       placeholder="Ej: Peaje">
                            </div>
                        </div>
                        
                        <div class="card-field">
                            <label class="card-field-label">Descripción</label>
                            <div class="card-field-input">
                                <textarea class="form-control" 
                                          name="${descripcion.name}" 
                                          rows="2"
                                          placeholder="Detalles del gasto">${descripcion.value}</textarea>
                            </div>
                        </div>
                        
                        <div class="card-field">
                            <label class="card-field-label">Montos</label>
                            <div class="currency-input-group">
                                <div class="currency-input">
                                    <span class="currency-symbol">S/</span>
                                    <input type="number" class="form-control" 
                                           name="${soles.name}" 
                                           value="${soles.value}"
                                           placeholder="0.00" 
                                           step="0.01"
                                           onchange="calcularTotales()">
                                </div>
                                <div class="currency-input">
                                    <span class="currency-symbol">$</span>
                                    <input type="number" class="form-control" 
                                           name="${dolares.name}" 
                                           value="${dolares.value}"
                                           placeholder="0.00" 
                                           step="0.01"
                                           onchange="calcularTotales()">
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            `;
        }
    });

    // Crear o actualizar contenedor de cards
    let mobileContainer = document.getElementById('gastosCardsMobile');
    if (!mobileContainer) {
        mobileContainer = document.createElement('div');
        mobileContainer.id = 'gastosCardsMobile';
        mobileContainer.className = 'mobile-card-view';
        tbody.parentElement.parentElement.appendChild(mobileContainer);
    }
    mobileContainer.innerHTML = cardsHTML;
}

// Agregar nueva fila de ingreso (versión mobile-friendly)
function agregarFilaIngresoMobile() {
    const tbody = document.getElementById('ingresosAdicionalesBody');
    const rowCount = tbody.querySelectorAll('tr').length;

    const newRow = document.createElement('tr');
    newRow.innerHTML = `
        <td>
            <input type="text" class="form-control" name="conceptoIngreso_${rowCount}" 
                   placeholder="Concepto" />
        </td>
        <td>
            <input type="text" class="form-control" name="descIngreso_${rowCount}" 
                   placeholder="Descripción" />
        </td>
        <td>
            <input type="number" class="form-control text-right" name="ingresoSoles_${rowCount}" 
                   placeholder="0.00" step="0.01" onchange="calcularTotales()" />
        </td>
        <td>
            <input type="number" class="form-control text-right" name="ingresoDolares_${rowCount}" 
                   placeholder="0.00" step="0.01" onchange="calcularTotales()" />
        </td>
        <td class="text-center">
            <button type="button" class="btn btn-danger btn-sm" onclick="eliminarFilaIngreso(this)">
                <i class="fas fa-trash"></i>
            </button>
        </td>
    `;

    tbody.appendChild(newRow);

    // Actualizar vista mobile
    if (esMobile()) {
        convertirIngresosACards();
    }
}

// Agregar nueva fila de gasto (versión mobile-friendly)
function agregarFilaGastoMobile() {
    const tbody = document.getElementById('gastosAdicionalesBody');
    const rowCount = tbody.querySelectorAll('tr').length;

    const newRow = document.createElement('tr');
    newRow.innerHTML = `
        <td>
            <input type="text" class="form-control" name="conceptoGasto_${rowCount}" 
                   placeholder="Concepto" />
        </td>
        <td>
            <input type="text" class="form-control" name="descGasto_${rowCount}" 
                   placeholder="Descripción" />
        </td>
        <td>
            <input type="number" class="form-control text-right" name="gastoSoles_${rowCount}" 
                   placeholder="0.00" step="0.01" onchange="calcularTotales()" />
        </td>
        <td>
            <input type="number" class="form-control text-right" name="gastoDolares_${rowCount}" 
                   placeholder="0.00" step="0.01" onchange="calcularTotales()" />
        </td>
        <td class="text-center">
            <button type="button" class="btn btn-danger btn-sm" onclick="eliminarFilaGasto(this)">
                <i class="fas fa-trash"></i>
            </button>
        </td>
    `;

    tbody.appendChild(newRow);

    // Actualizar vista mobile
    if (esMobile()) {
        convertirGastosACards();
    }
}

// Función mejorada para eliminar fila de ingreso
function eliminarFilaIngreso(btn) {
    if (esMobile()) {
        // En móvil, buscar el card padre
        const card = btn.closest('.financial-card');
        const rowIndex = card.getAttribute('data-row-index');
        const tbody = document.getElementById('ingresosAdicionalesBody');
        const row = tbody.querySelectorAll('tr')[rowIndex];
        if (row) {
            row.remove();
            convertirIngresosACards();
        }
    } else {
        // En desktop, comportamiento normal
        btn.closest('tr').remove();
    }
    calcularTotales();
}

// Función mejorada para eliminar fila de gasto
function eliminarFilaGasto(btn) {
    if (esMobile()) {
        // En móvil, buscar el card padre
        const card = btn.closest('.financial-card');
        const rowIndex = card.getAttribute('data-row-index');
        const tbody = document.getElementById('gastosAdicionalesBody');
        const row = tbody.querySelectorAll('tr')[rowIndex];
        if (row) {
            row.remove();
            convertirGastosACards();
        }
    } else {
        // En desktop, comportamiento normal
        btn.closest('tr').remove();
    }
    calcularTotales();
}

// Mejorar visualización de totales en móvil
function actualizarTotalesMobile() {
    if (!esMobile()) return;

    // Obtener totales
    const totalIngresosSoles = parseFloat($('#totalIngresosSoles').text().replace('S/', '').replace(',', '')) || 0;
    const totalIngresosDolares = parseFloat($('#totalIngresosDolares').text().replace('$', '').replace(',', '')) || 0;
    const totalGastosSoles = parseFloat($('#totalGastosSoles').text().replace('S/', '').replace(',', '')) || 0;
    const totalGastosDolares = parseFloat($('#totalGastosDolares').text().replace('$', '').replace(',', '')) || 0;

    // Crear o actualizar sección de totales mobile
    let totalesIngresosMobile = document.getElementById('totalesIngresosMobile');
    if (!totalesIngresosMobile) {
        totalesIngresosMobile = document.createElement('div');
        totalesIngresosMobile.id = 'totalesIngresosMobile';
        totalesIngresosMobile.className = 'totales-mobile mobile-card-view';
        document.getElementById('ingresosCardsMobile').appendChild(totalesIngresosMobile);
    }

    totalesIngresosMobile.innerHTML = `
        <div class="totales-mobile-title">
            <i class="fas fa-calculator mr-2"></i>Total Ingresos
        </div>
        <div class="total-row">
            <span class="total-label">Soles:</span>
            <span class="total-value">S/ ${totalIngresosSoles.toFixed(2)}</span>
        </div>
        <div class="total-row">
            <span class="total-label">Dólares:</span>
            <span class="total-value">$ ${totalIngresosDolares.toFixed(2)}</span>
        </div>
    `;

    let totalesGastosMobile = document.getElementById('totalesGastosMobile');
    if (!totalesGastosMobile) {
        totalesGastosMobile = document.createElement('div');
        totalesGastosMobile.id = 'totalesGastosMobile';
        totalesGastosMobile.className = 'totales-mobile mobile-card-view';
        document.getElementById('gastosCardsMobile').appendChild(totalesGastosMobile);
    }

    totalesGastosMobile.innerHTML = `
        <div class="totales-mobile-title">
            <i class="fas fa-calculator mr-2"></i>Total Gastos
        </div>
        <div class="total-row">
            <span class="total-label">Soles:</span>
            <span class="total-value">S/ ${totalGastosSoles.toFixed(2)}</span>
        </div>
        <div class="total-row">
            <span class="total-label">Dólares:</span>
            <span class="total-value">$ ${totalGastosDolares.toFixed(2)}</span>
        </div>
    `;
}

// Inicializar cuando el DOM esté listo
$(document).ready(function () {
    inicializarVistaResponsive();

    // Sobrescribir funciones originales si es móvil
    if (esMobile()) {
        // Guardar referencias a funciones originales
        window.agregarFilaIngresoOriginal = window.agregarFilaIngreso || function () { };
        window.agregarFilaGastoOriginal = window.agregarFilaGasto || function () { };

        // Reemplazar con versiones mobile
        window.agregarFilaIngreso = agregarFilaIngresoMobile;
        window.agregarFilaGasto = agregarFilaGastoMobile;
    }
});
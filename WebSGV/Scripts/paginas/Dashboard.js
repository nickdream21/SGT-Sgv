// Todos los gráficos inicializados
var charts = {};

// Inicializar todos los gráficos después de un postback
function pageLoad() {
    try {
        console.log("Iniciando carga de página...");

        // Registrar el plugin datalabels primero
        Chart.register(ChartDataLabels);

        // Obtener el elemento oculto - NOTA: usar la referencia dinámica correcta
        var hiddenField = document.getElementById(SGV.hdnDatosGraficos);

        if (!hiddenField || !hiddenField.value) {
            console.error("Campo oculto no encontrado o vacío");
            return;
        }

        // Pre-procesar la cadena JSON para eliminar caracteres problemáticos
        var rawValue = hiddenField.value;
        console.log("Valor original:", rawValue.substring(0, 100) + "..."); // Muestra los primeros 100 caracteres

        try {
            // Intenta parsear directamente
            var datosJSON = JSON.parse(rawValue);
            console.log("JSON parseado correctamente");
            inicializarGraficos(datosJSON);
        } catch (parseError) {
            console.error("Error en primer intento de parseo:", parseError);

            // Si falla, intenta limpiar la cadena y volver a intentar
            try {
                // Reemplazar caracteres problemáticos y volver a intentar
                var cleanedValue = rawValue
                    .replace(/\n/g, "\\n")
                    .replace(/\r/g, "\\r")
                    .replace(/\t/g, "\\t")
                    .replace(/\f/g, "\\f");

                var datosJSON = JSON.parse(cleanedValue);
                console.log("JSON parseado después de limpieza");
                inicializarGraficos(datosJSON);
            } catch (finalError) {
                console.error("Error final en parseo:", finalError);
                alert("No se pudieron cargar los datos. Por favor intente de nuevo.");
            }
        }
    } catch (e) {
        console.error("Error general:", e);
        alert("Ocurrió un error: " + e.message);
    }
}

// Función para traducir nombres de meses
function traducirMeses(meses) {
    const traduccion = {
        'january': 'enero',
        'february': 'febrero',
        'march': 'marzo',
        'april': 'abril',
        'may': 'mayo',
        'june': 'junio',
        'july': 'julio',
        'august': 'agosto',
        'september': 'septiembre',
        'october': 'octubre',
        'november': 'noviembre',
        'december': 'diciembre'
    };

    return meses.map(mes => traduccion[mes.toLowerCase()] || mes);
}

// Inicializar gráficos con los datos recibidos
// Inicializar gráficos con los datos recibidos
function inicializarGraficos(datos) {
    // Configuración base común para todos los gráficos
    const baseOptions = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: {
                position: 'right',
                labels: { color: '#fff', font: { size: 12 } }
            },
            tooltip: { mode: 'index', intersect: false }
        },
        scales: {
            y: {
                beginAtZero: true,
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' }
            },
            x: {
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' }
            }
        }
    };

    // Registrar el plugin datalabels primero
    Chart.register(ChartDataLabels);

    // Opciones para gráficos de barras (con datalabels)
    const barOptions = JSON.parse(JSON.stringify(baseOptions));
    barOptions.plugins.datalabels = {
        display: true,
        color: '#fff',
        font: { weight: 'bold', size: 10 },
        formatter: function (value) {
            return value.toFixed(3);
        },
        anchor: 'end',
        align: 'top',
        offset: 0
    };

    // Opciones para gráficos de línea (sin datalabels)
    const lineOptions = JSON.parse(JSON.stringify(baseOptions));
    lineOptions.plugins.datalabels = {
        display: false
    };

    // Gráfico Tiempos promedio en Trujillo (gráfico de barras)
    inicializarGraficoTrujillo(datos, barOptions);

    // Gráfico Tiempo Trujillo-Ecuador (gráfico de barras)
    inicializarGraficoTrujilloEcuador(datos, barOptions);

    // Gráfico Tiempo Base (gráfico de barras)
    inicializarGraficoBase(datos, barOptions);

    // Gráfico Inbalnor (gráfico de barras)
    inicializarGraficoInbalnor(datos, barOptions);

    // Gráfico Jave (gráfico de barras)
    inicializarGraficoJave(datos, barOptions);

    // Inicializar gráficos lineales de la pestaña Trámite Aduanero si están visibles
    if (document.getElementById('chartEsperaDepsa')) {
        // Estos son gráficos lineales
        inicializarGraficoEsperaDepsa(datos, lineOptions);
        inicializarGraficoEsperaComplex(datos, lineOptions);
        inicializarGraficoTiempoDepsa(datos, lineOptions);
        inicializarGraficoTiempoComplex(datos, lineOptions);
        inicializarGraficoTiempoCebaf(datos, lineOptions);
    }

    // Inicializar gráficos lineales de la pestaña Tiempos Adicionales si están visibles
    if (document.getElementById('chartTiempoTCI')) {
        // Estos son gráficos lineales
        inicializarGraficoTiempoTCI(datos, lineOptions);
        inicializarGraficoTiempoPuyango(datos, lineOptions);
        inicializarGraficoEsperaNacionalizacion(datos, lineOptions);
    }

    // Inicializar gráficos de barras de la pestaña Bodegas y Distancias si están visibles
    if (document.getElementById('chartBodegaInbalnor')) {
        // Estos son gráficos de barras
        inicializarGraficoBodegaInbalnor(datos, barOptions);
        inicializarGraficoBodegaJave(datos, barOptions);
        inicializarGraficoEcuatorianaInbalnor(datos, barOptions);
        inicializarGraficoEcuatorianaJave(datos, barOptions);
    }
}

// Función para inicializar el gráfico de Tiempos promedio en Trujillo con filtro por mes
function inicializarGraficoTrujillo(datos, commonOptions) {
    const ctxTrujillo = document.getElementById('chartTrujillo');
    if (!ctxTrujillo) return;

    // Destruir gráfico existente si lo hay
    if (charts.chartTrujillo) {
        charts.chartTrujillo.destroy();
    }

    // Obtener el mes seleccionado (1=enero, 2=febrero, etc.)
    const mesSeleccionado = document.getElementById(SGV.ddlMes).value;

    // Filtrar los datos según el mes seleccionado
    // Primero encontramos los índices de los meses que coinciden con el mes seleccionado
    let indicesFiltrados = [];
    let mesesTrujillo = datos.MesesTrujillo || [];

    // Mapeo de nombres de meses en inglés al número de mes
    const mesNumero = {
        'january': '1',
        'febrero': '2',
        'february': '2',
        'marzo': '3',
        'march': '3',
        'april': '4',
        'abril': '4',
        'may': '5',
        'mayo': '5',
        'june': '6',
        'junio': '6',
        'july': '7',
        'julio': '7',
        'august': '8',
        'agosto': '8',
        'september': '9',
        'septiembre': '9',
        'october': '10',
        'octubre': '10',
        'november': '11',
        'noviembre': '11',
        'december': '12',
        'diciembre': '12'
    };

    // Buscar los índices que corresponden al mes seleccionado
    for (let i = 0; i < mesesTrujillo.length; i++) {
        // Convertir a minúsculas para comparación
        const mes = mesesTrujillo[i].toLowerCase();

        // Si el mes coincide con el mes seleccionado (por nombre o por posición)
        if (mesNumero[mes] === mesSeleccionado || i === parseInt(mesSeleccionado) - 1) {
            indicesFiltrados.push(i);
        }
    }

    // Si no encontramos coincidencias, usar todos los datos (fallback)
    if (indicesFiltrados.length === 0) {
        indicesFiltrados = [...Array(mesesTrujillo.length).keys()]; // todos los índices
    }

    // Filtrar los arrays de datos según los índices encontrados
    const mesesFiltrados = indicesFiltrados.map(i => mesesTrujillo[i]);
    const esperaIngresoFiltrado = indicesFiltrados.map(i => datos.EsperaIngresoTrujillo ? datos.EsperaIngresoTrujillo[i] : 0);
    const esperaInicioFiltrado = indicesFiltrados.map(i => datos.EsperaInicio ? datos.EsperaInicio[i] : 0);
    const cargaFiltrado = indicesFiltrados.map(i => datos.Carga ? datos.Carga[i] : 0);
    const permanenciaFiltrado = indicesFiltrados.map(i => datos.PermanenciaTrujillo ? datos.PermanenciaTrujillo[i] : 0);

    // Traducir los meses filtrados a español
    const mesesTraducidos = traducirMeses(mesesFiltrados);

    charts.chartTrujillo = new Chart(ctxTrujillo, {
        type: 'bar',
        data: {
            labels: mesesTraducidos,
            datasets: [
                {
                    label: 'Espera a ingreso Trujillo',
                    data: esperaIngresoFiltrado,
                    backgroundColor: 'rgba(255, 99, 132, 0.7)',
                    borderColor: 'rgba(255, 99, 132, 1)',
                    borderWidth: 1
                },
                {
                    label: 'Espera inicio',
                    data: esperaInicioFiltrado,
                    backgroundColor: 'rgba(255, 159, 64, 0.7)',
                    borderColor: 'rgba(255, 159, 64, 1)',
                    borderWidth: 1
                },
                {
                    label: 'Carga',
                    data: cargaFiltrado,
                    backgroundColor: 'rgba(255, 205, 86, 0.7)',
                    borderColor: 'rgba(255, 205, 86, 1)',
                    borderWidth: 1
                },
                {
                    label: 'Permanencia en planta Trujillo',
                    data: permanenciaFiltrado,
                    backgroundColor: 'rgba(75, 192, 192, 0.7)',
                    borderColor: 'rgba(75, 192, 192, 1)',
                    borderWidth: 1
                }
            ]
        },
        options: commonOptions
    });
}

// Función para inicializar el gráfico de Tiempo Trujillo-Ecuador
// Aplicar el mismo patrón de filtrado a las demás funciones de inicialización
// Por ejemplo, para Trujillo-Ecuador:
function inicializarGraficoTrujilloEcuador(datos, commonOptions) {
    const ctx = document.getElementById('chartTrujilloEcuador');
    if (!ctx) return;

    if (charts.chartTrujilloEcuador) {
        charts.chartTrujilloEcuador.destroy();
    }

    // Obtener el mes seleccionado
    const mesSeleccionado = document.getElementById(SGV.ddlMes).value;

    // Filtrar los datos según el mes seleccionado
    let indicesFiltrados = [];
    let mesesTrujilloEcuador = datos.MesesTrujilloEcuador || [];

    // Mapeo de nombres de meses en inglés al número de mes
    const mesNumero = {
        'january': '1',
        'febrero': '2',
        'february': '2',
        'marzo': '3',
        'march': '3',
        'april': '4',
        'abril': '4',
        'may': '5',
        'mayo': '5',
        'june': '6',
        'junio': '6',
        'july': '7',
        'julio': '7',
        'august': '8',
        'agosto': '8',
        'september': '9',
        'septiembre': '9',
        'october': '10',
        'octubre': '10',
        'november': '11',
        'noviembre': '11',
        'december': '12',
        'diciembre': '12'
    };

    // Buscar los índices que corresponden al mes seleccionado
    for (let i = 0; i < mesesTrujilloEcuador.length; i++) {
        const mes = mesesTrujilloEcuador[i].toLowerCase();
        if (mesNumero[mes] === mesSeleccionado || i === parseInt(mesSeleccionado) - 1) {
            indicesFiltrados.push(i);
        }
    }

    // Si no encontramos coincidencias, usar todos los datos
    if (indicesFiltrados.length === 0) {
        indicesFiltrados = [...Array(mesesTrujilloEcuador.length).keys()];
    }

    // Filtrar los arrays de datos
    const mesesFiltrados = indicesFiltrados.map(i => mesesTrujilloEcuador[i]);
    const tiempoFiltrado = indicesFiltrados.map(i => datos.TiempoTrujilloEcuador ? datos.TiempoTrujilloEcuador[i] : 0);

    // Traducir los meses filtrados a español
    const mesesTraducidos = traducirMeses(mesesFiltrados);

    charts.chartTrujilloEcuador = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: mesesTraducidos,
            datasets: [
                {
                    label: 'Días',
                    data: tiempoFiltrado,
                    backgroundColor: 'rgba(54, 162, 235, 0.7)',
                    borderColor: 'rgba(54, 162, 235, 1)',
                    borderWidth: 1
                }
            ]
        },
        options: commonOptions
    });
}

// Función para inicializar el gráfico de Tiempo Base
function inicializarGraficoBase(datos, commonOptions) {
    const ctx = document.getElementById('chartBase');
    if (!ctx) return;

    if (charts.chartBase) {
        charts.chartBase.destroy();
    }

    // Traducir los meses a español
    const mesesTraducidos = traducirMeses(datos.MesesBase || []);

    charts.chartBase = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: mesesTraducidos,
            datasets: [
                {
                    label: 'Horas',
                    data: datos.TiempoBase || [],
                    backgroundColor: 'rgba(54, 162, 235, 0.7)',
                    borderColor: 'rgba(54, 162, 235, 1)',
                    borderWidth: 1
                }
            ]
        },
        options: commonOptions
    });
}

// Función para inicializar el gráfico de Inbalnor
function inicializarGraficoInbalnor(datos, commonOptions) {
    const ctx = document.getElementById('chartInbalnor');
    if (!ctx) return;

    if (charts.chartInbalnor) {
        charts.chartInbalnor.destroy();
    }

    // Traducir los días a español si contienen nombres de meses
    const diasTraducidos = traducirMeses(datos.DiasInbalnor || []);

    charts.chartInbalnor = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: diasTraducidos,
            datasets: [
                {
                    label: 'Espera descarga',
                    data: datos.EsperaDescargaInbalnor || [],
                    backgroundColor: 'rgba(255, 99, 132, 0.7)',
                    borderColor: 'rgba(255, 99, 132, 1)',
                    borderWidth: 1
                },
                {
                    label: 'Descarga',
                    data: datos.DescargaInbalnor || [],
                    backgroundColor: 'rgba(255, 159, 64, 0.7)',
                    borderColor: 'rgba(255, 159, 64, 1)',
                    borderWidth: 1
                }
            ]
        },
        options: commonOptions
    });
}

// Función para inicializar el gráfico de Jave
function inicializarGraficoJave(datos, commonOptions) {
    const ctx = document.getElementById('chartJave');
    if (!ctx) return;

    if (charts.chartJave) {
        charts.chartJave.destroy();
    }

    // Traducir los días a español si contienen nombres de meses
    const diasTraducidos = traducirMeses(datos.DiasJave || []);

    charts.chartJave = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: diasTraducidos,
            datasets: [
                {
                    label: 'Espera descarga',
                    data: datos.EsperaDescargaJave || [],
                    backgroundColor: 'rgba(255, 99, 132, 0.7)',
                    borderColor: 'rgba(255, 99, 132, 1)',
                    borderWidth: 1
                },
                {
                    label: 'Descarga',
                    data: datos.DescargaJave || [],
                    backgroundColor: 'rgba(255, 159, 64, 0.7)',
                    borderColor: 'rgba(255, 159, 64, 1)',
                    borderWidth: 1
                }
            ]
        },
        options: commonOptions
    });
}

// Función para inicializar el gráfico de Espera Depsa
function inicializarGraficoEsperaDepsa(datos, commonOptions) {
    const ctx = document.getElementById('chartEsperaDepsa');
    if (!ctx) return;

    if (charts.chartEsperaDepsa) {
        charts.chartEsperaDepsa.destroy();
    }

    // Procesar y agrupar los datos por día para calcular promedios
    const diasOriginales = datos.DiasEsperaDepsa || [];
    const valoresOriginales = datos.EsperaDepsa || [];

    // Objeto para agrupar valores por día
    const valoresPorDia = {};
    const diasUnicos = [];

    // Agrupar todos los valores por día
    for (let i = 0; i < diasOriginales.length; i++) {
        const dia = diasOriginales[i];
        if (!valoresPorDia[dia]) {
            valoresPorDia[dia] = [];
            diasUnicos.push(dia);
        }
        // Solo agregar valores válidos
        if (valoresOriginales[i] !== undefined && valoresOriginales[i] !== null) {
            valoresPorDia[dia].push(valoresOriginales[i]);
        }
    }

    // Calcular el promedio para cada día
    const diasPromedio = [];
    const valoresPromedio = [];

    // Ordenar los días numéricamente
    diasUnicos.sort((a, b) => parseInt(a) - parseInt(b));

    // Calcular promedio para cada día
    diasUnicos.forEach(dia => {
        // Solo procesar días con datos
        if (valoresPorDia[dia].length > 0) {
            // Calcular promedio
            const suma = valoresPorDia[dia].reduce((a, b) => a + b, 0);
            const promedio = suma / valoresPorDia[dia].length;

            // Guardar día y promedio
            diasPromedio.push(dia);
            valoresPromedio.push(promedio.toFixed(3)); // Mantener 3 decimales
        }
    });

    // Calcular el promedio total para mostrarlo
    let promedioTotal = 0;
    if (valoresPromedio.length > 0) {
        const sumaTotal = valoresPromedio.reduce((a, b) => a + parseFloat(b), 0);
        promedioTotal = (sumaTotal / valoresPromedio.length).toFixed(3);

        // Actualizar el literal con el promedio total si existe
        const totalPromElement = document.getElementById('litTotalPromDepsa');
        if (totalPromElement) {
            totalPromElement.innerText = promedioTotal;
        }
    }

    // Opciones personalizadas para el gráfico estilo Power BI
    const powerBIOptions = JSON.parse(JSON.stringify(commonOptions));

    // Configurar tooltip personalizado
    powerBIOptions.plugins.tooltip = {
        callbacks: {
            label: function (context) {
                return `Horas: ${parseFloat(context.parsed.y).toFixed(3)}`;
            }
        }
    };

    // Crear el gráfico con el estilo Power BI
    charts.chartEsperaDepsa = new Chart(ctx, {
        type: 'line',
        data: {
            labels: diasPromedio,
            datasets: [
                {
                    label: 'Horas',
                    data: valoresPromedio,
                    backgroundColor: 'rgba(54, 162, 235, 0.3)', // Color azul semitransparente para el área
                    borderColor: 'rgba(54, 162, 235, 1)',
                    borderWidth: 2,
                    fill: true, // Activar relleno
                    tension: 0.4, // Suavizar la línea
                    pointRadius: 4, // Puntos más visibles
                    pointBackgroundColor: 'rgba(54, 162, 235, 1)'
                }
            ]
        },
        options: powerBIOptions
    });
}

// Inicializaciones para los demás gráficos (similar a las anteriores)
function inicializarGraficoEsperaComplex(datos, commonOptions) {
    const ctx = document.getElementById('chartEsperaComplex');
    if (!ctx) return;

    if (charts.chartEsperaComplex) {
        charts.chartEsperaComplex.destroy();
    }

    // Procesar y agrupar los datos por día para calcular promedios
    const diasOriginales = datos.DiasEsperaComplex || [];
    const valoresOriginales = datos.EsperaComplex || [];

    // Objeto para agrupar valores por día
    const valoresPorDia = {};
    const diasUnicos = [];

    // Agrupar todos los valores por día
    for (let i = 0; i < diasOriginales.length; i++) {
        const dia = diasOriginales[i];
        if (!valoresPorDia[dia]) {
            valoresPorDia[dia] = [];
            diasUnicos.push(dia);
        }
        // Solo agregar valores válidos
        if (valoresOriginales[i] !== undefined && valoresOriginales[i] !== null) {
            valoresPorDia[dia].push(valoresOriginales[i]);
        }
    }

    // Calcular el promedio para cada día
    const diasPromedio = [];
    const valoresPromedio = [];

    // Ordenar los días numéricamente
    diasUnicos.sort((a, b) => parseInt(a) - parseInt(b));

    // Calcular promedio para cada día
    diasUnicos.forEach(dia => {
        // Solo procesar días con datos
        if (valoresPorDia[dia].length > 0) {
            // Calcular promedio
            const suma = valoresPorDia[dia].reduce((a, b) => a + b, 0);
            const promedio = suma / valoresPorDia[dia].length;

            // Guardar día y promedio
            diasPromedio.push(dia);
            valoresPromedio.push(promedio.toFixed(3)); // Mantener 3 decimales
        }
    });

    // Calcular el promedio total para mostrarlo
    let promedioTotal = 0;
    if (valoresPromedio.length > 0) {
        const sumaTotal = valoresPromedio.reduce((a, b) => a + parseFloat(b), 0);
        promedioTotal = (sumaTotal / valoresPromedio.length).toFixed(3);

        // Actualizar el literal con el promedio total si existe
        const totalPromElement = document.getElementById('litTotalPromComplex');
        if (totalPromElement) {
            totalPromElement.innerText = promedioTotal;
        }
    }

    // Opciones personalizadas para el gráfico estilo Power BI
    const powerBIOptions = JSON.parse(JSON.stringify(commonOptions));

    // Configurar tooltip personalizado
    powerBIOptions.plugins.tooltip = {
        callbacks: {
            label: function (context) {
                return `Horas: ${parseFloat(context.parsed.y).toFixed(3)}`;
            }
        }
    };

    // Crear el gráfico con el estilo Power BI
    charts.chartEsperaComplex = new Chart(ctx, {
        type: 'line',
        data: {
            labels: diasPromedio,
            datasets: [
                {
                    label: 'Horas',
                    data: valoresPromedio,
                    backgroundColor: 'rgba(54, 162, 235, 0.3)', // Color azul semitransparente para el área
                    borderColor: 'rgba(54, 162, 235, 1)',
                    borderWidth: 2,
                    fill: true, // Activar relleno
                    tension: 0.4, // Suavizar la línea
                    pointRadius: 4, // Puntos más visibles
                    pointBackgroundColor: 'rgba(54, 162, 235, 1)'
                }
            ]
        },
        options: powerBIOptions
    });
}

function inicializarGraficoTiempoDepsa(datos, commonOptions) {
    const ctx = document.getElementById('chartTiempoDepsa');
    if (!ctx) return;

    if (charts.chartTiempoDepsa) {
        charts.chartTiempoDepsa.destroy();
    }

    // Procesar y agrupar los datos por día para calcular promedios
    const diasOriginales = datos.DiasTiempoDepsa || [];
    const valoresOriginales = datos.TiempoDepsa || [];

    // Objeto para agrupar valores por día
    const valoresPorDia = {};
    const diasUnicos = [];

    // Agrupar todos los valores por día
    for (let i = 0; i < diasOriginales.length; i++) {
        const dia = diasOriginales[i];
        if (!valoresPorDia[dia]) {
            valoresPorDia[dia] = [];
            diasUnicos.push(dia);
        }
        // Solo agregar valores válidos
        if (valoresOriginales[i] !== undefined && valoresOriginales[i] !== null) {
            valoresPorDia[dia].push(valoresOriginales[i]);
        }
    }

    // Calcular el promedio para cada día
    const diasPromedio = [];
    const valoresPromedio = [];

    // Ordenar los días numéricamente
    diasUnicos.sort((a, b) => parseInt(a) - parseInt(b));

    // Calcular promedio para cada día
    diasUnicos.forEach(dia => {
        // Solo procesar días con datos
        if (valoresPorDia[dia] && valoresPorDia[dia].length > 0) {
            // Calcular promedio
            const suma = valoresPorDia[dia].reduce((a, b) => a + b, 0);
            const promedio = suma / valoresPorDia[dia].length;

            // Guardar día y promedio
            diasPromedio.push(dia);
            valoresPromedio.push(promedio.toFixed(1)); // Redondear a 1 decimal
        }
    });

    // Calcular el promedio total para mostrarlo
    let promedioTotal = 0;
    if (valoresPromedio.length > 0) {
        const sumaTotal = valoresPromedio.reduce((a, b) => a + parseFloat(b), 0);
        promedioTotal = (sumaTotal / valoresPromedio.length).toFixed(2);

        // Actualizar el literal con el promedio total si existe
        const totalPromElement = document.getElementById('litTotalPromDepsa');
        if (totalPromElement) {
            totalPromElement.innerText = promedioTotal;
        }
    }

    // Opciones personalizadas para este gráfico
    const chartOptions = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: {
                position: 'right',
                labels: { color: '#fff', font: { size: 12 } }
            },
            tooltip: {
                callbacks: {
                    label: function (context) {
                        return `Horas: ${parseFloat(context.parsed.y).toFixed(1)}`;
                    }
                }
            },
            datalabels: {
                display: false // Sin etiquetas en los puntos
            }
        },
        scales: {
            y: {
                beginAtZero: true,
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' }
            },
            x: {
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' }
            }
        }
    };

    // Crear el gráfico con el estilo Power BI
    charts.chartTiempoDepsa = new Chart(ctx, {
        type: 'line',
        data: {
            labels: diasPromedio,
            datasets: [
                {
                    label: 'Horas',
                    data: valoresPromedio,
                    backgroundColor: 'rgba(255, 99, 132, 0.3)', // Color rosa/rojo semitransparente para el área
                    borderColor: 'rgba(255, 99, 132, 1)', // Color rosa/rojo para la línea
                    borderWidth: 2,
                    fill: true, // Activar relleno
                    tension: 0.4, // Suavizar la línea
                    pointRadius: 4, // Puntos más visibles
                    pointBackgroundColor: 'rgba(255, 99, 132, 1)', // Color rosa/rojo para los puntos
                    pointBorderColor: 'rgba(255, 99, 132, 1)'
                }
            ]
        },
        options: chartOptions
    });

    // Log para debug
    console.log("Gráfico TIEMPO PROMEDIO EN DEPSA inicializado con éxito");
    console.log("Días procesados:", diasPromedio.length);
    console.log("Valores promedio:", valoresPromedio);
}

function inicializarGraficoTiempoComplex(datos, commonOptions) {
    const ctx = document.getElementById('chartTiempoComplex');
    if (!ctx) return;

    if (charts.chartTiempoComplex) {
        charts.chartTiempoComplex.destroy();
    }

    // Traducir los días a español si contienen nombres de meses
    const diasTraducidos = traducirMeses(datos.DiasTiempoComplex || []);

    charts.chartTiempoComplex = new Chart(ctx, {
        type: 'line',
        data: {
            labels: diasTraducidos,
            datasets: [
                {
                    label: 'Horas',
                    data: datos.TiempoComplex || [],
                    backgroundColor: 'rgba(255, 99, 132, 0.2)',
                    borderColor: 'rgba(255, 99, 132, 1)',
                    borderWidth: 2,
                    fill: true,
                    tension: 0.4
                }
            ]
        },
        options: commonOptions
    });
}

function inicializarGraficoTiempoCebaf(datos, commonOptions) {
    const ctx = document.getElementById('chartTiempoCebaf');
    if (!ctx) return;

    if (charts.chartTiempoCebaf) {
        charts.chartTiempoCebaf.destroy();
    }

    // Primero procesamos los datos para agrupar por día y calcular promedios
    const diasOriginales = datos.DiasCebaf || [];
    const valoresOriginales = datos.TiempoCebaf || [];

    // Crear un objeto para agrupar los valores por día
    const valoresPorDia = {};
    const diasUnicos = [];

    // Agrupar valores por día
    for (let i = 0; i < diasOriginales.length; i++) {
        const dia = diasOriginales[i];
        if (!valoresPorDia[dia]) {
            valoresPorDia[dia] = [];
            diasUnicos.push(dia);
        }
        // Solo agregar valores válidos
        if (valoresOriginales[i] !== undefined && valoresOriginales[i] !== null) {
            valoresPorDia[dia].push(valoresOriginales[i]);
        }
    }

    // Calcular promedios para cada día
    const diasPromedio = [];
    const valoresPromedio = [];
    const mesesTexto = {};

    diasUnicos.sort((a, b) => parseInt(a) - parseInt(b)); // Ordenar días numéricamente

    diasUnicos.forEach(dia => {
        // Solo procesar días con datos
        if (valoresPorDia[dia].length > 0) {
            // Calcular promedio
            const suma = valoresPorDia[dia].reduce((a, b) => a + b, 0);
            const promedio = suma / valoresPorDia[dia].length;

            // Guardar día y promedio
            diasPromedio.push(dia);
            valoresPromedio.push(promedio.toFixed(1)); // Redondear a 1 decimal

            // Detectar cambios de mes (para etiquetas)
            // Esto es aproximado, puedes ajustarlo según tus datos
            if (parseInt(dia) === 1) {
                // Asumimos que día 1 es inicio de mes
                mesesTexto[dia] = 'enero'; // Ajusta según el mes actual
            }
        }
    });

    // Opciones personalizadas para el gráfico estilo Power BI
    const powerBIOptions = JSON.parse(JSON.stringify(commonOptions));

    // Sobrescribir algunas opciones específicas
    powerBIOptions.plugins.tooltip = {
        callbacks: {
            label: function (context) {
                return `Minutos: ${context.parsed.y}`;
            }
        }
    };

    // Configuración para que sea como Power BI
    charts.chartTiempoCebaf = new Chart(ctx, {
        type: 'line', // Podemos usar line en lugar de area porque Chart.js no tiene tipo 'area' nativo
        data: {
            labels: diasPromedio,
            datasets: [
                {
                    label: 'Minutos',
                    data: valoresPromedio,
                    backgroundColor: 'rgba(153, 102, 255, 0.5)', // Más opaco para el relleno
                    borderColor: 'rgba(153, 102, 255, 1)',
                    borderWidth: 2,
                    fill: true, // Esto hace que se rellene debajo de la línea
                    tension: 0.4, // Para curvas suaves
                    pointRadius: 4, // Puntos más grandes
                    pointBackgroundColor: 'rgba(153, 102, 255, 1)'
                }
            ]
        },
        options: powerBIOptions
    });

    // Agregar la etiqueta de valor promedio total en la esquina
    if (valoresPromedio.length > 0) {
        const suma = valoresPromedio.reduce((a, b) => a + parseFloat(b), 0);
        const promedioTotal = (suma / valoresPromedio.length).toFixed(2);

        // Esto se puede hacer agregando un div con position absolute, pero requiere HTML adicional
        console.log("Promedio total CEBAF:", promedioTotal);

        // Agregar etiqueta con total promedio si existe un elemento para ello
        const totalPromElement = document.getElementById('litTotalPromCebaf');
        if (totalPromElement) {
            totalPromElement.innerText = promedioTotal;
        }
    }
}

// Funciones para la pestaña de Tiempos Adicionales
function inicializarGraficoTiempoTCI(datos, commonOptions) {
    const ctx = document.getElementById('chartTiempoTCI');
    if (!ctx) {
        console.error("No se encontró el elemento chartTiempoTCI");
        return;
    }

    if (charts.chartTiempoTCI) {
        charts.chartTiempoTCI.destroy();
    }

    console.log("Inicializando gráfico TCI con datos:", datos);

    // Verificar que los datos existan
    if (!datos.DiasTiempoTCI || !datos.TiempoTCI) {
        console.error("Faltan datos para el gráfico TCI");
        return;
    }

    // Procesar y agrupar los datos por día para calcular promedios
    const diasOriginales = datos.DiasTiempoTCI || [];
    const valoresOriginales = datos.TiempoTCI || [];

    console.log("Días TCI:", diasOriginales);
    console.log("Valores TCI:", valoresOriginales);

    // Objeto para agrupar valores por día
    const valoresPorDia = {};
    const diasUnicos = [];

    // Agrupar todos los valores por día
    for (let i = 0; i < diasOriginales.length; i++) {
        const dia = diasOriginales[i];
        if (!dia) continue; // Saltar valores nulos o undefined

        if (!valoresPorDia[dia]) {
            valoresPorDia[dia] = [];
            diasUnicos.push(dia);
        }

        // Solo agregar valores válidos
        if (valoresOriginales[i] !== undefined && valoresOriginales[i] !== null) {
            valoresPorDia[dia].push(parseFloat(valoresOriginales[i]));
        }
    }

    console.log("Valores agrupados por día:", valoresPorDia);

    // Calcular el promedio para cada día
    const diasPromedio = [];
    const valoresPromedio = [];

    // Ordenar los días numéricamente
    diasUnicos.sort((a, b) => parseInt(a) - parseInt(b));

    // Calcular promedio para cada día
    diasUnicos.forEach(dia => {
        // Solo procesar días con datos
        if (valoresPorDia[dia] && valoresPorDia[dia].length > 0) {
            // Calcular promedio
            const suma = valoresPorDia[dia].reduce((a, b) => a + b, 0);
            const promedio = suma / valoresPorDia[dia].length;

            // Guardar día y promedio
            diasPromedio.push(dia);
            valoresPromedio.push(promedio.toFixed(1)); // Redondear a 1 decimal
        }
    });

    console.log("Días procesados:", diasPromedio);
    console.log("Valores promedio:", valoresPromedio);

    // Calcular el promedio total para mostrarlo
    let promedioTotal = 0;
    if (valoresPromedio.length > 0) {
        const sumaTotal = valoresPromedio.reduce((a, b) => a + parseFloat(b), 0);
        promedioTotal = (sumaTotal / valoresPromedio.length).toFixed(1);

        // Actualizar el literal con el promedio total si existe
        const totalPromElement = document.getElementById('litTotalPromTCI');
        if (totalPromElement) {
            totalPromElement.innerText = promedioTotal;
        }
    }

    // Opciones personalizadas para este gráfico
    const chartOptions = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: {
                position: 'right',
                labels: { color: '#fff', font: { size: 12 } }
            },
            tooltip: {
                callbacks: {
                    label: function (context) {
                        return `Horas: ${parseFloat(context.parsed.y).toFixed(1)}`;
                    }
                }
            },
            datalabels: {
                display: true,
                color: '#fff',
                font: { weight: 'bold', size: 10 },
                formatter: function (value) {
                    return parseFloat(value).toFixed(1);
                },
                anchor: 'end',
                align: 'top',
                offset: 0
            }
        },
        scales: {
            y: {
                beginAtZero: true,
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' },
                // Ajustar escala para que coincida con el estilo Power BI
                suggestedMax: Math.max(...valoresPromedio.map(v => parseFloat(v))) * 1.2 || 1.0
            },
            x: {
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' }
            }
        }
    };

    try {
        // Crear el gráfico con el estilo Power BI
        charts.chartTiempoTCI = new Chart(ctx, {
            type: 'line',
            data: {
                labels: diasPromedio,
                datasets: [
                    {
                        label: 'Horas',
                        data: valoresPromedio,
                        backgroundColor: 'rgba(75, 192, 192, 0.3)', // Color turquesa/cian semitransparente para el área
                        borderColor: 'rgba(75, 192, 192, 1)', // Color turquesa/cian para la línea
                        borderWidth: 2,
                        fill: true, // Activar relleno
                        tension: 0.4, // Suavizar la línea
                        pointRadius: 5, // Puntos más visibles
                        pointBackgroundColor: 'rgba(75, 192, 192, 1)', // Color turquesa/cian para los puntos
                        pointBorderColor: 'rgba(75, 192, 192, 1)'
                    }
                ]
            },
            options: chartOptions
        });

        // Log para debug
        console.log("Gráfico TIEMPO PROMEDIO EN TCI inicializado con éxito");
    } catch (error) {
        console.error("Error al inicializar el gráfico TCI:", error);
    }
}

function inicializarGraficoTiempoPuyango(datos, commonOptions) {
    const ctx = document.getElementById('chartTiempoPuyango');
    if (!ctx) return;

    if (charts.chartTiempoPuyango) {
        charts.chartTiempoPuyango.destroy();
    }

    // Traducir los días a español si contienen nombres de meses
    const diasTraducidos = traducirMeses(datos.DiasTiempoPuyango || []);

    charts.chartTiempoPuyango = new Chart(ctx, {
        type: 'line',
        data: {
            labels: diasTraducidos,
            datasets: [
                {
                    label: 'Horas',
                    data: datos.TiempoPuyango || [],
                    backgroundColor: 'rgba(75, 192, 192, 0.2)',
                    borderColor: 'rgba(75, 192, 192, 1)',
                    borderWidth: 2,
                    fill: true,
                    tension: 0.4
                }
            ]
        },
        options: commonOptions
    });
}

function inicializarGraficoEsperaNacionalizacion(datos, commonOptions) {
    const ctx = document.getElementById('chartEsperaNacionalizacion');
    if (!ctx) {
        console.error("No se encontró el elemento chartEsperaNacionalizacion");
        return;
    }

    if (charts.chartEsperaNacionalizacion) {
        charts.chartEsperaNacionalizacion.destroy();
    }

    console.log("Inicializando gráfico Espera Nacionalización con datos:", datos);

    // Verificar que los datos existan
    if (!datos.DiasEsperaNacionalizacion || !datos.TiempoEsperaNacionalizacion) {
        console.error("Faltan datos para el gráfico Espera Nacionalización");
        return;
    }

    // Procesar y agrupar los datos por día para calcular promedios
    const diasOriginales = datos.DiasEsperaNacionalizacion || [];
    const valoresOriginales = datos.TiempoEsperaNacionalizacion || [];

    console.log("Días Espera Nacionalización:", diasOriginales);
    console.log("Valores Espera Nacionalización:", valoresOriginales);

    // Objeto para agrupar valores por día
    const valoresPorDia = {};
    const diasUnicos = [];

    // Agrupar todos los valores por día
    for (let i = 0; i < diasOriginales.length; i++) {
        const dia = diasOriginales[i];
        if (!dia) continue; // Saltar valores nulos o undefined

        if (!valoresPorDia[dia]) {
            valoresPorDia[dia] = [];
            diasUnicos.push(dia);
        }

        // Solo agregar valores válidos
        if (valoresOriginales[i] !== undefined && valoresOriginales[i] !== null) {
            valoresPorDia[dia].push(parseFloat(valoresOriginales[i]));
        }
    }

    console.log("Valores agrupados por día:", valoresPorDia);

    // Calcular el promedio para cada día
    const diasPromedio = [];
    const valoresPromedio = [];

    // Ordenar los días numéricamente
    diasUnicos.sort((a, b) => parseInt(a) - parseInt(b));

    // Calcular promedio para cada día
    diasUnicos.forEach(dia => {
        // Solo procesar días con datos
        if (valoresPorDia[dia] && valoresPorDia[dia].length > 0) {
            // Calcular promedio
            const suma = valoresPorDia[dia].reduce((a, b) => a + b, 0);
            const promedio = suma / valoresPorDia[dia].length;

            // Guardar día y promedio
            diasPromedio.push(dia);
            valoresPromedio.push(promedio.toFixed(1)); // Redondear a 1 decimal
        }
    });

    console.log("Días procesados:", diasPromedio);
    console.log("Valores promedio:", valoresPromedio);

    // Calcular el promedio total para mostrarlo
    let promedioTotal = 0;
    if (valoresPromedio.length > 0) {
        const sumaTotal = valoresPromedio.reduce((a, b) => a + parseFloat(b), 0);
        promedioTotal = (sumaTotal / valoresPromedio.length).toFixed(1);

        // Actualizar el literal con el promedio total si existe
        const totalPromElement = document.getElementById('litTotalPromNacionalizacion');
        if (totalPromElement) {
            totalPromElement.innerText = promedioTotal;
        }
    }

    // Opciones personalizadas para este gráfico
    const chartOptions = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: {
                position: 'right',
                labels: { color: '#fff', font: { size: 12 } }
            },
            tooltip: {
                callbacks: {
                    label: function (context) {
                        return `Horas: ${parseFloat(context.parsed.y).toFixed(1)}`;
                    }
                }
            },
            datalabels: {
                display: true,
                color: '#fff',
                font: { weight: 'bold', size: 10 },
                formatter: function (value) {
                    return parseFloat(value).toFixed(1);
                },
                anchor: 'end',
                align: 'top',
                offset: 0
            }
        },
        scales: {
            y: {
                beginAtZero: true,
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' },
                // Ajustar escala para que coincida con el estilo Power BI
                suggestedMax: Math.max(...valoresPromedio.map(v => parseFloat(v))) * 1.2 || 1.0
            },
            x: {
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' }
            }
        }
    };

    try {
        // Crear el gráfico con el estilo Power BI
        charts.chartEsperaNacionalizacion = new Chart(ctx, {
            type: 'line',
            data: {
                labels: diasPromedio,
                datasets: [
                    {
                        label: 'Horas',
                        data: valoresPromedio,
                        backgroundColor: 'rgba(255, 159, 64, 0.3)', // Color naranja semitransparente para el área
                        borderColor: 'rgba(255, 159, 64, 1)', // Color naranja para la línea
                        borderWidth: 2,
                        fill: true, // Activar relleno
                        tension: 0.4, // Suavizar la línea
                        pointRadius: 5, // Puntos más visibles
                        pointBackgroundColor: 'rgba(255, 159, 64, 1)', // Color naranja para los puntos
                        pointBorderColor: 'rgba(255, 159, 64, 1)'
                    }
                ]
            },
            options: chartOptions
        });

        // Log para debug
        console.log("Gráfico ESPERA DE NACIONALIZACIÓN inicializado con éxito");
    } catch (error) {
        console.error("Error al inicializar el gráfico Espera Nacionalización:", error);
    }
}

// Funciones para la pestaña de Bodegas y Distancias
function inicializarGraficoBodegaInbalnor(datos, commonOptions) {
    const ctx = document.getElementById('chartBodegaInbalnor');
    if (!ctx) {
        console.error("No se encontró el elemento chartBodegaInbalnor");
        return;
    }

    if (charts.chartBodegaInbalnor) {
        charts.chartBodegaInbalnor.destroy();
    }

    console.log("Inicializando gráfico Bodega a Inbalnor con datos:", datos);

    // Verificar que los datos existan
    if (!datos.DiasBodegaInbalnor || !datos.TiempoBodegaInbalnor) {
        console.error("Faltan datos para el gráfico Bodega a Inbalnor");
        return;
    }

    // Procesar datos
    const diasOriginales = datos.DiasBodegaInbalnor || [];
    const valoresOriginales = datos.TiempoBodegaInbalnor || [];

    // Objeto para agrupar valores por día
    const valoresPorDia = {};
    const diasUnicos = [];

    // Agrupar todos los valores por día
    for (let i = 0; i < diasOriginales.length; i++) {
        const dia = diasOriginales[i];
        if (!dia) continue;

        if (!valoresPorDia[dia]) {
            valoresPorDia[dia] = [];
            diasUnicos.push(dia);
        }

        if (valoresOriginales[i] !== undefined && valoresOriginales[i] !== null) {
            valoresPorDia[dia].push(parseFloat(valoresOriginales[i]));
        }
    }

    // Calcular promedios por día
    const diasPromedio = [];
    const valoresPromedio = [];

    // Ordenar días
    diasUnicos.sort((a, b) => parseInt(a) - parseInt(b));

    diasUnicos.forEach(dia => {
        if (valoresPorDia[dia] && valoresPorDia[dia].length > 0) {
            const suma = valoresPorDia[dia].reduce((a, b) => a + b, 0);
            const promedio = suma / valoresPorDia[dia].length;

            diasPromedio.push(dia);
            valoresPromedio.push(promedio.toFixed(1));
        }
    });

    // Calcular promedio total
    let promedioTotal = 0;
    if (valoresPromedio.length > 0) {
        const sumaTotal = valoresPromedio.reduce((a, b) => a + parseFloat(b), 0);
        promedioTotal = (sumaTotal / valoresPromedio.length).toFixed(1);

        const totalPromElement = document.getElementById('litTotalPromBodegaInbalnor');
        if (totalPromElement) {
            totalPromElement.innerText = promedioTotal;
        }
    }

    // Opciones del gráfico
    const chartOptions = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: {
                position: 'right',
                labels: { color: '#fff', font: { size: 12 } }
            },
            tooltip: {
                callbacks: {
                    label: function (context) {
                        return `Horas: ${parseFloat(context.parsed.y).toFixed(1)}`;
                    }
                }
            },
            datalabels: {
                display: true,
                color: '#fff',
                font: { weight: 'bold', size: 10 },
                formatter: function (value) {
                    return parseFloat(value).toFixed(1);
                },
                anchor: 'end',
                align: 'top',
                offset: 0
            }
        },
        scales: {
            y: {
                beginAtZero: true,
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' }
            },
            x: {
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' }
            }
        }
    };

    try {
        // Crear el gráfico
        charts.chartBodegaInbalnor = new Chart(ctx, {
            type: 'line',
            data: {
                labels: diasPromedio,
                datasets: [
                    {
                        label: 'Horas',
                        data: valoresPromedio,
                        backgroundColor: 'rgba(54, 162, 235, 0.3)',
                        borderColor: 'rgba(54, 162, 235, 1)',
                        borderWidth: 2,
                        fill: true,
                        tension: 0.4,
                        pointRadius: 5,
                        pointBackgroundColor: 'rgba(54, 162, 235, 1)',
                        pointBorderColor: 'rgba(54, 162, 235, 1)'
                    }
                ]
            },
            options: chartOptions
        });

        console.log("Gráfico TIEMPO PROMEDIO DE BODEGA NACIONAL A INBALNOR inicializado con éxito");
    } catch (error) {
        console.error("Error al inicializar el gráfico:", error);
    }
}

function inicializarGraficoBodegaJave(datos, commonOptions) {
    const ctx = document.getElementById('chartBodegaJave');
    if (!ctx) {
        console.error("No se encontró el elemento chartBodegaJave");
        return;
    }

    if (charts.chartBodegaJave) {
        charts.chartBodegaJave.destroy();
    }

    console.log("Inicializando gráfico Bodega a Jave con datos:", datos);

    // Verificar que los datos existan
    if (!datos.DiasBodegaJave || !datos.TiempoBodegaJave) {
        console.error("Faltan datos para el gráfico Bodega a Jave");
        return;
    }

    // Procesar datos
    const diasOriginales = datos.DiasBodegaJave || [];
    const valoresOriginales = datos.TiempoBodegaJave || [];

    // Objeto para agrupar valores por día
    const valoresPorDia = {};
    const diasUnicos = [];

    // Agrupar todos los valores por día
    for (let i = 0; i < diasOriginales.length; i++) {
        const dia = diasOriginales[i];
        if (!dia) continue;

        if (!valoresPorDia[dia]) {
            valoresPorDia[dia] = [];
            diasUnicos.push(dia);
        }

        if (valoresOriginales[i] !== undefined && valoresOriginales[i] !== null) {
            valoresPorDia[dia].push(parseFloat(valoresOriginales[i]));
        }
    }

    // Calcular promedios por día
    const diasPromedio = [];
    const valoresPromedio = [];

    // Ordenar días
    diasUnicos.sort((a, b) => parseInt(a) - parseInt(b));

    diasUnicos.forEach(dia => {
        if (valoresPorDia[dia] && valoresPorDia[dia].length > 0) {
            const suma = valoresPorDia[dia].reduce((a, b) => a + b, 0);
            const promedio = suma / valoresPorDia[dia].length;

            diasPromedio.push(dia);
            valoresPromedio.push(promedio.toFixed(1));
        }
    });

    // Calcular promedio total
    let promedioTotal = 0;
    if (valoresPromedio.length > 0) {
        const sumaTotal = valoresPromedio.reduce((a, b) => a + parseFloat(b), 0);
        promedioTotal = (sumaTotal / valoresPromedio.length).toFixed(1);

        const totalPromElement = document.getElementById('litTotalPromBodegaJave');
        if (totalPromElement) {
            totalPromElement.innerText = promedioTotal;
        }
    }

    // Opciones del gráfico
    const chartOptions = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: {
                position: 'right',
                labels: { color: '#fff', font: { size: 12 } }
            },
            tooltip: {
                callbacks: {
                    label: function (context) {
                        return `Horas: ${parseFloat(context.parsed.y).toFixed(1)}`;
                    }
                }
            },
            datalabels: {
                display: true,
                color: '#fff',
                font: { weight: 'bold', size: 10 },
                formatter: function (value) {
                    return parseFloat(value).toFixed(1);
                },
                anchor: 'end',
                align: 'top',
                offset: 0
            }
        },
        scales: {
            y: {
                beginAtZero: true,
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' }
            },
            x: {
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' }
            }
        }
    };

    try {
        // Crear el gráfico
        charts.chartBodegaJave = new Chart(ctx, {
            type: 'line',
            data: {
                labels: diasPromedio,
                datasets: [
                    {
                        label: 'Horas',
                        data: valoresPromedio,
                        backgroundColor: 'rgba(54, 162, 235, 0.3)', // Mismo color azul que el anterior
                        borderColor: 'rgba(54, 162, 235, 1)',
                        borderWidth: 2,
                        fill: true,
                        tension: 0.4,
                        pointRadius: 5,
                        pointBackgroundColor: 'rgba(54, 162, 235, 1)',
                        pointBorderColor: 'rgba(54, 162, 235, 1)'
                    }
                ]
            },
            options: chartOptions
        });

        console.log("Gráfico TIEMPO PROMEDIO DE BODEGA NACIONAL A JAVE inicializado con éxito");
    } catch (error) {
        console.error("Error al inicializar el gráfico:", error);
    }
}

function inicializarGraficoEcuatorianaInbalnor(datos, commonOptions) {
    const ctx = document.getElementById('chartEcuatorianaInbalnor');
    if (!ctx) {
        console.error("No se encontró el elemento chartEcuatorianaInbalnor");
        return;
    }

    if (charts.chartEcuatorianaInbalnor) {
        charts.chartEcuatorianaInbalnor.destroy();
    }

    console.log("Inicializando gráfico Ecuatoriana a Inbalnor con datos:", datos);

    // Verificar que los datos existan
    if (!datos.DiasEcuatorianaInbalnor || !datos.TiempoEcuatorianaInbalnor) {
        console.error("Faltan datos para el gráfico Ecuatoriana a Inbalnor");
        return;
    }

    // Procesar datos
    const diasOriginales = datos.DiasEcuatorianaInbalnor || [];
    const valoresOriginales = datos.TiempoEcuatorianaInbalnor || [];

    // Objeto para agrupar valores por día
    const valoresPorDia = {};
    const diasUnicos = [];

    // Agrupar todos los valores por día
    for (let i = 0; i < diasOriginales.length; i++) {
        const dia = diasOriginales[i];
        if (!dia) continue;

        if (!valoresPorDia[dia]) {
            valoresPorDia[dia] = [];
            diasUnicos.push(dia);
        }

        if (valoresOriginales[i] !== undefined && valoresOriginales[i] !== null) {
            valoresPorDia[dia].push(parseFloat(valoresOriginales[i]));
        }
    }

    // Calcular promedios por día
    const diasPromedio = [];
    const valoresPromedio = [];

    // Ordenar días
    diasUnicos.sort((a, b) => parseInt(a) - parseInt(b));

    diasUnicos.forEach(dia => {
        if (valoresPorDia[dia] && valoresPorDia[dia].length > 0) {
            const suma = valoresPorDia[dia].reduce((a, b) => a + b, 0);
            const promedio = suma / valoresPorDia[dia].length;

            diasPromedio.push(dia);
            valoresPromedio.push(promedio.toFixed(1));
        }
    });

    // Calcular promedio total
    let promedioTotal = 0;
    if (valoresPromedio.length > 0) {
        const sumaTotal = valoresPromedio.reduce((a, b) => a + parseFloat(b), 0);
        promedioTotal = (sumaTotal / valoresPromedio.length).toFixed(1);

        const totalPromElement = document.getElementById('litTotalPromEcuatorianaInbalnor');
        if (totalPromElement) {
            totalPromElement.innerText = promedioTotal;
        }
    }

    // Opciones del gráfico
    const chartOptions = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: {
                position: 'right',
                labels: { color: '#fff', font: { size: 12 } }
            },
            tooltip: {
                callbacks: {
                    label: function (context) {
                        return `Horas: ${parseFloat(context.parsed.y).toFixed(1)}`;
                    }
                }
            },
            datalabels: {
                display: true,
                color: '#fff',
                font: { weight: 'bold', size: 10 },
                formatter: function (value) {
                    return parseFloat(value).toFixed(1);
                },
                anchor: 'end',
                align: 'top',
                offset: 0
            }
        },
        scales: {
            y: {
                beginAtZero: true,
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' }
            },
            x: {
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' }
            }
        }
    };

    try {
        // Crear el gráfico
        charts.chartEcuatorianaInbalnor = new Chart(ctx, {
            type: 'line',
            data: {
                labels: diasPromedio,
                datasets: [
                    {
                        label: 'Horas',
                        data: valoresPromedio,
                        backgroundColor: 'rgba(255, 159, 64, 0.3)', // Color naranja para diferenciar
                        borderColor: 'rgba(255, 159, 64, 1)',
                        borderWidth: 2,
                        fill: true,
                        tension: 0.4,
                        pointRadius: 5,
                        pointBackgroundColor: 'rgba(255, 159, 64, 1)',
                        pointBorderColor: 'rgba(255, 159, 64, 1)'
                    }
                ]
            },
            options: chartOptions
        });

        console.log("Gráfico TIEMPO PROMEDIO DE BODEGA ECUATORIANA A BODEGA INBALNOR inicializado con éxito");
    } catch (error) {
        console.error("Error al inicializar el gráfico:", error);
    }
}

function inicializarGraficoEcuatorianaJave(datos, commonOptions) {
    const ctx = document.getElementById('chartEcuatorianaJave');
    if (!ctx) {
        console.error("No se encontró el elemento chartEcuatorianaJave");
        return;
    }

    if (charts.chartEcuatorianaJave) {
        charts.chartEcuatorianaJave.destroy();
    }

    console.log("Inicializando gráfico Ecuatoriana a Jave con datos:", datos);

    // Verificar que los datos existan
    if (!datos.DiasEcuatorianaJave || !datos.TiempoEcuatorianaJave) {
        console.error("Faltan datos para el gráfico Ecuatoriana a Jave");
        return;
    }

    // Procesar datos
    const diasOriginales = datos.DiasEcuatorianaJave || [];
    const valoresOriginales = datos.TiempoEcuatorianaJave || [];

    // Objeto para agrupar valores por día
    const valoresPorDia = {};
    const diasUnicos = [];

    // Agrupar todos los valores por día
    for (let i = 0; i < diasOriginales.length; i++) {
        const dia = diasOriginales[i];
        if (!dia) continue;

        if (!valoresPorDia[dia]) {
            valoresPorDia[dia] = [];
            diasUnicos.push(dia);
        }

        if (valoresOriginales[i] !== undefined && valoresOriginales[i] !== null) {
            valoresPorDia[dia].push(parseFloat(valoresOriginales[i]));
        }
    }

    // Calcular promedios por día
    const diasPromedio = [];
    const valoresPromedio = [];

    // Ordenar días
    diasUnicos.sort((a, b) => parseInt(a) - parseInt(b));

    diasUnicos.forEach(dia => {
        if (valoresPorDia[dia] && valoresPorDia[dia].length > 0) {
            const suma = valoresPorDia[dia].reduce((a, b) => a + b, 0);
            const promedio = suma / valoresPorDia[dia].length;

            diasPromedio.push(dia);
            valoresPromedio.push(promedio.toFixed(1));
        }
    });

    // Calcular promedio total
    let promedioTotal = 0;
    if (valoresPromedio.length > 0) {
        const sumaTotal = valoresPromedio.reduce((a, b) => a + parseFloat(b), 0);
        promedioTotal = (sumaTotal / valoresPromedio.length).toFixed(1);

        const totalPromElement = document.getElementById('litTotalPromEcuatorianaJave');
        if (totalPromElement) {
            totalPromElement.innerText = promedioTotal;
        }
    }

    // Opciones del gráfico
    const chartOptions = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: {
                position: 'right',
                labels: { color: '#fff', font: { size: 12 } }
            },
            tooltip: {
                callbacks: {
                    label: function (context) {
                        return `Horas: ${parseFloat(context.parsed.y).toFixed(1)}`;
                    }
                }
            },
            datalabels: {
                display: true,
                color: '#fff',
                font: { weight: 'bold', size: 10 },
                formatter: function (value) {
                    return parseFloat(value).toFixed(1);
                },
                anchor: 'end',
                align: 'top',
                offset: 0
            }
        },
        scales: {
            y: {
                beginAtZero: true,
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' }
            },
            x: {
                grid: { color: 'rgba(255, 255, 255, 0.1)' },
                ticks: { color: '#fff' }
            }
        }
    };

    try {
        // Crear el gráfico
        charts.chartEcuatorianaJave = new Chart(ctx, {
            type: 'line',
            data: {
                labels: diasPromedio,
                datasets: [
                    {
                        label: 'Horas',
                        data: valoresPromedio,
                        backgroundColor: 'rgba(255, 159, 64, 0.3)', // Color naranja igual que el anterior
                        borderColor: 'rgba(255, 159, 64, 1)',
                        borderWidth: 2,
                        fill: true,
                        tension: 0.4,
                        pointRadius: 5,
                        pointBackgroundColor: 'rgba(255, 159, 64, 1)',
                        pointBorderColor: 'rgba(255, 159, 64, 1)'
                    }
                ]
            },
            options: chartOptions
        });

        console.log("Gráfico TIEMPO PROMEDIO DE BODEGA ECUATORIANA A BODEGA JAVE inicializado con éxito");
    } catch (error) {
        console.error("Error al inicializar el gráfico:", error);
    }
}

/**
 * Scripts.js - Sistema de Gestión Vehicular (SGV)
 * Scripts mejorados para el correcto funcionamiento de modales y reportes
 */

// Ejecutar cuando el documento esté listo
$(document).ready(function () {
    console.log("Inicializando scripts del SGV...");

    // ===== INICIALIZACIONES GENERALES =====

    // Inicializar tooltips de Bootstrap
    $('[data-toggle="tooltip"]').tooltip();

    // Inicializar popovers de Bootstrap
    $('[data-toggle="popover"]').popover();

    // Inicializar datepickers
    if ($.fn.datepicker) {
        $('.datepicker').datepicker({
            format: 'dd/mm/yyyy',
            autoclose: true,
            language: 'es'
        });
    }

    // ===== FIXES PARA PROBLEMAS DE TRANSPARENCIA =====

    // Fix para encabezados de tabla transparentes
    function fixTableHeaders() {
        $(".table thead th").css({
            'background-color': '#0275d8',
            'color': 'white',
            'opacity': '1'
        });
    }

    // Fix para tarjetas de indicadores transparentes
    function fixIndicatorCards() {
        $(".card.shadow-sm, .card-body, .card-title, .card-body p, .card-body h4").css('opacity', '1');

        // Arreglar específicamente los gradientes
        $(".bg-gradient-primary").css({
            'background': 'linear-gradient(to right, #0062cc, #0275d8)',
            'opacity': '1'
        });

        $(".bg-gradient-danger").css({
            'background': 'linear-gradient(to right, #c82333, #dc3545)',
            'opacity': '1'
        });

        $(".bg-gradient-success").css({
            'background': 'linear-gradient(to right, #218838, #28a745)',
            'opacity': '1'
        });

        $(".bg-gradient-info").css({
            'background': 'linear-gradient(to right, #138496, #17a2b8)',
            'opacity': '1'
        });
    }

    // Aplicar fixes inmediatamente después de cargar la página
    fixTableHeaders();
    fixIndicatorCards();

    // ===== MEJORAS PARA MODALES =====

    // Prevenir que las modales se cierren en casos específicos
    $.fn.modal.Constructor.prototype.enforceFocus = function () { };

    // Configurar las modales al mostrarlas
    $('.modal').on('show.bs.modal', function () {
        console.log("Mostrando modal: " + $(this).attr('id'));

        // Asegurar que la modal esté visible y con los estilos correctos
        $(this).css('opacity', '1');
        $(this).find('.modal-content').css('opacity', '1');
        $(this).find('.modal-header, .modal-body, .modal-footer').css('opacity', '1');

        // Mejorar apariencia de la modal
        setTimeout(function () {
            $('.modal-backdrop').not('.modal-stack').css('z-index', 1040);
            $('.modal:visible').css('z-index', 1050);

            // Volver a aplicar fixes dentro de la modal
            fixTableHeaders();
            fixIndicatorCards();
        }, 10);
    });

    // Limpiar cuando se cierra la modal
    $('.modal').on('hidden.bs.modal', function () {
        console.log("Modal cerrada: " + $(this).attr('id'));
        // Eliminar backdrops sobrantes
        if ($('.modal:visible').length === 0) {
            $('.modal-backdrop').remove();
        }
    });

    // ===== MEJORAS ESPECÍFICAS PARA EL REPORTE MODAL =====

    // Específico para la modal de resultados de reporte
    $('#modalResultados').on('shown.bs.modal', function () {
        console.log("Modal de resultados mostrada");

        // Asegurarse que el encabezado tenga opacidad completa y color correcto
        $(this).find('.modal-header').css({
            'background-color': '#0275d8',
            'color': 'white',
            'opacity': '1'
        });

        // Asegurarse que las tablas dentro de la modal tengan los estilos correctos
        $(this).find('.table thead th').css({
            'background-color': '#0275d8',
            'color': 'white',
            'opacity': '1',
            'font-weight': 'bold',
            'text-align': 'center'
        });

        // Revisar indicadores
        fixIndicatorCards();
    });

    // Mejorar la funcionalidad de impresión
    $("#btnImprimirResultados").click(function () {
        console.log("Generando vista de impresión");

        var printContents = $("#pnlResultados").html();
        var originalContents = $("body").html();

        // Crear una ventana de impresión optimizada
        var printWindow = window.open('', '', 'height=600,width=800');
        printWindow.document.write('<html><head><title>Reporte SGV</title>');

        // Incluir CSS esenciales
        printWindow.document.write('<link rel="stylesheet" href="https://stackpath.bootstrapcdn.com/bootstrap/4.5.2/css/bootstrap.min.css">');
        printWindow.document.write('<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/5.15.1/css/all.min.css">');

        // Incluir estilos específicos para impresión
        printWindow.document.write('<style>');
        printWindow.document.write(`
            body { 
                padding: 20px; 
                font-family: Arial, sans-serif; 
            }
            .table { 
                width: 100%; 
                border-collapse: collapse; 
            }
            .table thead th { 
                background-color: #0275d8 !important; 
                color: white !important; 
                border: 1px solid #0269c2;
                padding: 8px;
                text-align: center;
            }
            .table tbody td {
                border: 1px solid #dee2e6;
                padding: 8px;
            }
            .card { 
                margin-bottom: 15px; 
                border-radius: 8px; 
                overflow: hidden; 
                border: 1px solid #dee2e6;
            }
            .bg-gradient-primary { 
                background: linear-gradient(to right, #0062cc, #0275d8) !important; 
                color: white !important;
            }
            .bg-gradient-danger { 
                background: linear-gradient(to right, #c82333, #dc3545) !important; 
                color: white !important;
            }
            .bg-gradient-success { 
                background: linear-gradient(to right, #218838, #28a745) !important; 
                color: white !important;
            }
            .bg-gradient-info { 
                background: linear-gradient(to right, #138496, #17a2b8) !important; 
                color: white !important;
            }
            .text-white { 
                color: white !important; 
            }
            @media print {
                .card { 
                    break-inside: avoid; 
                }
                .table { 
                    break-inside: auto; 
                }
                .table thead { 
                    display: table-header-group; 
                }
                .table tbody { 
                    display: table-row-group; 
                }
                .table-responsive { 
                    overflow: visible !important; 
                    max-height: none !important; 
                }
                /* Asegurarse que los colores se impriman */
                .bg-gradient-primary, 
                .bg-gradient-danger, 
                .bg-gradient-success, 
                .bg-gradient-info, 
                .table thead th {
                    -webkit-print-color-adjust: exact !important;
                    print-color-adjust: exact !important;
                }
            }
        `);
        printWindow.document.write('</style>');

        printWindow.document.write('</head><body>');

        // Añadir un título al reporte impreso
        printWindow.document.write('<div class="container-fluid">');
        printWindow.document.write('<h2 class="text-center mb-4">' + $("#modalResultadosLabel").text() + '</h2>');
        printWindow.document.write(printContents);
        printWindow.document.write('</div>');

        printWindow.document.write('</body></html>');
        printWindow.document.close();

        // Esperar a que cargue todo el contenido antes de imprimir
        printWindow.onload = function () {
            printWindow.focus();
            printWindow.print();
            printWindow.close();
        };
    });

    // ===== GESTIÓN DE AJAX Y POSTBACKS =====

    // Obtener instancia del PageRequestManager
    if (typeof Sys !== 'undefined' && Sys.WebForms && Sys.WebForms.PageRequestManager) {
        var prm = Sys.WebForms.PageRequestManager.getInstance();

        // Antes de enviar una solicitud AJAX
        prm.add_initializeRequest(function (sender, args) {
            console.log("Iniciando solicitud AJAX...");
        });

        // Al recibir una respuesta AJAX
        prm.add_endRequest(function (sender, args) {
            console.log("Solicitud AJAX completada");

            // Verificar si hubo error
            if (args.get_error() != undefined) {
                console.error("Error en solicitud AJAX: " + args.get_error().message);
                args.set_errorHandled(true);
            }

            // Re-aplicar mejoras visuales después de cada actualización AJAX
            fixTableHeaders();
            fixIndicatorCards();

            // Inicializar tooltips y popovers nuevamente
            $('[data-toggle="tooltip"]').tooltip();
            $('[data-toggle="popover"]').popover();

            // Si hay un panel de resultados visible, mostrar la modal
            if ($("#pnlResultados").is(":visible")) {
                console.log("Panel de resultados visible después de AJAX, mostrando modal");
                $('#modalResultados').modal('show');
            }
        });
    } else {
        console.warn("PageRequestManager no disponible");
    }

    // ===== FUNCIONES DE UTILIDAD =====

    // Función para formatear moneda
    window.formatCurrency = function (amount, currency = 'S/') {
        return currency + ' ' + parseFloat(amount).toFixed(2).replace(/\d(?=(\d{3})+\.)/g, '$&,');
    };

    // Función para formatear fecha
    window.formatDate = function (dateString) {
        var date = new Date(dateString);
        return date.toLocaleDateString('es-PE', { year: 'numeric', month: '2-digit', day: '2-digit' });
    };

    // ===== FUNCIÓN ESPECÍFICA PARA ABRIR LA MODAL DE REPORTE =====

    // Esta función se puede llamar directamente desde código para mostrar la modal
    window.mostrarModalReporte = function () {
        console.log("Mostrando modal de reporte programáticamente");

        // Asegurarse que el panel de resultados esté visible
        $("#pnlResultados").show();

        // Aplicar fixes antes de mostrar
        fixTableHeaders();
        fixIndicatorCards();

        // Mostrar la modal
        $('#modalResultados').modal('show');

        // Aplicar nuevamente fixes después de mostrar (para estar seguros)
        setTimeout(function () {
            fixTableHeaders();
            fixIndicatorCards();
        }, 100);
    };

    console.log("Scripts del SGV inicializados correctamente");
});
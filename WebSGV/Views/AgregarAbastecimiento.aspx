<%@ Page Title="Parte de Abastecimiento de Combustible" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AgregarAbastecimiento.aspx.cs" Inherits="WebSGV.Views.AgregarAbastecimiento" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        :root {
            --primary-color: #0056b3;
            --secondary-color: #0062cc;
            --accent-color: #f0f7ff;
            --border-color: #dee2e6;
        }

        .abastecimiento-container {
            background-color: white;
            border-radius: 6px;
            box-shadow: 0 0.5rem 1rem rgba(0, 0, 0, 0.1);
            padding: 20px;
            margin-bottom: 30px;
        }

        .header-container {
            border-bottom: 2px solid var(--primary-color);
            padding-bottom: 15px;
            margin-bottom: 25px;
        }

        .abastecimiento-header {
            color: var(--primary-color);
            font-size: 1.6rem;
            font-weight: 700;
            margin: 0;
        }

        .card {
            border: none;
            margin-bottom: 25px;
            box-shadow: 0 0.125rem 0.25rem rgba(0, 0, 0, 0.075);
        }

        .card-header {
            background-color: var(--primary-color);
            color: white;
            font-weight: 600;
            font-size: 1.1rem;
            padding: 12px 20px;
            border-radius: 4px 4px 0 0;
        }

        .card-body {
            padding: 20px;
            background-color: #fafafa;
            border: 1px solid #e9ecef;
            border-top: none;
            border-radius: 0 0 4px 4px;
        }

        .form-label {
            font-weight: 500;
            color: #495057;
        }

        .form-control {
            border: 1px solid #ced4da;
            padding: 8px 12px;
            height: auto;
        }

        .form-control:focus {
            border-color: #80bdff;
            box-shadow: 0 0 0 0.2rem rgba(0, 123, 255, 0.25);
        }

        .btn-primary {
            background-color: var(--primary-color);
            border-color: var(--primary-color);
            padding: 8px 20px;
        }

        .btn-primary:hover {
            background-color: var(--secondary-color);
            border-color: var(--secondary-color);
        }

        .calculation-box {
            background-color: var(--accent-color);
            border: 1px solid #d1e7ff;
            border-radius: 4px;
            padding: 15px;
            margin-top: 15px;
        }

        .calculation-result {
            display: flex;
            justify-content: space-between;
            font-weight: 600;
            color: var(--primary-color);
        }

        /* Estilos para campos calculados */
        .calculated-field {
            background-color: #f0f7ff;
            color: #495057;
            font-weight: 500;
        }

        .synchronized-field {
            background-color: #fff3cd;
            border-color: #ffeaa7;
        }

        /* Indicador visual de tanque de combustible */
        .fuel-section {
            background-color: #f8f9fa;
            border-radius: 6px;
            border: 1px solid #e9ecef;
            padding: 15px;
            margin-top: 20px;
        }

        .fuel-tank {
            height: 30px;
            background-color: #e9ecef;
            border-radius: 15px;
            position: relative;
            overflow: hidden;
            margin-bottom: 10px;
        }

        .fuel-level {
            height: 100%;
            background: linear-gradient(90deg, #28a745, #67c977);
            border-radius: 15px 0 0 15px;
            width: 75%;
        }

        .fuel-markers {
            display: flex;
            justify-content: space-between;
            font-size: 0.8rem;
            color: #6c757d;
        }

        /* NUEVOS ESTILOS PARA DETALLES DE COMPRA */
        .tickets-section {
            background-color: #f8f9fa;
            border: 1px solid #e9ecef;
            border-radius: 6px;
            padding: 20px;
            margin-bottom: 20px;
        }

        .tickets-table {
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 15px;
        }

        .tickets-table th {
            background-color: var(--primary-color);
            color: white;
            padding: 12px;
            text-align: center;
            font-weight: 600;
        }

        .tickets-table td {
            padding: 10px;
            text-align: center;
            border: 1px solid #dee2e6;
            background-color: white;
        }

        .tickets-table input {
            border: 1px solid #ced4da;
            padding: 8px;
            border-radius: 4px;
            width: 100%;
            text-align: center;
        }

        .btn-add-ticket {
            background-color: #28a745;
            border-color: #28a745;
            color: white;
            padding: 8px 15px;
            border-radius: 4px;
            cursor: pointer;
        }

        .btn-add-ticket:hover {
            background-color: #218838;
            border-color: #1e7e34;
        }

        .btn-remove-ticket {
            background-color: #dc3545;
            border-color: #dc3545;
            color: white;
            padding: 5px 10px;
            border-radius: 3px;
            cursor: pointer;
            font-size: 12px;
        }

        .btn-remove-ticket:hover {
            background-color: #c82333;
            border-color: #bd2130;
        }

        .totales-tickets {
            background-color: #e3f2fd;
            border: 1px solid #2196f3;
            border-radius: 4px;
            padding: 15px;
            margin-top: 15px;
        }

        .total-row {
            display: flex;
            justify-content: space-between;
            font-weight: 600;
            color: var(--primary-color);
            margin-bottom: 5px;
        }

        .observaciones-section {
            margin-top: 20px;
        }

        /* Estilos mejorados para Select2 */
        .select2-container {
            width: 100% !important;
        }

        .select2-container--default .select2-selection--single {
            height: 38px;
            border: 1px solid #ced4da;
            border-radius: 4px;
            background-color: #fff;
            box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
            transition: border-color 0.15s ease-in-out, box-shadow 0.15s ease-in-out;
        }

        .select2-container--default .select2-selection--single:focus,
        .select2-container--default.select2-container--focus .select2-selection--single {
            border-color: #80bdff;
            box-shadow: 0 0 0 0.2rem rgba(0, 123, 255, 0.25);
        }

        .select2-container--default .select2-selection--single .select2-selection__rendered {
            color: #495057;
            line-height: 36px;
            padding-left: 12px;
            padding-right: 30px;
        }

        .select2-container--default .select2-selection--single .select2-selection__placeholder {
            color: #6c757d;
        }

        .select2-container--default .select2-selection--single .select2-selection__arrow {
            height: 36px;
            width: 30px;
            right: 3px;
        }

        .select2-container--default .select2-selection--single .select2-selection__arrow b {
            border-color: #6c757d transparent transparent transparent;
        }

        .select2-container--default.select2-container--open .select2-selection--single .select2-selection__arrow b {
            border-color: transparent transparent #6c757d transparent;
        }

        .select2-container--default .select2-selection--single .select2-selection__clear {
            color: #dc3545;
            font-weight: bold;
            margin-right: 8px;
        }

        .select2-dropdown {
            border: 1px solid #ced4da;
            border-radius: 4px;
            box-shadow: 0 3px 8px rgba(0, 0, 0, 0.15);
            overflow: hidden;
        }

        .select2-search--dropdown {
            padding: 8px;
        }

        .select2-search--dropdown .select2-search__field {
            padding: 8px 12px;
            border: 1px solid #ced4da;
            border-radius: 4px;
            outline: none;
        }

        .select2-search--dropdown .select2-search__field:focus {
            border-color: #80bdff;
            box-shadow: 0 0 0 0.1rem rgba(0, 123, 255, 0.2);
        }

        .select2-results__option {
            padding: 8px 12px;
            font-size: 14px;
            transition: background-color 0.15s ease-in-out;
        }

        .select2-results__option[aria-selected=true] {
            background-color: #e9ecef;
        }

        .select2-container--default .select2-results__option--highlighted[aria-selected] {
            background-color: #0056b3;
            color: white;
        }

        /* Mensaje de éxito o error */
        .alert-message {
            padding: 10px 15px;
            margin-bottom: 15px;
            border-radius: 4px;
            display: none;
        }

        .alert-success {
            background-color: #d4edda;
            border-color: #c3e6cb;
            color: #155724;
        }

        .alert-danger {
            background-color: #f8d7da;
            border-color: #f5c6cb;
            color: #721c24;
        }

        .sync-indicator {
            font-size: 0.8em;
            color: #ffc107;
            margin-left: 5px;
        }
    </style>

    <div class="container-fluid abastecimiento-container">
        <!-- Mensaje de alerta para retroalimentación al usuario -->
        <div id="alertMessage" class="alert-message" role="alert"></div>

        <!-- Encabezado SIN número -->
        <div class="header-container">
            <h3 class="abastecimiento-header text-uppercase">Parte de Abastecimiento de Combustible</h3>
        </div>

        <!-- Información General -->
        <div class="card mb-4">
            <div class="card-header">
                <i class="fas fa-truck me-2"></i>Información General
            </div>
            <div class="card-body">
                <div class="row">
                    <div class="col-md-3">
                        <label for="<%= tipoVehiculo.ClientID %>" class="form-label">Tipo:</label>
                        <asp:DropDownList ID="tipoVehiculo" runat="server" CssClass="form-control">
                            <asp:ListItem Value="1">Camioneta</asp:ListItem>
                            <asp:ListItem Value="2" Selected="True">Camión</asp:ListItem>
                            <asp:ListItem Value="3">Trailer</asp:ListItem>
                            <asp:ListItem Value="4">Otro</asp:ListItem>
                        </asp:DropDownList>
                    </div>
                    <div class="col-md-3">
                        <label for="<%= ddlPlaca.ClientID %>" class="form-label">Placa:</label>
                        <asp:DropDownList ID="ddlPlaca" runat="server" CssClass="form-control"></asp:DropDownList>
                    </div>
                    <div class="col-md-3">
                        <label for="<%= ddlCarreta.ClientID %>" class="form-label">Carreta:</label>
                        <asp:DropDownList ID="ddlCarreta" runat="server" CssClass="form-control"></asp:DropDownList>
                    </div>
                    <div class="col-md-3">
                        <label for="<%= ddlConductor.ClientID %>" class="form-label">Conductor:</label>
                        <asp:DropDownList ID="ddlConductor" runat="server" CssClass="form-control"></asp:DropDownList>
                    </div>
                </div>
            </div>
        </div>

        <!-- Ruta y Producto -->
        <div class="card mb-4">
            <div class="card-header">
                <i class="fas fa-route me-2"></i>Ruta y Producto
            </div>
            <div class="card-body">
                <div class="row">
                    <div class="col-md-6">
                        <label for="<%= ddlRuta.ClientID %>" class="form-label">Ruta:</label>
                        <asp:DropDownList ID="ddlRuta" runat="server" CssClass="form-control"></asp:DropDownList>
                    </div>
                    <div class="col-md-6">
                        <label for="<%= txtProducto.ClientID %>" class="form-label">Producto:</label>
                        <asp:TextBox ID="txtProducto" runat="server" CssClass="form-control" placeholder="Ingrese el producto"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <!-- Control de Combustible -->
        <div class="card mb-4">
            <div class="card-header">
                <i class="fas fa-gas-pump me-2"></i>Control de Combustible
            </div>
            <div class="card-body">
                <div class="row">
                    <div class="col-md-4">
                        <label for="<%= lugarAbastecimiento.ClientID %>" class="form-label">Lugar de Abastecimiento:</label>
                        <asp:DropDownList ID="lugarAbastecimiento" runat="server" CssClass="form-control">
                            <asp:ListItem Value="1" Selected="True">Grifo Cochera 03</asp:ListItem>
                            <asp:ListItem Value="2">Otro</asp:ListItem>
                        </asp:DropDownList>
                    </div>
                    <div class="col-md-4">
                        <label for="<%= txtFecha.ClientID %>" class="form-label">Fecha:</label>
                        <asp:TextBox ID="txtFecha" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                    </div>
                    <div class="col-md-4">
                        <label for="<%= txtHora.ClientID %>" class="form-label">Hora:</label>
                        <asp:TextBox ID="txtHora" runat="server" CssClass="form-control" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <!-- NUEVA SECCIÓN: Detalles de Compra -->
        <div class="card mb-4">
            <div class="card-header">
                <i class="fas fa-receipt me-2"></i>Detalles de Compra - Tickets de Combustible
            </div>
            <div class="card-body">
                <div class="tickets-section">
                    <table class="tickets-table" id="ticketsTable">
                        <thead>
                            <tr>
                                <th style="width: 10%;">#</th>
                                <th style="width: 35%;">Costo del Ticket (USD)</th>
                                <th style="width: 35%;">Galones</th>
                                <th style="width: 20%;">Acciones</th>
                            </tr>
                        </thead>
                        <tbody id="ticketsTableBody">
                            <!-- Los tickets se agregarán dinámicamente aquí -->
                        </tbody>
                    </table>
                    
                    <div class="text-center">
                        <button type="button" class="btn-add-ticket" onclick="agregarTicket()">
                            <i class="fas fa-plus"></i> Agregar Ticket
                        </button>
                    </div>

                    <div class="totales-tickets" id="totalesTickets">
                        <div class="total-row">
                            <span>Total Tickets:</span>
                            <span id="totalTickets">0</span>
                        </div>
                        <div class="total-row">
                            <span>Costo Total (USD):</span>
                            <span id="costoTotalTickets">$ 0.00</span>
                        </div>
                        <div class="total-row">
                            <span>Galones Totales:</span>
                            <span id="galonesTotalesTickets">0.00 GL</span>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Detalles de Consumo -->
        <div class="card mb-4">
            <div class="card-header">
                <i class="fas fa-tachometer-alt me-2"></i>Detalles de Consumo
            </div>
            <div class="card-body">
                <div class="row">
                    <div class="col-md-6">
                        <div class="row">
                            <div class="col-md-6 form-group">
                                <label for="<%= txtGLRuta.ClientID %>" class="form-label">GL Ruta Asignada:</label>
                                <asp:TextBox ID="txtGLRuta" runat="server" CssClass="form-control" TextMode="Number" step="any" placeholder="Ej: 50" onchange="calcularTotales()"></asp:TextBox>
                            </div>
                            <div class="col-md-6 form-group">
                                <label for="<%= txtGLComprados.ClientID %>" class="form-label">
                                    GL Comprados en Ruta:
                                    <span class="sync-indicator">🔗</span>
                                </label>
                                <asp:TextBox ID="txtGLComprados" runat="server" CssClass="form-control synchronized-field" TextMode="Number" step="any" placeholder="Auto desde tickets" readonly="true"></asp:TextBox>
                            </div>
                        </div>
                        <div class="row mt-3">
                            <div class="col-md-6 form-group">
                                <label for="<%= txtTotalGL.ClientID %>" class="form-label">GL Total Abastecidos:</label>
                                <asp:TextBox ID="txtTotalGL" runat="server" CssClass="form-control calculated-field" TextMode="Number" step="any" placeholder="Calculado automáticamente" ReadOnly="true"></asp:TextBox>
                            </div>
                            <div class="col-md-6 form-group">
                                <label for="<%= txtGLFinal.ClientID %>" class="form-label">GL Trae al Finalizar:</label>
                                <asp:TextBox ID="txtGLFinal" runat="server" CssClass="form-control" TextMode="Number" step="any" placeholder="Ej: 62" onchange="calcularTotales()"></asp:TextBox>
                            </div>
                        </div>
                        <div class="row mt-3">
                            <div class="col-md-6 form-group">
                                <label for="<%= txtGLConsumidos.ClientID %>" class="form-label">GL Total Consumidos:</label>
                                <asp:TextBox ID="txtGLConsumidos" runat="server" CssClass="form-control calculated-field" TextMode="Number" step="any" placeholder="Calculado automáticamente" ReadOnly="true"></asp:TextBox>
                            </div>
                            <div class="col-md-6 form-group">
                                <label for="<%= txtPrecioDolar.ClientID %>" class="form-label">Precio del Dólar:</label>
                                <asp:TextBox ID="txtPrecioDolar" runat="server" CssClass="form-control" TextMode="Number" step="any" placeholder="Ej: 1.795"></asp:TextBox>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-6">
                        <div class="row">
                            <div class="col-md-6 form-group">
                                <label for="<%= txtMontoTotal.ClientID %>" class="form-label">
                                    Monto Total GL:
                                    <span class="sync-indicator">🔗</span>
                                </label>
                                <asp:TextBox ID="txtMontoTotal" runat="server" CssClass="form-control synchronized-field" TextMode="Number" step="any" placeholder="Auto desde tickets" readonly="true"></asp:TextBox>
                            </div>
                            <div class="col-md-6 form-group">
                                <label for="<%= txtDistancia.ClientID %>" class="form-label">Distancia en KM:</label>
                                <asp:TextBox ID="txtDistancia" runat="server" CssClass="form-control" TextMode="Number" step="any" placeholder="Ej: 1935.9" onchange="calcularRendimiento()"></asp:TextBox>
                            </div>
                        </div>
                        <div class="row mt-3">
                            <div class="col-md-6 form-group">
                                <label for="<%= txtConsumoComputador.ClientID %>" class="form-label">Consumo Computador:</label>
                                <asp:TextBox ID="txtConsumoComputador" runat="server" CssClass="form-control" TextMode="Number" step="any" placeholder="Ej: 184.2"></asp:TextBox>
                            </div>
                            <div class="col-md-6 form-group">
                                <label for="<%= txtHoraRetorno.ClientID %>" class="form-label">Hora Retorno:</label>
                                <asp:TextBox ID="txtHoraRetorno" runat="server" CssClass="form-control" TextMode="Time"></asp:TextBox>
                            </div>
                        </div>
                        <div class="fuel-section mt-3">
                            <h6 class="mb-2">Visualización de consumo de combustible</h6>
                            <div class="fuel-tank">
                                <div class="fuel-level" id="fuelLevelVisual"></div>
                            </div>
                            <div class="fuel-markers">
                                <span>0%</span>
                                <span>25%</span>
                                <span>50%</span>
                                <span>75%</span>
                                <span>100%</span>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Cálculos y Resultados -->
                <div class="calculation-box mt-3">
                    <div class="calculation-result">
                        <span>Rendimiento promedio (KM/GL):</span>
                        <span id="rendimientoPromedio">0.00</span>
                    </div>
                </div>
            </div>
        </div>

        <!-- Observaciones -->
        <div class="card mb-4 observaciones-section">
            <div class="card-header">
                <i class="fas fa-clipboard me-2"></i>Observaciones
            </div>
            <div class="card-body">
                <div class="row">
                    <div class="col-12">
                        <asp:TextBox ID="txtObservaciones" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="4" placeholder="Ingrese observaciones adicionales aquí..."></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <!-- Botones -->
        <div class="text-end mt-4">
            <asp:Button ID="btnImprimir" runat="server" CssClass="btn btn-success me-2" Text="Imprimir" OnClientClick="window.print(); return false;" />
            <asp:Button ID="btnLimpiar" runat="server" CssClass="btn btn-secondary me-2" Text="Limpiar" OnClientClick="limpiarFormulario(); return false;" />
            <asp:Button ID="btnGuardar" runat="server" CssClass="btn btn-primary" Text="Guardar" OnClick="btnGuardar_Click" UseSubmitBehavior="true" />
        </div>

        <!-- Campo oculto para almacenar datos de tickets -->
        <asp:HiddenField ID="hdnTicketsData" runat="server" />
    </div>

    <!-- Referencias a jQuery y jQuery UI -->
    <script src="https://code.jquery.com/jquery-3.6.0.min.js"></script>
    <script src="https://code.jquery.com/ui/1.13.2/jquery-ui.min.js"></script>
    <link rel="stylesheet" href="https://code.jquery.com/ui/1.13.2/themes/base/jquery-ui.css">

    <!-- Referencias a Select2 -->
    <link href="https://cdn.jsdelivr.net/npm/select2@4.1.0-rc.0/dist/css/select2.min.css" rel="stylesheet" />
    <script src="https://cdn.jsdelivr.net/npm/select2@4.1.0-rc.0/dist/js/select2.min.js"></script>

    <!-- Scripts para cálculos automáticos y manejo de tickets -->
    <script type="text/javascript">
        // Variables globales para tickets
        let contadorTickets = 0;
        let ticketsData = [];

        // Función para agregar un nuevo ticket
        function agregarTicket() {
            contadorTickets++;
            const tableBody = document.getElementById('ticketsTableBody');

            const newRow = document.createElement('tr');
            newRow.id = `ticket_${contadorTickets}`;
            newRow.innerHTML = `
                <td>${contadorTickets}</td>
                <td>
                    <input type="number" 
                           id="costo_${contadorTickets}" 
                           step="0.01" 
                           placeholder="548.54" 
                           onchange="actualizarTotalesTickets()" 
                           style="width: 100%; text-align: center;">
                </td>
                <td>
                    <input type="number" 
                           id="galones_${contadorTickets}" 
                           step="0.01" 
                           placeholder="305.2" 
                           onchange="actualizarTotalesTickets()" 
                           style="width: 100%; text-align: center;">
                </td>
                <td>
                    <button type="button" 
                            class="btn-remove-ticket" 
                            onclick="eliminarTicket(${contadorTickets})">
                        <i class="fas fa-trash"></i> Eliminar
                    </button>
                </td>
            `;

            tableBody.appendChild(newRow);
            actualizarTotalesTickets();
        }

        // Función para eliminar un ticket
        function eliminarTicket(id) {
            if (confirm('¿Está seguro de eliminar este ticket?')) {
                const row = document.getElementById(`ticket_${id}`);
                if (row) {
                    row.remove();
                    actualizarTotalesTickets();
                }
            }
        }

        // Función para actualizar totales de tickets
        function actualizarTotalesTickets() {
            let totalTickets = 0;
            let costoTotal = 0;
            let galonesTotal = 0;

            // Recorrer todas las filas de tickets
            const rows = document.querySelectorAll('#ticketsTableBody tr');
            rows.forEach(row => {
                const costoInput = row.querySelector('input[id^="costo_"]');
                const galonesInput = row.querySelector('input[id^="galones_"]');

                if (costoInput && galonesInput) {
                    const costo = parseFloat(costoInput.value) || 0;
                    const galones = parseFloat(galonesInput.value) || 0;

                    if (costo > 0 || galones > 0) {
                        totalTickets++;
                        costoTotal += costo;
                        galonesTotal += galones;
                    }
                }
            });

            // Actualizar totales en la interfaz
            document.getElementById('totalTickets').textContent = totalTickets;
            document.getElementById('costoTotalTickets').textContent = `$ ${costoTotal.toFixed(2)}`;
            document.getElementById('galonesTotalesTickets').textContent = `${galonesTotal.toFixed(2)} GL`;

            // Sincronizar con campos principales
            sincronizarCamposPrincipales(costoTotal, galonesTotal);

            // Guardar datos en campo oculto
            guardarTicketsData();
        }

        // Función para sincronizar campos principales
        function sincronizarCamposPrincipales(costoTotal, galonesTotal) {
            const txtGLComprados = document.getElementById('<%= txtGLComprados.ClientID %>');
            const txtMontoTotal = document.getElementById('<%= txtMontoTotal.ClientID %>');

            if (txtGLComprados) {
                txtGLComprados.value = galonesTotal.toFixed(2);
            }

            if (txtMontoTotal) {
                txtMontoTotal.value = costoTotal.toFixed(2);
            }

            // Recalcular totales generales
            calcularTotales();
        }

        // Función para guardar datos de tickets en campo oculto
        function guardarTicketsData() {
            const tickets = [];
            const rows = document.querySelectorAll('#ticketsTableBody tr');

            rows.forEach(row => {
                const costoInput = row.querySelector('input[id^="costo_"]');
                const galonesInput = row.querySelector('input[id^="galones_"]');

                if (costoInput && galonesInput) {
                    const costo = parseFloat(costoInput.value) || 0;
                    const galones = parseFloat(galonesInput.value) || 0;

                    if (costo > 0 || galones > 0) {
                        tickets.push({
                            costo: costo,
                            galones: galones
                        });
                    }
                }
            });

            const hdnTicketsData = document.getElementById('<%= hdnTicketsData.ClientID %>');
            if (hdnTicketsData) {
                hdnTicketsData.value = JSON.stringify(tickets);
            }
        }

        // Función para calcular totales automáticamente (MODIFICADA)
        function calcularTotales() {
            // Asegurar el uso de punto como separador decimal
            var glRuta = parseFloat((document.getElementById('<%= txtGLRuta.ClientID %>').value || "0").replace(",", ".")) || 0;
            var glComprados = parseFloat((document.getElementById('<%= txtGLComprados.ClientID %>').value || "0").replace(",", ".")) || 0;
            var glFinal = parseFloat((document.getElementById('<%= txtGLFinal.ClientID %>').value || "0").replace(",", ".")) || 0;

            // Calcular total abastecido
            var totalAbastecido = glRuta + glComprados;
            document.getElementById('<%= txtTotalGL.ClientID %>').value = totalAbastecido.toFixed(2);

            // Calcular total consumido
            var totalConsumido = totalAbastecido - glFinal;
            if (totalConsumido < 0) totalConsumido = 0;
            document.getElementById('<%= txtGLConsumidos.ClientID %>').value = totalConsumido.toFixed(2);

            // Actualizar visualización del nivel de combustible
            actualizarNivelCombustible(glFinal, totalAbastecido);

            // Calcular rendimiento si hay distancia
            calcularRendimiento();
        }

        // Función para calcular rendimiento
        function calcularRendimiento() {
            var distancia = parseFloat((document.getElementById('<%= txtDistancia.ClientID %>').value || "0").replace(",", ".")) || 0;
            var consumido = parseFloat((document.getElementById('<%= txtGLConsumidos.ClientID %>').value || "0").replace(",", ".")) || 0;

            if (distancia > 0 && consumido > 0) {
                var rendimiento = distancia / consumido;
                document.getElementById('rendimientoPromedio').textContent = rendimiento.toFixed(2);
            } else {
                document.getElementById('rendimientoPromedio').textContent = "0.00";
            }
        }

        // Función para actualizar la visualización del nivel de combustible
        function actualizarNivelCombustible(actual, total) {
            const fuelLevelVisual = document.getElementById('fuelLevelVisual');
            if (fuelLevelVisual) {
                if (total > 0) {
                    var porcentaje = (actual / total) * 100;
                    if (porcentaje > 100) porcentaje = 100;
                    fuelLevelVisual.style.width = porcentaje + '%';
                } else {
                    fuelLevelVisual.style.width = '0%';
                }
            }
        }

        // Inicializar cuando el documento está completamente cargado
        document.addEventListener('DOMContentLoaded', function () {
            // Establecer fecha y hora actuales si no hay valores
            var fechaInput = document.getElementById('<%= txtFecha.ClientID %>');
            var horaInput = document.getElementById('<%= txtHora.ClientID %>');

            if (!fechaInput.value) {
                var hoy = new Date().toISOString().split('T')[0];
                fechaInput.value = hoy;
            }

            if (!horaInput.value) {
                var ahora = new Date();
                var horas = ahora.getHours().toString().padStart(2, '0');
                var minutos = ahora.getMinutes().toString().padStart(2, '0');
                horaInput.value = horas + ":" + minutos;
            }

            // Agregar el primer ticket automáticamente
            agregarTicket();

            // Iniciar cálculos
            calcularTotales();
        });

        // Función para limpiar formulario (MODIFICADA)
        function limpiarFormulario() {
            // Resetear los Select2
            try {
                if ($.fn.select2) {
                    $('#<%= ddlPlaca.ClientID %>').val(null).trigger('change');
                    $('#<%= ddlCarreta.ClientID %>').val(null).trigger('change');
                    $('#<%= ddlConductor.ClientID %>').val(null).trigger('change');
                    $('#<%= ddlRuta.ClientID %>').val(null).trigger('change');
                }
            } catch (e) {
                console.error("Error al resetear Select2: ", e);
            }

            // Limpiar tickets
            document.getElementById('ticketsTableBody').innerHTML = '';
            contadorTickets = 0;
            agregarTicket(); // Agregar un ticket vacío

            // Limpiar otros campos
            document.getElementById('<%= txtProducto.ClientID %>').value = '';
            document.getElementById('<%= txtGLRuta.ClientID %>').value = '';
            document.getElementById('<%= txtGLComprados.ClientID %>').value = '';
            document.getElementById('<%= txtTotalGL.ClientID %>').value = '';
            document.getElementById('<%= txtGLFinal.ClientID %>').value = '';
            document.getElementById('<%= txtGLConsumidos.ClientID %>').value = '';
            document.getElementById('<%= txtPrecioDolar.ClientID %>').value = '';
            document.getElementById('<%= txtMontoTotal.ClientID %>').value = '';
            document.getElementById('<%= txtDistancia.ClientID %>').value = '';
            document.getElementById('<%= txtConsumoComputador.ClientID %>').value = '';
            document.getElementById('<%= txtObservaciones.ClientID %>').value = '';
            document.getElementById('rendimientoPromedio').textContent = '0.00';
            document.getElementById('fuelLevelVisual').style.width = '0%';

            // Ocultar mensaje de alerta si está visible
            document.getElementById('alertMessage').style.display = 'none';
        }

        // Función para mostrar mensajes al usuario
        function mostrarMensaje(mensaje, tipo) {
            var alertDiv = document.getElementById('alertMessage');
            
            // Configurar el tipo de alerta
            alertDiv.classList.remove('alert-success', 'alert-danger');
            if (tipo === 'success') {
                alertDiv.classList.add('alert-success');
            } else {
                alertDiv.classList.add('alert-danger');
            }
            
            // Establecer el mensaje
            alertDiv.innerHTML = mensaje;
            alertDiv.style.display = 'block';
            
            // Ocultar después de 5 segundos
            setTimeout(function() {
                alertDiv.style.display = 'none';
            }, 5000);
        }

        // Inicializar Select2 cuando el documento esté listo
        $(document).ready(function () {
            // Inicializar Select2 en los DropDownList
            try {
                $('#<%= ddlPlaca.ClientID %>').select2({
                    placeholder: "Buscar placa...",
                    allowClear: true,
                    width: '100%',
                    closeOnSelect: true,
                    language: {
                        noResults: function () {
                            return "No se encontraron resultados";
                        },
                        searching: function () {
                            return "Buscando...";
                        }
                    }
                });

                $('#<%= ddlCarreta.ClientID %>').select2({
                    placeholder: "Buscar carreta...",
                    allowClear: true,
                    width: '100%',
                    closeOnSelect: true,
                    language: {
                        noResults: function () {
                            return "No se encontraron resultados";
                        },
                        searching: function () {
                            return "Buscando...";
                        }
                    }
                });

                $('#<%= ddlConductor.ClientID %>').select2({
                    placeholder: "Buscar conductor...",
                    allowClear: true,
                    width: '100%',
                    closeOnSelect: true,
                    language: {
                        noResults: function () {
                            return "No se encontraron resultados";
                        },
                        searching: function () {
                            return "Buscando...";
                        }
                    }
                });

                $('#<%= ddlRuta.ClientID %>').select2({
                    placeholder: "Buscar ruta...",
                    allowClear: true,
                    width: '100%',
                    closeOnSelect: true,
                    language: {
                        noResults: function () {
                            return "No se encontraron resultados";
                        },
                        searching: function () {
                            return "Buscando...";
                        }
                    }
                });
            } catch (e) {
                console.error("Error al inicializar Select2: ", e);
            }

            // Asegurar que el botón Guardar tiene el comportamiento correcto
            $('#<%= btnGuardar.ClientID %>').on('click', function (e) {
                console.log('Botón Guardar clickeado');
                guardarTicketsData(); // Asegurar que los datos estén guardados antes del postback
            });
        });
    </script>
</asp:Content>
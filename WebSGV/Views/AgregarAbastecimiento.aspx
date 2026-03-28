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
            padding-bottom: 12px;
            margin-bottom: 20px;
        }

        .abastecimiento-header {
            color: var(--primary-color);
            font-size: 1.4rem;
            font-weight: 700;
            margin: 0;
        }

        /* === BANNER COMPACTO DE VIAJE === */
        .trip-info-banner {
            background: linear-gradient(135deg, #f0f7ff 0%, #e3f0ff 100%);
            border: 1px solid #b8d4f0;
            border-left: 4px solid var(--primary-color);
            border-radius: 6px;
            padding: 14px 18px;
            margin-bottom: 18px;
        }

        .trip-info-banner .trip-title {
            font-size: 0.8rem;
            font-weight: 600;
            color: var(--primary-color);
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 8px;
        }

        .trip-info-grid {
            display: flex;
            flex-wrap: wrap;
            gap: 6px 24px;
        }

        .trip-info-item {
            display: flex;
            align-items: center;
            gap: 6px;
            font-size: 0.88rem;
        }

        .trip-info-item .info-icon {
            color: var(--primary-color);
            font-size: 0.85rem;
            width: 16px;
            text-align: center;
        }

        .trip-info-item .info-label {
            color: #6c757d;
            font-weight: 500;
        }

        .trip-info-item .info-value {
            color: #212529;
            font-weight: 600;
        }

        .trip-ruta-badge {
            display: inline-block;
            background-color: var(--primary-color);
            color: white;
            padding: 2px 10px;
            border-radius: 12px;
            font-size: 0.8rem;
            font-weight: 600;
        }

        .trip-gl-badge {
            display: inline-block;
            background-color: #28a745;
            color: white;
            padding: 2px 10px;
            border-radius: 12px;
            font-size: 0.8rem;
            font-weight: 600;
        }

        /* === CARD ESTILOS COMPACTOS === */
        .card {
            border: none;
            margin-bottom: 16px;
            box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08);
        }

        .card-header {
            background-color: var(--primary-color);
            color: white;
            font-weight: 600;
            font-size: 0.95rem;
            padding: 10px 16px;
            border-radius: 4px 4px 0 0;
        }

        .card-body {
            padding: 16px;
            background-color: #fafafa;
            border: 1px solid #e9ecef;
            border-top: none;
            border-radius: 0 0 4px 4px;
        }

        .form-label {
            font-weight: 500;
            color: #495057;
            font-size: 0.85rem;
            margin-bottom: 3px;
        }

        .form-control {
            border: 1px solid #ced4da;
            padding: 6px 10px;
            height: auto;
            font-size: 0.9rem;
        }

        .form-control:focus {
            border-color: #80bdff;
            box-shadow: 0 0 0 0.2rem rgba(0, 123, 255, 0.25);
        }

        .form-group {
            margin-bottom: 10px;
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
            padding: 12px;
            margin-top: 10px;
        }

        .calculation-result {
            display: flex;
            justify-content: space-between;
            font-weight: 600;
            color: var(--primary-color);
            font-size: 0.95rem;
        }

        .calculated-field {
            background-color: #f0f7ff;
            color: #495057;
            font-weight: 500;
        }

        .synchronized-field {
            background-color: #fff3cd;
            border-color: #ffeaa7;
        }

        /* === FUEL VISUAL COMPACTO === */
        .fuel-section {
            background-color: #f8f9fa;
            border-radius: 6px;
            border: 1px solid #e9ecef;
            padding: 12px;
            margin-top: 10px;
        }

        .fuel-tank {
            height: 22px;
            background-color: #e9ecef;
            border-radius: 11px;
            position: relative;
            overflow: hidden;
            margin-bottom: 6px;
        }

        .fuel-level {
            height: 100%;
            background: linear-gradient(90deg, #28a745, #67c977);
            border-radius: 11px 0 0 11px;
            width: 75%;
        }

        .fuel-markers {
            display: flex;
            justify-content: space-between;
            font-size: 0.75rem;
            color: #6c757d;
        }

        /* === TICKETS COMPACTOS === */
        .tickets-section {
            background-color: #f8f9fa;
            border: 1px solid #e9ecef;
            border-radius: 6px;
            padding: 14px;
        }

        .tickets-table {
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 10px;
        }

        .tickets-table th {
            background-color: var(--primary-color);
            color: white;
            padding: 8px 10px;
            text-align: center;
            font-weight: 600;
            font-size: 0.85rem;
        }

        .tickets-table td {
            padding: 6px 8px;
            text-align: center;
            border: 1px solid #dee2e6;
            background-color: white;
        }

        .tickets-table input {
            border: 1px solid #ced4da;
            padding: 6px;
            border-radius: 4px;
            width: 100%;
            text-align: center;
            font-size: 0.9rem;
        }

        .btn-add-ticket {
            background-color: #28a745;
            border-color: #28a745;
            color: white;
            padding: 6px 12px;
            border-radius: 4px;
            cursor: pointer;
            font-size: 0.85rem;
        }

        .btn-add-ticket:hover {
            background-color: #218838;
        }

        .btn-remove-ticket {
            background-color: #dc3545;
            border-color: #dc3545;
            color: white;
            padding: 4px 8px;
            border-radius: 3px;
            cursor: pointer;
            font-size: 0.75rem;
        }

        .btn-remove-ticket:hover {
            background-color: #c82333;
        }

        .totales-tickets {
            background-color: #e3f2fd;
            border: 1px solid #2196f3;
            border-radius: 4px;
            padding: 10px 14px;
            margin-top: 10px;
        }

        .total-row {
            display: flex;
            justify-content: space-between;
            font-weight: 600;
            color: var(--primary-color);
            margin-bottom: 3px;
            font-size: 0.9rem;
        }

        /* === SELECT2 === */
        .select2-container {
            width: 100% !important;
        }

        .select2-container--default .select2-selection--single {
            height: 36px;
            border: 1px solid #ced4da;
            border-radius: 4px;
            background-color: #fff;
        }

        .select2-container--default .select2-selection--single .select2-selection__rendered {
            color: #495057;
            line-height: 34px;
            padding-left: 10px;
            font-size: 0.9rem;
        }

        .select2-container--default .select2-selection--single .select2-selection__arrow {
            height: 34px;
        }

        .select2-dropdown {
            border: 1px solid #ced4da;
            border-radius: 4px;
            box-shadow: 0 3px 8px rgba(0, 0, 0, 0.15);
        }

        .select2-results__option {
            padding: 6px 10px;
            font-size: 0.9rem;
        }

        .select2-container--default .select2-results__option--highlighted[aria-selected] {
            background-color: #0056b3;
            color: white;
        }

        /* === ALERTAS === */
        .alert-message {
            padding: 10px 15px;
            margin-bottom: 12px;
            border-radius: 4px;
            display: none;
            font-size: 0.9rem;
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
            font-size: 0.75em;
            color: #ffc107;
            margin-left: 3px;
        }

        /* === LINK VOLVER === */
        .btn-back-link {
            color: var(--primary-color);
            font-size: 0.85rem;
            text-decoration: none;
        }

        .btn-back-link:hover {
            text-decoration: underline;
        }
    </style>

    <div class="container-fluid abastecimiento-container">
        <div id="alertMessage" class="alert-message" role="alert"></div>

        <!-- Encabezado -->
        <div class="header-container d-flex justify-content-between align-items-center">
            <h3 class="abastecimiento-header text-uppercase">
                <i class="fas fa-gas-pump mr-2"></i>Registro de Abastecimiento
            </h3>
            <asp:Panel ID="pnlBackLink" runat="server" Visible="false">
                <a href="DashboardGrifo.aspx" class="btn-back-link"><i class="fas fa-arrow-left mr-1"></i>Volver al Dashboard</a>
            </asp:Panel>
        </div>

        <!-- ============================================= -->
        <!-- MODO VIAJE: Banner compacto (solo si idViaje) -->
        <!-- ============================================= -->
        <asp:Panel ID="pnlTripBanner" runat="server" Visible="false">
            <div class="trip-info-banner">
                <div class="trip-title"><i class="fas fa-truck mr-1"></i>Datos del Viaje</div>
                <div class="trip-info-grid">
                    <div class="trip-info-item">
                        <span class="info-icon"><i class="fas fa-user"></i></span>
                        <span class="info-label">Conductor:</span>
                        <span class="info-value"><asp:Literal ID="litConductor" runat="server" /></span>
                    </div>
                    <div class="trip-info-item">
                        <span class="info-icon"><i class="fas fa-truck-moving"></i></span>
                        <span class="info-label">Tracto:</span>
                        <span class="info-value"><asp:Literal ID="litPlacaTracto" runat="server" /></span>
                    </div>
                    <div class="trip-info-item">
                        <span class="info-icon"><i class="fas fa-trailer"></i></span>
                        <span class="info-label">Carreta:</span>
                        <span class="info-value"><asp:Literal ID="litPlacaCarreta" runat="server" /></span>
                    </div>
                    <div class="trip-info-item">
                        <span class="info-icon"><i class="fas fa-route"></i></span>
                        <span class="info-label">Ruta:</span>
                        <span class="trip-ruta-badge"><asp:Literal ID="litRutaViaje" runat="server" /></span>
                    </div>
                    <div class="trip-info-item">
                        <span class="info-icon"><i class="fas fa-tint"></i></span>
                        <span class="info-label">GL Asignados:</span>
                        <span class="trip-gl-badge"><asp:Literal ID="litGLAsignados" runat="server" /></span>
                    </div>
                </div>
            </div>
        </asp:Panel>

        <!-- ============================================= -->
        <!-- MODO MANUAL: Dropdowns completos              -->
        <!-- ============================================= -->
        <asp:Panel ID="pnlManualEntry" runat="server" Visible="true">
            <div class="card">
                <div class="card-header">
                    <i class="fas fa-truck mr-2"></i>Información del Vehículo y Conductor
                </div>
                <div class="card-body">
                    <div class="row">
                        <div class="col-md-3 form-group">
                            <label class="form-label">Tipo:</label>
                            <asp:DropDownList ID="tipoVehiculo" runat="server" CssClass="form-control">
                                <asp:ListItem Value="1">Camioneta</asp:ListItem>
                                <asp:ListItem Value="2" Selected="True">Camión</asp:ListItem>
                                <asp:ListItem Value="3">Trailer</asp:ListItem>
                                <asp:ListItem Value="4">Otro</asp:ListItem>
                            </asp:DropDownList>
                        </div>
                        <div class="col-md-3 form-group">
                            <label class="form-label">Placa Tracto:</label>
                            <asp:DropDownList ID="ddlPlaca" runat="server" CssClass="form-control"></asp:DropDownList>
                        </div>
                        <div class="col-md-3 form-group">
                            <label class="form-label">Carreta:</label>
                            <asp:DropDownList ID="ddlCarreta" runat="server" CssClass="form-control"></asp:DropDownList>
                        </div>
                        <div class="col-md-3 form-group">
                            <label class="form-label">Conductor:</label>
                            <asp:DropDownList ID="ddlConductor" runat="server" CssClass="form-control"></asp:DropDownList>
                        </div>
                    </div>
                    <div class="row mt-2">
                        <div class="col-md-6 form-group">
                            <label class="form-label">Ruta:</label>
                            <asp:DropDownList ID="ddlRuta" runat="server" CssClass="form-control"></asp:DropDownList>
                        </div>
                        <div class="col-md-6 form-group">
                            <label class="form-label">Producto:</label>
                            <asp:TextBox ID="txtProducto" runat="server" CssClass="form-control" placeholder="Ingrese el producto"></asp:TextBox>
                        </div>
                    </div>
                </div>
            </div>
        </asp:Panel>

        <!-- Producto (visible solo en modo viaje, ya que en manual está arriba) -->
        <asp:Panel ID="pnlProductoViaje" runat="server" Visible="false">
            <div class="row mb-3">
                <div class="col-md-4 form-group">
                    <label class="form-label">Producto:</label>
                    <asp:TextBox ID="txtProductoViaje" runat="server" CssClass="form-control" placeholder="Ej: Diesel B5"></asp:TextBox>
                </div>
            </div>
        </asp:Panel>

        <!-- ============================================= -->
        <!-- SECCIÓN ÚNICA: Combustible y Tickets          -->
        <!-- ============================================= -->
        <div class="card">
            <div class="card-header">
                <i class="fas fa-gas-pump mr-2"></i>Control de Combustible
            </div>
            <div class="card-body">
                <div class="row">
                    <!-- Columna izquierda: Lugar, Fecha/Hora, GL -->
                    <div class="col-md-6">
                        <div class="row">
                            <div class="col-md-6 form-group">
                                <label class="form-label">Lugar de Abastecimiento:</label>
                                <asp:DropDownList ID="lugarAbastecimiento" runat="server" CssClass="form-control">
                                    <asp:ListItem Value="1" Selected="True">Grifo Cochera 03</asp:ListItem>
                                    <asp:ListItem Value="2">Otro</asp:ListItem>
                                </asp:DropDownList>
                            </div>
                            <div class="col-md-3 form-group">
                                <label class="form-label">Fecha:</label>
                                <asp:TextBox ID="txtFecha" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                            </div>
                            <div class="col-md-3 form-group">
                                <label class="form-label">Hora:</label>
                                <asp:TextBox ID="txtHora" runat="server" CssClass="form-control" TextMode="Time"></asp:TextBox>
                            </div>
                        </div>
                        <div class="row">
                            <div class="col-md-6 form-group">
                                <label class="form-label">GL Ruta Asignada:</label>
                                <asp:TextBox ID="txtGLRuta" runat="server" CssClass="form-control" TextMode="Number" step="any" placeholder="Ej: 50" onchange="calcularTotales()"></asp:TextBox>
                            </div>
                            <div class="col-md-6 form-group">
                                <label class="form-label">GL Comprados <span class="sync-indicator">🔗</span></label>
                                <asp:TextBox ID="txtGLComprados" runat="server" CssClass="form-control synchronized-field" TextMode="Number" step="any" placeholder="Auto"></asp:TextBox>
                            </div>
                        </div>
                        <div class="row">
                            <div class="col-md-6 form-group">
                                <label class="form-label">GL Total Abastecidos:</label>
                                <asp:TextBox ID="txtTotalGL" runat="server" CssClass="form-control calculated-field" TextMode="Number" step="any"></asp:TextBox>
                            </div>
                            <div class="col-md-6 form-group">
                                <label class="form-label">GL Trae al Finalizar:</label>
                                <asp:TextBox ID="txtGLFinal" runat="server" CssClass="form-control" TextMode="Number" step="any" placeholder="Ej: 62" onchange="calcularTotales()"></asp:TextBox>
                            </div>
                        </div>
                        <div class="row">
                            <div class="col-md-6 form-group">
                                <label class="form-label">GL Consumidos:</label>
                                <asp:TextBox ID="txtGLConsumidos" runat="server" CssClass="form-control calculated-field" TextMode="Number" step="any"></asp:TextBox>
                            </div>
                            <div class="col-md-6 form-group">
                                <label class="form-label">Precio Dólar:</label>
                                <asp:TextBox ID="txtPrecioDolar" runat="server" CssClass="form-control" TextMode="Number" step="any" placeholder="Ej: 1.795"></asp:TextBox>
                            </div>
                        </div>
                        <div class="row">
                            <div class="col-md-6 form-group">
                                <label class="form-label">Monto Total <span class="sync-indicator">🔗</span></label>
                                <asp:TextBox ID="txtMontoTotal" runat="server" CssClass="form-control synchronized-field" TextMode="Number" step="any"></asp:TextBox>
                            </div>
                            <div class="col-md-6 form-group">
                                <label class="form-label">Distancia KM:</label>
                                <asp:TextBox ID="txtDistancia" runat="server" CssClass="form-control" TextMode="Number" step="any" placeholder="Ej: 1935.9" onchange="calcularRendimiento()"></asp:TextBox>
                            </div>
                        </div>
                        <div class="row">
                            <div class="col-md-6 form-group">
                                <label class="form-label">Consumo Computador:</label>
                                <asp:TextBox ID="txtConsumoComputador" runat="server" CssClass="form-control" TextMode="Number" step="any" placeholder="Ej: 184.2"></asp:TextBox>
                            </div>
                            <div class="col-md-6 form-group">
                                <label class="form-label">Hora Retorno:</label>
                                <asp:TextBox ID="txtHoraRetorno" runat="server" CssClass="form-control" TextMode="Time"></asp:TextBox>
                            </div>
                        </div>
                        <div class="fuel-section">
                            <small class="text-muted mb-1 d-block">Nivel de combustible</small>
                            <div class="fuel-tank">
                                <div class="fuel-level" id="fuelLevelVisual"></div>
                            </div>
                            <div class="fuel-markers">
                                <span>0%</span><span>25%</span><span>50%</span><span>75%</span><span>100%</span>
                            </div>
                        </div>
                        <div class="calculation-box">
                            <div class="calculation-result">
                                <span>Rendimiento (KM/GL):</span>
                                <span id="rendimientoPromedio">0.00</span>
                            </div>
                        </div>
                    </div>

                    <!-- Columna derecha: Tickets -->
                    <div class="col-md-6">
                        <div class="tickets-section">
                            <div class="d-flex justify-content-between align-items-center mb-2">
                                <strong style="font-size:0.9rem; color: var(--primary-color);"><i class="fas fa-receipt mr-1"></i>Tickets de Combustible</strong>
                                <button type="button" class="btn-add-ticket" onclick="agregarTicket()">
                                    <i class="fas fa-plus"></i> Agregar
                                </button>
                            </div>
                            <table class="tickets-table" id="ticketsTable">
                                <thead>
                                    <tr>
                                        <th style="width: 10%;">#</th>
                                        <th style="width: 35%;">Costo (USD)</th>
                                        <th style="width: 35%;">Galones</th>
                                        <th style="width: 20%;"></th>
                                    </tr>
                                </thead>
                                <tbody id="ticketsTableBody"></tbody>
                            </table>
                            <div class="totales-tickets" id="totalesTickets">
                                <div class="total-row">
                                    <span>Tickets:</span>
                                    <span id="totalTickets">0</span>
                                </div>
                                <div class="total-row">
                                    <span>Costo Total:</span>
                                    <span id="costoTotalTickets">$ 0.00</span>
                                </div>
                                <div class="total-row">
                                    <span>Galones:</span>
                                    <span id="galonesTotalesTickets">0.00 GL</span>
                                </div>
                            </div>
                        </div>
                        <!-- Observaciones integradas -->
                        <div class="form-group mt-3">
                            <label class="form-label"><i class="fas fa-clipboard mr-1"></i>Observaciones:</label>
                            <asp:TextBox ID="txtObservaciones" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3" placeholder="Observaciones adicionales..."></asp:TextBox>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Botones -->
        <div class="text-end mt-3">
            <asp:Button ID="btnImprimir" runat="server" CssClass="btn btn-outline-secondary mr-2" Text="Imprimir" OnClientClick="window.print(); return false;" />
            <asp:Button ID="btnLimpiar" runat="server" CssClass="btn btn-secondary mr-2" Text="Limpiar" OnClientClick="limpiarFormulario(); return false;" />
            <asp:Button ID="btnGuardar" runat="server" CssClass="btn btn-primary" Text="Guardar Abastecimiento" OnClick="btnGuardar_Click" UseSubmitBehavior="true" />
        </div>

        <asp:HiddenField ID="hdnTicketsData" runat="server" />
        <asp:HiddenField ID="hdnModoViaje" runat="server" Value="0" />
        <asp:HiddenField ID="hdnIdViaje" runat="server" Value="0" />
    </div>

    <!-- Referencias a jQuery y jQuery UI -->
    <script src="https://code.jquery.com/jquery-3.6.0.min.js"></script>
    <script src="https://code.jquery.com/ui/1.13.2/jquery-ui.min.js"></script>
    <link rel="stylesheet" href="https://code.jquery.com/ui/1.13.2/themes/base/jquery-ui.css">

    <!-- Referencias a Select2 -->
    <link href="https://cdn.jsdelivr.net/npm/select2@4.1.0-rc.0/dist/css/select2.min.css" rel="stylesheet" />
    <script src="https://cdn.jsdelivr.net/npm/select2@4.1.0-rc.0/dist/js/select2.min.js"></script>

    <script type="text/javascript">
        let contadorTickets = 0;
        var esModoViaje = '<%= hdnModoViaje.Value %>' === '1';

        function agregarTicket() {
            contadorTickets++;
            const tableBody = document.getElementById('ticketsTableBody');
            const newRow = document.createElement('tr');
            newRow.id = 'ticket_' + contadorTickets;
            newRow.innerHTML =
                '<td>' + contadorTickets + '</td>' +
                '<td><input type="number" id="costo_' + contadorTickets + '" step="0.01" placeholder="548.54" onchange="actualizarTotalesTickets()"></td>' +
                '<td><input type="number" id="galones_' + contadorTickets + '" step="0.01" placeholder="305.2" onchange="actualizarTotalesTickets()"></td>' +
                '<td><button type="button" class="btn-remove-ticket" onclick="eliminarTicket(' + contadorTickets + ')"><i class="fas fa-trash"></i></button></td>';
            tableBody.appendChild(newRow);
            actualizarTotalesTickets();
        }

        function eliminarTicket(id) {
            var row = document.getElementById('ticket_' + id);
            if (row) { row.remove(); actualizarTotalesTickets(); }
        }

        function actualizarTotalesTickets() {
            var totalTickets = 0, costoTotal = 0, galonesTotal = 0;
            var rows = document.querySelectorAll('#ticketsTableBody tr');
            rows.forEach(function (row) {
                var costoInput = row.querySelector('input[id^="costo_"]');
                var galonesInput = row.querySelector('input[id^="galones_"]');
                if (costoInput && galonesInput) {
                    var costo = parseFloat(costoInput.value) || 0;
                    var galones = parseFloat(galonesInput.value) || 0;
                    if (costo > 0 || galones > 0) { totalTickets++; costoTotal += costo; galonesTotal += galones; }
                }
            });
            document.getElementById('totalTickets').textContent = totalTickets;
            document.getElementById('costoTotalTickets').textContent = '$ ' + costoTotal.toFixed(2);
            document.getElementById('galonesTotalesTickets').textContent = galonesTotal.toFixed(2) + ' GL';
            sincronizarCamposPrincipales(costoTotal, galonesTotal);
            guardarTicketsData();
        }

        function sincronizarCamposPrincipales(costoTotal, galonesTotal) {
            var elGL = document.getElementById('<%= txtGLComprados.ClientID %>');
            var elMonto = document.getElementById('<%= txtMontoTotal.ClientID %>');
            if (elGL) elGL.value = galonesTotal.toFixed(2);
            if (elMonto) elMonto.value = costoTotal.toFixed(2);
            calcularTotales();
        }

        function guardarTicketsData() {
            var tickets = [];
            document.querySelectorAll('#ticketsTableBody tr').forEach(function (row) {
                var c = row.querySelector('input[id^="costo_"]');
                var g = row.querySelector('input[id^="galones_"]');
                if (c && g) {
                    var cv = parseFloat(c.value) || 0, gv = parseFloat(g.value) || 0;
                    if (cv > 0 || gv > 0) tickets.push({ costo: cv, galones: gv });
                }
            });
            var hdn = document.getElementById('<%= hdnTicketsData.ClientID %>');
            if (hdn) hdn.value = JSON.stringify(tickets);
        }

        function calcularTotales() {
            var glRuta = parseFloat((document.getElementById('<%= txtGLRuta.ClientID %>').value || "0").replace(",", ".")) || 0;
            var glComprados = parseFloat((document.getElementById('<%= txtGLComprados.ClientID %>').value || "0").replace(",", ".")) || 0;
            var glFinal = parseFloat((document.getElementById('<%= txtGLFinal.ClientID %>').value || "0").replace(",", ".")) || 0;
            var totalAbastecido = glRuta + glComprados;
            document.getElementById('<%= txtTotalGL.ClientID %>').value = totalAbastecido.toFixed(2);
            var totalConsumido = totalAbastecido - glFinal;
            if (totalConsumido < 0) totalConsumido = 0;
            document.getElementById('<%= txtGLConsumidos.ClientID %>').value = totalConsumido.toFixed(2);
            actualizarNivelCombustible(glFinal, totalAbastecido);
            calcularRendimiento();
        }

        function calcularRendimiento() {
            var distancia = parseFloat((document.getElementById('<%= txtDistancia.ClientID %>').value || "0").replace(",", ".")) || 0;
            var consumido = parseFloat((document.getElementById('<%= txtGLConsumidos.ClientID %>').value || "0").replace(",", ".")) || 0;
            document.getElementById('rendimientoPromedio').textContent = (distancia > 0 && consumido > 0) ? (distancia / consumido).toFixed(2) : "0.00";
        }

        function actualizarNivelCombustible(actual, total) {
            var el = document.getElementById('fuelLevelVisual');
            if (el) {
                if (total > 0) { var p = (actual / total) * 100; el.style.width = Math.min(p, 100) + '%'; }
                else { el.style.width = '0%'; }
            }
        }

        document.addEventListener('DOMContentLoaded', function () {
            var fechaInput = document.getElementById('<%= txtFecha.ClientID %>');
            var horaInput = document.getElementById('<%= txtHora.ClientID %>');
            if (!fechaInput.value) fechaInput.value = new Date().toISOString().split('T')[0];
            if (!horaInput.value) {
                var ahora = new Date();
                horaInput.value = ahora.getHours().toString().padStart(2, '0') + ":" + ahora.getMinutes().toString().padStart(2, '0');
            }
            agregarTicket();
            calcularTotales();
        });

        function limpiarFormulario() {
            if (!esModoViaje) {
                try {
                    if ($.fn.select2) {
                        $('#<%= ddlPlaca.ClientID %>').val(null).trigger('change');
                        $('#<%= ddlCarreta.ClientID %>').val(null).trigger('change');
                        $('#<%= ddlConductor.ClientID %>').val(null).trigger('change');
                        $('#<%= ddlRuta.ClientID %>').val(null).trigger('change');
                    }
                } catch (e) { }
                var txtProd = document.getElementById('<%= txtProducto.ClientID %>');
                if (txtProd) txtProd.value = '';
            }
            var txtProdViaje = document.getElementById('<%= txtProductoViaje.ClientID %>');
            if (txtProdViaje) txtProdViaje.value = '';
            document.getElementById('ticketsTableBody').innerHTML = '';
            contadorTickets = 0;
            agregarTicket();
            var campos = ['<%= txtGLRuta.ClientID %>', '<%= txtGLComprados.ClientID %>', '<%= txtTotalGL.ClientID %>',
                '<%= txtGLFinal.ClientID %>', '<%= txtGLConsumidos.ClientID %>', '<%= txtPrecioDolar.ClientID %>',
                '<%= txtMontoTotal.ClientID %>', '<%= txtDistancia.ClientID %>', '<%= txtConsumoComputador.ClientID %>',
                '<%= txtObservaciones.ClientID %>'];
            campos.forEach(function (id) { var el = document.getElementById(id); if (el) el.value = ''; });
            document.getElementById('rendimientoPromedio').textContent = '0.00';
            document.getElementById('fuelLevelVisual').style.width = '0%';
            document.getElementById('alertMessage').style.display = 'none';
        }

        function mostrarMensaje(mensaje, tipo) {
            var alertDiv = document.getElementById('alertMessage');
            alertDiv.classList.remove('alert-success', 'alert-danger');
            alertDiv.classList.add(tipo === 'success' ? 'alert-success' : 'alert-danger');
            alertDiv.innerHTML = mensaje;
            alertDiv.style.display = 'block';
            setTimeout(function () { alertDiv.style.display = 'none'; }, 5000);
        }

        $(document).ready(function () {
            // Aplicar readonly via JS para que ASP.NET no ignore los valores en postback
            $('#<%= txtGLComprados.ClientID %>').prop('readonly', true);
            $('#<%= txtTotalGL.ClientID %>').prop('readonly', true);
            $('#<%= txtGLConsumidos.ClientID %>').prop('readonly', true);
            $('#<%= txtMontoTotal.ClientID %>').prop('readonly', true);

            if (!esModoViaje) {
                try {
                    var select2Config = { allowClear: true, width: '100%', closeOnSelect: true, language: { noResults: function () { return "Sin resultados"; }, searching: function () { return "Buscando..."; } } };
                    $('#<%= ddlPlaca.ClientID %>').select2($.extend({}, select2Config, { placeholder: "Buscar placa..." }));
                    $('#<%= ddlCarreta.ClientID %>').select2($.extend({}, select2Config, { placeholder: "Buscar carreta..." }));
                    $('#<%= ddlConductor.ClientID %>').select2($.extend({}, select2Config, { placeholder: "Buscar conductor..." }));
                    $('#<%= ddlRuta.ClientID %>').select2($.extend({}, select2Config, { placeholder: "Buscar ruta..." }));
                } catch (e) { console.error("Error Select2: ", e); }
            }

            $('#<%= btnGuardar.ClientID %>').on('click', function () { guardarTicketsData(); });
        });
    </script>
</asp:Content>
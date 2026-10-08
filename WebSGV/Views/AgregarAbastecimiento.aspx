<%@ Page Title="Parte de Abastecimiento de Combustible" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AgregarAbastecimiento.aspx.cs" Inherits="WebSGV.Views.AgregarAbastecimiento" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/AgregarAbastecimiento.css") %>" rel="stylesheet" />

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
            <!-- Motivo de Salida de Combustible -->
            <div class="card mb-3">
                <div class="card-header" style="background-color:#fff3e0; color:#e65100;">
                    <i class="fas fa-clipboard-list mr-2"></i>Motivo de Salida de Combustible
                </div>
                <div class="card-body" style="padding: 12px 18px;">
                    <div class="row align-items-end">
                        <div class="col-md-4 form-group mb-0">
                            <label class="form-label">Tipo de Registro:</label>
                            <asp:DropDownList ID="ddlMotivoSalida" runat="server" CssClass="form-control" onchange="onMotivoChange()">
                                <asp:ListItem Value="ABASTECIMIENTO" Selected="True">Abastecimiento (viaje corto / rutina)</asp:ListItem>
                                <asp:ListItem Value="MANTENIMIENTO">Mantenimiento (filtros, limpieza, servicio)</asp:ListItem>
                                <asp:ListItem Value="OTRO">Otro (generador, equipo, especial)</asp:ListItem>
                            </asp:DropDownList>
                        </div>
                        <div class="col-md-8 mb-0">
                            <div id="motivoHint" class="motivo-hint">
                                <i class="fas fa-info-circle mr-1"></i>
                                <span id="motivoHintText">Rellenado de combustible para viaje corto o rutina operativa.</span>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <div class="card">
                <div class="card-header">
                    <i class="fas fa-truck mr-2"></i>Información del Vehículo y Conductor
                </div>
                <div class="card-body">
                    <div class="row">
                        <div class="col-md-3 form-group">
                            <label class="form-label">Tipo:</label>
                            <asp:DropDownList ID="tipoVehiculo" runat="server" CssClass="form-control"
                                onchange="onTipoVehiculoChange()">
                                <asp:ListItem Value="1">Camioneta</asp:ListItem>
                                <asp:ListItem Value="2" Selected="True">Camión</asp:ListItem>
                                <asp:ListItem Value="3">Trailer</asp:ListItem>
                                <asp:ListItem Value="4">Otro</asp:ListItem>
                            </asp:DropDownList>
                        </div>
                        <div class="col-md-3 form-group" id="divPlacaTracto">
                            <label class="form-label">Placa Tracto:</label>
                            <asp:DropDownList ID="ddlPlaca" runat="server" CssClass="form-control"></asp:DropDownList>
                        </div>
                        <div class="col-md-3 form-group" id="divPlacaVolquete" style="display:none;">
                            <label class="form-label">Placa Volquete:</label>
                            <asp:DropDownList ID="ddlPlacaVolquete" runat="server" CssClass="form-control"></asp:DropDownList>
                        </div>
                        <div class="col-md-3 form-group" id="divPlacaCamioneta" style="display:none;">
                            <label class="form-label">Placa Camioneta:</label>
                            <asp:DropDownList ID="ddlPlacaCamioneta" runat="server" CssClass="form-control"></asp:DropDownList>
                        </div>
                        <div class="col-md-3 form-group" id="divPlacaOtro" style="display:none;">
                            <label class="form-label">Placa (texto libre):</label>
                            <asp:TextBox ID="txtPlacaOtro" runat="server" CssClass="form-control" placeholder="Ingrese placa"></asp:TextBox>
                        </div>
                        <div class="col-md-3 form-group" id="divCarreta">
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
                            <asp:TextBox ID="txtRutaManual" runat="server" CssClass="form-control" placeholder="Ej: Lima → Trujillo, Planta - Cusco..."></asp:TextBox>
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

    <!-- jQuery y jQuery UI (JS y CSS) los carga Site.Master -->

    <!-- Referencias a Select2 -->
    <link href="https://cdn.jsdelivr.net/npm/select2@4.1.0-rc.0/dist/css/select2.min.css" rel="stylesheet" />
    <script src="https://cdn.jsdelivr.net/npm/select2@4.1.0-rc.0/dist/js/select2.min.js"></script>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hdnModoViajeValue: '<%= hdnModoViaje.Value %>',
            txtGLComprados: '<%= txtGLComprados.ClientID %>',
            txtMontoTotal: '<%= txtMontoTotal.ClientID %>',
            hdnTicketsData: '<%= hdnTicketsData.ClientID %>',
            txtGLRuta: '<%= txtGLRuta.ClientID %>',
            txtGLFinal: '<%= txtGLFinal.ClientID %>',
            txtTotalGL: '<%= txtTotalGL.ClientID %>',
            txtGLConsumidos: '<%= txtGLConsumidos.ClientID %>',
            txtDistancia: '<%= txtDistancia.ClientID %>',
            txtFecha: '<%= txtFecha.ClientID %>',
            txtHora: '<%= txtHora.ClientID %>',
            tipoVehiculo: '<%= tipoVehiculo.ClientID %>',
            ddlMotivoSalida: '<%= ddlMotivoSalida.ClientID %>',
            ddlPlaca: '<%= ddlPlaca.ClientID %>',
            ddlCarreta: '<%= ddlCarreta.ClientID %>',
            ddlConductor: '<%= ddlConductor.ClientID %>',
            txtProducto: '<%= txtProducto.ClientID %>',
            txtRutaManual: '<%= txtRutaManual.ClientID %>',
            txtProductoViaje: '<%= txtProductoViaje.ClientID %>',
            txtPrecioDolar: '<%= txtPrecioDolar.ClientID %>',
            txtConsumoComputador: '<%= txtConsumoComputador.ClientID %>',
            txtObservaciones: '<%= txtObservaciones.ClientID %>',
            btnGuardar: '<%= btnGuardar.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/AgregarAbastecimiento.js") %>"></script>
</asp:Content>
<%@ Page Title="Reportes de Órdenes de Viaje" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReportesOrdenesViaje.aspx.cs" Inherits="WebSGV.Views.ReportesOrdenesViaje" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-4">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-4">

        <!-- Header -->
        <div class="row mb-4">
            <div class="col-12">
                <div class="page-header">
                    <div class="d-flex justify-content-between align-items-center">
                        <div>
                            <h2 class="page-title mb-1">
                                <i class="fas fa-file-invoice-dollar mr-2"></i>Reportes de Órdenes de Viaje
                            </h2>
                            <p class="text-muted mb-0">
                                Consulte liquidaciones y viajes activos del sistema
                            </p>
                        </div>
                        <div class="header-stats">
                            <div class="stat-card stat-primary">
                                <div class="stat-icon">
                                    <i class="fas fa-money-check-alt"></i>
                                </div>
                                <div class="stat-info">
                                    <span class="stat-label">Liquidaciones</span>
                                    <span class="stat-value">
                                        <asp:Label ID="lblTotalRegistros" runat="server" Text="0"></asp:Label>
                                    </span>
                                </div>
                            </div>
                            <div class="stat-card stat-warning ml-3">
                                <div class="stat-icon">
                                    <i class="fas fa-truck-loading"></i>
                                </div>
                                <div class="stat-info">
                                    <span class="stat-label">Sin Liquidar</span>
                                    <span class="stat-value">
                                        <asp:Label ID="lblCountViajesActivos" runat="server" Text="0"></asp:Label>
                                    </span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Tabs de navegación -->
        <div class="row mb-3">
            <div class="col-12">
                <div class="tabs-navigation">
                    <button type="button" class="tab-btn tab-btn-active" id="tabLiquidaciones" onclick="cambiarTab('liquidaciones')">
                        <i class="fas fa-money-check-alt mr-2"></i>Liquidaciones
                    </button>
                    <button type="button" class="tab-btn" id="tabViajesActivos" onclick="cambiarTab('viajesActivos')">
                        <i class="fas fa-truck-loading mr-2"></i>Viajes Activos Sin Liquidación
                    </button>
                </div>
            </div>
        </div>

        <!-- ==================== SECCIÓN LIQUIDACIONES ==================== -->
        <div id="seccionLiquidaciones">

            <!-- Filtros -->
            <div class="section-card mb-4">
                <div class="section-header">
                    <h5 class="section-title">
                        <i class="fas fa-filter mr-2"></i>Filtros de Búsqueda
                    </h5>
                </div>
                <div class="section-body">
                    <div class="row">
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">Fecha Desde</label>
                                <asp:TextBox ID="txtFechaDesde" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">Fecha Hasta</label>
                                <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">Factor de Conversión ($ a S/)</label>
                                <div class="input-group">
                                    <div class="input-group-prepend">
                                        <span class="input-group-text">S/</span>
                                    </div>
                                    <asp:TextBox ID="txtFactorConversion" runat="server" CssClass="form-control" Text="3.75" step="0.01"></asp:TextBox>
                                </div>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">&nbsp;</label>
                                <div>
                                    <asp:Button ID="btnBuscarLiquidaciones" runat="server" Text="Buscar"
                                        CssClass="btn btn-primary-custom btn-block" OnClick="btnBuscarLiquidaciones_Click" />
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="row mt-2">
                        <div class="col-md-12">
                            <div class="d-flex" style="gap:0.5rem;">
                                <button type="button" class="btn btn-export btn-export-excel" onclick="exportarLiquidaciones()">
                                    <i class="fas fa-file-excel mr-1"></i>Exportar Excel
                                </button>
                                <button type="button" class="btn btn-export btn-export-pdf" onclick="generarPDFLiquidaciones()">
                                    <i class="fas fa-file-pdf mr-1"></i>Exportar PDF
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Resumen Financiero -->
            <div class="section-card mb-4">
                <div class="section-header section-header-success">
                    <h5 class="section-title mb-0">
                        <i class="fas fa-calculator mr-2"></i>Resumen Total en Soles
                    </h5>
                </div>
                <div class="section-body">
                    <div class="row">
                        <div class="col-md-3">
                            <div class="summary-card summary-card-blue">
                                <div class="summary-icon"><i class="fas fa-coins"></i></div>
                                <div class="summary-info">
                                    <span class="summary-label">Total en Soles</span>
                                    <span class="summary-amount">
                                        <asp:Label ID="lblResumenTotalSoles" runat="server" Text="S/ 0.00"></asp:Label>
                                    </span>
                                </div>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="summary-card summary-card-green">
                                <div class="summary-icon"><i class="fas fa-dollar-sign"></i></div>
                                <div class="summary-info">
                                    <span class="summary-label">Total en Dólares</span>
                                    <span class="summary-amount">
                                        <asp:Label ID="lblResumenTotalDolares" runat="server" Text="$ 0.00"></asp:Label>
                                    </span>
                                </div>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="summary-card summary-card-cyan">
                                <div class="summary-icon"><i class="fas fa-exchange-alt"></i></div>
                                <div class="summary-info">
                                    <span class="summary-label">Conversión a Soles</span>
                                    <span class="summary-amount">
                                        <asp:Label ID="lblResumenConversion" runat="server" Text="S/ 0.00"></asp:Label>
                                    </span>
                                </div>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="summary-card summary-card-total">
                                <div class="summary-icon"><i class="fas fa-wallet"></i></div>
                                <div class="summary-info">
                                    <span class="summary-label">TOTAL GENERAL</span>
                                    <span class="summary-amount summary-amount-total">
                                        <asp:Label ID="lblResumenTotal" runat="server" Text="S/ 0.00"></asp:Label>
                                    </span>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="alert alert-info-light mt-3 mb-0">
                        <i class="fas fa-info-circle mr-2"></i>
                        <strong>Leyenda:</strong>
                        <span class="ml-3 monto-descuento">● Rojo = Descuento Neto</span>
                        <span class="ml-3 monto-reintegro">● Azul = Reintegro Neto</span>
                        <span class="ml-3 text-dark">● Negro = Cero</span>
                    </div>
                </div>
            </div>

            <!-- Tabla de Liquidaciones -->
            <div class="section-card mb-4">
                <div class="section-header section-header-info">
                    <div class="d-flex justify-content-between align-items-center">
                        <h5 class="section-title mb-0">
                            <i class="fas fa-table mr-2"></i>Liquidaciones Registradas
                        </h5>
                        <span class="badge-count">
                            <asp:Label ID="lblTotalRegistrosTabla" runat="server" Text="0 registros"></asp:Label>
                        </span>
                    </div>
                </div>
                <div class="section-body p-0">
                    <div class="table-responsive">
                        <asp:GridView ID="gvLiquidaciones" runat="server"
                            CssClass="table table-reportes mb-0"
                            AutoGenerateColumns="false"
                            EmptyDataText="No hay liquidaciones para mostrar"
                            OnRowDataBound="gvLiquidaciones_RowDataBound"
                            ShowFooter="true">
                            <Columns>
                                <asp:BoundField DataField="DNI" HeaderText="DNI" />
                                <asp:BoundField DataField="Conductor" HeaderText="CONDUCTOR" />
                                <asp:BoundField DataField="FechaSalida" HeaderText="FECHA" DataFormatString="{0:dd/MM/yyyy}" />
                                <asp:BoundField DataField="NumeroLiquidacion" HeaderText="N° DE LIQ" />
                                <asp:TemplateField HeaderText="MONTO S/ (Reintegro-Descuento)">
                                    <ItemTemplate>
                                        <span class='<%# ObtenerClaseMontoSoles(Eval("MontoSoles")) %>'>
                                            <%# FormatearMontoSoles(Eval("MontoSoles")) %>
                                        </span>
                                    </ItemTemplate>
                                    <FooterTemplate>
                                        <strong>TOTAL: <asp:Label ID="lblTotalSoles" runat="server"></asp:Label></strong>
                                    </FooterTemplate>
                                    <ItemStyle CssClass="text-right" />
                                    <FooterStyle CssClass="text-right footer-total" />
                                </asp:TemplateField>
                                <asp:TemplateField HeaderText="MONTO $ (Reintegro-Descuento)">
                                    <ItemTemplate>
                                        <span class='<%# ObtenerClaseMontoDolares(Eval("MontoDolares")) %>'>
                                            <%# FormatearMontoDolares(Eval("MontoDolares")) %>
                                        </span>
                                    </ItemTemplate>
                                    <FooterTemplate>
                                        <strong>TOTAL: <asp:Label ID="lblTotalDolares" runat="server"></asp:Label></strong>
                                    </FooterTemplate>
                                    <ItemStyle CssClass="text-right" />
                                    <FooterStyle CssClass="text-right footer-total" />
                                </asp:TemplateField>
                                <asp:TemplateField HeaderText="ACCIONES">
                                    <ItemTemplate>
                                        <button type="button" class="btn btn-info-action" onclick='verDetalleOrden(<%# Eval("IdOrdenViaje") %>)' title="Ver Detalle">
                                            <i class="fas fa-eye"></i>
                                        </button>
                                    </ItemTemplate>
                                    <ItemStyle CssClass="text-center" />
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>
            </div>

        </div>

        <!-- ==================== SECCIÓN VIAJES ACTIVOS ==================== -->
        <div id="seccionViajesActivos" style="display: none;">

            <!-- Filtros -->
            <div class="section-card mb-4">
                <div class="section-header section-header-warning">
                    <h5 class="section-title">
                        <i class="fas fa-search mr-2"></i>Búsqueda de Viajes Activos
                    </h5>
                </div>
                <div class="section-body">
                    <div class="row">
                        <div class="col-md-5">
                            <div class="form-group">
                                <label class="form-label">Buscar Conductor</label>
                                <asp:TextBox ID="txtBuscarConductor" runat="server" CssClass="form-control"
                                    placeholder="Nombre, apellido o DNI"></asp:TextBox>
                            </div>
                        </div>
                        <div class="col-md-4">
                            <div class="form-group">
                                <label class="form-label">Estado del Viaje</label>
                                <asp:DropDownList ID="ddlEstadoViaje" runat="server" CssClass="form-control">
                                    <asp:ListItem Value="TODOS" Text="Todos los estados"></asp:ListItem>
                                    <asp:ListItem Value="ABIERTO" Text="Abierto" Selected="True"></asp:ListItem>
                                    <asp:ListItem Value="CERRADO" Text="Cerrado"></asp:ListItem>
                                </asp:DropDownList>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">&nbsp;</label>
                                <div>
                                    <asp:Button ID="btnBuscarViajesActivos" runat="server" Text="Buscar"
                                        CssClass="btn btn-primary-custom btn-block" OnClick="btnBuscarViajesActivos_Click" />
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="row mt-2">
                        <div class="col-md-12">
                            <button type="button" class="btn btn-export btn-export-excel" onclick="exportarViajesActivos()">
                                <i class="fas fa-file-excel mr-1"></i>Exportar Excel
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Alerta -->
            <div class="alert alert-info-light mb-4">
                <i class="fas fa-exclamation-triangle mr-2 text-warning"></i>
                <strong>Conductores con viajes sin liquidar:</strong>
                <asp:Label ID="lblTotalViajesActivos" runat="server" Text="0 conductores"></asp:Label>
                tienen viajes activos pendientes de liquidación.
            </div>

            <!-- Tabla -->
            <div class="section-card">
                <div class="section-header section-header-warning">
                    <div class="d-flex justify-content-between align-items-center">
                        <h5 class="section-title mb-0">
                            <i class="fas fa-truck mr-2"></i>Conductores con Viajes Sin Liquidación
                        </h5>
                        <span class="badge-count">
                            <asp:Label ID="lblCountViajesActivosTabla" runat="server" Text="0 viajes"></asp:Label>
                        </span>
                    </div>
                </div>
                <div class="section-body p-0">
                    <div class="table-responsive">
                        <asp:GridView ID="gvViajesActivos" runat="server"
                            CssClass="table table-reportes mb-0"
                            AutoGenerateColumns="false"
                            EmptyDataText="No hay viajes activos sin liquidación"
                            OnRowDataBound="gvViajesActivos_RowDataBound">
                            <Columns>
                                <asp:BoundField DataField="DNI" HeaderText="DNI" />
                                <asp:BoundField DataField="Conductor" HeaderText="CONDUCTOR" />
                                <asp:BoundField DataField="PlacaTracto" HeaderText="TRACTO" />
                                <asp:BoundField DataField="PlacaCarreta" HeaderText="CARRETA" />
                                <asp:BoundField DataField="Cliente" HeaderText="CLIENTE" />
                                <asp:BoundField DataField="Destino" HeaderText="DESTINO" />
                                <asp:TemplateField HeaderText="FECHA PROGRAMACIÓN">
                                    <ItemTemplate>
                                        <%# Eval("FechaProgramacion") != DBNull.Value ? Convert.ToDateTime(Eval("FechaProgramacion")).ToString("dd/MM/yyyy") : "N/A" %>
                                    </ItemTemplate>
                                    <ItemStyle CssClass="text-center" />
                                </asp:TemplateField>
                                <asp:TemplateField HeaderText="DÍAS EN VIAJE">
                                    <ItemTemplate>
                                        <span class='<%# Convert.ToInt32(Eval("DiasEnViaje")) > 7 ? "badge-dias-alerta" : "badge-dias-normal" %>'>
                                            <%# Eval("DiasEnViaje") %> días
                                        </span>
                                    </ItemTemplate>
                                    <ItemStyle CssClass="text-center" />
                                </asp:TemplateField>
                                <asp:TemplateField HeaderText="ESTADO">
                                    <ItemTemplate>
                                        <span class='<%# "badge-estado badge-estado-" + Eval("Estado").ToString().ToLower() %>'>
                                            <%# Eval("Estado") %>
                                        </span>
                                    </ItemTemplate>
                                    <ItemStyle CssClass="text-center" />
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>
            </div>

        </div>

    </div>

    <!-- Modal Detalle de Orden -->
    <div class="modal fade" id="modalDetalleOrden" tabindex="-1" role="dialog">
        <div class="modal-dialog modal-xl" role="document">
            <div class="modal-content">
                <div class="modal-header modal-header-info">
                    <h5 class="modal-title">
                        <i class="fas fa-file-invoice mr-2"></i>Detalle de Orden de Viaje
                    </h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <div id="detalleOrdenContent">
                        <div class="text-center py-5">
                            <div class="spinner-border text-primary" role="status">
                                <span class="sr-only">Cargando...</span>
                            </div>
                            <p class="mt-3 text-muted">Cargando información...</p>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary-custom" data-dismiss="modal">Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <style>
        :root {
            --rpt-primary: #2563eb;
            --rpt-success: #059669;
            --rpt-danger: #dc2626;
            --rpt-warning: #d97706;
            --rpt-info: #0891b2;
            --rpt-neutral: #64748b;
            --rpt-light-gray: #f8fafc;
            --rpt-medium-gray: #e2e8f0;
            --rpt-border: #cbd5e1;
        }

        /* === PAGE HEADER === */
        .page-header {
            padding-bottom: 1.5rem;
            border-bottom: 2px solid var(--rpt-border);
        }

        .page-title {
            color: #1e293b;
            font-weight: 600;
            font-size: 1.75rem;
        }

        .header-stats {
            display: flex;
        }

        .stat-card {
            background: white;
            border: 2px solid var(--rpt-border);
            border-radius: 0.5rem;
            padding: 1rem 1.25rem;
            display: flex;
            align-items: center;
            min-width: 150px;
        }

        .stat-card.stat-primary {
            border-color: #93c5fd;
            background: linear-gradient(135deg, #eff6ff 0%, #dbeafe 100%);
        }

        .stat-card.stat-warning {
            border-color: #fbbf24;
            background: linear-gradient(135deg, #fef3c7 0%, #fde68a 100%);
        }

        .stat-icon {
            font-size: 2rem;
            margin-right: 1rem;
            color: #1e40af;
        }

        .stat-card.stat-warning .stat-icon {
            color: #78350f;
        }

        .stat-info {
            display: flex;
            flex-direction: column;
        }

        .stat-label {
            font-size: 0.75rem;
            font-weight: 600;
            text-transform: uppercase;
            color: var(--rpt-neutral);
        }

        .stat-value {
            font-size: 1.875rem;
            font-weight: 700;
            color: #1e293b;
        }

        /* === TABS === */
        .tabs-navigation {
            display: flex;
            gap: 0.5rem;
            border-bottom: 2px solid var(--rpt-border);
            padding-bottom: 0;
        }

        .tab-btn {
            padding: 0.75rem 1.5rem;
            border: 1px solid var(--rpt-border);
            border-bottom: none;
            border-radius: 0.5rem 0.5rem 0 0;
            background: white;
            color: var(--rpt-neutral);
            font-weight: 600;
            font-size: 0.9375rem;
            cursor: pointer;
            transition: all 0.2s;
            position: relative;
            top: 2px;
        }

        .tab-btn:hover {
            background: var(--rpt-light-gray);
            color: #1e293b;
        }

        .tab-btn-active {
            background: white;
            color: var(--rpt-primary);
            border-color: var(--rpt-border);
            border-bottom: 2px solid white;
        }

        /* === SECTION CARDS === */
        .section-card {
            background: white;
            border: 1px solid var(--rpt-border);
            border-radius: 0.5rem;
            overflow: hidden;
        }

        .section-header {
            background: var(--rpt-light-gray);
            padding: 1rem 1.25rem;
            border-bottom: 1px solid var(--rpt-border);
        }

        .section-header-success {
            background: #f0fdf4;
            border-bottom-color: #86efac;
        }

        .section-header-info {
            background: #ecfeff;
            border-bottom-color: #67e8f9;
        }

        .section-header-warning {
            background: #fef3c7;
            border-bottom-color: #fbbf24;
        }

        .section-title {
            color: #1e293b;
            font-size: 1.125rem;
            font-weight: 600;
            margin: 0;
        }

        .section-body {
            padding: 1.5rem;
        }

        /* === SUMMARY CARDS === */
        .summary-card {
            background: white;
            border: 2px solid var(--rpt-border);
            border-radius: 0.5rem;
            padding: 1.25rem;
            display: flex;
            align-items: center;
            transition: transform 0.2s;
        }

        .summary-card:hover {
            transform: translateY(-2px);
        }

        .summary-card-blue {
            border-color: #93c5fd;
            background: #eff6ff;
        }

        .summary-card-green {
            border-color: #86efac;
            background: #f0fdf4;
        }

        .summary-card-cyan {
            border-color: #67e8f9;
            background: #ecfeff;
        }

        .summary-card-total {
            border-color: #059669;
            background: #ecfdf5;
            border-width: 3px;
        }

        .summary-icon {
            font-size: 1.75rem;
            margin-right: 1rem;
            color: var(--rpt-neutral);
        }

        .summary-card-blue .summary-icon { color: #2563eb; }
        .summary-card-green .summary-icon { color: #059669; }
        .summary-card-cyan .summary-icon { color: #0891b2; }
        .summary-card-total .summary-icon { color: #059669; }

        .summary-info {
            display: flex;
            flex-direction: column;
        }

        .summary-label {
            font-size: 0.75rem;
            font-weight: 600;
            text-transform: uppercase;
            color: var(--rpt-neutral);
            letter-spacing: 0.025em;
        }

        .summary-amount {
            font-size: 1.5rem;
            font-weight: 800;
            color: #1e293b;
        }

        .summary-amount-total {
            color: var(--rpt-success);
        }

        /* === TABLES === */
        .table-reportes {
            font-size: 0.875rem;
        }

        .table-reportes thead th {
            background: var(--rpt-light-gray);
            color: #1e293b;
            font-weight: 600;
            font-size: 0.8125rem;
            text-transform: uppercase;
            letter-spacing: 0.025em;
            padding: 0.875rem 0.75rem;
            border-bottom: 2px solid var(--rpt-border);
            white-space: nowrap;
            text-align: center;
            vertical-align: middle;
        }

        .table-reportes tbody td {
            vertical-align: middle;
            padding: 0.875rem 0.75rem;
            border-bottom: 1px solid var(--rpt-medium-gray);
        }

        .table-reportes tbody tr:hover {
            background: var(--rpt-light-gray);
        }

        .table-reportes tfoot td {
            background: #f1f5f9;
            font-weight: 700;
            padding: 1rem 0.75rem;
            border-top: 2px solid var(--rpt-border);
        }

        .footer-total {
            background: #f1f5f9 !important;
            font-weight: 700 !important;
        }

        /* === MONTOS === */
        .monto-descuento {
            color: var(--rpt-danger) !important;
            font-weight: 700 !important;
        }

        .monto-reintegro {
            color: var(--rpt-primary) !important;
            font-weight: 700 !important;
        }

        .monto-normal {
            color: #1e293b;
            font-weight: 500;
        }

        /* === BADGES === */
        .badge-count {
            background: white;
            border: 1px solid var(--rpt-border);
            padding: 0.375rem 0.75rem;
            border-radius: 0.375rem;
            font-size: 0.8125rem;
            font-weight: 600;
            color: var(--rpt-neutral);
        }

        .badge-estado {
            padding: 0.375rem 0.75rem;
            border-radius: 1rem;
            font-weight: 600;
            font-size: 0.75rem;
            display: inline-block;
        }

        .badge-estado-abierto {
            background: #fef3c7;
            color: #92400e;
            border: 1px solid #fbbf24;
        }

        .badge-estado-cerrado {
            background: #f1f5f9;
            color: #475569;
            border: 1px solid #cbd5e1;
        }

        .badge-dias-normal {
            background: #dbeafe;
            color: #1e40af;
            padding: 0.375rem 0.75rem;
            border-radius: 0.375rem;
            font-weight: 600;
            font-size: 0.8125rem;
        }

        .badge-dias-alerta {
            background: #fecaca;
            color: #991b1b;
            padding: 0.375rem 0.75rem;
            border-radius: 0.375rem;
            font-weight: 600;
            font-size: 0.8125rem;
            border: 1px solid #f87171;
        }

        /* === BUTTONS === */
        .btn-primary-custom {
            background: var(--rpt-primary);
            border: none;
            color: white;
            font-weight: 500;
            padding: 0.5rem 1rem;
            border-radius: 0.375rem;
        }

        .btn-primary-custom:hover {
            background: #1d4ed8;
            color: white;
        }

        .btn-secondary-custom {
            background: white;
            border: 1px solid var(--rpt-border);
            color: var(--rpt-neutral);
            font-weight: 500;
            border-radius: 0.375rem;
        }

        .btn-export {
            padding: 0.375rem 0.75rem;
            font-size: 0.8125rem;
            font-weight: 600;
            border: 1px solid;
            border-radius: 0.375rem;
            cursor: pointer;
            transition: all 0.2s;
        }

        .btn-export-excel {
            background: #f0fdf4;
            color: #065f46;
            border-color: #86efac;
        }

        .btn-export-excel:hover {
            background: #dcfce7;
            transform: translateY(-1px);
        }

        .btn-export-pdf {
            background: #fef2f2;
            color: #991b1b;
            border-color: #fca5a5;
        }

        .btn-export-pdf:hover {
            background: #fee2e2;
            transform: translateY(-1px);
        }

        .btn-info-action {
            padding: 0.5rem 0.75rem;
            border: none;
            border-radius: 0.375rem;
            font-size: 0.875rem;
            cursor: pointer;
            transition: all 0.2s;
            color: white;
            background: var(--rpt-info);
        }

        .btn-info-action:hover {
            background: #0e7490;
            transform: translateY(-1px);
        }

        /* === ALERTS === */
        .alert-info-light {
            background: #f0f9ff;
            border: 1px solid #bae6fd;
            border-radius: 0.5rem;
            padding: 0.875rem 1.25rem;
            font-size: 0.875rem;
            color: #0c4a6e;
        }

        /* === MODAL === */
        .modal-header-info {
            background: #e0f2fe;
            border-bottom: 2px solid #7dd3fc;
        }

        /* === FORMS === */
        .form-label {
            font-weight: 600;
            color: #475569;
            margin-bottom: 0.5rem;
            font-size: 0.875rem;
        }

        .form-control {
            border: 1px solid var(--rpt-border);
            border-radius: 0.375rem;
            padding: 0.5rem 0.75rem;
            font-size: 0.875rem;
            transition: border-color 0.2s;
        }

        .form-control:focus {
            border-color: var(--rpt-primary);
            box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
        }

        .input-group-text {
            background: var(--rpt-light-gray);
            border: 1px solid var(--rpt-border);
            font-weight: 600;
            color: var(--rpt-neutral);
        }

        /* === RESPONSIVE === */
        @media (max-width: 768px) {
            .page-header {
                text-align: center;
            }

            .header-stats {
                flex-direction: column;
                margin-top: 1rem;
            }

            .stat-card {
                margin-left: 0 !important;
                margin-bottom: 0.5rem;
            }

            .tabs-navigation {
                flex-direction: column;
            }

            .tab-btn {
                border-radius: 0.5rem;
                top: 0;
                border-bottom: 1px solid var(--rpt-border);
            }

            .summary-card {
                margin-bottom: 0.75rem;
            }

            .table-reportes {
                font-size: 0.75rem;
            }

            .table-reportes thead th {
                font-size: 0.6875rem;
                padding: 0.75rem 0.5rem;
            }
        }
    </style>

    <script type="text/javascript">
        function cambiarTab(tab) {
            if (tab === 'liquidaciones') {
                $('#seccionLiquidaciones').show();
                $('#seccionViajesActivos').hide();
                $('#tabLiquidaciones').addClass('tab-btn-active');
                $('#tabViajesActivos').removeClass('tab-btn-active');
            } else {
                $('#seccionLiquidaciones').hide();
                $('#seccionViajesActivos').show();
                $('#tabLiquidaciones').removeClass('tab-btn-active');
                $('#tabViajesActivos').addClass('tab-btn-active');
            }
        }

        $(document).ready(function () {
            establecerFechasPorDefecto();
        });

        function establecerFechasPorDefecto() {
            var hoy = new Date();
            var primerDia = new Date(hoy.getFullYear(), hoy.getMonth(), 1);

            if (!$('#<%= txtFechaDesde.ClientID %>').val()) {
                $('#<%= txtFechaDesde.ClientID %>').val(primerDia.toISOString().split('T')[0]);
            }

            if (!$('#<%= txtFechaHasta.ClientID %>').val()) {
                $('#<%= txtFechaHasta.ClientID %>').val(hoy.toISOString().split('T')[0]);
            }
        }

        function verDetalleOrden(idOrden) {
            $('#modalDetalleOrden').modal('show');
            $('#detalleOrdenContent').html('<div class="text-center py-5"><div class="spinner-border text-primary" role="status"><span class="sr-only">Cargando...</span></div><p class="mt-3 text-muted">Cargando información...</p></div>');

            $.ajax({
                url: 'ReportesOrdenesViaje.aspx/ObtenerDetalleOrden',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({ idOrden: idOrden }),
                success: function (response) {
                    $('#detalleOrdenContent').html(response.d);
                },
                error: function (xhr, status, error) {
                    $('#detalleOrdenContent').html('<div class="alert alert-danger"><i class="fas fa-exclamation-triangle mr-2"></i>Error al cargar el detalle: ' + error + '</div>');
                }
            });
        }

        function exportarLiquidaciones() {
            var fechaDesde = $('#<%= txtFechaDesde.ClientID %>').val();
            var fechaHasta = $('#<%= txtFechaHasta.ClientID %>').val();
            var factorConversion = $('#<%= txtFactorConversion.ClientID %>').val() || '3.75';

            if (!fechaDesde || !fechaHasta) {
                alert('Por favor seleccione el rango de fechas');
                return;
            }

            window.location.href = 'ReportesOrdenesViaje.aspx?action=exportarLiquidaciones&fechaDesde=' + fechaDesde + '&fechaHasta=' + fechaHasta + '&factor=' + factorConversion;
        }

        function exportarViajesActivos() {
            var buscarConductor = $('#<%= txtBuscarConductor.ClientID %>').val();
            var estadoViaje = $('#<%= ddlEstadoViaje.ClientID %>').val();

            window.location.href = 'ReportesOrdenesViaje.aspx?action=exportarViajesActivos&buscarConductor=' + encodeURIComponent(buscarConductor) + '&estadoViaje=' + estadoViaje;
        }

        function generarPDFLiquidaciones() {
            var fechaDesde = $('#<%= txtFechaDesde.ClientID %>').val();
            var fechaHasta = $('#<%= txtFechaHasta.ClientID %>').val();
            var factorConversion = $('#<%= txtFactorConversion.ClientID %>').val() || '3.75';

            if (!fechaDesde || !fechaHasta) {
                alert('Por favor seleccione el rango de fechas');
                return;
            }

            window.location.href = 'ReportesOrdenesViaje.aspx?action=generarPDF&fechaDesde=' + fechaDesde + '&fechaHasta=' + fechaHasta + '&factor=' + factorConversion;
        }
    </script>

</asp:Content>

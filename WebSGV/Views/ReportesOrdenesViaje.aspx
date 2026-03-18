<%@ Page Title="Reportes de Órdenes de Viaje" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReportesOrdenesViaje.aspx.cs" Inherits="WebSGV.Views.ReportesOrdenesViaje" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <!-- Panel de mensajes -->
    <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-4">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-4">

        <!-- Header -->
        <div class="row mb-4">
            <div class="col-12">
                <div class="page-header-box">
                    <div>
                        <h2 class="page-title">
                            <i class="fas fa-file-invoice-dollar"></i> Reportes de Órdenes de Viaje
                        </h2>
                        <p class="page-subtitle">Consulte liquidaciones y viajes activos del sistema</p>
                    </div>
                    <a href="ListaDespachos.aspx" class="btn btn-outline-light">
                        <i class="fas fa-arrow-left"></i> Volver
                    </a>
                </div>
            </div>
        </div>

        <!-- PESTAÑAS PRINCIPALES -->
        <div class="row">
            <div class="col-12">
                <ul class="nav nav-tabs nav-tabs-modern mb-0" id="reportesTabs" role="tablist">
                    <li class="nav-item">
                        <a class="nav-link active" id="liquidaciones-tab" data-toggle="tab" href="#liquidaciones" role="tab">
                            <i class="fas fa-money-check-alt"></i> Liquidaciones
                        </a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link" id="viajesActivos-tab" data-toggle="tab" href="#viajesActivos" role="tab">
                            <i class="fas fa-truck-loading"></i> Viajes Activos Sin Liquidación
                        </a>
                    </li>
                </ul>

                <div class="tab-content tab-content-modern" id="reportesTabsContent">

                    <!-- ============================================ -->
                    <!-- TAB 1: LIQUIDACIONES -->
                    <!-- ============================================ -->
                    <div class="tab-pane fade show active" id="liquidaciones" role="tabpanel">
                        <div class="p-4">

                            <!-- Filtros -->
                            <div class="card card-modern mb-4">
                                <div class="card-header card-header-primary">
                                    <h5 class="card-title">
                                        <i class="fas fa-filter"></i> Filtros de Búsqueda
                                    </h5>
                                </div>
                                <div class="card-body">
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
                                                        CssClass="btn btn-primary btn-block" OnClick="btnBuscarLiquidaciones_Click" />
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                    <div class="row">
                                        <div class="col-md-12">
                                            <button type="button" class="btn btn-success btn-sm mr-2" onclick="exportarLiquidaciones()">
                                                <i class="fas fa-file-excel"></i> Exportar Excel
                                            </button>
                                            <button type="button" class="btn btn-danger btn-sm" onclick="generarPDFLiquidaciones()">
                                                <i class="fas fa-file-pdf"></i> Exportar PDF
                                            </button>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <!-- Resumen Financiero -->
                            <div class="card card-modern mb-4">
                                <div class="card-header card-header-success">
                                    <h5 class="card-title">
                                        <i class="fas fa-calculator"></i> Resumen Total en Soles
                                    </h5>
                                </div>
                                <div class="card-body">
                                    <div class="row">
                                        <div class="col-md-3">
                                            <div class="summary-box summary-box-primary">
                                                <div class="summary-label">Total en Soles</div>
                                                <div class="summary-value">
                                                    <asp:Label ID="lblResumenTotalSoles" runat="server" Text="S/ 0.00"></asp:Label>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="col-md-3">
                                            <div class="summary-box summary-box-success">
                                                <div class="summary-label">Total en Dólares</div>
                                                <div class="summary-value">
                                                    <asp:Label ID="lblResumenTotalDolares" runat="server" Text="$ 0.00"></asp:Label>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="col-md-3">
                                            <div class="summary-box summary-box-info">
                                                <div class="summary-label">Conversión a Soles</div>
                                                <div class="summary-value">
                                                    <asp:Label ID="lblResumenConversion" runat="server" Text="S/ 0.00"></asp:Label>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="col-md-3">
                                            <div class="summary-box summary-box-total">
                                                <div class="summary-label">TOTAL GENERAL</div>
                                                <div class="summary-value-total">
                                                    <asp:Label ID="lblResumenTotal" runat="server" Text="S/ 0.00"></asp:Label>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                    <div class="row mt-3">
                                        <div class="col-md-12">
                                            <div class="alert alert-info mb-0">
                                                <i class="fas fa-info-circle"></i>
                                                <strong>Leyenda:</strong>
                                                <span class="ml-3 text-danger font-weight-bold">● Rojo = Descuento Neto</span>
                                                <span class="ml-3 text-primary font-weight-bold">● Azul = Reintegro Neto</span>
                                                <span class="ml-3">● Negro = Cero</span>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <!-- Tabla de Liquidaciones -->
                            <div class="card card-modern mb-4">
                                <div class="card-header card-header-info">
                                    <div class="d-flex justify-content-between align-items-center">
                                        <h5 class="card-title mb-0">
                                            <i class="fas fa-table"></i> Liquidaciones Registradas
                                        </h5>
                                        <span class="badge badge-light badge-lg">
                                            <asp:Label ID="lblTotalRegistros" runat="server" Text="0 registros"></asp:Label>
                                        </span>
                                    </div>
                                </div>
                                <div class="card-body p-0">
                                    <div class="table-responsive">
                                        <asp:GridView ID="gvLiquidaciones" runat="server"
                                            CssClass="table table-modern table-hover mb-0"
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
                                                    <FooterStyle CssClass="text-right bg-light" />
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
                                                    <FooterStyle CssClass="text-right bg-light" />
                                                </asp:TemplateField>
                                                <asp:TemplateField HeaderText="ACCIONES">
                                                    <ItemTemplate>
                                                        <button type="button" class="btn btn-info btn-sm" onclick='verDetalleOrden(<%# Eval("IdOrdenViaje") %>)'>
                                                            <i class="fas fa-eye"></i>
                                                        </button>
                                                    </ItemTemplate>
                                                    <ItemStyle CssClass="text-center" />
                                                </asp:TemplateField>
                                            </Columns>
                                            <HeaderStyle CssClass="thead-dark" />
                                        </asp:GridView>
                                    </div>
                                </div>
                            </div>

                        </div>
                    </div>

                    <!-- ============================================ -->
                    <!-- TAB 2: VIAJES ACTIVOS SIN LIQUIDACIÓN -->
                    <!-- ============================================ -->
                    <div class="tab-pane fade" id="viajesActivos" role="tabpanel">
                        <div class="p-4">

                            <!-- Filtros Viajes Activos -->
                            <div class="card card-modern mb-4">
                                <div class="card-header card-header-warning">
                                    <h5 class="card-title">
                                        <i class="fas fa-search"></i> Búsqueda de Viajes Activos
                                    </h5>
                                </div>
                                <div class="card-body">
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
                                                        CssClass="btn btn-warning btn-block" OnClick="btnBuscarViajesActivos_Click" />
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                    <div class="row">
                                        <div class="col-md-12">
                                            <button type="button" class="btn btn-success btn-sm" onclick="exportarViajesActivos()">
                                                <i class="fas fa-file-excel"></i> Exportar Excel
                                            </button>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <!-- Alerta de Resumen -->
                            <div class="alert alert-warning alert-modern mb-4">
                                <i class="fas fa-exclamation-triangle"></i>
                                <strong>Conductores con viajes sin liquidar:</strong>
                                <asp:Label ID="lblTotalViajesActivos" runat="server" Text="0 conductores"></asp:Label>
                                tienen viajes activos pendientes de liquidación.
                            </div>

                            <!-- Tabla de Viajes Activos -->
                            <div class="card card-modern">
                                <div class="card-header card-header-warning">
                                    <div class="d-flex justify-content-between align-items-center">
                                        <h5 class="card-title mb-0">
                                            <i class="fas fa-truck"></i> Conductores con Viajes Sin Liquidación
                                        </h5>
                                        <span class="badge badge-light badge-lg">
                                            <asp:Label ID="lblCountViajesActivos" runat="server" Text="0 viajes"></asp:Label>
                                        </span>
                                    </div>
                                </div>
                                <div class="card-body p-0">
                                    <div class="table-responsive">
                                        <asp:GridView ID="gvViajesActivos" runat="server"
                                            CssClass="table table-modern table-hover mb-0"
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
                                                <asp:BoundField DataField="DiasEnViaje" HeaderText="DÍAS EN VIAJE" />
                                                <asp:TemplateField HeaderText="ESTADO">
                                                    <ItemTemplate>
                                                        <span class='<%# "badge " + ObtenerClaseEstado(Eval("Estado")) %>'>
                                                            <%# Eval("Estado") %>
                                                        </span>
                                                    </ItemTemplate>
                                                    <ItemStyle CssClass="text-center" />
                                                </asp:TemplateField>
                                            </Columns>
                                            <HeaderStyle CssClass="thead-dark" />
                                        </asp:GridView>
                                    </div>
                                </div>
                            </div>

                        </div>
                    </div>

                </div>
            </div>
        </div>

    </div>

    <!-- Modal Detalle de Orden -->
    <div class="modal fade" id="modalDetalleOrden" tabindex="-1" role="dialog">
        <div class="modal-dialog modal-xl" role="document">
            <div class="modal-content">
                <div class="modal-header bg-primary text-white">
                    <h5 class="modal-title">
                        <i class="fas fa-file-invoice"></i> Detalle de Orden de Viaje
                    </h5>
                    <button type="button" class="close text-white" data-dismiss="modal" aria-label="Close">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <div id="detalleOrdenContent">
                        <div class="text-center py-5">
                            <div class="spinner-border text-primary" role="status">
                                <span class="sr-only">Cargando...</span>
                            </div>
                            <p class="mt-3">Cargando información...</p>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <!-- CSS MODERNO Y FUNCIONAL -->
    <style>
        /* === PAGE HEADER === */
        .page-header-box {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 1.5rem;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border-radius: 12px;
            box-shadow: 0 4px 15px rgba(102, 126, 234, 0.4);
            margin-bottom: 2rem;
        }

        .page-title {
            color: white;
            font-size: 1.75rem;
            font-weight: 700;
            margin: 0;
        }

        .page-subtitle {
            color: rgba(255, 255, 255, 0.9);
            font-size: 1rem;
            margin: 0.25rem 0 0 0;
        }

        /* === PESTAÑAS === */
        .nav-tabs-modern {
            border-bottom: 3px solid #e9ecef;
            background-color: #f8f9fa;
            border-radius: 8px 8px 0 0;
            padding: 0.5rem 1rem 0 1rem;
        }

        .nav-tabs-modern .nav-link {
            color: #6c757d;
            background-color: transparent;
            border: none;
            border-bottom: 3px solid transparent;
            padding: 1rem 1.5rem;
            font-weight: 600;
            font-size: 1rem;
            transition: all 0.3s ease;
        }

        .nav-tabs-modern .nav-link:hover {
            color: #495057;
            background-color: #e9ecef;
            border-bottom-color: #dee2e6;
        }

        .nav-tabs-modern .nav-link.active {
            color: #495057;
            background-color: white;
            border-bottom-color: #007bff;
        }

        .tab-content-modern {
            background-color: white;
            border: 1px solid #dee2e6;
            border-top: none;
            border-radius: 0 0 8px 8px;
            min-height: 500px;
        }

        /* === CARDS === */
        .card-modern {
            border: 1px solid #dee2e6;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0, 0, 0, 0.05);
            overflow: hidden;
        }

        .card-header {
            border-bottom: 2px solid #dee2e6;
            padding: 1rem 1.5rem;
        }

        .card-header-primary {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }

        .card-header-success {
            background: linear-gradient(135deg, #56ab2f 0%, #a8e063 100%);
            color: white;
        }

        .card-header-info {
            background: linear-gradient(135deg, #2193b0 0%, #6dd5ed 100%);
            color: white;
        }

        .card-header-warning {
            background: linear-gradient(135deg, #f09819 0%, #ff512f 100%);
            color: white;
        }

        .card-title {
            margin: 0;
            font-size: 1.125rem;
            font-weight: 600;
        }

        .card-body {
            padding: 1.5rem;
        }

        /* === BOTONES === */
        .btn {
            font-weight: 500;
            padding: 0.5rem 1.25rem;
            border-radius: 6px;
            transition: all 0.3s ease;
        }

        .btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
        }

        .btn-primary {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border: none;
        }

        .btn-success {
            background: linear-gradient(135deg, #56ab2f 0%, #a8e063 100%);
            border: none;
        }

        .btn-danger {
            background: linear-gradient(135deg, #eb3349 0%, #f45c43 100%);
            border: none;
        }

        .btn-warning {
            background: linear-gradient(135deg, #f09819 0%, #ff512f 100%);
            border: none;
            color: white;
        }

        .btn-info {
            background: linear-gradient(135deg, #2193b0 0%, #6dd5ed 100%);
            border: none;
        }

        /* === TABLAS === */
        .table-modern {
            font-size: 0.875rem;
            margin-bottom: 0;
        }

        .table-modern thead th {
            background-color: #343a40 !important;
            color: white !important;
            font-weight: 600 !important;
            text-transform: uppercase;
            font-size: 0.75rem;
            letter-spacing: 0.5px;
            padding: 1rem 0.75rem !important;
            border: none !important;
            text-align: center !important;
            vertical-align: middle !important;
        }

        .table-modern tbody td {
            padding: 0.875rem 0.75rem;
            vertical-align: middle;
            border-bottom: 1px solid #e9ecef;
        }

        .table-modern tbody tr:hover {
            background-color: #f8f9fa;
        }

        .table-modern tfoot td {
            background-color: #f8f9fa;
            font-weight: 700;
            padding: 1rem 0.75rem;
            border-top: 2px solid #dee2e6;
        }

        /* === THEAD-DARK OVERRIDE === */
        .thead-dark th {
            background-color: #343a40 !important;
            color: white !important;
            font-weight: 600 !important;
            text-transform: uppercase !important;
            font-size: 0.75rem !important;
            letter-spacing: 0.5px !important;
            padding: 1rem 0.75rem !important;
            border: none !important;
            text-align: center !important;
            vertical-align: middle !important;
        }

        /* === CLASES DE MONTOS === */
        .monto-descuento {
            color: #dc3545 !important;
            font-weight: 700 !important;
            font-size: 1rem;
        }

        .monto-reintegro {
            color: #007bff !important;
            font-weight: 700 !important;
            font-size: 1rem;
        }

        .monto-normal {
            color: #212529;
            font-weight: 500;
        }

        /* === BADGES === */
        .badge-lg {
            padding: 0.5rem 1rem;
            font-size: 0.875rem;
            font-weight: 600;
        }

        .badge-estado-activo {
            background-color: #ffc107;
            color: #212529;
            padding: 0.375rem 0.75rem;
            border-radius: 20px;
            font-weight: 600;
            font-size: 0.75rem;
        }

        .badge-estado-cerrado {
            background-color: #6c757d;
            color: white;
            padding: 0.375rem 0.75rem;
            border-radius: 20px;
            font-weight: 600;
            font-size: 0.75rem;
        }

        /* === RESUMEN FINANCIERO === */
        .summary-box {
            background-color: white;
            border: 2px solid #dee2e6;
            border-radius: 8px;
            padding: 1.5rem;
            text-align: center;
            transition: all 0.3s ease;
        }

        .summary-box:hover {
            transform: translateY(-4px);
            box-shadow: 0 8px 20px rgba(0, 0, 0, 0.1);
        }

        .summary-box-primary {
            border-color: #007bff;
            background: linear-gradient(135deg, #e3f2fd 0%, #bbdefb 100%);
        }

        .summary-box-success {
            border-color: #28a745;
            background: linear-gradient(135deg, #e8f5e9 0%, #c8e6c9 100%);
        }

        .summary-box-info {
            border-color: #17a2b8;
            background: linear-gradient(135deg, #e0f7fa 0%, #b2ebf2 100%);
        }

        .summary-box-total {
            border-color: #28a745;
            background: linear-gradient(135deg, #d4edda 0%, #c3e6cb 100%);
            border-width: 3px;
        }

        .summary-label {
            font-size: 0.875rem;
            color: #6c757d;
            font-weight: 600;
            text-transform: uppercase;
            margin-bottom: 0.5rem;
            letter-spacing: 0.5px;
        }

        .summary-value {
            font-size: 1.75rem;
            font-weight: 800;
            color: #212529;
        }

        .summary-value-total {
            font-size: 2rem;
            font-weight: 900;
            color: #28a745;
        }

        /* === ALERTAS === */
        .alert-modern {
            border-radius: 8px;
            border-left: 4px solid;
            padding: 1rem 1.25rem;
            box-shadow: 0 2px 8px rgba(0, 0, 0, 0.05);
        }

        .alert-warning {
            border-left-color: #ffc107;
            background-color: #fff3cd;
        }

        .alert-info {
            border-left-color: #17a2b8;
            background-color: #d1ecf1;
        }

        /* === FORMULARIOS === */
        .form-label {
            font-weight: 600;
            color: #495057;
            margin-bottom: 0.5rem;
            font-size: 0.875rem;
        }

        .form-control {
            border: 1px solid #ced4da;
            border-radius: 6px;
            padding: 0.5rem 0.75rem;
            font-size: 0.875rem;
            transition: all 0.3s ease;
        }

        .form-control:focus {
            border-color: #80bdff;
            box-shadow: 0 0 0 0.2rem rgba(0, 123, 255, 0.25);
        }

        .input-group-text {
            background-color: #e9ecef;
            border: 1px solid #ced4da;
            font-weight: 600;
        }

        /* === UTILIDADES === */
        .text-right {
            text-align: right !important;
        }

        .text-center {
            text-align: center !important;
        }

        .bg-light {
            background-color: #f8f9fa !important;
        }

        .mb-0 {
            margin-bottom: 0 !important;
        }

        /* === RESPONSIVE === */
        @media (max-width: 768px) {
            .page-header-box {
                flex-direction: column;
                text-align: center;
            }

            .summary-box {
                margin-bottom: 1rem;
            }

            .table-modern {
                font-size: 0.75rem;
            }

            .table-modern thead th {
                font-size: 0.6875rem;
                padding: 0.75rem 0.5rem !important;
            }
        }
    </style>

    <!-- JavaScript -->
    <script src="https://code.jquery.com/jquery-3.6.0.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@4.6.0/dist/js/bootstrap.bundle.min.js"></script>

    <script>
        $(document).ready(function () {
            console.log('Sistema de Reportes iniciado correctamente');
            establecerFechasPorDefecto();
        });

        function establecerFechasPorDefecto() {
            const hoy = new Date();
            const primerDia = new Date(hoy.getFullYear(), hoy.getMonth(), 1);

            if (!$('#<%= txtFechaDesde.ClientID %>').val()) {
                $('#<%= txtFechaDesde.ClientID %>').val(primerDia.toISOString().split('T')[0]);
            }

            if (!$('#<%= txtFechaHasta.ClientID %>').val()) {
                $('#<%= txtFechaHasta.ClientID %>').val(hoy.toISOString().split('T')[0]);
            }
        }

        function verDetalleOrden(idOrden) {
            $('#modalDetalleOrden').modal('show');
            $('#detalleOrdenContent').html('<div class="text-center py-5"><div class="spinner-border text-primary" role="status"><span class="sr-only">Cargando...</span></div><p class="mt-3">Cargando información...</p></div>');

            $.ajax({
                url: 'ReportesOrdenesViaje.aspx/ObtenerDetalleOrden',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({ idOrden: idOrden }),
                success: function (response) {
                    $('#detalleOrdenContent').html(response.d);
                },
                error: function (xhr, status, error) {
                    $('#detalleOrdenContent').html('<div class="alert alert-danger">Error al cargar el detalle: ' + error + '</div>');
                }
            });
        }

        function exportarLiquidaciones() {
            const fechaDesde = $('#<%= txtFechaDesde.ClientID %>').val();
            const fechaHasta = $('#<%= txtFechaHasta.ClientID %>').val();
            const factorConversion = $('#<%= txtFactorConversion.ClientID %>').val() || '3.75';

            if (!fechaDesde || !fechaHasta) {
                alert('Por favor seleccione el rango de fechas');
                return;
            }

            window.location.href = 'ReportesOrdenesViaje.aspx?action=exportarLiquidaciones&fechaDesde=' + fechaDesde + '&fechaHasta=' + fechaHasta + '&factor=' + factorConversion;
        }

        function exportarViajesActivos() {
            const buscarConductor = $('#<%= txtBuscarConductor.ClientID %>').val();
            const estadoViaje = $('#<%= ddlEstadoViaje.ClientID %>').val();

            window.location.href = 'ReportesOrdenesViaje.aspx?action=exportarViajesActivos&buscarConductor=' + encodeURIComponent(buscarConductor) + '&estadoViaje=' + estadoViaje;
        }

        function generarPDFLiquidaciones() {
            const fechaDesde = $('#<%= txtFechaDesde.ClientID %>').val();
            const fechaHasta = $('#<%= txtFechaHasta.ClientID %>').val();
            const factorConversion = $('#<%= txtFactorConversion.ClientID %>').val() || '3.75';

            if (!fechaDesde || !fechaHasta) {
                alert('Por favor seleccione el rango de fechas');
                return;
            }

            window.location.href = 'ReportesOrdenesViaje.aspx?action=generarPDF&fechaDesde=' + fechaDesde + '&fechaHasta=' + fechaHasta + '&factor=' + factorConversion;
        }
    </script>

</asp:Content>

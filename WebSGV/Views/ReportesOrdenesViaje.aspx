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
                <div class="page-header d-flex justify-content-between align-items-center">
                    <div>
                        <h2 class="page-title mb-1">
                            <i class="fas fa-file-invoice-dollar mr-2"></i>Reportes de Órdenes de Viaje
                        </h2>
                        <p class="text-muted mb-0">Consulte liquidaciones y viajes activos del sistema</p>
                    </div>
                    <a href="ListaDespachos.aspx" class="btn btn-outline-secondary btn-back">
                        <i class="fas fa-arrow-left mr-2"></i>Volver
                    </a>
                </div>
            </div>
        </div>

        <!-- PESTAÑAS PRINCIPALES -->
        <div class="row">
            <div class="col-12">
                <ul class="nav nav-tabs-custom mb-0" id="reportesTabs" role="tablist">
                    <li class="nav-item">
                        <a class="nav-link active" id="liquidaciones-tab" data-toggle="tab" href="#liquidaciones" role="tab">
                            <i class="fas fa-money-check-alt mr-2"></i>Liquidaciones
                        </a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link" id="viajesActivos-tab" data-toggle="tab" href="#viajesActivos" role="tab">
                            <i class="fas fa-truck-loading mr-2"></i>Viajes Activos
                        </a>
                    </li>
                </ul>

                <div class="tab-content tab-content-custom" id="reportesTabsContent">

                    <!-- TAB 1: LIQUIDACIONES -->
                    <div class="tab-pane fade show active" id="liquidaciones" role="tabpanel">
                        <div class="p-4">

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
                                                    <asp:TextBox ID="txtFactorConversion" runat="server" CssClass="form-control"
                                                        Text="3.75" step="0.01" placeholder="3.75"></asp:TextBox>
                                                </div>
                                                <small class="form-text text-muted">Tipo de cambio para conversión a soles</small>
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
                                    <div class="row">
                                        <div class="col-md-12">
                                            <button type="button" class="btn btn-success-custom btn-sm mr-2" onclick="exportarLiquidaciones()">
                                                <i class="fas fa-file-excel mr-1"></i>Exportar a Excel
                                            </button>
                                            <button type="button" class="btn btn-danger-custom btn-sm" onclick="generarPDFLiquidaciones()">
                                                <i class="fas fa-file-pdf mr-1"></i>Exportar a PDF
                                            </button>
                                        </div>
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
                                        <span class="badge badge-primary-custom">
                                            <asp:Label ID="lblTotalRegistros" runat="server" Text="0 registros"></asp:Label>
                                        </span>
                                    </div>
                                </div>
                                <div class="section-body p-0">
                                    <div class="table-responsive">
                                        <asp:GridView ID="gvLiquidaciones" runat="server"
                                            CssClass="table table-report mb-0"
                                            AutoGenerateColumns="false"
                                            EmptyDataText="No hay liquidaciones para mostrar"
                                            OnRowDataBound="gvLiquidaciones_RowDataBound"
                                            ShowFooter="true">
                                            <Columns>
                                                <asp:BoundField DataField="DNI" HeaderText="DNI" ItemStyle-CssClass="text-center" />
                                                <asp:BoundField DataField="Conductor" HeaderText="CONDUCTOR" ItemStyle-CssClass="font-weight-bold" />
                                                <asp:BoundField DataField="FechaSalida" HeaderText="FECHA" DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="text-center" />
                                                <asp:BoundField DataField="NumeroLiquidacion" HeaderText="N° DE LIQ" ItemStyle-CssClass="text-center font-weight-bold" />
                                                <asp:TemplateField HeaderText="MONTO S/">
                                                    <ItemTemplate>
                                                        <span class='<%# ObtenerClaseMontoSoles(Eval("MontoSoles")) %>'>
                                                            <%# FormatearMontoSoles(Eval("MontoSoles")) %>
                                                        </span>
                                                    </ItemTemplate>
                                                    <FooterTemplate>
                                                        <strong>TOTAL:
                                                            <asp:Label ID="lblTotalSoles" runat="server"></asp:Label></strong>
                                                    </FooterTemplate>
                                                    <ItemStyle CssClass="text-right" />
                                                    <FooterStyle CssClass="text-right font-weight-bold bg-light" />
                                                </asp:TemplateField>
                                                <asp:TemplateField HeaderText="MONTO $">
                                                    <ItemTemplate>
                                                        <span class='<%# ObtenerClaseMontoDolares(Eval("MontoDolares")) %>'>
                                                            <%# FormatearMontoDolares(Eval("MontoDolares")) %>
                                                        </span>
                                                    </ItemTemplate>
                                                    <FooterTemplate>
                                                        <strong>TOTAL:
                                                            <asp:Label ID="lblTotalDolares" runat="server"></asp:Label></strong>
                                                    </FooterTemplate>
                                                    <ItemStyle CssClass="text-right" />
                                                    <FooterStyle CssClass="text-right font-weight-bold bg-light" />
                                                </asp:TemplateField>
                                                <asp:TemplateField HeaderText="ACCIONES">
                                                    <ItemTemplate>
                                                        <button type="button" class="btn btn-info-custom btn-sm"
                                                            onclick='verDetalleOrden(<%# Eval("IdOrdenViaje") %>)'>
                                                            <i class="fas fa-eye"></i>
                                                        </button>
                                                    </ItemTemplate>
                                                    <ItemStyle CssClass="text-center" />
                                                </asp:TemplateField>
                                            </Columns>
                                            <HeaderStyle CssClass="bg-info text-white" />
                                        </asp:GridView>
                                    </div>
                                </div>
                            </div>

                            <!-- Resumen Financiero -->
                            <div class="section-card">
                                <div class="section-header section-header-success">
                                    <h5 class="section-title">
                                        <i class="fas fa-calculator mr-2"></i>Resumen Total en Soles
                                    </h5>
                                </div>
                                <div class="section-body">
                                    <div class="row">
                                        <div class="col-md-12">
                                            <div class="resumen-financiero">
                                                <div class="row">
                                                    <div class="col-md-3">
                                                        <div class="resumen-item">
                                                            <div class="resumen-label">Total en Soles (S/)</div>
                                                            <div class="resumen-value text-primary">
                                                                <asp:Label ID="lblResumenTotalSoles" runat="server" Text="S/ 0.00"></asp:Label>
                                                            </div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="resumen-item">
                                                            <div class="resumen-label">Total en Dólares ($)</div>
                                                            <div class="resumen-value text-success">
                                                                <asp:Label ID="lblResumenTotalDolares" runat="server" Text="$ 0.00"></asp:Label>
                                                            </div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="resumen-item">
                                                            <div class="resumen-label">Conversión a Soles</div>
                                                            <div class="resumen-value text-info">
                                                                <asp:Label ID="lblResumenConversion" runat="server" Text="S/ 0.00"></asp:Label>
                                                            </div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="resumen-item resumen-total">
                                                            <div class="resumen-label">TOTAL GENERAL</div>
                                                            <div class="resumen-value-total">
                                                                <asp:Label ID="lblResumenTotal" runat="server" Text="S/ 0.00"></asp:Label>
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                    <div class="row mt-3">
                                        <div class="col-md-12">
                                            <div class="alert alert-info-light mb-0">
                                                <i class="fas fa-info-circle mr-2"></i>
                                                <strong>Leyenda:</strong>
                                                <span class="ml-3 text-danger font-weight-bold">■ Rojo = Descuento</span>
                                                <span class="ml-3 text-primary font-weight-bold">■ Azul = Reintegro</span>
                                                <span class="ml-3">■ Negro = Monto Normal</span>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>

                        </div>
                    </div>

                    <!-- TAB 2: VIAJES ACTIVOS -->
                    <div class="tab-pane fade" id="viajesActivos" role="tabpanel">
                        <div class="p-4">

                            <!-- Filtros Viajes Activos -->
                            <div class="section-card mb-4">
                                <div class="section-header">
                                    <h5 class="section-title">
                                        <i class="fas fa-search mr-2"></i>Búsqueda de Viajes Activos
                                    </h5>
                                </div>
                                <div class="section-body">
                                    <div class="row">
                                        <div class="col-md-4">
                                            <div class="form-group">
                                                <label class="form-label">Buscar Conductor</label>
                                                <asp:TextBox ID="txtBuscarConductor" runat="server" CssClass="form-control"
                                                    placeholder="Nombre o DNI del conductor"></asp:TextBox>
                                            </div>
                                        </div>
                                        <div class="col-md-4">
                                            <div class="form-group">
                                                <label class="form-label">Estado del Viaje</label>
                                                <asp:DropDownList ID="ddlEstadoViaje" runat="server" CssClass="form-control">
                                                    <asp:ListItem Value="" Text="-- Todos los estados --"></asp:ListItem>
                                                    <asp:ListItem Value="EnProgreso" Text="En Progreso"></asp:ListItem>
                                                    <asp:ListItem Value="Iniciado" Text="Iniciado"></asp:ListItem>
                                                </asp:DropDownList>
                                            </div>
                                        </div>
                                        <div class="col-md-4">
                                            <div class="form-group">
                                                <label class="form-label">&nbsp;</label>
                                                <div>
                                                    <asp:Button ID="btnBuscarViajesActivos" runat="server" Text="Buscar Viajes Activos"
                                                        CssClass="btn btn-primary-custom btn-block" OnClick="btnBuscarViajesActivos_Click" />
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                    <div class="row">
                                        <div class="col-md-12">
                                            <button type="button" class="btn btn-success-custom btn-sm mr-2" onclick="exportarViajesActivos()">
                                                <i class="fas fa-file-excel mr-1"></i>Exportar a Excel
                                            </button>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <!-- Alerta de Resumen -->
                            <div class="alert alert-warning-custom mb-4">
                                <div class="d-flex justify-content-between align-items-center">
                                    <div>
                                        <i class="fas fa-exclamation-triangle mr-2"></i>
                                        <strong>Viajes sin liquidar:</strong>
                                        <asp:Label ID="lblTotalViajesActivos" runat="server" Text="0 conductores"></asp:Label>
                                        tienen viajes activos pendientes de liquidación.
                                    </div>
                                </div>
                            </div>

                            <!-- Tabla de Viajes Activos -->
                            <div class="section-card">
                                <div class="section-header section-header-warning">
                                    <div class="d-flex justify-content-between align-items-center">
                                        <h5 class="section-title mb-0">
                                            <i class="fas fa-truck mr-2"></i>Conductores con Viajes Activos
                                        </h5>
                                        <span class="badge badge-warning-custom">
                                            <asp:Label ID="lblCountViajesActivos" runat="server" Text="0 viajes"></asp:Label>
                                        </span>
                                    </div>
                                </div>
                                <div class="section-body p-0">
                                    <div class="table-responsive">
                                        <asp:GridView ID="gvViajesActivos" runat="server"
                                            CssClass="table table-report mb-0"
                                            AutoGenerateColumns="false"
                                            EmptyDataText="No hay viajes activos para mostrar"
                                            OnRowDataBound="gvViajesActivos_RowDataBound">
                                            <Columns>
                                                <asp:BoundField DataField="DNI" HeaderText="DNI" ItemStyle-CssClass="text-center" />
                                                <asp:BoundField DataField="Conductor" HeaderText="CONDUCTOR" ItemStyle-CssClass="font-weight-bold" />
                                                <asp:BoundField DataField="PlacaTracto" HeaderText="TRACTO" ItemStyle-CssClass="text-center" />
                                                <asp:BoundField DataField="PlacaCarreta" HeaderText="CARRETA" ItemStyle-CssClass="text-center" />
                                                <asp:BoundField DataField="Cliente" HeaderText="CLIENTE" />
                                                <asp:BoundField DataField="FechaInicio" HeaderText="FECHA INICIO" DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="text-center" />
                                                <asp:BoundField DataField="DiasEnViaje" HeaderText="DÍAS EN VIAJE" ItemStyle-CssClass="text-center" />
                                                <asp:TemplateField HeaderText="ESTADO">
                                                    <ItemTemplate>
                                                        <span class='<%# ObtenerClaseEstado(Eval("Estado")) %>'>
                                                            <%# Eval("Estado") %>
                                                        </span>
                                                    </ItemTemplate>
                                                    <ItemStyle CssClass="text-center" />
                                                </asp:TemplateField>
                                                <asp:TemplateField HeaderText="ACCIONES">
                                                    <ItemTemplate>
                                                        <button type="button" class="btn btn-primary-custom btn-sm"
                                                            onclick='finalizarViaje(<%# Eval("IdViaje") %>)'>
                                                            <i class="fas fa-flag-checkered mr-1"></i>Finalizar
                                                        </button>
                                                    </ItemTemplate>
                                                    <ItemStyle CssClass="text-center" />
                                                </asp:TemplateField>
                                            </Columns>
                                            <HeaderStyle CssClass="bg-warning text-dark" />
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
    <div class="modal fade" id="modalDetalleOrden" tabindex="-1">
        <div class="modal-dialog modal-xl">
            <div class="modal-content">
                <div class="modal-header modal-header-custom">
                    <h5 class="modal-title">
                        <i class="fas fa-file-invoice mr-2"></i>Detalle de Orden de Viaje
                    </h5>
                    <button type="button" class="close" data-dismiss="modal">
                        <span>&times;</span>
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
                    <button type="button" class="btn btn-secondary-custom" data-dismiss="modal">Cerrar</button>
                    <button type="button" class="btn btn-primary-custom" onclick="imprimirOrden()">
                        <i class="fas fa-print mr-1"></i>Imprimir
                    </button>
                </div>
            </div>
        </div>
    </div>

    <!-- CSS Profesional -->
    <style>
        /* === VARIABLES === */
        :root {
            --primary-color: #2563eb;
            --primary-dark: #1e40af;
            --success-color: #059669;
            --danger-color: #dc2626;
            --warning-color: #d97706;
            --info-color: #0891b2;
            --neutral-color: #475569;
            --light-gray: #f8fafc;
            --medium-gray: #e2e8f0;
            --dark-gray: #64748b;
            --border-color: #cbd5e1;
            --text-primary: #1e293b;
            --text-secondary: #64748b;
        }

        /* === LAYOUT === */
        .page-header {
            padding-bottom: 1rem;
            border-bottom: 2px solid var(--border-color);
        }

        .page-title {
            color: var(--text-primary);
            font-weight: 600;
            font-size: 1.75rem;
            margin: 0;
        }

        /* === BOTONES === */
        .btn-back {
            border-color: var(--border-color);
            color: var(--text-secondary);
            padding: 0.5rem 1.25rem;
            font-weight: 500;
            transition: all 0.2s;
        }

            .btn-back:hover {
                background-color: var(--light-gray);
                border-color: var(--dark-gray);
                color: var(--text-primary);
            }

        .btn-primary-custom {
            background-color: var(--primary-color);
            border-color: var(--primary-color);
            color: white;
            font-weight: 500;
        }

            .btn-primary-custom:hover {
                background-color: var(--primary-dark);
                border-color: var(--primary-dark);
            }

        .btn-secondary-custom {
            background-color: white;
            border: 1px solid var(--border-color);
            color: var(--text-secondary);
            font-weight: 500;
        }

        .btn-success-custom {
            background-color: var(--success-color);
            border-color: var(--success-color);
            color: white;
        }

        .btn-danger-custom {
            background-color: var(--danger-color);
            border-color: var(--danger-color);
            color: white;
        }

        .btn-info-custom {
            background-color: var(--info-color);
            border-color: var(--info-color);
            color: white;
        }

        /* === ALERTAS === */
        .alert-warning-custom {
            background-color: #fef3c7;
            border: 1px solid #fbbf24;
            border-radius: 0.5rem;
            color: #78350f;
            padding: 1rem;
        }

        .alert-info-light {
            background-color: #f0f9ff;
            border: 1px solid #bae6fd;
            border-radius: 0.5rem;
            padding: 1rem;
        }

        /* === PESTAÑAS === */
        .nav-tabs-custom {
            border-bottom: 2px solid var(--border-color);
        }

            .nav-tabs-custom .nav-link {
                color: var(--text-secondary);
                background-color: transparent;
                border: none;
                border-bottom: 3px solid transparent;
                padding: 1rem 1.5rem;
                font-weight: 500;
                transition: all 0.2s;
            }

                .nav-tabs-custom .nav-link:hover {
                    color: var(--primary-color);
                    background-color: var(--light-gray);
                }

                .nav-tabs-custom .nav-link.active {
                    color: var(--primary-color);
                    background-color: white;
                    border-bottom-color: var(--primary-color);
                }

        .tab-content-custom {
            background-color: white;
            border: 1px solid var(--border-color);
            border-top: none;
            min-height: 500px;
        }

        /* === SECCIONES === */
        .section-card {
            background-color: white;
            border: 1px solid var(--border-color);
            border-radius: 0.5rem;
            overflow: hidden;
        }

        .section-header {
            background-color: var(--light-gray);
            padding: 1rem 1.25rem;
            border-bottom: 1px solid var(--border-color);
        }

        .section-header-info {
            background-color: #e0f2fe;
            border-bottom-color: #7dd3fc;
        }

        .section-header-success {
            background-color: #f0fdf4;
            border-bottom-color: #86efac;
        }

        .section-header-warning {
            background-color: #fef3c7;
            border-bottom-color: #fbbf24;
        }

        .section-title {
            color: var(--text-primary);
            font-size: 1.125rem;
            font-weight: 600;
            margin: 0;
        }

        .section-body {
            padding: 1.5rem;
        }

        /* === FORMULARIOS === */
        .form-label {
            color: var(--text-primary);
            font-weight: 500;
            font-size: 0.875rem;
            margin-bottom: 0.375rem;
        }

        .form-control {
            border: 1px solid var(--border-color);
            font-size: 0.875rem;
            padding: 0.5rem 0.75rem;
        }

            .form-control:focus {
                border-color: var(--primary-color);
                box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
            }

        /* === TABLAS === */
        .table-report {
            font-size: 0.875rem;
        }

            .table-report thead th {
                background-color: var(--light-gray);
                color: var(--text-primary);
                font-weight: 600;
                font-size: 0.8125rem;
                text-transform: uppercase;
                letter-spacing: 0.025em;
                padding: 0.875rem 0.75rem;
                border-bottom: 2px solid var(--border-color);
            }

            .table-report tbody td {
                vertical-align: middle;
                padding: 0.75rem;
                border-bottom: 1px solid var(--medium-gray);
            }

            .table-report tbody tr:hover {
                background-color: var(--light-gray);
            }

        /* === CLASES DE MONTOS === */
        .monto-descuento {
            color: var(--danger-color);
            font-weight: 600;
        }

        .monto-reintegro {
            color: var(--primary-color);
            font-weight: 600;
        }

        .monto-normal {
            color: var(--text-primary);
            font-weight: 500;
        }

        /* === BADGES === */
        .badge-primary-custom {
            background-color: var(--primary-color);
            color: white;
            font-weight: 500;
            padding: 0.5rem 0.875rem;
            font-size: 0.875rem;
        }

        .badge-warning-custom {
            background-color: var(--warning-color);
            color: white;
            font-weight: 500;
            padding: 0.5rem 0.875rem;
            font-size: 0.875rem;
        }

        .badge-estado-activo {
            background-color: #fbbf24;
            color: #78350f;
            padding: 0.375rem 0.75rem;
            border-radius: 0.25rem;
            font-weight: 600;
            font-size: 0.75rem;
        }

        .badge-estado-progreso {
            background-color: #3b82f6;
            color: white;
            padding: 0.375rem 0.75rem;
            border-radius: 0.25rem;
            font-weight: 600;
            font-size: 0.75rem;
        }

        /* === RESUMEN FINANCIERO === */
        .resumen-financiero {
            background-color: var(--light-gray);
            border: 2px solid var(--border-color);
            border-radius: 0.5rem;
            padding: 1.5rem;
        }

        .resumen-item {
            background-color: white;
            border: 1px solid var(--border-color);
            border-radius: 0.375rem;
            padding: 1.25rem;
            text-align: center;
        }

        .resumen-total {
            background-color: #f0fdf4;
            border: 2px solid var(--success-color);
        }

        .resumen-label {
            font-size: 0.875rem;
            color: var(--text-secondary);
            font-weight: 500;
            margin-bottom: 0.5rem;
            text-transform: uppercase;
            letter-spacing: 0.025em;
        }

        .resumen-value {
            font-size: 1.5rem;
            font-weight: 700;
        }

        .resumen-value-total {
            font-size: 1.75rem;
            font-weight: 800;
            color: var(--success-color);
        }

        /* === MODAL === */
        .modal-header-custom {
            background-color: var(--light-gray);
            border-bottom: 2px solid var(--border-color);
        }

        /* === UTILIDADES === */
        .text-muted {
            color: var(--text-secondary) !important;
        }

        .mr-1 {
            margin-right: 0.25rem;
        }

        .mr-2 {
            margin-right: 0.5rem;
        }

        .ml-2 {
            margin-left: 0.5rem;
        }

        .ml-3 {
            margin-left: 0.75rem;
        }

        .mb-0 {
            margin-bottom: 0;
        }

        .mb-3 {
            margin-bottom: 0.75rem;
        }

        .mb-4 {
            margin-bottom: 1rem;
        }

        .mt-3 {
            margin-top: 0.75rem;
        }

        .px-4 {
            padding-left: 1rem;
            padding-right: 1rem;
        }

        .py-5 {
            padding-top: 1.25rem;
            padding-bottom: 1.25rem;
        }


        /* === FIX: CABECERAS INVISIBLES - FORZAR CONTRASTE === */
        #MainContent_gvLiquidaciones thead th,
        #MainContent_gvLiquidaciones th,
        .table-report thead th {
            background-color: #17a2b8 !important; /* bg-info de Bootstrap */
            color: #ffffff !important;
            font-weight: 600 !important;
            text-align: center !important;
            vertical-align: middle !important;
            border: 1px solid #dee2e6 !important;
        }

        /* También para el GridView de Viajes Activos */
        #MainContent_gvViajesActivos thead th,
        #MainContent_gvViajesActivos th {
            background-color: #ffc107 !important; /* bg-warning de Bootstrap */
            color: #212529 !important; /* texto oscuro para fondo amarillo */
            font-weight: 600 !important;
            text-align: center !important;
            vertical-align: middle !important;
            border: 1px solid #dee2e6 !important;
        }
    </style>

    <!-- JavaScript -->
    <script src="https://code.jquery.com/jquery-3.6.0.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@4.6.0/dist/js/bootstrap.bundle.min.js"></script>

    <script>
        $(document).ready(function () {
            console.log('Sistema de Reportes iniciado');

            // Establecer fechas por defecto
            establecerFechasPorDefecto();
        });

        function establecerFechasPorDefecto() {
            const hoy = new Date();
            const primerDia = new Date(hoy.getFullYear(), hoy.getMonth(), 1);

            // Establecer desde el primer día del mes actual
            if (!$('#<%= txtFechaDesde.ClientID %>').val()) {
                $('#<%= txtFechaDesde.ClientID %>').val(primerDia.toISOString().split('T')[0]);
            }

            // Establecer hasta hoy
            if (!$('#<%= txtFechaHasta.ClientID %>').val()) {
                $('#<%= txtFechaHasta.ClientID %>').val(hoy.toISOString().split('T')[0]);
            }
        }

        function verDetalleOrden(idOrden) {
            $('#modalDetalleOrden').modal('show');

            // Llamada AJAX para obtener detalle
            $.ajax({
                url: 'ReportesOrdenesViaje.aspx/ObtenerDetalleOrden',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({ idOrden: idOrden }),
                success: function (response) {
                    $('#detalleOrdenContent').html(response.d);
                },
                error: function () {
                    $('#detalleOrdenContent').html('<div class="alert alert-danger">Error al cargar el detalle</div>');
                }
            });
        }

        function finalizarViaje(idViaje) {
            if (confirm('¿Está seguro que desea finalizar este viaje y proceder a crear la orden de viaje?')) {
                window.location.href = 'AgregarOrdenViaje.aspx?idViaje=' + idViaje + '&origen=viajeFinalizado';
            }
        }

        function exportarLiquidaciones() {
            const fechaDesde = $('#<%= txtFechaDesde.ClientID %>').val();
            const fechaHasta = $('#<%= txtFechaHasta.ClientID %>').val();
            const factorConversion = $('#<%= txtFactorConversion.ClientID %>').val() || '3.75';

            window.location.href = 'ReportesOrdenesViaje.aspx?action=exportarLiquidaciones&fechaDesde=' + fechaDesde + '&fechaHasta=' + fechaHasta + '&factor=' + factorConversion;
        }

        function exportarViajesActivos() {
            window.location.href = 'ReportesOrdenesViaje.aspx?action=exportarViajesActivos';
        }

        function generarPDFLiquidaciones() {
            const fechaDesde = $('#<%= txtFechaDesde.ClientID %>').val();
            const fechaHasta = $('#<%= txtFechaHasta.ClientID %>').val();
            const factorConversion = $('#<%= txtFactorConversion.ClientID %>').val() || '3.75';

            window.open('ReportesOrdenesViaje.aspx?action=generarPDF&fechaDesde=' + fechaDesde + '&fechaHasta=' + fechaHasta + '&factor=' + factorConversion, '_blank');
        }

        function imprimirOrden() {
            const contenido = document.getElementById('detalleOrdenContent').innerHTML;
            const ventana = window.open('', '', 'width=800,height=600');
            ventana.document.write('<html><head><title>Orden de Viaje</title>');
            ventana.document.write('<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap@4.6.0/dist/css/bootstrap.min.css">');
            ventana.document.write('</head><body>');
            ventana.document.write(contenido);
            ventana.document.write('</body></html>');
            ventana.document.close();
            ventana.print();
        }
    </script>

</asp:Content>

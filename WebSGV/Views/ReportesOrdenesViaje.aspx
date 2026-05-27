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
                    <button type="button" class="tab-btn" id="tabPersonalizado" onclick="cambiarTab('personalizado')">
                        <i class="fas fa-sliders-h mr-2"></i>Reporte Personalizado
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
                                <small id="errFechaDesde" class="text-danger d-none"></small>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">Fecha Hasta</label>
                                 <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                <small id="errFechaHasta" class="text-danger d-none"></small>
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
                                <small id="errFactorConversion" class="text-danger d-none"></small>
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
                                <small id="errBuscarConductor" class="text-danger d-none"></small>
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
                                <small id="errEstadoViaje" class="text-danger d-none"></small>
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

        <!-- ==================== SECCIÓN REPORTE PERSONALIZADO ==================== -->
        <div id="seccionPersonalizado" style="display: none;">

            <!-- Filtros -->
            <div class="section-card mb-4">
                <div class="section-header section-header-info">
                    <h5 class="section-title">
                        <i class="fas fa-sliders-h mr-2"></i>Configuración del Reporte Personalizado
                    </h5>
                </div>
                <div class="section-body">
                    <div class="row">
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">Fecha Desde</label>
                                 <asp:TextBox ID="txtPersFechaDesde" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                <small id="errPersFechaDesde" class="text-danger d-none"></small>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">Fecha Hasta</label>
                                 <asp:TextBox ID="txtPersFechaHasta" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                <small id="errPersFechaHasta" class="text-danger d-none"></small>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">Estado del Viaje</label>
                                 <asp:DropDownList ID="ddlPersEstado" runat="server" CssClass="form-control">
                                    <asp:ListItem Value="TODOS" Text="Todos" Selected="True"></asp:ListItem>
                                    <asp:ListItem Value="COMPLETADO" Text="Liquidados (Completado)"></asp:ListItem>
                                    <asp:ListItem Value="PENDIENTE" Text="Pendientes de Aprobación"></asp:ListItem>
                                    <asp:ListItem Value="RECHAZADO" Text="Rechazados"></asp:ListItem>
                                 </asp:DropDownList>
                                <small id="errPersEstado" class="text-danger d-none"></small>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">Conductor</label>
                                 <asp:DropDownList ID="ddlPersConductor" runat="server" CssClass="form-control">
                                    <asp:ListItem Value="0" Text="Todos los conductores"></asp:ListItem>
                                </asp:DropDownList>
                                <small id="errPersConductor" class="text-danger d-none"></small>
                            </div>
                        </div>
                    </div>
                    <div class="row">
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">Cliente</label>
                                 <asp:DropDownList ID="ddlPersCliente" runat="server" CssClass="form-control">
                                    <asp:ListItem Value="0" Text="Todos los clientes"></asp:ListItem>
                                </asp:DropDownList>
                                <small id="errPersCliente" class="text-danger d-none"></small>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">Placa Tracto</label>
                                 <asp:TextBox ID="txtPersPlacaTracto" runat="server" CssClass="form-control" placeholder="Ej. ABC-123"></asp:TextBox>
                                <small id="errPersPlaca" class="text-danger d-none"></small>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">
                                    Categoría de Gasto Adicional
                                    <i class="fas fa-info-circle ml-1 text-muted" title="Solo incluye viajes con la categoría indicada (ej: propina, cochera, lavado)"></i>
                                </label>
                                 <asp:TextBox ID="txtPersCategoria" runat="server" CssClass="form-control" placeholder="Ej. propina, cochera..."></asp:TextBox>
                                <small id="errPersCategoria" class="text-danger d-none"></small>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">Ordenar por</label>
                                 <asp:DropDownList ID="ddlPersOrden" runat="server" CssClass="form-control">
                                    <asp:ListItem Value="fecha_desc" Text="Fecha (más reciente primero)" Selected="True"></asp:ListItem>
                                    <asp:ListItem Value="fecha_asc" Text="Fecha (más antigua primero)"></asp:ListItem>
                                    <asp:ListItem Value="conductor" Text="Conductor (A-Z)"></asp:ListItem>
                                    <asp:ListItem Value="cliente" Text="Cliente (A-Z)"></asp:ListItem>
                                 </asp:DropDownList>
                                <small id="errPersOrden" class="text-danger d-none"></small>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">Factor de Conversión ($ a S/)</label>
                                <div class="input-group">
                                    <div class="input-group-prepend">
                                        <span class="input-group-text">S/</span>
                                    </div>
                                     <asp:TextBox ID="txtPersFactor" runat="server" CssClass="form-control" Text="3.75"></asp:TextBox>
                                 </div>
                                <small id="errPersFactor" class="text-danger d-none"></small>
                            </div>
                        </div>
                    </div>
                    <div class="row">
                        <div class="col-md-12">
                            <div class="form-group">
                                <label class="form-label">Título del Reporte</label>
                                 <asp:TextBox ID="txtPersTitulo" runat="server" CssClass="form-control"
                                     Text="Reporte Personalizado de Órdenes de Viaje"></asp:TextBox>
                                <small id="errPersTitulo" class="text-danger d-none"></small>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Selección de Columnas -->
            <div class="section-card mb-4">
                <div class="section-header">
                    <div class="d-flex justify-content-between align-items-center">
                        <h5 class="section-title mb-0">
                            <i class="fas fa-columns mr-2"></i>Columnas del Reporte
                        </h5>
                        <div>
                            <button type="button" class="btn btn-sm btn-secondary-custom" onclick="marcarTodasColumnas(true)">
                                <i class="fas fa-check-square mr-1"></i>Marcar todas
                            </button>
                            <button type="button" class="btn btn-sm btn-secondary-custom ml-1" onclick="marcarTodasColumnas(false)">
                                <i class="fas fa-square mr-1"></i>Desmarcar todas
                            </button>
                        </div>
                    </div>
                </div>
                <div class="section-body">
                    <div class="row">
                        <div class="col-md-4">
                            <h6 class="columnas-grupo"><i class="fas fa-user mr-1"></i>Datos del Conductor</h6>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="dni" checked /> DNI</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="conductor" checked /> Conductor</label>
                        </div>
                        <div class="col-md-4">
                            <h6 class="columnas-grupo"><i class="fas fa-calendar-alt mr-1"></i>Fechas y Horas</h6>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="fechaSalida" checked /> Fecha Salida</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="fechaLlegada" checked /> Fecha Llegada</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="horaSalida" /> Hora Salida</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="horaLlegada" /> Hora Llegada</label>
                        </div>
                        <div class="col-md-4">
                            <h6 class="columnas-grupo"><i class="fas fa-truck mr-1"></i>Vehículo y Viaje</h6>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="tracto" checked /> Placa Tracto</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="carreta" /> Placa Carreta</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="cliente" checked /> Cliente</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="destino" checked /> Destino</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="numero" checked /> N° Liquidación</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="estado" checked /> Estado</label>
                        </div>
                    </div>
                    <hr />
                    <div class="row">
                        <div class="col-md-4">
                            <h6 class="columnas-grupo"><i class="fas fa-plus-circle mr-1 text-success"></i>Ingresos</h6>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="ingresosSoles" checked /> Total Ingresos S/</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="ingresosDolares" /> Total Ingresos $</label>
                        </div>
                        <div class="col-md-4">
                            <h6 class="columnas-grupo"><i class="fas fa-minus-circle mr-1 text-danger"></i>Gastos</h6>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="gastosSoles" checked /> Total Gastos S/</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="gastosDolares" /> Total Gastos $</label>
                        </div>
                        <div class="col-md-4">
                            <h6 class="columnas-grupo"><i class="fas fa-balance-scale mr-1 text-info"></i>Descuentos, Reintegros y Balance</h6>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="descuentoSoles" checked /> Descuento S/</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="descuentoDolares" /> Descuento $</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="reintegroSoles" checked /> Reintegro S/</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="reintegroDolares" /> Reintegro $</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="balanceSoles" checked /> Balance Neto S/</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="balanceDolares" /> Balance Neto $</label>
                        </div>
                    </div>
                    <hr />
                    <div class="row">
                        <div class="col-md-6">
                            <h6 class="columnas-grupo"><i class="fas fa-plus-square mr-1 text-success"></i>Detalle de Ingresos por Categoría</h6>
                            <div class="row">
                                <div class="col-sm-6">
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="iDespachoS" /> Despacho S/</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="iDespachoD" /> Despacho $</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="iPrestamoS" /> Préstamo S/</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="iPrestamoD" /> Préstamo $</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="iMensualidadS" /> Mensualidad S/</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="iMensualidadD" /> Mensualidad $</label>
                                </div>
                                <div class="col-sm-6">
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="iOtrosS" /> Otros Autorizados S/</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="iOtrosD" /> Otros Autorizados $</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="iAdicionalesS" /> Ing. Adicionales S/</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="iAdicionalesD" /> Ing. Adicionales $</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="iAdicionalesDet" /> Detalle Ing. Adicionales</label>
                                </div>
                            </div>
                        </div>
                        <div class="col-md-6">
                            <h6 class="columnas-grupo"><i class="fas fa-minus-square mr-1 text-danger"></i>Detalle de Gastos por Categoría</h6>
                            <div class="row">
                                <div class="col-sm-6">
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gPeajesS" /> Peajes S/</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gPeajesD" /> Peajes $</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gAlimentacionS" /> Alimentación S/</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gAlimentacionD" /> Alimentación $</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gCombustibleS" /> Combustible S/</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gCombustibleD" /> Combustible $</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gHospedajeS" /> Hospedaje S/</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gHospedajeD" /> Hospedaje $</label>
                                </div>
                                <div class="col-sm-6">
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gMovilidadS" /> Movilidad S/</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gMovilidadD" /> Movilidad $</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gApoyoSeguridadS" /> Apoyo Seguridad S/</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gApoyoSeguridadD" /> Apoyo Seguridad $</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gReparacionesS" /> Reparaciones S/</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gReparacionesD" /> Reparaciones $</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gEncarpadaS" /> Encarpada/Desenc. S/</label>
                                    <label class="col-check"><input type="checkbox" class="col-pers" value="gEncarpadaD" /> Encarpada/Desenc. $</label>
                                </div>
                            </div>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="gAdicionalesS" /> Otros Gastos (Propina/Cochera/...) S/</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="gAdicionalesD" /> Otros Gastos $</label>
                            <label class="col-check"><input type="checkbox" class="col-pers" value="gAdicionalesDet" checked /> Detalle Otros Gastos (texto)</label>
                        </div>
                    </div>
                    <hr />
                    <div class="row">
                        <div class="col-md-12">
                            <label class="col-check"><input type="checkbox" class="col-pers" value="observaciones" /> Incluir Observaciones</label>
                            <label class="col-check ml-3"><input type="checkbox" id="chkIncluirTotales" checked /> Incluir fila de totales</label>
                            <label class="col-check ml-3"><input type="checkbox" id="chkIncluirResumen" checked /> Incluir resumen ejecutivo</label>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Acciones -->
            <div class="section-card">
                <div class="section-body">
                    <div class="d-flex justify-content-between align-items-center flex-wrap">
                        <div class="text-muted small">
                            <i class="fas fa-info-circle mr-1"></i>
                            El reporte se generará con los filtros y columnas seleccionadas.
                            Se construye sobre liquidaciones aprobadas por el administrador.
                        </div>
                        <div class="d-flex" style="gap:0.5rem;">
                            <button type="button" class="btn btn-export btn-export-excel" onclick="generarPersonalizado('excel')">
                                <i class="fas fa-file-excel mr-1"></i>Generar Excel
                            </button>
                            <button type="button" class="btn btn-export btn-export-pdf" onclick="generarPersonalizado('pdf')">
                                <i class="fas fa-file-pdf mr-1"></i>Generar PDF
                            </button>
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

        /* === COLUMNAS PERSONALIZADO === */
        .columnas-grupo {
            font-weight: 700;
            color: #334155;
            font-size: 0.875rem;
            margin-bottom: 0.75rem;
            padding-bottom: 0.5rem;
            border-bottom: 1px solid var(--rpt-medium-gray);
            text-transform: uppercase;
            letter-spacing: 0.025em;
        }

        .col-check {
            display: block;
            padding: 0.375rem 0.5rem;
            margin-bottom: 0.25rem;
            border-radius: 0.25rem;
            font-size: 0.875rem;
            color: #1e293b;
            cursor: pointer;
            transition: background 0.15s;
        }

        .col-check:hover {
            background: var(--rpt-light-gray);
        }

        .col-check input[type="checkbox"] {
            margin-right: 0.5rem;
            cursor: pointer;
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
            $('#seccionLiquidaciones').hide();
            $('#seccionViajesActivos').hide();
            $('#seccionPersonalizado').hide();
            $('#tabLiquidaciones, #tabViajesActivos, #tabPersonalizado').removeClass('tab-btn-active');

            if (tab === 'liquidaciones') {
                $('#seccionLiquidaciones').show();
                $('#tabLiquidaciones').addClass('tab-btn-active');
            } else if (tab === 'viajesActivos') {
                $('#seccionViajesActivos').show();
                $('#tabViajesActivos').addClass('tab-btn-active');
            } else if (tab === 'personalizado') {
                $('#seccionPersonalizado').show();
                $('#tabPersonalizado').addClass('tab-btn-active');
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
            limpiarErroresValidacion();
            var fechaDesde = $('#<%= txtFechaDesde.ClientID %>').val();
            var fechaHasta = $('#<%= txtFechaHasta.ClientID %>').val();
            var factorConversion = $('#<%= txtFactorConversion.ClientID %>').val() || '3.75';

            if (!validarFiltrosLiquidaciones(fechaDesde, fechaHasta, factorConversion)) {
                return;
            }

            window.location.href = 'ReportesOrdenesViaje.aspx?action=exportarLiquidaciones&fechaDesde=' + fechaDesde + '&fechaHasta=' + fechaHasta + '&factor=' + factorConversion;
        }

        function exportarViajesActivos() {
            limpiarErroresValidacion();
            var buscarConductor = $('#<%= txtBuscarConductor.ClientID %>').val();
            var estadoViaje = $('#<%= ddlEstadoViaje.ClientID %>').val();

            if (!validarFiltroViajesActivos(buscarConductor, estadoViaje)) {
                return;
            }

            window.location.href = 'ReportesOrdenesViaje.aspx?action=exportarViajesActivos&buscarConductor=' + encodeURIComponent(buscarConductor) + '&estadoViaje=' + estadoViaje;
        }

        function generarPDFLiquidaciones() {
            limpiarErroresValidacion();
            var fechaDesde = $('#<%= txtFechaDesde.ClientID %>').val();
            var fechaHasta = $('#<%= txtFechaHasta.ClientID %>').val();
            var factorConversion = $('#<%= txtFactorConversion.ClientID %>').val() || '3.75';

            if (!validarFiltrosLiquidaciones(fechaDesde, fechaHasta, factorConversion)) {
                return;
            }

            window.location.href = 'ReportesOrdenesViaje.aspx?action=generarPDF&fechaDesde=' + fechaDesde + '&fechaHasta=' + fechaHasta + '&factor=' + factorConversion;
        }

        function marcarTodasColumnas(checked) {
            $('.col-pers').prop('checked', checked);
        }

        function generarPersonalizado(formato) {
            limpiarErroresValidacion();
            var fechaDesde = $('#<%= txtPersFechaDesde.ClientID %>').val();
            var fechaHasta = $('#<%= txtPersFechaHasta.ClientID %>').val();
            var estado = $('#<%= ddlPersEstado.ClientID %>').val();
            var idConductor = $('#<%= ddlPersConductor.ClientID %>').val();
            var idCliente = $('#<%= ddlPersCliente.ClientID %>').val();
            var placaTracto = $('#<%= txtPersPlacaTracto.ClientID %>').val();
            var categoria = $('#<%= txtPersCategoria.ClientID %>').val();
            var orden = $('#<%= ddlPersOrden.ClientID %>').val();
            var factor = $('#<%= txtPersFactor.ClientID %>').val() || '3.75';
            var titulo = $('#<%= txtPersTitulo.ClientID %>').val() || 'Reporte Personalizado';
            var incluirTotales = $('#chkIncluirTotales').is(':checked') ? '1' : '0';
            var incluirResumen = $('#chkIncluirResumen').is(':checked') ? '1' : '0';

            if (!validarFiltrosPersonalizados(fechaDesde, fechaHasta, estado, idConductor, idCliente, placaTracto, categoria, orden, factor, titulo)) {
                return;
            }

            var columnas = [];
            $('.col-pers:checked').each(function () { columnas.push($(this).val()); });

            if (columnas.length === 0) {
                mostrarErrorCampo('errPersTitulo', 'Debe seleccionar al menos una columna para el reporte.');
                return;
            }

            var qs = 'action=reportePersonalizado'
                + '&formato=' + formato
                + '&fechaDesde=' + encodeURIComponent(fechaDesde)
                + '&fechaHasta=' + encodeURIComponent(fechaHasta)
                + '&estado=' + encodeURIComponent(estado)
                + '&idConductor=' + encodeURIComponent(idConductor)
                + '&idCliente=' + encodeURIComponent(idCliente)
                + '&placaTracto=' + encodeURIComponent(placaTracto)
                + '&categoria=' + encodeURIComponent(categoria)
                + '&orden=' + encodeURIComponent(orden)
                + '&factor=' + encodeURIComponent(factor)
                + '&titulo=' + encodeURIComponent(titulo)
                + '&totales=' + incluirTotales
                + '&resumen=' + incluirResumen
                + '&cols=' + encodeURIComponent(columnas.join(','));

            window.location.href = 'ReportesOrdenesViaje.aspx?' + qs;
        }

        function limpiarErroresValidacion() {
            $('.text-danger[id^="err"]').each(function () {
                $(this).addClass('d-none').text('');
            });
        }

        function mostrarErrorCampo(id, mensaje) {
            $('#' + id).removeClass('d-none').text(mensaje);
        }

        function validarFechaIso(valor) {
            return /^\d{4}-\d{2}-\d{2}$/.test(valor);
        }

        function validarFiltrosLiquidaciones(fechaDesde, fechaHasta, factor) {
            var ok = true;
            if (!fechaDesde || !validarFechaIso(fechaDesde)) { mostrarErrorCampo('errFechaDesde', 'Ingrese una fecha inicial válida (aaaa-mm-dd).'); ok = false; }
            if (!fechaHasta || !validarFechaIso(fechaHasta)) { mostrarErrorCampo('errFechaHasta', 'Ingrese una fecha final válida (aaaa-mm-dd).'); ok = false; }
            if (ok && new Date(fechaDesde) > new Date(fechaHasta)) { mostrarErrorCampo('errFechaHasta', 'La fecha final debe ser mayor o igual a la fecha inicial.'); ok = false; }

            var factorNumero = parseFloat((factor || '').replace(',', '.'));
            if (isNaN(factorNumero) || factorNumero < 0.01 || factorNumero > 20) {
                mostrarErrorCampo('errFactorConversion', 'Ingrese un factor entre 0.01 y 20.');
                ok = false;
            }
            return ok;
        }

        function validarFiltroViajesActivos(buscarConductor, estadoViaje) {
            var ok = true;
            if (buscarConductor && buscarConductor.length > 100) {
                mostrarErrorCampo('errBuscarConductor', 'La búsqueda no debe superar 100 caracteres.');
                ok = false;
            }
            if (buscarConductor && !/^[a-zA-ZáéíóúÁÉÍÓÚñÑ0-9\s\-\.]+$/.test(buscarConductor)) {
                mostrarErrorCampo('errBuscarConductor', 'Solo se permiten letras, números, espacios, punto y guion.');
                ok = false;
            }
            if (['TODOS', 'ABIERTO', 'CERRADO'].indexOf(estadoViaje) === -1) {
                mostrarErrorCampo('errEstadoViaje', 'Seleccione un estado de viaje válido.');
                ok = false;
            }
            return ok;
        }

        function validarFiltrosPersonalizados(fechaDesde, fechaHasta, estado, idConductor, idCliente, placaTracto, categoria, orden, factor, titulo) {
            var ok = true;
            if (!fechaDesde || !validarFechaIso(fechaDesde)) { mostrarErrorCampo('errPersFechaDesde', 'Ingrese una fecha inicial válida (aaaa-mm-dd).'); ok = false; }
            if (!fechaHasta || !validarFechaIso(fechaHasta)) { mostrarErrorCampo('errPersFechaHasta', 'Ingrese una fecha final válida (aaaa-mm-dd).'); ok = false; }
            if (ok && new Date(fechaDesde) > new Date(fechaHasta)) { mostrarErrorCampo('errPersFechaHasta', 'La fecha final debe ser mayor o igual a la fecha inicial.'); ok = false; }
            if (['TODOS', 'COMPLETADO', 'PENDIENTE', 'RECHAZADO'].indexOf(estado) === -1) { mostrarErrorCampo('errPersEstado', 'Seleccione un estado válido.'); ok = false; }
            if (!/^\d+$/.test(idConductor) || parseInt(idConductor, 10) < 0) { mostrarErrorCampo('errPersConductor', 'Seleccione un conductor válido.'); ok = false; }
            if (!/^\d+$/.test(idCliente) || parseInt(idCliente, 10) < 0) { mostrarErrorCampo('errPersCliente', 'Seleccione un cliente válido.'); ok = false; }
            if (placaTracto && !/^[a-zA-Z0-9\-\s]{0,15}$/.test(placaTracto)) { mostrarErrorCampo('errPersPlaca', 'La placa permite letras, números, espacios y guion (máx. 15).'); ok = false; }
            if (categoria && !/^[a-zA-ZáéíóúÁÉÍÓÚñÑ0-9\s\-\.]{0,60}$/.test(categoria)) { mostrarErrorCampo('errPersCategoria', 'Categoría inválida. Máximo 60 caracteres.'); ok = false; }
            if (['fecha_desc', 'fecha_asc', 'conductor', 'cliente'].indexOf(orden) === -1) { mostrarErrorCampo('errPersOrden', 'Seleccione un orden válido.'); ok = false; }

            var factorNumero = parseFloat((factor || '').replace(',', '.'));
            if (isNaN(factorNumero) || factorNumero < 0.01 || factorNumero > 20) { mostrarErrorCampo('errPersFactor', 'Ingrese un factor entre 0.01 y 20.'); ok = false; }
            if (!titulo || titulo.trim().length < 5 || titulo.trim().length > 120) { mostrarErrorCampo('errPersTitulo', 'El título debe tener entre 5 y 120 caracteres.'); ok = false; }
            return ok;
        }
    </script>

</asp:Content>

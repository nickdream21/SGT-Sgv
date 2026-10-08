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
                                     placeholder="Nombre, apellido o DNI" autocomplete="off" AutoCompleteType="Disabled"
                                     spellcheck="false" autocapitalize="off" autocorrect="off"></asp:TextBox>
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
                                <asp:BoundField DataField="CantidadDespachos" HeaderText="N° DESP.">
                                    <ItemStyle CssClass="text-center" />
                                </asp:BoundField>
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
                            <label class="col-check"><input type="checkbox" class="col-pers" value="cantDespachos" /> N° Despachos</label>
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

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/ReportesOrdenesViaje.css") %>" rel="stylesheet" />

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            txtFechaDesde: '<%= txtFechaDesde.ClientID %>',
            txtFechaHasta: '<%= txtFechaHasta.ClientID %>',
            txtFactorConversion: '<%= txtFactorConversion.ClientID %>',
            txtBuscarConductor: '<%= txtBuscarConductor.ClientID %>',
            ddlEstadoViaje: '<%= ddlEstadoViaje.ClientID %>',
            txtPersFechaDesde: '<%= txtPersFechaDesde.ClientID %>',
            txtPersFechaHasta: '<%= txtPersFechaHasta.ClientID %>',
            ddlPersEstado: '<%= ddlPersEstado.ClientID %>',
            ddlPersConductor: '<%= ddlPersConductor.ClientID %>',
            ddlPersCliente: '<%= ddlPersCliente.ClientID %>',
            txtPersPlacaTracto: '<%= txtPersPlacaTracto.ClientID %>',
            txtPersCategoria: '<%= txtPersCategoria.ClientID %>',
            ddlPersOrden: '<%= ddlPersOrden.ClientID %>',
            txtPersFactor: '<%= txtPersFactor.ClientID %>',
            txtPersTitulo: '<%= txtPersTitulo.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/ReportesOrdenesViaje.js") %>"></script>

</asp:Content>

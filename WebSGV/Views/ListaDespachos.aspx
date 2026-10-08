<%@ Page Title="Gestión de Viajes y Lotes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ListaDespachos.aspx.cs" Inherits="WebSGV.Views.ListaDespachos" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

<link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/ListaDespachos.css") %>" rel="stylesheet" />

    <div class="container-fluid">
        <div class="row">
            <div class="col-md-12">
                <div class="card main-card">
                    <div class="ld-page-header">
                        <h4>
                            <i class="fas fa-tasks mr-2"></i> Gestión de Viajes y Lotes
                            <asp:Label ID="lblContadorGeneral" runat="server" CssClass="badge ml-3" style="background:#334155;font-size:.68rem;font-weight:600;"></asp:Label>
                        </h4>
                    </div>
                    <div class="card-body">
                        <!-- gvManifiestosLote está declarado como PostBackTrigger (no Async) en <Triggers> más
                             abajo: tiene FileUpload por fila y descarga de documentos vía Response.WriteFile,
                             ninguno de los dos funciona en un postback parcial de UpdatePanel. -->
                        <asp:UpdatePanel ID="UpdatePanelMain" runat="server" UpdateMode="Conditional">
                            <ContentTemplate>
                                
                                <!-- Mensajes -->
                                <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-3">
                                    <div class="position-relative">
                                        <asp:Label ID="lblMensaje" runat="server" CssClass="alert d-block mb-0 pe-5"></asp:Label>
                                        <button type="button" class="close position-absolute" style="top:.65rem;right:.75rem;"
                                            onclick="this.closest('.mb-3').style.display='none'" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>
                                    </div>
                                </asp:Panel>

                                <!-- RESUMEN GENERAL -->
                                <div class="ld-stats-row">
                                    <asp:LinkButton ID="lnkStatViajes" runat="server" CssClass="ld-stat-tile" OnClick="lnkStatViajes_Click" CausesValidation="false">
                                        <span class="ld-stat-icon ld-stat-icon-blue"><i class="fas fa-route"></i></span>
                                        <span class="ld-stat-text">
                                            <span class="ld-stat-value"><asp:Label ID="lblStatViajesActivos" runat="server">0</asp:Label></span>
                                            <span class="ld-stat-label">Viajes Activos</span>
                                        </span>
                                    </asp:LinkButton>

                                    <asp:LinkButton ID="lnkStatLotes" runat="server" CssClass="ld-stat-tile" OnClick="lnkStatLotes_Click" CausesValidation="false">
                                        <span class="ld-stat-icon ld-stat-icon-green"><i class="fas fa-layer-group"></i></span>
                                        <span class="ld-stat-text">
                                            <span class="ld-stat-value"><asp:Label ID="lblStatLotesActivos" runat="server">0</asp:Label></span>
                                            <span class="ld-stat-label">Lotes Activos</span>
                                        </span>
                                    </asp:LinkButton>

                                    <asp:LinkButton ID="lnkStatManifiestosPendientes" runat="server" CssClass="ld-stat-tile" OnClick="lnkStatManifiestosPendientes_Click" CausesValidation="false">
                                        <span class="ld-stat-icon ld-stat-icon-amber"><i class="fas fa-passport"></i></span>
                                        <span class="ld-stat-text">
                                            <span class="ld-stat-value"><asp:Label ID="lblStatManifiestosPendientes" runat="server">0</asp:Label></span>
                                            <span class="ld-stat-label">Manifiestos Pendientes</span>
                                        </span>
                                    </asp:LinkButton>
                                </div>

                                <!-- NAVEGACIÓN PRINCIPAL -->
                                <div class="ld-toolbar mb-4">
                                    <div class="ld-toolbar-nav">
                                        <asp:Button ID="btnMostrarViajes" runat="server" 
                                            Text="Viajes Activos" 
                                            CssClass="btn btn-outline-secondary btn-nav"
                                            OnClick="btnMostrarViajes_Click"
                                            CausesValidation="false" />
                                        <asp:Button ID="btnMostrarLotes" runat="server" 
                                            Text="Lotes Registrados" 
                                            CssClass="btn btn-outline-secondary btn-nav"
                                            OnClick="btnMostrarLotes_Click"
                                            CausesValidation="false" />
                                    </div>
                                    <asp:Button ID="btnVolver" runat="server" 
                                        Text="← Nuevo Registro" 
                                        CssClass="btn btn-outline-secondary ld-toolbar-back"
                                        OnClick="btnVolver_Click"
                                        CausesValidation="false" />
                                </div>

                                <!-- PANEL PRINCIPAL: LISTA DE VIAJES ACTIVOS -->
                                <asp:Panel ID="pnlListaViajes" runat="server" Visible="true">
                                    <div class="section-card viajes-section">
                                        <div class="section-header">
                                            <div class="row align-items-center">
                                                <div class="col-md-6">
                                                    <h5 class="section-title">
                                                            <i class="fas fa-route mr-2"></i> Viajes en Progreso
                                                            <asp:Label ID="lblContadorViajes" runat="server" CssClass="badge bg-secondary text-white ml-2"></asp:Label>
                                                        </h5>
                                                </div>
                                                <div class="col-md-6">
                                                    <div class="section-actions">
                                                        <asp:Button ID="btnRefrescarViajes" runat="server" 
                                                            Text="Refrescar" 
                                                            CssClass="btn btn-outline-primary btn-action"
                                                            OnClick="btnRefrescarViajes_Click"
                                                            CausesValidation="false" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="section-content">
                                            
                                            <!-- Filtros para Viajes -->
                                            <div class="filters-container">
                                                <div class="filters-heading"><i class="fas fa-filter"></i>Filtros de búsqueda</div>
                                                <div class="row">
                                                    <div class="col-md-4">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-id-card"></i>Conductor</label>
                                                            <asp:DropDownList ID="ddlFiltroConductorViajes" runat="server" 
                                                                CssClass="form-select"
                                                                AutoPostBack="true"
                                                                OnSelectedIndexChanged="ddlFiltroConductorViajes_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="-- Todos los conductores --"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-4">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-globe-americas"></i>Ámbito</label>
                                                            <asp:DropDownList ID="ddlFiltroTipoViajes" runat="server" 
                                                                CssClass="form-select"
                                                                AutoPostBack="true"
                                                                OnSelectedIndexChanged="ddlFiltroTipoViajes_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="-- Todos los tipos --"></asp:ListItem>
                                                                <asp:ListItem Value="1" Text="Internacional"></asp:ListItem>
                                                                <asp:ListItem Value="0" Text="Nacional"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-4">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-search"></i>N° de Viaje</label>
                                                            <div class="input-group">
                                                                <asp:TextBox ID="txtBuscarViaje" runat="server" 
                                                                    CssClass="form-control" 
                                                                    placeholder="Ej: VP-2025-001"
                                                                    MaxLength="20">
                                                                </asp:TextBox>
                                                                <asp:Button ID="btnBuscarViaje" runat="server" 
                                                                    Text="Buscar" 
                                                                    CssClass="btn btn-outline-secondary"
                                                                    OnClick="btnBuscarViaje_Click"
                                                                    CausesValidation="false" />
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- Grid de Viajes -->
                                            <div class="data-grid-container">
                                                <asp:GridView ID="gvViajesActivos" runat="server" 
                                                    CssClass="table data-table"
                                                    AutoGenerateColumns="false"
                                                    EmptyDataText="No se encontraron viajes activos"
                                                    OnRowCommand="gvViajesActivos_RowCommand"
                                                    DataKeyNames="IdViajeProgreso">
                                                    <Columns>
                                                        <asp:BoundField DataField="NumeroViajeProgreso" HeaderText="N° Viaje" 
                                                            ItemStyle-CssClass="viaje-number" />
                                                        
                                                        <asp:BoundField DataField="NombreConductor" HeaderText="Conductor" />
                                                        
                                                        <asp:BoundField DataField="FechaInicio" HeaderText="Fecha Inicio" 
                                                            DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                                                        
                                                        <asp:BoundField DataField="CantidadDespachos" HeaderText="Despachos" 
                                                            ItemStyle-CssClass="text-center" />
                                                        
                                                        <asp:TemplateField HeaderText="Tipo">
                                                            <ItemTemplate>
                                                                <asp:Label runat="server" 
                                                                    Text='<%# Convert.ToBoolean(Eval("EsInternacional")) ? "Internacional" : "Nacional" %>'
                                                                    CssClass='<%# Convert.ToBoolean(Eval("EsInternacional")) ? "badge tipo-internacional" : "badge tipo-nacional" %>'>
                                                                </asp:Label>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>
                                                        
                                                        <asp:BoundField DataField="FechaUltimaActividad" HeaderText="Última Actividad" 
                                                            DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                                                        
                                                        <asp:TemplateField HeaderText="Estado">
                                                            <ItemTemplate>
                                                                <asp:Label runat="server" 
                                                                    Text='<%# Eval("EstadoViaje") %>'
                                                                    CssClass="badge estado-abierto">
                                                                </asp:Label>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>
                                                        
                                                        <asp:TemplateField HeaderText="Acciones" ItemStyle-Width="200px">
                                                            <ItemTemplate>
                                                                <div class="action-buttons">
                                                                    <asp:Button runat="server" 
                                                                       Text="Ver" 
                                                                       CssClass="btn btn-sm btn-outline-primary"
                                                                       CommandName="VerDespachos"
                                                                       CommandArgument='<%# Eval("IdViajeProgreso") %>' />

                                                                    <asp:Button runat="server" 
                                                                       Text="Finalizar" 
                                                                       CssClass="btn btn-sm btn-outline-danger"
                                                                       CommandName="FinalizarViaje"
                                                                       CommandArgument='<%# Eval("IdViajeProgreso") %>'
                                                                       OnClientClick="return confirm('¿Está seguro de finalizar este viaje? No podrá agregar más despachos.');" />
                                                                </div>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>
                                                    </Columns>
                                                    <EmptyDataTemplate>
                                                        <div class="empty-data">
                                                            <i class="fas fa-info-circle"></i>
                                                            <p>No se encontraron viajes activos con los criterios seleccionados</p>
                                                        </div>
                                                    </EmptyDataTemplate>
                                                </asp:GridView>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                                <!-- PANEL PRINCIPAL: LISTA DE LOTES REGISTRADOS -->
                                <asp:Panel ID="pnlListaLotes" runat="server" Visible="false">
                                    <div class="section-card lotes-section">
                                        <div class="section-header">
                                            <div class="row align-items-center">
                                                <div class="col-md-6">
                                                    <h5 class="section-title">
                                                            <i class="fas fa-layer-group mr-2"></i> Lotes de Despacho
                                                            <asp:Label ID="lblContadorLotes" runat="server" CssClass="badge bg-secondary text-white ml-2"></asp:Label>
                                                        </h5>
                                                </div>
                                                <div class="col-md-6">
                                                    <div class="section-actions">
                                                        <asp:Button ID="btnRefrescarLotes" runat="server" 
                                                            Text="Refrescar" 
                                                            CssClass="btn btn-outline-success btn-action"
                                                            OnClick="btnRefrescarLotes_Click"
                                                            CausesValidation="false" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="section-content">
                                            
                                            <!-- Filtros para Lotes -->
                                            <div class="filters-container">
                                                <div class="filters-heading"><i class="fas fa-filter"></i>Filtros de búsqueda</div>
                                                <div class="row mb-3">
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-building"></i>Cliente</label>
                                                            <asp:DropDownList ID="ddlFiltroClienteLotes" runat="server" 
                                                                CssClass="form-select"
                                                                AutoPostBack="true"
                                                                OnSelectedIndexChanged="ddlFiltroClienteLotes_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="-- Todos los clientes --"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-exchange-alt"></i>Tipo Operación</label>
                                                            <asp:DropDownList ID="ddlFiltroOperacionLotes" runat="server" 
                                                                CssClass="form-select"
                                                                AutoPostBack="true"
                                                                OnSelectedIndexChanged="ddlFiltroOperacionLotes_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="-- Todas las operaciones --"></asp:ListItem>
                                                                <asp:ListItem Value="CARGA" Text="Carga"></asp:ListItem>
                                                                <asp:ListItem Value="DESCARGA" Text="Descarga"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-industry"></i>Planta</label>
                                                            <%-- Opciones cargadas desde el catálogo Planta en CargarPlantasFiltro(). --%>
                                                            <asp:DropDownList ID="ddlFiltroPlantaLotes" runat="server"
                                                                CssClass="form-select"
                                                                AutoPostBack="true"
                                                                OnSelectedIndexChanged="ddlFiltroPlantaLotes_SelectedIndexChanged">
                                                            </asp:DropDownList>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-hashtag"></i>N° de Pedido</label>
                                                            <div class="input-group">
                                                                <asp:TextBox ID="txtBuscarLote" runat="server" 
                                                                    CssClass="form-control" 
                                                                    placeholder="N° Pedido"
                                                                    MaxLength="20">
                                                                </asp:TextBox>
                                                                <asp:Button ID="btnBuscarLote" runat="server" 
                                                                    Text="Buscar" 
                                                                    CssClass="btn btn-outline-secondary"
                                                                    OnClick="btnBuscarLote_Click"
                                                                    CausesValidation="false" />
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>

                                                <!-- Búsqueda de documentos: encuentra el/los lote(s) asociados a un
                                                     número de Factura, CPIC o a un conductor, aunque no sepas la
                                                     fecha ni el cliente. -->
                                                <div class="row ld-filter-row-dates">
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-receipt"></i>N° de Factura</label>
                                                            <asp:TextBox ID="txtBuscarFacturaLotes" runat="server"
                                                                CssClass="form-control"
                                                                placeholder="Ej: F222-00004267"
                                                                MaxLength="30">
                                                            </asp:TextBox>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-shipping-fast"></i>N° de CPIC</label>
                                                            <asp:TextBox ID="txtBuscarCPICLotes" runat="server"
                                                                CssClass="form-control"
                                                                placeholder="Ej: 1234567"
                                                                MaxLength="20">
                                                            </asp:TextBox>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-id-card"></i>Conductor</label>
                                                            <asp:TextBox ID="txtBuscarConductorLotes" runat="server"
                                                                CssClass="form-control"
                                                                placeholder="Nombre o apellido">
                                                            </asp:TextBox>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3 d-flex align-items-end">
                                                        <div class="form-group w-100">
                                                            <div class="filter-buttons">
                                                                <asp:Button ID="btnBuscarDocumento" runat="server"
                                                                    Text="Buscar Documento"
                                                                    CssClass="btn btn-primary btn-action flex-fill"
                                                                    OnClick="btnBuscarDocumento_Click"
                                                                    CausesValidation="false" />
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>

                                                <!-- Filtro por Fechas y Estado -->
                                                <div class="row ld-filter-row-dates">
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-calendar-alt"></i>Fecha Desde</label>
                                                            <asp:TextBox ID="txtFechaDesde" runat="server"
                                                                CssClass="form-control"
                                                                TextMode="Date">
                                                            </asp:TextBox>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-calendar-alt"></i>Fecha Hasta</label>
                                                            <asp:TextBox ID="txtFechaHasta" runat="server"
                                                                CssClass="form-control"
                                                                TextMode="Date">
                                                            </asp:TextBox>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-flag"></i>Estado del Lote</label>
                                                            <asp:DropDownList ID="ddlFiltroEstadoLotes" runat="server"
                                                                CssClass="form-select"
                                                                AutoPostBack="true"
                                                                OnSelectedIndexChanged="ddlFiltroEstadoLotes_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="-- Todos --"></asp:ListItem>
                                                                <asp:ListItem Value="ACTIVO" Text="Activos" Selected="True"></asp:ListItem>
                                                                <asp:ListItem Value="ANULADO" Text="Anulados"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3 d-flex align-items-end">
                                                        <div class="form-group w-100">
                                                            <div class="filter-buttons">
                                                                <asp:Button ID="btnFiltrarFecha" runat="server"
                                                                    Text="Filtrar"
                                                                    CssClass="btn btn-primary btn-action flex-fill"
                                                                    OnClick="btnFiltrarFecha_Click"
                                                                    CausesValidation="false" />
                                                                <asp:Button ID="btnLimpiarFiltros" runat="server"
                                                                    Text="Limpiar"
                                                                    CssClass="btn btn-outline-secondary btn-action"
                                                                    OnClick="btnLimpiarFiltros_Click"
                                                                    CausesValidation="false" />
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- Grid de Lotes -->
                                            <div class="data-grid-container">
                                                <asp:GridView ID="gvLotesRegistrados" runat="server" 
                                                    CssClass="table data-table"
                                                    AutoGenerateColumns="false"
                                                    EmptyDataText="No se encontraron lotes registrados"
                                                    OnRowCommand="gvLotesRegistrados_RowCommand">
                                                    <Columns>
                                                        <asp:BoundField DataField="FechaProgramacion" HeaderText="Fecha Prog." 
                                                            DataFormatString="{0:dd/MM/yyyy}" />
                                                        
                                                        <asp:BoundField DataField="NombreCliente" HeaderText="Cliente" />
                                                        
                                                        <asp:BoundField DataField="NumeroPedido" HeaderText="N° Pedido" 
                                                            ItemStyle-CssClass="pedido-number" />
                                                        
                                                        <asp:BoundField DataField="TipoOperacion" HeaderText="Operación" />
                                                        
                                                        <asp:TemplateField HeaderText="Ámbito">
                                                            <ItemTemplate>
                                                                <asp:Label runat="server" 
                                                                    Text='<%# Convert.ToBoolean(Eval("EsInternacional")) ? "Internacional" : "Nacional" %>'
                                                                    CssClass='<%# Convert.ToBoolean(Eval("EsInternacional")) ? "badge tipo-internacional" : "badge tipo-nacional" %>'>
                                                                </asp:Label>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>
                                                        
                                                        <asp:TemplateField HeaderText="Manifiesto" ItemStyle-CssClass="text-center">
                                                            <ItemTemplate>
                                                                <asp:Label runat="server"
                                                                    Text='<%# Eval("ManifiestoEstado") %>'
                                                                    CssClass='<%# GetManifiestoBadgeClass(Eval("ManifiestoEstado").ToString()) %>'>
                                                                </asp:Label>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>

                                                        <asp:BoundField DataField="PlantaOperacion" HeaderText="Planta" />

                                                        <asp:BoundField DataField="CantidadDespachos" HeaderText="Despachos"
                                                            ItemStyle-CssClass="text-center despachos-count" />

                                                        <asp:BoundField DataField="NumeroFactura" HeaderText="N° Factura" />
                                                        
                                                        <asp:BoundField DataField="NumeroCPIC" HeaderText="N° CPIC" />
                                                        
                                                        <asp:BoundField DataField="FechaCreacion" HeaderText="Creado" 
                                                            DataFormatString="{0:dd/MM/yyyy HH:mm}" />

                                                        <asp:TemplateField HeaderText="Estado" ItemStyle-CssClass="text-center">
                                                            <ItemTemplate>
                                                                <asp:Label runat="server"
                                                                    Text='<%# Eval("EstadoLote") %>'
                                                                    CssClass='<%# (string)Eval("EstadoLote") == "ANULADO" ? "badge bg-danger text-white" : "badge bg-success text-white" %>'>
                                                                </asp:Label>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>

                                                        <asp:TemplateField HeaderText="Acciones" ItemStyle-Width="200px">
                                                            <ItemTemplate>
                                                                <div class="action-buttons">
                                                                    <asp:Button runat="server"
                                                                        Text="Ver"
                                                                        CssClass="btn btn-sm btn-outline-primary"
                                                                        CommandName="VerDetallesLote"
                                                                        CommandArgument='<%# Eval("IdLoteVirtual") %>' />

                                                                    <asp:Button runat="server"
                                                                        Text="Documentos"
                                                                        CssClass="btn btn-sm btn-outline-warning"
                                                                        CommandName="VerManifiestosLote"
                                                                        CommandArgument='<%# Eval("IdLoteVirtual") %>' />

                                                                    <asp:Button runat="server"
                                                                        Text="Editar"
                                                                        CssClass="btn btn-sm btn-outline-secondary"
                                                                        CommandName="EditarLote"
                                                                        CommandArgument='<%# Eval("IdLoteVirtual") %>' />
                                                                </div>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>
                                                    </Columns>
                                                    <EmptyDataTemplate>
                                                        <div class="empty-data">
                                                            <i class="fas fa-info-circle"></i>
                                                            <p>No se encontraron lotes registrados con los criterios seleccionados</p>
                                                        </div>
                                                    </EmptyDataTemplate>
                                                </asp:GridView>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                                <!-- PANEL DETALLES: DESPACHOS DEL VIAJE -->
                                <asp:Panel ID="pnlDetallesViaje" runat="server" Visible="false">
                                    <div class="section-card detalle-section">
                                        <div class="section-header">
                                            <div class="row align-items-center">
                                                <div class="col-md-8">
                                                    <h5 class="section-title">
                                                        <i class="fas fa-clipboard-list"></i> Detalles del Viaje: 
                                                        <asp:Label ID="lblNumeroViajeDetalle" runat="server" CssClass="detail-identifier"></asp:Label>
                                                    </h5>
                                                    <div class="detail-info">
                                                        Conductor: <asp:Label ID="lblConductorDetalle" runat="server" CssClass="detail-value"></asp:Label> | 
                                                        Inicio: <asp:Label ID="lblFechaInicioDetalle" runat="server" CssClass="detail-value"></asp:Label>
                                                    </div>
                                                </div>
                                                <div class="col-md-4">
                                                    <div class="section-actions">
                                                        <asp:Button ID="btnFinalizarViajeDetalle" runat="server" 
                                                            Text="Finalizar Viaje" 
                                                            CssClass="btn btn-outline-danger btn-action"
                                                            OnClick="btnFinalizarViajeDetalle_Click"
                                                            OnClientClick="return confirm('¿Finalizar este viaje?');"
                                                            CausesValidation="false" />
                                                        
                                                        <asp:Button ID="btnVolverViajes" runat="server" 
                                                            Text="Volver a Viajes" 
                                                            CssClass="btn btn-secondary btn-action"
                                                            OnClick="btnVolverViajes_Click"
                                                            CausesValidation="false" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="section-content">
                                            
                                            <!-- Resumen del Viaje -->
                                            <div class="summary-cards">
                                                <div class="row">
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblTotalDespachos" runat="server" Text="0"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Total Despachos</div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblTipoViajeDetalle" runat="server"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Tipo de Viaje</div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblEstadoViajeDetalle" runat="server"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Estado</div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblUltimaActividadDetalle" runat="server"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Última Actividad</div>
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- Grid de Despachos del Viaje -->
                                            <div class="data-section">
                                                <h6 class="data-section-title">
                                                    <i class="fas fa-truck"></i> Despachos Asociados
                                                </h6>
                                                
                                                <div class="data-grid-container">
                                                    <asp:GridView ID="gvDespachosViaje" runat="server" 
                                                        CssClass="table data-table detail-table"
                                                        AutoGenerateColumns="false"
                                                        EmptyDataText="No hay despachos asociados a este viaje">
                                                        <Columns>
                                                            <asp:BoundField DataField="NumeroDespacho" HeaderText="N° Despacho" 
                                                                ItemStyle-CssClass="despacho-number" />
                                                            
                                                            <asp:BoundField DataField="FechaDespacho" HeaderText="Fecha" 
                                                                DataFormatString="{0:dd/MM/yyyy}" />
                                                            
                                                            <asp:BoundField DataField="NombreCliente" HeaderText="Cliente" />
                                                            
                                                            <asp:BoundField DataField="PlacaTracto" HeaderText="Tracto" />
                                                            
                                                            <asp:BoundField DataField="PlacaCarreta" HeaderText="Carreta" />
                                                            
                                                            <asp:BoundField DataField="TipoOperacion" HeaderText="Operación" />
                                                            
                                                            <asp:BoundField DataField="LugarOperacion" HeaderText="Planta" />
                                                            
                                                            <asp:TemplateField HeaderText="Estado">
                                                                <ItemTemplate>
                                                                    <asp:Label runat="server" 
                                                                        Text='<%# Eval("EstadoDespacho") %>'
                                                                        CssClass='<%# GetEstadoDespachoClass(Eval("EstadoDespacho").ToString()) %>'>
                                                                    </asp:Label>
                                                                </ItemTemplate>
                                                            </asp:TemplateField>
                                                            
                                                            <asp:BoundField DataField="GuiaRemitente" HeaderText="Guía Remitente" />
                                                            
                                                            <asp:BoundField DataField="GuiaTransportista" HeaderText="Guía Transportista" />
                                                        </Columns>
                                                        <EmptyDataTemplate>
                                                            <div class="empty-data">
                                                                <i class="fas fa-exclamation-circle"></i>
                                                                <p>Este viaje no tiene despachos asociados</p>
                                                            </div>
                                                        </EmptyDataTemplate>
                                                    </asp:GridView>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                                <!-- PANEL EDICIÓN DE LOTE -->
                                <asp:Panel ID="pnlEdicionLote" runat="server" Visible="false">
                                    <div class="section-card edit-section">
                                        <div class="section-header">
                                            <div class="row align-items-center">
                                                <div class="col-md-8">
                                                    <h5 class="section-title">
                                                        <i class="fas fa-edit"></i> Editar Datos Base del Lote
                                                    </h5>
                                                    <div class="detail-info">
                                                        Lote: <asp:Label ID="lblIdentificadorLote" runat="server" CssClass="detail-value"></asp:Label> | 
                                                        Despachos Afectados: <asp:Label ID="lblDespachosSAfectados" runat="server" CssClass="detail-value affected-count"></asp:Label>
                                                    </div>
                                                </div>
                                                <div class="col-md-4">
                                                    <div class="section-actions">
                                                        <asp:Button ID="btnCancelarEdicion" runat="server" 
                                                            Text="Cancelar" 
                                                            CssClass="btn btn-secondary btn-action"
                                                            OnClick="btnCancelarEdicion_Click"
                                                            CausesValidation="false" />
                                                        
                                                        <asp:Button ID="btnVolverLotes" runat="server" 
                                                            Text="Volver a Lotes" 
                                                            CssClass="btn btn-outline-secondary btn-action"
                                                            OnClick="btnVolverLotes_Click"
                                                            CausesValidation="false" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="section-content">
                                            
                                            <!-- Advertencia -->
                                            <div class="alert alert-warning edit-warning">
                                                <i class="fas fa-exclamation-triangle"></i>
                                                <strong>Importante:</strong> Los cambios afectarán a todos los despachos de este lote. 
                                                Verifique cuidadosamente antes de guardar.
                                            </div>

                                            <div class="edit-form">

                                                <!-- Fila 1: Campos básicos en 4 columnas -->
                                                <div class="row mb-3">
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label required">Fecha de Programación:</label>
                                                            <asp:TextBox ID="txtFechaProgramacionEdit" runat="server" 
                                                                CssClass="form-control" 
                                                                TextMode="Date">
                                                            </asp:TextBox>
                                                            <asp:RequiredFieldValidator ID="rfvFechaProgramacionEdit" runat="server"
                                                                ControlToValidate="txtFechaProgramacionEdit"
                                                                ErrorMessage="Debe seleccionar una fecha de programación"
                                                                CssClass="field-error"
                                                                Display="Dynamic"
                                                                ValidationGroup="EdicionLote">
                                                            </asp:RequiredFieldValidator>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label">Cliente:</label>
                                                            <asp:TextBox ID="txtClienteEdit" runat="server" 
                                                                CssClass="form-control readonly-field" 
                                                                ReadOnly="true">
                                                            </asp:TextBox>
                                                            <small class="form-text">No se puede modificar el cliente</small>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label">N° de Pedido:</label>
                                                            <asp:TextBox ID="txtNumeroPedidoEdit" runat="server" 
                                                                CssClass="form-control" 
                                                                placeholder="Ej: 1234567890"
                                                                MaxLength="10">
                                                            </asp:TextBox>
                                                            <small class="form-text">Exactamente 10 dígitos numéricos</small>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label required">Planta de Operación:</label>
                                                            <%-- Opciones cargadas desde el catálogo Planta en CargarPlantasFiltro(). --%>
                                                            <asp:DropDownList ID="ddlPlantaEdit" runat="server" CssClass="form-select">
                                                            </asp:DropDownList>
                                                            <asp:RequiredFieldValidator ID="rfvPlantaEdit" runat="server"
                                                                ControlToValidate="ddlPlantaEdit"
                                                                InitialValue=""
                                                                ErrorMessage="Debe seleccionar una planta"
                                                                CssClass="field-error"
                                                                Display="Dynamic"
                                                                ValidationGroup="EdicionLote">
                                                            </asp:RequiredFieldValidator>
                                                        </div>
                                                    </div>
                                                </div>

                                                <!-- Fila 2: Info no editable + Documentación -->
                                                <div class="row mb-3">
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label required">Tipo de Operación:</label>
                                                            <asp:DropDownList ID="ddlTipoOperacionEdit" runat="server" CssClass="form-select"
                                                                AutoPostBack="true" OnSelectedIndexChanged="ddlTipoOperacionEdit_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="-- Seleccione --"></asp:ListItem>
                                                                <asp:ListItem Value="CARGA" Text="Carga"></asp:ListItem>
                                                                <asp:ListItem Value="DESCARGA" Text="Descarga"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                        <div class="form-group mt-2">
                                                            <label class="form-label required">Ámbito:</label>
                                                            <asp:RadioButtonList ID="rblAmbitoEdit" runat="server" CssClass="rbl-inline"
                                                                AutoPostBack="true" OnSelectedIndexChanged="rblAmbitoEdit_SelectedIndexChanged"
                                                                RepeatDirection="Horizontal" RepeatLayout="Flow">
                                                                <asp:ListItem Value="0" Text="Nacional &nbsp;"></asp:ListItem>
                                                                <asp:ListItem Value="1" Text="Internacional"></asp:ListItem>
                                                            </asp:RadioButtonList>
                                                        </div>
                                                        <small class="form-text text-warning mt-1">
                                                            <i class="fas fa-exclamation-triangle"></i> Al cambiar estos valores los paneles de documentos se actualizan automáticamente.
                                                        </small>
                                                    </div>
                                                    <div class="col-md-4">
                                                        <!-- Panel Factura -->
                                                        <asp:Panel ID="pnlFacturaEdit" runat="server" CssClass="doc-panel h-100" Visible="false">
                                                            <div class="doc-panel-header">
                                                                <i class="fas fa-receipt"></i> Datos de Factura
                                                            </div>
                                                            <div class="doc-panel-content">
                                                                <div class="form-group">
                                                                    <label class="form-label">N° Factura:</label>
                                                                    <asp:TextBox ID="txtNumeroFacturaEdit" runat="server" 
                                                                        CssClass="form-control" 
                                                                        placeholder="Ej: F222 - 00004267">
                                                                    </asp:TextBox>
                                                                </div>
                                                                <div class="form-group">
                                                                    <label class="form-label">Fecha Emisión:</label>
                                                                    <asp:TextBox ID="txtFechaEmisionFacturaEdit" runat="server" 
                                                                        CssClass="form-control" 
                                                                        TextMode="Date">
                                                                    </asp:TextBox>
                                                                </div>
                                                                <div class="form-group">
                                                                    <label class="form-label">Valor Total:</label>
                                                                    <asp:TextBox ID="txtValorTotalFacturaEdit" runat="server" 
                                                                        CssClass="form-control" 
                                                                        placeholder="0.00"
                                                                        TextMode="Number"
                                                                        step="0.01">
                                                                    </asp:TextBox>
                                                                </div>
                                                            </div>
                                                        </asp:Panel>
                                                    </div>
                                                    <div class="col-md-5">
                                                        <!-- Panel CPIC -->
                                                        <asp:Panel ID="pnlCPICEdit" runat="server" CssClass="doc-panel h-100" Visible="false">
                                                            <div class="doc-panel-header">
                                                                <i class="fas fa-shipping-fast"></i> Datos de CPIC
                                                            </div>
                                                            <div class="doc-panel-content">
                                                                <div class="form-group">
                                                                    <label class="form-label">N° CPIC:</label>
                                                                    <asp:TextBox ID="txtNumeroCPICEdit" runat="server" 
                                                                        CssClass="form-control" 
                                                                        placeholder="Ej: 1234567"
                                                                        MaxLength="7">
                                                                    </asp:TextBox>
                                                                </div>
                                                                <div class="form-group">
                                                                    <label class="form-label">Fecha Emisión CPIC:</label>
                                                                    <asp:TextBox ID="txtFechaEmisionCPICEdit" runat="server" 
                                                                        CssClass="form-control" 
                                                                        TextMode="Date">
                                                                    </asp:TextBox>
                                                                </div>
                                                                <div class="form-group">
                                                                    <label class="form-label">Valor Flete:</label>
                                                                    <asp:TextBox ID="txtValorFleteEdit" runat="server" 
                                                                        CssClass="form-control" 
                                                                        placeholder="0.00"
                                                                        TextMode="Number"
                                                                        step="0.01">
                                                                    </asp:TextBox>
                                                                </div>
                                                            </div>
                                                        </asp:Panel>
                                                    </div>
                                                </div>

                                                <!-- Fila 3: Conductores (ancho completo) -->
                                                <div class="row mb-3">
                                                    <div class="col-12">
                                                        <div class="form-section">
                                                            <h6 class="form-section-title">
                                                                <i class="fas fa-users"></i> Conductores por Despacho
                                                            </h6>
                                                            <p class="form-text mb-2">Puede cambiar el conductor de cada despacho individualmente. Los cambios se aplican al guardar.</p>
                                                            <div class="conductores-edit-container">
                                                                <div class="tabla-scroll-wrapper">
                                                                    <asp:GridView ID="gvConductoresLote" runat="server" 
                                                                        CssClass="table table-hover"
                                                                        AutoGenerateColumns="false"
                                                                        DataKeyNames="IdDespacho"
                                                                        OnRowDataBound="gvConductoresLote_RowDataBound"
                                                                        GridLines="None">
                                                                        <Columns>
                                                                            <asp:BoundField DataField="IdDespacho" HeaderText="ID" Visible="false" />

                                                                            <asp:BoundField DataField="NumeroDespacho" HeaderText="N° Despacho" 
                                                                                ItemStyle-CssClass="despacho-number fw-bold" />

                                                                            <asp:BoundField DataField="FechaDespacho" HeaderText="Fecha" 
                                                                                DataFormatString="{0:dd/MM/yyyy}" />

                                                                            <asp:TemplateField HeaderText="Conductor Actual">
                                                                                <ItemTemplate>
                                                                                    <span class="conductor-badge-actual">
                                                                                        <%# Eval("NombreConductorActual") %>
                                                                                    </span>
                                                                                </ItemTemplate>
                                                                            </asp:TemplateField>

                                                                            <asp:TemplateField HeaderText="Cambiar Conductor">
                                                                                <ItemTemplate>
                                                                                    <asp:DropDownList ID="ddlConductorDespacho" runat="server" 
                                                                                        CssClass="form-select conductor-select">
                                                                                    </asp:DropDownList>
                                                                                </ItemTemplate>
                                                                            </asp:TemplateField>
                                                                        </Columns>
                                                                    </asp:GridView>
                                                                </div>
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>

                                                <!-- Botones de Acción -->
                                                <div class="form-actions">
                                                    <div class="row">
                                                        <div class="col-md-6 text-start">
                                                            <asp:Button ID="btnEliminarLote" runat="server" 
                                                                Text="Eliminar Lote" 
                                                                CssClass="btn btn-outline-danger"
                                                                OnClick="btnEliminarLote_Click"
                                                                CausesValidation="false"
                                                                OnClientClick="return confirm('⚠️ ADVERTENCIA: Se eliminarán TODOS los despachos de este lote.\n\n¿Está COMPLETAMENTE seguro de eliminar este lote?');" />
                                                            <asp:Button ID="btnAnularLote" runat="server"
                                                                Text="Anular Lote"
                                                                CssClass="btn btn-warning ml-2"
                                                                OnClick="btnAnularLote_Click"
                                                                CausesValidation="false"
                                                                OnClientClick="return confirm('⚠️ ¿Anular este lote?\n\nTodos sus despachos y viajes activos quedarán como ANULADO.\nLos registros se conservan para auditoría.');" />
                                                        </div>
                                                        <div class="col-md-6 text-end">
                                                            <asp:Button ID="btnGuardarCambios" runat="server" 
                                                                Text="Guardar Cambios" 
                                                                CssClass="btn btn-primary"
                                                                OnClick="btnGuardarCambios_Click"
                                                                ValidationGroup="EdicionLote"
                                                                OnClientClick="return confirm('¿Está seguro de aplicar estos cambios a todo el lote?');" />
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                                <!-- PANEL DETALLES DE LOTE -->
                                <asp:Panel ID="pnlDetallesLote" runat="server" Visible="false">
                                    <div class="section-card detalle-section">
                                        <div class="section-header">
                                            <div class="row align-items-center">
                                                <div class="col-md-8">
                                                    <h5 class="section-title">
                                                        <i class="fas fa-eye"></i> Detalles del Lote
                                                    </h5>
                                                    <div class="detail-info">
                                                        Cliente: <asp:Label ID="lblClienteDetalleLote" runat="server" CssClass="detail-value"></asp:Label> | 
                                                        Pedido: <asp:Label ID="lblPedidoDetalleLote" runat="server" CssClass="detail-value"></asp:Label>
                                                    </div>
                                                </div>
                                                <div class="col-md-4">
                                                    <div class="section-actions">
                                                        <asp:Button ID="btnGestionarManifiestos" runat="server"
                                                            Text="Documentos"
                                                            CssClass="btn btn-outline-primary btn-action"
                                                            OnClick="btnGestionarManifiestos_Click"
                                                            CausesValidation="false" />

                                                        <asp:Button ID="btnEditarDesdeDetal" runat="server"
                                                            Text="Editar Lote"
                                                            CssClass="btn btn-outline-secondary btn-action"
                                                            OnClick="btnEditarDesdeDetal_Click"
                                                            CausesValidation="false" />

                                                        <asp:Button ID="btnVolverLotesDetalle" runat="server"
                                                            Text="Volver a Lotes"
                                                            CssClass="btn btn-secondary btn-action"
                                                            OnClick="btnVolverLotesDetalle_Click"
                                                            CausesValidation="false" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="section-content">
                                            
                                            <!-- Información General del Lote -->
                                            <div class="summary-cards">
                                                <div class="row">
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblTotalDespachosLote" runat="server" Text="0"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Total Despachos</div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblOperacionDetalleLote" runat="server"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Operación</div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblPlantaDetalleLote" runat="server"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Planta</div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblFechaCreacionDetalle" runat="server"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Fecha Creación</div>
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- Grid de Despachos del Lote -->
                                            <div class="data-section">
                                                <h6 class="data-section-title">
                                                    <i class="fas fa-truck"></i> Despachos del Lote
                                                </h6>
                                                
                                                <div class="data-grid-container">
                                                    <asp:GridView ID="gvDespachosLote" runat="server" 
                                                        CssClass="table data-table detail-table"
                                                        AutoGenerateColumns="false"
                                                        EmptyDataText="No hay despachos asociados a este lote">
                                                        <Columns>
                                                            <asp:BoundField DataField="NumeroDespacho" HeaderText="N° Despacho" 
                                                                ItemStyle-CssClass="despacho-number" />
                                                            
                                                            <asp:BoundField DataField="FechaDespacho" HeaderText="Fecha" 
                                                                DataFormatString="{0:dd/MM/yyyy}" />
                                                            
                                                            <asp:BoundField DataField="NombreConductor" HeaderText="Conductor" />
                                                            
                                                            <asp:BoundField DataField="PlacaTracto" HeaderText="Tracto" />
                                                            
                                                            <asp:BoundField DataField="PlacaCarreta" HeaderText="Carreta" />
                                                            
                                                            <asp:TemplateField HeaderText="Estado">
                                                                <ItemTemplate>
                                                                    <asp:Label runat="server" 
                                                                        Text='<%# Eval("EstadoDespacho") %>'
                                                                        CssClass='<%# GetEstadoDespachoClass(Eval("EstadoDespacho").ToString()) %>'>
                                                                    </asp:Label>
                                                                </ItemTemplate>
                                                            </asp:TemplateField>
                                                            
                                                            <asp:BoundField DataField="GuiaRemitente" HeaderText="Guía Remitente" />
                                                            
                                                            <asp:BoundField DataField="GuiaTransportista" HeaderText="Guía Transportista" />

                                                            <asp:BoundField DataField="NumeroViaje" HeaderText="N° Viaje" />
                                                        </Columns>
                                                        <EmptyDataTemplate>
                                                            <div class="empty-data">
                                                                <i class="fas fa-exclamation-circle"></i>
                                                                <p>Este lote no tiene despachos asociados</p>
                                                            </div>
                                                        </EmptyDataTemplate>
                                                    </asp:GridView>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                                <!-- ====== MANIFIESTOS DEL LOTE (viajes internacionales) ====== -->
                                <asp:Panel ID="pnlManifiestosLote" runat="server" Visible="false">
                                    <div class="section-card detalle-section">
                                        <div class="section-header">
                                            <div class="row align-items-center">
                                                <div class="col-md-8">
                                                    <h5 class="section-title">
                                                        <i class="fas fa-file-alt"></i> Documentos del Lote
                                                    </h5>
                                                    <div class="detail-info">
                                                        Cliente: <asp:Label ID="lblClienteManifiestos" runat="server" CssClass="detail-value"></asp:Label> |
                                                        Pedido: <asp:Label ID="lblPedidoManifiestos" runat="server" CssClass="detail-value"></asp:Label>
                                                    </div>
                                                </div>
                                                <div class="col-md-4">
                                                    <div class="section-actions">
                                                        <asp:Button ID="btnVolverManifiestos" runat="server"
                                                            Text="Volver a Detalles"
                                                            CssClass="btn btn-secondary btn-action"
                                                            OnClick="btnVolverManifiestos_Click"
                                                            CausesValidation="false" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="section-content">

                                            <asp:Panel ID="pnlMensajeManifiesto" runat="server" Visible="false" CssClass="mb-3">
                                                <asp:Label ID="lblMensajeManifiesto" runat="server" CssClass="alert d-block"></asp:Label>
                                            </asp:Panel>

                                            <!-- Documentos base del lote (Factura/CPIC), si fueron adjuntados -->
                                            <div class="data-section mb-3">
                                                <h6 class="data-section-title"><i class="fas fa-file-invoice"></i> Documentos Base</h6>
                                                <div class="rd-notice" style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:5px;padding:.7rem 1rem;">
                                                    <asp:Panel ID="pnlDocFacturaManifiesto" runat="server" Visible="false" CssClass="mb-1">
                                                        <i class="fas fa-receipt mr-1"></i> Factura:
                                                        <asp:LinkButton ID="lnkVerDocFactura" runat="server" OnClick="lnkVerDocFactura_Click" CausesValidation="false"></asp:LinkButton>
                                                    </asp:Panel>
                                                    <asp:Panel ID="pnlDocCpicManifiesto" runat="server" Visible="false">
                                                        <i class="fas fa-shipping-fast mr-1"></i> CPIC:
                                                        <asp:LinkButton ID="lnkVerDocCpic" runat="server" OnClick="lnkVerDocCpic_Click" CausesValidation="false"></asp:LinkButton>
                                                    </asp:Panel>
                                                    <asp:Label ID="lblSinDocsBase" runat="server" Text="Sin documentos base adjuntados." Visible="false" CssClass="text-muted"></asp:Label>
                                                </div>
                                            </div>

                                            <!-- Manifiesto por conductor/despacho: solo aplica a viajes internacionales -->
                                            <asp:Panel ID="pnlAvisoNacionalSinManifiesto" runat="server" Visible="false" CssClass="rd-notice" Style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:5px;padding:.7rem 1rem;">
                                                <i class="fas fa-info-circle mr-1"></i> Este lote es nacional: el manifiesto de aduana no aplica.
                                            </asp:Panel>

                                            <asp:Panel ID="pnlSeccionManifiestoConductor" runat="server">
                                            <div class="data-section">
                                                <h6 class="data-section-title"><i class="fas fa-users"></i> Manifiesto por Conductor</h6>
                                                <p class="text-muted small">Adjunte o reemplace el manifiesto de cruce/retorno de cada conductor a medida que avanza el viaje.</p>

                                                <div class="data-grid-container">
                                                    <asp:GridView ID="gvManifiestosLote" runat="server"
                                                        CssClass="table data-table detail-table"
                                                        AutoGenerateColumns="false"
                                                        DataKeyNames="IdDespacho"
                                                        OnRowCommand="gvManifiestosLote_RowCommand"
                                                        EmptyDataText="No hay conductores en este lote">
                                                        <Columns>
                                                            <asp:BoundField DataField="NumeroDespacho" HeaderText="N° Despacho" />
                                                            <asp:BoundField DataField="NombreConductor" HeaderText="Conductor" />

                                                            <asp:TemplateField HeaderText="Manifiesto de Cruce">
                                                                <ItemTemplate>
                                                                    <asp:LinkButton runat="server" CommandName="VerManifiesto" CommandArgument='<%# Eval("CruceIdDocumento") %>'
                                                                        Visible='<%# Eval("CruceIdDocumento") != null %>' CausesValidation="false">
                                                                        <i class="fas fa-eye"></i> <%# Eval("CruceNombreOriginal") %>
                                                                    </asp:LinkButton>
                                                                    <span class="text-muted small" style='<%# Eval("CruceIdDocumento") != null ? "display:none" : "" %>'>Sin adjuntar</span>
                                                                    <br />
                                                                    <asp:FileUpload runat="server" ID="fileCruceFila" CssClass="form-control form-control-sm mt-1" accept=".pdf,.jpg,.jpeg,.png" />
                                                                </ItemTemplate>
                                                            </asp:TemplateField>

                                                            <asp:TemplateField HeaderText="Manifiesto de Retorno">
                                                                <ItemTemplate>
                                                                    <asp:LinkButton runat="server" CommandName="VerManifiesto" CommandArgument='<%# Eval("RetornoIdDocumento") %>'
                                                                        Visible='<%# Eval("RetornoIdDocumento") != null %>' CausesValidation="false">
                                                                        <i class="fas fa-eye"></i> <%# Eval("RetornoNombreOriginal") %>
                                                                    </asp:LinkButton>
                                                                    <span class="text-muted small" style='<%# Eval("RetornoIdDocumento") != null ? "display:none" : "" %>'>Sin adjuntar</span>
                                                                    <br />
                                                                    <asp:FileUpload runat="server" ID="fileRetornoFila" CssClass="form-control form-control-sm mt-1" accept=".pdf,.jpg,.jpeg,.png" />
                                                                </ItemTemplate>
                                                            </asp:TemplateField>

                                                            <asp:TemplateField HeaderText="Acción">
                                                                <ItemTemplate>
                                                                    <asp:Button runat="server" Text="Guardar" CssClass="btn btn-primary btn-sm"
                                                                        CommandName="GuardarManifiesto" CommandArgument='<%# Eval("IdDespacho") %>' />
                                                                </ItemTemplate>
                                                            </asp:TemplateField>
                                                        </Columns>
                                                        <EmptyDataTemplate>
                                                            <div class="empty-data">
                                                                <i class="fas fa-exclamation-circle"></i>
                                                                <p>Este lote no tiene despachos asociados</p>
                                                            </div>
                                                        </EmptyDataTemplate>
                                                    </asp:GridView>
                                                </div>
                                            </div>
                                            </asp:Panel>
                                        </div>
                                    </div>
                                </asp:Panel>

                            </ContentTemplate>
                            <Triggers>
                                <asp:AsyncPostBackTrigger ControlID="lnkStatViajes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="lnkStatLotes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="lnkStatManifiestosPendientes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnMostrarViajes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnRefrescarViajes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="ddlFiltroConductorViajes" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="ddlFiltroTipoViajes" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="btnBuscarViaje" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="gvViajesActivos" EventName="RowCommand" />
                                <asp:AsyncPostBackTrigger ControlID="btnVolverViajes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnFinalizarViajeDetalle" EventName="Click" />
                                
                                <asp:AsyncPostBackTrigger ControlID="btnMostrarLotes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnRefrescarLotes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="ddlFiltroClienteLotes" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="ddlFiltroOperacionLotes" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="ddlFiltroPlantaLotes" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="btnBuscarLote" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnBuscarDocumento" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnFiltrarFecha" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnLimpiarFiltros" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="gvLotesRegistrados" EventName="RowCommand" />
                                
                                <asp:AsyncPostBackTrigger ControlID="btnCancelarEdicion" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnVolverLotes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnGuardarCambios" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnEliminarLote" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnAnularLote" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="ddlFiltroEstadoLotes" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="ddlTipoOperacionEdit" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="rblAmbitoEdit" EventName="SelectedIndexChanged" />

                                <asp:AsyncPostBackTrigger ControlID="btnEditarDesdeDetal" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnVolverLotesDetalle" EventName="Click" />

                                <asp:AsyncPostBackTrigger ControlID="btnGestionarManifiestos" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnVolverManifiestos" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="lnkVerDocFactura" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="lnkVerDocCpic" EventName="Click" />
                                <asp:PostBackTrigger ControlID="gvManifiestosLote" />

                                <asp:PostBackTrigger ControlID="btnVolver" />
                            </Triggers>
                        </asp:UpdatePanel>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- Loading Panel -->
    <asp:UpdateProgress ID="UpdateProgress1" runat="server" AssociatedUpdatePanelID="UpdatePanelMain">
        <ProgressTemplate>
            <div class="loading-overlay">
                <div class="loading-content">
                    <div class="spinner-border text-primary" role="status">
                        <span class="visually-hidden">Cargando...</span>
                    </div>
                    <div class="mt-2">Procesando...</div>
                </div>
            </div>
        </ProgressTemplate>
    </asp:UpdateProgress>

</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="ScriptsSection" runat="server">
    <!-- Select2 CSS -->
    <link href="https://cdn.jsdelivr.net/npm/select2@4.1.0-rc.0/dist/css/select2.min.css" rel="stylesheet" />
    <!-- Select2 JS -->
    <script src="https://cdn.jsdelivr.net/npm/select2@4.1.0-rc.0/dist/js/select2.min.js"></script>
    
    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            txtFechaDesde: '<%= txtFechaDesde.ClientID %>',
            txtFechaHasta: '<%= txtFechaHasta.ClientID %>',
            txtNumeroPedidoEdit: '<%= txtNumeroPedidoEdit.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/ListaDespachos.js") %>"></script>
    
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/ListaDespachos-2.css") %>" rel="stylesheet" />
</asp:Content>
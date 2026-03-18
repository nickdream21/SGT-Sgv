<%@ Page Title="Gestión de Viajes y Lotes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ListaDespachos.aspx.cs" Inherits="WebSGV.Views.ListaDespachos" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

<style type="text/css">
/* ---- Lista Despachos - Minimal Professional ---- */
.ld-page-header { background: #1e293b; color: #f8fafc; border-radius: 5px 5px 0 0; padding: .85rem 1.25rem; }
.ld-page-header h4 { margin: 0; font-size: 1rem; font-weight: 600; letter-spacing: .01em; }
.ld-toolbar { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: .5rem; padding: .65rem 1rem; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; margin-bottom: 1.25rem; }
.ld-toolbar-nav { display: flex; gap: .3rem; flex-wrap: wrap; }
.ld-toolbar-nav .btn, .ld-toolbar-back { font-size: .82rem; padding: .32rem .8rem; font-weight: 500; }
.viajes-section { border-left: none !important; border-top: 3px solid #1e40af !important; }
.lotes-section { border-left: none !important; border-top: 3px solid #16a34a !important; }
.detalle-section { border-left: none !important; border-top: 3px solid #374151 !important; }
.edit-section { border-left: none !important; border-top: 3px solid #374151 !important; }
.section-title { font-size: .9rem !important; font-weight: 600 !important; }
.tipo-internacional { background-color: #e0e7ff !important; color: #3730a3 !important; font-weight: 500; }
.tipo-nacional { background-color: #f0fdf4 !important; color: #166534 !important; border: 1px solid #bbf7d0 !important; font-weight: 500; }
.estado-abierto { background-color: #dcfce7 !important; color: #166534 !important; }
.edit-warning { background: #fffbeb !important; border-color: #fde68a !important; color: #92400e !important; }
.info-panel { background: #f8fafc !important; border: 1px solid #e2e8f0 !important; }
.info-title { color: #374151 !important; }
.info-content { color: #374151 !important; }
.conductores-edit-container { border: 1px solid #e2e8f0 !important; background: #f8fafc !important; }
.conductores-edit-container thead th { background: #f1f5f9 !important; color: #1e293b !important; }
.data-table thead th { background: #f1f5f9 !important; color: #374151 !important; font-weight: 600 !important; border-bottom: 2px solid #e2e8f0 !important; }
.card:hover { transform: none !important; box-shadow: 0 2px 8px rgba(0,0,0,.1) !important; }
</style>

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
                        <asp:UpdatePanel ID="UpdatePanelMain" runat="server" UpdateMode="Conditional">
                            <ContentTemplate>
                                
                                <!-- Mensajes -->
                                <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-3">
                                    <asp:Label ID="lblMensaje" runat="server" CssClass="alert"></asp:Label>
                                </asp:Panel>

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
                                                            <asp:Label ID="lblContadorViajes" runat="server" CssClass="badge badge-secondary ml-2"></asp:Label>
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
                                                <div class="row">
                                                    <div class="col-md-4">
                                                        <div class="form-group">
                                                            <label class="form-label">Filtrar por Conductor:</label>
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
                                                            <label class="form-label">Filtrar por Tipo:</label>
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
                                                            <label class="form-label">Buscar por N° Viaje:</label>
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
                                                            <asp:Label ID="lblContadorLotes" runat="server" CssClass="badge badge-secondary ml-2"></asp:Label>
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
                                                <div class="row mb-3">
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label">Filtrar por Cliente:</label>
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
                                                            <label class="form-label">Tipo Operación:</label>
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
                                                            <label class="form-label">Planta:</label>
                                                            <asp:DropDownList ID="ddlFiltroPlantaLotes" runat="server" 
                                                                CssClass="form-select"
                                                                AutoPostBack="true"
                                                                OnSelectedIndexChanged="ddlFiltroPlantaLotes_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="-- Todas las plantas --"></asp:ListItem>
                                                                <asp:ListItem Value="Lima" Text="Lima"></asp:ListItem>
                                                                <asp:ListItem Value="Guayaquil" Text="Guayaquil"></asp:ListItem>
                                                                <asp:ListItem Value="Trujillo" Text="Trujillo"></asp:ListItem>
                                                                <asp:ListItem Value="Quito" Text="Quito"></asp:ListItem>
                                                                <asp:ListItem Value="Chiclayo" Text="Chiclayo"></asp:ListItem>
                                                                <asp:ListItem Value="Manta" Text="Manta"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label">Buscar por Pedido:</label>
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

                                                <!-- Filtro por Fechas -->
                                                <div class="row">
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label">Fecha Desde:</label>
                                                            <asp:TextBox ID="txtFechaDesde" runat="server" 
                                                                CssClass="form-control" 
                                                                TextMode="Date">
                                                            </asp:TextBox>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label">Fecha Hasta:</label>
                                                            <asp:TextBox ID="txtFechaHasta" runat="server" 
                                                                CssClass="form-control" 
                                                                TextMode="Date">
                                                            </asp:TextBox>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label">Estado del Lote:</label>
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
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label">&nbsp;</label>
                                                            <div class="filter-buttons">
                                                                <asp:Button ID="btnFiltrarFecha" runat="server" 
                                                                    Text="Filtrar por Fecha" 
                                                                    CssClass="btn btn-primary btn-action"
                                                                    OnClick="btnFiltrarFecha_Click"
                                                                    CausesValidation="false" />
                                                                <asp:Button ID="btnLimpiarFiltros" runat="server" 
                                                                    Text="Limpiar" 
                                                                    CssClass="btn btn-secondary btn-action"
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
                                                                    CssClass='<%# (string)Eval("EstadoLote") == "ANULADO" ? "badge badge-danger" : "badge badge-success" %>'>
                                                                </asp:Label>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>

                                                        <asp:TemplateField HeaderText="Acciones" ItemStyle-Width="160px">
                                                            <ItemTemplate>
                                                                <div class="action-buttons">
                                                                    <asp:Button runat="server" 
                                                                        Text="Editar" 
                                                                        CssClass="btn btn-sm btn-outline-secondary"
                                                                        CommandName="EditarLote"
                                                                        CommandArgument='<%# Eval("IdLoteVirtual") %>' />

                                                                    <asp:Button runat="server" 
                                                                        Text="Ver" 
                                                                        CssClass="btn btn-sm btn-outline-primary"
                                                                        CommandName="VerDetallesLote"
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
                                                            <asp:RequiredFieldValidator ID="RequiredFieldValidator1" runat="server"
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
                                                            <asp:DropDownList ID="ddlPlantaEdit" runat="server" CssClass="form-select">
                                                                <asp:ListItem Value="" Text="-- Seleccione planta --"></asp:ListItem>
                                                                <asp:ListItem Value="Lima" Text="Lima"></asp:ListItem>
                                                                <asp:ListItem Value="Guayaquil" Text="Guayaquil"></asp:ListItem>
                                                                <asp:ListItem Value="Trujillo" Text="Trujillo"></asp:ListItem>
                                                                <asp:ListItem Value="Quito" Text="Quito"></asp:ListItem>
                                                                <asp:ListItem Value="Chiclayo" Text="Chiclayo"></asp:ListItem>
                                                                <asp:ListItem Value="Manta" Text="Manta"></asp:ListItem>
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

                            </ContentTemplate>
                            <Triggers>
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
    
    <script type="text/javascript">
        // Inicializar Select2 en los dropdowns de conductores
        function inicializarSelect2Conductores() {
            $('.conductor-select').select2({
                width: '100%',
                placeholder: 'Buscar conductor...',
                allowClear: false,
                language: {
                    noResults: function() {
                        return "No se encontraron conductores";
                    },
                    searching: function() {
                        return "Buscando...";
                    }
                }
            });
        }

        $(document).ready(function () {
            inicializarSelect2Conductores();
            autoHideMessages();
            establecerFechasPorDefecto();
        });

        var prm = Sys.WebForms.PageRequestManager.getInstance();
        prm.add_endRequest(function () {
            inicializarSelect2Conductores();
            autoHideMessages();
        });

        function autoHideMessages() {
            setTimeout(function () {
                $('.alert').fadeOut('slow');
            }, 5000);
        }

        function establecerFechasPorDefecto() {
            var hoy = new Date();
            var primerDiaMes = new Date(hoy.getFullYear(), hoy.getMonth(), 1);

            var fechaDesde = document.getElementById('<%= txtFechaDesde.ClientID %>');
            var fechaHasta = document.getElementById('<%= txtFechaHasta.ClientID %>');

            if (fechaDesde && fechaDesde.value === '') {
                fechaDesde.value = primerDiaMes.toISOString().substr(0, 10);
            }

            if (fechaHasta && fechaHasta.value === '') {
                fechaHasta.value = hoy.toISOString().substr(0, 10);
            }
        }

        function confirmarAccion(mensaje) {
            return confirm(mensaje);
        }

        function validarNumeroPedido(input) {
            var valor = input.value.replace(/[^0-9]/g, '');
            input.value = valor;

            if (valor.length > 0 && valor.length !== 10) {
                input.style.borderColor = '#dc3545';
                return false;
            } else {
                input.style.borderColor = '#ced4da';
                return true;
            }
        }

        $(document).on('input', '#<%= txtNumeroPedidoEdit.ClientID %>', function () {
            validarNumeroPedido(this);
        });
    </script>
    
    <style>
        /* === RESET Y BASE === */
        * {
            box-sizing: border-box;
        }
        
        /* === LAYOUT PRINCIPAL === */
        .main-card {
            border: none;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            border-radius: 8px;
            margin-bottom: 2rem;
        }
        
        .container-fluid {
            padding: 1rem;
        }

        /* === NAVEGACIÓN === */
        .navigation-card {
            background: #f8f9fa;
            border: 1px solid #dee2e6;
            border-radius: 6px;
            padding: 1rem;
        }

        .nav-header {
            margin: 0;
        }

        .nav-title {
            color: #6c757d;
            font-weight: 600;
            margin: 0;
        }

        .nav-buttons {
            text-align: right;
        }

        .btn-nav {
            margin-left: 0.5rem;
            padding: 0.375rem 0.75rem;
            font-size: 0.875rem;
            border-radius: 4px;
        }

        /* === SECCIONES === */
        .section-card {
            background: white;
            border: 1px solid #dee2e6;
            border-radius: 6px;
            margin-bottom: 1.5rem;
        }

        .viajes-section {
            border-left: 4px solid #007bff;
        }

        .lotes-section {
            border-left: 4px solid #28a745;
        }

        .detalle-section {
            border-left: 4px solid #17a2b8;
        }

        .edit-section {
            border-left: 4px solid #ffc107;
        }

        .section-header {
            background: #f8f9fa;
            border-bottom: 1px solid #dee2e6;
            padding: 1rem 1.25rem;
            border-top-left-radius: 6px;
            border-top-right-radius: 6px;
        }

        .section-title {
            color: #495057;
            font-weight: 600;
            margin: 0;
            font-size: 1.1rem;
        }

        .section-content {
            padding: 1.25rem;
        }

        .section-actions {
            text-align: right;
        }

        .btn-action {
            margin-left: 0.5rem;
            padding: 0.375rem 0.75rem;
            font-size: 0.875rem;
            border-radius: 4px;
        }

        /* === FILTROS === */
        .filters-container {
            background: #f8f9fa;
            border: 1px solid #e9ecef;
            border-radius: 4px;
            padding: 1rem;
            margin-bottom: 1.5rem;
        }

        .form-group {
            margin-bottom: 1rem;
        }

        .form-label {
            font-weight: 500;
            color: #495057;
            margin-bottom: 0.5rem;
            display: block;
            font-size: 0.875rem;
        }

        .form-control, .form-select {
            border: 1px solid #ced4da;
            border-radius: 4px;
            padding: 0.375rem 0.75rem;
            font-size: 0.875rem;
        }

        .form-control:focus, .form-select:focus {
            border-color: #80bdff;
            box-shadow: 0 0 0 0.2rem rgba(0, 123, 255, 0.25);
        }

        .input-group .btn {
            border-color: #ced4da;
        }

        .filter-buttons {
            display: flex;
            gap: 0.5rem;
        }

        /* === TABLAS === */
        .data-grid-container {
            background: white;
            border: 1px solid #dee2e6;
            border-radius: 4px;
            overflow-x: auto;
        }

        .data-table {
            width: 100%;
            margin: 0;
            border-collapse: separate;
            border-spacing: 0;
        }

        .data-table thead th {
            background: #f8f9fa;
            color: #495057;
            font-weight: 600;
            font-size: 0.875rem;
            padding: 0.75rem;
            border-bottom: 2px solid #dee2e6;
            text-align: left;
            position: sticky;
            top: 0;
        }

        .data-table tbody td {
            padding: 0.75rem;
            border-bottom: 1px solid #dee2e6;
            font-size: 0.875rem;
            vertical-align: middle;
        }

        .data-table tbody tr:last-child td {
            border-bottom: none;
        }

        .detail-table thead th {
            background: #e9ecef;
        }

        /* === BADGES === */
        .badge {
            font-size: 0.75rem;
            padding: 0.35em 0.65em;
            border-radius: 0.25rem;
            font-weight: 500;
        }

        .tipo-internacional {
            background-color: #ffc107;
            color: #000;
        }

        .tipo-nacional {
            background-color: #0dcaf0;
            color: #000;
        }

        .estado-abierto {
            background-color: #28a745;
            color: #fff;
        }

        .estado-programado {
            background-color: #17a2b8;
            color: #fff;
        }

        .estado-enprogreso {
            background-color: #ffc107;
            color: #000;
        }

        .estado-completado {
            background-color: #28a745;
            color: #fff;
        }

        .estado-cancelado {
            background-color: #dc3545;
            color: #fff;
        }

        /* === CAMPOS ESPECIALES === */
        .viaje-number, .pedido-number, .despacho-number {
            font-weight: 600;
            color: #0d6efd;
        }

        .despachos-count {
            font-weight: 600;
            color: #28a745;
        }

        .affected-count {
            color: #dc3545;
            font-weight: 600;
        }

        /* === BOTONES DE ACCIÓN === */
        .action-buttons {
            display: flex;
            gap: 0.25rem;
            flex-wrap: wrap;
        }

        .btn-info {
            background-color: #0dcaf0;
            border-color: #0dcaf0;
            color: #000;
        }

        .btn-warning {
            background-color: #ffc107;
            border-color: #ffc107;
            color: #000;
        }

        .btn-danger {
            background-color: #dc3545;
            border-color: #dc3545;
            color: #fff;
        }

        .btn-secondary {
            background-color: #6c757d;
            border-color: #6c757d;
            color: #fff;
        }

        /* === EMPTY DATA === */
        .empty-data {
            text-align: center;
            padding: 2rem;
            color: #6c757d;
        }

        .empty-data i {
            font-size: 2rem;
            margin-bottom: 0.5rem;
            display: block;
        }

        /* === CARDS DE RESUMEN === */
        .summary-cards {
            margin-bottom: 1.5rem;
        }

        .summary-card {
            background: #f8f9fa;
            border: 1px solid #e9ecef;
            border-radius: 4px;
            padding: 1rem;
            text-align: center;
            margin-bottom: 1rem;
        }

        .summary-value {
            font-size: 1.5rem;
            font-weight: 600;
            color: #495057;
            margin-bottom: 0.25rem;
        }

        .summary-label {
            font-size: 0.875rem;
            color: #6c757d;
        }

        /* === DETALLES === */
        .detail-info {
            font-size: 0.875rem;
            color: #6c757d;
            margin-top: 0.25rem;
        }

        .detail-identifier, .detail-value {
            font-weight: 600;
            color: #495057;
        }

        .data-section {
            margin-top: 1.5rem;
        }

        .data-section-title {
            color: #495057;
            font-weight: 600;
            margin-bottom: 1rem;
            font-size: 1rem;
        }

        /* === FORMULARIOS DE EDICIÓN === */
        .edit-form {
            margin-top: 1rem;
        }

        .form-section {
            background: #f8f9fa;
            border: 1px solid #e9ecef;
            border-radius: 4px;
            padding: 1rem;
            margin-bottom: 1rem;
        }

        .form-section-title {
            color: #495057;
            font-weight: 600;
            margin-bottom: 1rem;
            font-size: 1rem;
            border-bottom: 1px solid #dee2e6;
            padding-bottom: 0.5rem;
        }

        .required::after {
            content: " *";
            color: #dc3545;
        }

        .readonly-field {
            background-color: #e9ecef !important;
            cursor: not-allowed;
        }

        .form-text {
            font-size: 0.75rem;
            color: #6c757d;
            margin-top: 0.25rem;
        }

        .field-error {
            color: #dc3545;
            font-size: 0.75rem;
            margin-top: 0.25rem;
            display: block;
        }

        /* === GRID DE CONDUCTORES EDITABLE === */
        .conductores-edit-container {
            margin: 1rem 0;
            border: 1px solid #e2e8f0;
            border-radius: 6px;
            padding: 0;
            background: #f8fafc;
        }

        .conductores-edit-container .table {
            margin-bottom: 0;
            font-size: 0.9rem;
        }

        .conductores-edit-container thead {
            background: #f1f5f9;
        }

        .conductores-edit-container thead th {
            background: #f1f5f9 !important;
            color: #1e293b !important;
            font-weight: 600;
            padding: 0.75rem;
            border-bottom: 2px solid #e2e8f0 !important;
        }

        .conductores-edit-container tbody td {
            padding: 0.75rem;
            vertical-align: middle;
            background: white;
        }

        .conductores-edit-container .form-select {
            font-size: 0.875rem;
            padding: 0.375rem 0.75rem;
        }

        .conductor-badge-actual {
            display: inline-block;
            padding: 0.5rem 1rem;
            background: #6c757d;
            color: white;
            border-radius: 4px;
            font-weight: 500;
            font-size: 0.85rem;
        }

        .tabla-scroll-wrapper {
            overflow-x: auto;
            border-radius: 6px;
        }

        .tabla-scroll-wrapper::-webkit-scrollbar {
            width: 8px;
        }

        .tabla-scroll-wrapper::-webkit-scrollbar-track {
            background: #f1f1f1;
            border-radius: 4px;
        }

        .tabla-scroll-wrapper::-webkit-scrollbar-thumb {
            background: #888;
            border-radius: 4px;
        }

        .tabla-scroll-wrapper::-webkit-scrollbar-thumb:hover {
            background: #555;
        }

        /* === SELECT2 PERSONALIZADO === */
        .select2-container--default .select2-selection--single {
            min-height: 38px !important;
            border: 1px solid #ced4da !important;
            border-radius: 4px !important;
        }

        .select2-container--default .select2-selection--single .select2-selection__rendered {
            padding: 0.375rem 0.75rem !important;
            line-height: 1.5 !important;
        }

        .select2-container--default .select2-dropdown {
            border: 1px solid #ced4da !important;
            border-radius: 4px !important;
        }

        .select2-container--default .select2-results__option--highlighted[aria-selected] {
            background-color: #0d6efd !important;
            color: white !important;
        }

        .select2-search--dropdown .select2-search__field {
            border: 1px solid #ced4da !important;
            border-radius: 4px !important;
            padding: 0.5rem !important;
        }

        /* === PANELES DE DOCUMENTOS === */
        .doc-panel {
            border: 1px solid #dee2e6;
            border-radius: 4px;
            margin-bottom: 1rem;
        }

        .doc-panel-header {
            background: #f8f9fa;
            padding: 0.5rem 0.75rem;
            border-bottom: 1px solid #dee2e6;
            font-weight: 600;
            font-size: 0.875rem;
            color: #495057;
        }

        .doc-panel-content {
            padding: 0.75rem;
        }

        .info-panel {
            background: #d1ecf1;
            border: 1px solid #b6d4da;
            border-radius: 4px;
            padding: 0.75rem;
        }

        .info-title {
            font-weight: 600;
            font-size: 0.875rem;
            color: #0c5460;
            margin-bottom: 0.5rem;
        }

        .info-content {
            font-size: 0.875rem;
            color: #0c5460;
        }

        .info-value {
            font-weight: 600;
        }

        /* === ALERTAS === */
        .alert {
            border-radius: 4px;
            padding: 0.75rem 1rem;
            margin-bottom: 1rem;
        }

        .edit-warning {
            background-color: #fff3cd;
            border: 1px solid #ffeaa7;
            color: #856404;
        }

        /* === ACCIONES DE FORMULARIO === */
        .form-actions {
            text-align: center;
            margin-top: 1.5rem;
            padding-top: 1rem;
            border-top: 1px solid #dee2e6;
        }

        .btn-large {
            padding: 0.5rem 1.5rem;
            font-size: 1rem;
        }

        /* === LOADING === */
        .loading-overlay {
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background-color: rgba(0, 0, 0, 0.5);
            z-index: 9999;
            display: flex;
            justify-content: center;
            align-items: center;
        }

        .loading-content {
            background-color: white;
            padding: 1.5rem;
            border-radius: 6px;
            text-align: center;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
        }

        /* === RESPONSIVE === */
        @media (max-width: 768px) {
            .nav-buttons, .section-actions {
                text-align: left;
                margin-top: 0.5rem;
            }
            
            .btn-nav, .btn-action {
                margin-left: 0;
                margin-right: 0.5rem;
                margin-bottom: 0.5rem;
                display: inline-block;
            }
            
            .action-buttons {
                flex-direction: column;
            }
            
            .filter-buttons {
                flex-direction: column;
            }
            
            .data-grid-container {
                overflow-x: auto;
                -webkit-overflow-scrolling: touch;
            }
            
            .summary-card {
                margin-bottom: 0.5rem;
            }
            
            .form-section {
                margin-bottom: 0.5rem;
            }

            .conductores-edit-container .form-select {
                min-width: 200px;
            }
        }
        
        @media (max-width: 576px) {
            .container-fluid {
                padding: 0.5rem;
            }
            
            .section-content {
                padding: 0.75rem;
            }
            
            .filters-container {
                padding: 0.75rem;
            }
            
            .data-table thead th,
            .data-table tbody td {
                padding: 0.5rem;
            }
        }

        /* === UTILITIES === */
        .text-center { text-align: center; }
        .text-start { text-align: left; }
        .text-end { text-align: right; }
        .mb-0 { margin-bottom: 0; }
        .mb-1 { margin-bottom: 0.25rem; }
        .mb-2 { margin-bottom: 0.5rem; }
        .mb-3 { margin-bottom: 1rem; }
        .mt-2 { margin-top: 0.5rem; }
        .ms-2 { margin-left: 0.5rem; }
        .ms-3 { margin-left: 1rem; }
        .fw-bold { font-weight: 600; }
    </style>
</asp:Content>
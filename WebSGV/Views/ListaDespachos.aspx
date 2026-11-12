<%@ Page Title="Gestión de Viajes y Lotes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ListaDespachos.aspx.cs" Inherits="WebSGV.Views.ListaDespachos" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

<link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/ListaDespachos.css") %>" rel="stylesheet" />

    <div class="container-fluid">
        <div class="row">
            <div class="col-md-12">
                <div class="card main-card">
                    <div class="ld-page-header">
                        <h4>
                            Gestión de viajes y lotes
                            <asp:Label ID="lblContadorGeneral" runat="server" CssClass="badge ml-2" style="background:#e2e8f0;color:#334155;font-size:.75rem;font-weight:600;"></asp:Label>
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
                                            <span class="ld-stat-label">Viajes activos</span>
                                        </span>
                                    </asp:LinkButton>

                                    <asp:LinkButton ID="lnkStatLotes" runat="server" CssClass="ld-stat-tile" OnClick="lnkStatLotes_Click" CausesValidation="false">
                                        <span class="ld-stat-icon ld-stat-icon-green"><i class="fas fa-layer-group"></i></span>
                                        <span class="ld-stat-text">
                                            <span class="ld-stat-value"><asp:Label ID="lblStatLotesActivos" runat="server">0</asp:Label></span>
                                            <span class="ld-stat-label">Lotes activos</span>
                                        </span>
                                    </asp:LinkButton>

                                    <%-- El rojo va en el div y no en el LinkButton: un LinkButton con controles
                                         dentro al que se le cambia CssClass sale vacío tras un postback. --%>
                                    <div id="divStatManifiestos" runat="server" class="ld-stat-wrap">
                                    <asp:LinkButton ID="lnkStatManifiestosPendientes" runat="server" CssClass="ld-stat-tile" OnClick="lnkStatManifiestosPendientes_Click" CausesValidation="false">
                                        <span class="ld-stat-icon ld-stat-icon-amber"><i class="fas fa-passport"></i></span>
                                        <span class="ld-stat-text">
                                            <span class="ld-stat-value"><asp:Label ID="lblStatManifiestosPendientes" runat="server">0</asp:Label></span>
                                            <span class="ld-stat-label">Manifiestos pendientes</span>
                                        </span>
                                    </asp:LinkButton>
                                    </div>

                                    <div id="divStatDocumentos" runat="server" class="ld-stat-wrap">
                                    <asp:LinkButton ID="lnkStatDocumentosPendientes" runat="server" CssClass="ld-stat-tile" OnClick="lnkStatDocumentosPendientes_Click" CausesValidation="false">
                                        <span class="ld-stat-icon ld-stat-icon-amber"><i class="fas fa-file-invoice"></i></span>
                                        <span class="ld-stat-text">
                                            <span class="ld-stat-value"><asp:Label ID="lblStatDocumentosPendientes" runat="server">0</asp:Label></span>
                                            <span class="ld-stat-label">Lotes con documentos pendientes</span>
                                            <asp:Label ID="lblStatDocumentosAtrasados" runat="server" CssClass="ld-stat-sub"></asp:Label>
                                        </span>
                                    </asp:LinkButton>
                                    </div>
                                </div>

                                <!-- NAVEGACIÓN PRINCIPAL -->
                                <div class="ld-toolbar mb-4">
                                    <div class="ld-toolbar-nav">
                                        <asp:Button ID="btnMostrarViajes" runat="server" 
                                            Text="Viajes activos" 
                                            CssClass="btn btn-outline-secondary btn-nav"
                                            OnClick="btnMostrarViajes_Click"
                                            CausesValidation="false" />
                                        <asp:Button ID="btnMostrarLotes" runat="server" 
                                            Text="Lotes registrados" 
                                            CssClass="btn btn-outline-secondary btn-nav"
                                            OnClick="btnMostrarLotes_Click"
                                            CausesValidation="false" />
                                    </div>
                                    <div class="ld-toolbar-acciones">
                                    <asp:HyperLink ID="hlVolverBandeja" runat="server" Visible="false"
                                        NavigateUrl="~/Views/BandejaDocumentos.aspx"
                                        CssClass="btn btn-outline-primary ld-toolbar-back">← Bandeja de documentos</asp:HyperLink>
                                    <asp:Button ID="btnVolver" runat="server"
                                        Text="+ Nuevo despacho"
                                        CssClass="btn btn-outline-secondary ld-toolbar-back"
                                        OnClick="btnVolver_Click"
                                        CausesValidation="false" />
                                    </div>
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
                                                        
                                                        <asp:BoundField DataField="FechaInicio" HeaderText="Fecha inicio" 
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
                                                        
                                                        <asp:BoundField DataField="FechaUltimaActividad" HeaderText="Última actividad" 
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
                                            
                                            <!-- Filtros para Lotes: una sola barra y un solo botón; lo poco usado queda en "Más filtros". -->
                                            <asp:Panel ID="pnlFiltrosLotes" runat="server" CssClass="ld-filtros" DefaultButton="btnFiltrarFecha">
                                                <div class="ld-filtros-fila">
                                                    <div class="ld-filtro ld-filtro-buscar">
                                                        <label for="<%= txtBuscarLote.ClientID %>">N° de pedido</label>
                                                        <asp:TextBox ID="txtBuscarLote" runat="server" CssClass="form-control form-control-sm"
                                                            placeholder="Ej: 4400134786" MaxLength="20"></asp:TextBox>
                                                    </div>
                                                    <div class="ld-filtro">
                                                        <label for="<%= ddlFiltroClienteLotes.ClientID %>">Cliente</label>
                                                        <asp:DropDownList ID="ddlFiltroClienteLotes" runat="server"
                                                            CssClass="form-control form-control-sm" AutoPostBack="true"
                                                            OnSelectedIndexChanged="ddlFiltroClienteLotes_SelectedIndexChanged">
                                                            <asp:ListItem Value="" Text="Todos"></asp:ListItem>
                                                        </asp:DropDownList>
                                                    </div>
                                                    <div class="ld-filtro">
                                                        <label for="<%= ddlFiltroPlantaLotes.ClientID %>">Planta</label>
                                                        <%-- Opciones cargadas desde el catálogo Planta en CargarPlantasFiltro(). --%>
                                                        <asp:DropDownList ID="ddlFiltroPlantaLotes" runat="server"
                                                            CssClass="form-control form-control-sm" AutoPostBack="true"
                                                            OnSelectedIndexChanged="ddlFiltroPlantaLotes_SelectedIndexChanged">
                                                        </asp:DropDownList>
                                                    </div>
                                                    <div class="ld-filtro ld-filtro-fecha">
                                                        <label for="<%= txtFechaDesde.ClientID %>">Desde</label>
                                                        <asp:TextBox ID="txtFechaDesde" runat="server" CssClass="form-control form-control-sm" TextMode="Date"></asp:TextBox>
                                                    </div>
                                                    <div class="ld-filtro ld-filtro-fecha">
                                                        <label for="<%= txtFechaHasta.ClientID %>">Hasta</label>
                                                        <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control form-control-sm" TextMode="Date"></asp:TextBox>
                                                    </div>
                                                    <div class="ld-filtro">
                                                        <label for="<%= ddlFiltroDocumentosLotes.ClientID %>">Documentos</label>
                                                        <%-- Valores = ListaDespachosService.DOCUMENTOS_PENDIENTES / DOCUMENTOS_COMPLETOS. --%>
                                                        <asp:DropDownList ID="ddlFiltroDocumentosLotes" runat="server"
                                                            CssClass="form-control form-control-sm" AutoPostBack="true"
                                                            OnSelectedIndexChanged="ddlFiltroDocumentosLotes_SelectedIndexChanged">
                                                            <asp:ListItem Value="" Text="Todos"></asp:ListItem>
                                                            <asp:ListItem Value="PENDIENTES" Text="Con pendientes"></asp:ListItem>
                                                            <asp:ListItem Value="COMPLETOS" Text="Completos"></asp:ListItem>
                                                        </asp:DropDownList>
                                                    </div>
                                                    <div class="ld-filtro ld-filtro-botones">
                                                        <asp:Button ID="btnFiltrarFecha" runat="server" Text="Buscar"
                                                            CssClass="btn btn-primary btn-sm" OnClick="btnFiltrarFecha_Click" CausesValidation="false" />
                                                        <asp:Button ID="btnLimpiarFiltros" runat="server" Text="Limpiar"
                                                            CssClass="btn btn-outline-secondary btn-sm" OnClick="btnLimpiarFiltros_Click" CausesValidation="false" />
                                                    </div>
                                                </div>
                                                <%-- Para encontrar un lote por su factura, su CPIC o su conductor sin saber la fecha ni el cliente. --%>
                                                <details class="ld-filtros-mas" id="ldFiltrosMas">
                                                    <summary>Más filtros: factura, CPIC, conductor, operación y anulados</summary>
                                                    <div class="ld-filtros-fila">
                                                        <div class="ld-filtro">
                                                            <label for="<%= txtBuscarFacturaLotes.ClientID %>">N° de factura</label>
                                                            <asp:TextBox ID="txtBuscarFacturaLotes" runat="server" CssClass="form-control form-control-sm"
                                                                placeholder="Ej: F222-00004267" MaxLength="30"></asp:TextBox>
                                                        </div>
                                                        <div class="ld-filtro">
                                                            <label for="<%= txtBuscarCPICLotes.ClientID %>">N° de CPIC</label>
                                                            <asp:TextBox ID="txtBuscarCPICLotes" runat="server" CssClass="form-control form-control-sm"
                                                                placeholder="Ej: 0003958" MaxLength="20"></asp:TextBox>
                                                        </div>
                                                        <div class="ld-filtro ld-filtro-buscar">
                                                            <label for="<%= txtBuscarConductorLotes.ClientID %>">Conductor</label>
                                                            <asp:TextBox ID="txtBuscarConductorLotes" runat="server" CssClass="form-control form-control-sm"
                                                                placeholder="Nombre o apellido"></asp:TextBox>
                                                        </div>
                                                        <div class="ld-filtro">
                                                            <label for="<%= ddlFiltroOperacionLotes.ClientID %>">Operación</label>
                                                            <asp:DropDownList ID="ddlFiltroOperacionLotes" runat="server"
                                                                CssClass="form-control form-control-sm" AutoPostBack="true"
                                                                OnSelectedIndexChanged="ddlFiltroOperacionLotes_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="Todas"></asp:ListItem>
                                                                <asp:ListItem Value="CARGA" Text="Carga"></asp:ListItem>
                                                                <asp:ListItem Value="DESCARGA" Text="Descarga"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                        <div class="ld-filtro">
                                                            <label for="<%= ddlFiltroEstadoLotes.ClientID %>">Estado del lote</label>
                                                            <asp:DropDownList ID="ddlFiltroEstadoLotes" runat="server"
                                                                CssClass="form-control form-control-sm" AutoPostBack="true"
                                                                OnSelectedIndexChanged="ddlFiltroEstadoLotes_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="Todos"></asp:ListItem>
                                                                <asp:ListItem Value="ACTIVO" Text="Activos" Selected="True"></asp:ListItem>
                                                                <asp:ListItem Value="ANULADO" Text="Anulados"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                    </div>
                                                </details>
                                            </asp:Panel>

                                            <!-- Grid de Lotes -->
                                            <div class="data-grid-container">
                                                <asp:GridView ID="gvLotesRegistrados" runat="server"
                                                    CssClass="table data-table ld-tabla-lotes"
                                                    AutoGenerateColumns="false"
                                                    AllowPaging="true" PageSize="15"
                                                    OnPageIndexChanging="gvLotesRegistrados_PageIndexChanging"
                                                    EmptyDataText="No se encontraron lotes registrados"
                                                    OnRowCommand="gvLotesRegistrados_RowCommand">
                                                    <PagerStyle CssClass="ld-pager" HorizontalAlign="Center" />
                                                    <PagerSettings Mode="NumericFirstLast" FirstPageText="«" LastPageText="»" PageButtonCount="7" Position="TopAndBottom" />
                                                    <Columns>
                                                        <asp:BoundField DataField="FechaProgramacion" HeaderText="Fecha prog."
                                                            DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="ld-col-fecha" />

                                                        <asp:TemplateField HeaderText="Pedido">
                                                            <ItemTemplate>
                                                                <span class="pedido-number"><%#: Eval("PedidoMostrado") %></span>
                                                                <asp:Label runat="server" Text="Anulado" CssClass="ld-anulado"
                                                                    Visible='<%# (string)Eval("EstadoLote") == "ANULADO" %>'></asp:Label>
                                                                <span class="ld-sub"><%#: Eval("NombreCliente") %></span>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>

                                                        <asp:TemplateField HeaderText="Operación">
                                                            <ItemTemplate>
                                                                <span class="ld-op"><%#: Eval("TipoOperacion") %> · <%#: Eval("PlantaOperacion") %></span>
                                                                <span class="ld-sub"><%# Convert.ToBoolean(Eval("EsInternacional")) ? "Internacional" : "Nacional" %></span>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>

                                                        <asp:BoundField DataField="CantidadDespachos" HeaderText="Unid."
                                                            ItemStyle-CssClass="text-center despachos-count" HeaderStyle-CssClass="text-center" />

                                                        <asp:TemplateField HeaderText="Documentos" ItemStyle-CssClass="ld-docs-cell">
                                                            <ItemTemplate>
                                                                <asp:Label runat="server"
                                                                    Text='<%# Eval("DocumentosResumen") %>'
                                                                    CssClass='<%# (string)Eval("DocumentosClase") %>'>
                                                                </asp:Label>
                                                                <asp:Label runat="server"
                                                                    Text='<%# "Manifiesto: " + Eval("ManifiestoEstado") %>'
                                                                    CssClass='<%# GetManifiestoBadgeClass(Eval("ManifiestoEstado").ToString()) %>'
                                                                    Visible='<%# Convert.ToBoolean(Eval("EsInternacional")) && Eval("ManifiestoEstado").ToString() != "N/A" %>'>
                                                                </asp:Label>
                                                                <asp:Label runat="server"
                                                                    Text='<%# Eval("DocumentosAntiguedad") %>'
                                                                    CssClass='<%# (bool)Eval("DocumentosAtrasado") ? "ld-docs-edad ld-docs-edad-atrasado" : "ld-docs-edad" %>'
                                                                    Visible='<%# !string.IsNullOrEmpty((string)Eval("DocumentosAntiguedad")) %>'>
                                                                </asp:Label>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>

                                                        <asp:TemplateField HeaderText="Factura / CPIC">
                                                            <ItemTemplate>
                                                                <span class="ld-num"><%#: Eval("NumeroFactura") %></span>
                                                                <asp:Label runat="server" CssClass="ld-sub ld-num"
                                                                    Text='<%# "CPIC " + Eval("NumeroCPIC") %>'
                                                                    Visible='<%# (string)Eval("NumeroCPIC") != "—" %>'></asp:Label>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>

                                                        <asp:TemplateField HeaderText="" ItemStyle-CssClass="ld-col-acciones">
                                                            <ItemTemplate>
                                                                <div class="action-buttons">
                                                                    <asp:Button runat="server"
                                                                        Text="Ver unidades"
                                                                        CssClass="btn btn-sm btn-outline-primary"
                                                                        CommandName="VerDetallesLote"
                                                                        CommandArgument='<%# Eval("IdLoteVirtual") %>' />

                                                                    <asp:Button runat="server"
                                                                        Text="Documentos"
                                                                        ToolTip="Abre el expediente del pedido en la Bandeja de documentos"
                                                                        CssClass='<%# ((string)Eval("DocumentosClase")).Contains("pendiente") ? "btn btn-sm btn-warning ld-btn-docs" : "btn btn-sm btn-outline-secondary ld-btn-docs" %>'
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
                                                            Text="Finalizar viaje" 
                                                            CssClass="btn btn-outline-danger btn-action"
                                                            OnClick="btnFinalizarViajeDetalle_Click"
                                                            OnClientClick="return confirm('¿Finalizar este viaje?');"
                                                            CausesValidation="false" />
                                                        
                                                        <asp:Button ID="btnVolverViajes" runat="server" 
                                                            Text="Volver a viajes" 
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
                                                            <div class="summary-label">Total despachos</div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblTipoViajeDetalle" runat="server"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Tipo de viaje</div>
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
                                                            <div class="summary-label">Última actividad</div>
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
                                                            
                                                            <asp:BoundField DataField="GuiaRemitente" HeaderText="Guía remitente" />
                                                            
                                                            <asp:BoundField DataField="GuiaTransportista" HeaderText="Guía transportista" />
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
                                                            Text="Volver a lotes" 
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
                                                            <label class="form-label required">Fecha de programación:</label>
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
                                                            <label class="form-label required">Planta de operación:</label>
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
                                                            <label class="form-label required">Tipo de operación:</label>
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
                                                                    <label class="form-label">Fecha emisión:</label>
                                                                    <asp:TextBox ID="txtFechaEmisionFacturaEdit" runat="server" 
                                                                        CssClass="form-control" 
                                                                        TextMode="Date">
                                                                    </asp:TextBox>
                                                                </div>
                                                                <div class="form-group">
                                                                    <label class="form-label">Valor total:</label>
                                                                    <asp:TextBox ID="txtValorTotalFacturaEdit" runat="server"
                                                                        CssClass="form-control"
                                                                        placeholder="0.00"
                                                                        TextMode="Number"
                                                                        step="0.01">
                                                                    </asp:TextBox>
                                                                </div>
                                                                <div class="form-group">
                                                                    <label class="form-label">Moneda:</label>
                                                                    <asp:DropDownList ID="ddlMonedaFacturaEdit" runat="server" CssClass="form-select">
                                                                        <asp:ListItem Value="PEN" Text="Soles (PEN)"></asp:ListItem>
                                                                        <asp:ListItem Value="USD" Text="Dólares (USD)"></asp:ListItem>
                                                                    </asp:DropDownList>
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
                                                                    <label class="form-label">Fecha emisión CPIC:</label>
                                                                    <asp:TextBox ID="txtFechaEmisionCPICEdit" runat="server" 
                                                                        CssClass="form-control" 
                                                                        TextMode="Date">
                                                                    </asp:TextBox>
                                                                </div>
                                                                <div class="form-group">
                                                                    <label class="form-label">Valor flete:</label>
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
                                                            <p class="form-text mb-2">Puede cambiar el conductor de cada despacho individualmente. los cambios se aplican al guardar.</p>
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

                                                                            <asp:TemplateField HeaderText="Conductor actual">
                                                                                <ItemTemplate>
                                                                                    <span class="conductor-badge-actual">
                                                                                        <%# Eval("NombreConductorActual") %>
                                                                                    </span>
                                                                                </ItemTemplate>
                                                                            </asp:TemplateField>

                                                                            <asp:TemplateField HeaderText="Cambiar conductor">
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
                                                                Text="Eliminar lote" 
                                                                CssClass="btn btn-outline-danger"
                                                                OnClick="btnEliminarLote_Click"
                                                                CausesValidation="false"
                                                                OnClientClick="return confirm('⚠️ ADVERTENCIA: Se eliminarán TODOS los despachos de este lote.\n\n¿Está COMPLETAMENTE seguro de eliminar este lote?');" />
                                                            <asp:Button ID="btnAnularLote" runat="server"
                                                                Text="Anular lote"
                                                                CssClass="btn btn-warning ml-2"
                                                                OnClick="btnAnularLote_Click"
                                                                CausesValidation="false"
                                                                OnClientClick="return confirm('⚠️ ¿Anular este lote?\n\nTodos sus despachos y viajes activos quedarán como ANULADO.\nLos registros se conservan para auditoría.');" />
                                                        </div>
                                                        <div class="col-md-6 text-end">
                                                            <asp:Button ID="btnGuardarCambios" runat="server" 
                                                                Text="Guardar cambios" 
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
                                                            Text="Editar lote"
                                                            CssClass="btn btn-outline-secondary btn-action"
                                                            OnClick="btnEditarDesdeDetal_Click"
                                                            CausesValidation="false" />

                                                        <asp:Button ID="btnVolverLotesDetalle" runat="server"
                                                            Text="Volver a lotes"
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
                                                            <div class="summary-label">Total despachos</div>
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
                                                            <div class="summary-label">Fecha creación</div>
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
                                                            
                                                            <asp:BoundField DataField="GuiaRemitente" HeaderText="Guía remitente" />
                                                            
                                                            <asp:BoundField DataField="GuiaTransportista" HeaderText="Guía transportista" />

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

                                <!-- ====== DOCUMENTOS DEL LOTE ====== -->
                                <asp:Panel ID="pnlManifiestosLote" runat="server" Visible="false">
                                    <div class="section-card detalle-section ld-docs-page">
                                        <div class="ld-docs-top">
                                            <div>
                                                <h5 class="section-title mb-1"><i class="fas fa-folder-open"></i> Documentos del lote</h5>
                                                <div class="ld-docs-chips">
                                                    <span class="ld-chip"><i class="fas fa-building"></i> <asp:Label ID="lblClienteManifiestos" runat="server"></asp:Label></span>
                                                    <span class="ld-chip"><i class="fas fa-hashtag"></i> Pedido <asp:Label ID="lblPedidoManifiestos" runat="server"></asp:Label></span>
                                                </div>
                                            </div>
                                            <div class="ld-docs-volver">
                                                <asp:HyperLink ID="hlVolverBandejaDocs" runat="server" Visible="false"
                                                    NavigateUrl="~/Views/BandejaDocumentos.aspx"
                                                    CssClass="btn btn-primary btn-action">← Volver a la bandeja</asp:HyperLink>
                                                <asp:Button ID="btnVolverManifiestos" runat="server" Text="Ver el lote"
                                                    CssClass="btn btn-outline-secondary btn-action"
                                                    OnClick="btnVolverManifiestos_Click" CausesValidation="false" />
                                            </div>
                                        </div>
                                        <div class="section-content">

                                            <asp:Panel ID="pnlMensajeManifiesto" runat="server" Visible="false" CssClass="mb-3">
                                                <asp:Label ID="lblMensajeManifiesto" runat="server" CssClass="alert d-block"></asp:Label>
                                            </asp:Panel>

                                            <%-- Avance: barra + una pastilla por documento (verde = listo, ámbar = falta).
                                                 Lo arma CargarDocumentosBase con EstadoDocumental. --%>
                                            <div class="ld-docs-resumen">
                                                <asp:Label ID="lblEstadoDocumentalLote" runat="server" CssClass="ld-docs-titular"></asp:Label>
                                                <asp:Literal ID="litAvanceDocs" runat="server"></asp:Literal>
                                            </div>

                                            <%-- Lo recomendado: subir los documentos del correo; la bandeja los lee y los une solos. --%>
                                            <div class="ld-subir-docs">
                                                <i class="fas fa-inbox"></i>
                                                <div class="ld-subir-docs-texto">
                                                    <strong>Suba los documentos del correo</strong>
                                                    <span>El ZIP del cliente (factura, packing list y guías) o sus PDF: el sistema completa pedido, factura,
                                                        guías y carga de cada unidad, y adjunta el PDF de la factura. Sin escribir a mano.</span>
                                                </div>
                                                <asp:HyperLink ID="lnkCompletarConZip" runat="server" NavigateUrl="~/Views/BandejaDocumentos.aspx"
                                                    CssClass="btn btn-primary ld-subir-docs-btn"><i class="fas fa-cloud-upload-alt mr-1"></i>Subir documentos</asp:HyperLink>
                                            </div>
                                            <asp:Panel ID="pnlEsperaZip" runat="server" Visible="false" CssClass="ld-aviso ld-aviso-info">
                                                <i class="fas fa-envelope-open-text"></i>
                                                <div>
                                                    <strong>Esperando el correo con los documentos.</strong>
                                                    Si escribe la guía o la factura a mano aquí, el ZIP ya no reconoce esa carga: use la bandeja.
                                                </div>
                                            </asp:Panel>

                                            <%-- 1. Factura y CPIC: una tarjeta por documento --%>
                                            <div class="ld-docs-grid">
                                                <div class="ld-doc-card">
                                                    <div class="ld-doc-card-head">
                                                        <span class="ld-doc-card-icon"><i class="fas fa-receipt"></i></span>
                                                        <div>
                                                            <div class="ld-doc-card-titulo">Factura</div>
                                                            <div class="ld-doc-card-numero"><asp:Label ID="lblNumeroFacturaDocs" runat="server"></asp:Label></div>
                                                        </div>
                                                    </div>
                                                    <div class="ld-doc-archivo">
                                                        <asp:LinkButton ID="lnkVerDocFactura" runat="server" OnClick="lnkVerDocFactura_Click" CausesValidation="false" Visible="false" CssClass="ld-doc-ver"></asp:LinkButton>
                                                    </div>
                                                    <%-- Factura aún sin registrar: se anota aquí mismo, sin pasar por "Editar". --%>
                                                    <asp:Panel ID="pnlRegistrarFactura" runat="server" CssClass="ld-doc-manual-envoltura" Visible="false" DefaultButton="btnRegistrarFactura">
                                                    <details class="ld-manual">
                                                        <summary><i class="fas fa-keyboard"></i> Escribir la factura a mano <small>(solo si no tiene el documento)</small></summary>
                                                    <div class="ld-doc-registrar">
                                                        <div class="ld-doc-registrar-titulo">Registrar factura</div>
                                                        <div class="ld-doc-campos">
                                                            <div class="ld-campo ld-campo-ancho">
                                                                <label>N° de factura</label>
                                                                <asp:TextBox ID="txtFacturaNueva" runat="server" CssClass="form-control form-control-sm" MaxLength="30" placeholder="Ej: F222-00004267"></asp:TextBox>
                                                            </div>
                                                            <div class="ld-campo">
                                                                <label>Fecha de emisión</label>
                                                                <asp:TextBox ID="txtFacturaFecha" runat="server" CssClass="form-control form-control-sm" TextMode="Date"></asp:TextBox>
                                                            </div>
                                                            <div class="ld-campo">
                                                                <label>Valor total</label>
                                                                <asp:TextBox ID="txtFacturaValor" runat="server" CssClass="form-control form-control-sm" TextMode="Number" step="0.01" placeholder="0.00"></asp:TextBox>
                                                            </div>
                                                            <div class="ld-campo">
                                                                <label>Moneda</label>
                                                                <asp:DropDownList ID="ddlFacturaMoneda" runat="server" CssClass="form-control form-control-sm">
                                                                    <asp:ListItem Value="PEN" Text="Soles (PEN)"></asp:ListItem>
                                                                    <asp:ListItem Value="USD" Text="Dólares (USD)"></asp:ListItem>
                                                                </asp:DropDownList>
                                                            </div>
                                                        </div>
                                                        <asp:Button ID="btnRegistrarFactura" runat="server" Text="Registrar factura"
                                                            CssClass="btn btn-primary btn-sm" OnClick="btnRegistrarFactura_Click" CausesValidation="false" />
                                                    </div>
                                                    </details>
                                                    </asp:Panel>
                                                    <asp:Panel ID="pnlSubirPdfFactura" runat="server" CssClass="ld-doc-subir" Visible="false">
                                                        <label class="ld-doc-elegir">
                                                            <i class="fas fa-cloud-upload-alt"></i>
                                                            <span class="ld-doc-elegir-texto">Elegir PDF o foto</span>
                                                            <asp:FileUpload ID="fuPdfFactura" runat="server" CssClass="ld-doc-input" accept=".pdf,.jpg,.jpeg,.png" />
                                                        </label>
                                                        <asp:Button ID="btnSubirPdfFactura" runat="server" Text="Adjuntar"
                                                            CssClass="btn btn-primary btn-sm ld-doc-btn" OnClick="btnSubirPdfFactura_Click" CausesValidation="false" />
                                                    </asp:Panel>
                                                </div>

                                                <asp:Panel ID="pnlFilaCpicDocs" runat="server" CssClass="ld-doc-card">
                                                    <div class="ld-doc-card-head">
                                                        <span class="ld-doc-card-icon"><i class="fas fa-shipping-fast"></i></span>
                                                        <div>
                                                            <div class="ld-doc-card-titulo">CPIC</div>
                                                            <div class="ld-doc-card-numero"><asp:Label ID="lblNumeroCpicDocs" runat="server"></asp:Label></div>
                                                        </div>
                                                    </div>
                                                    <div class="ld-doc-archivo">
                                                        <asp:LinkButton ID="lnkVerDocCpic" runat="server" OnClick="lnkVerDocCpic_Click" CausesValidation="false" Visible="false" CssClass="ld-doc-ver"></asp:LinkButton>
                                                    </div>
                                                    <asp:Panel ID="pnlRegistrarCpic" runat="server" CssClass="ld-doc-registrar" Visible="false" DefaultButton="btnRegistrarCpic">
                                                        <div class="ld-doc-registrar-titulo">Registrar CPIC</div>
                                                        <div class="ld-doc-campos">
                                                            <div class="ld-campo ld-campo-ancho">
                                                                <label>N° de CPIC (7 dígitos)</label>
                                                                <asp:TextBox ID="txtCpicNuevo" runat="server" CssClass="form-control form-control-sm" MaxLength="7" placeholder="Ej: 1234567"></asp:TextBox>
                                                            </div>
                                                            <div class="ld-campo">
                                                                <label>Fecha de emisión</label>
                                                                <asp:TextBox ID="txtCpicFecha" runat="server" CssClass="form-control form-control-sm" TextMode="Date"></asp:TextBox>
                                                            </div>
                                                            <div class="ld-campo">
                                                                <label>Valor del flete</label>
                                                                <asp:TextBox ID="txtCpicFlete" runat="server" CssClass="form-control form-control-sm" TextMode="Number" step="0.01" placeholder="0.00"></asp:TextBox>
                                                            </div>
                                                        </div>
                                                        <asp:Button ID="btnRegistrarCpic" runat="server" Text="Registrar CPIC"
                                                            CssClass="btn btn-primary btn-sm" OnClick="btnRegistrarCpic_Click" CausesValidation="false" />
                                                    </asp:Panel>
                                                    <asp:Panel ID="pnlSubirPdfCpic" runat="server" CssClass="ld-doc-subir" Visible="false">
                                                        <label class="ld-doc-elegir">
                                                            <i class="fas fa-cloud-upload-alt"></i>
                                                            <span class="ld-doc-elegir-texto">Elegir PDF o foto</span>
                                                            <asp:FileUpload ID="fuPdfCpic" runat="server" CssClass="ld-doc-input" accept=".pdf,.jpg,.jpeg,.png" />
                                                        </label>
                                                        <asp:Button ID="btnSubirPdfCpic" runat="server" Text="Adjuntar"
                                                            CssClass="btn btn-primary btn-sm ld-doc-btn" OnClick="btnSubirPdfCpic_Click" CausesValidation="false" />
                                                    </asp:Panel>
                                                </asp:Panel>
                                            </div>
                                            <p class="ld-nota">
                                                <i class="fas fa-info-circle"></i>
                                                La factura, su PDF y las guías se registran solos al subir el ZIP del correo. El CPIC se registra aquí con su número y su archivo. Para corregir un número ya registrado use <strong>Editar</strong>.
                                            </p>

                                            <%-- 2. Guías por despacho --%>
                                            <asp:Panel ID="pnlGuiasLote" runat="server" CssClass="ld-bloque">
                                                <div class="ld-bloque-head">
                                                    <h6><i class="fas fa-truck-loading"></i> Guías por despacho</h6>
                                                    <span class="ld-bloque-ayuda">Las guías llegan en el ZIP del correo: súbalo con <strong>Subir documentos</strong>.</span>
                                                </div>
                                                <details class="ld-manual">
                                                    <summary><i class="fas fa-keyboard"></i> Ver o escribir las guías a mano <small>(solo si no tiene el documento)</small></summary>
                                                <div class="data-grid-container">
                                                    <asp:GridView ID="gvGuiasLote" runat="server"
                                                        CssClass="table data-table detail-table ld-tabla-guias"
                                                        AutoGenerateColumns="false"
                                                        DataKeyNames="IdDespacho"
                                                        EmptyDataText="No hay despachos en este lote">
                                                        <Columns>
                                                            <asp:BoundField DataField="NumeroDespacho" HeaderText="Despacho" />
                                                            <asp:BoundField DataField="NombreConductor" HeaderText="Conductor" />
                                                            <asp:BoundField DataField="PlacaTracto" HeaderText="Tracto" />
                                                            <asp:TemplateField HeaderText="Guía remitente">
                                                                <ItemTemplate>
                                                                    <asp:TextBox ID="txtGuiaRemitenteFila" runat="server" CssClass="form-control form-control-sm ld-guia-input"
                                                                        MaxLength="50" placeholder="Ej: T660-00016530" Text='<%# Eval("GuiaRemitente") %>'></asp:TextBox>
                                                                </ItemTemplate>
                                                            </asp:TemplateField>
                                                            <asp:TemplateField HeaderText="Guía transportista">
                                                                <ItemTemplate>
                                                                    <asp:TextBox ID="txtGuiaTransportistaFila" runat="server" CssClass="form-control form-control-sm ld-guia-input"
                                                                        MaxLength="50" placeholder="Opcional" Text='<%# Eval("GuiaTransportista") %>'></asp:TextBox>
                                                                </ItemTemplate>
                                                            </asp:TemplateField>
                                                        </Columns>
                                                    </asp:GridView>
                                                </div>
                                                <div class="ld-bloque-pie">
                                                    <asp:Button ID="btnGuardarGuias" runat="server" Text="Guardar guías"
                                                        CssClass="btn btn-primary" OnClick="btnGuardarGuias_Click" CausesValidation="false" />
                                                </div>
                                                </details>
                                            </asp:Panel>

                                            <%-- 3. Manifiestos de aduana (solo internacional) --%>
                                            <asp:Panel ID="pnlAvisoNacionalSinManifiesto" runat="server" Visible="false" CssClass="ld-aviso ld-aviso-neutro">
                                                <i class="fas fa-info-circle"></i>
                                                <div>Este lote es nacional: el manifiesto de aduana no aplica.</div>
                                            </asp:Panel>

                                            <asp:Panel ID="pnlSeccionManifiestoConductor" runat="server" CssClass="ld-bloque">
                                                <div class="ld-bloque-head">
                                                    <h6><i class="fas fa-passport"></i> Manifiestos por conductor</h6>
                                                    <span class="ld-bloque-ayuda">Adjunte o reemplace el manifiesto de cruce y de retorno a medida que avanza el viaje.</span>
                                                </div>
                                                <div class="data-grid-container">
                                                    <asp:GridView ID="gvManifiestosLote" runat="server"
                                                        CssClass="table data-table detail-table"
                                                        AutoGenerateColumns="false"
                                                        DataKeyNames="IdDespacho"
                                                        OnRowCommand="gvManifiestosLote_RowCommand"
                                                        EmptyDataText="No hay conductores en este lote">
                                                        <Columns>
                                                            <asp:BoundField DataField="NumeroDespacho" HeaderText="Despacho" />
                                                            <asp:BoundField DataField="NombreConductor" HeaderText="Conductor" />

                                                            <asp:TemplateField HeaderText="Manifiesto de cruce">
                                                                <ItemTemplate>
                                                                    <asp:LinkButton runat="server" CommandName="VerManifiesto" CommandArgument='<%# Eval("CruceIdDocumento") %>'
                                                                        Visible='<%# Eval("CruceIdDocumento") != null %>' CausesValidation="false" CssClass="ld-doc-ver">
                                                                        <i class="fas fa-file-pdf"></i> <%# Eval("CruceNombreOriginal") %>
                                                                    </asp:LinkButton>
                                                                    <span class="ld-sin-adjuntar" style='<%# Eval("CruceIdDocumento") != null ? "display:none" : "" %>'><i class="fas fa-circle"></i> Sin adjuntar</span>
                                                                    <asp:FileUpload runat="server" ID="fileCruceFila" CssClass="form-control form-control-sm mt-1" accept=".pdf,.jpg,.jpeg,.png" />
                                                                </ItemTemplate>
                                                            </asp:TemplateField>

                                                            <asp:TemplateField HeaderText="Manifiesto de retorno">
                                                                <ItemTemplate>
                                                                    <asp:LinkButton runat="server" CommandName="VerManifiesto" CommandArgument='<%# Eval("RetornoIdDocumento") %>'
                                                                        Visible='<%# Eval("RetornoIdDocumento") != null %>' CausesValidation="false" CssClass="ld-doc-ver">
                                                                        <i class="fas fa-file-pdf"></i> <%# Eval("RetornoNombreOriginal") %>
                                                                    </asp:LinkButton>
                                                                    <span class="ld-sin-adjuntar" style='<%# Eval("RetornoIdDocumento") != null ? "display:none" : "" %>'><i class="fas fa-circle"></i> Sin adjuntar</span>
                                                                    <asp:FileUpload runat="server" ID="fileRetornoFila" CssClass="form-control form-control-sm mt-1" accept=".pdf,.jpg,.jpeg,.png" />
                                                                </ItemTemplate>
                                                            </asp:TemplateField>

                                                            <asp:TemplateField HeaderText="">
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
                                            </asp:Panel>
                                        </div>
                                    </div>
                                </asp:Panel>

                            </ContentTemplate>
                            <Triggers>
                                <asp:AsyncPostBackTrigger ControlID="lnkStatViajes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="lnkStatLotes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="lnkStatManifiestosPendientes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="lnkStatDocumentosPendientes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="ddlFiltroDocumentosLotes" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="btnGuardarGuias" EventName="Click" />
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
                                <%-- FileUpload: postback completo, en uno parcial el archivo no llega. --%>
                                <asp:PostBackTrigger ControlID="btnSubirPdfFactura" />
                                <asp:PostBackTrigger ControlID="btnSubirPdfCpic" />

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
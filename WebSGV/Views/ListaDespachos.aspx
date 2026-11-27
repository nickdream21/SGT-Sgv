<%@ Page Title="Lista de despachos" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ListaDespachos.aspx.cs" Inherits="WebSGV.Views.ListaDespachos" %>

<%@ Import Namespace="WebSGV.Helpers" %>
<%@ Import Namespace="WebSGV.Services.Despachos" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

<link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/ListaDespachos.css") %>" rel="stylesheet" />

    <div class="ld-pagina">
        <div class="sgv-encabezado">
            <div class="sgv-encabezado-titulo">
                <h1 class="h2">Lista de despachos</h1>
                <p>Viajes activos y lotes registrados</p>
            </div>
            <div class="sgv-encabezado-acciones">
                <asp:HyperLink ID="hlNuevoDespacho" runat="server" NavigateUrl="~/Views/RegistroDespacho.aspx"
                    CssClass="btn btn-primary">Nuevo despacho</asp:HyperLink>
                <a class="sgv-ayuda" href="<%: ResolveUrl("~/Views/Ayuda.aspx?tema=lista-despachos") %>" target="_blank" rel="noopener"
                    data-ayuda="lista-despachos" aria-label="Ayuda de esta p&#xE1;gina">?</a>
            </div>
        </div>

        <asp:UpdatePanel ID="UpdatePanelMain" runat="server" UpdateMode="Conditional">
            <ContentTemplate>

                <%-- Botones de fila de las listas (E5-18a): las grillas no guardan ViewState; cada botón copia su
                     data-argumento ("VER|lote", "VERANULADO|lote" o "VIAJE|id") aquí y pulsa
                     btnAccionFila (SGV.acciones.enviarFila). El servidor lo lee con formato estricto y vuelve a
                     buscar el lote o el viaje. --%>
                <asp:HiddenField ID="hfArgumentoFila" runat="server" />
                <asp:Button ID="btnAccionFila" runat="server" Text="Acción de la fila" CssClass="d-none" CausesValidation="false"
                    OnClick="btnAccionFila_Click" tabindex="-1" aria-hidden="true" />

                <asp:Panel ID="pnlMensajes" runat="server" Visible="false" EnableViewState="false" CssClass="mb-3">
                    <div class="position-relative">
                        <asp:Label ID="lblMensaje" runat="server" EnableViewState="false" CssClass="alert d-block mb-0 pr-5"></asp:Label>
                        <button type="button" class="close sgv-cerrar-aviso" data-accion="cerrarBloqueMensaje" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>
                    </div>
                </asp:Panel>

                <%-- Pestañas y resumen: solo en las dos listas (M-082). El contador de cada pestaña va en su Text
                     (el LinkButton guarda el Text en el ViewState; con controles hijos se vaciaba tras un postback). --%>
                <asp:Panel ID="pnlNavegacionListas" runat="server">
                    <div class="sgv-tabs" role="tablist" aria-label="Listas de despachos">
                        <asp:LinkButton ID="btnMostrarViajes" runat="server" CssClass="sgv-tab" role="tab" aria-selected="false"
                            OnClick="btnMostrarViajes_Click" CausesValidation="false">Viajes activos</asp:LinkButton>
                        <asp:LinkButton ID="btnMostrarLotes" runat="server" CssClass="sgv-tab" role="tab" aria-selected="false"
                            OnClick="btnMostrarLotes_Click" CausesValidation="false">Lotes registrados</asp:LinkButton>
                    </div>
                    <p class="sgv-resumen" aria-live="polite">
                        <asp:Literal ID="litResumenListas" runat="server" />
                        <asp:PlaceHolder ID="phVerPendientes" runat="server"><span><asp:LinkButton ID="lnkStatDocumentosPendientes" runat="server"
                            OnClick="lnkStatDocumentosPendientes_Click" CausesValidation="false">Ver los lotes con pendientes</asp:LinkButton></span></asp:PlaceHolder>
                    </p>
                </asp:Panel>

                <!-- Lista de viajes activos -->
                <asp:Panel ID="pnlListaViajes" runat="server" Visible="false">
                    <section class="sgv-panel">
                        <div class="sgv-panel-cab">
                            <div>
                                <h2 class="sgv-panel-titulo">Viajes en progreso</h2>
                                <p class="sgv-panel-nota mb-0"><asp:Literal ID="litTotalViajes" runat="server" Mode="Encode" EnableViewState="false" /></p>
                            </div>
                            <asp:Button ID="btnRefrescarViajes" runat="server" Text="Refrescar"
                                CssClass="btn btn-outline-secondary btn-sm" OnClick="btnRefrescarViajes_Click" CausesValidation="false" />
                        </div>
                        <div class="sgv-panel-cuerpo">
                            <details class="sgv-filtros" open data-plegar-movil>
                                <summary>Filtros <span class="sgv-filtros-cuenta"></span></summary>
                                <div class="sgv-filtros-cuerpo" data-enter="<%= btnBuscarViaje.ClientID %>">
                                    <div class="ld-filtros-fila">
                                        <div class="ld-filtro ld-filtro-buscar">
                                            <label for="<%= txtBuscarViaje.ClientID %>">N° de viaje</label>
                                            <asp:TextBox ID="txtBuscarViaje" runat="server" CssClass="form-control form-control-sm"
                                                placeholder="Ej: VP-2026-001" MaxLength="20"></asp:TextBox>
                                        </div>
                                        <div class="ld-filtro ld-filtro-buscar">
                                            <label for="<%= ddlFiltroConductorViajes.ClientID %>">Conductor</label>
                                            <asp:DropDownList ID="ddlFiltroConductorViajes" runat="server"
                                                CssClass="form-control form-control-sm" AutoPostBack="true"
                                                OnSelectedIndexChanged="ddlFiltroConductorViajes_SelectedIndexChanged">
                                            </asp:DropDownList>
                                        </div>
                                        <div class="ld-filtro">
                                            <label for="<%= ddlFiltroTipoViajes.ClientID %>">Ámbito</label>
                                            <asp:DropDownList ID="ddlFiltroTipoViajes" runat="server"
                                                CssClass="form-control form-control-sm" AutoPostBack="true"
                                                OnSelectedIndexChanged="ddlFiltroTipoViajes_SelectedIndexChanged">
                                                <asp:ListItem Value="" Text="Todos"></asp:ListItem>
                                                <asp:ListItem Value="1" Text="Internacional"></asp:ListItem>
                                                <asp:ListItem Value="0" Text="Nacional"></asp:ListItem>
                                            </asp:DropDownList>
                                        </div>
                                        <div class="ld-filtro ld-filtro-botones">
                                            <asp:Button ID="btnBuscarViaje" runat="server" Text="Buscar"
                                                CssClass="btn btn-primary btn-sm" OnClick="btnBuscarViaje_Click" CausesValidation="false" />
                                        </div>
                                    </div>
                                </div>
                            </details>

                            <div class="table-responsive">
                                <asp:GridView ID="gvViajesActivos" runat="server"
                                    CssClass="table sgv-tabla mb-0 ld-tabla-lotes"
                                    AutoGenerateColumns="false"
                                    EnableViewState="false"
                                    AllowPaging="true" PageSize="25"
                                    OnPageIndexChanging="gvViajesActivos_PageIndexChanging"
                                    GridLines="None" CellSpacing="-1"
                                    EmptyDataText="No hay viajes activos con los filtros elegidos.">
                                    <PagerSettings Mode="NumericFirstLast" PageButtonCount="5" FirstPageText="Primera" LastPageText="Última" />
                                    <PagerStyle CssClass="sgv-paginador" />
                                    <EmptyDataRowStyle CssClass="sgv-vacio" />
                                    <Columns>
                                        <asp:BoundField DataField="NumeroViajeProgreso" HeaderText="N° de viaje" ItemStyle-CssClass="ld-numero" />
                                        <asp:BoundField DataField="NombreConductor" HeaderText="Conductor" />
                                        <asp:BoundField DataField="FechaInicio" HeaderText="Inicio" DataFormatString="{0:dd/MM/yyyy HH:mm}" ItemStyle-CssClass="ld-col-fecha" />
                                        <asp:BoundField DataField="CantidadDespachos" HeaderText="Despachos" HeaderStyle-CssClass="sgv-num" ItemStyle-CssClass="sgv-num" />
                                        <asp:TemplateField HeaderText="Ámbito">
                                            <ItemTemplate><%#: Convert.ToBoolean(Eval("EsInternacional")) ? "Internacional" : "Nacional" %></ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:BoundField DataField="FechaUltimaActividad" HeaderText="Última actividad" DataFormatString="{0:dd/MM/yyyy HH:mm}" ItemStyle-CssClass="ld-col-fecha" />
                                        <asp:TemplateField HeaderText="Estado">
                                            <ItemTemplate><%# ListaDespachosReglas.SelloEstadoViaje(Eval("EstadoViaje")) %></ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:TemplateField HeaderText="" ItemStyle-CssClass="sgv-col-acciones">
                                            <ItemTemplate><button type="button" class="btn btn-sm btn-outline-primary" data-accion="enviarFila" data-argumento='<%#: ListaDespachosReglas.ArgumentoFila(AccionFilaLista.VerViaje, Eval("IdViajeProgreso")) %>' aria-label='<%#: "Ver los despachos del viaje " + Eval("NumeroViajeProgreso") %>'>Ver despachos</button></ItemTemplate>
                                        </asp:TemplateField>
                                    </Columns>
                                </asp:GridView>
                            </div>
                        </div>
                    </section>
                </asp:Panel>

                <!-- Lista de lotes registrados -->
                <asp:Panel ID="pnlListaLotes" runat="server" Visible="false">
                    <section class="sgv-panel">
                        <div class="sgv-panel-cab">
                            <div>
                                <h2 class="sgv-panel-titulo">Lotes de despacho</h2>
                                <p class="sgv-panel-nota mb-0"><asp:Literal ID="litTotalLotes" runat="server" Mode="Encode" EnableViewState="false" /></p>
                            </div>
                            <asp:Button ID="btnRefrescarLotes" runat="server" Text="Refrescar"
                                CssClass="btn btn-outline-secondary btn-sm" OnClick="btnRefrescarLotes_Click" CausesValidation="false" />
                        </div>
                        <div class="sgv-panel-cuerpo">
                            <%-- Una sola barra y un solo botón; lo poco usado queda en "Más filtros". --%>
                            <details class="sgv-filtros" open data-plegar-movil>
                                <summary>Filtros <span class="sgv-filtros-cuenta"></span></summary>
                                <div class="sgv-filtros-cuerpo" data-enter="<%= btnBuscarLotes.ClientID %>">
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
                                            <asp:Button ID="btnBuscarLotes" runat="server" Text="Buscar"
                                                CssClass="btn btn-primary btn-sm" OnClick="btnBuscarLotes_Click" CausesValidation="false" />
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
                                </div>
                            </details>

                            <div class="table-responsive">
                                <asp:GridView ID="gvLotesRegistrados" runat="server"
                                    CssClass="table sgv-tabla mb-0 ld-tabla-lotes"
                                    AutoGenerateColumns="false"
                                    EnableViewState="false"
                                    AllowPaging="true" PageSize="25"
                                    OnPageIndexChanging="gvLotesRegistrados_PageIndexChanging"
                                    GridLines="None" CellSpacing="-1"
                                    EmptyDataText="No hay lotes registrados con los filtros elegidos.">
                                    <PagerSettings Mode="NumericFirstLast" PageButtonCount="5" FirstPageText="Primera" LastPageText="Última" />
                                    <PagerStyle CssClass="sgv-paginador" />
                                    <EmptyDataRowStyle CssClass="sgv-vacio" />
                                    <%-- Plantillas en una línea: 25 filas con su sangría pesan varios KB de espacios. --%>
                                    <Columns>
                                        <asp:BoundField DataField="FechaProgramacion" HeaderText="Fecha" DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="ld-col-fecha" />
                                        <asp:TemplateField HeaderText="Pedido">
                                            <ItemTemplate><span class="ld-numero"><%#: Eval("PedidoMostrado") %></span> <%# ListaDespachosReglas.SelloLoteAnulado(Eval("EstadoLote")) %><span class="ld-sub"><%#: Eval("NombreCliente") %></span></ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:TemplateField HeaderText="Operación">
                                            <ItemTemplate><span class="ld-op"><%#: ListaDespachosReglas.TextoOracion((string)Eval("TipoOperacion")) %> · <%#: Eval("PlantaOperacion") %></span><span class="ld-sub"><%#: Convert.ToBoolean(Eval("EsInternacional")) ? "Internacional" : "Nacional" %></span></ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:BoundField DataField="CantidadDespachos" HeaderText="Unidades" HeaderStyle-CssClass="sgv-num" ItemStyle-CssClass="sgv-num" />
                                        <asp:TemplateField HeaderText="Documentos" ItemStyle-CssClass="ld-docs-cell">
                                            <ItemTemplate><%# ListaDespachosReglas.SellosDocumentos(Eval("DocumentosResumen"), Eval("DocumentosTono"), Eval("MostrarManifiesto"), Eval("ManifiestoEstado")) %><span class='<%# (bool)Eval("DocumentosAtrasado") ? "ld-docs-edad ld-docs-edad-atrasado" : "ld-docs-edad" %>'><%#: Eval("DocumentosAntiguedad") %></span></ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:TemplateField HeaderText="Factura / CPIC">
                                            <ItemTemplate><span class="ld-num" title='<%#: Eval("FacturasTitulo") %>'><%#: Eval("NumeroFactura") %></span><asp:PlaceHolder runat="server" Visible='<%# (string)Eval("NumeroCPIC") != "—" %>'><span class="ld-sub ld-num" title='<%#: "CPIC " + Eval("CpicsTitulo") %>'><%#: "CPIC " + Eval("NumeroCPIC") %></span></asp:PlaceHolder></ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:TemplateField HeaderText="" ItemStyle-CssClass="sgv-col-acciones">
                                            <%-- Un lote anulado (filtro "Anulados") es solo de consulta: sus despachos tienen activo = 0.
                                                 "Ver unidades" abre su detalle en solo lectura (sin editar ni documentos). "Documentos" es un
                                                 enlace (GET) al expediente del pedido en la Bandeja y "Editar", a EditarLote.aspx. --%>
                                            <ItemTemplate><asp:PlaceHolder runat="server" Visible='<%# (string)Eval("EstadoLote") == "ANULADO" %>'><div class="sgv-acciones"><span class="sgv-suave">Solo consulta</span><button type="button" class="btn btn-sm btn-outline-secondary" data-accion="enviarFila" data-argumento='<%#: ListaDespachosReglas.ArgumentoFila(AccionFilaLista.VerLoteAnulado, Eval("IdLoteVirtual")) %>' aria-label='<%#: "Ver las unidades del " + ListaDespachosReglas.NombreLote(Eval("NumeroPedido"), Eval("NombreCliente"), Eval("FechaProgramacion")) + " (anulado)" %>'>Ver unidades</button></div></asp:PlaceHolder><asp:PlaceHolder runat="server" Visible='<%# (string)Eval("EstadoLote") != "ANULADO" %>'><div class="sgv-acciones"><button type="button" class="btn btn-sm btn-outline-primary" data-accion="enviarFila" data-argumento='<%#: ListaDespachosReglas.ArgumentoFila(AccionFilaLista.VerLote, Eval("IdLoteVirtual")) %>' aria-label='<%#: "Ver las unidades del " + ListaDespachosReglas.NombreLote(Eval("NumeroPedido"), Eval("NombreCliente"), Eval("FechaProgramacion")) %>'>Ver unidades</button><a href='<%#: UrlExpediente(Eval("IdLoteVirtual")) %>' class='<%#: (bool)Eval("DocumentosPendientes") ? "btn btn-sm btn-warning" : "btn btn-sm btn-outline-secondary" %>' title="Abre el expediente del pedido en la Bandeja de documentos" aria-label='<%#: "Documentos del " + ListaDespachosReglas.NombreLote(Eval("NumeroPedido"), Eval("NombreCliente"), Eval("FechaProgramacion")) %>'>Documentos</a><a href='<%#: UrlEditar(Eval("IdLoteVirtual")) %>' class="btn btn-sm btn-outline-secondary" aria-label='<%#: "Editar el " + ListaDespachosReglas.NombreLote(Eval("NumeroPedido"), Eval("NombreCliente"), Eval("FechaProgramacion")) %>'>Editar</a></div></asp:PlaceHolder></ItemTemplate>
                                        </asp:TemplateField>
                                    </Columns>
                                </asp:GridView>
                            </div>
                        </div>
                    </section>
                </asp:Panel>

                <!-- Detalle de un viaje: sus despachos -->
                <asp:Panel ID="pnlDetallesViaje" runat="server" Visible="false">
                    <section class="sgv-panel">
                        <div class="sgv-panel-cab">
                            <div>
                                <h2 class="sgv-panel-titulo">Detalle del viaje <asp:Label ID="lblNumeroViajeDetalle" runat="server" CssClass="ld-numero"></asp:Label></h2>
                                <p class="sgv-panel-nota mb-0">Conductor: <asp:Label ID="lblConductorDetalle" runat="server" CssClass="ld-dato"></asp:Label> · Inicio: <asp:Label ID="lblFechaInicioDetalle" runat="server" CssClass="ld-dato"></asp:Label></p>
                            </div>
                            <asp:Button ID="btnVolverViajes" runat="server" Text="Volver a viajes"
                                CssClass="btn btn-outline-secondary btn-sm" OnClick="btnVolverViajes_Click" CausesValidation="false" />
                        </div>
                        <div class="sgv-panel-cuerpo">
                            <asp:Literal ID="litResumenViaje" runat="server" />
                            <h3 class="ld-subtitulo">Despachos asociados</h3>
                            <div class="table-responsive">
                                <asp:GridView ID="gvDespachosViaje" runat="server"
                                    CssClass="table sgv-tabla mb-0"
                                    AutoGenerateColumns="false"
                                    EnableViewState="false"
                                    GridLines="None" CellSpacing="-1"
                                    EmptyDataText="Este viaje no tiene despachos asociados.">
                                    <EmptyDataRowStyle CssClass="sgv-vacio" />
                                    <Columns>
                                        <asp:BoundField DataField="NumeroDespacho" HeaderText="N° de despacho" ItemStyle-CssClass="ld-numero" />
                                        <asp:BoundField DataField="FechaDespacho" HeaderText="Fecha" DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="ld-col-fecha" />
                                        <asp:BoundField DataField="NombreCliente" HeaderText="Cliente" />
                                        <asp:BoundField DataField="PlacaTracto" HeaderText="Tracto" ItemStyle-CssClass="ld-placa" />
                                        <asp:BoundField DataField="PlacaCarreta" HeaderText="Carreta" ItemStyle-CssClass="ld-placa" />
                                        <asp:BoundField DataField="TipoOperacion" HeaderText="Operación" />
                                        <asp:BoundField DataField="LugarOperacion" HeaderText="Planta" />
                                        <asp:TemplateField HeaderText="Estado">
                                            <ItemTemplate><%# ListaDespachosReglas.SelloEstadoDespacho(Eval("EstadoDespacho")) %></ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:BoundField DataField="GuiaRemitente" HeaderText="Guía remitente" />
                                        <asp:BoundField DataField="GuiaTransportista" HeaderText="Guía transportista" />
                                    </Columns>
                                </asp:GridView>
                            </div>
                        </div>
                    </section>
                </asp:Panel>

                <!-- Detalle de un lote -->
                <asp:Panel ID="pnlDetallesLote" runat="server" Visible="false">
                    <section class="sgv-panel">
                        <div class="sgv-panel-cab">
                            <div>
                                <h2 class="sgv-panel-titulo">Detalle del lote</h2>
                                <p class="sgv-panel-nota mb-0">Cliente: <asp:Label ID="lblClienteDetalleLote" runat="server" CssClass="ld-dato"></asp:Label> · Pedido: <asp:Label ID="lblPedidoDetalleLote" runat="server" CssClass="ld-dato"></asp:Label></p>
                            </div>
                            <div class="ld-cab-acciones">
                                <asp:HyperLink ID="hlDocumentosLote" runat="server" CssClass="btn btn-outline-primary btn-sm"
                                    ToolTip="Abre el expediente del pedido en la Bandeja de documentos">Documentos</asp:HyperLink>
                                <asp:HyperLink ID="hlEditarLote" runat="server" CssClass="btn btn-outline-secondary btn-sm">Editar lote</asp:HyperLink>
                                <asp:Button ID="btnVolverLotesDetalle" runat="server" Text="Volver a lotes"
                                    CssClass="btn btn-outline-secondary btn-sm" OnClick="btnVolverLotesDetalle_Click" CausesValidation="false" />
                            </div>
                        </div>
                        <div class="sgv-panel-cuerpo">

                            <asp:Panel ID="pnlAvisoLoteAnulado" runat="server" Visible="false" CssClass="alert alert-secondary mb-3" role="status">
                                <strong>Lote anulado: solo consulta.</strong>
                                Sus despachos ya no cuentan en la liquidación ni en el seguimiento; no se pueden editar ni agregar documentos.
                            </asp:Panel>

                            <asp:Literal ID="litResumenLote" runat="server" />

                            <h3 class="ld-subtitulo">Despachos del lote</h3>
                            <div class="table-responsive">
                                <asp:GridView ID="gvDespachosLote" runat="server"
                                    CssClass="table sgv-tabla mb-0"
                                    AutoGenerateColumns="false"
                                    EnableViewState="false"
                                    GridLines="None" CellSpacing="-1"
                                    EmptyDataText="Este lote no tiene despachos asociados.">
                                    <EmptyDataRowStyle CssClass="sgv-vacio" />
                                    <Columns>
                                        <asp:BoundField DataField="NumeroDespacho" HeaderText="N° de despacho" ItemStyle-CssClass="ld-numero" />
                                        <asp:BoundField DataField="FechaDespacho" HeaderText="Fecha" DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="ld-col-fecha" />
                                        <asp:BoundField DataField="NombreConductor" HeaderText="Conductor" />
                                        <asp:BoundField DataField="PlacaTracto" HeaderText="Tracto" ItemStyle-CssClass="ld-placa" />
                                        <asp:BoundField DataField="PlacaCarreta" HeaderText="Carreta" ItemStyle-CssClass="ld-placa" />
                                        <asp:TemplateField HeaderText="Estado">
                                            <ItemTemplate><%# ListaDespachosReglas.SelloEstadoDespacho(Eval("EstadoDespacho")) %></ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:BoundField DataField="GuiaRemitente" HeaderText="Guía remitente" />
                                        <asp:BoundField DataField="GuiaTransportista" HeaderText="Guía transportista" />
                                        <asp:BoundField DataField="NumeroViaje" HeaderText="N° de viaje" />
                                    </Columns>
                                </asp:GridView>
                            </div>
                        </div>
                    </section>
                </asp:Panel>

            </ContentTemplate>
            <Triggers>
                <%-- Los controles dentro del ContentTemplate ya hacen un postback parcial; aquí van los que deben
                     hacerlo completo y, por claridad, los de siempre. Un ControlID que no existe hace
                     caer la página: al quitar un control, quite también su trigger. --%>
                <asp:AsyncPostBackTrigger ControlID="lnkStatDocumentosPendientes" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="ddlFiltroDocumentosLotes" EventName="SelectedIndexChanged" />
                <asp:AsyncPostBackTrigger ControlID="btnMostrarViajes" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="btnRefrescarViajes" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="ddlFiltroConductorViajes" EventName="SelectedIndexChanged" />
                <asp:AsyncPostBackTrigger ControlID="ddlFiltroTipoViajes" EventName="SelectedIndexChanged" />
                <asp:AsyncPostBackTrigger ControlID="btnBuscarViaje" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="btnVolverViajes" EventName="Click" />

                <asp:AsyncPostBackTrigger ControlID="btnMostrarLotes" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="btnRefrescarLotes" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="ddlFiltroClienteLotes" EventName="SelectedIndexChanged" />
                <asp:AsyncPostBackTrigger ControlID="ddlFiltroOperacionLotes" EventName="SelectedIndexChanged" />
                <asp:AsyncPostBackTrigger ControlID="ddlFiltroPlantaLotes" EventName="SelectedIndexChanged" />
                <asp:AsyncPostBackTrigger ControlID="btnBuscarLotes" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="btnLimpiarFiltros" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="btnAccionFila" EventName="Click" />

                <asp:AsyncPostBackTrigger ControlID="ddlFiltroEstadoLotes" EventName="SelectedIndexChanged" />
                <asp:AsyncPostBackTrigger ControlID="btnVolverLotesDetalle" EventName="Click" />
            </Triggers>
        </asp:UpdatePanel>
    </div>

</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="ScriptsSection" runat="server">
    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hfArgumentoFila: '<%= hfArgumentoFila.ClientID %>',
            btnAccionFila: '<%= btnAccionFila.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/ListaDespachos.js") %>"></script>
</asp:Content>

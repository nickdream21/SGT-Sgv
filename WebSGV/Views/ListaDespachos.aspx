<%@ Page Title="Lista de despachos" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ListaDespachos.aspx.cs" Inherits="WebSGV.Views.ListaDespachos" %>

<%@ Import Namespace="WebSGV.Helpers" %>
<%@ Import Namespace="WebSGV.Services.Despachos" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

<%-- select2.min.css va antes de la hoja de la página: sus reglas de select2 (antes en ListaDespachos-2.css, que se cargaba al final) deben ganarle. --%>
<link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/lib/select2-4.0.13/select2.min.css") %>" rel="stylesheet" />
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

        <%-- gvManifiestosLote está declarado como PostBackTrigger (no Async) en <Triggers> más abajo: tiene
             FileUpload por fila (un archivo no viaja en un postback parcial de UpdatePanel) y "Ver" abre el
             documento en otra pestaña al volver la página. --%>
        <asp:UpdatePanel ID="UpdatePanelMain" runat="server" UpdateMode="Conditional">
            <ContentTemplate>

                <%-- Botones de fila de las listas (E5-18a): las grillas no guardan ViewState; cada botón copia su
                     data-argumento ("VER|lote", "EDITAR|lote", "VERANULADO|lote" o "VIAJE|id") aquí y pulsa
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
                                                 enlace (GET) al expediente del pedido en la Bandeja. --%>
                                            <ItemTemplate><asp:PlaceHolder runat="server" Visible='<%# (string)Eval("EstadoLote") == "ANULADO" %>'><div class="sgv-acciones"><span class="sgv-suave">Solo consulta</span><button type="button" class="btn btn-sm btn-outline-secondary" data-accion="enviarFila" data-argumento='<%#: ListaDespachosReglas.ArgumentoFila(AccionFilaLista.VerLoteAnulado, Eval("IdLoteVirtual")) %>' aria-label='<%#: "Ver las unidades del lote anulado " + Eval("PedidoMostrado") %>'>Ver unidades</button></div></asp:PlaceHolder><asp:PlaceHolder runat="server" Visible='<%# (string)Eval("EstadoLote") != "ANULADO" %>'><div class="sgv-acciones"><button type="button" class="btn btn-sm btn-outline-primary" data-accion="enviarFila" data-argumento='<%#: ListaDespachosReglas.ArgumentoFila(AccionFilaLista.VerLote, Eval("IdLoteVirtual")) %>' aria-label='<%#: "Ver las unidades del pedido " + Eval("PedidoMostrado") %>'>Ver unidades</button><a href='<%#: UrlExpediente(Eval("IdLoteVirtual")) %>' class='<%#: (bool)Eval("DocumentosPendientes") ? "btn btn-sm btn-warning" : "btn btn-sm btn-outline-secondary" %>' title="Abre el expediente del pedido en la Bandeja de documentos" aria-label='<%#: "Documentos del pedido " + Eval("PedidoMostrado") %>'>Documentos</a><button type="button" class="btn btn-sm btn-outline-secondary" data-accion="enviarFila" data-argumento='<%#: ListaDespachosReglas.ArgumentoFila(AccionFilaLista.EditarLote, Eval("IdLoteVirtual")) %>' aria-label='<%#: "Editar el lote del pedido " + Eval("PedidoMostrado") %>'>Editar</button></div></asp:PlaceHolder></ItemTemplate>
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

                <!-- Edición del lote -->
                <asp:Panel ID="pnlEdicionLote" runat="server" Visible="false">
                    <section class="sgv-panel sgv-panel-acento" data-montos-grupo>
                        <div class="sgv-panel-cab">
                            <div>
                                <h2 class="sgv-panel-titulo">Editar datos base del lote</h2>
                                <p class="sgv-panel-nota mb-0">Lote: <asp:Label ID="lblIdentificadorLote" runat="server" CssClass="ld-dato"></asp:Label> · Despachos afectados: <asp:Label ID="lblDespachosSAfectados" runat="server" CssClass="ld-dato ld-afectados"></asp:Label></p>
                            </div>
                            <div class="ld-cab-acciones">
                                <asp:Button ID="btnCancelarEdicion" runat="server" Text="Cancelar"
                                    CssClass="btn btn-outline-secondary btn-sm" OnClick="btnCancelarEdicion_Click" CausesValidation="false" />
                                <asp:Button ID="btnVolverLotes" runat="server" Text="Volver a lotes"
                                    CssClass="btn btn-outline-secondary btn-sm" OnClick="btnVolverLotes_Click" CausesValidation="false" />
                            </div>
                        </div>
                        <div class="sgv-panel-cuerpo">

                            <div class="alert alert-warning">
                                <i class="fas fa-exclamation-triangle" aria-hidden="true"></i>
                                <strong>Importante:</strong> los cambios afectarán a todos los despachos de este lote.
                                Verifique cuidadosamente antes de guardar.
                            </div>

                            <div class="ld-edicion" data-enter="<%= btnGuardarCambios.ClientID %>">

                                <!-- Fila 1: campos básicos en 4 columnas -->
                                <div class="row mb-3">
                                    <div class="col-md-3">
                                        <div class="form-group">
                                            <label for="<%= txtFechaProgramacionEdit.ClientID %>" class="sgv-etiqueta">Fecha de programación: <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                            <asp:TextBox ID="txtFechaProgramacionEdit" runat="server" CssClass="form-control" TextMode="Date" aria-required="true"></asp:TextBox>
                                            <asp:RequiredFieldValidator ID="rfvFechaProgramacionEdit" runat="server"
                                                ControlToValidate="txtFechaProgramacionEdit"
                                                ErrorMessage="Seleccione la fecha de programación."
                                                Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true"
                                                ValidationGroup="EdicionLote" />
                                        </div>
                                    </div>
                                    <div class="col-md-3">
                                        <div class="form-group">
                                            <label for="<%= txtClienteEdit.ClientID %>" class="sgv-etiqueta">Cliente:</label>
                                            <asp:TextBox ID="txtClienteEdit" runat="server" CssClass="form-control readonly-field" ReadOnly="true"></asp:TextBox>
                                            <small class="form-text">No se puede modificar el cliente</small>
                                        </div>
                                    </div>
                                    <div class="col-md-3">
                                        <div class="form-group">
                                            <label for="<%= txtNumeroPedidoEdit.ClientID %>" class="sgv-etiqueta">N° de pedido:</label>
                                            <asp:TextBox ID="txtNumeroPedidoEdit" runat="server" CssClass="form-control"
                                                placeholder="Ej: 1234567890" MaxLength="10"></asp:TextBox>
                                            <small class="form-text">Exactamente 10 dígitos numéricos</small>
                                        </div>
                                    </div>
                                    <div class="col-md-3">
                                        <div class="form-group">
                                            <label for="<%= ddlPlantaEdit.ClientID %>" class="sgv-etiqueta">Planta de operación: <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                            <%-- Opciones cargadas desde el catálogo Planta en CargarPlantasFiltro(). --%>
                                            <asp:DropDownList ID="ddlPlantaEdit" runat="server" CssClass="custom-select" aria-required="true">
                                            </asp:DropDownList>
                                            <asp:RequiredFieldValidator ID="rfvPlantaEdit" runat="server"
                                                ControlToValidate="ddlPlantaEdit"
                                                InitialValue=""
                                                ErrorMessage="Seleccione la planta."
                                                Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true"
                                                ValidationGroup="EdicionLote" />
                                        </div>
                                    </div>
                                </div>

                                <!-- Fila 2: tipo, ámbito y documentos -->
                                <div class="row mb-3">
                                    <div class="col-md-3">
                                        <div class="form-group">
                                            <label for="<%= ddlTipoOperacionEdit.ClientID %>" class="sgv-etiqueta">Tipo de operación: <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                            <asp:DropDownList ID="ddlTipoOperacionEdit" runat="server" CssClass="custom-select" aria-required="true"
                                                AutoPostBack="true" OnSelectedIndexChanged="ddlTipoOperacionEdit_SelectedIndexChanged">
                                                <asp:ListItem Value="" Text="-- Seleccione --"></asp:ListItem>
                                                <asp:ListItem Value="CARGA" Text="Carga"></asp:ListItem>
                                                <asp:ListItem Value="DESCARGA" Text="Descarga"></asp:ListItem>
                                            </asp:DropDownList>
                                            <asp:RequiredFieldValidator ID="rfvTipoOperacionEdit" runat="server" ControlToValidate="ddlTipoOperacionEdit" InitialValue=""
                                                ErrorMessage="Seleccione el tipo de operación." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="EdicionLote" />
                                        </div>
                                        <div class="form-group mt-2">
                                            <span class="sgv-etiqueta" id="lblAmbitoEdit">Ámbito: <span class="sgv-obligatorio" aria-hidden="true">*</span></span>
                                            <div role="radiogroup" aria-labelledby="lblAmbitoEdit" aria-required="true">
                                                <asp:RadioButtonList ID="rblAmbitoEdit" runat="server" CssClass="rbl-inline"
                                                    AutoPostBack="true" OnSelectedIndexChanged="rblAmbitoEdit_SelectedIndexChanged"
                                                    RepeatDirection="Horizontal" RepeatLayout="Flow">
                                                    <asp:ListItem Value="0" Text="Nacional &nbsp;"></asp:ListItem>
                                                    <asp:ListItem Value="1" Text="Internacional"></asp:ListItem>
                                                </asp:RadioButtonList>
                                            </div>
                                            <asp:RequiredFieldValidator ID="rfvAmbitoEdit" runat="server" ControlToValidate="rblAmbitoEdit"
                                                ErrorMessage="Seleccione el ámbito." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="EdicionLote" />
                                        </div>
                                        <small class="form-text ld-nota-alerta mt-1">Al cambiar estos valores los paneles de documentos se actualizan automáticamente.</small>
                                    </div>
                                    <div class="col-md-4">
                                        <!-- Factura -->
                                        <asp:Panel ID="pnlFacturaEdit" runat="server" CssClass="doc-panel h-100" Visible="false">
                                            <h3 class="ld-doc-panel-titulo">Datos de factura</h3>
                                            <div class="doc-panel-content">
                                                <div class="form-group">
                                                    <label for="<%= txtNumeroFacturaEdit.ClientID %>" class="sgv-etiqueta">N° de factura:</label>
                                                    <asp:TextBox ID="txtNumeroFacturaEdit" runat="server" CssClass="form-control" placeholder="Ej: F222 - 00004267"></asp:TextBox>
                                                </div>
                                                <div class="form-group">
                                                    <label for="<%= txtFechaEmisionFacturaEdit.ClientID %>" class="sgv-etiqueta">Fecha de emisión:</label>
                                                    <asp:TextBox ID="txtFechaEmisionFacturaEdit" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                                </div>
                                                <div class="form-group">
                                                    <label for="<%= txtValorTotalFacturaEdit.ClientID %>" class="sgv-etiqueta">Valor total:</label>
                                                    <asp:TextBox ID="txtValorTotalFacturaEdit" runat="server"
                                                        CssClass="form-control"
                                                        placeholder="0.00"
                                                        inputmode="decimal" autocomplete="off" data-monto="true">
                                                    </asp:TextBox>
                                                </div>
                                                <div class="form-group">
                                                    <label for="<%= ddlMonedaFacturaEdit.ClientID %>" class="sgv-etiqueta">Moneda:</label>
                                                    <asp:DropDownList ID="ddlMonedaFacturaEdit" runat="server" CssClass="custom-select">
                                                        <asp:ListItem Value="PEN" Text="Soles (PEN)"></asp:ListItem>
                                                        <asp:ListItem Value="USD" Text="Dólares (USD)"></asp:ListItem>
                                                    </asp:DropDownList>
                                                </div>
                                                <div class="form-group mb-0">
                                                    <asp:CheckBox ID="chkQuitarFacturaEdit" runat="server" CssClass="ld-quitar-doc" Text="Quitar esta factura del lote" />
                                                    <small class="form-text">La factura no se borra: solo deja de estar en estos despachos. Si el lote tiene una CPIC de esta factura, quítela también.</small>
                                                </div>
                                            </div>
                                        </asp:Panel>
                                    </div>
                                    <div class="col-md-5">
                                        <!-- CPIC -->
                                        <asp:Panel ID="pnlCPICEdit" runat="server" CssClass="doc-panel h-100" Visible="false">
                                            <h3 class="ld-doc-panel-titulo">Datos de CPIC</h3>
                                            <div class="doc-panel-content">
                                                <div class="form-group">
                                                    <label for="<%= txtNumeroCPICEdit.ClientID %>" class="sgv-etiqueta">N° de CPIC:</label>
                                                    <asp:TextBox ID="txtNumeroCPICEdit" runat="server" CssClass="form-control" placeholder="Ej: 1234567" MaxLength="7"></asp:TextBox>
                                                </div>
                                                <div class="form-group">
                                                    <label for="<%= txtFechaEmisionCPICEdit.ClientID %>" class="sgv-etiqueta">Fecha de emisión de la CPIC:</label>
                                                    <asp:TextBox ID="txtFechaEmisionCPICEdit" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                                </div>
                                                <div class="form-group">
                                                    <label for="<%= txtValorFleteEdit.ClientID %>" class="sgv-etiqueta">Valor del flete:</label>
                                                    <asp:TextBox ID="txtValorFleteEdit" runat="server"
                                                        CssClass="form-control"
                                                        placeholder="0.00"
                                                        inputmode="decimal" autocomplete="off" data-monto="true">
                                                    </asp:TextBox>
                                                </div>
                                                <div class="form-group mb-0">
                                                    <asp:CheckBox ID="chkQuitarCpicEdit" runat="server" CssClass="ld-quitar-doc" Text="Quitar esta CPIC del lote" />
                                                    <small class="form-text">La CPIC no se borra: solo deja de estar en estos despachos.</small>
                                                </div>
                                            </div>
                                        </asp:Panel>
                                    </div>
                                </div>

                                <%-- Lote con más de una factura o CPIC: aquí no se editan (escribir un número las juntaría en una). --%>
                                <asp:Panel ID="pnlAvisoVariosDocumentosEdit" runat="server" Visible="false" CssClass="ld-aviso ld-aviso-info mb-3">
                                    <div>
                                        <strong>Este lote lleva más de una factura o CPIC.</strong>
                                        Sus números no se cambian aquí: corríjalos en
                                        <asp:LinkButton ID="lnkIrDocsPorDespacho" runat="server" OnClick="lnkIrDocsPorDespacho_Click" data-montos-libre="true"
                                            CausesValidation="false">Documentos del lote → Factura y CPIC por despacho</asp:LinkButton>.
                                        Los demás cambios de esta pantalla sí se guardan.
                                    </div>
                                </asp:Panel>

                                <!-- Fila 3: conductores (ancho completo) -->
                                <div class="form-section">
                                    <h3 class="form-section-title">Conductores por despacho</h3>
                                    <p class="form-text mb-2">Puede cambiar el conductor de cada despacho por separado. Los cambios se aplican al guardar.</p>
                                    <div class="table-responsive">
                                        <asp:GridView ID="gvConductoresLote" runat="server"
                                            CssClass="table sgv-tabla mb-0"
                                            AutoGenerateColumns="false"
                                            DataKeyNames="IdDespacho"
                                            OnRowDataBound="gvConductoresLote_RowDataBound"
                                            EmptyDataText="Este lote no tiene despachos."
                                            GridLines="None" CellSpacing="-1">
                                            <EmptyDataRowStyle CssClass="sgv-vacio" />
                                            <Columns>
                                                <asp:BoundField DataField="IdDespacho" HeaderText="ID" Visible="false" />
                                                <asp:BoundField DataField="NumeroDespacho" HeaderText="N° de despacho" ItemStyle-CssClass="ld-numero" />
                                                <asp:BoundField DataField="FechaDespacho" HeaderText="Fecha" DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="ld-col-fecha" />
                                                <asp:TemplateField HeaderText="Conductor actual">
                                                    <ItemTemplate><span class="ld-conductor-actual"><%#: Eval("NombreConductorActual") %></span></ItemTemplate>
                                                </asp:TemplateField>
                                                <asp:TemplateField HeaderText="Cambiar conductor">
                                                    <ItemTemplate>
                                                        <asp:DropDownList ID="ddlConductorDespacho" runat="server"
                                                            CssClass="custom-select conductor-select" aria-label='<%# "Conductor del despacho " + Eval("NumeroDespacho") %>'>
                                                        </asp:DropDownList>
                                                    </ItemTemplate>
                                                </asp:TemplateField>
                                            </Columns>
                                        </asp:GridView>
                                    </div>
                                </div>

                                <!-- Botones de acción -->
                                <div class="ld-form-acciones">
                                    <asp:Button ID="btnAnularLote" runat="server"
                                        Text="Anular lote"
                                        CssClass="btn btn-warning"
                                        OnClick="btnAnularLote_Click"
                                        CausesValidation="false"
                                        data-montos-libre="true"
                                        data-confirmar="¿Anular este lote?\n\nTodos sus despachos quedarán como ANULADO y saldrán de la lista, de la liquidación del conductor y del seguimiento. Un viaje que quede sin despachos también se anula.\nNo se puede anular si algún viaje del lote ya tiene liquidación del conductor.\nLos registros se conservan para auditoría."
                                        data-confirmar-titulo="Anular lote"
                                        data-confirmar-aceptar="Anular lote"
                                        data-confirmar-peligro="true" />
                                    <asp:Button ID="btnGuardarCambios" runat="server"
                                        Text="Guardar cambios"
                                        CssClass="btn btn-primary"
                                        OnClick="btnGuardarCambios_Click"
                                        ValidationGroup="EdicionLote"
                                        data-validar-grupo="EdicionLote"
                                        data-confirmar="¿Está seguro de aplicar estos cambios a todo el lote?"
                                        data-confirmar-aceptar="Guardar cambios" />
                                </div>
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
                                <asp:Button ID="btnEditarDesdeDetal" runat="server" Text="Editar lote"
                                    CssClass="btn btn-outline-secondary btn-sm" OnClick="btnEditarDesdeDetal_Click" CausesValidation="false" />
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

                <!-- ====== REGISTRO MANUAL DE DOCUMENTOS ======
                     Se abre solo desde el expediente de la Bandeja ("Registro manual", ?manual=1&lote=).
                     Ver los documentos y subirlos es trabajo del expediente; aquí queda lo que
                     solo se puede hacer a mano: escribir un número sin tener el documento, adjuntar
                     el PDF de la factura o del CPIC y los manifiestos de cruce y retorno. -->
                <asp:Panel ID="pnlManifiestosLote" runat="server" Visible="false">
                    <section class="sgv-panel ld-docs-page">
                        <div class="sgv-panel-cab">
                            <div>
                                <h2 class="sgv-panel-titulo">Registro manual de documentos</h2>
                                <p class="sgv-resumen"><span><asp:Label ID="lblClienteManifiestos" runat="server"></asp:Label></span><span>Pedido <asp:Label ID="lblPedidoManifiestos" runat="server"></asp:Label></span></p>
                            </div>
                            <div class="ld-cab-acciones">
                                <asp:HyperLink ID="hlVolverBandejaDocs" runat="server"
                                    NavigateUrl="~/Views/BandejaDocumentos.aspx"
                                    CssClass="btn btn-primary btn-sm">← Volver al expediente</asp:HyperLink>
                                <asp:Button ID="btnVolverManifiestos" runat="server" Text="Ver el lote"
                                    CssClass="btn btn-outline-secondary btn-sm"
                                    OnClick="btnVolverManifiestos_Click" CausesValidation="false" />
                            </div>
                        </div>
                        <div class="sgv-panel-cuerpo">

                            <asp:Panel ID="pnlMensajeManifiesto" runat="server" Visible="false" CssClass="mb-3">
                                <asp:Label ID="lblMensajeManifiesto" runat="server" CssClass="alert d-block"></asp:Label>
                            </asp:Panel>

                            <div class="ld-aviso ld-aviso-neutro">
                                <div>Use esta pantalla solo cuando no tenga el documento. Lo normal es soltar el ZIP del correo, la CPIC o los MCI
                                    en el expediente del pedido: el sistema lee los números y adjunta los PDF.</div>
                            </div>
                            <asp:Panel ID="pnlEsperaZip" runat="server" Visible="false" CssClass="ld-aviso ld-aviso-alerta">
                                <i class="fas fa-exclamation-triangle" aria-hidden="true"></i>
                                <div>
                                    <strong>Este lote todavía espera el correo con sus documentos.</strong>
                                    Si escribe aquí la guía o la factura, el ZIP ya no reconocerá esa carga.
                                </div>
                            </asp:Panel>

                            <%-- 1. Factura y CPIC: una tarjeta por documento (lote con una sola factura y CPIC) --%>
                            <asp:Panel ID="pnlDocsUnicos" runat="server">
                            <div class="ld-docs-grid">
                                <div class="ld-doc-card">
                                    <div class="ld-doc-card-head">
                                        <div class="ld-doc-card-titulo">Factura</div>
                                        <div class="ld-doc-card-numero"><asp:Label ID="lblNumeroFacturaDocs" runat="server"></asp:Label></div>
                                    </div>
                                    <div class="ld-doc-archivo">
                                        <asp:LinkButton ID="lnkVerDocFactura" runat="server" OnClick="lnkVerDocFactura_Click" CausesValidation="false" Visible="false" CssClass="ld-doc-ver"></asp:LinkButton>
                                    </div>
                                    <%-- Factura aún sin registrar: se anota aquí mismo, sin pasar por "Editar". --%>
                                    <asp:Panel ID="pnlRegistrarFactura" runat="server" CssClass="ld-doc-manual-envoltura" Visible="false" DefaultButton="btnRegistrarFactura">
                                    <div class="ld-doc-registrar" data-montos-grupo>
                                        <div class="ld-doc-registrar-titulo">Registrar factura</div>
                                        <div class="ld-doc-campos">
                                            <div class="ld-campo ld-campo-ancho">
                                                <label for="<%= txtFacturaNueva.ClientID %>">N° de factura <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                                <asp:TextBox ID="txtFacturaNueva" runat="server" CssClass="form-control form-control-sm" MaxLength="30" placeholder="Ej: F222-00004267" aria-required="true"></asp:TextBox>
                                                <asp:RequiredFieldValidator ID="rfvFacturaNueva" runat="server" ControlToValidate="txtFacturaNueva"
                                                    ErrorMessage="Escriba el número de la factura." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgFacturaLote" />
                                            </div>
                                            <div class="ld-campo">
                                                <label for="<%= txtFacturaFecha.ClientID %>">Fecha de emisión <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                                <asp:TextBox ID="txtFacturaFecha" runat="server" CssClass="form-control form-control-sm" TextMode="Date" aria-required="true"></asp:TextBox>
                                                <asp:RequiredFieldValidator ID="rfvFacturaFecha" runat="server" ControlToValidate="txtFacturaFecha"
                                                    ErrorMessage="Indique la fecha de emisión de la factura." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgFacturaLote" />
                                            </div>
                                            <div class="ld-campo">
                                                <label for="<%= txtFacturaValor.ClientID %>">Valor total</label>
                                                <asp:TextBox ID="txtFacturaValor" runat="server" CssClass="form-control form-control-sm" inputmode="decimal" autocomplete="off" data-monto="true" placeholder="0.00"></asp:TextBox>
                                            </div>
                                            <div class="ld-campo">
                                                <label for="<%= ddlFacturaMoneda.ClientID %>">Moneda</label>
                                                <asp:DropDownList ID="ddlFacturaMoneda" runat="server" CssClass="form-control form-control-sm">
                                                    <asp:ListItem Value="PEN" Text="Soles (PEN)"></asp:ListItem>
                                                    <asp:ListItem Value="USD" Text="Dólares (USD)"></asp:ListItem>
                                                </asp:DropDownList>
                                            </div>
                                        </div>
                                        <asp:Button ID="btnRegistrarFactura" runat="server" Text="Registrar factura"
                                            CssClass="btn btn-primary btn-sm" OnClick="btnRegistrarFactura_Click" ValidationGroup="vgFacturaLote" />
                                    </div>
                                    </asp:Panel>
                                    <asp:Panel ID="pnlSubirPdfFactura" runat="server" CssClass="ld-doc-subir" Visible="false">
                                        <label class="ld-doc-elegir">
                                            <span class="ld-doc-elegir-texto">Elegir PDF o foto (máx. 10 MB)</span>
                                            <asp:FileUpload ID="fuPdfFactura" runat="server" CssClass="ld-doc-input" accept=".pdf,.jpg,.jpeg,.png" data-max-mb="10" data-max-envio-mb="24" />
                                        </label>
                                        <asp:Button ID="btnSubirPdfFactura" runat="server" Text="Adjuntar"
                                            CssClass="btn btn-primary btn-sm ld-doc-btn" OnClick="btnSubirPdfFactura_Click" CausesValidation="false" />
                                    </asp:Panel>
                                </div>

                                <asp:Panel ID="pnlFilaCpicDocs" runat="server" CssClass="ld-doc-card">
                                    <div class="ld-doc-card-head">
                                        <div class="ld-doc-card-titulo">CPIC</div>
                                        <div class="ld-doc-card-numero"><asp:Label ID="lblNumeroCpicDocs" runat="server"></asp:Label></div>
                                    </div>
                                    <div class="ld-doc-archivo">
                                        <asp:LinkButton ID="lnkVerDocCpic" runat="server" OnClick="lnkVerDocCpic_Click" CausesValidation="false" Visible="false" CssClass="ld-doc-ver"></asp:LinkButton>
                                    </div>
                                    <asp:Panel ID="pnlRegistrarCpic" runat="server" CssClass="ld-doc-registrar" Visible="false" DefaultButton="btnRegistrarCpic" data-montos-grupo="true">
                                        <div class="ld-doc-registrar-titulo">Registrar CPIC</div>
                                        <div class="ld-doc-campos">
                                            <div class="ld-campo ld-campo-ancho">
                                                <label for="<%= txtCpicNuevo.ClientID %>">N° de CPIC (7 dígitos) <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                                <asp:TextBox ID="txtCpicNuevo" runat="server" CssClass="form-control form-control-sm" MaxLength="7" placeholder="Ej: 1234567" inputmode="numeric" aria-required="true"></asp:TextBox>
                                                <asp:RequiredFieldValidator ID="rfvCpicNuevo" runat="server" ControlToValidate="txtCpicNuevo"
                                                    ErrorMessage="Escriba el número de la CPIC." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgCpicLote" />
                                                <asp:RegularExpressionValidator ID="revCpicNuevo" runat="server" ControlToValidate="txtCpicNuevo" ValidationExpression="^\s*\d{7}\s*$"
                                                    ErrorMessage="El número de CPIC debe tener 7 dígitos." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgCpicLote" />
                                            </div>
                                            <div class="ld-campo">
                                                <label for="<%= txtCpicFecha.ClientID %>">Fecha de emisión <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                                <asp:TextBox ID="txtCpicFecha" runat="server" CssClass="form-control form-control-sm" TextMode="Date" aria-required="true"></asp:TextBox>
                                                <asp:RequiredFieldValidator ID="rfvCpicFecha" runat="server" ControlToValidate="txtCpicFecha"
                                                    ErrorMessage="Indique la fecha de emisión de la CPIC." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgCpicLote" />
                                            </div>
                                            <div class="ld-campo">
                                                <label for="<%= txtCpicFlete.ClientID %>">Valor del flete</label>
                                                <asp:TextBox ID="txtCpicFlete" runat="server" CssClass="form-control form-control-sm" inputmode="decimal" autocomplete="off" data-monto="true" placeholder="0.00"></asp:TextBox>
                                            </div>
                                        </div>
                                        <asp:Button ID="btnRegistrarCpic" runat="server" Text="Registrar CPIC"
                                            CssClass="btn btn-primary btn-sm" OnClick="btnRegistrarCpic_Click" ValidationGroup="vgCpicLote" />
                                    </asp:Panel>
                                    <asp:Panel ID="pnlSubirPdfCpic" runat="server" CssClass="ld-doc-subir" Visible="false">
                                        <label class="ld-doc-elegir">
                                            <span class="ld-doc-elegir-texto">Elegir PDF o foto (máx. 10 MB)</span>
                                            <asp:FileUpload ID="fuPdfCpic" runat="server" CssClass="ld-doc-input" accept=".pdf,.jpg,.jpeg,.png" data-max-mb="10" data-max-envio-mb="24" />
                                        </label>
                                        <asp:Button ID="btnSubirPdfCpic" runat="server" Text="Adjuntar"
                                            CssClass="btn btn-primary btn-sm ld-doc-btn" OnClick="btnSubirPdfCpic_Click" CausesValidation="false" />
                                    </asp:Panel>
                                </asp:Panel>
                            </div>
                            <p class="ld-nota">
                                Para corregir un número ya registrado use <strong>Editar</strong> en la lista de lotes.
                            </p>
                            </asp:Panel>

                            <%-- 1b. Factura y CPIC por despacho: separar un lote que lleva más de una factura o CPIC
                                 (p. ej. dos facturas de Novopan el mismo día). Abierta si el lote ya tiene más de una. --%>
                            <asp:Panel ID="pnlDocsPorDespacho" runat="server" CssClass="ld-bloque ld-docs-despacho">
                                <asp:Panel ID="pnlEnlaceDocsPorDespacho" runat="server" CssClass="ld-docs-despacho-enlace">
                                    <%-- El panel plegable tiene id fijo (ClientIDMode Static) para el data-toggle de Bootstrap 4. --%>
                                    <asp:HyperLink ID="lnkPlegarDocsPorDespacho" runat="server" NavigateUrl="#pnlCuerpoDocsPorDespacho"
                                        data-toggle="collapse" aria-controls="pnlCuerpoDocsPorDespacho" role="button">
                                        Esta carga lleva más de una factura
                                    </asp:HyperLink>
                                </asp:Panel>
                                <asp:Panel ID="pnlCuerpoDocsPorDespacho" runat="server" ClientIDMode="Static" CssClass="collapse">
                                    <div class="ld-bloque-head">
                                        <h3 class="ld-bloque-titulo">Factura y CPIC por despacho</h3>
                                        <span class="ld-bloque-ayuda">Marque los despachos que van con otra factura o CPIC, escriba sus números y pulse
                                            <strong>Asignar a los marcados</strong>. Los demás quedan como están.</span>
                                    </div>
                                    <asp:Panel ID="pnlAvisoVariosDocs" runat="server" Visible="false" CssClass="ld-aviso ld-aviso-info">
                                        <div>Este lote lleva más de una factura o CPIC: cada documento se corrige y se adjunta aquí, no en Editar.</div>
                                    </asp:Panel>

                                    <div class="table-responsive ld-docs-despacho-tabla">
                                        <table class="table sgv-tabla mb-0 ld-tabla-docs-despacho">
                                            <thead>
                                                <tr>
                                                    <th class="ld-col-marca"><span class="sr-only">Marcar</span></th>
                                                    <th>Chofer</th>
                                                    <th>Tracto / carreta</th>
                                                    <th>Guía / MCI</th>
                                                    <th>Factura</th>
                                                    <th>CPIC</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                <asp:Repeater ID="rptDespachosDocs" runat="server">
                                                    <ItemTemplate>
                                                        <tr>
                                                            <td class="ld-col-marca">
                                                                <label class="mb-0"><asp:CheckBox ID="chkDespachoDoc" runat="server" CssClass="ld-marca-despacho" /><span class="sr-only"><%#: "Marcar el despacho " + Eval("NumeroDespacho") %></span></label>
                                                                <asp:HiddenField ID="hidIdDespachoDoc" runat="server" Value='<%# Eval("IdDespacho") %>' />
                                                            </td>
                                                            <td data-label="Chofer"><%#: Eval("Chofer") %><div class="sgv-suave"><%#: Eval("NumeroDespacho") %></div></td>
                                                            <td data-label="Tracto / carreta"><%#: Eval("PlacaTracto") %> / <%#: Eval("PlacaCarreta") %></td>
                                                            <td data-label="Guía / MCI"><%# CeldaGuiaMci(Eval("Guia"), Eval("Manifiestos")) %></td>
                                                            <td data-label="Factura"><%# CeldaDocumento(Eval("NumeroFactura"), Eval("FacturaConPdf")) %></td>
                                                            <td data-label="CPIC"><%# CeldaDocumento(Eval("NumeroCPIC"), Eval("CpicConPdf")) %></td>
                                                        </tr>
                                                    </ItemTemplate>
                                                </asp:Repeater>
                                            </tbody>
                                        </table>
                                    </div>

                                    <div class="ld-doc-registrar ld-asignar-docs" data-montos-grupo>
                                        <div class="ld-doc-registrar-titulo">Asignar a los marcados</div>
                                        <p class="ld-bloque-ayuda mb-2">Si el número ya existe se usa ese documento (la factura, del mismo cliente);
                                            si no, se crea. Deje vacío lo que no cambia.</p>
                                        <div class="ld-doc-campos">
                                            <div class="ld-campo ld-campo-ancho">
                                                <label for="<%= txtAsigFactura.ClientID %>">N° de factura</label>
                                                <asp:TextBox ID="txtAsigFactura" runat="server" CssClass="form-control form-control-sm" MaxLength="30" placeholder="Ej: 001-098-000012345"></asp:TextBox>
                                            </div>
                                            <div class="ld-campo">
                                                <label for="<%= txtAsigFacturaFecha.ClientID %>">Fecha de emisión</label>
                                                <asp:TextBox ID="txtAsigFacturaFecha" runat="server" CssClass="form-control form-control-sm" TextMode="Date"></asp:TextBox>
                                            </div>
                                            <div class="ld-campo">
                                                <label for="<%= txtAsigFacturaValor.ClientID %>">Valor total</label>
                                                <asp:TextBox ID="txtAsigFacturaValor" runat="server" CssClass="form-control form-control-sm" inputmode="decimal" autocomplete="off" data-monto="true" placeholder="0.00"></asp:TextBox>
                                            </div>
                                            <div class="ld-campo">
                                                <label for="<%= ddlAsigFacturaMoneda.ClientID %>">Moneda</label>
                                                <asp:DropDownList ID="ddlAsigFacturaMoneda" runat="server" CssClass="form-control form-control-sm">
                                                    <asp:ListItem Value="USD" Text="Dólares (USD)"></asp:ListItem>
                                                    <asp:ListItem Value="PEN" Text="Soles (PEN)"></asp:ListItem>
                                                </asp:DropDownList>
                                            </div>
                                        </div>
                                        <div class="ld-doc-campos">
                                            <div class="ld-campo ld-campo-ancho">
                                                <label for="<%= txtAsigCpic.ClientID %>">N° de CPIC (7 dígitos)</label>
                                                <asp:TextBox ID="txtAsigCpic" runat="server" CssClass="form-control form-control-sm" MaxLength="7" placeholder="Ej: 1234567"></asp:TextBox>
                                            </div>
                                            <div class="ld-campo">
                                                <label for="<%= txtAsigCpicFecha.ClientID %>">Fecha de emisión</label>
                                                <asp:TextBox ID="txtAsigCpicFecha" runat="server" CssClass="form-control form-control-sm" TextMode="Date"></asp:TextBox>
                                            </div>
                                            <div class="ld-campo">
                                                <label for="<%= txtAsigCpicFlete.ClientID %>">Valor del flete</label>
                                                <asp:TextBox ID="txtAsigCpicFlete" runat="server" CssClass="form-control form-control-sm" inputmode="decimal" autocomplete="off" data-monto="true" placeholder="0.00"></asp:TextBox>
                                            </div>
                                        </div>
                                        <asp:Button ID="btnAsignarDocumentos" runat="server" Text="Asignar a los marcados"
                                            CssClass="btn btn-primary btn-sm" OnClick="btnAsignarDocumentos_Click" CausesValidation="false" />
                                    </div>

                                    <div class="ld-docs-lista">
                                        <div class="ld-doc-registrar-titulo">Documentos del lote</div>
                                        <asp:Panel ID="pnlSinDocumentosLote" runat="server" CssClass="sgv-suave">Todavía no tiene factura ni CPIC.</asp:Panel>
                                        <asp:Repeater ID="rptDocumentosLote" runat="server" OnItemCommand="rptDocumentosLote_ItemCommand">
                                            <ItemTemplate>
                                                <div class="ld-docs-lista-fila">
                                                    <div class="ld-docs-lista-doc">
                                                        <span class="ld-docs-lista-tipo"><%#: Eval("TipoTexto") %></span>
                                                        <span class="ld-docs-num"><%#: Eval("Numero") %></span>
                                                        <span class="sgv-suave"><%#: Eval("Despachos") %> despacho(s)</span>
                                                        <%# InsigniaDocumento((bool)Eval("ConPdf"), (bool)Eval("ConPdf") ? "PDF" : "sin PDF") %>
                                                    </div>
                                                    <div class="ld-docs-lista-acciones">
                                                        <asp:PlaceHolder runat="server" Visible='<%# (bool)Eval("ConPdf") %>'>
                                                            <a href="<%#: Eval("UrlPdf") %>" target="_blank" rel="noopener" class="ld-doc-ver">Ver PDF<%#: Eval("AvisoPdf") %></a>
                                                        </asp:PlaceHolder>
                                                        <asp:Panel runat="server" CssClass="ld-doc-subir" Visible='<%# (bool)Eval("PuedeSubir") %>'>
                                                            <label class="ld-doc-elegir">
                                                                <span class="ld-doc-elegir-texto">Elegir PDF o foto (máx. 10 MB)</span>
                                                                <asp:FileUpload ID="fuPdfDocumentoLote" runat="server" CssClass="ld-doc-input" accept=".pdf,.jpg,.jpeg,.png" data-max-mb="10" data-max-envio-mb="24" />
                                                            </label>
                                                            <asp:Button runat="server" Text="Subir PDF" CssClass="btn btn-primary btn-sm ld-doc-btn"
                                                                CommandName="SubirPdf" CommandArgument='<%# Eval("Clave") %>' CausesValidation="false" />
                                                        </asp:Panel>
                                                    </div>
                                                </div>
                                            </ItemTemplate>
                                        </asp:Repeater>
                                    </div>
                                </asp:Panel>
                            </asp:Panel>

                            <%-- 2. Guías por despacho --%>
                            <asp:Panel ID="pnlGuiasLote" runat="server" CssClass="ld-bloque">
                                <div class="ld-bloque-head">
                                    <h3 class="ld-bloque-titulo">Guías por despacho</h3>
                                    <span class="ld-bloque-ayuda">Escriba la guía de cada unidad y pulse <strong>Guardar guías</strong>.</span>
                                </div>
                                <div class="table-responsive" data-enter="<%= btnGuardarGuias.ClientID %>">
                                    <asp:GridView ID="gvGuiasLote" runat="server"
                                        CssClass="table sgv-tabla mb-0 ld-tabla-guias"
                                        AutoGenerateColumns="false"
                                        DataKeyNames="IdDespacho"
                                        GridLines="None" CellSpacing="-1"
                                        EmptyDataText="No hay despachos en este lote.">
                                        <EmptyDataRowStyle CssClass="sgv-vacio" />
                                        <Columns>
                                            <asp:BoundField DataField="NumeroDespacho" HeaderText="Despacho" />
                                            <asp:BoundField DataField="NombreConductor" HeaderText="Conductor" />
                                            <asp:BoundField DataField="PlacaTracto" HeaderText="Tracto" ItemStyle-CssClass="ld-placa" />
                                            <asp:TemplateField HeaderText="Guía remitente">
                                                <ItemTemplate>
                                                    <asp:TextBox ID="txtGuiaRemitenteFila" runat="server" CssClass="form-control form-control-sm ld-guia-input"
                                                        MaxLength="50" placeholder="Ej: T660-00016530" aria-label='<%# "Guía remitente del despacho " + Eval("NumeroDespacho") %>' Text='<%# Eval("GuiaRemitente") %>'></asp:TextBox>
                                                </ItemTemplate>
                                            </asp:TemplateField>
                                            <asp:TemplateField HeaderText="Guía transportista">
                                                <ItemTemplate>
                                                    <asp:TextBox ID="txtGuiaTransportistaFila" runat="server" CssClass="form-control form-control-sm ld-guia-input"
                                                        MaxLength="50" placeholder="Opcional" aria-label='<%# "Guía transportista del despacho " + Eval("NumeroDespacho") %>' Text='<%# Eval("GuiaTransportista") %>'></asp:TextBox>
                                                </ItemTemplate>
                                            </asp:TemplateField>
                                        </Columns>
                                    </asp:GridView>
                                </div>
                                <div class="ld-bloque-pie">
                                    <asp:Button ID="btnGuardarGuias" runat="server" Text="Guardar guías"
                                        CssClass="btn btn-primary" OnClick="btnGuardarGuias_Click" CausesValidation="false" />
                                </div>
                            </asp:Panel>

                            <%-- 3. Manifiestos de aduana (solo internacional) --%>
                            <asp:Panel ID="pnlAvisoNacionalSinManifiesto" runat="server" Visible="false" CssClass="ld-aviso ld-aviso-neutro">
                                <div>Este lote es nacional: el manifiesto de aduana no aplica.</div>
                            </asp:Panel>

                            <asp:Panel ID="pnlSeccionManifiestoConductor" runat="server" CssClass="ld-bloque">
                                <div class="ld-bloque-head">
                                    <h3 class="ld-bloque-titulo">Manifiestos por conductor</h3>
                                    <span class="ld-bloque-ayuda">Adjunte o reemplace el manifiesto de cruce y de retorno a medida que avanza el viaje.</span>
                                </div>
                                <div class="table-responsive">
                                    <asp:GridView ID="gvManifiestosLote" runat="server"
                                        CssClass="table sgv-tabla mb-0"
                                        AutoGenerateColumns="false"
                                        DataKeyNames="IdDespacho"
                                        OnRowCommand="gvManifiestosLote_RowCommand"
                                        GridLines="None" CellSpacing="-1"
                                        EmptyDataText="Este lote no tiene despachos asociados.">
                                        <EmptyDataRowStyle CssClass="sgv-vacio" />
                                        <Columns>
                                            <asp:BoundField DataField="NumeroDespacho" HeaderText="Despacho" />
                                            <asp:BoundField DataField="NombreConductor" HeaderText="Conductor" />

                                            <asp:TemplateField HeaderText="Manifiesto de cruce">
                                                <ItemTemplate>
                                                    <asp:LinkButton runat="server" CommandName="VerManifiesto" CommandArgument='<%# Eval("CruceIdDocumento") %>'
                                                        Visible='<%# Eval("CruceIdDocumento") != null %>' CausesValidation="false" CssClass="ld-doc-ver">
                                                        <%#: Eval("CruceNombreOriginal") %>
                                                    </asp:LinkButton>
                                                    <%# ListaDespachosReglas.SelloSinAdjuntar(Eval("CruceIdDocumento")) %>
                                                    <asp:FileUpload runat="server" ID="fileCruceFila" CssClass="form-control form-control-sm mt-1" accept=".pdf,.jpg,.jpeg,.png" ToolTip="PDF, JPG o PNG. Máx. 10 MB." data-max-mb="10" data-max-envio-mb="24" />
                                                </ItemTemplate>
                                            </asp:TemplateField>

                                            <asp:TemplateField HeaderText="Manifiesto de retorno">
                                                <ItemTemplate>
                                                    <asp:LinkButton runat="server" CommandName="VerManifiesto" CommandArgument='<%# Eval("RetornoIdDocumento") %>'
                                                        Visible='<%# Eval("RetornoIdDocumento") != null %>' CausesValidation="false" CssClass="ld-doc-ver">
                                                        <%#: Eval("RetornoNombreOriginal") %>
                                                    </asp:LinkButton>
                                                    <%# ListaDespachosReglas.SelloSinAdjuntar(Eval("RetornoIdDocumento")) %>
                                                    <asp:FileUpload runat="server" ID="fileRetornoFila" CssClass="form-control form-control-sm mt-1" accept=".pdf,.jpg,.jpeg,.png" ToolTip="PDF, JPG o PNG. Máx. 10 MB." data-max-mb="10" data-max-envio-mb="24" />
                                                </ItemTemplate>
                                            </asp:TemplateField>

                                            <asp:TemplateField HeaderText="">
                                                <ItemTemplate>
                                                    <asp:Button runat="server" Text="Guardar" CssClass="btn btn-primary btn-sm"
                                                        CommandName="GuardarManifiesto" CommandArgument='<%# Eval("IdDespacho") %>' />
                                                </ItemTemplate>
                                            </asp:TemplateField>
                                        </Columns>
                                    </asp:GridView>
                                </div>
                            </asp:Panel>
                        </div>
                    </section>
                </asp:Panel>

            </ContentTemplate>
            <Triggers>
                <%-- Los controles dentro del ContentTemplate ya hacen un postback parcial; aquí van los que deben
                     hacerlo completo (FileUpload) y, por claridad, los de siempre. Un ControlID que no existe hace
                     caer la página: al quitar un control, quite también su trigger. --%>
                <asp:AsyncPostBackTrigger ControlID="lnkStatDocumentosPendientes" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="ddlFiltroDocumentosLotes" EventName="SelectedIndexChanged" />
                <asp:AsyncPostBackTrigger ControlID="btnGuardarGuias" EventName="Click" />
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

                <asp:AsyncPostBackTrigger ControlID="btnCancelarEdicion" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="btnVolverLotes" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="btnGuardarCambios" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="btnAnularLote" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="ddlFiltroEstadoLotes" EventName="SelectedIndexChanged" />
                <asp:AsyncPostBackTrigger ControlID="ddlTipoOperacionEdit" EventName="SelectedIndexChanged" />
                <asp:AsyncPostBackTrigger ControlID="rblAmbitoEdit" EventName="SelectedIndexChanged" />

                <asp:AsyncPostBackTrigger ControlID="btnEditarDesdeDetal" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="btnVolverLotesDetalle" EventName="Click" />

                <asp:AsyncPostBackTrigger ControlID="btnVolverManifiestos" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="lnkVerDocFactura" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="lnkVerDocCpic" EventName="Click" />
                <asp:PostBackTrigger ControlID="gvManifiestosLote" />
                <%-- FileUpload: postback completo, en uno parcial el archivo no llega. --%>
                <asp:PostBackTrigger ControlID="btnSubirPdfFactura" />
                <asp:PostBackTrigger ControlID="btnSubirPdfCpic" />
                <asp:PostBackTrigger ControlID="rptDocumentosLote" />
                <asp:AsyncPostBackTrigger ControlID="btnAsignarDocumentos" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="lnkIrDocsPorDespacho" EventName="Click" />
            </Triggers>
        </asp:UpdatePanel>
    </div>

</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="ScriptsSection" runat="server">
    <!-- Select2 JS -->
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/lib/select2-4.0.13.min.js") %>"></script>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            txtNumeroPedidoEdit: '<%= txtNumeroPedidoEdit.ClientID %>',
            hfArgumentoFila: '<%= hfArgumentoFila.ClientID %>',
            btnAccionFila: '<%= btnAccionFila.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/ListaDespachos.js") %>"></script>
</asp:Content>

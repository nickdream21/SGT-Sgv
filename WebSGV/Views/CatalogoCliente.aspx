<%@ Page Title="Catálogo del cliente" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="CatalogoCliente.aspx.cs" Inherits="WebSGV.Views.CatalogoCliente" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdItem" runat="server" />
    <%-- Activar/Desactivar de la fila (E5-16): la grilla no guarda ViewState; el botón de la fila copia su
         data-argumento aquí y pulsa btnAccionFila (SGV.acciones.enviarFila). El servidor lo revalida. --%>
    <asp:HiddenField ID="hfArgumentoFila" runat="server" />
    <asp:Button ID="btnAccionFila" runat="server" Text="Cambiar estado" CssClass="d-none" CausesValidation="false"
        OnClick="btnAccionFila_Click" tabindex="-1" aria-hidden="true" />

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="sgv-encabezado">
        <div class="sgv-encabezado-titulo">
            <h1 class="h2">Cat&#xE1;logo del cliente <asp:Label ID="lblResumen" runat="server" CssClass="sgv-encabezado-contador" /></h1>
            <p>Productos, plantas y rutas propias de cada cliente</p>
        </div>
        <div class="sgv-encabezado-acciones">
            <% if (IdClienteSeleccionado > 0) { %>
            <a class="btn btn-primary btn-sm d-lg-none" href="#altaCatalogo">Agregar</a>
            <% } %>
            <a class="sgv-ayuda" href="<%: ResolveUrl("~/Views/Ayuda.aspx?tema=clientes-plantas") %>" target="_blank" rel="noopener"
                data-ayuda="clientes-plantas" aria-label="Ayuda de esta p&#xE1;gina">?</a>
        </div>
    </div>

    <!-- Selector de cliente -->
    <div class="row mb-4">
        <div class="col-12 col-md-6 col-lg-5">
            <section class="sgv-panel">
                <div class="sgv-panel-cuerpo">
                    <label for="<%= ddlCliente.ClientID %>" class="sgv-etiqueta">Cliente</label>
                    <asp:DropDownList ID="ddlCliente" runat="server" CssClass="form-control"
                        AutoPostBack="true" OnSelectedIndexChanged="ddlCliente_SelectedIndexChanged">
                    </asp:DropDownList>
                </div>
            </section>
        </div>
    </div>

    <asp:Panel ID="pnlCatalogo" runat="server" Visible="false">

        <!-- Pestañas de catálogo: icono, nombre y contador los arma el code-behind en LinkButton.Text (B5-9) -->
        <ul class="nav nav-tabs mb-0" role="tablist">
            <li class="nav-item">
                <asp:LinkButton ID="lnkProductos" runat="server" CssClass="nav-link"
                    CommandArgument="Producto" OnCommand="CambiarTab_Command" CausesValidation="false" />
            </li>
            <li class="nav-item">
                <asp:LinkButton ID="lnkPlantasCarga" runat="server" CssClass="nav-link"
                    CommandArgument="PlantaCarga" OnCommand="CambiarTab_Command" CausesValidation="false" />
            </li>
            <li class="nav-item">
                <asp:LinkButton ID="lnkPlantasDescarga" runat="server" CssClass="nav-link"
                    CommandArgument="PlantaDescarga" OnCommand="CambiarTab_Command" CausesValidation="false" />
            </li>
            <li class="nav-item">
                <asp:LinkButton ID="lnkRutas" runat="server" CssClass="nav-link"
                    CommandArgument="Ruta" OnCommand="CambiarTab_Command" CausesValidation="false" />
            </li>
        </ul>

        <section class="sgv-panel">
            <div class="sgv-panel-cuerpo">
                <%-- B5-5: las rutas "General" (sin cliente) las ven todos los clientes --%>
                <asp:Panel ID="pnlNotaRutas" runat="server" Visible="false" CssClass="alert alert-warning py-2 small" role="note">
                    Las rutas marcadas <strong>General</strong> son comunes a todos los clientes: editarlas o desactivarlas
                    cambia la ruta para todos. El nombre de una ruta no se puede repetir, aunque la otra sea de otro cliente.
                </asp:Panel>
                <div class="row">

                    <!-- Alta -->
                    <div class="col-12 col-lg-4 mb-4" id="altaCatalogo">
                        <div class="border rounded p-3 h-100">
                            <h2 class="h6 font-weight-bold mb-3">
                                Agregar <asp:Literal ID="litEtiquetaAlta" runat="server" Text="elemento" />
                            </h2>
                            <div class="form-group">
                                <label for="<%= txtNombre.ClientID %>" class="sgv-etiqueta">Nombre <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                <asp:TextBox ID="txtNombre" runat="server" aria-required="true" CssClass="form-control" MaxLength="50"></asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvNombre" runat="server" ValidationGroup="vgAlta"
                                    ControlToValidate="txtNombre" ErrorMessage="El nombre es obligatorio."
                                    CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                            </div>
                            <div class="form-group">
                                <label for="<%= txtDetalle.ClientID %>" class="sgv-etiqueta"><asp:Literal ID="litEtiquetaDetalle" runat="server" Text="Descripción" />:</label>
                                <asp:TextBox ID="txtDetalle" runat="server" CssClass="form-control" MaxLength="300"
                                    TextMode="MultiLine" Rows="2"></asp:TextBox>
                            </div>
                            <asp:Button ID="btnAgregar" runat="server" CssClass="btn btn-primary btn-block"
                                Text="Agregar" ValidationGroup="vgAlta" OnClick="btnAgregar_Click" />
                        </div>
                    </div>

                    <!-- Listado -->
                    <div class="col-12 col-lg-8 mb-4 order-first order-lg-0">
                        <p class="sgv-panel-nota mb-2" aria-live="polite"><asp:Literal ID="litResumen" runat="server" Mode="Encode" EnableViewState="false" /></p>
                        <div class="table-responsive">
                            <asp:GridView ID="gvCatalogo" runat="server"
                                CssClass="table sgv-tabla mb-0"
                                AutoGenerateColumns="false"
                                EnableViewState="false"
                                AllowPaging="true" PageSize="25"
                                OnPageIndexChanging="gvCatalogo_PageIndexChanging"
                                GridLines="None" CellSpacing="-1"
                                EmptyDataText="Este cliente todavía no tiene nada cargado en esta sección.">
                                <PagerSettings Mode="NumericFirstLast" PageButtonCount="5" FirstPageText="Primera" LastPageText="Última" />
                                <PagerStyle CssClass="sgv-paginador" />
                                <EmptyDataRowStyle CssClass="sgv-vacio" />
                                <Columns>
                                    <asp:TemplateField HeaderText="Nombre">
                                        <ItemTemplate>
                                            <span class="font-weight-bold"><%#: Eval("nombre") %></span><%# EsGeneral(Eval("esGeneral")) ? " <span class=\"badge badge-secondary ml-1\">General</span>" : "" %>
                                            <%-- En el celular (< 576 px) el detalle va bajo el nombre: su columna se oculta. --%>
                                            <div class="small text-muted d-sm-none"><%#: Eval("detalle") %></div>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="Detalle" HeaderStyle-CssClass="d-none d-sm-table-cell" ItemStyle-CssClass="d-none d-sm-table-cell">
                                        <ItemTemplate>
                                            <small class="text-muted"><%#: Eval("detalle") %></small>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="Estado" HeaderStyle-CssClass="sgv-col-estado" ItemStyle-CssClass="sgv-col-estado">
                                        <ItemTemplate>
                                            <span class='badge <%#: ObtenerClaseEstado(Eval("activo")) %>'><%#: ObtenerTextoEstado(Eval("activo")) %></span>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="Acciones" HeaderStyle-CssClass="sgv-col-acciones" ItemStyle-CssClass="sgv-col-acciones">
                                        <ItemTemplate>
                                            <div class="sgv-acciones">
                                                <button type="button" class="sgv-btn-icono info btn-editar" title="Editar"
                                                    aria-label='<%#: "Editar " + EtiquetaTab + " " + Eval("nombre") %>'
                                                    data-id='<%#: Eval("idItem") %>'
                                                    data-nombre='<%# AttrEncode(Eval("nombre")) %>'
                                                    data-detalle='<%# AttrEncode(Eval("detalle")) %>'>
                                                    <i class="fas fa-edit" aria-hidden="true"></i>
                                                </button>
                                                <button type="button" class='<%#: ObtenerClaseBoton(Eval("activo")) %>'
                                                    data-accion="enviarFila"
                                                    data-argumento='<%#: ArgumentoEstado(Eval("idItem"), Eval("activo")) %>'
                                                    data-confirmar='<%#: PreguntaEstado(Eval("activo"), Eval("nombre")) %>'><%#: ObtenerTextoBoton(Eval("activo")) %></button>
                                            </div>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                </Columns>
                            </asp:GridView>
                        </div>
                    </div>

                </div>
            </div>
        </section>
    </asp:Panel>

    <!-- Modal Editar -->
    <div class="modal fade" id="modalEditar" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalEditarTitulo">
        <div class="modal-dialog" role="document">
            <div class="modal-content" data-enter="<%= btnActualizar.ClientID %>">
                <div class="modal-header">
                    <h5 class="modal-title" id="modalEditarTitulo">Editar <%: EtiquetaTab %></h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">&times;</button>
                </div>
                <div class="modal-body">
                    <asp:Panel ID="pnlErrorModal" runat="server" Visible="false" EnableViewState="false" CssClass="alert alert-danger" role="alert">
                        <asp:Label ID="lblErrorModal" runat="server" EnableViewState="false"></asp:Label>
                    </asp:Panel>
                    <div class="form-group">
                        <label for="<%= txtEditarNombre.ClientID %>" class="sgv-etiqueta">Nombre <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                        <asp:TextBox ID="txtEditarNombre" runat="server" aria-required="true" CssClass="form-control" MaxLength="50"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvEditarNombre" runat="server" ControlToValidate="txtEditarNombre" ValidationGroup="vgEditarCatalogo" ErrorMessage="El nombre es obligatorio." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                    </div>
                    <div class="form-group mb-0">
                        <label for="<%= txtEditarDetalle.ClientID %>" class="sgv-etiqueta"><asp:Literal ID="litEtiquetaDetalleEditar" runat="server" Text="Descripción" />:</label>
                        <asp:TextBox ID="txtEditarDetalle" runat="server" CssClass="form-control" MaxLength="300"
                            TextMode="MultiLine" Rows="2"></asp:TextBox>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnActualizar" runat="server" ValidationGroup="vgEditarCatalogo" CssClass="btn btn-info"
                        Text="Guardar cambios" OnClick="btnActualizar_Click" />
                </div>
            </div>
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hfIdItem: '<%= hfIdItem.ClientID %>',
            hfArgumentoFila: '<%= hfArgumentoFila.ClientID %>',
            btnAccionFila: '<%= btnAccionFila.ClientID %>',
            txtEditarNombre: '<%= txtEditarNombre.ClientID %>',
            txtEditarDetalle: '<%= txtEditarDetalle.ClientID %>',
            pnlMensaje: '<%= pnlMensaje.ClientID %>',
            pnlErrorModal: '<%= pnlErrorModal.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/CatalogoCliente.js") %>"></script>

</asp:Content>

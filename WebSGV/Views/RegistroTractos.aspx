<%@ Page Title="Tractos" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroTractos.aspx.cs" Inherits="WebSGV.Views.RegistroTractos" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdTracto" runat="server" />
    <%-- Activar/Desactivar de la fila (E5-16): la grilla no guarda ViewState; el botón de la fila copia su
         data-argumento aquí y pulsa btnAccionFila (SGV.acciones.enviarFila). El servidor lo revalida. --%>
    <asp:HiddenField ID="hfArgumentoFila" runat="server" />
    <%-- Texto de la última búsqueda (solo lo escribe Buscar): con él se filtra en todo postback. --%>
    <asp:HiddenField ID="hfFiltroAplicado" runat="server" />
    <asp:Button ID="btnAccionFila" runat="server" Text="Cambiar estado" CssClass="d-none" CausesValidation="false"
        OnClick="btnAccionFila_Click" tabindex="-1" aria-hidden="true" />

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="sgv-encabezado">
        <div class="sgv-encabezado-titulo">
            <h1 class="h2">Tractos <asp:Label ID="lblTotalTractos" runat="server" CssClass="sgv-encabezado-contador" Text="0 registro(s)" /></h1>
            <p>Administra los tractos disponibles para las operaciones</p>
        </div>
        <div class="sgv-encabezado-acciones">
            <a class="btn btn-primary btn-sm d-md-none" href="#altaCatalogo">Agregar tracto</a>
            <button type="button" class="sgv-ayuda" data-toggle="collapse" data-target="#ayudaPagina"
                aria-controls="ayudaPagina" aria-expanded="false" aria-label="Ayuda de esta página">?</button>
        </div>
    </div>
    <div id="ayudaPagina" class="collapse">
        <p class="sgv-ayuda-texto">Los tractos activos aparecen en los formularios de operaci&#xF3;n.
            Los inactivos quedan ocultos, pero sus datos hist&#xF3;ricos se conservan.</p>
    </div>

    <div class="row">

        <div class="col-12 col-md-4 mb-4" id="altaCatalogo">
            <section class="sgv-panel">
                <div class="sgv-panel-cab">
                    <h2 class="sgv-panel-titulo">Agregar tracto</h2>
                </div>
                <div class="sgv-panel-cuerpo">
                    <div class="form-group">
                        <label for="<%= txtPlaca.ClientID %>" class="sgv-etiqueta">N.&#xBA; de placa <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                        <asp:TextBox ID="txtPlaca" runat="server" aria-required="true" CssClass="form-control"
                            placeholder="Ej: ABC-123" MaxLength="10"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvPlaca" runat="server" ControlToValidate="txtPlaca" ValidationGroup="vgRegistroTracto"
                            ErrorMessage="Placa: este campo es obligatorio." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                        <asp:RegularExpressionValidator ID="revPlaca" runat="server" ControlToValidate="txtPlaca" ValidationGroup="vgRegistroTracto"
                            ValidationExpression="^\s*(?:[A-Za-z0-9\-]\s*){6,10}$" ErrorMessage="Placa: use 6 a 10 caracteres (letras, números o guion)." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                    </div>
                    <div class="form-group">
                        <label for="<%= txtMarca.ClientID %>" class="sgv-etiqueta">Marca <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                        <asp:TextBox ID="txtMarca" runat="server" aria-required="true" CssClass="form-control"
                            placeholder="Ej: VOLVO" MaxLength="30"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvMarca" runat="server" ControlToValidate="txtMarca" ValidationGroup="vgRegistroTracto"
                            ErrorMessage="Marca: este campo es obligatorio." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                        <asp:RegularExpressionValidator ID="revMarca" runat="server" ControlToValidate="txtMarca" ValidationGroup="vgRegistroTracto"
                            ValidationExpression="^[A-Za-zÁÉÍÓÚÑáéíóú0-9\s\-\.]{2,30}$" ErrorMessage="Marca: use entre 2 y 30 caracteres válidos." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                    </div>
                    <div class="form-group">
                        <label for="<%= txtModelo.ClientID %>" class="sgv-etiqueta">Modelo <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                        <asp:TextBox ID="txtModelo" runat="server" aria-required="true" CssClass="form-control"
                            placeholder="Ej: FH 460" MaxLength="30"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvModelo" runat="server" ControlToValidate="txtModelo" ValidationGroup="vgRegistroTracto"
                            ErrorMessage="Modelo: este campo es obligatorio." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                        <asp:RegularExpressionValidator ID="revModelo" runat="server" ControlToValidate="txtModelo" ValidationGroup="vgRegistroTracto"
                            ValidationExpression="^[A-Za-zÁÉÍÓÚÑáéíóú0-9\s\-\.]{2,30}$" ErrorMessage="Modelo: use entre 2 y 30 caracteres válidos." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                    </div>
                    <asp:Button ID="btnRegistrar" runat="server" Text="Registrar tracto" ValidationGroup="vgRegistroTracto"
                        CssClass="btn btn-primary btn-block" OnClick="btnRegistrar_Click" />
                </div>
            </section>
        </div>

        <div class="col-12 col-md-8 mb-4 order-first order-md-0">
            <section class="sgv-panel">
                <div class="sgv-panel-cab">
                    <div>
                        <h2 class="sgv-panel-titulo">Tractos registrados</h2>
                        <p class="sgv-panel-nota mb-0" aria-live="polite"><asp:Literal ID="litResumen" runat="server" Mode="Encode" EnableViewState="false" /></p>
                    </div>
                    <div class="sgv-buscador" role="search" data-enter="<%= btnBuscar.ClientID %>">
                        <asp:TextBox ID="txtBuscar" runat="server" CssClass="form-control form-control-sm" MaxLength="50"
                            placeholder="Placa, marca o modelo" aria-label="Buscar tractos por placa, marca o modelo"
                            autocomplete="off" spellcheck="false" autocapitalize="off" autocorrect="off" />
                        <asp:Button ID="btnBuscar" runat="server" Text="Buscar" CssClass="btn btn-outline-secondary btn-sm"
                            CausesValidation="false" OnClick="btnBuscar_Click" />
                    </div>
                </div>
                <div class="table-responsive">
                    <asp:GridView ID="gvTractos" runat="server"
                        CssClass="table sgv-tabla mb-0"
                        AutoGenerateColumns="false"
                        EnableViewState="false"
                        AllowPaging="true" PageSize="25"
                        OnPageIndexChanging="gvTractos_PageIndexChanging"
                        GridLines="None" CellSpacing="-1"
                        EmptyDataText="No hay tractos registrados.">
                        <PagerSettings Mode="NumericFirstLast" PageButtonCount="5" FirstPageText="Primera" LastPageText="Última" />
                        <PagerStyle CssClass="sgv-paginador" />
                        <EmptyDataRowStyle CssClass="sgv-vacio" />
                        <Columns>
                            <asp:BoundField DataField="idTracto" HeaderText="ID"
                                HeaderStyle-CssClass="sgv-col-id d-none d-md-table-cell" ItemStyle-CssClass="sgv-col-id d-none d-md-table-cell sgv-suave" />
                            <asp:TemplateField HeaderText="Placa">
                                <ItemTemplate>
                                    <span class="font-weight-bold text-nowrap"><%#: Eval("placaTracto") %></span>
                                    <div class="small text-muted d-sm-none"><%#: MarcaYModelo(Eval("marca"), Eval("modelo")) %></div>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="marca" HeaderText="Marca" HeaderStyle-CssClass="d-none d-sm-table-cell" ItemStyle-CssClass="d-none d-sm-table-cell" />
                            <asp:BoundField DataField="modelo" HeaderText="Modelo" HeaderStyle-CssClass="d-none d-sm-table-cell" ItemStyle-CssClass="d-none d-sm-table-cell" />
                            <asp:TemplateField HeaderText="Estado" HeaderStyle-CssClass="sgv-col-estado" ItemStyle-CssClass="sgv-col-estado">
                                <ItemTemplate>
                                    <span class='badge <%#: ObtenerClaseEstado(Eval("activo")) %>'><%#: ObtenerTextoEstado(Eval("activo")) %></span>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Acciones" HeaderStyle-CssClass="sgv-col-acciones" ItemStyle-CssClass="sgv-col-acciones">
                                <ItemTemplate>
                                    <div class="sgv-acciones">
                                        <button type="button" class="sgv-btn-icono info btn-editar" title="Editar"
                                            aria-label='Editar tracto <%#: Eval("placaTracto") %>'
                                            data-id='<%#: Eval("idTracto") %>'
                                            data-placa='<%# AttrEncode(Eval("placaTracto")) %>'
                                            data-marca='<%# AttrEncode(Eval("marca")) %>'
                                            data-modelo='<%# AttrEncode(Eval("modelo")) %>'
                                            data-con-historial='<%#: ConHistorial(Eval("conHistorial")) %>'>
                                            <i class="fas fa-edit" aria-hidden="true"></i>
                                        </button>
                                        <button type="button" class='<%#: ObtenerClaseBoton(Eval("activo")) %>'
                                            data-accion="enviarFila"
                                            data-argumento='<%#: ArgumentoEstado(Eval("idTracto"), Eval("activo")) %>'
                                            data-confirmar='<%#: PreguntaEstado(Eval("activo"), Eval("placaTracto")) %>'><%#: ObtenerTextoBoton(Eval("activo")) %></button>
                                    </div>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </section>
        </div>

    </div>

    <!-- Modal Editar Tracto -->
    <div class="modal fade" id="modalEditar" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalEditarTitulo">
        <div class="modal-dialog" role="document">
            <div class="modal-content" data-enter="<%= btnActualizarTracto.ClientID %>">
                <div class="modal-header">
                    <h5 class="modal-title" id="modalEditarTitulo">Editar tracto</h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">&times;</button>
                </div>
                <div class="modal-body">
                    <asp:Panel ID="pnlErrorModal" runat="server" Visible="false" EnableViewState="false" CssClass="alert alert-danger" role="alert">
                        <asp:Label ID="lblErrorModal" runat="server" EnableViewState="false"></asp:Label>
                    </asp:Panel>
                    <div class="form-group">
                        <label for="<%= txtEditarPlaca.ClientID %>" class="sgv-etiqueta">N.&#xBA; de placa <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                        <asp:TextBox ID="txtEditarPlaca" runat="server" aria-required="true" CssClass="form-control text-uppercase" MaxLength="10"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvEditarPlaca" runat="server" ControlToValidate="txtEditarPlaca" ValidationGroup="vgEditarTracto" ErrorMessage="Placa: este campo es obligatorio." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                        <small id="notaPlacaHistorial" class="form-text text-muted d-none">
                            Este tracto tiene despachos u otro historial: la placa no se puede cambiar.
                            Si la placa est&#xE1; mal, registre el tracto correcto y desactive este.
                        </small>
                    </div>
                    <div class="form-group">
                        <label for="<%= txtEditarMarca.ClientID %>" class="sgv-etiqueta">Marca</label>
                        <asp:TextBox ID="txtEditarMarca" runat="server" CssClass="form-control text-uppercase" MaxLength="30"></asp:TextBox>
                    </div>
                    <div class="form-group mb-0">
                        <label for="<%= txtEditarModelo.ClientID %>" class="sgv-etiqueta">Modelo</label>
                        <asp:TextBox ID="txtEditarModelo" runat="server" CssClass="form-control text-uppercase" MaxLength="30"></asp:TextBox>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnActualizarTracto" runat="server" ValidationGroup="vgEditarTracto" CssClass="btn btn-info"
                        Text="Guardar cambios" OnClick="btnActualizarTracto_Click" />
                </div>
            </div>
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hfIdTracto: '<%= hfIdTracto.ClientID %>',
            hfArgumentoFila: '<%= hfArgumentoFila.ClientID %>',
            btnAccionFila: '<%= btnAccionFila.ClientID %>',
            txtEditarPlaca: '<%= txtEditarPlaca.ClientID %>',
            txtEditarMarca: '<%= txtEditarMarca.ClientID %>',
            txtEditarModelo: '<%= txtEditarModelo.ClientID %>',
            pnlMensaje: '<%= pnlMensaje.ClientID %>',
            pnlErrorModal: '<%= pnlErrorModal.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistroTractos.js") %>"></script>

</asp:Content>

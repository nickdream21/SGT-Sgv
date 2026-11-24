<%@ Page Title="Peajes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroPeajes.aspx.cs" Inherits="WebSGV.Views.RegistroPeajes" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdEstacion" runat="server" />
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
            <h1 class="h2">Peajes <asp:Label ID="lblTotalPeajes" runat="server" CssClass="sgv-encabezado-contador" Text="0 registro(s)" /></h1>
            <p>Administra las estaciones de peaje disponibles para las liquidaciones</p>
        </div>
        <div class="sgv-encabezado-acciones">
            <a class="btn btn-primary btn-sm d-md-none" href="#altaCatalogo">Agregar estaci&#xF3;n</a>
            <button type="button" class="sgv-ayuda" data-toggle="collapse" data-target="#ayudaPagina"
                aria-controls="ayudaPagina" aria-expanded="false" aria-label="Ayuda de esta página">?</button>
        </div>
    </div>
    <div id="ayudaPagina" class="collapse">
        <p class="sgv-ayuda-texto">Las estaciones activas aparecen en el selector de peajes al registrar liquidaciones.
            Las inactivas quedan ocultas, pero sus datos hist&#xF3;ricos se conservan.</p>
    </div>

    <div class="row">

        <div class="col-12 col-md-4 mb-4" id="altaCatalogo">
            <section class="sgv-panel">
                <div class="sgv-panel-cab">
                    <h2 class="sgv-panel-titulo">Agregar estaci&#xF3;n</h2>
                </div>
                <div class="sgv-panel-cuerpo">
                    <div class="form-group">
                        <label for="<%= txtNombre.ClientID %>" class="sgv-etiqueta">Nombre de la estaci&#xF3;n <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                        <asp:TextBox ID="txtNombre" runat="server" aria-required="true" CssClass="form-control"
                            placeholder="Ej: ESTACION DE PEAJE CHICAMA" MaxLength="100"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvNombrePeaje" runat="server" ValidationGroup="vgRegistroPeaje"
                            ControlToValidate="txtNombre" ErrorMessage="Nombre de estación: este campo es obligatorio." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                        <asp:RegularExpressionValidator ID="revNombrePeaje" runat="server" ValidationGroup="vgRegistroPeaje"
                            ControlToValidate="txtNombre" ValidationExpression="^[A-Za-zÁÉÍÓÚÑáéíóú0-9\s\-\.,]{3,100}$" ErrorMessage="Nombre de estación: use entre 3 y 100 caracteres válidos." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                        <small class="form-text text-muted">El nombre se guardar&#xE1; en may&#xFA;sculas.</small>
                    </div>
                    <asp:Button ID="btnRegistrar" runat="server" Text="Registrar peaje" ValidationGroup="vgRegistroPeaje"
                        CssClass="btn btn-primary btn-block" OnClick="btnRegistrar_Click" />
                </div>
            </section>
        </div>

        <div class="col-12 col-md-8 mb-4 order-first order-md-0">
            <section class="sgv-panel">
                <div class="sgv-panel-cab">
                    <div>
                        <h2 class="sgv-panel-titulo">Estaciones registradas</h2>
                        <p class="sgv-panel-nota mb-0" aria-live="polite"><asp:Literal ID="litResumen" runat="server" Mode="Encode" EnableViewState="false" /></p>
                    </div>
                    <div class="sgv-buscador" role="search" data-enter="<%= btnBuscar.ClientID %>">
                        <asp:TextBox ID="txtBuscar" runat="server" CssClass="form-control form-control-sm" MaxLength="50"
                            placeholder="Nombre de la estación" aria-label="Buscar estaciones de peaje por nombre"
                            autocomplete="off" spellcheck="false" autocapitalize="off" autocorrect="off" />
                        <asp:Button ID="btnBuscar" runat="server" Text="Buscar" CssClass="btn btn-outline-secondary btn-sm"
                            CausesValidation="false" OnClick="btnBuscar_Click" />
                    </div>
                </div>
                <div class="table-responsive">
                    <asp:GridView ID="gvPeajes" runat="server"
                        CssClass="table sgv-tabla mb-0"
                        AutoGenerateColumns="false"
                        EnableViewState="false"
                        AllowPaging="true" PageSize="25"
                        OnPageIndexChanging="gvPeajes_PageIndexChanging"
                        GridLines="None" CellSpacing="-1"
                        EmptyDataText="No hay estaciones de peaje registradas.">
                        <PagerSettings Mode="NumericFirstLast" PageButtonCount="5" FirstPageText="Primera" LastPageText="Última" />
                        <PagerStyle CssClass="sgv-paginador" />
                        <EmptyDataRowStyle CssClass="sgv-vacio" />
                        <Columns>
                            <asp:BoundField DataField="idEstacion" HeaderText="ID"
                                HeaderStyle-CssClass="sgv-col-id d-none d-md-table-cell" ItemStyle-CssClass="sgv-col-id d-none d-md-table-cell sgv-suave" />
                            <%-- Encabezado corto en el celular (< 576 px): el largo ensanchaba la columna y la tabla desbordaba. --%>
                            <asp:TemplateField>
                                <HeaderTemplate><span class="d-none d-sm-inline">Nombre de la estaci&#xF3;n</span><span class="d-sm-none">Estaci&#xF3;n</span></HeaderTemplate>
                                <ItemTemplate><span class="font-weight-bold"><%#: Eval("nombre") %></span></ItemTemplate>
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
                                            aria-label='Editar estación <%#: Eval("nombre") %>'
                                            data-id='<%#: Eval("idEstacion") %>'
                                            data-nombre='<%# AttrEncode(Eval("nombre")) %>'>
                                            <i class="fas fa-edit" aria-hidden="true"></i>
                                        </button>
                                        <button type="button" class='<%#: ObtenerClaseBoton(Eval("activo")) %>'
                                            data-accion="enviarFila"
                                            data-argumento='<%#: ArgumentoEstado(Eval("idEstacion"), Eval("activo")) %>'
                                            data-confirmar='<%#: PreguntaEstado(Eval("activo"), Eval("nombre")) %>'><%#: ObtenerTextoBoton(Eval("activo")) %></button>
                                    </div>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </section>
        </div>

    </div>

    <!-- Modal Editar Peaje -->
    <div class="modal fade" id="modalEditar" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalEditarTitulo">
        <div class="modal-dialog" role="document">
            <div class="modal-content" data-enter="<%= btnActualizarPeaje.ClientID %>">
                <div class="modal-header">
                    <h5 class="modal-title" id="modalEditarTitulo">Editar estaci&#xF3;n de peaje</h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">&times;</button>
                </div>
                <div class="modal-body">
                    <asp:Panel ID="pnlErrorModal" runat="server" Visible="false" EnableViewState="false" CssClass="alert alert-danger" role="alert">
                        <asp:Label ID="lblErrorModal" runat="server" EnableViewState="false"></asp:Label>
                    </asp:Panel>
                    <div class="form-group mb-0">
                        <label for="<%= txtEditarNombre.ClientID %>" class="sgv-etiqueta">Nombre de la estaci&#xF3;n <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                        <asp:TextBox ID="txtEditarNombre" runat="server" aria-required="true" CssClass="form-control text-uppercase" MaxLength="100"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvEditarNombre" runat="server" ControlToValidate="txtEditarNombre" ValidationGroup="vgEditarPeaje" ErrorMessage="Nombre de estación: este campo es obligatorio." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnActualizarPeaje" runat="server" ValidationGroup="vgEditarPeaje" CssClass="btn btn-info"
                        Text="Guardar cambios" OnClick="btnActualizarPeaje_Click" />
                </div>
            </div>
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hfIdEstacion: '<%= hfIdEstacion.ClientID %>',
            hfArgumentoFila: '<%= hfArgumentoFila.ClientID %>',
            btnAccionFila: '<%= btnAccionFila.ClientID %>',
            txtEditarNombre: '<%= txtEditarNombre.ClientID %>',
            pnlMensaje: '<%= pnlMensaje.ClientID %>',
            pnlErrorModal: '<%= pnlErrorModal.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistroPeajes.js") %>"></script>

</asp:Content>

<%@ Page Title="Plantas" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroPlantas.aspx.cs" Inherits="WebSGV.Views.RegistroPlantas" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdPlanta" runat="server" />
    <%-- Envío único de "Registrar planta" (E5-3): un segundo POST del mismo formulario no registra otra vez --%>
    <asp:HiddenField ID="hfEnvioUnico" runat="server" />
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
            <h1 class="h2">Plantas <asp:Label ID="lblTotalPlantas" runat="server" CssClass="sgv-encabezado-contador" Text="0 registro(s)" /></h1>
            <p>Administra las plantas de operaci&#xF3;n disponibles para los despachos</p>
        </div>
        <div class="sgv-encabezado-acciones">
            <a class="btn btn-primary btn-sm d-md-none" href="#altaCatalogo">Agregar planta</a>
            <button type="button" class="sgv-ayuda" data-toggle="collapse" data-target="#ayudaPagina"
                aria-controls="ayudaPagina" aria-expanded="false" aria-label="Ayuda de esta página">?</button>
        </div>
    </div>
    <div id="ayudaPagina" class="collapse">
        <p class="sgv-ayuda-texto">Las plantas activas aparecen en el selector de planta de operaci&#xF3;n al registrar despachos.
            Las inactivas quedan ocultas, pero sus datos hist&#xF3;ricos se conservan.</p>
    </div>

    <div class="row">

        <div class="col-12 col-md-4 mb-4" id="altaCatalogo">
            <section class="sgv-panel">
                <div class="sgv-panel-cab">
                    <h2 class="sgv-panel-titulo">Agregar planta</h2>
                </div>
                <div class="sgv-panel-cuerpo">
                    <asp:ValidationSummary ID="vsRegistroPlanta" runat="server" ValidationGroup="vgRegistroPlanta" CssClass="text-danger small mb-3" DisplayMode="BulletList" />
                    <div class="form-group">
                        <label for="<%= txtNombre.ClientID %>" class="sgv-etiqueta">Nombre de la planta <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control"
                            placeholder="Ej: PLANTA TRUJILLO" MaxLength="200"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvNombre" runat="server" ValidationGroup="vgRegistroPlanta"
                            ControlToValidate="txtNombre"
                            ErrorMessage="El nombre de la planta es requerido."
                            CssClass="text-danger small" Display="Dynamic" />
                        <asp:RegularExpressionValidator ID="revNombrePlanta" runat="server" ValidationGroup="vgRegistroPlanta"
                            ControlToValidate="txtNombre" ValidationExpression="^[A-Za-zÁÉÍÓÚÑáéíóú0-9\s\-\.,]{3,200}$" ErrorMessage="Nombre de planta: use entre 3 y 200 caracteres válidos." CssClass="text-danger small" Display="Dynamic" />
                        <small class="form-text text-muted">El nombre se guardar&#xE1; en may&#xFA;sculas.</small>
                    </div>
                    <div class="form-group">
                        <label for="<%= ddlAmbito.ClientID %>" class="sgv-etiqueta">&#xC1;mbito <span class="text-danger">*</span></label>
                        <asp:DropDownList ID="ddlAmbito" runat="server" CssClass="form-control">
                            <asp:ListItem Value="0" Text="Nacional (Per&#xFA;)"></asp:ListItem>
                            <asp:ListItem Value="1" Text="Internacional"></asp:ListItem>
                        </asp:DropDownList>
                        <small class="form-text text-muted">
                            Define si la planta se muestra en operaciones nacionales o internacionales.
                        </small>
                    </div>
                    <asp:Button ID="btnRegistrar" runat="server" Text="Registrar planta" ValidationGroup="vgRegistroPlanta"
                        CssClass="btn btn-primary btn-block" OnClick="btnRegistrar_Click" />
                </div>
            </section>
        </div>

        <div class="col-12 col-md-8 mb-4 order-first order-md-0">
            <section class="sgv-panel">
                <div class="sgv-panel-cab">
                    <div>
                        <h2 class="sgv-panel-titulo">Plantas registradas</h2>
                        <p class="sgv-panel-nota mb-0" aria-live="polite"><asp:Literal ID="litResumen" runat="server" Mode="Encode" EnableViewState="false" /></p>
                    </div>
                    <div class="sgv-buscador" role="search" data-enter="<%= btnBuscar.ClientID %>">
                        <asp:TextBox ID="txtBuscar" runat="server" CssClass="form-control form-control-sm" MaxLength="50"
                            placeholder="Nombre de la planta" aria-label="Buscar plantas por nombre"
                            autocomplete="off" spellcheck="false" autocapitalize="off" autocorrect="off" />
                        <asp:Button ID="btnBuscar" runat="server" Text="Buscar" CssClass="btn btn-outline-secondary btn-sm"
                            CausesValidation="false" OnClick="btnBuscar_Click" />
                    </div>
                </div>
                <div class="table-responsive">
                    <asp:GridView ID="gvPlantas" runat="server"
                        CssClass="table sgv-tabla mb-0"
                        AutoGenerateColumns="false"
                        EnableViewState="false"
                        AllowPaging="true" PageSize="25"
                        OnPageIndexChanging="gvPlantas_PageIndexChanging"
                        GridLines="None" CellSpacing="-1"
                        EmptyDataText="No hay plantas registradas.">
                        <PagerSettings Mode="NumericFirstLast" PageButtonCount="5" FirstPageText="Primera" LastPageText="Última" />
                        <PagerStyle CssClass="sgv-paginador" />
                        <EmptyDataRowStyle CssClass="sgv-vacio" />
                        <Columns>
                            <asp:BoundField DataField="idPlanta" HeaderText="ID"
                                HeaderStyle-CssClass="sgv-col-id d-none d-md-table-cell" ItemStyle-CssClass="sgv-col-id d-none d-md-table-cell sgv-suave" />
                            <asp:TemplateField HeaderText="Nombre de la planta">
                                <ItemTemplate>
                                    <span class="font-weight-bold"><%#: Eval("nombre") %></span>
                                    <div class="small text-muted d-sm-none"><%#: ObtenerTextoAmbito(Eval("esInternacional")) %></div>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="&#xC1;mbito" HeaderStyle-CssClass="sgv-col-estado d-none d-sm-table-cell" ItemStyle-CssClass="sgv-col-estado d-none d-sm-table-cell">
                                <ItemTemplate>
                                    <span class='badge <%#: ObtenerClaseAmbito(Eval("esInternacional")) %>'><%#: ObtenerTextoAmbito(Eval("esInternacional")) %></span>
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
                                            aria-label='Editar planta <%#: Eval("nombre") %>'
                                            data-id='<%#: Eval("idPlanta") %>'
                                            data-nombre='<%# AttrEncode(Eval("nombre")) %>'
                                            data-internacional='<%#: Eval("esInternacional") %>'>
                                            <i class="fas fa-edit" aria-hidden="true"></i>
                                        </button>
                                        <button type="button" class='<%#: ObtenerClaseBoton(Eval("activo")) %>'
                                            data-accion="enviarFila"
                                            data-argumento='<%#: ArgumentoEstado(Eval("idPlanta"), Eval("activo")) %>'
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

    <!-- Modal Editar Planta -->
    <div class="modal fade" id="modalEditar" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalEditarTitulo">
        <div class="modal-dialog" role="document">
            <div class="modal-content" data-enter="<%= btnActualizarPlanta.ClientID %>">
                <div class="modal-header">
                    <h5 class="modal-title" id="modalEditarTitulo">Editar planta</h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">&times;</button>
                </div>
                <div class="modal-body">
                    <asp:Panel ID="pnlErrorModal" runat="server" Visible="false" EnableViewState="false" CssClass="alert alert-danger" role="alert">
                        <asp:Label ID="lblErrorModal" runat="server" EnableViewState="false"></asp:Label>
                    </asp:Panel>
                    <div class="form-group">
                        <label for="<%= txtEditarNombre.ClientID %>" class="sgv-etiqueta">Nombre de la planta <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtEditarNombre" runat="server" CssClass="form-control text-uppercase" MaxLength="200"></asp:TextBox>
                    </div>
                    <div class="form-group mb-0">
                        <label for="<%= ddlEditarAmbito.ClientID %>" class="sgv-etiqueta">&#xC1;mbito <span class="text-danger">*</span></label>
                        <asp:DropDownList ID="ddlEditarAmbito" runat="server" CssClass="form-control">
                            <asp:ListItem Value="0" Text="Nacional (Per&#xFA;)"></asp:ListItem>
                            <asp:ListItem Value="1" Text="Internacional"></asp:ListItem>
                        </asp:DropDownList>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnActualizarPlanta" runat="server" CssClass="btn btn-info"
                        Text="Guardar cambios" OnClick="btnActualizarPlanta_Click" />
                </div>
            </div>
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hfIdPlanta: '<%= hfIdPlanta.ClientID %>',
            hfArgumentoFila: '<%= hfArgumentoFila.ClientID %>',
            btnAccionFila: '<%= btnAccionFila.ClientID %>',
            txtEditarNombre: '<%= txtEditarNombre.ClientID %>',
            ddlEditarAmbito: '<%= ddlEditarAmbito.ClientID %>',
            pnlMensaje: '<%= pnlMensaje.ClientID %>',
            pnlErrorModal: '<%= pnlErrorModal.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistroPlantas.js") %>"></script>

</asp:Content>

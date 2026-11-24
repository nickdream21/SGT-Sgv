<%@ Page Title="Clientes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroClientes.aspx.cs" Inherits="WebSGV.Views.RegistroClientes" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdCliente" runat="server" />
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
            <h1 class="h2">Clientes <asp:Label ID="lblTotalClientes" runat="server" CssClass="sgv-encabezado-contador" Text="0 registro(s)" /></h1>
            <p>Administra los clientes disponibles para las operaciones</p>
        </div>
        <div class="sgv-encabezado-acciones">
            <a class="btn btn-primary btn-sm d-md-none" href="#altaCatalogo">Agregar cliente</a>
            <button type="button" class="sgv-ayuda" data-toggle="collapse" data-target="#ayudaPagina"
                aria-controls="ayudaPagina" aria-expanded="false" aria-label="Ayuda de esta página">?</button>
        </div>
    </div>
    <div id="ayudaPagina" class="collapse">
        <p class="sgv-ayuda-texto">Los clientes activos aparecen en los formularios de operaci&#xF3;n.
            Los inactivos quedan ocultos, pero sus datos hist&#xF3;ricos se conservan.</p>
    </div>

    <div class="row">

        <div class="col-12 col-md-4 mb-4" id="altaCatalogo">
            <section class="sgv-panel">
                <div class="sgv-panel-cab">
                    <h2 class="sgv-panel-titulo">Agregar cliente</h2>
                </div>
                <div class="sgv-panel-cuerpo">
                    <asp:Panel ID="pnlFormulario" runat="server">
                        <div class="form-group">
                            <label for="<%= txtRUC.ClientID %>" class="sgv-etiqueta">RUC (Opcional):</label>
                            <div class="input-group">
                                <asp:TextBox ID="txtRUC" runat="server" CssClass="form-control" inputmode="numeric" MaxLength="11"></asp:TextBox>
                                <div class="input-group-append">
                                    <asp:Button ID="btnBuscarRUC" runat="server" CausesValidation="false" CssClass="btn btn-secondary" Text="Verificar" data-accion="verificarRUC" />
                                </div>
                            </div>
                            <asp:RegularExpressionValidator ID="revRuc" runat="server" ControlToValidate="txtRUC" ValidationGroup="vgRegistroCliente"
                                ValidationExpression="^$|^\d{11}$" ErrorMessage="RUC: ingrese exactamente 11 dígitos o deje el campo vacío." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                            <small class="form-text text-muted">Ingrese el RUC si lo conoce, o deje en blanco.</small>
                        </div>
                        <div class="form-group">
                            <label for="<%= txtNombre.ClientID %>" class="sgv-etiqueta">Nombre / Raz&#xF3;n Social: <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                            <asp:TextBox ID="txtNombre" runat="server" aria-required="true" CssClass="form-control" MaxLength="100"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvNombre" runat="server" ValidationGroup="vgRegistroCliente"
                                ControlToValidate="txtNombre"
                                ErrorMessage="El nombre del cliente es obligatorio."
                                CssClass="sgv-campo-error"
                                Display="Dynamic" SetFocusOnError="true" />
                            <asp:RegularExpressionValidator ID="revNombre" runat="server" ControlToValidate="txtNombre" ValidationGroup="vgRegistroCliente"
                                ValidationExpression="^[A-Za-zÁÉÍÓÚÑáéíóú0-9\s\-\.&]{3,100}$" ErrorMessage="Nombre/Razón Social: use entre 3 y 100 caracteres válidos." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                        </div>

                        <hr class="my-3" />
                        <p class="sgv-etiqueta text-uppercase mb-2">Datos comerciales</p>

                        <div class="form-group">
                            <label for="<%= txtDireccion.ClientID %>" class="sgv-etiqueta">Direcci&#xF3;n fiscal:</label>
                            <asp:TextBox ID="txtDireccion" runat="server" CssClass="form-control" MaxLength="250"></asp:TextBox>
                        </div>
                        <div class="form-group">
                            <label for="<%= txtContacto.ClientID %>" class="sgv-etiqueta">Persona de contacto:</label>
                            <asp:TextBox ID="txtContacto" runat="server" CssClass="form-control" MaxLength="150"></asp:TextBox>
                        </div>
                        <div class="form-row">
                            <div class="form-group col-6">
                                <label for="<%= txtTelefono.ClientID %>" class="sgv-etiqueta">Tel&#xE9;fono:</label>
                                <asp:TextBox ID="txtTelefono" runat="server" CssClass="form-control" inputmode="tel" MaxLength="50"></asp:TextBox>
                            </div>
                            <div class="form-group col-6">
                                <label for="<%= ddlMoneda.ClientID %>" class="sgv-etiqueta">Moneda:</label>
                                <asp:DropDownList ID="ddlMoneda" runat="server" CssClass="form-control">
                                    <asp:ListItem Value="PEN" Text="PEN - Soles" Selected="True"></asp:ListItem>
                                    <asp:ListItem Value="USD" Text="USD - D&#xF3;lares"></asp:ListItem>
                                </asp:DropDownList>
                            </div>
                        </div>
                        <div class="form-group">
                            <label for="<%= txtCorreo.ClientID %>" class="sgv-etiqueta">Correo:</label>
                            <asp:TextBox ID="txtCorreo" runat="server" CssClass="form-control" MaxLength="150" TextMode="Email"></asp:TextBox>
                            <asp:RegularExpressionValidator ID="revCorreo" runat="server" ControlToValidate="txtCorreo" ValidationGroup="vgRegistroCliente"
                                ValidationExpression="^$|^[^@\s]+@[^@\s]+\.[^@\s]+$" ErrorMessage="Correo: ingrese una direcci&#xF3;n v&#xE1;lida o deje el campo vac&#xED;o." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                        </div>
                        <div class="form-group">
                            <div class="custom-control custom-checkbox">
                                <asp:CheckBox ID="chkEsExportador" runat="server" CssClass="mr-2" Text="Opera carga internacional" />
                            </div>
                            <small class="form-text text-muted">M&#xE1;rquelo si el cliente exporta: habilita su seguimiento de exportaci&#xF3;n.</small>
                        </div>
                        <div class="form-group">
                            <label for="<%= txtObservaciones.ClientID %>" class="sgv-etiqueta">Observaciones:</label>
                            <asp:TextBox ID="txtObservaciones" runat="server" CssClass="form-control" MaxLength="500"
                                TextMode="MultiLine" Rows="2"></asp:TextBox>
                        </div>

                        <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-primary btn-block"
                            Text="Registrar cliente" ValidationGroup="vgRegistroCliente" OnClick="btnRegistrar_Click" />
                    </asp:Panel>
                </div>
            </section>
        </div>

        <div class="col-12 col-md-8 mb-4 order-first order-md-0">
            <section class="sgv-panel">
                <div class="sgv-panel-cab">
                    <div>
                        <h2 class="sgv-panel-titulo">Clientes registrados</h2>
                        <p class="sgv-panel-nota mb-0" aria-live="polite"><asp:Literal ID="litResumen" runat="server" Mode="Encode" EnableViewState="false" /></p>
                    </div>
                    <div class="sgv-buscador" role="search" data-enter="<%= btnBuscar.ClientID %>">
                        <asp:TextBox ID="txtBuscar" runat="server" CssClass="form-control form-control-sm" MaxLength="50"
                            placeholder="RUC, nombre o contacto" aria-label="Buscar clientes por RUC, nombre o contacto"
                            autocomplete="off" spellcheck="false" autocapitalize="off" autocorrect="off" />
                        <asp:Button ID="btnBuscar" runat="server" Text="Buscar" CssClass="btn btn-outline-secondary btn-sm"
                            CausesValidation="false" OnClick="btnBuscar_Click" />
                    </div>
                </div>
                <div class="table-responsive">
                    <asp:GridView ID="gvClientes" runat="server"
                        CssClass="table sgv-tabla mb-0"
                        AutoGenerateColumns="false"
                        EnableViewState="false"
                        AllowPaging="true" PageSize="25"
                        OnPageIndexChanging="gvClientes_PageIndexChanging"
                        GridLines="None" CellSpacing="-1"
                        EmptyDataText="No hay clientes registrados.">
                        <PagerSettings Mode="NumericFirstLast" PageButtonCount="5" FirstPageText="Primera" LastPageText="Última" />
                        <PagerStyle CssClass="sgv-paginador" />
                        <EmptyDataRowStyle CssClass="sgv-vacio" />
                        <Columns>
                            <asp:BoundField DataField="idCliente" HeaderText="ID"
                                HeaderStyle-CssClass="sgv-col-id d-none d-md-table-cell" ItemStyle-CssClass="sgv-col-id d-none d-md-table-cell sgv-suave" />
                            <asp:BoundField DataField="ruc" HeaderText="RUC" HeaderStyle-CssClass="d-none d-sm-table-cell" ItemStyle-CssClass="d-none d-sm-table-cell text-nowrap" />
                            <%-- Encabezado corto en el celular (< 576 px): el largo ensanchaba la columna y la tabla desbordaba. --%>
                            <asp:TemplateField>
                                <HeaderTemplate><span class="d-none d-sm-inline">Nombre / raz&#xF3;n social</span><span class="d-sm-none">Cliente</span></HeaderTemplate>
                                <ItemTemplate>
                                    <span class="font-weight-bold"><%#: Eval("nombre") %></span>
                                    <div class="small text-muted d-sm-none"><%#: Eval("ruc") %></div>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Contacto" HeaderStyle-CssClass="d-none d-lg-table-cell" ItemStyle-CssClass="d-none d-lg-table-cell">
                                <ItemTemplate>
                                    <div><%#: Eval("contacto") %></div>
                                    <div class="small text-muted"><%#: Eval("telefono") %></div>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Operaci&#xF3;n" HeaderStyle-CssClass="sgv-col-estado d-none d-sm-table-cell" ItemStyle-CssClass="sgv-col-estado d-none d-sm-table-cell">
                                <ItemTemplate>
                                    <span class='badge <%#: Convert.ToBoolean(Eval("esExportador")) ? "badge-info" : "badge-secondary" %>'><%#: Convert.ToBoolean(Eval("esExportador")) ? "Exportador" : "Nacional" %></span>
                                    <div class="small text-muted"><%#: Eval("monedaFacturacion") %></div>
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
                                            aria-label='Editar cliente <%#: Eval("nombre") %>'
                                            data-id='<%#: Eval("idCliente") %>'
                                            data-ruc='<%# AttrEncode(Eval("ruc")) %>'
                                            data-nombre='<%# AttrEncode(Eval("nombre")) %>'
                                            data-direccion='<%# AttrEncode(Eval("direccion")) %>'
                                            data-contacto='<%# AttrEncode(Eval("contacto")) %>'
                                            data-telefono='<%# AttrEncode(Eval("telefono")) %>'
                                            data-correo='<%# AttrEncode(Eval("correo")) %>'
                                            data-moneda='<%# AttrEncode(Eval("monedaFacturacion")) %>'
                                            data-exportador='<%#: Convert.ToBoolean(Eval("esExportador")) ? "1" : "0" %>'
                                            data-observaciones='<%# AttrEncode(Eval("observaciones")) %>'>
                                            <i class="fas fa-edit" aria-hidden="true"></i>
                                        </button>
                                        <button type="button" class='<%#: ObtenerClaseBoton(Eval("activo")) %>'
                                            data-accion="enviarFila"
                                            data-argumento='<%#: ArgumentoEstado(Eval("idCliente"), Eval("activo")) %>'
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

    <!-- Modal Editar Cliente -->
    <div class="modal fade" id="modalEditar" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalEditarTitulo">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content" data-enter="<%= btnActualizarCliente.ClientID %>">
                <div class="modal-header">
                    <h5 class="modal-title" id="modalEditarTitulo">Editar cliente</h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">&times;</button>
                </div>
                <div class="modal-body">
                    <asp:Panel ID="pnlErrorModal" runat="server" Visible="false" EnableViewState="false" CssClass="alert alert-danger" role="alert">
                        <asp:Label ID="lblErrorModal" runat="server" EnableViewState="false"></asp:Label>
                    </asp:Panel>
                    <div class="form-row">
                        <div class="form-group col-md-4">
                            <label for="<%= txtEditarRUC.ClientID %>" class="sgv-etiqueta">RUC (Opcional):</label>
                            <asp:TextBox ID="txtEditarRUC" runat="server" CssClass="form-control" inputmode="numeric" MaxLength="11"></asp:TextBox>
                            <small class="form-text text-muted">11 d&#xED;gitos. Dejar en blanco si no aplica.</small>
                        </div>
                        <div class="form-group col-md-8">
                            <label for="<%= txtEditarNombre.ClientID %>" class="sgv-etiqueta">Nombre / Raz&#xF3;n Social <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                            <asp:TextBox ID="txtEditarNombre" runat="server" aria-required="true" CssClass="form-control" MaxLength="100"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvEditarNombre" runat="server" ControlToValidate="txtEditarNombre" ValidationGroup="vgEditarCliente" ErrorMessage="El nombre del cliente es obligatorio." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                        </div>
                    </div>
                    <div class="form-group">
                        <label for="<%= txtEditarDireccion.ClientID %>" class="sgv-etiqueta">Direcci&#xF3;n fiscal:</label>
                        <asp:TextBox ID="txtEditarDireccion" runat="server" CssClass="form-control" MaxLength="250"></asp:TextBox>
                    </div>
                    <div class="form-row">
                        <div class="form-group col-md-5">
                            <label for="<%= txtEditarContacto.ClientID %>" class="sgv-etiqueta">Persona de contacto:</label>
                            <asp:TextBox ID="txtEditarContacto" runat="server" CssClass="form-control" MaxLength="150"></asp:TextBox>
                        </div>
                        <div class="form-group col-md-3">
                            <label for="<%= txtEditarTelefono.ClientID %>" class="sgv-etiqueta">Tel&#xE9;fono:</label>
                            <asp:TextBox ID="txtEditarTelefono" runat="server" CssClass="form-control" inputmode="tel" MaxLength="50"></asp:TextBox>
                        </div>
                        <div class="form-group col-md-4">
                            <label for="<%= txtEditarCorreo.ClientID %>" class="sgv-etiqueta">Correo:</label>
                            <asp:TextBox ID="txtEditarCorreo" runat="server" CssClass="form-control" MaxLength="150" TextMode="Email"></asp:TextBox>
                        </div>
                    </div>
                    <div class="form-row align-items-center">
                        <div class="form-group col-md-4">
                            <label for="<%= ddlEditarMoneda.ClientID %>" class="sgv-etiqueta">Moneda de facturaci&#xF3;n:</label>
                            <asp:DropDownList ID="ddlEditarMoneda" runat="server" CssClass="form-control">
                                <asp:ListItem Value="PEN" Text="PEN - Soles"></asp:ListItem>
                                <asp:ListItem Value="USD" Text="USD - D&#xF3;lares"></asp:ListItem>
                            </asp:DropDownList>
                        </div>
                        <div class="form-group col-md-8 mb-0 pt-3">
                            <asp:CheckBox ID="chkEditarEsExportador" runat="server" CssClass="mr-2" Text="Opera carga internacional" />
                            <small class="form-text text-muted">Habilita el seguimiento de exportaci&#xF3;n para este cliente.</small>
                        </div>
                    </div>
                    <div class="form-group mb-0">
                        <label for="<%= txtEditarObservaciones.ClientID %>" class="sgv-etiqueta">Observaciones:</label>
                        <asp:TextBox ID="txtEditarObservaciones" runat="server" CssClass="form-control" MaxLength="500"
                            TextMode="MultiLine" Rows="2"></asp:TextBox>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnActualizarCliente" runat="server" ValidationGroup="vgEditarCliente" CssClass="btn btn-info"
                        Text="Guardar cambios" OnClick="btnActualizarCliente_Click" />
                </div>
            </div>
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hfIdCliente: '<%= hfIdCliente.ClientID %>',
            hfArgumentoFila: '<%= hfArgumentoFila.ClientID %>',
            btnAccionFila: '<%= btnAccionFila.ClientID %>',
            txtEditarRUC: '<%= txtEditarRUC.ClientID %>',
            txtEditarNombre: '<%= txtEditarNombre.ClientID %>',
            txtEditarDireccion: '<%= txtEditarDireccion.ClientID %>',
            txtEditarContacto: '<%= txtEditarContacto.ClientID %>',
            txtEditarTelefono: '<%= txtEditarTelefono.ClientID %>',
            txtEditarCorreo: '<%= txtEditarCorreo.ClientID %>',
            txtEditarObservaciones: '<%= txtEditarObservaciones.ClientID %>',
            ddlEditarMoneda: '<%= ddlEditarMoneda.ClientID %>',
            chkEditarEsExportador: '<%= chkEditarEsExportador.ClientID %>',
            pnlMensaje: '<%= pnlMensaje.ClientID %>',
            pnlErrorModal: '<%= pnlErrorModal.ClientID %>',
            txtRUC: '<%= txtRUC.ClientID %>',
            btnBuscarRUC: '<%= btnBuscarRUC.ClientID %>',
            txtNombre: '<%= txtNombre.ClientID %>',
            txtDireccion: '<%= txtDireccion.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistroClientes.js") %>"></script>
</asp:Content>

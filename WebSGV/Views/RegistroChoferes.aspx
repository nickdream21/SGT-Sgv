<%@ Page Title="Conductores" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroChoferes.aspx.cs" Inherits="WebSGV.Views.RegistroChoferes" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdConductor" runat="server" />
    <%-- Activar/Desactivar de la fila (E5-16): la grilla no guarda ViewState; el botón de la fila copia su
         data-argumento aquí y pulsa btnAccionFila (SGV.acciones.enviarFila). El servidor lo revalida. --%>
    <asp:HiddenField ID="hfArgumentoFila" runat="server" />
    <%-- Texto de la última búsqueda (solo lo escribe Buscar): con él se filtra en todo postback. --%>
    <asp:HiddenField ID="hfFiltroAplicado" runat="server" />
    <asp:Button ID="btnAccionFila" runat="server" Text="Cambiar estado" CssClass="d-none" CausesValidation="false"
        OnClick="btnAccionFila_Click" tabindex="-1" aria-hidden="true" />

    <asp:Panel ID="pnlMensaje" runat="server" EnableViewState="false" Visible="false">
        <asp:Label ID="lblMensaje" runat="server" EnableViewState="false"></asp:Label>
    </asp:Panel>

    <div class="sgv-encabezado">
        <div class="sgv-encabezado-titulo">
            <h1 class="h2">Conductores <asp:Label ID="lblTotalConductores" runat="server" CssClass="sgv-encabezado-contador" Text="0 registro(s)" /></h1>
            <p>Administra los conductores disponibles para las operaciones</p>
        </div>
        <div class="sgv-encabezado-acciones">
            <a class="btn btn-primary btn-sm d-md-none" href="#altaCatalogo">Agregar conductor</a>
            <button type="button" class="sgv-ayuda" data-toggle="collapse" data-target="#ayudaPagina"
                aria-controls="ayudaPagina" aria-expanded="false" aria-label="Ayuda de esta página">?</button>
        </div>
    </div>
    <div id="ayudaPagina" class="collapse">
        <p class="sgv-ayuda-texto">Los conductores activos aparecen en los formularios de operaci&#xF3;n.
            Los inactivos quedan ocultos, pero sus datos hist&#xF3;ricos se conservan.</p>
    </div>

    <%-- Lista de choferes actuales: compara y propone; no guarda hasta "Aplicar". Plegada (E5-16) salvo con una
         comparación en curso o un error de la carga (CargaAbierta). --%>
    <details class="sgv-plegable"<%= CargaAbierta ? " open" : "" %>>
        <summary>Actualizar con la lista de choferes actuales</summary>
        <div class="sgv-plegable-cuerpo">
            <p class="text-muted small mb-3">
                Suba el Excel de la lista (columnas CHOFER, NACIONALIDAD, LICENCIA y DNI) o pegue las filas copiadas desde Excel.
                El sistema la compara con los conductores registrados y muestra qu&#xE9; cambiar&#xED;a: registrar a los que faltan,
                completar licencias y nombres, y desactivar a los que no est&#xE1;n en la lista. Nada se guarda hasta pulsar <strong>Aplicar</strong>.
            </p>
            <div class="row align-items-end">
                <div class="col-md-4 form-group">
                    <label for="<%= fuListaChoferes.ClientID %>" class="sgv-etiqueta">Excel (.xlsx)</label>
                    <asp:FileUpload ID="fuListaChoferes" runat="server" CssClass="form-control-file" accept=".xlsx" />
                </div>
                <div class="col-md-6 form-group">
                    <label for="<%= txtListaChoferes.ClientID %>" class="sgv-etiqueta">o filas pegadas</label>
                    <asp:TextBox ID="txtListaChoferes" runat="server" TextMode="MultiLine" Rows="3" CssClass="form-control form-control-sm"
                        placeholder="1&#9;ALFREDO ... &#9;PERUANA&#9;B-00000000&#9;00000000"></asp:TextBox>
                </div>
                <div class="col-md-2 form-group">
                    <asp:Button ID="btnCompararLista" runat="server" CausesValidation="false" CssClass="btn btn-primary btn-block" Text="Comparar" OnClick="btnCompararLista_Click" />
                </div>
            </div>

            <asp:Panel ID="pnlComparacionLista" runat="server" Visible="false">
                <asp:Literal ID="litComparacionLista" runat="server" />
                <div class="d-flex justify-content-end mt-3">
                    <asp:Button ID="btnCancelarLista" runat="server" CausesValidation="false" CssClass="btn btn-secondary mr-2" Text="Cancelar" OnClick="btnCancelarLista_Click" />
                    <asp:Button ID="btnAplicarLista" runat="server" CausesValidation="false" CssClass="btn btn-success" Text="Aplicar los cambios marcados"
                        OnClick="btnAplicarLista_Click" data-confirmar="¿Aplicar los cambios marcados a los conductores?" />
                </div>
            </asp:Panel>
        </div>
    </details>

    <div class="row">

        <div class="col-12 col-md-5 mb-4" id="altaCatalogo">
            <section class="sgv-panel">
                <div class="sgv-panel-cab">
                    <h2 class="sgv-panel-titulo">Agregar conductor</h2>
                </div>
                <div class="sgv-panel-cuerpo">
                    <asp:Panel ID="pnlFormulario" runat="server">

                        <%-- Fila 1: Tipo documento + campo de documento (en la misma posición). Cada mensaje va en la
                             columna de su campo; el validador del tipo oculto se apaga (mostrarCampoDocumento en el
                             navegador, AjustarValidadoresDocumento en el servidor) para que no bloquee Registrar. --%>
                        <div class="row">
                            <div class="col-12 col-xl-6 form-group">
                                <label for="<%= ddlTipoDocumento.ClientID %>" class="sgv-etiqueta">Tipo documento:</label>
                                <asp:DropDownList ID="ddlTipoDocumento" runat="server" CssClass="form-control" data-accion-change="mostrarCampoDocumento">
                                    <asp:ListItem Value="DNI" Text="DNI" />
                                    <asp:ListItem Value="Carnet de Extranjería" Text="Carnet de extranjería" />
                                </asp:DropDownList>
                            </div>
                            <div class="col-12 col-xl-6 form-group" id="grupoDNI">
                                <label for="<%= txtDNI.ClientID %>" class="sgv-etiqueta">DNI: <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                <div class="input-group" data-enter="<%= btnBuscarDNI.ClientID %>">
                                    <asp:TextBox ID="txtDNI" runat="server" aria-required="true" CssClass="form-control" inputmode="numeric" MaxLength="8" placeholder="8 dígitos"></asp:TextBox>
                                    <div class="input-group-append">
                                        <asp:Button ID="btnBuscarDNI" runat="server" CausesValidation="false" CssClass="btn btn-secondary" Text="Buscar" data-accion="buscarPorDNI" />
                                    </div>
                                </div>
                                <asp:RequiredFieldValidator ID="rfvDni" runat="server" ValidationGroup="vgRegistroConductor" ControlToValidate="txtDNI" ErrorMessage="DNI: este campo es obligatorio." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                                <asp:RegularExpressionValidator ID="revDni" runat="server" ValidationGroup="vgRegistroConductor" ControlToValidate="txtDNI" ValidationExpression="^$|^\d{8}$" ErrorMessage="DNI: debe tener exactamente 8 dígitos." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                            </div>
                            <div class="col-12 col-xl-6 form-group d-none" id="grupoCarnet">
                                <label for="<%= txtCarnetExtranjeria.ClientID %>" class="sgv-etiqueta">Carnet Extranjer&#xED;a: <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                <asp:TextBox ID="txtCarnetExtranjeria" runat="server" aria-required="true" CssClass="form-control" MaxLength="11" placeholder="De 6 a 11 letras o dígitos"></asp:TextBox>
                                <%-- M-079: la misma regla que DocumentoConductor.EsCarnetValido (6 a 11 letras, dígitos o guion,
                                     con al menos un dígito; espacios a los lados permitidos, como los quita el servidor).
                                     WebUIValidation.js no recorta el valor: exige que la coincidencia sea el valor entero,
                                     por eso el \s* de los bordes. El campo vacío no lo evalúa. --%>
                                <asp:RequiredFieldValidator ID="rfvCarnet" runat="server" ValidationGroup="vgRegistroConductor" ControlToValidate="txtCarnetExtranjeria" ErrorMessage="Carnet de extranjería: este campo es obligatorio." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                                <asp:RegularExpressionValidator ID="revCarnet" runat="server" ValidationGroup="vgRegistroConductor" ControlToValidate="txtCarnetExtranjeria" ValidationExpression="^\s*(?=[A-Za-z\-]*\d)[A-Za-z0-9\-]{6,11}\s*$" ErrorMessage="Carnet de extranjería: use de 6 a 11 letras, dígitos o guion, con al menos un dígito." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                            </div>
                        </div>

                        <%-- Fila 2: Nombres + Apellido Paterno --%>
                        <div class="row">
                            <div class="col-md-6 form-group">
                                <label for="<%= txtNombres.ClientID %>" class="sgv-etiqueta">Nombres: <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                <asp:TextBox ID="txtNombres" runat="server" aria-required="true" CssClass="form-control" MaxLength="50"></asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvNombres" runat="server" ControlToValidate="txtNombres" ValidationGroup="vgRegistroConductor"
                                    ErrorMessage="El nombre es requerido" CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                                <asp:RegularExpressionValidator ID="revNombres" runat="server" ValidationGroup="vgRegistroConductor" ControlToValidate="txtNombres" ValidationExpression="^[A-Za-zÁÉÍÓÚÑÜáéíóúñü\s]{2,50}$" ErrorMessage="Nombres: solo letras y espacios (2 a 50 caracteres)." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                            </div>
                            <div class="col-md-6 form-group">
                                <label for="<%= txtApellidoPaterno.ClientID %>" class="sgv-etiqueta">Apellido paterno: <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                <asp:TextBox ID="txtApellidoPaterno" runat="server" aria-required="true" CssClass="form-control" MaxLength="20"></asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvApellidoPaterno" runat="server" ControlToValidate="txtApellidoPaterno" ValidationGroup="vgRegistroConductor"
                                    ErrorMessage="El apellido paterno es requerido" CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                                <asp:RegularExpressionValidator ID="revApellidoPaterno" runat="server" ValidationGroup="vgRegistroConductor" ControlToValidate="txtApellidoPaterno" ValidationExpression="^[A-Za-zÁÉÍÓÚÑÜáéíóúñü\s]{2,20}$" ErrorMessage="Apellido paterno: solo letras y espacios (2 a 20 caracteres)." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                            </div>
                        </div>

                        <%-- Fila 3: Apellido Materno + Teléfono --%>
                        <div class="row">
                            <div class="col-md-6 form-group">
                                <label for="<%= txtApellidoMaterno.ClientID %>" class="sgv-etiqueta">Apellido materno:</label>
                                <asp:TextBox ID="txtApellidoMaterno" runat="server" CssClass="form-control" MaxLength="20"></asp:TextBox>
                                <asp:RegularExpressionValidator ID="revApellidoMaterno" runat="server" ValidationGroup="vgRegistroConductor" ControlToValidate="txtApellidoMaterno" ValidationExpression="^$|^[A-Za-zÁÉÍÓÚÑÜáéíóúñü\s]{2,20}$" ErrorMessage="Apellido materno: solo letras y espacios (2 a 20 caracteres)." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                            </div>
                            <div class="col-md-6 form-group">
                                <label for="<%= txtTelefono.ClientID %>" class="sgv-etiqueta">Tel&#xE9;fono:</label>
                                <asp:TextBox ID="txtTelefono" runat="server" CssClass="form-control" inputmode="tel" MaxLength="9"></asp:TextBox>
                                <asp:RegularExpressionValidator ID="revTelefono" runat="server" ValidationGroup="vgRegistroConductor" ControlToValidate="txtTelefono" ValidationExpression="^$|^\d{7,9}$" ErrorMessage="Teléfono: ingrese entre 7 y 9 dígitos numéricos." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                            </div>
                        </div>

                        <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-primary btn-block" Text="Registrar conductor" ValidationGroup="vgRegistroConductor" OnClick="btnRegistrar_Click" />
                    </asp:Panel>
                </div>
            </section>
        </div>

        <div class="col-12 col-md-7 mb-4 order-first order-md-0">
            <section class="sgv-panel">
                <div class="sgv-panel-cab">
                    <div>
                        <h2 class="sgv-panel-titulo">Conductores registrados</h2>
                        <p class="sgv-panel-nota mb-0" aria-live="polite"><asp:Literal ID="litResumen" runat="server" Mode="Encode" EnableViewState="false" /></p>
                    </div>
                    <div class="sgv-buscador" role="search" data-enter="<%= btnBuscar.ClientID %>">
                        <asp:TextBox ID="txtBuscar" runat="server" CssClass="form-control form-control-sm" MaxLength="50"
                            placeholder="Documento, nombre o teléfono" aria-label="Buscar conductores por documento, nombre o teléfono"
                            autocomplete="off" spellcheck="false" autocapitalize="off" autocorrect="off" />
                        <asp:Button ID="btnBuscar" runat="server" Text="Buscar" CssClass="btn btn-outline-secondary btn-sm"
                            CausesValidation="false" OnClick="btnBuscar_Click" />
                    </div>
                </div>
                <div class="table-responsive">
                    <asp:GridView ID="gvConductores" runat="server"
                        CssClass="table sgv-tabla mb-0"
                        AutoGenerateColumns="false"
                        EnableViewState="false"
                        AllowPaging="true" PageSize="25"
                        OnPageIndexChanging="gvConductores_PageIndexChanging"
                        GridLines="None" CellSpacing="-1"
                        EmptyDataText="No hay conductores registrados.">
                        <PagerSettings Mode="NumericFirstLast" PageButtonCount="5" FirstPageText="Primera" LastPageText="Última" />
                        <PagerStyle CssClass="sgv-paginador" />
                        <EmptyDataRowStyle CssClass="sgv-vacio" />
                        <Columns>
                            <%-- Plantillas de fila en pocas líneas y sin sangría: la sangría se repetía en las 25 filas. --%>
                            <asp:TemplateField HeaderText="Conductor">
                                <ItemTemplate><span class="font-weight-bold"><%#: Eval("nombreCompleto") %></span>
<div class="small text-muted d-sm-none"><%#: DocumentoYTelefono(Eval("documentoDisplay"), Eval("telefono")) %></div></ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="documentoDisplay" HeaderText="Documento" HeaderStyle-CssClass="d-none d-sm-table-cell" ItemStyle-CssClass="d-none d-sm-table-cell text-nowrap" />
                            <asp:BoundField DataField="telefono" HeaderText="Tel&#xE9;fono" HeaderStyle-CssClass="d-none d-md-table-cell" ItemStyle-CssClass="d-none d-md-table-cell text-nowrap" />
                            <asp:TemplateField HeaderText="Estado" HeaderStyle-CssClass="sgv-col-estado" ItemStyle-CssClass="sgv-col-estado">
                                <ItemTemplate><span class='badge <%#: ObtenerClaseEstado(Eval("activo")) %>'><%#: ObtenerTextoEstado(Eval("activo")) %></span></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Acciones" HeaderStyle-CssClass="sgv-col-acciones" ItemStyle-CssClass="sgv-col-acciones">
                                <ItemTemplate><div class="sgv-acciones">
<button type="button" class="sgv-btn-icono info btn-editar" title="Editar" aria-label='Editar conductor <%#: Eval("nombreCompleto") %>'
 data-id='<%#: Eval("idConductor") %>' data-nombres='<%# AttrEncode(Eval("nombre")) %>' data-ap-paterno='<%# AttrEncode(Eval("apPaterno")) %>'
 data-ap-materno='<%# AttrEncode(Eval("apMaterno")) %>' data-telefono='<%# AttrEncode(Eval("telefono")) %>'><i class="fas fa-edit" aria-hidden="true"></i></button>
<button type="button" class='<%#: ObtenerClaseBoton(Eval("activo")) %>' data-accion="enviarFila"
 data-argumento='<%#: ArgumentoEstado(Eval("idConductor"), Eval("activo")) %>'
 data-confirmar='<%#: PreguntaEstado(Eval("activo"), Eval("nombreCompleto")) %>'><%#: ObtenerTextoBoton(Eval("activo")) %></button>
</div></ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </section>
        </div>

    </div>

    <!-- Modal Editar Conductor -->
    <div class="modal fade" id="modalEditar" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalEditarTitulo">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content" data-enter="<%= btnActualizarConductor.ClientID %>">
                <div class="modal-header">
                    <h5 class="modal-title" id="modalEditarTitulo">Editar conductor</h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">&times;</button>
                </div>
                <div class="modal-body">
                    <asp:Panel ID="pnlErrorModal" runat="server" Visible="false" EnableViewState="false" CssClass="alert alert-danger" role="alert">
                        <asp:Label ID="lblErrorModal" runat="server" EnableViewState="false"></asp:Label>
                    </asp:Panel>
                    <div class="row">
                        <div class="col-md-6 form-group">
                            <label for="<%= txtEditarNombres.ClientID %>" class="sgv-etiqueta">Nombres <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                            <asp:TextBox ID="txtEditarNombres" runat="server" aria-required="true" CssClass="form-control" MaxLength="50"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvEditarNombres" runat="server" ControlToValidate="txtEditarNombres" ValidationGroup="vgEditarConductor" ErrorMessage="El nombre es requerido." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                        </div>
                        <div class="col-md-6 form-group">
                            <label for="<%= txtEditarApellidoPaterno.ClientID %>" class="sgv-etiqueta">Apellido paterno <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                            <asp:TextBox ID="txtEditarApellidoPaterno" runat="server" aria-required="true" CssClass="form-control" MaxLength="20"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvEditarApellidoPaterno" runat="server" ControlToValidate="txtEditarApellidoPaterno" ValidationGroup="vgEditarConductor" ErrorMessage="El apellido paterno es requerido." CssClass="sgv-campo-error" Display="Dynamic" SetFocusOnError="true" />
                        </div>
                    </div>
                    <div class="row">
                        <div class="col-md-6 form-group">
                            <label for="<%= txtEditarApellidoMaterno.ClientID %>" class="sgv-etiqueta">Apellido materno</label>
                            <asp:TextBox ID="txtEditarApellidoMaterno" runat="server" CssClass="form-control" MaxLength="20"></asp:TextBox>
                        </div>
                        <div class="col-md-6 form-group mb-0">
                            <label for="<%= txtEditarTelefono.ClientID %>" class="sgv-etiqueta">Tel&#xE9;fono</label>
                            <asp:TextBox ID="txtEditarTelefono" runat="server" CssClass="form-control" inputmode="tel" MaxLength="12"></asp:TextBox>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnActualizarConductor" runat="server" ValidationGroup="vgEditarConductor" CssClass="btn btn-info"
                        Text="Guardar cambios" OnClick="btnActualizarConductor_Click" />
                </div>
            </div>
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hfIdConductor: '<%= hfIdConductor.ClientID %>',
            hfArgumentoFila: '<%= hfArgumentoFila.ClientID %>',
            btnAccionFila: '<%= btnAccionFila.ClientID %>',
            txtEditarNombres: '<%= txtEditarNombres.ClientID %>',
            txtEditarApellidoPaterno: '<%= txtEditarApellidoPaterno.ClientID %>',
            txtEditarApellidoMaterno: '<%= txtEditarApellidoMaterno.ClientID %>',
            txtEditarTelefono: '<%= txtEditarTelefono.ClientID %>',
            pnlMensaje: '<%= pnlMensaje.ClientID %>',
            pnlErrorModal: '<%= pnlErrorModal.ClientID %>',
            txtDNI: '<%= txtDNI.ClientID %>',
            btnBuscarDNI: '<%= btnBuscarDNI.ClientID %>',
            txtNombres: '<%= txtNombres.ClientID %>',
            txtApellidoPaterno: '<%= txtApellidoPaterno.ClientID %>',
            txtApellidoMaterno: '<%= txtApellidoMaterno.ClientID %>',
            ddlTipoDocumento: '<%= ddlTipoDocumento.ClientID %>',
            rfvDni: '<%= rfvDni.ClientID %>',
            revDni: '<%= revDni.ClientID %>',
            rfvCarnet: '<%= rfvCarnet.ClientID %>',
            revCarnet: '<%= revCarnet.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistroChoferes.js") %>"></script>
</asp:Content>

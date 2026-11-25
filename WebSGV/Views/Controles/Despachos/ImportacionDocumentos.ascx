<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="ImportacionDocumentos.ascx.cs" Inherits="WebSGV.Views.Controles.Despachos.ImportacionDocumentos" %>
<%@ Import Namespace="WebSGV.Services.Despachos" %>
<%-- "Leer documentos" de RegistroDespacho con el ZIP o los PDF del correo (E6-25): la vista previa de los tractos
     importados, las altas que requieren aprobación, "Crear lote con estos datos", "Corregir datos a mano" (entrega una
     PropuestaConfiguracionBase, evento ConfiguracionPropuesta) y el recuadro "se agregarán al pulsar Iniciar lote". El
     FileUpload y "Leer documentos" siguen en la página (PostBackTrigger): la página le pasa los archivos (Leer). Vive
     dentro de UpdatePanelMain, que registra en código sus botones (DisparadoresAsincronos). Los estilos y el script son
     los de la página (RegistroDespacho.css/.js). --%>
<asp:Panel ID="pnlVistaPreviaImportacion" runat="server" Visible="false" CssClass="mt-3">
    <%-- Marca de la importación que se muestra (RegistroDespachoReglas.MarcaImportacion): con dos pestañas, la otra pudo leer otro ZIP. --%>
    <asp:HiddenField ID="hfMarcaImp" runat="server" EnableViewState="false" />
    <div class="rd-lote-bar mb-3">
        <div class="rd-lote-grid">
            <div class="rd-lote-item"><span class="rd-lote-k">Pedido</span><span class="rd-lote-v"><asp:Literal ID="litImpPedido" runat="server" /></span></div>
            <div class="rd-lote-item"><span class="rd-lote-k">Cliente</span><span class="rd-lote-v"><asp:Literal ID="litImpCliente" runat="server" /></span></div>
            <div class="rd-lote-item"><span class="rd-lote-k">Factura</span><span class="rd-lote-v"><asp:Literal ID="litImpFactura" runat="server" /></span></div>
            <div class="rd-lote-item"><span class="rd-lote-k">Packing list</span><span class="rd-lote-v"><asp:Literal ID="litImpPackingList" runat="server" /></span></div>
            <div class="rd-lote-item"><span class="rd-lote-k">Operación</span><span class="rd-lote-v"><asp:Literal ID="litImpOperacion" runat="server" /></span></div>
            <div class="rd-lote-item"><span class="rd-lote-k">Destino de la carga</span><span class="rd-lote-v"><asp:Literal ID="litImpDestino" runat="server" /></span></div>
            <div class="rd-lote-item"><span class="rd-lote-k">Tractos</span><span class="rd-lote-v"><asp:Literal ID="litImpTractos" runat="server" /></span></div>
            <div class="rd-lote-item"><span class="rd-lote-k">Carga total</span><span class="rd-lote-v"><asp:Literal ID="litImpCarga" runat="server" /></span></div>
        </div>
    </div>

    <asp:Panel ID="pnlAdvertenciasImportacion" runat="server" CssClass="rd-notice rd-notice-warn mb-3" Visible="false">
        <strong><i class="fas fa-exclamation-triangle mr-1"></i> Revise antes de continuar:</strong>
        <asp:Literal ID="litAdvertenciasImportacion" runat="server" />
    </asp:Panel>

    <!-- Qué se tomó de la programación en Word (o el recordatorio de subirla) -->
    <asp:Panel ID="pnlProgramacionCarga" runat="server" CssClass="rd-notice mb-3">
        <asp:Literal ID="litProgramacionCarga" runat="server" />
    </asp:Panel>

    <!-- Lo único del lote que no viene en los documentos (o que conviene confirmar) -->
    <div class="rd-section-label">Datos del lote</div>
    <div class="row mb-2">
        <div class="col-md-4 form-group mb-2">
            <label for="<%= ddlClienteImp.ClientID %>" class="sgv-etiqueta small font-weight-bold mb-1">Cliente <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
            <asp:DropDownList ID="ddlClienteImp" runat="server" aria-required="true" CssClass="custom-select custom-select-sm" />
            <asp:RequiredFieldValidator ID="rfvClienteImp" runat="server" ControlToValidate="ddlClienteImp" InitialValue="0"
                ErrorMessage="Elija el cliente del pedido." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgImportar" />
            <small class="form-text text-muted">Reconocido por el RUC del remitente.</small>
        </div>
        <div class="col-md-4 form-group mb-2">
            <label for="<%= txtFechaProgramacionImp.ClientID %>" class="sgv-etiqueta small font-weight-bold mb-1">Fecha de programación <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
            <asp:TextBox ID="txtFechaProgramacionImp" runat="server" aria-required="true" CssClass="form-control form-control-sm" TextMode="Date" />
            <asp:RequiredFieldValidator ID="rfvFechaProgramacionImp" runat="server" ControlToValidate="txtFechaProgramacionImp"
                ErrorMessage="Indique la fecha de programación." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgImportar" />
            <small class="form-text text-muted">Se propone el día de carga de la programación (o, sin ella, la fecha de traslado de las guías).</small>
        </div>
        <div class="col-md-4 form-group mb-2">
            <label for="<%= ddlPlantaImp.ClientID %>" class="sgv-etiqueta small font-weight-bold mb-1">Planta de carga <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
            <asp:DropDownList ID="ddlPlantaImp" runat="server" aria-required="true" CssClass="custom-select custom-select-sm" />
            <asp:RequiredFieldValidator ID="rfvPlantaImp" runat="server" ControlToValidate="ddlPlantaImp" InitialValue="0"
                ErrorMessage="Elija la planta de carga." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgImportar" />
            <small class="form-text text-muted">Carga nacional; el destino queda como dato de la carga.</small>
        </div>
    </div>

    <div class="rd-section-label">Tractos del pedido</div>
    <div class="table-responsive">
        <table class="table table-sm rd-tabla-conductores rd-tabla-import align-middle mb-3">
            <thead>
                <tr>
                    <th>Incluir</th>
                    <th>Transporte / Guía</th>
                    <th>Conductor</th>
                    <th>Tracto</th>
                    <th>Carreta <span class="sgv-obligatorio" aria-hidden="true">*</span></th>
                    <th>Carga</th>
                    <th>Estado</th>
                </tr>
            </thead>
            <tbody>
                <asp:Repeater ID="rptTractosImportados" runat="server" EnableViewState="false" OnItemDataBound="rptTractosImportados_ItemDataBound">
                    <ItemTemplate>
                        <tr class="<%#: (bool)Eval("YaRegistrado") ? "rd-fila-registrada" : "" %>">
                            <td><label class="mb-0"><asp:CheckBox ID="chkIncluir" runat="server" /><span class="sr-only"><%#: "Incluir el transporte " + Eval("NumeroTransporte") %></span></label></td>
                            <td>
                                <div class="rd-placa"><%# HttpUtility.HtmlEncode(Eval("NumeroTransporte")) %></div>
                                <small class="text-muted"><%# HttpUtility.HtmlEncode(Eval("NumeroGuia") ?? "sin guía") %></small>
                                <asp:Panel ID="pnlProgramadoImp" runat="server" CssClass="mt-1 rd-programado-sel">
                                    <small class="rd-doc-dato">Completa el despacho</small>
                                    <asp:DropDownList ID="ddlProgramadoImp" runat="server" CssClass="custom-select custom-select-sm"
                                        ToolTip="Despacho programado con el Word que esta guía completa. Si eligió uno, chofer, tracto y carreta son los del despacho." />
                                </asp:Panel>
                            </td>
                            <td class="rd-col-conductor">
                                <small class="rd-doc-dato">Documento: <%# HttpUtility.HtmlEncode(Eval("NombreChofer")) %><%#: Eval("Licencia") == null ? "" : " · Lic. " + Eval("Licencia") %></small>
                                <asp:DropDownList ID="ddlConductorImp" runat="server" CssClass="custom-select custom-select-sm select2-searchable" aria-label='<%# "Conductor del transporte " + Eval("NumeroTransporte") %>' />
                            </td>
                            <td class="rd-col-placa">
                                <small class="rd-doc-dato" data-dato='<%#: Eval("Placa") %>'>Documento: <%# HttpUtility.HtmlEncode(Eval("Placa")) %></small>
                                <asp:DropDownList ID="ddlTractoImp" runat="server" CssClass="custom-select custom-select-sm select2-searchable" aria-label='<%# "Tracto del transporte " + Eval("NumeroTransporte") %>' />
                            </td>
                            <td class="rd-col-placa">
                                <small class="rd-doc-dato" data-dato='<%#: RegistroDespachoReglas.DatoPistaCarreta((WebSGV.Models.Despachos.TractoImportado)Container.DataItem) %>'><asp:Literal ID="litCarretaHint" runat="server" /></small>
                                <asp:DropDownList ID="ddlCarretaImp" runat="server" CssClass="custom-select custom-select-sm select2-searchable" aria-label='<%# "Carreta del transporte " + Eval("NumeroTransporte") %>' />
                            </td>
                            <td class="rd-carga-celda"><asp:Literal ID="litCargaImp" runat="server" /></td>
                            <td class="rd-col-estado"><asp:Literal ID="litEstadoImp" runat="server" /></td>
                        </tr>
                    </ItemTemplate>
                </asp:Repeater>
            </tbody>
        </table>
    </div>

    <!-- Altas que requieren aprobación -->
    <asp:Panel ID="pnlAltasImportacion" runat="server" Visible="false" CssClass="rd-doc-box mb-3">
        <div class="rd-doc-title">Registros nuevos que requieren su aprobación</div>

        <asp:Panel ID="pnlProductosNuevos" runat="server" Visible="false" CssClass="mb-3">
            <div class="small font-weight-bold mb-1">Productos que no están en el catálogo del cliente</div>
            <asp:Repeater ID="rptProductosNuevos" runat="server" EnableViewState="false">
                <ItemTemplate>
                    <div class="form-row align-items-center mb-1">
                        <div class="col-auto"><span class="rd-placa small"><%# HttpUtility.HtmlEncode(Eval("Codigo") ?? "—") %></span></div>
                        <div class="col">
                            <asp:TextBox ID="txtNombreProducto" runat="server" CssClass="form-control form-control-sm" MaxLength="50" aria-label='<%# "Nombre del producto " + (Eval("Codigo") ?? "") %>'
                                Text='<%# Eval("Descripcion").ToString().ToUpperInvariant() %>' />
                            <asp:HiddenField ID="hfCodigoProducto" runat="server" Value='<%# Eval("Codigo") %>' />
                            <asp:HiddenField ID="hfDescripcionProducto" runat="server" Value='<%# Eval("Descripcion") %>' />
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
        </asp:Panel>

        <asp:Panel ID="pnlConductoresNuevos" runat="server" Visible="false" CssClass="mb-3">
            <div class="small font-weight-bold mb-1">Conductores que no están registrados <span class="font-weight-normal text-muted">(se registran solo los que deje como "Registrar nuevo" en la tabla)</span></div>
            <asp:Repeater ID="rptConductoresNuevos" runat="server" EnableViewState="false" OnItemDataBound="rptConductoresNuevos_ItemDataBound">
                <ItemTemplate>
                    <div class="form-row align-items-center mb-2">
                        <div class="col-md-2 col-6">
                            <small class="text-muted d-block">DNI / Licencia</small>
                            <span class="rd-placa small"><%# HttpUtility.HtmlEncode(Eval("DniSegunLicencia")) %></span>
                            <small class="text-muted"><%# HttpUtility.HtmlEncode(Eval("Licencia")) %></small>
                            <asp:HiddenField ID="hfTransporteConductor" runat="server" Value='<%# Eval("NumeroTransporte") %>' />
                        </div>
                        <div class="col-md-3 col-6">
                            <small class="text-muted d-block">Apellido paterno</small>
                            <asp:TextBox ID="txtApPaterno" runat="server" CssClass="form-control form-control-sm" MaxLength="20" aria-label='<%# "Apellido paterno, transporte " + Eval("NumeroTransporte") %>' />
                        </div>
                        <div class="col-md-3 col-6">
                            <small class="text-muted d-block">Apellido materno</small>
                            <asp:TextBox ID="txtApMaterno" runat="server" CssClass="form-control form-control-sm" MaxLength="20" aria-label='<%# "Apellido materno, transporte " + Eval("NumeroTransporte") %>' />
                        </div>
                        <div class="col-md-4 col-6">
                            <small class="text-muted d-block">Nombres</small>
                            <asp:TextBox ID="txtNombres" runat="server" CssClass="form-control form-control-sm" MaxLength="50" aria-label='<%# "Nombres, transporte " + Eval("NumeroTransporte") %>' />
                        </div>
                        <asp:Literal ID="litNombreTruncado" runat="server" />
                    </div>
                </ItemTemplate>
            </asp:Repeater>
        </asp:Panel>

        <asp:CheckBox ID="chkAprobarAltas" runat="server" CssClass="rd-check-aprobar"
            Text="Apruebo registrar en el sistema los productos y conductores nuevos de esta lista." />
    </asp:Panel>

    <asp:Panel ID="pnlArchivosIgnorados" runat="server" Visible="false" CssClass="small text-muted mb-3">
        Archivos no usados: <asp:Literal ID="litArchivosIgnorados" runat="server" />
    </asp:Panel>

    <div class="d-flex justify-content-end rd-grupo">
        <asp:Button ID="btnDescartarImportacion" runat="server"
            Text="Descartar"
            CssClass="btn btn-outline-secondary"
            CausesValidation="false"
            OnClick="btnDescartarImportacion_Click" />
        <asp:Button ID="btnCorregirImportacion" runat="server"
            Text="Corregir datos a mano"
            CssClass="btn btn-outline-primary"
            CausesValidation="false"
            ToolTip="Pasa los datos leídos al formulario de configuración para modificarlos antes de iniciar el lote"
            OnClick="btnCorregirImportacion_Click" />
        <asp:Button ID="btnAplicarImportacion" runat="server"
            Text="Crear lote con estos datos"
            CssClass="btn btn-primary"
            ValidationGroup="vgImportar"
            OnClick="btnAplicarImportacion_Click" />
    </div>
</asp:Panel>

<!-- Importación ya aplicada: los conductores entran al iniciar el lote -->
<asp:Panel ID="pnlImportacionAplicada" runat="server" Visible="false" CssClass="rd-notice rd-notice-ok mt-3">
    <div class="d-flex justify-content-between align-items-center flex-wrap rd-grupo">
        <span><asp:Literal ID="litImportacionAplicada" runat="server" /></span>
        <%-- Marca de lo aplicado (M-206): "Iniciar lote" la compara con la de la sesión; sin el recuadro a la vista no llega. --%>
        <asp:HiddenField ID="hfMarcaImpAplicada" runat="server" EnableViewState="false" />
        <asp:Button ID="btnQuitarImportacion" runat="server"
            Text="Quitar importación"
            CssClass="btn btn-outline-secondary btn-sm"
            CausesValidation="false"
            OnClick="btnDescartarImportacion_Click" />
    </div>
</asp:Panel>

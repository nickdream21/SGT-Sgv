<%@ Page Title="Registro manual de documentos" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroManualLote.aspx.cs" Inherits="WebSGV.Views.RegistroManualLote" %>

<%@ Import Namespace="WebSGV.Helpers" %>
<%@ Import Namespace="WebSGV.Services.Despachos" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

<link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/RegistroManualLote.css") %>" rel="stylesheet" />

    <%-- Registro manual de documentos de un lote (E6-27, antes una vista de ListaDespachos con ?manual=1).
         Se abre desde el expediente de la Bandeja ("Registro manual", ?lote=) y desde el aviso de Editar lote
         (?lote=&despachos=1). Ver los documentos y subirlos es trabajo del expediente; aquí queda lo que solo se
         puede hacer a mano: escribir un número sin tener el documento, adjuntar el PDF de la factura o del CPIC
         y los manifiestos de cruce y retorno. --%>
    <div class="ld-pagina">
        <div class="sgv-encabezado">
            <div class="sgv-encabezado-titulo">
                <h1 class="h2">Registro manual de documentos</h1>
                <%-- Sin lote (pnlLoteNoDisponible) no hay cliente ni pedido que mostrar: el resumen y "Ver el lote" se ocultan. --%>
                <asp:PlaceHolder ID="phResumenLote" runat="server">
                <p class="sgv-resumen"><span><asp:Label ID="lblClienteManifiestos" runat="server"></asp:Label></span><span>Pedido <asp:Label ID="lblPedidoManifiestos" runat="server"></asp:Label></span></p>
                </asp:PlaceHolder>
            </div>
            <div class="sgv-encabezado-acciones">
                <asp:HyperLink ID="hlVolverBandejaDocs" runat="server"
                    NavigateUrl="~/Views/BandejaDocumentos.aspx"
                    CssClass="btn btn-primary btn-sm">← Volver al expediente</asp:HyperLink>
                <asp:HyperLink ID="hlVerLote" runat="server" NavigateUrl="~/Views/ListaDespachos.aspx"
                    CssClass="btn btn-outline-secondary btn-sm">Ver el lote</asp:HyperLink>
                <a class="sgv-ayuda" href="<%: ResolveUrl("~/Views/Ayuda.aspx?tema=bandeja-documentos") %>" target="_blank" rel="noopener"
                    data-ayuda="bandeja-documentos" aria-label="Ayuda de esta p&#xE1;gina">?</a>
            </div>
        </div>

        <%-- gvManifiestosLote está declarado como PostBackTrigger (no Async) en <Triggers> más abajo: tiene
             FileUpload por fila (un archivo no viaja en un postback parcial de UpdatePanel) y "Ver" abre el
             documento en otra pestaña al volver la página. --%>
        <asp:UpdatePanel ID="UpdatePanelMain" runat="server" UpdateMode="Conditional">
            <ContentTemplate>

                <asp:Panel ID="pnlMensajeManifiesto" runat="server" Visible="false" CssClass="mb-3">
                    <asp:Label ID="lblMensajeManifiesto" runat="server" CssClass="alert d-block"></asp:Label>
                </asp:Panel>

                <%-- El lote del enlace no existe, está anulado o cambió de clave (otra pestaña editó su fecha o pedido). --%>
                <asp:Panel ID="pnlLoteNoDisponible" runat="server" Visible="false" CssClass="ld-aviso ld-aviso-alerta" role="status">
                    <div>
                        <strong>El lote no existe, está anulado o cambió.</strong>
                        Vuelva a abrirlo desde la <asp:HyperLink ID="hlBandejaNoDisponible" runat="server" NavigateUrl="~/Views/BandejaDocumentos.aspx">Bandeja de documentos</asp:HyperLink>.
                    </div>
                </asp:Panel>

                <asp:Panel ID="pnlRegistroManual" runat="server" Visible="false">
                    <section class="sgv-panel ld-docs-page">
                        <div class="sgv-panel-cuerpo">

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
                                                            <td data-label="Guía / MCI"><%# RegistroManualLoteReglas.CeldaGuiaMci(Eval("Guia"), Eval("Manifiestos")) %></td>
                                                            <td data-label="Factura"><%# RegistroManualLoteReglas.CeldaDocumento(Eval("NumeroFactura"), Eval("FacturaConPdf")) %></td>
                                                            <td data-label="CPIC"><%# RegistroManualLoteReglas.CeldaDocumento(Eval("NumeroCPIC"), Eval("CpicConPdf")) %></td>
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
                                        <asp:Repeater ID="rptDocumentosLote" runat="server" OnItemCommand="rptDocumentosLote_ItemCommand" OnItemCreated="rptDocumentosLote_ItemCreated">
                                            <ItemTemplate>
                                                <div class="ld-docs-lista-fila">
                                                    <div class="ld-docs-lista-doc">
                                                        <span class="ld-docs-lista-tipo"><%#: Eval("TipoTexto") %></span>
                                                        <span class="ld-docs-num"><%#: Eval("Numero") %></span>
                                                        <span class="sgv-suave"><%#: Eval("Despachos") %> despacho(s)</span>
                                                        <%# RegistroManualLoteReglas.InsigniaDocumento((bool)Eval("ConPdf"), (bool)Eval("ConPdf") ? "PDF" : "sin PDF") %>
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
                                                            <%-- Con ID y registrado como postback completo en rptDocumentosLote_ItemCreated:
                                                                 el PostBackTrigger del repeater no lo cubre y sin él el archivo no viaja. --%>
                                                            <asp:Button ID="btnSubirPdfDocumentoLote" runat="server" Text="Subir PDF" CssClass="btn btn-primary btn-sm ld-doc-btn"
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
                <asp:AsyncPostBackTrigger ControlID="btnGuardarGuias" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="lnkVerDocFactura" EventName="Click" />
                <asp:AsyncPostBackTrigger ControlID="lnkVerDocCpic" EventName="Click" />
                <asp:PostBackTrigger ControlID="gvManifiestosLote" />
                <%-- FileUpload: postback completo, en uno parcial el archivo no llega. --%>
                <asp:PostBackTrigger ControlID="btnSubirPdfFactura" />
                <asp:PostBackTrigger ControlID="btnSubirPdfCpic" />
                <asp:PostBackTrigger ControlID="rptDocumentosLote" />
                <asp:AsyncPostBackTrigger ControlID="btnAsignarDocumentos" EventName="Click" />
            </Triggers>
        </asp:UpdatePanel>
    </div>

</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="ScriptsSection" runat="server">
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistroManualLote.js") %>"></script>
</asp:Content>

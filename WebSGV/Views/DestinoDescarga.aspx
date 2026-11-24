<%@ Page Title="Destino de descarga" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DestinoDescarga.aspx.cs" Inherits="WebSGV.Views.DestinoDescarga" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <%-- Quitar de la fila (E5-16): la grilla no guarda ViewState; el botón de la fila copia su data-argumento
         (el id del destino) aquí y pulsa btnAccionFila (SGV.acciones.enviarFila). El servidor lo revalida. --%>
    <asp:HiddenField ID="hfArgumentoFila" runat="server" />
    <%-- Pedido de la última búsqueda (lo escriben Buscar y, para mostrar lo recién guardado, Guardar): con él
         se filtra en todo postback. --%>
    <asp:HiddenField ID="hfFiltroAplicado" runat="server" />
    <asp:Button ID="btnAccionFila" runat="server" Text="Quitar destino" CssClass="d-none" CausesValidation="false"
        OnClick="btnAccionFila_Click" tabindex="-1" aria-hidden="true" />

    <asp:Panel ID="pnlMensaje" runat="server" EnableViewState="false" Visible="false">
        <asp:Label ID="lblMensaje" runat="server" EnableViewState="false"></asp:Label>
    </asp:Panel>

    <div class="sgv-encabezado">
        <div class="sgv-encabezado-titulo">
            <h1 class="h2">Destino de descarga</h1>
            <p>A qu&#xE9; planta del cliente va cada unidad de un pedido</p>
        </div>
        <div class="sgv-encabezado-acciones">
            <a class="btn btn-primary btn-sm d-lg-none" href="#altaCatalogo">Registrar unidad</a>
            <button type="button" class="sgv-ayuda" data-toggle="collapse" data-target="#ayudaPagina"
                aria-controls="ayudaPagina" aria-expanded="false" aria-label="Ayuda de esta página">?</button>
        </div>
    </div>
    <div id="ayudaPagina" class="collapse">
        <p class="sgv-ayuda-texto">Indica a qu&#xE9; planta del cliente va cada tracto de un pedido (en Vitapro, <strong>Milagro</strong> o <strong>Jave</strong>).
            La descarga por pedido de Registro de despachos usa esta lista para proponer la planta de cada unidad.
            Quitar un destino no lo borra: queda en el historial.</p>
    </div>

    <%-- Lista del cliente: se lee y se muestra qué cambiaría; no se guarda hasta "Aplicar". Plegada (E5-16) salvo
         con una vista previa en curso o un error de la carga (CargaAbierta). --%>
    <details class="sgv-plegable"<%= CargaAbierta ? " open" : "" %>>
        <summary>Cargar la lista de recepci&#xF3;n del cliente</summary>
        <div class="sgv-plegable-cuerpo">
            <p class="text-muted small mb-3">
                Suba el Excel que env&#xED;a el cliente (columnas <em>Pedido</em>, <em>Placa Cami&#xF3;n</em> con "tracto - carreta" y <em>Lugar de recepci&#xF3;n</em>)
                o pegue sus filas copiadas desde Excel. Nada se guarda hasta pulsar <strong>Aplicar</strong>.
            </p>
            <div class="row align-items-end">
                <div class="col-md-4 form-group">
                    <label for="<%= fuLista.ClientID %>" class="sgv-etiqueta">Excel (.xlsx)</label>
                    <asp:FileUpload ID="fuLista" runat="server" CssClass="form-control-file" accept=".xlsx" />
                </div>
                <div class="col-md-6 form-group">
                    <label for="<%= txtLista.ClientID %>" class="sgv-etiqueta">o filas pegadas</label>
                    <asp:TextBox ID="txtLista" runat="server" TextMode="MultiLine" Rows="3" CssClass="form-control form-control-sm"
                        placeholder="25/9/2026&#9;4400132772&#9;1&#9;BSP816 - TBE-995&#9;VIVIANA&#9;MILAGRO"></asp:TextBox>
                </div>
                <div class="col-md-2 form-group">
                    <asp:Button ID="btnLeer" runat="server" CssClass="btn btn-primary btn-block" Text="Leer la lista" OnClick="btnLeer_Click" />
                </div>
            </div>

            <asp:Panel ID="pnlVistaPrevia" runat="server" Visible="false">
                <asp:Literal ID="litVistaPrevia" runat="server" />
                <div class="d-flex justify-content-end mt-3">
                    <asp:Button ID="btnCancelar" runat="server" CssClass="btn btn-secondary mr-2" Text="Cancelar" OnClick="btnCancelar_Click" />
                    <asp:Button ID="btnAplicar" runat="server" CssClass="btn btn-success" Text="Aplicar"
                        OnClick="btnAplicar_Click" data-confirmar="¿Guardar los destinos de la lista?" />
                </div>
            </asp:Panel>
        </div>
    </details>

    <div class="row">
        <div class="col-12 col-lg-4 mb-4" id="altaCatalogo">
            <section class="sgv-panel">
                <div class="sgv-panel-cab">
                    <h2 class="sgv-panel-titulo">Registrar o cambiar una unidad</h2>
                </div>
                <div class="sgv-panel-cuerpo" data-enter="<%= btnGuardarManual.ClientID %>">
                    <p class="text-muted small">Para un desv&#xED;o o una unidad suelta. Si la carreta ya ten&#xED;a destino en ese pedido, se cambia y el anterior queda en el historial.</p>
                    <div class="form-group">
                        <label class="sgv-etiqueta" for="<%= txtPedido.ClientID %>">Pedido</label>
                        <asp:TextBox ID="txtPedido" runat="server" CssClass="form-control" MaxLength="12" placeholder="4400132772"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label class="sgv-etiqueta" for="<%= txtCarreta.ClientID %>">Carreta</label>
                        <asp:TextBox ID="txtCarreta" runat="server" CssClass="form-control text-uppercase" MaxLength="8" placeholder="TBE-995"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label class="sgv-etiqueta" for="<%= txtTracto.ClientID %>">Tracto <span class="text-muted font-weight-normal">(opcional)</span></label>
                        <asp:TextBox ID="txtTracto" runat="server" CssClass="form-control text-uppercase" MaxLength="8" placeholder="BSP-816"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label class="sgv-etiqueta" for="<%= ddlPlanta.ClientID %>">Planta de descarga</label>
                        <asp:DropDownList ID="ddlPlanta" runat="server" CssClass="form-control"></asp:DropDownList>
                    </div>
                    <asp:Button ID="btnGuardarManual" runat="server" CssClass="btn btn-primary btn-block" Text="Guardar" OnClick="btnGuardarManual_Click" />
                </div>
            </section>
        </div>

        <div class="col-12 col-lg-8 mb-4 order-first order-lg-0">
            <section class="sgv-panel">
                <div class="sgv-panel-cab">
                    <div>
                        <h2 class="sgv-panel-titulo">Destinos registrados</h2>
                        <p class="sgv-panel-nota mb-0" aria-live="polite"><asp:Literal ID="litResumen" runat="server" Mode="Encode" EnableViewState="false" /></p>
                    </div>
                    <div class="sgv-buscador" role="search" data-enter="<%= btnBuscar.ClientID %>">
                        <asp:TextBox ID="txtBuscar" runat="server" CssClass="form-control form-control-sm" MaxLength="12"
                            placeholder="Número de pedido" aria-label="Filtrar destinos por número de pedido"
                            inputmode="numeric" autocomplete="off" spellcheck="false" />
                        <asp:Button ID="btnBuscar" runat="server" Text="Buscar" CssClass="btn btn-outline-secondary btn-sm"
                            CausesValidation="false" OnClick="btnBuscar_Click" />
                    </div>
                </div>
                <div class="table-responsive">
                    <asp:GridView ID="gvDestinos" runat="server"
                        CssClass="table sgv-tabla mb-0"
                        AutoGenerateColumns="false"
                        EnableViewState="false"
                        AllowPaging="true" PageSize="25"
                        OnPageIndexChanging="gvDestinos_PageIndexChanging"
                        GridLines="None" CellSpacing="-1"
                        EmptyDataText="No hay destinos registrados.">
                        <PagerSettings Mode="NumericFirstLast" PageButtonCount="5" FirstPageText="Primera" LastPageText="Última" />
                        <PagerStyle CssClass="sgv-paginador" />
                        <EmptyDataRowStyle CssClass="sgv-vacio" />
                        <Columns>
                            <asp:BoundField DataField="numeroPedido" HeaderText="Pedido" />
                            <asp:TemplateField HeaderText="Carreta">
                                <ItemTemplate><%#: PlacaVisible(Eval("placaCarreta")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Tracto" HeaderStyle-CssClass="d-none d-lg-table-cell" ItemStyle-CssClass="d-none d-lg-table-cell">
                                <ItemTemplate><%#: PlacaVisible(Eval("placaTracto")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="planta" HeaderText="Planta" />
                            <asp:BoundField DataField="fechaRecepcion" HeaderText="Recepci&#xF3;n" DataFormatString="{0:dd/MM/yyyy}"
                                HeaderStyle-CssClass="d-none d-lg-table-cell" ItemStyle-CssClass="d-none d-lg-table-cell" />
                            <asp:BoundField DataField="origen" HeaderText="Origen"
                                HeaderStyle-CssClass="d-none d-lg-table-cell" ItemStyle-CssClass="d-none d-lg-table-cell" />
                            <asp:BoundField DataField="usuario" HeaderText="Registr&#xF3;"
                                HeaderStyle-CssClass="d-none d-lg-table-cell" ItemStyle-CssClass="d-none d-lg-table-cell" />
                            <asp:TemplateField HeaderStyle-CssClass="sgv-col-acciones" ItemStyle-CssClass="sgv-col-acciones">
                                <HeaderTemplate><span class="sr-only">Acciones</span></HeaderTemplate>
                                <ItemTemplate>
                                    <button type="button" class="sgv-btn-icono peligro" title="Quitar"
                                        data-accion="enviarFila"
                                        data-argumento='<%#: Eval("idDestinoDescarga") %>'
                                        data-confirmar='<%#: PreguntaQuitar(Eval("placaCarreta"), Eval("numeroPedido")) %>'
                                        data-confirmar-peligro="true"
                                        aria-label='<%#: EtiquetaQuitar(Eval("placaCarreta"), Eval("numeroPedido")) %>'>
                                        <i class="fas fa-times" aria-hidden="true"></i>
                                    </button>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </section>
        </div>
    </div>

    <script>
        // Valores del servidor que usa SiteMaster.js (acción enviarFila del botón Quitar).
        var SGV = Object.assign(window.SGV || {}, {
            hfArgumentoFila: '<%= hfArgumentoFila.ClientID %>',
            btnAccionFila: '<%= btnAccionFila.ClientID %>'
        });
    </script>

</asp:Content>

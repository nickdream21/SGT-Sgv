<%@ Page Title="Reporte GPS por placas" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReporteGps.aspx.cs" Inherits="WebSGV.Views.ReporteGps" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="sgv-encabezado">
        <div class="sgv-encabezado-titulo">
            <h1 class="h2">Reporte GPS por placas</h1>
            <p>
                El historial de varias unidades a la vez, un Excel por placa con las mismas columnas que
                <em>Reportes &#x2192; Historial &#x2192; Exportar datos</em> del portal Onway. Solo lee del GPS: no cambia nada.
            </p>
        </div>
        <div class="sgv-encabezado-acciones">
            <a class="sgv-ayuda" href="<%: ResolveUrl("~/Views/Ayuda.aspx?tema=reporte-gps") %>" target="_blank" rel="noopener"
                data-ayuda="reporte-gps" aria-label="Ayuda de esta p&#xE1;gina">?</a>
        </div>
    </div>

    <section class="sgv-panel mb-4">
        <div class="sgv-panel-cab">
            <h2 class="sgv-panel-titulo">Unidades y rango</h2>
        </div>
        <div class="sgv-panel-cuerpo">
            <div class="row">
                <div class="col-lg-5 form-group">
                    <label class="sgv-etiqueta" for="<%= txtPedidos.ClientID %>">Pedidos <span class="text-muted font-weight-normal">(opcional)</span></label>
                    <div class="input-group" data-enter="<%= btnTraerPlacas.ClientID %>">
                        <asp:TextBox ID="txtPedidos" runat="server" CssClass="form-control" placeholder="4400133751, 4400133813"></asp:TextBox>
                        <div class="input-group-append">
                            <asp:Button ID="btnTraerPlacas" runat="server" CssClass="btn btn-outline-primary" Text="Traer tractos" OnClick="btnTraerPlacas_Click" />
                        </div>
                    </div>
                    <small class="text-muted">Agrega a la lista los tractos de carga y de descarga registrados en esos pedidos.</small>
                    <asp:Literal ID="litPlacasPedido" runat="server" />
                </div>
                <div class="col-lg-7 form-group">
                    <div class="d-flex justify-content-between align-items-end flex-wrap mb-2">
                        <label class="sgv-etiqueta mb-0" for="<%= txtPlacas.ClientID %>">Placas de los tractos</label>
                        <asp:Button ID="btnActivasVitapro" runat="server" CssClass="btn btn-outline-primary btn-sm" Text="Activas de Vitapro" OnClick="btnActivasVitapro_Click"
                            ToolTip="Las unidades que hoy cargan, llevan, descargan o vuelven de un viaje de Vitapro, seg&#xFA;n el seguimiento" />
                    </div>
                    <asp:TextBox ID="txtPlacas" runat="server" TextMode="MultiLine" Rows="4" CssClass="form-control text-uppercase"
                        placeholder="CBU-945, BNG-862, C6K-799 (separadas por coma, espacio o una por l&#xED;nea)"></asp:TextBox>
                    <asp:Literal ID="litActivas" runat="server" />
                </div>
            </div>
            <div class="row align-items-end">
                <div class="col-sm-6 col-lg-3 form-group">
                    <label class="sgv-etiqueta" for="<%= txtDesde.ClientID %>">Desde</label>
                    <asp:TextBox ID="txtDesde" runat="server" TextMode="DateTimeLocal" CssClass="form-control"></asp:TextBox>
                </div>
                <div class="col-sm-6 col-lg-3 form-group">
                    <label class="sgv-etiqueta" for="<%= txtHasta.ClientID %>">Hasta</label>
                    <asp:TextBox ID="txtHasta" runat="server" TextMode="DateTimeLocal" CssClass="form-control"></asp:TextBox>
                </div>
                <div class="col-lg-6 form-group text-lg-right">
                    <asp:Button ID="btnHoy" runat="server" CssClass="btn btn-outline-secondary mr-2" Text="Hoy" OnClick="btnHoy_Click" />
                    <asp:Button ID="btnGenerar" runat="server" CssClass="btn btn-primary" Text="Generar reportes" OnClick="btnGenerar_Click" data-espera="600" />
                </div>
            </div>
            <div id="avisoGenerando" class="sgv-cargando d-none" role="status">
                Consultando el GPS: unos 5 segundos por placa. No cierre la p&#xE1;gina.
            </div>
        </div>
    </section>

    <asp:Panel ID="pnlResultados" runat="server" Visible="false">
        <section class="sgv-panel mb-4">
            <div class="sgv-panel-cab">
                <h2 class="sgv-panel-titulo"><asp:Literal ID="litTituloResultados" runat="server" /></h2>
                <asp:Button ID="btnZip" runat="server" CssClass="btn btn-success btn-sm" Text="Descargar todos (ZIP)" OnClick="btnZip_Click" data-descarga="true" />
            </div>
            <div>
                <div class="table-responsive">
                    <asp:GridView ID="gvResultados" runat="server" AutoGenerateColumns="false" CssClass="table sgv-tabla mb-0"
                        GridLines="None" CellSpacing="-1" OnRowCommand="gvResultados_RowCommand"
                        EmptyDataText="No hay resultados para las placas y el rango elegidos.">
                        <EmptyDataRowStyle CssClass="sgv-vacio" />
                        <Columns>
                            <asp:BoundField DataField="Placa" HeaderText="Placa" ItemStyle-CssClass="font-weight-bold" />
                            <asp:BoundField DataField="Puntos" HeaderText="Puntos" />
                            <asp:TemplateField HeaderText="&#xDA;ltima posici&#xF3;n">
                                <ItemTemplate><%# UltimaPosicion(Container.DataItem) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Hor&#xF3;metro">
                                <ItemTemplate><%# (bool)Eval("ConHorometro") ? "S&#xED;" : "<span class=\"text-muted\">No reporta</span>" %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Excel">
                                <ItemTemplate>
                                    <asp:LinkButton ID="btnDescargar" runat="server" CommandName="Descargar" data-descarga="true" CommandArgument='<%# Container.DataItemIndex %>'
                                        Visible='<%# (bool)Eval("TieneArchivo") %>' CssClass="small">
                                        <%# HttpUtility.HtmlEncode(Eval("NombreArchivo")) %>
                                    </asp:LinkButton>
                                    <span class="text-danger small"><%# HttpUtility.HtmlEncode(Eval("Error")) %></span>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
        </section>

        <section class="sgv-panel mb-4">
            <div class="sgv-panel-cab">
                <h2 class="sgv-panel-titulo">Para el correo</h2>
            </div>
            <div class="sgv-panel-cuerpo">
                <div class="form-group">
                    <label class="sgv-etiqueta" for="<%= txtAsunto.ClientID %>">Asunto</label>
                    <div class="input-group">
                        <asp:TextBox ID="txtAsunto" runat="server" CssClass="form-control"></asp:TextBox>
                        <div class="input-group-append">
                            <button type="button" class="btn btn-outline-secondary" data-copiar="<%= txtAsunto.ClientID %>">Copiar</button>
                        </div>
                    </div>
                </div>
                <div class="form-group mb-0">
                    <label class="sgv-etiqueta" for="<%= txtCuerpo.ClientID %>">Mensaje</label>
                    <asp:TextBox ID="txtCuerpo" runat="server" TextMode="MultiLine" Rows="4" CssClass="form-control"></asp:TextBox>
                    <button type="button" class="btn btn-outline-secondary btn-sm mt-2" data-copiar="<%= txtCuerpo.ClientID %>">Copiar mensaje</button>
                </div>
            </div>
        </section>
    </asp:Panel>

    <script>
        var SGV = Object.assign(window.SGV || {}, { btnGenerar: '<%= btnGenerar.ClientID %>' });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/ReporteGps.js") %>"></script>
</asp:Content>

<%@ Page Title="Reporte GPS por placas" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReporteGps.aspx.cs" Inherits="WebSGV.Views.ReporteGps" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-3">

        <div class="row mb-4">
            <div class="col-12">
                <div class="border-bottom pb-3">
                    <h2 class="mb-1" style="color:#1e293b;font-weight:600;">
                        <i class="fas fa-satellite-dish mr-2" style="color:#2563eb;"></i>Reporte GPS por placas
                    </h2>
                    <p class="text-muted mb-0">
                        El historial de varias unidades a la vez, un Excel por placa con las mismas columnas que
                        <em>Reportes &#x2192; Historial &#x2192; Exportar datos</em> del portal Onway. Solo lee del GPS: no cambia nada.
                    </p>
                </div>
            </div>
        </div>

        <div class="card shadow-sm mb-4">
            <div class="card-header bg-light">
                <h5 class="mb-0"><i class="fas fa-truck mr-2"></i>Unidades y rango</h5>
            </div>
            <div class="card-body">
                <div class="row">
                    <div class="col-lg-5 form-group">
                        <label class="font-weight-bold" for="<%= txtPedidos.ClientID %>">Pedidos <span class="text-muted font-weight-normal">(opcional)</span></label>
                        <div class="input-group">
                            <asp:TextBox ID="txtPedidos" runat="server" CssClass="form-control" placeholder="4400133751, 4400133813"></asp:TextBox>
                            <div class="input-group-append">
                                <asp:Button ID="btnTraerPlacas" runat="server" CssClass="btn btn-outline-primary" Text="Traer tractos" OnClick="btnTraerPlacas_Click" />
                            </div>
                        </div>
                        <small class="text-muted">Agrega a la lista los tractos de carga y de descarga registrados en esos pedidos.</small>
                        <asp:Literal ID="litPlacasPedido" runat="server" />
                    </div>
                    <div class="col-lg-7 form-group">
                        <label class="font-weight-bold" for="<%= txtPlacas.ClientID %>">Placas de los tractos</label>
                        <asp:TextBox ID="txtPlacas" runat="server" TextMode="MultiLine" Rows="4" CssClass="form-control text-uppercase"
                            placeholder="CBU-945, BNG-862, C6K-799 (separadas por coma, espacio o una por l&#xED;nea)"></asp:TextBox>
                    </div>
                </div>
                <div class="row align-items-end">
                    <div class="col-sm-6 col-lg-3 form-group">
                        <label class="font-weight-bold" for="<%= txtDesde.ClientID %>">Desde</label>
                        <asp:TextBox ID="txtDesde" runat="server" TextMode="DateTimeLocal" CssClass="form-control"></asp:TextBox>
                    </div>
                    <div class="col-sm-6 col-lg-3 form-group">
                        <label class="font-weight-bold" for="<%= txtHasta.ClientID %>">Hasta</label>
                        <asp:TextBox ID="txtHasta" runat="server" TextMode="DateTimeLocal" CssClass="form-control"></asp:TextBox>
                    </div>
                    <div class="col-lg-6 form-group text-lg-right">
                        <asp:Button ID="btnHoy" runat="server" CssClass="btn btn-outline-secondary mr-2" Text="Hoy" OnClick="btnHoy_Click" />
                        <asp:Button ID="btnGenerar" runat="server" CssClass="btn btn-primary" Text="Generar reportes" OnClick="btnGenerar_Click" />
                    </div>
                </div>
                <div id="avisoGenerando" class="alert alert-info mb-0 d-none">
                    <span class="spinner-border spinner-border-sm mr-2" role="status"></span>
                    Consultando el GPS... unos 5 segundos por placa. No cierre la p&#xE1;gina.
                </div>
            </div>
        </div>

        <asp:Panel ID="pnlResultados" runat="server" Visible="false">
            <div class="card shadow-sm mb-4">
                <div class="card-header bg-light d-flex justify-content-between align-items-center flex-wrap">
                    <h5 class="mb-0"><i class="fas fa-file-excel mr-2"></i><asp:Literal ID="litTituloResultados" runat="server" /></h5>
                    <asp:Button ID="btnZip" runat="server" CssClass="btn btn-success btn-sm" Text="Descargar todos (ZIP)" OnClick="btnZip_Click" />
                </div>
                <div class="card-body p-0">
                    <div class="table-responsive">
                        <asp:GridView ID="gvResultados" runat="server" AutoGenerateColumns="false" CssClass="table table-sm table-hover mb-0"
                            GridLines="None" OnRowCommand="gvResultados_RowCommand">
                            <HeaderStyle CssClass="thead-light" />
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
                                        <asp:LinkButton ID="btnDescargar" runat="server" CommandName="Descargar" CommandArgument='<%# Container.DataItemIndex %>'
                                            Visible='<%# Eval("Contenido") != null %>' CssClass="small">
                                            <i class="fas fa-download"></i> <%# HttpUtility.HtmlEncode(Eval("NombreArchivo")) %>
                                        </asp:LinkButton>
                                        <span class="text-danger small"><%# HttpUtility.HtmlEncode(Eval("Error")) %></span>
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>
            </div>

            <div class="card shadow-sm mb-4">
                <div class="card-header bg-light">
                    <h5 class="mb-0"><i class="fas fa-envelope mr-2"></i>Para el correo</h5>
                </div>
                <div class="card-body">
                    <div class="form-group">
                        <label class="font-weight-bold" for="<%= txtAsunto.ClientID %>">Asunto</label>
                        <div class="input-group">
                            <asp:TextBox ID="txtAsunto" runat="server" CssClass="form-control"></asp:TextBox>
                            <div class="input-group-append">
                                <button type="button" class="btn btn-outline-secondary" data-copiar="<%= txtAsunto.ClientID %>">Copiar</button>
                            </div>
                        </div>
                    </div>
                    <div class="form-group mb-0">
                        <label class="font-weight-bold" for="<%= txtCuerpo.ClientID %>">Mensaje</label>
                        <asp:TextBox ID="txtCuerpo" runat="server" TextMode="MultiLine" Rows="4" CssClass="form-control"></asp:TextBox>
                        <button type="button" class="btn btn-outline-secondary btn-sm mt-2" data-copiar="<%= txtCuerpo.ClientID %>">Copiar mensaje</button>
                    </div>
                </div>
            </div>
        </asp:Panel>

    </div>

    <script>
        var SGV = Object.assign(window.SGV || {}, { btnGenerar: '<%= btnGenerar.ClientID %>' });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/ReporteGps.js") %>"></script>
</asp:Content>

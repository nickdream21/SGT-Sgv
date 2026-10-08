<%@ Page Title="Reporte de Abastecimiento" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReporteAbastecimiento.aspx.cs" Inherits="WebSGV.Views.ReporteAbastecimiento" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/ReporteAbastecimiento.css") %>" rel="stylesheet" />

    <div class="container-fluid report-container">

        <!-- Encabezado Gradiente -->
        <div class="rpt-header">
            <h3><i class="fas fa-gas-pump mr-2"></i>Reporte de Abastecimiento</h3>
            <div class="rpt-meta">
                <span class="meta-count">
                    <i class="fas fa-list-ol mr-1"></i>
                    <asp:Label ID="lblTotalRegistros" runat="server" Text="0"></asp:Label> registros
                </span>
            </div>
        </div>

        <!-- Filtros -->
        <div class="rpt-filter-card">
            <div class="filter-title"><i class="fas fa-filter mr-1"></i>Filtros de Búsqueda</div>
            <div class="rpt-filter-row">
                <div class="filter-group">
                    <label>Fecha Desde</label>
                    <asp:TextBox ID="txtFechaDesde" runat="server" TextMode="Date" CssClass="form-control" style="min-width:140px;"></asp:TextBox>
                </div>
                <div class="filter-group">
                    <label>Fecha Hasta</label>
                    <asp:TextBox ID="txtFechaHasta" runat="server" TextMode="Date" CssClass="form-control" style="min-width:140px;"></asp:TextBox>
                </div>
                <div class="filter-group">
                    <label>Conductor</label>
                    <asp:TextBox ID="txtBuscarConductor" runat="server" CssClass="form-control" placeholder="Nombre o DNI..." style="min-width:160px;" autocomplete="off"></asp:TextBox>
                </div>
                <div class="filter-group">
                    <label>Motivo</label>
                    <asp:DropDownList ID="ddlTipoAbastecimiento" runat="server" CssClass="form-control" style="min-width:150px;">
                        <asp:ListItem Value="" Text="Todos"></asp:ListItem>
                        <asp:ListItem Value="VIAJE PROGRAMADO" Text="Viaje Programado"></asp:ListItem>
                        <asp:ListItem Value="ABASTECIMIENTO" Text="Abastecimiento"></asp:ListItem>
                        <asp:ListItem Value="MANTENIMIENTO" Text="Mantenimiento"></asp:ListItem>
                        <asp:ListItem Value="OTRO" Text="Otro"></asp:ListItem>
                    </asp:DropDownList>
                </div>
            </div>
            <div class="rpt-filter-actions">
                <asp:Button ID="btnBuscar" runat="server" CssClass="btn-filter btn-filter-primary" Text="Buscar" OnClick="btnBuscar_Click" />
                <asp:Button ID="btnLimpiar" runat="server" CssClass="btn-filter btn-filter-outline" Text="Limpiar" OnClick="btnLimpiar_Click" />
                <div class="filter-separator"></div>
                <asp:LinkButton ID="btnExportarExcel" runat="server" CssClass="btn-export" OnClick="btnExportarExcel_Click" CausesValidation="false">
                    <i class="fas fa-file-excel"></i> Exportar Excel
                </asp:LinkButton>
            </div>
        </div>

        <!-- Resumen (hidden labels kept for code-behind compatibility) -->
        <asp:Panel ID="pnlResumen" runat="server" Visible="false">
            <asp:Label ID="lblTotalGLAbastecidos" runat="server" Text="0" Visible="false"></asp:Label>
            <asp:Label ID="lblTotalGLConsumidos" runat="server" Text="0" Visible="false"></asp:Label>
            <asp:Label ID="lblTotalKM" runat="server" Text="0" Visible="false"></asp:Label>
        </asp:Panel>

        <!-- Tabla de reporte -->
        <asp:Panel ID="pnlReporte" runat="server" Visible="false">
            <div class="scroll-hint">
                <i class="fas fa-arrows-alt-h"></i> Desliza horizontalmente para ver más columnas
            </div>
            <div class="table-scroll-wrapper">
            <div class="table-wrapper">
                <div class="table-responsive">
                    <asp:GridView ID="gvReporte" runat="server" CssClass="table report-table mb-0"
                        AutoGenerateColumns="false" ShowHeaderWhenEmpty="false"
                        OnRowDataBound="gvReporte_RowDataBound">
                        <Columns>
                            <asp:BoundField DataField="NumeroFormato" HeaderText="Nro. Formato" />
                            <asp:BoundField DataField="Fecha" HeaderText="Fecha" DataFormatString="{0:dd/MM/yyyy}" />
                            <asp:TemplateField HeaderText="Motivo">
                                <ItemTemplate>
                                    <%# FormatTipoAbastecimiento(Eval("TipoAbastecimiento")) %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Tipo Vehiculo">
                                <ItemTemplate>
                                    <%# FormatTipoVehiculo(Eval("TipoVehiculo")) %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="Conductor" HeaderText="Nombre" />
                            <asp:BoundField DataField="PlacaTracto" HeaderText="Placa Tracto" />
                            <asp:BoundField DataField="Ruta" HeaderText="Ruta" />
                            <asp:BoundField DataField="Producto" HeaderText="Producto" />
                            <asp:BoundField DataField="LugarAbastecimiento" HeaderText="Lugar Abastecimiento" />
                            <asp:BoundField DataField="Hora" HeaderText="Hora" />
                            <asp:BoundField DataField="GLRutaAsignada" HeaderText="GL Asignados" DataFormatString="{0:#,##0.##}" />
                            <asp:BoundField DataField="GLCompradosRuta" HeaderText="GL Comprados" DataFormatString="{0:#,##0.##}" />
                            <asp:BoundField DataField="GLTotalAbastecidos" HeaderText="GL Total Abast." DataFormatString="{0:#,##0.##}" />
                            <asp:BoundField DataField="GLAlFinalizar" HeaderText="GL Finalizar" DataFormatString="{0:#,##0.##}" />
                            <asp:BoundField DataField="GLTotalConsumidos" HeaderText="GL Consumidos" DataFormatString="{0:#,##0.##}" />
                            <asp:BoundField DataField="DistanciaKM" HeaderText="Distancia KM" DataFormatString="{0:#,##0.#}" />
                            <asp:BoundField DataField="ConsumoComputador" HeaderText="Consumo Comp." DataFormatString="{0:#,##0.#}" />
                            <asp:TemplateField HeaderText="Observaciones">
                                <ItemTemplate>
                                    <%# FormatObservaciones(Eval("Observaciones")) %>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
            </div>
        </asp:Panel>

        <!-- Estado vacio -->
        <asp:Panel ID="pnlSinResultados" runat="server" Visible="true">
            <div class="empty-state">
                <div class="empty-icon"><i class="fas fa-file-alt"></i></div>
                <h5>Seleccione un rango de fechas</h5>
                <p>Use los filtros para generar el reporte de abastecimiento.</p>
            </div>
        </asp:Panel>
    </div>
</asp:Content>

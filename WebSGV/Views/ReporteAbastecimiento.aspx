<%@ Page Title="Reporte de Abastecimiento" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReporteAbastecimiento.aspx.cs" Inherits="WebSGV.Views.ReporteAbastecimiento" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        :root {
            --primary-color: #0056b3;
            --secondary-color: #0062cc;
            --accent-color: #f0f7ff;
        }

        .report-container {
            background-color: white;
            border-radius: 6px;
            box-shadow: 0 0.5rem 1rem rgba(0, 0, 0, 0.1);
            padding: 20px;
            margin-bottom: 30px;
        }

        /* === ENCABEZADO === */
        .header-bar {
            display: flex;
            align-items: center;
            justify-content: space-between;
            border-bottom: 2px solid var(--primary-color);
            padding-bottom: 12px;
            margin-bottom: 16px;
        }

        .header-bar h3 {
            color: var(--primary-color);
            font-size: 1.3rem;
            font-weight: 700;
            margin: 0;
        }

        .header-bar .header-meta {
            display: flex;
            align-items: center;
            gap: 12px;
            font-size: 0.82rem;
            color: #6c757d;
        }

        .header-bar .header-meta .meta-count {
            background-color: var(--accent-color);
            border: 1px solid #d1e7ff;
            border-radius: 20px;
            padding: 3px 12px;
            font-weight: 600;
            color: var(--primary-color);
        }

        /* === FILTROS === */
        .filter-bar {
            display: flex;
            align-items: flex-end;
            gap: 10px;
            margin-bottom: 16px;
            flex-wrap: wrap;
        }

        .filter-bar .filter-group {
            display: flex;
            flex-direction: column;
            gap: 3px;
        }

        .filter-bar .filter-group label {
            font-size: 0.75rem;
            font-weight: 600;
            color: #6c757d;
            text-transform: uppercase;
            letter-spacing: 0.3px;
            margin: 0;
        }

        .filter-bar .filter-group input,
        .filter-bar .filter-group select {
            height: 36px;
            font-size: 0.85rem;
            border: 1px solid #ced4da;
            border-radius: 4px;
            padding: 0 10px;
        }

        .filter-bar .filter-group input:focus,
        .filter-bar .filter-group select:focus {
            border-color: #80bdff;
            box-shadow: 0 0 0 0.15rem rgba(0, 123, 255, 0.2);
            outline: none;
        }

        .filter-bar .btn-filter {
            height: 36px;
            padding: 0 16px;
            font-size: 0.85rem;
            border-radius: 4px;
            font-weight: 500;
            cursor: pointer;
        }

        .btn-filter-primary {
            background-color: var(--primary-color);
            border: 1px solid var(--primary-color);
            color: white;
        }

        .btn-filter-primary:hover {
            background-color: var(--secondary-color);
        }

        .btn-filter-outline {
            background-color: transparent;
            border: 1px solid #ced4da;
            color: #495057;
        }

        .btn-filter-outline:hover {
            background-color: #f8f9fa;
        }

        .btn-export {
            height: 36px;
            padding: 0 18px;
            font-size: 0.85rem;
            border-radius: 4px;
            font-weight: 600;
            background-color: #217346;
            border: 1px solid #1a5c38;
            color: white;
            cursor: pointer;
            display: inline-flex;
            align-items: center;
            gap: 6px;
            text-decoration: none;
            transition: background-color 0.2s ease, box-shadow 0.2s ease;
            line-height: 36px;
            white-space: nowrap;
        }

        .btn-export:hover {
            background-color: #1a5c38;
            color: white;
            text-decoration: none;
            box-shadow: 0 2px 6px rgba(33, 115, 70, 0.35);
        }

        .btn-export:active {
            background-color: #155d2e;
        }

        .btn-export i {
            font-size: 0.9rem;
        }

        .filter-separator {
            width: 1px;
            height: 24px;
            background-color: #dee2e6;
            align-self: flex-end;
            margin-bottom: 6px;
        }

        /* === TABLA REPORTE === */
        .table-wrapper {
            border: 1px solid #e9ecef;
            border-radius: 6px;
            overflow: hidden;
        }

        .report-table {
            margin-bottom: 0;
            font-size: 0.78rem;
            border-collapse: collapse;
            width: 100%;
        }

        .report-table thead th {
            background-color: var(--primary-color) !important;
            color: white !important;
            font-weight: 600;
            text-align: center;
            vertical-align: middle;
            padding: 8px 6px;
            border: 1px solid #004494 !important;
            text-transform: uppercase;
            letter-spacing: 0.2px;
            font-size: 0.72rem;
            white-space: nowrap;
        }

        .report-table thead th.th-group {
            background-color: #003d82 !important;
            font-size: 0.7rem;
            letter-spacing: 0.4px;
        }

        .report-table tbody td {
            vertical-align: middle;
            text-align: center;
            padding: 7px 5px;
            font-size: 0.8rem;
            border: 1px solid #e9ecef;
        }

        .report-table tbody tr {
            transition: background-color 0.15s ease;
        }

        .report-table tbody tr:hover {
            background-color: #edf5ff !important;
        }

        .report-table tbody tr:nth-child(even) {
            background-color: #fafbfc;
        }

        /* Columnas alineadas a izquierda */
        .report-table tbody td.td-left {
            text-align: left;
            font-weight: 500;
        }

        /* Columna observaciones */
        .report-table tbody td.td-obs {
            text-align: left;
            font-size: 0.72rem;
            max-width: 200px;
            white-space: pre-line;
            line-height: 1.3;
            color: #495057;
        }

        /* Numeros */
        .report-table tbody td.td-num {
            font-variant-numeric: tabular-nums;
            font-weight: 500;
        }

        /* Fila de totales */
        .report-table tfoot td {
            background-color: #f0f7ff;
            font-weight: 700;
            text-align: center;
            padding: 8px 5px;
            font-size: 0.8rem;
            border: 1px solid #d1e7ff;
            color: var(--primary-color);
        }

        /* Badge tipo vehiculo */
        .tipo-badge {
            display: inline-block;
            padding: 2px 8px;
            border-radius: 10px;
            font-size: 0.7rem;
            font-weight: 600;
        }

        .tipo-trailer { background-color: #e3f2fd; color: #1565c0; }
        .tipo-camioneta { background-color: #fff3e0; color: #e65100; }
        .tipo-camion { background-color: #e8f5e9; color: #2e7d32; }
        .tipo-otro { background-color: #f5f5f5; color: #616161; }

        /* Badge motivo salida */
        .motivo-badge {
            display: inline-block;
            padding: 2px 8px;
            border-radius: 10px;
            font-size: 0.7rem;
            font-weight: 600;
            white-space: nowrap;
        }

        .motivo-viaje { background-color: #e3f2fd; color: #1565c0; }
        .motivo-abastecimiento { background-color: #e8f5e9; color: #2e7d32; }
        .motivo-mantenimiento { background-color: #fff3e0; color: #e65100; }
        .motivo-otro { background-color: #f5f5f5; color: #616161; }

        /* Estado vacio */
        .empty-state {
            text-align: center;
            padding: 50px 20px;
            color: #6c757d;
        }

        .empty-state .empty-icon {
            font-size: 2.5rem;
            opacity: 0.3;
            margin-bottom: 12px;
        }

        .empty-state h5 {
            font-size: 1rem;
            font-weight: 600;
            color: #495057;
            margin-bottom: 6px;
        }

        .empty-state p {
            font-size: 0.85rem;
        }

        /* Resumen compacto */
        .summary-bar {
            display: flex;
            gap: 20px;
            margin-bottom: 14px;
            flex-wrap: wrap;
        }

        .summary-item {
            display: flex;
            align-items: center;
            gap: 6px;
            font-size: 0.82rem;
            color: #495057;
        }

        .summary-item .summary-value {
            font-weight: 700;
            color: var(--primary-color);
        }

        .summary-item .summary-icon {
            width: 28px;
            height: 28px;
            border-radius: 6px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 0.75rem;
            color: white;
        }

        .summary-icon.bg-blue { background-color: var(--primary-color); }
        .summary-icon.bg-green { background-color: #28a745; }
        .summary-icon.bg-orange { background-color: #e65100; }
    </style>

    <div class="container-fluid report-container">

        <!-- Encabezado -->
        <div class="header-bar">
            <h3><i class="fas fa-file-alt mr-2"></i>Reporte de Abastecimiento</h3>
            <div class="header-meta">
                <span class="meta-count">
                    <asp:Label ID="lblTotalRegistros" runat="server" Text="0"></asp:Label> registros
                </span>
            </div>
        </div>

        <!-- Filtros -->
        <div class="filter-bar">
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
                    <asp:ListItem Value="VIAJE PROGRAMADO" Text="📦 Viaje Programado"></asp:ListItem>
                    <asp:ListItem Value="ABASTECIMIENTO" Text="🚛 Abastecimiento"></asp:ListItem>
                    <asp:ListItem Value="MANTENIMIENTO" Text="🔧 Mantenimiento"></asp:ListItem>
                    <asp:ListItem Value="OTRO" Text="📋 Otro"></asp:ListItem>
                </asp:DropDownList>
            </div>
            <asp:Button ID="btnBuscar" runat="server" CssClass="btn-filter btn-filter-primary" Text="Buscar" OnClick="btnBuscar_Click" />
            <asp:Button ID="btnLimpiar" runat="server" CssClass="btn-filter btn-filter-outline" Text="Limpiar" OnClick="btnLimpiar_Click" />
            <div class="filter-separator"></div>
            <asp:LinkButton ID="btnExportarExcel" runat="server" CssClass="btn-export" OnClick="btnExportarExcel_Click" CausesValidation="false">
                <i class="fas fa-file-excel"></i> Exportar Excel
            </asp:LinkButton>
        </div>

        <!-- Resumen -->
        <asp:Panel ID="pnlResumen" runat="server" Visible="false">
            <div class="summary-bar">
                <div class="summary-item">
                    <div class="summary-icon bg-blue"><i class="fas fa-gas-pump"></i></div>
                    <div>
                        <div style="font-size:0.7rem;color:#6c757d;">GL Total Abastecidos</div>
                        <div class="summary-value"><asp:Label ID="lblTotalGLAbastecidos" runat="server" Text="0"></asp:Label></div>
                    </div>
                </div>
                <div class="summary-item">
                    <div class="summary-icon bg-green"><i class="fas fa-tachometer-alt"></i></div>
                    <div>
                        <div style="font-size:0.7rem;color:#6c757d;">GL Total Consumidos</div>
                        <div class="summary-value"><asp:Label ID="lblTotalGLConsumidos" runat="server" Text="0"></asp:Label></div>
                    </div>
                </div>
                <div class="summary-item">
                    <div class="summary-icon bg-orange"><i class="fas fa-road"></i></div>
                    <div>
                        <div style="font-size:0.7rem;color:#6c757d;">KM Total Recorridos</div>
                        <div class="summary-value"><asp:Label ID="lblTotalKM" runat="server" Text="0"></asp:Label></div>
                    </div>
                </div>
            </div>
        </asp:Panel>

        <!-- Tabla de reporte -->
        <asp:Panel ID="pnlReporte" runat="server" Visible="false">
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

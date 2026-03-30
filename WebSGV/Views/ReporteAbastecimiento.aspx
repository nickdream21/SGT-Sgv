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

        /* ============================================
           MOBILE-FIRST RESPONSIVE
           ============================================ */

        /* Gradient Header */
        .rpt-header {
            background: linear-gradient(135deg, #0056b3 0%, #2980e8 100%);
            color: white;
            padding: 16px 20px;
            border-radius: 6px 6px 0 0;
            margin: -20px -20px 20px -20px;
        }

        .rpt-header h3 {
            color: white;
            font-size: 1.15rem;
            font-weight: 700;
            margin: 0 0 6px 0;
        }

        .rpt-header .rpt-meta {
            display: flex;
            align-items: center;
            gap: 8px;
            font-size: 0.82rem;
            opacity: 0.92;
        }

        .rpt-header .rpt-meta .meta-count {
            background: rgba(255,255,255,0.2);
            border-radius: 20px;
            padding: 3px 12px;
            font-weight: 600;
            font-size: 0.8rem;
        }

        /* Filter Section Card */
        .rpt-filter-card {
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 0.5rem;
            padding: 16px;
            margin-bottom: 16px;
        }

        .rpt-filter-card .filter-title {
            font-size: 0.78rem;
            font-weight: 600;
            color: #6c757d;
            text-transform: uppercase;
            letter-spacing: 0.4px;
            margin-bottom: 12px;
        }

        .rpt-filter-row {
            display: flex;
            align-items: flex-end;
            gap: 10px;
            flex-wrap: wrap;
        }

        .rpt-filter-actions {
            display: flex;
            align-items: center;
            gap: 8px;
            flex-wrap: wrap;
            margin-top: 12px;
        }

        /* Summary cards improved */
        .summary-cards-row {
            display: flex;
            gap: 12px;
            margin-bottom: 16px;
            flex-wrap: wrap;
        }

        .summary-card {
            display: flex;
            align-items: center;
            gap: 12px;
            background: white;
            border: 1px solid #e2e8f0;
            border-radius: 0.5rem;
            padding: 14px 18px;
            flex: 1;
            min-width: 180px;
            transition: box-shadow 0.2s;
        }

        .summary-card:hover {
            box-shadow: 0 2px 8px rgba(0,0,0,0.08);
        }

        .summary-card .sc-icon {
            width: 40px;
            height: 40px;
            border-radius: 10px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 0.9rem;
            color: white;
            flex-shrink: 0;
        }

        .summary-card .sc-label {
            font-size: 0.72rem;
            color: #6c757d;
            font-weight: 500;
            text-transform: uppercase;
            letter-spacing: 0.02em;
        }

        .summary-card .sc-value {
            font-size: 1.15rem;
            font-weight: 700;
            color: #0056b3;
            margin-top: 2px;
        }

        /* Scroll hint for mobile table */
        .scroll-hint {
            display: none;
            align-items: center;
            justify-content: center;
            gap: 6px;
            padding: 8px;
            font-size: 0.75rem;
            color: #6c757d;
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-bottom: none;
            border-radius: 6px 6px 0 0;
        }

        .scroll-hint i {
            animation: scrollHint 1.5s ease-in-out infinite;
        }

        @keyframes scrollHint {
            0%, 100% { transform: translateX(0); }
            50% { transform: translateX(6px); }
        }

        /* Table scroll wrapper */
        .table-scroll-wrapper {
            position: relative;
        }

        .table-scroll-wrapper::after {
            content: '';
            position: absolute;
            right: 0;
            top: 0;
            bottom: 0;
            width: 24px;
            background: linear-gradient(to left, rgba(0,0,0,0.05), transparent);
            pointer-events: none;
            border-radius: 0 6px 6px 0;
            display: none;
        }

        /* === RESPONSIVE MOBILE === */
        @media (max-width: 768px) {
            /* Container flush */
            .report-container {
                padding: 0;
                border-radius: 0;
                box-shadow: none;
                margin-bottom: 0;
            }

            /* Header gradient flush */
            .rpt-header {
                border-radius: 0;
                margin: 0;
                padding: 16px;
            }

            /* Filter card flush */
            .rpt-filter-card {
                border-radius: 0;
                border-left: none;
                border-right: none;
                padding: 12px;
                margin-bottom: 0;
            }

            .rpt-filter-row {
                flex-direction: column;
                gap: 10px;
            }

            .rpt-filter-row .filter-group {
                width: 100% !important;
                min-width: 0 !important;
            }

            .rpt-filter-row .filter-group input,
            .rpt-filter-row .filter-group select {
                width: 100%;
                min-width: 0 !important;
                height: 42px !important;
                font-size: 0.9rem !important;
            }

            .rpt-filter-actions {
                width: 100%;
                flex-direction: column;
            }

            .rpt-filter-actions .btn-filter,
            .rpt-filter-actions .btn-export {
                width: 100%;
                height: 44px;
                font-size: 0.9rem;
                justify-content: center;
                text-align: center;
            }

            .filter-separator {
                display: none;
            }

            /* Summary cards stacked */
            .summary-cards-row {
                flex-direction: column;
                gap: 8px;
                padding: 12px;
            }

            .summary-card {
                min-width: 0;
            }

            /* Scroll hint visible */
            .scroll-hint {
                display: flex;
                border-radius: 0;
                border-left: none;
                border-right: none;
                margin-top: 12px;
            }

            .table-scroll-wrapper::after {
                display: block;
            }

            .table-wrapper {
                border-radius: 0;
                border-left: none;
                border-right: none;
            }

            /* Sticky first column */
            .report-table thead th:first-child,
            .report-table tbody td:first-child,
            .report-table tfoot td:first-child {
                position: sticky;
                left: 0;
                z-index: 1;
            }

            .report-table thead th:first-child {
                z-index: 3;
                background-color: var(--primary-color) !important;
            }

            .report-table tbody td:first-child {
                background-color: white;
                box-shadow: 2px 0 4px rgba(0,0,0,0.06);
            }

            .report-table tbody tr:nth-child(even) td:first-child {
                background-color: #fafbfc;
            }

            .report-table tbody tr:hover td:first-child {
                background-color: #edf5ff;
            }

            .report-table tfoot td:first-child {
                background-color: #f0f7ff;
                z-index: 2;
            }

            /* Larger touch targets in table */
            .report-table tbody td {
                padding: 10px 8px;
                font-size: 0.78rem;
                white-space: nowrap;
            }

            .report-table thead th {
                padding: 10px 8px;
                font-size: 0.68rem;
            }

            /* Empty state compact */
            .empty-state {
                padding: 40px 16px;
            }

            .empty-state .empty-icon {
                font-size: 2rem;
            }

            /* Buttons touch-friendly */
            .btn-filter {
                min-height: 42px;
            }
        }

        /* === DESKTOP OVERRIDES === */
        @media (min-width: 769px) {
            .rpt-header {
                border-radius: 6px 6px 0 0;
                margin: -20px -20px 20px -20px;
            }

            .rpt-header h3 {
                font-size: 1.3rem;
            }
        }
    </style>

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

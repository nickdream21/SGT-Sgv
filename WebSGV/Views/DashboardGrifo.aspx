<%@ Page Title="Dashboard - Administrador de Grifo" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DashboardGrifo.aspx.cs" Inherits="WebSGV.Views.DashboardGrifo" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        :root {
            --primary-color: #0056b3;
            --secondary-color: #0062cc;
            --accent-color: #f0f7ff;
            --border-color: #dee2e6;
        }

        .dashboard-container {
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
            gap: 16px;
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

        /* === BARRA DE FILTRO INLINE === */
        .filter-bar {
            display: flex;
            align-items: center;
            gap: 10px;
            margin-bottom: 14px;
            flex-wrap: wrap;
        }

        .filter-bar .filter-input {
            flex: 1;
            min-width: 200px;
            max-width: 320px;
            position: relative;
        }

        .filter-bar .filter-input input {
            padding-left: 34px;
            height: 36px;
            font-size: 0.88rem;
            border: 1px solid #ced4da;
            border-radius: 4px;
            width: 100%;
        }

        .filter-bar .filter-input input:focus {
            border-color: #80bdff;
            box-shadow: 0 0 0 0.15rem rgba(0, 123, 255, 0.2);
            outline: none;
        }

        .filter-bar .filter-input .search-icon {
            position: absolute;
            left: 10px;
            top: 50%;
            transform: translateY(-50%);
            color: #adb5bd;
            font-size: 0.85rem;
            pointer-events: none;
        }

        .filter-bar select {
            height: 36px;
            font-size: 0.88rem;
            border: 1px solid #ced4da;
            border-radius: 4px;
            padding: 0 10px;
            min-width: 150px;
        }

        .filter-bar .btn-filter {
            height: 36px;
            padding: 0 16px;
            font-size: 0.85rem;
            border-radius: 4px;
            font-weight: 500;
        }

        .filter-bar .btn-filter-primary {
            background-color: var(--primary-color);
            border-color: var(--primary-color);
            color: white;
        }

        .filter-bar .btn-filter-primary:hover {
            background-color: var(--secondary-color);
        }

        .filter-bar .btn-filter-outline {
            background-color: transparent;
            border: 1px solid #ced4da;
            color: #495057;
        }

        .filter-bar .btn-filter-outline:hover {
            background-color: #f8f9fa;
        }

        .filter-bar .filter-separator {
            width: 1px;
            height: 24px;
            background-color: #dee2e6;
        }

        /* === TABLA === */
        .table-wrapper {
            border: 1px solid #e9ecef;
            border-radius: 6px;
            overflow: hidden;
        }

        .table-viajes {
            margin-bottom: 0;
        }

        .table-viajes thead th {
            background-color: var(--primary-color) !important;
            color: white !important;
            font-weight: 600;
            font-size: 0.82rem;
            text-align: center;
            vertical-align: middle;
            padding: 10px 8px;
            border: none !important;
            text-transform: uppercase;
            letter-spacing: 0.3px;
        }

        .table-viajes tbody td {
            vertical-align: middle;
            text-align: center;
            padding: 9px 8px;
            font-size: 0.88rem;
            border-color: #f0f0f0;
        }

        .table-viajes tbody tr {
            transition: background-color 0.15s ease;
        }

        .table-viajes tbody tr:hover {
            background-color: #edf5ff !important;
        }

        .table-viajes tbody tr:nth-child(even) {
            background-color: #fafbfc;
        }

        /* Columna conductor alineada a izquierda */
        .table-viajes tbody td.td-conductor {
            text-align: left;
            font-weight: 500;
        }

        /* Badge destino */
        .destino-badge {
            display: inline-block;
            padding: 3px 10px;
            border-radius: 12px;
            font-size: 0.78rem;
            font-weight: 600;
            letter-spacing: 0.2px;
        }

        .destino-frontera {
            background-color: #fce4ec;
            color: #c62828;
        }

        .destino-trujillo {
            background-color: #e3f2fd;
            color: #1565c0;
        }

        .destino-default {
            background-color: #f5f5f5;
            color: #616161;
        }

        /* Dias en viaje */
        .dias-badge {
            display: inline-block;
            min-width: 28px;
            padding: 2px 8px;
            border-radius: 10px;
            font-size: 0.82rem;
            font-weight: 600;
            text-align: center;
        }

        .dias-ok {
            background-color: #e8f5e9;
            color: #2e7d32;
        }

        .dias-warning {
            background-color: #fff3e0;
            color: #e65100;
        }

        .dias-danger {
            background-color: #fce4ec;
            color: #c62828;
        }

        /* Boton abastecer */
        .btn-abastecer {
            display: inline-flex;
            align-items: center;
            gap: 5px;
            background-color: #28a745;
            border: none;
            color: white;
            padding: 5px 14px;
            font-size: 0.82rem;
            font-weight: 600;
            border-radius: 4px;
            text-decoration: none;
            transition: background-color 0.15s ease;
        }

        .btn-abastecer:hover {
            background-color: #218838;
            color: white;
            text-decoration: none;
        }

        /* Link manual */
        .btn-manual-link {
            color: var(--primary-color);
            font-size: 0.82rem;
            text-decoration: none;
            font-weight: 500;
        }

        .btn-manual-link:hover {
            text-decoration: underline;
        }

        /* Boton crear abastecimiento */
        .btn-crear-abastecimiento {
            display: inline-flex;
            align-items: center;
            gap: 4px;
            height: 36px;
            padding: 0 16px;
            font-size: 0.85rem;
            font-weight: 600;
            border-radius: 4px;
            background-color: #ff8f00;
            border: none;
            color: white;
            text-decoration: none;
            transition: background-color 0.15s ease;
        }

        .btn-crear-abastecimiento:hover {
            background-color: #ff6f00;
            color: white;
            text-decoration: none;
        }

        /* === ESTADO VACIO === */
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
            margin-bottom: 16px;
        }

        /* Labels ocultas para accesibilidad */
        .sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0,0,0,0); border: 0; }

        /* === TABS DE NAVEGACIÓN === */
        .tabs-navigation {
            display: flex;
            gap: 0;
            background: white;
            border: 1px solid var(--border-color);
            border-radius: 0.5rem;
            overflow: hidden;
            margin-bottom: 20px;
        }

        .tab-btn {
            flex: 1;
            padding: 0.875rem 1.25rem;
            border: none;
            background: white;
            color: #6c757d;
            font-weight: 600;
            font-size: 0.9375rem;
            cursor: pointer;
            transition: all 0.2s;
            border-bottom: 3px solid transparent;
        }

        .tab-btn:hover {
            background: #f8f9fa;
            color: var(--primary-color);
        }

        .tab-btn-active {
            color: var(--primary-color);
            border-bottom-color: var(--primary-color);
            background: var(--accent-color);
        }
    </style>

    <div class="container-fluid px-4">

        <%-- Labels ocultas que mantienen compatibilidad con el code-behind --%>
        <asp:Label ID="lblAbastecimientosHoy" runat="server" Text="0" CssClass="sr-only"></asp:Label>
        <asp:Label ID="lblGalonesHoy" runat="server" Text="0" CssClass="sr-only"></asp:Label>

        <!-- TABS DE NAVEGACIÓN -->
        <div class="tabs-navigation">
            <button type="button" class="tab-btn tab-btn-active" id="tabViajes" onclick="cambiarTabGrifo('viajes')">
                <i class="fas fa-gas-pump mr-2"></i>Viajes Activos
                <span class="meta-count ml-2">
                    <asp:Label ID="lblTotalViajesActivos" runat="server" Text="0"></asp:Label>
                </span>
            </button>
            <button type="button" class="tab-btn" id="tabHistorial" onclick="cambiarTabGrifo('historial')">
                <i class="fas fa-history mr-2"></i>Historial de Abastecimientos
                <span class="meta-count ml-2">
                    <asp:Label ID="lblTotalAbastecimientos" runat="server" Text="0"></asp:Label>
                </span>
            </button>
        </div>

        <!-- SECCIÓN VIAJES ACTIVOS -->
        <div id="seccionViajes">

    <div class="container-fluid dashboard-container">

        <!-- Encabezado -->
        <div class="header-bar">
            <h3><i class="fas fa-gas-pump mr-2"></i>Viajes Activos</h3>
            <div class="header-meta">
                <span><i class="fas fa-calendar-alt mr-1"></i><%= DateTime.Now.ToString("dd/MM/yyyy HH:mm") %></span>
            </div>
        </div>

        <!-- Filtro inline -->
        <div class="filter-bar">
            <div class="filter-input">
                <i class="fas fa-search search-icon"></i>
                <asp:TextBox ID="txtBuscarConductor" runat="server" CssClass="form-control" placeholder="Buscar conductor o DNI..."></asp:TextBox>
            </div>
            <asp:DropDownList ID="ddlEstado" runat="server" CssClass="form-control">
                <asp:ListItem Value="ABIERTO" Selected="True">Abiertos</asp:ListItem>
                <asp:ListItem Value="ABASTECIDO">Abastecidos</asp:ListItem>
                <asp:ListItem Value="TODOS">Todos</asp:ListItem>
            </asp:DropDownList>
            <asp:Button ID="btnFiltrar" runat="server" CssClass="btn-filter btn-filter-primary" Text="Filtrar" OnClick="btnFiltrar_Click" />
            <div class="filter-separator"></div>
            <asp:Button ID="btnRefrescar" runat="server" CssClass="btn-filter btn-filter-outline" Text="Refrescar" OnClick="btnRefrescar_Click" />
            <div class="filter-separator"></div>
            <a href="AgregarAbastecimiento.aspx" class="btn-crear-abastecimiento">
                <i class="fas fa-plus-circle mr-1"></i>Crear Abastecimiento
            </a>
        </div>

        <!-- Tabla de viajes -->
        <asp:Panel ID="pnlViajes" runat="server">
            <div class="table-wrapper">
                <div class="table-responsive">
                    <asp:GridView ID="gvViajesActivos" runat="server" CssClass="table table-viajes mb-0"
                        AutoGenerateColumns="false" EmptyDataText="" ShowHeaderWhenEmpty="false"
                        OnRowCommand="gvViajesActivos_RowCommand" OnRowDataBound="gvViajesActivos_RowDataBound">
                        <Columns>
                            <asp:BoundField DataField="NumeroViajeProgreso" HeaderText="Nro. Viaje" />
                            <asp:BoundField DataField="Conductor" HeaderText="Conductor" />
                            <asp:BoundField DataField="DNI" HeaderText="DNI" />
                            <asp:BoundField DataField="PlacaTracto" HeaderText="Tracto" />
                            <asp:BoundField DataField="PlacaCarreta" HeaderText="Carreta" />
                            <asp:BoundField DataField="Cliente" HeaderText="Cliente" />
                            <asp:TemplateField HeaderText="Destino">
                                <ItemTemplate>
                                    <%# FormatDestinoBadge(Eval("Destino")) %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="FechaInicio" HeaderText="Inicio" DataFormatString="{0:dd/MM/yyyy}" />
                            <asp:TemplateField HeaderText="Dias">
                                <ItemTemplate>
                                    <%# FormatDiasBadge(Eval("DiasEnViaje")) %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="">
                                <ItemTemplate>
                                    <asp:LinkButton ID="btnRegistrar" runat="server"
                                        CssClass="btn-abastecer"
                                        CommandName="RegistrarAbastecimiento"
                                        CommandArgument='<%# Eval("IdViaje") + "|" + Eval("IdConductor") + "|" + Eval("IdTracto") + "|" + Eval("IdCarreta") %>'>
                                        <i class="fas fa-gas-pump"></i> Abastecer
                                    </asp:LinkButton>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
        </asp:Panel>

        <asp:Panel ID="pnlSinViajes" runat="server" Visible="false">
            <div class="empty-state">
                <div class="empty-icon"><i class="fas fa-truck"></i></div>
                <h5>Sin viajes pendientes</h5>
                <p>No hay viajes abiertos en este momento. Puede crear un abastecimiento manualmente.</p>
                <a href="AgregarAbastecimiento.aspx" class="btn-crear-abastecimiento" style="display:inline-flex;">
                    <i class="fas fa-plus-circle mr-1"></i> Crear Abastecimiento
                </a>
            </div>
        </asp:Panel>
    </div>

        </div><!-- /seccionViajes -->

        <!-- SECCIÓN HISTORIAL DE ABASTECIMIENTOS -->
        <div id="seccionHistorial" style="display:none;">

    <div class="container-fluid dashboard-container">
        <div class="header-bar">
            <h3><i class="fas fa-history mr-2"></i>Historial de Abastecimientos</h3>
            <div class="header-meta">
                <span><i class="fas fa-calendar-alt mr-1"></i><%= DateTime.Now.ToString("dd/MM/yyyy HH:mm") %></span>
            </div>
        </div>

        <!-- Filtros del historial -->
        <div class="filter-bar">
            <div class="filter-group" style="display:flex; flex-direction:column; gap:2px;">
                <label style="font-size:0.78rem; color:#6c757d; font-weight:500; margin:0;">Desde</label>
                <asp:TextBox ID="txtFechaDesde" runat="server" CssClass="form-control" TextMode="Date" style="height:36px; font-size:0.88rem; min-width:150px;"></asp:TextBox>
            </div>
            <div class="filter-group" style="display:flex; flex-direction:column; gap:2px;">
                <label style="font-size:0.78rem; color:#6c757d; font-weight:500; margin:0;">Hasta</label>
                <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control" TextMode="Date" style="height:36px; font-size:0.88rem; min-width:150px;"></asp:TextBox>
            </div>
            <div class="filter-input">
                <i class="fas fa-search search-icon"></i>
                <asp:TextBox ID="txtBuscarAbastecimiento" runat="server" CssClass="form-control" placeholder="Buscar conductor o placa..."></asp:TextBox>
            </div>
            <asp:Button ID="btnFiltrarHistorial" runat="server" CssClass="btn-filter btn-filter-primary" Text="Buscar" OnClick="btnFiltrarHistorial_Click" />
            <div class="filter-separator"></div>
            <asp:Button ID="btnLimpiarHistorial" runat="server" CssClass="btn-filter btn-filter-outline" Text="Limpiar" OnClick="btnLimpiarHistorial_Click" />
        </div>

        <!-- Tabla de abastecimientos -->
        <asp:Panel ID="pnlAbastecimientos" runat="server">
            <div class="table-wrapper">
                <div class="table-responsive">
                    <asp:GridView ID="gvAbastecimientos" runat="server" CssClass="table table-viajes mb-0"
                        AutoGenerateColumns="false" EmptyDataText="" ShowHeaderWhenEmpty="false"
                        OnRowCommand="gvAbastecimientos_RowCommand" OnRowDataBound="gvAbastecimientos_RowDataBound"
                        AllowPaging="true" PageSize="15" OnPageIndexChanging="gvAbastecimientos_PageIndexChanging">
                        <PagerSettings Mode="NumericFirstLast" FirstPageText="&laquo;" LastPageText="&raquo;" PageButtonCount="5" />
                        <PagerStyle CssClass="gridview-pager" HorizontalAlign="Center" />
                        <Columns>
                            <asp:BoundField DataField="IdAbastecimiento" HeaderText="ID" />
                            <asp:BoundField DataField="FechaHora" HeaderText="Fecha" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                            <asp:BoundField DataField="Conductor" HeaderText="Conductor" />
                            <asp:BoundField DataField="PlacaTracto" HeaderText="Tracto" />
                            <asp:BoundField DataField="PlacaCarreta" HeaderText="Carreta" />
                            <asp:BoundField DataField="Lugar" HeaderText="Lugar" />
                            <asp:BoundField DataField="GLAbastecidos" HeaderText="GL Total" DataFormatString="{0:N2}" />
                            <asp:BoundField DataField="GLConsumidos" HeaderText="GL Cons." DataFormatString="{0:N2}" />
                            <asp:BoundField DataField="MontoTotal" HeaderText="Monto USD" DataFormatString="{0:N2}" />
                            <asp:BoundField DataField="Rendimiento" HeaderText="KM/GL" DataFormatString="{0:N2}" />
                            <asp:TemplateField HeaderText="">
                                <ItemTemplate>
                                    <asp:LinkButton ID="btnVerDetalle" runat="server"
                                        CssClass="btn-ver-detalle"
                                        CommandName="VerDetalle"
                                        CommandArgument='<%# Eval("NumeroAbastecimiento") %>'>
                                        <i class="fas fa-eye"></i> Ver
                                    </asp:LinkButton>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
        </asp:Panel>

        <asp:Panel ID="pnlSinAbastecimientos" runat="server" Visible="false">
            <div class="empty-state">
                <div class="empty-icon"><i class="fas fa-gas-pump"></i></div>
                <h5>Sin registros</h5>
                <p>No se encontraron abastecimientos con los filtros seleccionados.</p>
            </div>
        </asp:Panel>
    </div>

        </div><!-- /seccionHistorial -->

    </div><!-- /container-fluid -->

    <script type="text/javascript">
        function cambiarTabGrifo(tab) {
            if (tab === 'viajes') {
                document.getElementById('seccionViajes').style.display = '';
                document.getElementById('seccionHistorial').style.display = 'none';
                document.getElementById('tabViajes').className = 'tab-btn tab-btn-active';
                document.getElementById('tabHistorial').className = 'tab-btn';
            } else {
                document.getElementById('seccionViajes').style.display = 'none';
                document.getElementById('seccionHistorial').style.display = '';
                document.getElementById('tabViajes').className = 'tab-btn';
                document.getElementById('tabHistorial').className = 'tab-btn tab-btn-active';
            }
        }
    </script>

    <style>
        .btn-ver-detalle {
            display: inline-flex;
            align-items: center;
            gap: 4px;
            background-color: var(--primary-color);
            border: none;
            color: white;
            padding: 4px 12px;
            font-size: 0.8rem;
            font-weight: 500;
            border-radius: 4px;
            text-decoration: none;
            transition: background-color 0.15s ease;
        }

        .btn-ver-detalle:hover {
            background-color: var(--secondary-color);
            color: white;
            text-decoration: none;
        }

        .gridview-pager {
            padding: 10px;
            background-color: #f8f9fa;
        }

        .gridview-pager a, .gridview-pager span {
            padding: 4px 10px;
            margin: 0 2px;
            border: 1px solid #dee2e6;
            border-radius: 4px;
            font-size: 0.85rem;
            text-decoration: none;
            color: var(--primary-color);
        }

        .gridview-pager span {
            background-color: var(--primary-color);
            color: white;
            border-color: var(--primary-color);
        }

        .gridview-pager a:hover {
            background-color: #e9ecef;
        }
    </style>
</asp:Content>
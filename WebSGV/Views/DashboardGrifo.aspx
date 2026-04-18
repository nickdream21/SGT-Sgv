<%@ Page Title="Dashboard - Administrador de Grifo" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DashboardGrifo.aspx.cs" Inherits="WebSGV.Views.DashboardGrifo" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfTabActiva" runat="server" ClientIDMode="Static" Value="viajes" />

    <style>
        /* ============================================
           DASHBOARD GRIFO - Mobile First
           ============================================ */
        :root {
            --gf-primary: #1565c0;
            --gf-secondary: #1976d2;
            --gf-accent: #e3f2fd;
            --gf-light: #f5f9ff;
            --gf-border: #bbdefb;
            --gf-success: #28a745;
        }

        .dash-gf {
            background: #f5f5f5;
            min-height: 100vh;
            padding-bottom: 30px;
        }

        /* --- HEADER --- */
        .gf-header {
            background: linear-gradient(135deg, var(--gf-primary) 0%, var(--gf-secondary) 100%);
            color: white;
            padding: 16px;
        }

        .gf-header h2 {
            font-size: 1.15rem;
            font-weight: 700;
            margin: 0 0 4px 0;
        }

        .gf-header .gf-sub {
            font-size: 0.82rem;
            opacity: 0.9;
            margin: 0;
        }

        .gf-fecha-badge {
            display: inline-block;
            background: rgba(255,255,255,0.2);
            border-radius: 20px;
            padding: 4px 12px;
            font-size: 0.78rem;
            margin-top: 6px;
        }

        /* --- TABS --- */
        .gf-tabs {
            display: flex;
            gap: 0;
            margin: 0;
            background: white;
            overflow: hidden;
            box-shadow: 0 2px 8px rgba(0,0,0,0.06);
        }

        .gf-tab {
            flex: 1;
            padding: 12px 8px;
            border: none;
            background: white;
            color: #999;
            font-weight: 600;
            font-size: 0.82rem;
            cursor: pointer;
            border-bottom: 3px solid transparent;
            transition: all 0.2s;
            text-align: center;
            touch-action: manipulation;
        }

        .gf-tab:hover {
            background: #f8f9fa;
            color: var(--gf-primary);
        }

        .gf-tab.active {
            color: var(--gf-primary);
            border-bottom-color: var(--gf-primary);
            background: var(--gf-accent);
        }

        .tab-badge {
            display: inline-block;
            background: #e0e0e0;
            color: #666;
            border-radius: 10px;
            padding: 1px 8px;
            font-size: 0.72rem;
            margin-left: 4px;
        }

        .gf-tab.active .tab-badge {
            background: var(--gf-primary);
            color: white;
        }

        /* --- FILTER --- */
        .gf-filter {
            background: white;
            padding: 12px;
            border-bottom: 1px solid #eee;
            margin: 0 0 8px 0;
        }

        .gf-filter-row {
            display: flex;
            flex-direction: column;
            gap: 8px;
        }

        .gf-filter-input {
            position: relative;
            flex: 1;
        }

        .gf-filter-input .fa-search {
            position: absolute;
            left: 10px;
            top: 50%;
            transform: translateY(-50%);
            color: #adb5bd;
            font-size: 0.85rem;
            pointer-events: none;
        }

        .gf-filter-input input {
            padding-left: 34px !important;
        }

        .gf-filter .form-control {
            height: 42px;
            font-size: 0.88rem;
            border-radius: 8px;
            border: 1px solid #ddd;
        }

        .gf-filter .form-control:focus {
            border-color: var(--gf-primary);
            box-shadow: 0 0 0 2px rgba(21,101,192,0.15);
        }

        .gf-filter-actions {
            display: flex;
            gap: 8px;
        }

        .gf-filter-label {
            font-size: 0.75rem;
            color: #888;
            margin-bottom: 2px;
            font-weight: 500;
        }

        /* --- BUTTONS --- */
        .btn-gf-primary {
            background: var(--gf-primary);
            color: white;
            border: none;
            border-radius: 8px;
            padding: 10px 16px;
            font-size: 0.88rem;
            font-weight: 600;
            cursor: pointer;
            flex: 1;
            touch-action: manipulation;
        }

        .btn-gf-outline {
            background: white;
            color: #666;
            border: 1px solid #ddd;
            border-radius: 8px;
            padding: 10px 16px;
            font-size: 0.88rem;
            font-weight: 500;
            cursor: pointer;
            flex: 1;
            touch-action: manipulation;
        }

        .btn-gf-crear {
            display: block;
            background: #ff8f00;
            color: white;
            border: none;
            border-radius: 8px;
            padding: 10px 16px;
            font-size: 0.88rem;
            font-weight: 600;
            cursor: pointer;
            text-align: center;
            text-decoration: none;
            touch-action: manipulation;
            width: 100%;
        }

        .btn-gf-crear:hover {
            background: #ff6f00;
            color: white;
            text-decoration: none;
        }

        /* --- CARD LIST --- */
        .gf-card-list {
            padding: 8px 12px;
        }

        /* --- VIAJE CARD --- */
        .vj-card {
            background: white;
            border-radius: 10px;
            margin-bottom: 10px;
            box-shadow: 0 1px 4px rgba(0,0,0,0.06);
            overflow: hidden;
            border-left: 4px solid var(--gf-primary);
        }

        .vj-card-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 10px 12px 6px;
            border-bottom: 1px solid #f0f0f0;
        }

        .vj-num {
            font-weight: 700;
            color: var(--gf-primary);
            font-size: 0.88rem;
        }

        .vj-estado {
            font-size: 0.72rem;
            font-weight: 600;
            border-radius: 10px;
            padding: 2px 8px;
        }

        .vj-estado-abierto { background: #e8f5e9; color: #2e7d32; }
        .vj-estado-abastecido { background: #e3f2fd; color: #1565c0; }

        /* Fase de carga (IDA / VUELTA / COMPLETO) */
        .fase-badge {
            font-size: 0.72rem;
            font-weight: 600;
            border-radius: 10px;
            padding: 3px 9px;
            display: inline-flex;
            align-items: center;
            gap: 4px;
        }
        .fase-badge small { font-weight: 500; opacity: 0.8; }
        .fase-ida { background: #fff3e0; color: #e65100; }
        .fase-completo { background: #e3f2fd; color: #1565c0; }

        .vj-card-body { padding: 8px 12px; }

        .vj-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 6px;
        }

        .vj-label {
            font-size: 0.7rem;
            color: #999;
            text-transform: uppercase;
            letter-spacing: 0.3px;
        }

        .vj-value {
            font-size: 0.85rem;
            font-weight: 500;
            color: #333;
        }

        .vj-card-footer {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 8px 12px;
            background: #fafafa;
            border-top: 1px solid #f0f0f0;
            gap: 8px;
        }

        .vj-dias-info {
            display: flex;
            align-items: center;
            gap: 6px;
            font-size: 0.8rem;
            color: #666;
            flex-wrap: wrap;
        }

        .btn-abastecer-card {
            display: inline-flex;
            align-items: center;
            gap: 5px;
            background: var(--gf-success);
            color: white;
            border: none;
            border-radius: 8px;
            padding: 8px 16px;
            font-size: 0.85rem;
            font-weight: 600;
            text-decoration: none;
            touch-action: manipulation;
            white-space: nowrap;
            flex-shrink: 0;
        }

        .btn-abastecer-card:hover {
            background: #218838;
            color: white;
            text-decoration: none;
        }

        .btn-retorno-card {
            display: inline-flex;
            align-items: center;
            gap: 5px;
            background: #6a1b9a;
            color: white;
            border: none;
            border-radius: 8px;
            padding: 8px 16px;
            font-size: 0.85rem;
            font-weight: 600;
            text-decoration: none;
            touch-action: manipulation;
            white-space: nowrap;
            flex-shrink: 0;
        }

        .btn-retorno-card:hover {
            background: #4a148c;
            color: white;
            text-decoration: none;
        }

        /* --- BADGES --- */
        .destino-badge {
            display: inline-block;
            padding: 2px 8px;
            border-radius: 8px;
            font-size: 0.72rem;
            font-weight: 600;
        }

        .destino-frontera { background: #fce4ec; color: #c62828; }
        .destino-trujillo { background: #e3f2fd; color: #1565c0; }
        .destino-default { background: #f5f5f5; color: #616161; }

        .dias-badge {
            display: inline-block;
            min-width: 24px;
            padding: 2px 6px;
            border-radius: 8px;
            font-size: 0.78rem;
            font-weight: 700;
            text-align: center;
        }

        .dias-ok { background: #e8f5e9; color: #2e7d32; }
        .dias-warning { background: #fff3e0; color: #e65100; }
        .dias-danger { background: #fce4ec; color: #c62828; }

        /* --- ABASTECIMIENTO CARD --- */
        .ab-card {
            background: white;
            border-radius: 10px;
            margin-bottom: 10px;
            box-shadow: 0 1px 4px rgba(0,0,0,0.06);
            overflow: hidden;
        }

        .ab-card-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 10px 12px 6px;
            background: var(--gf-light);
            border-bottom: 1px solid #eef3f9;
        }

        .ab-num {
            font-weight: 700;
            color: var(--gf-primary);
            font-size: 0.85rem;
        }

        .ab-fecha {
            font-size: 0.78rem;
            color: #888;
        }

        .ab-card-body { padding: 8px 12px; }

        .ab-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 6px;
        }

        .ab-label {
            font-size: 0.7rem;
            color: #999;
            text-transform: uppercase;
        }

        .ab-value {
            font-size: 0.85rem;
            font-weight: 500;
            color: #333;
        }

        .ab-highlight {
            color: var(--gf-primary);
            font-weight: 700;
        }

        .ab-card-footer {
            display: flex;
            justify-content: flex-end;
            padding: 8px 12px;
            background: #fafafa;
            border-top: 1px solid #f0f0f0;
        }

        .btn-ver-card {
            display: inline-flex;
            align-items: center;
            gap: 4px;
            background: var(--gf-primary);
            color: white;
            border: none;
            border-radius: 8px;
            padding: 6px 14px;
            font-size: 0.82rem;
            font-weight: 500;
            text-decoration: none;
            touch-action: manipulation;
        }

        .btn-ver-card:hover {
            background: var(--gf-secondary);
            color: white;
            text-decoration: none;
        }

        /* --- EMPTY STATE --- */
        .gf-empty {
            text-align: center;
            padding: 40px 20px;
            background: white;
            margin: 0 12px;
            border-radius: 10px;
        }

        .gf-empty i {
            font-size: 2.5rem;
            color: #ddd;
            display: block;
            margin-bottom: 12px;
        }

        .gf-empty h5 {
            color: #666;
            font-size: 1rem;
            margin-bottom: 6px;
        }

        .gf-empty p {
            color: #999;
            font-size: 0.85rem;
            margin-bottom: 14px;
        }

        /* --- TAB CONTENT --- */
        .gf-tab-content { display: none; }
        .gf-tab-content.active { display: block; }

        /* --- SR ONLY --- */
        .sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0,0,0,0); border: 0; }

        /* --- DESKTOP --- */
        @media (min-width: 576px) {
            .gf-filter-row {
                flex-direction: row;
                align-items: flex-end;
            }

            .gf-filter-actions {
                flex-direction: row;
            }

            .btn-gf-primary,
            .btn-gf-outline {
                flex: 0;
                white-space: nowrap;
            }

            .btn-gf-crear {
                width: auto;
                display: inline-block;
            }
        }

        @media (min-width: 768px) {
            .gf-header h2 { font-size: 1.4rem; }

            .gf-tabs {
                max-width: 500px;
                margin: 0 auto;
                border-radius: 0;
            }

            .vj-grid { grid-template-columns: 1fr 1fr 1fr; }
            .ab-grid { grid-template-columns: 1fr 1fr 1fr; }
            .gf-card-list { padding: 12px 16px; }

            .gf-filter {
                margin: 0 12px;
                border-radius: 10px;
                margin-bottom: 8px;
            }
        }

        @media (min-width: 992px) {
            .ab-grid { grid-template-columns: 1fr 1fr 1fr 1fr; }
        }
    </style>

    <div class="dash-gf">

        <%-- Labels ocultas para compatibilidad --%>
        <asp:Label ID="lblAbastecimientosHoy" runat="server" Text="0" CssClass="sr-only"></asp:Label>
        <asp:Label ID="lblGalonesHoy" runat="server" Text="0" CssClass="sr-only"></asp:Label>

        <!-- ========== HEADER ========== -->
        <div class="gf-header">
            <h2><i class="fas fa-gas-pump mr-2"></i>Dashboard de Grifo</h2>
            <p class="gf-sub">Control de abastecimiento de combustible</p>
            <span class="gf-fecha-badge">
                <i class="fas fa-calendar-alt mr-1"></i>
                <%= DateTime.Now.ToString("dddd, dd/MM/yyyy HH:mm", new System.Globalization.CultureInfo("es-PE")) %>
            </span>
            <div style="margin-top:12px;">
                <a href="RegistrarDespachoObra.aspx" class="btn btn-light"
                   style="background:#fff;color:#e65100;font-weight:600;border:none;box-shadow:0 2px 6px rgba(0,0,0,.15);">
                    <i class="fas fa-truck-loading mr-1"></i>Registrar Despacho a Obra
                </a>
            </div>
        </div>

        <!-- ========== TABS ========== -->
        <div class="gf-tabs">
            <button type="button" class="gf-tab active" id="tabViajes" onclick="cambiarTab('viajes')">
                <i class="fas fa-truck mr-1"></i>Viajes
                <span class="tab-badge"><asp:Label ID="lblTotalViajesActivos" runat="server" Text="0"></asp:Label></span>
            </button>
            <button type="button" class="gf-tab" id="tabHistorial" onclick="cambiarTab('historial')">
                <i class="fas fa-history mr-1"></i>Historial
                <span class="tab-badge"><asp:Label ID="lblTotalAbastecimientos" runat="server" Text="0"></asp:Label></span>
            </button>
            <button type="button" class="gf-tab" id="tabRetornos" onclick="cambiarTab('retornos')">
                <i class="fas fa-globe-americas mr-1"></i>Retornos Ecuador
                <span class="tab-badge"><asp:Label ID="lblTotalRetornos" runat="server" Text="0"></asp:Label></span>
            </button>
        </div>

        <!-- ======================================================
             TAB 1: VIAJES ACTIVOS
             ====================================================== -->
        <div class="gf-tab-content active" id="seccionViajes">

            <!-- Filtro -->
            <div class="gf-filter">
                <div class="gf-filter-row">
                    <div class="gf-filter-input">
                        <i class="fas fa-search"></i>
                        <asp:TextBox ID="txtBuscarConductor" runat="server" CssClass="form-control"
                            placeholder="Buscar conductor o DNI..."></asp:TextBox>
                    </div>
                    <asp:DropDownList ID="ddlEstado" runat="server" CssClass="form-control">
                        <asp:ListItem Value="ACTIVO" Selected="True">Por abastecer</asp:ListItem>
                        <asp:ListItem Value="COMPLETO">Abastecidos</asp:ListItem>
                        <asp:ListItem Value="RETORNO_PEND">Retorno Ecuador pendiente</asp:ListItem>
                        <asp:ListItem Value="TODOS">Todos</asp:ListItem>
                    </asp:DropDownList>
                    <div class="gf-filter-actions">
                        <asp:Button ID="btnFiltrar" runat="server" CssClass="btn-gf-primary"
                            Text="Filtrar" OnClick="btnFiltrar_Click" />
                        <asp:Button ID="btnRefrescar" runat="server" CssClass="btn-gf-outline"
                            Text="Refrescar" OnClick="btnRefrescar_Click" />
                    </div>
                </div>
                <div style="margin-top: 8px;">
                    <a href="AgregarAbastecimiento.aspx" class="btn-gf-crear">
                        <i class="fas fa-plus-circle mr-1"></i>Crear Abastecimiento Manual
                    </a>
                </div>
            </div>

            <!-- Cards de viajes -->
            <asp:Panel ID="pnlViajes" runat="server">
                <div class="gf-card-list">
                    <asp:Repeater ID="rptViajesActivos" runat="server">
                        <ItemTemplate>
                            <div class="vj-card">
                                <div class="vj-card-header">
                                    <span class="vj-num">
                                        <i class="fas fa-route mr-1"></i><%# Eval("NumeroViajeProgreso") %>
                                    </span>
                                    <%# FormatFaseBadge(Eval("FaseCarga"), Eval("EsInternacional"), Eval("CargasRealizadas")) %>
                                </div>
                                <div class="vj-card-body">
                                    <div class="vj-grid">
                                        <div class="vj-item">
                                            <div class="vj-label">Conductor</div>
                                            <div class="vj-value"><%# Eval("Conductor") %></div>
                                        </div>
                                        <div class="vj-item">
                                            <div class="vj-label">DNI</div>
                                            <div class="vj-value"><%# Eval("DNI") %></div>
                                        </div>
                                        <div class="vj-item">
                                            <div class="vj-label">Tracto</div>
                                            <div class="vj-value"><%# Eval("PlacaTracto") %></div>
                                        </div>
                                        <div class="vj-item">
                                            <div class="vj-label">Carreta</div>
                                            <div class="vj-value"><%# Eval("PlacaCarreta") %></div>
                                        </div>
                                        <div class="vj-item">
                                            <div class="vj-label">Cliente</div>
                                            <div class="vj-value"><%# Eval("Cliente") %></div>
                                        </div>
                                        <div class="vj-item">
                                            <div class="vj-label">Destino</div>
                                            <div class="vj-value"><%# FormatDestinoBadge(Eval("Destino")) %></div>
                                        </div>
                                    </div>
                                </div>
                                <div class="vj-card-footer">
                                    <div class="vj-dias-info">
                                        <i class="fas fa-calendar-alt"></i>
                                        <%# Eval("FechaInicio", "{0:dd/MM/yyyy}") %>
                                        <span>&#183;</span>
                                        <%# FormatDiasBadge(Eval("DiasEnViaje")) %> d&#237;as
                                    </div>
                                    <a href='<%# "AgregarAbastecimiento.aspx?idViaje=" + Eval("IdViaje") + "&amp;idConductor=" + Eval("IdConductor") + "&amp;idTracto=" + Eval("IdTracto") + "&amp;idCarreta=" + Eval("IdCarreta") %>'
                                       class="btn-abastecer-card"
                                       style='<%# PuedeAbastecer(Eval("FaseCarga")) ? "" : "display:none;" %>'>
                                        <i class="fas fa-gas-pump"></i> Abastecer
                                    </a>
                                </div>
                            </div>
                        </ItemTemplate>
                    </asp:Repeater>
                </div>
            </asp:Panel>

            <asp:Panel ID="pnlSinViajes" runat="server" Visible="false">
                <div class="gf-empty">
                    <i class="fas fa-truck"></i>
                    <h5>Sin viajes pendientes</h5>
                    <p>No hay viajes abiertos en este momento.</p>
                    <a href="AgregarAbastecimiento.aspx" class="btn-gf-crear" style="max-width: 280px; margin: 0 auto;">
                        <i class="fas fa-plus-circle mr-1"></i>Crear Abastecimiento
                    </a>
                </div>
            </asp:Panel>

        </div>

        <!-- ======================================================
             TAB 2: HISTORIAL DE ABASTECIMIENTOS
             ====================================================== -->
        <div class="gf-tab-content" id="seccionHistorial">

            <!-- Filtros -->
            <div class="gf-filter">
                <div class="gf-filter-row">
                    <div style="flex:1;">
                        <div class="gf-filter-label">Desde</div>
                        <asp:TextBox ID="txtFechaDesde" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                    </div>
                    <div style="flex:1;">
                        <div class="gf-filter-label">Hasta</div>
                        <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                    </div>
                    <div class="gf-filter-input" style="flex:1.5;">
                        <i class="fas fa-search"></i>
                        <asp:TextBox ID="txtBuscarAbastecimiento" runat="server" CssClass="form-control"
                            placeholder="Conductor o placa..."></asp:TextBox>
                    </div>
                </div>
                <div class="gf-filter-actions" style="margin-top: 8px;">
                    <asp:Button ID="btnFiltrarHistorial" runat="server" CssClass="btn-gf-primary"
                        Text="Buscar" OnClick="btnFiltrarHistorial_Click" />
                    <asp:Button ID="btnLimpiarHistorial" runat="server" CssClass="btn-gf-outline"
                        Text="Limpiar" OnClick="btnLimpiarHistorial_Click" />
                </div>
            </div>

            <!-- Cards de historial -->
            <asp:Panel ID="pnlAbastecimientos" runat="server">
                <div class="gf-card-list">
                    <asp:Repeater ID="rptAbastecimientos" runat="server">
                        <ItemTemplate>
                            <div class="ab-card">
                                <div class="ab-card-header">
                                    <span class="ab-num">
                                        <i class="fas fa-file-alt mr-1"></i><%# Eval("NumeroAbastecimiento") %>
                                    </span>
                                    <span class="ab-fecha">
                                        <i class="fas fa-clock mr-1"></i><%# Eval("FechaHora", "{0:dd/MM/yyyy HH:mm}") %>
                                    </span>
                                </div>
                                <div class="ab-card-body">
                                    <div class="ab-grid">
                                        <div class="ab-item">
                                            <div class="ab-label">Conductor</div>
                                            <div class="ab-value"><%# Eval("Conductor") %></div>
                                        </div>
                                        <div class="ab-item">
                                            <div class="ab-label">Tracto</div>
                                            <div class="ab-value"><%# Eval("PlacaTracto") %></div>
                                        </div>
                                        <div class="ab-item">
                                            <div class="ab-label">Carreta</div>
                                            <div class="ab-value"><%# Eval("PlacaCarreta") %></div>
                                        </div>
                                        <div class="ab-item">
                                            <div class="ab-label">Lugar</div>
                                            <div class="ab-value"><%# Eval("Lugar") %></div>
                                        </div>
                                        <div class="ab-item">
                                            <div class="ab-label">GL Abastecidos</div>
                                            <div class="ab-value ab-highlight"><%# Eval("GLAbastecidos", "{0:N2}") %></div>
                                        </div>
                                        <div class="ab-item">
                                            <div class="ab-label">GL Consumidos</div>
                                            <div class="ab-value"><%# Eval("GLConsumidos", "{0:N2}") %></div>
                                        </div>
                                        <div class="ab-item">
                                            <div class="ab-label">Monto USD</div>
                                            <div class="ab-value">$<%# Eval("MontoTotal", "{0:N2}") %></div>
                                        </div>
                                        <div class="ab-item">
                                            <div class="ab-label">Rendimiento</div>
                                            <div class="ab-value"><%# Eval("Rendimiento", "{0:N2}") %> KM/GL</div>
                                        </div>
                                    </div>
                                </div>
                                <div class="ab-card-footer">
                                    <a href='<%# "BuscarAbastecimiento.aspx?numero=" + HttpUtility.UrlEncode(Eval("NumeroAbastecimiento").ToString()) %>'
                                       class="btn-ver-card">
                                        <i class="fas fa-eye"></i> Ver Detalle
                                    </a>
                                </div>
                            </div>
                        </ItemTemplate>
                    </asp:Repeater>
                </div>
            </asp:Panel>

            <asp:Panel ID="pnlSinAbastecimientos" runat="server" Visible="false">
                <div class="gf-empty">
                    <i class="fas fa-gas-pump"></i>
                    <h5>Sin registros</h5>
                    <p>No se encontraron abastecimientos con los filtros seleccionados.</p>
                </div>
            </asp:Panel>

        </div>

        <!-- ======================================================
             TAB 3: RETORNOS ECUADOR (Informativo)
             ====================================================== -->
        <div class="gf-tab-content" id="seccionRetornos">

            <div class="gf-filter">
                <div style="display:flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 8px;">
                    <div style="font-size: 0.85rem; color: #555;">
                        <i class="fas fa-info-circle mr-1"></i>
                        Registro informativo de combustible comprado en Ecuador (tickets presentados al retorno).
                    </div>
                    <div style="font-size: 0.95rem; font-weight: 600; color: #1565c0;">
                        Total acumulado: <asp:Label ID="lblTotalGalonesEcuador" runat="server" Text="0.00"></asp:Label> GL
                        &middot; $<asp:Label ID="lblTotalUsdEcuador" runat="server" Text="0.00"></asp:Label>
                    </div>
                </div>
            </div>

            <asp:Panel ID="pnlRetornos" runat="server">
                <div class="gf-card-list">
                    <asp:Repeater ID="rptRetornos" runat="server">
                        <ItemTemplate>
                            <div class="ab-card">
                                <div class="ab-card-header">
                                    <span class="ab-num">
                                        <i class="fas fa-route mr-1"></i><%# Eval("NumeroViajeProgreso") %>
                                    </span>
                                    <span class="ab-fecha">
                                        <i class="fas fa-clock mr-1"></i><%# Eval("FechaRecepcion", "{0:dd/MM/yyyy HH:mm}") %>
                                    </span>
                                </div>
                                <div class="ab-card-body">
                                    <div class="ab-grid">
                                        <div class="ab-item">
                                            <div class="ab-label">Conductor</div>
                                            <div class="ab-value"><%# Eval("Conductor") %></div>
                                        </div>
                                        <div class="ab-item">
                                            <div class="ab-label">Tracto</div>
                                            <div class="ab-value"><%# Eval("PlacaTracto") %></div>
                                        </div>
                                        <div class="ab-item">
                                            <div class="ab-label">Tickets</div>
                                            <div class="ab-value"><%# Eval("CantidadTickets") %></div>
                                        </div>
                                        <div class="ab-item">
                                            <div class="ab-label">Galones</div>
                                            <div class="ab-value ab-highlight"><%# Eval("TotalGalones", "{0:N2}") %></div>
                                        </div>
                                        <div class="ab-item">
                                            <div class="ab-label">Monto USD</div>
                                            <div class="ab-value">$<%# Eval("TotalUSD", "{0:N2}") %></div>
                                        </div>
                                        <div class="ab-item">
                                            <div class="ab-label">Observaciones</div>
                                            <div class="ab-value"><%# Eval("Observaciones") %></div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </ItemTemplate>
                    </asp:Repeater>
                </div>
            </asp:Panel>

            <asp:Panel ID="pnlSinRetornos" runat="server" Visible="false">
                <div class="gf-empty">
                    <i class="fas fa-globe-americas"></i>
                    <h5>Sin retornos registrados</h5>
                    <p>Aún no se han registrado ingresos de combustible desde Ecuador.</p>
                </div>
            </asp:Panel>

        </div>

    </div>

    <script type="text/javascript">
        function cambiarTab(tab) {
            var viajes = document.getElementById('seccionViajes');
            var historial = document.getElementById('seccionHistorial');
            var retornos = document.getElementById('seccionRetornos');
            var tabV = document.getElementById('tabViajes');
            var tabH = document.getElementById('tabHistorial');
            var tabR = document.getElementById('tabRetornos');
            var hf = document.getElementById('hfTabActiva');

            viajes.className = 'gf-tab-content';
            historial.className = 'gf-tab-content';
            retornos.className = 'gf-tab-content';
            tabV.className = 'gf-tab';
            tabH.className = 'gf-tab';
            tabR.className = 'gf-tab';

            if (tab === 'historial') {
                historial.className = 'gf-tab-content active';
                tabH.className = 'gf-tab active';
            } else if (tab === 'retornos') {
                retornos.className = 'gf-tab-content active';
                tabR.className = 'gf-tab active';
            } else {
                viajes.className = 'gf-tab-content active';
                tabV.className = 'gf-tab active';
            }
            hf.value = tab;
        }

        // Restore tab after postback
        (function () {
            var hf = document.getElementById('hfTabActiva');
            if (hf && (hf.value === 'historial' || hf.value === 'retornos')) {
                cambiarTab(hf.value);
            }
        })();
    </script>

</asp:Content>

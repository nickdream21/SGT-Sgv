<%@ Page Title="Dashboard de Exportacion" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DashboardExportacion.aspx.cs" Inherits="WebSGV.Views.Exportacion.DashboardExportacion" %>

<asp:Content ID="ContentDE" ContentPlaceHolderID="MainContent" runat="server">
    <meta charset="utf-8" />
    <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap" rel="stylesheet" />
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/chartjs-plugin-annotation@3.0.1/dist/chartjs-plugin-annotation.min.js"></script>
    <style>
        :root {
            --de-ink: #0B1426;
            --de-ink-soft: #1A2540;
            --de-bg: #F4F6FB;
            --de-card: #FFFFFF;
            --de-border: #E4E8F0;
            --de-primary: #2563EB;
            --de-primary-dark: #1D4ED8;
            --de-accent: #00D4AA;
            --de-warning: #F59E0B;
            --de-danger: #EF4444;
            --de-violet: #8B5CF6;
            --de-muted: #6B7280;
        }
        .de-wrap { font-family: 'Plus Jakarta Sans', sans-serif; background: var(--de-bg); padding: 24px; margin: -20px -15px 0; min-height: calc(100vh - 56px); }
        .de-header { display:flex; align-items:center; justify-content:space-between; margin-bottom:18px; flex-wrap:wrap; gap:12px; }
        .de-title { font-size:28px; font-weight:800; color:var(--de-ink); margin:0; letter-spacing:-0.02em; }
        .de-subtitle { font-size:14px; color:var(--de-muted); margin-top:4px; }
        .de-btn-back { background:var(--de-ink); color:#fff; border:none; padding:10px 18px; border-radius:10px; font-weight:600; font-size:14px; text-decoration:none; display:inline-flex; align-items:center; gap:8px; transition:all .2s; }
        .de-btn-back:hover { background:var(--de-primary); color:#fff; text-decoration:none; transform:translateY(-1px); }

        .de-filters { display:flex; flex-wrap:wrap; gap:10px; align-items:flex-end; background:var(--de-card); border:1px solid var(--de-border); border-radius:14px; padding:14px 18px; margin-bottom:18px; box-shadow:0 1px 3px rgba(0,0,0,0.02); }
        .de-filter { display:flex; flex-direction:column; gap:6px; min-width:140px; }
        .de-filter label { font-size:11px; text-transform:uppercase; font-weight:700; color:var(--de-muted); letter-spacing:.06em; }
        .de-filter select { padding:9px 12px; border:1px solid var(--de-border); border-radius:9px; font-size:14px; background:#fff; color:var(--de-ink); }
        .de-filter select:focus { outline:none; border-color:var(--de-primary); box-shadow:0 0 0 3px rgba(37,99,235,.1); }
        .de-btn { padding:10px 18px; border-radius:10px; font-weight:600; font-size:14px; border:none; cursor:pointer; transition:all .2s; display:inline-flex; align-items:center; gap:8px; }
        .de-btn-primary { background:var(--de-primary); color:#fff; }
        .de-btn-primary:hover { background:var(--de-primary-dark); }
        .de-btn-ghost { background:transparent; color:var(--de-muted); border:1px solid var(--de-border); }
        .de-btn-ghost:hover { background:var(--de-bg); color:var(--de-ink); }

        .de-tabs { display:flex; gap:4px; background:var(--de-card); padding:6px; border-radius:12px; border:1px solid var(--de-border); margin-bottom:18px; flex-wrap:wrap; }
        .de-tab { flex:1 1 160px; padding:10px 14px; border:none; background:transparent; border-radius:8px; font-weight:600; font-size:13px; color:var(--de-muted); cursor:pointer; transition:all .2s; }
        .de-tab.active { background:var(--de-ink); color:#fff; box-shadow:0 2px 8px rgba(11,20,38,.15); }

        .de-kpi-grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(180px,1fr)); gap:14px; margin-bottom:18px; }
        .de-kpi { background:var(--de-card); border:1px solid var(--de-border); border-radius:14px; padding:18px 18px 16px; position:relative; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,.02); }
        .de-kpi::before { content:''; position:absolute; left:0; top:0; bottom:0; width:4px; background:var(--de-primary); }
        .de-kpi.k-accent::before { background:var(--de-accent); }
        .de-kpi.k-warn::before   { background:var(--de-warning); }
        .de-kpi.k-danger::before { background:var(--de-danger); }
        .de-kpi.k-violet::before { background:var(--de-violet); }
        .de-kpi .kpi-label { font-size:11px; text-transform:uppercase; letter-spacing:.06em; color:var(--de-muted); font-weight:700; }
        .de-kpi .kpi-value { font-size:30px; font-weight:800; color:var(--de-ink); margin-top:6px; line-height:1.1; }
        .de-kpi .kpi-hint  { font-size:12px; color:var(--de-muted); margin-top:4px; }

        .de-row { display:grid; grid-template-columns:repeat(12,1fr); gap:18px; margin-bottom:18px; }
        .de-chart { background:var(--de-card); border:1px solid var(--de-border); border-radius:16px; padding:20px; box-shadow:0 4px 12px rgba(0,0,0,.03); display:flex; flex-direction:column; }
        .de-chart h3 { font-size:14px; font-weight:700; color:var(--de-ink); letter-spacing:.02em; margin:0 0 16px; display:flex; align-items:center; gap:8px; }
        .de-chart h3 i { color:var(--de-primary); width:20px; text-align:center; }
        .de-chart .canvas-wrap { position:relative; min-height:300px; flex:1; width:100%; }
        .de-chart .canvas-wrap.short { min-height:220px; }
        .de-chart .canvas-wrap.tall { min-height:360px; }

        .de-col-12 { grid-column:span 12; } .de-col-8 { grid-column:span 8; } .de-col-7 { grid-column:span 7; } .de-col-6 { grid-column:span 6; } .de-col-5 { grid-column:span 5; } .de-col-4 { grid-column:span 4; } .de-col-3 { grid-column:span 3; }
        @media (max-width: 992px) { .de-col-8,.de-col-7,.de-col-6,.de-col-5,.de-col-4,.de-col-3 { grid-column:span 12; } }

        .de-tab-panel { display:none; }
        .de-tab-panel.active { display:block; animation:deFade .25s ease; }
        @keyframes deFade { from { opacity:0; transform:translateY(6px); } to { opacity:1; transform:translateY(0); } }

        .de-alert { padding:14px 18px; border-radius:10px; font-weight:500; font-size:14px; margin-bottom:16px; border-left:4px solid; }
        .de-alert-info { background:#EFF6FF; color:#1E40AF; border-color:#2563EB; }
        .de-alert-danger { background:#FEF2F2; color:#991B1B; border-color:#EF4444; }

        .de-resumen-list { display:flex; flex-direction:column; gap:10px; padding:4px 2px; }
        .de-resumen-item { display:flex; align-items:center; justify-content:space-between; padding:12px 14px; border-radius:10px; background:var(--de-bg); border:1px solid var(--de-border); transition:all .2s; }
        .de-resumen-item:hover { transform:translateX(4px); }
        .de-resumen-item .lbl { display:flex; align-items:center; gap:10px; font-size:13px; font-weight:600; color:var(--de-ink); }
        .de-resumen-item .dot { width:10px; height:10px; border-radius:50%; display:inline-block; }
        .de-resumen-item .val { font-size:18px; font-weight:800; color:var(--de-ink); }
        .de-resumen-item .pct { font-size:11px; color:var(--de-muted); margin-left:6px; font-weight:600; }

        .de-chart h3 .badge { font-size:10px; background:var(--de-bg); color:var(--de-muted); padding:3px 8px; border-radius:6px; font-weight:600; margin-left:auto; letter-spacing:0; text-transform:none; }

        /* === Interacción dinámica === */
        .de-chip-bar { display:flex; flex-wrap:wrap; gap:8px; align-items:center; margin-bottom:14px; min-height:30px; }
        .de-chip { background:var(--de-primary); color:#fff; padding:6px 12px; border-radius:999px; font-size:12px; font-weight:600; display:inline-flex; align-items:center; gap:8px; box-shadow:0 1px 3px rgba(37,99,235,.25); animation:deChipIn .25s ease; }
        .de-chip .x { cursor:pointer; opacity:.85; }
        .de-chip .x:hover { opacity:1; }
        @keyframes deChipIn { from { opacity:0; transform:scale(.85);} to { opacity:1; transform:scale(1);} }

        .de-chart { transition: transform .2s, box-shadow .2s; }
        .de-chart:hover { box-shadow:0 8px 24px rgba(11,20,38,.08); }

        canvas { cursor: default; }
        canvas.de-clickable { cursor: pointer; }

        .de-mini-kpi { position:absolute; top:14px; right:18px; font-size:11px; color:var(--de-muted); font-weight:600; text-transform:uppercase; letter-spacing:.05em; }
        .de-mini-kpi b { font-size:14px; color:var(--de-ink); font-weight:800; margin-left:4px; }

        /* Animación KPI value */
        .kpi-value { transition: color .25s; }
        .de-kpi:hover .kpi-value { color: var(--de-primary); }

        /* === Vista diaria desplegable === */
        .de-daily { background:var(--de-card); border:1px solid var(--de-border); border-radius:16px; margin-bottom:18px; box-shadow:0 1px 3px rgba(0,0,0,.02); overflow:hidden; transition:all .25s; }
        .de-daily.disabled { opacity:.6; }
        .de-daily-toggle { width:100%; display:flex; justify-content:space-between; align-items:center; padding:14px 20px; background:transparent; border:none; cursor:pointer; font-family:inherit; font-size:15px; font-weight:700; color:var(--de-ink); text-align:left; }
        .de-daily-toggle:hover { background:var(--de-bg); }
        .de-daily-toggle .left, .de-daily-toggle .right { display:inline-flex; align-items:center; gap:10px; }
        .de-daily-toggle i:first-child { color:var(--de-primary); width:22px; }
        .de-daily-hint { font-weight:500; color:var(--de-muted); font-size:13px; margin-left:6px; }
        .de-daily-pico { font-size:12px; font-weight:600; color:var(--de-muted); background:var(--de-bg); padding:5px 10px; border-radius:8px; }
        .de-daily-pico b { color:var(--de-ink); font-weight:800; margin-left:4px; }
        .chev { transition: transform .25s; color:var(--de-muted); }
        .de-daily[data-collapsed="false"] .chev { transform: rotate(180deg); }
        .de-daily-body { max-height:0; opacity:0; padding:0 20px; transition:max-height .35s ease, opacity .25s ease, padding .25s ease; overflow:hidden; }
        .de-daily[data-collapsed="false"] .de-daily-body { max-height:720px; opacity:1; padding:8px 20px 20px; }

        .de-daily-controls { display:flex; flex-wrap:wrap; gap:12px; align-items:center; justify-content:space-between; margin-bottom:14px; }
        .de-stage-picker { display:inline-flex; align-items:center; gap:10px; }
        .de-stage-picker label { font-size:11px; text-transform:uppercase; letter-spacing:.06em; font-weight:700; color:var(--de-muted); margin:0; }
        .de-stage-select { padding:9px 14px; border:1px solid var(--de-border); border-radius:10px; background:#fff; color:var(--de-ink); font-weight:600; font-size:13.5px; min-width:280px; cursor:pointer; transition:all .2s; }
        .de-stage-select:focus { outline:none; border-color:var(--de-primary); box-shadow:0 0 0 3px rgba(37,99,235,.1); }
        .de-stage-select optgroup { font-weight:700; color:var(--de-ink); }

        .de-daily-stats { display:flex; gap:14px; flex-wrap:wrap; }
        .de-daily-stat { font-size:11px; text-transform:uppercase; letter-spacing:.05em; color:var(--de-muted); font-weight:700; }
        .de-daily-stat b { display:block; font-size:18px; color:var(--de-ink); font-weight:800; text-transform:none; letter-spacing:0; line-height:1.1; margin-top:2px; }
        .de-daily-stat.acc b { color:var(--de-primary); }
        .de-daily-stat.warn b { color:var(--de-warning); }
        .de-daily-stat.danger b { color:var(--de-danger); }

        .de-daily-foot { font-size:11.5px; color:var(--de-muted); padding-top:10px; border-top:1px dashed var(--de-border); margin-top:14px; }
        .de-daily-foot i { color:var(--de-primary); margin-right:6px; }
    </style>

    <div class="de-wrap">
        <div class="de-header">
            <div>
                <h1 class="de-title">Dashboard de Exportaci&oacute;n</h1>
                <div class="de-subtitle">An&aacute;lisis operativo de viajes Per&uacute; &rarr; Ecuador &middot; agrupado por <strong>F.H. Programaci&oacute;n</strong></div>
            </div>
            <a href="RegistroSeguimiento.aspx" class="de-btn-back">
                <i class="fas fa-arrow-left"></i> Volver a Registro
            </a>
        </div>

        <asp:Panel ID="pnlAlert" runat="server" Visible="false" CssClass="de-alert de-alert-danger">
            <asp:Literal ID="litAlert" runat="server"></asp:Literal>
        </asp:Panel>

        <!-- Filtros -->
        <div class="de-filters">
            <div class="de-filter">
                <label>Mes</label>
                <asp:DropDownList ID="ddlMes" runat="server">
                    <asp:ListItem Text="Todos" Value="0" />
                    <asp:ListItem Text="Enero" Value="1" />
                    <asp:ListItem Text="Febrero" Value="2" />
                    <asp:ListItem Text="Marzo" Value="3" />
                    <asp:ListItem Text="Abril" Value="4" />
                    <asp:ListItem Text="Mayo" Value="5" />
                    <asp:ListItem Text="Junio" Value="6" />
                    <asp:ListItem Text="Julio" Value="7" />
                    <asp:ListItem Text="Agosto" Value="8" />
                    <asp:ListItem Text="Septiembre" Value="9" />
                    <asp:ListItem Text="Octubre" Value="10" />
                    <asp:ListItem Text="Noviembre" Value="11" />
                    <asp:ListItem Text="Diciembre" Value="12" />
                </asp:DropDownList>
            </div>
            <div class="de-filter">
                <label>A&ntilde;o</label>
                <asp:DropDownList ID="ddlAnio" runat="server" />
            </div>
            <asp:Button ID="btnFiltrar" runat="server" Text="Aplicar filtros" CssClass="de-btn de-btn-primary" OnClick="btnFiltrar_Click" />
            <asp:Button ID="btnLimpiar" runat="server" Text="Limpiar" CssClass="de-btn de-btn-ghost" OnClick="btnLimpiar_Click" CausesValidation="false" />
        </div>

        <!-- Tabs -->
        <div class="de-tabs" role="tablist">
            <button type="button" class="de-tab active" data-target="panel-general"><i class="fas fa-chart-pie"></i> General</button>
            <button type="button" class="de-tab" data-target="panel-trujillo"><i class="fas fa-truck-loading"></i> Trujillo &amp; Base</button>
            <button type="button" class="de-tab" data-target="panel-bodega"><i class="fas fa-warehouse"></i> Bodega Nacional</button>
            <button type="button" class="de-tab" data-target="panel-tramite"><i class="fas fa-file-signature"></i> Tr&aacute;mite Aduanero</button>
            <button type="button" class="de-tab" data-target="panel-descarga"><i class="fas fa-box-open"></i> Descarga Ecuador</button>
            <button type="button" class="de-tab" data-target="panel-cumplimiento"><i class="fas fa-bullseye"></i> Cumplimiento</button>
            <button type="button" class="de-tab" data-target="panel-operativo"><i class="fas fa-chart-line"></i> Clientes &amp; Tendencia</button>
        </div>

        <!-- Chips de filtros activos en cliente -->
        <div id="deChips" class="de-chip-bar" aria-live="polite"></div>

        <!-- KPIs -->
        <div class="de-kpi-grid">
            <div class="de-kpi" id="kpiCumplCard">
                <div class="kpi-label">% Cumplimiento prog.</div>
                <div class="kpi-value" id="kpiCumplValue"><asp:Literal ID="litCumplimiento" runat="server" Text="0" /><span id="kpiCumplSufijo">%</span></div>
                <div class="kpi-hint" id="kpiCumplHint">Llegada a Trujillo &le; F.H. Programaci&oacute;n</div>
                <div class="kpi-detail" id="kpiCumplDetalle" style="font-size:11px;color:#888;margin-top:4px;"></div>
            </div>
            <div class="de-kpi k-accent">
                <div class="kpi-label">Total Camiones</div>
                <div class="kpi-value"><asp:Literal ID="litCamiones" runat="server" Text="0" /></div>
                <div class="kpi-hint">Viajes en el período</div>
            </div>
            <div class="de-kpi k-violet">
                <div class="kpi-label">Total Pedidos</div>
                <div class="kpi-value"><asp:Literal ID="litPedidos" runat="server" Text="0" /></div>
                <div class="kpi-hint">Clientes distintos</div>
            </div>
            <div class="de-kpi k-warn">
                <div class="kpi-label">Camiones / Pedido</div>
                <div class="kpi-value"><asp:Literal ID="litCamionesPedido" runat="server" Text="0" /></div>
                <div class="kpi-hint">Promedio del per&iacute;odo</div>
            </div>
            <div class="de-kpi">
                <div class="kpi-label">Horas promedio del viaje</div>
                <div class="kpi-value"><asp:Literal ID="litHorasViaje" runat="server" Text="0" /></div>
                <div class="kpi-hint">F.H. Salida Base → F.H. Salida</div>
            </div>
            <div class="de-kpi k-danger">
                <div class="kpi-label">Total Incidencias</div>
                <div class="kpi-value"><asp:Literal ID="litIncidencias" runat="server" Text="0" /></div>
                <div class="kpi-hint">Sacos robados + rotos + mojados</div>
            </div>
        </div>

        <!-- ===== Vista diaria desplegable: trazabilidad diaria de tiempos por etapa ===== -->
        <div id="dailyPanel" class="de-daily" data-collapsed="true">
            <button type="button" class="de-daily-toggle" id="dailyToggle">
                <span class="left">
                    <i class="fas fa-stopwatch"></i>
                    Trazabilidad diaria de tiempos
                    <span class="de-daily-hint" id="dailyHint">— selecciona un mes específico para activar</span>
                </span>
                <span class="right">
                    <span class="de-daily-pico" id="dailyPico"></span>
                    <i class="fas fa-chevron-down chev"></i>
                </span>
            </button>
            <div class="de-daily-body">
                <div class="de-daily-controls">
                    <div class="de-stage-picker">
                        <label>Etapa</label>
                        <select id="dailyStage" class="de-stage-select"></select>
                    </div>
                    <div class="de-daily-stats" id="dailyStats"></div>
                </div>
                <div class="canvas-wrap" style="min-height:300px;"><canvas id="chartDaily"></canvas></div>
                <div class="de-daily-foot">
                    <i class="fas fa-info-circle"></i>
                    Pasa el cursor por la curva para ver el detalle del día · línea punteada = promedio del mes · punto naranja = día pico
                </div>
            </div>
        </div>

        <!-- ===== Panel General ===== -->
        <div id="panel-general" class="de-tab-panel active">
            <div class="de-row">
                <div class="de-chart de-col-6">
                    <h3><i class="fas fa-chart-area"></i> Tendencia mensual de camiones</h3>
                    <div class="canvas-wrap"><canvas id="chartTendencia"></canvas></div>
                </div>
                <div class="de-chart de-col-3">
                    <h3><i class="fas fa-bullseye"></i> Cumplimiento</h3>
                    <div class="canvas-wrap short"><canvas id="chartCumplGauge"></canvas></div>
                </div>
                <div class="de-chart de-col-3">
                    <h3><i class="fas fa-exclamation-triangle"></i> Incidencias</h3>
                    <div class="canvas-wrap short"><canvas id="chartIncidencias"></canvas></div>
                </div>
            </div>
            <div class="de-row">
                <div class="de-chart de-col-6">
                    <h3><i class="fas fa-users"></i> Top 10 Pedidos (Clientes)</h3>
                    <div class="canvas-wrap tall"><canvas id="chartClientes"></canvas></div>
                </div>
                <div class="de-chart de-col-6">
                    <h3><i class="fas fa-tasks"></i> Estado de los viajes</h3>
                    <div class="canvas-wrap tall"><canvas id="chartEstado"></canvas></div>
                </div>
            </div>
        </div>

        <!-- ===== Panel Trujillo &amp; Base ===== -->
        <div id="panel-trujillo" class="de-tab-panel">
            <div class="de-row">
                <div class="de-chart de-col-12">
                    <h3><i class="far fa-clock"></i> Tiempos promedio en Trujillo (Hrs)</h3>
                    <div class="canvas-wrap" style="min-height:260px; height:auto;"><canvas id="chartTrujillo"></canvas></div>
                </div>
            </div>
            <div class="de-row">
                <div class="de-chart de-col-6">
                    <h3><i class="fas fa-route"></i> Trujillo → Planta Ecuador (días)</h3>
                    <div class="canvas-wrap"><canvas id="chartTrujilloEcu"></canvas></div>
                </div>
                <div class="de-chart de-col-6">
                    <h3><i class="fas fa-parking"></i> Tiempo promedio en Base (Hrs)</h3>
                    <div class="canvas-wrap"><canvas id="chartBase"></canvas></div>
                </div>
            </div>
        </div>

        <!-- ===== Panel Bodega Nacional ===== -->
        <div id="panel-bodega" class="de-tab-panel">
            <div class="de-row">
                <div class="de-chart de-col-6">
                    <h3><i class="fas fa-warehouse"></i> DEPSA (Hrs)</h3>
                    <div class="canvas-wrap tall"><canvas id="chartDepsaPuro"></canvas></div>
                </div>
                <div class="de-chart de-col-6">
                    <h3><i class="fas fa-building"></i> COMPLEX (Hrs)</h3>
                    <div class="canvas-wrap tall"><canvas id="chartComplex"></canvas></div>
                </div>
            </div>
            <div class="de-row">
                <div class="de-chart de-col-12">
                    <h3><i class="fas fa-cubes"></i> DEPSA + COMPLEX consolidado (Hrs)</h3>
                    <div class="canvas-wrap"><canvas id="chartDepsa"></canvas></div>
                </div>
            </div>
        </div>

        <!-- ===== Panel Trámite Aduanero ===== -->
        <div id="panel-tramite" class="de-tab-panel">
            <div class="de-row">
                <div class="de-chart de-col-6">
                    <h3><i class="fas fa-receipt"></i> TCI / Nacionalización (Hrs)</h3>
                    <div class="canvas-wrap tall"><canvas id="chartTCI"></canvas></div>
                </div>
                <div class="de-chart de-col-6">
                    <h3><i class="fas fa-stamp"></i> Tiempo promedio en CEBAF (min)</h3>
                    <div class="canvas-wrap tall"><canvas id="chartCEBAF"></canvas></div>
                </div>
            </div>
        </div>

        <!-- ===== Panel Descarga Ecuador ===== -->
        <div id="panel-descarga" class="de-tab-panel">
            <div class="de-row">
                <div class="de-chart de-col-6">
                    <h3><i class="fas fa-box-open"></i> Tiempos en Inbalnor (Hrs)</h3>
                    <div class="canvas-wrap"><canvas id="chartInbalnor"></canvas></div>
                </div>
                <div class="de-chart de-col-6">
                    <h3><i class="fas fa-box-open"></i> Tiempos en Jave (Hrs)</h3>
                    <div class="canvas-wrap"><canvas id="chartJave"></canvas></div>
                </div>
            </div>
            <div class="de-row">
                <div class="de-chart de-col-6">
                    <h3><i class="fas fa-truck-moving"></i> DEPSA → Bodega de descarga (Hrs)</h3>
                    <div class="canvas-wrap"><canvas id="chartDepsaSplit"></canvas></div>
                </div>
                <div class="de-chart de-col-6">
                    <h3><i class="fas fa-truck-moving"></i> TCI → Bodega de descarga (Hrs)</h3>
                    <div class="canvas-wrap"><canvas id="chartTciSplit"></canvas></div>
                </div>
            </div>
        </div>

        <!-- ===== Panel Cumplimiento ===== -->
        <div id="panel-cumplimiento" class="de-tab-panel">
            <div class="de-row">
                <div class="de-chart de-col-7">
                    <h3><i class="fas fa-chart-pie"></i> Distribución del cumplimiento (camiones)</h3>
                    <div class="canvas-wrap tall"><canvas id="chartCumplBuckets"></canvas></div>
                </div>
                <div class="de-chart de-col-5">
                    <h3><i class="fas fa-list"></i> Resumen</h3>
                    <div id="cumplResumen" class="de-resumen-list"></div>
                </div>
            </div>
        </div>

        <!-- ===== Panel Clientes & Tendencia ===== -->
        <div id="panel-operativo" class="de-tab-panel">
            <div class="de-row">
                <div class="de-chart de-col-12">
                    <h3><i class="fas fa-chart-line"></i> Tendencia mensual por pedido (Top 5)</h3>
                    <div class="canvas-wrap tall"><canvas id="chartClienteMes"></canvas></div>
                </div>
            </div>
        </div>

        <asp:HiddenField ID="hdnDatos" runat="server" />
        <asp:HiddenField ID="hdnCumplDetalle" runat="server" />
    </div>

    <script type="text/javascript">
        (function () {
            // Tabs
            document.querySelectorAll('.de-tab').forEach(function (tab) {
                tab.addEventListener('click', function () {
                    var target = this.getAttribute('data-target');
                    document.querySelectorAll('.de-tab').forEach(function (t) { t.classList.remove('active'); });
                    document.querySelectorAll('.de-tab-panel').forEach(function (p) { p.classList.remove('active'); });
                    this.classList.add('active');
                    var panel = document.getElementById(target);
                    if (panel) {
                        panel.classList.add('active');
                        // Forzar resize de chart cuando se muestra
                        setTimeout(function () {
                            for (var k in window.deCharts) {
                                if (window.deCharts[k] && typeof window.deCharts[k].resize === 'function') {
                                    window.deCharts[k].resize();
                                }
                            }
                        }, 60);
                    }
                });
            });
        })();

        function deRenderCharts() {
            var hdn = document.getElementById('<%= hdnDatos.ClientID %>');
            if (!hdn || !hdn.value) return;
            var d;
            try { d = JSON.parse(hdn.value); } catch (e) { console.error('JSON dashboard inválido', e); return; }

            window.deCharts = window.deCharts || {};
            // Destruir previos
            Object.keys(window.deCharts).forEach(function (k) {
                if (window.deCharts[k]) { window.deCharts[k].destroy(); window.deCharts[k] = null; }
            });

            var palette = {
                primary:  '#2563EB',
                accent:   '#00D4AA',
                warning:  '#F59E0B',
                danger:   '#EF4444',
                violet:   '#8B5CF6',
                ink:      '#0B1426',
                soft:     '#94A3B8'
            };

            // Registrar plugin annotation si está disponible
            if (window['chartjs-plugin-annotation'] && Chart.registry && !Chart.registry.plugins.get('annotation')) {
                try { Chart.register(window['chartjs-plugin-annotation']); } catch(e) {}
            }

            // Helper: crea un gradiente vertical para áreas
            function gradientFor(ctx, area, hex) {
                if (!area) return hex + '33';
                var g = ctx.createLinearGradient(0, area.top, 0, area.bottom);
                g.addColorStop(0, hex + 'CC');
                g.addColorStop(1, hex + '08');
                return g;
            }

            // Plugin: dibuja valor + label en el centro de un doughnut
            var centerTextPlugin = {
                id: 'centerText',
                afterDraw: function (chart, args, opts) {
                    if (!opts || !opts.text) return;
                    var ctx2 = chart.ctx;
                    var x = (chart.chartArea.left + chart.chartArea.right) / 2;
                    var y = (chart.chartArea.top + chart.chartArea.bottom) / 2;
                    ctx2.save();
                    ctx2.textAlign = 'center';
                    ctx2.textBaseline = 'middle';
                    ctx2.fillStyle = palette.ink;
                    ctx2.font = '800 24px "Plus Jakarta Sans"';
                    ctx2.fillText(opts.text, x, y - 8);
                    if (opts.subtext) {
                        ctx2.fillStyle = palette.soft;
                        ctx2.font = '600 11px "Plus Jakarta Sans"';
                        ctx2.fillText(opts.subtext, x, y + 14);
                    }
                    ctx2.restore();
                }
            };

            var commonOpts = {
                responsive: true,
                maintainAspectRatio: false,
                layout: { padding: { top: 10, left: 0, right: 0, bottom: 0 } },
                plugins: {
                    legend: { position: 'bottom', labels: { font: { family: "'Plus Jakarta Sans'", size: 12, weight: '600' }, color: palette.ink, boxWidth: 12, usePointStyle: true } },
                    tooltip: { backgroundColor: palette.ink, padding: 10, titleFont: { weight: '700' }, cornerRadius: 8 }
                },
                scales: {
                    x: { ticks: { color: palette.soft, font: { family: "'Plus Jakarta Sans'", size: 11, weight: '500' } }, grid: { display:false, drawBorder:false } },
                    y: { beginAtZero: true, border: { dash: [4, 4] }, ticks: { color: palette.soft, font: { family: "'Plus Jakarta Sans'", size: 11, weight: '500' }, maxTicksLimit: 6 }, grid: { color: '#EEF2F7', drawBorder:false } }
                }
            };

            function bar(id, labels, values, color, suffix) {
                var ctx = document.getElementById(id); if (!ctx) return;
                window.deCharts[id] = new Chart(ctx, {
                    type: 'bar',
                    data: { labels: labels, datasets: [{ data: values, backgroundColor: color, borderRadius: 8, maxBarThickness: 48 }] },
                    options: Object.assign({}, commonOpts, {
                        plugins: Object.assign({}, commonOpts.plugins, {
                            legend: { display:false },
                            tooltip: { callbacks: { label: function(c){ return c.parsed.y + (suffix || ''); } } }
                        })
                    })
                });
            }

            function hbar(id, labels, values, color, suffix) {
                var ctx = document.getElementById(id); if (!ctx) return;
                window.deCharts[id] = new Chart(ctx, {
                    type: 'bar',
                    data: { labels: labels, datasets: [{ data: values, backgroundColor: color, borderRadius: 8, maxBarThickness: 26 }] },
                    options: Object.assign({}, commonOpts, {
                        indexAxis: 'y',
                        plugins: Object.assign({}, commonOpts.plugins, {
                            legend: { display:false },
                            tooltip: { callbacks: { label: function(c){ return c.parsed.x + (suffix || ''); } } }
                        }),
                        scales: { x: { beginAtZero: true, ticks: { color: palette.soft }, grid: { color: '#EEF2F7' } }, y: { ticks: { color: palette.ink, font: { weight:'600' } }, grid: { display:false } } }
                    })
                });
            }

            function doughnut(id, labels, values, colors, centerOpts) {
                var ctx = document.getElementById(id); if (!ctx) return;
                var cfg = {
                    type: 'doughnut',
                    data: { labels: labels, datasets: [{ data: values, backgroundColor: colors, borderWidth: 0, hoverOffset: 8 }] },
                    options: { responsive:true, maintainAspectRatio:false, cutout: '62%',
                        plugins: Object.assign({}, commonOpts.plugins, { centerText: centerOpts || false }),
                        animation: { animateScale: true, animateRotate: true }
                    },
                    plugins: [centerTextPlugin]
                };
                window.deCharts[id] = new Chart(ctx, cfg);
            }

            function line(id, labels, values, color, opts) {
                var ctx = document.getElementById(id); if (!ctx) return;
                opts = opts || {};
                window.deCharts[id] = new Chart(ctx, {
                    type: 'line',
                    data: { labels: labels, datasets: [{
                        label: opts.label || 'Camiones',
                        data: values,
                        borderColor: color,
                        backgroundColor: function (c) { return gradientFor(c.chart.ctx, c.chart.chartArea, color); },
                        tension: .4,
                        fill: true,
                        pointRadius: function (c) {
                            var max = Math.max.apply(null, values);
                            return c.parsed && c.parsed.y === max ? 6 : 3;
                        },
                        pointHoverRadius: 7,
                        pointBackgroundColor: color,
                        pointBorderColor: '#fff',
                        pointBorderWidth: 2,
                        borderWidth: 2.5
                    }]},
                    options: Object.assign({}, commonOpts, {
                        onClick: opts.onClick || null,
                        interaction: { mode: 'index', intersect: false },
                        plugins: Object.assign({}, commonOpts.plugins, {
                            tooltip: {
                                backgroundColor: palette.ink, padding: 10, cornerRadius: 8,
                                callbacks: {
                                    label: function (c) {
                                        return ' ' + (opts.label || 'Valor') + ': ' + c.parsed.y + (opts.suffix || '');
                                    }
                                }
                            }
                        })
                    })
                });
                if (opts.onClick) ctx.classList.add('de-clickable');
            }

            function gauge(id, value, max, color, suffix) {
                var ctx = document.getElementById(id); if (!ctx) return;
                var v = Math.max(0, Math.min(value, max));
                var rest = Math.max(0, max - v);
                window.deCharts[id] = new Chart(ctx, {
                    type: 'doughnut',
                    data: {
                        labels: ['Cumple', 'Pendiente'],
                        datasets: [{ data: [v, rest], backgroundColor: [color, '#EEF2F7'], borderWidth: 0 }]
                    },
                    options: {
                        responsive: true, maintainAspectRatio: false,
                        cutout: '72%',
                        rotation: -90, circumference: 180,
                        plugins: {
                            legend: { display: false },
                            tooltip: { callbacks: { label: function(c){ return c.label + ': ' + c.parsed.toFixed(2) + (suffix || ''); } } }
                        }
                    },
                    plugins: [{
                        id: 'gaugeText',
                        afterDraw: function (chart) {
                            var ctx2 = chart.ctx;
                            var w = chart.width, h = chart.height;
                            ctx2.save();
                            ctx2.font = '800 28px "Plus Jakarta Sans"';
                            ctx2.textAlign = 'center';
                            ctx2.fillStyle = palette.ink;
                            ctx2.fillText(value.toFixed(1) + (suffix || ''), w / 2, h * 0.72);
                            ctx2.restore();
                        }
                    }]
                });
            }

            function stackedBar(id, labels, datasets) {
                var ctx = document.getElementById(id); if (!ctx) return;
                window.deCharts[id] = new Chart(ctx, {
                    type: 'bar',
                    data: { labels: labels, datasets: datasets },
                    options: Object.assign({}, commonOpts, {
                        scales: {
                            x: { stacked: true, ticks: { color: palette.soft }, grid: { display: false } },
                            y: { stacked: true, beginAtZero: true, ticks: { color: palette.soft }, grid: { color: '#EEF2F7' } }
                        }
                    })
                });
            }

            // Horizontal stacked bar — igual al visual del Power BI de Tiempos Trujillo
            function hStackedBar(id, labels, datasets, suffix) {
                var ctx = document.getElementById(id); if (!ctx) return;
                window.deCharts[id] = new Chart(ctx, {
                    type: 'bar',
                    data: { labels: labels, datasets: datasets },
                    options: {
                        indexAxis: 'y',
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: {
                            legend: {
                                display: true,
                                position: 'right',
                                labels: { color: palette.ink, font: { size: 11 }, boxWidth: 14 }
                            },
                            tooltip: {
                                callbacks: {
                                    label: function(c) {
                                        var v = c.parsed.x;
                                        var base = ' ' + c.dataset.label + ': ' + (v != null ? v.toFixed(1) : '0') + (suffix || '');
                                        var counts = c.dataset.counts;
                                        if (counts && counts[c.dataIndex] != null) {
                                            base += '  (n=' + counts[c.dataIndex] + ')';
                                        }
                                        return base;
                                    }
                                }
                            }
                        },
                        scales: {
                            x: {
                                stacked: true,
                                beginAtZero: true,
                                ticks: { color: palette.soft, callback: function(v){ return v + (suffix || ''); } },
                                grid: { color: '#EEF2F7' }
                            },
                            y: {
                                stacked: true,
                                ticks: { color: palette.ink, font: { weight: '600' } },
                                grid: { display: false }
                            }
                        }
                    }
                });
            }

            function multiBar(id, labels, datasets, suffix) {
                var ctx = document.getElementById(id); if (!ctx) return;
                window.deCharts[id] = new Chart(ctx, {
                    type: 'bar',
                    data: { labels: labels, datasets: datasets },
                    options: Object.assign({}, commonOpts, {
                        plugins: Object.assign({}, commonOpts.plugins, {
                            tooltip: { callbacks: { label: function(c){ return c.dataset.label + ': ' + c.parsed.y + (suffix || ''); } } }
                        })
                    })
                });
            }

            // ============= Render =============

            // Trujillo — stacked bar horizontal por mes (igual al Power BI)
            var mesesNombresT = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
            var trujMeses = d.trujillo || [];

            // Detectar si hay algún dato real cargado en planta
            var totalConDatosPlanta = trujMeses.reduce(function(acc, x){
                return acc + (x.nEsperaIngreso||0) + (x.nEsperaInicio||0) + (x.nCarga||0) + (x.nPermanencia||0);
            }, 0);

            var canvasTruj = document.getElementById('chartTrujillo');
            if (canvasTruj && totalConDatosPlanta === 0 && trujMeses.length > 0) {
                // Mostrar mensaje claro en lugar de un chart vacío
                var wrap = canvasTruj.parentElement;
                wrap.innerHTML =
                    '<div style="display:flex;flex-direction:column;align-items:center;justify-content:center;height:100%;min-height:220px;color:#888;text-align:center;padding:20px;">' +
                        '<i class="fas fa-database" style="font-size:42px;color:#ddd;margin-bottom:12px;"></i>' +
                        '<div style="font-weight:600;color:#555;margin-bottom:4px;">Sin datos de planta cargados</div>' +
                        '<div style="font-size:12px;">Hay <b>' + trujMeses.reduce(function(a,x){return a+(x.totalRegistros||0);},0) + '</b> registros en el per&iacute;odo,<br/>' +
                        'pero ninguno tiene <b>F.H.I PLANTA</b>, <b>INICIO/TERMINO DE CARGA</b> ni <b>F.H.S PLANTA</b>.</div>' +
                        '<div style="font-size:11px;margin-top:10px;color:#aaa;">Completa esas columnas en el registro o en el Excel de importaci&oacute;n.</div>' +
                    '</div>';
            } else {
                var trujLabels = trujMeses.map(function(x){ return mesesNombresT[x.mes - 1]; });
                hStackedBar('chartTrujillo', trujLabels, [
                    {
                        label: 'Espera a ingreso (Hrs)',
                        data: trujMeses.map(function(x){ return x.esperaIngresoTrujillo; }),
                        counts: trujMeses.map(function(x){ return x.nEsperaIngreso; }),
                        backgroundColor: '#D4A0A0',
                        borderWidth: 0
                    },
                    {
                        label: 'Espera inicio carga (Hrs)',
                        data: trujMeses.map(function(x){ return x.esperaInicioCarga; }),
                        counts: trujMeses.map(function(x){ return x.nEsperaInicio; }),
                        backgroundColor: '#A8BCA1',
                        borderWidth: 0
                    },
                    {
                        label: 'Carga (Hrs)',
                        data: trujMeses.map(function(x){ return x.cargaHoras; }),
                        counts: trujMeses.map(function(x){ return x.nCarga; }),
                        backgroundColor: '#6C7A9C',
                        borderWidth: 0
                    },
                    {
                        label: 'Permanencia total (Hrs)',
                        data: trujMeses.map(function(x){ return x.permanenciaPlanta; }),
                        counts: trujMeses.map(function(x){ return x.nPermanencia; }),
                        backgroundColor: '#E8A838',
                        borderWidth: 0
                    }
                ], ' hrs');
            }

            // Trujillo → Planta Ecuador
            bar('chartTrujilloEcu', ['Días promedio'], [d.diasTrujilloPlantaEcu], palette.violet, ' d');

            // Base
            bar('chartBase', ['Tiempo Base'], [d.tiempoBaseHoras], palette.accent, ' hrs');


            // Estado
            var estadoLabels = (d.estados || []).map(function (x) { return x.estado; });
            var estadoValues = (d.estados || []).map(function (x) { return x.total; });
            var totalEstado = estadoValues.reduce(function(a,b){return a+(b||0);},0);
            doughnut('chartEstado', estadoLabels, estadoValues,
                [palette.primary, palette.accent, palette.warning, palette.danger, palette.violet],
                { text: totalEstado.toString(), subtext: 'viajes' });

            // Incidencias
            var totalInc = (d.incidencias.robados||0) + (d.incidencias.rotos||0) + (d.incidencias.mojados||0);
            doughnut('chartIncidencias', ['Robados', 'Rotos', 'Mojados'],
                [d.incidencias.robados, d.incidencias.rotos, d.incidencias.mojados],
                [palette.danger, palette.warning, palette.primary],
                { text: totalInc.toString(), subtext: 'sacos' });

            // TCI
            bar('chartTCI',
                ['Tiempo TCI', 'Espera nacionalización'],
                [d.tci.tiempoTCIHoras, d.tci.esperaNacionalizacion],
                palette.primary, ' hrs');

            // CEBAF
            bar('chartCEBAF', ['CEBAF'], [d.tci.tiempoCEBAFMin], palette.warning, ' min');

            // DEPSA consolidado
            bar('chartDepsa',
                ['Tiempo en bodega', 'Espera para ingresar'],
                [d.depsa.tiempoDepsa, d.depsa.esperaIngresoDepsa],
                palette.violet, ' hrs');

            // DEPSA puro
            bar('chartDepsaPuro',
                ['Tiempo en DEPSA', 'Espera ingreso DEPSA'],
                [d.depsaPuro.tiempo, d.depsaPuro.espera],
                palette.violet, ' hrs');

            // COMPLEX
            bar('chartComplex',
                ['Tiempo en COMPLEX', 'Espera ingreso COMPLEX'],
                [d.complex.tiempo, d.complex.espera],
                palette.accent, ' hrs');

            // Inbalnor
            bar('chartInbalnor',
                ['Descarga', 'Espera para descarga'],
                [d.inbalnor.descarga, d.inbalnor.espera],
                palette.accent, ' hrs');

            // Jave
            bar('chartJave',
                ['Descarga', 'Espera para descarga'],
                [d.jave.descarga, d.jave.espera],
                palette.primary, ' hrs');

            // DEPSA -> Inbalnor / Jave
            multiBar('chartDepsaSplit',
                ['Hacia bodega'],
                [
                    { label: 'Inbalnor', data: [d.depsaSplit.aInbalnor], backgroundColor: palette.accent, borderRadius: 8, maxBarThickness: 48 },
                    { label: 'Jave',     data: [d.depsaSplit.aJave],     backgroundColor: palette.primary, borderRadius: 8, maxBarThickness: 48 }
                ], ' hrs');

            // TCI -> Inbalnor / Jave
            multiBar('chartTciSplit',
                ['Hacia bodega'],
                [
                    { label: 'Inbalnor', data: [d.tciSplit.aInbalnor], backgroundColor: palette.accent, borderRadius: 8, maxBarThickness: 48 },
                    { label: 'Jave',     data: [d.tciSplit.aJave],     backgroundColor: palette.primary, borderRadius: 8, maxBarThickness: 48 }
                ], ' hrs');

            // Tendencia mensual — click navega al mes
            var mesesNombres = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
            var tendData = (d.tendencia || []);
            var tendLabels = tendData.map(function (x) { return mesesNombres[x.mes - 1]; });
            var tendValues = tendData.map(function (x) { return x.totalCamiones; });
            line('chartTendencia', tendLabels, tendValues, palette.primary, {
                label: 'Camiones', suffix: '',
                onClick: function (evt, elements) {
                    if (!elements || !elements.length) return;
                    var idx = elements[0].index;
                    var mes = tendData[idx] ? tendData[idx].mes : null;
                    if (!mes) return;
                    var ddl = document.getElementById('<%= ddlMes.ClientID %>');
                    if (ddl) {
                        ddl.value = mes.toString();
                        var btn = document.getElementById('<%= btnFiltrar.ClientID %>');
                        if (btn) btn.click();
                    }
                }
            });

            // Cumplimiento (gauge - usa valor del KPI literal)
            var pctCumpl = parseFloat(('<%= litCumplimiento.Text %>' || '0').replace(',', '.')) || 0;
            gauge('chartCumplGauge', pctCumpl, 100, palette.accent, '%');

            // Leer detalle de cumplimiento desde hidden field
            (function () {
                var hdnCumpl = document.getElementById('<%= hdnCumplDetalle.ClientID %>');
                if (!hdnCumpl || !hdnCumpl.value) return;
                try {
                    var det = JSON.parse(hdnCumpl.value);
                    // Texto de detalle: A tiempo / Con llegada registrada
                    var detEl = document.getElementById('kpiCumplDetalle');
                    if (detEl) {
                        detEl.innerHTML =
                            '<i class="fas fa-check-circle" style="color:#27ae60"></i> ' + det.aTiempo + ' a tiempo &nbsp;|&nbsp; ' +
                            '<i class="fas fa-clock" style="color:#e67e22"></i> ' + det.tardios + ' tard&iacute;os &nbsp;|&nbsp; ' +
                            '<i class="fas fa-question-circle" style="color:#bbb"></i> ' + det.sinDato + ' sin dato';
                    }
                    // Si no hay llegadas registradas, mostrar "S/D" en lugar de %
                    if (det.conLlegada === 0) {
                        var sufijo = document.getElementById('kpiCumplSufijo');
                        if (sufijo) sufijo.textContent = '';
                    }
                } catch(e) {}
            })();

            // Cumplimiento buckets (doughnut segmentado con valor central)
            var b = d.cumplimientoBuckets || { aTiempo:0, retraso6h:0, retraso24h:0, retrasoMas24h:0, sinDato:0 };
            var bLabels = ['A tiempo', '≤ 6 h', '6 - 24 h', '> 24 h', 'Sin dato'];
            var bValues = [b.aTiempo, b.retraso6h, b.retraso24h, b.retrasoMas24h, b.sinDato];
            var bColors = [palette.accent, palette.primary, palette.warning, palette.danger, palette.soft];
            var totalCu = bValues.reduce(function(a,c){return a+(c||0);},0);
            var pctATiempoCalc = totalCu ? (bValues[0] * 100 / totalCu).toFixed(0) : '0';
            doughnut('chartCumplBuckets', bLabels, bValues, bColors,
                { text: pctATiempoCalc + '%', subtext: 'a tiempo' });

            // Resumen del cumplimiento
            var totalCumpl = bValues.reduce(function (a, c) { return a + (c || 0); }, 0) || 1;
            var resumenHtml = bLabels.map(function (lbl, i) {
                var v = bValues[i] || 0;
                var pct = (v * 100 / totalCumpl).toFixed(1);
                return '<div class="de-resumen-item">' +
                    '<span class="lbl"><span class="dot" style="background:' + bColors[i] + '"></span>' + lbl + '</span>' +
                    '<span class="val">' + v + '<span class="pct">' + pct + '%</span></span>' +
                    '</div>';
            }).join('');
            var elResumen = document.getElementById('cumplResumen');
            if (elResumen) elResumen.innerHTML = resumenHtml;

            // Top 10 pedidos (clientes) — click filtra cliente activo
            var cliLabels = (d.clientes || []).map(function (x) { return x.cliente; });
            var cliValues = (d.clientes || []).map(function (x) { return x.totalCamiones; });
            var ctxCli = document.getElementById('chartClientes');
            if (ctxCli) {
                ctxCli.classList.add('de-clickable');
                window.deCharts['chartClientes'] = new Chart(ctxCli, {
                    type: 'bar',
                    data: { labels: cliLabels, datasets: [{
                        data: cliValues,
                        backgroundColor: cliLabels.map(function (lbl) {
                            return (window._deClienteActivo && window._deClienteActivo !== lbl) ? palette.soft : palette.primary;
                        }),
                        borderRadius: 8, maxBarThickness: 26
                    }] },
                    options: Object.assign({}, commonOpts, {
                        indexAxis: 'y',
                        onClick: function (evt, elements) {
                            if (!elements || !elements.length) return;
                            var lbl = cliLabels[elements[0].index];
                            window._deClienteActivo = (window._deClienteActivo === lbl) ? null : lbl;
                            deRefrescarChips();
                            deRefrescarClienteUI();
                        },
                        plugins: Object.assign({}, commonOpts.plugins, {
                            legend: { display:false },
                            tooltip: { callbacks: {
                                label: function(c){ return c.parsed.x + ' camiones'; },
                                afterLabel: function(){ return 'Click para filtrar / quitar filtro'; }
                            } }
                        }),
                        scales: { x: { beginAtZero: true, ticks: { color: palette.soft }, grid: { color: '#EEF2F7' } }, y: { ticks: { color: palette.ink, font: { weight:'600' } }, grid: { display:false } } }
                    })
                });
            }

            // Tendencia por cliente (stacked bar 5 clientes x 12 meses)
            var clientesUnicos = [];
            (d.tendenciaCliente || []).forEach(function (x) {
                if (clientesUnicos.indexOf(x.cliente) === -1) clientesUnicos.push(x.cliente);
            });
            var stackColors = [palette.primary, palette.accent, palette.warning, palette.violet, palette.danger];
            var datasetsTC = clientesUnicos.map(function (cli, idx) {
                var serie = mesesNombres.map(function (_, m) {
                    var item = (d.tendenciaCliente || []).find(function (x) {
                        return x.cliente === cli && x.mes === (m + 1);
                    });
                    return item ? item.totalCamiones : 0;
                });
                return {
                    label: cli,
                    data: serie,
                    backgroundColor: stackColors[idx % stackColors.length],
                    borderRadius: 6,
                    maxBarThickness: 32
                };
            });
            stackedBar('chartClienteMes', mesesNombres, datasetsTC);

            // ===== Vista diaria (drill-down) =====
            deRenderDaily(d, palette);

            // Guardar referencias para re-render local
            window._deDatos = d;
            deRefrescarChips();
            deRefrescarClienteUI();
        }

        // ===== Vista diaria: trazabilidad de tiempos por etapa =====
        // Catálogo de etapas. Cada una mapea a un campo del payload serieDiaria.
        // 'n' = campo de conteo (para saber cuántos camiones aportaron al promedio)
        var DAILY_STAGES = [
            { group: 'Trujillo (planta)',
              items: [
                  { id: 'horasTrujilloProm', n: 'nTrujillo',     label: 'Permanencia en Trujillo',          unit: 'h',   color: 'primary' },
                  { id: 'horasViajeProm',    n: 'nViaje',        label: 'Viaje Trujillo → destino',         unit: 'h',   color: 'violet' }
              ] },
            { group: 'Tránsito a Ecuador',
              items: [
                  { id: 'tBNInbalnor',  n: 'nBNInbalnor',  label: 'Bodega Nacional → INBALNOR',        unit: 'h', color: 'primary' },
                  { id: 'tBNJave',      n: 'nBNJave',      label: 'Bodega Nacional → JAVE',            unit: 'h', color: 'primary' },
                  { id: 'tTciInbalnor', n: 'nTciInbalnor', label: 'TCI → INBALNOR',                    unit: 'h', color: 'accent' },
                  { id: 'tTciJave',     n: 'nTciJave',     label: 'TCI → JAVE',                        unit: 'h', color: 'accent' }
              ] },
            { group: 'Trámite aduanero',
              items: [
                  { id: 'tTci',           n: 'nTci',           label: 'Tiempo en TCI',                       unit: 'h',   color: 'primary' },
                  { id: 'tEsperaNac',     n: 'nEsperaNac',     label: 'Espera nacionalización',              unit: 'h',   color: 'warning' },
                  { id: 'tEsperaDepsa',   n: 'nEsperaDepsa',   label: 'Espera para ingresar a DEPSA',        unit: 'h',   color: 'warning' },
                  { id: 'tDepsa',         n: 'nDepsa',         label: 'Tiempo dentro de DEPSA',              unit: 'h',   color: 'violet' },
                  { id: 'tEsperaComplex', n: 'nEsperaComplex', label: 'Espera para ingresar a COMPLEX',      unit: 'h',   color: 'warning' },
                  { id: 'tComplex',       n: 'nComplex',       label: 'Tiempo dentro de COMPLEX',            unit: 'h',   color: 'violet' },
                  { id: 'tCebafMin',      n: 'nCebaf',         label: 'Tiempo en CEBAF',                     unit: 'min', color: 'accent' }
              ] }
        ];

        function deRellenarSelectorEtapas() {
            var sel = document.getElementById('dailyStage');
            if (!sel || sel.dataset.filled === '1') return;
            var html = '';
            DAILY_STAGES.forEach(function (g) {
                html += '<optgroup label="' + g.group + '">';
                g.items.forEach(function (it) {
                    html += '<option value="' + it.id + '">' + it.label + ' (' + it.unit + ')</option>';
                });
                html += '</optgroup>';
            });
            sel.innerHTML = html;
            sel.dataset.filled = '1';
            // valor por defecto: la primera de tránsito a Ecuador (más representativo)
            sel.value = 'tBNInbalnor';
        }

        function deBuscarEtapa(id) {
            for (var i = 0; i < DAILY_STAGES.length; i++) {
                for (var j = 0; j < DAILY_STAGES[i].items.length; j++) {
                    if (DAILY_STAGES[i].items[j].id === id) return DAILY_STAGES[i].items[j];
                }
            }
            return null;
        }

        function deRenderDaily(d, palette) {
            var panel  = document.getElementById('dailyPanel');
            var hint   = document.getElementById('dailyHint');
            var picoEl = document.getElementById('dailyPico');
            var stats  = document.getElementById('dailyStats');
            if (!panel) return;
            deRellenarSelectorEtapas();

            var serie = (d && d.serieDiaria) || [];
            var hayMes = serie.length > 0;
            panel.classList.toggle('disabled', !hayMes);
            if (!hayMes) {
                hint.textContent = '— selecciona un mes específico para activar';
                picoEl.innerHTML = '';
                stats.innerHTML  = '';
                panel.dataset.collapsed = 'true';
                if (window.deCharts && window.deCharts.chartDaily) {
                    window.deCharts.chartDaily.destroy();
                    window.deCharts.chartDaily = null;
                }
                return;
            }
            hint.textContent = '— ' + serie.length + ' día(s) con registros';

            // Etapa activa
            var sel    = document.getElementById('dailyStage');
            var etapa  = deBuscarEtapa(sel.value) || DAILY_STAGES[1].items[0];
            var unit   = etapa.unit === 'min' ? ' min' : ' h';
            var label  = etapa.label;

            // Sólo incluyo días donde la etapa tiene al menos un camión que aportó (n > 0).
            // Eso evita que un valor 0 "sin dato" baje el promedio del gráfico.
            var puntos = serie
                .filter(function (x) { return (x[etapa.n] || 0) > 0; })
                .map(function (x) { return { dia: x.dia, value: Number(x[etapa.id] || 0), row: x }; });

            if (!puntos.length) {
                stats.innerHTML  = '<div class="de-daily-stat warn">Sin datos para esta etapa<b style="font-size:14px;">— intenta otra etapa</b></div>';
                picoEl.innerHTML = '';
                if (window.deCharts && window.deCharts.chartDaily) {
                    window.deCharts.chartDaily.destroy();
                    window.deCharts.chartDaily = null;
                }
                return;
            }

            var labels = puntos.map(function (p) { return 'Día ' + p.dia; });
            var values = puntos.map(function (p) { return p.value; });

            // Estadísticos del mes en esta etapa
            var maxVal = -Infinity, minVal = Infinity, idxMax = -1, idxMin = -1;
            var suma   = 0, nAporte = 0;
            puntos.forEach(function (p, i) {
                suma    += p.value;
                nAporte += (p.row[etapa.n] || 0);
                if (p.value > maxVal) { maxVal = p.value; idxMax = i; }
                if (p.value < minVal) { minVal = p.value; idxMin = i; }
            });
            var promedio = suma / values.length;
            var fmt = function (v) { return (Math.round(v * 10) / 10).toLocaleString('es-PE'); };

            picoEl.innerHTML = '<i class="fas fa-arrow-up" style="margin-right:4px;color:#F59E0B;"></i>Pico en <b>Día ' + puntos[idxMax].dia + '</b> · ' + fmt(maxVal) + unit;

            // Cuántos días superan el promedio (útil para detectar desviaciones)
            var sobrePromedio = values.filter(function (v) { return v > promedio; }).length;

            stats.innerHTML =
                '<div class="de-daily-stat acc">Promedio del mes<b>'   + fmt(promedio)   + unit + '</b></div>' +
                '<div class="de-daily-stat warn">Día pico<b>Día '       + puntos[idxMax].dia + ' · ' + fmt(maxVal) + unit + '</b></div>' +
                '<div class="de-daily-stat">Día mínimo<b>Día '          + puntos[idxMin].dia + ' · ' + fmt(minVal) + unit + '</b></div>' +
                '<div class="de-daily-stat">Días sobre el promedio<b>'  + sobrePromedio + ' / ' + values.length + '</b></div>' +
                '<div class="de-daily-stat">Camiones que aportaron<b>'  + nAporte.toLocaleString('es-PE') + '</b></div>';

            // Render Chart
            var canvas = document.getElementById('chartDaily');
            if (!canvas) return;
            var ctx = canvas.getContext('2d');
            if (window.deCharts && window.deCharts.chartDaily) {
                window.deCharts.chartDaily.destroy();
                window.deCharts.chartDaily = null;
            }

            var colorMap = { primary: palette.primary, accent: palette.accent, warning: palette.warning, danger: palette.danger, violet: palette.violet };
            var color    = colorMap[etapa.color] || palette.primary;

            var pointBg  = values.map(function (_, i) { return i === idxMax ? palette.warning : color; });
            var pointRad = values.map(function (_, i) { return i === idxMax ? 8 : 3.5; });

            window.deCharts = window.deCharts || {};
            window.deCharts.chartDaily = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: [{
                        label: label,
                        data: values,
                        borderColor: color,
                        backgroundColor: function (c) {
                            var area = c.chart.chartArea;
                            if (!area) return color + '22';
                            var g = c.chart.ctx.createLinearGradient(0, area.top, 0, area.bottom);
                            g.addColorStop(0, color + 'AA');
                            g.addColorStop(1, color + '08');
                            return g;
                        },
                        borderWidth: 2.5,
                        tension: 0.35,
                        fill: true,
                        pointBackgroundColor: pointBg,
                        pointBorderColor: '#fff',
                        pointBorderWidth: 1.5,
                        pointRadius: pointRad,
                        pointHoverRadius: 8
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    interaction: { mode: 'index', intersect: false },
                    onClick: function (evt, els) {
                        if (!els || !els.length) return;
                        var i  = els[0].index;
                        var pt = puntos[i];
                        if (!pt) return;
                        picoEl.innerHTML = '<i class="fas fa-crosshairs" style="margin-right:4px;"></i>Día seleccionado: <b>Día ' + pt.dia + '</b> · ' + fmt(pt.value) + unit;
                    },
                    plugins: {
                        legend: { display: false },
                        tooltip: {
                            backgroundColor: 'rgba(11,20,38,.95)',
                            padding: 12, cornerRadius: 8,
                            titleFont: { weight: 'bold' },
                            callbacks: {
                                title: function (items) {
                                    if (!items || !items.length) return '';
                                    return 'Día ' + puntos[items[0].dataIndex].dia;
                                },
                                label: function (item) {
                                    var pt   = puntos[item.dataIndex] || { row: {} };
                                    var row  = pt.row || {};
                                    var diff = pt.value - promedio;
                                    var arr  = (diff >= 0 ? '▲ ' : '▼ ') + Math.abs(Math.round(diff * 10) / 10).toLocaleString('es-PE') + unit + ' vs prom';
                                    return [
                                        label + ': ' + fmt(pt.value) + unit + '  (' + arr + ')',
                                        '— Camiones del día: '  + (row.totalCamiones || 0),
                                        '— Aportaron a esta etapa: ' + (row[etapa.n] || 0),
                                        '— A tiempo / Tardíos: ' + (row.aTiempo || 0) + ' / ' + (row.tardios || 0)
                                    ];
                                }
                            }
                        },
                        annotation: {
                            annotations: {
                                promedio: {
                                    type: 'line',
                                    yMin: promedio, yMax: promedio,
                                    borderColor: palette.soft,
                                    borderWidth: 1.5,
                                    borderDash: [6, 4],
                                    label: {
                                        enabled: true, display: true,
                                        content: 'Promedio ' + fmt(promedio) + unit,
                                        position: 'end',
                                        backgroundColor: 'rgba(148,163,184,.85)',
                                        color: '#fff', font: { size: 10, weight: 'bold' },
                                        padding: 4
                                    }
                                },
                                pico: {
                                    type: 'point',
                                    xValue: idxMax, yValue: values[idxMax],
                                    backgroundColor: 'rgba(245,158,11,.15)',
                                    borderColor: palette.warning,
                                    borderWidth: 2,
                                    radius: 14
                                }
                            }
                        }
                    },
                    scales: {
                        x: { grid: { display: false }, ticks: { color: palette.soft, font: { size: 11 } } },
                        y: { beginAtZero: true,
                             grid: { color: 'rgba(0,0,0,.05)' },
                             ticks: { color: palette.soft, font: { size: 11 },
                                      callback: function (v) { return v + (unit === ' min' ? '' : ''); } },
                             title: { display: true, text: etapa.unit === 'min' ? 'Minutos' : 'Horas',
                                      color: palette.soft, font: { size: 11, weight: 'bold' } } }
                    }
                }
            });
        }

        // Toggle + cambio de etapa
        (function () {
            var DEF_PAL = { primary:'#2563EB', accent:'#00D4AA', warning:'#F59E0B',
                            danger:'#EF4444', violet:'#8B5CF6', ink:'#0B1426', soft:'#94A3B8' };
            document.addEventListener('click', function (ev) {
                var t = ev.target.closest && ev.target.closest('#dailyToggle');
                if (!t) return;
                var p = document.getElementById('dailyPanel');
                if (p.classList.contains('disabled')) return;
                p.dataset.collapsed = (p.dataset.collapsed === 'true') ? 'false' : 'true';
                if (p.dataset.collapsed === 'false' && window._deDatos) {
                    setTimeout(function () { deRenderDaily(window._deDatos, DEF_PAL); }, 200);
                }
            });
            document.addEventListener('change', function (ev) {
                if (ev.target && ev.target.id === 'dailyStage' && window._deDatos) {
                    deRenderDaily(window._deDatos, DEF_PAL);
                }
            });
        })();

        // ============= Interacción cliente-side =============
        function deRefrescarChips() {
            var cont = document.getElementById('deChips');
            if (!cont) return;
            cont.innerHTML = '';
            if (window._deClienteActivo) {
                var chip = document.createElement('span');
                chip.className = 'de-chip';
                chip.innerHTML = '<i class="fas fa-filter"></i> Pedido: ' + window._deClienteActivo +
                                 ' <span class="x" title="Quitar filtro">&times;</span>';
                chip.querySelector('.x').addEventListener('click', function () {
                    window._deClienteActivo = null;
                    deRefrescarChips();
                    deRefrescarClienteUI();
                });
                cont.appendChild(chip);
            }
        }

        // Recolorea las barras del Top 10 según el cliente activo
        function deRefrescarClienteUI() {
            var ch = window.deCharts && window.deCharts['chartClientes'];
            if (!ch) return;
            var lbls = ch.data.labels;
            ch.data.datasets[0].backgroundColor = lbls.map(function (lbl) {
                if (!window._deClienteActivo) return '#2563EB';
                return (window._deClienteActivo === lbl) ? '#F59E0B' : '#94A3B8';
            });
            ch.update('none');

            // Resaltar la serie del cliente activo en tendencia por cliente (panel operativo)
            var tc = window.deCharts && window.deCharts['chartClienteMes'];
            if (tc) {
                tc.data.datasets.forEach(function (ds) {
                    var match = !window._deClienteActivo || ds.label === window._deClienteActivo;
                    ds.backgroundColor = match ? ds._origColor || ds.backgroundColor : '#E4E8F0';
                    if (!ds._origColor) ds._origColor = ds.backgroundColor;
                });
                tc.update('none');
            }
        }

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', deRenderCharts);
        } else {
            deRenderCharts();
        }
        // Re-render tras postback (UpdatePanel no se usa aquí, pero por si acaso)
        if (typeof Sys !== 'undefined' && Sys.WebForms && Sys.WebForms.PageRequestManager) {
            Sys.WebForms.PageRequestManager.getInstance().add_endRequest(deRenderCharts);
        }
    </script>
</asp:Content>

<%@ Page Title="Dashboard de Exportacion" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DashboardExportacion.aspx.cs" Inherits="WebSGV.Views.Exportacion.DashboardExportacion" %>

<asp:Content ID="ContentDE" ContentPlaceHolderID="MainContent" runat="server">
    <meta charset="utf-8" />
    <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap" rel="stylesheet" />
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/chartjs-plugin-annotation@3.0.1/dist/chartjs-plugin-annotation.min.js"></script>
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/Exportacion/DashboardExportacion.css") %>" rel="stylesheet" />

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

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hdnDatos: '<%= hdnDatos.ClientID %>',
            ddlMes: '<%= ddlMes.ClientID %>',
            btnFiltrar: '<%= btnFiltrar.ClientID %>',
            litCumplimientoText: '<%= litCumplimiento.Text %>',
            hdnCumplDetalle: '<%= hdnCumplDetalle.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/Exportacion/DashboardExportacion.js") %>"></script>
</asp:Content>

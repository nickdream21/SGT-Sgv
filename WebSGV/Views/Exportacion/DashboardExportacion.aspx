<%@ Page Title="Dashboard de Exportacion" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DashboardExportacion.aspx.cs" Inherits="WebSGV.Views.Exportacion.DashboardExportacion" %>

<asp:Content ID="ContentDE" ContentPlaceHolderID="MainContent" runat="server">
    <meta charset="utf-8" />
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
            <button type="button" class="de-tab active" data-target="panel-general">General</button>
            <button type="button" class="de-tab" data-target="panel-trujillo">Trujillo y base</button>
            <button type="button" class="de-tab" data-target="panel-bodega">Bodega nacional</button>
            <button type="button" class="de-tab" data-target="panel-tramite">Tr&aacute;mite aduanero</button>
            <button type="button" class="de-tab" data-target="panel-descarga">Descarga en Ecuador</button>
            <button type="button" class="de-tab" data-target="panel-cumplimiento">Cumplimiento</button>
            <button type="button" class="de-tab" data-target="panel-operativo">Clientes y tendencia</button>
        </div>

        <!-- Chips de filtros activos en cliente -->
        <div id="deChips" class="de-chip-bar" aria-live="polite"></div>

        <!-- KPIs -->
        <div class="de-kpi-grid">
            <div class="de-kpi" id="kpiCumplCard">
                <div class="kpi-label">Cumplimiento de programación</div>
                <div class="kpi-value" id="kpiCumplValue"><asp:Literal ID="litCumplimiento" runat="server" Text="0" /><span id="kpiCumplSufijo">%</span></div>
                <div class="kpi-hint" id="kpiCumplHint">Llegada a Trujillo &le; F.H. Programaci&oacute;n</div>
                <div class="kpi-detail" id="kpiCumplDetalle" style="font-size:11px;color:#888;margin-top:4px;"></div>
            </div>
            <div class="de-kpi k-accent">
                <div class="kpi-label">Total camiones</div>
                <div class="kpi-value"><asp:Literal ID="litCamiones" runat="server" Text="0" /></div>
                <div class="kpi-hint">Viajes en el período</div>
            </div>
            <div class="de-kpi k-violet">
                <div class="kpi-label">Total pedidos</div>
                <div class="kpi-value"><asp:Literal ID="litPedidos" runat="server" Text="0" /></div>
                <div class="kpi-hint">Clientes distintos</div>
            </div>
            <div class="de-kpi k-warn">
                <div class="kpi-label">Camiones por pedido</div>
                <div class="kpi-value"><asp:Literal ID="litCamionesPedido" runat="server" Text="0" /></div>
                <div class="kpi-hint">Promedio del per&iacute;odo</div>
            </div>
            <div class="de-kpi">
                <div class="kpi-label">Horas promedio del viaje</div>
                <div class="kpi-value"><asp:Literal ID="litHorasViaje" runat="server" Text="0" /></div>
                <div class="kpi-hint">F.H. Salida Base → F.H. Salida</div>
            </div>
            <div class="de-kpi k-danger">
                <div class="kpi-label">Total incidencias</div>
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
                    <h3>Tendencia mensual de camiones</h3>
                    <div class="canvas-wrap"><canvas id="chartTendencia"></canvas></div>
                </div>
                <div class="de-chart de-col-3">
                    <h3>Cumplimiento</h3>
                    <div class="canvas-wrap short"><canvas id="chartCumplGauge"></canvas></div>
                </div>
                <div class="de-chart de-col-3">
                    <h3>Incidencias</h3>
                    <div class="canvas-wrap short"><canvas id="chartIncidencias"></canvas></div>
                </div>
            </div>
            <div class="de-row">
                <div class="de-chart de-col-6">
                    <h3>Top 10 pedidos (clientes)</h3>
                    <div class="canvas-wrap tall"><canvas id="chartClientes"></canvas></div>
                </div>
                <div class="de-chart de-col-6">
                    <h3>Estado de los viajes</h3>
                    <div class="canvas-wrap tall"><canvas id="chartEstado"></canvas></div>
                </div>
            </div>
        </div>

        <!-- ===== Panel Trujillo &amp; Base ===== -->
        <div id="panel-trujillo" class="de-tab-panel">
            <div class="de-row">
                <div class="de-chart de-col-12">
                    <h3>Tiempos promedio en Trujillo (h)</h3>
                    <div class="canvas-wrap" style="min-height:260px; height:auto;"><canvas id="chartTrujillo"></canvas></div>
                </div>
            </div>
            <div class="de-row">
                <div class="de-chart de-col-6">
                    <h3>Trujillo a planta Ecuador (días)</h3>
                    <div class="canvas-wrap"><canvas id="chartTrujilloEcu"></canvas></div>
                </div>
                <div class="de-chart de-col-6">
                    <h3>Tiempo promedio en base (h)</h3>
                    <div class="canvas-wrap"><canvas id="chartBase"></canvas></div>
                </div>
            </div>
        </div>

        <!-- ===== Panel Bodega Nacional ===== -->
        <div id="panel-bodega" class="de-tab-panel">
            <div class="de-row">
                <div class="de-chart de-col-6">
                    <h3>DEPSA (h)</h3>
                    <div class="canvas-wrap tall"><canvas id="chartDepsaPuro"></canvas></div>
                </div>
                <div class="de-chart de-col-6">
                    <h3>COMPLEX (h)</h3>
                    <div class="canvas-wrap tall"><canvas id="chartComplex"></canvas></div>
                </div>
            </div>
            <div class="de-row">
                <div class="de-chart de-col-12">
                    <h3>DEPSA + COMPLEX consolidado (h)</h3>
                    <div class="canvas-wrap"><canvas id="chartDepsa"></canvas></div>
                </div>
            </div>
        </div>

        <!-- ===== Panel Trámite Aduanero ===== -->
        <div id="panel-tramite" class="de-tab-panel">
            <div class="de-row">
                <div class="de-chart de-col-6">
                    <h3>TCI / nacionalización (h)</h3>
                    <div class="canvas-wrap tall"><canvas id="chartTCI"></canvas></div>
                </div>
                <div class="de-chart de-col-6">
                    <h3>Tiempo promedio en CEBAF (min)</h3>
                    <div class="canvas-wrap tall"><canvas id="chartCEBAF"></canvas></div>
                </div>
            </div>
        </div>

        <!-- ===== Panel Descarga Ecuador ===== -->
        <div id="panel-descarga" class="de-tab-panel">
            <div class="de-row">
                <div class="de-chart de-col-6">
                    <h3>Tiempos en Inbalnor (h)</h3>
                    <div class="canvas-wrap"><canvas id="chartInbalnor"></canvas></div>
                </div>
                <div class="de-chart de-col-6">
                    <h3>Tiempos en Jave (h)</h3>
                    <div class="canvas-wrap"><canvas id="chartJave"></canvas></div>
                </div>
            </div>
            <div class="de-row">
                <div class="de-chart de-col-6">
                    <h3>DEPSA a bodega de descarga (h)</h3>
                    <div class="canvas-wrap"><canvas id="chartDepsaSplit"></canvas></div>
                </div>
                <div class="de-chart de-col-6">
                    <h3>TCI a bodega de descarga (h)</h3>
                    <div class="canvas-wrap"><canvas id="chartTciSplit"></canvas></div>
                </div>
            </div>
        </div>

        <!-- ===== Panel Cumplimiento ===== -->
        <div id="panel-cumplimiento" class="de-tab-panel">
            <div class="de-row">
                <div class="de-chart de-col-7">
                    <h3>Distribución del cumplimiento (camiones)</h3>
                    <div class="canvas-wrap tall"><canvas id="chartCumplBuckets"></canvas></div>
                </div>
                <div class="de-chart de-col-5">
                    <h3>Resumen</h3>
                    <div id="cumplResumen" class="de-resumen-list"></div>
                </div>
            </div>
        </div>

        <!-- ===== Panel Clientes & Tendencia ===== -->
        <div id="panel-operativo" class="de-tab-panel">
            <div class="de-row">
                <div class="de-chart de-col-12">
                    <h3>Tendencia mensual por pedido (Top 5)</h3>
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

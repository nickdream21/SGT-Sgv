<%@ Page Title="Dashboard de Exportacion" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DashboardExportacion.aspx.cs" Inherits="WebSGV.Views.Exportacion.DashboardExportacion" %>

<asp:Content ID="ContentDE" ContentPlaceHolderID="MainContent" runat="server">
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/Exportacion/DashboardExportacion.css") %>" rel="stylesheet" />

    <div class="de-wrap">
        <div class="de-header">
            <div>
                <h1 class="de-title">Dashboard de exportaci&oacute;n</h1>
                <div class="de-subtitle">Viajes de Vitapro Per&uacute; &rarr; Ecuador &middot; tiempos medidos con los hitos del GPS</div>
            </div>
            <a href="RegistroSeguimiento.aspx" class="de-btn de-btn-ghost">
                <i class="fas fa-arrow-left"></i> Tablero del seguimiento
            </a>
        </div>

        <asp:Panel ID="pnlAlert" runat="server" Visible="false" CssClass="de-alert de-alert-danger">
            <asp:Literal ID="litAlert" runat="server"></asp:Literal>
        </asp:Panel>

        <!-- ===== Operación ahora (no depende del período) ===== -->
        <section class="de-panel">
            <div class="de-panel-head">
                <h2><span class="de-live"></span>Operaci&oacute;n ahora</h2>
                <div class="de-panel-meta" id="opMeta"></div>
            </div>
            <div class="de-ruta" id="opRuta"></div>
            <div class="de-flota" id="opFlota"></div>
            <div id="opAlertas"></div>
        </section>

        <!-- ===== Filtros del período ===== -->
        <div class="de-filters">
            <button type="button" class="de-btn de-btn-ghost de-btn-icon" id="btnMesAnterior" title="Mes anterior" aria-label="Mes anterior"><i class="fas fa-chevron-left"></i></button>
            <div class="de-filter">
                <label for="<%= ddlMes.ClientID %>">Mes</label>
                <asp:DropDownList ID="ddlMes" runat="server">
                    <asp:ListItem Text="Todo el año" Value="0" />
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
                <label for="<%= ddlAnio.ClientID %>">A&ntilde;o</label>
                <asp:DropDownList ID="ddlAnio" runat="server" />
            </div>
            <button type="button" class="de-btn de-btn-ghost de-btn-icon" id="btnMesSiguiente" title="Mes siguiente" aria-label="Mes siguiente"><i class="fas fa-chevron-right"></i></button>
            <asp:Button ID="btnFiltrar" runat="server" Text="Aplicar" CssClass="de-btn de-btn-primary" OnClick="btnFiltrar_Click" />
            <asp:Button ID="btnMesActual" runat="server" Text="Mes actual" CssClass="de-btn de-btn-ghost" OnClick="btnMesActual_Click" CausesValidation="false" />
            <div class="de-filters-note" id="periodoNota"></div>
        </div>

        <!-- ===== Indicadores del período ===== -->
        <div class="de-kpis" id="kpis"></div>

        <!-- ===== Composición del ciclo y tiempo por tramo ===== -->
        <div class="de-row">
            <section class="de-panel de-col-4">
                <div class="de-panel-head">
                    <h2>Composici&oacute;n del ciclo</h2>
                    <div class="de-panel-meta">Suma de medianas por fase</div>
                </div>
                <div class="de-canvas de-canvas-dona"><canvas id="chartFases" aria-label="Horas del ciclo por fase" role="img"></canvas></div>
                <div class="de-fases-leyenda" id="fasesLeyenda"></div>
            </section>
            <section class="de-panel de-col-8">
                <div class="de-panel-head">
                    <h2>Tiempo t&iacute;pico por tramo</h2>
                    <div class="de-panel-meta">Barra = mediana &middot; fondo claro = P90 &middot; clic para ver su evoluci&oacute;n</div>
                </div>
                <div class="de-canvas de-canvas-tramos"><canvas id="chartTramos" aria-label="Mediana y P90 de cada tramo" role="img"></canvas></div>
            </section>
        </div>

        <div class="de-row">
            <section class="de-panel de-col-7">
                <div class="de-panel-head">
                    <h2 id="evolTitulo">Evoluci&oacute;n del tramo</h2>
                    <div class="de-panel-meta" id="evolMeta"></div>
                </div>
                <div class="de-chips" id="evolChips"></div>
                <div class="de-canvas"><canvas id="chartEvolucion" aria-label="Evolución del tramo seleccionado" role="img"></canvas></div>
            </section>
            <section class="de-panel de-col-5">
                <div class="de-panel-head">
                    <h2>Llegada a Trujillo frente a lo programado</h2>
                    <div class="de-panel-meta" id="cumplMeta"></div>
                </div>
                <div class="de-canvas de-canvas-con-chips"><canvas id="chartCumplimiento" aria-label="Llegadas a tiempo y tarde por día" role="img"></canvas></div>
            </section>
        </div>

        <div class="de-row">
            <section class="de-panel de-col-5">
                <div class="de-panel-head">
                    <h2>Plantas de descarga</h2>
                    <div class="de-panel-meta">Mediana en horas</div>
                </div>
                <div class="de-plantas-resumen" id="plantasResumen"></div>
                <div class="de-canvas de-canvas-plantas"><canvas id="chartPlantas" aria-label="Tiempos por planta de descarga" role="img"></canvas></div>
            </section>
            <section class="de-panel de-col-7">
                <div class="de-panel-head">
                    <h2>Viajes con mayor tr&aacute;nsito</h2>
                    <div class="de-panel-meta">En qu&eacute; fase se les fue el tiempo &middot; l&iacute;nea = tr&aacute;nsito t&iacute;pico</div>
                </div>
                <div class="de-lentos" id="listaLentos"></div>
            </section>
        </div>

        <!-- ===== Detalle por tramo (tabla) ===== -->
        <section class="de-panel">
            <button type="button" class="de-plegable" id="btnDetalle" aria-expanded="false" aria-controls="detalleTramos">
                <span><i class="fas fa-table"></i> Detalle por tramo <span class="de-panel-meta">mediana, rango t&iacute;pico, P90 y comparaci&oacute;n</span></span>
                <i class="fas fa-chevron-down chev"></i>
            </button>
            <div id="detalleTramos" hidden>
                <div class="de-table-wrap">
                    <table class="de-table de-tramos" id="tablaTramos">
                        <thead>
                            <tr>
                                <th>Tramo</th>
                                <th class="num">Mediana</th>
                                <th class="de-col-rango">Rango t&iacute;pico <span class="de-th-hint">P25&ndash;P75 &middot; marca = P90</span></th>
                                <th class="num">P90</th>
                                <th class="num">Viajes</th>
                                <th class="num" id="thAnterior">vs. anterior</th>
                            </tr>
                        </thead>
                        <tbody></tbody>
                    </table>
                </div>
            </div>
        </section>

        <section class="de-panel">
            <div class="de-panel-head">
                <h2>Pedidos del per&iacute;odo</h2>
                <div class="de-panel-meta" id="pedidosMeta"></div>
            </div>
            <div class="de-table-wrap">
                <table class="de-table" id="tablaPedidos">
                    <thead>
                        <tr>
                            <th>Pedido</th>
                            <th>Planta</th>
                            <th>Programaci&oacute;n</th>
                            <th class="num">Camiones</th>
                            <th class="de-col-avance">Descargados</th>
                            <th class="num">Tr&aacute;nsito (mediana)</th>
                        </tr>
                    </thead>
                    <tbody></tbody>
                </table>
            </div>
        </section>

        <p class="de-foot">
            Las medianas muestran el viaje t&iacute;pico sin que uno atascado las mueva; el P90 es el tiempo que no supera 9 de cada 10 viajes.
            Se descartan los tramos negativos o de m&aacute;s de 20 d&iacute;as (dato mal puesto). Inicio y t&eacute;rmino de carga y de descarga
            no se registran, por eso la planta se mide de ingreso a salida. El per&iacute;odo se toma por F.H. Programaci&oacute;n; la comparaci&oacute;n
            con el per&iacute;odo anterior solo se muestra si este tiene al menos 10 viajes con el dato.
        </p>

        <asp:HiddenField ID="hdnDatos" runat="server" />
    </div>

    <script>
        // Valores del servidor que usa el script de la página.
        var SGV = Object.assign(window.SGV || {}, {
            hdnDatos: '<%= hdnDatos.ClientID %>',
            ddlMes: '<%= ddlMes.ClientID %>',
            ddlAnio: '<%= ddlAnio.ClientID %>',
            btnFiltrar: '<%= btnFiltrar.ClientID %>',
            urlViaje: '<%= ResolveUrl("~/Views/Exportacion/SeguimientoViaje.aspx") %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/Exportacion/DashboardExportacion.js") %>"></script>
</asp:Content>

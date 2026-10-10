<%@ Page Title="Dashboard - SGV" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="WebSGV.Views.Dashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <!-- Añadir UpdatePanel -->
    <asp:UpdatePanel ID="upDashboard" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <!-- Timer para actualización automática (opcional) -->
            <asp:Timer ID="tmrRefresh" runat="server" Interval="60000" OnTick="tmrRefresh_Tick" Enabled="false" />

            <div class="dashboard-container">
                <div class="dashboard-header">
                    <h1 class="dashboard-title">Indicadores de servicios generales</h1>
                    <!-- Botón server-side -->
                    <asp:Button ID="btnRefresh" runat="server" Text="Actualizar" OnClick="btnRefresh_Click" CssClass="refresh-btn" />
                </div>

                <div class="date-filter">
                    <label for="ddlMes">Mes:</label>
                    <asp:DropDownList ID="ddlMes" runat="server" CssClass="filter-dropdown">
                        <asp:ListItem Value="1">Enero</asp:ListItem>
                        <asp:ListItem Value="2">Febrero</asp:ListItem>
                        <asp:ListItem Value="3">Marzo</asp:ListItem>
                        <asp:ListItem Value="4">Abril</asp:ListItem>
                        <asp:ListItem Value="5">Mayo</asp:ListItem>
                        <asp:ListItem Value="6">Junio</asp:ListItem>
                        <asp:ListItem Value="7">Julio</asp:ListItem>
                        <asp:ListItem Value="8">Agosto</asp:ListItem>
                        <asp:ListItem Value="9">Septiembre</asp:ListItem>
                        <asp:ListItem Value="10">Octubre</asp:ListItem>
                        <asp:ListItem Value="11">Noviembre</asp:ListItem>
                        <asp:ListItem Value="12">Diciembre</asp:ListItem>
                    </asp:DropDownList>

                    <label for="ddlAnio">Año:</label>
                    <asp:DropDownList ID="ddlAnio" runat="server" CssClass="filter-dropdown">
                    </asp:DropDownList>

                    <asp:Button ID="btnFiltrar" runat="server" Text="Aplicar filtro" OnClick="btnFiltrar_Click" CssClass="refresh-btn" />
                    <!-- Agregar justo debajo del botón de filtro -->
                    <asp:Button ID="btnDiagnostico" runat="server" Text="Diagnóstico" OnClick="btnDiagnostico_Click" CssClass="refresh-btn refresh-btn-secundario" />
                </div>

                <!-- Tabs de navegación usando LinkButtons para postbacks limpios -->
                <div class="tabs-container">
                    <asp:LinkButton ID="lnkGeneral" runat="server" CssClass="tab active" OnClick="lnkGeneral_Click">General</asp:LinkButton>
                    <asp:LinkButton ID="lnkTramiteAduanero" runat="server" CssClass="tab" OnClick="lnkTramiteAduanero_Click">Trámite aduanero</asp:LinkButton>
                    <asp:LinkButton ID="lnkTiemposAdicionales" runat="server" CssClass="tab" OnClick="lnkTiemposAdicionales_Click">Tiempos adicionales</asp:LinkButton>
                    <asp:LinkButton ID="lnkBodegasDistancias" runat="server" CssClass="tab" OnClick="lnkBodegasDistancias_Click">Bodegas y distancias</asp:LinkButton>
                </div>

                <div id="loader" class="loader" runat="server" visible="false"></div>

                <!-- Panel 1: General -->
                <asp:Panel ID="pnlGeneral" runat="server" CssClass="tab-content active">
                    <div class="dashboard-row">
                        <div class="kpi-card">
                            <div class="kpi-title">Cumplimiento de hora programada</div>
                            <div class="kpi-value">
                                <asp:Literal ID="litCumplimiento" runat="server">0</asp:Literal>
                            </div>
                        </div>
                        <div class="kpi-card">
                            <div class="kpi-title">Total camiones</div>
                            <div class="kpi-value">
                                <asp:Literal ID="litCamiones" runat="server">0</asp:Literal>
                            </div>
                        </div>
                        <div class="kpi-card">
                            <div class="kpi-title">Total pedidos</div>
                            <div class="kpi-value">
                                <asp:Literal ID="litPedidos" runat="server">0</asp:Literal>
                            </div>
                        </div>
                        <div class="kpi-card">
                            <div class="kpi-title">Camiones por pedido</div>
                            <div class="kpi-value">
                                <asp:Literal ID="litCamionesPedido" runat="server">0</asp:Literal>
                            </div>
                        </div>
                    </div>

                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">Tiempos promedio en Trujillo (h)</div>
                            <canvas id="chartTrujillo"></canvas>
                        </div>
                    </div>

                    <div class="chart-row">
                        <div class="chart-container half">
                            <div class="chart-title">Tiempo promedio de Trujillo a planta Ecuador (días)</div>
                            <canvas id="chartTrujilloEcuador"></canvas>
                        </div>
                        <div class="chart-container half">
                            <div class="chart-title">Tiempo promedio en base (h)</div>
                            <canvas id="chartBase"></canvas>
                        </div>
                    </div>

                    <div class="chart-row">
                        <div class="chart-container half">
                            <div class="chart-title">Tiempos promedio en Inbalnor (h)</div>
                            <canvas id="chartInbalnor"></canvas>
                        </div>
                        <div class="chart-container half">
                            <div class="chart-title">Tiempos promedio en Jave (h)</div>
                            <canvas id="chartJave"></canvas>
                        </div>
                    </div>
                </asp:Panel>

                <!-- Panel 2: Trámite Aduanero -->
                <!-- Panel 2: Trámite Aduanero -->
                <asp:Panel ID="pnlTramiteAduanero" runat="server" CssClass="tab-content" Visible="false">
                    <div class="dashboard-row">
                        <div class="kpi-card">
                            <div class="kpi-title">Promedio Depsa</div>
                            <div class="kpi-value">
                                <asp:Literal ID="litTotalPromDepsa" runat="server">0</asp:Literal>
                            </div>
                        </div>
                        <div class="kpi-card">
                            <div class="kpi-title">Promedio Complex</div>
                            <div class="kpi-value">
                                <asp:Literal ID="litTotalPromComplex" runat="server">0</asp:Literal>
                            </div>
                        </div>
                    </div>

                    <!-- Cambiar esta parte - Grafico 1 -->
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">Espera para ingresar a Depsa (h)</div>
                            <canvas id="chartEsperaDepsa"></canvas>
                        </div>
                    </div>

                    <!-- Grafico 2 -->
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">Espera para ingresar a Complex (h)</div>
                            <canvas id="chartEsperaComplex"></canvas>
                        </div>
                    </div>

                    <!-- Grafico 3 -->
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">Tiempo promedio en Depsa</div>
                            <canvas id="chartTiempoDepsa"></canvas>
                        </div>
                    </div>

                    <!-- Grafico 4 -->
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">Tiempo promedio en Complex</div>
                            <canvas id="chartTiempoComplex"></canvas>
                        </div>
                    </div>

                    <!-- Añadir antes del gráfico CEBAF -->
                    <div class="total-prom-container">
                        <div class="total-prom-label">Promedio Cebaf (min)</div>
                        <div class="total-prom-value">
                            <asp:Literal ID="litTotalPromCebaf" runat="server">0</asp:Literal>
                        </div>
                    </div>

                    <!-- Luego el gráfico -->
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">Tiempo promedio en Cebaf (min)</div>
                            <canvas id="chartTiempoCebaf"></canvas>
                        </div>
                    </div>

                </asp:Panel>

                <!-- Panel 3: Tiempos Adicionales -->
                <!-- Panel 3: Tiempos Adicionales -->
                <asp:Panel ID="pnlTiemposAdicionales" runat="server" CssClass="tab-content" Visible="false">
                    <div class="dashboard-row">
                        <div class="kpi-card">
                            <div class="kpi-title">Promedio TCI</div>
                            <div class="kpi-value">
                                <asp:Literal ID="litTotalPromTCI" runat="server">0</asp:Literal>
                            </div>
                        </div>
                        <div class="kpi-card">
                            <div class="kpi-title">Promedio Puyango</div>
                            <div class="kpi-value">
                                <asp:Literal ID="litTotalPromPuyango" runat="server">0</asp:Literal>
                            </div>
                        </div>
                    </div>

                    <!-- Grafico 1 -->
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">Tiempo promedio en TCI (h)</div>
                            <canvas id="chartTiempoTCI"></canvas>
                        </div>
                    </div>

                    <!-- Grafico 3 -->
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">Espera de nacionalización (h)</div>
                            <canvas id="chartEsperaNacionalizacion"></canvas>
                        </div>
                    </div>
                </asp:Panel>

                <!-- Panel 4: Bodegas y Distancias -->
                <!-- Panel 4: Bodegas y Distancias -->
                <asp:Panel ID="pnlBodegasDistancias" runat="server" CssClass="tab-content" Visible="false">
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">Tiempo promedio de bodega nacional a Inbalnor (h)</div>
                            <canvas id="chartBodegaInbalnor"></canvas>
                        </div>
                    </div>

                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">Tiempo promedio de bodega nacional a Jave (h)</div>
                            <canvas id="chartBodegaJave"></canvas>
                        </div>
                    </div>

                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">Tiempo promedio de bodega ecuatoriana a Inbalnor (h)</div>
                            <canvas id="chartEcuatorianaInbalnor"></canvas>
                        </div>
                    </div>

                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">Tiempo promedio de bodega ecuatoriana a Jave (h)</div>
                            <canvas id="chartEcuatorianaJave"></canvas>
                        </div>
                    </div>
                </asp:Panel>

                <!-- Div oculto para almacenar datos JSON para los gráficos -->
                <asp:HiddenField ID="hdnDatosGraficos" runat="server" />
            </div>
        </ContentTemplate>
        <Triggers>
            <asp:AsyncPostBackTrigger ControlID="btnFiltrar" EventName="Click" />
            <asp:AsyncPostBackTrigger ControlID="btnRefresh" EventName="Click" />
            <asp:AsyncPostBackTrigger ControlID="tmrRefresh" EventName="Tick" />
            <asp:AsyncPostBackTrigger ControlID="lnkGeneral" EventName="Click" />
            <asp:AsyncPostBackTrigger ControlID="lnkTramiteAduanero" EventName="Click" />
            <asp:AsyncPostBackTrigger ControlID="lnkTiemposAdicionales" EventName="Click" />
            <asp:AsyncPostBackTrigger ControlID="lnkBodegasDistancias" EventName="Click" />
        </Triggers>
    </asp:UpdatePanel>

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/Dashboard.css") %>" rel="stylesheet" />
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="ScriptsSection" runat="server">
    <script src="https://cdn.jsdelivr.net/npm/chart.js@3.7.1/dist/chart.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/chartjs-plugin-datalabels@2.0.0"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.0.0-beta3/js/all.min.js"></script>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hdnDatosGraficos: '<%= hdnDatosGraficos.ClientID %>',
            ddlMes: '<%= ddlMes.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/Dashboard.js") %>"></script>
</asp:Content>

<%@ Page Title="Dashboard - SGV" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="WebSGV.Views.Dashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <!-- Añadir UpdatePanel -->
    <asp:UpdatePanel ID="upDashboard" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <!-- Timer para actualización automática (opcional) -->
            <asp:Timer ID="tmrRefresh" runat="server" Interval="60000" OnTick="tmrRefresh_Tick" Enabled="false" />

            <div class="dashboard-container">
                <div class="dashboard-header">
                    <h1 class="dashboard-title">INDICADORES - SERVICIOS GENERALES VIVIANA E.I.R.L</h1>
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
                        <asp:ListItem Value="2024">2024</asp:ListItem>
                        <asp:ListItem Value="2025">2025</asp:ListItem>
                    </asp:DropDownList>

                    <asp:Button ID="btnFiltrar" runat="server" Text="Aplicar filtro" OnClick="btnFiltrar_Click" CssClass="refresh-btn" />
                    <!-- Agregar justo debajo del botón de filtro -->
                    <asp:Button ID="btnDiagnostico" runat="server" Text="Diagnóstico" OnClick="btnDiagnostico_Click" CssClass="refresh-btn" />
                </div>

                <!-- Tabs de navegación usando LinkButtons para postbacks limpios -->
                <div class="tabs-container">
                    <asp:LinkButton ID="lnkGeneral" runat="server" CssClass="tab active" OnClick="lnkGeneral_Click">General</asp:LinkButton>
                    <asp:LinkButton ID="lnkTramiteAduanero" runat="server" CssClass="tab" OnClick="lnkTramiteAduanero_Click">Trámite Aduanero</asp:LinkButton>
                    <asp:LinkButton ID="lnkTiemposAdicionales" runat="server" CssClass="tab" OnClick="lnkTiemposAdicionales_Click">Tiempos Adicionales</asp:LinkButton>
                    <asp:LinkButton ID="lnkBodegasDistancias" runat="server" CssClass="tab" OnClick="lnkBodegasDistancias_Click">Bodegas y Distancias</asp:LinkButton>
                </div>

                <div id="loader" class="loader" runat="server" visible="false"></div>

                <!-- Panel 1: General -->
                <asp:Panel ID="pnlGeneral" runat="server" CssClass="tab-content active">
                    <div class="dashboard-row">
                        <div class="kpi-card">
                            <div class="kpi-title">% Cump hora prog.</div>
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
                            <div class="kpi-title">Total Pedidos</div>
                            <div class="kpi-value">
                                <asp:Literal ID="litPedidos" runat="server">0</asp:Literal>
                            </div>
                        </div>
                        <div class="kpi-card">
                            <div class="kpi-title">Camiones x Pedido</div>
                            <div class="kpi-value">
                                <asp:Literal ID="litCamionesPedido" runat="server">0</asp:Literal>
                            </div>
                        </div>
                    </div>

                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">Tiempos promedio en Trujillo (Hrs)</div>
                            <canvas id="chartTrujillo"></canvas>
                        </div>
                    </div>

                    <div class="chart-row">
                        <div class="chart-container half">
                            <div class="chart-title">Tiempo prom. deTrujillo-Planta Ecuador (días)</div>
                            <canvas id="chartTrujilloEcuador"></canvas>
                        </div>
                        <div class="chart-container half">
                            <div class="chart-title">Tiempo promedio en Base (Hrs)</div>
                            <canvas id="chartBase"></canvas>
                        </div>
                    </div>

                    <div class="chart-row">
                        <div class="chart-container half">
                            <div class="chart-title">Tiempos promedio en Inbalnor (Hrs)</div>
                            <canvas id="chartInbalnor"></canvas>
                        </div>
                        <div class="chart-container half">
                            <div class="chart-title">Tiempos promedio en Jave (Hrs)</div>
                            <canvas id="chartJave"></canvas>
                        </div>
                    </div>
                </asp:Panel>

                <!-- Panel 2: Trámite Aduanero -->
                <!-- Panel 2: Trámite Aduanero -->
                <asp:Panel ID="pnlTramiteAduanero" runat="server" CssClass="tab-content" Visible="false">
                    <div class="dashboard-row">
                        <div class="kpi-card">
                            <div class="kpi-title">TOTAL PROM.</div>
                            <div class="kpi-value">
                                <asp:Literal ID="litTotalPromDepsa" runat="server">0</asp:Literal>
                            </div>
                        </div>
                        <div class="kpi-card">
                            <div class="kpi-title">TOTAL PROM.</div>
                            <div class="kpi-value">
                                <asp:Literal ID="litTotalPromComplex" runat="server">0</asp:Literal>
                            </div>
                        </div>
                    </div>

                    <!-- Cambiar esta parte - Grafico 1 -->
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">ESPERA PARA INGRESAR A DEPSA (HRS)</div>
                            <canvas id="chartEsperaDepsa"></canvas>
                        </div>
                    </div>

                    <!-- Grafico 2 -->
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">ESPERA PARA INGRESAR A COMPLEX (HRS)</div>
                            <canvas id="chartEsperaComplex"></canvas>
                        </div>
                    </div>

                    <!-- Grafico 3 -->
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">TIEMPO PROMEDIO EN DEPSA</div>
                            <canvas id="chartTiempoDepsa"></canvas>
                        </div>
                    </div>

                    <!-- Grafico 4 -->
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">TIEMPO PROMEDIO EN COMPLEX</div>
                            <canvas id="chartTiempoComplex"></canvas>
                        </div>
                    </div>

                    <!-- Añadir antes del gráfico CEBAF -->
                    <div class="total-prom-container">
                        <div class="total-prom-label">TOTAL PROM.</div>
                        <div class="total-prom-value">
                            <asp:Literal ID="litTotalPromCebaf" runat="server">0</asp:Literal>
                        </div>
                    </div>

                    <!-- Luego el gráfico -->
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">TIEMPO PROMEDIO EN CEBAF (MIN)</div>
                            <canvas id="chartTiempoCebaf"></canvas>
                        </div>
                    </div>

                </asp:Panel>

                <!-- Panel 3: Tiempos Adicionales -->
                <!-- Panel 3: Tiempos Adicionales -->
                <asp:Panel ID="pnlTiemposAdicionales" runat="server" CssClass="tab-content" Visible="false">
                    <div class="dashboard-row">
                        <div class="kpi-card">
                            <div class="kpi-title">TOTAL PROM.</div>
                            <div class="kpi-value">
                                <asp:Literal ID="litTotalPromTCI" runat="server">0</asp:Literal>
                            </div>
                        </div>
                        <div class="kpi-card">
                            <div class="kpi-title">TOTAL PROM.</div>
                            <div class="kpi-value">
                                <asp:Literal ID="litTotalPromPuyango" runat="server">0</asp:Literal>
                            </div>
                        </div>
                    </div>

                    <!-- Grafico 1 -->
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">TIEMPO PROMEDIO EN TCI (HRS)</div>
                            <canvas id="chartTiempoTCI"></canvas>
                        </div>
                    </div>

                    <!-- Grafico 3 -->
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">ESPERA DE NACIONALIZACIÓN (HRS)</div>
                            <canvas id="chartEsperaNacionalizacion"></canvas>
                        </div>
                    </div>
                </asp:Panel>

                <!-- Panel 4: Bodegas y Distancias -->
                <!-- Panel 4: Bodegas y Distancias -->
                <asp:Panel ID="pnlBodegasDistancias" runat="server" CssClass="tab-content" Visible="false">
                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">TIEMPO PROMEDIO DE BODEGA NACIONAL A INBALNOR (HRS)</div>
                            <canvas id="chartBodegaInbalnor"></canvas>
                        </div>
                    </div>

                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">TIEMPO PROMEDIO DE BODEGA NACIONAL A JAVE (HRS)</div>
                            <canvas id="chartBodegaJave"></canvas>
                        </div>
                    </div>

                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">TIEMPO PROMEDIO DE BODEGA ECUATORIANA A BODEGA INBALNOR (HRS)</div>
                            <canvas id="chartEcuatorianaInbalnor"></canvas>
                        </div>
                    </div>

                    <div class="chart-row">
                        <div class="chart-container full">
                            <div class="chart-title">TIEMPO PROMEDIO DE BODEGA ECUATORIANA A BODEGA JAVE (HRS)</div>
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

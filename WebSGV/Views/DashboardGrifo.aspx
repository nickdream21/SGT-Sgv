<%@ Page Title="Dashboard - Administrador de Grifo" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DashboardGrifo.aspx.cs" Inherits="WebSGV.Views.DashboardGrifo" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfTabActiva" runat="server" ClientIDMode="Static" Value="viajes" />

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/DashboardGrifo.css") %>" rel="stylesheet" />

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
                <%= WebSGV.Helpers.FechaHelper.Ahora().ToString("dddd, dd/MM/yyyy HH:mm", new System.Globalization.CultureInfo("es-PE")) %>
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
                            placeholder="Nº, conductor o placa..."></asp:TextBox>
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
                                        <i class="fas fa-eye"></i> Ver / Editar
                                    </a>
                                    <a href='<%# "DescargarPdfAbastecimiento.aspx?num=" + HttpUtility.UrlEncode(Eval("NumeroAbastecimiento").ToString()) %>'
                                       target="_blank" class="btn-ver-card" style="background:#28a745; margin-left:6px;">
                                        <i class="fas fa-file-pdf"></i> PDF
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

    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/DashboardGrifo.js") %>"></script>

</asp:Content>

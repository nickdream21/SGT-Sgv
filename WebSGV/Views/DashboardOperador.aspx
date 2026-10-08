<%@ Page Title="Mi Dashboard - Operador" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DashboardOperador.aspx.cs" Inherits="WebSGV.Views.DashboardOperador" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdOperador" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdAsignacion" runat="server" ClientIDMode="Static" />

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/DashboardOperador.css") %>" rel="stylesheet" />

    <div class="dash-op">

        <!-- ========== HEADER ========== -->
        <div class="op-header">
            <h2><i class="fas fa-hard-hat mr-2"></i>Parte Diario de Trabajo</h2>
            <p class="op-sub">
                <strong><asp:Label ID="lblNombreOperador" runat="server"></asp:Label></strong>
                &nbsp;|&nbsp; DNI: <asp:Label ID="lblDNIOperador" runat="server"></asp:Label>
            </p>
            <span class="op-fecha-badge">
                <i class="fas fa-calendar-alt mr-1"></i>
                <asp:Label ID="lblFechaHoy" runat="server"></asp:Label>
            </span>
        </div>

        <!-- ========== ASIGNACION ACTIVA ========== -->
        <asp:Panel ID="pnlAsignacion" runat="server" Visible="false">
            <div class="asig-card">
                <div class="asig-title">
                    <span><i class="fas fa-clipboard-check mr-1"></i>ASIGNACI&#211;N ACTIVA</span>
                    <span class="asig-badge"><i class="fas fa-check-circle mr-1"></i>Vigente</span>
                </div>
                <div class="asig-grid">
                    <div class="asig-item">
                        <div class="asig-label">Placa Equipo</div>
                        <div class="asig-value"><asp:Label ID="lblPlacaEquipo" runat="server"></asp:Label></div>
                    </div>
                    <div class="asig-item">
                        <div class="asig-label">Tipo</div>
                        <div class="asig-value"><asp:Label ID="lblTipoEquipo" runat="server"></asp:Label></div>
                    </div>
                    <div class="asig-item">
                        <div class="asig-label">Cliente</div>
                        <div class="asig-value"><asp:Label ID="lblClienteObra" runat="server"></asp:Label></div>
                    </div>
                    <div class="asig-item">
                        <div class="asig-label">Obra</div>
                        <div class="asig-value"><asp:Label ID="lblNombreObra" runat="server"></asp:Label></div>
                    </div>
                </div>
            </div>
        </asp:Panel>

        <!-- ========== SIN ASIGNACION ========== -->
        <asp:Panel ID="pnlSinAsignacion" runat="server" Visible="false">
            <div class="sin-asig">
                <div class="sa-icon">
                    <i class="fas fa-hard-hat"></i>
                </div>
                <h4>No tienes asignaci&#243;n activa</h4>
                <p>El Administrador de Maquinaria a&#250;n no te ha asignado<br />
                   un equipo, cliente u obra de trabajo.</p>
                <div class="sa-status-bar">
                    <i class="fas fa-clock"></i>
                    <span>En espera de asignaci&#243;n por tu supervisor</span>
                </div>
                <p class="sa-tip">
                    <i class="fas fa-info-circle mr-1"></i>
                    Cuando te asignen una obra, aqu&#237; aparecer&#225;n<br />
                    los datos y podr&#225;s registrar tu parte diario.
                </p>
            </div>
        </asp:Panel>

        <!-- ========== HISTORIAL SIN ASIGNACION (partes previos) ========== -->
        <asp:Panel ID="pnlHistorialSinAsignacion" runat="server" Visible="false">
            <div style="margin: 0 12px;">
                <div class="sec-card">
                    <div class="sec-header">
                        <i class="fas fa-history mr-1"></i>Mi Historial de Partes
                    </div>
                    <div class="sec-body">
                        <asp:Panel ID="pnlHistSA" runat="server">
                            <div class="hist-summary">
                                <div class="hist-stat">
                                    <div class="stat-num"><asp:Label ID="lblTotalPartesSA" runat="server" Text="0"></asp:Label></div>
                                    <div class="stat-label">Partes</div>
                                </div>
                                <div class="hist-stat">
                                    <div class="stat-num"><asp:Label ID="lblTotalHorasSA" runat="server" Text="0"></asp:Label></div>
                                    <div class="stat-label">Horas Trabajo</div>
                                </div>
                            </div>
                            <asp:Repeater ID="rptHistorialSA" runat="server">
                                <ItemTemplate>
                                    <div class="hist-card">
                                        <div class="hist-card-top">
                                            <div>
                                                <div class="hist-card-num"><%# Eval("numeroParte") %></div>
                                                <div class="hist-card-fecha">
                                                    <i class="fas fa-calendar-alt mr-1"></i><%# Eval("fecha", "{0:dd/MM/yyyy}") %>
                                                </div>
                                            </div>
                                            <span class='hist-card-badge <%# GetBadgeClass(Eval("estado").ToString()) %>'>
                                                <%# Eval("estado") %>
                                            </span>
                                        </div>
                                        <div class="hist-card-body">
                                            <div class="hc-item">
                                                <div class="hc-label">Placa</div>
                                                <div class="hc-val"><%# Eval("placa") %></div>
                                            </div>
                                            <div class="hc-item">
                                                <div class="hc-label">Cliente</div>
                                                <div class="hc-val"><%# Eval("nombreCliente") %></div>
                                            </div>
                                            <div class="hc-item">
                                                <div class="hc-label">Obra</div>
                                                <div class="hc-val"><%# Eval("nombreObra") %></div>
                                            </div>
                                            <div class="hc-item">
                                                <div class="hc-label">Horas</div>
                                                <div class="hc-val"><%# Eval("horometroHoras") != DBNull.Value ? string.Format("{0:N1}h", Eval("horometroHoras")) : "--" %></div>
                                            </div>
                                        </div>
                                        <div class="hist-card-labor" style='<%# string.IsNullOrEmpty(Eval("labor").ToString()) ? "display:none" : "" %>'>
                                            <i class="fas fa-wrench"></i><%# Eval("labor") %>
                                        </div>
                                    </div>
                                </ItemTemplate>
                            </asp:Repeater>
                        </asp:Panel>
                        <asp:Panel ID="pnlHistSAVacio" runat="server" Visible="false">
                            <div class="hist-empty">
                                <i class="fas fa-inbox"></i>
                                <p>A&#250;n no tienes partes diarios registrados.</p>
                            </div>
                        </asp:Panel>
                    </div>
                </div>
            </div>
        </asp:Panel>

        <!-- ========== CONTENIDO PRINCIPAL ========== -->
        <asp:Panel ID="pnlContenido" runat="server" Visible="false">

            <!-- TABS -->
            <ul class="nav op-tabs" id="operadorTabs" role="tablist">
                <li class="nav-item">
                    <a class="nav-link active" id="nuevoParte-tab" data-toggle="tab" href="#nuevoParte" role="tab">
                        <i class="fas fa-plus-circle"></i> Nuevo Parte
                    </a>
                </li>
                <li class="nav-item">
                    <a class="nav-link" id="historial-tab" data-toggle="tab" href="#historial" role="tab">
                        <i class="fas fa-history"></i> Mi Historial
                        <asp:Label ID="lblCantidadPartes" runat="server" CssClass="badge badge-secondary" Visible="false"></asp:Label>
                    </a>
                </li>
            </ul>

            <div class="tab-content" id="operadorTabsContent">

                <!-- ====================================================
                     TAB 1: NUEVO PARTE DIARIO
                     ==================================================== -->
                <div class="tab-pane fade show active" id="nuevoParte" role="tabpanel">
                    <div class="tab-body">

                        <!-- Fecha -->
                        <div class="sec-card">
                            <div class="sec-body" style="padding:12px 14px;">
                                <div class="campo" style="margin-bottom:0;">
                                    <label><i class="fas fa-calendar-alt mr-1"></i>Fecha del Parte</label>
                                    <asp:TextBox ID="txtFechaParte" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                </div>
                            </div>
                        </div>

                        <!-- MEDICIONES: Odometro y Horometro -->
                        <div class="sec-card">
                            <div class="sec-header">
                                <i class="fas fa-tachometer-alt"></i> PARTE DIARIO DE TRABAJO
                            </div>
                            <div class="sec-body">

                                <!-- Od&#243;metro -->
                                <div class="medicion-card">
                                    <div class="medicion-titulo">
                                        <i class="fas fa-road"></i> OD&#211;METRO
                                    </div>
                                    <div class="medicion-inputs">
                                        <div class="campo" style="margin-bottom:0;">
                                            <label>Comienzo</label>
                                            <asp:TextBox ID="txtOdometroComienzo" runat="server" CssClass="form-control"
                                                placeholder="0.0" onchange="calcularDiferencias()" inputMode="decimal"></asp:TextBox>
                                        </div>
                                        <div class="campo" style="margin-bottom:0;">
                                            <label>T&#233;rmino</label>
                                            <asp:TextBox ID="txtOdometroTermino" runat="server" CssClass="form-control"
                                                placeholder="0.0" onchange="calcularDiferencias()" inputMode="decimal"></asp:TextBox>
                                        </div>
                                    </div>
                                    <div class="medicion-result">
                                        <span>KM Recorridos</span>
                                        <span class="result-val">
                                            <asp:TextBox ID="txtOdometroKmHoras" runat="server" CssClass="form-control"
                                                ReadOnly="true" placeholder="--"
                                                style="background:transparent;border:none;font-size:1.1rem;font-weight:700;color:#2d6a4f;text-align:right;width:80px;padding:0;height:auto;"></asp:TextBox>
                                        </span>
                                    </div>
                                </div>

                                <!-- Hor&#243;metro -->
                                <div class="medicion-card" style="margin-bottom:0;">
                                    <div class="medicion-titulo">
                                        <i class="fas fa-clock"></i> HOR&#211;METRO
                                    </div>
                                    <div class="medicion-inputs">
                                        <div class="campo" style="margin-bottom:0;">
                                            <label>Comienzo</label>
                                            <asp:TextBox ID="txtHorometroComienzo" runat="server" CssClass="form-control"
                                                placeholder="0.0" onchange="calcularDiferencias()" inputMode="decimal"></asp:TextBox>
                                        </div>
                                        <div class="campo" style="margin-bottom:0;">
                                            <label>T&#233;rmino</label>
                                            <asp:TextBox ID="txtHorometroTermino" runat="server" CssClass="form-control"
                                                placeholder="0.0" onchange="calcularDiferencias()" inputMode="decimal"></asp:TextBox>
                                        </div>
                                    </div>
                                    <div class="medicion-result">
                                        <span>Horas de Trabajo</span>
                                        <span class="result-val">
                                            <asp:TextBox ID="txtHorometroHoras" runat="server" CssClass="form-control"
                                                ReadOnly="true" placeholder="--"
                                                style="background:transparent;border:none;font-size:1.1rem;font-weight:700;color:#2d6a4f;text-align:right;width:80px;padding:0;height:auto;"></asp:TextBox>
                                        </span>
                                    </div>
                                </div>

                            </div>
                        </div>

                        <!-- CONSUMO -->
                        <div class="sec-card">
                            <div class="sec-header">
                                <i class="fas fa-gas-pump"></i> CONSUMO
                            </div>
                            <div class="sec-body">
                                <div class="consumo-grid">
                                    <div class="consumo-card">
                                        <i class="fas fa-oil-can ic-pet"></i>
                                        <div class="c-label">Petr&#243;leo (GL)</div>
                                        <asp:TextBox ID="txtConsumoPetroleo" runat="server" CssClass="form-control"
                                            placeholder="0.00" inputMode="decimal"></asp:TextBox>
                                    </div>
                                    <div class="consumo-card">
                                        <i class="fas fa-fire ic-gas"></i>
                                        <div class="c-label">Gasolina (GL)</div>
                                        <asp:TextBox ID="txtConsumoGasolina" runat="server" CssClass="form-control"
                                            placeholder="0.00" inputMode="decimal"></asp:TextBox>
                                    </div>
                                    <div class="consumo-card">
                                        <i class="fas fa-tint ic-ace"></i>
                                        <div class="c-label">Aceite (GL)</div>
                                        <asp:TextBox ID="txtConsumoAceite" runat="server" CssClass="form-control"
                                            placeholder="0.00" inputMode="decimal"></asp:TextBox>
                                    </div>
                                    <div class="consumo-card">
                                        <i class="fas fa-cog ic-gra"></i>
                                        <div class="c-label">Grasa (Und)</div>
                                        <asp:TextBox ID="txtConsumoGrasa" runat="server" CssClass="form-control"
                                            placeholder="0.00" inputMode="decimal"></asp:TextBox>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <!-- DESCRIPCION DEL TRABAJO -->
                        <div class="sec-card">
                            <div class="sec-header">
                                <i class="fas fa-clipboard-list"></i> DESCRIPCI&#211;N DEL TRABAJO
                            </div>
                            <div class="sec-body">
                                <div class="trabajo-row">
                                    <div class="campo">
                                        <label>Carretera</label>
                                        <asp:TextBox ID="txtCarretera" runat="server" CssClass="form-control"></asp:TextBox>
                                    </div>
                                    <div class="campo">
                                        <label>Sector</label>
                                        <asp:TextBox ID="txtSector" runat="server" CssClass="form-control"></asp:TextBox>
                                    </div>
                                    <div class="campo">
                                        <label>Sector KM</label>
                                        <asp:TextBox ID="txtSectorKm" runat="server" CssClass="form-control" inputMode="decimal"></asp:TextBox>
                                    </div>
                                    <div class="campo">
                                        <label>Al KM</label>
                                        <asp:TextBox ID="txtAlKm" runat="server" CssClass="form-control" inputMode="decimal"></asp:TextBox>
                                    </div>
                                </div>
                                <div class="trabajo-row" style="margin-top:4px;">
                                    <div class="campo trabajo-full">
                                        <label>Labor realizada</label>
                                        <asp:TextBox ID="txtLabor" runat="server" CssClass="form-control"
                                            placeholder="Describa el trabajo realizado"></asp:TextBox>
                                    </div>
                                </div>
                                <div class="trabajo-row">
                                    <div class="campo">
                                        <label>C&#243;digo</label>
                                        <asp:TextBox ID="txtCodigo" runat="server" CssClass="form-control"></asp:TextBox>
                                    </div>
                                    <div class="campo">
                                        <label>N&#176; de Viajes</label>
                                        <asp:TextBox ID="txtCantidadViajes" runat="server" CssClass="form-control"
                                            TextMode="Number" placeholder="0" inputMode="numeric"></asp:TextBox>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <!-- RECLAMO Y OBSERVACIONES -->
                        <div class="sec-card">
                            <div class="sec-header">
                                <i class="fas fa-comment-alt"></i> RECLAMO / OBSERVACIONES
                            </div>
                            <div class="sec-body">
                                <div class="campo">
                                    <label>Reclamo</label>
                                    <asp:TextBox ID="txtReclamo" runat="server" CssClass="form-control"
                                        TextMode="MultiLine" Rows="2" placeholder="Ingrese reclamo si aplica"></asp:TextBox>
                                </div>
                                <div class="campo" style="margin-bottom:0;">
                                    <label>Observaciones</label>
                                    <asp:TextBox ID="txtObservaciones" runat="server" CssClass="form-control"
                                        TextMode="MultiLine" Rows="2" placeholder="Observaciones adicionales (opcional)"></asp:TextBox>
                                </div>
                            </div>
                        </div>

                        <!-- Hidden para numero de parte -->
                        <asp:TextBox ID="txtNumeroParte" runat="server" CssClass="form-control" ReadOnly="true" style="display:none;"></asp:TextBox>

                        <!-- MENSAJE -->
                        <asp:Label ID="lblMensaje" runat="server" CssClass="op-msg"></asp:Label>

                        <!-- BOTONES -->
                        <div class="acciones-row">
                            <asp:Button ID="btnLimpiar" runat="server" CssClass="btn-limpiar" Text="Limpiar campos" OnClick="LimpiarFormulario" />
                            <asp:Button ID="btnGuardarParte" runat="server" CssClass="btn-registrar"
                                Text="&#10004;  Registrar Parte Diario" OnClick="GuardarParteDiario"
                                OnClientClick="return validarFormulario();" />
                        </div>

                    </div>
                </div>

                <!-- ====================================================
                     TAB 2: MI HISTORIAL
                     ==================================================== -->
                <div class="tab-pane fade" id="historial" role="tabpanel">
                    <div class="tab-body">

                        <!-- Resumen -->
                        <asp:Panel ID="pnlHistorial" runat="server" Visible="false">

                            <div class="hist-summary">
                                <div class="hist-stat">
                                    <div class="stat-num"><asp:Label ID="lblTotalPartes" runat="server" Text="0"></asp:Label></div>
                                    <div class="stat-label">Partes</div>
                                </div>
                                <div class="hist-stat">
                                    <div class="stat-num"><asp:Label ID="lblTotalHoras" runat="server" Text="0"></asp:Label></div>
                                    <div class="stat-label">Horas Trabajo</div>
                                </div>
                            </div>

                            <!-- Cards de historial -->
                            <asp:Repeater ID="rptHistorial" runat="server">
                                <ItemTemplate>
                                    <div class="hist-card">
                                        <div class="hist-card-top">
                                            <div>
                                                <div class="hist-card-num"><%# Eval("numeroParte") %></div>
                                                <div class="hist-card-fecha">
                                                    <i class="fas fa-calendar-alt mr-1"></i><%# Eval("fecha", "{0:dd/MM/yyyy}") %>
                                                </div>
                                            </div>
                                            <span class='hist-card-badge <%# GetBadgeClass(Eval("estado").ToString()) %>'>
                                                <%# Eval("estado") %>
                                            </span>
                                        </div>
                                        <div class="hist-card-body">
                                            <div class="hc-item">
                                                <div class="hc-label">Placa</div>
                                                <div class="hc-val"><%# Eval("placa") %></div>
                                            </div>
                                            <div class="hc-item">
                                                <div class="hc-label">Cliente</div>
                                                <div class="hc-val"><%# Eval("nombreCliente") %></div>
                                            </div>
                                            <div class="hc-item">
                                                <div class="hc-label">Obra</div>
                                                <div class="hc-val"><%# Eval("nombreObra") %></div>
                                            </div>
                                            <div class="hc-item">
                                                <div class="hc-label">Horas</div>
                                                <div class="hc-val"><%# Eval("horometroHoras") != DBNull.Value ? string.Format("{0:N1}h", Eval("horometroHoras")) : "--" %></div>
                                            </div>
                                        </div>
                                        <div class="hist-card-labor" style='<%# string.IsNullOrEmpty(Eval("labor").ToString()) ? "display:none" : "" %>'>
                                            <i class="fas fa-wrench"></i><%# Eval("labor") %>
                                        </div>
                                    </div>
                                </ItemTemplate>
                            </asp:Repeater>

                        </asp:Panel>

                        <!-- Historial vacio -->
                        <asp:Panel ID="pnlHistorialVacio" runat="server" Visible="false">
                            <div class="hist-empty">
                                <i class="fas fa-inbox"></i>
                                <p>A&#250;n no tienes partes diarios registrados.<br />
                                   Registra tu primer parte en la pesta&#241;a <strong>Nuevo Parte</strong>.</p>
                            </div>
                        </asp:Panel>

                    </div>
                </div>
            </div>
        </asp:Panel>
    </div>

    <!-- Scripts -->
    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            txtOdometroComienzo: '<%= txtOdometroComienzo.ClientID %>',
            txtOdometroTermino: '<%= txtOdometroTermino.ClientID %>',
            txtOdometroKmHoras: '<%= txtOdometroKmHoras.ClientID %>',
            txtHorometroComienzo: '<%= txtHorometroComienzo.ClientID %>',
            txtHorometroTermino: '<%= txtHorometroTermino.ClientID %>',
            txtHorometroHoras: '<%= txtHorometroHoras.ClientID %>',
            txtFechaParte: '<%= txtFechaParte.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/DashboardOperador.js") %>"></script>
</asp:Content>

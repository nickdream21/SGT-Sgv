<%@ Page Title="Mi Dashboard - Operador" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DashboardOperador.aspx.cs" Inherits="WebSGV.Views.DashboardOperador" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdOperador" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdAsignacion" runat="server" ClientIDMode="Static" />

    <style>
        /* ============================================
           DASHBOARD OPERADOR - Mobile First
           ============================================ */
        :root {
            --mq-primary: #2d6a4f;
            --mq-secondary: #40916c;
            --mq-accent: #b7e4c7;
            --mq-light: #f0faf4;
            --mq-border: #d8f3dc;
        }

        .dash-op {
            background: #f5f5f5;
            min-height: 100vh;
            padding-bottom: 30px;
        }

        /* --- HEADER --- */
        .op-header {
            background: linear-gradient(135deg, var(--mq-primary) 0%, var(--mq-secondary) 100%);
            color: white;
            padding: 16px;
        }

        .op-header h2 {
            font-size: 1.15rem;
            font-weight: 700;
            margin: 0 0 4px 0;
        }

        .op-header .op-sub {
            font-size: 0.82rem;
            opacity: 0.9;
            margin: 0;
        }

        .op-fecha-badge {
            display: inline-block;
            background: rgba(255,255,255,0.2);
            border-radius: 20px;
            padding: 4px 12px;
            font-size: 0.78rem;
            margin-top: 6px;
        }

        /* --- ASIGNACION CARD --- */
        .asig-card {
            background: white;
            border-radius: 10px;
            margin: 12px;
            padding: 14px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.06);
            border-left: 4px solid var(--mq-primary);
        }

        .asig-title {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 10px;
        }

        .asig-title span:first-child {
            font-weight: 700;
            color: var(--mq-primary);
            font-size: 0.8rem;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        .asig-badge {
            background: #28a745;
            color: white;
            font-size: 0.7rem;
            padding: 3px 10px;
            border-radius: 12px;
            font-weight: 600;
        }

        .asig-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 8px;
        }

        .asig-item .asig-label {
            font-size: 0.68rem;
            text-transform: uppercase;
            color: #999;
            font-weight: 600;
            letter-spacing: 0.3px;
        }

        .asig-item .asig-value {
            font-size: 0.92rem;
            font-weight: 700;
            color: #333;
            line-height: 1.2;
        }

        /* --- SIN ASIGNACION --- */
        .sin-asig {
            background: white;
            border-radius: 12px;
            margin: 12px;
            padding: 30px 20px;
            text-align: center;
            box-shadow: 0 2px 8px rgba(0,0,0,0.06);
            border-top: 4px solid #f0ad4e;
        }

        .sin-asig .sa-icon {
            width: 70px;
            height: 70px;
            background: #fff8e1;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 16px auto;
        }

        .sin-asig .sa-icon i { font-size: 2.2rem; color: #f0ad4e; }
        .sin-asig h4 { color: #555; font-size: 1.1rem; font-weight: 700; margin-bottom: 8px; }
        .sin-asig p { font-size: 0.88rem; color: #888; margin-bottom: 4px; line-height: 1.5; }

        .sa-status-bar {
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 8px;
            margin-top: 16px;
            padding: 10px;
            background: #fff8e1;
            border-radius: 8px;
            font-size: 0.82rem;
            color: #856404;
        }

        .sa-status-bar i { font-size: 1rem; }

        .sa-tip {
            margin-top: 14px;
            font-size: 0.8rem;
            color: #aaa;
            font-style: italic;
        }

        /* --- TABS --- */
        .op-tabs {
            display: flex;
            background: white;
            margin: 0 12px;
            border-radius: 10px;
            overflow: hidden;
            box-shadow: 0 2px 8px rgba(0,0,0,0.06);
        }

        .op-tabs .nav-link {
            flex: 1;
            text-align: center;
            padding: 12px 8px;
            font-size: 0.85rem;
            font-weight: 600;
            color: #888;
            border: none;
            border-bottom: 3px solid transparent;
            background: transparent;
            transition: all 0.2s;
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 6px;
        }

        .op-tabs .nav-link.active {
            color: var(--mq-primary);
            border-bottom-color: var(--mq-primary);
            background: var(--mq-light);
        }

        .op-tabs .badge {
            font-size: 0.7rem;
            padding: 2px 7px;
            border-radius: 10px;
        }

        .tab-body {
            padding: 12px;
        }

        /* --- SECCIONES DEL FORMULARIO --- */
        .sec-card {
            background: white;
            border-radius: 10px;
            margin-bottom: 12px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.06);
            overflow: hidden;
        }

        .sec-header {
            background: var(--mq-primary);
            color: white;
            padding: 10px 14px;
            font-weight: 600;
            font-size: 0.85rem;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .sec-body {
            padding: 14px;
        }

        /* --- CAMPO DE FORMULARIO --- */
        .campo {
            margin-bottom: 12px;
        }

        .campo label {
            display: block;
            font-size: 0.78rem;
            font-weight: 600;
            color: #555;
            margin-bottom: 4px;
            text-transform: uppercase;
            letter-spacing: 0.3px;
        }

        .campo .form-control {
            border-radius: 8px;
            border: 1.5px solid #e0e0e0;
            padding: 10px 12px;
            font-size: 0.95rem;
            height: auto;
            transition: border-color 0.2s;
        }

        .campo .form-control:focus {
            border-color: var(--mq-primary);
            box-shadow: 0 0 0 3px rgba(45,106,79,0.12);
        }

        /* --- MEDICIONES (Odometro/Horometro) --- */
        .medicion-card {
            background: #fafafa;
            border: 1.5px solid #e8e8e8;
            border-radius: 10px;
            padding: 12px;
            margin-bottom: 10px;
        }

        .medicion-card .medicion-titulo {
            font-weight: 700;
            font-size: 0.82rem;
            color: var(--mq-primary);
            text-transform: uppercase;
            margin-bottom: 10px;
            display: flex;
            align-items: center;
            gap: 6px;
        }

        .medicion-inputs {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 8px;
        }

        .medicion-result {
            margin-top: 8px;
            background: var(--mq-light);
            border: 1.5px solid var(--mq-border);
            border-radius: 8px;
            padding: 8px 12px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .medicion-result span:first-child {
            font-size: 0.78rem;
            color: #666;
            font-weight: 600;
        }

        .medicion-result .result-val {
            font-size: 1.1rem;
            font-weight: 700;
            color: var(--mq-primary);
        }

        .medicion-result .result-val input {
            background: transparent;
            border: none;
            font-size: 1.1rem;
            font-weight: 700;
            color: var(--mq-primary);
            text-align: right;
            width: 80px;
            padding: 0;
        }

        /* --- CONSUMO --- */
        .consumo-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 10px;
        }

        .consumo-card {
            background: white;
            border: 1.5px solid #e8e8e8;
            border-radius: 10px;
            padding: 10px;
            text-align: center;
        }

        .consumo-card i {
            font-size: 1.5rem;
            margin-bottom: 4px;
            display: block;
        }

        .consumo-card .c-label {
            font-size: 0.72rem;
            font-weight: 600;
            color: #777;
            text-transform: uppercase;
            margin-bottom: 6px;
        }

        .consumo-card .form-control {
            text-align: center;
            font-weight: 600;
            font-size: 1rem;
            border-radius: 8px;
            padding: 8px;
        }

        .ic-pet { color: #343a40; }
        .ic-gas { color: #fd7e14; }
        .ic-ace { color: #6f42c1; }
        .ic-gra { color: #20c997; }

        /* --- TRABAJO FIELDS --- */
        .trabajo-row {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 10px;
        }

        .trabajo-full {
            grid-column: 1 / -1;
        }

        /* --- BOTON REGISTRAR --- */
        .btn-registrar {
            background: var(--mq-primary);
            color: white;
            border: none;
            border-radius: 10px;
            width: 100%;
            padding: 14px;
            font-size: 1rem;
            font-weight: 700;
            margin-top: 8px;
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 8px;
            box-shadow: 0 4px 12px rgba(45,106,79,0.3);
            transition: all 0.2s;
        }

        .btn-registrar:hover {
            background: var(--mq-secondary);
            color: white;
            transform: translateY(-1px);
            box-shadow: 0 6px 16px rgba(45,106,79,0.35);
        }

        .btn-limpiar {
            background: #f0f0f0;
            color: #666;
            border: none;
            border-radius: 10px;
            width: 100%;
            padding: 12px;
            font-size: 0.9rem;
            font-weight: 600;
            margin-top: 8px;
        }

        /* --- HISTORIAL CARDS --- */
        .hist-summary {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 10px;
            margin-bottom: 14px;
        }

        .hist-stat {
            background: white;
            border-radius: 10px;
            padding: 12px;
            text-align: center;
            box-shadow: 0 2px 6px rgba(0,0,0,0.05);
        }

        .hist-stat .stat-num {
            font-size: 1.6rem;
            font-weight: 800;
            color: var(--mq-primary);
            line-height: 1;
        }

        .hist-stat .stat-label {
            font-size: 0.72rem;
            color: #999;
            font-weight: 600;
            text-transform: uppercase;
            margin-top: 2px;
        }

        .hist-card {
            background: white;
            border-radius: 10px;
            padding: 14px;
            margin-bottom: 10px;
            box-shadow: 0 2px 6px rgba(0,0,0,0.05);
            border-left: 4px solid var(--mq-primary);
        }

        .hist-card-top {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            margin-bottom: 8px;
        }

        .hist-card-num {
            font-weight: 700;
            font-size: 0.9rem;
            color: var(--mq-primary);
        }

        .hist-card-fecha {
            font-size: 0.78rem;
            color: #999;
        }

        .hist-card-badge {
            font-size: 0.68rem;
            padding: 3px 10px;
            border-radius: 12px;
            font-weight: 700;
            color: white;
        }

        .hist-card-body {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 6px;
        }

        .hist-card-body .hc-item {
            font-size: 0.8rem;
        }

        .hc-item .hc-label {
            font-size: 0.68rem;
            color: #aaa;
            text-transform: uppercase;
            font-weight: 600;
        }

        .hc-item .hc-val {
            font-weight: 600;
            color: #333;
        }

        .hist-card-labor {
            margin-top: 8px;
            padding-top: 8px;
            border-top: 1px solid #f0f0f0;
            font-size: 0.82rem;
            color: #555;
        }

        .hist-card-labor i {
            color: var(--mq-primary);
            margin-right: 4px;
        }

        .badge-reg { background: #17a2b8; }
        .badge-apr { background: #28a745; }
        .badge-anu { background: #dc3545; }

        /* --- EMPTY HISTORIAL --- */
        .hist-empty {
            text-align: center;
            padding: 40px 20px;
            color: #bbb;
        }

        .hist-empty i { font-size: 3rem; display: block; margin-bottom: 12px; }
        .hist-empty p { font-size: 0.88rem; color: #999; }

        /* --- MENSAJE --- */
        .op-msg {
            border-radius: 10px;
            padding: 12px 14px;
            font-size: 0.88rem;
            margin-bottom: 12px;
        }

        /* --- DESKTOP ENHANCEMENTS --- */
        @media (min-width: 768px) {
            .dash-op { padding: 0 0 30px; }
            .op-header { padding: 20px 28px; }
            .op-header h2 { font-size: 1.4rem; }
            .asig-card { margin: 16px 28px 12px; padding: 18px; }
            .asig-grid { grid-template-columns: repeat(4, 1fr); }
            .op-tabs { margin: 0 28px; }
            .tab-body { padding: 20px 28px; }
            .medicion-inputs { grid-template-columns: 1fr 1fr; }
            .consumo-grid { grid-template-columns: repeat(4, 1fr); }
            .trabajo-row { grid-template-columns: 1fr 1fr 1fr 1fr; }
            .btn-registrar { width: auto; min-width: 280px; margin-left: auto; margin-right: 0; }
            .btn-limpiar { width: auto; min-width: 140px; }
            .hist-summary { grid-template-columns: repeat(4, 1fr); }
            .sec-body { padding: 18px; }
            .hist-card-body { grid-template-columns: 1fr 1fr 1fr 1fr; }
            .acciones-row { display: flex; justify-content: flex-end; gap: 10px; }
            .acciones-row .btn-limpiar { width: auto; }
        }
    </style>

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
    <script type="text/javascript">
        function calcularDiferencias() {
            var odoIni = parseFloat(document.getElementById('<%= txtOdometroComienzo.ClientID %>').value) || 0;
            var odoFin = parseFloat(document.getElementById('<%= txtOdometroTermino.ClientID %>').value) || 0;
            var odoResult = document.getElementById('<%= txtOdometroKmHoras.ClientID %>');
            if (odoIni > 0 && odoFin > 0 && odoFin >= odoIni) {
                odoResult.value = (odoFin - odoIni).toFixed(1);
            } else {
                odoResult.value = '';
            }

            var horoIni = parseFloat(document.getElementById('<%= txtHorometroComienzo.ClientID %>').value) || 0;
            var horoFin = parseFloat(document.getElementById('<%= txtHorometroTermino.ClientID %>').value) || 0;
            var horoResult = document.getElementById('<%= txtHorometroHoras.ClientID %>');
            if (horoIni > 0 && horoFin > 0 && horoFin >= horoIni) {
                horoResult.value = (horoFin - horoIni).toFixed(1);
            } else {
                horoResult.value = '';
            }
        }

        function validarFormulario() {
            var fecha = document.getElementById('<%= txtFechaParte.ClientID %>').value;
            if (!fecha) {
                alert('Por favor, ingrese la fecha del parte.');
                return false;
            }

            var horoIni = document.getElementById('<%= txtHorometroComienzo.ClientID %>').value;
            var horoFin = document.getElementById('<%= txtHorometroTermino.ClientID %>').value;
            if (horoIni && horoFin) {
                if (parseFloat(horoFin) < parseFloat(horoIni)) {
                    alert('El hor\u00f3metro de t\u00e9rmino no puede ser menor al de comienzo.');
                    return false;
                }
            }

            var odoIni = document.getElementById('<%= txtOdometroComienzo.ClientID %>').value;
            var odoFin = document.getElementById('<%= txtOdometroTermino.ClientID %>').value;
            if (odoIni && odoFin) {
                if (parseFloat(odoFin) < parseFloat(odoIni)) {
                    alert('El od\u00f3metro de t\u00e9rmino no puede ser menor al de comienzo.');
                    return false;
                }
            }

            return confirm('\u00bfEst\u00e1 seguro de registrar este Parte Diario de Trabajo?');
        }

        // Touch: al enfocar un input, scroll suave al campo
        document.addEventListener('DOMContentLoaded', function () {
            var inputs = document.querySelectorAll('.dash-op input[type="text"], .dash-op textarea, .dash-op input[type="number"], .dash-op input[type="date"]');
            inputs.forEach(function (input) {
                input.addEventListener('focus', function () {
                    var self = this;
                    setTimeout(function () {
                        self.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    }, 300);
                });
            });
        });
    </script>
</asp:Content>

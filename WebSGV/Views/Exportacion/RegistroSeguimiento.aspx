<%@ Page Title="Registro de Seguimiento de Exportación" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroSeguimiento.aspx.cs" Inherits="WebSGV.Views.Exportacion.RegistroSeguimiento" %>

<asp:Content ID="ContentSE" ContentPlaceHolderID="MainContent" runat="server">
    <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap" rel="stylesheet" />
    <style>
        :root {
            --se-ink: #0B1426;
            --se-ink-soft: #1A2540;
            --se-bg: #F4F6FB;
            --se-card: #FFFFFF;
            --se-border: #E4E8F0;
            --se-primary: #2563EB;
            --se-primary-dark: #1D4ED8;
            --se-accent: #00D4AA;
            --se-warning: #F59E0B;
            --se-danger: #EF4444;
            --se-muted: #6B7280;
        }

        .se-wrap { font-family: 'Plus Jakarta Sans', sans-serif; background: var(--se-bg); padding: 24px; margin: -20px -15px 0; min-height: calc(100vh - 56px); }
        .se-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 24px; flex-wrap: wrap; gap: 12px; }
        .se-title { font-size: 28px; font-weight: 800; color: var(--se-ink); margin: 0; letter-spacing: -0.02em; }
        .se-subtitle { font-size: 14px; color: var(--se-muted); margin-top: 4px; }

        .se-btn-dashboard { background: var(--se-ink); color: #fff; border: none; padding: 10px 18px; border-radius: 10px; font-weight: 600; font-size: 14px; text-decoration: none; display: inline-flex; align-items: center; gap: 8px; transition: all 0.2s; }
        .se-btn-dashboard:hover { background: var(--se-primary); color: #fff; text-decoration: none; transform: translateY(-1px); }

        .se-tabs { display: flex; gap: 4px; background: var(--se-card); padding: 6px; border-radius: 12px; border: 1px solid var(--se-border); margin-bottom: 20px; flex-wrap: wrap; }
        .se-tab { flex: 1; padding: 10px 16px; border: none; background: transparent; border-radius: 8px; font-weight: 600; font-size: 13px; color: var(--se-muted); cursor: pointer; transition: all 0.2s; }
        .se-tab.active { background: var(--se-ink); color: #fff; box-shadow: 0 2px 8px rgba(11,20,38,0.15); }

        .se-card { background: var(--se-card); border-radius: 16px; border: 1px solid var(--se-border); padding: 24px; margin-bottom: 20px; box-shadow: 0 1px 3px rgba(0,0,0,0.02); }

        .se-section-title { font-size: 15px; font-weight: 700; color: var(--se-ink); text-transform: uppercase; letter-spacing: 0.06em; margin-bottom: 16px; padding-bottom: 12px; border-bottom: 2px solid var(--se-border); display: flex; align-items: center; gap: 10px; }
        .se-section-title::before { content: ''; display: inline-block; width: 4px; height: 18px; background: var(--se-primary); border-radius: 2px; }

        .se-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 14px; }
        .se-field label { display: block; font-size: 12px; font-weight: 600; color: var(--se-ink); margin-bottom: 5px; text-transform: uppercase; letter-spacing: 0.04em; }
        .se-field input, .se-field select, .se-field textarea { width: 100%; padding: 9px 12px; border: 1px solid var(--se-border); border-radius: 9px; font-size: 14px; font-family: inherit; color: var(--se-ink); background: #fff; transition: all 0.2s; }
        .se-field input:focus, .se-field select:focus, .se-field textarea:focus { outline: none; border-color: var(--se-primary); box-shadow: 0 0 0 3px rgba(37,99,235,0.1); }

        .se-actions { display: flex; gap: 10px; justify-content: flex-end; margin-top: 20px; }
        .se-btn { padding: 11px 22px; border-radius: 10px; font-weight: 600; font-size: 14px; border: none; cursor: pointer; transition: all 0.2s; display: inline-flex; align-items: center; gap: 8px; }
        .se-btn-primary { background: var(--se-primary); color: #fff; }
        .se-btn-primary:hover { background: var(--se-primary-dark); }
        .se-btn-ghost { background: transparent; color: var(--se-muted); border: 1px solid var(--se-border); }
        .se-btn-ghost:hover { background: var(--se-bg); color: var(--se-ink); }

        .se-import-zone { border: 2px dashed var(--se-border); border-radius: 14px; padding: 40px 20px; text-align: center; cursor: pointer; transition: all 0.2s; background: #FAFBFE; }
        .se-import-zone:hover { border-color: var(--se-primary); background: rgba(37,99,235,0.04); }
        .se-import-zone.drag-over { border-color: var(--se-accent); background: rgba(0,212,170,0.06); }
        .se-import-icon { font-size: 48px; color: var(--se-primary); margin-bottom: 12px; }
        .se-import-zone h4 { font-weight: 700; color: var(--se-ink); margin-bottom: 6px; }
        .se-import-zone p { color: var(--se-muted); font-size: 13px; margin: 0; }

        .se-alert { padding: 14px 18px; border-radius: 10px; font-weight: 500; font-size: 14px; margin-bottom: 16px; border-left: 4px solid; }
        .se-alert-success { background: #ECFDF5; color: #065F46; border-color: #10B981; }
        .se-alert-danger  { background: #FEF2F2; color: #991B1B; border-color: #EF4444; }
        .se-alert-warning { background: #FFFBEB; color: #92400E; border-color: #F59E0B; }
        .se-alert-info    { background: #EFF6FF; color: #1E40AF; border-color: #2563EB; }

        .se-table { width: 100%; background: #fff; border-radius: 12px; overflow: hidden; border: 1px solid var(--se-border); }
        .se-table table { width: 100%; border-collapse: collapse; }
        .se-table th { background: var(--se-ink); color: #fff; padding: 12px 14px; font-size: 12px; text-transform: uppercase; letter-spacing: 0.06em; font-weight: 600; text-align: left; }
        .se-table td { padding: 12px 14px; border-top: 1px solid var(--se-border); font-size: 14px; color: var(--se-ink); }
        .se-table tr:hover { background: #FAFBFE; }

        .se-badge { display: inline-block; padding: 4px 10px; border-radius: 999px; font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.04em; }
        .se-badge-encurso    { background: #DBEAFE; color: #1E40AF; }
        .se-badge-finalizado { background: #D1FAE5; color: #065F46; }
        .se-badge-retrasado  { background: #FEF3C7; color: #92400E; }
        .se-badge-cancelado  { background: #FEE2E2; color: #991B1B; }
        .se-badge-borrador   { background: #E0E7FF; color: #3730A3; }
        .se-badge-completo   { background: #D1FAE5; color: #065F46; }

        .se-tab-panel { display: none; }
        .se-tab-panel.active { display: block; animation: fadeIn 0.3s ease; }
        @keyframes fadeIn { from { opacity: 0; transform: translateY(6px); } to { opacity: 1; transform: translateY(0); } }

        /* ===== Bandeja de viajes en curso ===== */
        .se-bandeja-toolbar { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; margin-bottom: 16px; }
        .se-bandeja-toolbar input[type=text] { flex: 1; min-width: 220px; padding: 9px 12px; border: 1px solid var(--se-border); border-radius: 9px; font-size: 14px; }
        .se-bandeja-counter { font-size: 13px; color: var(--se-muted); font-weight: 600; }
        .se-bandeja-counter strong { color: var(--se-primary); font-size: 18px; }

        .se-bandeja-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 14px; }
        .se-trip-card { background: #fff; border: 1px solid var(--se-border); border-radius: 14px; padding: 16px; display: flex; flex-direction: column; gap: 10px; transition: all 0.2s; position: relative; overflow: hidden; }
        .se-trip-card:hover { border-color: var(--se-primary); box-shadow: 0 4px 16px rgba(37,99,235,0.08); transform: translateY(-2px); }
        .se-trip-card::before { content: ''; position: absolute; left: 0; top: 0; bottom: 0; width: 4px; background: var(--se-primary); }
        .se-trip-card.complete::before { background: var(--se-accent); }
        .se-trip-card.pending::before { background: var(--se-muted); }
        .se-trip-head { display: flex; justify-content: space-between; align-items: flex-start; gap: 8px; }
        .se-trip-client { font-weight: 800; color: var(--se-ink); font-size: 15px; line-height: 1.3; }
        .se-trip-meta { font-size: 12px; color: var(--se-muted); margin-top: 2px; }
        .se-trip-info { font-size: 13px; color: var(--se-ink-soft); display: grid; grid-template-columns: auto 1fr; gap: 4px 10px; }
        .se-trip-info span:nth-child(odd) { color: var(--se-muted); font-weight: 600; font-size: 11px; text-transform: uppercase; letter-spacing: 0.04em; align-self: center; }
        .se-trip-next { background: #EFF6FF; color: #1E40AF; padding: 8px 12px; border-radius: 8px; font-size: 12px; font-weight: 600; border-left: 3px solid var(--se-primary); }
        .se-trip-next small { display: block; color: var(--se-muted); font-weight: 500; text-transform: uppercase; letter-spacing: 0.04em; font-size: 10px; margin-bottom: 2px; }

        .se-progress { background: #F1F5F9; border-radius: 999px; height: 8px; overflow: hidden; position: relative; }
        .se-progress-bar { background: linear-gradient(90deg, var(--se-primary), var(--se-accent)); height: 100%; border-radius: 999px; transition: width 0.4s ease; }
        .se-progress-label { display: flex; justify-content: space-between; font-size: 11px; color: var(--se-muted); font-weight: 600; margin-bottom: 4px; }

        .se-trip-actions { display: flex; gap: 6px; margin-top: 4px; }
        .se-trip-actions .se-btn { flex: 1; padding: 8px 12px; font-size: 13px; justify-content: center; }

        .se-empty { text-align: center; padding: 50px 20px; color: var(--se-muted); }
        .se-empty-icon { font-size: 48px; color: var(--se-border); margin-bottom: 12px; }

        /* ===== Formulario en secciones colapsables ===== */
        .se-form-banner { background: linear-gradient(135deg, #EFF6FF, #F0FDF4); border: 1px solid var(--se-primary); border-radius: 12px; padding: 14px 18px; margin-bottom: 16px; display: flex; align-items: center; gap: 12px; }
        .se-form-banner i { font-size: 24px; color: var(--se-primary); }
        .se-form-banner strong { color: var(--se-ink); font-size: 15px; }
        .se-form-banner small { display: block; color: var(--se-muted); font-size: 12px; margin-top: 2px; }
        .se-form-banner .se-btn-cancel { margin-left: auto; }

        details.se-accordion { border: 1px solid var(--se-border); border-radius: 12px; margin-bottom: 12px; background: #fff; overflow: hidden; }
        details.se-accordion[open] { box-shadow: 0 1px 3px rgba(0,0,0,0.03); }
        details.se-accordion > summary { padding: 16px 20px; cursor: pointer; font-weight: 700; color: var(--se-ink); font-size: 14px; text-transform: uppercase; letter-spacing: 0.06em; list-style: none; display: flex; align-items: center; gap: 12px; user-select: none; }
        details.se-accordion > summary::-webkit-details-marker { display: none; }
        details.se-accordion > summary::before { content: '▶'; font-size: 10px; color: var(--se-primary); transition: transform 0.2s; }
        details.se-accordion[open] > summary::before { transform: rotate(90deg); }
        details.se-accordion > summary:hover { background: var(--se-bg); }
        details.se-accordion .se-acc-body { padding: 0 20px 20px 20px; }

        /* ===== Autocompletado de despachos ===== */
        .se-ac-list { display: none; position: absolute; z-index: 20; left: 0; right: 0; top: 100%; margin-top: 4px; background: #fff; border: 1px solid var(--se-border); border-radius: 10px; box-shadow: 0 8px 24px rgba(11,20,38,0.12); max-height: 260px; overflow-y: auto; }
        .se-ac-list.show { display: block; }
        .se-ac-item { padding: 10px 14px; font-size: 13px; color: var(--se-ink); cursor: pointer; border-bottom: 1px solid var(--se-border); }
        .se-ac-item:last-child { border-bottom: none; }
        .se-ac-item:hover { background: var(--se-bg); }
        .se-ac-item.se-ac-empty { color: var(--se-muted); cursor: default; font-style: italic; }
        .se-ac-item.se-ac-empty:hover { background: transparent; }

        /* ===== Línea de tiempo del recorrido (integración GPS) ===== */
        .se-timeline-tramo { position: relative; border: 1px solid var(--se-border); border-radius: 14px; margin-bottom: 14px; background: #fff; overflow: hidden; }
        .se-timeline-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 16px 20px; border-bottom: 1px solid var(--se-border); background: #FAFBFE; }
        .se-timeline-head-left { display: flex; align-items: center; gap: 10px; }
        .se-timeline-head-icon { width: 30px; height: 30px; border-radius: 50%; background: var(--se-ink); color: #fff; display: flex; align-items: center; justify-content: center; font-size: 13px; font-weight: 700; flex-shrink: 0; }
        .se-timeline-head h4 { margin: 0; font-size: 14px; font-weight: 700; color: var(--se-ink); text-transform: uppercase; letter-spacing: 0.05em; }
        .se-timeline-head small { display: block; color: var(--se-muted); font-size: 11.5px; font-weight: 500; margin-top: 1px; }
        .se-badge-progreso { font-size: 11.5px; font-weight: 700; padding: 5px 12px; border-radius: 999px; white-space: nowrap; }
        .se-badge-progreso.completo { background: #ECFDF5; color: #065F46; }
        .se-badge-progreso.parcial  { background: #FFFBEB; color: #92400E; }
        .se-badge-progreso.vacio    { background: #F3F4F6; color: #6B7280; }

        .se-timeline-body { padding: 18px 20px 20px 20px; position: relative; }
        .se-timeline-steps { display: flex; flex-direction: column; gap: 0; }
        .se-timeline-step { display: grid; grid-template-columns: 28px 1fr; gap: 14px; position: relative; padding-bottom: 16px; }
        .se-timeline-step:last-child { padding-bottom: 0; }
        .se-timeline-dot { width: 12px; height: 12px; border-radius: 50%; background: var(--se-border); margin-top: 4px; position: relative; z-index: 1; justify-self: center; }
        .se-timeline-step.gps .se-timeline-dot { background: var(--se-accent); }
        .se-timeline-step:not(:last-child)::before { content: ''; position: absolute; left: 13px; top: 16px; bottom: -4px; width: 2px; background: var(--se-border); }
        .se-timeline-step-label { display: flex; align-items: baseline; gap: 6px; margin-bottom: 5px; }
        .se-timeline-step-label label { font-size: 12px; font-weight: 600; color: var(--se-ink); text-transform: uppercase; letter-spacing: 0.03em; margin: 0; }
        .se-timeline-tag { font-size: 10px; font-weight: 700; padding: 1px 7px; border-radius: 999px; text-transform: uppercase; letter-spacing: 0.03em; }
        .se-timeline-tag.gps { background: rgba(0,212,170,0.12); color: #067A5F; }
        .se-timeline-tag.manual { background: rgba(107,114,128,0.12); color: #4B5563; }
        .se-timeline-step input, .se-timeline-step select { width: 100%; max-width: 280px; padding: 8px 11px; border: 1px solid var(--se-border); border-radius: 8px; font-size: 13.5px; font-family: inherit; color: var(--se-ink); background: #fff; }
        .se-timeline-step input:focus, .se-timeline-step select:focus { outline: none; border-color: var(--se-primary); box-shadow: 0 0 0 3px rgba(37,99,235,0.1); }
        .se-acc-badge { margin-left: auto; background: var(--se-bg); color: var(--se-muted); padding: 3px 10px; border-radius: 999px; font-size: 11px; font-weight: 700; }
        .se-acc-badge.ok { background: #D1FAE5; color: #065F46; }

    </style>

    <%-- Datos de autocompletado emitidos desde el code-behind --%>
    <asp:Literal ID="litAutoComplete" runat="server" />

    <%-- Datalist elements para autocompletado HTML5 nativo --%>
    <datalist id="seListClientes"></datalist>
    <datalist id="seListConductores"></datalist>
    <datalist id="seListTractos"></datalist>
    <datalist id="seListCarretas"></datalist>

    <div class="se-wrap">
        <div class="se-header">
            <div>
                <h1 class="se-title">Seguimiento de Exportación</h1>
                <div class="se-subtitle">Registro de viajes Perú → Ecuador | Reemplazo digital del Excel STATUS GENERAL VIVIANA</div>
            </div>
            <a href="DashboardExportacion.aspx" class="se-btn-dashboard">
                <i class="fas fa-chart-line"></i> Ver Dashboard Analytics
            </a>
        </div>

        <!-- Tabs -->
        <div class="se-tabs" role="tablist">
            <button type="button" class="se-tab" data-target="panel-bandeja">🚚 Viajes en curso</button>
            <button type="button" class="se-tab active" data-target="panel-import">📂 Importar Excel</button>
            <button type="button" class="se-tab"        data-target="panel-list">📋 Historial</button>
            <%-- Botón oculto: no es navegable a mano, solo existe para que ActivarTab("panel-form") (code-behind)
                 pueda "hacer clic" en él por JS y mostrar el formulario tras Nuevo Viaje / Editar / Verificar GPS. --%>
            <button type="button" class="se-tab" data-target="panel-form" style="display:none;" aria-hidden="true"></button>
        </div>

        <asp:HiddenField ID="hdnIdSeguimiento" runat="server" Value="0" />

        <asp:Panel ID="pnlAlert" runat="server" Visible="false" CssClass="se-alert se-alert-info">
            <asp:Literal ID="litAlert" runat="server"></asp:Literal>
        </asp:Panel>

        <!-- ========== TAB: BANDEJA DE VIAJES EN CURSO ========== -->
        <div id="panel-bandeja" class="se-tab-panel">
            <div class="se-card">
                <div class="se-section-title">Viajes en curso (registro progresivo)</div>
                <div class="se-alert se-alert-info" style="margin-bottom:14px;">
                    💡 <strong>Captura progresiva:</strong> No necesitas llenar todo un viaje de golpe. Crea uno con los datos mínimos y ve agregando los hitos conforme avanza el viaje.
                </div>

                <div class="se-bandeja-toolbar">
                    <asp:TextBox ID="txtFiltroBandeja" runat="server" placeholder="Buscar por cliente, conductor o tracto..." AutoCompleteType="Disabled" />
                    <asp:Button ID="btnBuscarBandeja" runat="server" Text="🔍 Buscar" CssClass="se-btn se-btn-ghost" OnClick="btnBuscarBandeja_Click" CausesValidation="false" />
                    <asp:Button ID="btnRefrescarBandeja" runat="server" Text="↻ Refrescar" CssClass="se-btn se-btn-ghost" OnClick="btnRefrescarBandeja_Click" CausesValidation="false" />
                    <asp:Button ID="btnNuevoViaje" runat="server" Text="➕ Nuevo viaje" CssClass="se-btn se-btn-primary" OnClick="btnNuevoViaje_Click" CausesValidation="false" />
                    <div class="se-bandeja-counter">
                        <strong><asp:Literal ID="litCountBandeja" runat="server" Text="0" /></strong> abierto(s)
                    </div>
                </div>

                <asp:Panel ID="pnlBandejaVacia" runat="server" Visible="false" CssClass="se-empty">
                    <div class="se-empty-icon">📭</div>
                    <h4 style="color: var(--se-ink); font-weight: 700;">No hay viajes en curso</h4>
                    <p>Crea un nuevo viaje o importa el Excel masivo para empezar.</p>
                </asp:Panel>

                <asp:Repeater ID="rptBandeja" runat="server" OnItemCommand="rptBandeja_ItemCommand">
                    <HeaderTemplate><div class="se-bandeja-grid"></HeaderTemplate>
                    <ItemTemplate>
                        <div class='se-trip-card <%# (int)Eval("porcentaje") >= 90 ? "complete" : ((int)Eval("porcentaje") <= 10 ? "pending" : "") %>'>
                            <div class="se-trip-head">
                                <div>
                                    <div class="se-trip-client"><%# Eval("cliente") %></div>
                                    <div class="se-trip-meta">Programación: <%# Eval("fhProgramacionFmt") %></div>
                                </div>
                                <span class='se-badge se-badge-<%# (Eval("estado") ?? "").ToString().ToLower().Replace("_","") %>'><%# Eval("estado") %></span>
                            </div>
                            <div class="se-trip-info">
                                <span>Tracto 1</span><span><%# Eval("tracto1") %></span>
                                <span>Conductor</span><span><%# Eval("conductorOrigen") %></span>
                                <span>Carreta</span><span><%# Eval("carreta") %></span>
                                <span>Destino</span><span><%# Eval("bodegaDescarga") %></span>
                            </div>
                            <div>
                                <div class="se-progress-label">
                                    <span>Avance del viaje</span>
                                    <span><%# Eval("hitosCompletados") %>/<%# Eval("hitosTotales") %> hitos · <%# Eval("porcentaje") %>%</span>
                                </div>
                                <div class="se-progress"><div class="se-progress-bar" style='width: <%# Eval("porcentaje") %>%;'></div></div>
                            </div>
                            <div class="se-trip-next">
                                <small>Siguiente hito por registrar</small>
                                <%# Eval("siguienteHito") %>
                            </div>
                            <div class="se-trip-actions">
                                <asp:LinkButton runat="server" CssClass="se-btn se-btn-primary"
                                    CommandName="Continuar"
                                    CommandArgument='<%# Eval("idSeguimiento") %>'
                                    CausesValidation="false">▶ Continuar registro</asp:LinkButton>
                                <asp:LinkButton runat="server" CssClass="se-btn se-btn-ghost"
                                    CommandName="Finalizar"
                                    CommandArgument='<%# Eval("idSeguimiento") %>'
                                    CausesValidation="false"
                                    OnClientClick="return confirm('¿Marcar este viaje como FINALIZADO?');">✓ Finalizar</asp:LinkButton>
                            </div>
                        </div>
                    </ItemTemplate>
                    <FooterTemplate></div></FooterTemplate>
                </asp:Repeater>
            </div>
        </div>

        <!-- ========== TAB: FORMULARIO (registro/edición) ========== -->
        <div id="panel-form" class="se-tab-panel">
            <asp:Panel ID="pnlFormBanner" runat="server" CssClass="se-form-banner" Visible="false">
                <i class="fas fa-edit"></i>
                <div>
                    <strong><asp:Literal ID="litFormBannerTitle" runat="server" /></strong>
                    <small><asp:Literal ID="litFormBannerSub" runat="server" /></small>
                </div>
                <asp:Literal ID="litEstadoRegistro" runat="server" />
                <asp:Button ID="btnCancelarEdicion" runat="server" Text="✕ Cancelar edición" CssClass="se-btn se-btn-ghost se-btn-cancel" CausesValidation="false" OnClick="btnCancelarEdicion_Click" />
            </asp:Panel>

            <%-- Sección ①: Vincular despachos reales (el viaje se CREA en Despacho, acá solo se selecciona) --%>
            <details class="se-accordion" open>
                <summary>① Despachos del viaje (obligatorio: al menos el nacional)</summary>
                <div class="se-acc-body">
                    <div class="se-alert se-alert-info" style="margin-bottom:14px;">
                        💡 Los viajes se crean en <strong>Despacho</strong>. Acá solo buscas y seleccionas los despachos reales que corresponden a este viaje de exportación — cliente, conductor y placa se traen automáticamente, no se re-escriben.
                    </div>
                    <div class="se-grid" style="grid-template-columns: 1fr 1fr;">
                        <div>
                            <div class="se-field" style="position:relative;"><label>Buscar despacho Nacional (Trujillo) — Tracto 1 *</label>
                                <asp:TextBox ID="txtBuscarDespachoNacional" runat="server" placeholder="Escribe cliente, conductor o placa..." autocomplete="off" onkeyup="seAutocompletarDespacho(this, false)" />
                                <div id="acDespachoNacional" class="se-ac-list"></div>
                            </div>
                            <asp:Panel ID="pnlResumenNacional" runat="server" Visible="false" CssClass="se-alert se-alert-success" style="margin-top:10px;">
                                <asp:Literal ID="litResumenNacional" runat="server" Mode="PassThrough" />
                            </asp:Panel>
                        </div>
                        <div>
                            <div class="se-field" style="position:relative;"><label>Buscar despacho Internacional (Ecuador) — Tracto 2</label>
                                <asp:TextBox ID="txtBuscarDespachoInternacional" runat="server" placeholder="Escribe cliente, conductor o placa..." autocomplete="off" onkeyup="seAutocompletarDespacho(this, true)" />
                                <div id="acDespachoInternacional" class="se-ac-list"></div>
                            </div>
                            <asp:Panel ID="pnlResumenInternacional" runat="server" Visible="false" CssClass="se-alert se-alert-success" style="margin-top:10px;">
                                <asp:Literal ID="litResumenInternacional" runat="server" Mode="PassThrough" />
                            </asp:Panel>
                        </div>
                        <%-- Ganchos de postback: el clic en un ítem del autocompletado (JS) guarda el id en el
                             hidden field correspondiente y dispara estos LinkButton ocultos vía __doPostBack. --%>
                        <asp:LinkButton ID="lnkAplicarDespachoNacional" runat="server" OnClick="lnkAplicarDespachoNacional_Click" style="display:none;" CausesValidation="false" />
                        <asp:LinkButton ID="lnkAplicarDespachoInternacional" runat="server" OnClick="lnkAplicarDespachoInternacional_Click" style="display:none;" CausesValidation="false" />
                    </div>

                    <%-- Portadores de datos para el guardado/carga existente — no se editan a mano, los llena la selección de arriba. --%>
                    <div style="display:none;">
                        <asp:TextBox ID="txtCliente" runat="server" />
                        <asp:RequiredFieldValidator ID="rfvCliente" runat="server" ControlToValidate="txtCliente" ValidationGroup="vgGuardarSeguimiento" Display="Dynamic" CssClass="text-danger" ErrorMessage="Selecciona el despacho Nacional (Tracto 1)." />
                        <asp:TextBox ID="txtConductorOrigen" runat="server" />
                        <asp:TextBox ID="txtTracto1" runat="server" />
                        <asp:RequiredFieldValidator ID="rfvTracto1" runat="server" ControlToValidate="txtTracto1" ValidationGroup="vgGuardarSeguimiento" Display="Dynamic" CssClass="text-danger" ErrorMessage="Selecciona el despacho Nacional (Tracto 1)." />
                        <asp:TextBox ID="txtCarreta" runat="server" />
                        <asp:TextBox ID="txtConductorDestino" runat="server" />
                        <asp:TextBox ID="txtTracto2" runat="server" />
                    </div>
                    <asp:HiddenField ID="hdnIdDespachoOrigen" runat="server" Value="0" />
                    <asp:HiddenField ID="hdnIdDespachoDestino" runat="server" Value="0" />

                    <div class="se-grid" style="margin-top:16px;">
                        <div class="se-field"><label>F.H. Programación *</label>
                            <asp:TextBox ID="txtFhProgramacion" runat="server" TextMode="DateTimeLocal" />
                            <asp:RequiredFieldValidator ID="rfvFhProgramacion" runat="server" ControlToValidate="txtFhProgramacion" ValidationGroup="vgGuardarSeguimiento" Display="Dynamic" CssClass="text-danger" ErrorMessage="F.H. Programación es obligatoria." />
                        </div>
                        <div class="se-field"><label>Estado</label>
                            <asp:DropDownList ID="ddlEstado" runat="server">
                                <asp:ListItem Text="En curso"   Value="EN_CURSO" />
                                <asp:ListItem Text="Pendiente"  Value="PENDIENTE" />
                                <asp:ListItem Text="Finalizado" Value="FINALIZADO" />
                                <asp:ListItem Text="Completado" Value="COMPLETADO" />
                                <asp:ListItem Text="Retrasado"  Value="RETRASADO" />
                                <asp:ListItem Text="Cancelado"  Value="CANCELADO" />
                            </asp:DropDownList>
                        </div>
                    </div>
                </div>
            </details>

            <%-- Sección ②-⑤: Línea de tiempo del recorrido (Base → Trujillo → Base → Ecuador → Base) --%>
            <div class="se-card" style="padding:20px 20px 6px 20px;">
                <div class="se-section-title" style="margin-bottom:4px;">② Línea de tiempo del recorrido</div>
                <p style="color:var(--se-muted);font-size:12.5px;margin:-6px 0 16px 0;">
                    <span class="se-timeline-tag gps">GPS</span> se llena solo al presionar "Verificar GPS" abajo (puedes corregirlo si hace falta) ·
                    <span class="se-timeline-tag manual">MANUAL</span> siempre se escribe a mano.
                </p>

                <%-- Tramo Nacional (Tracto 1): Base → Trujillo → Base --%>
                <div class="se-timeline-tramo">
                    <div class="se-timeline-head">
                        <div class="se-timeline-head-left">
                            <div class="se-timeline-head-icon">1</div>
                            <div><h4>Tramo Nacional — Base ⇄ Trujillo</h4><small>Tracto 1 · <asp:Literal ID="litTracto1Resumen" runat="server" /></small></div>
                        </div>
                        <asp:Literal ID="litProgresoNacional" runat="server" Mode="PassThrough" />
                    </div>
                    <div class="se-timeline-body">
                        <div class="se-timeline-steps">
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Salida Base</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhSalidaBase1" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada Trujillo</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaTrujillo" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Ingreso Planta</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhIngresoPlanta" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Inicio Carga</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:TextBox ID="txtFhInicioCarga" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Término Carga</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:TextBox ID="txtFhTerminoCarga" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Salida Planta</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhSalidaPlanta" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada Base</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaBase2" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Registro (sistema)</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:TextBox ID="txtFhRegistro" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                        </div>
                    </div>
                </div>

                <%-- Tramo Internacional (Tracto 2): Base → Bodega Nacional --%>
                <div class="se-timeline-tramo">
                    <div class="se-timeline-head">
                        <div class="se-timeline-head-left">
                            <div class="se-timeline-head-icon">2</div>
                            <div><h4>Salida a Ecuador — Bodega Nacional</h4><small>Tracto 2 · <asp:Literal ID="litTracto2Resumen" runat="server" /></small></div>
                        </div>
                        <asp:Literal ID="litProgresoBodegaNacional" runat="server" Mode="PassThrough" />
                    </div>
                    <div class="se-timeline-body">
                        <div class="se-timeline-steps">
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Salida Base (hacia Ecuador)</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhSalidaBase2" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada Bodega Nacional</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaBodegaNacional" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Ingreso Bodega Nacional</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhIngresoBodegaNacional" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Salida Bodega Nacional</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhSalidaBodegaNacional" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>Bodega Nacional</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:DropDownList ID="ddlBodegaNacional" runat="server">
                                        <asp:ListItem Text="-- Seleccionar --" Value="" />
                                        <asp:ListItem Text="DEPSA"    Value="DEPSA" />
                                        <asp:ListItem Text="COMPLEX"  Value="COMPLEX" />
                                    </asp:DropDownList>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <%-- Frontera --%>
                <div class="se-timeline-tramo">
                    <div class="se-timeline-head">
                        <div class="se-timeline-head-left">
                            <div class="se-timeline-head-icon">3</div>
                            <div><h4>Frontera — CEBAF / Nacionalización</h4><small>Tracto 2</small></div>
                        </div>
                        <asp:Literal ID="litProgresoFrontera" runat="server" Mode="PassThrough" />
                    </div>
                    <div class="se-timeline-body">
                        <div class="se-timeline-steps">
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada CEBAF</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaCEBAF" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Cruce Ecuador</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhCruceEcuador" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Autorización Nacionalización</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:TextBox ID="txtFhAutorizacionNacionalizacion" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                        </div>
                    </div>
                </div>

                <%-- Ecuador --%>
                <div class="se-timeline-tramo">
                    <div class="se-timeline-head">
                        <div class="se-timeline-head-left">
                            <div class="se-timeline-head-icon">4</div>
                            <div><h4>Ecuador — TCI → Jave / Inbalnor</h4><small>Tracto 2 · la ruta (Jave/Inbalnor) se detecta sola por GPS</small></div>
                        </div>
                        <asp:Literal ID="litProgresoEcuador" runat="server" Mode="PassThrough" />
                    </div>
                    <div class="se-timeline-body">
                        <div class="se-timeline-steps">
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada TCI</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaTCI" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Salida TCI</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhSalidaTCI" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>Bodega Ecuatoriana</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:DropDownList ID="ddlBodegaEcuatoriana" runat="server">
                                        <asp:ListItem Text="-- Seleccionar --" Value="" />
                                        <asp:ListItem Text="TCI"     Value="TCI" />
                                        <asp:ListItem Text="PUYANGO" Value="PUYANGO" />
                                    </asp:DropDownList>
                                </div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>Bodega Descarga</label><span class="se-timeline-tag manual">Manual (o auto por ruta GPS)</span></div>
                                    <asp:DropDownList ID="ddlBodegaDescarga" runat="server">
                                        <asp:ListItem Text="-- Seleccionar --" Value="" />
                                        <asp:ListItem Text="INBALNOR" Value="INBALNOR" />
                                        <asp:ListItem Text="JAVE"     Value="JAVE" />
                                        <asp:ListItem Text="OREMANS"  Value="OREMANS" />
                                    </asp:DropDownList>
                                </div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada Planta Ecuador</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaPlantaEcuador" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada Almacén</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaAlmacen" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Ingreso</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhIngreso" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Inicio Descarga</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:TextBox ID="txtFhInicioDescarga" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Término Descarga</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:TextBox ID="txtFhTerminoDescarga" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Salida</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhSalida" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                        </div>
                    </div>
                </div>

                <%-- Regreso final --%>
                <div class="se-timeline-tramo">
                    <div class="se-timeline-head">
                        <div class="se-timeline-head-left">
                            <div class="se-timeline-head-icon">5</div>
                            <div><h4>Regreso Final a Base</h4><small>Tracto 2 · cierra el recorrido</small></div>
                        </div>
                        <asp:Literal ID="litProgresoRegreso" runat="server" Mode="PassThrough" />
                    </div>
                    <div class="se-timeline-body">
                        <div class="se-timeline-steps">
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada Base</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaBaseFinal" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <%-- Sección ⑥: Incidencias --%>
            <details class="se-accordion">
                <summary>⑥ Incidencias y Observaciones</summary>
                <div class="se-acc-body">
                    <div class="se-grid">
                        <div class="se-field"><label>Sacos Robados</label>
                            <asp:TextBox ID="txtSacosRobados" runat="server" TextMode="Number" Text="0" min="0" step="1" />
                            <asp:RegularExpressionValidator ID="revSacosRobados" runat="server" ControlToValidate="txtSacosRobados" ValidationGroup="vgGuardarSeguimiento" Display="Dynamic" CssClass="text-danger" ValidationExpression="^\d+$" ErrorMessage="Sacos Robados debe ser un número entero mayor o igual a 0." />
                        </div>
                        <div class="se-field"><label>Sacos Rotos</label>
                            <asp:TextBox ID="txtSacosRotos" runat="server" TextMode="Number" Text="0" min="0" step="1" />
                            <asp:RegularExpressionValidator ID="revSacosRotos" runat="server" ControlToValidate="txtSacosRotos" ValidationGroup="vgGuardarSeguimiento" Display="Dynamic" CssClass="text-danger" ValidationExpression="^\d+$" ErrorMessage="Sacos Rotos debe ser un número entero mayor o igual a 0." />
                        </div>
                        <div class="se-field"><label>Sacos Mojados</label>
                            <asp:TextBox ID="txtSacosMojados" runat="server" TextMode="Number" Text="0" min="0" step="1" />
                            <asp:RegularExpressionValidator ID="revSacosMojados" runat="server" ControlToValidate="txtSacosMojados" ValidationGroup="vgGuardarSeguimiento" Display="Dynamic" CssClass="text-danger" ValidationExpression="^\d+$" ErrorMessage="Sacos Mojados debe ser un número entero mayor o igual a 0." />
                        </div>
                    </div>
                    <div class="se-field" style="margin-top:14px;"><label>Motivo de retraso / Comentario</label>
                        <asp:TextBox ID="txtMotivoRetraso" runat="server" TextMode="MultiLine" Rows="3" />
                    </div>
                </div>
            </details>

            <div class="se-card">
                <div class="se-actions">
                    <asp:Button ID="btnLimpiar" runat="server" Text="Limpiar" CssClass="se-btn se-btn-ghost" CausesValidation="false" OnClick="btnLimpiar_Click" />
                    <asp:Button ID="btnVerificarGps" runat="server" Text="🛰️ Verificar GPS" CssClass="se-btn se-btn-ghost" CausesValidation="false" OnClick="btnVerificarGps_Click" ToolTip="Consulta el historial GPS del Tracto 1 y Tracto 2 y llena automáticamente los campos de fecha/hora detectables. Puede tardar 1-2 minutos." />
                    <asp:Button ID="btnGuardarBorrador" runat="server" Text="📝 Guardar borrador" CssClass="se-btn se-btn-ghost" OnClick="btnGuardarBorrador_Click" CausesValidation="false" />
                    <asp:Button ID="btnGuardarFinal" runat="server" Text="✅ Guardar final" CssClass="se-btn se-btn-primary" OnClick="btnGuardarFinal_Click" ValidationGroup="vgGuardarSeguimiento" />
                </div>
                <asp:Panel ID="pnlResultadoGps" runat="server" Visible="false" CssClass="se-alert se-alert-info" style="margin-top:10px;">
                    <asp:Literal ID="litResultadoGps" runat="server" Mode="PassThrough" />
                </asp:Panel>
                <asp:ValidationSummary ID="vsGuardarSeguimiento" runat="server" ValidationGroup="vgGuardarSeguimiento" DisplayMode="BulletList" CssClass="se-alert se-alert-warning" HeaderText="Corrige los siguientes campos:" />
                <div style="text-align:right;font-size:12px;color:var(--se-muted);margin-top:8px;">
                    Borrador permite avance parcial con validaciones de formato. Guardado final exige todos los campos obligatorios del seguimiento completos.
                </div>
            </div>
        </div>

        <!-- ========== TAB: IMPORT EXCEL ========== -->
        <div id="panel-import" class="se-tab-panel active">
            <div class="se-card">
                <div class="se-section-title">Importación masiva desde Excel</div>
                <div class="se-alert se-alert-info" style="border-color: var(--se-primary);">
                    <strong>Paso 1:</strong> descarga la plantilla y complétala con los datos del viaje.
                    <strong>Paso 2:</strong> sube el archivo completado. El sistema mapea automáticamente las columnas reconocidas.
                </div>

                <div class="se-actions" style="justify-content:flex-start; margin-top:0; margin-bottom:20px;">
                    <asp:Button ID="btnDescargarPlantilla" runat="server" Text="📄 Descargar plantilla Excel" CssClass="se-btn se-btn-ghost" CausesValidation="false" OnClick="btnDescargarPlantilla_Click" />
                </div>

                <div id="dropZone" class="se-import-zone" onclick="document.getElementById('<%= fileExcel.ClientID %>').click();">
                    <div class="se-import-icon"><i class="fas fa-file-excel"></i></div>
                    <h4>Arrastra tu archivo aquí o haz clic para seleccionar</h4>
                    <p>Acepta .xlsx y .xls — máximo 10MB</p>
                    <asp:FileUpload ID="fileExcel" runat="server" CssClass="d-none" accept=".xlsx,.xls" onchange="seUpdateFileName(this);" />
                    <div id="fileInfo" style="margin-top:14px;font-style:italic;color:var(--se-muted);">Ningún archivo seleccionado</div>
                </div>

                <div class="se-actions">
                    <asp:Button ID="btnImportar" runat="server" Text="📂 Procesar Archivo" CssClass="se-btn se-btn-primary" OnClick="btnImportar_Click" />
                </div>
            </div>
        </div>

        <!-- ========== TAB: HISTORIAL ========== -->
        <div id="panel-list" class="se-tab-panel">
            <div class="se-card">
                <div class="se-section-title">Últimos registros</div>
                <div class="se-table">
                    <asp:GridView ID="gvRecientes" runat="server" AutoGenerateColumns="false" AllowPaging="true" PageSize="10"
                        GridLines="None" CssClass=""
                        EmptyDataText="No hay registros aún. Comienza usando el formulario o importando un Excel."
                        OnPageIndexChanging="gvRecientes_PageIndexChanging">
                        <Columns>
                            <asp:BoundField DataField="idSeguimiento"   HeaderText="ID" ItemStyle-Width="60px" />
                            <asp:BoundField DataField="cliente"          HeaderText="Cliente" />
                            <asp:BoundField DataField="conductorOrigen"  HeaderText="Conductor Origen" />
                            <asp:BoundField DataField="tracto1"          HeaderText="Tracto 1" />
                            <asp:BoundField DataField="bodegaDescarga"   HeaderText="Bodega Destino" />
                            <asp:TemplateField HeaderText="Estado">
                                <ItemTemplate>
                                    <span class='se-badge se-badge-<%# (Eval("estado") ?? "").ToString().ToLower().Replace("_","") %>'>
                                        <%# Eval("estado") %>
                                    </span>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="fechaRegistroFmt" HeaderText="Registrado" />
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
        </div>
    </div>

    <script type="text/javascript">
        // ============================================================
        //  Tabs
        // ============================================================
        document.querySelectorAll('.se-tab').forEach(function (tab) {
            tab.addEventListener('click', function () {
                var target = this.getAttribute('data-target');
                document.querySelectorAll('.se-tab').forEach(function (t) { t.classList.remove('active'); });
                document.querySelectorAll('.se-tab-panel').forEach(function (p) { p.classList.remove('active'); });
                this.classList.add('active');
                var panel = document.getElementById(target);
                if (panel) panel.classList.add('active');
            });
        });

        // ============================================================
        //  Autocompletado de despachos (Nacional / Internacional)
        // ============================================================
        var seAcTimer = null;
        function seAutocompletarDespacho(input, esInternacional) {
            clearTimeout(seAcTimer);
            var texto = input.value;
            var list = document.getElementById(esInternacional ? 'acDespachoInternacional' : 'acDespachoNacional');
            if (texto.length < 2) { list.classList.remove('show'); list.innerHTML = ''; return; }

            seAcTimer = setTimeout(function () {
                var idSeguimientoActual = parseInt(document.getElementById('<%= hdnIdSeguimiento.ClientID %>').value, 10) || 0;
                fetch('RegistroSeguimiento.aspx/BuscarDespachosAjax', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json; charset=utf-8' },
                    body: JSON.stringify({ esInternacional: esInternacional, texto: texto, idSeguimientoActual: idSeguimientoActual })
                })
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    var items = data.d;
                    list.innerHTML = '';
                    if (!items.length) {
                        var vacio = document.createElement('div');
                        vacio.className = 'se-ac-item se-ac-empty';
                        vacio.textContent = 'Sin despachos disponibles con ese texto';
                        list.appendChild(vacio);
                    } else {
                        items.forEach(function (it) {
                            var div = document.createElement('div');
                            div.className = 'se-ac-item';
                            div.textContent = it.Etiqueta;
                            div.addEventListener('click', function () { seSeleccionarDespacho(it.IdDespacho, esInternacional, it.Etiqueta); });
                            list.appendChild(div);
                        });
                    }
                    list.classList.add('show');
                })
                .catch(function (err) { console.error('Error buscando despachos:', err); });
            }, 300);
        }

        function seSeleccionarDespacho(idDespacho, esInternacional, etiqueta) {
            document.getElementById(esInternacional ? 'acDespachoInternacional' : 'acDespachoNacional').classList.remove('show');
            if (esInternacional) {
                document.getElementById('<%= txtBuscarDespachoInternacional.ClientID %>').value = etiqueta;
                document.getElementById('<%= hdnIdDespachoDestino.ClientID %>').value = idDespacho;
                __doPostBack('<%= lnkAplicarDespachoInternacional.UniqueID %>', '');
            } else {
                document.getElementById('<%= txtBuscarDespachoNacional.ClientID %>').value = etiqueta;
                document.getElementById('<%= hdnIdDespachoOrigen.ClientID %>').value = idDespacho;
                __doPostBack('<%= lnkAplicarDespachoNacional.UniqueID %>', '');
            }
        }

        document.addEventListener('click', function (e) {
            if (!e.target.closest('.se-field')) {
                document.querySelectorAll('.se-ac-list').forEach(function (l) { l.classList.remove('show'); });
            }
        });

        // ============================================================
        //  File upload preview
        // ============================================================
        function seUpdateFileName(input) {
            var info = document.getElementById('fileInfo');
            if (input.files && input.files[0]) {
                info.innerHTML = 'Archivo: <strong>' + input.files[0].name + '</strong> (' + (input.files[0].size / 1024 / 1024).toFixed(2) + ' MB)';
            } else {
                info.innerHTML = 'Ningún archivo seleccionado';
            }
        }
        var dropZone = document.getElementById('dropZone');
        if (dropZone) {
            ['dragover','dragenter'].forEach(function (ev) {
                dropZone.addEventListener(ev, function (e) { e.preventDefault(); e.stopPropagation(); this.classList.add('drag-over'); });
            });
            ['dragleave','drop'].forEach(function (ev) {
                dropZone.addEventListener(ev, function (e) { e.preventDefault(); e.stopPropagation(); this.classList.remove('drag-over'); });
            });
            dropZone.addEventListener('drop', function (e) {
                var fu = document.getElementById('<%= fileExcel.ClientID %>');
                if (fu && e.dataTransfer.files.length) { fu.files = e.dataTransfer.files; seUpdateFileName(fu); }
            });
        }

        // Restaurar tab activo si viene en hash
        (function restoreTab() {
            var hash = window.location.hash;
            if (hash && hash.indexOf('#tab=') === 0) {
                var t = hash.replace('#tab=', '');
                var btn = document.querySelector('.se-tab[data-target="' + t + '"]');
                if (btn) btn.click();
            }
        })();

        // ============================================================
        //  Datalist: poblar desde window.SE_AC (emitido por code-behind)
        // ============================================================
        function sePopulateDatalist(id, items) {
            var dl = document.getElementById(id);
            if (!dl || !items) return;
            var html = '';
            for (var i = 0; i < items.length; i++) {
                var v = items[i].replace(/&/g,'&amp;').replace(/"/g,'&quot;').replace(/</g,'&lt;');
                html += '<option value="' + v + '">';
            }
            dl.innerHTML = html;
        }

        document.addEventListener('DOMContentLoaded', function () {
            if (!window.SE_AC) return;
            sePopulateDatalist('seListClientes',    window.SE_AC.clientes);
            sePopulateDatalist('seListConductores', window.SE_AC.conductores);
            sePopulateDatalist('seListTractos',     window.SE_AC.tractos);
            sePopulateDatalist('seListCarretas',    window.SE_AC.carretas);
        });
    </script>
</asp:Content>

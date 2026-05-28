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
        #panel-grid {
            width: 100vw;
            position: relative;
            left: 50%;
            right: 50%;
            margin-left: -50vw;
            margin-right: -50vw;
            box-sizing: border-box;
        }
        #panel-grid > .se-card {
            border-radius: 0;
            border-left: none;
            border-right: none;
            padding-left: 24px;
            padding-right: 24px;
        }
        #panel-grid .se-xls-wrap {
            border-left: none;
            border-right: none;
            border-radius: 0;
            width: calc(100vw - 17px);
        }
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
        .se-acc-badge { margin-left: auto; background: var(--se-bg); color: var(--se-muted); padding: 3px 10px; border-radius: 999px; font-size: 11px; font-weight: 700; }
        .se-acc-badge.ok { background: #D1FAE5; color: #065F46; }

        /* ===== Grid estilo Excel ===== */
        .se-grid-toolbar { display:flex; gap:8px; align-items:center; flex-wrap:wrap; margin-bottom:12px; }
        .se-grid-toolbar .spacer { flex:1; }
        .se-grid-status { font-size:12px; color:var(--se-muted); font-weight:600; }
        .se-grid-status .dirty { color: var(--se-warning); }
        .se-grid-status .saved { color: #065F46; }

        .se-xls-wrap { border:1px solid var(--se-border); border-radius:0; overflow:auto; background:#fff; height: calc(100vh - 320px); min-height: 400px; position:relative; width: 100%; }
        table.se-xls { border-collapse: separate; border-spacing: 0; font-size: 12px; font-family: 'Plus Jakarta Sans', sans-serif; min-width: 100%; table-layout: fixed; }
        table.se-xls thead th { position: sticky; top: 0; z-index: 3; background: var(--se-ink); color:#fff; padding: 7px 6px; font-size: 10.5px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.04em; text-align: left; white-space: nowrap; border-right: 1px solid #1f2a44; border-bottom: 2px solid #000; overflow: hidden; text-overflow: ellipsis; }
        table.se-xls thead th.colgroup-PE { background: #1E3A8A; }
        table.se-xls thead th.colgroup-BN { background: #92400E; }
        table.se-xls thead th.colgroup-FR { background: #6B21A8; }
        table.se-xls thead th.colgroup-EC { background: #065F46; }
        table.se-xls thead th.colgroup-INC{ background: #991B1B; }
        table.se-xls tbody td { padding: 0; border-right: 1px solid var(--se-border); border-bottom: 1px solid var(--se-border); background:#fff; white-space: nowrap; vertical-align: middle; overflow: hidden; }
        table.se-xls tbody tr:nth-child(even) td { background: #FAFBFE; }
        table.se-xls tbody tr.row-dirty td { background: #FFFBEB !important; }
        table.se-xls tbody tr.row-new td { background: #EFF6FF !important; }
        table.se-xls tbody tr.row-saved td { background: #ECFDF5 !important; }
        table.se-xls tbody tr.row-error td { background: #FEF2F2 !important; }
        table.se-xls .rownum { position: sticky; left: 0; z-index: 2; background: var(--se-bg) !important; color: var(--se-muted); font-weight: 700; text-align: center; padding: 4px 6px; width: 36px; min-width: 36px; max-width: 36px; border-right: 2px solid var(--se-border); }
        table.se-xls thead th.rownum { background: var(--se-ink) !important; color:#fff; z-index: 4; width: 36px; min-width: 36px; max-width: 36px; }
        table.se-xls th.col-text     { width: 120px; min-width: 100px; }
        table.se-xls th.col-narrow   { width: 88px;  min-width: 76px;  }
        table.se-xls th.col-dt       { width: 148px; min-width: 140px; }
        table.se-xls th.col-num      { width: 72px;  min-width: 60px;  }
        table.se-xls th.col-estado   { width: 110px; min-width: 100px; }
        table.se-xls th.col-bodega   { width: 100px; min-width: 90px;  }
        table.se-xls th.col-comentario { width: 160px; min-width: 140px; }
        table.se-xls td.col-text input,
        table.se-xls td.col-narrow input { width: 100%; }
        table.se-xls input, table.se-xls select { width: 100%; border: none; padding: 5px 6px; background: transparent; font: inherit; font-size: 12px; color: var(--se-ink); outline: none; box-sizing: border-box; }
        table.se-xls input[type="datetime-local"] { font-size: 11.5px; padding: 5px 4px; }
        table.se-xls input[type="number"] { text-align:right; }
        table.se-xls td:focus-within { box-shadow: inset 0 0 0 2px var(--se-primary); background: #fff !important; z-index: 1; position: relative; }
        table.se-xls input:focus, table.se-xls select:focus { background:#fff; }

        /* Autocomplete: deja que el input muestre el datalist nativo */
        table.se-xls input[list] { cursor: text; }

        .se-xls-legend { display:flex; gap:14px; flex-wrap:wrap; font-size:11px; color:var(--se-muted); margin-top:8px; }
        .se-xls-legend span { display:inline-flex; align-items:center; gap:6px; }
        .se-xls-legend i { width:12px; height:12px; border-radius:3px; display:inline-block; border:1px solid var(--se-border); }

        /* Hint de navegación */
        .se-nav-hint { font-size:11px; color:var(--se-muted); margin-top:6px; }
        .se-nav-hint kbd { background:var(--se-bg); border:1px solid var(--se-border); border-radius:4px; padding:1px 5px; font-family:monospace; font-size:11px; }
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
            <button type="button" class="se-tab active" data-target="panel-grid">📊 Grid (Excel)</button>
            <button type="button" class="se-tab"        data-target="panel-bandeja">🚛 Viajes en curso</button>
            <button type="button" class="se-tab"        data-target="panel-form">📝 Registro / Edición</button>
            <button type="button" class="se-tab"        data-target="panel-import">📂 Importar Excel</button>
            <button type="button" class="se-tab"        data-target="panel-list">📋 Historial</button>
        </div>

        <asp:HiddenField ID="hdnIdSeguimiento" runat="server" Value="0" />

        <asp:Panel ID="pnlAlert" runat="server" Visible="false" CssClass="se-alert se-alert-info">
            <asp:Literal ID="litAlert" runat="server"></asp:Literal>
        </asp:Panel>

        <!-- ========== TAB GRID (estilo Excel) ========== -->
        <div id="panel-grid" class="se-tab-panel active">
            <div class="se-card" style="padding:18px;">
                <div class="se-section-title">Grid editable — captura tipo Excel</div>
                <div class="se-alert se-alert-info" style="margin-bottom:12px;">
                    💡 Edita cualquier celda como en Excel. Columnas con <strong>autocompletado</strong>: Cliente, Conductor, Tracto, Carreta. Usa <kbd>Enter</kbd> para bajar a la fila siguiente; <kbd>Tab</kbd> para avanzar al campo siguiente.
                </div>

                <div class="se-grid-toolbar">
                    <asp:Button ID="btnAgregarFila" runat="server" Text="➕ Nueva fila" CssClass="se-btn se-btn-ghost" CausesValidation="false" OnClientClick="seGridAddRow(); return false;" />
                    <asp:Button ID="btnRefrescarGrid" runat="server" Text="↻ Recargar" CssClass="se-btn se-btn-ghost" CausesValidation="false" OnClick="btnRefrescarGrid_Click" />
                    <label style="display:inline-flex;align-items:center;gap:6px;font-size:13px;color:var(--se-muted);font-weight:600;">
                        <asp:CheckBox ID="chkIncluirFinalizados" runat="server" AutoPostBack="true" OnCheckedChanged="chkIncluirFinalizados_CheckedChanged" />
                        Incluir finalizados
                    </label>
                    <div class="spacer"></div>
                    <div class="se-grid-status" id="seGridStatus">
                        <span id="seGridRowCount">0</span> filas ·
                        <span id="seGridDirtyCount" class="dirty">0</span> con cambios
                    </div>
                    <asp:Button ID="btnGuardarGrid" runat="server" Text="💾 Guardar cambios" CssClass="se-btn se-btn-primary" OnClick="btnGuardarGrid_Click" OnClientClick="return seGridSerialize();" />
                </div>

                <asp:HiddenField ID="hdnGridChanges" runat="server" />

                <div class="se-xls-wrap">
                    <table class="se-xls" id="seGrid">
                        <thead>
                            <tr>
                                <th class="rownum">#</th>
                                <%-- Identidad del viaje — orden igual al Excel --%>
                                <th class="col-text">Cliente</th>
                                <th class="col-text">Conductor Origen</th>
                                <th class="col-narrow">Tracto 1</th>
                                <th class="col-narrow">Carreta</th>
                                <th class="col-text">Conductor Destino</th>
                                <th class="col-narrow">Tracto 2</th>
                                <th class="col-estado">Estado</th>
                                <%-- Tiempos Perú (igual que Excel: SalidaBase → Trujillo → Registro → Programación → ...) --%>
                                <th class="colgroup-PE col-dt">F.H. Salida Base</th>
                                <th class="colgroup-PE col-dt">F.H. Llegada Trujillo</th>
                                <th class="colgroup-PE col-dt">F.H. Registro</th>
                                <th class="colgroup-PE col-dt">F.H. Programación</th>
                                <th class="colgroup-PE col-dt">F.H. Ingreso Planta</th>
                                <th class="colgroup-PE col-dt">F.H. Inicio Carga</th>
                                <th class="colgroup-PE col-dt">F.H. Término Carga</th>
                                <th class="colgroup-PE col-dt">F.H. Salida Planta</th>
                                <th class="colgroup-PE col-dt">F.H. Llegada Base 2</th>
                                <th class="colgroup-PE col-dt">F.H. Salida Base 2</th>
                                <%-- Bodega Nacional --%>
                                <th class="colgroup-BN col-dt">F.H. Llegada Bodega Nac.</th>
                                <th class="colgroup-BN col-dt">F.H. Ingreso Bodega Nac.</th>
                                <th class="colgroup-BN col-dt">F.H. Salida Bodega Nac.</th>
                                <th class="colgroup-BN col-bodega">Bodega Nacional</th>
                                <%-- Frontera --%>
                                <th class="colgroup-FR col-dt">F.H. Llegada CEBAF</th>
                                <th class="colgroup-FR col-dt">F.H. Cruce Ecuador</th>
                                <th class="colgroup-FR col-dt">F.H. Autoriz. Nac.</th>
                                <%-- Ecuador --%>
                                <th class="colgroup-EC col-bodega">Bodega Ecuatoriana</th>
                                <th class="colgroup-EC col-dt">F.H. Llegada TCI</th>
                                <th class="colgroup-EC col-dt">F.H. Salida TCI</th>
                                <th class="colgroup-EC col-bodega">Bodega Descarga</th>
                                <th class="colgroup-EC col-dt">F.H. Llegada Planta EC</th>
                                <th class="colgroup-EC col-dt">F.H. Llegada Almacén</th>
                                <th class="colgroup-EC col-dt">F.H. Ingreso</th>
                                <th class="colgroup-EC col-dt">F.H. Inicio Descarga</th>
                                <th class="colgroup-EC col-dt">F.H. Término Descarga</th>
                                <th class="colgroup-EC col-dt">F.H. Salida</th>
                                <%-- Incidencias --%>
                                <th class="colgroup-INC col-num">S.Robados</th>
                                <th class="colgroup-INC col-num">S.Rotos</th>
                                <th class="colgroup-INC col-num">S.Mojados</th>
                                <th class="colgroup-INC col-comentario">Motivo / Comentario</th>
                            </tr>
                        </thead>
                        <tbody id="seGridBody"></tbody>
                    </table>
                </div>

                <div class="se-xls-legend">
                    <span><i style="background:#fff;"></i> Sin cambios</span>
                    <span><i style="background:#FFFBEB;border-color:#F59E0B;"></i> Modificada</span>
                    <span><i style="background:#EFF6FF;border-color:#2563EB;"></i> Nueva</span>
                    <span><i style="background:#ECFDF5;border-color:#10B981;"></i> Guardada</span>
                    <span><i style="background:#FEF2F2;border-color:#EF4444;"></i> Error</span>
                </div>
                <div class="se-nav-hint">
                    <kbd>Enter</kbd> siguiente fila &nbsp;|&nbsp; <kbd>Tab</kbd> siguiente campo &nbsp;|&nbsp; <kbd>Shift+Tab</kbd> campo anterior
                </div>
            </div>
        </div>

        <!-- ========== TAB: BANDEJA DE VIAJES EN CURSO ========== -->
        <div id="panel-bandeja" class="se-tab-panel">
            <div class="se-card">
                <div class="se-section-title">Viajes en curso (registro progresivo)</div>
                <div class="se-alert se-alert-info" style="margin-bottom:14px;">
                    💡 <strong>Captura progresiva:</strong> No necesitas llenar todo un viaje de golpe. Crea uno con los datos mínimos y ve agregando los hitos conforme avanza el viaje.
                </div>

                <div class="se-bandeja-toolbar">
                    <asp:TextBox ID="txtFiltroBandeja" runat="server" placeholder="Buscar por cliente, conductor o tracto..." />
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
                <asp:Button ID="btnCancelarEdicion" runat="server" Text="✕ Cancelar edición" CssClass="se-btn se-btn-ghost se-btn-cancel" CausesValidation="false" OnClick="btnCancelarEdicion_Click" />
            </asp:Panel>

            <%-- Sección ①: Identidad del viaje (orden igual al Excel) --%>
            <details class="se-accordion" open>
                <summary>① Datos del viaje (obligatorios para abrir)</summary>
                <div class="se-acc-body">
                    <div class="se-grid">
                        <div class="se-field"><label>Cliente *</label>
                            <asp:TextBox ID="txtCliente" runat="server" placeholder="Escribe para buscar..." /></div>
                        <div class="se-field"><label>Conductor Origen</label>
                            <asp:TextBox ID="txtConductorOrigen" runat="server" placeholder="Escribe el nombre..." /></div>
                        <div class="se-field"><label>Tracto 1 *</label>
                            <asp:TextBox ID="txtTracto1" runat="server" placeholder="Placa..." /></div>
                        <div class="se-field"><label>Carreta</label>
                            <asp:TextBox ID="txtCarreta" runat="server" placeholder="Placa..." /></div>
                        <div class="se-field"><label>Conductor Destino</label>
                            <asp:TextBox ID="txtConductorDestino" runat="server" placeholder="Escribe el nombre..." /></div>
                        <div class="se-field"><label>Tracto 2</label>
                            <asp:TextBox ID="txtTracto2" runat="server" placeholder="Placa..." /></div>
                        <div class="se-field"><label>F.H. Programación *</label>
                            <asp:TextBox ID="txtFhProgramacion" runat="server" TextMode="DateTimeLocal" /></div>
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

            <%-- Sección ②: Tiempos en Perú (orden: SalidaBase → Trujillo → Registro → IngresoPlanta → ...) --%>
            <details class="se-accordion">
                <summary>② Tiempos en Perú</summary>
                <div class="se-acc-body">
                    <div class="se-grid">
                        <div class="se-field"><label>F.H. Salida Base</label>
                            <asp:TextBox ID="txtFhSalidaBase1" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Llegada Trujillo</label>
                            <asp:TextBox ID="txtFhLlegadaTrujillo" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Registro</label>
                            <asp:TextBox ID="txtFhRegistro" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Ingreso Planta</label>
                            <asp:TextBox ID="txtFhIngresoPlanta" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Inicio Carga</label>
                            <asp:TextBox ID="txtFhInicioCarga" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Término Carga</label>
                            <asp:TextBox ID="txtFhTerminoCarga" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Salida Planta</label>
                            <asp:TextBox ID="txtFhSalidaPlanta" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Llegada Base 2</label>
                            <asp:TextBox ID="txtFhLlegadaBase2" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Salida Base 2</label>
                            <asp:TextBox ID="txtFhSalidaBase2" runat="server" TextMode="DateTimeLocal" /></div>
                    </div>
                </div>
            </details>

            <%-- Sección ③: Bodega Nacional --%>
            <details class="se-accordion">
                <summary>③ Bodega Nacional</summary>
                <div class="se-acc-body">
                    <div class="se-grid">
                        <div class="se-field"><label>F.H. Llegada Bodega Nacional</label>
                            <asp:TextBox ID="txtFhLlegadaBodegaNacional" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Ingreso Bodega Nacional</label>
                            <asp:TextBox ID="txtFhIngresoBodegaNacional" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Salida Bodega Nacional</label>
                            <asp:TextBox ID="txtFhSalidaBodegaNacional" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>Bodega Nacional</label>
                            <asp:DropDownList ID="ddlBodegaNacional" runat="server" CssClass="se-field-select">
                                <asp:ListItem Text="-- Seleccionar --" Value="" />
                                <asp:ListItem Text="DEPSA"    Value="DEPSA" />
                                <asp:ListItem Text="COMPLEX"  Value="COMPLEX" />
                            </asp:DropDownList>
                        </div>
                    </div>
                </div>
            </details>

            <%-- Sección ④: Frontera (CEBAF / Nacionalización) --%>
            <details class="se-accordion">
                <summary>④ Frontera (CEBAF / Nacionalización)</summary>
                <div class="se-acc-body">
                    <div class="se-grid">
                        <div class="se-field"><label>F.H. Llegada CEBAF</label>
                            <asp:TextBox ID="txtFhLlegadaCEBAF" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Cruce Ecuador</label>
                            <asp:TextBox ID="txtFhCruceEcuador" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Autorización Nacionalización</label>
                            <asp:TextBox ID="txtFhAutorizacionNacionalizacion" runat="server" TextMode="DateTimeLocal" /></div>
                    </div>
                </div>
            </details>

            <%-- Sección ⑤: Tiempos en Ecuador --%>
            <details class="se-accordion">
                <summary>⑤ Tiempos en Ecuador</summary>
                <div class="se-acc-body">
                    <div class="se-grid">
                        <div class="se-field"><label>Bodega Ecuatoriana</label>
                            <asp:DropDownList ID="ddlBodegaEcuatoriana" runat="server">
                                <asp:ListItem Text="-- Seleccionar --" Value="" />
                                <asp:ListItem Text="TCI"     Value="TCI" />
                                <asp:ListItem Text="PUYANGO" Value="PUYANGO" />
                            </asp:DropDownList>
                        </div>
                        <div class="se-field"><label>F.H. Llegada TCI</label>
                            <asp:TextBox ID="txtFhLlegadaTCI" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Salida TCI</label>
                            <asp:TextBox ID="txtFhSalidaTCI" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>Bodega Descarga</label>
                            <asp:DropDownList ID="ddlBodegaDescarga" runat="server">
                                <asp:ListItem Text="-- Seleccionar --" Value="" />
                                <asp:ListItem Text="INBALNOR" Value="INBALNOR" />
                                <asp:ListItem Text="JAVE"     Value="JAVE" />
                                <asp:ListItem Text="OREMANS"  Value="OREMANS" />
                            </asp:DropDownList>
                        </div>
                        <div class="se-field"><label>F.H. Llegada Planta Ecuador</label>
                            <asp:TextBox ID="txtFhLlegadaPlantaEcuador" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Llegada Almacén</label>
                            <asp:TextBox ID="txtFhLlegadaAlmacen" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Ingreso</label>
                            <asp:TextBox ID="txtFhIngreso" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Inicio Descarga</label>
                            <asp:TextBox ID="txtFhInicioDescarga" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Término Descarga</label>
                            <asp:TextBox ID="txtFhTerminoDescarga" runat="server" TextMode="DateTimeLocal" /></div>
                        <div class="se-field"><label>F.H. Salida</label>
                            <asp:TextBox ID="txtFhSalida" runat="server" TextMode="DateTimeLocal" /></div>
                    </div>
                </div>
            </details>

            <%-- Sección ⑥: Incidencias --%>
            <details class="se-accordion">
                <summary>⑥ Incidencias y Observaciones</summary>
                <div class="se-acc-body">
                    <div class="se-grid">
                        <div class="se-field"><label>Sacos Robados</label>
                            <asp:TextBox ID="txtSacosRobados" runat="server" TextMode="Number" Text="0" /></div>
                        <div class="se-field"><label>Sacos Rotos</label>
                            <asp:TextBox ID="txtSacosRotos" runat="server" TextMode="Number" Text="0" /></div>
                        <div class="se-field"><label>Sacos Mojados</label>
                            <asp:TextBox ID="txtSacosMojados" runat="server" TextMode="Number" Text="0" /></div>
                    </div>
                    <div class="se-field" style="margin-top:14px;"><label>Motivo de retraso / Comentario</label>
                        <asp:TextBox ID="txtMotivoRetraso" runat="server" TextMode="MultiLine" Rows="3" />
                    </div>
                </div>
            </details>

            <div class="se-card">
                <div class="se-actions">
                    <asp:Button ID="btnLimpiar" runat="server" Text="Limpiar" CssClass="se-btn se-btn-ghost" CausesValidation="false" OnClick="btnLimpiar_Click" />
                    <asp:Button ID="btnGuardar" runat="server" Text="💾 Guardar avance" CssClass="se-btn se-btn-primary" OnClick="btnGuardar_Click" />
                </div>
                <div style="text-align:right;font-size:12px;color:var(--se-muted);margin-top:8px;">
                    Solo se guardan los campos llenos. Los que dejes vacíos no se sobrescriben — puedes seguir avanzando otro día.
                </div>
            </div>
        </div>

        <!-- ========== TAB: IMPORT EXCEL ========== -->
        <div id="panel-import" class="se-tab-panel">
            <div class="se-card">
                <div class="se-section-title">Importación masiva desde Excel</div>
                <div class="se-alert se-alert-info" style="border-color: var(--se-primary);">
                    <strong>Formato esperado:</strong> el archivo Excel debe tener una hoja con los encabezados del STATUS GENERAL VIVIANA. El sistema mapea automáticamente las columnas reconocidas.
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

        // ============================================================
        //  GRID ESTILO EXCEL
        //  Columnas en el mismo orden que el Excel STATUS GENERAL VIVIANA:
        //  cliente → conductorOrigen → tracto1 → carreta → conductorDestino → tracto2 → estado
        //  → fhSalidaBase1 → fhLlegadaTrujillo → fhRegistro → fhProgramacion → ...
        // ============================================================
        var SE_GRID_COLS = [
            { key: 'cliente',                       type: 'autocomplete', list: 'seListClientes'    },
            { key: 'conductorOrigen',               type: 'autocomplete', list: 'seListConductores' },
            { key: 'tracto1',                       type: 'autocomplete', list: 'seListTractos'     },
            { key: 'carreta',                       type: 'autocomplete', list: 'seListCarretas'    },
            { key: 'conductorDestino',              type: 'autocomplete', list: 'seListConductores' },
            { key: 'tracto2',                       type: 'autocomplete', list: 'seListTractos'     },
            { key: 'estado',                        type: 'select', options: ['EN_CURSO','PENDIENTE','FINALIZADO','COMPLETADO','RETRASADO','CANCELADO'] },
            { key: 'fhSalidaBase1',                 type: 'datetime' },
            { key: 'fhLlegadaTrujillo',             type: 'datetime' },
            { key: 'fhRegistro',                    type: 'datetime' },
            { key: 'fhProgramacion',                type: 'datetime' },
            { key: 'fhIngresoPlanta',               type: 'datetime' },
            { key: 'fhInicioCarga',                 type: 'datetime' },
            { key: 'fhTerminoCarga',                type: 'datetime' },
            { key: 'fhSalidaPlanta',                type: 'datetime' },
            { key: 'fhLlegadaBase2',                type: 'datetime' },
            { key: 'fhSalidaBase2',                 type: 'datetime' },
            { key: 'fhLlegadaBodegaNacional',       type: 'datetime' },
            { key: 'fhIngresoBodegaNacional',       type: 'datetime' },
            { key: 'fhSalidaBodegaNacional',        type: 'datetime' },
            { key: 'bodegaNacional',                type: 'select', options: ['','DEPSA','COMPLEX'] },
            { key: 'fhLlegadaCEBAF',                type: 'datetime' },
            { key: 'fhCruceEcuador',                type: 'datetime' },
            { key: 'fhAutorizacionNacionalizacion', type: 'datetime' },
            { key: 'bodegaEcuatoriana',             type: 'select', options: ['','TCI','PUYANGO'] },
            { key: 'fhLlegadaTCI',                  type: 'datetime' },
            { key: 'fhSalidaTCI',                   type: 'datetime' },
            { key: 'bodegaDescarga',                type: 'select', options: ['','INBALNOR','JAVE','OREMANS'] },
            { key: 'fhLlegadaPlantaEcuador',        type: 'datetime' },
            { key: 'fhLlegadaAlmacen',              type: 'datetime' },
            { key: 'fhIngreso',                     type: 'datetime' },
            { key: 'fhInicioDescarga',              type: 'datetime' },
            { key: 'fhTerminoDescarga',             type: 'datetime' },
            { key: 'fhSalida',                      type: 'datetime' },
            { key: 'sacosRobados',                  type: 'number'   },
            { key: 'sacosRotos',                    type: 'number'   },
            { key: 'sacosMojados',                  type: 'number'   },
            { key: 'motivoRetraso',                 type: 'text'     }
        ];

        var SE_GRID_DATA   = [];
        var SE_GRID_ROWS   = [];
        var SE_GRID_NEXTID = -1;

        function seToInputDate(v) {
            if (!v) return '';
            var s = String(v).replace(' ', 'T');
            if (s.length >= 16) return s.substring(0, 16);
            return s;
        }

        function seBuildCell(col, value) {
            var td = document.createElement('td');
            var el;
            if (col.type === 'select') {
                el = document.createElement('select');
                // Opción vacía si la columna lo necesita (bodegas)
                col.options.forEach(function (o) {
                    var op = document.createElement('option');
                    op.value = o;
                    op.textContent = o === '' ? '—' : o;
                    if ((value || '') === o) op.selected = true;
                    el.appendChild(op);
                });
            } else if (col.type === 'autocomplete') {
                el = document.createElement('input');
                el.type = 'text';
                if (col.list) el.setAttribute('list', col.list);
                el.setAttribute('autocomplete', 'off');
                el.value = value == null ? '' : value;
            } else if (col.type === 'datetime') {
                el = document.createElement('input');
                el.type = 'datetime-local';
                el.value = seToInputDate(value);
            } else if (col.type === 'number') {
                el = document.createElement('input');
                el.type = 'number';
                el.min = '0';
                el.value = (value === null || value === undefined || value === '') ? '' : value;
            } else {
                el = document.createElement('input');
                el.type = 'text';
                el.value = value == null ? '' : value;
            }
            el.setAttribute('data-key', col.key);
            el.addEventListener('input',  seGridOnEdit);
            el.addEventListener('change', seGridOnEdit);
            td.appendChild(el);
            return td;
        }

        function seGridOnEdit(e) {
            var tr = e.target.closest('tr');
            if (!tr) return;
            var idx = parseInt(tr.getAttribute('data-idx'), 10);
            var key = e.target.getAttribute('data-key');
            var row = SE_GRID_ROWS[idx];
            if (!row) return;
            row[key] = e.target.value;
            row._dirty = true;
            tr.classList.remove('row-saved', 'row-error');
            if (row.idSeguimiento > 0) {
                tr.classList.add('row-dirty');
            } else {
                tr.classList.add('row-new');
            }
            seGridUpdateStatus();
        }

        function seGridUpdateStatus() {
            var dirty = 0;
            SE_GRID_ROWS.forEach(function (r) { if (r._dirty) dirty++; });
            var rc = document.getElementById('seGridRowCount');
            var dc = document.getElementById('seGridDirtyCount');
            if (rc) rc.textContent = SE_GRID_ROWS.length;
            if (dc) dc.textContent = dirty;
        }

        function seGridRenderRow(row, idx) {
            var tr = document.createElement('tr');
            tr.setAttribute('data-idx', idx);
            tr.setAttribute('data-id',  row.idSeguimiento || 0);
            if (row.idSeguimiento <= 0) tr.classList.add('row-new');

            var num = document.createElement('td');
            num.className = 'rownum';
            num.textContent = (idx + 1);
            tr.appendChild(num);

            SE_GRID_COLS.forEach(function (col) { tr.appendChild(seBuildCell(col, row[col.key])); });
            return tr;
        }

        function seGridRender() {
            var body = document.getElementById('seGridBody');
            if (!body) return;
            body.innerHTML = '';
            SE_GRID_ROWS.forEach(function (row, idx) {
                body.appendChild(seGridRenderRow(row, idx));
            });
            seGridUpdateStatus();
        }

        function seGridAddRow() {
            var nr = { idSeguimiento: SE_GRID_NEXTID--, estado: 'EN_CURSO', _dirty: true };
            SE_GRID_ROWS.push(nr);
            var body = document.getElementById('seGridBody');
            body.appendChild(seGridRenderRow(nr, SE_GRID_ROWS.length - 1));
            seGridUpdateStatus();
            var tr = body.lastElementChild;
            var first = tr.querySelector('input,select');
            if (first) { first.focus(); if (first.select) first.select(); }
        }

        function seGridSerialize() {
            var changes = [];
            SE_GRID_ROWS.forEach(function (r) {
                if (!r._dirty) return;
                var c = {};
                c.idSeguimiento = r.idSeguimiento;
                SE_GRID_COLS.forEach(function (col) {
                    var v = r[col.key];
                    c[col.key] = (v === undefined || v === null) ? '' : String(v);
                });
                changes.push(c);
            });
            var hdn = document.getElementById('<%= hdnGridChanges.ClientID %>');
            if (changes.length === 0) {
                alert('No hay cambios pendientes para guardar.');
                if (hdn) hdn.value = '';
                return false;
            }
            if (hdn) hdn.value = JSON.stringify(changes);
            return true;
        }

        function seGridInit() {
            var raw = document.getElementById('seGridInitialData');
            if (!raw) return;
            try {
                SE_GRID_DATA = JSON.parse(raw.textContent || raw.innerText || '[]');
            } catch (e) {
                SE_GRID_DATA = [];
            }
            SE_GRID_ROWS = SE_GRID_DATA.map(function (r) { return Object.assign({}, r, { _dirty: false }); });
            seGridRender();
        }

        document.addEventListener('DOMContentLoaded', seGridInit);

        // ============================================================
        //  Navegación con Enter en el grid: baja a la siguiente fila
        // ============================================================
        document.addEventListener('keydown', function (e) {
            if (e.key !== 'Enter') return;
            var target = e.target;
            if (!target || !target.closest('#seGrid')) return;
            if (target.tagName === 'TEXTAREA') return;
            var td  = target.closest('td');
            if (!td) return;
            var tr  = td.closest('tr');
            var colIdx = Array.from(tr.cells).indexOf(td);
            var nextTr = tr.nextElementSibling;
            if (!nextTr) {
                seGridAddRow();
                nextTr = document.getElementById('seGridBody').lastElementChild;
            }
            var nextTd = nextTr.cells[colIdx];
            if (nextTd) {
                var nextInput = nextTd.querySelector('input,select');
                if (nextInput) { nextInput.focus(); if (nextInput.select) nextInput.select(); }
            }
            e.preventDefault();
        });
    </script>

    <%-- Payload inicial del grid --%>
    <script type="application/json" id="seGridInitialData"><asp:Literal ID="litGridDataJson" runat="server" /></script>
</asp:Content>

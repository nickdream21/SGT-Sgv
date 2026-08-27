<%@ Page Title="Gestión de Viajes y Lotes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ListaDespachos.aspx.cs" Inherits="WebSGV.Views.ListaDespachos" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

<style type="text/css">
/* ==========================================================================
   Lista Despachos — Professional / Intuitive redesign
   Misma paleta que RegistroDespacho.aspx: slate #1e293b, azul #1e40af,
   verde #16a34a, bordes #e2e8f0, fondos #f8fafc. Solo presentación: no se
   tocó ningún ID, control de servidor ni handler.
   ========================================================================== */

.card:hover { transform: none !important; box-shadow: 0 2px 8px rgba(0,0,0,.08) !important; }

/* ---- Encabezado de página ---- */
.ld-page-header { background: #1e293b; color: #f8fafc; border-radius: 8px 8px 0 0; padding: 1rem 1.5rem; }
.ld-page-header h4 { margin: 0; font-size: 1.05rem; font-weight: 600; letter-spacing: .01em; display: flex; align-items: center; flex-wrap: wrap; gap: .6rem; }
.ld-page-header .badge { font-size: .68rem; font-weight: 600; padding: .35em .7em; }

/* ---- Resumen general (stat tiles) ---- */
.ld-stats-row { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; margin-bottom: 1.75rem; }
.ld-stat-tile {
    display: flex; align-items: center; gap: .9rem; background: #fff; border: 1px solid #e2e8f0;
    border-radius: 10px; padding: 1rem 1.15rem; text-decoration: none !important; cursor: pointer;
    transition: box-shadow .15s ease, transform .15s ease; width: 100%; text-align: left;
}
.ld-stat-tile:hover { box-shadow: 0 4px 14px rgba(15,23,42,.08); transform: translateY(-1px); }
.ld-stat-icon { width: 44px; height: 44px; border-radius: 10px; display: flex; align-items: center; justify-content: center; font-size: 1.05rem; flex-shrink: 0; }
.ld-stat-icon-blue { background: #dbeafe; color: #1e40af; }
.ld-stat-icon-green { background: #dcfce7; color: #16a34a; }
.ld-stat-icon-amber { background: #fef3c7; color: #b45309; }
.ld-stat-text { display: flex; flex-direction: column; }
.ld-stat-value { font-size: 1.5rem; font-weight: 700; color: #1e293b; line-height: 1.15; }
.ld-stat-label { font-size: .74rem; font-weight: 600; text-transform: uppercase; letter-spacing: .04em; color: #64748b; margin-top: .15rem; }
.ld-stat-tile-alert { border-color: #fca5a5 !important; background: #fff5f5 !important; }
.ld-stat-tile-alert .ld-stat-value { color: #b91c1c !important; }

/* ---- Barra de navegación: tabs segmentados + CTA ---- */
.ld-toolbar { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: .75rem; margin-bottom: 1.5rem; }
.ld-toolbar-nav { display: inline-flex; background: #f1f5f9; border: 1px solid #e2e8f0; border-radius: 8px; padding: .25rem; gap: .2rem; }
.ld-toolbar-nav .btn.btn-nav {
    border: 1px solid transparent !important;
    background: transparent !important;
    color: #475569 !important;
    font-weight: 600;
    font-size: .82rem;
    padding: .5rem 1.15rem;
    border-radius: 6px;
    box-shadow: none !important;
    transition: background-color .15s ease, color .15s ease;
}
.ld-toolbar-nav .btn.btn-nav:hover { background: #e2e8f0 !important; color: #1e293b !important; }
.ld-toolbar-nav .btn.btn-nav.active-nav { background: #1e40af !important; color: #fff !important; }
.ld-toolbar-back {
    font-weight: 600 !important;
    font-size: .82rem !important;
    padding: .55rem 1.15rem !important;
    border-radius: 6px !important;
    border-color: #1e40af !important;
    color: #1e40af !important;
    background: #fff !important;
}
.ld-toolbar-back:hover { background: #eff6ff !important; }

/* ---- Tarjetas de sección ---- */
.section-card { background: #fff; border: 1px solid #e2e8f0; border-radius: 8px; box-shadow: 0 1px 3px rgba(15,23,42,.04); overflow: hidden; }
.viajes-section { border-top: 3px solid #1e40af !important; }
.lotes-section { border-top: 3px solid #16a34a !important; }
.detalle-section { border-top: 3px solid #334155 !important; }
.edit-section { border-top: 3px solid #d97706 !important; }

.section-header { background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 1rem 1.25rem; }
.section-title { color: #1e293b !important; font-weight: 600 !important; font-size: .95rem !important; margin: 0; display: flex; align-items: center; flex-wrap: wrap; gap: .5rem; }
.section-title i { color: #64748b; }
.section-actions { display: flex; align-items: center; justify-content: flex-end; flex-wrap: wrap; gap: .5rem; }
.section-content { padding: 1.5rem; }

/* ---- Filtros ---- */
.filters-container { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 1.1rem 1.25rem .35rem; margin-bottom: 1.5rem; }
.filters-container .form-group { margin-bottom: .9rem; }
.filters-heading { font-size: .72rem; font-weight: 700; text-transform: uppercase; letter-spacing: .06em; color: #64748b; margin-bottom: .9rem; display: flex; align-items: center; gap: .4rem; }
.filters-heading i { color: #94a3b8; }
.form-label { font-weight: 600; color: #475569; font-size: .74rem; text-transform: uppercase; letter-spacing: .04em; margin-bottom: .4rem; display: flex; align-items: center; gap: .35rem; }
.form-label i { color: #94a3b8; font-size: .75rem; }
.form-control, .form-select { border: 1px solid #cbd5e1; border-radius: 6px; padding: .5rem .75rem; font-size: .85rem; background: #fff; }
.form-control:focus, .form-select:focus { border-color: #1e40af; box-shadow: 0 0 0 3px rgba(30,64,175,.12); }
.filter-buttons { display: flex; gap: .5rem; }
.filter-buttons .btn { border-radius: 6px; font-weight: 600; font-size: .82rem; padding: .5rem 1rem; white-space: nowrap; margin-left: 0 !important; }
.ld-filter-row-dates { margin-top: .35rem; padding-top: 1rem; border-top: 1px dashed #e2e8f0; }

/* ---- Botones: recolorear Bootstrap para que combine con la paleta ---- */
.btn-primary { background-color: #1e40af !important; border-color: #1e40af !important; }
.btn-primary:hover { background-color: #1e3a8a !important; border-color: #1e3a8a !important; }
.btn-outline-primary { color: #1e40af !important; border-color: #1e40af !important; }
.btn-outline-primary:hover { background-color: #1e40af !important; color: #fff !important; }
.btn-success { background-color: #16a34a !important; border-color: #16a34a !important; }
.btn-success:hover { background-color: #15803d !important; border-color: #15803d !important; }
.btn-outline-success { color: #16a34a !important; border-color: #16a34a !important; }
.btn-outline-success:hover { background-color: #16a34a !important; color: #fff !important; }
.btn-secondary { background-color: #475569 !important; border-color: #475569 !important; }
.btn-outline-secondary { color: #475569 !important; border-color: #cbd5e1 !important; }
.btn-outline-secondary:hover { background-color: #475569 !important; border-color: #475569 !important; color: #fff !important; }
.btn { border-radius: 6px; font-size: .85rem; }
.btn-sm { border-radius: 6px; font-size: .78rem; }
.btn-action { font-weight: 600; }

/* ---- Tabla de datos ---- */
.data-grid-container { background: #fff; border: 1px solid #e2e8f0; border-radius: 8px; overflow-x: auto; }
.data-table thead th {
    background: #f1f5f9 !important; color: #334155 !important; font-weight: 700 !important;
    font-size: .72rem; text-transform: uppercase; letter-spacing: .04em;
    padding: .8rem .9rem; border-bottom: 2px solid #e2e8f0 !important; white-space: nowrap;
}
.data-table tbody td { padding: .7rem .9rem; font-size: .85rem; color: #334155; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
.data-table tbody tr:nth-child(even) { background: #f8fafc; }
.data-table tbody tr:hover { background: #eff6ff; }
.action-buttons { display: flex; gap: .4rem; flex-wrap: wrap; }

/* ---- Badges ---- */
.badge { font-size: .72rem; padding: .4em .75em; border-radius: 999px; font-weight: 600; }
.tipo-internacional { background-color: #e0e7ff !important; color: #3730a3 !important; }
.tipo-nacional { background-color: #dcfce7 !important; color: #166534 !important; }
.estado-abierto, .estado-completado { background-color: #dcfce7 !important; color: #166534 !important; }
.estado-programado { background-color: #dbeafe !important; color: #1e40af !important; }
.estado-enprogreso { background-color: #fef3c7 !important; color: #92400e !important; }
.estado-cancelado { background-color: #fee2e2 !important; color: #991b1b !important; }

/* ---- Tarjetas de resumen (stat tiles) ---- */
.summary-card { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 1.1rem .75rem; text-align: center; }
.summary-value { font-size: 1.4rem !important; font-weight: 700 !important; color: #1e293b !important; }
.summary-label { font-size: .72rem !important; text-transform: uppercase; letter-spacing: .04em; color: #64748b !important; font-weight: 600; }

/* ---- Paneles de documento y detalle ---- */
.doc-panel { border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; }
.doc-panel-header { background: #f1f5f9; color: #1e293b; }
.detail-info { font-size: .82rem; color: #64748b; }
.detail-value, .detail-identifier { color: #1e293b !important; }
.form-section { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; }
.form-section-title { color: #1e293b; }

.conductores-edit-container { border: 1px solid #e2e8f0 !important; background: #f8fafc !important; border-radius: 8px; }
.conductores-edit-container thead th { background: #f1f5f9 !important; color: #1e293b !important; }
.info-panel { background: #eff6ff !important; border: 1px solid #bfdbfe !important; border-radius: 8px; }
.info-title { color: #1e3a8a !important; }
.info-content { color: #1e3a8a !important; }
.edit-warning { background: #fffbeb !important; border-color: #fde68a !important; color: #92400e !important; border-radius: 8px; }

.active-nav { font-weight: 600 !important; }
</style>

    <div class="container-fluid">
        <div class="row">
            <div class="col-md-12">
                <div class="card main-card">
                    <div class="ld-page-header">
                        <h4>
                            <i class="fas fa-tasks mr-2"></i> Gestión de Viajes y Lotes
                            <asp:Label ID="lblContadorGeneral" runat="server" CssClass="badge ml-3" style="background:#334155;font-size:.68rem;font-weight:600;"></asp:Label>
                        </h4>
                    </div>
                    <div class="card-body">
                        <!-- gvManifiestosLote está declarado como PostBackTrigger (no Async) en <Triggers> más
                             abajo: tiene FileUpload por fila y descarga de documentos vía Response.WriteFile,
                             ninguno de los dos funciona en un postback parcial de UpdatePanel. -->
                        <asp:UpdatePanel ID="UpdatePanelMain" runat="server" UpdateMode="Conditional">
                            <ContentTemplate>
                                
                                <!-- Mensajes -->
                                <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-3">
                                    <div class="position-relative">
                                        <asp:Label ID="lblMensaje" runat="server" CssClass="alert d-block mb-0 pe-5"></asp:Label>
                                        <button type="button" class="btn-close position-absolute" style="top:.65rem;right:.75rem;"
                                            onclick="this.closest('.mb-3').style.display='none'" aria-label="Cerrar"></button>
                                    </div>
                                </asp:Panel>

                                <!-- RESUMEN GENERAL -->
                                <div class="ld-stats-row">
                                    <asp:LinkButton ID="lnkStatViajes" runat="server" CssClass="ld-stat-tile" OnClick="lnkStatViajes_Click" CausesValidation="false">
                                        <span class="ld-stat-icon ld-stat-icon-blue"><i class="fas fa-route"></i></span>
                                        <span class="ld-stat-text">
                                            <span class="ld-stat-value"><asp:Label ID="lblStatViajesActivos" runat="server">0</asp:Label></span>
                                            <span class="ld-stat-label">Viajes Activos</span>
                                        </span>
                                    </asp:LinkButton>

                                    <asp:LinkButton ID="lnkStatLotes" runat="server" CssClass="ld-stat-tile" OnClick="lnkStatLotes_Click" CausesValidation="false">
                                        <span class="ld-stat-icon ld-stat-icon-green"><i class="fas fa-layer-group"></i></span>
                                        <span class="ld-stat-text">
                                            <span class="ld-stat-value"><asp:Label ID="lblStatLotesActivos" runat="server">0</asp:Label></span>
                                            <span class="ld-stat-label">Lotes Activos</span>
                                        </span>
                                    </asp:LinkButton>

                                    <asp:LinkButton ID="lnkStatManifiestosPendientes" runat="server" CssClass="ld-stat-tile" OnClick="lnkStatManifiestosPendientes_Click" CausesValidation="false">
                                        <span class="ld-stat-icon ld-stat-icon-amber"><i class="fas fa-passport"></i></span>
                                        <span class="ld-stat-text">
                                            <span class="ld-stat-value"><asp:Label ID="lblStatManifiestosPendientes" runat="server">0</asp:Label></span>
                                            <span class="ld-stat-label">Manifiestos Pendientes</span>
                                        </span>
                                    </asp:LinkButton>
                                </div>

                                <!-- NAVEGACIÓN PRINCIPAL -->
                                <div class="ld-toolbar mb-4">
                                    <div class="ld-toolbar-nav">
                                        <asp:Button ID="btnMostrarViajes" runat="server" 
                                            Text="Viajes Activos" 
                                            CssClass="btn btn-outline-secondary btn-nav"
                                            OnClick="btnMostrarViajes_Click"
                                            CausesValidation="false" />
                                        <asp:Button ID="btnMostrarLotes" runat="server" 
                                            Text="Lotes Registrados" 
                                            CssClass="btn btn-outline-secondary btn-nav"
                                            OnClick="btnMostrarLotes_Click"
                                            CausesValidation="false" />
                                    </div>
                                    <asp:Button ID="btnVolver" runat="server" 
                                        Text="← Nuevo Registro" 
                                        CssClass="btn btn-outline-secondary ld-toolbar-back"
                                        OnClick="btnVolver_Click"
                                        CausesValidation="false" />
                                </div>

                                <!-- PANEL PRINCIPAL: LISTA DE VIAJES ACTIVOS -->
                                <asp:Panel ID="pnlListaViajes" runat="server" Visible="true">
                                    <div class="section-card viajes-section">
                                        <div class="section-header">
                                            <div class="row align-items-center">
                                                <div class="col-md-6">
                                                    <h5 class="section-title">
                                                            <i class="fas fa-route mr-2"></i> Viajes en Progreso
                                                            <asp:Label ID="lblContadorViajes" runat="server" CssClass="badge bg-secondary text-white ml-2"></asp:Label>
                                                        </h5>
                                                </div>
                                                <div class="col-md-6">
                                                    <div class="section-actions">
                                                        <asp:Button ID="btnRefrescarViajes" runat="server" 
                                                            Text="Refrescar" 
                                                            CssClass="btn btn-outline-primary btn-action"
                                                            OnClick="btnRefrescarViajes_Click"
                                                            CausesValidation="false" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="section-content">
                                            
                                            <!-- Filtros para Viajes -->
                                            <div class="filters-container">
                                                <div class="filters-heading"><i class="fas fa-filter"></i>Filtros de búsqueda</div>
                                                <div class="row">
                                                    <div class="col-md-4">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-id-card"></i>Conductor</label>
                                                            <asp:DropDownList ID="ddlFiltroConductorViajes" runat="server" 
                                                                CssClass="form-select"
                                                                AutoPostBack="true"
                                                                OnSelectedIndexChanged="ddlFiltroConductorViajes_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="-- Todos los conductores --"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-4">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-globe-americas"></i>Ámbito</label>
                                                            <asp:DropDownList ID="ddlFiltroTipoViajes" runat="server" 
                                                                CssClass="form-select"
                                                                AutoPostBack="true"
                                                                OnSelectedIndexChanged="ddlFiltroTipoViajes_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="-- Todos los tipos --"></asp:ListItem>
                                                                <asp:ListItem Value="1" Text="Internacional"></asp:ListItem>
                                                                <asp:ListItem Value="0" Text="Nacional"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-4">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-search"></i>N° de Viaje</label>
                                                            <div class="input-group">
                                                                <asp:TextBox ID="txtBuscarViaje" runat="server" 
                                                                    CssClass="form-control" 
                                                                    placeholder="Ej: VP-2025-001"
                                                                    MaxLength="20">
                                                                </asp:TextBox>
                                                                <asp:Button ID="btnBuscarViaje" runat="server" 
                                                                    Text="Buscar" 
                                                                    CssClass="btn btn-outline-secondary"
                                                                    OnClick="btnBuscarViaje_Click"
                                                                    CausesValidation="false" />
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- Grid de Viajes -->
                                            <div class="data-grid-container">
                                                <asp:GridView ID="gvViajesActivos" runat="server" 
                                                    CssClass="table data-table"
                                                    AutoGenerateColumns="false"
                                                    EmptyDataText="No se encontraron viajes activos"
                                                    OnRowCommand="gvViajesActivos_RowCommand"
                                                    DataKeyNames="IdViajeProgreso">
                                                    <Columns>
                                                        <asp:BoundField DataField="NumeroViajeProgreso" HeaderText="N° Viaje" 
                                                            ItemStyle-CssClass="viaje-number" />
                                                        
                                                        <asp:BoundField DataField="NombreConductor" HeaderText="Conductor" />
                                                        
                                                        <asp:BoundField DataField="FechaInicio" HeaderText="Fecha Inicio" 
                                                            DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                                                        
                                                        <asp:BoundField DataField="CantidadDespachos" HeaderText="Despachos" 
                                                            ItemStyle-CssClass="text-center" />
                                                        
                                                        <asp:TemplateField HeaderText="Tipo">
                                                            <ItemTemplate>
                                                                <asp:Label runat="server" 
                                                                    Text='<%# Convert.ToBoolean(Eval("EsInternacional")) ? "Internacional" : "Nacional" %>'
                                                                    CssClass='<%# Convert.ToBoolean(Eval("EsInternacional")) ? "badge tipo-internacional" : "badge tipo-nacional" %>'>
                                                                </asp:Label>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>
                                                        
                                                        <asp:BoundField DataField="FechaUltimaActividad" HeaderText="Última Actividad" 
                                                            DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                                                        
                                                        <asp:TemplateField HeaderText="Estado">
                                                            <ItemTemplate>
                                                                <asp:Label runat="server" 
                                                                    Text='<%# Eval("EstadoViaje") %>'
                                                                    CssClass="badge estado-abierto">
                                                                </asp:Label>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>
                                                        
                                                        <asp:TemplateField HeaderText="Acciones" ItemStyle-Width="200px">
                                                            <ItemTemplate>
                                                                <div class="action-buttons">
                                                                    <asp:Button runat="server" 
                                                                       Text="Ver" 
                                                                       CssClass="btn btn-sm btn-outline-primary"
                                                                       CommandName="VerDespachos"
                                                                       CommandArgument='<%# Eval("IdViajeProgreso") %>' />

                                                                    <asp:Button runat="server" 
                                                                       Text="Finalizar" 
                                                                       CssClass="btn btn-sm btn-outline-danger"
                                                                       CommandName="FinalizarViaje"
                                                                       CommandArgument='<%# Eval("IdViajeProgreso") %>'
                                                                       OnClientClick="return confirm('¿Está seguro de finalizar este viaje? No podrá agregar más despachos.');" />
                                                                </div>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>
                                                    </Columns>
                                                    <EmptyDataTemplate>
                                                        <div class="empty-data">
                                                            <i class="fas fa-info-circle"></i>
                                                            <p>No se encontraron viajes activos con los criterios seleccionados</p>
                                                        </div>
                                                    </EmptyDataTemplate>
                                                </asp:GridView>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                                <!-- PANEL PRINCIPAL: LISTA DE LOTES REGISTRADOS -->
                                <asp:Panel ID="pnlListaLotes" runat="server" Visible="false">
                                    <div class="section-card lotes-section">
                                        <div class="section-header">
                                            <div class="row align-items-center">
                                                <div class="col-md-6">
                                                    <h5 class="section-title">
                                                            <i class="fas fa-layer-group mr-2"></i> Lotes de Despacho
                                                            <asp:Label ID="lblContadorLotes" runat="server" CssClass="badge bg-secondary text-white ml-2"></asp:Label>
                                                        </h5>
                                                </div>
                                                <div class="col-md-6">
                                                    <div class="section-actions">
                                                        <asp:Button ID="btnRefrescarLotes" runat="server" 
                                                            Text="Refrescar" 
                                                            CssClass="btn btn-outline-success btn-action"
                                                            OnClick="btnRefrescarLotes_Click"
                                                            CausesValidation="false" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="section-content">
                                            
                                            <!-- Filtros para Lotes -->
                                            <div class="filters-container">
                                                <div class="filters-heading"><i class="fas fa-filter"></i>Filtros de búsqueda</div>
                                                <div class="row mb-3">
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-building"></i>Cliente</label>
                                                            <asp:DropDownList ID="ddlFiltroClienteLotes" runat="server" 
                                                                CssClass="form-select"
                                                                AutoPostBack="true"
                                                                OnSelectedIndexChanged="ddlFiltroClienteLotes_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="-- Todos los clientes --"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-exchange-alt"></i>Tipo Operación</label>
                                                            <asp:DropDownList ID="ddlFiltroOperacionLotes" runat="server" 
                                                                CssClass="form-select"
                                                                AutoPostBack="true"
                                                                OnSelectedIndexChanged="ddlFiltroOperacionLotes_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="-- Todas las operaciones --"></asp:ListItem>
                                                                <asp:ListItem Value="CARGA" Text="Carga"></asp:ListItem>
                                                                <asp:ListItem Value="DESCARGA" Text="Descarga"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-industry"></i>Planta</label>
                                                            <asp:DropDownList ID="ddlFiltroPlantaLotes" runat="server" 
                                                                CssClass="form-select"
                                                                AutoPostBack="true"
                                                                OnSelectedIndexChanged="ddlFiltroPlantaLotes_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="-- Todas las plantas --"></asp:ListItem>
                                                                <asp:ListItem Value="Lima" Text="Lima"></asp:ListItem>
                                                                <asp:ListItem Value="Guayaquil" Text="Guayaquil"></asp:ListItem>
                                                                <asp:ListItem Value="Trujillo" Text="Trujillo"></asp:ListItem>
                                                                <asp:ListItem Value="Quito" Text="Quito"></asp:ListItem>
                                                                <asp:ListItem Value="Chiclayo" Text="Chiclayo"></asp:ListItem>
                                                                <asp:ListItem Value="Manta" Text="Manta"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-hashtag"></i>N° de Pedido</label>
                                                            <div class="input-group">
                                                                <asp:TextBox ID="txtBuscarLote" runat="server" 
                                                                    CssClass="form-control" 
                                                                    placeholder="N° Pedido"
                                                                    MaxLength="20">
                                                                </asp:TextBox>
                                                                <asp:Button ID="btnBuscarLote" runat="server" 
                                                                    Text="Buscar" 
                                                                    CssClass="btn btn-outline-secondary"
                                                                    OnClick="btnBuscarLote_Click"
                                                                    CausesValidation="false" />
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>

                                                <!-- Búsqueda de documentos: encuentra el/los lote(s) asociados a un
                                                     número de Factura, CPIC o a un conductor, aunque no sepas la
                                                     fecha ni el cliente. -->
                                                <div class="row ld-filter-row-dates">
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-receipt"></i>N° de Factura</label>
                                                            <asp:TextBox ID="txtBuscarFacturaLotes" runat="server"
                                                                CssClass="form-control"
                                                                placeholder="Ej: F222-00004267"
                                                                MaxLength="30">
                                                            </asp:TextBox>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-shipping-fast"></i>N° de CPIC</label>
                                                            <asp:TextBox ID="txtBuscarCPICLotes" runat="server"
                                                                CssClass="form-control"
                                                                placeholder="Ej: 1234567"
                                                                MaxLength="20">
                                                            </asp:TextBox>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-id-card"></i>Conductor</label>
                                                            <asp:TextBox ID="txtBuscarConductorLotes" runat="server"
                                                                CssClass="form-control"
                                                                placeholder="Nombre o apellido">
                                                            </asp:TextBox>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3 d-flex align-items-end">
                                                        <div class="form-group w-100">
                                                            <div class="filter-buttons">
                                                                <asp:Button ID="btnBuscarDocumento" runat="server"
                                                                    Text="Buscar Documento"
                                                                    CssClass="btn btn-primary btn-action flex-fill"
                                                                    OnClick="btnBuscarDocumento_Click"
                                                                    CausesValidation="false" />
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>

                                                <!-- Filtro por Fechas y Estado -->
                                                <div class="row ld-filter-row-dates">
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-calendar-alt"></i>Fecha Desde</label>
                                                            <asp:TextBox ID="txtFechaDesde" runat="server"
                                                                CssClass="form-control"
                                                                TextMode="Date">
                                                            </asp:TextBox>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-calendar-alt"></i>Fecha Hasta</label>
                                                            <asp:TextBox ID="txtFechaHasta" runat="server"
                                                                CssClass="form-control"
                                                                TextMode="Date">
                                                            </asp:TextBox>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label"><i class="fas fa-flag"></i>Estado del Lote</label>
                                                            <asp:DropDownList ID="ddlFiltroEstadoLotes" runat="server"
                                                                CssClass="form-select"
                                                                AutoPostBack="true"
                                                                OnSelectedIndexChanged="ddlFiltroEstadoLotes_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="-- Todos --"></asp:ListItem>
                                                                <asp:ListItem Value="ACTIVO" Text="Activos" Selected="True"></asp:ListItem>
                                                                <asp:ListItem Value="ANULADO" Text="Anulados"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3 d-flex align-items-end">
                                                        <div class="form-group w-100">
                                                            <div class="filter-buttons">
                                                                <asp:Button ID="btnFiltrarFecha" runat="server"
                                                                    Text="Filtrar"
                                                                    CssClass="btn btn-primary btn-action flex-fill"
                                                                    OnClick="btnFiltrarFecha_Click"
                                                                    CausesValidation="false" />
                                                                <asp:Button ID="btnLimpiarFiltros" runat="server"
                                                                    Text="Limpiar"
                                                                    CssClass="btn btn-outline-secondary btn-action"
                                                                    OnClick="btnLimpiarFiltros_Click"
                                                                    CausesValidation="false" />
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- Grid de Lotes -->
                                            <div class="data-grid-container">
                                                <asp:GridView ID="gvLotesRegistrados" runat="server" 
                                                    CssClass="table data-table"
                                                    AutoGenerateColumns="false"
                                                    EmptyDataText="No se encontraron lotes registrados"
                                                    OnRowCommand="gvLotesRegistrados_RowCommand">
                                                    <Columns>
                                                        <asp:BoundField DataField="FechaProgramacion" HeaderText="Fecha Prog." 
                                                            DataFormatString="{0:dd/MM/yyyy}" />
                                                        
                                                        <asp:BoundField DataField="NombreCliente" HeaderText="Cliente" />
                                                        
                                                        <asp:BoundField DataField="NumeroPedido" HeaderText="N° Pedido" 
                                                            ItemStyle-CssClass="pedido-number" />
                                                        
                                                        <asp:BoundField DataField="TipoOperacion" HeaderText="Operación" />
                                                        
                                                        <asp:TemplateField HeaderText="Ámbito">
                                                            <ItemTemplate>
                                                                <asp:Label runat="server" 
                                                                    Text='<%# Convert.ToBoolean(Eval("EsInternacional")) ? "Internacional" : "Nacional" %>'
                                                                    CssClass='<%# Convert.ToBoolean(Eval("EsInternacional")) ? "badge tipo-internacional" : "badge tipo-nacional" %>'>
                                                                </asp:Label>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>
                                                        
                                                        <asp:TemplateField HeaderText="Manifiesto" ItemStyle-CssClass="text-center">
                                                            <ItemTemplate>
                                                                <asp:Label runat="server"
                                                                    Text='<%# Eval("ManifiestoEstado") %>'
                                                                    CssClass='<%# GetManifiestoBadgeClass(Eval("ManifiestoEstado").ToString()) %>'>
                                                                </asp:Label>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>

                                                        <asp:BoundField DataField="PlantaOperacion" HeaderText="Planta" />

                                                        <asp:BoundField DataField="CantidadDespachos" HeaderText="Despachos"
                                                            ItemStyle-CssClass="text-center despachos-count" />

                                                        <asp:BoundField DataField="NumeroFactura" HeaderText="N° Factura" />
                                                        
                                                        <asp:BoundField DataField="NumeroCPIC" HeaderText="N° CPIC" />
                                                        
                                                        <asp:BoundField DataField="FechaCreacion" HeaderText="Creado" 
                                                            DataFormatString="{0:dd/MM/yyyy HH:mm}" />

                                                        <asp:TemplateField HeaderText="Estado" ItemStyle-CssClass="text-center">
                                                            <ItemTemplate>
                                                                <asp:Label runat="server"
                                                                    Text='<%# Eval("EstadoLote") %>'
                                                                    CssClass='<%# (string)Eval("EstadoLote") == "ANULADO" ? "badge bg-danger text-white" : "badge bg-success text-white" %>'>
                                                                </asp:Label>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>

                                                        <asp:TemplateField HeaderText="Acciones" ItemStyle-Width="200px">
                                                            <ItemTemplate>
                                                                <div class="action-buttons">
                                                                    <asp:Button runat="server"
                                                                        Text="Ver"
                                                                        CssClass="btn btn-sm btn-outline-primary"
                                                                        CommandName="VerDetallesLote"
                                                                        CommandArgument='<%# Eval("IdLoteVirtual") %>' />

                                                                    <asp:Button runat="server"
                                                                        Text="Documentos"
                                                                        CssClass="btn btn-sm btn-outline-warning"
                                                                        CommandName="VerManifiestosLote"
                                                                        CommandArgument='<%# Eval("IdLoteVirtual") %>' />

                                                                    <asp:Button runat="server"
                                                                        Text="Editar"
                                                                        CssClass="btn btn-sm btn-outline-secondary"
                                                                        CommandName="EditarLote"
                                                                        CommandArgument='<%# Eval("IdLoteVirtual") %>' />
                                                                </div>
                                                            </ItemTemplate>
                                                        </asp:TemplateField>
                                                    </Columns>
                                                    <EmptyDataTemplate>
                                                        <div class="empty-data">
                                                            <i class="fas fa-info-circle"></i>
                                                            <p>No se encontraron lotes registrados con los criterios seleccionados</p>
                                                        </div>
                                                    </EmptyDataTemplate>
                                                </asp:GridView>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                                <!-- PANEL DETALLES: DESPACHOS DEL VIAJE -->
                                <asp:Panel ID="pnlDetallesViaje" runat="server" Visible="false">
                                    <div class="section-card detalle-section">
                                        <div class="section-header">
                                            <div class="row align-items-center">
                                                <div class="col-md-8">
                                                    <h5 class="section-title">
                                                        <i class="fas fa-clipboard-list"></i> Detalles del Viaje: 
                                                        <asp:Label ID="lblNumeroViajeDetalle" runat="server" CssClass="detail-identifier"></asp:Label>
                                                    </h5>
                                                    <div class="detail-info">
                                                        Conductor: <asp:Label ID="lblConductorDetalle" runat="server" CssClass="detail-value"></asp:Label> | 
                                                        Inicio: <asp:Label ID="lblFechaInicioDetalle" runat="server" CssClass="detail-value"></asp:Label>
                                                    </div>
                                                </div>
                                                <div class="col-md-4">
                                                    <div class="section-actions">
                                                        <asp:Button ID="btnFinalizarViajeDetalle" runat="server" 
                                                            Text="Finalizar Viaje" 
                                                            CssClass="btn btn-outline-danger btn-action"
                                                            OnClick="btnFinalizarViajeDetalle_Click"
                                                            OnClientClick="return confirm('¿Finalizar este viaje?');"
                                                            CausesValidation="false" />
                                                        
                                                        <asp:Button ID="btnVolverViajes" runat="server" 
                                                            Text="Volver a Viajes" 
                                                            CssClass="btn btn-secondary btn-action"
                                                            OnClick="btnVolverViajes_Click"
                                                            CausesValidation="false" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="section-content">
                                            
                                            <!-- Resumen del Viaje -->
                                            <div class="summary-cards">
                                                <div class="row">
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblTotalDespachos" runat="server" Text="0"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Total Despachos</div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblTipoViajeDetalle" runat="server"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Tipo de Viaje</div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblEstadoViajeDetalle" runat="server"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Estado</div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblUltimaActividadDetalle" runat="server"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Última Actividad</div>
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- Grid de Despachos del Viaje -->
                                            <div class="data-section">
                                                <h6 class="data-section-title">
                                                    <i class="fas fa-truck"></i> Despachos Asociados
                                                </h6>
                                                
                                                <div class="data-grid-container">
                                                    <asp:GridView ID="gvDespachosViaje" runat="server" 
                                                        CssClass="table data-table detail-table"
                                                        AutoGenerateColumns="false"
                                                        EmptyDataText="No hay despachos asociados a este viaje">
                                                        <Columns>
                                                            <asp:BoundField DataField="NumeroDespacho" HeaderText="N° Despacho" 
                                                                ItemStyle-CssClass="despacho-number" />
                                                            
                                                            <asp:BoundField DataField="FechaDespacho" HeaderText="Fecha" 
                                                                DataFormatString="{0:dd/MM/yyyy}" />
                                                            
                                                            <asp:BoundField DataField="NombreCliente" HeaderText="Cliente" />
                                                            
                                                            <asp:BoundField DataField="PlacaTracto" HeaderText="Tracto" />
                                                            
                                                            <asp:BoundField DataField="PlacaCarreta" HeaderText="Carreta" />
                                                            
                                                            <asp:BoundField DataField="TipoOperacion" HeaderText="Operación" />
                                                            
                                                            <asp:BoundField DataField="LugarOperacion" HeaderText="Planta" />
                                                            
                                                            <asp:TemplateField HeaderText="Estado">
                                                                <ItemTemplate>
                                                                    <asp:Label runat="server" 
                                                                        Text='<%# Eval("EstadoDespacho") %>'
                                                                        CssClass='<%# GetEstadoDespachoClass(Eval("EstadoDespacho").ToString()) %>'>
                                                                    </asp:Label>
                                                                </ItemTemplate>
                                                            </asp:TemplateField>
                                                            
                                                            <asp:BoundField DataField="GuiaRemitente" HeaderText="Guía Remitente" />
                                                            
                                                            <asp:BoundField DataField="GuiaTransportista" HeaderText="Guía Transportista" />
                                                        </Columns>
                                                        <EmptyDataTemplate>
                                                            <div class="empty-data">
                                                                <i class="fas fa-exclamation-circle"></i>
                                                                <p>Este viaje no tiene despachos asociados</p>
                                                            </div>
                                                        </EmptyDataTemplate>
                                                    </asp:GridView>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                                <!-- PANEL EDICIÓN DE LOTE -->
                                <asp:Panel ID="pnlEdicionLote" runat="server" Visible="false">
                                    <div class="section-card edit-section">
                                        <div class="section-header">
                                            <div class="row align-items-center">
                                                <div class="col-md-8">
                                                    <h5 class="section-title">
                                                        <i class="fas fa-edit"></i> Editar Datos Base del Lote
                                                    </h5>
                                                    <div class="detail-info">
                                                        Lote: <asp:Label ID="lblIdentificadorLote" runat="server" CssClass="detail-value"></asp:Label> | 
                                                        Despachos Afectados: <asp:Label ID="lblDespachosSAfectados" runat="server" CssClass="detail-value affected-count"></asp:Label>
                                                    </div>
                                                </div>
                                                <div class="col-md-4">
                                                    <div class="section-actions">
                                                        <asp:Button ID="btnCancelarEdicion" runat="server" 
                                                            Text="Cancelar" 
                                                            CssClass="btn btn-secondary btn-action"
                                                            OnClick="btnCancelarEdicion_Click"
                                                            CausesValidation="false" />
                                                        
                                                        <asp:Button ID="btnVolverLotes" runat="server" 
                                                            Text="Volver a Lotes" 
                                                            CssClass="btn btn-outline-secondary btn-action"
                                                            OnClick="btnVolverLotes_Click"
                                                            CausesValidation="false" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="section-content">
                                            
                                            <!-- Advertencia -->
                                            <div class="alert alert-warning edit-warning">
                                                <i class="fas fa-exclamation-triangle"></i>
                                                <strong>Importante:</strong> Los cambios afectarán a todos los despachos de este lote. 
                                                Verifique cuidadosamente antes de guardar.
                                            </div>

                                            <div class="edit-form">

                                                <!-- Fila 1: Campos básicos en 4 columnas -->
                                                <div class="row mb-3">
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label required">Fecha de Programación:</label>
                                                            <asp:TextBox ID="txtFechaProgramacionEdit" runat="server" 
                                                                CssClass="form-control" 
                                                                TextMode="Date">
                                                            </asp:TextBox>
                                                            <asp:RequiredFieldValidator ID="rfvFechaProgramacionEdit" runat="server"
                                                                ControlToValidate="txtFechaProgramacionEdit"
                                                                ErrorMessage="Debe seleccionar una fecha de programación"
                                                                CssClass="field-error"
                                                                Display="Dynamic"
                                                                ValidationGroup="EdicionLote">
                                                            </asp:RequiredFieldValidator>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label">Cliente:</label>
                                                            <asp:TextBox ID="txtClienteEdit" runat="server" 
                                                                CssClass="form-control readonly-field" 
                                                                ReadOnly="true">
                                                            </asp:TextBox>
                                                            <small class="form-text">No se puede modificar el cliente</small>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label">N° de Pedido:</label>
                                                            <asp:TextBox ID="txtNumeroPedidoEdit" runat="server" 
                                                                CssClass="form-control" 
                                                                placeholder="Ej: 1234567890"
                                                                MaxLength="10">
                                                            </asp:TextBox>
                                                            <small class="form-text">Exactamente 10 dígitos numéricos</small>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label required">Planta de Operación:</label>
                                                            <asp:DropDownList ID="ddlPlantaEdit" runat="server" CssClass="form-select">
                                                                <asp:ListItem Value="" Text="-- Seleccione planta --"></asp:ListItem>
                                                                <asp:ListItem Value="Lima" Text="Lima"></asp:ListItem>
                                                                <asp:ListItem Value="Guayaquil" Text="Guayaquil"></asp:ListItem>
                                                                <asp:ListItem Value="Trujillo" Text="Trujillo"></asp:ListItem>
                                                                <asp:ListItem Value="Quito" Text="Quito"></asp:ListItem>
                                                                <asp:ListItem Value="Chiclayo" Text="Chiclayo"></asp:ListItem>
                                                                <asp:ListItem Value="Manta" Text="Manta"></asp:ListItem>
                                                            </asp:DropDownList>
                                                            <asp:RequiredFieldValidator ID="rfvPlantaEdit" runat="server"
                                                                ControlToValidate="ddlPlantaEdit"
                                                                InitialValue=""
                                                                ErrorMessage="Debe seleccionar una planta"
                                                                CssClass="field-error"
                                                                Display="Dynamic"
                                                                ValidationGroup="EdicionLote">
                                                            </asp:RequiredFieldValidator>
                                                        </div>
                                                    </div>
                                                </div>

                                                <!-- Fila 2: Info no editable + Documentación -->
                                                <div class="row mb-3">
                                                    <div class="col-md-3">
                                                        <div class="form-group">
                                                            <label class="form-label required">Tipo de Operación:</label>
                                                            <asp:DropDownList ID="ddlTipoOperacionEdit" runat="server" CssClass="form-select"
                                                                AutoPostBack="true" OnSelectedIndexChanged="ddlTipoOperacionEdit_SelectedIndexChanged">
                                                                <asp:ListItem Value="" Text="-- Seleccione --"></asp:ListItem>
                                                                <asp:ListItem Value="CARGA" Text="Carga"></asp:ListItem>
                                                                <asp:ListItem Value="DESCARGA" Text="Descarga"></asp:ListItem>
                                                            </asp:DropDownList>
                                                        </div>
                                                        <div class="form-group mt-2">
                                                            <label class="form-label required">Ámbito:</label>
                                                            <asp:RadioButtonList ID="rblAmbitoEdit" runat="server" CssClass="rbl-inline"
                                                                AutoPostBack="true" OnSelectedIndexChanged="rblAmbitoEdit_SelectedIndexChanged"
                                                                RepeatDirection="Horizontal" RepeatLayout="Flow">
                                                                <asp:ListItem Value="0" Text="Nacional &nbsp;"></asp:ListItem>
                                                                <asp:ListItem Value="1" Text="Internacional"></asp:ListItem>
                                                            </asp:RadioButtonList>
                                                        </div>
                                                        <small class="form-text text-warning mt-1">
                                                            <i class="fas fa-exclamation-triangle"></i> Al cambiar estos valores los paneles de documentos se actualizan automáticamente.
                                                        </small>
                                                    </div>
                                                    <div class="col-md-4">
                                                        <!-- Panel Factura -->
                                                        <asp:Panel ID="pnlFacturaEdit" runat="server" CssClass="doc-panel h-100" Visible="false">
                                                            <div class="doc-panel-header">
                                                                <i class="fas fa-receipt"></i> Datos de Factura
                                                            </div>
                                                            <div class="doc-panel-content">
                                                                <div class="form-group">
                                                                    <label class="form-label">N° Factura:</label>
                                                                    <asp:TextBox ID="txtNumeroFacturaEdit" runat="server" 
                                                                        CssClass="form-control" 
                                                                        placeholder="Ej: F222 - 00004267">
                                                                    </asp:TextBox>
                                                                </div>
                                                                <div class="form-group">
                                                                    <label class="form-label">Fecha Emisión:</label>
                                                                    <asp:TextBox ID="txtFechaEmisionFacturaEdit" runat="server" 
                                                                        CssClass="form-control" 
                                                                        TextMode="Date">
                                                                    </asp:TextBox>
                                                                </div>
                                                                <div class="form-group">
                                                                    <label class="form-label">Valor Total:</label>
                                                                    <asp:TextBox ID="txtValorTotalFacturaEdit" runat="server" 
                                                                        CssClass="form-control" 
                                                                        placeholder="0.00"
                                                                        TextMode="Number"
                                                                        step="0.01">
                                                                    </asp:TextBox>
                                                                </div>
                                                            </div>
                                                        </asp:Panel>
                                                    </div>
                                                    <div class="col-md-5">
                                                        <!-- Panel CPIC -->
                                                        <asp:Panel ID="pnlCPICEdit" runat="server" CssClass="doc-panel h-100" Visible="false">
                                                            <div class="doc-panel-header">
                                                                <i class="fas fa-shipping-fast"></i> Datos de CPIC
                                                            </div>
                                                            <div class="doc-panel-content">
                                                                <div class="form-group">
                                                                    <label class="form-label">N° CPIC:</label>
                                                                    <asp:TextBox ID="txtNumeroCPICEdit" runat="server" 
                                                                        CssClass="form-control" 
                                                                        placeholder="Ej: 1234567"
                                                                        MaxLength="7">
                                                                    </asp:TextBox>
                                                                </div>
                                                                <div class="form-group">
                                                                    <label class="form-label">Fecha Emisión CPIC:</label>
                                                                    <asp:TextBox ID="txtFechaEmisionCPICEdit" runat="server" 
                                                                        CssClass="form-control" 
                                                                        TextMode="Date">
                                                                    </asp:TextBox>
                                                                </div>
                                                                <div class="form-group">
                                                                    <label class="form-label">Valor Flete:</label>
                                                                    <asp:TextBox ID="txtValorFleteEdit" runat="server" 
                                                                        CssClass="form-control" 
                                                                        placeholder="0.00"
                                                                        TextMode="Number"
                                                                        step="0.01">
                                                                    </asp:TextBox>
                                                                </div>
                                                            </div>
                                                        </asp:Panel>
                                                    </div>
                                                </div>

                                                <!-- Fila 3: Conductores (ancho completo) -->
                                                <div class="row mb-3">
                                                    <div class="col-12">
                                                        <div class="form-section">
                                                            <h6 class="form-section-title">
                                                                <i class="fas fa-users"></i> Conductores por Despacho
                                                            </h6>
                                                            <p class="form-text mb-2">Puede cambiar el conductor de cada despacho individualmente. Los cambios se aplican al guardar.</p>
                                                            <div class="conductores-edit-container">
                                                                <div class="tabla-scroll-wrapper">
                                                                    <asp:GridView ID="gvConductoresLote" runat="server" 
                                                                        CssClass="table table-hover"
                                                                        AutoGenerateColumns="false"
                                                                        DataKeyNames="IdDespacho"
                                                                        OnRowDataBound="gvConductoresLote_RowDataBound"
                                                                        GridLines="None">
                                                                        <Columns>
                                                                            <asp:BoundField DataField="IdDespacho" HeaderText="ID" Visible="false" />

                                                                            <asp:BoundField DataField="NumeroDespacho" HeaderText="N° Despacho" 
                                                                                ItemStyle-CssClass="despacho-number fw-bold" />

                                                                            <asp:BoundField DataField="FechaDespacho" HeaderText="Fecha" 
                                                                                DataFormatString="{0:dd/MM/yyyy}" />

                                                                            <asp:TemplateField HeaderText="Conductor Actual">
                                                                                <ItemTemplate>
                                                                                    <span class="conductor-badge-actual">
                                                                                        <%# Eval("NombreConductorActual") %>
                                                                                    </span>
                                                                                </ItemTemplate>
                                                                            </asp:TemplateField>

                                                                            <asp:TemplateField HeaderText="Cambiar Conductor">
                                                                                <ItemTemplate>
                                                                                    <asp:DropDownList ID="ddlConductorDespacho" runat="server" 
                                                                                        CssClass="form-select conductor-select">
                                                                                    </asp:DropDownList>
                                                                                </ItemTemplate>
                                                                            </asp:TemplateField>
                                                                        </Columns>
                                                                    </asp:GridView>
                                                                </div>
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>

                                                <!-- Botones de Acción -->
                                                <div class="form-actions">
                                                    <div class="row">
                                                        <div class="col-md-6 text-start">
                                                            <asp:Button ID="btnEliminarLote" runat="server" 
                                                                Text="Eliminar Lote" 
                                                                CssClass="btn btn-outline-danger"
                                                                OnClick="btnEliminarLote_Click"
                                                                CausesValidation="false"
                                                                OnClientClick="return confirm('⚠️ ADVERTENCIA: Se eliminarán TODOS los despachos de este lote.\n\n¿Está COMPLETAMENTE seguro de eliminar este lote?');" />
                                                            <asp:Button ID="btnAnularLote" runat="server"
                                                                Text="Anular Lote"
                                                                CssClass="btn btn-warning ml-2"
                                                                OnClick="btnAnularLote_Click"
                                                                CausesValidation="false"
                                                                OnClientClick="return confirm('⚠️ ¿Anular este lote?\n\nTodos sus despachos y viajes activos quedarán como ANULADO.\nLos registros se conservan para auditoría.');" />
                                                        </div>
                                                        <div class="col-md-6 text-end">
                                                            <asp:Button ID="btnGuardarCambios" runat="server" 
                                                                Text="Guardar Cambios" 
                                                                CssClass="btn btn-primary"
                                                                OnClick="btnGuardarCambios_Click"
                                                                ValidationGroup="EdicionLote"
                                                                OnClientClick="return confirm('¿Está seguro de aplicar estos cambios a todo el lote?');" />
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                                <!-- PANEL DETALLES DE LOTE -->
                                <asp:Panel ID="pnlDetallesLote" runat="server" Visible="false">
                                    <div class="section-card detalle-section">
                                        <div class="section-header">
                                            <div class="row align-items-center">
                                                <div class="col-md-8">
                                                    <h5 class="section-title">
                                                        <i class="fas fa-eye"></i> Detalles del Lote
                                                    </h5>
                                                    <div class="detail-info">
                                                        Cliente: <asp:Label ID="lblClienteDetalleLote" runat="server" CssClass="detail-value"></asp:Label> | 
                                                        Pedido: <asp:Label ID="lblPedidoDetalleLote" runat="server" CssClass="detail-value"></asp:Label>
                                                    </div>
                                                </div>
                                                <div class="col-md-4">
                                                    <div class="section-actions">
                                                        <asp:Button ID="btnGestionarManifiestos" runat="server"
                                                            Text="Documentos"
                                                            CssClass="btn btn-outline-primary btn-action"
                                                            OnClick="btnGestionarManifiestos_Click"
                                                            CausesValidation="false" />

                                                        <asp:Button ID="btnEditarDesdeDetal" runat="server"
                                                            Text="Editar Lote"
                                                            CssClass="btn btn-outline-secondary btn-action"
                                                            OnClick="btnEditarDesdeDetal_Click"
                                                            CausesValidation="false" />

                                                        <asp:Button ID="btnVolverLotesDetalle" runat="server"
                                                            Text="Volver a Lotes"
                                                            CssClass="btn btn-secondary btn-action"
                                                            OnClick="btnVolverLotesDetalle_Click"
                                                            CausesValidation="false" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="section-content">
                                            
                                            <!-- Información General del Lote -->
                                            <div class="summary-cards">
                                                <div class="row">
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblTotalDespachosLote" runat="server" Text="0"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Total Despachos</div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblOperacionDetalleLote" runat="server"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Operación</div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblPlantaDetalleLote" runat="server"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Planta</div>
                                                        </div>
                                                    </div>
                                                    <div class="col-md-3">
                                                        <div class="summary-card">
                                                            <div class="summary-value">
                                                                <asp:Label ID="lblFechaCreacionDetalle" runat="server"></asp:Label>
                                                            </div>
                                                            <div class="summary-label">Fecha Creación</div>
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- Grid de Despachos del Lote -->
                                            <div class="data-section">
                                                <h6 class="data-section-title">
                                                    <i class="fas fa-truck"></i> Despachos del Lote
                                                </h6>
                                                
                                                <div class="data-grid-container">
                                                    <asp:GridView ID="gvDespachosLote" runat="server" 
                                                        CssClass="table data-table detail-table"
                                                        AutoGenerateColumns="false"
                                                        EmptyDataText="No hay despachos asociados a este lote">
                                                        <Columns>
                                                            <asp:BoundField DataField="NumeroDespacho" HeaderText="N° Despacho" 
                                                                ItemStyle-CssClass="despacho-number" />
                                                            
                                                            <asp:BoundField DataField="FechaDespacho" HeaderText="Fecha" 
                                                                DataFormatString="{0:dd/MM/yyyy}" />
                                                            
                                                            <asp:BoundField DataField="NombreConductor" HeaderText="Conductor" />
                                                            
                                                            <asp:BoundField DataField="PlacaTracto" HeaderText="Tracto" />
                                                            
                                                            <asp:BoundField DataField="PlacaCarreta" HeaderText="Carreta" />
                                                            
                                                            <asp:TemplateField HeaderText="Estado">
                                                                <ItemTemplate>
                                                                    <asp:Label runat="server" 
                                                                        Text='<%# Eval("EstadoDespacho") %>'
                                                                        CssClass='<%# GetEstadoDespachoClass(Eval("EstadoDespacho").ToString()) %>'>
                                                                    </asp:Label>
                                                                </ItemTemplate>
                                                            </asp:TemplateField>
                                                            
                                                            <asp:BoundField DataField="GuiaRemitente" HeaderText="Guía Remitente" />
                                                            
                                                            <asp:BoundField DataField="GuiaTransportista" HeaderText="Guía Transportista" />

                                                            <asp:BoundField DataField="NumeroViaje" HeaderText="N° Viaje" />
                                                        </Columns>
                                                        <EmptyDataTemplate>
                                                            <div class="empty-data">
                                                                <i class="fas fa-exclamation-circle"></i>
                                                                <p>Este lote no tiene despachos asociados</p>
                                                            </div>
                                                        </EmptyDataTemplate>
                                                    </asp:GridView>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                                <!-- ====== MANIFIESTOS DEL LOTE (viajes internacionales) ====== -->
                                <asp:Panel ID="pnlManifiestosLote" runat="server" Visible="false">
                                    <div class="section-card detalle-section">
                                        <div class="section-header">
                                            <div class="row align-items-center">
                                                <div class="col-md-8">
                                                    <h5 class="section-title">
                                                        <i class="fas fa-file-alt"></i> Documentos del Lote
                                                    </h5>
                                                    <div class="detail-info">
                                                        Cliente: <asp:Label ID="lblClienteManifiestos" runat="server" CssClass="detail-value"></asp:Label> |
                                                        Pedido: <asp:Label ID="lblPedidoManifiestos" runat="server" CssClass="detail-value"></asp:Label>
                                                    </div>
                                                </div>
                                                <div class="col-md-4">
                                                    <div class="section-actions">
                                                        <asp:Button ID="btnVolverManifiestos" runat="server"
                                                            Text="Volver a Detalles"
                                                            CssClass="btn btn-secondary btn-action"
                                                            OnClick="btnVolverManifiestos_Click"
                                                            CausesValidation="false" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="section-content">

                                            <asp:Panel ID="pnlMensajeManifiesto" runat="server" Visible="false" CssClass="mb-3">
                                                <asp:Label ID="lblMensajeManifiesto" runat="server" CssClass="alert d-block"></asp:Label>
                                            </asp:Panel>

                                            <!-- Documentos base del lote (Factura/CPIC), si fueron adjuntados -->
                                            <div class="data-section mb-3">
                                                <h6 class="data-section-title"><i class="fas fa-file-invoice"></i> Documentos Base</h6>
                                                <div class="rd-notice" style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:5px;padding:.7rem 1rem;">
                                                    <asp:Panel ID="pnlDocFacturaManifiesto" runat="server" Visible="false" CssClass="mb-1">
                                                        <i class="fas fa-receipt mr-1"></i> Factura:
                                                        <asp:LinkButton ID="lnkVerDocFactura" runat="server" OnClick="lnkVerDocFactura_Click" CausesValidation="false"></asp:LinkButton>
                                                    </asp:Panel>
                                                    <asp:Panel ID="pnlDocCpicManifiesto" runat="server" Visible="false">
                                                        <i class="fas fa-shipping-fast mr-1"></i> CPIC:
                                                        <asp:LinkButton ID="lnkVerDocCpic" runat="server" OnClick="lnkVerDocCpic_Click" CausesValidation="false"></asp:LinkButton>
                                                    </asp:Panel>
                                                    <asp:Label ID="lblSinDocsBase" runat="server" Text="Sin documentos base adjuntados." Visible="false" CssClass="text-muted"></asp:Label>
                                                </div>
                                            </div>

                                            <!-- Manifiesto por conductor/despacho: solo aplica a viajes internacionales -->
                                            <asp:Panel ID="pnlAvisoNacionalSinManifiesto" runat="server" Visible="false" CssClass="rd-notice" Style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:5px;padding:.7rem 1rem;">
                                                <i class="fas fa-info-circle mr-1"></i> Este lote es nacional: el manifiesto de aduana no aplica.
                                            </asp:Panel>

                                            <asp:Panel ID="pnlSeccionManifiestoConductor" runat="server">
                                            <div class="data-section">
                                                <h6 class="data-section-title"><i class="fas fa-users"></i> Manifiesto por Conductor</h6>
                                                <p class="text-muted small">Adjunte o reemplace el manifiesto de cruce/retorno de cada conductor a medida que avanza el viaje.</p>

                                                <div class="data-grid-container">
                                                    <asp:GridView ID="gvManifiestosLote" runat="server"
                                                        CssClass="table data-table detail-table"
                                                        AutoGenerateColumns="false"
                                                        DataKeyNames="IdDespacho"
                                                        OnRowCommand="gvManifiestosLote_RowCommand"
                                                        EmptyDataText="No hay conductores en este lote">
                                                        <Columns>
                                                            <asp:BoundField DataField="NumeroDespacho" HeaderText="N° Despacho" />
                                                            <asp:BoundField DataField="NombreConductor" HeaderText="Conductor" />

                                                            <asp:TemplateField HeaderText="Manifiesto de Cruce">
                                                                <ItemTemplate>
                                                                    <asp:LinkButton runat="server" CommandName="VerManifiesto" CommandArgument='<%# Eval("CruceIdDocumento") %>'
                                                                        Visible='<%# Eval("CruceIdDocumento") != null %>' CausesValidation="false">
                                                                        <i class="fas fa-eye"></i> <%# Eval("CruceNombreOriginal") %>
                                                                    </asp:LinkButton>
                                                                    <span class="text-muted small" style='<%# Eval("CruceIdDocumento") != null ? "display:none" : "" %>'>Sin adjuntar</span>
                                                                    <br />
                                                                    <asp:FileUpload runat="server" ID="fileCruceFila" CssClass="form-control form-control-sm mt-1" accept=".pdf,.jpg,.jpeg,.png" />
                                                                </ItemTemplate>
                                                            </asp:TemplateField>

                                                            <asp:TemplateField HeaderText="Manifiesto de Retorno">
                                                                <ItemTemplate>
                                                                    <asp:LinkButton runat="server" CommandName="VerManifiesto" CommandArgument='<%# Eval("RetornoIdDocumento") %>'
                                                                        Visible='<%# Eval("RetornoIdDocumento") != null %>' CausesValidation="false">
                                                                        <i class="fas fa-eye"></i> <%# Eval("RetornoNombreOriginal") %>
                                                                    </asp:LinkButton>
                                                                    <span class="text-muted small" style='<%# Eval("RetornoIdDocumento") != null ? "display:none" : "" %>'>Sin adjuntar</span>
                                                                    <br />
                                                                    <asp:FileUpload runat="server" ID="fileRetornoFila" CssClass="form-control form-control-sm mt-1" accept=".pdf,.jpg,.jpeg,.png" />
                                                                </ItemTemplate>
                                                            </asp:TemplateField>

                                                            <asp:TemplateField HeaderText="Acción">
                                                                <ItemTemplate>
                                                                    <asp:Button runat="server" Text="Guardar" CssClass="btn btn-primary btn-sm"
                                                                        CommandName="GuardarManifiesto" CommandArgument='<%# Eval("IdDespacho") %>' />
                                                                </ItemTemplate>
                                                            </asp:TemplateField>
                                                        </Columns>
                                                        <EmptyDataTemplate>
                                                            <div class="empty-data">
                                                                <i class="fas fa-exclamation-circle"></i>
                                                                <p>Este lote no tiene despachos asociados</p>
                                                            </div>
                                                        </EmptyDataTemplate>
                                                    </asp:GridView>
                                                </div>
                                            </div>
                                            </asp:Panel>
                                        </div>
                                    </div>
                                </asp:Panel>

                            </ContentTemplate>
                            <Triggers>
                                <asp:AsyncPostBackTrigger ControlID="lnkStatViajes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="lnkStatLotes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="lnkStatManifiestosPendientes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnMostrarViajes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnRefrescarViajes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="ddlFiltroConductorViajes" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="ddlFiltroTipoViajes" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="btnBuscarViaje" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="gvViajesActivos" EventName="RowCommand" />
                                <asp:AsyncPostBackTrigger ControlID="btnVolverViajes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnFinalizarViajeDetalle" EventName="Click" />
                                
                                <asp:AsyncPostBackTrigger ControlID="btnMostrarLotes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnRefrescarLotes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="ddlFiltroClienteLotes" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="ddlFiltroOperacionLotes" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="ddlFiltroPlantaLotes" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="btnBuscarLote" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnBuscarDocumento" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnFiltrarFecha" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnLimpiarFiltros" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="gvLotesRegistrados" EventName="RowCommand" />
                                
                                <asp:AsyncPostBackTrigger ControlID="btnCancelarEdicion" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnVolverLotes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnGuardarCambios" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnEliminarLote" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnAnularLote" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="ddlFiltroEstadoLotes" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="ddlTipoOperacionEdit" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="rblAmbitoEdit" EventName="SelectedIndexChanged" />

                                <asp:AsyncPostBackTrigger ControlID="btnEditarDesdeDetal" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnVolverLotesDetalle" EventName="Click" />

                                <asp:AsyncPostBackTrigger ControlID="btnGestionarManifiestos" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnVolverManifiestos" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="lnkVerDocFactura" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="lnkVerDocCpic" EventName="Click" />
                                <asp:PostBackTrigger ControlID="gvManifiestosLote" />

                                <asp:PostBackTrigger ControlID="btnVolver" />
                            </Triggers>
                        </asp:UpdatePanel>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- Loading Panel -->
    <asp:UpdateProgress ID="UpdateProgress1" runat="server" AssociatedUpdatePanelID="UpdatePanelMain">
        <ProgressTemplate>
            <div class="loading-overlay">
                <div class="loading-content">
                    <div class="spinner-border text-primary" role="status">
                        <span class="visually-hidden">Cargando...</span>
                    </div>
                    <div class="mt-2">Procesando...</div>
                </div>
            </div>
        </ProgressTemplate>
    </asp:UpdateProgress>

</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="ScriptsSection" runat="server">
    <!-- Select2 CSS -->
    <link href="https://cdn.jsdelivr.net/npm/select2@4.1.0-rc.0/dist/css/select2.min.css" rel="stylesheet" />
    <!-- Select2 JS -->
    <script src="https://cdn.jsdelivr.net/npm/select2@4.1.0-rc.0/dist/js/select2.min.js"></script>
    
    <script type="text/javascript">
        // Inicializar Select2 en los dropdowns de conductores
        function inicializarSelect2Conductores() {
            $('.conductor-select').select2({
                width: '100%',
                placeholder: 'Buscar conductor...',
                allowClear: false,
                language: {
                    noResults: function() {
                        return "No se encontraron conductores";
                    },
                    searching: function() {
                        return "Buscando...";
                    }
                }
            });
        }

        $(document).ready(function () {
            inicializarSelect2Conductores();
            autoHideMessages();
            establecerFechasPorDefecto();
        });

        var prm = Sys.WebForms.PageRequestManager.getInstance();
        prm.add_endRequest(function () {
            inicializarSelect2Conductores();
            autoHideMessages();
        });

        function autoHideMessages() {
            setTimeout(function () {
                $('.alert').fadeOut('slow');
            }, 5000);
        }

        function establecerFechasPorDefecto() {
            var hoy = new Date();
            var primerDiaMes = new Date(hoy.getFullYear(), hoy.getMonth(), 1);

            var fechaDesde = document.getElementById('<%= txtFechaDesde.ClientID %>');
            var fechaHasta = document.getElementById('<%= txtFechaHasta.ClientID %>');

            if (fechaDesde && fechaDesde.value === '') {
                fechaDesde.value = primerDiaMes.toISOString().substr(0, 10);
            }

            if (fechaHasta && fechaHasta.value === '') {
                fechaHasta.value = hoy.toISOString().substr(0, 10);
            }
        }

        function confirmarAccion(mensaje) {
            return confirm(mensaje);
        }

        function validarNumeroPedido(input) {
            var valor = input.value.replace(/[^0-9]/g, '');
            input.value = valor;

            if (valor.length > 0 && valor.length !== 10) {
                input.style.borderColor = '#dc3545';
                return false;
            } else {
                input.style.borderColor = '#ced4da';
                return true;
            }
        }

        $(document).on('input', '#<%= txtNumeroPedidoEdit.ClientID %>', function () {
            validarNumeroPedido(this);
        });
    </script>
    
    <style>
        /* === RESET Y BASE === */
        * {
            box-sizing: border-box;
        }

        /* === LAYOUT PRINCIPAL === */
        .main-card {
            border: none;
            box-shadow: 0 2px 8px rgba(15,23,42,0.08);
            border-radius: 8px;
            margin-bottom: 2rem;
        }

        .container-fluid {
            padding: 1rem;
        }

        /* === NAVEGACIÓN (legado, ya no se usa: ver .ld-toolbar-nav arriba) === */
        .navigation-card {
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 8px;
            padding: 1rem;
        }

        .nav-header {
            margin: 0;
        }

        .nav-title {
            color: #64748b;
            font-weight: 600;
            margin: 0;
        }

        .nav-buttons {
            text-align: right;
        }

        .btn-nav {
            font-size: 0.85rem;
        }

        /* === SECCIONES === */
        .section-card {
            background: #fff;
            border: 1px solid #e2e8f0;
            border-radius: 8px;
            box-shadow: 0 1px 3px rgba(15,23,42,.04);
            margin-bottom: 1.5rem;
            overflow: hidden;
        }

        .section-header {
            background: #f8fafc;
            border-bottom: 1px solid #e2e8f0;
            padding: 1rem 1.25rem;
        }

        .section-title {
            color: #1e293b;
            font-weight: 600;
            margin: 0;
            font-size: .95rem;
        }

        .section-content {
            padding: 1.5rem;
        }

        .section-actions {
            text-align: right;
        }

        .btn-action {
            margin-left: 0.5rem;
            font-weight: 600;
        }

        /* === FILTROS === */
        .filters-container {
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 8px;
            padding: 1.1rem 1.25rem .35rem;
            margin-bottom: 1.5rem;
        }

        .form-group {
            margin-bottom: .9rem;
        }

        .form-label {
            font-weight: 600;
            color: #475569;
            margin-bottom: .4rem;
            display: block;
            font-size: .74rem;
            text-transform: uppercase;
            letter-spacing: .04em;
        }

        .form-control, .form-select {
            border: 1px solid #cbd5e1;
            border-radius: 6px;
            padding: 0.5rem 0.75rem;
            font-size: 0.85rem;
        }

        .form-control:focus, .form-select:focus {
            border-color: #1e40af;
            box-shadow: 0 0 0 3px rgba(30,64,175,.12);
        }

        .input-group .btn {
            border-color: #cbd5e1;
        }

        .filter-buttons {
            display: flex;
            gap: 0.5rem;
        }

        /* Fila de fecha/estado/acciones: alinea el pie de los inputs con los botones,
           sin usar un <label>&nbsp;</label> como espaciador. */
        .filters-actions-row {
            display: flex;
            align-items: flex-end;
            height: 100%;
            padding-bottom: .9rem;
        }

        /* === TABLAS === */
        .data-grid-container {
            background: white;
            border: 1px solid #e2e8f0;
            border-radius: 8px;
            overflow-x: auto;
        }

        .data-table {
            width: 100%;
            margin: 0;
            border-collapse: separate;
            border-spacing: 0;
        }

        .data-table thead th {
            background: #f1f5f9;
            color: #334155;
            font-weight: 700;
            font-size: .72rem;
            text-transform: uppercase;
            letter-spacing: .04em;
            padding: .8rem .9rem;
            border-bottom: 2px solid #e2e8f0;
            text-align: left;
            white-space: nowrap;
        }

        .data-table tbody td {
            padding: .7rem .9rem;
            border-bottom: 1px solid #f1f5f9;
            font-size: .85rem;
            color: #334155;
            vertical-align: middle;
        }

        .data-table tbody tr:nth-child(even) {
            background: #f8fafc;
        }

        .data-table tbody tr:hover {
            background: #eff6ff;
        }

        .data-table tbody tr:last-child td {
            border-bottom: none;
        }

        .detail-table thead th {
            background: #f1f5f9;
        }

        /* === BADGES === */
        .badge {
            font-size: .72rem;
            padding: .4em .75em;
            border-radius: 999px;
            font-weight: 600;
        }

        .tipo-internacional {
            background-color: #e0e7ff;
            color: #3730a3;
        }

        .tipo-nacional {
            background-color: #dcfce7;
            color: #166534;
        }

        .estado-abierto {
            background-color: #dcfce7;
            color: #166534;
        }

        .estado-programado {
            background-color: #dbeafe;
            color: #1e40af;
        }

        .estado-enprogreso {
            background-color: #fef3c7;
            color: #92400e;
        }

        .estado-completado {
            background-color: #dcfce7;
            color: #166534;
        }

        .estado-cancelado {
            background-color: #fee2e2;
            color: #991b1b;
        }

        /* === CAMPOS ESPECIALES === */
        .viaje-number, .pedido-number, .despacho-number {
            font-weight: 700;
            color: #1e40af;
        }

        .despachos-count {
            font-weight: 700;
            color: #16a34a;
        }

        .affected-count {
            color: #dc2626;
            font-weight: 700;
        }

        /* === BOTONES DE ACCIÓN === */
        .action-buttons {
            display: flex;
            gap: 0.4rem;
            flex-wrap: wrap;
        }

        /* === EMPTY DATA === */
        .empty-data {
            text-align: center;
            padding: 2.5rem 1rem;
            color: #94a3b8;
        }

        .empty-data i {
            font-size: 1.75rem;
            margin-bottom: 0.5rem;
            display: block;
            color: #cbd5e1;
        }

        /* === CARDS DE RESUMEN === */
        .summary-cards {
            margin-bottom: 1.5rem;
        }

        .summary-card {
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 8px;
            padding: 1.1rem .75rem;
            text-align: center;
            margin-bottom: 1rem;
        }

        .summary-value {
            font-size: 1.4rem;
            font-weight: 700;
            color: #1e293b;
            margin-bottom: 0.25rem;
        }

        .summary-label {
            font-size: .72rem;
            text-transform: uppercase;
            letter-spacing: .04em;
            color: #64748b;
            font-weight: 600;
        }

        /* === DETALLES === */
        .detail-info {
            font-size: 0.85rem;
            color: #64748b;
            margin-top: 0.35rem;
        }

        .detail-identifier, .detail-value {
            font-weight: 600;
            color: #1e293b;
        }

        .data-section {
            margin-top: 1.75rem;
        }

        .data-section-title {
            color: #1e293b;
            font-weight: 600;
            margin-bottom: 1rem;
            font-size: .9rem;
            display: flex;
            align-items: center;
            gap: .45rem;
        }

        .data-section-title i {
            color: #64748b;
        }

        /* === FORMULARIOS DE EDICIÓN === */
        .edit-form {
            margin-top: 1rem;
        }

        .form-section {
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 8px;
            padding: 1.1rem 1.25rem;
            margin-bottom: 1rem;
        }

        .form-section-title {
            color: #1e293b;
            font-weight: 600;
            margin-bottom: 1rem;
            font-size: .9rem;
            border-bottom: 1px solid #e2e8f0;
            padding-bottom: 0.6rem;
            display: flex;
            align-items: center;
            gap: .45rem;
        }

        .required::after {
            content: " *";
            color: #dc2626;
        }

        .readonly-field {
            background-color: #f1f5f9 !important;
            cursor: not-allowed;
        }

        .form-text {
            font-size: 0.75rem;
            color: #64748b;
            margin-top: 0.25rem;
        }

        .field-error {
            color: #dc2626;
            font-size: 0.75rem;
            margin-top: 0.25rem;
            display: block;
        }

        /* === GRID DE CONDUCTORES EDITABLE === */
        .conductores-edit-container {
            margin: 1rem 0;
            border: 1px solid #e2e8f0;
            border-radius: 8px;
            padding: 0;
            background: #f8fafc;
        }

        .conductores-edit-container .table {
            margin-bottom: 0;
            font-size: 0.9rem;
        }

        .conductores-edit-container thead {
            background: #f1f5f9;
        }

        .conductores-edit-container thead th {
            background: #f1f5f9;
            color: #1e293b;
            font-weight: 600;
            padding: 0.75rem;
            border-bottom: 2px solid #e2e8f0;
        }

        .conductores-edit-container tbody td {
            padding: 0.75rem;
            vertical-align: middle;
            background: white;
        }

        .conductores-edit-container .form-select {
            font-size: 0.875rem;
            padding: 0.375rem 0.75rem;
        }

        .conductor-badge-actual {
            display: inline-block;
            padding: 0.4rem 0.9rem;
            background: #475569;
            color: white;
            border-radius: 999px;
            font-weight: 600;
            font-size: 0.78rem;
        }

        .tabla-scroll-wrapper {
            overflow-x: auto;
            border-radius: 6px;
        }

        .tabla-scroll-wrapper::-webkit-scrollbar {
            width: 8px;
            height: 8px;
        }

        .tabla-scroll-wrapper::-webkit-scrollbar-track {
            background: #f1f5f9;
            border-radius: 4px;
        }

        .tabla-scroll-wrapper::-webkit-scrollbar-thumb {
            background: #cbd5e1;
            border-radius: 4px;
        }

        .tabla-scroll-wrapper::-webkit-scrollbar-thumb:hover {
            background: #94a3b8;
        }

        /* === SELECT2 PERSONALIZADO === */
        .select2-container--default .select2-selection--single {
            min-height: 38px !important;
            border: 1px solid #cbd5e1 !important;
            border-radius: 6px !important;
        }

        .select2-container--default .select2-selection--single .select2-selection__rendered {
            padding: 0.375rem 0.75rem !important;
            line-height: 1.5 !important;
        }

        .select2-container--default .select2-dropdown {
            border: 1px solid #cbd5e1 !important;
            border-radius: 6px !important;
        }

        .select2-container--default .select2-results__option--highlighted[aria-selected] {
            background-color: #1e40af !important;
            color: white !important;
        }

        .select2-search--dropdown .select2-search__field {
            border: 1px solid #cbd5e1 !important;
            border-radius: 6px !important;
            padding: 0.5rem !important;
        }

        /* === PANELES DE DOCUMENTOS === */
        .doc-panel {
            border: 1px solid #e2e8f0;
            border-radius: 8px;
            overflow: hidden;
            margin-bottom: 1rem;
        }

        .doc-panel-header {
            background: #f1f5f9;
            padding: 0.6rem 0.9rem;
            border-bottom: 1px solid #e2e8f0;
            font-weight: 600;
            font-size: 0.85rem;
            color: #1e293b;
        }

        .doc-panel-content {
            padding: 0.9rem;
        }

        .info-panel {
            background: #eff6ff;
            border: 1px solid #bfdbfe;
            border-radius: 8px;
            padding: 0.9rem;
        }

        .info-title {
            font-weight: 600;
            font-size: 0.85rem;
            color: #1e3a8a;
            margin-bottom: 0.5rem;
        }

        .info-content {
            font-size: 0.85rem;
            color: #1e3a8a;
        }

        .info-value {
            font-weight: 600;
        }

        /* === ALERTAS === */
        .alert {
            border-radius: 8px;
            padding: 0.85rem 1.1rem;
            margin-bottom: 1rem;
        }

        .edit-warning {
            background-color: #fffbeb;
            border: 1px solid #fde68a;
            color: #92400e;
        }

        /* === ACCIONES DE FORMULARIO === */
        .form-actions {
            margin-top: 1.75rem;
            padding-top: 1.25rem;
            border-top: 1px solid #e2e8f0;
        }

        .btn-large {
            padding: 0.6rem 1.5rem;
            font-size: 1rem;
        }

        /* === LOADING === */
        .loading-overlay {
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background-color: rgba(15,23,42,0.5);
            z-index: 9999;
            display: flex;
            justify-content: center;
            align-items: center;
        }

        .loading-content {
            background-color: white;
            padding: 1.5rem;
            border-radius: 8px;
            text-align: center;
            box-shadow: 0 8px 24px rgba(15,23,42,0.2);
        }

        /* === RESPONSIVE === */
        @media (max-width: 768px) {
            .ld-toolbar {
                flex-direction: column;
                align-items: stretch;
            }

            .ld-toolbar-nav {
                justify-content: center;
            }

            .section-actions {
                text-align: left;
                justify-content: flex-start;
                margin-top: 0.5rem;
            }

            .btn-action {
                margin-left: 0;
                margin-right: 0.5rem;
                margin-bottom: 0.5rem;
                display: inline-block;
            }

            .action-buttons {
                flex-direction: column;
            }

            .filter-buttons {
                flex-direction: column;
            }

            .filters-actions-row {
                padding-bottom: 0;
            }

            .data-grid-container {
                overflow-x: auto;
                -webkit-overflow-scrolling: touch;
            }

            .summary-card {
                margin-bottom: 0.5rem;
            }

            .form-section {
                margin-bottom: 0.5rem;
            }

            .conductores-edit-container .form-select {
                min-width: 200px;
            }
        }

        @media (max-width: 576px) {
            .container-fluid {
                padding: 0.5rem;
            }

            .section-content {
                padding: 0.9rem;
            }

            .filters-container {
                padding: 0.85rem;
            }

            .data-table thead th,
            .data-table tbody td {
                padding: 0.55rem;
            }
        }

        /* === UTILITIES === */
        .text-center { text-align: center; }
        .text-start { text-align: left; }
        .text-end { text-align: right; }
        .mb-0 { margin-bottom: 0; }
        .mb-1 { margin-bottom: 0.25rem; }
        .mb-2 { margin-bottom: 0.5rem; }
        .mb-3 { margin-bottom: 1rem; }
        .mt-2 { margin-top: 0.5rem; }
        .ms-2 { margin-left: 0.5rem; }
        .ms-3 { margin-left: 1rem; }
        .fw-bold { font-weight: 600; }
    </style>
</asp:Content>
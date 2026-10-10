<%@ Page Title="Panel Admin Sistema" Language="C#" MasterPageFile="~/Site.Master"
    AutoEventWireup="true" CodeBehind="DashboardAdminSistema.aspx.cs"
    Inherits="WebSGV.Views.DashboardAdminSistema" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <style>
        .stat-card { border-left: 3px solid; border-radius: 6px; }
        .stat-card.blue  { border-left-color: #0275d8; }
        .stat-card.green { border-left-color: #28a745; }
        .stat-card.red   { border-left-color: #dc3545; }
        .stat-card.cyan  { border-left-color: #17a2b8; }
        .admin-titulo { font-size: 1.375rem; font-weight: 600; }
        .admin-kpi-etiqueta { font-size: .8125rem; color: #6b7280; }
        .admin-kpi-valor { font-size: 1.625rem; font-weight: 600; color: #1f2937; font-variant-numeric: tabular-nums; }
        .quick-link { transition: background .15s; }
        .quick-link:hover { background: #f0f4ff !important; }
    </style>

    <!-- Encabezado -->
    <div class="d-flex justify-content-between align-items-center mb-4">
        <div>
            <h2 class="admin-titulo mb-0">Panel de administración del sistema</h2>
            <small class="text-muted"><asp:Label ID="lblFechaHora" runat="server" /></small>
        </div>
        <a href="GestionUsuarios.aspx" class="btn btn-primary">
            <i class="fas fa-users-cog mr-1"></i>Gestión de usuarios
        </a>
    </div>

    <!-- Tarjetas de estadísticas -->
    <div class="row mb-4">
        <div class="col-md-3 mb-3">
            <div class="card shadow-sm stat-card blue h-100">
                <div class="card-body py-3">
                    <div class="admin-kpi-etiqueta">Usuarios totales</div>
                    <div class="admin-kpi-valor"><asp:Label ID="lblTotalUsuarios" runat="server" Text="0" /></div>
                </div>
            </div>
        </div>
        <div class="col-md-3 mb-3">
            <div class="card shadow-sm stat-card green h-100">
                <div class="card-body py-3">
                    <div class="admin-kpi-etiqueta">Usuarios activos</div>
                    <div class="admin-kpi-valor"><asp:Label ID="lblUsuariosActivos" runat="server" Text="0" /></div>
                </div>
            </div>
        </div>
        <div class="col-md-3 mb-3">
            <div class="card shadow-sm stat-card red h-100">
                <div class="card-body py-3">
                    <div class="admin-kpi-etiqueta">Usuarios inactivos</div>
                    <div class="admin-kpi-valor"><asp:Label ID="lblUsuariosInactivos" runat="server" Text="0" /></div>
                </div>
            </div>
        </div>
        <div class="col-md-3 mb-3">
            <div class="card shadow-sm stat-card cyan h-100">
                <div class="card-body py-3">
                    <div class="admin-kpi-etiqueta">Eventos de auditoría hoy</div>
                    <div class="admin-kpi-valor"><asp:Label ID="lblEventosHoy" runat="server" Text="0" /></div>
                </div>
            </div>
        </div>
    </div>

    <!-- Fila principal -->
    <div class="row">

        <!-- Accesos rápidos -->
        <div class="col-md-4 mb-4">
            <div class="card shadow-sm h-100">
                <div class="card-header font-weight-bold">Acceso Rápido
                </div>
                <div class="list-group list-group-flush">
                    <a href="GestionUsuarios.aspx" class="list-group-item list-group-item-action quick-link">
                        <i class="fas fa-users-cog text-primary mr-2"></i>Gestión de usuarios
                        <small class="text-muted d-block">Crear, editar y desactivar cuentas</small>
                    </a>
                    <a href="Auditoria.aspx" class="list-group-item list-group-item-action quick-link">
                        <i class="fas fa-shield-alt text-secondary mr-2"></i>Auditoría del sistema
                        <small class="text-muted d-block">Historial completo de actividades</small>
                    </a>
                    <a href="Inicio.aspx" class="list-group-item list-group-item-action quick-link">
                        <i class="fas fa-tachometer-alt text-info mr-2"></i>Inicio operativo
                        <small class="text-muted d-block">Vista de despachos y operaciones</small>
                    </a>
                    <a href="RegistroDespacho.aspx" class="list-group-item list-group-item-action quick-link">
                        <i class="fas fa-box-open text-warning mr-2"></i>Nuevo despacho
                        <small class="text-muted d-block">Registrar un despacho</small>
                    </a>
                    <a href="LiquidacionesPendientes.aspx" class="list-group-item list-group-item-action quick-link">
                        <i class="fas fa-clock text-danger mr-2"></i>Liquidaciones pendientes
                        <small class="text-muted d-block">Revisión y aprobación</small>
                    </a>
                </div>
            </div>
        </div>

        <!-- Distribución de Roles -->
        <div class="col-md-4 mb-4">
            <div class="card shadow-sm h-100">
                <div class="card-header font-weight-bold">Usuarios por rol
                </div>
                <div class="card-body p-0">
                    <asp:Repeater ID="rptRoles" runat="server">
                        <ItemTemplate>
                            <div class="d-flex justify-content-between align-items-center px-3 py-2 border-bottom">
                                <span class="small"><i class="fas fa-circle text-muted mr-2" style="font-size:.5rem;vertical-align:middle;"></i><%# Eval("rol") %></span>
                                <span class="badge badge-primary badge-pill"><%# Eval("total") %></span>
                            </div>
                        </ItemTemplate>
                    </asp:Repeater>
                    <asp:Panel ID="pnlSinRoles" runat="server" Visible="false">
                        <p class="text-center text-muted py-3 mb-0">Sin datos.</p>
                    </asp:Panel>
                </div>
            </div>
        </div>

        <!-- Últimas actividades de auditoría -->
        <div class="col-md-4 mb-4">
            <div class="card shadow-sm h-100">
                <div class="card-header font-weight-bold">Últimas actividades
                </div>
                <div class="card-body p-0">
                    <asp:Repeater ID="rptAuditoria" runat="server">
                        <ItemTemplate>
                            <div class="px-3 py-2 border-bottom">
                                <div class="d-flex justify-content-between">
                                    <span class="small font-weight-bold"><%# Eval("NombreUsuario") %></span>
                                    <small class="text-muted"><%# Eval("FechaHora", "{0:dd/MM HH:mm}") %></small>
                                </div>
                                <small>
                                    <span class="badge badge-secondary"><%# Eval("Accion") %></span>
                                    <span class="text-muted"><%# Eval("TablaAfectada") %></span>
                                </small>
                            </div>
                        </ItemTemplate>
                    </asp:Repeater>
                    <asp:Panel ID="pnlSinAuditoria" runat="server" Visible="false">
                        <p class="text-center text-muted py-3 mb-0">Sin actividad registrada.</p>
                    </asp:Panel>
                </div>
                <div class="card-footer text-right p-2">
                    <a href="Auditoria.aspx" class="btn btn-sm btn-outline-secondary">
                        <i class="fas fa-external-link-alt mr-1"></i>Ver todo
                    </a>
                </div>
            </div>
        </div>

    </div>
</asp:Content>

<%@ Page Title="Panel Admin Sistema" Language="C#" MasterPageFile="~/Site.Master"
    AutoEventWireup="true" CodeBehind="DashboardAdminSistema.aspx.cs"
    Inherits="WebSGV.Views.DashboardAdminSistema" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <style>
        .stat-card { border-left: 4px solid; border-radius: 8px; }
        .stat-card.blue  { border-left-color: #0275d8; }
        .stat-card.green { border-left-color: #28a745; }
        .stat-card.red   { border-left-color: #dc3545; }
        .stat-card.cyan  { border-left-color: #17a2b8; }
        .quick-link { transition: background .15s; }
        .quick-link:hover { background: #f0f4ff !important; }
    </style>

    <!-- Encabezado -->
    <div class="d-flex justify-content-between align-items-center mb-4">
        <div>
            <h2 class="mb-0"><i class="fas fa-shield-alt text-primary mr-2"></i>Panel de Administración del Sistema</h2>
            <small class="text-muted"><asp:Label ID="lblFechaHora" runat="server" /></small>
        </div>
        <a href="GestionUsuarios.aspx" class="btn btn-primary">
            <i class="fas fa-users-cog mr-1"></i>Gestión de Usuarios
        </a>
    </div>

    <!-- Tarjetas de estadísticas -->
    <div class="row mb-4">
        <div class="col-md-3 mb-3">
            <div class="card shadow-sm stat-card blue h-100">
                <div class="card-body text-center">
                    <i class="fas fa-users fa-2x text-primary mb-2"></i>
                    <h2 class="font-weight-bold mb-0"><asp:Label ID="lblTotalUsuarios" runat="server" Text="0" /></h2>
                    <p class="text-muted mb-0 small">Usuarios Totales</p>
                </div>
            </div>
        </div>
        <div class="col-md-3 mb-3">
            <div class="card shadow-sm stat-card green h-100">
                <div class="card-body text-center">
                    <i class="fas fa-user-check fa-2x text-success mb-2"></i>
                    <h2 class="font-weight-bold mb-0"><asp:Label ID="lblUsuariosActivos" runat="server" Text="0" /></h2>
                    <p class="text-muted mb-0 small">Usuarios Activos</p>
                </div>
            </div>
        </div>
        <div class="col-md-3 mb-3">
            <div class="card shadow-sm stat-card red h-100">
                <div class="card-body text-center">
                    <i class="fas fa-user-times fa-2x text-danger mb-2"></i>
                    <h2 class="font-weight-bold mb-0"><asp:Label ID="lblUsuariosInactivos" runat="server" Text="0" /></h2>
                    <p class="text-muted mb-0 small">Usuarios Inactivos</p>
                </div>
            </div>
        </div>
        <div class="col-md-3 mb-3">
            <div class="card shadow-sm stat-card cyan h-100">
                <div class="card-body text-center">
                    <i class="fas fa-clipboard-list fa-2x text-info mb-2"></i>
                    <h2 class="font-weight-bold mb-0"><asp:Label ID="lblEventosHoy" runat="server" Text="0" /></h2>
                    <p class="text-muted mb-0 small">Eventos de Auditoría Hoy</p>
                </div>
            </div>
        </div>
    </div>

    <!-- Fila principal -->
    <div class="row">

        <!-- Acceso Rápido -->
        <div class="col-md-4 mb-4">
            <div class="card shadow-sm h-100">
                <div class="card-header bg-primary text-white font-weight-bold">
                    <i class="fas fa-th-large mr-2"></i>Acceso Rápido
                </div>
                <div class="list-group list-group-flush">
                    <a href="GestionUsuarios.aspx" class="list-group-item list-group-item-action quick-link">
                        <i class="fas fa-users-cog text-primary mr-2"></i>Gestión de Usuarios
                        <small class="text-muted d-block">Crear, editar y desactivar cuentas</small>
                    </a>
                    <a href="Auditoria.aspx" class="list-group-item list-group-item-action quick-link">
                        <i class="fas fa-shield-alt text-secondary mr-2"></i>Auditoría del Sistema
                        <small class="text-muted d-block">Historial completo de actividades</small>
                    </a>
                    <a href="Inicio.aspx" class="list-group-item list-group-item-action quick-link">
                        <i class="fas fa-tachometer-alt text-info mr-2"></i>Dashboard Operacional
                        <small class="text-muted d-block">Vista de despachos y operaciones</small>
                    </a>
                    <a href="RegistroDespacho.aspx" class="list-group-item list-group-item-action quick-link">
                        <i class="fas fa-box-open text-warning mr-2"></i>Nuevo Despacho
                        <small class="text-muted d-block">Registrar un despacho</small>
                    </a>
                    <a href="LiquidacionesPendientes.aspx" class="list-group-item list-group-item-action quick-link">
                        <i class="fas fa-clock text-danger mr-2"></i>Liquidaciones Pendientes
                        <small class="text-muted d-block">Revisión y aprobación</small>
                    </a>
                </div>
            </div>
        </div>

        <!-- Distribución de Roles -->
        <div class="col-md-4 mb-4">
            <div class="card shadow-sm h-100">
                <div class="card-header bg-info text-white font-weight-bold">
                    <i class="fas fa-user-tag mr-2"></i>Usuarios por Rol
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
                <div class="card-header bg-warning text-dark font-weight-bold">
                    <i class="fas fa-history mr-2"></i>Últimas Actividades
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

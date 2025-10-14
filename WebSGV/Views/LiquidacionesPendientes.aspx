<%@ Page Title="Liquidaciones Pendientes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" 
    CodeBehind="LiquidacionesPendientes.aspx.cs" Inherits="WebSGV.Views.LiquidacionesPendientes" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdOrdenSeleccionada" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfDetallesJSON" runat="server" ClientIDMode="Static" />

    <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-4">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-4">

        <div class="row mb-4">
            <div class="col-12">
                <div class="page-header">
                    <div class="d-flex justify-content-between align-items-center">
                        <div>
                            <h2 class="page-title mb-1">
                                <i class="fas fa-clipboard-check mr-2"></i>Liquidaciones Pendientes de Aprobación
                            </h2>
                            <p class="text-muted mb-0">
                                Revisa y aprueba las liquidaciones registradas por los conductores
                            </p>
                        </div>
                        <div class="header-stats">
                            <div class="stat-card stat-warning">
                                <div class="stat-icon">
                                    <i class="fas fa-clock"></i>
                                </div>
                                <div class="stat-info">
                                    <span class="stat-label">Pendientes</span>
                                    <span class="stat-value">
                                        <asp:Label ID="lblTotalPendientes" runat="server" Text="0"></asp:Label>
                                    </span>
                                </div>
                            </div>
                            <div class="stat-card stat-danger ml-3">
                                <div class="stat-icon">
                                    <i class="fas fa-exclamation-triangle"></i>
                                </div>
                                <div class="stat-info">
                                    <span class="stat-label">Urgentes (>24h)</span>
                                    <span class="stat-value">
                                        <asp:Label ID="lblTotalUrgentes" runat="server" Text="0"></asp:Label>
                                    </span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <div class="section-card mb-4">
            <div class="section-header">
                <h5 class="section-title">
                    <i class="fas fa-filter mr-2"></i>Filtros de Búsqueda
                </h5>
            </div>
            <div class="section-body">
                <div class="row">
                    <div class="col-md-3">
                        <div class="form-group">
                            <label class="form-label">Conductor</label>
                            <asp:DropDownList ID="ddlConductorFiltro" runat="server" CssClass="form-control">
                                <asp:ListItem Value="">-- Todos los conductores --</asp:ListItem>
                            </asp:DropDownList>
                        </div>
                    </div>
                    <div class="col-md-2">
                        <div class="form-group">
                            <label class="form-label">Desde</label>
                            <asp:TextBox ID="txtFechaDesde" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                        </div>
                    </div>
                    <div class="col-md-2">
                        <div class="form-group">
                            <label class="form-label">Hasta</label>
                            <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                        </div>
                    </div>
                    <div class="col-md-2">
                        <div class="form-group">
                            <label class="form-label">Prioridad</label>
                            <asp:DropDownList ID="ddlPrioridad" runat="server" CssClass="form-control">
                                <asp:ListItem Value="">-- Todas --</asp:ListItem>
                                <asp:ListItem Value="URGENTE">Urgentes (>24h)</asp:ListItem>
                                <asp:ListItem Value="ALTA">Alta (12-24h)</asp:ListItem>
                                <asp:ListItem Value="NORMAL">Normal (<12h)</asp:ListItem>
                            </asp:DropDownList>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="form-group">
                            <label class="form-label">&nbsp;</label>
                            <div class="d-flex">
                                <asp:Button ID="btnFiltrar" runat="server" Text="Buscar" 
                                    CssClass="btn btn-primary-custom btn-block mr-2" 
                                    OnClick="btnFiltrar_Click" />
                                <asp:Button ID="btnLimpiar" runat="server" Text="Limpiar" 
                                    CssClass="btn btn-secondary-custom btn-block" 
                                    OnClick="btnLimpiar_Click" />
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <div class="section-card">
            <div class="section-header section-header-warning">
                <div class="d-flex justify-content-between align-items-center">
                    <h5 class="section-title mb-0">
                        <i class="fas fa-list mr-2"></i>Lista de Liquidaciones Pendientes
                    </h5>
                    <div>
                        <button type="button" class="btn btn-sm btn-outline-primary" onclick="location.reload()">
                            <i class="fas fa-sync-alt mr-1"></i>Actualizar
                        </button>
                    </div>
                </div>
            </div>
            <div class="section-body p-0">
                <div class="table-responsive">
                    <asp:GridView ID="gvLiquidacionesPendientes" runat="server"
                        CssClass="table table-liquidaciones mb-0"
                        AutoGenerateColumns="false"
                        EmptyDataText="No hay liquidaciones pendientes de aprobación"
                        OnRowCommand="gvLiquidacionesPendientes_RowCommand"
                        DataKeyNames="IdOrdenViaje">
                        <Columns>
                            
                            <asp:TemplateField HeaderText="N° ORDEN">
                                <ItemTemplate>
                                    <div class="orden-info">
                                        <span class="orden-numero"><%# Eval("NumeroOrdenViaje") %></span>
                                        <span class="orden-fecha"><%# Eval("FechaRegistro", "{0:dd/MM/yyyy HH:mm}") %></span>
                                    </div>
                                </ItemTemplate>
                                <ItemStyle CssClass="text-left" Width="120px" />
                            </asp:TemplateField>

                            <asp:TemplateField HeaderText="CONDUCTOR">
                                <ItemTemplate>
                                    <div class="conductor-info">
                                        <i class="fas fa-user-circle mr-2 text-primary"></i>
                                        <div>
                                            <strong><%# Eval("NombreConductor") %></strong>
                                            <br />
                                            <small class="text-muted"><%# Eval("PlacaTracto") %> / <%# Eval("PlacaCarreta") %></small>
                                        </div>
                                    </div>
                                </ItemTemplate>
                                <ItemStyle CssClass="text-left" />
                            </asp:TemplateField>

                            <asp:TemplateField HeaderText="PERIODO VIAJE">
                                <ItemTemplate>
                                    <div class="viaje-periodo">
                                        <div class="fecha-item">
                                            <i class="fas fa-calendar-check text-success"></i>
                                            <span><%# Eval("FechaSalida", "{0:dd/MM/yyyy}") %></span>
                                        </div>
                                        <div class="fecha-item">
                                            <i class="fas fa-calendar-times text-danger"></i>
                                            <span><%# Eval("FechaLlegada", "{0:dd/MM/yyyy}") %></span>
                                        </div>
                                    </div>
                                </ItemTemplate>
                                <ItemStyle CssClass="text-center" Width="140px" />
                            </asp:TemplateField>

                            <asp:TemplateField HeaderText="BALANCE">
                                <ItemTemplate>
                                    <div class="balance-preview">
                                        <div class="balance-row">
                                            <span class="balance-moneda">S/</span>
                                            <span class="balance-monto <%# ObtenerClaseBalance(Eval("BalanceSoles")) %>">
                                                <%# Eval("BalanceSoles", "{0:N2}") %>
                                            </span>
                                        </div>
                                        <div class="balance-row">
                                            <span class="balance-moneda">$</span>
                                            <span class="balance-monto <%# ObtenerClaseBalance(Eval("BalanceDolares")) %>">
                                                <%# Eval("BalanceDolares", "{0:N2}") %>
                                            </span>
                                        </div>
                                    </div>
                                </ItemTemplate>
                                <ItemStyle CssClass="text-right" Width="120px" />
                            </asp:TemplateField>

                            <asp:TemplateField HeaderText="TIEMPO">
                                <ItemTemplate>
                                    <div class="tiempo-pendiente">
                                        <span class="badge-prioridad badge-prioridad-<%# ObtenerPrioridad(Eval("HorasPendientes")) %>">
                                            <i class="fas fa-clock mr-1"></i>
                                            <%# FormatearTiempo(Eval("HorasPendientes")) %>
                                        </span>
                                    </div>
                                </ItemTemplate>
                                <ItemStyle CssClass="text-center" Width="100px" />
                            </asp:TemplateField>

                            <asp:TemplateField HeaderText="ORIGEN">
                                <ItemTemplate>
                                    <span class="badge badge-conductor">
                                        <i class="fas fa-user-edit mr-1"></i>
                                        <%# Eval("RegistradoPor") %>
                                    </span>
                                </ItemTemplate>
                                <ItemStyle CssClass="text-center" Width="100px" />
                            </asp:TemplateField>

                            <asp:TemplateField HeaderText="ACCIONES">
                                <ItemTemplate>
                                    <div class="acciones-grupo">
                                        <button type="button" class="btn btn-info-action" 
                                            onclick="verDetalleLiquidacion(<%# Eval("IdOrdenViaje") %>)"
                                            title="Ver Detalle">
                                            <i class="fas fa-eye"></i>
                                        </button>

                                        <asp:LinkButton ID="btnEditar" runat="server"
                                            CssClass="btn btn-warning-action"
                                            CommandName="Editar"
                                            CommandArgument='<%# Eval("IdOrdenViaje") %>'
                                            ToolTip="Editar Liquidación"
                                            OnClientClick="return confirm('¿Desea editar esta liquidación?');">
                                            <i class="fas fa-edit"></i>
                                        </asp:LinkButton>

                                        <asp:LinkButton ID="btnAprobar" runat="server"
                                            CssClass="btn btn-success-action"
                                            CommandName="Aprobar"
                                            CommandArgument='<%# Eval("IdOrdenViaje") %>'
                                            ToolTip="Aprobar Liquidación"
                                            OnClientClick="return confirm('¿Está seguro de APROBAR esta liquidación?\n\nEsta acción completará el viaje y no se podrá deshacer.');">
                                            <i class="fas fa-check"></i>
                                        </asp:LinkButton>

                                        <button type="button" class="btn btn-danger-action" 
                                            onclick="abrirModalRechazar(<%# Eval("IdOrdenViaje") %>, '<%# Eval("NumeroOrdenViaje") %>')"
                                            title="Rechazar Liquidación">
                                            <i class="fas fa-times"></i>
                                        </button>
                                    </div>
                                </ItemTemplate>
                                <ItemStyle CssClass="text-center" Width="200px" />
                            </asp:TemplateField>

                        </Columns>
                        <EmptyDataTemplate>
                            <div class="empty-state-table">
                                <i class="fas fa-check-circle empty-icon"></i>
                                <h4>¡Excelente trabajo!</h4>
                                <p class="text-muted">No hay liquidaciones pendientes de aprobación en este momento.</p>
                            </div>
                        </EmptyDataTemplate>
                    </asp:GridView>
                </div>
            </div>
        </div>

        <div class="alert alert-info-light mt-4">
            <h6 class="mb-2"><i class="fas fa-info-circle mr-2"></i>Leyenda de Prioridades:</h6>
            <div class="d-flex align-items-center">
                <span class="badge-prioridad badge-prioridad-normal mr-3">Normal: < 12 horas</span>
                <span class="badge-prioridad badge-prioridad-alta mr-3">Alta: 12-24 horas</span>
                <span class="badge-prioridad badge-prioridad-urgente">Urgente: > 24 horas</span>
            </div>
        </div>

    </div>

    <div class="modal fade" id="modalDetalleLiquidacion" tabindex="-1">
        <div class="modal-dialog modal-xl">
            <div class="modal-content">
                <div class="modal-header modal-header-info">
                    <h5 class="modal-title">
                        <i class="fas fa-file-invoice-dollar mr-2"></i>
                        Detalle de Liquidación - <span id="modalNumeroOrden"></span>
                    </h5>
                    <button type="button" class="close" data-dismiss="modal">
                        <span>&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    
                    <div class="detail-section">
                        <h6 class="detail-section-title">
                            <i class="fas fa-route mr-2"></i>Información del Viaje
                        </h6>
                        <div class="row">
                            <div class="col-md-3">
                                <label class="detail-label">Conductor:</label>
                                <p class="detail-value" id="detalleConductor"></p>
                            </div>
                            <div class="col-md-3">
                                <label class="detail-label">Tracto:</label>
                                <p class="detail-value" id="detalleTracto"></p>
                            </div>
                            <div class="col-md-3">
                                <label class="detail-label">Carreta:</label>
                                <p class="detail-value" id="detalleCarreta"></p>
                            </div>
                            <div class="col-md-3">
                                <label class="detail-label">Periodo:</label>
                                <p class="detail-value" id="detallePeriodo"></p>
                            </div>
                        </div>
                        <div class="row mt-2" id="detalleObservacionesRow" style="display: none;">
                            <div class="col-md-12">
                                <label class="detail-label">Observaciones del Conductor:</label>
                                <p class="detail-value detail-observaciones" id="detalleObservaciones"></p>
                            </div>
                        </div>
                    </div>

                    <div class="detail-section">
                        <h6 class="detail-section-title">
                            <i class="fas fa-calculator mr-2"></i>Resumen Financiero
                        </h6>
                        <div class="row">
                            <div class="col-md-6">
                                <div class="financial-card financial-card-success">
                                    <div class="financial-header">
                                        <i class="fas fa-plus-circle"></i>
                                        <span>Total Ingresos</span>
                                    </div>
                                    <div class="financial-amounts">
                                        <div class="amount-row">
                                            <span class="currency">S/</span>
                                            <span class="amount" id="detalleIngresosSoles">0.00</span>
                                        </div>
                                        <div class="amount-row">
                                            <span class="currency">$</span>
                                            <span class="amount" id="detalleIngresosDolares">0.00</span>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            <div class="col-md-6">
                                <div class="financial-card financial-card-danger">
                                    <div class="financial-header">
                                        <i class="fas fa-minus-circle"></i>
                                        <span>Total Gastos</span>
                                    </div>
                                    <div class="financial-amounts">
                                        <div class="amount-row">
                                            <span class="currency">S/</span>
                                            <span class="amount" id="detalleGastosSoles">0.00</span>
                                        </div>
                                        <div class="amount-row">
                                            <span class="currency">$</span>
                                            <span class="amount" id="detalleGastosDolares">0.00</span>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                        <div class="row mt-3">
                            <div class="col-md-12">
                                <div class="financial-card financial-card-neutral">
                                    <div class="financial-header">
                                        <i class="fas fa-balance-scale"></i>
                                        <span>Balance Final</span>
                                    </div>
                                    <div class="financial-amounts">
                                        <div class="amount-row">
                                            <span class="currency">S/</span>
                                            <span class="amount balance-amount" id="detalleBalanceSoles">0.00</span>
                                        </div>
                                        <div class="amount-row">
                                            <span class="currency">$</span>
                                            <span class="amount balance-amount" id="detalleBalanceDolares">0.00</span>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div class="detail-section">
                        <h6 class="detail-section-title">
                            <i class="fas fa-receipt mr-2"></i>Desglose de Gastos
                        </h6>
                        <div class="table-responsive">
                            <table class="table table-sm table-detail">
                                <thead>
                                    <tr>
                                        <th>Concepto</th>
                                        <th class="text-right">Soles (S/)</th>
                                        <th class="text-right">Dólares ($)</th>
                                    </tr>
                                </thead>
                                <tbody id="detalleGastosBody">
                                </tbody>
                            </table>
                        </div>
                    </div>

                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary-custom" data-dismiss="modal">Cerrar</button>
                    <button type="button" class="btn btn-primary-custom" onclick="imprimirDetalle()">
                        <i class="fas fa-print mr-1"></i>Imprimir
                    </button>
                </div>
            </div>
        </div>
    </div>

    <div class="modal fade" id="modalRechazar" tabindex="-1">
        <div class="modal-dialog">
            <div class="modal-content">
                <div class="modal-header modal-header-danger">
                    <h5 class="modal-title">
                        <i class="fas fa-exclamation-triangle mr-2"></i>
                        Rechazar Liquidación
                    </h5>
                    <button type="button" class="close" data-dismiss="modal">
                        <span>&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <div class="alert alert-warning">
                        <i class="fas fa-info-circle mr-2"></i>
                        <strong>Atención:</strong> Al rechazar esta liquidación, el viaje se reabrirá automáticamente 
                        para que el conductor pueda corregir los datos.
                    </div>
                    
                    <input type="hidden" id="rechazarIdOrden" />
                    
                    <div class="form-group">
                        <label class="form-label">N° de Orden:</label>
                        <input type="text" id="rechazarNumeroOrden" class="form-control" readonly />
                    </div>

                    <div class="form-group">
                        <label class="form-label">Motivo del Rechazo <span class="text-danger">*</span></label>
                        <textarea id="rechazarObservaciones" class="form-control" rows="4" 
                            placeholder="Explique detalladamente qué debe corregir el conductor...&#13;&#10;Ejemplo: Los montos de peajes no coinciden con los comprobantes adjuntos. Por favor revisar."></textarea>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary-custom" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnConfirmarRechazo" runat="server" 
                        CssClass="btn btn-danger-custom" 
                        Text="Rechazar Liquidación"
                        OnClick="btnConfirmarRechazo_Click"
                        OnClientClick="return validarRechazo();" />
                </div>
            </div>
        </div>
    </div>

    <style>
        :root {
            --primary-color: #2563eb;
            --success-color: #059669;
            --danger-color: #dc2626;
            --warning-color: #d97706;
            --info-color: #0891b2;
            --neutral-color: #64748b;
            --light-gray: #f8fafc;
            --medium-gray: #e2e8f0;
            --border-color: #cbd5e1;
        }

        .page-header {
            padding-bottom: 1.5rem;
            border-bottom: 2px solid var(--border-color);
        }

        .page-title {
            color: #1e293b;
            font-weight: 600;
            font-size: 1.75rem;
        }

        .header-stats {
            display: flex;
        }

        .stat-card {
            background: white;
            border: 2px solid var(--border-color);
            border-radius: 0.5rem;
            padding: 1rem 1.25rem;
            display: flex;
            align-items: center;
            min-width: 150px;
        }

        .stat-card.stat-warning {
            border-color: #fbbf24;
            background: linear-gradient(135deg, #fef3c7 0%, #fde68a 100%);
        }

        .stat-card.stat-danger {
            border-color: #f87171;
            background: linear-gradient(135deg, #fee2e2 0%, #fecaca 100%);
        }

        .stat-icon {
            font-size: 2rem;
            margin-right: 1rem;
            color: #78350f;
        }

        .stat-card.stat-danger .stat-icon {
            color: #991b1b;
        }

        .stat-info {
            display: flex;
            flex-direction: column;
        }

        .stat-label {
            font-size: 0.75rem;
            font-weight: 600;
            text-transform: uppercase;
            color: #64748b;
        }

        .stat-value {
            font-size: 1.875rem;
            font-weight: 700;
            color: #1e293b;
        }

        .section-card {
            background: white;
            border: 1px solid var(--border-color);
            border-radius: 0.5rem;
            overflow: hidden;
        }

        .section-header {
            background: var(--light-gray);
            padding: 1rem 1.25rem;
            border-bottom: 1px solid var(--border-color);
        }

        .section-header-warning {
            background: #fef3c7;
            border-bottom-color: #fbbf24;
        }

        .section-title {
            color: #1e293b;
            font-size: 1.125rem;
            font-weight: 600;
            margin: 0;
        }

        .section-body {
            padding: 1.5rem;
        }

        .table-liquidaciones {
            font-size: 0.875rem;
        }

        .table-liquidaciones thead th {
            background: var(--light-gray);
            color: #1e293b;
            font-weight: 600;
            font-size: 0.8125rem;
            text-transform: uppercase;
            letter-spacing: 0.025em;
            padding: 0.875rem 0.75rem;
            border-bottom: 2px solid var(--border-color);
            white-space: nowrap;
        }

        .table-liquidaciones tbody td {
            vertical-align: middle;
            padding: 1rem 0.75rem;
            border-bottom: 1px solid var(--medium-gray);
        }

        .table-liquidaciones tbody tr:hover {
            background: var(--light-gray);
        }

        .orden-info {
            display: flex;
            flex-direction: column;
        }

        .orden-numero {
            font-weight: 700;
            color: var(--primary-color);
            font-size: 1rem;
        }

        .orden-fecha {
            font-size: 0.75rem;
            color: var(--neutral-color);
            margin-top: 0.25rem;
        }

        .conductor-info {
            display: flex;
            align-items: center;
        }

        .viaje-periodo {
            display: flex;
            flex-direction: column;
            gap: 0.5rem;
        }

        .fecha-item {
            display: flex;
            align-items: center;
            gap: 0.5rem;
            font-size: 0.875rem;
        }

        .balance-preview {
            display: flex;
            flex-direction: column;
            gap: 0.25rem;
        }

        .balance-row {
            display: flex;
            justify-content: flex-end;
            align-items: center;
            gap: 0.5rem;
        }

        .balance-moneda {
            font-weight: 600;
            color: var(--neutral-color);
        }

        .balance-monto {
            font-weight: 700;
            font-size: 1rem;
        }

        .balance-positivo {
            color: var(--success-color);
        }

        .balance-negativo {
            color: var(--danger-color);
        }

        .badge-prioridad {
            display: inline-flex;
            align-items: center;
            padding: 0.5rem 0.75rem;
            border-radius: 0.375rem;
            font-weight: 600;
            font-size: 0.8125rem;
        }

        .badge-prioridad-normal {
            background: #d1fae5;
            color: #065f46;
            border: 1px solid #86efac;
        }

        .badge-prioridad-alta {
            background: #fed7aa;
            color: #92400e;
            border: 1px solid #fdba74;
        }

        .badge-prioridad-urgente {
            background: #fecaca;
            color: #991b1b;
            border: 1px solid #f87171;
            animation: pulse-urgent 2s infinite;
        }

        @keyframes pulse-urgent {
            0%, 100% { opacity: 1; }
            50% { opacity: 0.7; }
        }

        .badge-conductor {
            background: #dbeafe;
            color: #1e40af;
            padding: 0.5rem 0.75rem;
            border-radius: 0.375rem;
            font-weight: 600;
            font-size: 0.75rem;
        }

        .acciones-grupo {
            display: flex;
            gap: 0.5rem;
            justify-content: center;
        }

        .btn-info-action,
        .btn-warning-action,
        .btn-success-action,
        .btn-danger-action {
            padding: 0.5rem 0.75rem;
            border: none;
            border-radius: 0.375rem;
            font-size: 0.875rem;
            cursor: pointer;
            transition: all 0.2s;
            color: white;
        }

        .btn-info-action {
            background: var(--info-color);
        }

        .btn-info-action:hover {
            background: #0e7490;
            transform: translateY(-1px);
        }

        .btn-warning-action {
            background: var(--warning-color);
        }

        .btn-warning-action:hover {
            background: #b45309;
            transform: translateY(-1px);
        }

        .btn-success-action {
            background: var(--success-color);
        }

        .btn-success-action:hover {
            background: #047857;
            transform: translateY(-1px);
        }

        .btn-danger-action {
            background: var(--danger-color);
        }

        .btn-danger-action:hover {
            background: #b91c1c;
            transform: translateY(-1px);
        }

        .btn-primary-custom {
            background: var(--primary-color);
            border: none;
            color: white;
            font-weight: 500;
            padding: 0.5rem 1rem;
        }

        .btn-secondary-custom {
            background: white;
            border: 1px solid var(--border-color);
            color: var(--neutral-color);
            font-weight: 500;
        }

        .btn-danger-custom {
            background: var(--danger-color);
            border: none;
            color: white;
            font-weight: 500;
        }

        .empty-state-table {
            text-align: center;
            padding: 4rem 2rem;
        }

        .empty-icon {
            font-size: 4rem;
            color: var(--success-color);
            margin-bottom: 1rem;
        }

        .modal-header-info {
            background: #e0f2fe;
            border-bottom: 2px solid #7dd3fc;
        }

        .modal-header-danger {
            background: #fee2e2;
            border-bottom: 2px solid #f87171;
        }

        .detail-section {
            padding: 1.5rem;
            background: var(--light-gray);
            border-radius: 0.5rem;
            margin-bottom: 1.5rem;
        }

        .detail-section-title {
            font-weight: 600;
            color: #1e293b;
            margin-bottom: 1rem;
            padding-bottom: 0.5rem;
            border-bottom: 2px solid var(--border-color);
        }

        .detail-label {
            font-size: 0.75rem;
            font-weight: 600;
            color: var(--neutral-color);
            text-transform: uppercase;
            margin-bottom: 0.25rem;
        }

        .detail-value {
            font-size: 1rem;
            font-weight: 600;
            color: #1e293b;
        }

        .detail-observaciones {
            background: white;
            padding: 0.75rem;
            border-radius: 0.375rem;
            border: 1px solid var(--border-color);
            font-style: italic;
        }

        .financial-card {
            border: 2px solid var(--border-color);
            border-radius: 0.5rem;
            padding: 1rem;
        }

        .financial-card-success {
            background: linear-gradient(135deg, #d1fae5 0%, #a7f3d0 100%);
            border-color: #86efac;
        }

        .financial-card-danger {
            background: linear-gradient(135deg, #fee2e2 0%, #fecaca 100%);
            border-color: #fca5a5;
        }

        .financial-card-neutral {
            background: linear-gradient(135deg, #e0f2fe 0%, #bae6fd 100%);
            border-color: #7dd3fc;
        }

        .financial-header {
            display: flex;
            align-items: center;
            gap: 0.5rem;
            font-weight: 600;
            margin-bottom: 0.75rem;
            font-size: 1rem;
        }

        .financial-amounts {
            display: flex;
            flex-direction: column;
            gap: 0.5rem;
        }

        .amount-row {
            display: flex;
            align-items: center;
            gap: 0.75rem;
        }

        .amount-row .currency {
            font-weight: 600;
            color: var(--neutral-color);
            font-size: 1rem;
        }

        .amount-row .amount {
            font-size: 1.5rem;
            font-weight: 700;
            color: #1e293b;
        }

        .balance-amount {
            font-size: 1.75rem !important;
        }

        .table-detail thead th {
            background: var(--light-gray);
            font-weight: 600;
            padding: 0.75rem;
        }

        .table-detail tbody td {
            padding: 0.625rem 0.75rem;
        }

        .alert-info-light {
            background: #f0f9ff;
            border: 1px solid #bae6fd;
            border-radius: 0.5rem;
            padding: 1rem;
        }

        .form-label {
            color: #1e293b;
            font-weight: 500;
            font-size: 0.875rem;
            margin-bottom: 0.375rem;
        }

        .form-control {
            border: 1px solid var(--border-color);
            font-size: 0.875rem;
            padding: 0.5rem 0.75rem;
        }

        .form-control:focus {
            border-color: var(--primary-color);
            box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
        }

        .text-muted { color: var(--neutral-color) !important; }
        .mr-1 { margin-right: 0.25rem; }
        .mr-2 { margin-right: 0.5rem; }
        .mr-3 { margin-right: 0.75rem; }
        .ml-3 { margin-left: 0.75rem; }
        .mb-2 { margin-bottom: 0.5rem; }
        .mb-4 { margin-bottom: 1rem; }
        .mt-2 { margin-top: 0.5rem; }
        .mt-3 { margin-top: 0.75rem; }
        .mt-4 { margin-top: 1rem; }
    </style>

    <script src="https://code.jquery.com/jquery-3.6.0.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@4.6.0/dist/js/bootstrap.bundle.min.js"></script>

    <script>
        function verDetalleLiquidacion(idOrdenViaje) {
            console.log('Ver detalle:', idOrdenViaje);

            $.ajax({
                type: "POST",
                url: "LiquidacionesPendientes.aspx/ObtenerDetalleLiquidacion",
                data: JSON.stringify({ idOrdenViaje: idOrdenViaje }),
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                success: function (response) {
                    mostrarDetalle(response.d);
                },
                error: function (error) {
                    console.error('Error:', error);
                    alert('Error al cargar el detalle de la liquidación');
                }
            });
        }

        function mostrarDetalle(datos) {
            if (!datos) {
                alert('No se encontraron datos de la liquidación');
                return;
            }

            $('#modalNumeroOrden').text(datos.NumeroOrdenViaje);
            $('#detalleConductor').text(datos.NombreConductor);
            $('#detalleTracto').text(datos.PlacaTracto);
            $('#detalleCarreta').text(datos.PlacaCarreta);
            $('#detallePeriodo').text(datos.FechaSalida + ' al ' + datos.FechaLlegada);

            if (datos.Observaciones) {
                $('#detalleObservaciones').text(datos.Observaciones);
                $('#detalleObservacionesRow').show();
            } else {
                $('#detalleObservacionesRow').hide();
            }

            $('#detalleIngresosSoles').text(parseFloat(datos.TotalIngresosSoles).toFixed(2));
            $('#detalleIngresosDolares').text(parseFloat(datos.TotalIngresosDolares).toFixed(2));
            $('#detalleGastosSoles').text(parseFloat(datos.TotalGastosSoles).toFixed(2));
            $('#detalleGastosDolares').text(parseFloat(datos.TotalGastosDolares).toFixed(2));

            let balanceSoles = datos.TotalIngresosSoles - datos.TotalGastosSoles;
            let balanceDolares = datos.TotalIngresosDolares - datos.TotalGastosDolares;

            $('#detalleBalanceSoles').text(balanceSoles.toFixed(2))
                .css('color', balanceSoles >= 0 ? '#059669' : '#dc2626');
            $('#detalleBalanceDolares').text(balanceDolares.toFixed(2))
                .css('color', balanceDolares >= 0 ? '#059669' : '#dc2626');

            let gastosHtml = '';
            const conceptos = ['Peajes', 'Alimentación', 'ApoyoSeguridad', 'Reparaciones', 'Movilidad', 'Encarpada', 'Hospedaje', 'Combustible'];

            conceptos.forEach(concepto => {
                let soles = parseFloat(datos['Gastos' + concepto + 'Soles'] || 0);
                let dolares = parseFloat(datos['Gastos' + concepto + 'Dolares'] || 0);

                if (soles > 0 || dolares > 0) {
                    gastosHtml += `
                        <tr>
                            <td><strong>${concepto}</strong></td>
                            <td class="text-right">S/ ${soles.toFixed(2)}</td>
                            <td class="text-right">$ ${dolares.toFixed(2)}</td>
                        </tr>
                    `;
                }
            });

            if (!gastosHtml) {
                gastosHtml = '<tr><td colspan="3" class="text-center text-muted">No hay gastos registrados</td></tr>';
            }

            $('#detalleGastosBody').html(gastosHtml);
            $('#modalDetalleLiquidacion').modal('show');
        }

        function abrirModalRechazar(idOrden, numeroOrden) {
            $('#rechazarIdOrden').val(idOrden);
            $('#rechazarNumeroOrden').val(numeroOrden);
            $('#rechazarObservaciones').val('');
            $('#hfIdOrdenSeleccionada').val(idOrden);
            $('#modalRechazar').modal('show');
        }

        function validarRechazo() {
            const observaciones = $('#rechazarObservaciones').val().trim();

            if (!observaciones) {
                alert('Por favor, ingrese el motivo del rechazo');
                $('#rechazarObservaciones').focus();
                return false;
            }

            if (observaciones.length < 10) {
                alert('El motivo del rechazo debe tener al menos 10 caracteres');
                $('#rechazarObservaciones').focus();
                return false;
            }

            $('<input>').attr({
                type: 'hidden',
                name: 'observacionesRechazo',
                value: observaciones
            }).appendTo('form');

            return confirm('¿Está seguro de RECHAZAR esta liquidación?\n\nEl viaje se reabrirá para correcciones.');
        }

        function imprimirDetalle() {
            window.print();
        }

        $(document).ready(function () {
            console.log('LiquidacionesPendientes cargado');
        });
    </script>

</asp:Content>

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
                                <i class="fas fa-clipboard-check mr-2"></i>Gestión de Liquidaciones
                            </h2>
                            <p class="text-muted mb-0">
                                Aprueba, revisa y controla las liquidaciones de los conductores
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

        <!-- TABS DE NAVEGACIÓN -->
        <div class="row mb-3">
            <div class="col-12">
                <div class="tabs-navigation">
                    <button type="button" class="tab-btn tab-btn-active" id="tabPendientes" onclick="cambiarTab('pendientes')">
                        <i class="fas fa-clock mr-2"></i>Pendientes de Aprobación
                    </button>
                    <button type="button" class="tab-btn" id="tabAprobadas" onclick="cambiarTab('aprobadas')">
                        <i class="fas fa-shield-alt mr-2"></i>Control de Aprobadas
                    </button>
                </div>
            </div>
        </div>

        <!-- SECCIÓN PENDIENTES -->
        <div id="seccionPendientes">

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
                            <div class="conductor-autocomplete-wrap">
                                 <input type="text" id="txtConductorBuscar" class="form-control"
                                     placeholder="Buscar conductor..." autocomplete="off" spellcheck="false" autocapitalize="off" autocorrect="off" name="filtro_conductor_pendiente" />
                                 <asp:HiddenField ID="hfConductorId" runat="server" ClientIDMode="Static" />
                                 <small id="errorConductorBuscar" class="text-danger d-none"></small>
                                 <div id="conductorPendientesSugg" class="conductor-autocomplete-dropdown"></div>
                             </div>
                        </div>
                    </div>
                    <div class="col-md-2">
                        <div class="form-group">
                            <label class="form-label">Desde</label>
                             <asp:TextBox ID="txtFechaDesde" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                             <small id="errorFechaDesde" class="text-danger d-none"></small>
                        </div>
                    </div>
                    <div class="col-md-2">
                        <div class="form-group">
                            <label class="form-label">Hasta</label>
                             <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                             <small id="errorFechaHasta" class="text-danger d-none"></small>
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
                            <small id="errorPrioridad" class="text-danger d-none"></small>
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

                                        <button type="button" class="btn btn-success-action"
                                            onclick="abrirModalAprobar(<%# Eval("IdOrdenViaje") %>)"
                                            title="Aprobar Liquidación">
                                            <i class="fas fa-check"></i>
                                        </button>

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

        </div><!-- /seccionPendientes -->

        <!-- SECCIÓN CONTROL DE APROBADAS -->
        <div id="seccionAprobadas" style="display:none;">

            <div class="row mb-3">
                <div class="col-12">
                    <div class="d-flex align-items-center" style="gap:1rem;">
                        <div class="stat-card stat-success-custom">
                            <div class="stat-icon"><i class="fas fa-check-circle"></i></div>
                            <div class="stat-info">
                                <span class="stat-label">Encontradas</span>
                                <span class="stat-value" id="lblTotalAprobadas">0</span>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <div class="section-card mb-4">
                <div class="section-header">
                    <h5 class="section-title"><i class="fas fa-filter mr-2"></i>Filtros</h5>
                </div>
                <div class="section-body">
                    <div class="row">
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">Conductor</label>
                                <div class="conductor-autocomplete-wrap">
                                    <input type="text" id="txtConductorAprobadas" class="form-control"
                                        placeholder="Buscar conductor..." autocomplete="off" spellcheck="false" autocapitalize="off" autocorrect="off" name="filtro_conductor_aprobada" />
                                    <input type="hidden" id="filtroCondAprobadasId" value="" />
                                    <div id="conductorAprobadasSugg" class="conductor-autocomplete-dropdown"></div>
                                </div>
                            </div>
                        </div>
                        <div class="col-md-2">
                            <div class="form-group">
                                <label class="form-label">Desde</label>
                                <input type="date" id="filtroDesdeAprobadas" class="form-control" />
                            </div>
                        </div>
                        <div class="col-md-2">
                            <div class="form-group">
                                <label class="form-label">Hasta</label>
                                <input type="date" id="filtroHastaAprobadas" class="form-control" />
                            </div>
                        </div>
                        <div class="col-md-2">
                            <div class="form-group">
                                <label class="form-label">N° Orden</label>
                                <input type="text" id="filtroOrdenAprobadas" class="form-control" placeholder="Buscar..." />
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="form-label">&nbsp;</label>
                                <div class="d-flex">
                                    <button type="button" class="btn btn-primary-custom btn-block mr-2" onclick="cargarLiquidacionesAprobadas()">Buscar</button>
                                    <button type="button" class="btn btn-secondary-custom btn-block" onclick="limpiarFiltrosAprobadas()">Limpiar</button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <div class="section-card">
                <div class="section-header section-header-success">
                    <div class="d-flex justify-content-between align-items-center">
                        <h5 class="section-title mb-0">
                            <i class="fas fa-clipboard-check mr-2"></i>Liquidaciones Aprobadas
                        </h5>
                        <button type="button" class="btn btn-sm btn-outline-success" onclick="cargarLiquidacionesAprobadas()">
                            <i class="fas fa-sync-alt mr-1"></i>Actualizar
                        </button>
                    </div>
                </div>
                <div class="section-body p-0">
                    <div id="loadingAprobadas" class="text-center py-4" style="display:none;">
                        <div class="spinner-border text-success"><span class="sr-only">Cargando...</span></div>
                        <p class="text-muted mt-2">Cargando liquidaciones aprobadas...</p>
                    </div>
                    <div class="table-responsive">
                        <table class="table table-liquidaciones mb-0" id="tablaAprobadas">
                            <thead>
                                <tr>
                                    <th>N° ORDEN</th>
                                    <th>CONDUCTOR</th>
                                    <th>PERIODO VIAJE</th>
                                    <th class="text-right">DESCUENTO</th>
                                    <th class="text-right">REINTEGRO</th>
                                    <th class="text-right">BALANCE FINAL</th>
                                    <th class="text-center">ACCIONES</th>
                                </tr>
                            </thead>
                            <tbody id="tbodyAprobadas">
                                <tr>
                                    <td colspan="7" class="text-center text-muted py-4">
                                        <i class="fas fa-search mr-2"></i>Haga clic en "Buscar" para cargar las liquidaciones aprobadas
                                    </td>
                                </tr>
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>

            <div class="alert alert-info-light mt-4">
                <h6 class="mb-2"><i class="fas fa-info-circle mr-2"></i>Acciones disponibles:</h6>
                <div class="d-flex flex-wrap" style="gap:0.75rem;">
                    <span><i class="fas fa-eye text-info mr-1"></i><strong>Ver:</strong> Revisar el detalle completo de la liquidación</span>
                    <span><i class="fas fa-edit text-warning mr-1"></i><strong>Corregir:</strong> Modificar descuentos y reintegros aplicados</span>
                    <span><i class="fas fa-undo text-danger mr-1"></i><strong>Revertir:</strong> Devolver la liquidación a estado pendiente</span>
                </div>
            </div>

        </div><!-- /seccionAprobadas -->

    </div>

    <div class="modal fade" id="modalDetalleLiquidacion" tabindex="-1">
        <div class="modal-dialog modal-xl">
            <div class="modal-content">
                <div class="modal-header modal-header-info" id="modalDetalleLiquidacionHeader">
                    <div class="d-flex align-items-center" style="flex:1;min-width:0;">
                        <h5 class="modal-title mb-0">
                            <i class="fas fa-file-invoice-dollar mr-2"></i>
                            <span id="modalModeLabel">Detalle de Liquidación</span>
                            <span class="modal-orden-ref ml-2" id="modalNumeroOrden"></span>
                        </h5>
                        <span class="badge-modo-aprobacion ml-3" id="modalModeBadge" style="display:none;">
                            <i class="fas fa-check-circle mr-1"></i>APROBANDO
                        </span>
                    </div>
                    <button type="button" class="close ml-2" data-dismiss="modal">
                        <span>&times;</span>
                    </button>
                </div>
                <div class="modal-body">

                    <div id="detalleLoading" class="text-center py-5" style="display:none;">
                        <div class="spinner-border text-primary" role="status" style="width:3rem;height:3rem;">
                            <span class="sr-only">Cargando...</span>
                        </div>
                        <p class="text-muted mt-3 mb-0">Cargando detalle de la liquidación...</p>
                    </div>

                    <div id="detalleError" class="text-center py-4" style="display:none;">
                        <i class="fas fa-exclamation-triangle text-danger fa-3x mb-3 d-block"></i>
                        <p class="text-muted">Error al cargar el detalle. Por favor, intente de nuevo.</p>
                    </div>

                    <div id="approvalBanner" class="approval-guidance-banner" style="display:none;">
                        <div class="agb-content">
                            <i class="fas fa-clipboard-check agb-icon"></i>
                            <div class="agb-text">
                                <strong>Revisión para Aprobación</strong>
                                <span>Revise el balance financiero. Si aplica descuentos o reintegros, complételos en los <a href="#" onclick="scrollToAjustes(); return false;" class="agb-link">Ajustes Administrativos ↓</a>. Luego use el botón <strong>Aprobar Liquidación</strong>.</span>
                            </div>
                        </div>
                    </div>

                    <div class="detail-section viaje-info-section">
                        <div class="viaje-info-strip">
                            <div class="vi-item vi-item-conductor">
                                <span class="vi-label"><i class="fas fa-user-tie mr-1"></i>Conductor</span>
                                <span class="vi-value" id="detalleConductor"></span>
                            </div>
                            <div class="vi-sep"></div>
                            <div class="vi-item">
                                <span class="vi-label"><i class="fas fa-truck mr-1"></i>Tracto</span>
                                <span class="vi-value" id="detalleTracto"></span>
                            </div>
                            <div class="vi-sep"></div>
                            <div class="vi-item">
                                <span class="vi-label"><i class="fas fa-trailer mr-1"></i>Carreta</span>
                                <span class="vi-value" id="detalleCarreta"></span>
                            </div>
                            <div class="vi-sep"></div>
                            <div class="vi-item">
                                <span class="vi-label"><i class="fas fa-calendar-alt mr-1"></i>Periodo</span>
                                <span class="vi-value vi-value-periodo" id="detallePeriodo"></span>
                            </div>
                        </div>
                        <div id="detalleObservacionesRow" style="display:none;" class="vi-obs-row">
                            <i class="fas fa-comment-alt vi-obs-icon"></i>
                            <span class="vi-obs-label">Obs. Conductor:</span>
                            <span class="vi-obs-text" id="detalleObservaciones"></span>
                        </div>
                    </div>

                    <!-- Salida registrada por el conductor + corrección (solo administradora) -->
                    <div class="detail-section salida-section" id="seccionCorregirSalida" style="display:none;">
                        <div class="salida-head">
                            <div class="salida-info">
                                <span class="salida-label"><i class="fas fa-sign-out-alt mr-1"></i>Salida registrada por el conductor</span>
                                <span class="salida-value">
                                    <i class="far fa-clock salida-value-icon"></i>
                                    <span id="detalleSalidaValor">—</span>
                                </span>
                            </div>
                            <button type="button" class="btn-corregir-salida" onclick="toggleCorregirSalida()">
                                <i class="fas fa-pen mr-1"></i>Corregir
                            </button>
                        </div>
                        <div id="panelCorregirSalida" class="salida-edit-panel" style="display:none;">
                            <div class="row">
                                <div class="col-12 col-md-4 form-group">
                                    <label class="form-label">Fecha de salida</label>
                                    <input type="date" class="form-control" id="corregirSalidaFecha" />
                                </div>
                                <div class="col-12 col-md-4 form-group">
                                    <label class="form-label">Hora de salida</label>
                                    <input type="time" class="form-control" id="corregirSalidaHora" />
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="form-label">Motivo de la corrección <span class="text-danger">*</span></label>
                                <textarea class="form-control" id="corregirSalidaMotivo" rows="2" maxlength="500"
                                    placeholder="Explique por qué corrige la salida (mín. 10 caracteres)"></textarea>
                                <small class="text-danger" id="errorCorregirSalidaMotivo" style="display:none;"></small>
                            </div>
                            <div class="salida-edit-actions">
                                <button type="button" class="btn btn-sm btn-light" onclick="toggleCorregirSalida()">Cancelar</button>
                                <button type="button" class="btn btn-sm btn-primary" id="btnGuardarCorregirSalida" onclick="corregirSalidaDesdeModal()">
                                    <i class="fas fa-save mr-1"></i>Guardar corrección
                                </button>
                            </div>
                        </div>
                    </div>

                    <div class="detail-section rf-section">
                        <h6 class="detail-section-title">
                            <i class="fas fa-calculator mr-2"></i>Resumen Financiero
                        </h6>
                        <div class="rf-grid">
                            <div class="rf-cell rf-ingresos">
                                <div class="rf-cell-header">
                                    <i class="fas fa-arrow-up rf-icon-up"></i>
                                    <span class="rf-cell-label">Ingresos</span>
                                </div>
                                <div class="rf-amounts">
                                    <div class="rf-amount-row">
                                        <span class="rf-currency">S/</span>
                                        <span class="rf-number" id="detalleIngresosSoles">0.00</span>
                                    </div>
                                    <div class="rf-amount-row rf-amount-secondary">
                                        <span class="rf-currency">$</span>
                                        <span class="rf-number" id="detalleIngresosDolares">0.00</span>
                                    </div>
                                </div>
                            </div>
                            <div class="rf-cell rf-gastos">
                                <div class="rf-cell-header">
                                    <i class="fas fa-arrow-down rf-icon-down"></i>
                                    <span class="rf-cell-label">Gastos</span>
                                </div>
                                <div class="rf-amounts">
                                    <div class="rf-amount-row">
                                        <span class="rf-currency">S/</span>
                                        <span class="rf-number" id="detalleGastosSoles">0.00</span>
                                    </div>
                                    <div class="rf-amount-row rf-amount-secondary">
                                        <span class="rf-currency">$</span>
                                        <span class="rf-number" id="detalleGastosDolares">0.00</span>
                                    </div>
                                </div>
                            </div>
                        </div>
                        <div class="rf-balance-bar" id="rfBalanceBar">
                            <div class="rf-balance-label">
                                <i class="fas fa-balance-scale mr-2"></i>Balance Final
                            </div>
                            <div class="rf-balance-amounts">
                                <div class="rf-bal-item">
                                    <span class="rf-bal-currency">S/</span>
                                    <span class="rf-bal-number" id="detalleBalanceSoles">0.00</span>
                                </div>
                                <div class="rf-bal-divider"></div>
                                <div class="rf-bal-item">
                                    <span class="rf-bal-currency">$</span>
                                    <span class="rf-bal-number" id="detalleBalanceDolares">0.00</span>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div class="detail-section">
                        <h6 class="detail-section-title">
                            <i class="fas fa-hand-holding-usd mr-2"></i>Desglose de Ingresos
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
                                <tbody id="detalleIngresosBody">
                                </tbody>
                            </table>
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

                    <div class="detail-section" id="sectionAjustes">
                        <h6 class="detail-section-title">
                            <i class="fas fa-sliders-h mr-2"></i>Ajustes Administrativos
                            <span class="badge badge-warning ml-2" style="font-size:0.7rem;font-weight:600;">Solo Admin</span>
                        </h6>
                        <div class="alert-warning-light mb-3" style="font-size:0.85rem;padding:0.75rem;border:1px solid #fde68a;border-radius:0.375rem;background:#fffbeb;">
                            <i class="fas fa-info-circle mr-1"></i>
                            Registra descuentos o reintegros a aplicar al balance del conductor. Deja en cero si no aplica.
                        </div>
                        <div class="row">
                            <div class="col-md-6 mb-3">
                                <div class="ajuste-card ajuste-descuento">
                                    <div class="ajuste-header">
                                        <i class="fas fa-arrow-down mr-2 text-danger"></i>Descuento al Conductor
                                    </div>
                                    <div class="row mt-2">
                                        <div class="col-6">
                                            <label class="form-label">Soles (S/)</label>
                                            <input type="number" id="ajusteDescuentoSoles" class="form-control form-control-sm"
                                                placeholder="0.00" step="0.01" min="0" value=""
                                                oninput="actualizarBalanceAjustado()">
                                        </div>
                                        <div class="col-6">
                                            <label class="form-label">Dólares ($)</label>
                                            <input type="number" id="ajusteDescuentoDolares" class="form-control form-control-sm"
                                                placeholder="0.00" step="0.01" min="0" value=""
                                                oninput="actualizarBalanceAjustado()">
                                        </div>
                                    </div>
                                </div>
                            </div>
                            <div class="col-md-6 mb-3">
                                <div class="ajuste-card ajuste-reintegro">
                                    <div class="ajuste-header">
                                        <i class="fas fa-arrow-up mr-2 text-success"></i>Reintegro al Conductor
                                    </div>
                                    <div class="row mt-2">
                                        <div class="col-6">
                                            <label class="form-label">Soles (S/)</label>
                                            <input type="number" id="ajusteReintegroSoles" class="form-control form-control-sm"
                                                placeholder="0.00" step="0.01" min="0" value=""
                                                oninput="actualizarBalanceAjustado()">
                                        </div>
                                        <div class="col-6">
                                            <label class="form-label">Dólares ($)</label>
                                            <input type="number" id="ajusteReintegroDolares" class="form-control form-control-sm"
                                                placeholder="0.00" step="0.01" min="0" value=""
                                                oninput="actualizarBalanceAjustado()">
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                        <div id="balanceAjustadoPanel" class="mt-2" style="display:none;">
                            <div class="financial-card financial-card-neutral">
                                <div class="financial-header">
                                    <i class="fas fa-balance-scale"></i>
                                    <span>Balance Ajustado (con descuentos/reintegros)</span>
                                </div>
                                <div class="financial-amounts">
                                    <div class="amount-row">
                                        <span class="currency">S/</span>
                                        <span class="amount balance-amount" id="balanceAjustadoSoles">0.00</span>
                                    </div>
                                    <div class="amount-row">
                                        <span class="currency">$</span>
                                        <span class="amount balance-amount" id="balanceAjustadoDolares">0.00</span>
                                    </div>
                                </div>
                            </div>
                        </div>
                        <div class="form-group mt-3" id="notaAprobacionGroup">
                            <label class="form-label">
                                <i class="fas fa-comment-alt mr-1 text-muted"></i>Nota de Aprobación
                                <span class="text-muted font-weight-normal">(opcional)</span>
                            </label>
                            <textarea id="notaAprobacion" class="form-control" rows="2" maxlength="500"
                                placeholder="Ej: Aprobado. Comprobantes revisados y conformes."></textarea>
                            <small class="text-muted">Máximo 500 caracteres. Se adjuntará a la liquidación como observación administrativa.</small>
                        </div>
                    </div>

                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary-custom" data-dismiss="modal">Cerrar</button>
                    <button type="button" class="btn btn-primary-custom" onclick="imprimirDetalle()">
                        <i class="fas fa-print mr-1"></i>Imprimir
                    </button>
                    <button type="button" id="btnAprobarDesdeModal" class="btn btn-success-custom"
                        onclick="aprobarDesdeModal()" style="display:none;">
                        <i class="fas fa-check mr-1"></i>Aprobar Liquidación
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
                        <textarea id="rechazarObservaciones" name="observacionesRechazo" class="form-control" rows="4" 
                            placeholder="Explique detalladamente qué debe corregir el conductor...&#13;&#10;Ejemplo: Los montos de peajes no coinciden con los comprobantes adjuntos. Por favor revisar."></textarea>
                        <small id="errorRechazoObservaciones" class="text-danger d-none"></small>
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

    <!-- Modal Revertir Aprobación -->
    <div class="modal fade" id="modalRevertir" tabindex="-1">
        <div class="modal-dialog">
            <div class="modal-content">
                <div class="modal-header modal-header-danger">
                    <h5 class="modal-title">
                        <i class="fas fa-undo mr-2"></i>Revertir Aprobación
                    </h5>
                    <button type="button" class="close" data-dismiss="modal"><span>&times;</span></button>
                </div>
                <div class="modal-body">
                    <div class="alert alert-danger">
                        <i class="fas fa-exclamation-triangle mr-2"></i>
                        <strong>Atención:</strong> Esta acción devolverá la liquidación al estado
                        <strong>Pendiente de Aprobación</strong>. Deberá ser revisada y aprobada nuevamente.
                    </div>
                    <input type="hidden" id="revertirIdOrden" />
                    <div class="form-group">
                        <label class="form-label">N° de Orden:</label>
                        <input type="text" id="revertirNumeroOrden" class="form-control" readonly />
                    </div>
                    <div class="form-group">
                        <label class="form-label">Conductor:</label>
                        <input type="text" id="revertirConductor" class="form-control" readonly />
                    </div>
                    <div class="form-group">
                        <label class="form-label">Motivo de la Reversión <span class="text-danger">*</span></label>
                        <textarea id="revertirMotivo" class="form-control" rows="4"
                            placeholder="Explique por qué se revierte esta aprobación...&#13;&#10;Ej: Se detectó que el descuento aplicado es incorrecto y necesita re-evaluación."></textarea>
                        <small id="errorRevertirMotivo" class="text-danger d-none"></small>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary-custom" data-dismiss="modal">Cancelar</button>
                    <button type="button" class="btn btn-danger-custom" id="btnConfirmarReversion" onclick="confirmarReversion()">
                        <i class="fas fa-undo mr-1"></i>Confirmar Reversión
                    </button>
                </div>
            </div>
        </div>
    </div>

    <!-- Modal Corregir Ajustes -->
    <div class="modal fade" id="modalCorregirAjustes" tabindex="-1">
        <div class="modal-dialog modal-lg">
            <div class="modal-content">
                <div class="modal-header modal-header-corregir">
                    <h5 class="modal-title">
                        <i class="fas fa-edit mr-2"></i>Corregir Ajustes de Liquidación Aprobada
                    </h5>
                    <button type="button" class="close" data-dismiss="modal"><span>&times;</span></button>
                </div>
                <div class="modal-body">
                    <div class="alert alert-warning">
                        <i class="fas fa-exclamation-triangle mr-2"></i>
                        Está modificando los ajustes de una liquidación <strong>ya aprobada</strong>.
                        Los cambios se reflejarán inmediatamente en los reportes.
                    </div>
                    <input type="hidden" id="corregirIdOrden" />
                    <div class="row mb-3">
                        <div class="col-md-6">
                            <label class="form-label">N° Orden:</label>
                            <input type="text" id="corregirNumeroOrden" class="form-control" readonly />
                        </div>
                        <div class="col-md-6">
                            <label class="form-label">Conductor:</label>
                            <input type="text" id="corregirConductor" class="form-control" readonly />
                        </div>
                    </div>

                    <div class="valores-actuales-panel mb-3">
                        <div class="vap-title">
                            <i class="fas fa-history mr-1"></i>Valores Registrados Actualmente
                        </div>
                        <div class="vap-grid">
                            <div class="vap-item vap-descuento">
                                <span class="vap-label">Descuento S/</span>
                                <span class="vap-number" id="corregirDescSolesActual">0.00</span>
                            </div>
                            <div class="vap-item vap-descuento">
                                <span class="vap-label">Descuento $</span>
                                <span class="vap-number" id="corregirDescDolaresActual">0.00</span>
                            </div>
                            <div class="vap-item vap-reintegro">
                                <span class="vap-label">Reintegro S/</span>
                                <span class="vap-number" id="corregirReintSolesActual">0.00</span>
                            </div>
                            <div class="vap-item vap-reintegro">
                                <span class="vap-label">Reintegro $</span>
                                <span class="vap-number" id="corregirReintDolaresActual">0.00</span>
                            </div>
                        </div>
                        <div class="vap-hint"><i class="fas fa-arrow-down mr-1"></i>Ingrese los nuevos valores a continuación</div>
                    </div>

                    <div class="row">
                        <div class="col-md-6 mb-3">
                            <div class="ajuste-card ajuste-descuento">
                                <div class="ajuste-header">
                                    <i class="fas fa-arrow-down mr-2 text-danger"></i>Nuevo Descuento
                                </div>
                                <div class="row mt-2">
                                    <div class="col-6">
                                        <label class="form-label">Soles (S/)</label>
                                        <input type="number" id="corregirDescSoles" class="form-control form-control-sm"
                                            placeholder="0.00" step="0.01" min="0">
                                    </div>
                                    <div class="col-6">
                                        <label class="form-label">Dólares ($)</label>
                                        <input type="number" id="corregirDescDolares" class="form-control form-control-sm"
                                            placeholder="0.00" step="0.01" min="0">
                                    </div>
                                </div>
                            </div>
                        </div>
                        <div class="col-md-6 mb-3">
                            <div class="ajuste-card ajuste-reintegro">
                                <div class="ajuste-header">
                                    <i class="fas fa-arrow-up mr-2 text-success"></i>Nuevo Reintegro
                                </div>
                                <div class="row mt-2">
                                    <div class="col-6">
                                        <label class="form-label">Soles (S/)</label>
                                        <input type="number" id="corregirReintSoles" class="form-control form-control-sm"
                                            placeholder="0.00" step="0.01" min="0">
                                    </div>
                                    <div class="col-6">
                                        <label class="form-label">Dólares ($)</label>
                                        <input type="number" id="corregirReintDolares" class="form-control form-control-sm"
                                            placeholder="0.00" step="0.01" min="0">
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div class="form-group">
                        <label class="form-label">Motivo de la Corrección <span class="text-danger">*</span></label>
                        <textarea id="corregirMotivo" class="form-control" rows="3"
                            placeholder="Explique por qué se corrigen los ajustes..."></textarea>
                        <small id="errorCorregirMotivo" class="text-danger d-none"></small>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary-custom" data-dismiss="modal">Cancelar</button>
                    <button type="button" class="btn btn-primary-custom" id="btnConfirmarCorreccion" onclick="confirmarCorreccion()">
                        <i class="fas fa-save mr-1"></i>Guardar Corrección
                    </button>
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
            font-size: 0.9375rem;
            font-weight: 600;
            letter-spacing: -0.01em;
            color: #1e293b;
            margin-bottom: 1rem;
            padding-bottom: 0.6rem;
            border-bottom: 1px solid var(--border-color);
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

        .btn-expand {
            background: none;
            border: 1px solid var(--border-color);
            border-radius: 0.25rem;
            padding: 0.1rem 0.4rem;
            font-size: 0.7rem;
            color: var(--neutral-color);
            cursor: pointer;
            margin-left: 0.5rem;
            line-height: 1.2;
            vertical-align: middle;
            transition: all 0.15s;
        }
        .btn-expand:hover { background: var(--medium-gray); color: var(--primary-color); }

        .detail-sub-content {
            padding: 0.75rem 1rem;
            background: #f8f9fa;
            border-top: 1px solid var(--medium-gray);
        }
        .detail-desc {
            font-size: 0.8125rem;
            color: var(--neutral-color);
            font-style: italic;
            margin-bottom: 0.5rem;
        }
        .table-sub {
            font-size: 0.8rem;
            margin-bottom: 0 !important;
        }
        .table-sub thead th {
            background: var(--medium-gray);
            font-size: 0.75rem;
            padding: 0.4rem 0.5rem;
            font-weight: 600;
        }
        .table-sub tbody td {
            padding: 0.35rem 0.5rem;
            border-color: var(--medium-gray);
        }

        .ajuste-card {
            border: 2px solid var(--border-color);
            border-radius: 0.5rem;
            padding: 1rem;
            height: 100%;
        }

        .ajuste-descuento {
            background: linear-gradient(135deg, #fee2e2 0%, #fecaca 100%);
            border-color: #fca5a5;
        }

        .ajuste-reintegro {
            background: linear-gradient(135deg, #d1fae5 0%, #a7f3d0 100%);
            border-color: #86efac;
        }

        .ajuste-header {
            font-weight: 600;
            font-size: 0.95rem;
            margin-bottom: 0.25rem;
        }

        .btn-success-custom {
            background: var(--success-color);
            border: none;
            color: white;
            font-weight: 500;
            padding: 0.5rem 1rem;
        }

        .btn-success-custom:hover {
            background: #047857;
            color: white;
        }

        /* === TABS DE NAVEGACIÓN === */
        .tabs-navigation {
            display: flex;
            gap: 0;
            background: white;
            border: 1px solid var(--border-color);
            border-radius: 0.5rem;
            overflow: hidden;
        }

        .tab-btn {
            flex: 1;
            padding: 0.875rem 1.25rem;
            border: none;
            background: white;
            color: var(--neutral-color);
            font-weight: 600;
            font-size: 0.9375rem;
            cursor: pointer;
            transition: all 0.2s;
            border-bottom: 3px solid transparent;
        }

        .tab-btn:hover {
            background: var(--light-gray);
            color: var(--primary-color);
        }

        .tab-btn-active {
            color: var(--primary-color);
            border-bottom-color: var(--primary-color);
            background: #eff6ff;
        }

        .section-header-success {
            background: #d1fae5;
            border-bottom-color: #86efac;
        }

        .stat-card.stat-success-custom {
            border-color: #86efac;
            background: linear-gradient(135deg, #d1fae5 0%, #a7f3d0 100%);
        }

        .stat-card.stat-success-custom .stat-icon {
            color: #065f46;
        }

        .badge-aprobada {
            background: #d1fae5;
            color: #065f46;
            padding: 0.5rem 0.75rem;
            border-radius: 0.375rem;
            font-weight: 600;
            font-size: 0.75rem;
        }

        .btn-revert-action {
            padding: 0.5rem 0.75rem;
            border: none;
            border-radius: 0.375rem;
            font-size: 0.875rem;
            cursor: pointer;
            transition: all 0.2s;
            color: white;
            background: #7c3aed;
        }

        .btn-revert-action:hover {
            background: #6d28d9;
            transform: translateY(-1px);
        }

        /* === AUTOCOMPLETE CONDUCTOR === */
        .conductor-autocomplete-wrap {
            position: relative;
        }

        .conductor-autocomplete-dropdown {
            position: absolute;
            top: 100%;
            left: 0;
            right: 0;
            background: #fff;
            border: 1px solid var(--border-color);
            border-top: none;
            border-radius: 0 0 0.375rem 0.375rem;
            box-shadow: 0 4px 12px rgba(0,0,0,0.1);
            z-index: 1050;
            max-height: 220px;
            overflow-y: auto;
            display: none;
        }

        .conductor-autocomplete-dropdown .autocomplete-item {
            padding: 0.5rem 0.75rem;
            cursor: pointer;
            font-size: 0.875rem;
            color: #1e293b;
            border-bottom: 1px solid var(--medium-gray);
            transition: background 0.15s;
        }

        .conductor-autocomplete-dropdown .autocomplete-item:last-child {
            border-bottom: none;
        }

        .conductor-autocomplete-dropdown .autocomplete-item:hover,
        .conductor-autocomplete-dropdown .autocomplete-item.active {
            background: #eff6ff;
            color: var(--primary-color);
        }

        .conductor-autocomplete-dropdown .autocomplete-no-results {
            padding: 0.5rem 0.75rem;
            font-size: 0.875rem;
            color: var(--neutral-color);
            font-style: italic;
        }

        .conductor-selected-badge {
            display: inline-flex;
            align-items: center;
            background: #dbeafe;
            color: #1e40af;
            border-radius: 0.25rem;
            padding: 0.1rem 0.4rem;
            font-size: 0.75rem;
            font-weight: 600;
            margin-top: 0.25rem;
        }

        .conductor-selected-badge .clear-selection {
            cursor: pointer;
            margin-left: 0.35rem;
            font-size: 0.85rem;
            line-height: 1;
        }

        /* === MODAL HEADER CORREGIR === */
        .modal-header-corregir {
            background: #fef3c7;
            border-bottom: 2px solid #fbbf24;
        }

        /* === MODAL DETAIL: MODE INDICATOR === */
        .modal-orden-ref {
            font-size: 0.9rem;
            font-weight: 600;
            color: var(--neutral-color);
        }

        .badge-modo-aprobacion {
            display: inline-flex;
            align-items: center;
            background: #059669;
            color: white;
            padding: 0.3rem 0.75rem;
            border-radius: 0.375rem;
            font-size: 0.75rem;
            font-weight: 700;
            letter-spacing: 0.05em;
            white-space: nowrap;
            flex-shrink: 0;
        }

        #modalDetalleLiquidacionHeader.mode-aprobacion {
            background: #d1fae5;
            border-bottom: 2px solid #86efac;
        }

        /* === APPROVAL GUIDANCE BANNER === */
        .approval-guidance-banner {
            background: linear-gradient(135deg, #ecfdf5 0%, #d1fae5 100%);
            border: 1px solid #86efac;
            border-left: 4px solid #059669;
            border-radius: 0.375rem;
            padding: 0.875rem 1rem;
            margin-bottom: 1.25rem;
        }

        .agb-content {
            display: flex;
            align-items: flex-start;
            gap: 0.75rem;
        }

        .agb-icon {
            color: #059669;
            font-size: 1.2rem;
            flex-shrink: 0;
            margin-top: 0.1rem;
        }

        .agb-text {
            display: flex;
            flex-direction: column;
            gap: 0.15rem;
            font-size: 0.875rem;
            line-height: 1.5;
        }

        .agb-text strong {
            color: #065f46;
            font-size: 0.9rem;
        }

        .agb-text span {
            color: #374151;
        }

        .agb-link {
            color: #065f46;
            font-weight: 600;
            text-decoration: underline;
        }

        .agb-link:hover {
            color: #059669;
            text-decoration: none;
        }

        /* === VALORES ACTUALES PANEL (modal corregir) === */
        .valores-actuales-panel {
            background: var(--light-gray);
            border: 1px solid var(--border-color);
            border-radius: 0.375rem;
            padding: 0.875rem 1rem;
        }

        .vap-title {
            font-size: 0.8125rem;
            font-weight: 600;
            color: var(--neutral-color);
            text-transform: uppercase;
            letter-spacing: 0.03em;
            margin-bottom: 0.75rem;
        }

        .vap-grid {
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 0.625rem;
        }

        .vap-item {
            background: white;
            border: 1px solid var(--border-color);
            border-radius: 0.375rem;
            padding: 0.5rem 0.625rem;
            text-align: center;
        }

        .vap-label {
            display: block;
            font-size: 0.7rem;
            font-weight: 600;
            text-transform: uppercase;
            color: var(--neutral-color);
            letter-spacing: 0.03em;
            margin-bottom: 0.2rem;
        }

        .vap-number {
            font-size: 1.0625rem;
            font-weight: 700;
            font-variant-numeric: tabular-nums;
            color: #1e293b;
        }

        .vap-descuento {
            border-color: #fca5a5;
        }

        .vap-descuento .vap-number {
            color: var(--danger-color);
        }

        .vap-reintegro {
            border-color: #86efac;
        }

        .vap-reintegro .vap-number {
            color: var(--success-color);
        }

        .vap-hint {
            font-size: 0.75rem;
            color: var(--neutral-color);
            margin-top: 0.5rem;
            font-style: italic;
        }

        /* === VIAJE INFO STRIP === */
        .viaje-info-section {
            padding: 1rem 1.25rem;
        }

        .viaje-info-strip {
            display: flex;
            align-items: center;
            flex-wrap: wrap;
            gap: 0;
        }

        .vi-item {
            display: flex;
            flex-direction: column;
            gap: 0.15rem;
            padding: 0.5rem 1.125rem;
            min-width: 0;
        }

        .vi-item-conductor {
            padding-left: 0;
            flex: 1.5;
        }

        .vi-item:not(.vi-item-conductor) {
            flex: 1;
        }

        .vi-label {
            font-size: 0.7rem;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.05em;
            color: var(--neutral-color);
            white-space: nowrap;
        }

        .vi-value {
            font-size: 0.9375rem;
            font-weight: 600;
            color: #1e293b;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }

        .vi-value-periodo {
            font-size: 0.8125rem;
        }

        .vi-sep {
            width: 1px;
            height: 2.5rem;
            background: var(--border-color);
            flex-shrink: 0;
        }

        .vi-obs-row {
            display: flex;
            align-items: baseline;
            gap: 0.5rem;
            margin-top: 0.75rem;
            padding-top: 0.75rem;
            border-top: 1px dashed var(--border-color);
            font-size: 0.875rem;
        }

        .vi-obs-icon {
            color: var(--neutral-color);
            font-size: 0.8rem;
            flex-shrink: 0;
        }

        .vi-obs-label {
            font-weight: 700;
            color: var(--neutral-color);
            white-space: nowrap;
            font-size: 0.75rem;
            text-transform: uppercase;
            letter-spacing: 0.04em;
        }

        .vi-obs-text {
            color: #374151;
            font-style: italic;
        }

        /* === SALIDA REGISTRADA / CORRECCIÓN === */
        .salida-section {
            padding: 1rem 1.25rem;
        }

        .salida-head {
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 1rem;
            flex-wrap: wrap;
        }

        .salida-info {
            display: flex;
            flex-direction: column;
            gap: 0.25rem;
            min-width: 0;
        }

        .salida-label {
            font-size: 0.7rem;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.05em;
            color: var(--neutral-color);
        }

        .salida-value {
            display: inline-flex;
            align-items: center;
            gap: 0.5rem;
            font-size: 1.0625rem;
            font-weight: 600;
            color: #0f172a;
            font-variant-numeric: tabular-nums;
        }

        .salida-value-icon {
            color: var(--neutral-color);
            font-size: 0.9rem;
        }

        .btn-corregir-salida {
            display: inline-flex;
            align-items: center;
            background: #fff;
            border: 1px solid var(--border-color);
            color: #475569;
            font-size: 0.8125rem;
            font-weight: 600;
            padding: 0.4rem 0.85rem;
            border-radius: 0.5rem;
            transition: border-color .15s ease, color .15s ease, box-shadow .15s ease;
            white-space: nowrap;
        }

        .btn-corregir-salida:hover {
            border-color: #94a3b8;
            color: #0f172a;
            box-shadow: 0 1px 2px rgba(15, 23, 42, .08);
        }

        .salida-edit-panel {
            margin-top: 1rem;
            padding-top: 1rem;
            border-top: 1px dashed var(--border-color);
        }

        .salida-edit-actions {
            display: flex;
            justify-content: flex-end;
            gap: 0.5rem;
        }

        /* === RESUMEN FINANCIERO GRID === */
        .rf-section {
            padding: 1.25rem 1.25rem 0;
        }

        .rf-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 0.75rem;
            margin-bottom: 0.75rem;
        }

        .rf-cell {
            border-radius: 0.5rem;
            padding: 0.875rem 1rem;
            border: 1px solid transparent;
        }

        .rf-ingresos {
            background: #f0fdf4;
            border-color: #bbf7d0;
        }

        .rf-gastos {
            background: #fff1f2;
            border-color: #fecdd3;
        }

        .rf-cell-header {
            display: flex;
            align-items: center;
            gap: 0.5rem;
            margin-bottom: 0.625rem;
        }

        .rf-cell-label {
            font-size: 0.75rem;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.05em;
            color: var(--neutral-color);
        }

        .rf-icon-up { color: #16a34a; font-size: 0.875rem; }
        .rf-icon-down { color: #dc2626; font-size: 0.875rem; }

        .rf-amounts {
            display: flex;
            flex-direction: column;
            gap: 0.2rem;
        }

        .rf-amount-row {
            display: flex;
            align-items: baseline;
            gap: 0.3rem;
        }

        .rf-currency {
            font-size: 0.75rem;
            font-weight: 700;
            color: var(--neutral-color);
            width: 1.2rem;
            flex-shrink: 0;
        }

        .rf-number {
            font-size: 1.125rem;
            font-weight: 700;
            font-variant-numeric: tabular-nums;
            color: #1e293b;
        }

        .rf-amount-secondary .rf-number {
            font-size: 0.9375rem;
            color: #475569;
        }

        .rf-amount-secondary .rf-currency {
            color: #94a3b8;
        }

        /* Balance bar */
        .rf-balance-bar {
            display: flex;
            align-items: center;
            justify-content: space-between;
            background: #1e293b;
            color: white;
            border-radius: 0.5rem;
            padding: 0.875rem 1.25rem;
            margin-bottom: 1.25rem;
        }

        .rf-balance-label {
            font-size: 0.8125rem;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.05em;
            opacity: 0.8;
            white-space: nowrap;
        }

        .rf-balance-amounts {
            display: flex;
            align-items: center;
            gap: 1rem;
        }

        .rf-bal-item {
            display: flex;
            align-items: baseline;
            gap: 0.25rem;
        }

        .rf-bal-currency {
            font-size: 0.8125rem;
            font-weight: 700;
            opacity: 0.65;
        }

        .rf-bal-number {
            font-size: 1.375rem;
            font-weight: 800;
            font-variant-numeric: tabular-nums;
        }

        .rf-bal-divider {
            width: 1px;
            height: 1.75rem;
            background: rgba(255,255,255,0.2);
        }
    </style>

    <script src="https://code.jquery.com/jquery-3.6.0.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@4.6.0/dist/js/bootstrap.bundle.min.js"></script>

    <script>
        let idOrdenParaAprobar = 0;
        let _idOrdenDetalleActual = 0;
        let modoAprobacion = false;
        let _balanceSoles = 0;
        let _balanceDolares = 0;

        function verDetalleLiquidacion(idOrdenViaje) {
            console.log('Ver detalle:', idOrdenViaje);

            $('#btnAprobarDesdeModal').hide().prop('disabled', false).html('<i class="fas fa-check mr-1"></i>Aprobar Liquidación');
            $('#detalleLoading').show();
            $('#detalleError').hide();
            $('#approvalBanner').hide();
            $('#modalModeBadge').hide();
            $('#modalDetalleLiquidacionHeader').removeClass('mode-aprobacion');
            $('#modalDetalleLiquidacion .detail-section').hide();
            $('#modalNumeroOrden').text('');
            $('#modalDetalleLiquidacion').modal('show');

            $.ajax({
                type: "POST",
                url: "LiquidacionesPendientes.aspx/ObtenerDetalleLiquidacion",
                data: JSON.stringify({ idOrdenViaje: idOrdenViaje }),
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                success: function (response) {
                    mostrarDetalle(response.d);
                },
                error: function (xhr, status, error) {
                    console.error('Error:', error);
                    $('#detalleLoading').hide();
                    $('#detalleError').show();
                }
            });
        }

        function toggleDetail(btn) {
            var $btn = $(btn);
            var $subRow = $btn.closest('tr').next('.detail-sub-row');
            $subRow.toggle();
            $btn.find('i').toggleClass('fa-chevron-down fa-chevron-up');
        }

        function buildSubTablePeajes(items) {
            if (!items || items.length === 0) return '';
            var html = '<table class="table table-sub table-bordered mb-0">' +
                '<thead><tr><th>Estaci&oacute;n</th><th>Fecha</th><th>Comprobante</th>' +
                '<th class="text-right">S/</th><th class="text-right">$</th><th>Observaciones</th></tr></thead><tbody>';
            items.forEach(function (item) {
                html += '<tr>' +
                    '<td>' + (item.Estacion || '-') + '</td>' +
                    '<td>' + (item.Fecha || '-') + '</td>' +
                    '<td>' + (item.Comprobante || '-') + '</td>' +
                    '<td class="text-right">' + parseFloat(item.Soles || 0).toFixed(2) + '</td>' +
                    '<td class="text-right">' + parseFloat(item.Dolares || 0).toFixed(2) + '</td>' +
                    '<td>' + (item.Observaciones || '-') + '</td>' +
                    '</tr>';
            });
            html += '</tbody></table>';
            return html;
        }

        function buildSubTableGenerica(items) {
            if (!items || items.length === 0) return '';
            var html = '<table class="table table-sub table-bordered mb-0">' +
                '<thead><tr><th>Fecha</th><th>Comprobante</th>' +
                '<th class="text-right">S/</th><th class="text-right">$</th><th>Observaciones</th></tr></thead><tbody>';
            items.forEach(function (item) {
                html += '<tr>' +
                    '<td>' + (item.Fecha || '-') + '</td>' +
                    '<td>' + (item.Comprobante || '-') + '</td>' +
                    '<td class="text-right">' + parseFloat(item.Soles || 0).toFixed(2) + '</td>' +
                    '<td class="text-right">' + parseFloat(item.Dolares || 0).toFixed(2) + '</td>' +
                    '<td>' + (item.Observaciones || '-') + '</td>' +
                    '</tr>';
            });
            html += '</tbody></table>';
            return html;
        }

        function makeRow(icon, iconClass, label, soles, dolares, desc, subTable) {
            var hasDetail = (desc && desc.trim()) || (subTable && subTable.trim());
            var expandBtn = hasDetail
                ? ' <button type="button" class="btn btn-expand ml-1" onclick="toggleDetail(this)" title="Ver detalle">' +
                  '<i class="fas fa-chevron-down"></i></button>'
                : '';

            var mainRow = '<tr>' +
                '<td><i class="fas ' + icon + ' ' + iconClass + ' mr-2"></i>' + label + expandBtn + '</td>' +
                '<td class="text-right">S/ ' + parseFloat(soles || 0).toFixed(2) + '</td>' +
                '<td class="text-right">$ ' + parseFloat(dolares || 0).toFixed(2) + '</td>' +
                '</tr>';

            if (!hasDetail) return mainRow;

            var subContent = '';
            if (desc && desc.trim()) {
                subContent += '<div class="detail-desc"><strong>Descripci&oacute;n:</strong> ' +
                    $('<span>').text(desc).html() + '</div>';
            }
            if (subTable && subTable.trim()) {
                subContent += '<div class="detail-sub-content">' + subTable + '</div>';
            }

            var subRow = '<tr class="detail-sub-row" style="display:none;">' +
                '<td colspan="3" class="p-0 border-top-0">' + subContent + '</td>' +
                '</tr>';

            return mainRow + subRow;
        }

        function mostrarDetalle(datos) {
            if (!datos) {
                $('#detalleLoading').hide();
                $('#detalleError').show();
                return;
            }

            $('#modalNumeroOrden').text(datos.NumeroOrdenViaje);
            $('#detalleConductor').text(datos.NombreConductor);
            $('#detalleTracto').text(datos.PlacaTracto);
            $('#detalleCarreta').text(datos.PlacaCarreta);
            $('#detallePeriodo').text(datos.FechaSalida + ' al ' + datos.FechaLlegada);

            // Corrección de salida: solo disponible mientras la liquidación esté PENDIENTE.
            _idOrdenDetalleActual = datos.IdOrdenViaje;
            $('#panelCorregirSalida').hide();
            $('#corregirSalidaMotivo').val('');
            $('#errorCorregirSalidaMotivo').hide();
            if (datos.EstadoAprobacion === 'PENDIENTE') {
                var horaTxt = datos.HoraSalida ? datos.HoraSalida + ' h' : 'sin hora';
                $('#detalleSalidaValor').text(datos.FechaSalida + '  ·  ' + horaTxt);
                $('#corregirSalidaFecha').val(datos.FechaSalidaISO || '');
                $('#corregirSalidaHora').val(datos.HoraSalida || '');
                $('#seccionCorregirSalida').show();
            } else {
                $('#seccionCorregirSalida').hide();
            }

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

            var balanceSoles = parseFloat(datos.TotalIngresosSoles) - parseFloat(datos.TotalGastosSoles);
            var balanceDolares = parseFloat(datos.TotalIngresosDolares) - parseFloat(datos.TotalGastosDolares);

            _balanceSoles = balanceSoles;
            _balanceDolares = balanceDolares;

            var balPositivo = balanceSoles >= 0 && balanceDolares >= 0;
            var balNegativo = balanceSoles < 0 || balanceDolares < 0;
            var balColor = balNegativo ? '#7f1d1d' : '#064e3b';
            var balBg = balNegativo
                ? 'linear-gradient(135deg, #7f1d1d 0%, #991b1b 100%)'
                : 'linear-gradient(135deg, #064e3b 0%, #065f46 100%)';

            $('#detalleBalanceSoles').text(balanceSoles.toFixed(2))
                .css('color', balanceSoles >= 0 ? '#6ee7b7' : '#fca5a5');
            $('#detalleBalanceDolares').text(balanceDolares.toFixed(2))
                .css('color', balanceDolares >= 0 ? '#6ee7b7' : '#fca5a5');
            $('#rfBalanceBar').css('background', balBg);

            // --- Desglose de Ingresos ---
            var ingresosItems = [
                { key: 'Despacho',      label: 'Flete / Despacho', descKey: 'DescDespacho' },
                { key: 'Prestamo',      label: 'Pr&eacute;stamo',  descKey: 'DescPrestamo' },
                { key: 'Mensualidad',   label: 'Mensualidad',      descKey: 'DescMensualidad' },
                { key: 'OtrosIngresos', label: 'Otros Ingresos',   descKey: 'DescOtros' }
            ];

            var ingresosHtml = '';
            ingresosItems.forEach(function (item) {
                var soles = parseFloat(datos[item.key + 'Soles'] || 0);
                var dolares = parseFloat(datos[item.key + 'Dolares'] || 0);
                if (soles > 0 || dolares > 0) {
                    var desc = datos[item.descKey] || '';
                    ingresosHtml += makeRow('fa-plus-circle', 'text-success', item.label, soles, dolares, desc, '');
                }
            });

            if (datos.DetallesIngresosAdicionales && datos.DetallesIngresosAdicionales.length > 0) {
                datos.DetallesIngresosAdicionales.forEach(function (item) {
                    var nombre = item.Nombre || 'Ingreso Adicional';
                    ingresosHtml += makeRow('fa-plus-circle', 'text-success', nombre,
                        item.Soles, item.Dolares, item.Descripcion || '', '');
                });
            }

            if (!ingresosHtml) {
                ingresosHtml = '<tr><td colspan="3" class="text-center text-muted">No hay ingresos registrados</td></tr>';
            }
            $('#detalleIngresosBody').html(ingresosHtml);

            // --- Desglose de Gastos ---
            var gastosItems = [
                { key: 'Peajes',         label: 'Peajes',                   descKey: 'DescPeajes',         detailKey: 'DetallesPeajes',       detailType: 'peajes' },
                { key: 'Alimentacion',   label: 'Alimentaci&oacute;n',      descKey: 'DescAlimentacion',   detailKey: '',                     detailType: '' },
                { key: 'ApoyoSeguridad', label: 'Apoyo de Seguridad',       descKey: 'DescApoyoSeguridad', detailKey: '',                     detailType: '' },
                { key: 'Reparaciones',   label: 'Reparaciones y Varios',    descKey: 'DescReparaciones',   detailKey: 'DetallesReparaciones', detailType: 'generica' },
                { key: 'Movilidad',      label: 'Movilidad',                descKey: 'DescMovilidad',      detailKey: '',                     detailType: '' },
                { key: 'Encarpada',      label: 'Encarpada / Desencarpada', descKey: 'DescEncarpada',      detailKey: '',                     detailType: '' },
                { key: 'Hospedaje',      label: 'Hospedaje',                descKey: 'DescHospedaje',      detailKey: 'DetallesHospedaje',    detailType: 'generica' },
                { key: 'Combustible',    label: 'Combustible',              descKey: 'DescCombustible',    detailKey: 'DetallesCombustible',  detailType: 'generica' }
            ];

            var gastosHtml = '';
            gastosItems.forEach(function (item) {
                var soles = parseFloat(datos['Gastos' + item.key + 'Soles'] || 0);
                var dolares = parseFloat(datos['Gastos' + item.key + 'Dolares'] || 0);
                if (soles > 0 || dolares > 0) {
                    var desc = datos[item.descKey] || '';
                    var subTable = '';
                    if (item.detailKey && datos[item.detailKey] && datos[item.detailKey].length > 0) {
                        subTable = item.detailType === 'peajes'
                            ? buildSubTablePeajes(datos[item.detailKey])
                            : buildSubTableGenerica(datos[item.detailKey]);
                    }
                    gastosHtml += makeRow('fa-minus-circle', 'text-danger', item.label, soles, dolares, desc, subTable);
                }
            });

            if (datos.DetallesGastosAdicionales && datos.DetallesGastosAdicionales.length > 0) {
                datos.DetallesGastosAdicionales.forEach(function (item) {
                    var nombre = item.Nombre || 'Gasto Adicional';
                    gastosHtml += makeRow('fa-minus-circle', 'text-danger', nombre,
                        item.Soles, item.Dolares, item.Descripcion || '', '');
                });
            }

            if (!gastosHtml) {
                gastosHtml = '<tr><td colspan="3" class="text-center text-muted">No hay gastos registrados</td></tr>';
            }
            $('#detalleGastosBody').html(gastosHtml);

            // --- Ajustes Administrativos: pre-poblar inputs ---
            var descSoles = parseFloat(datos.DescuentoSoles || 0);
            var descDolares = parseFloat(datos.DescuentoDolares || 0);
            var reintSoles = parseFloat(datos.ReintegroSoles || 0);
            var reintDolares = parseFloat(datos.ReintegroDolares || 0);

            $('#ajusteDescuentoSoles').val(descSoles > 0 ? descSoles.toFixed(2) : '');
            $('#ajusteDescuentoDolares').val(descDolares > 0 ? descDolares.toFixed(2) : '');
            $('#ajusteReintegroSoles').val(reintSoles > 0 ? reintSoles.toFixed(2) : '');
            $('#ajusteReintegroDolares').val(reintDolares > 0 ? reintDolares.toFixed(2) : '');
            actualizarBalanceAjustado();

            $('#detalleLoading').hide();
            $('#modalDetalleLiquidacion .detail-section').show();
            $('#sectionAjustes').toggle(modoAprobacion);
            $('#approvalBanner').toggle(modoAprobacion);
            $('#modalModeBadge').toggle(modoAprobacion);
            $('#modalDetalleLiquidacionHeader').toggleClass('mode-aprobacion', modoAprobacion);

            if (modoAprobacion) {
                $('#btnAprobarDesdeModal').show();
                // Scroll modal to top so admin sees the guidance banner first
                setTimeout(function () {
                    $('#modalDetalleLiquidacion .modal-body').scrollTop(0);
                }, 50);
            }
        }

        function scrollToAjustes() {
            var $body = $('#modalDetalleLiquidacion .modal-body');
            var $target = $('#sectionAjustes');
            if ($body.length && $target.length) {
                $body.animate({ scrollTop: $target.position().top + $body.scrollTop() - 16 }, 350);
            }
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
            limpiarErroresFormulario();

            if (!observaciones) {
                mostrarErrorCampo('#rechazarObservaciones', '#errorRechazoObservaciones', 'Debe ingresar el motivo del rechazo.');
                $('#rechazarObservaciones').focus();
                return false;
            }

            if (observaciones.length < 10) {
                mostrarErrorCampo('#rechazarObservaciones', '#errorRechazoObservaciones', 'El motivo del rechazo debe tener al menos 10 caracteres.');
                $('#rechazarObservaciones').focus();
                return false;
            }

            if (observaciones.length > 500) {
                mostrarErrorCampo('#rechazarObservaciones', '#errorRechazoObservaciones', 'El motivo del rechazo no puede superar 500 caracteres.');
                $('#rechazarObservaciones').focus();
                return false;
            }

            $('[name=observacionesRechazo]').remove();
            $('<input>').attr({
                type: 'hidden',
                name: 'observacionesRechazo',
                value: observaciones
            }).appendTo('form');

            return confirm('¿Está seguro de RECHAZAR esta liquidación?\n\nEl viaje se reabrirá para correcciones.');
        }

        function imprimirDetalle() {
            // Recopilar datos del modal actual
            var numeroOrden = $('#modalNumeroOrden').text();
            var conductor = $('#detalleConductor').text();
            var tracto = $('#detalleTracto').text();
            var carreta = $('#detalleCarreta').text();
            var periodo = $('#detallePeriodo').text();
            var observaciones = $('#detalleObservaciones').text() || '';

            var ingresosSoles = $('#detalleIngresosSoles').text();
            var ingresosDolares = $('#detalleIngresosDolares').text();
            var gastosSoles = $('#detalleGastosSoles').text();
            var gastosDolares = $('#detalleGastosDolares').text();
            var balanceSoles = $('#detalleBalanceSoles').text();
            var balanceDolares = $('#detalleBalanceDolares').text();
            var balSolesColor = parseFloat(balanceSoles) >= 0 ? '#059669' : '#dc2626';
            var balDolaresColor = parseFloat(balanceDolares) >= 0 ? '#059669' : '#dc2626';

            // Recopilar filas de ingresos
            var ingresosRows = '';
            $('#detalleIngresosBody tr:not(.detail-sub-row)').each(function () {
                var cols = $(this).find('td');
                if (cols.length >= 3) {
                    var concepto = $(cols[0]).clone();
                    concepto.find('.btn-expand').remove();
                    ingresosRows += '<tr>' +
                        '<td style="padding:8px 10px;border-bottom:1px solid #e2e8f0;font-size:13px;">' + concepto.html() + '</td>' +
                        '<td style="padding:8px 10px;border-bottom:1px solid #e2e8f0;text-align:right;font-size:13px;">' + $(cols[1]).text() + '</td>' +
                        '<td style="padding:8px 10px;border-bottom:1px solid #e2e8f0;text-align:right;font-size:13px;">' + $(cols[2]).text() + '</td></tr>';

                    // Incluir sub-detalle si existe
                    var $subRow = $(this).next('.detail-sub-row');
                    if ($subRow.length) {
                        ingresosRows += '<tr><td colspan="3" style="padding:4px 20px 10px;border-bottom:1px solid #e2e8f0;background:#f8fafc;font-size:12px;">' + $subRow.find('td').html() + '</td></tr>';
                    }
                }
            });

            // Recopilar filas de gastos
            var gastosRows = '';
            $('#detalleGastosBody tr:not(.detail-sub-row)').each(function () {
                var cols = $(this).find('td');
                if (cols.length >= 3) {
                    var concepto = $(cols[0]).clone();
                    concepto.find('.btn-expand').remove();
                    gastosRows += '<tr>' +
                        '<td style="padding:8px 10px;border-bottom:1px solid #e2e8f0;font-size:13px;">' + concepto.html() + '</td>' +
                        '<td style="padding:8px 10px;border-bottom:1px solid #e2e8f0;text-align:right;font-size:13px;">' + $(cols[1]).text() + '</td>' +
                        '<td style="padding:8px 10px;border-bottom:1px solid #e2e8f0;text-align:right;font-size:13px;">' + $(cols[2]).text() + '</td></tr>';

                    var $subRow = $(this).next('.detail-sub-row');
                    if ($subRow.length) {
                        gastosRows += '<tr><td colspan="3" style="padding:4px 20px 10px;border-bottom:1px solid #e2e8f0;background:#f8fafc;font-size:12px;">' + $subRow.find('td').html() + '</td></tr>';
                    }
                }
            });

            var fechaImpresion = new Date().toLocaleString('es-PE', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });

            var logoUrl = window.location.origin + '/Content/favicon.png';

            // === DATOS CORPORATIVOS (Empresa + Formato Controlado ISO/BASC) ===
            var EMPRESA_NOMBRE = 'SERVICIOS GENERALES VIVIANA E.I.R.L.';
            var EMPRESA_RUBRO = 'Transporte y Construcci\u00f3n';
            var EMPRESA_RUC = '20483851171';
            var EMPRESA_DIRECCION = 'Jr. Ca\u00f1ete Nro. 416 Dpto. 100 - Cercado de Lima, Lima';
            var EMPRESA_WEB = 'www.serviciosgviviana.somee.com';
            var FORMATO_CODIGO = 'SGV-CDF-F-05';
            var FORMATO_VERSION = '01';
            var FORMATO_VIGENCIA = '01/01/2025';

            // === Conversi\u00f3n monto a letras (peruano) ===
            function numeroALetras(num, moneda) {
                num = parseFloat(num) || 0;
                var negativo = num < 0;
                num = Math.abs(num);
                var entero = Math.floor(num);
                var decimal = Math.round((num - entero) * 100);
                if (decimal === 100) { entero += 1; decimal = 0; }

                var UNIDADES = ['', 'UNO', 'DOS', 'TRES', 'CUATRO', 'CINCO', 'SEIS', 'SIETE', 'OCHO', 'NUEVE', 'DIEZ', 'ONCE', 'DOCE', 'TRECE', 'CATORCE', 'QUINCE', 'DIECIS\u00c9IS', 'DIECISIETE', 'DIECIOCHO', 'DIECINUEVE', 'VEINTE'];
                var DECENAS = ['', '', 'VEINTI', 'TREINTA', 'CUARENTA', 'CINCUENTA', 'SESENTA', 'SETENTA', 'OCHENTA', 'NOVENTA'];
                var CENTENAS = ['', 'CIENTO', 'DOSCIENTOS', 'TRESCIENTOS', 'CUATROCIENTOS', 'QUINIENTOS', 'SEISCIENTOS', 'SETECIENTOS', 'OCHOCIENTOS', 'NOVECIENTOS'];

                function seccion(n) {
                    if (n === 0) return '';
                    if (n <= 20) return UNIDADES[n];
                    if (n < 30) return 'VEINTI' + UNIDADES[n - 20].toLowerCase();
                    if (n < 100) {
                        var d = Math.floor(n / 10), u = n % 10;
                        return DECENAS[d] + (u ? ' Y ' + UNIDADES[u] : '');
                    }
                    if (n === 100) return 'CIEN';
                    if (n < 1000) {
                        var c = Math.floor(n / 100), r = n % 100;
                        return CENTENAS[c] + (r ? ' ' + seccion(r) : '');
                    }
                    if (n < 1000000) {
                        var miles = Math.floor(n / 1000), resto = n % 1000;
                        var pref = (miles === 1) ? 'MIL' : seccion(miles) + ' MIL';
                        return pref + (resto ? ' ' + seccion(resto) : '');
                    }
                    var mill = Math.floor(n / 1000000), rest2 = n % 1000000;
                    var pref2 = (mill === 1) ? 'UN MILL\u00d3N' : seccion(mill) + ' MILLONES';
                    return pref2 + (rest2 ? ' ' + seccion(rest2) : '');
                }

                var letras = seccion(entero);
                if (!letras) letras = 'CERO';
                letras = letras.replace('VEINTIuno', 'VEINTIUNO').replace('VEINTIdos', 'VEINTID\u00d3S').replace('VEINTItres', 'VEINTITR\u00c9S').replace('VEINTIcuatro', 'VEINTICUATRO').replace('VEINTIcinco', 'VEINTICINCO').replace('VEINTIseis', 'VEINTIS\u00c9IS').replace('VEINTIsiete', 'VEINTISIETE').replace('VEINTIocho', 'VEINTIOCHO').replace('VEINTInueve', 'VEINTINUEVE');
                var dec = (decimal < 10 ? '0' : '') + decimal;
                var signo = negativo ? 'MENOS ' : '';
                return signo + letras + ' CON ' + dec + '/100 ' + moneda;
            }

            var balSolesNum = parseFloat(balanceSoles) || 0;
            var balDolaresNum = parseFloat(balanceDolares) || 0;
            var balSolesLetras = numeroALetras(balSolesNum, 'SOLES');
            var balDolaresLetras = numeroALetras(balDolaresNum, 'D\u00d3LARES AMERICANOS');

            var printHtml = '<!DOCTYPE html><html lang="es"><head><meta charset="utf-8"/>' +
                '<title>Orden de Viaje ' + htmlEncode(numeroOrden) + '</title>' +
                '<link href="https://fonts.googleapis.com/css2?family=Montserrat:wght@500;600;700;800&family=Open+Sans:wght@400;600;700&display=swap" rel="stylesheet"/>' +
                '<link href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/5.15.4/css/all.min.css" rel="stylesheet"/>' +
                '<style>' +
                // --- Variables y reset ---
                '@page { size: A4; margin: 12mm 12mm 14mm 12mm; }' +
                '*, *::before, *::after { box-sizing: border-box; }' +
                'body { font-family: "Open Sans", "Segoe UI", Arial, sans-serif; color: #1F2937; margin: 0; padding: 0; font-size: 12px; line-height: 1.45; -webkit-print-color-adjust: exact; print-color-adjust: exact; }' +
                '.print-page { max-width: 210mm; margin: 0 auto; padding: 0; }' +
                'h1, h2, h3, h4 { font-family: "Montserrat", "Segoe UI", Arial, sans-serif; margin: 0; }' +

                // --- Header controlado (3 columnas: logo | empresa | formato) ---
                '.doc-header { display: table; width: 100%; border: 1.5px solid #0B3D91; border-collapse: collapse; margin-bottom: 10px; }' +
                '.doc-header > div { display: table-cell; vertical-align: middle; padding: 8px 10px; border-right: 1px solid #0B3D91; }' +
                '.doc-header > div:last-child { border-right: none; }' +
                '.dh-logo { width: 90px; text-align: center; }' +
                '.dh-logo img { width: 70px; height: 70px; object-fit: contain; }' +
                '.dh-empresa { text-align: center; }' +
                '.dh-empresa .razon { font-family: "Montserrat"; font-size: 13px; font-weight: 800; color: #0B3D91; text-transform: uppercase; letter-spacing: 0.5px; }' +
                '.dh-empresa .rubro { font-size: 10.5px; color: #6B7280; font-weight: 600; margin-top: 1px; }' +
                '.dh-empresa .titulo-doc { font-family: "Montserrat"; font-size: 15px; font-weight: 700; color: #1F2937; letter-spacing: 3px; margin-top: 6px; text-transform: uppercase; }' +
                '.dh-formato { width: 160px; font-size: 10px; color: #1F2937; }' +
                '.dh-formato table { width: 100%; border-collapse: collapse; }' +
                '.dh-formato td { padding: 2px 4px; border-bottom: 1px dotted #D1D5DB; }' +
                '.dh-formato td.lbl { font-weight: 700; color: #6B7280; text-transform: uppercase; font-size: 9px; letter-spacing: 0.3px; }' +
                '.dh-formato td.val { font-family: "Montserrat"; font-weight: 700; color: #0B3D91; text-align: right; font-size: 10px; }' +
                '.dh-formato tr:last-child td { border-bottom: none; }' +

                // --- Sub-cabecera: datos empresa + n\u00famero doc ---
                '.sub-header { display: table; width: 100%; margin-bottom: 12px; font-size: 10.5px; }' +
                '.sub-header > div { display: table-cell; vertical-align: top; }' +
                '.sub-header .emp-data { color: #374151; line-height: 1.55; }' +
                '.sub-header .emp-data strong { color: #0B3D91; font-weight: 700; }' +
                '.sub-header .doc-num { text-align: right; width: 200px; }' +
                '.sub-header .doc-num .nro-label { font-size: 9.5px; font-weight: 700; color: #6B7280; letter-spacing: 0.4px; text-transform: uppercase; }' +
                '.sub-header .doc-num .nro-value { font-family: "Montserrat"; font-size: 17px; font-weight: 800; color: #C8102E; border: 1.5px solid #C8102E; padding: 4px 12px; display: inline-block; margin-top: 3px; letter-spacing: 0.8px; }' +
                '.sub-header .doc-num .emitido { font-size: 9.5px; color: #6B7280; margin-top: 5px; }' +

                // --- Secciones numeradas ---
                '.section { margin-bottom: 10px; page-break-inside: avoid; }' +
                '.section-title { font-family: "Montserrat"; font-size: 11px; font-weight: 700; color: #FFFFFF; background: #0B3D91; padding: 5px 10px; text-transform: uppercase; letter-spacing: 0.6px; }' +
                '.section-title .num { display: inline-block; background: #FFFFFF; color: #0B3D91; width: 18px; height: 18px; line-height: 18px; text-align: center; border-radius: 2px; margin-right: 8px; font-weight: 800; }' +

                // --- Grid de info ---
                '.info-grid { display: table; width: 100%; border-collapse: collapse; }' +
                '.info-grid .info-row { display: table-row; }' +
                '.info-item { display: table-cell; border: 1px solid #D1D5DB; padding: 6px 10px; width: 25%; }' +
                '.info-item .label { font-size: 9px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.4px; color: #6B7280; }' +
                '.info-item .value { font-family: "Montserrat"; font-size: 12px; font-weight: 700; color: #1F2937; margin-top: 2px; }' +
                '.obs-box { border: 1px solid #D1D5DB; border-left: 3px solid #C8102E; padding: 7px 10px; font-size: 11px; color: #374151; margin-top: 6px; background: #FAFAFA; }' +

                // --- Tablas ---
                'table.report-table { width: 100%; border-collapse: collapse; font-size: 11.5px; border: 1px solid #D1D5DB; }' +
                'table.report-table thead th { background: #0B3D91; color: #FFFFFF; padding: 6px 10px; font-family: "Montserrat"; font-weight: 600; font-size: 10.5px; text-transform: uppercase; letter-spacing: 0.4px; border: 1px solid #0B3D91; }' +
                'table.report-table thead th:first-child { text-align: left; }' +
                'table.report-table thead th.text-right { text-align: right; }' +
                'table.report-table tbody td { padding: 5px 10px; border-bottom: 1px solid #E5E7EB; }' +
                'table.report-table tbody tr:nth-child(odd) td { background: #E8EEF7; }' +
                'table.report-table tbody tr:nth-child(even) td { background: #FFFFFF; }' +
                'table.report-table tfoot td { background: #F3F4F6; font-family: "Montserrat"; font-weight: 700; padding: 7px 10px; border-top: 1.5px solid #0B3D91; font-size: 12px; color: #0B3D91; }' +

                // --- Resumen financiero (tabla sobria) ---
                'table.resumen-table { width: 100%; border-collapse: collapse; font-size: 11.5px; border: 1px solid #D1D5DB; }' +
                'table.resumen-table th { background: #F3F4F6; color: #1F2937; padding: 6px 10px; font-family: "Montserrat"; font-weight: 700; font-size: 10.5px; text-transform: uppercase; border: 1px solid #D1D5DB; }' +
                'table.resumen-table td { padding: 6px 10px; border: 1px solid #D1D5DB; }' +
                'table.resumen-table td.concepto { font-weight: 600; color: #1F2937; }' +
                'table.resumen-table td.num { text-align: right; font-family: "Montserrat"; font-weight: 600; }' +
                'table.resumen-table tr.total td { background: #E8EEF7; font-weight: 800; color: #0B3D91; }' +
                'table.resumen-table tr.balance td { background: #0B3D91; color: #FFFFFF; font-family: "Montserrat"; font-weight: 800; font-size: 13px; }' +
                'table.resumen-table tr.balance td.num.neg { color: #FCA5A5; }' +
                '.monto-letras { background: #F9FAFB; border: 1px dashed #9CA3AF; padding: 6px 10px; font-size: 10.5px; color: #374151; margin-top: 6px; }' +
                '.monto-letras strong { font-family: "Montserrat"; font-weight: 700; color: #0B3D91; display: inline-block; min-width: 60px; }' +

                // --- Constancia y firma ---
                '.constancia { border: 1px solid #D1D5DB; padding: 10px 12px; margin-top: 10px; page-break-inside: avoid; }' +
                '.constancia .const-title { font-family: "Montserrat"; font-size: 11px; font-weight: 700; color: #0B3D91; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 6px; }' +
                '.constancia .const-text { font-size: 11px; text-align: justify; color: #374151; line-height: 1.55; margin-bottom: 10px; }' +
                '.firma-box { width: 280px; margin: 0 auto; text-align: center; }' +
                '.firma-box .firma-area { border: 1px dashed #9CA3AF; height: 70px; margin-bottom: 4px; background: #FAFAFA; display: flex; align-items: center; justify-content: center; font-size: 9px; color: #9CA3AF; font-style: italic; }' +
                '.firma-box .firma-line { border-top: 1px solid #1F2937; padding-top: 3px; }' +
                '.firma-box .firma-nombre { font-family: "Montserrat"; font-size: 11px; font-weight: 700; color: #1F2937; }' +
                '.firma-box .firma-rol { font-size: 10px; color: #6B7280; margin-top: 1px; }' +
                '.firma-meta { margin-top: 8px; font-size: 9px; color: #6B7280; text-align: center; font-family: "Consolas", monospace; }' +

                // --- Pie ---
                '.doc-footer { margin-top: 14px; border-top: 1.5px solid #0B3D91; padding-top: 8px; font-size: 9px; color: #6B7280; display: table; width: 100%; }' +
                '.doc-footer > div { display: table-cell; vertical-align: top; }' +
                '.doc-footer .foot-legal { width: 75%; line-height: 1.5; }' +
                '.doc-footer .foot-legal strong { color: #0B3D91; }' +
                '.doc-footer .foot-pag { width: 25%; text-align: right; font-family: "Consolas", monospace; }' +
                '.doc-footer .hash { font-family: "Consolas", monospace; color: #374151; word-break: break-all; }' +

                // --- Detalles anidados / tablas hijas ---
                '.detail-sub-content { padding: 4px 8px; font-size: 10.5px; }' +
                '.detail-desc { font-size: 10.5px; color: #6B7280; font-style: italic; margin-bottom: 4px; }' +
                '.table-sub { font-size: 10px !important; }' +
                '.table-sub thead th { background: #E5E7EB !important; color: #1F2937 !important; font-size: 9.5px !important; padding: 3px 6px !important; }' +
                '.table-sub tbody td { padding: 3px 6px !important; font-size: 10px !important; background: #FFFFFF !important; }' +

                '</style></head><body>' +
                '<div class="print-page">' +

                // === 1. ENCABEZADO CONTROLADO (ISO/BASC) ===
                '<div class="doc-header">' +
                '<div class="dh-logo"><img src="' + logoUrl + '" alt="Logo Viviana"/></div>' +
                '<div class="dh-empresa">' +
                '<div class="razon">' + EMPRESA_NOMBRE + '</div>' +
                '<div class="rubro">' + EMPRESA_RUBRO + '</div>' +
                '<div class="titulo-doc">Orden de Viaje</div>' +
                '</div>' +
                '<div class="dh-formato">' +
                '<table>' +
                '<tr><td class="lbl">C\u00f3digo</td><td class="val">' + FORMATO_CODIGO + '</td></tr>' +
                '<tr><td class="lbl">Versi\u00f3n</td><td class="val">' + FORMATO_VERSION + '</td></tr>' +
                '<tr><td class="lbl">Vigencia</td><td class="val">' + FORMATO_VIGENCIA + '</td></tr>' +
                '<tr><td class="lbl">P\u00e1gina</td><td class="val">1 de 1</td></tr>' +
                '</table>' +
                '</div>' +
                '</div>' +

                // === SUB-HEADER: datos empresa + n\u00famero de documento ===
                '<div class="sub-header">' +
                '<div class="emp-data">' +
                '<strong>RUC:</strong> ' + EMPRESA_RUC + '<br/>' +
                '<strong>Domicilio Fiscal:</strong> ' + EMPRESA_DIRECCION + '<br/>' +
                '<strong>Web:</strong> ' + EMPRESA_WEB +
                '</div>' +
                '<div class="doc-num">' +
                '<div class="nro-label">N.\u00b0 de Orden</div>' +
                '<div class="nro-value">' + htmlEncode(numeroOrden) + '</div>' +
                '<div class="emitido">Emitido: ' + fechaImpresion + '</div>' +
                '</div>' +
                '</div>' +

                // === 1. INFORMACI\u00d3N DEL VIAJE ===
                '<div class="section">' +
                '<div class="section-title"><span class="num">1</span>Informaci\u00f3n del Viaje</div>' +
                '<div class="info-grid"><div class="info-row">' +
                '<div class="info-item"><div class="label">Conductor</div><div class="value">' + htmlEncode(conductor) + '</div></div>' +
                '<div class="info-item"><div class="label">Tracto</div><div class="value">' + htmlEncode(tracto) + '</div></div>' +
                '<div class="info-item"><div class="label">Carreta</div><div class="value">' + htmlEncode(carreta) + '</div></div>' +
                '<div class="info-item"><div class="label">Periodo</div><div class="value">' + htmlEncode(periodo) + '</div></div>' +
                '</div></div>' +
                '</div>' +

                // === 2. RESUMEN FINANCIERO (tabla sobria) ===
                '<div class="section">' +
                '<div class="section-title"><span class="num">2</span>Resumen Financiero</div>' +
                '<table class="resumen-table">' +
                '<thead><tr><th style="text-align:left;">Concepto</th><th style="text-align:right;">Soles (S/)</th><th style="text-align:right;">D\u00f3lares ($)</th></tr></thead>' +
                '<tbody>' +
                '<tr><td class="concepto">Total Ingresos</td><td class="num">S/ ' + ingresosSoles + '</td><td class="num">$ ' + ingresosDolares + '</td></tr>' +
                '<tr><td class="concepto">Total Gastos</td><td class="num">S/ ' + gastosSoles + '</td><td class="num">$ ' + gastosDolares + '</td></tr>' +
                '<tr class="balance"><td>BALANCE FINAL DEL VIAJE</td><td class="num' + (balSolesNum < 0 ? ' neg' : '') + '">S/ ' + balanceSoles + '</td><td class="num' + (balDolaresNum < 0 ? ' neg' : '') + '">$ ' + balanceDolares + '</td></tr>' +
                '</tbody></table>' +
                '<div class="monto-letras"><strong>SOLES:</strong> ' + balSolesLetras + '</div>' +
                '<div class="monto-letras"><strong>D\u00d3LARES:</strong> ' + balDolaresLetras + '</div>' +
                '</div>' +

                // === 3. DESGLOSE DE INGRESOS ===
                '<div class="section">' +
                '<div class="section-title"><span class="num">3</span>Desglose de Ingresos</div>' +
                '<table class="report-table">' +
                '<thead><tr><th>Concepto</th><th class="text-right">Soles (S/)</th><th class="text-right">D\u00f3lares ($)</th></tr></thead>' +
                '<tbody>' + ingresosRows + '</tbody>' +
                '<tfoot><tr><td>Total Ingresos</td><td style="text-align:right;">S/ ' + ingresosSoles + '</td><td style="text-align:right;">$ ' + ingresosDolares + '</td></tr></tfoot>' +
                '</table></div>' +

                // === 4. DESGLOSE DE GASTOS ===
                '<div class="section">' +
                '<div class="section-title"><span class="num">4</span>Desglose de Gastos</div>' +
                '<table class="report-table">' +
                '<thead><tr><th>Concepto</th><th class="text-right">Soles (S/)</th><th class="text-right">D\u00f3lares ($)</th></tr></thead>' +
                '<tbody>' + gastosRows + '</tbody>' +
                '<tfoot><tr><td>Total Gastos</td><td style="text-align:right;">S/ ' + gastosSoles + '</td><td style="text-align:right;">$ ' + gastosDolares + '</td></tr></tfoot>' +
                '</table></div>' +

                // === 5. OBSERVACIONES ===
                (observaciones ?
                    '<div class="section">' +
                    '<div class="section-title"><span class="num">5</span>Observaciones</div>' +
                    '<div class="obs-box">' + htmlEncode(observaciones) + '</div>' +
                    '</div>'
                    : '') +

                // === 6. CONSTANCIA DE CONFORMIDAD Y FIRMA ===
                '<div class="constancia">' +
                '<div class="const-title">Constancia de Conformidad</div>' +
                '<div class="const-text">Yo, <strong>' + htmlEncode(conductor) + '</strong>, en mi condici\u00f3n de conductor del viaje ' +
                '<strong>' + htmlEncode(numeroOrden) + '</strong>, declaro haber revisado el detalle de ingresos, gastos y balance ' +
                'consignados en el presente documento, y manifiesto mi conformidad con la informaci\u00f3n registrada. ' +
                'Firmo en se\u00f1al de aceptaci\u00f3n y asumo las obligaciones derivadas de la presente liquidaci\u00f3n.</div>' +
                '<div class="firma-box">' +
                '<div class="firma-area">[Espacio reservado para firma del conductor]</div>' +
                '<div class="firma-line">' +
                '<div class="firma-nombre">' + htmlEncode(conductor) + '</div>' +
                '<div class="firma-rol">Conductor</div>' +
                '</div>' +
                '</div>' +
                '<div class="firma-meta">Firma pendiente de registro digital</div>' +
                '</div>' +

                // === PIE CONTROLADO ===
                '<div class="doc-footer">' +
                '<div class="foot-legal">' +
                '<strong>Documento interno controlado.</strong> Generado electr\u00f3nicamente por el Sistema SGV. ' +
                'Los comprobantes tributarios (facturas, boletas, guias, tickets) que respaldan los importes aqu\u00ed consignados ' +
                'se conservan digitalmente vinculados a este documento conforme al art. 87 del C\u00f3digo Tributario (plazo m\u00ednimo 5 a\u00f1os).' +
                '<br/><span class="hash">Hash documento: [pendiente de firma digital]</span>' +
                '</div>' +
                '<div class="foot-pag">' +
                FORMATO_CODIGO + ' v.' + FORMATO_VERSION + '<br/>' +
                'P\u00e1gina 1 de 1' +
                '</div>' +
                '</div>' +

                '</div></body></html>';

            var printWindow = window.open('', '_blank', 'width=900,height=700');
            if (printWindow) {
                printWindow.document.write(printHtml);
                printWindow.document.close();
                printWindow.onload = function () {
                    setTimeout(function () {
                        printWindow.print();
                    }, 400);
                };
            } else {
                alert('El navegador bloque\u00f3 la ventana emergente. Por favor, permita las ventanas emergentes para este sitio.');
            }
        }

        function abrirModalAprobar(idOrdenViaje) {
            modoAprobacion = true;
            idOrdenParaAprobar = idOrdenViaje;
            verDetalleLiquidacion(idOrdenViaje);
        }

        function actualizarBalanceAjustado() {
            var descS = parseFloat($('#ajusteDescuentoSoles').val()) || 0;
            var descD = parseFloat($('#ajusteDescuentoDolares').val()) || 0;
            var reintS = parseFloat($('#ajusteReintegroSoles').val()) || 0;
            var reintD = parseFloat($('#ajusteReintegroDolares').val()) || 0;

            if (descS > 0 || descD > 0 || reintS > 0 || reintD > 0) {
                var balS = _balanceSoles - descS + reintS;
                var balD = _balanceDolares - descD + reintD;
                $('#balanceAjustadoSoles').text(balS.toFixed(2))
                    .css('color', balS >= 0 ? '#059669' : '#dc2626');
                $('#balanceAjustadoDolares').text(balD.toFixed(2))
                    .css('color', balD >= 0 ? '#059669' : '#dc2626');
                $('#balanceAjustadoPanel').show();
            } else {
                $('#balanceAjustadoPanel').hide();
            }
        }

        function toggleCorregirSalida() {
            $('#errorCorregirSalidaMotivo').hide();
            $('#panelCorregirSalida').slideToggle(150);
        }

        function corregirSalidaDesdeModal() {
            if (!_idOrdenDetalleActual) return;

            var fecha = $('#corregirSalidaFecha').val();
            var hora = $('#corregirSalidaHora').val();
            var motivo = $('#corregirSalidaMotivo').val().trim();

            $('#errorCorregirSalidaMotivo').hide();

            if (!fecha || !hora) {
                mostrarErrorCampo('#corregirSalidaMotivo', '#errorCorregirSalidaMotivo', 'Debe indicar la fecha y la hora de salida.');
                return;
            }
            if (motivo.length < 10) {
                mostrarErrorCampo('#corregirSalidaMotivo', '#errorCorregirSalidaMotivo', 'El motivo debe tener al menos 10 caracteres.');
                return;
            }
            if (motivo.length > 500) {
                mostrarErrorCampo('#corregirSalidaMotivo', '#errorCorregirSalidaMotivo', 'El motivo no puede superar 500 caracteres.');
                return;
            }

            if (!confirm('¿Confirma corregir la fecha/hora de salida de esta liquidación?\n\nEl cambio quedará auditado.')) return;

            var $btn = $('#btnGuardarCorregirSalida');
            $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-1"></i>Guardando...');

            $.ajax({
                type: "POST",
                url: "LiquidacionesPendientes.aspx/CorregirSalidaPendiente",
                data: JSON.stringify({ idOrdenViaje: _idOrdenDetalleActual, fechaSalida: fecha, horaSalida: hora, motivo: motivo }),
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                success: function (response) {
                    var result = response.d;
                    if (result.success) {
                        $('#modalDetalleLiquidacion').modal('hide');
                        alert('✅ ' + result.message);
                        location.reload();
                    } else {
                        alert('❌ ' + result.message);
                        $btn.prop('disabled', false).html('<i class="fas fa-save mr-1"></i>Guardar corrección');
                    }
                },
                error: function (xhr) {
                    console.error('Error:', xhr.responseText);
                    alert('Error al corregir la salida. Por favor intente de nuevo.');
                    $btn.prop('disabled', false).html('<i class="fas fa-save mr-1"></i>Guardar corrección');
                }
            });
        }

        function aprobarDesdeModal() {
            if (!idOrdenParaAprobar) return;
            limpiarErroresFormulario();

            var descS = parseFloat($('#ajusteDescuentoSoles').val()) || 0;
            var descD = parseFloat($('#ajusteDescuentoDolares').val()) || 0;
            var reintS = parseFloat($('#ajusteReintegroSoles').val()) || 0;
            var reintD = parseFloat($('#ajusteReintegroDolares').val()) || 0;
            var nota = $('#notaAprobacion').val().trim();

            if (descS < 0 || descD < 0 || reintS < 0 || reintD < 0) {
                alert('⚠️ Los montos de ajuste no pueden ser negativos.');
                return;
            }

            if (nota.length > 500) {
                alert('La nota de aprobación no puede superar 500 caracteres.');
                $('#notaAprobacion').focus();
                return;
            }

            var confirmMsg = '¿Está seguro de APROBAR esta liquidación?\n\n';
            if (descS > 0 || descD > 0 || reintS > 0 || reintD > 0) {
                confirmMsg += 'Se aplicarán los siguientes ajustes:\n';
                if (descS > 0) confirmMsg += '  \u2022 Descuento S/: ' + descS.toFixed(2) + '\n';
                if (descD > 0) confirmMsg += '  \u2022 Descuento $: ' + descD.toFixed(2) + '\n';
                if (reintS > 0) confirmMsg += '  \u2022 Reintegro S/: ' + reintS.toFixed(2) + '\n';
                if (reintD > 0) confirmMsg += '  \u2022 Reintegro $: ' + reintD.toFixed(2) + '\n';
                confirmMsg += '\n';
            }
            if (nota) confirmMsg += 'Nota: ' + nota + '\n\n';
            confirmMsg += 'Esta acción completará el viaje y no se podrá deshacer.';

            if (!confirm(confirmMsg)) return;

            var $btn = $('#btnAprobarDesdeModal');
            $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-1"></i>Aprobando...');

            $.ajax({
                type: "POST",
                url: "LiquidacionesPendientes.aspx/AprobarLiquidacionConFirma",
                data: JSON.stringify({
                    idOrdenViaje: idOrdenParaAprobar,
                    descuentoSoles: descS,
                    descuentoDolares: descD,
                    reintegroSoles: reintS,
                    reintegroDolares: reintD,
                    notaAprobacion: nota || null
                }),
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                success: function (response) {
                    var result = response.d;
                    if (result.success) {
                        $('#modalDetalleLiquidacion').modal('hide');
                        alert('\u2705 ' + result.message);
                        location.reload();
                    } else {
                        alert('\u274c ' + result.message);
                        $btn.prop('disabled', false).html('<i class="fas fa-check mr-1"></i>Aprobar Liquidación');
                    }
                },
                error: function (xhr) {
                    console.error('Error:', xhr.responseText);
                    alert('Error al aprobar la liquidación. Por favor intente de nuevo.');
                    $btn.prop('disabled', false).html('<i class="fas fa-check mr-1"></i>Aprobar Liquidación');
                }
            });
        }

        $(document).ready(function () {
            console.log('LiquidacionesPendientes cargado');
            $('#modalDetalleLiquidacion').on('hidden.bs.modal', function () {
                modoAprobacion = false;
                idOrdenParaAprobar = 0;
                $('#btnAprobarDesdeModal').hide();
                $('#approvalBanner').hide();
                $('#modalModeBadge').hide();
                $('#modalDetalleLiquidacionHeader').removeClass('mode-aprobacion');
                $('#notaAprobacion').val('');
                $('#ajusteDescuentoSoles, #ajusteDescuentoDolares, #ajusteReintegroSoles, #ajusteReintegroDolares').val('');
                $('#balanceAjustadoPanel').hide();
            });
        });

        // ============================================================
        // === SECCIÓN CONTROL DE APROBADAS ===
        // ============================================================

        var _aprobadasData = {};
        var _aprobadasCargadas = false;

        function cambiarTab(tab) {
            if (tab === 'pendientes') {
                $('#seccionPendientes').show();
                $('#seccionAprobadas').hide();
                $('#tabPendientes').addClass('tab-btn-active');
                $('#tabAprobadas').removeClass('tab-btn-active');
            } else {
                $('#seccionPendientes').hide();
                $('#seccionAprobadas').show();
                $('#tabPendientes').removeClass('tab-btn-active');
                $('#tabAprobadas').addClass('tab-btn-active');
                if (!_aprobadasCargadas) {
                    inicializarFiltrosAprobadas();
                    cargarLiquidacionesAprobadas();
                    _aprobadasCargadas = true;
                }
            }
        }

        function inicializarFiltrosAprobadas() {
            var hoy = new Date();
            var hace30 = new Date(hoy);
            hace30.setDate(hace30.getDate() - 30);
            $('#filtroDesdeAprobadas').val(hace30.toISOString().substr(0, 10));
            $('#filtroHastaAprobadas').val(hoy.toISOString().substr(0, 10));
        }

        function limpiarFiltrosAprobadas() {
            $('#txtConductorAprobadas').val('');
            $('#filtroCondAprobadasId').val('');
            $('#filtroOrdenAprobadas').val('');
            var hoy = new Date();
            var hace30 = new Date(hoy);
            hace30.setDate(hace30.getDate() - 30);
            $('#filtroDesdeAprobadas').val(hace30.toISOString().substr(0, 10));
            $('#filtroHastaAprobadas').val(hoy.toISOString().substr(0, 10));
            cargarLiquidacionesAprobadas();
        }

        function cargarLiquidacionesAprobadas() {
            limpiarErroresFormulario();
            if (!validarFiltrosAprobadasCliente()) return;

            var filtros = {
                idConductor: parseInt($('#filtroCondAprobadasId').val()) || 0,
                fechaDesde: $('#filtroDesdeAprobadas').val() || '',
                fechaHasta: $('#filtroHastaAprobadas').val() || '',
                numeroOrden: $('#filtroOrdenAprobadas').val() || ''
            };

            $('#loadingAprobadas').show();
            $('#tbodyAprobadas').html('');

            $.ajax({
                type: "POST",
                url: "LiquidacionesPendientes.aspx/ObtenerLiquidacionesAprobadas",
                data: JSON.stringify(filtros),
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                success: function (response) {
                    $('#loadingAprobadas').hide();
                    var datos = response.d;
                    if (!datos || datos.length === 0) {
                        $('#tbodyAprobadas').html(
                            '<tr><td colspan="7" class="text-center py-4">' +
                            '<div class="empty-state-table" style="padding:2rem;">' +
                            '<i class="fas fa-check-circle" style="font-size:2.5rem;color:#059669;display:block;margin-bottom:0.5rem;"></i>' +
                            '<p class="text-muted mb-0">No se encontraron liquidaciones aprobadas en el rango seleccionado.</p>' +
                            '</div></td></tr>'
                        );
                        $('#lblTotalAprobadas').text('0');
                        return;
                    }

                    _aprobadasData = {};
                    $('#lblTotalAprobadas').text(datos.length);
                    var html = '';

                    datos.forEach(function (item) {
                        _aprobadasData[item.IdOrdenViaje] = item;

                        html += '<tr>';

                        // N° Orden
                        html += '<td><div class="orden-info">';
                        html += '<span class="orden-numero">' + htmlEncode(item.NumeroOrdenViaje) + '</span>';
                        html += '<span class="orden-fecha">' + htmlEncode(item.FechaSalida) + '</span>';
                        html += '</div></td>';

                        // Conductor
                        html += '<td><div class="conductor-info">';
                        html += '<i class="fas fa-user-circle mr-2 text-success"></i><div>';
                        html += '<strong>' + htmlEncode(item.NombreConductor) + '</strong><br/>';
                        html += '<small class="text-muted">' + htmlEncode(item.PlacaTracto) + ' / ' + htmlEncode(item.PlacaCarreta) + '</small>';
                        html += '</div></div></td>';

                        // Periodo
                        html += '<td class="text-center"><div class="viaje-periodo">';
                        html += '<div class="fecha-item"><i class="fas fa-calendar-check text-success"></i><span>' + htmlEncode(item.FechaSalida) + '</span></div>';
                        html += '<div class="fecha-item"><i class="fas fa-calendar-times text-danger"></i><span>' + htmlEncode(item.FechaLlegada) + '</span></div>';
                        html += '</div></td>';

                        // Descuento
                        var descS = parseFloat(item.DescuentoSoles || 0);
                        var descD = parseFloat(item.DescuentoDolares || 0);
                        html += '<td class="text-right">';
                        if (descS > 0 || descD > 0) {
                            html += '<div class="balance-preview">';
                            html += '<div class="balance-row"><span class="balance-moneda">S/</span><span class="balance-monto balance-negativo">' + descS.toFixed(2) + '</span></div>';
                            html += '<div class="balance-row"><span class="balance-moneda">$</span><span class="balance-monto balance-negativo">' + descD.toFixed(2) + '</span></div>';
                            html += '</div>';
                        } else {
                            html += '<span class="text-muted">-</span>';
                        }
                        html += '</td>';

                        // Reintegro
                        var reintS = parseFloat(item.ReintegroSoles || 0);
                        var reintD = parseFloat(item.ReintegroDolares || 0);
                        html += '<td class="text-right">';
                        if (reintS > 0 || reintD > 0) {
                            html += '<div class="balance-preview">';
                            html += '<div class="balance-row"><span class="balance-moneda">S/</span><span class="balance-monto balance-positivo">' + reintS.toFixed(2) + '</span></div>';
                            html += '<div class="balance-row"><span class="balance-moneda">$</span><span class="balance-monto balance-positivo">' + reintD.toFixed(2) + '</span></div>';
                            html += '</div>';
                        } else {
                            html += '<span class="text-muted">-</span>';
                        }
                        html += '</td>';

                        // Balance Final
                        var bfS = parseFloat(item.BalanceSoles || 0);
                        var bfD = parseFloat(item.BalanceDolares || 0);
                        html += '<td class="text-right"><div class="balance-preview">';
                        html += '<div class="balance-row"><span class="balance-moneda">S/</span><span class="balance-monto ' + (bfS >= 0 ? 'balance-positivo' : 'balance-negativo') + '">' + bfS.toFixed(2) + '</span></div>';
                        html += '<div class="balance-row"><span class="balance-moneda">$</span><span class="balance-monto ' + (bfD >= 0 ? 'balance-positivo' : 'balance-negativo') + '">' + bfD.toFixed(2) + '</span></div>';
                        html += '</div></td>';

                        // Acciones
                        html += '<td class="text-center"><div class="acciones-grupo">';
                        html += '<button type="button" class="btn btn-info-action" onclick="verDetalleLiquidacion(' + item.IdOrdenViaje + ')" title="Ver Detalle"><i class="fas fa-eye"></i></button>';
                        html += '<button type="button" class="btn btn-success-action" onclick="descargarPdfOrdenViaje(' + item.IdOrdenViaje + ')" title="Descargar PDF SGV-CDF-F-05"><i class="fas fa-file-pdf"></i></button>';
                        html += '<button type="button" class="btn btn-warning-action" onclick="abrirModalCorregir(' + item.IdOrdenViaje + ')" title="Corregir Ajustes"><i class="fas fa-edit"></i></button>';
                        html += '<button type="button" class="btn btn-danger-action" onclick="abrirModalRevertir(' + item.IdOrdenViaje + ')" title="Revertir Aprobaci\u00f3n"><i class="fas fa-undo"></i></button>';
                        html += '</div></td>';

                        html += '</tr>';
                    });

                    $('#tbodyAprobadas').html(html);
                },
                error: function (xhr) {
                    $('#loadingAprobadas').hide();
                    console.error('Error cargando aprobadas:', xhr.responseText);
                    $('#tbodyAprobadas').html(
                        '<tr><td colspan="7" class="text-center text-danger py-4">' +
                        '<i class="fas fa-exclamation-triangle mr-2"></i>Error al cargar las liquidaciones aprobadas</td></tr>'
                    );
                }
            });
        }

        function htmlEncode(text) {
            if (!text) return '';
            var div = document.createElement('div');
            div.appendChild(document.createTextNode(text));
            return div.innerHTML;
        }

        // --- Descargar PDF SGV-CDF-F-05 ---
        function descargarPdfOrdenViaje(idOrden) {
            if (!idOrden) return;
            $.ajax({
                type: "POST",
                url: "LiquidacionesPendientes.aspx/ObtenerUrlPdfOrdenViaje",
                data: JSON.stringify({ idOrdenViaje: idOrden }),
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                success: function (response) {
                    var r = response.d;
                    if (r && r.success && r.url) {
                        // Abrir en pestaña nueva (vista previa). Para forzar descarga: &download=1
                        window.open(r.url, '_blank');
                    } else {
                        alert('\u274c ' + (r && r.message ? r.message : 'No se pudo obtener el PDF.'));
                    }
                },
                error: function (xhr) {
                    console.error('Error PDF:', xhr.responseText);
                    alert('Error al solicitar el PDF.');
                }
            });
        }

        // --- Modal Revertir ---
        function abrirModalRevertir(idOrden) {
            var item = _aprobadasData[idOrden];
            if (!item) { alert('No se encontraron datos de la liquidaci\u00f3n.'); return; }
            $('#revertirIdOrden').val(idOrden);
            $('#revertirNumeroOrden').val(item.NumeroOrdenViaje);
            $('#revertirConductor').val(item.NombreConductor);
            $('#revertirMotivo').val('');
            $('#modalRevertir').modal('show');
        }

        function confirmarReversion() {
            var idOrden = parseInt($('#revertirIdOrden').val());
            var motivo = $('#revertirMotivo').val().trim();
            limpiarErroresFormulario();

            if (!motivo) {
                mostrarErrorCampo('#revertirMotivo', '#errorRevertirMotivo', 'Debe ingresar el motivo de la reversión.');
                $('#revertirMotivo').focus();
                return;
            }
            if (motivo.length < 10) {
                mostrarErrorCampo('#revertirMotivo', '#errorRevertirMotivo', 'El motivo debe tener al menos 10 caracteres.');
                $('#revertirMotivo').focus();
                return;
            }
            if (motivo.length > 500) {
                mostrarErrorCampo('#revertirMotivo', '#errorRevertirMotivo', 'El motivo no puede superar 500 caracteres.');
                $('#revertirMotivo').focus();
                return;
            }

            if (!confirm('\u26a0\ufe0f \u00bfEst\u00e1 seguro de REVERTIR esta aprobaci\u00f3n?\n\nLa liquidaci\u00f3n volver\u00e1 al estado PENDIENTE y deber\u00e1 ser revisada nuevamente.')) return;

            var $btn = $('#btnConfirmarReversion');
            $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-1"></i>Procesando...');

            $.ajax({
                type: "POST",
                url: "LiquidacionesPendientes.aspx/RevertirAprobacion",
                data: JSON.stringify({ idOrdenViaje: idOrden, motivo: motivo }),
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                success: function (response) {
                    var result = response.d;
                    if (result.success) {
                        $('#modalRevertir').modal('hide');
                        alert('\u2705 ' + result.message);
                        cargarLiquidacionesAprobadas();
                    } else {
                        alert('\u274c ' + result.message);
                    }
                    $btn.prop('disabled', false).html('<i class="fas fa-undo mr-1"></i>Confirmar Reversi\u00f3n');
                },
                error: function (xhr) {
                    console.error('Error:', xhr.responseText);
                    alert('Error al revertir la aprobaci\u00f3n.');
                    $btn.prop('disabled', false).html('<i class="fas fa-undo mr-1"></i>Confirmar Reversi\u00f3n');
                }
            });
        }

        // --- Modal Corregir Ajustes ---
        function abrirModalCorregir(idOrden) {
            var item = _aprobadasData[idOrden];
            if (!item) { alert('No se encontraron datos de la liquidaci\u00f3n.'); return; }

            $('#corregirIdOrden').val(idOrden);
            $('#corregirNumeroOrden').val(item.NumeroOrdenViaje);
            $('#corregirConductor').val(item.NombreConductor);

            var descS = parseFloat(item.DescuentoSoles || 0);
            var descD = parseFloat(item.DescuentoDolares || 0);
            var reintS = parseFloat(item.ReintegroSoles || 0);
            var reintD = parseFloat(item.ReintegroDolares || 0);

            $('#corregirDescSolesActual').text(descS.toFixed(2));
            $('#corregirDescDolaresActual').text(descD.toFixed(2));
            $('#corregirReintSolesActual').text(reintS.toFixed(2));
            $('#corregirReintDolaresActual').text(reintD.toFixed(2));

            $('#corregirDescSoles').val(descS > 0 ? descS.toFixed(2) : '');
            $('#corregirDescDolares').val(descD > 0 ? descD.toFixed(2) : '');
            $('#corregirReintSoles').val(reintS > 0 ? reintS.toFixed(2) : '');
            $('#corregirReintDolares').val(reintD > 0 ? reintD.toFixed(2) : '');
            $('#corregirMotivo').val('');

            $('#modalCorregirAjustes').modal('show');
        }

        function confirmarCorreccion() {
            var idOrden = parseInt($('#corregirIdOrden').val());
            var motivo = $('#corregirMotivo').val().trim();
            var descS = parseFloat($('#corregirDescSoles').val()) || 0;
            var descD = parseFloat($('#corregirDescDolares').val()) || 0;
            var reintS = parseFloat($('#corregirReintSoles').val()) || 0;
            var reintD = parseFloat($('#corregirReintDolares').val()) || 0;
            limpiarErroresFormulario();

            if (!motivo) {
                mostrarErrorCampo('#corregirMotivo', '#errorCorregirMotivo', 'Debe ingresar el motivo de la corrección.');
                $('#corregirMotivo').focus();
                return;
            }
            if (motivo.length < 10) {
                mostrarErrorCampo('#corregirMotivo', '#errorCorregirMotivo', 'El motivo debe tener al menos 10 caracteres.');
                $('#corregirMotivo').focus();
                return;
            }
            if (motivo.length > 500) {
                mostrarErrorCampo('#corregirMotivo', '#errorCorregirMotivo', 'El motivo no puede superar 500 caracteres.');
                $('#corregirMotivo').focus();
                return;
            }

            if (descS < 0 || descD < 0 || reintS < 0 || reintD < 0) {
                alert('Los montos de descuento/reintegro no pueden ser negativos.');
                return;
            }

            var msg = '\u00bfEst\u00e1 seguro de CORREGIR los ajustes?\n\n';
            msg += 'Nuevos valores:\n';
            msg += '  \u2022 Descuento S/: ' + descS.toFixed(2) + '\n';
            msg += '  \u2022 Descuento $: ' + descD.toFixed(2) + '\n';
            msg += '  \u2022 Reintegro S/: ' + reintS.toFixed(2) + '\n';
            msg += '  \u2022 Reintegro $: ' + reintD.toFixed(2) + '\n';

            if (!confirm(msg)) return;

            var $btn = $('#btnConfirmarCorreccion');
            $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-1"></i>Guardando...');

            $.ajax({
                type: "POST",
                url: "LiquidacionesPendientes.aspx/CorregirAjustesAprobada",
                data: JSON.stringify({
                    idOrdenViaje: idOrden,
                    descuentoSoles: descS,
                    descuentoDolares: descD,
                    reintegroSoles: reintS,
                    reintegroDolares: reintD,
                    motivo: motivo
                }),
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                success: function (response) {
                    var result = response.d;
                    if (result.success) {
                        $('#modalCorregirAjustes').modal('hide');
                        alert('\u2705 ' + result.message);
                        _aprobadasCargadas = false;
                        cargarLiquidacionesAprobadas();
                    } else {
                        alert('\u274c ' + result.message);
                    }
                    $btn.prop('disabled', false).html('<i class="fas fa-save mr-1"></i>Guardar Correcci\u00f3n');
                },
                error: function (xhr) {
                    console.error('Error:', xhr.responseText);
                    alert('Error al guardar la correcci\u00f3n.');
                    $btn.prop('disabled', false).html('<i class="fas fa-save mr-1"></i>Guardar Correcci\u00f3n');
                }
            });
        }

        // ============================================================
        // === AUTOCOMPLETE DE CONDUCTOR ===
        // ============================================================

        function initConductorAutocomplete(inputId, suggestionsId, hiddenId) {
            var timer = null;
            var activeIndex = -1;

            var $input = $('#' + inputId);
            var $sugg = $('#' + suggestionsId);
            var $hidden = $('#' + hiddenId);

            $input.on('input', function () {
                var term = $(this).val().trim();
                activeIndex = -1;
                $hidden.val('');

                if (term.length < 2) {
                    $sugg.hide().empty();
                    return;
                }

                clearTimeout(timer);
                timer = setTimeout(function () {
                    $.ajax({
                        type: 'POST',
                        url: 'LiquidacionesPendientes.aspx/BuscarConductores',
                        data: JSON.stringify({ term: term }),
                        contentType: 'application/json; charset=utf-8',
                        dataType: 'json',
                        success: function (response) {
                            var items = JSON.parse(response.d);
                            $sugg.empty();
                            if (items.length === 0) {
                                $sugg.append('<div class="autocomplete-no-results">Sin resultados</div>').show();
                                return;
                            }
                            items.forEach(function (item) {
                                var $item = $('<div class="autocomplete-item"></div>')
                                    .text(item.text)
                                    .attr('data-id', item.id)
                                    .attr('data-text', item.text);
                                $sugg.append($item);
                            });
                            $sugg.show();
                        }
                    });
                }, 280);
            });

            $input.on('keydown', function (e) {
                var $items = $sugg.find('.autocomplete-item');
                if (!$sugg.is(':visible') || $items.length === 0) return;

                if (e.key === 'ArrowDown') {
                    e.preventDefault();
                    activeIndex = Math.min(activeIndex + 1, $items.length - 1);
                    $items.removeClass('active').eq(activeIndex).addClass('active');
                } else if (e.key === 'ArrowUp') {
                    e.preventDefault();
                    activeIndex = Math.max(activeIndex - 1, 0);
                    $items.removeClass('active').eq(activeIndex).addClass('active');
                } else if (e.key === 'Enter') {
                    e.preventDefault();
                    if (activeIndex >= 0) {
                        $items.eq(activeIndex).trigger('click');
                    }
                } else if (e.key === 'Escape') {
                    $sugg.hide().empty();
                    $hidden.val('');
                }
            });

            $(document).on('click', '#' + suggestionsId + ' .autocomplete-item', function () {
                $input.val($(this).attr('data-text'));
                $hidden.val($(this).attr('data-id'));
                $sugg.hide().empty();
                activeIndex = -1;
            });

            // Limpiar selección si el usuario borra el texto manualmente
            $input.on('change', function () {
                if ($(this).val().trim() === '') {
                    $hidden.val('');
                }
            });

            // Cerrar al hacer clic fuera
            $(document).on('click', function (e) {
                if (!$(e.target).closest('#' + inputId + ', #' + suggestionsId).length) {
                    $sugg.hide();
                }
            });
        }

        $(document).ready(function () {
            initConductorAutocomplete('txtConductorBuscar', 'conductorPendientesSugg', 'hfConductorId');
            initConductorAutocomplete('txtConductorAprobadas', 'conductorAprobadasSugg', 'filtroCondAprobadasId');
            $('#<%= btnFiltrar.ClientID %>').on('click', function () {
                return validarFiltrosPendientesCliente();
            });
        });

        function mostrarErrorCampo(selectorInput, selectorError, mensaje) {
            $(selectorInput).addClass('is-invalid');
            $(selectorError).text(mensaje).removeClass('d-none');
        }

        function limpiarErroresFormulario() {
            $('.is-invalid').removeClass('is-invalid');
            $('#errorConductorBuscar,#errorFechaDesde,#errorFechaHasta,#errorPrioridad,#errorRechazoObservaciones,#errorRevertirMotivo,#errorCorregirMotivo').addClass('d-none').text('');
        }

        function validarFiltrosPendientesCliente() {
            limpiarErroresFormulario();
            var valido = true;
            var conductorTexto = $('#txtConductorBuscar').val().trim();
            var conductorId = $('#hfConductorId').val().trim();
            var fechaDesde = $('#<%= txtFechaDesde.ClientID %>').val();
            var fechaHasta = $('#<%= txtFechaHasta.ClientID %>').val();
            var prioridad = $('#<%= ddlPrioridad.ClientID %>').val();

            if (conductorTexto && !conductorId) {
                mostrarErrorCampo('#txtConductorBuscar', '#errorConductorBuscar', 'Seleccione un conductor de la lista sugerida.');
                valido = false;
            }
            if (fechaDesde && fechaHasta && fechaDesde > fechaHasta) {
                mostrarErrorCampo('#<%= txtFechaHasta.ClientID %>', '#errorFechaHasta', 'La fecha "Hasta" debe ser mayor o igual a "Desde".');
                valido = false;
            }
            if (prioridad && ['URGENTE', 'ALTA', 'NORMAL'].indexOf(prioridad) === -1) {
                mostrarErrorCampo('#<%= ddlPrioridad.ClientID %>', '#errorPrioridad', 'La prioridad seleccionada no es válida.');
                valido = false;
            }
            return valido;
        }

        function validarFiltrosAprobadasCliente() {
            var fechaDesde = $('#filtroDesdeAprobadas').val();
            var fechaHasta = $('#filtroHastaAprobadas').val();
            var numeroOrden = ($('#filtroOrdenAprobadas').val() || '').trim();

            if (fechaDesde && fechaHasta && fechaDesde > fechaHasta) {
                alert('En filtros de aprobadas, la fecha "Hasta" debe ser mayor o igual a "Desde".');
                return false;
            }
            if (numeroOrden && !/^[A-Za-z0-9\-_/]{1,30}$/.test(numeroOrden)) {
                alert('El filtro N° Orden solo permite letras, números, guion (-), guion bajo (_) y barra (/), máximo 30 caracteres.');
                $('#filtroOrdenAprobadas').focus();
                return false;
            }
            return true;
        }
    </script>

</asp:Content>

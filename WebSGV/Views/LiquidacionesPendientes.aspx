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
                            <div class="vi-sep"></div>
                            <div class="vi-item">
                                <span class="vi-label"><i class="fas fa-clock mr-1"></i>Hora Llegada</span>
                                <span class="vi-value vi-value-periodo" id="detalleHoraLlegada"></span>
                                <button type="button" id="btnConsultarHoraGps" class="btn btn-sm btn-outline-secondary ml-2" title="Consultar hora real de llegada por GPS (Onway)">
                                    <i class="fas fa-satellite-dish"></i> Verificar GPS
                                </button>
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

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/LiquidacionesPendientes.css") %>" rel="stylesheet" />

    <!-- jQuery y Bootstrap los carga Site.Master -->

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            btnFiltrar: '<%= btnFiltrar.ClientID %>',
            txtFechaDesde: '<%= txtFechaDesde.ClientID %>',
            txtFechaHasta: '<%= txtFechaHasta.ClientID %>',
            ddlPrioridad: '<%= ddlPrioridad.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/LiquidacionesPendientes.js") %>"></script>

</asp:Content>

<%@ Page Title="Liquidaciones Pendientes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" 
    CodeBehind="LiquidacionesPendientes.aspx.cs" Inherits="WebSGV.Views.LiquidacionesPendientes" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdOrdenSeleccionada" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfDetallesJSON" runat="server" ClientIDMode="Static" />

    <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-4">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-4">

        <div class="sgv-encabezado">
            <div>
                <h2>Gestión de liquidaciones</h2>
                <p>Aprueba, revisa y controla las liquidaciones de los conductores</p>
            </div>
        </div>

        <!-- TABS DE NAVEGACIÓN -->
        <div class="sgv-tabs">
            <button type="button" class="sgv-tab activo" id="tabPendientes" onclick="cambiarTab('pendientes')">
                Pendientes de aprobación
                <asp:Label ID="lblTotalPendientes" runat="server" CssClass="sgv-tab-contador" Text="0"></asp:Label>
                <asp:Label ID="lblTotalUrgentes" runat="server" CssClass="sgv-tab-alerta" ToolTip="Liquidaciones esperando más de 24 horas"></asp:Label>
            </button>
            <button type="button" class="sgv-tab" id="tabPorConformar" onclick="cambiarTab('porConformar')"
                title="Revisadas y enviadas al conductor: esperan que firme su conformidad">
                Esperando conformidad
                <span class="sgv-tab-contador" id="lblTotalPorConformar"></span>
            </button>
            <button type="button" class="sgv-tab" id="tabAprobadas" onclick="cambiarTab('aprobadas')">
                Control de aprobadas
            </button>
        </div>

        <!-- SECCIÓN PENDIENTES -->
        <div id="seccionPendientes">

        <div class="sgv-panel mb-3">
            <div class="sgv-panel-cuerpo pb-0">
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

        <div class="sgv-panel sgv-panel-acento">
            <div class="sgv-panel-cab">
                <h5>Liquidaciones pendientes</h5>
                <button type="button" class="btn btn-sm btn-light border" onclick="location.reload()">
                    <i class="fas fa-sync-alt mr-1"></i>Actualizar
                </button>
            </div>
            <div>
                <div class="table-responsive">
                    <asp:GridView ID="gvLiquidacionesPendientes" runat="server"
                        CssClass="table sgv-tabla mb-0"
                        AutoGenerateColumns="false"
                        EmptyDataText="No hay liquidaciones pendientes de aprobación"
                        DataKeyNames="IdOrdenViaje">
                        <Columns>
                            
                            <asp:TemplateField HeaderText="N° orden">
                                <ItemTemplate>
                                    <div class="orden-numero"><%# Eval("NumeroOrdenViaje") %></div>
                                    <div class="sgv-suave"><%# Eval("FechaRegistro", "{0:dd/MM/yyyy HH:mm}") %></div>
                                    <%# EtiquetaObservada(Eval("ObservacionConductor")) %>
                                </ItemTemplate>
                            </asp:TemplateField>

                            <asp:TemplateField HeaderText="Conductor">
                                <ItemTemplate>
                                    <div><%# Eval("NombreConductor") %></div>
                                    <div class="sgv-suave"><%# Eval("PlacaTracto") %> / <%# Eval("PlacaCarreta") %></div>
                                </ItemTemplate>
                            </asp:TemplateField>

                            <asp:TemplateField HeaderText="Periodo">
                                <ItemTemplate>
                                    <span class="text-nowrap"><%# Eval("FechaSalida", "{0:dd/MM/yyyy}") %> – <%# Eval("FechaLlegada", "{0:dd/MM/yyyy}") %></span>
                                </ItemTemplate>
                            </asp:TemplateField>

                            <asp:TemplateField HeaderText="Balance S/">
                                <HeaderStyle CssClass="text-right" />
                                <ItemTemplate>
                                    <span class="<%# ClaseMonto(Eval("BalanceSoles")) %>"><%# Eval("BalanceSoles", "{0:N2}") %></span>
                                </ItemTemplate>
                                <ItemStyle CssClass="sgv-num" />
                            </asp:TemplateField>

                            <asp:TemplateField HeaderText="Balance $">
                                <HeaderStyle CssClass="text-right" />
                                <ItemTemplate>
                                    <span class="<%# ClaseMonto(Eval("BalanceDolares")) %>"><%# Eval("BalanceDolares", "{0:N2}") %></span>
                                </ItemTemplate>
                                <ItemStyle CssClass="sgv-num" />
                            </asp:TemplateField>

                            <asp:TemplateField HeaderText="Espera">
                                <ItemTemplate>
                                    <span class="sgv-estado <%# ClaseEstadoPrioridad(Eval("HorasPendientes")) %>"
                                        title="Prioridad <%# ObtenerPrioridad(Eval("HorasPendientes")) %> (normal &lt; 12 h, alta 12–24 h, urgente &gt; 24 h)">
                                        <%# FormatearTiempo(Eval("HorasPendientes")) %>
                                    </span>
                                </ItemTemplate>
                            </asp:TemplateField>

                            <asp:TemplateField HeaderText="Registrado por">
                                <ItemTemplate>
                                    <span class="sgv-suave"><%# Eval("RegistradoPor") %></span>
                                </ItemTemplate>
                            </asp:TemplateField>

                            <asp:TemplateField HeaderText="">
                                <ItemTemplate>
                                    <div class="sgv-acciones">
                                        <button type="button" class="sgv-btn-icono info"
                                            onclick="verDetalleLiquidacion(<%# Eval("IdOrdenViaje") %>)"
                                            title="Ver detalle">
                                            <i class="fas fa-eye"></i>
                                        </button>

                                        <button type="button" class="sgv-btn-icono ok"
                                            onclick="abrirModalAprobar(<%# Eval("IdOrdenViaje") %>)"
                                            title="Aprobar liquidación">
                                            <i class="fas fa-check"></i>
                                        </button>

                                        <button type="button" class="sgv-btn-icono peligro"
                                            onclick="abrirModalRechazar(<%# Eval("IdOrdenViaje") %>, '<%# Eval("NumeroOrdenViaje") %>')"
                                            title="Rechazar liquidación">
                                            <i class="fas fa-times"></i>
                                        </button>
                                    </div>
                                </ItemTemplate>
                                <ItemStyle CssClass="text-right" Width="170px" />
                            </asp:TemplateField>

                        </Columns>
                        <EmptyDataTemplate>
                            <div class="sgv-vacio">No hay liquidaciones pendientes de aprobación.</div>
                        </EmptyDataTemplate>
                    </asp:GridView>
                </div>
            </div>
        </div>

        </div><!-- /seccionPendientes -->

        <!-- SECCIÓN ESPERANDO CONFORMIDAD DEL CONDUCTOR -->
        <div id="seccionPorConformar" style="display:none;">
            <div class="sgv-panel sgv-panel-acento">
                <div class="sgv-panel-cab">
                    <h5>Esperando la conformidad del conductor <span class="sgv-panel-nota ml-2"><span id="lblResultadosPorConformar">0</span> resultados</span></h5>
                    <button type="button" class="btn btn-sm btn-light border" onclick="cargarLiquidacionesPorConformar()">
                        <i class="fas fa-sync-alt mr-1"></i>Actualizar
                    </button>
                </div>
                <div class="sgv-panel-cuerpo py-2">
                    <p class="sgv-suave mb-0" style="font-size:0.85rem;">
                        La administración ya revisó estas liquidaciones. El viaje se completa cuando el conductor firma su
                        conformidad; si no está de acuerdo, la liquidación vuelve a <em>Pendientes</em> con su observación.
                        Para corregir un monto antes de que firme, use <strong>Retirar envío</strong>.
                    </p>
                </div>
                <div class="table-responsive">
                    <table class="table sgv-tabla mb-0">
                        <thead>
                            <tr>
                                <th>N° orden</th>
                                <th>Conductor</th>
                                <th>Enviada al conductor</th>
                                <th class="text-right">Descuento</th>
                                <th class="text-right">Reintegro</th>
                                <th class="text-right">Saldo final</th>
                                <th></th>
                            </tr>
                        </thead>
                        <tbody id="tbodyPorConformar">
                            <tr><td colspan="7" class="sgv-vacio">Cargando...</td></tr>
                        </tbody>
                    </table>
                </div>
            </div>
        </div><!-- /seccionPorConformar -->

        <!-- SECCIÓN CONTROL DE APROBADAS -->
        <div id="seccionAprobadas" style="display:none;">

            <div class="sgv-panel mb-3">
                <div class="sgv-panel-cuerpo pb-0">
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

            <div class="sgv-panel sgv-panel-acento">
                <div class="sgv-panel-cab">
                    <h5>Liquidaciones aprobadas <span class="sgv-panel-nota ml-2"><span id="lblTotalAprobadas">0</span> resultados</span></h5>
                    <button type="button" class="btn btn-sm btn-light border" onclick="cargarLiquidacionesAprobadas()">
                        <i class="fas fa-sync-alt mr-1"></i>Actualizar
                    </button>
                </div>
                <div>
                    <div id="loadingAprobadas" class="sgv-vacio" style="display:none;">
                        <span class="spinner-border spinner-border-sm mr-2" role="status"></span>Cargando liquidaciones aprobadas...
                    </div>
                    <div class="table-responsive">
                        <table class="table sgv-tabla mb-0" id="tablaAprobadas">
                            <thead>
                                <tr>
                                    <th>N° orden</th>
                                    <th>Conductor</th>
                                    <th>Periodo</th>
                                    <th class="text-right">Descuento</th>
                                    <th class="text-right">Reintegro</th>
                                    <th class="text-right">Balance final</th>
                                    <th></th>
                                </tr>
                            </thead>
                            <tbody id="tbodyAprobadas">
                                <tr>
                                    <td colspan="7" class="sgv-vacio">
                                        Use “Buscar” para cargar las liquidaciones aprobadas.
                                    </td>
                                </tr>
                            </tbody>
                        </table>
                    </div>
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
                            <span id="modalModeLabel">Detalle de liquidación</span>
                            <span class="modal-orden-ref ml-2" id="modalNumeroOrden"></span>
                        </h5>
                        <span class="badge-modo-aprobacion ml-3" id="modalModeBadge" style="display:none;">
                            En aprobación
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
                        <p class="text-muted">Error al cargar el detalle. Por favor, intente de nuevo.</p>
                    </div>

                    <div id="approvalBanner" class="approval-guidance-banner" style="display:none;">
                        <div class="agb-content">
                            <div class="agb-text">
                                <strong>Revisión para aprobación</strong>
                                <span>Revise los gastos declarados por el conductor. Registre el <a href="#" onclick="scrollToAjustes(); return false;" class="agb-link">dinero que se le entregó</a> y, si aplica, descuentos o reintegros. Luego use <strong>Aprobar y enviar al conductor</strong>: el conductor revisará la liquidación final y firmará su conformidad.</span>
                            </div>
                        </div>
                    </div>

                    <div class="detail-section viaje-info-section">
                        <div class="viaje-info-strip">
                            <div class="vi-item vi-item-conductor">
                                <span class="vi-label">Conductor</span>
                                <span class="vi-value" id="detalleConductor"></span>
                            </div>
                            <div class="vi-sep"></div>
                            <div class="vi-item">
                                <span class="vi-label">Tracto</span>
                                <span class="vi-value" id="detalleTracto"></span>
                            </div>
                            <div class="vi-sep"></div>
                            <div class="vi-item">
                                <span class="vi-label">Carreta</span>
                                <span class="vi-value" id="detalleCarreta"></span>
                            </div>
                            <div class="vi-sep"></div>
                            <div class="vi-item">
                                <span class="vi-label">Periodo</span>
                                <span class="vi-value vi-value-periodo" id="detallePeriodo"></span>
                            </div>
                            <div class="vi-sep"></div>
                            <div class="vi-item">
                                <span class="vi-label">Hora de llegada</span>
                                <span class="vi-value vi-value-periodo" id="detalleHoraLlegada"></span>
                                <button type="button" id="btnConsultarHoraGps" class="btn btn-sm btn-outline-secondary ml-2" title="Consultar hora real de llegada por GPS (Onway)">
                                    <i class="fas fa-satellite-dish"></i> Verificar GPS
                                </button>
                            </div>
                        </div>
                        <div id="detalleObservacionConductorRow" style="display:none;" class="vi-obs-row vi-obs-alerta">
                            <span class="vi-obs-label">El conductor observó la revisión anterior<span id="detalleFechaObservacion"></span>:</span>
                            <span class="vi-obs-text" id="detalleObservacionConductor"></span>
                        </div>
                        <div id="detalleObservacionesRow" style="display:none;" class="vi-obs-row">
                            <span class="vi-obs-label">Obs. Conductor:</span>
                            <span class="vi-obs-text" id="detalleObservaciones"></span>
                        </div>
                    </div>

                    <!-- Salida registrada por el conductor + corrección (solo administradora) -->
                    <div class="detail-section salida-section" id="seccionCorregirSalida" style="display:none;">
                        <div class="salida-head">
                            <div class="salida-info">
                                <span class="salida-label">Salida registrada por el conductor</span>
                                <span class="salida-value">
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
                            Resumen Financiero
                        </h6>
                        <div class="rf-grid">
                            <div class="rf-cell rf-ingresos">
                                <div class="rf-cell-header">
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
                                Balance Final
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
                            Desglose de ingresos
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
                            Desglose de gastos
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
                            Dinero entregado y ajustes administrativos
                        </h6>
                        <div class="ajuste-card ajuste-entregado mb-3">
                            <div class="ajuste-header">
                                Dinero entregado al conductor
                                <span class="ajuste-sub">El conductor solo declara sus gastos: registre aquí lo que se le entregó para el viaje.</span>
                            </div>
                            <div class="table-responsive">
                                <table class="table table-sm tabla-entregado mb-0">
                                    <thead>
                                        <tr>
                                            <th>Concepto</th>
                                            <th class="col-monto">Soles (S/)</th>
                                            <th class="col-monto">Dólares ($)</th>
                                            <th>Detalle</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr data-ingreso="Despacho">
                                            <td>Adelanto para el viaje</td>
                                            <td><input type="number" class="form-control form-control-sm ing-soles" step="0.01" min="0" placeholder="0.00" oninput="actualizarBalanceAjustado()"></td>
                                            <td><input type="number" class="form-control form-control-sm ing-dolares" step="0.01" min="0" placeholder="0.00" oninput="actualizarBalanceAjustado()"></td>
                                            <td><input type="text" class="form-control form-control-sm ing-desc" maxlength="250" placeholder="Opcional"></td>
                                        </tr>
                                        <tr data-ingreso="Mensualidad">
                                            <td>Mensualidad</td>
                                            <td><input type="number" class="form-control form-control-sm ing-soles" step="0.01" min="0" placeholder="0.00" oninput="actualizarBalanceAjustado()"></td>
                                            <td><input type="number" class="form-control form-control-sm ing-dolares" step="0.01" min="0" placeholder="0.00" oninput="actualizarBalanceAjustado()"></td>
                                            <td><input type="text" class="form-control form-control-sm ing-desc" maxlength="250" placeholder="Opcional"></td>
                                        </tr>
                                        <tr data-ingreso="Prestamo">
                                            <td>Préstamo</td>
                                            <td><input type="number" class="form-control form-control-sm ing-soles" step="0.01" min="0" placeholder="0.00" oninput="actualizarBalanceAjustado()"></td>
                                            <td><input type="number" class="form-control form-control-sm ing-dolares" step="0.01" min="0" placeholder="0.00" oninput="actualizarBalanceAjustado()"></td>
                                            <td><input type="text" class="form-control form-control-sm ing-desc" maxlength="250" placeholder="Opcional"></td>
                                        </tr>
                                        <tr data-ingreso="OtrosIngresos">
                                            <td>Otros autorizados</td>
                                            <td><input type="number" class="form-control form-control-sm ing-soles" step="0.01" min="0" placeholder="0.00" oninput="actualizarBalanceAjustado()"></td>
                                            <td><input type="number" class="form-control form-control-sm ing-dolares" step="0.01" min="0" placeholder="0.00" oninput="actualizarBalanceAjustado()"></td>
                                            <td><input type="text" class="form-control form-control-sm ing-desc" maxlength="250" placeholder="Opcional"></td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                        </div>
                        <div class="alert-warning-light mb-3" style="font-size:0.85rem;padding:0.75rem;border:1px solid #fde68a;border-radius:0.375rem;background:#fffbeb;">
                            Registra descuentos o reintegros a aplicar al balance del conductor. Deja en cero si no aplica.
                            Cada ajuste necesita su <strong>motivo</strong>: el conductor lo verá en la liquidación final y
                            firmará su conformidad (o la observará) antes de que el viaje se complete.
                        </div>
                        <div class="row">
                            <div class="col-md-6 mb-3">
                                <div class="ajuste-card ajuste-descuento">
                                    <div class="ajuste-header">
                                        Descuento al conductor
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
                                    <div class="mt-2" id="grupoMotivoDescuento">
                                        <label class="form-label" for="ajusteMotivoDescuento">Motivo del descuento</label>
                                        <textarea id="ajusteMotivoDescuento" class="form-control form-control-sm" rows="2" maxlength="500"
                                            placeholder="Ej.: Gasto de alimentación del 18/07 sin comprobante."></textarea>
                                        <small class="text-danger d-none" id="errorMotivoDescuento"></small>
                                    </div>
                                </div>
                            </div>
                            <div class="col-md-6 mb-3">
                                <div class="ajuste-card ajuste-reintegro">
                                    <div class="ajuste-header">
                                        Reintegro al conductor
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
                                    <div class="mt-2" id="grupoMotivoReintegro">
                                        <label class="form-label" for="ajusteMotivoReintegro">Motivo del reintegro</label>
                                        <textarea id="ajusteMotivoReintegro" class="form-control form-control-sm" rows="2" maxlength="500"
                                            placeholder="Ej.: Peaje de Chullqui pagado y no declarado (ticket B002-2)."></textarea>
                                        <small class="text-danger d-none" id="errorMotivoReintegro"></small>
                                    </div>
                                </div>
                            </div>
                        </div>
                        <div id="balanceAjustadoPanel" class="mt-2" style="display:none;">
                            <div class="financial-card financial-card-neutral">
                                <div class="financial-header">
                                    <span>Saldo final (entregado − gastos + descuentos − reintegros) · negativo: la empresa paga al conductor · positivo: el conductor devuelve</span>
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
                                Nota de aprobación
                                <span class="text-muted font-weight-normal">(opcional)</span>
                            </label>
                            <textarea id="notaAprobacion" class="form-control" rows="2" maxlength="500"
                                placeholder="Ej: Aprobado. Comprobantes revisados y conformes."></textarea>
                            <small class="text-muted">Máximo 500 caracteres. se adjuntará a la liquidación como observación administrativa.</small>
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
                        <i class="fas fa-paper-plane mr-1"></i>Aprobar y enviar al conductor
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
                        Rechazar liquidación
                    </h5>
                    <button type="button" class="close" data-dismiss="modal">
                        <span>&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <div class="alert alert-warning">
                        <strong>Atención:</strong> Al rechazar esta liquidación, el viaje se reabrirá automáticamente 
                        para que el conductor pueda corregir los datos.
                    </div>
                    
                    <input type="hidden" id="rechazarIdOrden" />
                    
                    <div class="form-group">
                        <label class="form-label">N° de Orden:</label>
                        <input type="text" id="rechazarNumeroOrden" class="form-control" readonly />
                    </div>

                    <div class="form-group">
                        <label class="form-label">Motivo del rechazo <span class="text-danger">*</span></label>
                        <textarea id="rechazarObservaciones" name="observacionesRechazo" class="form-control" rows="4" 
                            placeholder="Explique detalladamente qué debe corregir el conductor...&#13;&#10;Ejemplo: Los montos de peajes no coinciden con los comprobantes adjuntos. Por favor revisar."></textarea>
                        <small id="errorRechazoObservaciones" class="text-danger d-none"></small>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary-custom" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnConfirmarRechazo" runat="server" 
                        CssClass="btn btn-danger-custom" 
                        Text="Rechazar liquidación"
                        OnClick="btnConfirmarRechazo_Click"
                        OnClientClick="return validarRechazo();" />
                </div>
            </div>
        </div>
    </div>

    <!-- Modal Revertir aprobación -->
    <div class="modal fade" id="modalRevertir" tabindex="-1">
        <div class="modal-dialog">
            <div class="modal-content">
                <div class="modal-header modal-header-danger">
                    <h5 class="modal-title">
                        Revertir aprobación
                    </h5>
                    <button type="button" class="close" data-dismiss="modal"><span>&times;</span></button>
                </div>
                <div class="modal-body">
                    <div class="alert alert-danger">
                        <strong>Atención:</strong> Esta acción devolverá la liquidación al estado
                        <strong>Pendiente de aprobación</strong> y anulará la revisión y la conformidad firmadas (quedan en el historial).
                        Al aprobarla de nuevo, el conductor deberá firmar otra vez su conformidad.
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
                        <label class="form-label">Motivo de la reversión <span class="text-danger">*</span></label>
                        <textarea id="revertirMotivo" class="form-control" rows="4"
                            placeholder="Explique por qué se revierte esta aprobación...&#13;&#10;Ej: Se detectó que el descuento aplicado es incorrecto y necesita re-evaluación."></textarea>
                        <small id="errorRevertirMotivo" class="text-danger d-none"></small>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary-custom" data-dismiss="modal">Cancelar</button>
                    <button type="button" class="btn btn-danger-custom" id="btnConfirmarReversion" onclick="confirmarReversion()">
                        <i class="fas fa-undo mr-1"></i>Confirmar reversión
                    </button>
                </div>
            </div>
        </div>
    </div>

    <!-- Modal Retirar envío al conductor (antes de su conformidad) -->
    <div class="modal fade" id="modalRetirarEnvio" tabindex="-1">
        <div class="modal-dialog">
            <div class="modal-content">
                <div class="modal-header modal-header-corregir">
                    <h5 class="modal-title">Retirar envío al conductor</h5>
                    <button type="button" class="close" data-dismiss="modal"><span>&times;</span></button>
                </div>
                <div class="modal-body">
                    <div class="alert alert-warning">
                        La liquidación volverá a <strong>Pendientes</strong> y la revisión enviada quedará anulada
                        (queda registrada en el historial de firmas). Podrá corregirla y aprobarla de nuevo; el conductor
                        recibirá la nueva versión para firmar su conformidad.
                    </div>
                    <input type="hidden" id="retirarIdOrden" />
                    <div class="form-group">
                        <label class="form-label">N° de Orden:</label>
                        <input type="text" id="retirarNumeroOrden" class="form-control" readonly />
                    </div>
                    <div class="form-group">
                        <label class="form-label" for="retirarMotivo">Motivo <span class="text-danger">*</span></label>
                        <textarea id="retirarMotivo" class="form-control" rows="3" maxlength="500"
                            placeholder="Ej.: El adelanto registrado era S/ 800 y no S/ 900."></textarea>
                        <small id="errorRetirarMotivo" class="text-danger d-none"></small>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary-custom" data-dismiss="modal">Cancelar</button>
                    <button type="button" class="btn btn-primary-custom" id="btnConfirmarRetiro" onclick="confirmarRetiroEnvio()">
                        <i class="fas fa-undo mr-1"></i>Retirar envío
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

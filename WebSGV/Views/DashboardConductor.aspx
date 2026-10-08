<%@ Page Title="Mi Dashboard - Conductor" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DashboardConductor.aspx.cs" Inherits="WebSGV.Views.DashboardConductor" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <!-- HiddenFields para datos -->
    <asp:HiddenField ID="hfIdConductor" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdViajeActivo" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdsViajesActivos" runat="server" ClientIDMode="Static" Value="" />
    <asp:HiddenField ID="hfEsInternacional" runat="server" ClientIDMode="Static" Value="0" />
    <asp:HiddenField ID="hfGastosFinancieros" runat="server" ClientIDMode="Static" Value="[]" />
    <asp:HiddenField ID="hfIngresosAdicionales" runat="server" ClientIDMode="Static" Value="[]" />
    <asp:HiddenField ID="hfGastosAdicionales" runat="server" ClientIDMode="Static" Value="[]" />
    <asp:HiddenField ID="hfNumeroOrdenExistente" runat="server" ClientIDMode="Static" Value="" />

    <!-- Panel de mensajes -->
    <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-4">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="dash-conductor">

        <!-- Header mobile-first con gradiente -->
        <div class="cd-header">
            <h2><i class="fas fa-tachometer-alt mr-2"></i>Mi Dashboard</h2>
            <p class="cd-sub">
                <strong><asp:Label ID="lblNombreConductor" runat="server"></asp:Label></strong>
                &nbsp;|&nbsp; DNI: <asp:Label ID="lblDNIConductor" runat="server"></asp:Label>
            </p>
            <asp:Panel ID="pnlEstadoViaje" runat="server" CssClass="cd-estado-badge">
                <i class="fas fa-circle mr-1"></i>
                <asp:Label ID="lblEstadoViaje" runat="server" Text="Sin viajes activos"></asp:Label>
            </asp:Panel>
        </div>

        <!-- PESTA&#209;AS PRINCIPALES -->
        <div class="cd-tabs-wrapper">
            <div class="row">
                <div class="col-12">
                    <ul class="nav nav-tabs-custom mb-0" id="conductorTabs" role="tablist">
                    <li class="nav-item">
                        <a class="nav-link active" id="viajesActivos-tab" data-toggle="tab" href="#viajesActivos" role="tab">
                            <i class="fas fa-truck-loading mr-2"></i><span class="d-none d-sm-inline">Mis </span>Viajes
                            <asp:Panel ID="pnlBadgeActivos" runat="server" CssClass="badge badge-warning ml-2" Visible="false">
                                <asp:Label ID="lblCantidadActivos" runat="server"></asp:Label>
                            </asp:Panel>
                        </a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link" id="liquidarViaje-tab" data-toggle="tab" href="#liquidarViaje" role="tab">
                            <i class="fas fa-calculator mr-2"></i>Liquidar
                            <asp:Panel ID="pnlBadgeLiquidar" runat="server" CssClass="badge badge-danger ml-2" Visible="false">
                                <i class="fas fa-exclamation"></i>
                            </asp:Panel>
                        </a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link" id="historial-tab" data-toggle="tab" href="#historial" role="tab">
                            <i class="fas fa-history mr-2"></i>Historial
                        </a>
                    </li>
                </ul>

                <div class="tab-content tab-content-custom" id="conductorTabsContent">

                    <!-- ========================================== -->
                    <!-- TAB 1: MIS VIAJES ACTIVOS -->
                    <!-- ========================================== -->
                    <div class="tab-pane fade show active" id="viajesActivos" role="tabpanel">
                        <div class="p-3 p-md-4">

                            <!-- Info del Viaje Activo -->
                            <asp:Panel ID="pnlViajeActivo" runat="server" Visible="false">
                                <div class="alert alert-info-conductor mb-4">
                                    <div class="row align-items-center">
                                        <div class="col-12 col-md-8">
                                            <h5 class="mb-2">
                                                <i class="fas fa-route mr-2"></i>Viaje en Progreso
                                            </h5>
                                            <div class="info-grid">
                                                <div class="info-item">
                                                    <span class="info-label">N° Viaje:</span>
                                                    <span class="info-value">
                                                        <asp:Label ID="lblNumeroViaje" runat="server"></asp:Label></span>
                                                </div>
                                                <div class="info-item">
                                                    <span class="info-label">Fecha Inicio:</span>
                                                    <span class="info-value">
                                                        <asp:Label ID="lblFechaInicio" runat="server"></asp:Label></span>
                                                </div>
                                                <div class="info-item">
                                                    <span class="info-label">Días en Ruta:</span>
                                                    <span class="info-value">
                                                        <asp:Label ID="lblDiasRuta" runat="server"></asp:Label></span>
                                                </div>
                                                <div class="info-item">
                                                    <span class="info-label">Despachos:</span>
                                                    <span class="info-value">
                                                        <asp:Label ID="lblCantidadDespachos" runat="server"></asp:Label></span>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="col-12 col-md-4 text-center text-md-right mt-3 mt-md-0">
                                            <asp:Button ID="btnIrLiquidar" runat="server"
                                                Text="Liquidar Ahora"
                                                CssClass="btn btn-success-custom btn-lg btn-block btn-md-auto"
                                                OnClientClick="$('#liquidarViaje-tab').tab('show'); return false;" />
                                        </div>
                                    </div>
                                </div>
                            </asp:Panel>

                            <!-- Sin viajes activos -->
                            <asp:Panel ID="pnlSinViajes" runat="server" Visible="true">
                                <div class="empty-state">
                                    <i class="fas fa-inbox empty-icon"></i>
                                    <h4>No tienes viajes activos</h4>
                                    <p class="text-muted">Actualmente no tienes despachos programados. Contacta con la administración.</p>
                                </div>
                            </asp:Panel>

                            <!-- Tabla de Despachos del Viaje Activo -->
                            <asp:Panel ID="pnlDespachos" runat="server" Visible="false">
                                <div class="section-card">
                                    <div class="section-header section-header-primary">
                                        <h5 class="section-title">
                                            <i class="fas fa-list-alt mr-2"></i>Despachos de Este Viaje
                                        </h5>
                                    </div>
                                    <div class="section-body p-0">
                                        <div class="table-responsive">
                                            <asp:GridView ID="gvDespachosActivos" runat="server"
                                                CssClass="table table-conductor mb-0"
                                                AutoGenerateColumns="false"
                                                EmptyDataText="No hay despachos registrados">
                                                <Columns>
                                                    <asp:BoundField DataField="NumeroDespacho" HeaderText="N° DESPACHO"
                                                        ItemStyle-CssClass="font-weight-bold text-primary" />
                                                    <asp:BoundField DataField="FechaDespacho" HeaderText="FECHA"
                                                        DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="text-center" />
                                                    <asp:BoundField DataField="Cliente" HeaderText="CLIENTE" />
                                                    <asp:BoundField DataField="TipoOperacion" HeaderText="OPERACIÓN"
                                                        ItemStyle-CssClass="text-center" />
                                                    <asp:BoundField DataField="LugarOperacion" HeaderText="PLANTA" />
                                                    <asp:TemplateField HeaderText="TRACTO">
                                                        <ItemTemplate>
                                                            <span class="badge badge-vehicle"><%# Eval("PlacaTracto") %></span>
                                                        </ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:TemplateField HeaderText="CARRETA">
                                                        <ItemTemplate>
                                                            <span class="badge badge-vehicle"><%# Eval("PlacaCarreta") %></span>
                                                        </ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:TemplateField HeaderText="ESTADO">
                                                        <ItemTemplate>
                                                            <span class='badge-estado badge-estado-<%# ObtenerClaseEstado(Eval("EstadoDespacho")) %>'>
                                                                <%# Eval("EstadoDespacho") %>
                                                            </span>
                                                        </ItemTemplate>
                                                        <ItemStyle CssClass="text-center" />
                                                    </asp:TemplateField>
                                                </Columns>
                                            </asp:GridView>
                                        </div>
                                    </div>
                                </div>

                                <!-- Información Importante -->
                                <div class="alert alert-warning-conductor mt-4">
                                    <i class="fas fa-info-circle mr-2"></i>
                                    <strong>Importante:</strong> Una vez que completes todos los despachos, debes registrar tu liquidación en la pestaña "Liquidar Mi Viaje".
                                </div>
                            </asp:Panel>

                        </div>
                    </div>

                    <!-- ========================================== -->
                    <!-- TAB 2: LIQUIDAR MI VIAJE -->
                    <!-- ========================================== -->
                    <div class="tab-pane fade" id="liquidarViaje" role="tabpanel">
                        <div class="p-3 p-md-4">

                            <!-- Verificación de viaje activo -->
                            <asp:Panel ID="pnlSinViajeParaLiquidar" runat="server" Visible="true">
                                <div class="empty-state">
                                    <i class="fas fa-clipboard-list empty-icon"></i>
                                    <h4>No hay viajes para liquidar</h4>
                                    <p class="text-muted">Debes tener un viaje activo para poder registrar la liquidación.</p>
                                </div>
                            </asp:Panel>

                            <!-- Formulario de Liquidación -->
                            <asp:Panel ID="pnlFormularioLiquidacion" runat="server" Visible="false">

                                <!-- Resumen del Viaje a Liquidar -->
                                <div class="section-card mb-4">
                                    <div class="section-header section-header-info">
                                        <h5 class="section-title">
                                            <i class="fas fa-info-circle mr-2"></i>Resumen del Viaje a Liquidar
                                        </h5>
                                    </div>
                                    <div class="section-body">
                                        <div class="row">
                                            <div class="col-6 col-md-3">
                                                <label class="form-label-summary">N° de Viaje</label>
                                                <p class="form-value-summary">
                                                    <asp:Label ID="lblNumeroViajeResumen" runat="server"></asp:Label>
                                                </p>
                                            </div>
                                            <div class="col-6 col-md-3">
                                                <label class="form-label-summary">Fecha Inicio</label>
                                                <p class="form-value-summary">
                                                    <asp:Label ID="lblFechaInicioResumen" runat="server"></asp:Label>
                                                </p>
                                            </div>
                                            <div class="col-6 col-md-3">
                                                <label class="form-label-summary">Despachos</label>
                                                <p class="form-value-summary">
                                                    <asp:Label ID="lblDespachosResumen" runat="server"></asp:Label>
                                                </p>
                                            </div>
                                            <div class="col-6 col-md-3">
                                                <label class="form-label-summary">Estado</label>
                                                <p class="form-value-summary"><span class="badge badge-info">En Progreso</span></p>
                                            </div>
                                        </div>
                                    </div>
                                </div>

                                <!-- Datos Generales de la Liquidación -->
                                <div class="section-card mb-4">
                                    <div class="section-header">
                                        <h5 class="section-title">
                                            <i class="fas fa-clipboard-list mr-2"></i>Datos Generales
                                        </h5>
                                    </div>
                                    <div class="section-body">
                                        <div class="row">
                                            <div class="col-12 col-md-3">
                                                <div class="form-group">
                                                    <label class="form-label">Fecha Salida <span class="text-danger">*</span></label>
                                                    <asp:TextBox ID="txtFechaSalida" runat="server" CssClass="form-control" TextMode="Date" required></asp:TextBox>
                                                </div>
                                            </div>
                                            <div class="col-12 col-md-3">
                                                <div class="form-group">
                                                    <label class="form-label">Fecha Llegada <span class="text-danger">*</span></label>
                                                    <asp:TextBox ID="txtFechaLlegada" runat="server" CssClass="form-control" TextMode="Date" required ReadOnly="true"></asp:TextBox>
                                                </div>
                                            </div>
                                            <div class="col-12 col-md-3">
                                                <div class="form-group">
                                                    <label class="form-label">Hora Salida <span class="text-danger">*</span></label>
                                                    <asp:TextBox ID="txtHoraSalida" runat="server" CssClass="form-control" TextMode="Time" required></asp:TextBox>
                                                </div>
                                            </div>
                                            <div class="col-12 col-md-3">
                                                <div class="form-group">
                                                    <label class="form-label">Hora de Envío (automática)</label>
                                                    <asp:TextBox ID="txtHoraLlegada" runat="server" CssClass="form-control" TextMode="Time" ReadOnly="true"></asp:TextBox>
                                                </div>
                                            </div>
                                            <div class="col-12 col-md-3">
                                                <div class="form-group">
                                                    <label class="form-label">Hora Real de Llegada a Base</label>
                                                    <asp:TextBox ID="txtHoraLlegadaDeclarada" runat="server" CssClass="form-control" TextMode="Time"></asp:TextBox>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="row">
                                            <div class="col-12">
                                                <small class="text-muted">La "Hora de Envío" la registra el sistema automáticamente y no se puede editar. Si llegaste antes de registrar la liquidación, ajusta la "Hora Real de Llegada" a la hora en que realmente llegaste a base.</small>
                                            </div>
                                        </div>
                                        <div class="row">
                                            <div class="col-12">
                                                <div class="form-group">
                                                    <label class="form-label">Observaciones del Viaje</label>
                                                    <asp:TextBox ID="txtObservaciones" runat="server" CssClass="form-control"
                                                        TextMode="MultiLine" Rows="2" placeholder="Describe cualquier eventualidad o comentario importante del viaje"></asp:TextBox>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>

                                <!-- GESTIÓN FINANCIERA -->

                                <!-- Ingresos -->
                                <div class="section-card mb-4">
                                    <div class="section-header section-header-success">
                                        <h5 class="section-title">
                                            <i class="fas fa-plus-circle mr-2"></i>Ingresos del Viaje
                                        </h5>
                                    </div>
                                    <div class="section-body">
                                        <div class="alert alert-info-light mb-3">
                                            <i class="fas fa-lightbulb mr-2"></i>
                                            Registra todos los <strong>ingresos</strong> que recibiste para este viaje.
                                        </div>
                                        <div class="table-responsive">
                                            <table class="table table-financial">
                                                <thead>
                                                    <tr>
                                                        <th style="width: 5%">#</th>
                                                        <th style="width: 20%">Concepto</th>
                                                        <th style="width: 30%">Descripción</th>
                                                        <th style="width: 18%">Soles (S/)</th>
                                                        <th style="width: 18%">Dólares ($)</th>
                                                        <th style="width: 9%">Acción</th>
                                                    </tr>
                                                </thead>
                                                <tbody id="ingresosBody">
                                                    <tr>
                                                        <td class="text-center">1</td>
                                                        <td><strong>Despacho</strong></td>
                                                        <td>
                                                            <input type="text" class="form-control form-control-sm" name="descDespacho"
                                                                placeholder="Adelanto o pago por despacho">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm ingreso-soles"
                                                                name="despachoSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm ingreso-dolares"
                                                                name="despachoDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td class="text-center"><span class="badge badge-fixed">Fijo</span></td>
                                                    </tr>
                                                    <tr style="display:none">
                                                        <td class="text-center">2</td>
                                                        <td><strong>Mensualidad</strong></td>
                                                        <td>
                                                            <input type="text" class="form-control form-control-sm" name="descMensualidad"
                                                                placeholder="Pago de mensualidad del tracto">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm ingreso-soles"
                                                                name="mensualidadSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm ingreso-dolares"
                                                                name="mensualidadDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td class="text-center"><span class="badge badge-fixed">Fijo</span></td>
                                                    </tr>
                                                    <tr style="display:none">
                                                        <td class="text-center">3</td>
                                                        <td><strong>Otros Autorizados</strong></td>
                                                        <td>
                                                            <input type="text" class="form-control form-control-sm" name="descOtros"
                                                                placeholder="Otros ingresos autorizados">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm ingreso-soles"
                                                                name="otrosSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm ingreso-dolares"
                                                                name="otrosDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td class="text-center"><span class="badge badge-fixed">Fijo</span></td>
                                                    </tr>
                                                    <tr style="display:none">
                                                        <td class="text-center">4</td>
                                                        <td><strong>Préstamo</strong></td>
                                                        <td>
                                                            <input type="text" class="form-control form-control-sm" name="descPrestamo"
                                                                placeholder="Préstamos recibidos">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm ingreso-soles"
                                                                name="prestamoSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm ingreso-dolares"
                                                                name="prestamoDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td class="text-center"><span class="badge badge-fixed">Fijo</span></td>
                                                    </tr>
                                                </tbody>
                                                <tbody id="ingresosAdicionalesBody"></tbody>
                                                <tfoot>
                                                    <tr>
                                                        <td colspan="6" class="text-left pt-3">
                                                            <button type="button" class="btn btn-success-custom btn-sm" onclick="agregarIngreso()">
                                                                <i class="fas fa-plus mr-1"></i>Agregar Otro Ingreso
                                                            </button>
                                                        </td>
                                                    </tr>
                                                    <tr class="total-row">
                                                        <td colspan="3" class="text-right"><strong>Total Ingresos:</strong></td>
                                                        <td><strong>S/ <span id="totalIngresosSoles">0.00</span></strong></td>
                                                        <td><strong>$ <span id="totalIngresosDolares">0.00</span></strong></td>
                                                        <td></td>
                                                    </tr>
                                                </tfoot>
                                            </table>
                                        </div>
                                    </div>
                                </div>

                                <!-- Gastos -->
                                <div class="section-card mb-4">
                                    <div class="section-header section-header-danger">
                                        <h5 class="section-title">
                                            <i class="fas fa-minus-circle mr-2"></i>Gastos del Viaje
                                        </h5>
                                    </div>
                                    <div class="section-body">
                                        <div class="alert alert-warning-light mb-3">
                                            <i class="fas fa-exclamation-triangle mr-2"></i>
                                            Registra <strong>todos los gastos</strong> con sus comprobantes. Entrega los f&#237;sicos en oficina.
                                        </div>
                                        <div class="table-responsive">
                                            <table class="table table-financial">
                                                <thead>
                                                    <tr>
                                                        <th style="width: 5%">#</th>
                                                        <th style="width: 20%">Concepto</th>
                                                        <th style="width: 30%">Descripción</th>
                                                        <th style="width: 18%">Soles (S/)</th>
                                                        <th style="width: 18%">Dólares ($)</th>
                                                        <th style="width: 9%">Acción</th>
                                                    </tr>
                                                </thead>
                                                <tbody id="gastosBody">
                                                    <!-- Peajes Nacionales (Perú) - Solo Soles -->
                                                    <tr id="filaPeajesNacionales">
                                                        <td class="text-center">1</td>
                                                        <td><strong>Peajes Nacionales</strong><br/><small class="text-muted">Perú (S/)</small></td>
                                                        <td>
                                                            <input type="text" class="form-control form-control-sm"
                                                                name="descPeajesNacionales" id="descPeajesNacionales" placeholder="Observaciones peajes nacionales">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm gasto-soles"
                                                                name="peajesNacSoles" id="peajesNacSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm form-control-readonly"
                                                                value="0.00" readonly title="Peajes nacionales solo en soles">
                                                        </td>
                                                        <td class="text-center"><span class="badge badge-fixed">Manual</span></td>
                                                    </tr>
                                                    <!-- Peajes Extranjeros (Ecuador) - Solo Dólares -->
                                                    <tr id="filaPeajesExtranjeros">
                                                        <td class="text-center">2</td>
                                                        <td><strong>Peajes Extranjeros</strong><br/><small class="text-muted">Ecuador ($)</small></td>
                                                        <td>
                                                            <input type="text" class="form-control form-control-sm"
                                                                name="descPeajesExtranjeros" id="descPeajesExtranjeros" placeholder="Observaciones peajes extranjeros">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm form-control-readonly"
                                                                value="0.00" readonly title="Peajes extranjeros solo en dólares">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm gasto-dolares"
                                                                name="peajesExtDolares" id="peajesExtDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td class="text-center"><span class="badge badge-fixed">Manual</span></td>
                                                    </tr>

                                                    <!-- Alimentación -->
                                                    <tr>
                                                        <td class="text-center">3</td>
                                                        <td><strong>Alimentación</strong></td>
                                                        <td>
                                                            <input type="text" class="form-control form-control-sm" name="descAlimentacion"
                                                                placeholder="Comidas durante el viaje">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm gasto-soles"
                                                                name="alimentacionSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm gasto-dolares"
                                                                name="alimentacionDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td class="text-center"><span class="badge badge-fixed">Manual</span></td>
                                                    </tr>

                                                    <!-- Apoyo-Seguridad -->
                                                    <tr>
                                                        <td class="text-center">4</td>
                                                        <td><strong>Apoyo-Seguridad</strong></td>
                                                        <td>
                                                            <input type="text" class="form-control form-control-sm" name="descApoyoSeguridad"
                                                                placeholder="Gastos de apoyo o seguridad">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm gasto-soles"
                                                                name="apoyoSeguridadSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm gasto-dolares"
                                                                name="apoyoSeguridadDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td class="text-center"><span class="badge badge-fixed">Manual</span></td>
                                                    </tr>

                                                    <!-- Reparaciones -->
                                                    <tr>
                                                        <td class="text-center">5</td>
                                                        <td>
                                                            <strong>Reparaciones Varios</strong>
                                                            <button type="button" class="btn btn-detail-modal btn-sm ml-2" onclick="abrirModalReparaciones()">
                                                                <i class="fas fa-edit"></i>Detalle
                                                            </button>
                                                        </td>
                                                        <td>
                                                            <input type="text" class="form-control form-control-sm form-control-readonly"
                                                                name="descReparaciones" id="descReparaciones" readonly placeholder="Sin reparaciones registradas">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm form-control-readonly gasto-soles"
                                                                name="reparacionesSoles" id="reparacionesSoles" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm form-control-readonly gasto-dolares"
                                                                name="reparacionesDolares" id="reparacionesDolares" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td class="text-center"><span class="badge badge-count" id="contadorReparaciones">0</span></td>
                                                    </tr>

                                                    <!-- Movilidad -->
                                                    <tr>
                                                        <td class="text-center">6</td>
                                                        <td><strong>Movilidad</strong></td>
                                                        <td>
                                                            <input type="text" class="form-control form-control-sm" name="descMovilidad"
                                                                placeholder="Gastos de movilidad">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm gasto-soles"
                                                                name="movilidadSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm gasto-dolares"
                                                                name="movilidadDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td class="text-center"><span class="badge badge-fixed">Manual</span></td>
                                                    </tr>

                                                    <!-- Encarpada/Desencarpada -->
                                                    <tr>
                                                        <td class="text-center">7</td>
                                                        <td><strong>Encarpada/Desencarpada</strong></td>
                                                        <td>
                                                            <input type="text" class="form-control form-control-sm" name="descEncapada"
                                                                placeholder="Gastos de encarpada/desencarpada">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm gasto-soles"
                                                                name="encapadaSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm gasto-dolares"
                                                                name="encapadaDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td class="text-center"><span class="badge badge-fixed">Manual</span></td>
                                                    </tr>

                                                    <!-- Hospedaje -->
                                                    <tr>
                                                        <td class="text-center">8</td>
                                                        <td>
                                                            <strong>Hospedaje</strong>
                                                            <button type="button" class="btn btn-detail-modal btn-sm ml-2" onclick="abrirModalHospedaje()">
                                                                <i class="fas fa-edit"></i>Detalle
                                                            </button>
                                                        </td>
                                                        <td>
                                                            <input type="text" class="form-control form-control-sm form-control-readonly"
                                                                name="descHospedaje" id="descHospedaje" readonly placeholder="Sin hospedajes registrados">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm form-control-readonly gasto-soles"
                                                                name="hospedajeSoles" id="hospedajeSoles" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm form-control-readonly gasto-dolares"
                                                                name="hospedajeDolares" id="hospedajeDolares" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td class="text-center"><span class="badge badge-count" id="contadorHospedaje">0</span></td>
                                                    </tr>

                                                    <!-- Combustible -->
                                                    <tr>
                                                        <td class="text-center">9</td>
                                                        <td>
                                                            <strong>Combustible</strong>
                                                            <button type="button" class="btn btn-detail-modal btn-sm ml-2" onclick="abrirModalCombustible()">
                                                                <i class="fas fa-edit"></i>Detalle
                                                            </button>
                                                        </td>
                                                        <td>
                                                            <input type="text" class="form-control form-control-sm form-control-readonly"
                                                                name="descCombustible" id="descCombustible" readonly placeholder="Sin combustibles registrados">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm form-control-readonly gasto-soles"
                                                                name="combustibleSoles" id="combustibleSoles" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td>
                                                            <input type="number" class="form-control form-control-sm form-control-readonly gasto-dolares"
                                                                name="combustibleDolares" id="combustibleDolares" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                        </td>
                                                        <td class="text-center"><span class="badge badge-count" id="contadorCombustible">0</span></td>
                                                    </tr>
                                                </tbody>
                                                <tbody id="gastosAdicionalesBody"></tbody>
                                                <tfoot>
                                                    <tr>
                                                        <td colspan="6" class="text-left pt-3">
                                                            <button type="button" class="btn btn-danger-custom btn-sm" onclick="agregarGasto()">
                                                                <i class="fas fa-plus mr-1"></i>Agregar Otro Gasto
                                                            </button>
                                                        </td>
                                                    </tr>
                                                    <tr class="total-row">
                                                        <td colspan="3" class="text-right"><strong>Total Gastos:</strong></td>
                                                        <td><strong>S/ <span id="totalGastosSoles">0.00</span></strong></td>
                                                        <td><strong>$ <span id="totalGastosDolares">0.00</span></strong></td>
                                                        <td></td>
                                                    </tr>
                                                </tfoot>
                                            </table>
                                        </div>
                                    </div>
                                </div>

                                <!-- Balance Final -->
                                <div class="section-card mb-4">
                                    <div class="section-header section-header-neutral">
                                        <h5 class="section-title">
                                            <i class="fas fa-balance-scale mr-2"></i>Balance Final del Viaje
                                        </h5>
                                    </div>
                                    <div class="section-body">
                                        <div class="balance-final">
                                            <div class="row">
                                                <div class="col-12 col-md-6 mb-3 mb-md-0">
                                                    <div class="balance-item">
                                                        <span class="balance-label">Balance en Soles:</span>
                                                        <span class="balance-amount" id="diferenciaSoles">S/ 0.00</span>
                                                    </div>
                                                </div>
                                                <div class="col-12 col-md-6">
                                                    <div class="balance-item">
                                                        <span class="balance-label">Balance en Dólares:</span>
                                                        <span class="balance-amount" id="diferenciaDolares">$ 0.00</span>
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="alert alert-info-light mt-3 mb-0">
                                            <i class="fas fa-info-circle mr-2"></i>
                                            El balance muestra la diferencia entre ingresos y gastos. Un balance positivo indica que te queda dinero, un balance negativo indica que gastaste más de lo recibido.
                                        </div>
                                    </div>
                                </div>

                                <!-- Bot&#243;n de Env&#237;o -->
                                <div class="text-center mb-5" style="padding: 0 12px;">
                                    <asp:Button ID="btnEnviarLiquidacion" runat="server"
                                        Text="Enviar Liquidaci&#243;n"
                                        CssClass="btn btn-primary-conductor btn-lg px-4 px-md-5 mb-2 mb-md-0"
                                        OnClick="btnEnviarLiquidacion_Click"
                                        OnClientClick="prepararDatosFinancieros(); return confirmarEnvioLiquidacion();" />
                                    <button type="button" class="btn btn-secondary-custom btn-lg ml-0 ml-md-3 px-3 px-md-4" onclick="limpiarFormulario()">
                                        <i class="fas fa-times mr-2"></i>Limpiar
                                    </button>
                                </div>

                            </asp:Panel>

                        </div>
                    </div>

                    <!-- ========================================== -->
                    <!-- TAB 3: MI HISTORIAL -->
                    <!-- ========================================== -->
                    <div class="tab-pane fade" id="historial" role="tabpanel">
                        <div class="p-3 p-md-4">

                            <!-- Filtros -->
                            <div class="section-card mb-4">
                                <div class="section-header">
                                    <h5 class="section-title">
                                        <i class="fas fa-filter mr-2"></i>Filtrar Historial
                                    </h5>
                                </div>
                                <div class="section-body">
                                    <div class="row">
                                        <div class="col-12 col-md-3">
                                            <div class="form-group">
                                                <label class="form-label">Desde</label>
                                                <asp:TextBox ID="txtFechaDesde" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                            </div>
                                        </div>
                                        <div class="col-12 col-md-3">
                                            <div class="form-group">
                                                <label class="form-label">Hasta</label>
                                                <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                            </div>
                                        </div>
                                        <div class="col-12 col-md-3">
                                            <div class="form-group">
                                                <label class="form-label">Estado</label>
                                                <asp:DropDownList ID="ddlEstadoFiltro" runat="server" CssClass="form-control">
                                                    <asp:ListItem Value="">-- Todos --</asp:ListItem>
                                                    <asp:ListItem Value="COMPLETADO">Completado</asp:ListItem>
                                                    <asp:ListItem Value="PENDIENTE">Pendiente Revisión</asp:ListItem>
                                                </asp:DropDownList>
                                            </div>
                                        </div>
                                        <div class="col-12 col-md-3">
                                            <div class="form-group">
                                                <label class="form-label d-none d-md-block">&nbsp;</label>
                                                <div>
                                                    <asp:Button ID="btnBuscarHistorial" runat="server" Text="Buscar"
                                                        CssClass="btn btn-primary-custom btn-block" OnClick="btnBuscarHistorial_Click" />
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <!-- Tabla de Historial -->
                            <div class="section-card">
                                <div class="section-header section-header-info">
                                    <div class="d-flex justify-content-between align-items-center flex-wrap">
                                        <h5 class="section-title mb-2 mb-md-0">
                                            <i class="fas fa-list mr-2"></i>Mis Liquidaciones
                                        </h5>
                                        <span class="badge badge-primary-custom">
                                            <asp:Label ID="lblTotalHistorial" runat="server" Text="0 registros"></asp:Label>
                                        </span>
                                    </div>
                                </div>
                                <div class="section-body p-0">
                                    <div class="table-responsive">
                                        <asp:GridView ID="gvHistorial" runat="server"
                                            CssClass="table table-conductor mb-0"
                                            AutoGenerateColumns="false"
                                            EmptyDataText="No hay liquidaciones registradas"
                                            OnRowCommand="gvHistorial_RowCommand">
                                            <Columns>
                                                <asp:BoundField DataField="NumeroViaje" HeaderText="N° VIAJE"
                                                    ItemStyle-CssClass="font-weight-bold text-primary" />
                                                <asp:BoundField DataField="FechaSalida" HeaderText="FECHA SALIDA"
                                                    DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="text-center" />
                                                <asp:BoundField DataField="FechaLlegada" HeaderText="FECHA LLEGADA"
                                                    DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="text-center" />
                                                <asp:BoundField DataField="CantidadDespachos" HeaderText="DESPACHOS"
                                                    ItemStyle-CssClass="text-center" />
                                                <asp:TemplateField HeaderText="BALANCE S/">
                                                    <ItemTemplate>
                                                        <span class='<%# ObtenerClaseBalance(Eval("BalanceSoles")) %>'>S/ <%# Eval("BalanceSoles", "{0:N2}") %>
                                                        </span>
                                                    </ItemTemplate>
                                                    <ItemStyle CssClass="text-right" />
                                                </asp:TemplateField>
                                                <asp:TemplateField HeaderText="BALANCE $">
                                                    <ItemTemplate>
                                                        <span class='<%# ObtenerClaseBalance(Eval("BalanceDolares")) %>'>$ <%# Eval("BalanceDolares", "{0:N2}") %>
                                                        </span>
                                                    </ItemTemplate>
                                                    <ItemStyle CssClass="text-right" />
                                                </asp:TemplateField>
                                                <asp:TemplateField HeaderText="ESTADO">
                                                    <ItemTemplate>
                                                        <span class='badge-estado badge-estado-<%# ObtenerClaseEstado(Eval("Estado")) %>'>
                                                            <%# Eval("Estado") %>
                                                        </span>
                                                    </ItemTemplate>
                                                    <ItemStyle CssClass="text-center" />
                                                </asp:TemplateField>
                                                <asp:TemplateField HeaderText="ACCIONES">
                                                    <ItemTemplate>
                                                        <button type="button" class="btn btn-info-custom btn-sm"
                                                            onclick='verDetalleLiquidacion(<%# Eval("IdOrdenViaje") %>)'>
                                                            <i class="fas fa-eye"></i>
                                                        </button>
                                                        <asp:Panel runat="server" Visible='<%# Eval("Estado").ToString() == "PENDIENTE" %>' style="display:inline-block;">
                                                            <button type="button" class="btn btn-sm ml-1 btn-retirar-liq"
                                                                onclick='abrirModalRetirar(<%# Eval("IdOrdenViaje") %>)'
                                                                title="Retirar liquidación para corregir">
                                                                <i class="fas fa-undo mr-1"></i>Retirar
                                                            </button>
                                                        </asp:Panel>
                                                    </ItemTemplate>
                                                    <ItemStyle CssClass="text-center" />
                                                </asp:TemplateField>
                                            </Columns>
                                        </asp:GridView>
                                    </div>
                                </div>
                            </div>

                        </div>
                    </div>

                </div>
            </div>
        </div>
    </div>

    <!-- ========== MODALES ========== -->

    <!-- Modal Peajes eliminado - Ahora se usan campos directos de Peajes Nacionales/Extranjeros -->

    <!-- Modal Reparaciones -->
    <div class="modal fade" id="modalReparaciones" tabindex="-1">
        <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
            <div class="modal-content">
                <div class="modal-header modal-header-custom">
                    <h5 class="modal-title"><i class="fas fa-wrench mr-2"></i>Registrar Reparaciones</h5>
                    <button type="button" class="close" data-dismiss="modal"><span>&times;</span></button>
                </div>
                <div class="modal-body">
                    <div class="card card-form mb-3">
                        <div class="card-header"><i class="fas fa-plus mr-2"></i>Agregar Reparación</div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-12 col-md-6">
                                    <label class="form-label">Tipo de Reparación <span class="text-danger">*</span></label>
                                    <input type="text" class="form-control form-control-sm" id="nuevaReparacionTipo" placeholder="Ej: Cambio de llanta">
                                </div>
                                <div class="col-12 col-md-6">
                                    <label class="form-label">Fecha <span class="text-danger">*</span></label>
                                    <input type="date" class="form-control form-control-sm" id="nuevaReparacionFecha">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-12 col-md-4">
                                    <label class="form-label">N° Comprobante</label>
                                    <input type="text" class="form-control form-control-sm" id="nuevaReparacionComprobante" placeholder="001-123456">
                                </div>
                                <div class="col-12 col-md-4">
                                    <label class="form-label">Monto Soles</label>
                                    <input type="number" class="form-control form-control-sm" id="nuevaReparacionSoles" placeholder="0.00" step="0.01">
                                </div>
                                <div class="col-12 col-md-4">
                                    <label class="form-label">Monto Dólares</label>
                                    <input type="number" class="form-control form-control-sm" id="nuevaReparacionDolares" placeholder="0.00" step="0.01">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-12">
                                    <label class="form-label">Observaciones</label>
                                    <input type="text" class="form-control form-control-sm" id="nuevaReparacionObservaciones" placeholder="Detalles de la reparación">
                                </div>
                            </div>
                            <div class="text-right mt-3">
                                <button type="button" class="btn btn-success-custom btn-sm" onclick="agregarReparacion()">
                                    <i class="fas fa-plus mr-1"></i>Agregar Reparación
                                </button>
                            </div>
                        </div>
                    </div>
                    <div class="card card-list">
                        <div class="card-header d-flex justify-content-between">
                            <span><i class="fas fa-list mr-2"></i>Reparaciones Registradas</span>
                            <span class="badge badge-primary-custom" id="totalReparaciones">0 reparaciones</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-modal mb-0">
                                    <thead>
                                        <tr>
                                            <th>Tipo</th>
                                            <th>Fecha</th>
                                            <th>Comprobante</th>
                                            <th>Soles</th>
                                            <th>Dólares</th>
                                            <th width="80">Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaReparaciones"></tbody>
                                </table>
                            </div>
                        </div>
                        <div class="card-footer-totals">
                            <div class="row">
                                <div class="col-6"><strong>Total Soles: <span id="totalReparacionesSoles">S/ 0.00</span></strong></div>
                                <div class="col-6 text-right"><strong>Total Dólares: <span id="totalReparacionesDolares">$ 0.00</span></strong></div>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary-custom" data-dismiss="modal">Cerrar</button>
                    <button type="button" class="btn btn-primary-custom" onclick="aplicarReparaciones()">Aplicar y Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <!-- Modal Hospedaje -->
    <div class="modal fade" id="modalHospedaje" tabindex="-1">
        <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
            <div class="modal-content">
                <div class="modal-header modal-header-custom">
                    <h5 class="modal-title"><i class="fas fa-bed mr-2"></i>Registrar Hospedajes</h5>
                    <button type="button" class="close" data-dismiss="modal"><span>&times;</span></button>
                </div>
                <div class="modal-body">
                    <div class="card card-form mb-3">
                        <div class="card-header"><i class="fas fa-plus mr-2"></i>Agregar Hospedaje</div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-12 col-md-6">
                                    <label class="form-label">Hotel/Lugar <span class="text-danger">*</span></label>
                                    <input type="text" class="form-control form-control-sm" id="nuevoHospedajeLugar" placeholder="Ej: Hotel Pacifico">
                                </div>
                                <div class="col-12 col-md-6">
                                    <label class="form-label">Fecha <span class="text-danger">*</span></label>
                                    <input type="date" class="form-control form-control-sm" id="nuevoHospedajeFecha">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-12 col-md-4">
                                    <label class="form-label">N° Comprobante</label>
                                    <input type="text" class="form-control form-control-sm" id="nuevoHospedajeComprobante" placeholder="001-123456">
                                </div>
                                <div class="col-12 col-md-4">
                                    <label class="form-label">Monto Soles</label>
                                    <input type="number" class="form-control form-control-sm" id="nuevoHospedajeSoles" placeholder="0.00" step="0.01">
                                </div>
                                <div class="col-12 col-md-4">
                                    <label class="form-label">Monto Dólares</label>
                                    <input type="number" class="form-control form-control-sm" id="nuevoHospedajeDolares" placeholder="0.00" step="0.01">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-12">
                                    <label class="form-label">Observaciones</label>
                                    <input type="text" class="form-control form-control-sm" id="nuevoHospedajeObservaciones" placeholder="Detalles del hospedaje">
                                </div>
                            </div>
                            <div class="text-right mt-3">
                                <button type="button" class="btn btn-success-custom btn-sm" onclick="agregarHospedaje()">
                                    <i class="fas fa-plus mr-1"></i>Agregar Hospedaje
                                </button>
                            </div>
                        </div>
                    </div>
                    <div class="card card-list">
                        <div class="card-header d-flex justify-content-between">
                            <span><i class="fas fa-list mr-2"></i>Hospedajes Registrados</span>
                            <span class="badge badge-primary-custom" id="totalHospedajes">0 hospedajes</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-modal mb-0">
                                    <thead>
                                        <tr>
                                            <th>Lugar</th>
                                            <th>Fecha</th>
                                            <th>Comprobante</th>
                                            <th>Soles</th>
                                            <th>Dólares</th>
                                            <th width="80">Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaHospedajes"></tbody>
                                </table>
                            </div>
                        </div>
                        <div class="card-footer-totals">
                            <div class="row">
                                <div class="col-6"><strong>Total Soles: <span id="totalHospedajesSoles">S/ 0.00</span></strong></div>
                                <div class="col-6 text-right"><strong>Total Dólares: <span id="totalHospedajesDolares">$ 0.00</span></strong></div>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary-custom" data-dismiss="modal">Cerrar</button>
                    <button type="button" class="btn btn-primary-custom" onclick="aplicarHospedajes()">Aplicar y Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <!-- Modal Combustible -->
    <div class="modal fade" id="modalCombustible" tabindex="-1">
        <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
            <div class="modal-content">
                <div class="modal-header modal-header-custom">
                    <h5 class="modal-title"><i class="fas fa-gas-pump mr-2"></i>Registrar Combustible</h5>
                    <button type="button" class="close" data-dismiss="modal"><span>&times;</span></button>
                </div>
                <div class="modal-body">
                    <div class="card card-form mb-3">
                        <div class="card-header"><i class="fas fa-plus mr-2"></i>Agregar Combustible</div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-12 col-md-6">
                                    <label class="form-label">Estación/Lugar <span class="text-danger">*</span></label>
                                    <input type="text" class="form-control form-control-sm" id="nuevoCombustibleLugar" placeholder="Ej: Grifo Primax">
                                </div>
                                <div class="col-12 col-md-6">
                                    <label class="form-label">Fecha <span class="text-danger">*</span></label>
                                    <input type="date" class="form-control form-control-sm" id="nuevoCombustibleFecha">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-12 col-md-4">
                                    <label class="form-label">N° Comprobante</label>
                                    <input type="text" class="form-control form-control-sm" id="nuevoCombustibleComprobante" placeholder="001-123456">
                                </div>
                                <div class="col-12 col-md-4">
                                    <label class="form-label">Monto Soles</label>
                                    <input type="number" class="form-control form-control-sm" id="nuevoCombustibleSoles" placeholder="0.00" step="0.01">
                                </div>
                                <div class="col-12 col-md-4">
                                    <label class="form-label">Monto Dólares</label>
                                    <input type="number" class="form-control form-control-sm" id="nuevoCombustibleDolares" placeholder="0.00" step="0.01">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-12">
                                    <label class="form-label">Observaciones</label>
                                    <input type="text" class="form-control form-control-sm" id="nuevoCombustibleObservaciones" placeholder="Tipo de combustible, galones, etc.">
                                </div>
                            </div>
                            <div class="text-right mt-3">
                                <button type="button" class="btn btn-success-custom btn-sm" onclick="agregarCombustible()">
                                    <i class="fas fa-plus mr-1"></i>Agregar Combustible
                                </button>
                            </div>
                        </div>
                    </div>
                    <div class="card card-list">
                        <div class="card-header d-flex justify-content-between">
                            <span><i class="fas fa-list mr-2"></i>Combustibles Registrados</span>
                            <span class="badge badge-primary-custom" id="totalCombustibles">0 combustibles</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-modal mb-0">
                                    <thead>
                                        <tr>
                                            <th>Lugar</th>
                                            <th>Fecha</th>
                                            <th>Comprobante</th>
                                            <th>Soles</th>
                                            <th>Dólares</th>
                                            <th width="80">Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaCombustibles"></tbody>
                                </table>
                            </div>
                        </div>
                        <div class="card-footer-totals">
                            <div class="row">
                                <div class="col-6"><strong>Total Soles: <span id="totalCombustiblesSoles">S/ 0.00</span></strong></div>
                                <div class="col-6 text-right"><strong>Total Dólares: <span id="totalCombustiblesDolares">$ 0.00</span></strong></div>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary-custom" data-dismiss="modal">Cerrar</button>
                    <button type="button" class="btn btn-primary-custom" onclick="aplicarCombustibles()">Aplicar y Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <!-- CSS PROFESIONAL CON RESPONSIVE MOBILE-FIRST -->
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/DashboardConductor.css") %>" rel="stylesheet" />

    <!-- JAVASCRIPT COMPLETO Y CORREGIDO -->


    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            pnlFormularioLiquidacion: '<%= pnlFormularioLiquidacion.ClientID %>',
            txtFechaSalida: '<%= txtFechaSalida.ClientID %>',
            txtFechaLlegada: '<%= txtFechaLlegada.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/DashboardConductor.js") %>"></script>

    <!-- Modal Retirar Liquidación -->
    <div class="modal fade" id="modalRetirarLiquidacion" tabindex="-1" role="dialog" aria-labelledby="modalRetirarLabel" aria-hidden="true">
        <div class="modal-dialog modal-dialog-centered" role="document">
            <div class="modal-content">
                <div class="modal-header" style="background:#d97706;color:white;">
                    <h5 class="modal-title" id="modalRetirarLabel">
                        <i class="fas fa-undo mr-2"></i>Retirar Liquidación
                    </h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar" style="color:white;">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <div class="alert alert-warning mb-3">
                        <i class="fas fa-exclamation-triangle mr-2"></i>
                        <strong>¿Está seguro de retirar esta liquidación?</strong>
                    </div>
                    <p>Al retirar la liquidación:</p>
                    <ul class="mb-0">
                        <li>Se <strong>eliminará</strong> la orden de viaje y todos sus datos financieros.</li>
                        <li>El viaje será <strong>reabierto</strong> y aparecerá nuevamente en la pestaña "Mis Viajes".</li>
                        <li>Podrá volver a completar y enviar la liquidación.</li>
                    </ul>
                    <hr />
                    <small class="text-muted">
                        <i class="fas fa-info-circle mr-1"></i>
                        Solo puede retirar liquidaciones que aún no hayan sido revisadas por la administración.
                    </small>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <button type="button" id="btnConfirmarRetirar" class="btn" style="background:#d97706;color:white;border-color:#d97706;" onclick="confirmarRetirar()">
                        <i class="fas fa-undo mr-1"></i>Sí, Retirar
                    </button>
                </div>
            </div>
        </div>
    </div>

</asp:Content>

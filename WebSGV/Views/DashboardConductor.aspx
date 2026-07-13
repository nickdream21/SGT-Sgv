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
                                                    <label class="form-label">Hora Salida</label>
                                                    <asp:TextBox ID="txtHoraSalida" runat="server" CssClass="form-control" TextMode="Time"></asp:TextBox>
                                                </div>
                                            </div>
                                            <div class="col-12 col-md-3">
                                                <div class="form-group">
                                                    <label class="form-label">Hora Llegada</label>
                                                    <asp:TextBox ID="txtHoraLlegada" runat="server" CssClass="form-control" TextMode="Time" ReadOnly="true"></asp:TextBox>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="row">
                                            <div class="col-12">
                                                <small class="text-muted">La hora de llegada se registra automáticamente con la hora oficial del servidor al enviar la liquidación.</small>
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
    <style>
        /* === VARIABLES DE COLOR === */
        :root {
            --primary-color: #2563eb;
            --primary-dark: #1e40af;
            --success-color: #059669;
            --danger-color: #dc2626;
            --warning-color: #d97706;
            --info-color: #0891b2;
            --neutral-color: #475569;
            --light-gray: #f8fafc;
            --medium-gray: #e2e8f0;
            --dark-gray: #64748b;
            --border-color: #cbd5e1;
            --text-primary: #1e293b;
            --text-secondary: #64748b;
        }

        /* === CONTENEDOR PRINCIPAL === */
        .dash-conductor {
            background: #f5f5f5;
            min-height: 100vh;
            padding-bottom: 30px;
        }

        /* === HEADER GRADIENTE === */
        .cd-header {
            background: linear-gradient(135deg, var(--primary-color) 0%, #3b82f6 100%);
            color: white;
            padding: 16px;
        }

        .cd-header h2 {
            font-size: 1.15rem;
            font-weight: 700;
            margin: 0 0 4px 0;
        }

        .cd-header .cd-sub {
            font-size: 0.82rem;
            opacity: 0.9;
            margin: 0 0 6px 0;
        }

        .cd-estado-badge {
            display: inline-block;
            background: rgba(255,255,255,0.2);
            border-radius: 20px;
            padding: 4px 12px;
            font-size: 0.78rem;
        }

        .cd-estado-badge i {
            animation: pulse 2s infinite;
        }

        /* === TABS WRAPPER === */
        .cd-tabs-wrapper {
            background: white;
        }

        @keyframes pulse {
            0%, 100% {
                opacity: 1;
            }

            50% {
                opacity: 0.5;
            }
        }

        /* === BOTONES === */
        .btn-primary-custom {
            background-color: var(--primary-color);
            border-color: var(--primary-color);
            color: white;
            font-weight: 500;
            transition: all 0.2s;
        }

            .btn-primary-custom:hover {
                background-color: var(--primary-dark);
                border-color: var(--primary-dark);
                transform: translateY(-1px);
                box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            }

        .btn-primary-conductor {
            background: linear-gradient(135deg, #2563eb 0%, #3b82f6 100%);
            border: none;
            color: white;
            font-weight: 600;
            transition: all 0.3s;
            box-shadow: 0 4px 15px rgba(37, 99, 235, 0.3);
        }

            .btn-primary-conductor:hover {
                background: linear-gradient(135deg, #1e40af 0%, #2563eb 100%);
                transform: translateY(-2px);
                box-shadow: 0 6px 20px rgba(37, 99, 235, 0.4);
            }

        .btn-secondary-custom {
            background-color: white;
            border: 1px solid var(--border-color);
            color: var(--text-secondary);
            font-weight: 500;
            transition: all 0.2s;
        }

        .btn-success-custom {
            background-color: var(--success-color);
            border-color: var(--success-color);
            color: white;
            font-weight: 500;
        }

        .btn-danger-custom {
            background-color: var(--danger-color);
            border-color: var(--danger-color);
            color: white;
            font-weight: 500;
        }

        .btn-info-custom {
            background-color: var(--info-color);
            border-color: var(--info-color);
            color: white;
            font-weight: 500;
            padding: 0.375rem 0.75rem;
        }

        .btn-retirar-liq {
            background-color: #d97706;
            border-color: #d97706;
            color: white;
            font-weight: 500;
        }

            .btn-retirar-liq:hover {
                background-color: #b45309;
                border-color: #b45309;
                color: white;
            }

        .btn-detail-modal {
            background-color: transparent;
            border: 1px solid var(--primary-color);
            color: var(--primary-color);
            padding: 0.25rem 0.5rem;
            font-size: 0.75rem;
        }

        /* === ALERTAS === */
        .alert-info-conductor {
            background: linear-gradient(135deg, #e0f2fe 0%, #bae6fd 100%);
            border: 2px solid #7dd3fc;
            border-radius: 0.5rem;
            padding: 1.5rem;
        }

        .alert-warning-conductor {
            background-color: #fef3c7;
            border: 1px solid #fbbf24;
            border-radius: 0.5rem;
            padding: 1rem;
        }

        .alert-info-light {
            background-color: #f0f9ff;
            border: 1px solid #bae6fd;
            border-radius: 0.5rem;
            padding: 1rem;
        }

        .alert-warning-light {
            background-color: #fffbeb;
            border: 1px solid #fde68a;
            border-radius: 0.5rem;
            padding: 1rem;
        }

        /* === INFO GRID === */
        .info-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
            gap: 1rem;
            margin-top: 1rem;
        }

        .info-item {
            display: flex;
            flex-direction: column;
        }

        .info-label {
            font-size: 0.75rem;
            color: var(--text-secondary);
            font-weight: 500;
            text-transform: uppercase;
            letter-spacing: 0.05em;
        }

        .info-value {
            font-size: 1.125rem;
            color: var(--text-primary);
            font-weight: 600;
            margin-top: 0.25rem;
        }

        /* === ESTADO VACÍO === */
        .empty-state {
            text-align: center;
            padding: 4rem 2rem;
        }

        .empty-icon {
            font-size: 4rem;
            color: var(--medium-gray);
            margin-bottom: 1rem;
        }

        .empty-state h4 {
            color: var(--text-primary);
            font-weight: 600;
            margin-bottom: 0.5rem;
        }

        /* === PESTAÑAS === */
        .nav-tabs-custom {
            border-bottom: 2px solid var(--border-color);
        }

            .nav-tabs-custom .nav-link {
                color: var(--text-secondary);
                background-color: transparent;
                border: none;
                border-bottom: 3px solid transparent;
                padding: 1rem 1.5rem;
                font-weight: 500;
                transition: all 0.2s;
            }

                .nav-tabs-custom .nav-link:hover {
                    color: var(--primary-color);
                    background-color: var(--light-gray);
                }

                .nav-tabs-custom .nav-link.active {
                    color: var(--primary-color);
                    background-color: white;
                    border-bottom-color: var(--primary-color);
                }

        .tab-content-custom {
            background-color: white;
            border: 1px solid var(--border-color);
            border-top: none;
            min-height: 500px;
        }

        /* === SECCIONES === */
        .section-card {
            background-color: white;
            border: 1px solid var(--border-color);
            border-radius: 0.5rem;
            overflow: hidden;
        }

        .section-header {
            background-color: var(--light-gray);
            padding: 1rem 1.25rem;
            border-bottom: 1px solid var(--border-color);
        }

        .section-header-primary {
            background-color: #dbeafe;
            border-bottom-color: #93c5fd;
        }

        .section-header-success {
            background-color: #f0fdf4;
            border-bottom-color: #86efac;
        }

        .section-header-danger {
            background-color: #fef2f2;
            border-bottom-color: #fca5a5;
        }

        .section-header-info {
            background-color: #e0f2fe;
            border-bottom-color: #7dd3fc;
        }

        .section-header-neutral {
            background-color: var(--light-gray);
            border-bottom-color: var(--border-color);
        }

        .section-title {
            color: var(--text-primary);
            font-size: 1.125rem;
            font-weight: 600;
            margin: 0;
        }

        .section-body {
            padding: 1.5rem;
        }

        /* === FORMULARIOS === */
        .form-label {
            color: var(--text-primary);
            font-weight: 500;
            font-size: 0.875rem;
            margin-bottom: 0.375rem;
        }

        .form-label-summary {
            font-size: 0.75rem;
            font-weight: 600;
            color: var(--text-secondary);
            text-transform: uppercase;
            letter-spacing: 0.05em;
            margin-bottom: 0.25rem;
        }

        .form-value-summary {
            font-size: 1.125rem;
            font-weight: 600;
            color: var(--text-primary);
            margin: 0;
        }

        .form-control {
            border: 1px solid var(--border-color);
            font-size: 0.875rem;
            padding: 0.5rem 0.75rem;
            transition: all 0.2s;
        }

            .form-control:focus {
                border-color: var(--primary-color);
                box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
            }

        .form-control-readonly {
            background-color: var(--light-gray) !important;
            border-color: var(--medium-gray);
            color: var(--text-secondary);
        }

        .form-control-sm {
            font-size: 0.8125rem;
            padding: 0.375rem 0.625rem;
        }

        /* === TABLAS === */
        .table-conductor {
            font-size: 0.875rem;
        }

            .table-conductor thead th {
                background-color: var(--light-gray);
                color: var(--text-primary);
                font-weight: 600;
                font-size: 0.8125rem;
                text-transform: uppercase;
                letter-spacing: 0.025em;
                padding: 0.875rem 0.75rem;
                border-bottom: 2px solid var(--border-color);
            }

            .table-conductor tbody td {
                vertical-align: middle;
                padding: 0.75rem;
                border-bottom: 1px solid var(--medium-gray);
            }

            .table-conductor tbody tr:hover {
                background-color: var(--light-gray);
            }

        .table-financial {
            font-size: 0.875rem;
            margin-bottom: 0;
        }

            .table-financial thead th {
                background-color: var(--light-gray);
                color: var(--text-primary);
                font-weight: 600;
                font-size: 0.8125rem;
                padding: 0.75rem;
                border-bottom: 2px solid var(--border-color);
            }

            .table-financial tbody td {
                vertical-align: middle;
                padding: 0.625rem;
                border-bottom: 1px solid var(--medium-gray);
            }

            .table-financial tfoot .total-row td {
                background-color: var(--light-gray);
                font-weight: 600;
                padding: 1rem 0.75rem;
                border-top: 2px solid var(--border-color);
            }

        .table-modal {
            font-size: 0.8125rem;
        }

            .table-modal thead th {
                background-color: var(--light-gray);
                color: var(--text-primary);
                font-weight: 600;
                padding: 0.625rem;
                font-size: 0.75rem;
                text-transform: uppercase;
            }

        /* === BADGES === */
        .badge-vehicle {
            background-color: #3b82f6;
            color: white;
            padding: 0.375rem 0.75rem;
            border-radius: 0.25rem;
            font-weight: 600;
            font-size: 0.8125rem;
        }

        .badge-estado {
            padding: 0.375rem 0.75rem;
            border-radius: 0.25rem;
            font-weight: 600;
            font-size: 0.75rem;
            text-transform: uppercase;
        }

        .badge-estado-programado {
            background-color: #fbbf24;
            color: #78350f;
        }

        .badge-estado-en_proceso {
            background-color: #3b82f6;
            color: white;
        }

        .badge-estado-completado {
            background-color: #10b981;
            color: white;
        }

        .badge-fixed {
            background-color: var(--medium-gray);
            color: var(--text-secondary);
            font-weight: 500;
            padding: 0.375rem 0.75rem;
            font-size: 0.75rem;
        }

        .badge-count {
            background-color: var(--primary-color);
            color: white;
            font-weight: 600;
            padding: 0.375rem 0.625rem;
            font-size: 0.75rem;
        }

        .badge-primary-custom {
            background-color: var(--primary-color);
            color: white;
            font-weight: 500;
            padding: 0.375rem 0.75rem;
        }

        /* === BALANCE === */
        .balance-final {
            background-color: var(--light-gray);
            border: 2px solid var(--border-color);
            border-radius: 0.5rem;
            padding: 1.5rem;
        }

        .balance-item {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 0.75rem 1rem;
            background-color: white;
            border: 1px solid var(--border-color);
            border-radius: 0.375rem;
        }

        .balance-label {
            font-weight: 600;
            color: var(--text-primary);
            font-size: 1rem;
        }

        .balance-amount {
            font-size: 1.5rem;
            font-weight: 700;
            color: var(--primary-color);
        }

        /* === MODALES === */
        .modal-header-custom {
            background-color: var(--light-gray);
            border-bottom: 2px solid var(--border-color);
        }

        .card-form {
            border: 1px solid var(--border-color);
            border-radius: 0.5rem;
        }

            .card-form .card-header {
                background-color: var(--light-gray);
                border-bottom: 1px solid var(--border-color);
                font-weight: 600;
                padding: 0.75rem 1rem;
            }

        .card-list {
            border: 1px solid var(--border-color);
            border-radius: 0.5rem;
        }

            .card-list .card-header {
                background-color: var(--light-gray);
                border-bottom: 1px solid var(--border-color);
                font-weight: 600;
                padding: 0.75rem 1rem;
            }

        .card-footer-totals {
            background-color: var(--light-gray);
            border-top: 2px solid var(--border-color);
            padding: 1rem;
            font-weight: 600;
        }

        /* === ALERTA DE RECHAZO === */
        .alert-rechazo {
            background: linear-gradient(135deg, #fff3cd 0%, #ffe69c 100%);
            border: 2px solid #ff9800;
            border-radius: 0.5rem;
            padding: 1.5rem;
            margin-bottom: 2rem;
            box-shadow: 0 4px 15px rgba(255, 152, 0, 0.2);
        }

            .alert-rechazo .alert-icon {
                color: #f57c00;
            }

            .alert-rechazo .alert-heading {
                color: #e65100;
                font-weight: 700;
                font-size: 1.25rem;
            }

        .rechazo-motivo {
            background-color: rgba(255, 255, 255, 0.7);
            border-left: 4px solid #f57c00;
            padding: 1rem;
            border-radius: 0.25rem;
            margin: 1rem 0;
        }

            .rechazo-motivo label {
                color: #c62828;
                font-size: 0.95rem;
                margin-bottom: 0.5rem;
            }

            .rechazo-motivo p {
                color: #424242;
                font-size: 1rem;
                line-height: 1.6;
            }

        /* === RESPONSIVE MÓVIL === */
        @media (max-width: 768px) {
            /* Contenedor sin padding excesivo */
            .cd-tabs-wrapper .container-fluid {
                padding-left: 0;
                padding-right: 0;
            }

            .nav-tabs-custom .nav-link {
                padding: 0.75rem 0.75rem;
                font-size: 0.82rem;
            }

            .section-body {
                padding: 0.875rem;
            }

            .info-grid {
                grid-template-columns: repeat(2, 1fr);
                gap: 0.75rem;
            }

            .info-value {
                font-size: 1rem;
            }

            .balance-amount {
                font-size: 1.25rem;
            }

            .balance-label {
                font-size: 0.875rem;
            }

            .balance-item {
                flex-direction: column;
                text-align: center;
                gap: 0.5rem;
            }

            .table-conductor {
                font-size: 0.75rem;
            }

                .table-conductor thead th {
                    font-size: 0.7rem;
                    padding: 0.5rem 0.25rem;
                }

                .table-conductor tbody td {
                    padding: 0.5rem 0.25rem;
                }

            .btn-detail-modal {
                display: inline-block;
                margin-top: 0.25rem;
                font-size: 0.7rem;
                padding: 0.2rem 0.5rem;
            }

            /* Modales fullscreen en m&#243;vil */
            .modal-dialog {
                margin: 0;
                max-width: 100%;
                min-height: 100vh;
            }

            .modal-content {
                border-radius: 0;
                min-height: 100vh;
            }

            .modal-body {
                padding: 1rem;
            }

            .empty-state {
                padding: 2rem 1rem;
            }

            .empty-icon {
                font-size: 3rem;
            }

            .btn-md-auto {
                width: 100%;
            }

            /* Inputs touch-friendly m&#225;s grandes */
            .form-control {
                min-height: 42px;
                font-size: 0.9rem;
            }

            .form-control-sm {
                font-size: 0.875rem;
                padding: 0.5rem 0.625rem;
                min-height: 40px;
            }

            /* Botones m&#225;s grandes para touch */
            .btn-success-custom.btn-sm,
            .btn-danger-custom.btn-sm {
                padding: 0.5rem 1rem;
                font-size: 0.85rem;
            }

            .btn-primary-conductor {
                width: 100%;
                padding: 0.875rem;
                font-size: 1rem;
            }

            .btn-secondary-custom.btn-lg {
                width: 100%;
                margin-left: 0 !important;
                margin-top: 0.5rem;
            }

            /* Ocultar columnas menos importantes en tabla despachos */
            .table-conductor th:nth-child(5),
            .table-conductor td:nth-child(5),
            .table-conductor th:nth-child(6),
            .table-conductor td:nth-child(6) {
                display: none;
            }

            /* Tabs scrollables */
            .nav-tabs-custom {
                display: flex;
                flex-wrap: nowrap;
                overflow-x: auto;
                -webkit-overflow-scrolling: touch;
                scrollbar-width: none;
            }

                .nav-tabs-custom::-webkit-scrollbar {
                    display: none;
                }

                .nav-tabs-custom .nav-item {
                    flex-shrink: 0;
                }

            /* Cards de resumen viaje apiladas */
            .alert-info-conductor .row {
                flex-direction: column;
            }

            .alert-info-conductor .col-12.col-md-4 {
                margin-top: 1rem;
            }

            /* Section cards sin bordes laterales en m&#243;vil */
            .section-card {
                border-radius: 0;
                border-left: none;
                border-right: none;
            }

            /* Resumen viaje mejorado en m&#243;vil */
            .form-label-summary {
                font-size: 0.7rem;
            }

            .form-value-summary {
                font-size: 1rem;
            }

            /* Balance final prominente */
            .balance-final {
                padding: 1rem;
            }

            /* Tab content sin borde superior en m&#243;vil */
            .tab-content-custom {
                border: none;
            }

            /* Alert info m&#225;s compacto */
            .alert-info-light,
            .alert-warning-light {
                padding: 0.75rem;
                font-size: 0.82rem;
            }

            /* =============================================
               TABLAS FINANCIERAS → Card layout en móvil
               ============================================= */
            .table-financial {
                border-collapse: separate;
                border-spacing: 0;
                border: 0;
            }

                .table-financial thead {
                    display: none;
                }

                .table-financial,
                .table-financial tbody,
                .table-financial tfoot {
                    display: block;
                    width: 100%;
                }

                    .table-financial tbody tr {
                        display: block;
                        border: 1px solid var(--border-color);
                        border-radius: 0.5rem;
                        padding: 0.75rem;
                        margin-bottom: 0.75rem;
                        background: #fff;
                        position: relative;
                        box-shadow: 0 1px 3px rgba(0,0,0,0.06);
                    }

                    .table-financial tbody td {
                        display: block;
                        border: none !important;
                        padding: 0.25rem 0;
                        width: 100% !important;
                        text-align: left !important;
                    }

                        /* Ocultar columna # */
                        .table-financial tbody td:first-child {
                            display: none;
                        }

                        /* Concepto → título de la card */
                        .table-financial tbody td:nth-child(2) {
                            border-bottom: 1px solid var(--medium-gray) !important;
                            padding-bottom: 0.5rem;
                            margin-bottom: 0.5rem;
                            padding-right: 70px;
                            font-size: 0.9rem;
                        }

                        /* Descripción → ancho completo */
                        .table-financial tbody td:nth-child(3) {
                            margin-bottom: 0.5rem;
                        }

                            .table-financial tbody td:nth-child(3)::before {
                                content: 'Descripción';
                                display: block;
                                font-size: 0.7rem;
                                font-weight: 600;
                                color: var(--text-secondary);
                                text-transform: uppercase;
                                letter-spacing: 0.03em;
                                margin-bottom: 0.15rem;
                            }

                        /* Soles y Dólares lado a lado */
                        .table-financial tbody td:nth-child(4),
                        .table-financial tbody td:nth-child(5) {
                            display: inline-block !important;
                            width: 48% !important;
                            vertical-align: top;
                        }

                        .table-financial tbody td:nth-child(4) {
                            margin-right: 3%;
                        }

                            .table-financial tbody td:nth-child(4)::before {
                                content: 'Soles (S/)';
                                display: block;
                                font-size: 0.7rem;
                                font-weight: 600;
                                color: var(--text-secondary);
                                text-transform: uppercase;
                                letter-spacing: 0.03em;
                                margin-bottom: 0.15rem;
                            }

                        .table-financial tbody td:nth-child(5)::before {
                            content: 'Dólares ($)';
                            display: block;
                            font-size: 0.7rem;
                            font-weight: 600;
                            color: var(--text-secondary);
                            text-transform: uppercase;
                            letter-spacing: 0.03em;
                            margin-bottom: 0.15rem;
                        }

                        /* Badge/Acción → esquina superior derecha */
                        .table-financial tbody td:nth-child(6) {
                            position: absolute;
                            top: 0.6rem;
                            right: 0.75rem;
                            width: auto !important;
                            padding: 0;
                        }

                    /* Inputs táctiles más grandes */
                    .table-financial .form-control-sm {
                        font-size: 0.875rem;
                        padding: 0.5rem 0.625rem;
                        min-height: 38px;
                    }

                    /* Footer de tabla financiera */
                    .table-financial tfoot tr {
                        display: block;
                    }

                        .table-financial tfoot tr td {
                            display: block;
                            border: none !important;
                        }

                        .table-financial tfoot tr.total-row {
                            background: var(--light-gray);
                            border-radius: 0.5rem;
                            padding: 0.75rem;
                            margin-top: 0.5rem;
                            text-align: center;
                        }

                            .table-financial tfoot tr.total-row td:last-child:empty {
                                display: none;
                            }

            /* =============================================
               TABLAS DE MODALES → Card layout en móvil
               ============================================= */
            .table-modal thead {
                display: none;
            }

            .table-modal,
            .table-modal tbody {
                display: block;
                width: 100%;
            }

                .table-modal tbody tr {
                    display: block;
                    border: 1px solid var(--border-color);
                    border-radius: 0.5rem;
                    padding: 0.75rem;
                    margin: 0.5rem 0.25rem;
                    background: #fff;
                    position: relative;
                }

                .table-modal tbody td {
                    display: block;
                    border: none !important;
                    padding: 0.2rem 0;
                }

                    /* Primera columna (nombre/estación) → título de card */
                    .table-modal tbody td:first-child {
                        font-weight: 600;
                        color: var(--primary-color);
                        border-bottom: 1px solid var(--medium-gray);
                        padding-bottom: 0.4rem;
                        margin-bottom: 0.4rem;
                        padding-right: 50px;
                    }

                    .table-modal tbody td:nth-child(2)::before {
                        content: 'Fecha: ';
                        font-weight: 600;
                        color: var(--text-secondary);
                        font-size: 0.75rem;
                    }

                    .table-modal tbody td:nth-child(3)::before {
                        content: 'Comprobante: ';
                        font-weight: 600;
                        color: var(--text-secondary);
                        font-size: 0.75rem;
                    }

                    /* Montos lado a lado */
                    .table-modal tbody td:nth-child(4),
                    .table-modal tbody td:nth-child(5) {
                        display: inline-block;
                        width: 48%;
                        vertical-align: top;
                    }

                    .table-modal tbody td:nth-child(4) {
                        margin-right: 3%;
                    }

                        .table-modal tbody td:nth-child(4)::before {
                            content: 'Soles: ';
                            font-weight: 600;
                            color: var(--text-secondary);
                            font-size: 0.75rem;
                        }

                    .table-modal tbody td:nth-child(5)::before {
                        content: 'Dólares: ';
                        font-weight: 600;
                        color: var(--text-secondary);
                        font-size: 0.75rem;
                    }

                    /* Botón eliminar → esquina superior derecha */
                    .table-modal tbody td:last-child {
                        position: absolute;
                        top: 0.5rem;
                        right: 0.5rem;
                    }
        }

        /* === AUTOCOMPLETE PEAJES === */
        .peaje-autocomplete-wrapper {
            position: relative;
        }

        .peaje-input-container {
            position: relative;
        }

        .peaje-input-container input {
            padding-right: 2rem;
        }

        .peaje-search-icon {
            position: absolute;
            right: 0.625rem;
            top: 50%;
            transform: translateY(-50%);
            color: var(--text-secondary);
            font-size: 0.75rem;
            pointer-events: none;
        }

        .peaje-dropdown {
            position: absolute;
            top: 100%;
            left: 0;
            right: 0;
            background: white;
            border: 1px solid var(--border-color);
            border-top: none;
            border-radius: 0 0 0.375rem 0.375rem;
            max-height: 220px;
            overflow-y: auto;
            z-index: 9999;
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        }

        .peaje-dropdown-item {
            padding: 0.6rem 0.75rem;
            cursor: pointer;
            font-size: 0.8125rem;
            border-bottom: 1px solid var(--light-gray);
            transition: background 0.1s;
        }

        .peaje-dropdown-item:last-child {
            border-bottom: none;
        }

        .peaje-dropdown-item:hover,
        .peaje-dropdown-item.highlighted {
            background-color: #dbeafe;
            color: var(--primary-dark);
        }

        .peaje-dropdown-item strong {
            color: var(--primary-color);
        }

        .peaje-dropdown-empty {
            padding: 0.75rem;
            color: var(--text-secondary);
            font-size: 0.8125rem;
            text-align: center;
        }

        .peaje-selected-indicator {
            position: absolute;
            right: 0.625rem;
            top: 50%;
            transform: translateY(-50%);
            color: var(--success-color);
            font-size: 0.75rem;
            pointer-events: none;
            display: none;
        }

        /* === DESKTOP OVERRIDES === */
        @media (min-width: 769px) {
            .cd-header h2 {
                font-size: 1.5rem;
            }

            .cd-header {
                padding: 20px 24px;
            }

            .dash-conductor .tab-content-custom {
                border: 1px solid var(--border-color);
                border-top: none;
            }

            .dash-conductor .section-card {
                border-radius: 0.5rem;
                border: 1px solid var(--border-color);
            }

            /* Modales normales en desktop */
            .modal-dialog {
                margin: 1.75rem auto;
            }

            .modal-content {
                border-radius: 0.5rem;
                min-height: auto;
            }
        }
    </style>

    <!-- JAVASCRIPT COMPLETO Y CORREGIDO -->


    <script>
        // === SEGURIDAD: escapado HTML para prevenir XSS en renderizado dinámico ===
        function escHtml(str) {
            if (str == null) return '';
            return String(str)
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#39;');
        }

        // === VARIABLES GLOBALES ===
        let contadorIngresosAdicionales = 0;
        let contadorGastosAdicionales = 0;

        // Datos de modales
        let reparacionesData = [];
        let hospedajesData = [];
        let combustiblesData = [];

        let contadorReparaciones = 0;
        let contadorHospedajes = 0;
        let contadorCombustibles = 0;

        // === INICIALIZACIÓN ===
        $(document).ready(function () {
            console.log('✅ Dashboard Conductor iniciado');
            configurarFechasPorDefecto();
            aplicarVisibilidadPeajes();
            calcularTotales();
        });

        // Los peajes (nacionales y extranjeros) los paga directamente la empresa a la
        // concesionaria. En viajes nacionales el conductor NO debe registrarlos, por lo
        // que ocultamos ambas filas y limpiamos sus montos para que no entren en la liquidación.
        function aplicarVisibilidadPeajes() {
            const esInternacional = $('#hfEsInternacional').val() === '1';
            if (!esInternacional) {
                $('#filaPeajesNacionales, #filaPeajesExtranjeros').hide();
                $('#peajesNacSoles, #peajesExtDolares').val('');
                $('#descPeajesNacionales, #descPeajesExtranjeros').val('');
            } else {
                $('#filaPeajesNacionales, #filaPeajesExtranjeros').show();
            }
        }

        function configurarFechasPorDefecto() {
            const hoy = new Date();
            const fechaHoy = hoy.toISOString().split('T')[0];

            $('#nuevaReparacionFecha, #nuevoHospedajeFecha, #nuevoCombustibleFecha').val(fechaHoy);

            if (!$('#<%= txtFechaSalida.ClientID %>').val()) {
                $('#<%= txtFechaSalida.ClientID %>').val(fechaHoy);
            }

            if (!$('#<%= txtFechaLlegada.ClientID %>').val()) {
                $('#<%= txtFechaLlegada.ClientID %>').val(fechaHoy);
            }
        }

        // === GESTIÓN DE INGRESOS Y GASTOS
        function agregarIngreso() {
            contadorIngresosAdicionales++;
            const numeroFila = 4 + contadorIngresosAdicionales;

            $('#ingresosAdicionalesBody').append(`
            <tr id="ingresoAdicional_${contadorIngresosAdicionales}">
                <td class="text-center">${numeroFila}</td>
                <td>
                    <input type="text" class="form-control form-control-sm" name="conceptoIngreso_${contadorIngresosAdicionales}" 
                           placeholder="Concepto de ingreso" required>
                </td>
                <td>
                    <input type="text" class="form-control form-control-sm" name="descIngreso_${contadorIngresosAdicionales}" 
                           placeholder="Descripción del ingreso">
                </td>
                <td>
                    <input type="number" class="form-control form-control-sm ingreso-soles" 
                           name="ingresoSoles_${contadorIngresosAdicionales}" 
                           placeholder="0.00" step="0.01" onchange="calcularTotales()">
                </td>
                <td>
                    <input type="number" class="form-control form-control-sm ingreso-dolares" 
                           name="ingresoDolares_${contadorIngresosAdicionales}" 
                           placeholder="0.00" step="0.01" onchange="calcularTotales()">
                </td>
                <td class="text-center">
                    <button type="button" class="btn btn-danger btn-sm" onclick="eliminarIngreso(${contadorIngresosAdicionales})">
                        <i class="fas fa-trash"></i>
                    </button>
                </td>
            </tr>
        `);
        }

        function eliminarIngreso(id) {
            if (confirm('¿Está seguro de eliminar este ingreso?')) {
                $(`#ingresoAdicional_${id}`).remove();
                calcularTotales();
            }
        }

        function agregarGasto() {
            contadorGastosAdicionales++;
            const numeroFila = 8 + contadorGastosAdicionales;

            $('#gastosAdicionalesBody').append(`
            <tr id="gastoAdicional_${contadorGastosAdicionales}">
                <td class="text-center">${numeroFila}</td>
                <td>
                    <input type="text" class="form-control form-control-sm" name="conceptoGasto_${contadorGastosAdicionales}" 
                           placeholder="Concepto de gasto" required>
                </td>
                <td>
                    <input type="text" class="form-control form-control-sm" name="descGasto_${contadorGastosAdicionales}" 
                           placeholder="Descripción del gasto">
                </td>
                <td>
                    <input type="number" class="form-control form-control-sm gasto-soles" 
                           name="gastoSoles_${contadorGastosAdicionales}" 
                           placeholder="0.00" step="0.01" onchange="calcularTotales()">
                </td>
                <td>
                    <input type="number" class="form-control form-control-sm gasto-dolares" 
                           name="gastoDolares_${contadorGastosAdicionales}" 
                           placeholder="0.00" step="0.01" onchange="calcularTotales()">
                </td>
                <td class="text-center">
                    <button type="button" class="btn btn-danger btn-sm" onclick="eliminarGasto(${contadorGastosAdicionales})">
                        <i class="fas fa-trash"></i>
                    </button>
                </td>
            </tr>
        `);
        }

        function eliminarGasto(id) {
            if (confirm('¿Está seguro de eliminar este gasto?')) {
                $(`#gastoAdicional_${id}`).remove();
                calcularTotales();
            }
        }

        function calcularTotales() {
            let totalIngresosSoles = 0;
            let totalIngresosDolares = 0;
            let totalGastosSoles = 0;
            let totalGastosDolares = 0;

            $('.ingreso-soles').each(function () {
                totalIngresosSoles += parseFloat($(this).val()) || 0;
            });

            $('.ingreso-dolares').each(function () {
                totalIngresosDolares += parseFloat($(this).val()) || 0;
            });

            $('.gasto-soles').each(function () {
                totalGastosSoles += parseFloat($(this).val()) || 0;
            });

            $('.gasto-dolares').each(function () {
                totalGastosDolares += parseFloat($(this).val()) || 0;
            });

            const diferenciaSoles = totalIngresosSoles - totalGastosSoles;
            const diferenciaDolares = totalIngresosDolares - totalGastosDolares;

            $('#totalIngresosSoles').text(totalIngresosSoles.toFixed(2));
            $('#totalIngresosDolares').text(totalIngresosDolares.toFixed(2));
            $('#totalGastosSoles').text(totalGastosSoles.toFixed(2));
            $('#totalGastosDolares').text(totalGastosDolares.toFixed(2));

            $('#diferenciaSoles').text('S/ ' + diferenciaSoles.toFixed(2));
            $('#diferenciaDolares').text('$ ' + diferenciaDolares.toFixed(2));

            $('#diferenciaSoles').css('color', diferenciaSoles >= 0 ? '#059669' : '#dc2626');
            $('#diferenciaDolares').css('color', diferenciaDolares >= 0 ? '#059669' : '#dc2626');
        }

        // === GESTIÓN DE PEAJES (Simplificado: Nacionales/Extranjeros) ===
        // Ya no se usa modal ni detalle individual, los campos son directos en la tabla de egresos.

        // === GESTIÓN DE REPARACIONES ===

        function abrirModalReparaciones() {
            $('#modalReparaciones').modal('show');
            actualizarTablaReparaciones();
        }

        function agregarReparacion() {
            const tipo = $('#nuevaReparacionTipo').val().trim();
            const fecha = $('#nuevaReparacionFecha').val();
            const comprobante = $('#nuevaReparacionComprobante').val().trim();
            const soles = parseFloat($('#nuevaReparacionSoles').val()) || 0;
            const dolares = parseFloat($('#nuevaReparacionDolares').val()) || 0;
            const observaciones = $('#nuevaReparacionObservaciones').val().trim();

            if (!tipo) { alert('Ingrese el tipo de reparación'); $('#nuevaReparacionTipo').focus(); return; }
            if (!fecha) { alert('Seleccione una fecha'); $('#nuevaReparacionFecha').focus(); return; }
            if (soles <= 0 && dolares <= 0) { alert('Ingrese al menos un monto'); $('#nuevaReparacionSoles').focus(); return; }

            reparacionesData.push({
                id: ++contadorReparaciones,
                tipo, fecha, comprobante, soles, dolares, observaciones
            });

            $('#nuevaReparacionTipo, #nuevaReparacionComprobante, #nuevaReparacionObservaciones').val('');
            $('#nuevaReparacionSoles, #nuevaReparacionDolares').val('');

            actualizarTablaReparaciones();
            actualizarTotalesReparaciones();
        }

        function eliminarReparacion(id) {
            if (confirm('¿Eliminar esta reparación?')) {
                reparacionesData = reparacionesData.filter(r => r.id !== id);
                actualizarTablaReparaciones();
                actualizarTotalesReparaciones();
            }
        }

        function actualizarTablaReparaciones() {
            const tbody = $('#tablaReparaciones');
            tbody.empty();
            reparacionesData.forEach(r => {
                tbody.append(`
                <tr>
                    <td>${escHtml(r.tipo)}</td>
                    <td>${escHtml(r.fecha)}</td>
                    <td>${escHtml(r.comprobante) || 'N/A'}</td>
                    <td>S/ ${r.soles.toFixed(2)}</td>
                    <td>$ ${r.dolares.toFixed(2)}</td>
                    <td class="text-center">
                        <button type="button" class="btn btn-danger btn-sm" onclick="eliminarReparacion(${r.id})">
                            <i class="fas fa-trash"></i>
                        </button>
                    </td>
                </tr>
            `);
            });
            $('#totalReparaciones').text(`${reparacionesData.length} reparaciones`);
        }

        function actualizarTotalesReparaciones() {
            const totalSoles = reparacionesData.reduce((sum, r) => sum + r.soles, 0);
            const totalDolares = reparacionesData.reduce((sum, r) => sum + r.dolares, 0);

            $('#totalReparacionesSoles').text(`S/ ${totalSoles.toFixed(2)}`);
            $('#totalReparacionesDolares').text(`$ ${totalDolares.toFixed(2)}`);
            $('#reparacionesSoles').val(totalSoles > 0 ? totalSoles.toFixed(2) : '');
            $('#reparacionesDolares').val(totalDolares > 0 ? totalDolares.toFixed(2) : '');
            $('#descReparaciones').val(reparacionesData.length > 0 ? `${reparacionesData.length} reparaciones` : 'Sin reparaciones');
            $('#contadorReparaciones').text(reparacionesData.length);

            calcularTotales();
        }

        function aplicarReparaciones() {
            $('#modalReparaciones').modal('hide');
        }

        // === GESTIÓN DE HOSPEDAJE ===

        function abrirModalHospedaje() {
            $('#modalHospedaje').modal('show');
            actualizarTablaHospedajes();
        }

        function agregarHospedaje() {
            const lugar = $('#nuevoHospedajeLugar').val().trim();
            const fecha = $('#nuevoHospedajeFecha').val();
            const comprobante = $('#nuevoHospedajeComprobante').val().trim();
            const soles = parseFloat($('#nuevoHospedajeSoles').val()) || 0;
            const dolares = parseFloat($('#nuevoHospedajeDolares').val()) || 0;
            const observaciones = $('#nuevoHospedajeObservaciones').val().trim();

            if (!lugar) { alert('Ingrese el lugar'); $('#nuevoHospedajeLugar').focus(); return; }
            if (!fecha) { alert('Seleccione una fecha'); $('#nuevoHospedajeFecha').focus(); return; }
            if (soles <= 0 && dolares <= 0) { alert('Ingrese al menos un monto'); $('#nuevoHospedajeSoles').focus(); return; }

            hospedajesData.push({
                id: ++contadorHospedajes,
                lugar, fecha, comprobante, soles, dolares, observaciones
            });

            $('#nuevoHospedajeLugar, #nuevoHospedajeComprobante, #nuevoHospedajeObservaciones').val('');
            $('#nuevoHospedajeSoles, #nuevoHospedajeDolares').val('');

            actualizarTablaHospedajes();
            actualizarTotalesHospedajes();
        }

        function eliminarHospedaje(id) {
            if (confirm('¿Eliminar este hospedaje?')) {
                hospedajesData = hospedajesData.filter(h => h.id !== id);
                actualizarTablaHospedajes();
                actualizarTotalesHospedajes();
            }
        }

        function actualizarTablaHospedajes() {
            const tbody = $('#tablaHospedajes');
            tbody.empty();
            hospedajesData.forEach(h => {
                tbody.append(`
                <tr>
                    <td>${escHtml(h.lugar)}</td>
                    <td>${escHtml(h.fecha)}</td>
                    <td>${escHtml(h.comprobante) || 'N/A'}</td>
                    <td>S/ ${h.soles.toFixed(2)}</td>
                    <td>$ ${h.dolares.toFixed(2)}</td>
                    <td class="text-center">
                        <button type="button" class="btn btn-danger btn-sm" onclick="eliminarHospedaje(${h.id})">
                            <i class="fas fa-trash"></i>
                        </button>
                    </td>
                </tr>
            `);
            });
            $('#totalHospedajes').text(`${hospedajesData.length} hospedajes`);
        }

        function actualizarTotalesHospedajes() {
            const totalSoles = hospedajesData.reduce((sum, h) => sum + h.soles, 0);
            const totalDolares = hospedajesData.reduce((sum, h) => sum + h.dolares, 0);

            $('#totalHospedajesSoles').text(`S/ ${totalSoles.toFixed(2)}`);
            $('#totalHospedajesDolares').text(`$ ${totalDolares.toFixed(2)}`);
            $('#hospedajeSoles').val(totalSoles > 0 ? totalSoles.toFixed(2) : '');
            $('#hospedajeDolares').val(totalDolares > 0 ? totalDolares.toFixed(2) : '');
            $('#descHospedaje').val(hospedajesData.length > 0 ? `${hospedajesData.length} hospedajes` : 'Sin hospedajes');
            $('#contadorHospedaje').text(hospedajesData.length);

            calcularTotales();
        }

        function aplicarHospedajes() {
            $('#modalHospedaje').modal('hide');
        }

        // === GESTIÓN DE COMBUSTIBLE ===

        function abrirModalCombustible() {
            $('#modalCombustible').modal('show');
            actualizarTablaCombustibles();
        }

        function agregarCombustible() {
            const lugar = $('#nuevoCombustibleLugar').val().trim();
            const fecha = $('#nuevoCombustibleFecha').val();
            const comprobante = $('#nuevoCombustibleComprobante').val().trim();
            const soles = parseFloat($('#nuevoCombustibleSoles').val()) || 0;
            const dolares = parseFloat($('#nuevoCombustibleDolares').val()) || 0;
            const observaciones = $('#nuevoCombustibleObservaciones').val().trim();

            if (!lugar) { alert('Ingrese el lugar'); $('#nuevoCombustibleLugar').focus(); return; }
            if (!fecha) { alert('Seleccione una fecha'); $('#nuevoCombustibleFecha').focus(); return; }
            if (soles <= 0 && dolares <= 0) { alert('Ingrese al menos un monto'); $('#nuevoCombustibleSoles').focus(); return; }

            combustiblesData.push({
                id: ++contadorCombustibles,
                lugar, fecha, comprobante, soles, dolares, observaciones
            });

            $('#nuevoCombustibleLugar, #nuevoCombustibleComprobante, #nuevoCombustibleObservaciones').val('');
            $('#nuevoCombustibleSoles, #nuevoCombustibleDolares').val('');

            actualizarTablaCombustibles();
            actualizarTotalesCombustibles();
        }

        function eliminarCombustible(id) {
            if (confirm('¿Eliminar este combustible?')) {
                combustiblesData = combustiblesData.filter(c => c.id !== id);
                actualizarTablaCombustibles();
                actualizarTotalesCombustibles();
            }
        }

        function actualizarTablaCombustibles() {
            const tbody = $('#tablaCombustibles');
            tbody.empty();
            combustiblesData.forEach(c => {
                tbody.append(`
                <tr>
                    <td>${escHtml(c.lugar)}</td>
                    <td>${escHtml(c.fecha)}</td>
                    <td>${escHtml(c.comprobante) || 'N/A'}</td>
                    <td>S/ ${c.soles.toFixed(2)}</td>
                    <td>$ ${c.dolares.toFixed(2)}</td>
                    <td class="text-center">
                        <button type="button" class="btn btn-danger btn-sm" onclick="eliminarCombustible(${c.id})">
                            <i class="fas fa-trash"></i>
                        </button>
                    </td>
                </tr>
            `);
            });
            $('#totalCombustibles').text(`${combustiblesData.length} combustibles`);
        }

        function actualizarTotalesCombustibles() {
            const totalSoles = combustiblesData.reduce((sum, c) => sum + c.soles, 0);
            const totalDolares = combustiblesData.reduce((sum, c) => sum + c.dolares, 0);

            $('#totalCombustiblesSoles').text(`S/ ${totalSoles.toFixed(2)}`);
            $('#totalCombustiblesDolares').text(`$ ${totalDolares.toFixed(2)}`);
            $('#combustibleSoles').val(totalSoles > 0 ? totalSoles.toFixed(2) : '');
            $('#combustibleDolares').val(totalDolares > 0 ? totalDolares.toFixed(2) : '');
            $('#descCombustible').val(combustiblesData.length > 0 ? `${combustiblesData.length} combustibles` : 'Sin combustibles');
            $('#contadorCombustible').text(combustiblesData.length);

            calcularTotales();
        }

        function aplicarCombustibles() {
            $('#modalCombustible').modal('hide');
        }

        // === PREPARAR DATOS PARA ENVÍO ===

        function prepararDatosFinancieros() {
            const gastosFinancieros = [];

            // Peajes Nacionales (Perú) - solo Soles
            const pnSoles = parseFloat($('#peajesNacSoles').val()) || 0;
            if (pnSoles > 0) {
                gastosFinancieros.push({
                    categoria: 'Peajes',
                    estacion: 'Peajes Nacionales (Perú)',
                    soles: pnSoles,
                    dolares: 0,
                    observaciones: $('#descPeajesNacionales').val() || ''
                });
            }

            // Peajes Extranjeros (Ecuador) - solo Dólares
            const peDolares = parseFloat($('#peajesExtDolares').val()) || 0;
            if (peDolares > 0) {
                gastosFinancieros.push({
                    categoria: 'Peajes',
                    estacion: 'Peajes Extranjeros (Ecuador)',
                    soles: 0,
                    dolares: peDolares,
                    observaciones: $('#descPeajesExtranjeros').val() || ''
                });
            }

            // Otros gastos detallados
            gastosFinancieros.push(
                ...reparacionesData.map(r => ({ categoria: 'Reparaciones', ...r })),
                ...hospedajesData.map(h => ({ categoria: 'Hospedaje', ...h })),
                ...combustiblesData.map(c => ({ categoria: 'Combustible', ...c }))
            );
            $('#hfGastosFinancieros').val(JSON.stringify(gastosFinancieros));

            const ingresosAdicionales = [];
            $('#ingresosAdicionalesBody tr').each(function () {
                const concepto = $(this).find('input[name^="conceptoIngreso_"]').val()?.trim() || '';
                const desc = $(this).find('input[name^="descIngreso_"]').val()?.trim() || '';
                const soles = parseFloat($(this).find('input[name^="ingresoSoles_"]').val()) || 0;
                const dolares = parseFloat($(this).find('input[name^="ingresoDolares_"]').val()) || 0;
                if (concepto && (soles > 0 || dolares > 0)) {
                    ingresosAdicionales.push({ categoria: concepto, nombreCategoria: concepto, descripcion: desc, soles, dolares });
                }
            });
            $('#hfIngresosAdicionales').val(JSON.stringify(ingresosAdicionales));

            const gastosAdicionales = [];
            $('#gastosAdicionalesBody tr').each(function () {
                const concepto = $(this).find('input[name^="conceptoGasto_"]').val()?.trim() || '';
                const desc = $(this).find('input[name^="descGasto_"]').val()?.trim() || '';
                const soles = parseFloat($(this).find('input[name^="gastoSoles_"]').val()) || 0;
                const dolares = parseFloat($(this).find('input[name^="gastoDolares_"]').val()) || 0;
                if (concepto && (soles > 0 || dolares > 0)) {
                    gastosAdicionales.push({ categoria: concepto, nombreCategoria: concepto, descripcion: desc, soles, dolares });
                }
            });
            $('#hfGastosAdicionales').val(JSON.stringify(gastosAdicionales));
        }

        // === FUNCIONES AUXILIARES ===

        function confirmarEnvioLiquidacion() {
            return confirm(
                '¿Está seguro de enviar esta liquidación?\n\n' +
                'Una vez enviada, no podrá modificarla y quedará pendiente de revisión por la administración.\n\n' +
                'Asegúrese de que todos los datos son correctos.'
            );
        }

        function limpiarFormulario() {
            if (confirm('¿Está seguro de limpiar el formulario? Se perderán todos los datos ingresados.')) {
                location.reload();
            }
        }

        function verDetalleLiquidacion(idOrdenViaje) {
            window.location.href = `DetalleOrdenViaje.aspx?id=${idOrdenViaje}`;
        }

        // === Retirar Liquidación ===
        var idOrdenViajeARetirar = 0;

        function abrirModalRetirar(idOrdenViaje) {
            idOrdenViajeARetirar = idOrdenViaje;
            $('#modalRetirarLiquidacion').modal('show');
        }

        function confirmarRetirar() {
            if (idOrdenViajeARetirar === 0) return;

            var btnConfirmar = $('#btnConfirmarRetirar');
            btnConfirmar.prop('disabled', true).html('<i class="fas fa-spinner fa-spin mr-1"></i>Procesando...');

            $.ajax({
                type: 'POST',
                url: 'DashboardConductor.aspx/RetirarLiquidacion',
                data: JSON.stringify({ idOrdenViaje: idOrdenViajeARetirar }),
                contentType: 'application/json; charset=utf-8',
                dataType: 'json',
                success: function (response) {
                    var data = response.d;
                    $('#modalRetirarLiquidacion').modal('hide');

                    if (data.success) {
                        alert('✅ ' + data.message);
                        location.reload();
                    } else {
                        alert('⚠️ ' + data.message);
                    }
                },
                error: function (xhr) {
                    $('#modalRetirarLiquidacion').modal('hide');
                    alert('❌ Error de comunicación. Intente nuevamente.');
                    console.error(xhr.responseText);
                },
                complete: function () {
                    btnConfirmar.prop('disabled', false).html('<i class="fas fa-undo mr-1"></i>Sí, Retirar');
                    idOrdenViajeARetirar = 0;
                }
            });
        }
    </script>

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

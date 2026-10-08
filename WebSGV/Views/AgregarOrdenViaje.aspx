<%@ Page Title="Agregar Orden de Viaje" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AgregarOrdenViaje.aspx.cs" Inherits="WebSGV.Views.AgregarOrdenViaje" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <!-- Campos ocultos -->
    <asp:HiddenField ID="hfIdViajeProgreso" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdConductor" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdTracto" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdCarreta" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdCliente" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfEsInternacional" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdCPIC" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdOrdenViaje" runat="server" ClientIDMode="Static" Value="0" />
    <asp:HiddenField ID="hfProductosDescarga" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfProductosCarga" runat="server" ClientIDMode="Static" Value="[]" />
    <asp:HiddenField ID="hfGastosFinancieros" runat="server" ClientIDMode="Static" Value="[]" />
    <asp:HiddenField ID="hfOrigenViaje" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="HiddenField1" runat="server" Value="0" />
    <asp:HiddenField ID="hfIngresosAdicionales" runat="server" ClientIDMode="Static" Value="[]" />
    <asp:HiddenField ID="hfGastosAdicionales" runat="server" ClientIDMode="Static" Value="[]" />

    <!-- Panel de mensajes -->
    <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-4">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-4">
        <!-- Header -->
        <div class="row mb-4">
            <div class="col-12">
                <div class="page-header d-flex justify-content-between align-items-center">
                    <div>
                        <h2 class="page-title mb-1">
                            <i class="fas fa-route mr-2" id="pageTitleIcon"></i><span id="pageTitleText">Crear Orden de Viaje</span>
                        </h2>
                        <p class="text-muted mb-0" id="pageTitleSub">Complete la información del viaje y gestión financiera</p>
                    </div>
                    <a href="ListaDespachos.aspx" class="btn btn-outline-secondary btn-back">
                        <i class="fas fa-arrow-left mr-2"></i>Volver
                    </a>
                </div>
            </div>
        </div>

        <!-- Panel de Información de Viaje Origen -->
        <asp:Panel ID="pnlInfoViajeOrigen" runat="server" Visible="false" CssClass="mb-3">
            <div class="alert alert-info-custom">
                <div class="d-flex justify-content-between align-items-center">
                    <div>
                        <i class="fas fa-check-circle mr-2"></i>
                        <strong>Viaje Finalizado:</strong>
                        <asp:Label ID="lblCantidadDespachosOrigen" runat="server"></asp:Label>
                        despachos procesados |
                        <strong>Conductor:</strong>
                        <asp:Label ID="lblConductorOrigen" runat="server"></asp:Label>
                    </div>
                    <button type="button" class="btn btn-sm btn-outline-info" onclick="mostrarDetallesViajeOrigen()">
                        <i class="fas fa-eye mr-1"></i>Ver Detalles
                    </button>
                </div>
            </div>
        </asp:Panel>

        <!-- Detalles de Despachos del Viaje Origen -->
        <asp:Panel ID="pnlDetallesViajeOrigen" runat="server" Visible="false" CssClass="collapse mb-4" ClientIDMode="Static">
            <div class="card card-detalles">
                <div class="card-header-custom">
                    <h6 class="mb-0">
                        <i class="fas fa-list-alt mr-2"></i>Despachos del Viaje Finalizado
                    </h6>
                </div>
                <div class="card-body p-0">
                    <div class="table-responsive">
                        <asp:GridView ID="gvDespachosViajeOrigen" runat="server"
                            CssClass="table table-professional mb-0"
                            AutoGenerateColumns="false"
                            EmptyDataText="No hay despachos para mostrar">
                            <Columns>
                                <asp:BoundField DataField="NumeroDespacho" HeaderText="N° Despacho" ItemStyle-CssClass="font-weight-bold" />
                                <asp:BoundField DataField="NombreCliente" HeaderText="Cliente" />
                                <asp:BoundField DataField="TipoOperacion" HeaderText="Operación" />
                                <asp:BoundField DataField="LugarOperacion" HeaderText="Planta" />
                                <asp:BoundField DataField="PlacaTracto" HeaderText="Tracto" />
                                <asp:BoundField DataField="PlacaCarreta" HeaderText="Carreta" />
                                <asp:BoundField DataField="FechaDespacho" HeaderText="Fecha" DataFormatString="{0:dd/MM/yyyy}" />
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>
            </div>
        </asp:Panel>

        <!-- PESTAÑAS PRINCIPALES -->
        <div class="row">
            <div class="col-12">
                <ul class="nav nav-tabs-custom mb-0" id="mainTabs" role="tablist">
                    <li class="nav-item">
                        <a class="nav-link active" id="datosViaje-tab" data-toggle="tab" href="#datosViaje" role="tab">
                            <i class="fas fa-truck mr-2"></i>Datos del Viaje
                        </a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link" id="gestionFinanciera-tab" data-toggle="tab" href="#gestionFinanciera" role="tab">
                            <i class="fas fa-calculator mr-2"></i>Gestión Financiera
                        </a>
                    </li>
                </ul>

                <div class="tab-content tab-content-custom" id="mainTabsContent">

                    <!-- TAB 1: DATOS DEL VIAJE -->
                    <div class="tab-pane fade show active" id="datosViaje" role="tabpanel">
                        <div class="p-4">

                            <!-- Datos Generales de la Orden -->
                            <div class="section-card mb-4">
                                <div class="section-header">
                                    <h5 class="section-title">
                                        <i class="fas fa-clipboard-list mr-2"></i>Datos Generales de la Orden
                                    </h5>
                                </div>
                                <div class="section-body">
                                    <div class="row">
                                        <div class="col-md-3">
                                            <div class="form-group">
                                                <label class="form-label">N° Orden Viaje</label>
                                                <asp:TextBox ID="txtNumeroOrdenViaje" runat="server" CssClass="form-control" placeholder="123456" MaxLength="6"></asp:TextBox>
                                            </div>
                                        </div>
                                        <div class="col-md-3">
                                            <div class="form-group">
                                                <label class="form-label">Fecha Salida</label>
                                                <asp:TextBox ID="txtFechaSalida" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                            </div>
                                        </div>
                                        <div class="col-md-3">
                                            <div class="form-group">
                                                <label class="form-label">Fecha Llegada</label>
                                                <asp:TextBox ID="txtFechaLlegada" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                            </div>
                                        </div>
                                    </div>

                                    <div class="row">
                                        <div class="col-md-3">
                                            <div class="form-group">
                                                <label class="form-label">Hora Salida</label>
                                                <asp:TextBox ID="txtHoraSalida" runat="server" CssClass="form-control" TextMode="Time"></asp:TextBox>
                                            </div>
                                        </div>
                                        <div class="col-md-3">
                                            <div class="form-group">
                                                <label class="form-label">Hora Llegada</label>
                                                <asp:TextBox ID="txtHoraLlegada" runat="server" CssClass="form-control" TextMode="Time"></asp:TextBox>
                                            </div>
                                        </div>
                                        <div class="col-md-6">
                                            <div class="form-group">
                                                <label class="form-label">Observaciones Generales</label>
                                                <asp:TextBox ID="txtObservaciones" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="2" placeholder="Ingrese observaciones"></asp:TextBox>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <!-- Información del Viaje/Vehículo -->
                            <div class="section-card mb-4">
                                <div class="section-header">
                                    <h5 class="section-title">
                                        <i class="fas fa-truck-moving mr-2"></i>Información del Viaje y Vehículo
                                    </h5>
                                </div>
                                <div class="section-body">
                                    <div class="row">
                                        <div class="col-md-4">
                                            <div class="form-group">
                                                <label class="form-label">Conductor</label>
                                                <asp:TextBox ID="txtConductor" runat="server" CssClass="form-control form-control-readonly" ReadOnly="true"></asp:TextBox>
                                                <small class="form-text text-muted">Información del viaje finalizado</small>
                                            </div>
                                        </div>
                                        <div class="col-md-4">
                                            <div class="form-group">
                                                <label class="form-label">Placa Tracto</label>
                                                <asp:TextBox ID="txtPlacaTracto" runat="server" CssClass="form-control form-control-readonly" ReadOnly="true"></asp:TextBox>
                                                <small class="form-text text-muted">Información del viaje finalizado</small>
                                            </div>
                                        </div>
                                        <div class="col-md-4">
                                            <div class="form-group">
                                                <label class="form-label">Placa Carreta</label>
                                                <asp:TextBox ID="txtPlacaCarreta" runat="server" CssClass="form-control form-control-readonly" ReadOnly="true"></asp:TextBox>
                                                <small class="form-text text-muted">Información del viaje finalizado</small>
                                            </div>
                                        </div>
                                    </div>

                                    <div class="row">
                                        <div class="col-md-3">
                                            <div class="form-group">
                                                <label class="form-label">Cliente Principal</label>
                                                <asp:TextBox ID="txtClientePrincipal" runat="server" CssClass="form-control form-control-readonly" ReadOnly="true"></asp:TextBox>
                                                <small class="form-text text-muted">Del viaje finalizado</small>
                                            </div>
                                        </div>
                                        <div class="col-md-3">
                                            <div class="form-group">
                                                <label class="form-label">Tipo Operación</label>
                                                <asp:TextBox ID="txtTipoOperacion" runat="server" CssClass="form-control form-control-readonly" ReadOnly="true"></asp:TextBox>
                                                <small class="form-text text-muted">Del viaje finalizado</small>
                                            </div>
                                        </div>
                                        <div class="col-md-3">
                                            <div class="form-group">
                                                <label class="form-label">Planta Principal</label>
                                                <asp:TextBox ID="txtPlantaPrincipal" runat="server" CssClass="form-control form-control-readonly" ReadOnly="true"></asp:TextBox>
                                                <small class="form-text text-muted">Del viaje finalizado</small>
                                            </div>
                                        </div>
                                        <div class="col-md-3">
                                            <div class="form-group">
                                                <label class="form-label">Tipo de Viaje</label>
                                                <asp:TextBox ID="TextBox1" runat="server" CssClass="form-control form-control-readonly" ReadOnly="true"></asp:TextBox>
                                                <small class="form-text text-muted">Detectado automáticamente</small>
                                            </div>
                                        </div>
                                    </div>

                                    <!-- Panel CPIC -->
                                    <asp:Panel ID="pnlMostrarCPIC" runat="server" Visible="false" CssClass="row">
                                        <div class="col-md-12">
                                            <div class="alert alert-info-light">
                                                <div class="row">
                                                    <div class="col-md-6">
                                                        <label class="form-label">
                                                            <i class="fas fa-shipping-fast mr-2"></i>N° CPIC Registrado
                                                        </label>
                                                        <asp:TextBox ID="txtCPICMostrar" runat="server"
                                                            CssClass="form-control form-control-readonly"
                                                            ReadOnly="true"></asp:TextBox>
                                                        <small class="form-text text-muted">Registrado en el despacho original</small>
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </asp:Panel>
                                </div>
                            </div>

                            <!-- Panel de Variaciones -->
                            <asp:Panel ID="pnlVariaciones" runat="server" Visible="false">
                                <div class="alert alert-warning-custom">
                                    <i class="fas fa-info-circle mr-2"></i>
                                    <strong>Múltiples operaciones detectadas.</strong>
                                    Los datos mostrados corresponden a la primera operación.
                                    <button type="button" class="btn btn-sm btn-link" onclick="$('#pnlDetallesViajeOrigen').collapse('show')">
                                        Ver todas las operaciones
                                    </button>
                                </div>
                            </asp:Panel>

                        </div>
                    </div>

                    <!-- TAB 2: GESTIÓN FINANCIERA -->
                    <div class="tab-pane fade" id="gestionFinanciera" role="tabpanel">
                        <div class="p-4">

                            <!-- Ingresos -->
                            <div class="section-card mb-4">
                                <div class="section-header section-header-success">
                                    <h5 class="section-title">
                                        <i class="fas fa-plus-circle mr-2"></i>Ingresos del Viaje
                                    </h5>
                                </div>
                                <div class="section-body">
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
                                                        <input type="text" class="form-control form-control-sm" name="descDespacho" placeholder="Descripción del despacho">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm ingreso-soles" name="despachoSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm ingreso-dolares" name="despachoDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td class="text-center"><span class="badge badge-fixed">Fijo</span></td>
                                                </tr>
                                                <tr>
                                                    <td class="text-center">2</td>
                                                    <td><strong>Mensualidad</strong></td>
                                                    <td>
                                                        <input type="text" class="form-control form-control-sm" name="descMensualidad" placeholder="Descripción de mensualidad">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm ingreso-soles" name="mensualidadSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm ingreso-dolares" name="mensualidadDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td class="text-center"><span class="badge badge-fixed">Fijo</span></td>
                                                </tr>
                                                <tr>
                                                    <td class="text-center">3</td>
                                                    <td><strong>Otros Autorizados</strong></td>
                                                    <td>
                                                        <input type="text" class="form-control form-control-sm" name="descOtros" placeholder="Descripción">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm ingreso-soles" name="otrosSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm ingreso-dolares" name="otrosDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td class="text-center"><span class="badge badge-fixed">Fijo</span></td>
                                                </tr>
                                                <tr>
                                                    <td class="text-center">4</td>
                                                    <td><strong>Préstamo</strong></td>
                                                    <td>
                                                        <input type="text" class="form-control form-control-sm" name="descPrestamo" placeholder="Descripción">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm ingreso-soles" name="prestamoSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm ingreso-dolares" name="prestamoDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td class="text-center"><span class="badge badge-fixed">Fijo</span></td>
                                                </tr>
                                            </tbody>
                                            <tbody id="ingresosAdicionalesBody"></tbody>
                                            <tfoot>
                                                <tr>
                                                    <td colspan="6" class="text-left pt-3">
                                                        <button type="button" class="btn btn-success-custom btn-sm" onclick="agregarIngreso()">
                                                            <i class="fas fa-plus mr-1"></i>Agregar Ingreso
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
                                                <!-- Peajes -->
                                                <tr>
                                                    <td class="text-center">1</td>
                                                    <td>
                                                        <strong>Peajes</strong>
                                                        <button type="button" class="btn btn-detail-modal btn-sm ml-2" onclick="abrirModalPeajes()">
                                                            <i class="fas fa-edit"></i>
                                                        </button>
                                                    </td>
                                                    <td>
                                                        <input type="text" class="form-control form-control-sm form-control-readonly" name="descPeajes" id="descPeajes" readonly placeholder="Sin peajes registrados">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm form-control-readonly gasto-soles" name="peajesSoles" id="peajesSoles" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm form-control-readonly gasto-dolares" name="peajesDolares" id="peajesDolares" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td class="text-center"><span class="badge badge-count" id="contadorPeajes">0</span></td>
                                                </tr>

                                                <!-- Alimentación -->
                                                <tr>
                                                    <td class="text-center">2</td>
                                                    <td><strong>Alimentación</strong></td>
                                                    <td>
                                                        <input type="text" class="form-control form-control-sm" name="descAlimentacion" placeholder="Descripción">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm gasto-soles" name="alimentacionSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm gasto-dolares" name="alimentacionDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td class="text-center"><span class="badge badge-fixed">Manual</span></td>
                                                </tr>

                                                <!-- Apoyo-Seguridad -->
                                                <tr>
                                                    <td class="text-center">3</td>
                                                    <td><strong>Apoyo-Seguridad</strong></td>
                                                    <td>
                                                        <input type="text" class="form-control form-control-sm" name="descApoyoSeguridad" placeholder="Descripción">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm gasto-soles" name="apoyoSeguridadSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm gasto-dolares" name="apoyoSeguridadDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td class="text-center"><span class="badge badge-fixed">Manual</span></td>
                                                </tr>

                                                <!-- Reparaciones -->
                                                <tr>
                                                    <td class="text-center">4</td>
                                                    <td>
                                                        <strong>Reparaciones Varios</strong>
                                                        <button type="button" class="btn btn-detail-modal btn-sm ml-2" onclick="abrirModalReparaciones()">
                                                            <i class="fas fa-edit"></i>
                                                        </button>
                                                    </td>
                                                    <td>
                                                        <input type="text" class="form-control form-control-sm form-control-readonly" name="descReparaciones" id="descReparaciones" readonly placeholder="Sin reparaciones registradas">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm form-control-readonly gasto-soles" name="reparacionesSoles" id="reparacionesSoles" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm form-control-readonly gasto-dolares" name="reparacionesDolares" id="reparacionesDolares" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td class="text-center"><span class="badge badge-count" id="contadorReparaciones">0</span></td>
                                                </tr>

                                                <!-- Movilidad -->
                                                <tr>
                                                    <td class="text-center">5</td>
                                                    <td><strong>Movilidad</strong></td>
                                                    <td>
                                                        <input type="text" class="form-control form-control-sm" name="descMovilidad" placeholder="Descripción">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm gasto-soles" name="movilidadSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm gasto-dolares" name="movilidadDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td class="text-center"><span class="badge badge-fixed">Manual</span></td>
                                                </tr>

                                                <!-- Encarpada/Descencarpada -->
                                                <tr>
                                                    <td class="text-center">6</td>
                                                    <td><strong>Encarpada/Descencarpada</strong></td>
                                                    <td>
                                                        <input type="text" class="form-control form-control-sm" name="descEncapada" placeholder="Descripción">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm gasto-soles" name="encapadaSoles" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm gasto-dolares" name="encapadaDolares" placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td class="text-center"><span class="badge badge-fixed">Manual</span></td>
                                                </tr>

                                                <!-- Hospedaje -->
                                                <tr>
                                                    <td class="text-center">7</td>
                                                    <td>
                                                        <strong>Hospedaje</strong>
                                                        <button type="button" class="btn btn-detail-modal btn-sm ml-2" onclick="abrirModalHospedaje()">
                                                            <i class="fas fa-edit"></i>
                                                        </button>
                                                    </td>
                                                    <td>
                                                        <input type="text" class="form-control form-control-sm form-control-readonly" name="descHospedaje" id="descHospedaje" readonly placeholder="Sin hospedajes registrados">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm form-control-readonly gasto-soles" name="hospedajeSoles" id="hospedajeSoles" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm form-control-readonly gasto-dolares" name="hospedajeDolares" id="hospedajeDolares" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td class="text-center"><span class="badge badge-count" id="contadorHospedaje">0</span></td>
                                                </tr>

                                                <!-- Combustible -->
                                                <tr>
                                                    <td class="text-center">8</td>
                                                    <td>
                                                        <strong>Combustible</strong>
                                                        <button type="button" class="btn btn-detail-modal btn-sm ml-2" onclick="abrirModalCombustible()">
                                                            <i class="fas fa-edit"></i>
                                                        </button>
                                                    </td>
                                                    <td>
                                                        <input type="text" class="form-control form-control-sm form-control-readonly" name="descCombustible" id="descCombustible" readonly placeholder="Sin combustibles registrados">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm form-control-readonly gasto-soles" name="combustibleSoles" id="combustibleSoles" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td>
                                                        <input type="number" class="form-control form-control-sm form-control-readonly gasto-dolares" name="combustibleDolares" id="combustibleDolares" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </td>
                                                    <td class="text-center"><span class="badge badge-count" id="contadorCombustible">0</span></td>
                                                </tr>
                                            </tbody>
                                            <tbody id="gastosAdicionalesBody"></tbody>
                                            <tfoot>
                                                <tr>
                                                    <td colspan="6" class="text-left pt-3">
                                                        <button type="button" class="btn btn-danger-custom btn-sm" onclick="agregarGasto()">
                                                            <i class="fas fa-plus mr-1"></i>Agregar Gasto
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

                            <!-- Ajustes Finales -->
                            <div class="section-card">
                                <div class="section-header section-header-neutral">
                                    <h5 class="section-title">
                                        <i class="fas fa-calculator mr-2"></i>Ajustes y Balance Final
                                    </h5>
                                </div>
                                <div class="section-body">
                                    <div class="row">
                                        <div class="col-md-6">
                                            <div class="adjustment-card adjustment-discount">
                                                <label class="adjustment-label">
                                                    <i class="fas fa-minus-circle mr-2"></i>Descuento
                                                </label>
                                                <p class="adjustment-description">Monto a descontar al conductor</p>
                                                <div class="row">
                                                    <div class="col-6">
                                                        <label class="form-label-sm">Soles (S/)</label>
                                                        <input type="number" class="form-control form-control-sm descuento-soles"
                                                            name="descuentoSoles" id="descuentoSoles"
                                                            placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </div>
                                                    <div class="col-6">
                                                        <label class="form-label-sm">Dólares ($)</label>
                                                        <input type="number" class="form-control form-control-sm descuento-dolares"
                                                            name="descuentoDolares" id="descuentoDolares"
                                                            placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                        <div class="col-md-6">
                                            <div class="adjustment-card adjustment-reintegro">
                                                <label class="adjustment-label">
                                                    <i class="fas fa-plus-circle mr-2"></i>Reintegro
                                                </label>
                                                <p class="adjustment-description">Monto a reintegrar al conductor</p>
                                                <div class="row">
                                                    <div class="col-6">
                                                        <label class="form-label-sm">Soles (S/)</label>
                                                        <input type="number" class="form-control form-control-sm reintegro-soles"
                                                            name="reintegroSoles" id="reintegroSoles"
                                                            placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </div>
                                                    <div class="col-6">
                                                        <label class="form-label-sm">Dólares ($)</label>
                                                        <input type="number" class="form-control form-control-sm reintegro-dolares"
                                                            name="reintegroDolares" id="reintegroDolares"
                                                            placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </div>

                                    <!-- Balance Final -->
                                    <div class="balance-final mt-4" id="balanceFinalBar">
                                        <div class="bf-header">
                                            <i class="fas fa-balance-scale bf-icon"></i>
                                            <span class="bf-title">Balance Final</span>
                                            <span class="bf-hint">Ingresos − Gastos − Descuentos + Reintegros</span>
                                        </div>
                                        <div class="bf-amounts">
                                            <div class="bf-amount-block">
                                                <span class="bf-currency">S/</span>
                                                <span class="bf-number" id="diferenciaSoles">0.00</span>
                                                <span class="bf-label">Soles</span>
                                            </div>
                                            <div class="bf-divider"></div>
                                            <div class="bf-amount-block">
                                                <span class="bf-currency">$</span>
                                                <span class="bf-number" id="diferenciaDolares">0.00</span>
                                                <span class="bf-label">Dólares</span>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>

                        </div>
                    </div>

                </div>
            </div>
        </div>

        <!-- Botones de Acción -->
        <div class="row mt-4 mb-5">
            <div class="col-12 text-center">
                <asp:Button ID="btnGuardarOrden" runat="server" Text="Guardar Orden de Viaje"
                    CssClass="btn btn-primary-custom btn-lg px-5" OnClick="btnGuardarOrden_Click"
                    OnClientClick="prepararDatosFinancieros(); return true;" />
                <a href="ListaDespachos.aspx" class="btn btn-secondary-custom btn-lg ml-3 px-4">
                    <i class="fas fa-times mr-2"></i>Cancelar
                </a>
            </div>
        </div>

    </div>

    <!-- ========== MODALES ========== -->

    <!-- Modal Peajes -->
    <div class="modal fade" id="modalPeajes" tabindex="-1">
        <div class="modal-dialog modal-lg">
            <div class="modal-content">
                <div class="modal-header modal-header-custom">
                    <h5 class="modal-title"><i class="fas fa-road mr-2"></i>Gestión de Peajes</h5>
                    <button type="button" class="close" data-dismiss="modal">
                        <span>&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <!-- Formulario agregar peaje -->
                    <div class="card card-form mb-3">
                        <div class="card-header"><i class="fas fa-plus mr-2"></i>Agregar Peaje</div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-4">
                                    <label class="form-label">Estación/Peaje <span class="text-danger">*</span></label>
                                    <input type="text" 
                                        class="form-control form-control-sm" 
                                        id="nuevoPeajeEstacion" 
                                        list="listaEstaciones"
                                        placeholder="Escriba para buscar..." 
                                        autocomplete="off">
                                    <datalist id="listaEstaciones"></datalist>
                                    <small class="form-text text-muted">
                                        <i class="fas fa-search mr-1"></i>Escriba para buscar
                                    </small>
                                </div>
                                <div class="col-md-4">
                                    <label class="form-label">Fecha <span class="text-danger">*</span></label>
                                    <input type="date" class="form-control form-control-sm" id="nuevoPeajeFecha">
                                </div>
                                <div class="col-md-4">
                                    <label class="form-label">N° Comprobante</label>
                                    <input type="text" class="form-control form-control-sm" id="nuevoPeajeComprobante" placeholder="001-123456">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-6">
                                    <label class="form-label">Monto Soles</label>
                                    <input type="number" class="form-control form-control-sm" id="nuevoPeajeSoles" placeholder="0.00" step="0.01">
                                </div>
                                <div class="col-md-6">
                                    <label class="form-label">Monto Dólares</label>
                                    <input type="number" class="form-control form-control-sm" id="nuevoPeajeDolares" placeholder="0.00" step="0.01">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-12">
                                    <label class="form-label">Observaciones</label>
                                    <input type="text" class="form-control form-control-sm" id="nuevoPeajeObservaciones" placeholder="Observaciones">
                                </div>
                            </div>
                            <div class="text-right mt-3">
                                <button type="button" class="btn btn-success-custom btn-sm" onclick="agregarPeaje()">
                                    <i class="fas fa-plus mr-1"></i>Agregar Peaje
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Lista de peajes -->
                    <div class="card card-list">
                        <div class="card-header d-flex justify-content-between">
                            <span><i class="fas fa-list mr-2"></i>Peajes Registrados</span>
                            <span class="badge badge-primary-custom" id="totalPeajes">0 peajes</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-modal mb-0">
                                    <thead>
                                        <tr>
                                            <th>Estación</th>
                                            <th>Fecha</th>
                                            <th>Comprobante</th>
                                            <th>Soles</th>
                                            <th>Dólares</th>
                                            <th width="80">Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaPeajes"></tbody>
                                </table>
                            </div>
                        </div>
                        <div class="card-footer-totals">
                            <div class="row">
                                <div class="col-6">
                                    <strong>Total Soles: <span id="totalPeajesSoles">S/ 0.00</span></strong>
                                </div>
                                <div class="col-6 text-right">
                                    <strong>Total Dólares: <span id="totalPeajesDolares">$ 0.00</span></strong>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary-custom" data-dismiss="modal">Cerrar</button>
                    <button type="button" class="btn btn-primary-custom" onclick="aplicarPeajes()">Aplicar y Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <!-- Modal Reparaciones -->
    <div class="modal fade" id="modalReparaciones" tabindex="-1">
        <div class="modal-dialog modal-lg">
            <div class="modal-content">
                <div class="modal-header modal-header-custom">
                    <h5 class="modal-title"><i class="fas fa-wrench mr-2"></i>Gestión de Reparaciones</h5>
                    <button type="button" class="close" data-dismiss="modal">
                        <span>&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <div class="card card-form mb-3">
                        <div class="card-header"><i class="fas fa-plus mr-2"></i>Agregar Reparación</div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-6">
                                    <label class="form-label">Tipo de Reparación</label>
                                    <input type="text" class="form-control form-control-sm" id="nuevaReparacionTipo" placeholder="Ej: Cambio de llanta">
                                </div>
                                <div class="col-md-6">
                                    <label class="form-label">Fecha</label>
                                    <input type="date" class="form-control form-control-sm" id="nuevaReparacionFecha">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-4">
                                    <label class="form-label">N° Comprobante</label>
                                    <input type="text" class="form-control form-control-sm" id="nuevaReparacionComprobante" placeholder="001-123456">
                                </div>
                                <div class="col-md-4">
                                    <label class="form-label">Monto Soles</label>
                                    <input type="number" class="form-control form-control-sm" id="nuevaReparacionSoles" placeholder="0.00" step="0.01">
                                </div>
                                <div class="col-md-4">
                                    <label class="form-label">Monto Dólares</label>
                                    <input type="number" class="form-control form-control-sm" id="nuevaReparacionDolares" placeholder="0.00" step="0.01">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-12">
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
                                <div class="col-6">
                                    <strong>Total Soles: <span id="totalReparacionesSoles">S/ 0.00</span></strong>
                                </div>
                                <div class="col-6 text-right">
                                    <strong>Total Dólares: <span id="totalReparacionesDolares">$ 0.00</span></strong>
                                </div>
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
        <div class="modal-dialog modal-lg">
            <div class="modal-content">
                <div class="modal-header modal-header-custom">
                    <h5 class="modal-title"><i class="fas fa-bed mr-2"></i>Gestión de Hospedaje</h5>
                    <button type="button" class="close" data-dismiss="modal">
                        <span>&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <div class="card card-form mb-3">
                        <div class="card-header"><i class="fas fa-plus mr-2"></i>Agregar Hospedaje</div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-6">
                                    <label class="form-label">Hotel/Lugar</label>
                                    <input type="text" class="form-control form-control-sm" id="nuevoHospedajeLugar" placeholder="Ej: Hotel Pacifico">
                                </div>
                                <div class="col-md-6">
                                    <label class="form-label">Fecha</label>
                                    <input type="date" class="form-control form-control-sm" id="nuevoHospedajeFecha">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-4">
                                    <label class="form-label">N° Comprobante</label>
                                    <input type="text" class="form-control form-control-sm" id="nuevoHospedajeComprobante" placeholder="001-123456">
                                </div>
                                <div class="col-md-4">
                                    <label class="form-label">Monto Soles</label>
                                    <input type="number" class="form-control form-control-sm" id="nuevoHospedajeSoles" placeholder="0.00" step="0.01">
                                </div>
                                <div class="col-md-4">
                                    <label class="form-label">Monto Dólares</label>
                                    <input type="number" class="form-control form-control-sm" id="nuevoHospedajeDolares" placeholder="0.00" step="0.01">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-12">
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
                                <div class="col-6">
                                    <strong>Total Soles: <span id="totalHospedajesSoles">S/ 0.00</span></strong>
                                </div>
                                <div class="col-6 text-right">
                                    <strong>Total Dólares: <span id="totalHospedajesDolares">$ 0.00</span></strong>
                                </div>
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
        <div class="modal-dialog modal-lg">
            <div class="modal-content">
                <div class="modal-header modal-header-custom">
                    <h5 class="modal-title"><i class="fas fa-gas-pump mr-2"></i>Gestión de Combustible</h5>
                    <button type="button" class="close" data-dismiss="modal">
                        <span>&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <div class="card card-form mb-3">
                        <div class="card-header"><i class="fas fa-plus mr-2"></i>Agregar Combustible</div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-6">
                                    <label class="form-label">Estación/Lugar</label>
                                    <input type="text" class="form-control form-control-sm" id="nuevoCombustibleLugar" placeholder="Ej: Grifo Primax">
                                </div>
                                <div class="col-md-6">
                                    <label class="form-label">Fecha</label>
                                    <input type="date" class="form-control form-control-sm" id="nuevoCombustibleFecha">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-4">
                                    <label class="form-label">N° Comprobante</label>
                                    <input type="text" class="form-control form-control-sm" id="nuevoCombustibleComprobante" placeholder="001-123456">
                                </div>
                                <div class="col-md-4">
                                    <label class="form-label">Monto Soles</label>
                                    <input type="number" class="form-control form-control-sm" id="nuevoCombustibleSoles" placeholder="0.00" step="0.01">
                                </div>
                                <div class="col-md-4">
                                    <label class="form-label">Monto Dólares</label>
                                    <input type="number" class="form-control form-control-sm" id="nuevoCombustibleDolares" placeholder="0.00" step="0.01">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-12">
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
                                <div class="col-6">
                                    <strong>Total Soles: <span id="totalCombustiblesSoles">S/ 0.00</span></strong>
                                </div>
                                <div class="col-6 text-right">
                                    <strong>Total Dólares: <span id="totalCombustiblesDolares">$ 0.00</span></strong>
                                </div>
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

    <!-- CSS Profesional -->
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/AgregarOrdenViaje.css") %>" rel="stylesheet" />

    <!-- JavaScript (jQuery y Bootstrap los carga Site.Master) -->

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            txtFechaSalida: '<%= txtFechaSalida.ClientID %>',
            txtFechaLlegada: '<%= txtFechaLlegada.ClientID %>',
            ObtenerEstacionesPeajeJSON: '<%= ObtenerEstacionesPeajeJSON() %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/AgregarOrdenViaje.js") %>"></script>
</asp:Content>
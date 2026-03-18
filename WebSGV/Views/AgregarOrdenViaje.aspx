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
                            <i class="fas fa-route mr-2"></i>Crear Orden de Viaje
                        </h2>
                        <p class="text-muted mb-0">Complete la información del viaje y gestión financiera</p>
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
                                    <div class="balance-final mt-4">
                                        <div class="row">
                                            <div class="col-md-6">
                                                <div class="balance-item">
                                                    <span class="balance-label">Balance en Soles:</span>
                                                    <span class="balance-amount" id="diferenciaSoles">0.00</span>
                                                </div>
                                            </div>
                                            <div class="col-md-6">
                                                <div class="balance-item">
                                                    <span class="balance-label">Balance en Dólares:</span>
                                                    <span class="balance-amount" id="diferenciaDolares">0.00</span>
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

        /* === LAYOUT === */
        .page-header {
            padding-bottom: 1rem;
            border-bottom: 2px solid var(--border-color);
        }

        .page-title {
            color: var(--text-primary);
            font-weight: 600;
            font-size: 1.75rem;
            margin: 0;
        }

        /* === BOTONES === */
        .btn-back {
            border-color: var(--border-color);
            color: var(--text-secondary);
            padding: 0.5rem 1.25rem;
            font-weight: 500;
            transition: all 0.2s;
        }

            .btn-back:hover {
                background-color: var(--light-gray);
                border-color: var(--dark-gray);
                color: var(--text-primary);
            }

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
            }

        .btn-secondary-custom {
            background-color: white;
            border: 1px solid var(--border-color);
            color: var(--text-secondary);
            font-weight: 500;
            transition: all 0.2s;
        }

            .btn-secondary-custom:hover {
                background-color: var(--light-gray);
                border-color: var(--dark-gray);
                color: var(--text-primary);
            }

        .btn-success-custom {
            background-color: var(--success-color);
            border-color: var(--success-color);
            color: white;
            font-weight: 500;
        }

            .btn-success-custom:hover {
                background-color: #047857;
            }

        .btn-danger-custom {
            background-color: var(--danger-color);
            border-color: var (--danger-color);
            color: white;
            font-weight: 500;
        }

            .btn-danger-custom:hover {
                background-color: #b91c1c;
            }

        .btn-detail-modal {
            background-color: transparent;
            border: 1px solid var(--primary-color);
            color: var(--primary-color);
            padding: 0.25rem 0.5rem;
            font-size: 0.75rem;
        }

            .btn-detail-modal:hover {
                background-color: var(--primary-color);
                color: white;
            }

        /* === ALERTAS === */
        .alert-info-custom {
            background-color: #e0f2fe;
            border: 1px solid #7dd3fc;
            border-radius: 0.5rem;
            color: #0c4a6e;
            padding: 1rem;
        }

        .alert-warning-custom {
            background-color: #fef3c7;
            border: 1px solid #fbbf24;
            border-radius: 0.5rem;
            color: #78350f;
            padding: 1rem;
        }

        .alert-info-light {
            background-color: #f0f9ff;
            border: 1px solid #bae6fd;
            border-radius: 0.5rem;
            padding: 1rem;
        }

        /* === PESTAÑAS === */
        .nav-tabs-custom {
            border-bottom: 2px solid var(--border-color);
            margin-bottom: 0 !important;
        }

            .nav-tabs-custom .nav-item {
                margin-bottom: -2px;
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
                    border-bottom-color: var(--medium-gray);
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

        .section-header-success {
            background-color: #f0fdf4;
            border-bottom-color: #86efac;
        }

        .section-header-danger {
            background-color: #fef2f2;
            border-bottom-color: #fca5a5;
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

        .form-label-sm {
            font-size: 0.75rem;
            font-weight: 500;
            color: var(--text-secondary);
            margin-bottom: 0.25rem;
        }

        .form-group {
            margin-bottom: 1rem;
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

        .form-text {
            font-size: 0.75rem;
        }

        /* === TABLAS === */
        .table-professional {
            font-size: 0.875rem;
        }

            .table-professional thead th {
                background-color: var(--light-gray);
                border-bottom: 2px solid var(--border-color);
                color: var(--text-primary);
                font-weight: 600;
                font-size: 0.8125rem;
                text-transform: uppercase;
                letter-spacing: 0.025em;
                padding: 0.75rem;
            }

            .table-professional tbody td {
                vertical-align: middle;
                padding: 0.75rem;
                border-bottom: 1px solid var(--medium-gray);
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
                letter-spacing: 0.025em;
            }

            .table-modal tbody td {
                padding: 0.625rem;
                vertical-align: middle;
            }

        /* === BADGES === */
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

        /* === AJUSTES FINANCIEROS === */
        .adjustment-card {
            background-color: var(--light-gray);
            border: 1px solid var(--border-color);
            border-radius: 0.5rem;
            padding: 1rem;
        }

        .adjustment-discount {
            background-color: #fff7ed;
            border-color: #fed7aa;
        }

        .adjustment-reintegro {
            background-color: #ecfdf5;
            border-color: #a7f3d0;
        }

        .adjustment-label {
            font-weight: 600;
            color: var(--text-primary);
            font-size: 0.9375rem;
            display: block;
            margin-bottom: 0.25rem;
        }

        .adjustment-description {
            font-size: 0.8125rem;
            color: var(--text-secondary);
            margin-bottom: 0.75rem;
        }

        /* === BALANCE FINAL === */
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

        .modal-title {
            color: var(--text-primary);
            font-weight: 600;
        }

        .card-form {
            border: 1px solid var(--border-color);
            border-radius: 0.5rem;
        }

            .card-form .card-header {
                background-color: var(--light-gray);
                border-bottom: 1px solid var(--border-color);
                font-weight: 600;
                color: var(--text-primary);
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

        .card-detalles {
            border: 1px solid var(--border-color);
            border-radius: 0.5rem;
        }

        .card-header-custom {
            background-color: var(--light-gray);
            border-bottom: 1px solid var(--border-color);
            padding: 0.875rem 1.25rem;
        }

        /* === RESPONSIVE === */
        @media (max-width: 768px) {
            .page-title {
                font-size: 1.375rem;
            }

            .nav-tabs-custom .nav-link {
                padding: 0.75rem 1rem;
                font-size: 0.875rem;
            }

            .section-body {
                padding: 1rem;
            }

            .balance-amount {
                font-size: 1.25rem;
            }
        }

        /* === UTILIDADES === */
        .text-muted {
            color: var(--text-secondary) !important;
        }

        .mr-1 {
            margin-right: 0.25rem;
        }

        .mr-2 {
            margin-right: 0.5rem;
        }

        .ml-2 {
            margin-left: 0.5rem;
        }

        .ml-3 {
            margin-left: 0.75rem;
        }

        .mb-3 {
            margin-bottom: 0.75rem;
        }

        .mb-4 {
            margin-bottom: 1rem;
        }

        .mb-5 {
            margin-bottom: 1.25rem;
        }

        .mt-3 {
            margin-top: 0.75rem;
        }

        .mt-4 {
            margin-top: 1rem;
        }

        .pt-3 {
            padding-top: 0.75rem;
        }

        .px-4 {
            padding-left: 1rem;
            padding-right: 1rem;
        }

        .px-5 {
            padding-left: 1.25rem;
            padding-right: 1.25rem;
        }
    </style>

    <!-- JavaScript -->
    <script src="https://code.jquery.com/jquery-3.6.0.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@4.6.0/dist/js/bootstrap.bundle.min.js"></script>

    <script>
        // Variables globales
        let contadorIngresosAdicionales = 0;
        let contadorGastosAdicionales = 0;

        // Datos de modales
        let peajesData = [];
        let reparacionesData = [];
        let hospedajesData = [];
        let combustiblesData = [];

        let contadorPeajes = 0;
        let contadorReparaciones = 0;
        let contadorHospedajes = 0;
        let contadorCombustibles = 0;

        let estacionesPeaje = [];

        // Inicialización
        $(document).ready(function () {
            console.log('✅ Sistema iniciado');
            detectarOrigenViaje();
            configurarFechasPorDefecto();
            cargarEstacionesPeaje();
            calcularTotales();
        });

        function detectarOrigenViaje() {
            const urlParams = new URLSearchParams(window.location.search);
            const origen = urlParams.get('origen');
            if (origen === 'viajeFinalizado') {
                $('#hfOrigenViaje').val('viajeFinalizado');
            }
        }

        function configurarFechasPorDefecto() {
            const hoy = new Date().toISOString().split('T')[0];
            $('#nuevoPeajeFecha, #nuevaReparacionFecha, #nuevoHospedajeFecha, #nuevoCombustibleFecha').val(hoy);

            if (!$('#<%= txtFechaSalida.ClientID %>').val()) {
                $('#<%= txtFechaSalida.ClientID %>').val(hoy);
            }
            if (!$('#<%= txtFechaLlegada.ClientID %>').val()) {
                const manana = new Date();
                manana.setDate(manana.getDate() + 1);
                $('#<%= txtFechaLlegada.ClientID %>').val(manana.toISOString().split('T')[0]);
            }
        }

        function mostrarDetallesViajeOrigen() {
            $('#pnlDetallesViajeOrigen').collapse('toggle');
        }

        // INGRESOS
        function agregarIngreso() {
            contadorIngresosAdicionales++;
            const numeroFila = 4 + contadorIngresosAdicionales;
            $('#ingresosAdicionalesBody').append(`
                <tr id="ingresoAdicional_${contadorIngresosAdicionales}">
                    <td class="text-center">${numeroFila}</td>
                    <td><input type="text" class="form-control form-control-sm" name="conceptoIngreso_${contadorIngresosAdicionales}" placeholder="Concepto" required></td>
                    <td><input type="text" class="form-control form-control-sm" name="descIngreso_${contadorIngresosAdicionales}" placeholder="Descripción"></td>
                    <td><input type="number" class="form-control form-control-sm ingreso-soles" name="ingresoSoles_${contadorIngresosAdicionales}" placeholder="0.00" step="0.01" onchange="calcularTotales()"></td>
                    <td><input type="number" class="form-control form-control-sm ingreso-dolares" name="ingresoDolares_${contadorIngresosAdicionales}" placeholder="0.00" step="0.01" onchange="calcularTotales()"></td>
                    <td class="text-center"><button type="button" class="btn btn-danger btn-sm" onclick="eliminarIngreso(${contadorIngresosAdicionales})"><i class="fas fa-trash"></i></button></td>
                </tr>
            `);
        }

        function eliminarIngreso(id) {
            if (confirm('¿Eliminar este ingreso?')) {
                $(`#ingresoAdicional_${id}`).remove();
                calcularTotales();
            }
        }

        // GASTOS
        function agregarGasto() {
            contadorGastosAdicionales++;
            const numeroFila = 8 + contadorGastosAdicionales;
            $('#gastosAdicionalesBody').append(`
                <tr id="gastoAdicional_${contadorGastosAdicionales}">
                    <td class="text-center">${numeroFila}</td>
                    <td><input type="text" class="form-control form-control-sm" name="conceptoGasto_${contadorGastosAdicionales}" placeholder="Concepto" required></td>
                    <td><input type="text" class="form-control form-control-sm" name="descGasto_${contadorGastosAdicionales}" placeholder="Descripción"></td>
                    <td><input type="number" class="form-control form-control-sm gasto-soles" name="gastoSoles_${contadorGastosAdicionales}" placeholder="0.00" step="0.01" onchange="calcularTotales()"></td>
                    <td><input type="number" class="form-control form-control-sm gasto-dolares" name="gastoDolares_${contadorGastosAdicionales}" placeholder="0.00" step="0.01" onchange="calcularTotales()"></td>
                    <td class="text-center"><button type="button" class="btn btn-danger btn-sm" onclick="eliminarGasto(${contadorGastosAdicionales})"><i class="fas fa-trash"></i></button></td>
                </tr>
            `);
        }

        function eliminarGasto(id) {
            if (confirm('¿Eliminar este gasto?')) {
                $(`#gastoAdicional_${id}`).remove();
                calcularTotales();
            }
        }

        // CALCULAR TOTALES
        function calcularTotales() {
            let totalIngresosSoles = 0, totalIngresosDolares = 0;
            let totalGastosSoles = 0, totalGastosDolares = 0;

            $('.ingreso-soles').each(function () { totalIngresosSoles += parseFloat($(this).val()) || 0; });
            $('.ingreso-dolares').each(function () { totalIngresosDolares += parseFloat($(this).val()) || 0; });
            $('.gasto-soles').each(function () { totalGastosSoles += parseFloat($(this).val()) || 0; });
            $('.gasto-dolares').each(function () { totalGastosDolares += parseFloat($(this).val()) || 0; });

            const descuentoSoles = parseFloat($('#descuentoSoles').val()) || 0;
            const descuentoDolares = parseFloat($('#descuentoDolares').val()) || 0;
            const reintegroSoles = parseFloat($('#reintegroSoles').val()) || 0;
            const reintegroDolares = parseFloat($('#reintegroDolares').val()) || 0;

            const diferenciaSoles = totalIngresosSoles - totalGastosSoles - descuentoSoles + reintegroSoles;
            const diferenciaDolares = totalIngresosDolares - totalGastosDolares - descuentoDolares + reintegroDolares;

            $('#totalIngresosSoles').text(totalIngresosSoles.toFixed(2));
            $('#totalIngresosDolares').text(totalIngresosDolares.toFixed(2));
            $('#totalGastosSoles').text(totalGastosSoles.toFixed(2));
            $('#totalGastosDolares').text(totalGastosDolares.toFixed(2));
            $('#diferenciaSoles').text(diferenciaSoles.toFixed(2)).css('color', diferenciaSoles >= 0 ? '#059669' : '#dc2626');
            $('#diferenciaDolares').text(diferenciaDolares.toFixed(2)).css('color', diferenciaDolares >= 0 ? '#059669' : '#dc2626');
        }

        // PEAJES
        function cargarEstacionesPeaje() {
            try {
                const json = '<%= ObtenerEstacionesPeajeJSON() %>';
                if (json && json !== '[]') {
                    estacionesPeaje = JSON.parse(json);
                    llenarDatalistEstaciones();
                }
            } catch (e) {
                console.error('Error cargando estaciones:', e);
            }
        }

        function llenarDatalistEstaciones() {
            const datalist = $('#listaEstaciones');
            datalist.empty();
            estacionesPeaje.forEach(est => {
                datalist.append(`<option value="${est.nombre}">`);
            });
        }

        function abrirModalPeajes() {
            $('#modalPeajes').modal('show');
            actualizarTablaPeajes();
        }

        function agregarPeaje() {
            const estacion = $('#nuevoPeajeEstacion').val().trim();
            const fecha = $('#nuevoPeajeFecha').val();
            const soles = parseFloat($('#nuevoPeajeSoles').val()) || 0;
            const dolares = parseFloat($('#nuevoPeajeDolares').val()) || 0;

            if (!estacion) { alert('⚠️ Seleccione una estación'); $('#nuevoPeajeEstacion').focus(); return; }
            if (!fecha) { alert('⚠️ Seleccione una fecha'); $('#nuevoPeajeFecha').focus(); return; }
            if (soles <= 0 && dolares <= 0) { alert('⚠️ Ingrese al menos un monto'); $('#nuevoPeajeSoles').focus(); return; }

            peajesData.push({
                id: ++contadorPeajes,
                estacion: estacion,
                fecha: fecha,
                comprobante: $('#nuevoPeajeComprobante').val().trim(),
                soles: soles,
                dolares: dolares,
                observaciones: $('#nuevoPeajeObservaciones').val().trim()
            });

            $('#nuevoPeajeEstacion, #nuevoPeajeComprobante, #nuevoPeajeObservaciones').val('');
            $('#nuevoPeajeSoles, #nuevoPeajeDolares').val('');
            actualizarTablaPeajes();
            actualizarTotalesPeajes();
        }

        function eliminarPeaje(id) {
            if (confirm('¿Eliminar este peaje?')) {
                peajesData = peajesData.filter(p => p.id !== id);
                actualizarTablaPeajes();
                actualizarTotalesPeajes();
            }
        }

        function actualizarTablaPeajes() {
            const tbody = $('#tablaPeajes');
            tbody.empty();
            peajesData.forEach(p => {
                tbody.append(`
                    <tr>
                        <td>${p.estacion}</td>
                        <td>${p.fecha}</td>
                        <td>${p.comprobante || 'N/A'}</td>
                        <td>S/ ${p.soles.toFixed(2)}</td>
                        <td>$ ${p.dolares.toFixed(2)}</td>
                        <td class="text-center"><button type="button" class="btn btn-danger btn-sm" onclick="eliminarPeaje(${p.id})"><i class="fas fa-trash"></i></button></td>
                    </tr>
                `);
            });
            $('#totalPeajes').text(`${peajesData.length} peajes`);
        }

        function actualizarTotalesPeajes() {
            const totalS = peajesData.reduce((sum, p) => sum + p.soles, 0);
            const totalD = peajesData.reduce((sum, p) => sum + p.dolares, 0);
            $('#totalPeajesSoles').text(`S/ ${totalS.toFixed(2)}`);
            $('#totalPeajesDolares').text(`$ ${totalD.toFixed(2)}`);
            $('#peajesSoles').val(totalS > 0 ? totalS.toFixed(2) : '');
            $('#peajesDolares').val(totalD > 0 ? totalD.toFixed(2) : '');
            $('#descPeajes').val(peajesData.length > 0 ? `${peajesData.length} peajes registrados` : '');
            $('#contadorPeajes').text(peajesData.length);
            calcularTotales();
        }

        function aplicarPeajes() {
            $('#modalPeajes').modal('hide');
        }

        // REPARACIONES
        function abrirModalReparaciones() {
            $('#modalReparaciones').modal('show');
            actualizarTablaReparaciones();
        }

        function agregarReparacion() {
            const tipo = $('#nuevaReparacionTipo').val().trim();
            const fecha = $('#nuevaReparacionFecha').val();
            const soles = parseFloat($('#nuevaReparacionSoles').val()) || 0;
            const dolares = parseFloat($('#nuevaReparacionDolares').val()) || 0;

            if (!tipo) { alert('Ingrese el tipo de reparación'); return; }
            if (!fecha) { alert('Seleccione una fecha'); return; }
            if (soles <= 0 && dolares <= 0) { alert('Ingrese al menos un monto'); return; }

            reparacionesData.push({
                id: ++contadorReparaciones,
                tipo: tipo,
                fecha: fecha,
                comprobante: $('#nuevaReparacionComprobante').val().trim(),
                soles: soles,
                dolares: dolares,
                observaciones: $('#nuevaReparacionObservaciones').val().trim()
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
                        <td>${r.tipo}</td>
                        <td>${r.fecha}</td>
                        <td>${r.comprobante || 'N/A'}</td>
                        <td>S/ ${r.soles.toFixed(2)}</td>
                        <td>$ ${r.dolares.toFixed(2)}</td>
                        <td class="text-center"><button type="button" class="btn btn-danger btn-sm" onclick="eliminarReparacion(${r.id})"><i class="fas fa-trash"></i></button></td>
                    </tr>
                `);
            });
            $('#totalReparaciones').text(`${reparacionesData.length} reparaciones`);
        }

        function actualizarTotalesReparaciones() {
            const totalS = reparacionesData.reduce((sum, r) => sum + r.soles, 0);
            const totalD = reparacionesData.reduce((sum, r) => sum + r.dolares, 0);
            $('#totalReparacionesSoles').text(`S/ ${totalS.toFixed(2)}`);
            $('#totalReparacionesDolares').text(`$ ${totalD.toFixed(2)}`);
            $('#reparacionesSoles').val(totalS > 0 ? totalS.toFixed(2) : '');
            $('#reparacionesDolares').val(totalD > 0 ? totalD.toFixed(2) : '');
            $('#descReparaciones').val(reparacionesData.length > 0 ? `${reparacionesData.length} reparaciones` : '');
            $('#contadorReparaciones').text(reparacionesData.length);
            calcularTotales();
        }

        function aplicarReparaciones() {
            $('#modalReparaciones').modal('hide');
        }

        // HOSPEDAJE
        function abrirModalHospedaje() {
            $('#modalHospedaje').modal('show');
            actualizarTablaHospedajes();
        }

        function agregarHospedaje() {
            const lugar = $('#nuevoHospedajeLugar').val().trim();
            const fecha = $('#nuevoHospedajeFecha').val();
            const soles = parseFloat($('#nuevoHospedajeSoles').val()) || 0;
            const dolares = parseFloat($('#nuevoHospedajeDolares').val()) || 0;

            if (!lugar) { alert('Ingrese el lugar'); return; }
            if (!fecha) { alert('Seleccione una fecha'); return; }
            if (soles <= 0 && dolares <= 0) { alert('Ingrese al menos un monto'); return; }

            hospedajesData.push({
                id: ++contadorHospedajes,
                lugar: lugar,
                fecha: fecha,
                comprobante: $('#nuevoHospedajeComprobante').val().trim(),
                soles: soles,
                dolares: dolares,
                observaciones: $('#nuevoHospedajeObservaciones').val().trim()
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
                        <td>${h.lugar}</td>
                        <td>${h.fecha}</td>
                        <td>${h.comprobante || 'N/A'}</td>
                        <td>S/ ${h.soles.toFixed(2)}</td>
                        <td>$ ${h.dolares.toFixed(2)}</td>
                        <td class="text-center"><button type="button" class="btn btn-danger btn-sm" onclick="eliminarHospedaje(${h.id})"><i class="fas fa-trash"></i></button></td>
                    </tr>
                `);
            });
            $('#totalHospedajes').text(`${hospedajesData.length} hospedajes`);
        }

        function actualizarTotalesHospedajes() {
            const totalS = hospedajesData.reduce((sum, h) => sum + h.soles, 0);
            const totalD = hospedajesData.reduce((sum, h) => sum + h.dolares, 0);
            $('#totalHospedajesSoles').text(`S/ ${totalS.toFixed(2)}`);
            $('#totalHospedajesDolares').text(`$ ${totalD.toFixed(2)}`);
            $('#hospedajeSoles').val(totalS > 0 ? totalS.toFixed(2) : '');
            $('#hospedajeDolares').val(totalD > 0 ? totalD.toFixed(2) : '');
            $('#descHospedaje').val(hospedajesData.length > 0 ? `${hospedajesData.length} hospedajes` : '');
            $('#contadorHospedaje').text(hospedajesData.length);
            calcularTotales();
        }

        function aplicarHospedajes() {
            $('#modalHospedaje').modal('hide');
        }

        // COMBUSTIBLE
        function abrirModalCombustible() {
            $('#modalCombustible').modal('show');
            actualizarTablaCombustibles();
        }

        function agregarCombustible() {
            const lugar = $('#nuevoCombustibleLugar').val().trim();
            const fecha = $('#nuevoCombustibleFecha').val();
            const soles = parseFloat($('#nuevoCombustibleSoles').val()) || 0;
            const dolares = parseFloat($('#nuevoCombustibleDolares').val()) || 0;

            if (!lugar) { alert('Ingrese el lugar'); return; }
            if (!fecha) { alert('Seleccione una fecha'); return; }
            if (soles <= 0 && dolares <= 0) { alert('Ingrese al menos un monto'); return; }

            combustiblesData.push({
                id: ++contadorCombustibles,
                lugar: lugar,
                fecha: fecha,
                comprobante: $('#nuevoCombustibleComprobante').val().trim(),
                soles: soles,
                dolares: dolares,
                observaciones: $('#nuevoCombustibleObservaciones').val().trim()
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
                        <td>${c.lugar}</td>
                        <td>${c.fecha}</td>
                        <td>${c.comprobante || 'N/A'}</td>
                        <td>S/ ${c.soles.toFixed(2)}</td>
                        <td>$ ${c.dolares.toFixed(2)}</td>
                        <td class="text-center"><button type="button" class="btn btn-danger btn-sm" onclick="eliminarCombustible(${c.id})"><i class="fas fa-trash"></i></button></td>
                    </tr>
                `);
            });
            $('#totalCombustibles').text(`${combustiblesData.length} combustibles`);
        }

        function actualizarTotalesCombustibles() {
            const totalS = combustiblesData.reduce((sum, c) => sum + c.soles, 0);
            const totalD = combustiblesData.reduce((sum, c) => sum + c.dolares, 0);
            $('#totalCombustiblesSoles').text(`S/ ${totalS.toFixed(2)}`);
            $('#totalCombustiblesDolares').text(`$ ${totalD.toFixed(2)}`);
            $('#combustibleSoles').val(totalS > 0 ? totalS.toFixed(2) : '');
            $('#combustibleDolares').val(totalD > 0 ? totalD.toFixed(2) : '');
            $('#descCombustible').val(combustiblesData.length > 0 ? `${combustiblesData.length} combustibles` : '');
            $('#contadorCombustible').text(combustiblesData.length);
            calcularTotales();
        }

        function aplicarCombustibles() {
            $('#modalCombustible').modal('hide');
        }

        // PREPARAR DATOS
        function prepararDatosFinancieros() {
            const gastosFinancieros = [
                ...peajesData.map(p => ({ categoria: 'Peajes', ...p })),
                ...reparacionesData.map(r => ({ categoria: 'Reparaciones', ...r })),
                ...hospedajesData.map(h => ({ categoria: 'Hospedaje', ...h })),
                ...combustiblesData.map(c => ({ categoria: 'Combustible', ...c }))
            ];
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
    </script>
</asp:Content>
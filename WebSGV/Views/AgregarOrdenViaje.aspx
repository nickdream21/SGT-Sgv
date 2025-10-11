<%@ Page Title="Agregar Orden de Viaje" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AgregarOrdenViaje.aspx.cs" Inherits="WebSGV.Views.AgregarOrdenViaje" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <!-- Campos ocultos -->
    <asp:HiddenField ID="hfIdViajeProgreso" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdConductor" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdTracto" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdCarreta" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdCliente" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfEsInternacional" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfProductosDescarga" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfProductosCarga" runat="server" ClientIDMode="Static" Value="[]" />
    <asp:HiddenField ID="hfGastosFinancieros" runat="server" ClientIDMode="Static" Value="[]" />
    <asp:HiddenField ID="hfOrigenViaje" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="HiddenField1" runat="server" Value="0" />

    <!-- Panel de mensajes -->
    <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-4">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid">
        <div class="row">
            <div class="col-12">
                <div class="d-flex justify-content-between align-items-center mb-3">
                    <h2>
                        <i class="fas fa-route"></i>Crear Orden de Viaje
                    </h2>
                    <a href="ListaDespachos.aspx" class="btn btn-outline-secondary">
                        <i class="fas fa-arrow-left"></i>Volver a Lista
                    </a>
                </div>

                <!-- Panel de Información de Viaje Origen -->
                <asp:Panel ID="pnlInfoViajeOrigen" runat="server" Visible="false" CssClass="alert alert-success mb-3">
                    <div class="d-flex justify-content-between align-items-center">
                        <div>
                            <i class="fas fa-check-circle"></i>
                            <strong>Viaje Finalizado:</strong>
                            <asp:Label ID="lblCantidadDespachosOrigen" runat="server"></asp:Label>
                            despachos procesados |
                            <strong>Conductor:</strong>
                            <asp:Label ID="lblConductorOrigen" runat="server"></asp:Label>
                        </div>
                        <button type="button" class="btn btn-sm btn-outline-success" onclick="mostrarDetallesViajeOrigen()">
                            <i class="fas fa-eye"></i>Ver Detalles
                        </button>
                    </div>
                </asp:Panel>

                <!-- Detalles de Despachos del Viaje Origen -->
                <asp:Panel ID="pnlDetallesViajeOrigen" runat="server" Visible="false" CssClass="collapse mb-4" ClientIDMode="Static">
                    <div class="card border-info">
                        <div class="card-header bg-light">
                            <h6 class="mb-0 text-info">
                                <i class="fas fa-list-alt"></i>Despachos del Viaje Finalizado
                            </h6>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <asp:GridView ID="gvDespachosViajeOrigen" runat="server"
                                    CssClass="table table-sm table-striped mb-0"
                                    AutoGenerateColumns="false"
                                    EmptyDataText="No hay despachos para mostrar">
                                    <Columns>
                                        <asp:BoundField DataField="NumeroDespacho" HeaderText="N° Despacho" ItemStyle-CssClass="font-weight-bold text-primary" />
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

                <hr class="mb-4">
            </div>
        </div>

        <!-- PESTAÑAS PRINCIPALES -->
        <div class="row">
            <div class="col-12">
                <ul class="nav nav-tabs nav-fill" id="mainTabs" role="tablist">
                    <li class="nav-item">
                        <a class="nav-link active" id="datosViaje-tab" data-toggle="tab" href="#datosViaje" role="tab">
                            <i class="fas fa-truck"></i>Datos del Viaje
                        </a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link" id="gestionFinanciera-tab" data-toggle="tab" href="#gestionFinanciera" role="tab">
                            <i class="fas fa-calculator"></i>Gestión Financiera
                        </a>
                    </li>
                </ul>

                <div class="tab-content border border-top-0" id="mainTabsContent">

                    <!-- TAB 1: DATOS DEL VIAJE -->
                    <div class="tab-pane fade show active" id="datosViaje" role="tabpanel">
                        <div class="p-4">

                            <!-- Datos Generales de la Orden -->
                            <div class="row mb-4">
                                <div class="col-12">
                                    <div class="card">
                                        <div class="card-header bg-primary text-white">
                                            <h5 class="mb-0">
                                                <i class="fas fa-clipboard-list"></i>Datos Generales de la Orden
                                            </h5>
                                        </div>
                                        <div class="card-body">
                                            <div class="row">
                                                <div class="col-md-3">
                                                    <label class="font-weight-bold">N° Orden Viaje:</label>
                                                    <asp:TextBox ID="txtNumeroOrdenViaje" runat="server" CssClass="form-control" placeholder="123456" MaxLength="6"></asp:TextBox>
                                                </div>
                                                <div class="col-md-3">
                                                    <label class="font-weight-bold">Fecha Salida:</label>
                                                    <asp:TextBox ID="txtFechaSalida" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                                </div>
                                                <div class="col-md-3">
                                                    <label class="font-weight-bold">Fecha Llegada:</label>
                                                    <asp:TextBox ID="txtFechaLlegada" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                                </div>
                                                <div class="col-md-3">
                                                    <label class="font-weight-bold">Estado:</label>
                                                    <asp:DropDownList ID="ddlEstadoOrden" runat="server" CssClass="form-control">
                                                        <asp:ListItem Text="PENDIENTE" Value="PENDIENTE" Selected="True"></asp:ListItem>
                                                        <asp:ListItem Text="EN_PROCESO" Value="EN_PROCESO"></asp:ListItem>
                                                        <asp:ListItem Text="COMPLETADO" Value="COMPLETADO"></asp:ListItem>
                                                    </asp:DropDownList>
                                                </div>
                                            </div>
                                            <div class="row mt-3">
                                                <div class="col-md-3">
                                                    <label class="font-weight-bold">Hora Salida:</label>
                                                    <asp:TextBox ID="txtHoraSalida" runat="server" CssClass="form-control" TextMode="Time"></asp:TextBox>
                                                </div>
                                                <div class="col-md-3">
                                                    <label class="font-weight-bold">Hora Llegada:</label>
                                                    <asp:TextBox ID="txtHoraLlegada" runat="server" CssClass="form-control" TextMode="Time"></asp:TextBox>
                                                </div>
                                                <div class="col-md-6">
                                                    <label class="font-weight-bold">Observaciones Generales:</label>
                                                    <asp:TextBox ID="txtObservaciones" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="2"></asp:TextBox>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <!-- Información del Viaje/Vehículo -->
                            <div class="row mb-4">
                                <div class="col-12">
                                    <div class="card">
                                        <div class="card-header bg-info text-white">
                                            <h5 class="mb-0">
                                                <i class="fas fa-truck-moving"></i>Información del Viaje y Vehículo
                                            </h5>
                                        </div>
                                        <div class="card-body">
                                            <div class="row">
                                                <div class="col-md-4">
                                                    <label class="font-weight-bold">Conductor:</label>
                                                    <asp:TextBox ID="txtConductor" runat="server" CssClass="form-control bg-light" ReadOnly="true"></asp:TextBox>
                                                    <small class="text-muted">Información del viaje finalizado</small>
                                                </div>
                                                <div class="col-md-4">
                                                    <label class="font-weight-bold">Placa Tracto:</label>
                                                    <asp:TextBox ID="txtPlacaTracto" runat="server" CssClass="form-control bg-light" ReadOnly="true"></asp:TextBox>
                                                    <small class="text-muted">Información del viaje finalizado</small>
                                                </div>
                                                <div class="col-md-4">
                                                    <label class="font-weight-bold">Placa Carreta:</label>
                                                    <asp:TextBox ID="txtPlacaCarreta" runat="server" CssClass="form-control bg-light" ReadOnly="true"></asp:TextBox>
                                                    <small class="text-muted">Información del viaje finalizado</small>
                                                </div>
                                            </div>
                                            <div class="row mt-3">
                                                <div class="col-md-4">
                                                    <label class="font-weight-bold">Cliente Principal:</label>
                                                    <asp:TextBox ID="txtClientePrincipal" runat="server" CssClass="form-control bg-light" ReadOnly="true"></asp:TextBox>
                                                    <small class="text-muted">Del viaje finalizado</small>
                                                </div>
                                                <div class="col-md-4">
                                                    <label class="font-weight-bold">Tipo Operación:</label>
                                                    <asp:TextBox ID="txtTipoOperacion" runat="server" CssClass="form-control bg-light" ReadOnly="true"></asp:TextBox>
                                                    <small class="text-muted">Del viaje finalizado</small>
                                                </div>
                                                <div class="col-md-4">
                                                    <label class="font-weight-bold">Planta Principal:</label>
                                                    <asp:TextBox ID="txtPlantaPrincipal" runat="server" CssClass="form-control bg-light" ReadOnly="true"></asp:TextBox>
                                                    <small class="text-muted">Del viaje finalizado</small>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <!-- Panel de Variaciones -->
                            <asp:Panel ID="pnlVariaciones" runat="server" Visible="false" CssClass="row mb-4">
                                <div class="col-12">
                                    <div class="alert alert-warning">
                                        <i class="fas fa-info-circle"></i>
                                        <strong>Múltiples operaciones detectadas.</strong>
                                        Los datos mostrados corresponden a la primera operación.
                                        <button type="button" class="btn btn-sm btn-outline-warning ml-2" onclick="$('#pnlDetallesViajeOrigen').collapse('show')">
                                            Ver todas las operaciones
                                        </button>
                                    </div>
                                </div>
                            </asp:Panel>

                        </div>
                    </div>

                    <!-- TAB 2: GESTIÓN FINANCIERA -->
                    <div class="tab-pane fade" id="gestionFinanciera" role="tabpanel">
                        <div class="p-4">

                            <!-- Resumen Financiero -->
                            <div class="row mb-4">
                                <div class="col-12">
                                    <div class="card">
                                        <div class="card-header bg-success text-white">
                                            <h5 class="mb-0">
                                                <i class="fas fa-chart-line"></i>Resumen Financiero del Viaje
                                            </h5>
                                        </div>
                                        <div class="card-body">

                                            <!-- Ingresos -->
                                            <div class="mb-4">
                                                <h6 class="border-bottom pb-2 text-success">
                                                    <i class="fas fa-plus-circle"></i>Ingresos del Viaje
                                                </h6>
                                                <div class="table-responsive">
                                                    <table class="table table-bordered">
                                                        <thead class="table-success">
                                                            <tr>
                                                                <th width="5%">#</th>
                                                                <th width="20%">Concepto</th>
                                                                <th width="25%">Descripción</th>
                                                                <th width="20%">Soles (S/)</th>
                                                                <th width="20%">Dólares ($)</th>
                                                                <th width="10%">Acción</th>
                                                            </tr>
                                                        </thead>
                                                        <tbody id="ingresosBody">
                                                            <tr>
                                                                <td>1</td>
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
                                                                <td><span class="badge badge-secondary">Fijo</span></td>
                                                            </tr>
                                                            <tr>
                                                                <td>2</td>
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
                                                                <td><span class="badge badge-secondary">Fijo</span></td>
                                                            </tr>
                                                            <tr>
                                                                <td>3</td>
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
                                                                <td><span class="badge badge-secondary">Fijo</span></td>
                                                            </tr>
                                                            <tr>
                                                                <td>4</td>
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
                                                                <td><span class="badge badge-secondary">Fijo</span></td>
                                                            </tr>
                                                        </tbody>
                                                        <tbody id="ingresosAdicionalesBody"></tbody>
                                                    </table>
                                                    <button type="button" class="btn btn-success btn-sm" onclick="agregarIngreso()">
                                                        <i class="fas fa-plus"></i>Agregar Ingreso
                                                    </button>
                                                </div>
                                            </div>

                                            <!-- Gastos -->
                                            <div class="mb-4">
                                                <h6 class="border-bottom pb-2 text-danger">
                                                    <i class="fas fa-minus-circle"></i>Gastos del Viaje
                                                </h6>
                                                <div class="table-responsive">
                                                    <table class="table table-bordered">
                                                        <thead class="table-danger">
                                                            <tr>
                                                                <th width="5%">#</th>
                                                                <th width="20%">Concepto</th>
                                                                <th width="25%">Descripción</th>
                                                                <th width="20%">Soles (S/)</th>
                                                                <th width="20%">Dólares ($)</th>
                                                                <th width="10%">Acción</th>
                                                            </tr>
                                                        </thead>
                                                        <tbody id="gastosBody">
                                                            <!-- 1. PEAJES - CON MODAL -->
                                                            <tr>
                                                                <td>1</td>
                                                                <td>
                                                                    <strong>Peajes</strong>
                                                                    <button type="button" class="btn btn-outline-primary btn-sm ml-2" onclick="abrirModalPeajes()">
                                                                        <i class="fas fa-edit"></i>Detallar
                                                                    </button>
                                                                </td>
                                                                <td>
                                                                    <input type="text" class="form-control form-control-sm" name="descPeajes" id="descPeajes" readonly placeholder="Descripción automática">
                                                                </td>
                                                                <td>
                                                                    <input type="number" class="form-control form-control-sm gasto-soles" name="peajesSoles" id="peajesSoles" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                                </td>
                                                                <td>
                                                                    <input type="number" class="form-control form-control-sm gasto-dolares" name="peajesDolares" id="peajesDolares" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                                </td>
                                                                <td><span class="badge badge-info" id="contadorPeajes">0</span></td>
                                                            </tr>

                                                            <!-- 2. ALIMENTACIÓN - SIN MODAL (MANUAL) -->
                                                            <tr>
                                                                <td>2</td>
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
                                                                <td><span class="badge badge-secondary">Manual</span></td>
                                                            </tr>

                                                            <!-- 3. APOYO-SEGURIDAD - SIN MODAL (MANUAL) -->
                                                            <tr>
                                                                <td>3</td>
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
                                                                <td><span class="badge badge-secondary">Manual</span></td>
                                                            </tr>

                                                            <!-- 4. REPARACIONES - CON MODAL -->
                                                            <tr>
                                                                <td>4</td>
                                                                <td>
                                                                    <strong>Reparaciones Varios</strong>
                                                                    <button type="button" class="btn btn-outline-primary btn-sm ml-2" onclick="abrirModalReparaciones()">
                                                                        <i class="fas fa-edit"></i>Detallar
                                                                    </button>
                                                                </td>
                                                                <td>
                                                                    <input type="text" class="form-control form-control-sm" name="descReparaciones" id="descReparaciones" readonly placeholder="Descripción automática">
                                                                </td>
                                                                <td>
                                                                    <input type="number" class="form-control form-control-sm gasto-soles" name="reparacionesSoles" id="reparacionesSoles" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                                </td>
                                                                <td>
                                                                    <input type="number" class="form-control form-control-sm gasto-dolares" name="reparacionesDolares" id="reparacionesDolares" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                                </td>
                                                                <td><span class="badge badge-info" id="contadorReparaciones">0</span></td>
                                                            </tr>

                                                            <!-- 5. MOVILIDAD - SIN MODAL (MANUAL) -->
                                                            <tr>
                                                                <td>5</td>
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
                                                                <td><span class="badge badge-secondary">Manual</span></td>
                                                            </tr>

                                                            <!-- 6. ENCARPADA/DESCENCARPADA - SIN MODAL (MANUAL) -->
                                                            <tr>
                                                                <td>6</td>
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
                                                                <td><span class="badge badge-secondary">Manual</span></td>
                                                            </tr>

                                                            <!-- 7. HOSPEDAJE - CON MODAL -->
                                                            <tr>
                                                                <td>7</td>
                                                                <td>
                                                                    <strong>Hospedaje</strong>
                                                                    <button type="button" class="btn btn-outline-primary btn-sm ml-2" onclick="abrirModalHospedaje()">
                                                                        <i class="fas fa-edit"></i>Detallar
                                                                    </button>
                                                                </td>
                                                                <td>
                                                                    <input type="text" class="form-control form-control-sm" name="descHospedaje" id="descHospedaje" readonly placeholder="Descripción automática">
                                                                </td>
                                                                <td>
                                                                    <input type="number" class="form-control form-control-sm gasto-soles" name="hospedajeSoles" id="hospedajeSoles" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                                </td>
                                                                <td>
                                                                    <input type="number" class="form-control form-control-sm gasto-dolares" name="hospedajeDolares" id="hospedajeDolares" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                                </td>
                                                                <td><span class="badge badge-info" id="contadorHospedaje">0</span></td>
                                                            </tr>

                                                            <!-- 8. COMBUSTIBLE - CON MODAL -->
                                                            <tr>
                                                                <td>8</td>
                                                                <td>
                                                                    <strong>Combustible</strong>
                                                                    <button type="button" class="btn btn-outline-primary btn-sm ml-2" onclick="abrirModalCombustible()">
                                                                        <i class="fas fa-edit"></i>Detallar
                                                                    </button>
                                                                </td>
                                                                <td>
                                                                    <input type="text" class="form-control form-control-sm" name="descCombustible" id="descCombustible" readonly placeholder="Descripción automática">
                                                                </td>
                                                                <td>
                                                                    <input type="number" class="form-control form-control-sm gasto-soles" name="combustibleSoles" id="combustibleSoles" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                                </td>
                                                                <td>
                                                                    <input type="number" class="form-control form-control-sm gasto-dolares" name="combustibleDolares" id="combustibleDolares" readonly placeholder="0.00" step="0.01" onchange="calcularTotales()">
                                                                </td>
                                                                <td><span class="badge badge-info" id="contadorCombustible">0</span></td>
                                                            </tr>
                                                        </tbody>
                                                        <tbody id="gastosAdicionalesBody"></tbody>
                                                    </table>
                                                    <button type="button" class="btn btn-danger btn-sm" onclick="agregarGasto()">
                                                        <i class="fas fa-plus"></i>Agregar Gasto
                                                    </button>
                                                </div>
                                            </div>

                                            <!-- Resumen Final -->
                                            <div class="mb-4">
                                                <h6 class="border-bottom pb-2">
                                                    <i class="fas fa-calculator"></i>Resumen Final
                                                </h6>
                                                <div class="table-responsive">
                                                    <table class="table table-bordered">
                                                        <thead class="table-light">
                                                            <tr>
                                                                <th width="50%">Concepto</th>
                                                                <th width="25%">Soles (S/)</th>
                                                                <th width="25%">Dólares ($)</th>
                                                            </tr>
                                                        </thead>
                                                        <tbody>
                                                            <tr class="table-success">
                                                                <td><strong><i class="fas fa-arrow-up text-success"></i>Total Ingresos</strong></td>
                                                                <td><strong>S/ <span id="totalIngresosSoles">0.00</span></strong></td>
                                                                <td><strong>$ <span id="totalIngresosDolares">0.00</span></strong></td>
                                                            </tr>
                                                            <tr class="table-danger">
                                                                <td><strong><i class="fas fa-arrow-down text-danger"></i>Total Gastos</strong></td>
                                                                <td><strong>S/ <span id="totalGastosSoles">0.00</span></strong></td>
                                                                <td><strong>$ <span id="totalGastosDolares">0.00</span></strong></td>
                                                            </tr>

                                                            <!-- DESCUENTOS -->
                                                            <tr class="table-warning">
                                                                <td>
                                                                    <strong><i class="fas fa-minus-circle text-warning"></i>Descuento</strong>
                                                                    <br>
                                                                    <small class="text-muted">Monto a descontar al chofer</small>
                                                                </td>
                                                                <td>
                                                                    <input type="number" class="form-control form-control-sm descuento-soles"
                                                                        name="descuentoSoles" id="descuentoSoles"
                                                                        placeholder="0.00" step="0.01" onchange="calcularTotales()"
                                                                        style="background-color: #fff3cd;">
                                                                </td>
                                                                <td>
                                                                    <input type="number" class="form-control form-control-sm descuento-dolares"
                                                                        name="descuentoDolares" id="descuentoDolares"
                                                                        placeholder="0.00" step="0.01" onchange="calcularTotales()"
                                                                        style="background-color: #fff3cd;">
                                                                </td>
                                                            </tr>

                                                            <!-- REINTEGROS -->
                                                            <tr class="table-info">
                                                                <td>
                                                                    <strong><i class="fas fa-plus-circle text-info"></i>Reintegro</strong>
                                                                    <br>
                                                                    <small class="text-muted">Monto a reintegrar al chofer</small>
                                                                </td>
                                                                <td>
                                                                    <input type="number" class="form-control form-control-sm reintegro-soles"
                                                                        name="reintegroSoles" id="reintegroSoles"
                                                                        placeholder="0.00" step="0.01" onchange="calcularTotales()"
                                                                        style="background-color: #d1ecf1;">
                                                                </td>
                                                                <td>
                                                                    <input type="number" class="form-control form-control-sm reintegro-dolares"
                                                                        name="reintegroDolares" id="reintegroDolares"
                                                                        placeholder="0.00" step="0.01" onchange="calcularTotales()"
                                                                        style="background-color: #d1ecf1;">
                                                                </td>
                                                            </tr>

                                                            <!-- DIFERENCIA FINAL -->
                                                            <tr class="table-primary">
                                                                <td><strong><i class="fas fa-equals"></i>Diferencia Final</strong></td>
                                                                <td><strong>S/ <span id="diferenciaSoles">0.00</span></strong></td>
                                                                <td><strong>$ <span id="diferenciaDolares">0.00</span></strong></td>
                                                            </tr>
                                                        </tbody>
                                                    </table>
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
        <div class="row mt-4 mb-4">
            <div class="col-12 text-center">
                <asp:Button ID="btnGuardarOrden" runat="server" Text="Guardar Orden de Viaje"
                    CssClass="btn btn-primary btn-lg px-5" OnClick="btnGuardarOrden_Click" />
                <a href="ListaDespachos.aspx" class="btn btn-secondary btn-lg ml-3 px-4">
                    <i class="fas fa-times"></i>Cancelar
                </a>
            </div>
        </div>

    </div>

    <!-- ========== MODALES ========== -->

    <!-- Modal Peajes -->
    <div class="modal fade" id="modalPeajes" tabindex="-1">
        <div class="modal-dialog modal-lg">
            <div class="modal-content">
                <div class="modal-header bg-primary text-white">
                    <h5 class="modal-title"><i class="fas fa-road"></i>Gestión de Peajes</h5>
                    <button type="button" class="close text-white" data-dismiss="modal">
                        <span>&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <!-- Formulario agregar peaje -->
                    <div class="card mb-3">
                        <div class="card-header"><i class="fas fa-plus"></i>Agregar Peaje</div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-4">
                                    <label>Estación/Peaje:</label>
                                    <input type="text"
                                        class="form-control"
                                        id="nuevoPeajeEstacion"
                                        list="listaEstaciones"
                                        placeholder="Ej: Peaje Sullana"
                                        autocomplete="off">
                                    <datalist id="listaEstaciones">
                                        <!-- Se llena dinámicamente con JavaScript -->
                                    </datalist>
                                </div>
                                <div class="col-md-4">
                                    <label>Fecha: <span class="text-danger">*</span></label>
                                    <input type="date" class="form-control" id="nuevoPeajeFecha">
                                </div>
                                <div class="col-md-4">
                                    <label>N° Comprobante:</label>
                                    <input type="text" class="form-control" id="nuevoPeajeComprobante" placeholder="001-123456">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-6">
                                    <label>Monto Soles:</label>
                                    <input type="number" class="form-control" id="nuevoPeajeSoles" placeholder="0.00" step="0.01">
                                </div>
                                <div class="col-md-6">
                                    <label>Monto Dólares:</label>
                                    <input type="number" class="form-control" id="nuevoPeajeDolares" placeholder="0.00" step="0.01">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-12">
                                    <label>Observaciones:</label>
                                    <input type="text" class="form-control" id="nuevoPeajeObservaciones" placeholder="Observaciones">
                                </div>
                            </div>
                            <div class="text-right mt-3">
                                <button type="button" class="btn btn-success" onclick="agregarPeaje()">
                                    <i class="fas fa-plus"></i>Agregar Peaje
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Lista de peajes -->
                    <div class="card">
                        <div class="card-header d-flex justify-content-between">
                            <span><i class="fas fa-list"></i>Peajes Registrados</span>
                            <span class="badge badge-primary" id="totalPeajes">0 peajes</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-sm mb-0">
                                    <thead class="thead-light">
                                        <tr>
                                            <th>Estación</th>
                                            <th>Fecha</th>
                                            <th>Comprobante</th>
                                            <th>Soles</th>
                                            <th>Dólares</th>
                                            <th>Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaPeajes"></tbody>
                                </table>
                            </div>
                        </div>
                        <div class="card-footer">
                            <div class="row">
                                <div class="col-6">
                                    <strong>Total Soles: <span id="totalPeajesSoles" class="text-success">S/ 0.00</span></strong>
                                </div>
                                <div class="col-6 text-right">
                                    <strong>Total Dólares: <span id="totalPeajesDolares" class="text-success">$ 0.00</span></strong>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cerrar</button>
                    <button type="button" class="btn btn-primary" onclick="aplicarPeajes()">Aplicar y Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <!-- Modal Reparaciones -->
    <div class="modal fade" id="modalReparaciones" tabindex="-1">
        <div class="modal-dialog modal-lg">
            <div class="modal-content">
                <div class="modal-header bg-warning text-dark">
                    <h5 class="modal-title"><i class="fas fa-wrench"></i>Gestión de Reparaciones</h5>
                    <button type="button" class="close" data-dismiss="modal">
                        <span>&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <!-- Formulario agregar reparación -->
                    <div class="card mb-3">
                        <div class="card-header"><i class="fas fa-plus"></i>Agregar Reparación</div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-6">
                                    <label>Tipo de Reparación:</label>
                                    <input type="text" class="form-control" id="nuevaReparacionTipo" placeholder="Ej: Cambio de llanta">
                                </div>
                                <div class="col-md-6">
                                    <label>Fecha:</label>
                                    <input type="date" class="form-control" id="nuevaReparacionFecha">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-4">
                                    <label>N° Comprobante:</label>
                                    <input type="text" class="form-control" id="nuevaReparacionComprobante" placeholder="001-123456">
                                </div>
                                <div class="col-md-4">
                                    <label>Monto Soles:</label>
                                    <input type="number" class="form-control" id="nuevaReparacionSoles" placeholder="0.00" step="0.01">
                                </div>
                                <div class="col-md-4">
                                    <label>Monto Dólares:</label>
                                    <input type="number" class="form-control" id="nuevaReparacionDolares" placeholder="0.00" step="0.01">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-12">
                                    <label>Observaciones:</label>
                                    <input type="text" class="form-control" id="nuevaReparacionObservaciones" placeholder="Detalles de la reparación">
                                </div>
                            </div>
                            <div class="text-right mt-3">
                                <button type="button" class="btn btn-success" onclick="agregarReparacion()">
                                    <i class="fas fa-plus"></i>Agregar Reparación
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Lista de reparaciones -->
                    <div class="card">
                        <div class="card-header d-flex justify-content-between">
                            <span><i class="fas fa-list"></i>Reparaciones Registradas</span>
                            <span class="badge badge-warning" id="totalReparaciones">0 reparaciones</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-sm mb-0">
                                    <thead class="thead-light">
                                        <tr>
                                            <th>Tipo</th>
                                            <th>Fecha</th>
                                            <th>Comprobante</th>
                                            <th>Soles</th>
                                            <th>Dólares</th>
                                            <th>Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaReparaciones"></tbody>
                                </table>
                            </div>
                        </div>
                        <div class="card-footer">
                            <div class="row">
                                <div class="col-6">
                                    <strong>Total Soles: <span id="totalReparacionesSoles" class="text-success">S/ 0.00</span></strong>
                                </div>
                                <div class="col-6 text-right">
                                    <strong>Total Dólares: <span id="totalReparacionesDolares" class="text-success">$ 0.00</span></strong>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cerrar</button>
                    <button type="button" class="btn btn-warning" onclick="aplicarReparaciones()">Aplicar y Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <!-- Modal Hospedaje -->
    <div class="modal fade" id="modalHospedaje" tabindex="-1">
        <div class="modal-dialog modal-lg">
            <div class="modal-content">
                <div class="modal-header bg-info text-white">
                    <h5 class="modal-title"><i class="fas fa-bed"></i>Gestión de Hospedaje</h5>
                    <button type="button" class="close text-white" data-dismiss="modal">
                        <span>&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <!-- Formulario agregar hospedaje -->
                    <div class="card mb-3">
                        <div class="card-header"><i class="fas fa-plus"></i>Agregar Hospedaje</div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-6">
                                    <label>Hotel/Lugar:</label>
                                    <input type="text" class="form-control" id="nuevoHospedajeLugar" placeholder="Ej: Hotel Pacifico">
                                </div>
                                <div class="col-md-6">
                                    <label>Fecha:</label>
                                    <input type="date" class="form-control" id="nuevoHospedajeFecha">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-4">
                                    <label>N° Comprobante:</label>
                                    <input type="text" class="form-control" id="nuevoHospedajeComprobante" placeholder="001-123456">
                                </div>
                                <div class="col-md-4">
                                    <label>Monto Soles:</label>
                                    <input type="number" class="form-control" id="nuevoHospedajeSoles" placeholder="0.00" step="0.01">
                                </div>
                                <div class="col-md-4">
                                    <label>Monto Dólares:</label>
                                    <input type="number" class="form-control" id="nuevoHospedajeDolares" placeholder="0.00" step="0.01">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-12">
                                    <label>Observaciones:</label>
                                    <input type="text" class="form-control" id="nuevoHospedajeObservaciones" placeholder="Detalles del hospedaje">
                                </div>
                            </div>
                            <div class="text-right mt-3">
                                <button type="button" class="btn btn-success" onclick="agregarHospedaje()">
                                    <i class="fas fa-plus"></i>Agregar Hospedaje
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Lista de hospedajes -->
                    <div class="card">
                        <div class="card-header d-flex justify-content-between">
                            <span><i class="fas fa-list"></i>Hospedajes Registrados</span>
                            <span class="badge badge-info" id="totalHospedajes">0 hospedajes</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-sm mb-0">
                                    <thead class="thead-light">
                                        <tr>
                                            <th>Lugar</th>
                                            <th>Fecha</th>
                                            <th>Comprobante</th>
                                            <th>Soles</th>
                                            <th>Dólares</th>
                                            <th>Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaHospedajes"></tbody>
                                </table>
                            </div>
                        </div>
                        <div class="card-footer">
                            <div class="row">
                                <div class="col-6">
                                    <strong>Total Soles: <span id="totalHospedajesSoles" class="text-success">S/ 0.00</span></strong>
                                </div>
                                <div class="col-6 text-right">
                                    <strong>Total Dólares: <span id="totalHospedajesDolares" class="text-success">$ 0.00</span></strong>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cerrar</button>
                    <button type="button" class="btn btn-info" onclick="aplicarHospedajes()">Aplicar y Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <!-- Modal Combustible -->
    <div class="modal fade" id="modalCombustible" tabindex="-1">
        <div class="modal-dialog modal-lg">
            <div class="modal-content">
                <div class="modal-header bg-danger text-white">
                    <h5 class="modal-title"><i class="fas fa-gas-pump"></i>Gestión de Combustible</h5>
                    <button type="button" class="close text-white" data-dismiss="modal">
                        <span>&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <!-- Formulario agregar combustible -->
                    <div class="card mb-3">
                        <div class="card-header"><i class="fas fa-plus"></i>Agregar Combustible</div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-6">
                                    <label>Estación/Lugar:</label>
                                    <input type="text" class="form-control" id="nuevoCombustibleLugar" placeholder="Ej: Grifo Primax">
                                </div>
                                <div class="col-md-6">
                                    <label>Fecha:</label>
                                    <input type="date" class="form-control" id="nuevoCombustibleFecha">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-4">
                                    <label>N° Comprobante:</label>
                                    <input type="text" class="form-control" id="nuevoCombustibleComprobante" placeholder="001-123456">
                                </div>
                                <div class="col-md-4">
                                    <label>Monto Soles:</label>
                                    <input type="number" class="form-control" id="nuevoCombustibleSoles" placeholder="0.00" step="0.01">
                                </div>
                                <div class="col-md-4">
                                    <label>Monto Dólares:</label>
                                    <input type="number" class="form-control" id="nuevoCombustibleDolares" placeholder="0.00" step="0.01">
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-12">
                                    <label>Observaciones:</label>
                                    <input type="text" class="form-control" id="nuevoCombustibleObservaciones" placeholder="Tipo de combustible, galones, etc.">
                                </div>
                            </div>
                            <div class="text-right mt-3">
                                <button type="button" class="btn btn-success" onclick="agregarCombustible()">
                                    <i class="fas fa-plus"></i>Agregar Combustible
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Lista de combustibles -->
                    <div class="card">
                        <div class="card-header d-flex justify-content-between">
                            <span><i class="fas fa-list"></i>Combustibles Registrados</span>
                            <span class="badge badge-danger" id="totalCombustibles">0 combustibles</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-sm mb-0">
                                    <thead class="thead-light">
                                        <tr>
                                            <th>Lugar</th>
                                            <th>Fecha</th>
                                            <th>Comprobante</th>
                                            <th>Soles</th>
                                            <th>Dólares</th>
                                            <th>Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaCombustibles"></tbody>
                                </table>
                            </div>
                        </div>
                        <div class="card-footer">
                            <div class="row">
                                <div class="col-6">
                                    <strong>Total Soles: <span id="totalCombustiblesSoles" class="text-success">S/ 0.00</span></strong>
                                </div>
                                <div class="col-6 text-right">
                                    <strong>Total Dólares: <span id="totalCombustiblesDolares" class="text-success">$ 0.00</span></strong>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cerrar</button>
                    <button type="button" class="btn btn-danger" onclick="aplicarCombustibles()">Aplicar y Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <!-- CSS Personalizado -->
    <style>
        .nav-tabs {
            border-bottom: 3px solid #dee2e6;
        }

            .nav-tabs .nav-link {
                color: #495057;
                font-weight: 600;
                border: none;
                border-bottom: 3px solid transparent;
                margin-right: 1rem;
            }

                .nav-tabs .nav-link:hover {
                    border-color: transparent;
                    background-color: #f8f9fa;
                }

                .nav-tabs .nav-link.active {
                    color: #007bff;
                    background-color: transparent;
                    border-color: #007bff;
                }

        .tab-content {
            min-height: 600px;
        }

        .card {
            border: 1px solid #dee2e6;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }

        .card-header {
            border-bottom: 2px solid #dee2e6;
            font-weight: 600;
        }

        .badge-lg {
            font-size: 0.9em;
            padding: 0.5rem 0.75rem;
        }

        .table th {
            background-color: #f8f9fa;
            border-top: 1px solid #dee2e6;
            font-weight: 600;
            font-size: 0.9em;
        }

        .table td {
            vertical-align: middle;
        }

        .form-control-sm {
            font-size: 0.875rem;
        }

        input[readonly], .bg-light {
            background-color: #f8f9fa !important;
            border-color: #ced4da;
        }

        .text-success {
            color: #28a745 !important;
        }

        .text-danger {
            color: #dc3545 !important;
        }

        .text-info {
            color: #17a2b8 !important;
        }

        .text-warning {
            color: #ffc107 !important;
        }

        .btn-lg {
            padding: 0.75rem 1.5rem;
            font-size: 1.1rem;
        }

        .modal-header {
            border-bottom: 2px solid rgba(255,255,255,0.2);
        }

        .collapse {
            transition: all 0.3s ease;
        }

        .fas {
            margin-right: 0.5rem;
        }

        .alert {
            border-radius: 0.5rem;
        }

        @media (max-width: 768px) {
            .tab-content {
                min-height: auto;
            }

            .nav-tabs {
                flex-direction: column;
            }

                .nav-tabs .nav-link {
                    margin-right: 0;
                    margin-bottom: 0.5rem;
                    text-align: center;
                }
        }
    </style>

    <!-- JavaScript -->
    <script src="https://code.jquery.com/jquery-3.6.0.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@4.6.0/dist/js/bootstrap.bundle.min.js"></script>

    <script>
        // Variables globales
        let contadorProductosCarga = 0;
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

        // Inicialización
        $(document).ready(function () {
            console.log('Sistema de Orden de Viaje iniciado');
            detectarOrigenViaje();

            const hoy = new Date().toISOString().split('T')[0];
            $('#nuevoPeajeFecha, #nuevaReparacionFecha, #nuevoHospedajeFecha, #nuevoCombustibleFecha').val(hoy);

            calcularTotales();
            configurarFechasPorDefecto();
        });

        function detectarOrigenViaje() {
            const urlParams = new URLSearchParams(window.location.search);
            const origen = urlParams.get('origen');

            if (origen === 'viajeFinalizado') {
                console.log('Detectado origen desde viaje finalizado');
                $('#hfOrigenViaje').val('viajeFinalizado');
            }
        }

        function configurarFechasPorDefecto() {
            const hoy = new Date();
            const fechaHoy = hoy.toISOString().split('T')[0];

            if (!$('#<%= txtFechaSalida.ClientID %>').val()) {
                $('#<%= txtFechaSalida.ClientID %>').val(fechaHoy);
            }

            if (!$('#<%= txtFechaLlegada.ClientID %>').val()) {
                const manana = new Date(hoy);
                manana.setDate(manana.getDate() + 1);
                $('#<%= txtFechaLlegada.ClientID %>').val(manana.toISOString().split('T')[0]);
            }
        }

        function mostrarDetallesViajeOrigen() {
            $('#pnlDetallesViajeOrigen').collapse('toggle');
        }

        // Función de agregar ingreso
        function agregarIngreso() {
            contadorIngresosAdicionales++;
            const numeroFila = 4 + contadorIngresosAdicionales;

            $('#ingresosAdicionalesBody').append(`
                <tr id="ingresoAdicional_${contadorIngresosAdicionales}">
                    <td>${numeroFila}</td>
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
                    <td>
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
                    <td>${numeroFila}</td>
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
                    <td>
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

        // FUNCIÓN MEJORADA: Calcular totales CON descuentos y reintegros
        function calcularTotales() {
            let totalIngresosSoles = 0;
            let totalIngresosDolares = 0;
            let totalGastosSoles = 0;
            let totalGastosDolares = 0;

            // Sumar ingresos
            $('.ingreso-soles').each(function () {
                const valor = parseFloat($(this).val()) || 0;
                totalIngresosSoles += valor;
            });

            $('.ingreso-dolares').each(function () {
                const valor = parseFloat($(this).val()) || 0;
                totalIngresosDolares += valor;
            });

            // Sumar gastos
            $('.gasto-soles').each(function () {
                const valor = parseFloat($(this).val()) || 0;
                totalGastosSoles += valor;
            });

            $('.gasto-dolares').each(function () {
                const valor = parseFloat($(this).val()) || 0;
                totalGastosDolares += valor;
            });

            // Obtener descuentos y reintegros
            const descuentoSoles = parseFloat($('#descuentoSoles').val()) || 0;
            const descuentoDolares = parseFloat($('#descuentoDolares').val()) || 0;
            const reintegroSoles = parseFloat($('#reintegroSoles').val()) || 0;
            const reintegroDolares = parseFloat($('#reintegroDolares').val()) || 0;

            // Calcular diferencias: Ingresos - Gastos - Descuentos + Reintegros
            const diferenciaSoles = totalIngresosSoles - totalGastosSoles - descuentoSoles + reintegroSoles;
            const diferenciaDolares = totalIngresosDolares - totalGastosDolares - descuentoDolares + reintegroDolares;

            // Actualizar interfaz
            $('#totalIngresosSoles').text(totalIngresosSoles.toFixed(2));
            $('#totalIngresosDolares').text(totalIngresosDolares.toFixed(2));
            $('#totalGastosSoles').text(totalGastosSoles.toFixed(2));
            $('#totalGastosDolares').text(totalGastosDolares.toFixed(2));

            $('#diferenciaSoles').text(diferenciaSoles.toFixed(2));
            $('#diferenciaDolares').text(diferenciaDolares.toFixed(2));

            // Cambiar color según si es positivo o negativo
            $('#diferenciaSoles').css('color', diferenciaSoles >= 0 ? '#28a745' : '#dc3545');
            $('#diferenciaDolares').css('color', diferenciaDolares >= 0 ? '#28a745' : '#dc3545');
        }

        // Modal Peajes
        function abrirModalPeajes() {
            $('#modalPeajes').modal('show');

            // Asegurar que las estaciones estén cargadas
            if (estacionesPeaje.length === 0) {
                cargarEstacionesPeaje();
            } else {
                // Refrescar el datalist por si acaso
                llenarDatalistEstaciones();
            }

            actualizarTablaPeajes();
        }

        function agregarPeaje() {
            const estacion = $('#nuevoPeajeEstacion').val().trim();
            const fecha = $('#nuevoPeajeFecha').val();
            const comprobante = $('#nuevoPeajeComprobante').val().trim();
            const soles = parseFloat($('#nuevoPeajeSoles').val()) || 0;
            const dolares = parseFloat($('#nuevoPeajeDolares').val()) || 0;
            const observaciones = $('#nuevoPeajeObservaciones').val().trim();

            // Validaciones
            if (!estacion) {
                alert('Debe ingresar la estación de peaje');
                $('#nuevoPeajeEstacion').focus();
                return;
            }

            if (!fecha) {
                alert('Debe seleccionar una fecha');
                $('#nuevoPeajeFecha').focus();
                return;
            }

            if (soles <= 0 && dolares <= 0) {
                alert('Debe ingresar al menos un monto');
                $('#nuevoPeajeSoles').focus();
                return;
            }

            // OPCIONAL: Validar si la estación existe (con advertencia, no bloqueo)
            if (!validarEstacionPeaje(estacion)) {
                const confirmar = confirm(
                    `La estación "${estacion}" no está en la lista de estaciones registradas.\n\n` +
                    '¿Desea agregarla de todas formas?'
                );

                if (!confirmar) {
                    $('#nuevoPeajeEstacion').focus();
                    return;
                }
            }

            // Agregar peaje
            peajesData.push({
                id: ++contadorPeajes,
                estacion: estacion,
                fecha: fecha,
                comprobante: comprobante,
                soles: soles,
                dolares: dolares,
                observaciones: observaciones
            });

            // Limpiar campos
            $('#nuevoPeajeEstacion, #nuevoPeajeComprobante, #nuevoPeajeObservaciones').val('');
            $('#nuevoPeajeSoles, #nuevoPeajeDolares').val('');

            actualizarTablaPeajes();
            actualizarTotalesPeajes();
        }

        function eliminarPeaje(id) {
            if (confirm('¿Está seguro de eliminar este peaje?')) {
                peajesData = peajesData.filter(p => p.id !== id);
                actualizarTablaPeajes();
                actualizarTotalesPeajes();
            }
        }

        function actualizarTablaPeajes() {
            const tbody = $('#tablaPeajes');
            tbody.empty();

            peajesData.forEach(peaje => {
                tbody.append(`
                    <tr>
                        <td>${peaje.estacion}</td>
                        <td>${peaje.fecha}</td>
                        <td>${peaje.comprobante || 'N/A'}</td>
                        <td>S/ ${peaje.soles.toFixed(2)}</td>
                        <td>$ ${peaje.dolares.toFixed(2)}</td>
                        <td>
                            <button type="button" class="btn btn-danger btn-sm" onclick="eliminarPeaje(${peaje.id})">
                                <i class="fas fa-trash"></i>
                            </button>
                        </td>
                    </tr>
                `);
            });

            $('#totalPeajes').text(`${peajesData.length} peajes`);
        }

        function actualizarTotalesPeajes() {
            const totalSoles = peajesData.reduce((sum, p) => sum + p.soles, 0);
            const totalDolares = peajesData.reduce((sum, p) => sum + p.dolares, 0);

            $('#totalPeajesSoles').text(`S/ ${totalSoles.toFixed(2)}`);
            $('#totalPeajesDolares').text(`$ ${totalDolares.toFixed(2)}`);

            $('#peajesSoles').val(totalSoles > 0 ? totalSoles.toFixed(2) : '');
            $('#peajesDolares').val(totalDolares > 0 ? totalDolares.toFixed(2) : '');
            $('#descPeajes').val(peajesData.length > 0 ? `${peajesData.length} peajes registrados` : '');
            $('#contadorPeajes').text(peajesData.length);

            calcularTotales();
        }

        function aplicarPeajes() {
            $('#modalPeajes').modal('hide');
        }

        // Modal Reparaciones
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

            if (!tipo) {
                alert('Debe ingresar el tipo de reparación');
                return;
            }
            if (!fecha) {
                alert('Debe seleccionar una fecha');
                return;
            }
            if (soles <= 0 && dolares <= 0) {
                alert('Debe ingresar al menos un monto');
                return;
            }

            reparacionesData.push({
                id: ++contadorReparaciones,
                tipo: tipo,
                fecha: fecha,
                comprobante: comprobante,
                soles: soles,
                dolares: dolares,
                observaciones: observaciones
            });

            $('#nuevaReparacionTipo, #nuevaReparacionComprobante, #nuevaReparacionObservaciones').val('');
            $('#nuevaReparacionSoles, #nuevaReparacionDolares').val('');

            actualizarTablaReparaciones();
            actualizarTotalesReparaciones();
        }

        function eliminarReparacion(id) {
            if (confirm('¿Está seguro de eliminar esta reparación?')) {
                reparacionesData = reparacionesData.filter(r => r.id !== id);
                actualizarTablaReparaciones();
                actualizarTotalesReparaciones();
            }
        }

        function actualizarTablaReparaciones() {
            const tbody = $('#tablaReparaciones');
            tbody.empty();

            reparacionesData.forEach(reparacion => {
                tbody.append(`
                    <tr>
                        <td>${reparacion.tipo}</td>
                        <td>${reparacion.fecha}</td>
                        <td>${reparacion.comprobante || 'N/A'}</td>
                        <td>S/ ${reparacion.soles.toFixed(2)}</td>
                        <td>$ ${reparacion.dolares.toFixed(2)}</td>
                        <td>
                            <button type="button" class="btn btn-danger btn-sm" onclick="eliminarReparacion(${reparacion.id})">
                                <i class="fas fa-trash"></i>
                            </button>
                        </td>
                    </tr>
                `);
            });

            $('#totalReparaciones').text(`${reparacionesData.length} reparaciones`);
        }


        // Variable global para almacenar las estaciones
        let estacionesPeaje = [];

        /**
         * Carga las estaciones de peaje desde el servidor al iniciar la página
         */
        function cargarEstacionesPeaje() {
            try {
                // Obtener JSON desde el método del .CS
                const estacionesJson = '<%= ObtenerEstacionesPeajeJSON() %>';

                if (!estacionesJson || estacionesJson === '[]') {
                    console.warn('⚠️ No se obtuvieron estaciones del servidor, usando fallback');
                    usarEstacionesFallback();
                    return;
                }

                estacionesPeaje = JSON.parse(estacionesJson);

                console.log(`✅ ${estacionesPeaje.length} estaciones de peaje cargadas desde BD`);

                // Llenar el datalist
                llenarDatalistEstaciones();

            } catch (error) {
                console.error('❌ Error cargando estaciones de peaje:', error);
                usarEstacionesFallback();
            }
        }

        /**
         * Llena el datalist con las estaciones disponibles para autocompletado
         */
        function llenarDatalistEstaciones() {
            const datalist = $('#listaEstaciones');

            // Limpiar opciones existentes
            datalist.empty();

            // Verificar que hay estaciones para cargar
            if (!estacionesPeaje || estacionesPeaje.length === 0) {
                console.warn('⚠️ No hay estaciones para llenar el datalist');
                return;
            }

            // Agregar cada estación como opción
            estacionesPeaje.forEach(estacion => {
                datalist.append(`<option value="${estacion.nombre}">`);
            });

            console.log(`✅ Datalist llenado con ${estacionesPeaje.length} estaciones`);
        }

        /**
 * Valida si una estación existe en la lista
 */
        function validarEstacionPeaje(nombreEstacion) {
            if (!nombreEstacion || nombreEstacion.trim() === '') {
                return false;
            }

            // Buscar coincidencia exacta o parcial
            const estacionEncontrada = estacionesPeaje.some(estacion =>
                estacion.nombre.toLowerCase() === nombreEstacion.toLowerCase().trim()
            );

            return estacionEncontrada;
        }

        /**
         * Obtiene sugerencias de estaciones basadas en texto ingresado
         */
        function obtenerSugerenciasEstacion(texto) {
            if (!texto || texto.length < 2) {
                return [];
            }

            const textoLower = texto.toLowerCase();

            return estacionesPeaje.filter(estacion =>
                estacion.nombre.toLowerCase().includes(textoLower)
            ).map(estacion => estacion.nombre);
        }







        function actualizarTotalesReparaciones() {
            const totalSoles = reparacionesData.reduce((sum, r) => sum + r.soles, 0);
            const totalDolares = reparacionesData.reduce((sum, r) => sum + r.dolares, 0);

            $('#totalReparacionesSoles').text(`S/ ${totalSoles.toFixed(2)}`);
            $('#totalReparacionesDolares').text(`$ ${totalDolares.toFixed(2)}`);

            $('#reparacionesSoles').val(totalSoles > 0 ? totalSoles.toFixed(2) : '');
            $('#reparacionesDolares').val(totalDolares > 0 ? totalDolares.toFixed(2) : '');
            $('#descReparaciones').val(reparacionesData.length > 0 ? `${reparacionesData.length} reparaciones registradas` : '');
            $('#contadorReparaciones').text(reparacionesData.length);

            calcularTotales();
        }

        function aplicarReparaciones() {
            $('#modalReparaciones').modal('hide');
        }

        // Modal Hospedaje
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

            if (!lugar) {
                alert('Debe ingresar el lugar de hospedaje');
                return;
            }
            if (!fecha) {
                alert('Debe seleccionar una fecha');
                return;
            }
            if (soles <= 0 && dolares <= 0) {
                alert('Debe ingresar al menos un monto');
                return;
            }

            hospedajesData.push({
                id: ++contadorHospedajes,
                lugar: lugar,
                fecha: fecha,
                comprobante: comprobante,
                soles: soles,
                dolares: dolares,
                observaciones: observaciones
            });

            $('#nuevoHospedajeLugar, #nuevoHospedajeComprobante, #nuevoHospedajeObservaciones').val('');
            $('#nuevoHospedajeSoles, #nuevoHospedajeDolares').val('');

            actualizarTablaHospedajes();
            actualizarTotalesHospedajes();
        }

        function eliminarHospedaje(id) {
            if (confirm('¿Está seguro de eliminar este hospedaje?')) {
                hospedajesData = hospedajesData.filter(h => h.id !== id);
                actualizarTablaHospedajes();
                actualizarTotalesHospedajes();
            }
        }

        function actualizarTablaHospedajes() {
            const tbody = $('#tablaHospedajes');
            tbody.empty();

            hospedajesData.forEach(hospedaje => {
                tbody.append(`
                    <tr>
                        <td>${hospedaje.lugar}</td>
                        <td>${hospedaje.fecha}</td>
                        <td>${hospedaje.comprobante || 'N/A'}</td>
                        <td>S/ ${hospedaje.soles.toFixed(2)}</td>
                        <td>$ ${hospedaje.dolares.toFixed(2)}</td>
                        <td>
                            <button type="button" class="btn btn-danger btn-sm" onclick="eliminarHospedaje(${hospedaje.id})">
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
            $('#descHospedaje').val(hospedajesData.length > 0 ? `${hospedajesData.length} hospedajes registrados` : '');
            $('#contadorHospedaje').text(hospedajesData.length);

            calcularTotales();
        }

        function aplicarHospedajes() {
            $('#modalHospedaje').modal('hide');
        }

        // Modal Combustible
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

            if (!lugar) {
                alert('Debe ingresar el lugar de combustible');
                return;
            }
            if (!fecha) {
                alert('Debe seleccionar una fecha');
                return;
            }
            if (soles <= 0 && dolares <= 0) {
                alert('Debe ingresar al menos un monto');
                return;
            }

            combustiblesData.push({
                id: ++contadorCombustibles,
                lugar: lugar,
                fecha: fecha,
                comprobante: comprobante,
                soles: soles,
                dolares: dolares,
                observaciones: observaciones
            });

            $('#nuevoCombustibleLugar, #nuevoCombustibleComprobante, #nuevoCombustibleObservaciones').val('');
            $('#nuevoCombustibleSoles, #nuevoCombustibleDolares').val('');

            actualizarTablaCombustibles();
            actualizarTotalesCombustibles();
        }

        function eliminarCombustible(id) {
            if (confirm('¿Está seguro de eliminar este combustible?')) {
                combustiblesData = combustiblesData.filter(c => c.id !== id);
                actualizarTablaCombustibles();
                actualizarTotalesCombustibles();
            }
        }

        function actualizarTablaCombustibles() {
            const tbody = $('#tablaCombustibles');
            tbody.empty();

            combustiblesData.forEach(combustible => {
                tbody.append(`
                    <tr>
                        <td>${combustible.lugar}</td>
                        <td>${combustible.fecha}</td>
                        <td>${combustible.comprobante || 'N/A'}</td>
                        <td>S/ ${combustible.soles.toFixed(2)}</td>
                        <td>$ ${combustible.dolares.toFixed(2)}</td>
                        <td>
                            <button type="button" class="btn btn-danger btn-sm" onclick="eliminarCombustible(${combustible.id})">
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
            $('#descCombustible').val(combustiblesData.length > 0 ? `${combustiblesData.length} combustibles registrados` : '');
            $('#contadorCombustible').text(combustiblesData.length);

            calcularTotales();
        }

        function aplicarCombustibles() {
            $('#modalCombustible').modal('hide');
        }

        // Preparar datos antes del envío
        function prepararDatosFinancieros() {
            const gastosFinancieros = [
                ...peajesData.map(p => ({ categoria: 'Peajes', ...p })),
                ...reparacionesData.map(r => ({ categoria: 'Reparaciones', ...r })),
                ...hospedajesData.map(h => ({ categoria: 'Hospedaje', ...h })),
                ...combustiblesData.map(c => ({ categoria: 'Combustible', ...c }))
            ];

            $('#hfGastosFinancieros').val(JSON.stringify(gastosFinancieros));
        }

        // Ejecutar antes de enviar el formulario
        $('form').on('submit', function () {
            prepararDatosFinancieros();
        });

    </script>
</asp:Content>

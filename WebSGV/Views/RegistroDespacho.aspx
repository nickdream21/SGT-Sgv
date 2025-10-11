<%@ Page Title="Registro de Despachos Unificado" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroDespacho.aspx.cs" Inherits="WebSGV.Views.RegistroDespacho" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="container-fluid">
        <div class="row">
            <div class="col-md-12">
                <div class="card">
                    <div class="card-header bg-primary text-white">
                        <h4 class="mb-0">
                            <i class="fas fa-truck"></i> Registro de Despachos Unificado
                            <asp:Label ID="lblEstadoLote" runat="server" CssClass="badge bg-success ms-3" Visible="false"></asp:Label>
                        </h4>
                    </div>
                    <div class="card-body">
                        <asp:UpdatePanel ID="UpdatePanelMain" runat="server" UpdateMode="Conditional">
                            <ContentTemplate>
                                
                                <!-- Mensajes -->
                                <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-3">
                                    <asp:Label ID="lblMensaje" runat="server" CssClass="alert"></asp:Label>
                                </asp:Panel>

                                <!-- ====== FASE 1: CONFIGURACIÓN BASE DEL LOTE ====== -->
                                <asp:Panel ID="pnlConfiguracionBase" runat="server">
                                    <div class="card mb-4" style="border-left: 4px solid #007bff;">
                                        <div class="card-header bg-primary text-white">
                                            <h5 class="mb-0">
                                                <i class="fas fa-cogs"></i> PASO 1: Configuración Base del Lote
                                            </h5>
                                            <small>Complete los datos comunes que aplicarán a todos los despachos de este lote</small>
                                        </div>
                                        <div class="card-body">
                                            <div class="row">
                                                <!-- Columna Izquierda -->
                                                <div class="col-md-6">
                                                    
                                                    <!-- Fecha de Despacho -->
                                                    <div class="form-group mb-3">
                                                        <label for="txtFechaDespachoBase" class="form-label">
                                                            <strong>Fecha de Programación:</strong>
                                                            <span class="text-danger">*</span>
                                                        </label>
                                                        <asp:TextBox ID="txtFechaDespachoBase" runat="server" 
                                                            CssClass="form-control" 
                                                            TextMode="Date">
                                                        </asp:TextBox>
                                                        <asp:RequiredFieldValidator ID="rfvFechaDespachoBase" runat="server"
                                                            ControlToValidate="txtFechaDespachoBase"
                                                            ErrorMessage="Debe seleccionar una fecha de programación"
                                                            CssClass="text-danger small"
                                                            Display="Dynamic"
                                                            ValidationGroup="ConfiguracionBase">
                                                        </asp:RequiredFieldValidator>
                                                    </div>

                                                    <!-- Cliente -->
                                                    <div class="form-group mb-3">
                                                        <label for="ddlClienteBase" class="form-label">
                                                            <strong>Cliente:</strong>
                                                            <span class="text-danger">*</span>
                                                        </label>
                                                        <asp:DropDownList ID="ddlClienteBase" runat="server" 
                                                            CssClass="form-select"
                                                            DataTextField="nombre"
                                                            DataValueField="idCliente"
                                                            AppendDataBoundItems="true">
                                                            <asp:ListItem Value="0" Text="-- Seleccione un cliente --"></asp:ListItem>
                                                        </asp:DropDownList>
                                                        <asp:RequiredFieldValidator ID="rfvClienteBase" runat="server"
                                                            ControlToValidate="ddlClienteBase"
                                                            InitialValue="0"
                                                            ErrorMessage="Debe seleccionar un cliente"
                                                            CssClass="text-danger small"
                                                            Display="Dynamic"
                                                            ValidationGroup="ConfiguracionBase">
                                                        </asp:RequiredFieldValidator>
                                                    </div>

                                                    <!-- Número de Pedido - Solo para Carga Nacional -->
                                                    <asp:Panel ID="pnlNumeroPedido" runat="server" Visible="false">
                                                        <div class="form-group mb-3">
                                                            <label for="txtNumeroPedidoBase" class="form-label">
                                                                <strong>N° de Pedido:</strong>
                                                            </label>
                                                            <asp:TextBox ID="txtNumeroPedidoBase" runat="server" 
                                                                CssClass="form-control" 
                                                                placeholder="Ej: 1234567890"
                                                                MaxLength="10">
                                                            </asp:TextBox>
                                                            <small class="form-text text-muted">Debe tener exactamente 10 dígitos numéricos</small>
                                                        </div>
                                                    </asp:Panel>
                                                </div>

                                                <!-- Columna Derecha -->
                                                <div class="col-md-6">
                                                    
                                                    <!-- Tipo de Operación -->
                                                    <div class="form-group mb-3">
                                                        <label for="ddlTipoOperacionBase" class="form-label">
                                                            <strong>Tipo de Operación:</strong>
                                                            <span class="text-danger">*</span>
                                                        </label>
                                                        <asp:DropDownList ID="ddlTipoOperacionBase" runat="server" 
                                                            CssClass="form-select"
                                                            AutoPostBack="true"
                                                            OnSelectedIndexChanged="ddlTipoOperacionBase_SelectedIndexChanged">
                                                            <asp:ListItem Value="" Text="-- Seleccione tipo --"></asp:ListItem>
                                                            <asp:ListItem Value="CARGA" Text="Carga"></asp:ListItem>
                                                            <asp:ListItem Value="DESCARGA" Text="Descarga"></asp:ListItem>
                                                        </asp:DropDownList>
                                                        <asp:RequiredFieldValidator ID="rfvTipoOperacionBase" runat="server"
                                                            ControlToValidate="ddlTipoOperacionBase"
                                                            InitialValue=""
                                                            ErrorMessage="Debe seleccionar un tipo de operación"
                                                            CssClass="text-danger small"
                                                            Display="Dynamic"
                                                            ValidationGroup="ConfiguracionBase">
                                                        </asp:RequiredFieldValidator>
                                                    </div>

                                                    <!-- Es Internacional -->
                                                    <div class="form-group mb-3">
                                                        <label class="form-label">
                                                            <strong>Ámbito:</strong>
                                                            <span class="text-danger">*</span>
                                                        </label>
                                                        <div>
                                                            <asp:RadioButtonList ID="rblAmbitoOperacionBase" runat="server" 
                                                                CssClass="form-check" 
                                                                RepeatDirection="Horizontal"
                                                                AutoPostBack="true"
                                                                OnSelectedIndexChanged="rblAmbitoOperacionBase_SelectedIndexChanged">
                                                                <asp:ListItem Value="0" Text="Nacional" Selected="True"></asp:ListItem>
                                                                <asp:ListItem Value="1" Text="Internacional"></asp:ListItem>
                                                            </asp:RadioButtonList>
                                                        </div>
                                                    </div>

                                                    <!-- Lugar de Operación -->
                                                    <div class="form-group mb-3">
                                                        <label for="ddlLugarOperacionBase" class="form-label">
                                                            <strong>Planta de Operación:</strong>
                                                            <span class="text-danger">*</span>
                                                        </label>
                                                        <asp:DropDownList ID="ddlLugarOperacionBase" runat="server" CssClass="form-select">
                                                            <asp:ListItem Value="" Text="-- Seleccione planta --"></asp:ListItem>
                                                            <asp:ListItem Value="Lima" Text="Lima"></asp:ListItem>
                                                            <asp:ListItem Value="Guayaquil" Text="Guayaquil"></asp:ListItem>
                                                            <asp:ListItem Value="Trujillo" Text="Trujillo"></asp:ListItem>
                                                            <asp:ListItem Value="Quito" Text="Quito"></asp:ListItem>
                                                            <asp:ListItem Value="Chiclayo" Text="Chiclayo"></asp:ListItem>
                                                            <asp:ListItem Value="Manta" Text="Manta"></asp:ListItem>
                                                        </asp:DropDownList>
                                                        <asp:RequiredFieldValidator ID="rfvLugarOperacionBase" runat="server"
                                                            ControlToValidate="ddlLugarOperacionBase"
                                                            InitialValue=""
                                                            ErrorMessage="Debe seleccionar una planta"
                                                            CssClass="text-danger small"
                                                            Display="Dynamic"
                                                            ValidationGroup="ConfiguracionBase">
                                                        </asp:RequiredFieldValidator>
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- DOCUMENTACIÓN BASE -->
                                            <div class="row mt-4">
                                                <div class="col-12">
                                                    <h6 class="text-primary mb-3">
                                                        <i class="fas fa-file-alt"></i> Documentación Base del Lote
                                                    </h6>
                                                    
                                                    <!-- Panel de Ayuda de Documentos -->
                                                    <asp:Panel ID="pnlAyudaDocumentosBase" runat="server" CssClass="alert alert-info mb-3">
                                                        <asp:Label ID="lblAyudaDocumentosBase" runat="server" Text="Seleccione el tipo de operación y ámbito para ver los documentos requeridos."></asp:Label>
                                                    </asp:Panel>

                                                    <div class="row">
                                                        <!-- FACTURA BASE -->
                                                        <div class="col-md-6">
                                                            <asp:Panel ID="pnlFacturaBase" runat="server" CssClass="border p-3 mb-3" Visible="false">
                                                                <h6 class="text-primary"><i class="fas fa-receipt"></i> Datos de Factura</h6>
                                                                
                                                                <div class="form-group mb-2">
                                                                    <label for="txtNumeroFacturaBase" class="form-label">
                                                                        <strong>N° Factura:</strong>
                                                                        <span class="text-danger">*</span>
                                                                    </label>
                                                                    <asp:TextBox ID="txtNumeroFacturaBase" runat="server" 
                                                                        CssClass="form-control" 
                                                                        placeholder="Ej: F222 - 00004267">
                                                                    </asp:TextBox>
                                                                </div>

                                                                <div class="form-group mb-2">
                                                                    <label for="txtFechaEmisionFacturaBase" class="form-label">
                                                                        <strong>Fecha Emisión:</strong>
                                                                        <span class="text-danger">*</span>
                                                                    </label>
                                                                    <asp:TextBox ID="txtFechaEmisionFacturaBase" runat="server" 
                                                                        CssClass="form-control" 
                                                                        TextMode="Date">
                                                                    </asp:TextBox>
                                                                </div>

                                                                <div class="form-group mb-2">
                                                                    <label for="txtValorTotalFacturaBase" class="form-label">
                                                                        <strong>Valor Total:</strong>
                                                                        <span class="text-danger">*</span>
                                                                    </label>
                                                                    <asp:TextBox ID="txtValorTotalFacturaBase" runat="server" 
                                                                        CssClass="form-control" 
                                                                        placeholder="0.00"
                                                                        TextMode="Number"
                                                                        step="0.01">
                                                                    </asp:TextBox>
                                                                </div>
                                                            </asp:Panel>
                                                        </div>

                                                        <!-- CPIC BASE -->
                                                        <div class="col-md-6">
                                                            <asp:Panel ID="pnlCPICBase" runat="server" CssClass="border p-3 mb-3" Visible="false">
                                                                <h6 class="text-success"><i class="fas fa-shipping-fast"></i> Datos de CPIC</h6>
                                                                
                                                                <div class="form-group mb-2">
                                                                    <label for="txtNumeroCPICBase" class="form-label">
                                                                        <strong>N° CPIC:</strong>
                                                                        <span class="text-danger">*</span>
                                                                    </label>
                                                                    <asp:TextBox ID="txtNumeroCPICBase" runat="server" 
                                                                        CssClass="form-control" 
                                                                        placeholder="Ej: 1234567"
                                                                        MaxLength="7">
                                                                    </asp:TextBox>
                                                                </div>

                                                                <div class="form-group mb-2">
                                                                    <label for="txtFechaEmisionCPICBase" class="form-label">
                                                                        <strong>Fecha Emisión CPIC:</strong>
                                                                        <span class="text-danger">*</span>
                                                                    </label>
                                                                    <asp:TextBox ID="txtFechaEmisionCPICBase" runat="server" 
                                                                        CssClass="form-control" 
                                                                        TextMode="Date">
                                                                    </asp:TextBox>
                                                                </div>

                                                                <div class="form-group mb-2">
                                                                    <label for="txtValorFleteBase" class="form-label">
                                                                        <strong>Valor Flete:</strong>
                                                                        <span class="text-danger">*</span>
                                                                    </label>
                                                                    <asp:TextBox ID="txtValorFleteBase" runat="server" 
                                                                        CssClass="form-control" 
                                                                        placeholder="0.00"
                                                                        TextMode="Number"
                                                                        step="0.01">
                                                                    </asp:TextBox>
                                                                </div>
                                                            </asp:Panel>
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- Botón para iniciar lote -->
                                            <div class="row">
                                                <div class="col-md-12">
                                                    <div class="d-flex justify-content-end gap-2">
                                                        <asp:Button ID="btnIniciarLote" runat="server" 
                                                            Text="Iniciar Lote de Despachos" 
                                                            CssClass="btn btn-primary btn-lg"
                                                            OnClick="btnIniciarLote_Click"
                                                            ValidationGroup="ConfiguracionBase" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                                <!-- ====== RESUMEN DEL LOTE ACTIVO ====== -->
                                <asp:Panel ID="pnlResumenLote" runat="server" Visible="false">
                                    <div class="card mb-4" style="border-left: 4px solid #28a745;">
                                        <div class="card-header bg-success text-white">
                                            <h5 class="mb-0">
                                                <i class="fas fa-clipboard-list"></i> Lote Activo - Resumen de Configuración
                                            </h5>
                                        </div>
                                        <div class="card-body bg-light">
                                            <div class="row">
                                                <div class="col-md-4">
                                                    <p class="mb-1"><strong>Fecha:</strong> <asp:Label ID="lblResumenFecha" runat="server"></asp:Label></p>
                                                    <p class="mb-1"><strong>Cliente:</strong> <asp:Label ID="lblResumenCliente" runat="server"></asp:Label></p>
                                                    <p class="mb-1"><strong>Pedido:</strong> <asp:Label ID="lblResumenPedido" runat="server"></asp:Label></p>
                                                </div>
                                                <div class="col-md-4">
                                                    <p class="mb-1"><strong>Operación:</strong> <asp:Label ID="lblResumenOperacion" runat="server"></asp:Label></p>
                                                    <p class="mb-1"><strong>Ámbito:</strong> <asp:Label ID="lblResumenAmbito" runat="server"></asp:Label></p>
                                                    <p class="mb-1"><strong>Planta:</strong> <asp:Label ID="lblResumenPlanta" runat="server"></asp:Label></p>
                                                </div>
                                                <div class="col-md-4">
                                                    <p class="mb-1"><strong>Conductores Agregados:</strong> <asp:Label ID="lblResumenCantidad" runat="server" CssClass="badge bg-primary fs-6">0</asp:Label></p>
                                                    <div class="mt-2">
                                                        <asp:Button ID="btnCancelarLote" runat="server" 
                                                            Text="Cancelar Lote" 
                                                            CssClass="btn btn-outline-danger btn-sm me-2"
                                                            OnClick="btnCancelarLote_Click"
                                                            CausesValidation="false"
                                                            OnClientClick="return confirm('¿Está seguro de cancelar este lote? Se perderán todos los datos.');" />
                                                        
                                                        <asp:Button ID="btnFinalizarLote" runat="server" 
                                                            Text="Finalizar Lote" 
                                                            CssClass="btn btn-success btn-sm"
                                                            OnClick="btnFinalizarLote_Click"
                                                            CausesValidation="false"
                                                            OnClientClick="return confirm('¿Finalizar lote y guardar todos los despachos?');" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                                <!-- ====== FASE 2: ADICIÓN DE CONDUCTORES ====== -->
                                <asp:Panel ID="pnlAdicionConductores" runat="server" Visible="false">
                                    <div class="card mb-4" style="border-left: 4px solid #ffc107;">
                                        <div class="card-header bg-warning text-dark">
                                            <h5 class="mb-0">
                                                <i class="fas fa-user-plus"></i> PASO 2: Agregar Conductores al Lote
                                            </h5>
                                            <small>Complete los datos específicos para cada conductor</small>
                                        </div>
                                        <div class="card-body">

                                            <!-- INFORMACIÓN DE VIAJES EN PROGRESO -->
                                            <asp:Panel ID="pnlViajesProgreso" runat="server" CssClass="card mb-4" style="border-left: 4px solid #6f42c1;" Visible="false">
                                                <div class="card-header" style="background: linear-gradient(90deg, #6f42c1, #8e44ad); color: white;">
                                                    <h6 class="mb-0">
                                                        <i class="fas fa-route"></i> Viajes en Progreso - <span id="spanNombreConductor" runat="server"></span>
                                                    </h6>
                                                </div>
                                                <div class="card-body">
                                                    <div class="row">
                                                        <div class="col-md-8">
                                                            <asp:Panel ID="pnlSinViajes" runat="server" Visible="true">
                                                                <div class="alert alert-info mb-0">
                                                                    <i class="fas fa-info-circle"></i> 
                                                                    <strong>Sin viajes activos:</strong> Se creará automáticamente un nuevo viaje en progreso para este conductor.
                                                                </div>
                                                            </asp:Panel>
                                                            
                                                            <asp:Panel ID="pnlConViajes" runat="server" Visible="false">
                                                                <label class="form-label">
                                                                    <strong>Viaje activo encontrado:</strong>
                                                                </label>
                                                                <div class="alert alert-success">
                                                                    <div class="row align-items-center">
                                                                        <div class="col-md-10">
                                                                            <strong><i class="fas fa-clipboard-list"></i> <asp:Label ID="lblNumeroViaje" runat="server"></asp:Label></strong><br>
                                                                            <small class="text-muted">
                                                                                Iniciado: <asp:Label ID="lblFechaInicio" runat="server"></asp:Label> | 
                                                                                Despachos: <asp:Label ID="lblCantidadDespachos" runat="server"></asp:Label> | 
                                                                                Tipo: <asp:Label ID="lblTipoViaje" runat="server"></asp:Label>
                                                                            </small>
                                                                        </div>
                                                                        <div class="col-md-2 text-end">
                                                                            <asp:Button ID="btnFinalizarViajeUnico" runat="server" 
                                                                                Text="Finalizar" 
                                                                                CssClass="btn btn-outline-danger btn-sm"
                                                                                OnClick="btnFinalizarViajeUnico_Click"
                                                                                OnClientClick="return confirm('¿Está seguro de finalizar este viaje? No podrá agregar más despachos.');"
                                                                                CausesValidation="false" />
                                                                        </div>
                                                                    </div>
                                                                </div>
                                                            </asp:Panel>
                                                            
                                                            <asp:Panel ID="pnlMultiplesViajes" runat="server" Visible="false">
                                                                <div class="alert alert-warning">
                                                                    <i class="fas fa-exclamation-triangle"></i> 
                                                                    <strong>Múltiples viajes activos:</strong> Seleccione en cuál desea agregar este despacho.
                                                                </div>
                                                                <div class="row align-items-end">
                                                                    <div class="col-md-8">
                                                                        <asp:DropDownList ID="ddlViajesActivos" runat="server" 
                                                                            CssClass="form-select"
                                                                            DataTextField="Display"
                                                                            DataValueField="IdViajeProgreso">
                                                                            <asp:ListItem Value="0" Text="-- Seleccione un viaje --"></asp:ListItem>
                                                                        </asp:DropDownList>
                                                                    </div>
                                                                    <div class="col-md-4">
                                                                        <asp:Button ID="btnCrearNuevoViaje" runat="server" 
                                                                            Text="Crear Nuevo Viaje" 
                                                                            CssClass="btn btn-outline-success btn-sm w-100"
                                                                            OnClick="btnCrearNuevoViaje_Click"
                                                                            CausesValidation="false" />
                                                                    </div>
                                                                </div>
                                                            </asp:Panel>
                                                        </div>
                                                        <div class="col-md-4">
                                                            <div class="text-center">
                                                                <asp:Button ID="btnVerHistorialViajes" runat="server" 
                                                                    Text="Ver Historial" 
                                                                    CssClass="btn btn-outline-info w-100"
                                                                    OnClick="btnVerHistorialViajes_Click"
                                                                    CausesValidation="false" />
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>
                                            </asp:Panel>

                                            <div class="row">
                                                <!-- Columna Izquierda -->
                                                <div class="col-md-6">
                                                    
                                                    <!-- Conductor -->
                                                    <div class="form-group mb-3">
                                                        <label for="ddlConductor" class="form-label">
                                                            <strong>Conductor:</strong>
                                                            <span class="text-danger">*</span>
                                                        </label>
                                                        <asp:DropDownList ID="ddlConductor" runat="server" 
                                                            CssClass="form-select select2-searchable"
                                                            DataTextField="NombreCompleto"
                                                            DataValueField="idConductor"
                                                            AppendDataBoundItems="true"
                                                            AutoPostBack="true"
                                                            OnSelectedIndexChanged="ddlConductor_SelectedIndexChanged">
                                                            <asp:ListItem Value="0" Text="-- Seleccione un conductor --"></asp:ListItem>
                                                        </asp:DropDownList>
                                                        <asp:RequiredFieldValidator ID="rfvConductor" runat="server"
                                                            ControlToValidate="ddlConductor"
                                                            InitialValue="0"
                                                            ErrorMessage="Debe seleccionar un conductor"
                                                            CssClass="text-danger small"
                                                            Display="Dynamic"
                                                            ValidationGroup="AgregarConductor">
                                                        </asp:RequiredFieldValidator>
                                                    </div>

                                                    <!-- Placa Tracto -->
                                                    <div class="form-group mb-3">
                                                        <label for="ddlPlacaTracto" class="form-label">
                                                            <strong>Placa Tracto:</strong>
                                                            <span class="text-danger">*</span>
                                                        </label>
                                                        <asp:DropDownList ID="ddlPlacaTracto" runat="server" 
                                                            CssClass="form-select select2-searchable"
                                                            DataTextField="placaTracto"
                                                            DataValueField="idTracto"
                                                            AppendDataBoundItems="true">
                                                            <asp:ListItem Value="0" Text="-- Seleccione una placa --"></asp:ListItem>
                                                        </asp:DropDownList>
                                                        <asp:RequiredFieldValidator ID="rfvPlacaTracto" runat="server"
                                                            ControlToValidate="ddlPlacaTracto"
                                                            InitialValue="0"
                                                            ErrorMessage="Debe seleccionar una placa de tracto"
                                                            CssClass="text-danger small"
                                                            Display="Dynamic"
                                                            ValidationGroup="AgregarConductor">
                                                        </asp:RequiredFieldValidator>
                                                    </div>

                                                    <!-- Placa Carreta -->
                                                    <div class="form-group mb-3">
                                                        <label for="ddlPlacaCarreta" class="form-label">
                                                            <strong>Placa Carreta:</strong>
                                                            <span class="text-danger">*</span>
                                                        </label>
                                                        <asp:DropDownList ID="ddlPlacaCarreta" runat="server" 
                                                            CssClass="form-select select2-searchable"
                                                            DataTextField="placaCarreta"
                                                            DataValueField="idCarreta"
                                                            AppendDataBoundItems="true">
                                                            <asp:ListItem Value="0" Text="-- Seleccione una placa --"></asp:ListItem>
                                                        </asp:DropDownList>
                                                        <asp:RequiredFieldValidator ID="rfvPlacaCarreta" runat="server"
                                                            ControlToValidate="ddlPlacaCarreta"
                                                            InitialValue="0"
                                                            ErrorMessage="Debe seleccionar una placa de carreta"
                                                            CssClass="text-danger small"
                                                            Display="Dynamic"
                                                            ValidationGroup="AgregarConductor">
                                                        </asp:RequiredFieldValidator>
                                                    </div>
                                                </div>

                                                <!-- Columna Derecha - Guías Específicas -->
                                                <div class="col-md-6">
                                                    <h6 class="text-secondary mb-3">
                                                        <i class="fas fa-file-alt"></i> Documentos Específicos del Conductor
                                                    </h6>

                                                    <!-- Guía Remitente -->
                                                    <asp:Panel ID="pnlGuiaRemitenteConductor" runat="server" Visible="false">
                                                        <div class="form-group mb-3">
                                                            <label for="txtGuiaRemitente" class="form-label">
                                                                <strong>Guía Remitente:</strong>
                                                                <span class="text-danger">*</span>
                                                            </label>
                                                            <asp:TextBox ID="txtGuiaRemitente" runat="server" 
                                                                CssClass="form-control" 
                                                                placeholder="Número de guía remitente">
                                                            </asp:TextBox>
                                                            <asp:RequiredFieldValidator ID="rfvGuiaRemitente" runat="server"
                                                                ControlToValidate="txtGuiaRemitente"
                                                                ErrorMessage="Guía remitente requerida"
                                                                CssClass="text-danger small"
                                                                Display="Dynamic"
                                                                ValidationGroup="AgregarConductor">
                                                            </asp:RequiredFieldValidator>
                                                        </div>
                                                    </asp:Panel>

                                                    <!-- Guía Transportista -->
                                                    <asp:Panel ID="pnlGuiaTransportistaConductor" runat="server" Visible="false">
                                                        <div class="form-group mb-3">
                                                            <label for="txtGuiaTransportista" class="form-label">
                                                                <strong>Guía Transportista:</strong>
                                                                <span class="text-danger">*</span>
                                                            </label>
                                                            <asp:TextBox ID="txtGuiaTransportista" runat="server" 
                                                                CssClass="form-control" 
                                                                placeholder="Número de guía transportista">
                                                            </asp:TextBox>
                                                            <asp:RequiredFieldValidator ID="rfvGuiaTransportista" runat="server"
                                                                ControlToValidate="txtGuiaTransportista"
                                                                ErrorMessage="Guía transportista requerida"
                                                                CssClass="text-danger small"
                                                                Display="Dynamic"
                                                                ValidationGroup="AgregarConductor">
                                                            </asp:RequiredFieldValidator>
                                                        </div>
                                                    </asp:Panel>

                                                    <!-- Información de documentos que se reutilizan -->
                                                    <div class="alert alert-info">
                                                        <small>
                                                            <i class="fas fa-info-circle"></i> 
                                                            <strong>Se reutilizan del lote:</strong><br>
                                                            <asp:Label ID="lblDocumentosReutilizados" runat="server"></asp:Label>
                                                        </small>
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- Botones de Acción -->
                                            <div class="row">
                                                <div class="col-md-12">
                                                    <div class="d-flex justify-content-end gap-2">
                                                        <asp:Button ID="btnLimpiarConductor" runat="server" 
                                                            Text="Limpiar Datos" 
                                                            CssClass="btn btn-secondary"
                                                            CausesValidation="false"
                                                            OnClick="btnLimpiarConductor_Click" />
                                                        
                                                        <asp:Button ID="btnAgregarConductor" runat="server" 
                                                            Text="Agregar Conductor al Lote" 
                                                            CssClass="btn btn-warning btn-lg"
                                                            OnClick="btnAgregarConductor_Click"
                                                            ValidationGroup="AgregarConductor" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                                <!-- ====== LISTA DE CONDUCTORES AGREGADOS AL LOTE ====== -->
                                <asp:Panel ID="pnlListaConductores" runat="server" Visible="false">
                                    <div class="card mb-4">
                                        <div class="card-header bg-info text-white">
                                            <h5 class="mb-0">
                                                <i class="fas fa-list"></i> Conductores Agregados al Lote
                                            </h5>
                                        </div>
                                        <div class="card-body">
                                            <asp:GridView ID="gvConductoresLote" runat="server" 
                                                CssClass="table table-striped table-hover"
                                                AutoGenerateColumns="false"
                                                EmptyDataText="No hay conductores agregados al lote"
                                                OnRowCommand="gvConductoresLote_RowCommand">
                                                <Columns>
                                                    <asp:BoundField DataField="NombreConductor" HeaderText="Conductor" />
                                                    <asp:BoundField DataField="PlacaTracto" HeaderText="Tracto" />
                                                    <asp:BoundField DataField="PlacaCarreta" HeaderText="Carreta" />
                                                    <asp:BoundField DataField="GuiaRemitente" HeaderText="Guía Remitente" />
                                                    <asp:BoundField DataField="GuiaTransportista" HeaderText="Guía Transportista" />
                                                    <asp:BoundField DataField="EstadoViaje" HeaderText="Estado Viaje" />
                                                    <asp:TemplateField HeaderText="Acciones">
                                                        <ItemTemplate>
                                                            <asp:Button runat="server" 
                                                                Text="Quitar" 
                                                                CssClass="btn btn-danger btn-sm"
                                                                CommandName="Quitar"
                                                                CommandArgument='<%# Container.DataItemIndex %>'
                                                                OnClientClick="return confirm('¿Quitar este conductor del lote?');" />
                                                        </ItemTemplate>
                                                    </asp:TemplateField>
                                                </Columns>
                                            </asp:GridView>
                                        </div>
                                    </div>
                                </asp:Panel>

                            </ContentTemplate>
                            <Triggers>
                                <asp:AsyncPostBackTrigger ControlID="btnIniciarLote" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnCancelarLote" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnFinalizarLote" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnAgregarConductor" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnLimpiarConductor" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="ddlTipoOperacionBase" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="rblAmbitoOperacionBase" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="ddlConductor" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="btnCrearNuevoViaje" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnFinalizarViajeUnico" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnVerHistorialViajes" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="gvConductoresLote" EventName="RowCommand" />
                            </Triggers>
                        </asp:UpdatePanel>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- Modal para Historial de Viajes -->
    <div class="modal fade" id="modalHistorialViajes" tabindex="-1" aria-labelledby="modalHistorialViajesLabel" aria-hidden="true">
        <div class="modal-dialog modal-xl">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title" id="modalHistorialViajesLabel">
                        <i class="fas fa-history"></i> Historial de Viajes - <span id="spanConductorModal" runat="server"></span>
                    </h5>
                    <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                </div>
                <div class="modal-body">
                    <asp:UpdatePanel ID="UpdatePanelModal" runat="server" UpdateMode="Conditional">
                        <ContentTemplate>
                            <asp:GridView ID="gvHistorialViajes" runat="server" 
                                CssClass="table table-striped table-hover"
                                AutoGenerateColumns="false"
                                EmptyDataText="No se encontraron viajes para este conductor"
                                OnRowCommand="gvHistorialViajes_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="NumeroViajeProgreso" HeaderText="N° Viaje" />
                                    <asp:BoundField DataField="FechaInicio" HeaderText="Fecha Inicio" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                                    <asp:BoundField DataField="FechaCierre" HeaderText="Fecha Cierre" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                                    <asp:BoundField DataField="CantidadDespachos" HeaderText="Despachos" />
                                    <asp:BoundField DataField="TipoViaje" HeaderText="Tipo" />
                                    <asp:BoundField DataField="EstadoViaje" HeaderText="Estado" />
                                    <asp:TemplateField HeaderText="Acciones">
                                        <ItemTemplate>
                                            <asp:Button runat="server" 
                                                Text="Reabrir" 
                                                CssClass="btn btn-warning btn-sm"
                                                CommandName="Reabrir"
                                                CommandArgument='<%# Eval("IdViajeProgreso") %>'
                                                Visible='<%# Eval("EstadoViaje").ToString() == "CERRADO" %>'
                                                OnClientClick="return confirm('¿Reabrir este viaje para agregar más despachos?');" />
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                </Columns>
                            </asp:GridView>
                        </ContentTemplate>
                        <Triggers>
                            <asp:AsyncPostBackTrigger ControlID="gvHistorialViajes" EventName="RowCommand" />
                        </Triggers>
                    </asp:UpdatePanel>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <!-- Loading Panel -->
    <asp:UpdateProgress ID="UpdateProgress1" runat="server" AssociatedUpdatePanelID="UpdatePanelMain">
        <ProgressTemplate>
            <div class="loading-overlay">
                <div class="loading-content">
                    <div class="spinner-border text-primary" role="status">
                        <span class="visually-hidden">Cargando...</span>
                    </div>
                    <div class="mt-2">Procesando...</div>
                </div>
            </div>
        </ProgressTemplate>
    </asp:UpdateProgress>

</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="ScriptsSection" runat="server">
    <link href="https://cdnjs.cloudflare.com/ajax/libs/select2/4.0.13/css/select2.min.css" rel="stylesheet" />
    <link href="https://cdnjs.cloudflare.com/ajax/libs/select2-bootstrap-5-theme/1.3.2/select2-bootstrap-5-theme.min.css" rel="stylesheet" />
    <script src="https://cdnjs.cloudflare.com/ajax/libs/select2/4.0.13/js/select2.min.js"></script>
    <script type="text/javascript">
        $(document).ready(function () {
            initializeSelect2();
            setDefaultDate();
        });

        function initializeSelect2() {
            $('.select2-searchable').select2({
                theme: 'bootstrap-5',
                placeholder: function () {
                    return $(this).find('option:first-child').text();
                },
                allowClear: false,
                width: '100%'
            });
        }

        function setDefaultDate() {
            var today = new Date();
            var dd = String(today.getDate()).padStart(2, '0');
            var mm = String(today.getMonth() + 1).padStart(2, '0');
            var yyyy = today.getFullYear();
            var todayString = yyyy + '-' + mm + '-' + dd;

            var fechaInputs = [
                '<%= txtFechaDespachoBase.ClientID %>',
                '<%= txtFechaEmisionFacturaBase.ClientID %>',
                '<%= txtFechaEmisionCPICBase.ClientID %>'
            ];

            fechaInputs.forEach(function (inputId) {
                var input = document.getElementById(inputId);
                if (input && input.value === '') {
                    input.value = todayString;
                }
            });
        }

        // Función para mostrar el modal de historial
        function showHistorialModal() {
            var modal = new bootstrap.Modal(document.getElementById('modalHistorialViajes'));
            modal.show();
        }

        // Re-inicializar Select2 después de un postback parcial del UpdatePanel
        var prm = Sys.WebForms.PageRequestManager.getInstance();
        prm.add_endRequest(function () {
            initializeSelect2();
            setDefaultDate();
            autoHideMessages();
        });

        // Función para auto-ocultar mensajes después de 5 segundos
        function autoHideMessages() {
            setTimeout(function () {
                $('.alert-dismissible').fadeOut('slow');
            }, 5000);
        }

        // Función para scroll automático a la sección de conductores
        function scrollToConductores() {
            setTimeout(function () {
                var conductoresPanel = document.getElementById('<%= pnlAdicionConductores.ClientID %>');
                if (conductoresPanel) {
                    conductoresPanel.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }
            }, 100);
        }
    </script>
    <style>
        /* Estilos mejorados para el sistema de lotes */
        .card {
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            border: none;
            margin-bottom: 1rem;
            transition: transform 0.2s ease-in-out;
        }

        .card:hover {
            transform: translateY(-2px);
        }

        .card-header {
            border-bottom: 2px solid rgba(0, 0, 0, 0.1);
        }

        .form-label {
            font-weight: 500;
            margin-bottom: 0.5rem;
        }

        .text-danger {
            font-size: 0.875rem;
        }

        .gap-2 > * {
            margin-right: 0.5rem;
        }

        .gap-2 > *:last-child {
            margin-right: 0;
        }

        .loading-overlay {
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background-color: rgba(0, 0, 0, 0.5);
            z-index: 9999;
            display: flex;
            justify-content: center;
            align-items: center;
        }

        .loading-content {
            background-color: white;
            padding: 20px;
            border-radius: 8px;
            text-align: center;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
        }

        /* Paneles de documentos */
        .border {
            border-radius: 8px;
            background-color: #f8f9fa;
        }

        /* Radio buttons style */
        .form-check input[type="radio"] {
            margin-right: 0.5rem;
        }

        /* File upload styling */
        .form-control[type="file"] {
            padding: 0.375rem 0.75rem;
        }

        /* Estilos específicos para el sistema de lotes */
        .card[style*="border-left: 4px solid #007bff"] {
            animation: slideInLeft 0.5s ease-out;
        }

        .card[style*="border-left: 4px solid #28a745"] {
            animation: slideInDown 0.5s ease-out;
        }

        .card[style*="border-left: 4px solid #ffc107"] {
            animation: slideInRight 0.5s ease-out;
        }

        @keyframes slideInLeft {
            from {
                opacity: 0;
                transform: translateX(-30px);
            }
            to {
                opacity: 1;
                transform: translateX(0);
            }
        }

        @keyframes slideInRight {
            from {
                opacity: 0;
                transform: translateX(30px);
            }
            to {
                opacity: 1;
                transform: translateX(0);
            }
        }

        @keyframes slideInDown {
            from {
                opacity: 0;
                transform: translateY(-20px);
            }
            to {
                opacity: 1;
                transform: translateY(0);
            }
        }

        /* Estilo para el badge del estado del lote */
        .badge {
            font-size: 0.75rem;
            padding: 0.5rem 1rem;
        }

        /* Resumen del lote */
        .bg-light {
            background-color: #f8f9fa !important;
        }

        /* Tablas mejoradas */
        .table {
            border-radius: 8px;
            overflow: hidden;
        }

        .table thead th {
            background-color: #f8f9fa;
            border-bottom: 2px solid #dee2e6;
            font-weight: 600;
        }

        .table-hover tbody tr:hover {
            background-color: rgba(0, 0, 0, 0.075);
            transition: background-color 0.15s ease-in-out;
        }

        /* Botones de acción mejorados */
        .btn {
            transition: all 0.2s ease-in-out;
        }

        .btn:hover {
            transform: translateY(-1px);
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
        }

        /* Estilos para alertas de información */
        .alert-info {
            background: linear-gradient(135deg, #d1ecf1 0%, #bee5eb 100%);
            border: 1px solid #b6d4da;
        }

        .alert-success {
            background: linear-gradient(135deg, #d4edda 0%, #c3e6cb 100%);
            border: 1px solid #badbcc;
        }

        .alert-warning {
            background: linear-gradient(135deg, #fff3cd 0%, #ffeaa7 100%);
            border: 1px solid #ffeaa7;
        }

        /* Modal styling */
        .modal-xl {
            max-width: 1200px;
        }

        .modal-content {
            border: none;
            box-shadow: 0 10px 25px rgba(0, 0, 0, 0.2);
        }

        /* Animación para mensajes */
        .alert {
            animation: fadeInDown 0.5s ease-out;
        }

        @keyframes fadeInDown {
            from {
                opacity: 0;
                transform: translate3d(0, -100%, 0);
            }
            to {
                opacity: 1;
                transform: translate3d(0, 0, 0);
            }
        }

        /* Responsive adjustments */
        @media (max-width: 768px) {
            .d-flex.justify-content-end {
                justify-content: center !important;
            }
            
            .gap-2 {
                flex-direction: column;
            }
            
            .gap-2 > * {
                margin-right: 0;
                margin-bottom: 0.5rem;
                width: 100%;
            }

            .col-md-6, .col-md-4 {
                margin-bottom: 1rem;
            }
        }

        /* Estilo especial para panel bloqueado */
        .panel-bloqueado {
            opacity: 0.8;
            background-color: #f8f9fa;
        }

        .panel-bloqueado .form-control,
        .panel-bloqueado .form-select {
            background-color: #e9ecef;
            cursor: not-allowed;
        }

        /* Progress indicators */
        .step-indicator {
            display: flex;
            align-items: center;
            margin-bottom: 1rem;
        }

        .step {
            display: flex;
            align-items: center;
            justify-content: center;
            width: 40px;
            height: 40px;
            border-radius: 50%;
            background-color: #6c757d;
            color: white;
            font-weight: bold;
            margin-right: 1rem;
        }

        .step.active {
            background-color: #007bff;
        }

        .step.completed {
            background-color: #28a745;
        }
    </style>
</asp:Content>
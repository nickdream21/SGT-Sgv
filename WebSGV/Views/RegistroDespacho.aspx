<%@ Page Title="Registro de Despachos Unificado" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" MaintainScrollPositionOnPostback="true" CodeBehind="RegistroDespacho.aspx.cs" Inherits="WebSGV.Views.RegistroDespacho" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

<link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/RegistroDespacho.css") %>" rel="stylesheet" />

    <div class="container-fluid">
        <div class="row">
            <div class="col-md-12">
                <div class="card">
                    <div class="rd-page-header">
                        <h4>
                            <i class="fas fa-truck mr-2"></i> Registro de Despachos
                            <asp:Label ID="lblEstadoLote" runat="server" CssClass="badge ml-3" style="background:#16a34a;font-size:.68rem;font-weight:600;" Visible="false"></asp:Label>
                        </h4>
                    </div>
                    <div class="card-body">
                        <!-- Solo btnIniciarLote es PostBackTrigger (ver <Triggers> más abajo): ese paso incluye
                             los FileUpload de Factura/CPIC y UpdatePanel no soporta subida de archivos por AJAX
                             parcial. Agregar conductor sí es asíncrono — ya no hay FileUpload en ese paso. -->
                        <asp:UpdatePanel ID="UpdatePanelMain" runat="server" UpdateMode="Conditional">
                            <ContentTemplate>
                                
                                <!-- Estado del lote para JS (se actualiza en cada postback parcial) -->
                                <asp:HiddenField ID="hfLoteActivo" runat="server" Value="0" />

                                <!-- Indicador de progreso de pasos -->
                                <div class="rd-stepper mb-4">
                                    <div class="rd-stepper-inner">
                                        <div id="rdStep1" class="rd-stepper-item">
                                            <div class="rd-stepper-circle">1</div>
                                            <div class="rd-stepper-label">Configurar Lote</div>
                                        </div>
                                        <div class="rd-stepper-connector"></div>
                                        <div id="rdStep2" class="rd-stepper-item">
                                            <div class="rd-stepper-circle">2</div>
                                            <div class="rd-stepper-label">Agregar Conductores</div>
                                        </div>
                                    </div>
                                </div>

                                <!-- Mensajes -->
                                <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-3">
                                    <div class="position-relative">
                                        <asp:Label ID="lblMensaje" runat="server" CssClass="alert d-block mb-0 pe-5"></asp:Label>
                                        <button type="button" class="close position-absolute" style="top:.65rem;right:.75rem;"
                                            onclick="this.closest('.mb-3').style.display='none'" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>
                                    </div>
                                </asp:Panel>

                                <!-- ====== FASE 1: CONFIGURACIÓN BASE DEL LOTE ====== -->
                                <asp:Panel ID="pnlConfiguracionBase" runat="server">
                                    <div class="card mb-4" style="border-top: 3px solid #1e40af;">
                                        <div class="rd-step-header">
                                            <h5><span class="rd-step-num">1</span> Configuración Base del Lote</h5>
                                            <div class="rd-subtitle">Complete los datos comunes que aplicarán a todos los despachos de este lote</div>
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
                                                            SetFocusOnError="true"
                                                            ValidationGroup="ConfiguracionBase">
                                                        </asp:RequiredFieldValidator>
                                                        <asp:CustomValidator ID="cvFechaDespachoBase" runat="server"
                                                            ControlToValidate="txtFechaDespachoBase"
                                                            ErrorMessage="Ingrese una fecha de programación válida (año realista y rango permitido)"
                                                            CssClass="text-danger small"
                                                            Display="Dynamic"
                                                            SetFocusOnError="true"
                                                            ClientValidationFunction="validarFechaProgramacionCliente"
                                                            OnServerValidate="cvFechaDespachoBase_ServerValidate"
                                                            ValidationGroup="ConfiguracionBase">
                                                        </asp:CustomValidator>
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
                                                            SetFocusOnError="true"
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
                                                                AutoCompleteType="Disabled" autocomplete="off"
                                                                placeholder="Ej: 1234567890"
                                                                MaxLength="10">
                                                            </asp:TextBox>
                                                            <asp:RegularExpressionValidator ID="revNumeroPedidoBase" runat="server"
                                                                ControlToValidate="txtNumeroPedidoBase"
                                                                ValidationExpression="^\d{10}$"
                                                                ErrorMessage="Debe tener exactamente 10 dígitos numéricos"
                                                                CssClass="text-danger small"
                                                                Display="Dynamic"
                                                                SetFocusOnError="true"
                                                                ValidationGroup="ConfiguracionBase">
                                                            </asp:RegularExpressionValidator>
                                                            <small class="form-text text-muted">Opcional. Si se ingresa, debe tener exactamente 10 dígitos.</small>
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
                                                            SetFocusOnError="true"
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
                                                        </asp:DropDownList>
                                                        <asp:RequiredFieldValidator ID="rfvLugarOperacionBase" runat="server"
                                                            ControlToValidate="ddlLugarOperacionBase"
                                                            InitialValue=""
                                                            ErrorMessage="Debe seleccionar una planta"
                                                            CssClass="text-danger small"
                                                            Display="Dynamic"
                                                            SetFocusOnError="true"
                                                            ValidationGroup="ConfiguracionBase">
                                                        </asp:RequiredFieldValidator>
                                                    </div>
                                                </div>
                                            </div>

                                            <!-- DOCUMENTACIÓN BASE -->
                                            <div class="row mt-4">
                                                <div class="col-12">
                                                    <div class="rd-section-label"><i class="fas fa-file-alt mr-1"></i> Documentación Base del Lote</div>
                                                    
                                                    <!-- Panel de Ayuda de Documentos -->
                                                    <asp:Panel ID="pnlAyudaDocumentosBase" runat="server" CssClass="rd-notice mb-3">
                                                        <asp:Label ID="lblAyudaDocumentosBase" runat="server" Text="Seleccione el tipo de operación y ámbito para ver los documentos requeridos."></asp:Label>
                                                    </asp:Panel>

                                                    <div class="row">
                                                        <!-- FACTURA BASE -->
                                                        <div class="col-md-6">
                                                            <asp:Panel ID="pnlFacturaBase" runat="server" CssClass="rd-doc-box mb-3" Visible="false">
                                                                <div class="rd-doc-title"><i class="fas fa-receipt mr-1"></i> Datos de Factura</div>

                                                                <div class="form-group mb-2">
                                                                    <label for="txtNumeroFacturaBase" class="form-label">
                                                                        <strong>N° Factura:</strong>
                                                                        <span class="text-danger">*</span>
                                                                    </label>
                                                                    <asp:TextBox ID="txtNumeroFacturaBase" runat="server"
                                                                        CssClass="form-control"
                                                                        AutoCompleteType="Disabled" autocomplete="off"
                                                                        placeholder="Ej: F222-00004267"
                                                                        MaxLength="30">
                                                                    </asp:TextBox>
                                                                    <asp:RequiredFieldValidator ID="rfvNumeroFacturaBase" runat="server"
                                                                        ControlToValidate="txtNumeroFacturaBase"
                                                                        ErrorMessage="El número de factura es obligatorio"
                                                                        CssClass="text-danger small"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase">
                                                                    </asp:RequiredFieldValidator>
                                                                    <asp:RegularExpressionValidator ID="revNumeroFacturaBase" runat="server"
                                                                        ControlToValidate="txtNumeroFacturaBase"
                                                                        ValidationExpression="^[A-Za-z0-9\-/]{3,30}$"
                                                                        ErrorMessage="El N° de factura solo permite letras, números, guion o slash (3-30 caracteres)"
                                                                        CssClass="text-danger small"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase">
                                                                    </asp:RegularExpressionValidator>
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
                                                                    <asp:RequiredFieldValidator ID="rfvFechaEmisionFacturaBase" runat="server"
                                                                        ControlToValidate="txtFechaEmisionFacturaBase"
                                                                        ErrorMessage="La fecha de emisión de la factura es obligatoria"
                                                                        CssClass="text-danger small"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase">
                                                                    </asp:RequiredFieldValidator>
                                                                    <asp:CustomValidator ID="cvFechaEmisionFacturaBase" runat="server"
                                                                        ControlToValidate="txtFechaEmisionFacturaBase"
                                                                        ErrorMessage="La fecha de emisión de factura no es válida"
                                                                        CssClass="text-danger small"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ClientValidationFunction="validarFechaEmisionCliente"
                                                                        OnServerValidate="cvFechaEmisionFacturaBase_ServerValidate"
                                                                        ValidationGroup="ConfiguracionBase">
                                                                    </asp:CustomValidator>
                                                                </div>

                                                                <div class="form-group mb-2">
                                                                    <label for="txtValorTotalFacturaBase" class="form-label">
                                                                        <strong>Valor Total (S/):</strong>
                                                                        <span class="text-danger">*</span>
                                                                    </label>
                                                                    <asp:TextBox ID="txtValorTotalFacturaBase" runat="server"
                                                                        CssClass="form-control"
                                                                        placeholder="0.00"
                                                                        TextMode="Number"
                                                                        step="0.01"
                                                                        min="0">
                                                                    </asp:TextBox>
                                                                    <asp:RequiredFieldValidator ID="rfvValorTotalFacturaBase" runat="server"
                                                                        ControlToValidate="txtValorTotalFacturaBase"
                                                                        ErrorMessage="El valor total de la factura es obligatorio"
                                                                        CssClass="text-danger small"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase">
                                                                    </asp:RequiredFieldValidator>
                                                                    <asp:RangeValidator ID="rvValorTotalFacturaBase" runat="server"
                                                                        ControlToValidate="txtValorTotalFacturaBase"
                                                                        MinimumValue="0.01"
                                                                        MaximumValue="99999999"
                                                                        Type="Double"
                                                                        CultureInvariantValues="true"
                                                                        ErrorMessage="Ingrese un monto válido (mayor a 0)"
                                                                        CssClass="text-danger small"
                                                                        Display="Dynamic"
                                                                        ValidationGroup="ConfiguracionBase">
                                                                    </asp:RangeValidator>
                                                                </div>

                                                                <div class="form-group mb-0">
                                                                    <label for="fileFacturaBase" class="form-label">
                                                                        <strong>Adjuntar Documento:</strong>
                                                                        <span class="text-muted small">(Opcional)</span>
                                                                    </label>
                                                                    <asp:FileUpload ID="fileFacturaBase" runat="server" CssClass="form-control" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png" />
                                                                    <small class="form-text text-muted">PDF, DOC, DOCX, JPG o PNG. Máx. 50MB.</small>
                                                                </div>
                                                            </asp:Panel>
                                                        </div>

                                                        <!-- CPIC BASE -->
                                                        <div class="col-md-6">
                                                            <asp:Panel ID="pnlCPICBase" runat="server" CssClass="rd-doc-box mb-3" Visible="false">
                                                                <div class="rd-doc-title"><i class="fas fa-shipping-fast mr-1"></i> Datos de CPIC</div>

                                                                <div class="form-group mb-2">
                                                                    <label for="txtNumeroCPICBase" class="form-label">
                                                                        <strong>N° CPIC:</strong>
                                                                        <span class="text-danger">*</span>
                                                                    </label>
                                                                    <asp:TextBox ID="txtNumeroCPICBase" runat="server"
                                                                        CssClass="form-control"
                                                                        AutoCompleteType="Disabled" autocomplete="off"
                                                                        placeholder="Ej: 1234567"
                                                                        MaxLength="10">
                                                                    </asp:TextBox>
                                                                    <asp:RequiredFieldValidator ID="rfvNumeroCPICBase" runat="server"
                                                                        ControlToValidate="txtNumeroCPICBase"
                                                                        ErrorMessage="El número de CPIC es obligatorio"
                                                                        CssClass="text-danger small"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase">
                                                                    </asp:RequiredFieldValidator>
                                                                    <asp:RegularExpressionValidator ID="revNumeroCPICBase" runat="server"
                                                                        ControlToValidate="txtNumeroCPICBase"
                                                                        ValidationExpression="^[A-Za-z0-9\-/]{3,20}$"
                                                                        ErrorMessage="El N° de CPIC solo permite letras, números, guion o slash (3-20 caracteres)"
                                                                        CssClass="text-danger small"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase">
                                                                    </asp:RegularExpressionValidator>
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
                                                                    <asp:RequiredFieldValidator ID="rfvFechaEmisionCPICBase" runat="server"
                                                                        ControlToValidate="txtFechaEmisionCPICBase"
                                                                        ErrorMessage="La fecha de emisión del CPIC es obligatoria"
                                                                        CssClass="text-danger small"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase">
                                                                    </asp:RequiredFieldValidator>
                                                                    <asp:CustomValidator ID="cvFechaEmisionCPICBase" runat="server"
                                                                        ControlToValidate="txtFechaEmisionCPICBase"
                                                                        ErrorMessage="La fecha de emisión de CPIC no es válida"
                                                                        CssClass="text-danger small"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ClientValidationFunction="validarFechaEmisionCliente"
                                                                        OnServerValidate="cvFechaEmisionCPICBase_ServerValidate"
                                                                        ValidationGroup="ConfiguracionBase">
                                                                    </asp:CustomValidator>
                                                                </div>

                                                                <div class="form-group mb-2">
                                                                    <label for="txtValorFleteBase" class="form-label">
                                                                        <strong>Valor Flete (S/):</strong>
                                                                        <span class="text-danger">*</span>
                                                                    </label>
                                                                    <asp:TextBox ID="txtValorFleteBase" runat="server"
                                                                        CssClass="form-control"
                                                                        placeholder="0.00"
                                                                        TextMode="Number"
                                                                        step="0.01"
                                                                        min="0">
                                                                    </asp:TextBox>
                                                                    <asp:RequiredFieldValidator ID="rfvValorFleteBase" runat="server"
                                                                        ControlToValidate="txtValorFleteBase"
                                                                        ErrorMessage="El valor del flete es obligatorio"
                                                                        CssClass="text-danger small"
                                                                        Display="Dynamic"
                                                                        SetFocusOnError="true"
                                                                        ValidationGroup="ConfiguracionBase">
                                                                    </asp:RequiredFieldValidator>
                                                                    <asp:RangeValidator ID="rvValorFleteBase" runat="server"
                                                                        ControlToValidate="txtValorFleteBase"
                                                                        MinimumValue="0.01"
                                                                        MaximumValue="99999999"
                                                                        Type="Double"
                                                                        CultureInvariantValues="true"
                                                                        ErrorMessage="Ingrese un monto válido (mayor a 0)"
                                                                        CssClass="text-danger small"
                                                                        Display="Dynamic"
                                                                        ValidationGroup="ConfiguracionBase">
                                                                    </asp:RangeValidator>
                                                                </div>

                                                                <div class="form-group mb-0">
                                                                    <label for="fileCPICBase" class="form-label">
                                                                        <strong>Adjuntar Documento:</strong>
                                                                        <span class="text-muted small">(Opcional)</span>
                                                                    </label>
                                                                    <asp:FileUpload ID="fileCPICBase" runat="server" CssClass="form-control" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png" />
                                                                    <small class="form-text text-muted">PDF, DOC, DOCX, JPG o PNG. Máx. 50MB.</small>
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
                                    <div class="card mb-4" style="border-top: 3px solid #16a34a;">
                                        <div class="rd-summary-header">
                                            <h5><i class="fas fa-clipboard-check mr-2"></i> Lote Activo</h5>
                                        </div>
                                        <div class="card-body">
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
                                    <asp:Panel ID="pnlConductorCard" runat="server" CssClass="card mb-4 rd-conductor-card" style="border-top: 3px solid #1e40af;">
                                        <div class="rd-step-header">
                                            <h5><span class="rd-step-num">2</span> <asp:Literal ID="litTituloConductor" runat="server" Text="Agregar Conductores al Lote"></asp:Literal></h5>
                                            <div class="rd-subtitle">Complete los datos específicos para cada conductor</div>
                                        </div>
                                        <div class="card-body">

                                            <!-- Aviso de modo edición -->
                                            <asp:Panel ID="pnlModoEdicion" runat="server" Visible="false" CssClass="rd-edit-flag mb-3">
                                                <i class="fas fa-pen mr-1"></i>
                                                Editando a <asp:Label ID="lblConductorEnEdicion" runat="server"></asp:Label>.
                                                Los cambios reemplazan los datos de esa fila.
                                            </asp:Panel>

                                            <!-- Mensajes de esta sección: se muestran acá y no al tope de la página,
                                                 para que el usuario no tenga que subir a enterarse del error. -->
                                            <asp:Panel ID="pnlMensajeConductor" runat="server" Visible="false" CssClass="mb-3">
                                                <div class="position-relative">
                                                    <asp:Label ID="lblMensajeConductor" runat="server" CssClass="alert d-block mb-0 pe-5"></asp:Label>
                                                    <button type="button" class="close position-absolute" style="top:.65rem;right:.75rem;"
                                                        onclick="this.closest('.mb-3').style.display='none'" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>
                                                </div>
                                            </asp:Panel>

                                            <!-- INFORMACIÓN DE VIAJES EN PROGRESO
                                                 UpdatePanel propio: elegir un conductor solo refresca este bloque, en vez de
                                                 re-renderizar todo el formulario (lo que reiniciaba los Select2 y movía la página). -->
                                            <asp:UpdatePanel ID="UpdatePanelViajes" runat="server" UpdateMode="Conditional">
                                                <ContentTemplate>
                                            <asp:Panel ID="pnlViajesProgreso" runat="server" CssClass="card mb-4" style="border-top: 2px solid #6d28d9;" Visible="false">
                                                <div class="rd-viaje-header">
                                                    <h6><i class="fas fa-route mr-1"></i> Viajes en Progreso — <span id="spanNombreConductor" runat="server"></span></h6>
                                                </div>
                                                <div class="card-body">
                                                    <div class="row">
                                                        <div class="col-md-8">
                                                            <asp:Panel ID="pnlSinViajes" runat="server" Visible="true">
                                                                <div class="rd-notice mb-0">
                                                                    <i class="fas fa-info-circle mr-1"></i>
                                                                    <strong>Sin viajes activos:</strong> Se creará automáticamente un nuevo viaje en progreso para este conductor.
                                                                </div>
                                                            </asp:Panel>
                                                            
                                                            <asp:Panel ID="pnlConViajes" runat="server" Visible="false">
                                                                <label class="form-label">
                                                                    <strong>Viaje activo encontrado:</strong>
                                                                </label>
                                                                 <div class="rd-notice rd-notice-ok">
                                                                    <div class="row align-items-center">
                                                                        <div class="col-md-12">
                                                                            <strong><i class="fas fa-clipboard-list"></i> <asp:Label ID="lblNumeroViaje" runat="server"></asp:Label></strong><br>
                                                                            <small class="text-muted">
                                                                                Iniciado: <asp:Label ID="lblFechaInicio" runat="server"></asp:Label> | 
                                                                                Despachos: <asp:Label ID="lblCantidadDespachos" runat="server"></asp:Label> | 
                                                                                Tipo: <asp:Label ID="lblTipoViaje" runat="server"></asp:Label>
                                                                            </small>
                                                                        </div>
                                                                    </div>
                                                                </div>
                                                                <!-- BLK-003: Botón para finalizar el viaje único activo -->
                                                                <div class="mt-2">
                                                                    <asp:Button ID="btnFinalizarViajeUnico" runat="server"
                                                                        Text="Finalizar Viaje"
                                                                        CssClass="btn btn-outline-warning btn-sm"
                                                                        OnClick="btnFinalizarViajeUnico_Click"
                                                                        CausesValidation="false"
                                                                        ToolTip="Cierra el viaje activo del conductor"
                                                                        OnClientClick="return confirm('¿Desea finalizar este viaje en progreso?');" />
                                                                </div>
                                                            </asp:Panel>
                                                            
                                                            <asp:Panel ID="pnlMultiplesViajes" runat="server" Visible="false">
                                                                <div class="rd-notice rd-notice-warn mb-3">
                                                                    <i class="fas fa-exclamation-triangle mr-1"></i>
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
                                                </ContentTemplate>
                                                <Triggers>
                                                    <asp:AsyncPostBackTrigger ControlID="ddlConductor" EventName="SelectedIndexChanged" />
                                                </Triggers>
                                            </asp:UpdatePanel>

                                            <div class="row">
                                                <!-- Columna Izquierda -->
                                                <div class="col-md-6 rd-form-col">

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
                                                            SetFocusOnError="true"
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
                                                            SetFocusOnError="true"
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
                                                            SetFocusOnError="true"
                                                            ValidationGroup="AgregarConductor">
                                                        </asp:RequiredFieldValidator>
                                                    </div>
                                                </div>

                                                <!-- Columna Derecha - Guías Específicas -->
                                                <div class="col-md-6 rd-form-col">
                                                    <div class="rd-section-label mt-3"><i class="fas fa-file-alt mr-1"></i> Documentos del Conductor</div>

                                                    <!-- Guía Remitente -->
                                                    <asp:Panel ID="pnlGuiaRemitenteConductor" runat="server" Visible="false">
                                                        <div class="form-group mb-3">
                                                            <label for="txtGuiaRemitente" class="form-label">
                                                                <strong>Guía Remitente:</strong>
                                                                <span class="text-muted small">(Opcional)</span>
                                                            </label>
                                                            <asp:TextBox ID="txtGuiaRemitente" runat="server"
                                                                CssClass="form-control"
                                                                AutoCompleteType="Disabled" autocomplete="off"
                                                                MaxLength="30"
                                                                placeholder="Número de guía remitente (puede completarse después)">
                                                            </asp:TextBox>
                                                            <asp:RequiredFieldValidator ID="rfvGuiaRemitente" runat="server"
                                                                ControlToValidate="txtGuiaRemitente"
                                                                ErrorMessage="Guía remitente requerida"
                                                                CssClass="text-danger small"
                                                                Display="Dynamic"
                                                                SetFocusOnError="true"
                                                                Enabled="false"
                                                                ValidationGroup="AgregarConductor">
                                                            </asp:RequiredFieldValidator>
                                                            <small class="form-text text-muted">Si no dispone del número ahora, puede dejarlo en blanco.</small>
                                                            <asp:RegularExpressionValidator ID="revGuiaRemitente" runat="server"
                                                                ControlToValidate="txtGuiaRemitente"
                                                                ValidationExpression="^$|^[A-Za-z0-9\-/]{4,30}$"
                                                                ErrorMessage="La guía remitente debe tener entre 4 y 30 caracteres (letras, números, guion o slash)"
                                                                CssClass="text-danger small"
                                                                Display="Dynamic"
                                                                SetFocusOnError="true"
                                                                ValidationGroup="AgregarConductor">
                                                            </asp:RegularExpressionValidator>
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
                                                                AutoCompleteType="Disabled" autocomplete="off"
                                                                MaxLength="30"
                                                                placeholder="Número de guía transportista">
                                                            </asp:TextBox>
                                                            <asp:RequiredFieldValidator ID="rfvGuiaTransportista" runat="server"
                                                                ControlToValidate="txtGuiaTransportista"
                                                                ErrorMessage="Guía transportista requerida"
                                                                CssClass="text-danger small"
                                                                Display="Dynamic"
                                                                SetFocusOnError="true"
                                                                ValidationGroup="AgregarConductor">
                                                            </asp:RequiredFieldValidator>
                                                            <asp:RegularExpressionValidator ID="revGuiaTransportista" runat="server"
                                                                ControlToValidate="txtGuiaTransportista"
                                                                ValidationExpression="^[A-Za-z0-9\-/]{4,30}$"
                                                                ErrorMessage="La guía transportista debe tener entre 4 y 30 caracteres (letras, números, guion o slash)"
                                                                CssClass="text-danger small"
                                                                Display="Dynamic"
                                                                SetFocusOnError="true"
                                                                ValidationGroup="AgregarConductor">
                                                            </asp:RegularExpressionValidator>
                                                        </div>
                                                    </asp:Panel>

                                                    <!-- Manifiesto (solo viajes internacionales).
                                                         La subida se hace desde Gestión de Despachos, no acá: el conductor
                                                         normalmente recién obtiene los ejemplares durante el viaje, y mantener
                                                         un FileUpload en este paso obligaba a un postback completo (recarga de
                                                         página, salto de scroll y aviso de "¿desea salir del sitio?") cada vez
                                                         que se agregaba un conductor. -->
                                                    <asp:Panel ID="pnlManifiestoConductor" runat="server" Visible="false">
                                                        <div class="rd-notice rd-notice-warn mb-3">
                                                            <div class="rd-doc-title" style="color:inherit;"><i class="fas fa-passport mr-1"></i> Manifiesto de Aduana</div>
                                                            <small>
                                                                Cada conductor porta dos ejemplares: uno para cruzar la frontera y otro para el regreso.
                                                                Se adjuntan desde <strong>Gestión de Despachos</strong>, buscando el despacho una vez
                                                                creado el lote — que es cuando el conductor ya cuenta con ellos.
                                                            </small>
                                                        </div>
                                                    </asp:Panel>

                                                    <!-- Información de documentos que se reutilizan -->
                                                    <div class="rd-notice">
                                                        <small>
                                                            <i class="fas fa-info-circle mr-1"></i>
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
                                                        <asp:Button ID="btnCancelarEdicion" runat="server"
                                                            Text="Cancelar edición"
                                                            CssClass="btn btn-outline-secondary"
                                                            CausesValidation="false"
                                                            Visible="false"
                                                            OnClick="btnCancelarEdicion_Click" />

                                                        <asp:Button ID="btnLimpiarConductor" runat="server"
                                                            Text="Limpiar"
                                                            CssClass="btn btn-outline-secondary"
                                                            CausesValidation="false"
                                                            OnClick="btnLimpiarConductor_Click" />

                                                        <asp:Button ID="btnAgregarConductor" runat="server"
                                                            Text="Agregar Conductor"
                                                            CssClass="btn btn-primary"
                                                            OnClick="btnAgregarConductor_Click"
                                                            ValidationGroup="AgregarConductor" />
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </asp:Panel>
                                </asp:Panel>

                                <!-- ====== LISTA DE CONDUCTORES AGREGADOS AL LOTE ====== -->
                                <asp:Panel ID="pnlListaConductores" runat="server" Visible="false">
                                    <div class="card mb-4">
                                        <div class="rd-list-header">
                                            <h5><i class="fas fa-list mr-2"></i> Conductores del Lote</h5>
                                        </div>
                                        <div class="card-body">
                                            <div class="table-responsive">
                                            <asp:GridView ID="gvConductoresLote" runat="server"
                                                CssClass="table table-hover rd-tabla-conductores align-middle"
                                                AutoGenerateColumns="false"
                                                EmptyDataText="Todavía no hay conductores en este lote."
                                                OnRowCommand="gvConductoresLote_RowCommand"
                                                OnRowDataBound="gvConductoresLote_RowDataBound">
                                                <Columns>
                                                    <asp:TemplateField HeaderText="#" ItemStyle-CssClass="text-muted" ItemStyle-Width="34px">
                                                        <ItemTemplate><%# Container.DataItemIndex + 1 %></ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:BoundField DataField="NombreConductor" HeaderText="Conductor" />
                                                    <asp:TemplateField HeaderText="Tracto">
                                                        <ItemTemplate><span class="rd-placa"><%# Eval("PlacaTracto") %></span></ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:TemplateField HeaderText="Carreta">
                                                        <ItemTemplate><span class="rd-placa"><%# Eval("PlacaCarreta") %></span></ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:TemplateField HeaderText="Guía Remitente">
                                                        <ItemTemplate><%# MostrarDato(Eval("GuiaRemitente")) %></ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:TemplateField HeaderText="Guía Transportista">
                                                        <ItemTemplate><%# MostrarDato(Eval("GuiaTransportista")) %></ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:TemplateField HeaderText="Manifiesto">
                                                        <ItemTemplate><%# ChipManifiesto(Eval("Manifiesto")) %></ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:TemplateField HeaderText="Estado Viaje">
                                                        <ItemTemplate><%# ChipEstadoViaje(Eval("EstadoViaje")) %></ItemTemplate>
                                                    </asp:TemplateField>
                                                    <asp:TemplateField HeaderText="Acciones" ItemStyle-CssClass="text-end" HeaderStyle-CssClass="text-end">
                                                        <ItemTemplate>
                                                            <asp:Button runat="server"
                                                                Text="Editar"
                                                                CssClass="btn btn-outline-primary btn-sm"
                                                                CommandName="Editar"
                                                                CausesValidation="false"
                                                                CommandArgument='<%# Container.DataItemIndex %>' />
                                                            <asp:Button runat="server"
                                                                Text="Quitar"
                                                                CssClass="btn btn-outline-danger btn-sm"
                                                                CommandName="Quitar"
                                                                CausesValidation="false"
                                                                CommandArgument='<%# Container.DataItemIndex %>'
                                                                OnClientClick="return confirm('¿Quitar este conductor del lote?');" />
                                                        </ItemTemplate>
                                                    </asp:TemplateField>
                                                </Columns>
                                            </asp:GridView>
                                            </div>
                                        </div>
                                    </div>
                                </asp:Panel>

                            </ContentTemplate>
                            <Triggers>
                                <asp:PostBackTrigger ControlID="btnIniciarLote" />
                                <asp:AsyncPostBackTrigger ControlID="btnCancelarLote" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnFinalizarLote" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnAgregarConductor" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnCancelarEdicion" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnLimpiarConductor" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="ddlTipoOperacionBase" EventName="SelectedIndexChanged" />
                                <asp:AsyncPostBackTrigger ControlID="rblAmbitoOperacionBase" EventName="SelectedIndexChanged" />
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
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>
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
                                                     <asp:BoundField DataField="TipoViaje" HeaderText="Tipo Viaje" />
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
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cerrar</button>
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
    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            txtFechaDespachoBase: '<%= txtFechaDespachoBase.ClientID %>',
            txtFechaEmisionFacturaBase: '<%= txtFechaEmisionFacturaBase.ClientID %>',
            txtFechaEmisionCPICBase: '<%= txtFechaEmisionCPICBase.ClientID %>',
            pnlAdicionConductores: '<%= pnlAdicionConductores.ClientID %>',
            hfLoteActivo: '<%= hfLoteActivo.ClientID %>',
            txtNumeroPedidoBase: '<%= txtNumeroPedidoBase.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistroDespacho.js") %>"></script>
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/RegistroDespacho-2.css") %>" rel="stylesheet" />
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistroDespacho-2.js") %>"></script>
</asp:Content>

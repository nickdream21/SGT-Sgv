<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="ConfiguracionBaseLote.ascx.cs" Inherits="WebSGV.Views.Controles.Despachos.ConfiguracionBaseLote" %>
<%-- "Registrar un lote a mano" de RegistroDespacho (E6-26): la configuración base del lote (fecha, cliente, pedido,
     operación, ámbito, planta y la factura y la CPIC que se reutilizan para todos los conductores) e "Iniciar lote"
     (evento IniciarLotePulsado; PostBackTrigger por los FileUpload, en DisparadoresCompletos). Vive dentro de
     pnlConfiguracionBase y de UpdatePanelMain de la página, que lo abre o pliega (Abierto) y le aplica lo que proponen
     la importación y la descarga (Aplicar). Los estilos y el script son los de la página (RegistroDespacho.css/.js). --%>
<div class="sgv-panel sgv-panel-acento mb-4">
    <button type="button" class="sgv-panel-cab rd-manual-toggle<%= Abierto ? "" : " collapsed" %>"
        data-toggle="collapse" data-target="#rdManualCuerpo"
        aria-expanded="<%= Abierto ? "true" : "false" %>" aria-controls="rdManualCuerpo">
        <span class="d-flex align-items-center">
            <span class="sgv-panel-titulo flex-grow-1"><span class="rd-step-num">1</span> Registrar un lote a mano</span>
            <i class="fas fa-chevron-down rd-manual-chevron ml-2" aria-hidden="true"></i>
        </span>
        <small class="sgv-panel-nota d-block rd-paso-nota">Para despachos sin Word ni documentos del correo (nacionales u otros clientes). Los de exportación se registran arriba.</small>
    </button>
    <div id="rdManualCuerpo" class="sgv-panel-cuerpo collapse<%= Abierto ? " show" : "" %>">
        <div class="row">
            <!-- Columna Izquierda -->
            <div class="col-md-6">
                
                <!-- Fecha de Despacho -->
                <div class="form-group mb-3">
                    <label for="<%= txtFechaDespachoBase.ClientID %>" class="sgv-etiqueta">
                        <strong>Fecha de programación:</strong>
                        <span class="sgv-obligatorio" aria-hidden="true">*</span>
                    </label>
                    <asp:TextBox ID="txtFechaDespachoBase" runat="server" aria-required="true" 
                        CssClass="form-control" 
                        TextMode="Date">
                    </asp:TextBox>
                    <asp:RequiredFieldValidator ID="rfvFechaDespachoBase" runat="server"
                        ControlToValidate="txtFechaDespachoBase"
                        ErrorMessage="Debe seleccionar una fecha de programación"
                        CssClass="sgv-campo-error"
                        Display="Dynamic"
                        SetFocusOnError="true"
                        ValidationGroup="ConfiguracionBase" />
                    <asp:CustomValidator ID="cvFechaDespachoBase" runat="server"
                        ControlToValidate="txtFechaDespachoBase"
                        ErrorMessage="Ingrese una fecha de programación válida (año realista y rango permitido)"
                        CssClass="sgv-campo-error"
                        Display="Dynamic"
                        SetFocusOnError="true"
                        ClientValidationFunction="validarFechaProgramacionCliente"
                        OnServerValidate="cvFechaDespachoBase_ServerValidate"
                        ValidationGroup="ConfiguracionBase" />
                </div>

                <!-- Cliente -->
                <div class="form-group mb-3">
                    <label for="<%= ddlClienteBase.ClientID %>" class="sgv-etiqueta">
                        <strong>Cliente:</strong>
                        <span class="sgv-obligatorio" aria-hidden="true">*</span>
                    </label>
                    <asp:DropDownList ID="ddlClienteBase" runat="server" aria-required="true" 
                        CssClass="custom-select"
                        DataTextField="nombre"
                        DataValueField="idCliente"
                        AppendDataBoundItems="true">
                        <asp:ListItem Value="0" Text="-- Seleccione un cliente --"></asp:ListItem>
                    </asp:DropDownList>
                    <asp:RequiredFieldValidator ID="rfvClienteBase" runat="server"
                        ControlToValidate="ddlClienteBase"
                        InitialValue="0"
                        ErrorMessage="Debe seleccionar un cliente"
                        CssClass="sgv-campo-error"
                        Display="Dynamic"
                        SetFocusOnError="true"
                        ValidationGroup="ConfiguracionBase" />
                </div>

                <!-- Número de Pedido - Solo para Carga Nacional -->
                <asp:Panel ID="pnlNumeroPedido" runat="server" Visible="false">
                    <div class="form-group mb-3">
                        <label for="<%= txtNumeroPedidoBase.ClientID %>" class="sgv-etiqueta">
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
                            CssClass="sgv-campo-error"
                            Display="Dynamic"
                            SetFocusOnError="true"
                            ValidationGroup="ConfiguracionBase" />
                        <small class="form-text text-muted">Opcional. Si se ingresa, debe tener exactamente 10 dígitos.</small>
                    </div>
                </asp:Panel>
            </div>

            <!-- Columna Derecha -->
            <div class="col-md-6">
                
                <!-- Tipo de Operación -->
                <div class="form-group mb-3">
                    <label for="<%= ddlTipoOperacionBase.ClientID %>" class="sgv-etiqueta">
                        <strong>Tipo de operación:</strong>
                        <span class="sgv-obligatorio" aria-hidden="true">*</span>
                    </label>
                    <asp:DropDownList ID="ddlTipoOperacionBase" runat="server" aria-required="true" 
                        CssClass="custom-select"
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
                        CssClass="sgv-campo-error"
                        Display="Dynamic"
                        SetFocusOnError="true"
                        ValidationGroup="ConfiguracionBase" />
                </div>

                <!-- Es Internacional -->
                <div class="form-group mb-3">
                    <span class="sgv-etiqueta d-inline-block mb-2" id="lblAmbitoOperacionBase">
                        <strong>Ámbito:</strong>
                        <span class="sgv-obligatorio" aria-hidden="true">*</span>
                    </span>
                    <div role="radiogroup" aria-labelledby="lblAmbitoOperacionBase" aria-required="true">
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
                    <label for="<%= ddlLugarOperacionBase.ClientID %>" class="sgv-etiqueta">
                        <strong>Planta de operación:</strong>
                        <span class="sgv-obligatorio" aria-hidden="true">*</span>
                    </label>
                    <asp:DropDownList ID="ddlLugarOperacionBase" runat="server" aria-required="true" CssClass="custom-select">
                    </asp:DropDownList>
                    <asp:RequiredFieldValidator ID="rfvLugarOperacionBase" runat="server"
                        ControlToValidate="ddlLugarOperacionBase"
                        InitialValue=""
                        ErrorMessage="Debe seleccionar una planta"
                        CssClass="sgv-campo-error"
                        Display="Dynamic"
                        SetFocusOnError="true"
                        ValidationGroup="ConfiguracionBase" />
                </div>
            </div>
        </div>

        <!-- DOCUMENTACIÓN BASE -->
        <div class="row mt-4">
            <div class="col-12">
                <div class="rd-section-label">Documentación base del lote</div>
                
                <!-- Panel de Ayuda de Documentos -->
                <asp:Panel ID="pnlAyudaDocumentosBase" runat="server" CssClass="rd-notice mb-3">
                    <asp:Label ID="lblAyudaDocumentosBase" runat="server" Text="Seleccione el tipo de operación y ámbito para ver los documentos requeridos."></asp:Label>
                </asp:Panel>

                <div class="row">
                    <!-- FACTURA BASE -->
                    <div class="col-md-6">
                        <asp:Panel ID="pnlFacturaBase" runat="server" CssClass="rd-doc-box mb-3" Visible="false">
                            <div class="rd-doc-title">Datos de factura</div>

                            <div class="form-group mb-2">
                                <label for="<%= txtNumeroFacturaBase.ClientID %>" class="sgv-etiqueta">
                                    <strong>N° Factura:</strong>
                                    <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                </label>
                                <asp:TextBox ID="txtNumeroFacturaBase" runat="server" aria-required="true"
                                    CssClass="form-control"
                                    AutoCompleteType="Disabled" autocomplete="off"
                                    placeholder="Ej: F222-00004267"
                                    MaxLength="30">
                                </asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvNumeroFacturaBase" runat="server"
                                    ControlToValidate="txtNumeroFacturaBase"
                                    ErrorMessage="El número de factura es obligatorio"
                                    CssClass="sgv-campo-error"
                                    Display="Dynamic"
                                    SetFocusOnError="true"
                                    ValidationGroup="ConfiguracionBase" />
                                <asp:RegularExpressionValidator ID="revNumeroFacturaBase" runat="server"
                                    ControlToValidate="txtNumeroFacturaBase"
                                    ValidationExpression="^[A-Za-z0-9\-/]{3,30}$"
                                    ErrorMessage="El N° de factura solo permite letras, números, guion o slash (3-30 caracteres)"
                                    CssClass="sgv-campo-error"
                                    Display="Dynamic"
                                    SetFocusOnError="true"
                                    ValidationGroup="ConfiguracionBase" />
                            </div>

                            <div class="form-group mb-2">
                                <label for="<%= txtFechaEmisionFacturaBase.ClientID %>" class="sgv-etiqueta">
                                    <strong>Fecha emisión:</strong>
                                    <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                </label>
                                <asp:TextBox ID="txtFechaEmisionFacturaBase" runat="server" aria-required="true"
                                    CssClass="form-control"
                                    TextMode="Date">
                                </asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvFechaEmisionFacturaBase" runat="server"
                                    ControlToValidate="txtFechaEmisionFacturaBase"
                                    ErrorMessage="La fecha de emisión de la factura es obligatoria"
                                    CssClass="sgv-campo-error"
                                    Display="Dynamic"
                                    SetFocusOnError="true"
                                    ValidationGroup="ConfiguracionBase" />
                                <asp:CustomValidator ID="cvFechaEmisionFacturaBase" runat="server"
                                    ControlToValidate="txtFechaEmisionFacturaBase"
                                    ErrorMessage="La fecha de emisión de factura no es válida"
                                    CssClass="sgv-campo-error"
                                    Display="Dynamic"
                                    SetFocusOnError="true"
                                    ClientValidationFunction="validarFechaEmisionCliente"
                                    OnServerValidate="cvFechaEmisionFacturaBase_ServerValidate"
                                    ValidationGroup="ConfiguracionBase" />
                            </div>

                            <div class="form-group mb-2">
                                <label for="<%= txtValorTotalFacturaBase.ClientID %>" class="sgv-etiqueta">
                                    <strong>Valor total:</strong>
                                    <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                </label>
                                <div class="input-group">
                                    <div class="input-group-prepend">
                                        <asp:DropDownList ID="ddlMonedaFacturaBase" runat="server" CssClass="custom-select rd-moneda" ToolTip="Moneda de la factura">
                                            <asp:ListItem Value="PEN" Text="S/" Selected="True"></asp:ListItem>
                                            <asp:ListItem Value="USD" Text="US$"></asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                    <asp:TextBox ID="txtValorTotalFacturaBase" runat="server" aria-required="true"
                                        CssClass="form-control"
                                        placeholder="0.00"
                                        inputmode="decimal" autocomplete="off" data-monto="true">
                                    </asp:TextBox>
                                </div>
                                <asp:RequiredFieldValidator ID="rfvValorTotalFacturaBase" runat="server"
                                    ControlToValidate="txtValorTotalFacturaBase"
                                    ErrorMessage="El valor total de la factura es obligatorio"
                                    CssClass="sgv-campo-error"
                                    Display="Dynamic"
                                    SetFocusOnError="true"
                                    ValidationGroup="ConfiguracionBase" />
                                <asp:RangeValidator ID="rvValorTotalFacturaBase" runat="server"
                                    ControlToValidate="txtValorTotalFacturaBase"
                                    MinimumValue="0.01"
                                    MaximumValue="9999999"
                                    Type="Double"
                                    CultureInvariantValues="true"
                                    ErrorMessage="Ingrese un monto mayor a 0 y menor a 10 millones."
                                    CssClass="sgv-campo-error"
                                    Display="Dynamic" SetFocusOnError="true"
                                    ValidationGroup="ConfiguracionBase" />
                            </div>

                            <div class="form-group mb-0">
                                <label for="<%= fileFacturaBase.ClientID %>" class="sgv-etiqueta">
                                    <strong>Adjuntar documento:</strong>
                                    <span class="text-muted small">(Opcional)</span>
                                </label>
                                <asp:FileUpload ID="fileFacturaBase" runat="server" CssClass="form-control" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png" data-max-mb="10" data-max-envio-mb="24" />
                                <small class="form-text text-muted">PDF, DOC, DOCX, JPG o PNG. Máx. 10 MB.</small>
                            </div>
                        </asp:Panel>
                    </div>

                    <!-- CPIC BASE -->
                    <div class="col-md-6">
                        <asp:Panel ID="pnlCPICBase" runat="server" CssClass="rd-doc-box mb-3" Visible="false">
                            <div class="rd-doc-title">Datos de CPIC</div>

                            <div class="form-group mb-2">
                                <label for="<%= txtNumeroCPICBase.ClientID %>" class="sgv-etiqueta">
                                    <strong>N° CPIC:</strong>
                                    <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                    <span class="text-muted small rd-opcional">(Opcional: si llega después, agréguelo en Lista de despachos)</span>
                                </label>
                                <%-- aria-required de los tres campos de la CPIC: lo pone OnPreRender (opcional en la descarga internacional). --%>
                                <asp:TextBox ID="txtNumeroCPICBase" runat="server"
                                    CssClass="form-control"
                                    AutoCompleteType="Disabled" autocomplete="off"
                                    placeholder="Ej: 1234567"
                                    MaxLength="10">
                                </asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvNumeroCPICBase" runat="server"
                                    ControlToValidate="txtNumeroCPICBase"
                                    ErrorMessage="El número de CPIC es obligatorio"
                                    CssClass="sgv-campo-error"
                                    Display="Dynamic"
                                    SetFocusOnError="true"
                                    ValidationGroup="ConfiguracionBase" />
                                <asp:RegularExpressionValidator ID="revNumeroCPICBase" runat="server"
                                    ControlToValidate="txtNumeroCPICBase"
                                    ValidationExpression="^[A-Za-z0-9\-/]{3,20}$"
                                    ErrorMessage="El N° de CPIC solo permite letras, números, guion o slash (3-20 caracteres)"
                                    CssClass="sgv-campo-error"
                                    Display="Dynamic"
                                    SetFocusOnError="true"
                                    ValidationGroup="ConfiguracionBase" />
                            </div>

                            <div class="form-group mb-2">
                                <label for="<%= txtFechaEmisionCPICBase.ClientID %>" class="sgv-etiqueta">
                                    <strong>Fecha emisión CPIC:</strong>
                                    <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                </label>
                                <asp:TextBox ID="txtFechaEmisionCPICBase" runat="server"
                                    CssClass="form-control"
                                    TextMode="Date">
                                </asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvFechaEmisionCPICBase" runat="server"
                                    ControlToValidate="txtFechaEmisionCPICBase"
                                    ErrorMessage="La fecha de emisión del CPIC es obligatoria"
                                    CssClass="sgv-campo-error"
                                    Display="Dynamic"
                                    SetFocusOnError="true"
                                    ValidationGroup="ConfiguracionBase" />
                                <asp:CustomValidator ID="cvFechaEmisionCPICBase" runat="server"
                                    ControlToValidate="txtFechaEmisionCPICBase"
                                    ErrorMessage="La fecha de emisión de CPIC no es válida"
                                    CssClass="sgv-campo-error"
                                    Display="Dynamic"
                                    SetFocusOnError="true"
                                    ClientValidationFunction="validarFechaEmisionCliente"
                                    OnServerValidate="cvFechaEmisionCPICBase_ServerValidate"
                                    ValidationGroup="ConfiguracionBase" />
                            </div>

                            <div class="form-group mb-2">
                                <label for="<%= txtValorFleteBase.ClientID %>" class="sgv-etiqueta">
                                    <strong>Valor flete (S/):</strong>
                                    <span class="sgv-obligatorio" aria-hidden="true">*</span>
                                </label>
                                <asp:TextBox ID="txtValorFleteBase" runat="server"
                                    CssClass="form-control"
                                    placeholder="0.00"
                                    inputmode="decimal" autocomplete="off" data-monto="true">
                                </asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvValorFleteBase" runat="server"
                                    ControlToValidate="txtValorFleteBase"
                                    ErrorMessage="El valor del flete es obligatorio"
                                    CssClass="sgv-campo-error"
                                    Display="Dynamic"
                                    SetFocusOnError="true"
                                    ValidationGroup="ConfiguracionBase" />
                                <asp:RangeValidator ID="rvValorFleteBase" runat="server"
                                    ControlToValidate="txtValorFleteBase"
                                    MinimumValue="0.01"
                                    MaximumValue="9999999"
                                    Type="Double"
                                    CultureInvariantValues="true"
                                    ErrorMessage="Ingrese un monto mayor a 0 y menor a 10 millones."
                                    CssClass="sgv-campo-error"
                                    Display="Dynamic" SetFocusOnError="true"
                                    ValidationGroup="ConfiguracionBase" />
                            </div>

                            <div class="form-group mb-0">
                                <label for="<%= fileCPICBase.ClientID %>" class="sgv-etiqueta">
                                    <strong>Adjuntar documento:</strong>
                                    <span class="text-muted small">(Opcional)</span>
                                </label>
                                <asp:FileUpload ID="fileCPICBase" runat="server" CssClass="form-control" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png" data-max-mb="10" data-max-envio-mb="24" />
                                <small class="form-text text-muted">PDF, DOC, DOCX, JPG o PNG. Máx. 10 MB.</small>
                            </div>
                        </asp:Panel>
                    </div>
                </div>
            </div>
        </div>

        <!-- Botón para iniciar lote -->
        <div class="row">
            <div class="col-md-12">
                <div class="d-flex justify-content-end rd-grupo">
                    <asp:Button ID="btnIniciarLote" runat="server" 
                        Text="Iniciar lote de despachos" 
                        CssClass="btn btn-primary rd-btn-leer"
                        OnClick="btnIniciarLote_Click"
                        data-espera="300"
                        ValidationGroup="ConfiguracionBase" />
                </div>
            </div>
        </div>
    </div>
</div>

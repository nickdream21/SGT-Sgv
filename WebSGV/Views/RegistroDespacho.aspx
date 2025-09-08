<%@ Page Title="Registro de Despachos" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroDespacho.aspx.cs" Inherits="WebSGV.Views.RegistroDespacho" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="container-fluid">
        <div class="row">
            <div class="col-md-12">
                <div class="card">
                    <div class="card-header bg-primary text-white">
                        <h4 class="mb-0">
                            <i class="fas fa-truck"></i> Registro de Despachos
                        </h4>
                    </div>
                    <div class="card-body">
                        <asp:UpdatePanel ID="UpdatePanelMain" runat="server" UpdateMode="Conditional">
                            <ContentTemplate>
                                
                                <!-- Mensajes -->
                                <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-3">
                                    <asp:Label ID="lblMensaje" runat="server" CssClass="alert"></asp:Label>
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
                                                AppendDataBoundItems="true">
                                                <asp:ListItem Value="0" Text="-- Seleccione un conductor --"></asp:ListItem>
                                            </asp:DropDownList>
                                            <asp:RequiredFieldValidator ID="rfvConductor" runat="server"
                                                ControlToValidate="ddlConductor"
                                                InitialValue="0"
                                                ErrorMessage="Debe seleccionar un conductor"
                                                CssClass="text-danger small"
                                                Display="Dynamic">
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
                                                Display="Dynamic">
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
                                                Display="Dynamic">
                                            </asp:RequiredFieldValidator>
                                        </div>
                                    </div>

                                    <!-- Columna Derecha -->
                                    <div class="col-md-6">
                                        
                                        <!-- Fecha de Despacho -->
                                        <div class="form-group mb-3">
                                            <label for="txtFechaDespacho" class="form-label">
                                                <strong>Fecha de Despacho:</strong>
                                                <span class="text-danger">*</span>
                                            </label>
                                            <asp:TextBox ID="txtFechaDespacho" runat="server" 
                                                CssClass="form-control" 
                                                TextMode="Date">
                                            </asp:TextBox>
                                            <asp:RequiredFieldValidator ID="rfvFechaDespacho" runat="server"
                                                ControlToValidate="txtFechaDespacho"
                                                ErrorMessage="Debe seleccionar una fecha de despacho"
                                                CssClass="text-danger small"
                                                Display="Dynamic">
                                            </asp:RequiredFieldValidator>
                                            <asp:CompareValidator ID="cvFechaDespacho" runat="server"
                                                ControlToValidate="txtFechaDespacho"
                                                Type="Date"
                                                Operator="DataTypeCheck"
                                                ErrorMessage="Debe ingresar una fecha válida"
                                                CssClass="text-danger small"
                                                Display="Dynamic">
                                            </asp:CompareValidator>
                                        </div>

                                        <!-- Lugar de Operación -->
                                        <div class="form-group mb-3">
                                            <label for="ddlLugarOperacion" class="form-label">
                                                <strong>Lugar de Operación:</strong>
                                                <span class="text-danger">*</span>
                                            </label>
                                            <asp:DropDownList ID="ddlLugarOperacion" runat="server" CssClass="form-select">
                                                <asp:ListItem Value="" Text="-- Seleccione un lugar --"></asp:ListItem>
                                                <asp:ListItem Value="Lima" Text="Lima"></asp:ListItem>
                                                <asp:ListItem Value="Guayaquil" Text="Guayaquil"></asp:ListItem>
                                                <asp:ListItem Value="Trujillo" Text="Trujillo"></asp:ListItem>
                                                <asp:ListItem Value="Quito" Text="Quito"></asp:ListItem>
                                                <asp:ListItem Value="Chiclayo" Text="Chiclayo"></asp:ListItem>
                                                <asp:ListItem Value="Manta" Text="Manta"></asp:ListItem>
                                            </asp:DropDownList>
                                            <asp:RequiredFieldValidator ID="rfvLugarOperacion" runat="server"
                                                ControlToValidate="ddlLugarOperacion"
                                                InitialValue=""
                                                ErrorMessage="Debe seleccionar un lugar de operación"
                                                CssClass="text-danger small"
                                                Display="Dynamic">
                                            </asp:RequiredFieldValidator>
                                        </div>

                                        <!-- Tipo de Operación -->
                                        <div class="form-group mb-3">
                                            <label for="ddlTipoOperacion" class="form-label">
                                                <strong>Tipo de Operación:</strong>
                                                <span class="text-danger">*</span>
                                            </label>
                                            <asp:DropDownList ID="ddlTipoOperacion" runat="server" CssClass="form-select">
                                                <asp:ListItem Value="" Text="-- Seleccione tipo de operación --"></asp:ListItem>
                                                <asp:ListItem Value="CARGA" Text="Carga"></asp:ListItem>
                                                <asp:ListItem Value="DESCARGA" Text="Descarga"></asp:ListItem>
                                            </asp:DropDownList>
                                            <asp:RequiredFieldValidator ID="rfvTipoOperacion" runat="server"
                                                ControlToValidate="ddlTipoOperacion"
                                                InitialValue=""
                                                ErrorMessage="Debe seleccionar un tipo de operación"
                                                CssClass="text-danger small"
                                                Display="Dynamic">
                                            </asp:RequiredFieldValidator>
                                        </div>

                                        <!-- Cliente -->
                                        <div class="form-group mb-3">
                                            <label for="ddlCliente" class="form-label">
                                                <strong>Cliente:</strong>
                                                <span class="text-danger">*</span>
                                            </label>
                                            <asp:DropDownList ID="ddlCliente" runat="server" 
                                                CssClass="form-select"
                                                DataTextField="nombre"
                                                DataValueField="idCliente"
                                                AppendDataBoundItems="true">
                                                <asp:ListItem Value="0" Text="-- Seleccione un cliente --"></asp:ListItem>
                                            </asp:DropDownList>
                                            <asp:RequiredFieldValidator ID="rfvCliente" runat="server"
                                                ControlToValidate="ddlCliente"
                                                InitialValue="0"
                                                ErrorMessage="Debe seleccionar un cliente"
                                                CssClass="text-danger small"
                                                Display="Dynamic">
                                            </asp:RequiredFieldValidator>
                                        </div>
                                    </div>
                                </div>

                                <!-- Botones -->
                                <div class="row">
                                    <div class="col-md-12">
                                        <div class="d-flex justify-content-end gap-2">
                                            <asp:Button ID="btnLimpiar" runat="server" 
                                                Text="Limpiar" 
                                                CssClass="btn btn-secondary"
                                                CausesValidation="false"
                                                OnClick="btnLimpiar_Click" />
                                            
                                            <asp:Button ID="btnGuardar" runat="server" 
                                                Text="Guardar Despacho" 
                                                CssClass="btn btn-success"
                                                OnClick="btnGuardar_Click" />
                                        </div>
                                    </div>
                                </div>

                            </ContentTemplate>
                            <Triggers>
                                <asp:AsyncPostBackTrigger ControlID="btnGuardar" EventName="Click" />
                                <asp:AsyncPostBackTrigger ControlID="btnLimpiar" EventName="Click" />
                            </Triggers>
                        </asp:UpdatePanel>
                    </div>
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
            // Establecer fecha actual por defecto
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

            var fechaInput = document.getElementById('<%= txtFechaDespacho.ClientID %>');
            if (fechaInput && fechaInput.value === '') {
                fechaInput.value = todayString;
            }
        }

        // Re-inicializar Select2 después de un postback parcial del UpdatePanel
        var prm = Sys.WebForms.PageRequestManager.getInstance();
        prm.add_endRequest(function () {
            initializeSelect2();
            setDefaultDate();
        });
    </script>
    <style>
        .card {
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            border: none;
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

        /* Estilo específico para el campo de fecha */
        input[type="date"] {
            position: relative;
        }

        input[type="date"]::-webkit-calendar-picker-indicator {
            position: absolute;
            right: 10px;
            color: #6c757d;
            cursor: pointer;
        }

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
            }
        }
    </style>
</asp:Content>
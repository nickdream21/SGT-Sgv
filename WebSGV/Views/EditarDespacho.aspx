<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="EditarDespacho.aspx.cs" Inherits="WebSGV.Views.EditarDespacho" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <!-- Select2 CSS -->
    <link href="https://cdnjs.cloudflare.com/ajax/libs/select2/4.0.13/css/select2.min.css" rel="stylesheet" />
    
    <style>
        .main-container {
            max-width: 1200px;
            margin: 0 auto;
        }

        .page-header {
            margin-bottom: 40px;
            text-align: center;
        }

        .page-title {
            color: #1e293b;
            font-size: 2.2rem;
            font-weight: 700;
            margin-bottom: 12px;
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 16px;
        }

        .page-subtitle {
            color: #64748b;
            font-size: 1.15rem;
            font-weight: 400;
        }

        .form-card {
            background: white;
            border-radius: 16px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.06);
            border: 1px solid #e2e8f0;
            margin-bottom: 32px;
            overflow: hidden;
        }

        .form-header {
            background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%);
            color: white;
            padding: 24px 32px;
            font-weight: 600;
            font-size: 1.15rem;
            display: flex;
            align-items: center;
            gap: 12px;
        }

        .form-body {
            padding: 32px;
            background: #fafbfc;
        }

        .form-section {
            background: white;
            border-radius: 12px;
            padding: 28px;
            margin-bottom: 24px;
            border: 1px solid #e2e8f0;
            box-shadow: 0 2px 8px rgba(0,0,0,0.02);
        }

        .form-section-title {
            color: #374151;
            font-size: 1.1rem;
            font-weight: 600;
            margin-bottom: 24px;
            padding-bottom: 12px;
            border-bottom: 1px solid #f1f5f9;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .form-section-title i {
            color: #2563eb;
            font-size: 1.2rem;
        }

        .field-label {
            color: #374151;
            font-weight: 600;
            font-size: 0.9rem;
            margin-bottom: 8px;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            display: block;
        }

        .required-field {
            color: #dc2626;
        }

        .form-control, .form-select {
            border: 2px solid #e2e8f0;
            border-radius: 8px;
            padding: 12px 16px;
            font-size: 0.95rem;
            transition: all 0.3s ease;
            background: white;
            min-height: 48px;
            width: 100%;
        }

        .form-control:focus, .form-select:focus {
            border-color: #2563eb;
            box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
            outline: none;
        }

        .form-field {
            margin-bottom: 24px;
        }

        .buttons-section {
            background: white;
            border-radius: 12px;
            padding: 24px 28px;
            border: 1px solid #e2e8f0;
            box-shadow: 0 2px 8px rgba(0,0,0,0.02);
            display: flex;
            justify-content: center;
            gap: 16px;
            flex-wrap: wrap;
        }

        .btn-save {
            background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%);
            border: none;
            padding: 14px 32px;
            font-weight: 600;
            border-radius: 8px;
            color: white !important;
            font-size: 0.95rem;
            transition: all 0.3s ease;
            min-width: 160px;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            gap: 8px;
            text-decoration: none;
            cursor: pointer;
        }

        .btn-save:hover {
            background: linear-gradient(135deg, #1d4ed8 0%, #1e40af 100%);
            transform: translateY(-2px);
            box-shadow: 0 8px 25px rgba(37, 99, 235, 0.3);
            color: white !important;
            text-decoration: none;
        }

        .btn-cancel {
            background: #64748b;
            border: none;
            padding: 14px 32px;
            font-weight: 600;
            border-radius: 8px;
            color: white !important;
            font-size: 0.95rem;
            transition: all 0.3s ease;
            min-width: 160px;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            gap: 8px;
            text-decoration: none;
            cursor: pointer;
        }

        .btn-cancel:hover {
            background: #475569;
            color: white !important;
            transform: translateY(-2px);
            box-shadow: 0 6px 20px rgba(100, 116, 139, 0.3);
            text-decoration: none;
        }

        /* Select2 Custom Styles - SOLO para los 4 campos específicos */
        .select2-container {
            width: 100% !important;
        }

        .select2-container--default .select2-selection--single {
            border: 2px solid #e2e8f0 !important;
            border-radius: 8px !important;
            min-height: 48px !important;
            padding: 8px 12px !important;
            font-size: 0.95rem !important;
            transition: all 0.3s ease !important;
        }

        .select2-container--default .select2-selection--single:focus {
            border-color: #2563eb !important;
            box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1) !important;
            outline: none !important;
        }

        .select2-container--default.select2-container--focus .select2-selection--single {
            border-color: #2563eb !important;
            box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1) !important;
        }

        .select2-container--default .select2-selection--single .select2-selection__rendered {
            color: #374151 !important;
            line-height: 32px !important;
            padding-left: 8px !important;
        }

        .select2-container--default .select2-selection--single .select2-selection__arrow {
            height: 44px !important;
            right: 8px !important;
        }

        .select2-dropdown {
            border: 2px solid #e2e8f0 !important;
            border-radius: 8px !important;
            box-shadow: 0 10px 40px rgba(0,0,0,0.1) !important;
        }

        .select2-container--default .select2-results__option--highlighted[aria-selected] {
            background-color: #2563eb !important;
        }

        .select2-search--dropdown .select2-search__field {
            border: 1px solid #e2e8f0 !important;
            border-radius: 6px !important;
            padding: 8px 12px !important;
            font-size: 0.9rem !important;
        }

        .select2-results__option {
            padding: 12px 16px !important;
            font-size: 0.9rem !important;
        }

        /* RESPONSIVE */
        @media (max-width: 992px) {
            .form-body {
                padding: 24px;
            }

            .form-section {
                padding: 20px;
            }

            .page-title {
                font-size: 1.8rem;
            }
        }

        @media (max-width: 768px) {
            .form-body {
                padding: 20px;
            }

            .form-section {
                padding: 16px;
                margin-bottom: 20px;
            }

            .buttons-section {
                padding: 16px;
                flex-direction: column;
            }

            .btn-save, .btn-cancel {
                width: 100%;
                justify-content: center;
            }

            .page-title {
                font-size: 1.6rem;
                flex-direction: column;
                gap: 8px;
            }
        }
    </style>

    <div class="container-fluid">
        <div class="main-container">

            <!-- Header -->
            <div class="page-header">
                <h1 class="page-title">
                    <i class="fas fa-edit"></i>
                    Editar Despacho
                </h1>
                <p class="page-subtitle">Modifica los datos del despacho</p>
            </div>

            <!-- Formulario -->
            <div class="form-card">
                <div class="form-header">
                    <i class="fas fa-edit"></i>
                    Datos del Despacho
                </div>
                <div class="form-body">

                    <!-- Datos Principales -->
                    <div class="form-section">
                        <div class="form-section-title">
                            <i class="fas fa-calendar-check"></i>
                            Información del Despacho
                        </div>
                        <div class="row">
                            <div class="col-lg-4 col-md-6">
                                <div class="form-field">
                                    <label class="field-label">Fecha de Despacho <span class="required-field">*</span></label>
                                    <asp:TextBox ID="txtFechaDespacho" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                </div>
                            </div>

                            <div class="col-lg-4 col-md-6">
                                <div class="form-field">
                                    <label class="field-label">Lugar de Operación <span class="required-field">*</span></label>
                                    <asp:DropDownList ID="ddlLugar" runat="server" CssClass="form-select">
                                        <asp:ListItem Value="">-- Seleccionar Lugar --</asp:ListItem>
                                        <asp:ListItem Value="TRUJILLO">Trujillo</asp:ListItem>
                                        <asp:ListItem Value="GUAYAQUIL">Guayaquil</asp:ListItem>
                                        <asp:ListItem Value="LIMA">Lima</asp:ListItem>
                                        <asp:ListItem Value="QUITO">Quito</asp:ListItem>
                                        <asp:ListItem Value="PIURA">Piura</asp:ListItem>
                                        <asp:ListItem Value="CHICLAYO">Chiclayo</asp:ListItem>
                                        <asp:ListItem Value="MACHALA">Machala</asp:ListItem>
                                    </asp:DropDownList>
                                </div>
                            </div>

                            <div class="col-lg-4 col-md-6">
                                <div class="form-field">
                                    <label class="field-label">Tipo de Operación <span class="required-field">*</span></label>
                                    <asp:DropDownList ID="ddlTipoOperacion" runat="server" CssClass="form-select">
                                        <asp:ListItem Value="">-- Seleccionar Operación --</asp:ListItem>
                                        <asp:ListItem Value="CARGA">Carga</asp:ListItem>
                                        <asp:ListItem Value="DESCARGA">Descarga</asp:ListItem>
                                        <asp:ListItem Value="CARGA_DESCARGA">Carga y Descarga</asp:ListItem>
                                        <asp:ListItem Value="TRANSITO">Tránsito</asp:ListItem>
                                    </asp:DropDownList>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Recursos Asignados -->
                    <div class="form-section">
                        <div class="form-section-title">
                            <i class="fas fa-users-cog"></i>
                            Asignaciones
                        </div>
                        <div class="row">
                            <div class="col-lg-6 col-md-12">
                                <div class="form-field">
                                    <label class="field-label">Conductor <span class="required-field">*</span></label>
                                    <asp:DropDownList ID="ddlConductor" runat="server" CssClass="form-select searchable-select">
                                    </asp:DropDownList>
                                </div>
                            </div>

                            <div class="col-lg-6 col-md-12">
                                <div class="form-field">
                                    <label class="field-label">Cliente <span class="required-field">*</span></label>
                                    <asp:DropDownList ID="ddlCliente" runat="server" CssClass="form-select searchable-select">
                                    </asp:DropDownList>
                                </div>
                            </div>
                        </div>

                        <div class="row">
                            <div class="col-lg-6 col-md-12">
                                <div class="form-field">
                                    <label class="field-label">Tracto <span class="required-field">*</span></label>
                                    <asp:DropDownList ID="ddlTracto" runat="server" CssClass="form-select searchable-select">
                                    </asp:DropDownList>
                                </div>
                            </div>

                            <div class="col-lg-6 col-md-12">
                                <div class="form-field">
                                    <label class="field-label">Carreta <span class="required-field">*</span></label>
                                    <asp:DropDownList ID="ddlCarreta" runat="server" CssClass="form-select searchable-select">
                                    </asp:DropDownList>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Botones de Acción -->
                    <div class="buttons-section">
                        <asp:Button ID="btnGuardar" runat="server" CssClass="btn-save"
                            Text="Guardar Cambios" OnClick="btnGuardar_Click" />

                        <asp:Button ID="btnCancelar" runat="server" CssClass="btn-cancel"
                            Text="Cancelar" OnClick="btnCancelar_Click" CausesValidation="false" />
                    </div>

                </div>
            </div>

            <!-- Panel de Mensajes -->
            <asp:Panel ID="pnlMensaje" runat="server" Visible="false" CssClass="mt-4">
                <div class="alert alert-dismissible fade show" role="alert" id="divMensaje" runat="server"
                    style="border-radius: 12px; box-shadow: 0 6px 25px rgba(0,0,0,0.1);">
                    <asp:Literal ID="litMensaje" runat="server"></asp:Literal>
                    <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
                </div>
            </asp:Panel>

        </div>
    </div>

    <!-- Select2 JavaScript -->
    <script src="https://cdnjs.cloudflare.com/ajax/libs/select2/4.0.13/js/select2.min.js"></script>

    <script>
        document.addEventListener('DOMContentLoaded', function () {
            // Configurar Select2 SOLO para los 4 campos específicos
            $('.searchable-select').select2({
                placeholder: function () {
                    return $(this).find('option').first().text();
                },
                allowClear: false,
                width: '100%',
                language: {
                    noResults: function () {
                        return "No se encontraron resultados";
                    },
                    searching: function () {
                        return "Buscando...";
                    },
                    inputTooShort: function () {
                        return "Escribe para buscar";
                    }
                },
                templateResult: function (option) {
                    if (!option.id) {
                        return option.text;
                    }

                    // Resaltar texto coincidente
                    var term = $('.select2-search__field').val();
                    if (term && term.length > 0) {
                        var regex = new RegExp('(' + term + ')', 'gi');
                        var highlighted = option.text.replace(regex, '<strong>$1</strong>');
                        return $('<span>' + highlighted + '</span>');
                    }

                    return option.text;
                },
                escapeMarkup: function (markup) {
                    return markup;
                }
            });
        });

        // Auto-hide alerts
        document.addEventListener('DOMContentLoaded', function () {
            setTimeout(function () {
                const alerts = document.querySelectorAll('.alert');
                alerts.forEach(function (alert) {
                    alert.style.transition = 'opacity 0.5s';
                    alert.style.opacity = '0';
                    setTimeout(function () {
                        alert.style.display = 'none';
                    }, 500);
                });
            }, 8000);
        });

        // Confirmación antes de cancelar si hay cambios
        document.addEventListener('DOMContentLoaded', function () {
            const form = document.querySelector('form');
            const cancelBtn = document.getElementById('<%= btnCancelar.ClientID %>');
            let formChanged = false;

            // Detectar cambios en el formulario
            const inputs = form.querySelectorAll('input, select, textarea');
            inputs.forEach(function (input) {
                input.addEventListener('change', function () {
                    formChanged = true;
                });
            });

            // Detectar cambios en Select2
            $('.searchable-select').on('change', function () {
                formChanged = true;
            });

            // Confirmar cancelación si hay cambios
            if (cancelBtn) {
                cancelBtn.addEventListener('click', function (e) {
                    if (formChanged) {
                        if (!confirm('¿Está seguro de que desea cancelar? Se perderán los cambios no guardados.')) {
                            e.preventDefault();
                            return false;
                        }
                    }
                });
            }
        });

        // Validaciones en el cliente
        function validarFormulario() {
            let esValido = true;
            let mensajes = [];

            // Validar fecha
            const fecha = document.getElementById('<%= txtFechaDespacho.ClientID %>').value;
            if (!fecha) {
                esValido = false;
                mensajes.push('La fecha de despacho es obligatoria');
            }

            // Validar conductor
            const conductor = $('#<%= ddlConductor.ClientID %>').val();
            if (!conductor) {
                esValido = false;
                mensajes.push('Debe seleccionar un conductor');
            }

            // Validar cliente
            const cliente = $('#<%= ddlCliente.ClientID %>').val();
            if (!cliente) {
                esValido = false;
                mensajes.push('Debe seleccionar un cliente');
            }

            // Validar tracto
            const tracto = $('#<%= ddlTracto.ClientID %>').val();
            if (!tracto) {
                esValido = false;
                mensajes.push('Debe seleccionar un tracto');
            }

            // Validar carreta
            const carreta = $('#<%= ddlCarreta.ClientID %>').val();
            if (!carreta) {
                esValido = false;
                mensajes.push('Debe seleccionar una carreta');
            }

            // Validar lugar
            const lugar = document.getElementById('<%= ddlLugar.ClientID %>').value;
            if (!lugar) {
                esValido = false;
                mensajes.push('Debe seleccionar un lugar de operación');
            }

            // Validar tipo de operación
            const tipoOperacion = document.getElementById('<%= ddlTipoOperacion.ClientID %>').value;
            if (!tipoOperacion) {
                esValido = false;
                mensajes.push('Debe seleccionar un tipo de operación');
            }

            if (!esValido) {
                alert('Por favor, corrija los siguientes errores:\n\n• ' + mensajes.join('\n• '));
            }

            return esValido;
        }

        // Asociar validación al botón guardar
        document.addEventListener('DOMContentLoaded', function () {
            const saveBtn = document.getElementById('<%= btnGuardar.ClientID %>');
            if (saveBtn) {
                saveBtn.addEventListener('click', function (e) {
                    if (!validarFormulario()) {
                        e.preventDefault();
                        return false;
                    }
                });
            }
        });
    </script>
</asp:Content>
<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="EditarDespacho.aspx.cs" Inherits="WebSGV.Views.EditarDespacho" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <!-- Select2 CSS -->
    <link href="https://cdnjs.cloudflare.com/ajax/libs/select2/4.0.13/css/select2.min.css" rel="stylesheet" />
    
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/EditarDespacho.css") %>" rel="stylesheet" />

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
                                    <%-- Opciones cargadas desde el catálogo Planta en CargarLugares(). --%>
                                    <asp:DropDownList ID="ddlLugar" runat="server" CssClass="form-select">
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
                    <button type="button" class="close" data-dismiss="alert" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>
                </div>
            </asp:Panel>

        </div>
    </div>

    <!-- Select2 JavaScript -->
    <script src="https://cdnjs.cloudflare.com/ajax/libs/select2/4.0.13/js/select2.min.js"></script>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            btnCancelar: '<%= btnCancelar.ClientID %>',
            txtFechaDespacho: '<%= txtFechaDespacho.ClientID %>',
            ddlConductor: '<%= ddlConductor.ClientID %>',
            ddlCliente: '<%= ddlCliente.ClientID %>',
            ddlTracto: '<%= ddlTracto.ClientID %>',
            ddlCarreta: '<%= ddlCarreta.ClientID %>',
            ddlLugar: '<%= ddlLugar.ClientID %>',
            ddlTipoOperacion: '<%= ddlTipoOperacion.ClientID %>',
            btnGuardar: '<%= btnGuardar.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/EditarDespacho.js") %>"></script>
</asp:Content>
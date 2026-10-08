<%@ Page Title="Agregar CPIC" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AgregarCPIC.aspx.cs" Inherits="WebSGV.Views.AgregarCPIC" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="main-container agregar-cpic-container">
        <div class="form-container">
            <h1 class="header">Registro de CPIC</h1>

            <!-- Mensajes de estado -->
            <div class="row">
                <div class="col-md-12">
                    <asp:Label ID="lblMensaje" runat="server" CssClass="" Visible="false"></asp:Label>
                </div>
            </div>

            <!-- Campos de Entrada -->
            <div class="row">
                <div class="col-md-6 form-group">
                    <label for="txtNumCPIC">N° CPIC:</label>
                    <asp:TextBox ID="txtNumCPIC" runat="server" CssClass="form-control" placeholder="Ingrese el N° CPIC" MaxLength="7"></asp:TextBox>
                </div> 
                <div class="col-md-6 form-group">
                    <label for="txtNumFactura">N° Factura:</label>
                    <asp:TextBox ID="txtNumFactura" runat="server" CssClass="form-control" AutoPostBack="true" OnTextChanged="TxtNumFactura_TextChanged" placeholder="Ingrese el N° Factura"></asp:TextBox>
                    <asp:Label ID="lblErrorFactura" runat="server" CssClass="text-danger"></asp:Label>
                </div>
            </div>

            <div class="row">
                <div class="col-md-6 form-group">
                    <label for="txtFechaEmision">Fecha de Emisión:</label>
                    <asp:TextBox ID="txtFechaEmision" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                </div>
                <div class="col-md-6 form-group">
                    <label for="txtTotalFlete">Valor Total del Flete:</label>
                    <asp:TextBox ID="txtTotalFlete" runat="server" CssClass="form-control" placeholder="Ingrese el valor total"></asp:TextBox>
                </div>
            </div>

            <!-- NUEVA SECCIÓN: Upload de Documento -->
            <div class="row document-upload-section">
                <div class="col-md-12">
                    <h3><i class="fa fa-file-pdf-o"></i> Documento CPIC</h3>
                    <div class="upload-container">
                        <div class="col-md-8 form-group">
                            <label for="fileUploadCPIC">Adjuntar Documento (PDF recomendado):</label>
                            <asp:FileUpload ID="fileUploadCPIC" runat="server" CssClass="form-control file-input" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png" />
                            <small class="text-muted">
                                <i class="fa fa-info-circle"></i> 
                                Formatos permitidos: PDF, DOC, DOCX, JPG, PNG. Tamaño máximo: 50MB
                            </small>
                        </div>
                        <div class="col-md-4 form-group">
                            <label for="txtDescripcionDoc">Descripción del documento:</label>
                            <asp:TextBox ID="txtDescripcionDoc" runat="server" CssClass="form-control" 
                                placeholder="Ej: CPIC Original, Documento Escaneado" MaxLength="300"></asp:TextBox>
                        </div>
                    </div>
                    
                    <!-- Vista previa del archivo seleccionado -->
                    <!-- Vista previa del archivo seleccionado -->
                    <div id="file-preview" class="file-preview" style="display: none;">
                        <div class="alert alert-info">
                            <i class="fa fa-file"></i>
                            <span id="file-name"></span> 
                            <span id="file-size" class="text-muted"></span>
                            <button type="button" class="btn btn-sm btn-link" onclick="clearFileSelection()">
                                <i class="fa fa-times"></i> Quitar
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            <!-- SECCIÓN DE PRODUCTOS ELIMINADA -->

            <div class="form-group text-center mt-4">
                <asp:Button ID="btnGuardar" runat="server" CssClass="btn btn-primary btn-lg"
                    Text="Guardar CPIC" OnClick="GuardarCPIC" />
            </div>
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            fileUploadCPIC: '<%= fileUploadCPIC.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/AgregarCPIC.js") %>"></script>

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/AgregarCPIC.css") %>" rel="stylesheet" />
</asp:Content>
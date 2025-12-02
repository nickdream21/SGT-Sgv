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
        // Inicializar funcionalidad de upload al cargar la página
        document.addEventListener("DOMContentLoaded", () => {
            initFileUpload(); // Inicializar funcionalidad de upload
        });

        // Inicializar funcionalidad de carga de archivos
        function initFileUpload() {
            const fileInput = document.getElementById('<%= fileUploadCPIC.ClientID %>');

            fileInput.addEventListener('change', function (e) {
                const file = e.target.files[0];
                const preview = document.getElementById('file-preview');
                const fileName = document.getElementById('file-name');
                const fileSize = document.getElementById('file-size');

                if (file) {
                    // Validar tamaño (50MB máximo)
                    const maxSize = 50 * 1024 * 1024; // 50MB en bytes
                    if (file.size > maxSize) {
                        alert('El archivo es demasiado grande. El tamaño máximo permitido es 50MB.');
                        clearFileSelection();
                        return;
                    }

                    // Validar tipo de archivo
                    const allowedTypes = ['.pdf', '.doc', '.docx', '.jpg', '.jpeg', '.png'];
                    const fileExtension = '.' + file.name.split('.').pop().toLowerCase();

                    if (!allowedTypes.includes(fileExtension)) {
                        alert('Tipo de archivo no permitido. Use: PDF, DOC, DOCX, JPG, PNG');
                        clearFileSelection();
                        return;
                    }

                    // Mostrar información del archivo
                    fileName.textContent = file.name;
                    fileSize.textContent = `(${formatFileSize(file.size)})`;
                    preview.style.display = 'block';
                } else {
                    preview.style.display = 'none';
                }
            });
        }

        // Limpiar selección de archivo
        function clearFileSelection() {
            const fileInput = document.getElementById('<%= fileUploadCPIC.ClientID %>');
            const preview = document.getElementById('file-preview');

            fileInput.value = '';
            preview.style.display = 'none';
        }

        // Formatear tamaño de archivo
        function formatFileSize(bytes) {
            if (bytes === 0) return '0 Bytes';

            const k = 1024;
            const sizes = ['Bytes', 'KB', 'MB', 'GB'];
            const i = Math.floor(Math.log(bytes) / Math.log(k));

            return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
        }
    </script>

    <style>
        /* Estilos adicionales para mejorar la apariencia */
        .main-container {
            padding: 20px;
        }

        .form-container {
            background-color: #fff;
            padding: 25px;
            border-radius: 5px;
            box-shadow: 0 2px 5px rgba(0,0,0,0.1);
        }

        .header {
            margin-bottom: 30px;
            color: #337ab7;
            border-bottom: 2px solid #eee;
            padding-bottom: 10px;
        }

        .alert {
            padding: 15px;
            margin-bottom: 20px;
            border: 1px solid transparent;
            border-radius: 4px;
        }

        .alert-success {
            color: #3c763d;
            background-color: #dff0d8;
            border-color: #d6e9c6;
        }

        .alert-danger {
            color: #a94442;
            background-color: #f2dede;
            border-color: #ebccd1;
        }

        .text-danger {
            color: #a94442;
            font-size: 0.9em;
            display: block;
            margin-top: 5px;
        }

        /* Estilos para labels */
        .form-group label {
            font-weight: bold;
            margin-bottom: 5px;
        }

        /* ESTILOS PARA UPLOAD */
        .document-upload-section {
            background-color: #f8f9fa;
            padding: 20px;
            border-radius: 5px;
            margin: 20px 0;
            border: 1px solid #dee2e6;
        }

        .document-upload-section h3 {
            color: #495057;
            margin-bottom: 15px;
            font-size: 1.1em;
        }

        .document-upload-section h3 i {
            color: #dc3545;
            margin-right: 8px;
        }

        .upload-container {
            display: flex;
            align-items: end;
            gap: 15px;
        }

        .file-input {
            border: 2px dashed #ced4da;
            border-radius: 5px;
            padding: 10px;
            transition: border-color 0.3s;
        }

        .file-input:hover {
            border-color: #007bff;
        }

        .file-preview {
            margin-top: 10px;
        }

        .file-preview .alert {
            margin-bottom: 0;
            display: flex;
            align-items: center;
            justify-content: space-between;
        }

        .file-preview .alert i {
            margin-right: 8px;
        }

        .text-muted {
            font-size: 0.875em;
        }

        /* Responsivo para upload */
        @media (max-width: 768px) {
            .upload-container {
                flex-direction: column;
                align-items: stretch;
            }
        }
    </style>
</asp:Content>
<%@ Page Title="Registro de Factura" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AgregarFactura.aspx.cs" Inherits="WebSGV.Views.AgregarFactura" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style type="text/css">
        /* Estilos base y reseteo */
        .main-container {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            color: #333;
            background-color: #fff;
            padding: 0;
        }

        .agregar-factura-container {
            max-width: 1200px;
            margin: 0 auto;
        }

        /* Estilos del encabezado */
        .header {
            font-size: 28px;
            font-weight: 500;
            color: #333;
            margin-bottom: 30px;
            padding-bottom: 10px;
            border-bottom: 1px solid #eaeaea;
        }

        /* Organización del formulario en grid */
        .form-container {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 30px;
            padding: 25px;
            max-width: 1400px;
            margin: 0 auto;
        }
        
        /* Para pantallas pequeñas, una sola columna */
        @media (max-width: 768px) {
            .form-container {
                grid-template-columns: 1fr;
                gap: 20px;
                padding: 15px;
            }
        }

        /* Estilo para grupos de formularios */
        .form-group {
            margin-bottom: 0;
            display: flex;
            flex-direction: column;
        }

        .form-group label {
            display: block;
            font-weight: 500;
            margin-bottom: 8px;
            color: #444;
            font-size: 14px;
            min-height: 20px;
        }

        /* Estilos de los campos de entrada */
        .form-control {
            width: 100%;
            height: 38px;
            padding: 8px 12px;
            border: 1px solid #ddd;
            border-radius: 4px;
            box-sizing: border-box;
            font-size: 14px;
            transition: border-color 0.2s, box-shadow 0.2s;
            background-color: #fafafa;
        }

        .form-control:focus {
            border-color: #2196F3;
            outline: none;
            box-shadow: 0 0 0 2px rgba(33, 150, 243, 0.1);
            background-color: #fff;
        }

        /* Estilo específico para DropDownList */
        select.form-control {
            background-image: url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 20 20'%3e%3cpath stroke='%236b7280' stroke-linecap='round' stroke-linejoin='round' stroke-width='1.5' d='m6 8 4 4 4-4'/%3e%3c/svg%3e");
            background-position: right 8px center;
            background-repeat: no-repeat;
            background-size: 16px 12px;
            padding-right: 40px;
            appearance: none;
            -webkit-appearance: none;
            -moz-appearance: none;
        }

        /* Estilo para el botón */
        .btn {
            background-color: #2196F3;
            color: white;
            border: none;
            border-radius: 5px;
            padding: 12px 30px;
            font-size: 14px;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.3s ease;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            min-width: 140px;
            box-shadow: 0 2px 4px rgba(33, 150, 243, 0.2);
        }

        .btn:hover {
            background-color: #1976D2;
            box-shadow: 0 4px 8px rgba(33, 150, 243, 0.3);
            transform: translateY(-1px);
        }

        .btn:active {
            transform: translateY(0);
            box-shadow: 0 2px 4px rgba(33, 150, 243, 0.2);
        }

        /* Contenedor para el botón */
        .btn-container {
            grid-column: 1 / -1;
            margin-top: 15px;
            display: flex;
            justify-content: flex-start;
        }

        /* Mensajes de error */
        .text-danger {
            color: #f44336;
            font-size: 13px;
            margin-top: 5px;
        }

        .text-success {
            color: #4caf50;
            font-size: 13px;
            margin-top: 5px;
        }

        /* Ajustes para campos específicos */
        input[type="date"] {
            background-color: #fafafa;
        }

        /* Ajustes para el encabezado y controles de página completa */
        .header-container {
            grid-column: 1 / -1;
        }

        /* ESTILOS PARA UPLOAD DE DOCUMENTOS */
        .document-upload-section {
            grid-column: 1 / -1;
            background-color: #f8f9fa;
            padding: 25px;
            border-radius: 6px;
            margin: 10px 0 25px 0;
            border: 1px solid #dee2e6;
            box-shadow: 0 1px 3px rgba(0,0,0,0.1);
        }

        .document-upload-section h3 {
            color: #495057;
            margin-bottom: 20px;
            font-size: 1.1em;
            display: flex;
            align-items: center;
            font-weight: 600;
        }

        .document-upload-section h3 i {
            color: #dc3545;
            margin-right: 10px;
            font-size: 1.2em;
        }

        .upload-container {
            display: grid;
            grid-template-columns: 2fr 1fr;
            gap: 20px;
            align-items: start;
        }

        @media (max-width: 768px) {
            .upload-container {
                grid-template-columns: 1fr;
                gap: 15px;
            }
        }

        .file-input {
            border: 2px dashed #ced4da;
            border-radius: 6px;
            padding: 12px;
            transition: all 0.3s ease;
            background-color: #fafafa;
            min-height: 45px;
        }

        .file-input:hover {
            border-color: #007bff;
            background-color: #fff;
            box-shadow: 0 2px 4px rgba(0,123,255,0.1);
        }

        .file-preview {
            margin-top: 10px;
        }

        .file-preview .alert {
            margin-bottom: 0;
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding: 10px 15px;
            border-radius: 4px;
            background-color: #d1ecf1;
            border: 1px solid #bee5eb;
            color: #0c5460;
        }

        .file-preview .alert i {
            margin-right: 8px;
            color: #17a2b8;
        }

        .text-muted {
            font-size: 0.875em;
            color: #6c757d;
        }

        .btn-link {
            background: none;
            border: none;
            color: #dc3545;
            text-decoration: none;
            padding: 2px 5px;
            font-size: 0.9em;
        }

        .btn-link:hover {
            text-decoration: underline;
        }

        .fa {
            font-family: FontAwesome;
        }

        /* Indicador de campo requerido */
        .required {
            color: #f44336;
            margin-left: 3px;
        }
    </style>

    <div class="main-container agregar-factura-container">
        <div class="form-container">
            <!-- Encabezado - ocupa toda la fila -->
            <div class="header-container">
                <h1 class="header">Registro de Factura</h1>
            </div>
            
            <!-- Primera fila: Cliente y N° Factura -->
            <div class="form-group" style="margin-bottom: 25px;">
                <label for="ddlCliente">Cliente:<span class="required">*</span></label>
                <asp:DropDownList ID="ddlCliente" runat="server" CssClass="form-control">
                    <asp:ListItem Value="">-- Seleccione un Cliente --</asp:ListItem>
                </asp:DropDownList>
                <asp:RequiredFieldValidator ID="rfvCliente" runat="server" 
                    ControlToValidate="ddlCliente" 
                    ErrorMessage="El cliente es requerido" 
                    CssClass="text-danger" 
                    Display="Dynamic">
                </asp:RequiredFieldValidator>
            </div>
            
            <div class="form-group" style="margin-bottom: 25px;">
                <label for="txtNumFactura">N° Factura:<span class="required">*</span></label>
                <asp:TextBox ID="txtNumFactura" runat="server" CssClass="form-control" Placeholder="Ingrese el N° de Factura" MaxLength="50" />
                <asp:RequiredFieldValidator ID="rfvNumFactura" runat="server" 
                    ControlToValidate="txtNumFactura" 
                    ErrorMessage="El número de factura es requerido" 
                    CssClass="text-danger" 
                    Display="Dynamic">
                </asp:RequiredFieldValidator>
            </div>
            
            <!-- Segunda fila: Fecha de Emisión y N° Pedido -->
            <div class="form-group" style="margin-bottom: 25px;">
                <label for="txtFechaEmision">Fecha de Emisión:<span class="required">*</span></label>
                <asp:TextBox ID="txtFechaEmision" runat="server" CssClass="form-control" TextMode="Date" />
                <asp:RequiredFieldValidator ID="rfvFechaEmision" runat="server" 
                    ControlToValidate="txtFechaEmision" 
                    ErrorMessage="La fecha de emisión es requerida" 
                    CssClass="text-danger" 
                    Display="Dynamic">
                </asp:RequiredFieldValidator>
            </div>

            <div class="form-group" style="margin-bottom: 25px;">
                <label for="txtNumPedido">N° Pedido:</label>
                <asp:TextBox ID="txtNumPedido" runat="server" CssClass="form-control" Placeholder="Ingrese el N° de Pedido (10 dígitos)" MaxLength="10" />
            </div>

            <!-- Tercera fila: Importe Total -->
            <div class="form-group" style="margin-bottom: 35px;">
                <label for="txtImporteTotal">Importe Total:<span class="required">*</span></label>
                <asp:TextBox ID="txtImporteTotal" runat="server" CssClass="form-control" Placeholder="Ingrese el Importe Total" />
                <asp:RequiredFieldValidator ID="rfvImporteTotal" runat="server" 
                    ControlToValidate="txtImporteTotal" 
                    ErrorMessage="El importe total es requerido" 
                    CssClass="text-danger" 
                    Display="Dynamic">
                </asp:RequiredFieldValidator>
            </div>

            <!-- Espacio vacío para mantener la grid -->
            <div class="form-group" style="margin-bottom: 35px;"></div>

            <!-- Cuarta fila: Sección de Upload de Documento - ocupa toda la fila -->
            <div class="document-upload-section">
                <h3><i class="fa fa-file-pdf-o"></i> Documento de Factura</h3>
                <div class="upload-container">
                    <div class="form-group">
                        <label for="fileUploadFactura">Adjuntar Documento (PDF recomendado):</label>
                        <asp:FileUpload ID="fileUploadFactura" runat="server" CssClass="form-control file-input" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png" />
                        <small class="text-muted">
                            <i class="fa fa-info-circle"></i> 
                            Formatos permitidos: PDF, DOC, DOCX, JPG, PNG. Tamaño máximo: 50MB
                        </small>
                    </div>
                    <div class="form-group">
                        <label for="txtDescripcionDoc">Descripción del documento:</label>
                        <asp:TextBox ID="txtDescripcionDoc" runat="server" CssClass="form-control" 
                            placeholder="Ej: Factura Original, Documento Escaneado" MaxLength="300"></asp:TextBox>
                    </div>
                </div>
                
                <!-- Vista previa del archivo seleccionado -->
                <div id="file-preview" class="file-preview" style="display: none;">
                    <div class="alert">
                        <div>
                            <i class="fa fa-file"></i>
                            <span id="file-name"></span> 
                            <span id="file-size" class="text-muted"></span>
                        </div>
                        <button type="button" class="btn-link" onclick="clearFileSelection()">
                            <i class="fa fa-times"></i> Quitar
                        </button>
                    </div>
                </div>
            </div>
            
            <!-- Quinta fila: Mensaje de error/éxito - ocupa toda la fila -->
            <div class="mensaje-container">
                <asp:Label ID="lblMensaje" runat="server" CssClass="text-danger"></asp:Label>
            </div>
            
            <!-- Sexta fila: Botón Guardar - ocupa toda la fila -->
            <div class="btn-container">
                <asp:Button ID="btnGuardarFactura" runat="server" CssClass="btn btn-primary" Text="GUARDAR"
                    OnClick="GuardarFactura" />
            </div>
        </div>
    </div>

    <script>
        // Inicializar funcionalidades al cargar la página
        document.addEventListener('DOMContentLoaded', function () {
            // Funcionalidad existente para validación de campos
            setupFormValidation();

            // Nueva funcionalidad para upload de archivos
            setupFileUpload();

            // Establecer fecha actual como valor por defecto
            setDefaultDate();
        });

        // Establecer fecha actual
        function setDefaultDate() {
            const fechaEmision = document.getElementById('<%= txtFechaEmision.ClientID %>');
            if (fechaEmision && !fechaEmision.value) {
                const today = new Date();
                const formattedDate = today.toISOString().split('T')[0];
                fechaEmision.value = formattedDate;
            }
        }

        // Configurar validaciones de formulario
        function setupFormValidation() {
            // Bloquear letras en el campo "Importe Total"
            const inputImporteTotal = document.getElementById('<%= txtImporteTotal.ClientID %>');
            if (inputImporteTotal) {
                inputImporteTotal.addEventListener('keypress', function (e) {
                    const key = e.key;
                    // Permitir números, punto decimal y teclas de control como backspace
                    if (!/[0-9.]/.test(key) && e.keyCode !== 8) {
                        e.preventDefault();
                    }
                });
                inputImporteTotal.addEventListener('input', function () {
                    // Asegurarse de que solo hay un punto decimal y limpiar caracteres inválidos
                    this.value = this.value.replace(/[^0-9.]/g, '');
                    if ((this.value.match(/\./g) || []).length > 1) {
                        this.value = this.value.replace(/\.+$/, '');
                    }
                });
            }

            // Validación para el número de pedido: solo permitir dígitos y limitar a 10
            const inputNumPedido = document.getElementById('<%= txtNumPedido.ClientID %>');
            if (inputNumPedido) {
                inputNumPedido.addEventListener('keypress', function (e) {
                    const key = e.key;
                    // Permitir solo números y teclas de control
                    if (!/[0-9]/.test(key) && e.keyCode !== 8) {
                        e.preventDefault();
                    }
                });
                inputNumPedido.addEventListener('input', function () {
                    // Eliminar caracteres no numéricos
                    this.value = this.value.replace(/[^0-9]/g, '');
                    // Limitar a 10 dígitos
                    if (this.value.length > 10) {
                        this.value = this.value.slice(0, 10);
                    }
                });
            }
        }

        // Configurar funcionalidad de upload de archivos
        function setupFileUpload() {
            const fileInput = document.getElementById('<%= fileUploadFactura.ClientID %>');
            
            if (fileInput) {
                fileInput.addEventListener('change', function(e) {
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
                        if (fileName && fileSize && preview) {
                            fileName.textContent = file.name;
                            fileSize.textContent = `(${formatFileSize(file.size)})`;
                            preview.style.display = 'block';
                        }
                    } else {
                        if (preview) {
                            preview.style.display = 'none';
                        }
                    }
                });
            }
        }

        // Limpiar selección de archivo
        function clearFileSelection() {
            const fileInput = document.getElementById('<%= fileUploadFactura.ClientID %>');
            const preview = document.getElementById('file-preview');

            if (fileInput) {
                fileInput.value = '';
            }
            if (preview) {
                preview.style.display = 'none';
            }
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
</asp:Content>
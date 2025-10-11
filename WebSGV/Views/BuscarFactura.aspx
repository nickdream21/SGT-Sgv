<%@ Page Title="Buscar Factura" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="BuscarFactura.aspx.cs" Inherits="WebSGV.Views.BusquedaFactura" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="main-container buscar-factura-container">
        <div class="form-container">
            <h1 class="header">Búsqueda de Factura</h1>

            <!-- Sección de Búsqueda -->
            <div class="row search-section">
                <div class="col-md-8 form-group">
                    <label for="txtBuscarFactura">N° Factura:</label>
                    <asp:TextBox ID="txtBuscarFactura" runat="server" CssClass="form-control" placeholder="Ingrese el N° de Factura a buscar"></asp:TextBox>
                </div>
                <div class="col-md-4 form-group">
                    <label>&nbsp;</label> <!-- Espacio para alinear con el campo de texto -->
                    <asp:Button ID="btnBuscar" runat="server" CssClass="btn btn-primary form-control" Text="🔍 Buscar" OnClick="BuscarFacturaClick" />
                </div>
            </div>

            <!-- NUEVA SECCIÓN: Tabla de Todas las Facturas -->
            <div class="panel panel-info facturas-panel">
                <div class="panel-heading">
                    <h2><i class="fa fa-list"></i> Lista de Facturas Registradas</h2>
                    <div class="panel-actions">
                        <asp:Button ID="btnRefrescarLista" runat="server" CssClass="btn btn-sm btn-light" 
                            Text="🔄 Refrescar" OnClick="RefrescarListaFacturas" ToolTip="Actualizar lista de facturas" />
                    </div>
                </div>
                <div class="panel-body">
                    <asp:Panel ID="pnlListaFacturas" runat="server">
                        <div class="table-responsive">
                            <asp:GridView ID="gvFacturas" runat="server" AutoGenerateColumns="False" 
                                CssClass="table table-bordered table-hover facturas-table"
                                DataKeyNames="idFactura" OnRowCommand="gvFacturas_RowCommand" 
                                EmptyDataText="No hay facturas registradas en el sistema."
                                AllowPaging="true" PageSize="10" OnPageIndexChanging="gvFacturas_PageIndexChanging">
                                <Columns>
                                    <asp:BoundField DataField="numeroFactura" HeaderText="N° Factura" />
                                    <asp:TemplateField HeaderText="Cliente">
                                        <ItemTemplate>
                                            <div class="cliente-info">
                                                <strong><%# Eval("nombreCliente") %></strong>
                                                <%# !string.IsNullOrEmpty(Eval("rucCliente").ToString()) ? 
                                                    "<br/><small class='text-muted'>RUC: " + Eval("rucCliente") + "</small>" : "" %>
                                            </div>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="Fecha Emisión">
                                        <ItemTemplate>
                                            <span><%# Convert.ToDateTime(Eval("fechaEmision")).ToString("dd/MM/yyyy") %></span>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="N° Pedido">
                                        <ItemTemplate>
                                            <span><%# string.IsNullOrEmpty(Eval("numeroPedido").ToString()) ? 
                                                "<em class='text-muted'>Sin pedido</em>" : Eval("numeroPedido").ToString() %></span>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="Importe Total">
                                        <ItemTemplate>
                                            <span class="importe-total">S/ <%# Convert.ToDecimal(Eval("valorTotal")).ToString("N2") %></span>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="CPIC">
                                        <ItemTemplate>
                                            <div class="cpic-actions">
                                                <asp:Panel ID="pnlSinCPIC" runat="server" Visible='<%# string.IsNullOrEmpty(Eval("numeroCPIC").ToString()) %>'>
                                                    <asp:Button ID="btnCrearCPIC" runat="server" CssClass="btn btn-sm btn-warning" 
                                                        Text="📄 Crear CPIC" CommandName="CrearCPIC" 
                                                        CommandArgument='<%# Eval("idFactura") %>' 
                                                        ToolTip="Crear CPIC para esta factura" />
                                                </asp:Panel>
                                                <asp:Panel ID="pnlConCPIC" runat="server" Visible='<%# !string.IsNullOrEmpty(Eval("numeroCPIC").ToString()) %>'>
                                                    <span class="cpic-numero">
                                                        <i class="fa fa-check-circle text-success"></i> 
                                                        <%# Eval("numeroCPIC") %>
                                                    </span>
                                                </asp:Panel>
                                            </div>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="Acciones">
                                        <ItemTemplate>
                                            <asp:Button ID="btnEditarFactura" runat="server" CssClass="btn btn-sm btn-primary" 
                                                Text="✏️ Editar" CommandName="EditarFactura" 
                                                CommandArgument='<%# Eval("numeroFactura") %>' 
                                                ToolTip="Editar esta factura" />
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                </Columns>
                                <PagerStyle CssClass="pagination-ys" />
                                <EmptyDataTemplate>
                                    <div class="alert alert-info text-center">
                                        <i class="fa fa-info-circle"></i>
                                        <strong>No hay facturas registradas</strong><br />
                                        <a href="AgregarFactura.aspx" class="btn btn-primary btn-sm mt-2">
                                            <i class="fa fa-plus"></i> Registrar Primera Factura
                                        </a>
                                    </div>
                                </EmptyDataTemplate>
                            </asp:GridView>
                        </div>
                    </asp:Panel>
                </div>
            </div>

            <asp:Panel ID="pnlResultados" runat="server" Visible="false">
                <!-- Información Principal de la Factura -->
                <div class="panel panel-default">
                    <div class="panel-heading">
                        <h2><i class="fa fa-file-text-o"></i> Información de la Factura</h2>
                    </div>
                    <div class="panel-body">
                        <!-- NUEVA FILA: Cliente -->
                        <div class="row">
                            <div class="col-md-12 form-group">
                                <label for="ddlCliente">Cliente: <span class="required">*</span></label>
                                <asp:DropDownList ID="ddlCliente" runat="server" CssClass="form-control cliente-dropdown" Enabled="false">
                                    <asp:ListItem Value="">-- Seleccione un Cliente --</asp:ListItem>
                                </asp:DropDownList>
                                <asp:RequiredFieldValidator ID="rfvCliente" runat="server" 
                                    ControlToValidate="ddlCliente" 
                                    ErrorMessage="El cliente es requerido" 
                                    CssClass="text-danger" 
                                    Display="Dynamic"
                                    Enabled="false">
                                </asp:RequiredFieldValidator>
                            </div>
                        </div>

                        <!-- PRIMERA FILA: N° Factura y N° Pedido -->
                        <div class="row">
                            <div class="col-md-6 form-group">
                                <label for="txtNumFactura">N° Factura:</label>
                                <asp:TextBox ID="txtNumFactura" runat="server" CssClass="form-control"></asp:TextBox>
                            </div>
                            <div class="col-md-6 form-group">
                                <label for="txtNumPedido">N° Pedido:</label>
                                <asp:TextBox ID="txtNumPedido" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                            </div>
                        </div>

                        <!-- SEGUNDA FILA: Fecha de Emisión y Valor Total -->
                        <div class="row">
                            <div class="col-md-6 form-group">
                                <label for="txtFechaEmision">Fecha de Emisión:</label>
                                <asp:TextBox ID="txtFechaEmision" runat="server" CssClass="form-control" TextMode="Date" ReadOnly="true"></asp:TextBox>
                            </div>
                            <div class="col-md-6 form-group">
                                <label for="txtValorTotal">Valor Total:</label>
                                <asp:TextBox ID="txtValorTotal" runat="server" CssClass="form-control valor-total-input" ReadOnly="true"></asp:TextBox>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- NUEVA SECCIÓN: Upload de Documento -->
                <div class="panel panel-success upload-panel">
                    <div class="panel-heading">
                        <h2><i class="fa fa-upload"></i> Agregar Nuevo Documento</h2>
                    </div>
                    <div class="panel-body">
                        <div class="upload-container">
                            <div class="row">
                                <div class="col-md-8 form-group">
                                    <label for="fileUploadFactura">Adjuntar Documento (PDF recomendado):</label>
                                    <asp:FileUpload ID="fileUploadFactura" runat="server" CssClass="form-control file-input" 
                                        accept=".pdf,.doc,.docx,.jpg,.jpeg,.png" Enabled="false" />
                                    <small class="text-muted">
                                        <i class="fa fa-info-circle"></i> 
                                        Formatos permitidos: PDF, DOC, DOCX, JPG, PNG. Tamaño máximo: 50MB
                                    </small>
                                </div>
                                <div class="col-md-4 form-group">
                                    <label for="txtDescripcionDoc">Descripción del documento:</label>
                                    <asp:TextBox ID="txtDescripcionDoc" runat="server" CssClass="form-control" 
                                        placeholder="Ej: Factura Original, Documento Escaneado" MaxLength="300" ReadOnly="true"></asp:TextBox>
                                </div>
                            </div>
                            
                            <!-- Vista previa del archivo seleccionado -->
                            <div id="file-preview" class="file-preview" style="display: none;">
                                <div class="alert alert-info">
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
                    </div>
                </div>

                <!-- SECCIÓN: Documentos Asociados -->
                <div class="panel panel-default documentos-panel">
                    <div class="panel-heading">
                        <h2><i class="fa fa-file-pdf-o"></i> Documentos Asociados</h2>
                    </div>
                    <div class="panel-body">
                        <asp:Panel ID="pnlDocumentos" runat="server">
                            <div class="table-responsive">
                                <asp:GridView ID="gvDocumentos" runat="server" AutoGenerateColumns="False" CssClass="table table-bordered table-hover documentos-table"
                                    DataKeyNames="idDocumento" OnRowCommand="gvDocumentos_RowCommand" EmptyDataText="No hay documentos asociados a esta factura.">
                                    <Columns>
                                        <asp:TemplateField HeaderText="Archivo">
                                            <ItemTemplate>
                                                <div class="documento-info">
                                                    <i class="fa <%# ObtenerIconoArchivo(Eval("tipoArchivo").ToString()) %>"></i>
                                                    <span class="nombre-archivo"><%# Eval("nombreOriginal") %></span>
                                                    <small class="text-muted"><%# Eval("tipoArchivo") %></small>
                                                </div>
                                            </ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:TemplateField HeaderText="Tamaño">
                                            <ItemTemplate>
                                                <span class="tamano-archivo"><%# FormatearTamano(Convert.ToInt64(Eval("tamanoBytes"))) %></span>
                                            </ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:TemplateField HeaderText="Fecha Subida">
                                            <ItemTemplate>
                                                <span><%# Convert.ToDateTime(Eval("fechaSubida")).ToString("dd/MM/yyyy HH:mm") %></span>
                                            </ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:TemplateField HeaderText="Usuario">
                                            <ItemTemplate>
                                                <span><%# Eval("usuarioSubida") %></span>
                                            </ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:TemplateField HeaderText="Descripción">
                                            <ItemTemplate>
                                                <span class="descripcion-doc"><%# Eval("descripcion") ?? "Sin descripción" %></span>
                                            </ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:TemplateField HeaderText="Acciones">
                                            <ItemTemplate>
                                                <asp:Button ID="btnDescargar" runat="server" CssClass="btn btn-sm btn-success" 
                                                    Text="📥 Descargar" CommandName="Descargar" 
                                                    CommandArgument='<%# Eval("idDocumento") %>' 
                                                    ToolTip="Descargar documento" />
                                                <asp:Button ID="btnVer" runat="server" CssClass="btn btn-sm btn-info" 
                                                    Text="👁️ Ver" CommandName="Ver" 
                                                    CommandArgument='<%# Eval("idDocumento") %>' 
                                                    ToolTip="Abrir documento en nueva ventana" />
                                            </ItemTemplate>
                                        </asp:TemplateField>
                                    </Columns>
                                    <EmptyDataTemplate>
                                        <div class="alert alert-info">
                                            <i class="fa fa-info-circle"></i>
                                            <strong>No hay documentos asociados</strong><br />
                                            Esta factura no tiene documentos adjuntos.
                                        </div>
                                    </EmptyDataTemplate>
                                </asp:GridView>
                            </div>
                        </asp:Panel>
                    </div>
                </div>

                <!-- Botones de Acción -->
                <div class="row">
                    <div class="col-md-12 form-group text-center">
                        <asp:Button ID="btnHabilitarEdicion" runat="server" CssClass="btn btn-primary" Text="✏️ Habilitar Edición" OnClick="HabilitarEdicion" />
                        <asp:Button ID="btnGuardarCambios" runat="server" CssClass="btn btn-success" Text="💾 Guardar Cambios" OnClick="GuardarCambios" Visible="false" />
                        <asp:Button ID="btnCancelar" runat="server" CssClass="btn btn-danger" Text="❌ Cancelar" OnClick="Cancelar" />
                        <asp:Button ID="btnNuevaBusqueda" runat="server" CssClass="btn btn-info" Text="🔍 Nueva Búsqueda" OnClick="NuevaBusqueda" />
                    </div>
                </div>

                <div class="form-group text-center">
                    <asp:Label ID="lblMensaje" runat="server" CssClass="text-info"></asp:Label>
                </div>
            </asp:Panel>

            <!-- Panel de No Resultados -->
            <asp:Panel ID="pnlNoResultados" runat="server" Visible="false">
                <div class="alert alert-warning">
                    <i class="fa fa-exclamation-triangle"></i>
                    <strong>No se encontró ninguna factura con el número especificado.</strong>
                    <p>Verifique el número e intente nuevamente o <a href="AgregarFactura.aspx" class="alert-link">cree una nueva factura</a>.</p>
                </div>
            </asp:Panel>
        </div>
    </div>
    
    <script>
        // Script para validar números de pedido al editar y funcionalidad de upload
        document.addEventListener('DOMContentLoaded', function () {
            // Funcionalidad existente para validación de números de pedido
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

            // NUEVA: Funcionalidad para upload de archivos
            setupFileUpload();
        });

        // NUEVA FUNCIÓN: Configurar funcionalidad de upload de archivos
        function setupFileUpload() {
            const fileInput = document.getElementById('<%= fileUploadFactura.ClientID %>');

            if (fileInput) {
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

        // NUEVA FUNCIÓN: Limpiar selección de archivo
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

        // NUEVA FUNCIÓN: Formatear tamaño de archivo
        function formatFileSize(bytes) {
            if (bytes === 0) return '0 Bytes';

            const k = 1024;
            const sizes = ['Bytes', 'KB', 'MB', 'GB'];
            const i = Math.floor(Math.log(bytes) / Math.log(k));

            return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
        }
    </script>

    <style>
        /* Estilos existentes mejorados */
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

        .search-section {
            background-color: #f8f9fa;
            padding: 20px;
            border-radius: 5px;
            margin-bottom: 20px;
            border: 1px solid #dee2e6;
        }

        .valor-total-input {
            border-left: 3px solid #28a745;
            font-weight: bold;
        }

        /* NUEVO: Estilo especial para el dropdown del cliente */
        .cliente-dropdown {
            border-left: 3px solid #337ab7;
            font-weight: 500;
            background-color: #f8f9fa;
        }

        .cliente-dropdown:disabled {
            background-color: #e9ecef;
            opacity: 1;
        }

        /* Indicador de campo requerido */
        .required {
            color: #dc3545;
            margin-left: 3px;
        }

        /* NUEVOS ESTILOS PARA LA TABLA DE FACTURAS */
        .facturas-panel .panel-heading {
            background-color: #5bc0de;
            color: white;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .facturas-panel .panel-heading h2 {
            color: white;
            margin: 0;
            flex-grow: 1;
        }

        .panel-actions .btn {
            margin-left: 10px;
        }

        .facturas-table {
            font-size: 0.9em;
        }

        .facturas-table th {
            background-color: #f8f9fa;
            border-top: 2px solid #5bc0de;
            font-weight: bold;
            text-align: center;
            vertical-align: middle;
        }

        .facturas-table td {
            vertical-align: middle;
            text-align: center;
        }

        .cliente-info {
            text-align: left;
            padding: 5px;
        }

        .cliente-info strong {
            color: #2c3e50;
        }

        .importe-total {
            font-weight: bold;
            color: #27ae60;
            font-size: 1.1em;
        }

        .cpic-actions {
            text-align: center;
        }

        .cpic-numero {
            font-weight: bold;
            color: #27ae60;
            background-color: #d4edda;
            padding: 4px 8px;
            border-radius: 12px;
            font-size: 0.9em;
        }

        .cpic-numero i {
            margin-right: 4px;
        }

        /* Estilos para botones en la tabla */
        .facturas-table .btn-sm {
            font-size: 0.8em;
            padding: 3px 8px;
            margin: 1px;
        }

        /* Paginación */
        .pagination-ys {
            text-align: center;
            margin-top: 20px;
        }

        .pagination-ys .pagination {
            display: inline-block;
            padding-left: 0;
            margin: 20px 0;
            border-radius: 4px;
        }

        .pagination-ys .pagination > li {
            display: inline;
        }

        .pagination-ys .pagination > li > a,
        .pagination-ys .pagination > li > span {
            position: relative;
            float: left;
            padding: 6px 12px;
            margin-left: -1px;
            line-height: 1.42857143;
            color: #337ab7;
            text-decoration: none;
            background-color: #fff;
            border: 1px solid #ddd;
        }

        /* NUEVOS ESTILOS PARA UPLOAD */
        .upload-panel .panel-heading {
            background-color: #5cb85c;
            color: white;
        }

        .upload-panel .panel-heading h2 {
            color: white;
            margin: 0;
        }

        .file-input {
            border: 2px dashed #ced4da;
            border-radius: 6px;
            padding: 12px;
            transition: all 0.3s ease;
            background-color: #fafafa;
            min-height: 45px;
        }

        .file-input:hover:not(:disabled) {
            border-color: #007bff;
            background-color: #fff;
            box-shadow: 0 2px 4px rgba(0,123,255,0.1);
        }

        .file-input:disabled {
            background-color: #e9ecef;
            border-color: #ced4da;
            opacity: 0.6;
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
        }

        .btn-link {
            background: none;
            border: none;
            color: #dc3545;
            text-decoration: none;
            padding: 2px 5px;
            font-size: 0.9em;
            cursor: pointer;
        }

        .btn-link:hover {
            text-decoration: underline;
        }

        .upload-container {
            background-color: #f8f9fa;
            padding: 15px;
            border-radius: 4px;
            border: 1px solid #dee2e6;
        }

        /* NUEVOS ESTILOS PARA DOCUMENTOS */
        .documentos-panel .panel-heading {
            background-color: #dc3545;
            color: white;
        }

        .documentos-panel .panel-heading h2 {
            color: white;
            margin: 0;
        }

        .documento-info {
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .documento-info i {
            font-size: 1.2em;
            width: 20px;
        }

        .fa-file-pdf-o { color: #dc3545; }
        .fa-file-word-o { color: #0066cc; }
        .fa-file-image-o { color: #28a745; }
        .fa-file-o { color: #6c757d; }

        .nombre-archivo {
            font-weight: bold;
            flex-grow: 1;
        }

        .tamano-archivo {
            font-size: 0.9em;
            color: #6c757d;
        }

        .descripcion-doc {
            font-style: italic;
            color: #495057;
            max-width: 200px;
            display: block;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
        }

        .documentos-table .btn {
            margin-right: 5px;
            margin-bottom: 2px;
        }

        .panel-heading h2 {
            margin: 0;
            font-size: 1.3em;
        }

        .panel-heading i {
            margin-right: 8px;
        }

        .alert i {
            margin-right: 8px;
        }

        .panel {
            margin-bottom: 20px;
            border: 1px solid #ddd;
            border-radius: 4px;
        }

        .panel-heading {
            padding: 10px 15px;
            border-bottom: 1px solid #ddd;
            background-color: #f5f5f5;
        }

        .panel-body {
            padding: 15px;
        }

        .alert {
            padding: 15px;
            margin-bottom: 20px;
            border: 1px solid transparent;
            border-radius: 4px;
        }

        .alert-warning {
            background-color: #fcf8e3;
            border-color: #faebcc;
            color: #8a6d3b;
        }

        .alert-info {
            background-color: #d9edf7;
            border-color: #bce8f1;
            color: #31708f;
        }

        .btn {
            padding: 6px 12px;
            margin-bottom: 0;
            font-size: 14px;
            font-weight: normal;
            line-height: 1.42857143;
            text-align: center;
            white-space: nowrap;
            vertical-align: middle;
            cursor: pointer;
            border: 1px solid transparent;
            border-radius: 4px;
            text-decoration: none;
            display: inline-block;
        }

        .btn-primary {
            color: #fff;
            background-color: #337ab7;
            border-color: #2e6da4;
        }

        .btn-success {
            color: #fff;
            background-color: #5cb85c;
            border-color: #4cae4c;
        }

        .btn-info {
            color: #fff;
            background-color: #5bc0de;
            border-color: #46b8da;
        }

        .btn-danger {
            color: #fff;
            background-color: #d9534f;
            border-color: #d43f3a;
        }

        .btn-warning {
            color: #fff;
            background-color: #f0ad4e;
            border-color: #eea236;
        }

        .btn-light {
            color: #333;
            background-color: #f8f9fa;
            border-color: #dae0e5;
        }

        .btn-sm {
            padding: 5px 10px;
            font-size: 12px;
            line-height: 1.5;
            border-radius: 3px;
        }

        .table {
            width: 100%;
            max-width: 100%;
            margin-bottom: 20px;
            border-collapse: collapse;
        }

        .table-bordered {
            border: 1px solid #ddd;
        }

        .table-bordered th,
        .table-bordered td {
            border: 1px solid #ddd;
            padding: 8px;
            vertical-align: top;
        }

        .table-hover tbody tr:hover {
            background-color: #f5f5f5;
        }

        .table th {
            background-color: #f9f9f9;
            font-weight: bold;
        }

        .text-muted {
            color: #777;
        }

        .text-danger {
            color: #d9534f;
        }

        .text-success {
            color: #5cb85c;
        }

        .form-control {
            display: block;
            width: 100%;
            padding: 6px 12px;
            font-size: 14px;
            line-height: 1.42857143;
            color: #555;
            background-color: #fff;
            border: 1px solid #ccc;
            border-radius: 4px;
        }

        .form-group {
            margin-bottom: 15px;
        }

        .row {
            margin-right: -15px;
            margin-left: -15px;
        }

        .col-md-6 {
            position: relative;
            min-height: 1px;
            padding-right: 15px;
            padding-left: 15px;
            width: 50%;
            float: left;
        }

        .col-md-8 {
            position: relative;
            min-height: 1px;
            padding-right: 15px;
            padding-left: 15px;
            width: 66.66666667%;
            float: left;
        }

        .col-md-4 {
            position: relative;
            min-height: 1px;
            padding-right: 15px;
            padding-left: 15px;
            width: 33.33333333%;
            float: left;
        }

        .col-md-12 {
            position: relative;
            min-height: 1px;
            padding-right: 15px;
            padding-left: 15px;
            width: 100%;
        }

        .text-center {
            text-align: center;
        }

        .table-responsive {
            overflow-x: auto;
            min-height: 0.01%;
        }

        /* Responsivo */
        @media (max-width: 768px) {
            .documento-info {
                flex-direction: column;
                align-items: flex-start;
                gap: 4px;
            }
            
            .documentos-table .btn {
                display: block;
                width: 100%;
                margin-bottom: 5px;
            }

            .col-md-6, .col-md-8, .col-md-4 {
                width: 100%;
                float: none;
            }

            .facturas-table {
                font-size: 0.8em;
            }

            .panel-actions {
                margin-top: 10px;
            }

            .facturas-panel .panel-heading {
                flex-direction: column;
                align-items: flex-start;
            }
        }
    </style>
</asp:Content>
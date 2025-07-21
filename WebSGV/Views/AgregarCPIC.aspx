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

            <!-- Nueva fila para Peso Neto y Peso Bruto -->
            <div class="row">
                <div class="col-md-6 form-group">
                    <label for="txtPesoNeto">Peso Neto (Kg):</label>
                    <asp:TextBox ID="txtPesoNeto" runat="server" CssClass="form-control" placeholder="Ingrese el peso neto" step="0.01" TextMode="Number"></asp:TextBox>
                </div>
                <div class="col-md-6 form-group">
                    <label for="txtPesoBruto">Peso Bruto (Kg):</label>
                    <asp:TextBox ID="txtPesoBruto" runat="server" CssClass="form-control" placeholder="Ingrese el peso bruto" step="0.01" TextMode="Number"></asp:TextBox>
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

            <!-- Tabla para Productos -->
            <h2>Productos</h2>
            <div class="table-responsive">
                <table class="table table-bordered" id="tablaProductos">
                    <thead>
                        <tr>
                            <th>Producto</th>
                            <th>Cantidad de Bolsas</th>
                            <th>Acciones</th>
                        </tr>
                    </thead>
                    <tbody>
                        <!-- Las filas se generarán dinámicamente con JavaScript -->
                    </tbody>
                </table>
            </div>

            <asp:HiddenField ID="hiddenProductos" runat="server" />

            <div class="form-group text-center mt-4">
                <asp:Button ID="btnGuardar" runat="server" CssClass="btn btn-primary btn-lg"
                    Text="Guardar CPIC" OnClientClick="return prepararProductos();" OnClick="GuardarCPIC" />
            </div>
        </div>
    </div>

    <script>
        // Variable para almacenar los productos cargados desde la base de datos
        const productos = <%= ObtenerProductosJSON() %>;

        // Bandera para evitar validaciones durante el guardado
        window.skipRowValidation = false;

        // Conjunto para rastrear productos seleccionados y evitar duplicados
        const productosSeleccionados = new Set();

        // Inicializar la tabla al cargar la página
        document.addEventListener("DOMContentLoaded", () => {
            agregarFila(); // Agregar una fila inicial al cargar
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

        // Función para agregar una nueva fila a la tabla de productos
        function agregarFila() {
            const tabla = document.querySelector("#tablaProductos tbody");

            // Validar la última fila antes de agregar otra, pero solo si no estamos guardando
            if (!window.skipRowValidation) {
                const ultimaFila = tabla.lastElementChild;
                if (ultimaFila) {
                    const producto = ultimaFila.querySelector(".producto-dropdown").value;
                    const cantidad = ultimaFila.querySelector("input[name='cantidad']").value;

                    // Solo validar si alguno de los campos tiene datos
                    if ((producto !== "0" || cantidad) &&
                        (producto === "0" || !cantidad || cantidad <= 0)) {
                        alert("Complete todos los datos de la fila actual antes de agregar una nueva.");
                        return;
                    }
                }
            }

            // Crear nueva fila (sin columna de peso)
            const nuevaFila = document.createElement("tr");
            nuevaFila.innerHTML = `
                <td>
                    <select class="form-control producto-dropdown" onchange="actualizarSeleccion(this)">
                        <option value="0">Seleccione un producto</option>
                        ${productos.map(p => `<option value="${p.idProducto}">${p.nombre}</option>`).join('')}
                    </select>
                </td>
                <td>
                    <input type="number" class="form-control" placeholder="Cantidad" name="cantidad" min="1">
                </td>
                <td>
                    <button type="button" class="btn btn-success btn-sm" onclick="agregarFila()">Añadir</button>
                    <button type="button" class="btn btn-danger btn-sm" onclick="eliminarFila(this)">Eliminar</button>
                </td>
            `;
            tabla.appendChild(nuevaFila);

            actualizarDropdowns();
        }

        // Función para actualizar la selección de productos y evitar duplicados
        function actualizarSeleccion(dropdown) {
            const valorAnterior = dropdown.dataset.valorAnterior || "0";
            const valorNuevo = dropdown.value;

            // Liberar el producto previamente seleccionado
            if (valorAnterior !== "0") {
                productosSeleccionados.delete(valorAnterior);
            }

            // Validar la nueva selección
            if (valorNuevo !== "0") {
                if (productosSeleccionados.has(valorNuevo)) {
                    alert("Este producto ya está seleccionado en otra fila.");
                    dropdown.value = "0"; // Restablecer selección
                    return;
                }
                productosSeleccionados.add(valorNuevo);
            }

            dropdown.dataset.valorAnterior = valorNuevo; // Guardar la nueva selección
            actualizarDropdowns();
        }

        // Deshabilitar productos seleccionados en otros dropdowns
        function actualizarDropdowns() {
            document.querySelectorAll(".producto-dropdown").forEach(dropdown => {
                const opciones = dropdown.querySelectorAll("option");
                opciones.forEach(opcion => {
                    if (opcion.value !== "0" && productosSeleccionados.has(opcion.value) && opcion.value !== dropdown.value) {
                        opcion.disabled = true; // Deshabilitar producto seleccionado en otro dropdown
                    } else {
                        opcion.disabled = false; // Habilitar si está disponible
                    }
                });
            });
        }

        // Eliminar fila y liberar producto
        function eliminarFila(boton) {
            const tabla = document.querySelector("#tablaProductos tbody");
            const filas = tabla.querySelectorAll("tr");

            // Evitar eliminar la última fila
            if (filas.length === 1) {
                alert("Debe existir al menos una fila en la tabla.");
                return;
            }

            const fila = boton.closest("tr");
            const dropdown = fila.querySelector(".producto-dropdown");
            const valorSeleccionado = dropdown.value;

            // Liberar el producto seleccionado
            if (valorSeleccionado !== "0") {
                productosSeleccionados.delete(valorSeleccionado);
            }

            fila.remove();
            actualizarDropdowns();
        }

        // Función para preparar los productos antes de enviar el formulario
        function prepararProductos() {
            // Evitar la validación de fila incompleta durante el guardado
            window.skipRowValidation = true;

            // Capturar todos los productos de la tabla
            const tabla = document.querySelector("#tablaProductos tbody");
            const filas = tabla.querySelectorAll("tr");
            const productos = [];

            // Recorrer cada fila y extraer los datos (sin peso)
            for (let i = 0; i < filas.length; i++) {
                const fila = filas[i];
                const productoSelect = fila.querySelector(".producto-dropdown");
                const cantidadInput = fila.querySelector("input[name='cantidad']");

                if (productoSelect && cantidadInput) {
                    const idProducto = productoSelect.value;
                    const cantidad = cantidadInput.value;

                    // Validar que todos los campos tengan valores válidos
                    if (idProducto && idProducto !== "0" && cantidad && cantidad > 0) {
                        productos.push({
                            IdProducto: parseInt(idProducto),
                            Cantidad: parseInt(cantidad)
                        });
                    }
                }
            }

            // Verificar si hay productos válidos
            if (productos.length === 0) {
                alert("Debe agregar al menos un producto con valores válidos.");
                window.skipRowValidation = false;
                return false;
            }

            // Validar que todos los campos requeridos estén completos
            const numeroCPIC = document.getElementById('<%= txtNumCPIC.ClientID %>').value;
            const numeroFactura = document.getElementById('<%= txtNumFactura.ClientID %>').value;
            const totalFlete = document.getElementById('<%= txtTotalFlete.ClientID %>').value;
            const pesoNeto = document.getElementById('<%= txtPesoNeto.ClientID %>').value;
            const pesoBruto = document.getElementById('<%= txtPesoBruto.ClientID %>').value;

            if (!numeroCPIC) {
                alert("Debe ingresar un número de CPIC.");
                window.skipRowValidation = false;
                return false;
            }

            if (numeroCPIC.length !== 7) {
                alert("El número de CPIC debe tener exactamente 7 caracteres.");
                window.skipRowValidation = false;
                return false;
            }

            if (!numeroFactura) {
                alert("Debe ingresar un número de factura.");
                window.skipRowValidation = false;
                return false;
            }

            if (!totalFlete || parseFloat(totalFlete) <= 0) {
                alert("El valor total del flete no es válido.");
                window.skipRowValidation = false;
                return false;
            }

            // Validar peso neto (obligatorio)
            if (!pesoNeto || parseFloat(pesoNeto) <= 0) {
                alert("Debe ingresar un peso neto válido.");
                window.skipRowValidation = false;
                return false;
            }

            // Validar peso bruto (obligatorio)
            if (!pesoBruto || parseFloat(pesoBruto) <= 0) {
                alert("Debe ingresar un peso bruto válido.");
                window.skipRowValidation = false;
                return false;
            }

            // Validar que el peso bruto sea mayor al peso neto
            if (parseFloat(pesoBruto) <= parseFloat(pesoNeto)) {
                alert("El peso bruto debe ser mayor al peso neto.");
                window.skipRowValidation = false;
                return false;
            }

            // Guardar los productos en el campo oculto como JSON
            document.getElementById('<%= hiddenProductos.ClientID %>').value = JSON.stringify(productos);
            return true;
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

        .table-responsive {
            margin-bottom: 20px;
        }

        /* Mejorar el espaciado de los botones en las celdas de la tabla */
        .table .btn {
            margin-right: 5px;
        }

        .text-danger {
            color: #a94442;
            font-size: 0.9em;
            display: block;
            margin-top: 5px;
        }

        /* Estilos adicionales para los nuevos campos de peso */
        .form-group label {
            font-weight: bold;
            margin-bottom: 5px;
        }

        /* Resaltar campos de peso */
        #<%= txtPesoNeto.ClientID %>,
        #<%= txtPesoBruto.ClientID %> {
            border-left: 3px solid #5bc0de;
        }

        /* NUEVOS ESTILOS PARA UPLOAD */
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
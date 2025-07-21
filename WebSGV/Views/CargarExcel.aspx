<%@ Page Title="Carga de Indicadores vía Excel" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="CargarExcel.aspx.cs" Inherits="WebSGV.Views.CargarExcel" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .upload-container {
            border: 2px dashed #ccc;
            padding: 20px;
            border-radius: 8px;
            text-align: center;
            margin-bottom: 30px;
            cursor: pointer;
            transition: all 0.3s;
        }
        .upload-container:hover {
            border-color: #0056b3;
        }
        .upload-icon {
            font-size: 48px;
            color: #0056b3;
            margin-bottom: 10px;
        }
        .file-info {
            margin-top: 15px;
            font-style: italic;
            color: #666;
        }
        .action-buttons {
            margin-top: 20px;
        }
        .records-table {
            margin-top: 30px;
        }
        .month-filter {
            margin-bottom: 20px;
        }
        .status-pill {
            display: inline-block;
            padding: 3px 10px;
            border-radius: 12px;
            font-size: 12px;
            font-weight: 500;
        }
        .status-success {
            background-color: #d4edda;
            color: #155724;
        }
        .status-error {
            background-color: #f8d7da;
            color: #721c24;
        }
        .status-warning {
            background-color: #fff3cd;
            color: #856404;
        }
        .status-info {
            background-color: #d1ecf1;
            color: #0c5460;
        }
        .template-section {
            margin-top: 40px;
            background-color: #f9f9f9;
            padding: 20px;
            border-radius: 8px;
        }
        .section-header {
            background-color: #0056b3;
            color: white;
            padding: 10px 15px;
            margin-bottom: 15px;
            border-radius: 4px;
            font-weight: 600;
        }
        .instructions-box {
            background-color: #e9f7fe;
            border-left: 4px solid #0077cc;
            padding: 15px;
            margin-bottom: 20px;
            border-radius: 3px;
        }
    </style>

    <div class="container">
        <h2 class="mt-4 mb-4">Carga de Indicadores vía Excel</h2>
        
        <!-- Mensaje de alerta -->
        <asp:Panel ID="alertPanel" runat="server" CssClass="alert alert-info" Visible="false">
            <asp:Literal ID="alertMessage" runat="server"></asp:Literal>
        </asp:Panel>
        
        <!-- Instrucciones -->
        <div class="instructions-box">
            <h5><i class="fas fa-info-circle mr-2"></i>Instrucciones de uso:</h5>
            <ol>
                <li>Descargue la plantilla Excel al final de esta página si aún no la tiene.</li>
                <li>Pegue los datos del Excel de oficina en la <strong>Hoja 2</strong> de la plantilla.</li>
                <li>Verifique que los datos se han convertido correctamente en la <strong>Hoja 1</strong>.</li>
                <li>Suba el archivo Excel completo utilizando el formulario a continuación.</li>
                <li>El sistema procesará <strong>únicamente la Hoja 1</strong> para la importación.</li>
            </ol>
        </div>

        <!-- Sección de carga de archivo -->
        <div class="section-header">
            <i class="fas fa-upload mr-2"></i> Carga de Archivo Excel
        </div>

        <div class="row">
            <div class="col-md-8">
                <div id="dropZone" class="upload-container" onclick="document.getElementById('<%= fileUpload.ClientID %>').click();">
                    <div class="upload-icon">
                        <i class="fas fa-file-excel"></i>
                    </div>
                    <h4>Arrastra tu archivo Excel de conversión aquí o haz clic para seleccionar</h4>
                    <p>El sistema importará la Hoja 1 (formato simplificado con 33 columnas)</p>
                    <asp:FileUpload ID="fileUpload" runat="server" CssClass="d-none" onchange="updateFileName(this)" accept=".xlsx, .xls" />
                    <div id="fileInfo" class="file-info">Ningún archivo seleccionado</div>
                </div>
            </div>
            <div class="col-md-4">
                <div class="card">
                    <div class="card-header bg-primary text-white">
                        <i class="fas fa-info-circle mr-2"></i> Importante
                    </div>
                    <div class="card-body">
                        <p><strong>Atención:</strong> Al cargar un nuevo archivo para un mes que ya tiene datos, se sobrescribirán todos los indicadores existentes para ese mes.</p>
                        <p>Asegúrese de que la <strong>Hoja 1</strong> contiene los datos en el formato correcto para importar.</p>
                        <p>Tamaño máximo del archivo: 10MB.</p>
                    </div>
                </div>
            </div>
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="ddlMes" class="form-label mt-3">Mes correspondiente:</label>
                    <asp:DropDownList ID="ddlMes" runat="server" CssClass="form-control">
                        <asp:ListItem Text="Enero" Value="1"></asp:ListItem>
                        <asp:ListItem Text="Febrero" Value="2"></asp:ListItem>
                        <asp:ListItem Text="Marzo" Value="3"></asp:ListItem>
                        <asp:ListItem Text="Abril" Value="4"></asp:ListItem>
                        <asp:ListItem Text="Mayo" Value="5"></asp:ListItem>
                        <asp:ListItem Text="Junio" Value="6"></asp:ListItem>
                        <asp:ListItem Text="Julio" Value="7"></asp:ListItem>
                        <asp:ListItem Text="Agosto" Value="8"></asp:ListItem>
                        <asp:ListItem Text="Septiembre" Value="9"></asp:ListItem>
                        <asp:ListItem Text="Octubre" Value="10"></asp:ListItem>
                        <asp:ListItem Text="Noviembre" Value="11"></asp:ListItem>
                        <asp:ListItem Text="Diciembre" Value="12"></asp:ListItem>
                    </asp:DropDownList>
                </div>
            </div>
            <div class="col-md-6">
                <div class="form-group">
                    <label for="ddlAnio" class="form-label mt-3">Año correspondiente:</label>
                    <asp:DropDownList ID="ddlAnio" runat="server" CssClass="form-control">
                        <asp:ListItem Text="2023" Value="2023"></asp:ListItem>
                        <asp:ListItem Text="2024" Value="2024"></asp:ListItem>
                        <asp:ListItem Text="2025" Value="2025" Selected="True"></asp:ListItem>
                        <asp:ListItem Text="2026" Value="2026"></asp:ListItem>
                    </asp:DropDownList>
                </div>
            </div>
        </div>

        <div class="action-buttons text-center">
            <asp:Button ID="btnProcesar" runat="server" Text="Procesar Archivo" CssClass="btn btn-primary" OnClick="btnProcesar_Click" Enabled="false" />
            <asp:Button ID="btnCancelar" runat="server" Text="Cancelar" CssClass="btn btn-secondary ml-2" OnClick="btnCancelar_Click" />
        </div>

        <!-- Historial de cargas -->
        <div class="section-header mt-5">
            <i class="fas fa-history mr-2"></i> Historial de Cargas
        </div>

        <div class="month-filter">
            <div class="row">
                <div class="col-md-4">
                    <label for="ddlMesFilter" class="form-label">Filtrar por mes:</label>
                    <asp:DropDownList ID="ddlMesFilter" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="UpdateHistorial">
                        <asp:ListItem Text="Todos" Value="0"></asp:ListItem>
                        <asp:ListItem Text="Enero" Value="1"></asp:ListItem>
                        <asp:ListItem Text="Febrero" Value="2"></asp:ListItem>
                        <asp:ListItem Text="Marzo" Value="3"></asp:ListItem>
                        <asp:ListItem Text="Abril" Value="4"></asp:ListItem>
                        <asp:ListItem Text="Mayo" Value="5"></asp:ListItem>
                        <asp:ListItem Text="Junio" Value="6"></asp:ListItem>
                        <asp:ListItem Text="Julio" Value="7"></asp:ListItem>
                        <asp:ListItem Text="Agosto" Value="8"></asp:ListItem>
                        <asp:ListItem Text="Septiembre" Value="9"></asp:ListItem>
                        <asp:ListItem Text="Octubre" Value="10"></asp:ListItem>
                        <asp:ListItem Text="Noviembre" Value="11"></asp:ListItem>
                        <asp:ListItem Text="Diciembre" Value="12"></asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="col-md-4">
                    <label for="ddlAnioFilter" class="form-label">Filtrar por año:</label>
                    <asp:DropDownList ID="ddlAnioFilter" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="UpdateHistorial">
                        <asp:ListItem Text="Todos" Value="0"></asp:ListItem>
                        <asp:ListItem Text="2023" Value="2023"></asp:ListItem>
                        <asp:ListItem Text="2024" Value="2024"></asp:ListItem>
                        <asp:ListItem Text="2025" Value="2025" Selected="True"></asp:ListItem>
                        <asp:ListItem Text="2026" Value="2026"></asp:ListItem>
                    </asp:DropDownList>
                </div>
            </div>
        </div>

        <div class="records-table">
            <asp:GridView ID="gvHistorial" runat="server" CssClass="table table-striped table-bordered table-hover" 
                AutoGenerateColumns="false" AllowPaging="true" PageSize="10" OnPageIndexChanging="gvHistorial_PageIndexChanging"
                EmptyDataText="No hay registros de cargas para mostrar">
                <Columns>
                    <asp:BoundField DataField="UploadID" HeaderText="ID" />
                    <asp:BoundField DataField="FileName" HeaderText="Nombre de Archivo" />
                    <asp:BoundField DataField="UploadDate" HeaderText="Fecha de Carga" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                    <asp:BoundField DataField="Month" HeaderText="Mes" />
                    <asp:BoundField DataField="Year" HeaderText="Año" />
                    <asp:BoundField DataField="RowsProcessed" HeaderText="Registros Procesados" />
                    <asp:TemplateField HeaderText="Estado">
                        <ItemTemplate>
                            <span class='status-pill <%# GetStatusClass(Eval("Status").ToString()) %>'>
                                <%# Eval("Status") %>
                            </span>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Acciones">
                        <ItemTemplate>
                            <asp:LinkButton ID="btnDeleteUpload" runat="server" CssClass="btn btn-sm btn-danger" 
                                CommandName="DeleteUpload" CommandArgument='<%# Eval("UploadID") %>'
                                OnCommand="btnDeleteUpload_Command" 
                                OnClientClick="return confirm('¿Está seguro de que desea eliminar esta carga y todos sus registros asociados?');">
                                <i class="fas fa-trash-alt"></i> Eliminar
                            </asp:LinkButton>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>

        <!-- Descargar Plantilla -->
        <div class="template-section">
            <div class="section-header">
                <i class="fas fa-file-download mr-2"></i> Descargar Plantilla Excel
            </div>
            <div class="row">
                <div class="col-md-8">
                    <p>Descargue la plantilla Excel de conversión que contiene:</p>
                    <ul>
                        <li><strong>Hoja 1 (Importación):</strong> Formato simplificado con 33 columnas que se importará a la base de datos.</li>
                        <li><strong>Hoja 2 (Oficina):</strong> Formato original con 57 columnas donde puede pegar los datos del Excel de oficina.</li>
                    </ul>
                    <p>Las fórmulas automáticas convierten los datos de la Hoja 2 al formato de la Hoja 1.</p>
                </div>
                <div class="col-md-4 text-center mt-4">
                    <asp:Button ID="btnDescargarPlantilla" runat="server" Text="Descargar Plantilla de Conversión" CssClass="btn btn-success" OnClick="btnDescargarPlantilla_Click" />
                </div>
            </div>
        </div>
    </div>

    <script type="text/javascript">
        function updateFileName(input) {
            const fileInfo = document.getElementById('fileInfo');
            const btnProcesar = document.getElementById('<%= btnProcesar.ClientID %>');
            
            if (input.files && input.files[0]) {
                const fileName = input.files[0].name;
                fileInfo.innerHTML = 'Archivo seleccionado: <strong>' + fileName + '</strong>';
                btnProcesar.disabled = false;
            } else {
                fileInfo.innerHTML = 'Ningún archivo seleccionado';
                btnProcesar.disabled = true;
            }
        }

        // Funcionalidad de drag & drop
        const dropZone = document.getElementById('dropZone');
        
        dropZone.addEventListener('dragover', function(e) {
            e.preventDefault();
            e.stopPropagation();
            this.style.backgroundColor = '#f8f9fa';
        });
        
        dropZone.addEventListener('dragleave', function(e) {
            e.preventDefault();
            e.stopPropagation();
            this.style.backgroundColor = '';
        });
        
        dropZone.addEventListener('drop', function(e) {
            e.preventDefault();
            e.stopPropagation();
            this.style.backgroundColor = '';
            
            const fileUpload = document.getElementById('<%= fileUpload.ClientID %>');
            fileUpload.files = e.dataTransfer.files;
            updateFileName(fileUpload);
        });
    </script>
</asp:Content>
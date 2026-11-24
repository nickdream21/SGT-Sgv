<%@ Page Title="Carga de indicadores vía Excel" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="CargarExcel.aspx.cs" Inherits="WebSGV.Views.CargarExcel" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/CargarExcel.css") %>" rel="stylesheet" />

    <div class="container">
        <div class="sgv-encabezado">
            <div class="sgv-encabezado-titulo">
                <h1 class="h2">Carga de indicadores vía Excel</h1>
            </div>
        </div>
        
        <!-- Mensaje de alerta -->
        <asp:Panel ID="alertPanel" runat="server" EnableViewState="false" Visible="false">
            <asp:Literal ID="alertMessage" runat="server" EnableViewState="false"></asp:Literal>
        </asp:Panel>
        
        <!-- Instrucciones -->
        <div class="instructions-box">
            <h5>Instrucciones de uso:</h5>
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
            Carga de Archivo Excel
        </div>

        <div class="row">
            <div class="col-md-8">
                <div id="dropZone" class="upload-container" data-accion="elegirArchivo">
                    <h4>Arrastra tu archivo Excel de conversión aquí o haz clic para seleccionar</h4>
                    <p>El sistema importará la hoja 1 (formato simplificado con 33 columnas)</p>
                    <asp:FileUpload ID="fileUpload" runat="server" CssClass="d-none" aria-label="Archivo Excel de conversión" data-accion-change="mostrarArchivoElegido" accept=".xlsx" />
                    <div id="fileInfo" class="file-info">Ningún archivo seleccionado</div>
                </div>
            </div>
            <div class="col-md-4">
                <div class="card">
                    <div class="card-header bg-primary text-white">
                        Importante
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
                    <label for="<%= ddlMes.ClientID %>" class="sgv-etiqueta mt-3">Mes correspondiente:</label>
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
                    <label for="<%= ddlAnio.ClientID %>" class="sgv-etiqueta mt-3">Año correspondiente:</label>
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
            <asp:Button ID="btnProcesar" runat="server" Text="Procesar archivo" CssClass="btn btn-primary" OnClick="btnProcesar_Click" data-espera="120" Enabled="false" />
            <asp:Button ID="btnCancelar" runat="server" Text="Cancelar" CssClass="btn btn-secondary ml-2" OnClick="btnCancelar_Click" />
        </div>

        <!-- Historial de cargas -->
        <div class="section-header mt-5">
            Historial de Cargas
        </div>

        <div class="month-filter">
            <div class="row">
                <div class="col-md-4">
                    <label for="<%= ddlMesFilter.ClientID %>" class="sgv-etiqueta">Filtrar por mes:</label>
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
                    <label for="<%= ddlAnioFilter.ClientID %>" class="sgv-etiqueta">Filtrar por año:</label>
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
            <div class="table-responsive">
            <asp:GridView ID="gvHistorial" runat="server" CssClass="table table-striped table-bordered table-hover" 
                AutoGenerateColumns="false" AllowPaging="true" PageSize="10" OnPageIndexChanging="gvHistorial_PageIndexChanging"
                EmptyDataText="No hay registros de cargas para mostrar">
                <Columns>
                    <asp:BoundField DataField="UploadID" HeaderText="ID" />
                    <asp:BoundField DataField="FileName" HeaderText="Nombre de archivo" />
                    <asp:BoundField DataField="UploadDate" HeaderText="Fecha de carga" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                    <asp:BoundField DataField="Month" HeaderText="Mes" />
                    <asp:BoundField DataField="Year" HeaderText="Año" />
                    <asp:BoundField DataField="RowsProcessed" HeaderText="Registros procesados" />
                    <asp:TemplateField HeaderText="Estado">
                        <ItemTemplate>
                            <span class='status-pill <%#: GetStatusClass(Eval("Status").ToString()) %>'>
                                <%#: Eval("Status") %>
                            </span>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Acciones">
                        <ItemTemplate>
                            <asp:LinkButton ID="btnDeleteUpload" runat="server" CssClass="btn btn-sm btn-danger" 
                                CommandName="DeleteUpload" CommandArgument='<%# Eval("UploadID") %>'
                                OnCommand="btnDeleteUpload_Command" 
                                data-confirmar="¿Está seguro de que desea eliminar esta carga y todos sus registros asociados?" data-confirmar-peligro="true">
                                Eliminar
                            </asp:LinkButton>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
            </div>
        </div>

        <!-- Descargar Plantilla -->
        <div class="template-section">
            <div class="section-header">
                Descargar Plantilla Excel
            </div>
            <div class="row">
                <div class="col-md-8">
                    <p>Descargue la plantilla Excel de conversión que contiene:</p>
                    <ul>
                        <li><strong>Hoja 1 (Importación):</strong> Formato simplificado con 33 columnas que se importará a la base de datos.</li>
                        <li><strong>Hoja 2 (Oficina):</strong> Formato original con 57 columnas donde puede pegar los datos del Excel de oficina.</li>
                    </ul>
                    <p>Las fórmulas automáticas convierten los datos de la hoja 2 al formato de la hoja 1.</p>
                </div>
                <div class="col-md-4 text-center mt-4">
                    <asp:Button ID="btnDescargarPlantilla" runat="server" Text="Descargar plantilla de conversión" CssClass="btn btn-success" OnClick="btnDescargarPlantilla_Click" data-descarga="true" />
                </div>
            </div>
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            btnProcesar: '<%= btnProcesar.ClientID %>',
            fileUpload: '<%= fileUpload.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/CargarExcel.js") %>"></script>
</asp:Content>
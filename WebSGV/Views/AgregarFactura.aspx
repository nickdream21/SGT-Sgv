<%@ Page Title="Registro de Factura" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AgregarFactura.aspx.cs" Inherits="WebSGV.Views.AgregarFactura" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/AgregarFactura.css") %>" rel="stylesheet" />

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
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            txtFechaEmision: '<%= txtFechaEmision.ClientID %>',
            txtImporteTotal: '<%= txtImporteTotal.ClientID %>',
            txtNumPedido: '<%= txtNumPedido.ClientID %>',
            fileUploadFactura: '<%= fileUploadFactura.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/AgregarFactura.js") %>"></script>
</asp:Content>
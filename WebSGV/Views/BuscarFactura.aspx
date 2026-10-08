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
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            txtNumPedido: '<%= txtNumPedido.ClientID %>',
            fileUploadFactura: '<%= fileUploadFactura.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/BuscarFactura.js") %>"></script>

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/BuscarFactura.css") %>" rel="stylesheet" />
</asp:Content>
<%@ Page Title="Buscar CPIC" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="BuscarCPIC.aspx.cs" Inherits="WebSGV.Views.BusquedaCPIC" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="main-container buscar-cpic-container">
        <div class="form-container">
            <h1 class="header">Búsqueda de CPIC</h1>

            <!-- Sección de Búsqueda -->
            <div class="row search-section">
                <div class="col-md-8 form-group">
                    <label for="txtBuscarCPIC">N° CPIC:</label>
                    <asp:TextBox ID="txtBuscarCPIC" runat="server" CssClass="form-control" placeholder="Ingrese el N° CPIC a buscar" MaxLength="7"></asp:TextBox>
                </div>
                <div class="col-md-4 form-group">
                    <label>&nbsp;</label> <!-- Espacio para alinear con el campo de texto -->
                    <asp:Button ID="btnBuscar" runat="server" CssClass="btn btn-primary form-control" Text="🔍 Buscar" OnClick="BuscarCPICClick" />
                </div>
            </div>

            <asp:Panel ID="pnlResultados" runat="server" Visible="false">
                <!-- Información Principal del CPIC -->
                <div class="panel panel-default">
                    <div class="panel-heading">
                        <h2><i class="fa fa-info-circle"></i> Información del CPIC</h2>
                    </div>
                    <div class="panel-body">
                        <div class="row">
                            <div class="col-md-6 form-group">
                                <label for="txtNumCPIC">N° CPIC:</label>
                                <asp:TextBox ID="txtNumCPIC" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                            </div>
                            <div class="col-md-6 form-group">
                                <label for="txtNumFactura">N° Factura:</label>
                                <asp:TextBox ID="txtNumFactura" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                            </div>
                        </div>

                        <div class="row">
                            <div class="col-md-6 form-group">
                                <label for="txtFechaEmision">Fecha de Emisión:</label>
                                <asp:TextBox ID="txtFechaEmision" runat="server" CssClass="form-control" TextMode="Date" ReadOnly="true"></asp:TextBox>
                            </div>
                            <div class="col-md-6 form-group">
                                <label for="txtTotalFlete">Valor Total del Flete:</label>
                                <asp:TextBox ID="txtTotalFlete" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                            </div>
                        </div>

                        <!-- NUEVA FILA: Peso Neto y Peso Bruto -->
                        <div class="row peso-section">
                            <div class="col-md-6 form-group">
                                <label for="txtPesoNeto">Peso Neto (Kg):</label>
                                <asp:TextBox ID="txtPesoNeto" runat="server" CssClass="form-control peso-input" ReadOnly="true"></asp:TextBox>
                            </div>
                            <div class="col-md-6 form-group">
                                <label for="txtPesoBruto">Peso Bruto (Kg):</label>
                                <asp:TextBox ID="txtPesoBruto" runat="server" CssClass="form-control peso-input" ReadOnly="true"></asp:TextBox>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- NUEVA SECCIÓN: Documentos Asociados -->
                <div class="panel panel-default documentos-panel">
                    <div class="panel-heading">
                        <h2><i class="fa fa-file-pdf-o"></i> Documentos Asociados</h2>
                    </div>
                    <div class="panel-body">
                        <asp:Panel ID="pnlDocumentos" runat="server">
                            <div class="table-responsive">
                                <asp:GridView ID="gvDocumentos" runat="server" AutoGenerateColumns="False" CssClass="table table-bordered table-hover documentos-table"
                                    DataKeyNames="idDocumento" OnRowCommand="gvDocumentos_RowCommand" EmptyDataText="No hay documentos asociados a este CPIC.">
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
                                            Este CPIC no tiene documentos adjuntos.
                                        </div>
                                    </EmptyDataTemplate>
                                </asp:GridView>
                            </div>
                        </asp:Panel>
                    </div>
                </div>

                <!-- Tabla de Productos (SIN columna de peso) -->
                <div class="panel panel-default">
                    <div class="panel-heading">
                        <h2><i class="fa fa-cube"></i> Productos</h2>
                    </div>
                    <div class="panel-body">
                        <div class="table-responsive">
                            <asp:GridView ID="gvProductos" runat="server" AutoGenerateColumns="False" CssClass="table table-bordered table-hover"
                                DataKeyNames="ID" OnRowEditing="gvProductos_RowEditing" OnRowCancelingEdit="gvProductos_RowCancelingEdit"
                                OnRowUpdating="gvProductos_RowUpdating">
                                <Columns>
                                    <asp:TemplateField HeaderText="Producto">
                                        <ItemTemplate>
                                            <asp:Label ID="lblProducto" runat="server" Text='<%# Eval("NombreProducto") %>'></asp:Label>
                                        </ItemTemplate>
                                        <EditItemTemplate>
                                            <asp:DropDownList ID="ddlProductos" runat="server" CssClass="form-control producto-dropdown" DataTextField="Nombre" DataValueField="IdProducto">
                                            </asp:DropDownList>
                                        </EditItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="Cantidad de Bolsas">
                                        <ItemTemplate>
                                            <asp:Label ID="lblCantidad" runat="server" Text='<%# Eval("Cantidad") %>'></asp:Label>
                                        </ItemTemplate>
                                        <EditItemTemplate>
                                            <asp:TextBox ID="txtCantidad" runat="server" CssClass="form-control" Text='<%# Bind("Cantidad") %>' TextMode="Number" min="1"></asp:TextBox>
                                        </EditItemTemplate>
                                    </asp:TemplateField>
                                  
                                    <asp:CommandField ShowEditButton="True" ButtonType="Button" EditText="✏️ Editar" UpdateText="💾 Guardar" CancelText="❌ Cancelar" 
                                        ControlStyle-CssClass="btn btn-sm btn-info" HeaderText="Acciones" />
                                </Columns>
                            </asp:GridView>
                        </div>
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
                    <strong>No se encontró ningún CPIC con el número especificado.</strong>
                    <p>Verifique el número e intente nuevamente o <a href="AgregarCPIC.aspx" class="alert-link">cree un nuevo CPIC</a>.</p>
                </div>
            </asp:Panel>
        </div>
    </div>

    <style>
        /* Estilos adicionales para la búsqueda mejorada */
        .search-section {
            background-color: #f8f9fa;
            padding: 20px;
            border-radius: 5px;
            margin-bottom: 20px;
            border: 1px solid #dee2e6;
        }

        .peso-section {
            background-color: #f0f8ff;
            padding: 15px;
            border-radius: 5px;
            margin: 10px 0;
            border: 1px solid #b0d4f1;
        }

        .peso-input {
            border-left: 3px solid #5bc0de;
            font-weight: bold;
        }

        .documentos-panel .panel-heading {
            background-color: #d9534f;
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
        }
    </style>
</asp:Content>
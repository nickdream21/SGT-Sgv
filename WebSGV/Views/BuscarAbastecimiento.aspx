<%@ Page Title="Buscar Abastecimiento de Combustible" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="BuscarAbastecimiento.aspx.cs" Inherits="WebSGV.Views.BusquedaAbastecimiento" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/BuscarAbastecimiento.css") %>" rel="stylesheet" />

    <div class="container-fluid buscar-abastecimiento-container">
        <!-- Encabezado -->
        <div class="d-flex justify-content-between align-items-center header-container">
            <h3 class="abastecimiento-header text-uppercase">Buscar Abastecimiento de Combustible</h3>
        </div>

        <!-- Sección de Búsqueda -->
        <div class="search-section">
            <div class="row">
                <div class="col-md-8">
                    <label for="txtBuscarAbastecimiento" class="form-label">N° de Abastecimiento:</label>
                    <asp:TextBox ID="txtBuscarAbastecimiento" runat="server" CssClass="form-control" placeholder="Ingrese el número de abastecimiento a buscar"></asp:TextBox>
                </div>
                <div class="col-md-4">
                    <label class="form-label">&nbsp;</label>
                    <asp:Button ID="btnBuscar" runat="server" CssClass="btn btn-primary form-control" Text="Buscar" OnClick="BuscarAbastecimientoClick" />
                </div>
            </div>
        </div>

        <!-- Panel de Resultados -->
        <asp:Panel ID="pnlResultados" runat="server" Visible="false">
            <!-- Banner de ANULADO (solo visible si está anulado) -->
            <asp:Panel ID="pnlAnuladoBanner" runat="server" Visible="false">
                <div class="estado-anulado-banner">
                    <i class="fas fa-ban mr-2"></i>Este registro ha sido ANULADO y no puede ser editado.
                </div>
            </asp:Panel>

            <!-- Tipo de Registro -->
            <div class="tipo-info-banner">
                <span class="tipo-label"><i class="fas fa-tag mr-1"></i>Tipo de Registro:</span>
                <asp:Label ID="lblTipoAbastecimiento" runat="server" CssClass="tipo-badge tipo-abastecimiento" Text="ABASTECIMIENTO" />
                <asp:DropDownList ID="ddlTipoAbastecimientoEdit" runat="server" CssClass="ddl-tipo-edit" Visible="false">
                    <asp:ListItem Value="ABASTECIMIENTO">Abastecimiento</asp:ListItem>
                    <asp:ListItem Value="MANTENIMIENTO">Mantenimiento</asp:ListItem>
                    <asp:ListItem Value="OTRO">Otro</asp:ListItem>
                </asp:DropDownList>
                <asp:HiddenField ID="hdnTipoAbastecimiento" runat="server" Value="" />
            </div>
            <div id="divMotivoHint" class="motivo-hint-edit" style="display:none;"></div>

            <!-- Información General -->
            <div class="card mb-4">
                <div class="card-header">
                    <i class="fas fa-truck me-2"></i>Información General
                </div>
                <div class="card-body">
                    <div class="row">
                        <div class="col-md-3">
                            <label for="txtNumAbastecimiento" class="form-label">N° Abastecimiento:</label>
                            <asp:TextBox ID="txtNumAbastecimiento" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                        </div>
                        <div class="col-md-3">
                            <label for="ddlTipoVehiculo" class="form-label">Tipo:</label>
                            <asp:DropDownList ID="ddlTipoVehiculo" runat="server" CssClass="form-control" Enabled="false">
                                <asp:ListItem Value="camioneta">Camioneta</asp:ListItem>
                                <asp:ListItem Value="camion">Camión</asp:ListItem>
                                <asp:ListItem Value="trailer">Trailer</asp:ListItem>
                                <asp:ListItem Value="otro">Otro</asp:ListItem>
                            </asp:DropDownList>
                        </div>
                        <div class="col-md-3">
                            <label for="txtPlaca" class="form-label">Placa:</label>
                            <asp:TextBox ID="txtPlaca" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                        </div>
                        <div class="col-md-3">
                            <label for="txtCarreta" class="form-label">Carreta:</label>
                            <asp:TextBox ID="txtCarreta" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                        </div>
                    </div>
                    <div class="row mt-3">
                        <div class="col-md-6">
                            <label for="txtConductor" class="form-label">Conductor:</label>
                            <asp:TextBox ID="txtConductor" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                        </div>
                        <div class="col-md-6">
                            <label for="txtRuta" class="form-label">Ruta:</label>
                            <asp:TextBox ID="txtRuta" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Ruta y Producto -->
            <div class="card mb-4">
                <div class="card-header">
                    <i class="fas fa-route me-2"></i>Ruta y Producto
                </div>
                <div class="card-body">
                    <div class="row">
                        <div class="col-md-6">
                            <label for="txtProducto" class="form-label">Producto:</label>
                            <asp:TextBox ID="txtProducto" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                        </div>
                        <div class="col-md-6">
                            <label for="txtLugarAbastecimiento" class="form-label">Lugar de Abastecimiento:</label>
                            <asp:TextBox ID="txtLugarAbastecimiento" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                        </div>
                    </div>
                    <div class="row mt-3">
                        <div class="col-md-6">
                            <label for="txtFechaAbastecimiento" class="form-label">Fecha:</label>
                            <asp:TextBox ID="txtFechaAbastecimiento" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                        </div>
                        <div class="col-md-6">
                            <label for="txtHoraAbastecimiento" class="form-label">Hora:</label>
                            <asp:TextBox ID="txtHoraAbastecimiento" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Detalles de Consumo -->
            <div class="card mb-4">
                <div class="card-header">
                    <i class="fas fa-tachometer-alt me-2"></i>Detalles de Consumo
                </div>
                <div class="card-body">
                    <div class="row">
                        <div class="col-md-6">
                            <div class="row">
                                <div class="col-md-6 form-group">
                                    <label for="txtGLRuta" class="form-label">GL Ruta Asignada:</label>
                                    <asp:TextBox ID="txtGLRuta" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                                </div>
                                <div class="col-md-6 form-group">
                                    <label for="txtGLComprados" class="form-label">GL Comprados en Ruta:</label>
                                    <asp:TextBox ID="txtGLComprados" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-6 form-group">
                                    <label for="txtTotalGL" class="form-label">GL Total Abastecidos:</label>
                                    <asp:TextBox ID="txtTotalGL" runat="server" CssClass="form-control calculated-field" ReadOnly="true"></asp:TextBox>
                                </div>
                                <div class="col-md-6 form-group">
                                    <label for="txtGLFinal" class="form-label">GL Trae al Finalizar:</label>
                                    <asp:TextBox ID="txtGLFinal" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-6 form-group">
                                    <label for="txtGLConsumidos" class="form-label">GL Total Consumidos:</label>
                                    <asp:TextBox ID="txtGLConsumidos" runat="server" CssClass="form-control calculated-field" ReadOnly="true"></asp:TextBox>
                                </div>
                                <div class="col-md-6 form-group">
                                    <label for="txtPrecioDolar" class="form-label">Precio del Dólar:</label>
                                    <asp:TextBox ID="txtPrecioDolar" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                                </div>
                            </div>
                        </div>
                        <div class="col-md-6">
                            <div class="row">
                                <div class="col-md-6 form-group">
                                    <label for="txtMontoTotal" class="form-label">Monto Total GL:</label>
                                    <asp:TextBox ID="txtMontoTotal" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                                </div>
                                <div class="col-md-6 form-group">
                                    <label for="txtDistancia" class="form-label">Distancia en KM:</label>
                                    <asp:TextBox ID="txtDistancia" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-6 form-group">
                                    <label for="txtConsumoComputador" class="form-label">Consumo Computador:</label>
                                    <asp:TextBox ID="txtConsumoComputador" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                                </div>
                                <div class="col-md-6 form-group">
                                    <label for="txtHoraRetorno" class="form-label">Hora Retorno:</label>
                                    <asp:TextBox ID="txtHoraRetorno" runat="server" CssClass="form-control" ReadOnly="true"></asp:TextBox>
                                </div>
                            </div>
                            <div class="fuel-section mt-3">
                                <h6 class="mb-2">Visualización de consumo de combustible</h6>
                                <div class="fuel-tank">
                                    <div class="fuel-level" id="fuelLevelVisual"></div>
                                </div>
                                <div class="fuel-markers">
                                    <span>0%</span>
                                    <span>25%</span>
                                    <span>50%</span>
                                    <span>75%</span>
                                    <span>100%</span>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Cálculos y Resultados -->
                    <div class="calculation-box mt-3">
                        <div class="calculation-result">
                            <span>Rendimiento promedio (KM/GL):</span>
                            <asp:Label ID="lblRendimientoPromedio" runat="server" Text="0.00"></asp:Label>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Observaciones -->
            <div class="card mb-4 observaciones-section">
                <div class="card-header">
                    <i class="fas fa-clipboard me-2"></i>Observaciones
                </div>
                <div class="card-body">
                    <div class="row">
                        <div class="col-12">
                            <asp:TextBox ID="txtObservaciones" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="4" ReadOnly="true"></asp:TextBox>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Botones de Acción -->
            <div class="text-end mt-4">
                <asp:Button ID="btnHabilitarEdicion" runat="server" CssClass="btn btn-primary" Text="Habilitar Edición" OnClick="HabilitarEdicion" />
                <asp:Button ID="btnGuardarCambios" runat="server" CssClass="btn btn-success" Text="Guardar Cambios" OnClick="GuardarCambios" Visible="false" />
                <asp:Button ID="btnCancelar" runat="server" CssClass="btn btn-secondary" Text="Cancelar" OnClick="Cancelar" />
                <button type="button" id="btnDescargarPdfAbast" class="btn btn-success" onclick="descargarPdfAbastecimiento();">
                    <i class="fas fa-file-pdf"></i> Descargar PDF
                </button>
                <asp:Button ID="btnImprimir" runat="server" CssClass="btn btn-info" Text="Imprimir" OnClientClick="window.print(); return false;" />
                <asp:Button ID="btnAnular" runat="server" CssClass="btn btn-anular" Text="Anular" OnClick="AnularAbastecimiento" OnClientClick="return confirm('¿Está seguro que desea ANULAR este registro de abastecimiento?\n\nEsta acción marcará el registro como anulado.');" />
                <asp:Button ID="btnEliminar" runat="server" CssClass="btn btn-eliminar" Text="Eliminar" OnClick="EliminarAbastecimiento" OnClientClick="return confirm('¿Está seguro que desea ELIMINAR este registro de abastecimiento?\n\nEsta acción es IRREVERSIBLE y eliminará permanentemente el registro.');" />
            </div>

            <div class="form-group text-center mt-3">
                <asp:Label ID="lblMensaje" runat="server" CssClass="text-info"></asp:Label>
            </div>
        </asp:Panel>

        <!-- Panel de No Resultados -->
        <asp:Panel ID="pnlNoResultados" runat="server" Visible="false">
            <div class="alert alert-warning">
                <strong>No se encontró ningún abastecimiento con el número especificado.</strong>
                <p>Verifique el número e intente nuevamente o <a href="AgregarAbastecimiento.aspx" class="alert-link">cree un nuevo registro de abastecimiento</a>.</p>
            </div>
        </asp:Panel>
    </div>

    <!-- Scripts para cálculos automáticos -->
    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            txtNumAbastecimiento: '<%= txtNumAbastecimiento.ClientID %>',
            txtGLRuta: '<%= txtGLRuta.ClientID %>',
            txtGLComprados: '<%= txtGLComprados.ClientID %>',
            txtGLFinal: '<%= txtGLFinal.ClientID %>',
            txtTotalGL: '<%= txtTotalGL.ClientID %>',
            txtGLConsumidos: '<%= txtGLConsumidos.ClientID %>',
            txtDistancia: '<%= txtDistancia.ClientID %>',
            lblRendimientoPromedio: '<%= lblRendimientoPromedio.ClientID %>',
            ddlTipoAbastecimientoEdit: '<%= ddlTipoAbastecimientoEdit.ClientID %>',
            hdnTipoAbastecimiento: '<%= hdnTipoAbastecimiento.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/BuscarAbastecimiento.js") %>"></script>
</asp:Content>

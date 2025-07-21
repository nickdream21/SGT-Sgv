<%@ Page Title="Buscar Abastecimiento de Combustible" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="BuscarAbastecimiento.aspx.cs" Inherits="WebSGV.Views.BusquedaAbastecimiento" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        :root {
            --primary-color: #0056b3;
            --secondary-color: #0062cc;
            --accent-color: #f0f7ff;
            --border-color: #dee2e6;
        }

        .buscar-abastecimiento-container {
            background-color: white;
            border-radius: 6px;
            box-shadow: 0 0.5rem 1rem rgba(0, 0, 0, 0.1);
            padding: 20px;
            margin-bottom: 30px;
        }

        .header-container {
            border-bottom: 2px solid var(--primary-color);
            padding-bottom: 15px;
            margin-bottom: 25px;
        }

        .abastecimiento-header {
            color: var(--primary-color);
            font-size: 1.6rem;
            font-weight: 700;
            margin: 0;
        }

        .search-section {
            background-color: #f8f9fa;
            border-radius: 6px;
            padding: 20px;
            margin-bottom: 25px;
            border: 1px solid var(--border-color);
        }

        .card {
            border: none;
            margin-bottom: 25px;
            box-shadow: 0 0.125rem 0.25rem rgba(0, 0, 0, 0.075);
        }

        .card-header {
            background-color: var(--primary-color);
            color: white;
            font-weight: 600;
            font-size: 1.1rem;
            padding: 12px 20px;
            border-radius: 4px 4px 0 0;
        }

        .card-body {
            padding: 20px;
            background-color: #fafafa;
            border: 1px solid #e9ecef;
            border-top: none;
            border-radius: 0 0 4px 4px;
        }

        .form-label {
            font-weight: 500;
            color: #495057;
        }

        .form-control {
            border: 1px solid #ced4da;
            padding: 8px 12px;
            height: auto;
        }

            .form-control:focus {
                border-color: #80bdff;
                box-shadow: 0 0 0 0.2rem rgba(0, 123, 255, 0.25);
            }

        .btn-primary {
            background-color: var(--primary-color);
            border-color: var(--primary-color);
            padding: 8px 20px;
        }

            .btn-primary:hover {
                background-color: var(--secondary-color);
                border-color: var(--secondary-color);
            }

        .calculation-box {
            background-color: var(--accent-color);
            border: 1px solid #d1e7ff;
            border-radius: 4px;
            padding: 15px;
            margin-top: 15px;
        }

        .calculation-result {
            display: flex;
            justify-content: space-between;
            font-weight: 600;
            color: var(--primary-color);
        }

        /* Estilos para campos calculados */
        .calculated-field {
            background-color: #f0f7ff;
            color: #495057;
            font-weight: 500;
        }

        /* Indicador visual de tanque de combustible */
        .fuel-section {
            background-color: #f8f9fa;
            border-radius: 6px;
            border: 1px solid #e9ecef;
            padding: 15px;
            margin-top: 20px;
        }

        .fuel-tank {
            height: 30px;
            background-color: #e9ecef;
            border-radius: 15px;
            position: relative;
            overflow: hidden;
            margin-bottom: 10px;
        }

        .fuel-level {
            height: 100%;
            background: linear-gradient(90deg, #28a745, #67c977);
            border-radius: 15px 0 0 15px;
            width: 75%;
        }

        .fuel-markers {
            display: flex;
            justify-content: space-between;
            font-size: 0.8rem;
            color: #6c757d;
        }

        .observaciones-section {
            margin-top: 20px;
        }

        .alert {
            border-radius: 4px;
            padding: 15px 20px;
            margin-bottom: 20px;
        }

        .alert-warning {
            background-color: #fff3cd;
            border-color: #ffeeba;
            color: #856404;
        }

        /* Estilos para el modo de edición */
        .edit-mode .form-control:not([readonly]) {
            background-color: #fffdf0;
            border-color: #ffc107;
        }
    </style>

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
                <asp:Button ID="btnCancelar" runat="server" CssClass="btn btn-danger" Text="Cancelar" OnClick="Cancelar" />
                <asp:Button ID="btnImprimir" runat="server" CssClass="btn btn-info" Text="Imprimir" OnClientClick="window.print(); return false;" />
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
    <script type="text/javascript">
        // Función para actualizar la visualización del nivel de combustible
        function actualizarNivelCombustible(actual, total) {
            if (total > 0) {
                var porcentaje = (actual / total) * 100;
                document.getElementById('fuelLevelVisual').style.width = porcentaje + '%';
            } else {
                document.getElementById('fuelLevelVisual').style.width = '0%';
            }
        }

        // Función para calcular totales automáticamente (para cuando está en modo edición)
        function calcularTotales() {
            var glRuta = parseFloat(document.getElementById('<%= txtGLRuta.ClientID %>').value) || 0;
            var glComprados = parseFloat(document.getElementById('<%= txtGLComprados.ClientID %>').value) || 0;
            var glFinal = parseFloat(document.getElementById('<%= txtGLFinal.ClientID %>').value) || 0;

            // Calcular total abastecido
            var totalAbastecido = glRuta + glComprados;
            document.getElementById('<%= txtTotalGL.ClientID %>').value = totalAbastecido.toFixed(2);

            // Calcular total consumido
            var totalConsumido = totalAbastecido - glFinal;
            document.getElementById('<%= txtGLConsumidos.ClientID %>').value = totalConsumido.toFixed(2);

            // Actualizar visualización del nivel de combustible
            actualizarNivelCombustible(glFinal, totalAbastecido);

            // Calcular rendimiento
            calcularRendimiento();
        }

        // Función para calcular rendimiento
        function calcularRendimiento() {
            var distancia = parseFloat(document.getElementById('<%= txtDistancia.ClientID %>').value) || 0;
            var consumido = parseFloat(document.getElementById('<%= txtGLConsumidos.ClientID %>').value) || 0;

            if (distancia > 0 && consumido > 0) {
                var rendimiento = distancia / consumido;
                document.getElementById('<%= lblRendimientoPromedio.ClientID %>').textContent = rendimiento.toFixed(2);
                return rendimiento.toFixed(2);
            } else {
                document.getElementById('<%= lblRendimientoPromedio.ClientID %>').textContent = "0.00";
                return "0.00";
            }
        }

        // Inicializar visualización al cargar la página
        window.onload = function () {
            try {
                var txtTotalGLElement = document.getElementById('<%= txtTotalGL.ClientID %>');
                var txtGLFinalElement = document.getElementById('<%= txtGLFinal.ClientID %>');

                if (txtTotalGLElement && txtGLFinalElement) {
                    var totalGL = parseFloat(txtTotalGLElement.value) || 0;
                    var glFinal = parseFloat(txtGLFinalElement.value) || 0;

                    actualizarNivelCombustible(glFinal, totalGL);
                } else {
                    console.log("Elementos no encontrados: txtTotalGL o txtGLFinal");
                }
            } catch (error) {
                console.error("Error en window.onload:", error);
            }
        };
    </script>
</asp:Content>

<%@ Page Title="Buscar Orden de Viaje" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="BuscarOrdenViaje.aspx.cs" Inherits="WebSGV.Views.BuscarOrdenViaje" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <!-- Asegúrate de que estas referencias estén en el orden correcto -->
    <script src="https://code.jquery.com/jquery-3.6.4.min.js"></script>
    <script src="https://code.jquery.com/ui/1.13.2/jquery-ui.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/select2/4.1.0-rc.0/js/select2.min.js"></script>
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/select2/4.1.0-rc.0/css/select2.min.css" />

    <div class="container">
        <h2 class="mt-4 mb-4">Búsqueda de Orden de Viaje</h2>

        <!-- Sección de búsqueda -->
        <div class="row mb-4">
            <div class="col-md-8">
                <label for="txtBuscarOrdenViaje">N° Orden Viaje:</label>
                <asp:TextBox ID="txtBuscarOrdenViaje" runat="server" CssClass="form-control" placeholder="Ingrese el N° de Orden de Viaje a buscar"></asp:TextBox>
            </div>
            <div class="col-md-4 d-flex align-items-end">
                <asp:Button ID="btnBuscar" runat="server" Text="Buscar" CssClass="btn btn-primary" OnClick="btnBuscar_Click" />
            </div>
        </div>

        <!-- Panel de resultados (inicialmente oculto) -->
        <asp:Panel ID="pnlResultados" runat="server" Visible="false">
            <h3>Información de la Orden de Viaje</h3>

            <div class="tabs-container mt-4">
                <!-- Pestañas -->
                <ul class="nav nav-tabs" id="ordenViajeTabs" role="tablist">
                    <li class="nav-item">
                        <a class="nav-link active" id="datos-tab" data-toggle="tab" href="#datos" role="tab" aria-controls="datos" aria-selected="true">Datos del Viaje</a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link" id="liquidacion-tab" data-toggle="tab" href="#liquidacion" role="tab" aria-controls="liquidacion" aria-selected="false">Liquidación</a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link" id="guias-tab" data-toggle="tab" href="#guias" role="tab" aria-controls="guias" aria-selected="false">Guías</a>
                    </li>
                    <li class="nav-item">&nbsp;</li>
                </ul>

                <!-- Contenido de las pestañas -->
                <div class="tab-content" id="ordenViajeContent">
                    <!-- Pestaña 1: Datos del Viaje -->
                    <div class="tab-pane fade show active" id="datos" role="tabpanel" aria-labelledby="datos-tab">
                        <h4 class="tab-header text-center mb-4">Datos del Viaje</h4>
                        <div id="formDatosViaje">
                            <!-- Primera fila: N° CPI y N° Orden Viaje -->
                            <div class="row mb-3">
                                <div class="col-md-6 form-group">
                                    <label for="txtCPI">N° CPI:</label>
                                    <asp:TextBox ID="txtCPI" runat="server" CssClass="form-control" Enabled="false"></asp:TextBox>
                                </div>
                                <div class="col-md-6 form-group">
                                    <label for="txtOrdenViaje">N° Orden Viaje:</label>
                                    <asp:TextBox ID="txtOrdenViaje" runat="server" CssClass="form-control" Enabled="false"></asp:TextBox>
                                </div>
                            </div>

                            <!-- Segunda fila: Fechas y horas -->
                            <div class="row mb-3">
                                <div class="col-md-3 form-group">
                                    <label for="txtFechaSalida">Fecha de Salida:</label>
                                    <asp:TextBox ID="txtFechaSalida" runat="server" CssClass="form-control" TextMode="Date" Enabled="false"></asp:TextBox>
                                </div>
                                <div class="col-md-3 form-group">
                                    <label for="txtHoraSalida">Hora de Salida:</label>
                                    <asp:TextBox ID="txtHoraSalida" runat="server" CssClass="form-control" TextMode="Time" Enabled="false"></asp:TextBox>
                                </div>
                                <div class="col-md-3 form-group">
                                    <label for="txtFechaLlegada">Fecha de Llegada:</label>
                                    <asp:TextBox ID="txtFechaLlegada" runat="server" CssClass="form-control" TextMode="Date" Enabled="false"></asp:TextBox>
                                </div>
                                <div class="col-md-3 form-group">
                                    <label for="txtHoraLlegada">Hora de Llegada:</label>
                                    <asp:TextBox ID="txtHoraLlegada" runat="server" CssClass="form-control" TextMode="Time" Enabled="false"></asp:TextBox>
                                </div>
                            </div>

                            <!-- Tercera fila: Cliente, Placas -->
                            <div class="row mb-3">
                                <div class="col-md-4 form-group">
                                    <label for="ddlCliente">Cliente:</label>
                                    <asp:DropDownList ID="ddlCliente" runat="server" CssClass="form-control select2" Enabled="false">
                                    </asp:DropDownList>
                                </div>
                                <div class="col-md-4 form-group">
                                    <label for="ddlPlacaTracto">Placa Tracto:</label>
                                    <asp:DropDownList ID="ddlPlacaTracto" runat="server" CssClass="form-control select2" Enabled="false">
                                    </asp:DropDownList>
                                </div>
                                <div class="col-md-4 form-group">
                                    <label for="ddlPlacaCarreta">Placa Carreta:</label>
                                    <asp:DropDownList ID="ddlPlacaCarreta" runat="server" CssClass="form-control select2" Enabled="false">
                                    </asp:DropDownList>
                                </div>
                            </div>

                            <!-- Cuarta fila: Conductor y Observaciones -->
                            <div class="row mb-4">
                                <div class="col-md-6 form-group">
                                    <label for="ddlConductor">Conductor:</label>
                                    <asp:DropDownList ID="ddlConductor" runat="server" CssClass="form-control select2" Enabled="false">
                                    </asp:DropDownList>
                                </div>
                                <div class="col-md-6 form-group">
                                    <label for="txtObservaciones">Observaciones:</label>
                                    <asp:TextBox ID="txtObservaciones" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3" Enabled="false"></asp:TextBox>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Pestaña 2: Liquidación -->
                    <div class="tab-pane fade" id="liquidacion" role="tabpanel" aria-labelledby="liquidacion-tab">
                        <h4 class="tab-header">Liquidación</h4>
                        <div id="formLiquidacion">
                            <!-- Tabla de ingresos -->
                            <h5>Ingresos</h5>
                            <table class="table table-bordered liquidacion-tabla-ingresos">
                                <thead class="liquidacion-tabla-cabecera">
                                    <tr>
                                        <th>#</th>
                                        <th>Ingresos</th>
                                        <th>Descripción</th>
                                        <th>Soles (S/)</th>
                                        <th>Dólares ($)</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    <tr>
                                        <td>1</td>
                                        <td>Despacho</td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtDescDespacho" CssClass="form-control" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtDespachoSoles" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtDespachoDolares" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>2</td>
                                        <td>Mensualidad</td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtDescMensualidad" CssClass="form-control" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtMensualidadSoles" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtMensualidadDolares" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>3</td>
                                        <td>Otros Autorizados</td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtDescOtros" CssClass="form-control" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtOtrosSoles" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtOtrosDolares" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>4</td>
                                        <td>Préstamo</td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtDescPrestamo" CssClass="form-control" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtPrestamoSoles" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtPrestamoDolares" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                    </tr>
                                </tbody>
                                <!-- Ingresos adicionales se cargarán dinámicamente -->
                                <asp:Repeater ID="rptIngresosAdicionales" runat="server">
                                    <ItemTemplate>
                                        <tr>
                                            <td><%# Container.ItemIndex + 5 %></td>
                                            <td>
                                                <asp:HiddenField ID="hfIdIngresoAdicional" runat="server" Value='<%# Eval("IdIngresoAdicional") %>' />
                                                <asp:TextBox runat="server" ID="txtNombreIngreso" CssClass="form-control" Text='<%# Eval("NombreCategoria") %>' Enabled="false"></asp:TextBox>
                                            </td>
                                            <td>
                                                <asp:TextBox runat="server" ID="txtDescripcionIngreso" CssClass="form-control" Text='<%# Eval("Descripcion") %>' Enabled="false"></asp:TextBox>
                                            </td>
                                            <td>
                                                <asp:TextBox runat="server" ID="txtIngresoSoles" CssClass="form-control" TextMode="Number" Step="0.01" Text='<%# Eval("Soles") %>' Enabled="false"></asp:TextBox>
                                            </td>
                                            <td>
                                                <asp:TextBox runat="server" ID="txtIngresoDolares" CssClass="form-control" TextMode="Number" Step="0.01" Text='<%# Eval("Dolares") %>' Enabled="false"></asp:TextBox>
                                            </td>
                                        </tr>
                                    </ItemTemplate>
                                </asp:Repeater>
                            </table>

                            <!-- Tabla de Gastos -->
                            <h5 class="mt-4">Gastos</h5>
                            <table class="table table-bordered liquidacion-tabla-gastos">
                                <thead class="liquidacion-tabla-cabecera">
                                    <tr>
                                        <th>#</th>
                                        <th>Gastos</th>
                                        <th>Descripción</th>
                                        <th>Soles (S/)</th>
                                        <th>Dólares ($)</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    <tr>
                                        <td>1</td>
                                        <td>Peajes</td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtDescPeajes" CssClass="form-control" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtPeajesSoles" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtPeajesDolares" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>2</td>
                                        <td>Alimentación</td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtDescAlimentacion" CssClass="form-control" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtAlimentacionSoles" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtAlimentacionDolares" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>3</td>
                                        <td>Apoyo-Seguridad</td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtDescApoyoSeguridad" CssClass="form-control" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtApoyoSeguridadSoles" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtApoyoSeguridadDolares" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>4</td>
                                        <td>Reparaciones Varios</td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtDescReparaciones" CssClass="form-control" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtReparacionesSoles" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtReparacionesDolares" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>5</td>
                                        <td>Movilidad</td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtDescMovilidad" CssClass="form-control" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtMovilidadSoles" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtMovilidadDolares" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>6</td>
                                        <td>Encapada/Descencarpada</td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtDescEncapada" CssClass="form-control" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtEncapadaSoles" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtEncapadaDolares" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>7</td>
                                        <td>Hospedaje</td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtDescHospedaje" CssClass="form-control" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtHospedajeSoles" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtHospedajeDolares" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td>8</td>
                                        <td>Combustible</td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtDescCombustible" CssClass="form-control" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtCombustibleSoles" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                        <td>
                                            <asp:TextBox runat="server" ID="txtCombustibleDolares" CssClass="form-control" TextMode="Number" Step="0.01" Enabled="false"></asp:TextBox>
                                        </td>
                                    </tr>
                                </tbody>
                                <!-- Gastos adicionales se cargarán dinámicamente -->
                                <asp:Repeater ID="rptGastosAdicionales" runat="server">
                                    <ItemTemplate>
                                        <tr>
                                            <td><%# Container.ItemIndex + 9 %></td>
                                            <td>
                                                <asp:HiddenField ID="hfIdGastoAdicional" runat="server" Value='<%# Eval("idCategoriaAdicional") %>' />
                                                <asp:TextBox runat="server" ID="txtNombreGasto" CssClass="form-control" Text='<%# Eval("NombreCategoria") %>' Enabled="false"></asp:TextBox>
                                            </td>
                                            <td>
                                                <asp:TextBox runat="server" ID="txtDescripcionGasto" CssClass="form-control" Text='<%# Eval("Descripcion") %>' Enabled="false"></asp:TextBox>
                                            </td>
                                            <td>
                                                <asp:TextBox runat="server" ID="txtGastoSoles" CssClass="form-control" TextMode="Number" Step="0.01" Text='<%# Eval("Soles") %>' Enabled="false"></asp:TextBox>
                                            </td>
                                            <td>
                                                <asp:TextBox runat="server" ID="txtGastoDolares" CssClass="form-control" TextMode="Number" Step="0.01" Text='<%# Eval("Dolares") %>' Enabled="false"></asp:TextBox>
                                            </td>
                                        </tr>
                                    </ItemTemplate>
                                </asp:Repeater>
                            </table>

                            <!-- Observaciones de Liquidación -->
                            <div class="form-group mt-3">
                                <label for="txtObservacionesLiquidacion">Observaciones de Liquidación:</label>
                                <asp:TextBox ID="txtObservacionesLiquidacion" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3" Enabled="false"></asp:TextBox>
                            </div>
                        </div>
                    </div>

                    <!-- Pestaña 3: Guías -->
                    <div class="tab-pane fade" id="guias" role="tabpanel" aria-labelledby="guias-tab">
                        <h4 class="tab-header">Guías de Transporte</h4>
                        <div id="formGuias">
                            <!-- Número de Guías -->
                            <div class="row">
                                <div class="col-md-6 form-group">
                                    <label for="txtGuiaTransportista">N° Guía Transportista:</label>
                                    <asp:TextBox ID="txtGuiaTransportista" runat="server" CssClass="form-control" Enabled="false"></asp:TextBox>
                                </div>
                                <div class="col-md-6 form-group">
                                    <label for="txtGuiaCliente">N° Guía Cliente:</label>
                                    <asp:TextBox ID="txtGuiaCliente" runat="server" CssClass="form-control" Enabled="false"></asp:TextBox>
                                </div>
                            </div>

                            <!-- Campo Rutas -->
                            <div class="row">
                                <div class="col-md-6 form-group">
                                    <label for="ddlRuta">Ruta:</label>
                                    <asp:DropDownList ID="ddlRuta" runat="server" CssClass="form-control" ClientIDMode="Static" Enabled="false" onchange="checkRutaAndShowFields()">
                                    </asp:DropDownList>
                                </div>
                            </div>

                            <!-- Campos adicionales para Ruta 2 -->
                            <div id="rutaDetails" style="display:none;">
                                <div class="row">
                                    <div class="col-md-6 form-group">
                                        <label for="ddlPlantaDescarga">Planta de Descarga:</label>
                                        <asp:DropDownList ID="ddlPlantaDescarga" runat="server" CssClass="form-control" Enabled="false">
                                        </asp:DropDownList>
                                    </div>
                                    <div class="col-md-6 form-group">
                                        <label for="txtManifiesto">N° Manifiesto:</label>
                                        <asp:TextBox ID="txtManifiesto" runat="server" CssClass="form-control" Enabled="false"></asp:TextBox>
                                    </div>
                                </div>
                            </div>

                            <h5 class="mt-4">Productos asociados</h5>
                            <!-- Tabla Productos -->
                            <asp:GridView ID="gvProductos" runat="server" AutoGenerateColumns="false" CssClass="table table-bordered guias-tabla-productos">
                                <Columns>
                                    <asp:BoundField DataField="idProducto" HeaderText="ID" Visible="false" />
                                    <asp:BoundField DataField="NombreProducto" HeaderText="Producto" />
                                    <asp:BoundField DataField="CantidadBolsas" HeaderText="Cantidad de Bolsas" />
                                </Columns>
                            </asp:GridView>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Botones de acción -->
            <div class="row mt-3 mb-4">
                <div class="col-md-12 text-right">
                    <asp:Button ID="btnHabilitarEdicion" runat="server" Text="Habilitar Edición" CssClass="btn btn-primary" OnClick="btnHabilitarEdicion_Click" />
                    <asp:Button ID="btnGuardarCambios" runat="server" Text="Guardar Cambios" CssClass="btn btn-success" Visible="false" OnClick="btnGuardarCambios_Click" />
                    <asp:Button ID="btnCancelar" runat="server" Text="Cancelar" CssClass="btn btn-danger" OnClick="btnCancelar_Click" />
                </div>
            </div>

            <asp:Label ID="lblMensaje" runat="server" CssClass="alert alert-info d-block" Visible="false"></asp:Label>
        </asp:Panel>
    </div>

    <script type="text/javascript">
        // Script específico para manejar los campos de ruta en esta página
        $(document).ready(function () {
            // Verificar ruta inicial y mostrar/ocultar campos
            setTimeout(function () {
                checkRutaAndShowFields();
            }, 200);

            // Manejar cambio en la ruta
            $('#ddlRuta').on('change', function () {
                checkRutaAndShowFields();
            });

            // Manejar clic en botón de habilitar edición
            $('#btnHabilitarEdicion').on('click', function () {
                setTimeout(function () {
                    checkRutaAndShowFields();
                }, 300);
            });

            // Manejar cambio de pestaña
            $('a[data-toggle="tab"]').on('shown.bs.tab', function (e) {
                if ($(e.target).attr('href') === "#guias") {
                    setTimeout(function () {
                        checkRutaAndShowFields();
                    }, 300);
                }
            });
        });

        // Función específica para verificar la ruta y mostrar los campos
        function checkRutaAndShowFields() {
            var rutaValue = $('#ddlRuta').val();
            var rutaText = $('#ddlRuta option:selected').text();

            console.log("Ruta seleccionada:", rutaValue, rutaText);

            if (rutaValue === '2' || rutaText.toLowerCase().indexOf('guayaquil') > -1) {
                $('#rutaDetails').show();
                console.log("Mostrando campos de Planta y Manifiesto");
            } else {
                $('#rutaDetails').hide();
                console.log("Ocultando campos de Planta y Manifiesto");
            }
        }
    </script>

    <script src="<%= ResolveUrl("~/Scripts/BuscarOrdenViaje.js") %>"></script>
</asp:Content>
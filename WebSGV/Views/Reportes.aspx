<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Reportes.aspx.cs" Inherits="WebSGV.Views.Reportes" EnableEventValidation="false" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <div class="row">
        <!-- Panel lateral de selección de reportes -->
        <div class="col-md-3">
            <div class="card shadow-sm mb-4">
                <div class="card-body">
                    <h5 class="card-title mb-3">
                        <i class="fas fa-file-alt text-primary mr-2"></i>
                        Tipos de Reportes
                    </h5>

                    <div class="list-group">
                        <asp:LinkButton ID="lnkConductor" runat="server" CssClass="list-group-item list-group-item-action d-flex align-items-center active" OnClick="lnkTipoReporte_Click" CommandArgument="conductor">
                            <i class="fas fa-user mr-2"></i> Reportes por Conductor
                        </asp:LinkButton>
                        <asp:LinkButton ID="lnkVehiculo" runat="server" CssClass="list-group-item list-group-item-action d-flex align-items-center" OnClick="lnkTipoReporte_Click" CommandArgument="vehiculo">
                            <i class="fas fa-truck mr-2"></i> Reportes por Vehículo
                        </asp:LinkButton>
                        <asp:LinkButton ID="lnkPedido" runat="server" CssClass="list-group-item list-group-item-action d-flex align-items-center" OnClick="lnkTipoReporte_Click" CommandArgument="pedido">
                            <i class="fas fa-clipboard-list mr-2"></i> Reportes por Pedido
                        </asp:LinkButton>
                        <asp:LinkButton ID="lnkFinanciero" runat="server" CssClass="list-group-item list-group-item-action d-flex align-items-center" OnClick="lnkTipoReporte_Click" CommandArgument="financiero">
                            <i class="fas fa-dollar-sign mr-2"></i> Reportes Financieros
                        </asp:LinkButton>
                        <asp:LinkButton ID="lnkCombustible" runat="server" CssClass="list-group-item list-group-item-action d-flex align-items-center" OnClick="lnkTipoReporte_Click" CommandArgument="combustible">
                            <i class="fas fa-gas-pump mr-2"></i> Reportes de Combustible
                        </asp:LinkButton>
                        <asp:LinkButton ID="lnkProducto" runat="server" CssClass="list-group-item list-group-item-action d-flex align-items-center" OnClick="lnkTipoReporte_Click" CommandArgument="producto">
                            <i class="fas fa-box mr-2"></i> Reportes por Producto
                        </asp:LinkButton>
                    </div>
                </div>
            </div>
        </div>

        <!-- Contenido principal -->
        <div class="col-md-9">
            <div class="card shadow-sm">
                <div class="card-body">
                    <div class="d-flex justify-content-between align-items-center mb-4">
                        <h4 class="card-title font-weight-bold mb-0">
                            <asp:Literal ID="litTituloReporte" runat="server" Text="Reportes por Conductor"></asp:Literal>
                        </h4>

                        <div>
                            <asp:Button ID="btnExportarExcel" runat="server" CssClass="btn btn-success" Text="Exportar a Excel" OnClick="btnExportarExcel_Click" />
                        </div>
                    </div>

                    <!-- Filtros Básicos -->
                    <div class="bg-light p-3 rounded mb-4">
                        <div class="row" id="filtrosBasicos">
                            <div class="col-md-3 mb-3">
                                <div class="form-group">
                                    <label>Fecha Desde</label>
                                    <asp:TextBox ID="txtFechaDesde" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                </div>
                            </div>
                            <div class="col-md-3 mb-3">
                                <div class="form-group">
                                    <label>Fecha Hasta</label>
                                    <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                </div>
                            </div>

                            <!-- Filtros específicos por tipo de reporte -->
                            <!-- Conductor -->
                            <asp:Panel ID="pnlFiltroConductor" runat="server" CssClass="col-md-3 mb-3">
                                <div class="form-group">
                                    <label>Conductor</label>
                                    <asp:DropDownList ID="ddlConductor" runat="server" CssClass="form-control" DataTextField="NombreCompleto" DataValueField="idConductor">
                                        <asp:ListItem Value="" Text="Todos los conductores" Selected="True"></asp:ListItem>
                                    </asp:DropDownList>
                                </div>
                            </asp:Panel>

                            <!-- Vehículo -->
                            <asp:Panel ID="pnlFiltroVehiculo" runat="server" CssClass="col-md-3 mb-3" Visible="false">
                                <div class="form-group">
                                    <label>Vehículo (Tracto)</label>
                                    <asp:DropDownList ID="ddlVehiculo" runat="server" CssClass="form-control" DataTextField="placaTracto" DataValueField="idTracto">
                                        <asp:ListItem Value="" Text="Todos los vehículos" Selected="True"></asp:ListItem>
                                    </asp:DropDownList>
                                </div>
                            </asp:Panel>

                            <!-- Pedido/CPIC -->
                            <asp:Panel ID="pnlFiltroPedido" runat="server" CssClass="col-md-3 mb-3" Visible="false">
                                <div class="form-group">
                                    <label>Número de Pedido (CPIC)</label>
                                    <asp:TextBox ID="txtNumeroPedido" runat="server" CssClass="form-control" placeholder="Ej. PED12345"></asp:TextBox>

                                </div>
                            </asp:Panel>

                            <!-- Financiero -->
                            <asp:Panel ID="pnlFiltroFinanciero" runat="server" CssClass="col-md-3 mb-3" Visible="false">
                                <div class="form-group">
                                    <label>Tipo de Transacción</label>
                                    <asp:DropDownList ID="ddlTipoTransaccion" runat="server" CssClass="form-control">
                                        <asp:ListItem Value="" Text="Todas las transacciones" Selected="True"></asp:ListItem>
                                        <asp:ListItem Value="ingresos" Text="Solo Ingresos"></asp:ListItem>
                                        <asp:ListItem Value="egresos" Text="Solo Egresos"></asp:ListItem>
                                    </asp:DropDownList>
                                </div>
                            </asp:Panel>

                            <!-- Combustible -->
                            <asp:Panel ID="pnlFiltroCombustible" runat="server" CssClass="col-md-3 mb-3" Visible="false">
                                <div class="form-group">
                                    <label>Lugar de Abastecimiento</label>
                                    <asp:DropDownList ID="ddlLugarAbastecimiento" runat="server" CssClass="form-control" DataTextField="nombreAbastecimiento" DataValueField="idLugarAbastecimiento">
                                        <asp:ListItem Value="" Text="Todos los lugares" Selected="True"></asp:ListItem>
                                    </asp:DropDownList>
                                </div>
                            </asp:Panel>

                            <!-- Producto -->
                            <asp:Panel ID="pnlFiltroProducto" runat="server" CssClass="col-md-3 mb-3" Visible="false">
                                <div class="form-group">
                                    <label>Producto</label>
                                    <asp:DropDownList ID="ddlProducto" runat="server" CssClass="form-control" DataTextField="nombre" DataValueField="idProducto">
                                        <asp:ListItem Value="" Text="Todos los productos" Selected="True"></asp:ListItem>
                                    </asp:DropDownList>
                                </div>
                            </asp:Panel>

                            <!-- Filtro Tipo de Reporte -->
                            <div class="col-md-3 mb-3">
                                <div class="form-group">
                                    <label>Tipo de Reporte</label>
                                    <asp:DropDownList ID="ddlTipoReporteDetalle" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlTipoReporteDetalle_SelectedIndexChanged">
                                    </asp:DropDownList>
                                </div>
                            </div>
                        </div>

                        <!-- Filtros personalizados (solo visible cuando se selecciona "Reporte Personalizado") -->
                        <asp:Panel ID="pnlFiltrosPersonalizados" runat="server" Visible="false">
                            <hr />
                            <h6 class="mb-3">Campos a mostrar en el reporte</h6>
                            <div class="row">
                                <div class="col-md-4">
                                    <div class="form-check mb-2">
                                        <asp:CheckBox ID="chkConductorInfo" runat="server" Text="Información del Conductor" CssClass="form-check-input" />
                                    </div>
                                    <div class="form-check mb-2">
                                        <asp:CheckBox ID="chkVehiculoInfo" runat="server" Text="Información del Vehículo" CssClass="form-check-input" />
                                    </div>
                                    <div class="form-check mb-2">
                                        <asp:CheckBox ID="chkRutaInfo" runat="server" Text="Información de la Ruta" CssClass="form-check-input" />
                                    </div>
                                </div>
                                <div class="col-md-4">
                                    <div class="form-check mb-2">
                                        <asp:CheckBox ID="chkProductoInfo" runat="server" Text="Información del Producto" CssClass="form-check-input" />
                                    </div>
                                    <div class="form-check mb-2">
                                        <asp:CheckBox ID="chkIngresoInfo" runat="server" Text="Información de Ingresos" CssClass="form-check-input" />
                                    </div>
                                    <div class="form-check mb-2">
                                        <asp:CheckBox ID="chkEgresoInfo" runat="server" Text="Información de Egresos" CssClass="form-check-input" />
                                    </div>
                                </div>
                                <div class="col-md-4">
                                    <div class="form-check mb-2">
                                        <asp:CheckBox ID="chkCombustibleInfo" runat="server" Text="Información de Combustible" CssClass="form-check-input" />
                                    </div>
                                    <div class="form-check mb-2">
                                        <asp:CheckBox ID="chkClienteInfo" runat="server" Text="Información del Cliente" CssClass="form-check-input" />
                                    </div>
                                    <div class="form-check mb-2">
                                        <asp:CheckBox ID="chkFacturaInfo" runat="server" Text="Información de Factura" CssClass="form-check-input" />
                                    </div>
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Agrupar resultados por</label>
                                        <asp:DropDownList ID="ddlAgrupamiento" runat="server" CssClass="form-control">
                                            <asp:ListItem Value="" Text="Sin agrupamiento" Selected="True"></asp:ListItem>
                                            <asp:ListItem Value="conductor" Text="Conductor"></asp:ListItem>
                                            <asp:ListItem Value="vehiculo" Text="Vehículo"></asp:ListItem>
                                            <asp:ListItem Value="cliente" Text="Cliente"></asp:ListItem>
                                            <asp:ListItem Value="producto" Text="Producto"></asp:ListItem>
                                            <asp:ListItem Value="mes" Text="Mes"></asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Ordenar por</label>
                                        <asp:DropDownList ID="ddlOrdenamiento" runat="server" CssClass="form-control">
                                            <asp:ListItem Value="fecha_desc" Text="Fecha (más reciente primero)" Selected="True"></asp:ListItem>
                                            <asp:ListItem Value="fecha_asc" Text="Fecha (más antigua primero)"></asp:ListItem>
                                            <asp:ListItem Value="monto_desc" Text="Monto (mayor a menor)"></asp:ListItem>
                                            <asp:ListItem Value="monto_asc" Text="Monto (menor a mayor)"></asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>
                            </div>
                        </asp:Panel>

                        <div class="text-right mt-3">
                            <asp:Button ID="btnLimpiarFiltros" runat="server" CssClass="btn btn-outline-secondary mr-2" Text="Limpiar Filtros" OnClick="btnLimpiarFiltros_Click" />
                            <asp:Button ID="btnGenerarReporte" runat="server" CssClass="btn btn-primary" Text="Generar Reporte" OnClick="btnGenerarReporte_Click" />
                        </div>
                    </div>

                    <!-- Panel de resultados oculto para mantener la compatibilidad con el código existente -->
                    <asp:Panel ID="pnlResultadosHidden" runat="server" Visible="false"></asp:Panel>
                </div>
            </div>
        </div>
    </div>

    <!-- Modal de Resultados - CORREGIDO -->
    <div class="modal fade" id="modalResultados" tabindex="-1" role="dialog" aria-labelledby="modalResultadosLabel" aria-hidden="true">
        <div class="modal-dialog modal-dialog-centered modal-dialog-scrollable modal-xl" role="document">
            <div class="modal-content">
                <div class="modal-header bg-primary text-black">
                    <h5 class="modal-title" id="modalResultadosLabel">
                        <i class="fas fa-chart-bar mr-2"></i>
                        <asp:Literal ID="litTituloResultados" runat="server"></asp:Literal>
                    </h5>
                    <button type="button" class="close text-black" data-dismiss="modal" aria-label="Close">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>
                <div class="modal-body p-4">
                    <asp:UpdatePanel ID="upResultados" runat="server" UpdateMode="Conditional">
                        <ContentTemplate>
                            <asp:Panel ID="pnlResultados" runat="server" Visible="true">
                                <div class="d-flex justify-content-between align-items-center mb-3">
                                    <asp:Label ID="lblTotalRegistros" runat="server" CssClass="badge badge-info p-2 font-weight-normal" Style="font-size: 14px;"></asp:Label>
                                    <div class="text-right">
                                        <button type="button" class="btn btn-sm btn-outline-secondary" id="btnImprimirResultados">
                                            <i class="fas fa-print mr-1"></i>Imprimir
                                        </button>
                                    </div>
                                </div>

                                <!-- Resumen de indicadores clave -->
                                <div class="row mb-4">
                                    <div class="col-md-3">
                                        <div class="card shadow-sm bg-gradient-primary text-white">
                                            <div class="card-body p-3 bg-primary">
                                                <h6 class="card-title mb-1 text-uppercase"><i class="fas fa-money-bill-wave mr-2"></i>Total Ingresos</h6>
                                                <h4 class="mb-0 font-weight-bold">
                                                    <asp:Literal ID="litTotalIngresos" runat="server"></asp:Literal></h4>
                                            </div>
                                        </div>
                                    </div>
                                    <div class="col-md-3">
                                        <div class="card shadow-sm bg-gradient-danger text-white">
                                            <div class="card-body p-3 bg-danger">
                                                <h6 class="card-title mb-1 text-uppercase"><i class="fas fa-file-invoice mr-2"></i>Total Egresos</h6>
                                                <h4 class="mb-0 font-weight-bold">
                                                    <asp:Literal ID="litTotalEgresos" runat="server"></asp:Literal></h4>
                                            </div>
                                        </div>
                                    </div>
                                    <div class="col-md-3">
                                        <div class="card shadow-sm bg-gradient-success text-white">
                                            <div class="card-body p-3 bg-success">
                                                <h6 class="card-title mb-1 text-uppercase"><i class="fas fa-balance-scale mr-2"></i>Balance</h6>
                                                <h4 class="mb-0 font-weight-bold">
                                                    <asp:Literal ID="litBalance" runat="server"></asp:Literal></h4>
                                            </div>
                                        </div>
                                    </div>
                                    <div class="col-md-3">
                                        <div class="card shadow-sm bg-gradient-info text-white">
                                            <div class="card-body p-3 bg-info">
                                                <h6 class="card-title mb-1 text-uppercase" id="indicadorAdicionalTitulo">
                                                    <i class="fas fa-gas-pump mr-2"></i>
                                                    <asp:Literal ID="litIndicadorAdicionalTitulo" runat="server" Text="Total Combustible"></asp:Literal>
                                                </h6>
                                                <h4 class="mb-0 font-weight-bold">
                                                    <asp:Literal ID="litIndicadorAdicional" runat="server"></asp:Literal></h4>
                                            </div>
                                        </div>
                                    </div>
                                </div>

                                <!-- Tabla de resultados -->
                                <div class="table-responsive mt-3" style="max-height: 60vh; overflow-y: auto;">
                                    <asp:GridView ID="gvReporte" runat="server" CssClass="table table-striped table-bordered table-hover"
                                        AutoGenerateColumns="False" AllowPaging="True"
                                        PageSize="10" OnPageIndexChanging="gvReporte_PageIndexChanging"
                                        OnSorting="gvReporte_Sorting">
                                        <Columns>
                                            <asp:BoundField DataField="ID" HeaderText="ID" SortExpression="ID" />
                                            <%-- Las demás columnas se generarán dinámicamente en el code-behind --%>
                                        </Columns>
                                        <PagerStyle CssClass="pagination-ys bg-light p-2" HorizontalAlign="Center" />
                                        <HeaderStyle CssClass="bg-light " />
                                        <RowStyle CssClass="align-middle" />
                                        <FooterStyle CssClass="bg-light font-weight-bold" />
                                        <EmptyDataTemplate>
                                            <div class="alert alert-info text-center">
                                                <i class="fas fa-info-circle mr-2"></i>
                                                No se encontraron datos para los criterios seleccionados.
                                            </div>
                                        </EmptyDataTemplate>
                                    </asp:GridView>
                                </div>
                            </asp:Panel>
                        </ContentTemplate>
                        <Triggers>
                            <asp:AsyncPostBackTrigger ControlID="btnGenerarReporte" EventName="Click" />
                        </Triggers>
                    </asp:UpdatePanel>
                </div>
                <div class="modal-footer bg-light">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">
                        <i class="fas fa-times mr-1"></i>Cerrar
                    </button>
                    <asp:Button ID="btnExportarExcelModal" runat="server" CssClass="btn btn-success" Text="Exportar a Excel" OnClick="btnExportarExcel_Click" />

                </div>
            </div>
        </div>
    </div>

    <!-- Modal de Filtros Avanzados -->
    <div class="modal fade" id="filtrosAvanzadosModal" tabindex="-1" role="dialog" aria-labelledby="filtrosAvanzadosModalLabel" aria-hidden="true">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title" id="filtrosAvanzadosModalLabel">Filtros Avanzados</h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                        <span aria-hidden="true">×</span>
                    </button>
                </div>
                <div class="modal-body">
                    <div class="row">
                        <!-- Filtros para reportes de conductor -->
                        <asp:Panel ID="pnlFiltrosAvanzadosConductor" runat="server" CssClass="col-12">
                            <h6 class="mb-3">Filtros para Conductor</h6>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>DNI</label>
                                        <asp:TextBox ID="txtDNIConductor" runat="server" CssClass="form-control"></asp:TextBox>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Nombre o Apellido</label>
                                        <asp:TextBox ID="txtNombreConductor" runat="server" CssClass="form-control"></asp:TextBox>
                                    </div>
                                </div>
                            </div>
                        </asp:Panel>

                        <!-- Filtros para reportes de vehículo -->
                        <asp:Panel ID="pnlFiltrosAvanzadosVehiculo" runat="server" CssClass="col-12" Visible="false">
                            <h6 class="mb-3">Filtros para Vehículo</h6>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Placa</label>
                                        <asp:TextBox ID="txtPlacaVehiculo" runat="server" CssClass="form-control"></asp:TextBox>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Marca</label>
                                        <asp:DropDownList ID="ddlMarcaVehiculo" runat="server" CssClass="form-control">
                                            <asp:ListItem Value="" Text="Todas" Selected="True"></asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Modelo</label>
                                        <asp:DropDownList ID="ddlModeloVehiculo" runat="server" CssClass="form-control">
                                            <asp:ListItem Value="" Text="Todos" Selected="True"></asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Carreta</label>
                                        <asp:DropDownList ID="ddlCarreta" runat="server" CssClass="form-control" DataTextField="placaCarreta" DataValueField="idCarreta">
                                            <asp:ListItem Value="" Text="Todas" Selected="True"></asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>
                            </div>
                        </asp:Panel>

                        <!-- Filtros para reportes de pedido -->
                        <asp:Panel ID="pnlFiltrosAvanzadosPedido" runat="server" CssClass="col-12" Visible="false">
                            <h6 class="mb-3">Filtros para Pedido</h6>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Cliente</label>
                                        <asp:DropDownList ID="ddlClientePedido" runat="server" CssClass="form-control" DataTextField="nombre" DataValueField="idCliente">
                                            <asp:ListItem Value="" Text="Todos" Selected="True"></asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Número de Factura</label>
                                        <asp:TextBox ID="txtNumeroFactura" runat="server" CssClass="form-control"></asp:TextBox>
                                    </div>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Valor Mínimo</label>
                                        <asp:TextBox ID="txtValorMinimo" runat="server" CssClass="form-control" TextMode="Number" Step="0.01"></asp:TextBox>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Valor Máximo</label>
                                        <asp:TextBox ID="txtValorMaximo" runat="server" CssClass="form-control" TextMode="Number" Step="0.01"></asp:TextBox>
                                    </div>
                                </div>
                            </div>
                        </asp:Panel>

                        <!-- Filtros para reportes financieros -->
                        <asp:Panel ID="pnlFiltrosAvanzadosFinanciero" runat="server" CssClass="col-12" Visible="false">
                            <h6 class="mb-3">Filtros Financieros</h6>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Moneda</label>
                                        <asp:DropDownList ID="ddlMoneda" runat="server" CssClass="form-control">
                                            <asp:ListItem Value="" Text="Ambas" Selected="True"></asp:ListItem>
                                            <asp:ListItem Value="soles" Text="Soles (S/)"></asp:ListItem>
                                            <asp:ListItem Value="dolares" Text="Dólares ($)"></asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Categoría</label>
                                        <asp:DropDownList ID="ddlCategoriaFinanciera" runat="server" CssClass="form-control">
                                            <asp:ListItem Value="" Text="Todas" Selected="True"></asp:ListItem>
                                            <asp:ListItem Value="peajes" Text="Peajes"></asp:ListItem>
                                            <asp:ListItem Value="alimentacion" Text="Alimentación"></asp:ListItem>
                                            <asp:ListItem Value="hospedaje" Text="Hospedaje"></asp:ListItem>
                                            <asp:ListItem Value="combustible" Text="Combustible"></asp:ListItem>
                                            <asp:ListItem Value="reparaciones" Text="Reparaciones"></asp:ListItem>
                                            <asp:ListItem Value="movilidad" Text="Movilidad"></asp:ListItem>
                                            <asp:ListItem Value="seguridad" Text="Apoyo Seguridad"></asp:ListItem>
                                            <asp:ListItem Value="encarpada" Text="Encarpada/Desencarpada"></asp:ListItem>
                                            <asp:ListItem Value="despacho" Text="Despacho"></asp:ListItem>
                                            <asp:ListItem Value="prestamos" Text="Préstamos"></asp:ListItem>
                                            <asp:ListItem Value="mensualidad" Text="Mensualidad"></asp:ListItem>
                                            <asp:ListItem Value="otros" Text="Otros"></asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Monto Mínimo</label>
                                        <asp:TextBox ID="txtMontoMinimo" runat="server" CssClass="form-control" TextMode="Number" Step="0.01"></asp:TextBox>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Monto Máximo</label>
                                        <asp:TextBox ID="txtMontoMaximo" runat="server" CssClass="form-control" TextMode="Number" Step="0.01"></asp:TextBox>
                                    </div>
                                </div>
                            </div>
                        </asp:Panel>

                        <!-- Filtros para reportes de combustible -->
                        <asp:Panel ID="pnlFiltrosAvanzadosCombustible" runat="server" CssClass="col-12" Visible="false">
                            <h6 class="mb-3">Filtros para Combustible</h6>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Producto</label>
                                        <asp:TextBox ID="txtProductoCombustible" runat="server" CssClass="form-control"></asp:TextBox>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Número de Abastecimiento</label>
                                        <asp:TextBox ID="txtNumeroAbastecimiento" runat="server" CssClass="form-control"></asp:TextBox>
                                    </div>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-4">
                                    <div class="form-group">
                                        <label>Galones Mínimos</label>
                                        <asp:TextBox ID="txtGalonesMinimos" runat="server" CssClass="form-control" TextMode="Number" Step="0.01"></asp:TextBox>
                                    </div>
                                </div>
                                <div class="col-md-4">
                                    <div class="form-group">
                                        <label>Rendimiento Mínimo (km/gal)</label>
                                        <asp:TextBox ID="txtRendimientoMinimo" runat="server" CssClass="form-control" TextMode="Number" Step="0.01"></asp:TextBox>
                                    </div>
                                </div>
                                <div class="col-md-4">
                                    <div class="form-group">
                                        <label>Incluir</label>
                                        <asp:DropDownList ID="ddlTipoReporteCombustible" runat="server" CssClass="form-control">
                                            <asp:ListItem Value="todos" Text="Todos los registros" Selected="True"></asp:ListItem>
                                            <asp:ListItem Value="sobrante" Text="Solo con combustible sobrante"></asp:ListItem>
                                            <asp:ListItem Value="comprado" Text="Solo con combustible comprado"></asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>
                            </div>
                        </asp:Panel>

                        <!-- Filtros para reportes de producto -->
                        <asp:Panel ID="pnlFiltrosAvanzadosProducto" runat="server" CssClass="col-12" Visible="false">
                            <h6 class="mb-3">Filtros para Producto</h6>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Cliente</label>
                                        <asp:DropDownList ID="ddlClienteProducto" runat="server" CssClass="form-control" DataTextField="nombre" DataValueField="idCliente">
                                            <asp:ListItem Value="" Text="Todos" Selected="True"></asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Planta de Descarga</label>
                                        <asp:DropDownList ID="ddlPlantaDescarga" runat="server" CssClass="form-control" DataTextField="nombre" DataValueField="idPlanta">
                                            <asp:ListItem Value="" Text="Todas" Selected="True"></asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Cantidad Mínima (Bolsas)</label>
                                        <asp:TextBox ID="txtCantidadMinimaBolsas" runat="server" CssClass="form-control" TextMode="Number"></asp:TextBox>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Peso Mínimo (Kg)</label>
                                        <asp:TextBox ID="txtPesoMinimo" runat="server" CssClass="form-control" TextMode="Number" Step="0.01"></asp:TextBox>
                                    </div>
                                </div>
                            </div>
                        </asp:Panel>

                        <!-- Filtros para reportes personalizados -->
                        <asp:Panel ID="pnlFiltrosAvanzadosPersonalizado" runat="server" CssClass="col-12" Visible="false">
                            <h6 class="mb-3">Filtros Avanzados Adicionales</h6>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Cliente</label>
                                        <asp:DropDownList ID="ddlClientePersonalizado" runat="server" CssClass="form-control" DataTextField="nombre" DataValueField="idCliente">
                                            <asp:ListItem Value="" Text="Todos" Selected="True"></asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-group">
                                        <label>Tipo de Carro</label>
                                        <asp:DropDownList ID="ddlTipoCarro" runat="server" CssClass="form-control" DataTextField="descripcion" DataValueField="idTipoCarro">
                                            <asp:ListItem Value="" Text="Todos" Selected="True"></asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>
                            </div>
                        </asp:Panel>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cerrar</button>
                    <asp:Button ID="btnAplicarFiltrosAvanzados" runat="server" CssClass="btn btn-primary" Text="Aplicar Filtros" OnClick="btnAplicarFiltrosAvanzados_Click" />
                </div>
            </div>
        </div>
    </div>

    <!-- Modal para visualizar detalles de una orden de viaje -->
    <div class="modal fade" id="detalleOrdenViajeModal" tabindex="-1" role="dialog" aria-labelledby="detalleOrdenViajeModalLabel" aria-hidden="true">
        <div class="modal-dialog modal-xl" role="document">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title" id="detalleOrdenViajeModalLabel">Detalle de Orden de Viaje</h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                        <span aria-hidden="true">×</span>
                    </button>
                </div>
                <div class="modal-body">
                    <asp:UpdatePanel ID="upDetalleOrdenViaje" runat="server">
                        <ContentTemplate>
                            <!-- Datos generales de la orden -->
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="card mb-3">
                                        <div class="card-header bg-primary text-white">
                                            <h6 class="mb-0">Información General</h6>
                                        </div>
                                        <div class="card-body">
                                            <table class="table table-sm table-borderless">
                                                <tr>
                                                    <th style="width: 40%">Número de Orden:</th>
                                                    <td>
                                                        <asp:Label ID="lblNumeroOrden" runat="server"></asp:Label></td>
                                                </tr>
                                                <tr>
                                                    <th>Cliente:</th>
                                                    <td>
                                                        <asp:Label ID="lblCliente" runat="server"></asp:Label></td>
                                                </tr>
                                                <tr>
                                                    <th>Conductor:</th>
                                                    <td>
                                                        <asp:Label ID="lblConductor" runat="server"></asp:Label></td>
                                                </tr>
                                                <tr>
                                                    <th>Fecha Salida:</th>
                                                    <td>
                                                        <asp:Label ID="lblFechaSalida" runat="server"></asp:Label></td>
                                                </tr>
                                                <tr>
                                                    <th>Fecha Llegada:</th>
                                                    <td>
                                                        <asp:Label ID="lblFechaLlegada" runat="server"></asp:Label></td>
                                                </tr>
                                                <tr>
                                                    <th>CPIC:</th>
                                                    <td>
                                                        <asp:Label ID="lblCPIC" runat="server"></asp:Label></td>
                                                </tr>
                                            </table>
                                        </div>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="card mb-3">
                                        <div class="card-header bg-info text-white">
                                            <h6 class="mb-0">Vehículo y Ruta</h6>
                                        </div>
                                        <div class="card-body">
                                            <table class="table table-sm table-borderless">
                                                <tr>
                                                    <th style="width: 40%">Tracto:</th>
                                                    <td>
                                                        <asp:Label ID="lblTracto" runat="server"></asp:Label></td>
                                                </tr>
                                                <tr>
                                                    <th>Carreta:</th>
                                                    <td>
                                                        <asp:Label ID="lblCarreta" runat="server"></asp:Label></td>
                                                </tr>
                                                <tr>
                                                    <th>Producto:</th>
                                                    <td>
                                                        <asp:Label ID="lblProducto" runat="server"></asp:Label></td>
                                                </tr>
                                                <tr>
                                                    <th>Planta Descarga:</th>
                                                    <td>
                                                        <asp:Label ID="lblPlantaDescarga" runat="server"></asp:Label></td>
                                                </tr>
                                                <tr>
                                                    <th>Guía Transportista:</th>
                                                    <td>
                                                        <asp:Label ID="lblGuiaTransportista" runat="server"></asp:Label></td>
                                                </tr>
                                                <tr>
                                                    <th>Observaciones:</th>
                                                    <td>
                                                        <asp:Label ID="lblObservaciones" runat="server"></asp:Label></td>
                                                </tr>
                                            </table>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <!-- Pestañas para detalles específicos -->
                            <ul class="nav nav-tabs" id="detalleOrdenViajeTabs" role="tablist">
                                <li class="nav-item">
                                    <a class="nav-link active" id="ingresos-tab" data-toggle="tab" href="#ingresos" role="tab" aria-controls="ingresos" aria-selected="true">Ingresos</a>
                                </li>
                                <li class="nav-item">
                                    <a class="nav-link" id="egresos-tab" data-toggle="tab" href="#egresos" role="tab" aria-controls="egresos" aria-selected="false">Egresos</a>
                                </li>
                                <li class="nav-item">
                                    <a class="nav-link" id="combustible-tab" data-toggle="tab" href="#combustible" role="tab" aria-controls="combustible" aria-selected="false">Combustible</a>
                                </li>
                                <li class="nav-item">
                                    <a class="nav-link" id="productos-tab" data-toggle="tab" href="#productos" role="tab" aria-controls="productos" aria-selected="false">Productos</a>
                                </li>
                            </ul>
                            <div class="tab-content" id="detalleOrdenViajeTabContent">
                                <!-- Pestaña de Ingresos -->
                                <div class="tab-pane fade show active" id="ingresos" role="tabpanel" aria-labelledby="ingresos-tab">
                                    <div class="table-responsive mt-3">
                                        <asp:GridView ID="gvIngresos" runat="server" CssClass="table table-striped table-sm" AutoGenerateColumns="false">
                                            <Columns>
                                                <asp:BoundField DataField="Tipo" HeaderText="Tipo de Ingreso" />
                                                <asp:BoundField DataField="Soles" HeaderText="Monto (S/)" DataFormatString="{0:N2}" />
                                                <asp:BoundField DataField="Dolares" HeaderText="Monto ($)" DataFormatString="{0:N2}" />
                                                <asp:BoundField DataField="Descripcion" HeaderText="Descripción" />
                                            </Columns>
                                            <EmptyDataTemplate>
                                                <div class="alert alert-info">No hay registros de ingresos para esta orden de viaje.</div>
                                            </EmptyDataTemplate>
                                        </asp:GridView>
                                    </div>
                                </div>
                                <!-- Pestaña de Egresos -->
                                <div class="tab-pane fade" id="egresos" role="tabpanel" aria-labelledby="egresos-tab">
                                    <div class="table-responsive mt-3">
                                        <asp:GridView ID="gvEgresos" runat="server" CssClass="table table-striped table-sm" AutoGenerateColumns="false">
                                            <Columns>
                                                <asp:BoundField DataField="Tipo" HeaderText="Tipo de Egreso" />
                                                <asp:BoundField DataField="Soles" HeaderText="Monto (S/)" DataFormatString="{0:N2}" />
                                                <asp:BoundField DataField="Dolares" HeaderText="Monto ($)" DataFormatString="{0:N2}" />
                                                <asp:BoundField DataField="Descripcion" HeaderText="Descripción" />
                                            </Columns>
                                            <EmptyDataTemplate>
                                                <div class="alert alert-info">No hay registros de egresos para esta orden de viaje.</div>
                                            </EmptyDataTemplate>
                                        </asp:GridView>
                                    </div>
                                </div>
                                <!-- Pestaña de Combustible -->
                                <div class="tab-pane fade" id="combustible" role="tabpanel" aria-labelledby="combustible-tab">
                                    <div class="table-responsive mt-3">
                                        <asp:GridView ID="gvCombustible" runat="server" CssClass="table table-striped table-sm" AutoGenerateColumns="false">
                                            <Columns>
                                                <asp:BoundField DataField="NumeroAbastecimiento" HeaderText="Nº Abastecimiento" />
                                                <asp:BoundField DataField="Producto" HeaderText="Producto" />
                                                <asp:BoundField DataField="LugarAbastecimiento" HeaderText="Lugar" />
                                                <asp:BoundField DataField="FechaHora" HeaderText="Fecha y Hora" />
                                                <asp:BoundField DataField="GalonesRutaAsignada" HeaderText="Galones Asignados" DataFormatString="{0:N2}" />
                                                <asp:BoundField DataField="GalonesCompradosRuta" HeaderText="Galones Comprados" DataFormatString="{0:N2}" />
                                                <asp:BoundField DataField="GalonesTotalAbastecidos" HeaderText="Total Abastecido" DataFormatString="{0:N2}" />
                                                <asp:BoundField DataField="GalonesTotalConsumidos" HeaderText="Total Consumido" DataFormatString="{0:N2}" />
                                                <asp:BoundField DataField="RendimientoPromedio" HeaderText="Rendimiento (km/gal)" DataFormatString="{0:N2}" />
                                            </Columns>
                                            <EmptyDataTemplate>
                                                <div class="alert alert-info">No hay registros de combustible para esta orden de viaje.</div>
                                            </EmptyDataTemplate>
                                        </asp:GridView>
                                    </div>
                                </div>
                                <!-- Pestaña de Productos -->
                                <div class="tab-pane fade" id="productos" role="tabpanel" aria-labelledby="productos-tab">
                                    <div class="table-responsive mt-3">
                                        <asp:GridView ID="gvProductos" runat="server" CssClass="table table-striped table-sm" AutoGenerateColumns="false">
                                            <Columns>
                                                <asp:BoundField DataField="GuiaTransportista" HeaderText="Guía Transportista" />
                                                <asp:BoundField DataField="GuiaCliente" HeaderText="Guía Cliente" />
                                                <asp:BoundField DataField="Producto" HeaderText="Producto" />
                                                <asp:BoundField DataField="CantidadBolsas" HeaderText="Cantidad Bolsas" />
                                                <asp:BoundField DataField="Peso" HeaderText="Peso (Kg)" DataFormatString="{0:N2}" />
                                            </Columns>
                                            <EmptyDataTemplate>
                                                <div class="alert alert-info">No hay registros de productos para esta orden de viaje.</div>
                                            </EmptyDataTemplate>
                                        </asp:GridView>
                                    </div>
                                </div>
                            </div>
                        </ContentTemplate>
                    </asp:UpdatePanel>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cerrar</button>
                    <asp:Button ID="btnExportarDetalleExcel" runat="server" CssClass="btn btn-success" Text="Exportar Detalle" OnClick="btnExportarDetalleExcel_Click" />
                </div>
            </div>
        </div>
    </div>


    <style>
        /* Estilos específicos para modal de resultados */
        #modalResultados {
            z-index: 1050 !important;
        }

            #modalResultados .modal-content,
            #modalResultados .modal-header,
            #modalResultados .modal-body,
            #modalResultados .modal-footer {
                opacity: 1 !important;
            }

            #modalResultados .table,
            #modalResultados .table thead th,
            #modalResultados .table tbody tr,
            #modalResultados .table tbody td {
                opacity: 1 !important;
            }

            #modalResultados .card,
            #modalResultados .card-body,
            #modalResultados .card-title {
                opacity: 1 !important;
            }

        .modal-backdrop {
            z-index: 1040 !important;
            opacity: 0.5 !important;
        }
    </style>
    <!-- Referencias a librerías JavaScript -->
    <script type="text/javascript">
        $(document).ready(function () {
            console.log("Inicializando manejador específico para modal de resultados");

            // Eliminar cualquier backdrop residual
            $('.modal-backdrop').remove();

            // Obtener el PageRequestManager una sola vez
            var prm = Sys.WebForms.PageRequestManager.getInstance();

            // Desregistrar cualquier handler existente (para evitar duplicados)
            // Esta es una técnica avanzada para limpiar handlers previos
            if (prm._events && prm._events.endRequest) {
                var handlers = prm._events.endRequest.slice();
                for (var i = 0; i < handlers.length; i++) {
                    prm.remove_endRequest(handlers[i]);
                }
            }

            // Registrar un nuevo handler limpio
            prm.add_endRequest(function (sender, args) {
                console.log("Solicitud AJAX completada");

                // Verificar si el panel de resultados está visible
                if ($("#<%= pnlResultados.ClientID %>").is(":visible")) {
                    console.log("Panel de resultados visible, mostrando modal");

                    // 1. Eliminar backdrop anterior
                    $('.modal-backdrop').remove();

                    // 2. Restablecer estilos de la modal
                    $('#modalResultados').css({
                        'display': 'block',
                        'opacity': '1',
                        'z-index': '1050'
                    });

                    // 3. Asegurar que el contenido sea visible
                    $('#modalResultados .modal-content').css('opacity', '1');
                    $('#modalResultados .modal-header').css('opacity', '1');
                    $('#modalResultados .modal-body').css('opacity', '1');
                    $('#modalResultados .modal-footer').css('opacity', '1');

                    // 4. Mostrar la modal con un pequeño delay para asegurar que todo esté listo
                    setTimeout(function () {
                        $('#modalResultados').modal({
                            backdrop: 'static',
                            keyboard: true,
                            show: true
                        });

                        // 5. Configurar z-index correcto
                        $('.modal-backdrop').css('z-index', '1040');
                        $('#modalResultados').css('z-index', '1050');

                        // 6. Forzar opacidad en elementos clave dentro de la modal
                        $('.table thead th').css({
                            'background-color': '#0275d8',
                            'color': 'white',
                            'opacity': '1'
                        });

                        $('.card.shadow-sm, .card-body').css('opacity', '1');

                        console.log("Modal mostrada correctamente");
                    }, 100);
                }
            });

            // Manejador para botón de impresión
            $("#btnImprimirResultados").off('click').on('click', function () {
                var printWindow = window.open('', '_blank', 'height=600,width=800');

                printWindow.document.write('<html><head>');
                printWindow.document.write('<title>Reporte SGV</title>');
                printWindow.document.write('<link href="https://cdn.jsdelivr.net/npm/bootstrap@4.6.2/dist/css/bootstrap.min.css" rel="stylesheet" />');
                printWindow.document.write('<style>');
                printWindow.document.write('body { padding: 20px; font-family: Arial, sans-serif; }');
                printWindow.document.write('.table { width: 100%; border-collapse: collapse; margin-bottom: 1rem; }');
                printWindow.document.write('.table th { background-color: #0275d8; color: white; font-weight: bold; padding: 8px; text-align: center; }');
                printWindow.document.write('.table td { padding: 8px; border: 1px solid #dee2e6; }');
                printWindow.document.write('.card { margin-bottom: 1rem; border: 1px solid #dee2e6; border-radius: 0.25rem; }');
                printWindow.document.write('.card-body { padding: 1rem; }');
                printWindow.document.write('.bg-gradient-primary { background-color: #007bff; color: white; }');
                printWindow.document.write('.bg-gradient-danger { background-color: #dc3545; color: white; }');
                printWindow.document.write('.bg-gradient-success { background-color: #28a745; color: white; }');
                printWindow.document.write('.bg-gradient-info { background-color: #17a2b8; color: white; }');
                printWindow.document.write('</style>');
                printWindow.document.write('</head><body>');
                printWindow.document.write('<h1>Reporte - Sistema SGV</h1>');
                printWindow.document.write($("#<%= pnlResultados.ClientID %>").html());
            printWindow.document.write('</body></html>');

            printWindow.document.close();
            setTimeout(function () { printWindow.print(); }, 500);

            return false;
        });
        });

        // Código JavaScript para implementar la dependencia entre dropdowns
        $(document).ready(function () {
            // Referencia a los dropdowns
            var ddlTipoTransaccion = $("#<%= ddlTipoTransaccion.ClientID %>");
            var ddlTipoReporteDetalle = $("#<%= ddlTipoReporteDetalle.ClientID %>");

            // Cuando cambie el tipo de transacción
            ddlTipoTransaccion.change(function () {
                var selectedValue = $(this).val();

                // Guardar la selección actual si es posible
                var currentReportType = ddlTipoReporteDetalle.val();

                // Limpiar opciones actuales
                ddlTipoReporteDetalle.empty();

                // Añadir opciones básicas que siempre están disponibles
                ddlTipoReporteDetalle.append('<option value="balance_general">Balance general</option>');
                ddlTipoReporteDetalle.append('<option value="analisis_comparativo">Análisis comparativo</option>');

                // Añadir opciones basadas en la selección de transacción
                if (selectedValue === "" || selectedValue === "ingresos") {
                    ddlTipoReporteDetalle.append('<option value="ingresos_detallados">Ingresos detallados</option>');
                }

                if (selectedValue === "" || selectedValue === "egresos") {
                    ddlTipoReporteDetalle.append('<option value="egresos_detallados">Egresos detallados</option>');
                }

                // Intentar restaurar la selección anterior si sigue siendo válida
                if (ddlTipoReporteDetalle.find('option[value="' + currentReportType + '"]').length) {
                    ddlTipoReporteDetalle.val(currentReportType);
                } else {
                    ddlTipoReporteDetalle.val("balance_general"); // Valor por defecto
                }
            });

            // Ejecutar al inicio para establecer el estado correcto
            ddlTipoTransaccion.trigger('change');
        });


    </script>
</asp:Content>

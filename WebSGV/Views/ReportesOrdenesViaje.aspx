<%@ Page Title="Reporte de liquidaciones y viajes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReportesOrdenesViaje.aspx.cs" Inherits="WebSGV.Views.ReportesOrdenesViaje" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:Panel ID="pnlMensajes" runat="server" Visible="false" EnableViewState="false" CssClass="mb-4">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="sgv-encabezado">
        <div class="sgv-encabezado-titulo">
            <h1 class="h2">Reporte de liquidaciones y viajes</h1>
            <p>Consulte liquidaciones y viajes activos del sistema</p>
        </div>
        <div class="sgv-encabezado-acciones">
            <a class="sgv-ayuda" href="<%: ResolveUrl("~/Views/Ayuda.aspx?tema=reportes-ordenes-viaje") %>" target="_blank" rel="noopener"
                data-ayuda="reportes-ordenes-viaje" aria-label="Ayuda de esta p&#xE1;gina">?</a>
        </div>
    </div>

    <!-- Pestañas. hdnPestana: la pestaña abierta, para volver a ella tras un Buscar (M-077). -->
    <asp:HiddenField ID="hdnPestana" runat="server" Value="liquidaciones" />
    <div class="sgv-tabs" role="tablist" aria-label="Secciones del reporte">
        <button type="button" class="sgv-tab activo" id="tabLiquidaciones" role="tab" aria-selected="true" data-accion="cambiarTab" data-tab="liquidaciones">
            Liquidaciones
            <asp:Label ID="lblTotalRegistros" runat="server" CssClass="sgv-tab-contador" Text="0" EnableViewState="false"></asp:Label>
        </button>
        <button type="button" class="sgv-tab" id="tabViajesActivos" role="tab" aria-selected="false" data-accion="cambiarTab" data-tab="viajesActivos">
            Viajes activos sin liquidar
            <asp:Label ID="lblCountViajesActivos" runat="server" CssClass="sgv-tab-contador" Text="0" EnableViewState="false"></asp:Label>
        </button>
        <button type="button" class="sgv-tab" id="tabPersonalizado" role="tab" aria-selected="false" data-accion="cambiarTab" data-tab="personalizado">
            Reporte personalizado
        </button>
    </div>

    <!-- ==================== SECCIÓN LIQUIDACIONES ==================== -->
    <div id="seccionLiquidaciones">

        <details class="sgv-filtros" open data-plegar-movil>
            <summary>Filtros <span class="sgv-filtros-cuenta"></span></summary>
            <div class="sgv-filtros-cuerpo" data-enter="<%= btnBuscarLiquidaciones.ClientID %>">
                <div class="form-row align-items-end">
                    <div class="col-6 col-md-3 form-group">
                        <label for="<%= txtFechaDesde.ClientID %>" class="sgv-etiqueta">Fecha desde</label>
                        <asp:TextBox ID="txtFechaDesde" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                        <small id="errFechaDesde" class="text-danger d-none"></small>
                    </div>
                    <div class="col-6 col-md-3 form-group">
                        <label for="<%= txtFechaHasta.ClientID %>" class="sgv-etiqueta">Fecha hasta</label>
                        <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                        <small id="errFechaHasta" class="text-danger d-none"></small>
                    </div>
                    <div class="col-12 col-md-3 form-group">
                        <label for="<%= txtFactorConversion.ClientID %>" class="sgv-etiqueta">Factor de conversión ($ a S/)</label>
                        <div class="input-group">
                            <div class="input-group-prepend">
                                <span class="input-group-text">S/</span>
                            </div>
                            <asp:TextBox ID="txtFactorConversion" runat="server" CssClass="form-control" Text="3.75"></asp:TextBox>
                        </div>
                        <small id="errFactorConversion" class="text-danger d-none"></small>
                    </div>
                    <div class="col-12 col-md-3 form-group">
                        <asp:Button ID="btnBuscarLiquidaciones" runat="server" Text="Buscar"
                            CssClass="btn btn-primary btn-block" OnClick="btnBuscarLiquidaciones_Click" />
                    </div>
                </div>
            </div>
        </details>

        <section class="sgv-panel mb-4">
            <div class="sgv-panel-cab">
                <div>
                    <h2 class="sgv-panel-titulo">Liquidaciones registradas</h2>
                    <p class="sgv-panel-nota mb-0"><asp:Label ID="lblTotalRegistrosTabla" runat="server" Text="0 registros" EnableViewState="false"></asp:Label></p>
                </div>
                <div class="rpt-acciones">
                    <button type="button" class="btn btn-success btn-sm" data-accion="exportarLiquidaciones">Exportar Excel</button>
                    <button type="button" class="btn btn-outline-danger btn-sm" data-accion="generarPDFLiquidaciones">Exportar PDF</button>
                </div>
            </div>
            <div class="sgv-panel-cuerpo pb-2">
                <%-- Totales en una línea (antes, cuatro tarjetas): TextosTotales en el code-behind. --%>
                <p class="sgv-resumen mb-1">
                    <span>En soles <strong><asp:Label ID="lblResumenTotalSoles" runat="server" Text="S/ 0.00" EnableViewState="false"></asp:Label></strong></span>
                    <span>En dólares <strong><asp:Label ID="lblResumenTotalDolares" runat="server" Text="$ 0.00" EnableViewState="false"></asp:Label></strong></span>
                    <span>Conversión<asp:Label ID="lblResumenFactor" runat="server" EnableViewState="false"></asp:Label> <strong><asp:Label ID="lblResumenConversion" runat="server" Text="S/ 0.00" EnableViewState="false"></asp:Label></strong></span>
                    <span>Total general <strong><asp:Label ID="lblResumenTotal" runat="server" Text="S/ 0.00" EnableViewState="false"></asp:Label></strong></span>
                </p>
                <p class="sgv-panel-nota mb-0">
                    Montos: <span class="monto-descuento">en rojo, descuento neto</span>; <span class="monto-reintegro">en azul, reintegro neto</span>; en negro, cero.
                </p>
            </div>
            <div class="table-responsive">
                <asp:GridView ID="gvLiquidaciones" runat="server" EnableViewState="false"
                    CssClass="table sgv-tabla mb-0"
                    AutoGenerateColumns="false"
                    GridLines="None" CellSpacing="-1"
                    EmptyDataText="No hay liquidaciones para mostrar"
                    OnRowDataBound="gvLiquidaciones_RowDataBound"
                    ShowFooter="true">
                    <EmptyDataRowStyle CssClass="sgv-vacio" />
                    <Columns>
                        <asp:BoundField DataField="DNI" HeaderText="DNI" />
                        <asp:BoundField DataField="Conductor" HeaderText="Conductor" />
                        <asp:BoundField DataField="FechaSalida" HeaderText="Fecha" DataFormatString="{0:dd/MM/yyyy}" />
                        <asp:BoundField DataField="NumeroLiquidacion" HeaderText="N° de liq." />
                        <asp:TemplateField HeaderText="Monto S/ (reintegro − descuento)">
                            <ItemTemplate>
                                <span class='<%#: ObtenerClaseMontoSoles(Eval("MontoSoles")) %>'>
                                    <%#: FormatearMontoSoles(Eval("MontoSoles")) %>
                                </span>
                            </ItemTemplate>
                            <FooterTemplate>
                                <strong>Total: <asp:Label ID="lblTotalSoles" runat="server"></asp:Label></strong>
                            </FooterTemplate>
                            <HeaderStyle CssClass="text-right" />
                            <ItemStyle CssClass="text-right" />
                            <FooterStyle CssClass="text-right footer-total" />
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Monto $ (reintegro − descuento)">
                            <ItemTemplate>
                                <span class='<%#: ObtenerClaseMontoDolares(Eval("MontoDolares")) %>'>
                                    <%#: FormatearMontoDolares(Eval("MontoDolares")) %>
                                </span>
                            </ItemTemplate>
                            <FooterTemplate>
                                <strong>Total: <asp:Label ID="lblTotalDolares" runat="server"></asp:Label></strong>
                            </FooterTemplate>
                            <HeaderStyle CssClass="text-right" />
                            <ItemStyle CssClass="text-right" />
                            <FooterStyle CssClass="text-right footer-total" />
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Detalle">
                            <ItemTemplate>
                                <button type="button" class="sgv-btn-icono info" data-accion="verDetalleOrden" data-id='<%#: Eval("IdOrdenViaje") %>' title="Ver detalle" aria-label='<%#: "Ver detalle de la liquidación " + Eval("NumeroLiquidacion") %>'>
                                    <i class="fas fa-eye" aria-hidden="true"></i>
                                </button>
                            </ItemTemplate>
                            <HeaderStyle CssClass="sgv-col-acciones text-center" />
                            <ItemStyle CssClass="sgv-col-acciones text-center" />
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </section>

    </div>

    <!-- ==================== SECCIÓN VIAJES ACTIVOS ==================== -->
    <div id="seccionViajesActivos" class="d-none">

        <details class="sgv-filtros" open data-plegar-movil>
            <summary>Filtros <span class="sgv-filtros-cuenta"></span></summary>
            <div class="sgv-filtros-cuerpo" data-enter="<%= btnBuscarViajesActivos.ClientID %>">
                <div class="form-row align-items-end">
                    <div class="col-12 col-md-5 form-group">
                        <label for="<%= txtBuscarConductor.ClientID %>" class="sgv-etiqueta">Buscar conductor</label>
                        <asp:TextBox ID="txtBuscarConductor" runat="server" CssClass="form-control"
                            placeholder="Nombre, apellido o DNI" autocomplete="off" AutoCompleteType="Disabled"
                            spellcheck="false" autocapitalize="off" autocorrect="off"></asp:TextBox>
                        <small id="errBuscarConductor" class="text-danger d-none"></small>
                    </div>
                    <div class="col-12 col-md-4 form-group">
                        <%-- Texto fijo: la consulta solo lista viajes ABIERTO (M-077); el servidor usa EstadoViajeAbierto. --%>
                        <span class="sgv-etiqueta d-block">Estado del viaje</span>
                        <p class="form-control-plaintext">Abiertos (sin liquidar)</p>
                    </div>
                    <div class="col-12 col-md-3 form-group">
                        <asp:Button ID="btnBuscarViajesActivos" runat="server" Text="Buscar"
                            CssClass="btn btn-primary btn-block" OnClick="btnBuscarViajesActivos_Click" />
                    </div>
                </div>
            </div>
        </details>

        <section class="sgv-panel mb-4">
            <div class="sgv-panel-cab">
                <div>
                    <h2 class="sgv-panel-titulo">Conductores con viajes sin liquidación</h2>
                    <p class="sgv-panel-nota mb-0">
                        <asp:Label ID="lblCountViajesActivosTabla" runat="server" Text="0 viajes" EnableViewState="false"></asp:Label>
                        de <asp:Label ID="lblTotalViajesActivos" runat="server" Text="0 conductores" EnableViewState="false"></asp:Label>,
                        pendientes de liquidación
                    </p>
                </div>
                <div class="rpt-acciones">
                    <button type="button" class="btn btn-success btn-sm" data-accion="exportarViajesActivos" data-formato="excel">Exportar Excel</button>
                    <button type="button" class="btn btn-outline-danger btn-sm" data-accion="exportarViajesActivos" data-formato="pdf">Exportar PDF</button>
                </div>
            </div>
            <div class="table-responsive">
                <asp:GridView ID="gvViajesActivos" runat="server" EnableViewState="false"
                    CssClass="table sgv-tabla mb-0"
                    AutoGenerateColumns="false"
                    GridLines="None" CellSpacing="-1"
                    EmptyDataText="No hay viajes activos sin liquidación"
                    OnRowDataBound="gvViajesActivos_RowDataBound">
                    <EmptyDataRowStyle CssClass="sgv-vacio" />
                    <Columns>
                        <asp:BoundField DataField="DNI" HeaderText="DNI" />
                        <asp:BoundField DataField="Conductor" HeaderText="Conductor" />
                        <asp:BoundField DataField="PlacaTracto" HeaderText="Tracto" />
                        <asp:BoundField DataField="PlacaCarreta" HeaderText="Carreta" />
                        <asp:BoundField DataField="Cliente" HeaderText="Cliente" />
                        <asp:BoundField DataField="Destino" HeaderText="Destino" />
                        <asp:BoundField DataField="CantidadDespachos" HeaderText="N° desp.">
                            <HeaderStyle CssClass="text-center" />
                            <ItemStyle CssClass="text-center" />
                        </asp:BoundField>
                        <asp:TemplateField HeaderText="Fecha de programación">
                            <ItemTemplate>
                                <%#: Eval("FechaProgramacion") != DBNull.Value ? Convert.ToDateTime(Eval("FechaProgramacion")).ToString("dd/MM/yyyy") : "N/A" %>
                            </ItemTemplate>
                            <HeaderStyle CssClass="text-center" />
                            <ItemStyle CssClass="text-center" />
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Días en viaje">
                            <ItemTemplate>
                                <span class='<%#: Convert.ToInt32(Eval("DiasEnViaje")) > 7 ? "badge-dias-alerta" : "badge-dias-normal" %>'>
                                    <%#: Eval("DiasEnViaje") %> días
                                </span>
                            </ItemTemplate>
                            <HeaderStyle CssClass="text-center" />
                            <ItemStyle CssClass="text-center" />
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Estado">
                            <ItemTemplate>
                                <span class='<%#: "badge-estado badge-estado-" + Eval("Estado").ToString().ToLower() %>'>
                                    <%#: Eval("Estado") %>
                                </span>
                            </ItemTemplate>
                            <HeaderStyle CssClass="text-center" />
                            <ItemStyle CssClass="text-center" />
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </section>

    </div>

    <!-- ==================== SECCIÓN REPORTE PERSONALIZADO ==================== -->
    <div id="seccionPersonalizado" class="d-none">

        <section class="sgv-panel mb-4">
            <div class="sgv-panel-cab">
                <h2 class="sgv-panel-titulo">Configuración del reporte personalizado</h2>
            </div>
            <div class="sgv-panel-cuerpo">
                <div class="form-row">
                    <div class="col-6 col-md-3 form-group">
                        <label for="<%= txtPersFechaDesde.ClientID %>" class="sgv-etiqueta">Fecha desde</label>
                        <asp:TextBox ID="txtPersFechaDesde" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                        <small id="errPersFechaDesde" class="text-danger d-none"></small>
                    </div>
                    <div class="col-6 col-md-3 form-group">
                        <label for="<%= txtPersFechaHasta.ClientID %>" class="sgv-etiqueta">Fecha hasta</label>
                        <asp:TextBox ID="txtPersFechaHasta" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                        <small id="errPersFechaHasta" class="text-danger d-none"></small>
                    </div>
                    <div class="col-12 col-md-3 form-group">
                        <label for="<%= ddlPersEstado.ClientID %>" class="sgv-etiqueta">Estado de la liquidación</label>
                        <%-- Opciones: FiltroEstadoLiquidacion.Opciones (CargarEstadosPersonalizado). Las listas Pers* no guardan
                             ViewState (M-136): se llenan en OnInit en cada pedido y solo las lee ReportesOrdenesViaje.js. --%>
                        <asp:DropDownList ID="ddlPersEstado" runat="server" CssClass="form-control" EnableViewState="false">
                        </asp:DropDownList>
                        <small id="errPersEstado" class="text-danger d-none"></small>
                    </div>
                    <div class="col-12 col-md-3 form-group">
                        <label for="<%= ddlPersConductor.ClientID %>" class="sgv-etiqueta">Conductor</label>
                        <asp:DropDownList ID="ddlPersConductor" runat="server" CssClass="form-control" EnableViewState="false">
                            <asp:ListItem Value="0" Text="Todos los conductores"></asp:ListItem>
                        </asp:DropDownList>
                        <small id="errPersConductor" class="text-danger d-none"></small>
                    </div>
                </div>
                <div class="form-row">
                    <div class="col-12 col-md-3 form-group">
                        <label for="<%= ddlPersCliente.ClientID %>" class="sgv-etiqueta">Cliente</label>
                        <asp:DropDownList ID="ddlPersCliente" runat="server" CssClass="form-control" EnableViewState="false">
                            <asp:ListItem Value="0" Text="Todos los clientes"></asp:ListItem>
                        </asp:DropDownList>
                        <small id="errPersCliente" class="text-danger d-none"></small>
                    </div>
                    <div class="col-6 col-md-3 form-group">
                        <label for="<%= txtPersPlacaTracto.ClientID %>" class="sgv-etiqueta">Placa tracto</label>
                        <asp:TextBox ID="txtPersPlacaTracto" runat="server" CssClass="form-control" placeholder="Ej. ABC-123"></asp:TextBox>
                        <small id="errPersPlaca" class="text-danger d-none"></small>
                    </div>
                    <div class="col-6 col-md-3 form-group">
                        <label for="<%= txtPersCategoria.ClientID %>" class="sgv-etiqueta"
                            title="Solo incluye viajes con la categoría indicada (ej: propina, cochera, lavado)">
                            Categoría de gasto adicional
                        </label>
                        <asp:TextBox ID="txtPersCategoria" runat="server" CssClass="form-control" placeholder="Ej. propina, cochera..."></asp:TextBox>
                        <small id="errPersCategoria" class="text-danger d-none"></small>
                    </div>
                    <div class="col-12 col-md-3 form-group">
                        <label for="<%= ddlPersOrden.ClientID %>" class="sgv-etiqueta">Ordenar por</label>
                        <asp:DropDownList ID="ddlPersOrden" runat="server" CssClass="form-control" EnableViewState="false">
                            <asp:ListItem Value="fecha_desc" Text="Fecha (más reciente primero)" Selected="True"></asp:ListItem>
                            <asp:ListItem Value="fecha_asc" Text="Fecha (más antigua primero)"></asp:ListItem>
                            <asp:ListItem Value="conductor" Text="Conductor (A-Z)"></asp:ListItem>
                            <asp:ListItem Value="cliente" Text="Cliente (A-Z)"></asp:ListItem>
                        </asp:DropDownList>
                        <small id="errPersOrden" class="text-danger d-none"></small>
                    </div>
                    <div class="col-12 col-md-3 form-group">
                        <label for="<%= txtPersFactor.ClientID %>" class="sgv-etiqueta">Factor de conversión ($ a S/)</label>
                        <div class="input-group">
                            <div class="input-group-prepend">
                                <span class="input-group-text">S/</span>
                            </div>
                            <asp:TextBox ID="txtPersFactor" runat="server" CssClass="form-control" Text="3.75"></asp:TextBox>
                        </div>
                        <small id="errPersFactor" class="text-danger d-none"></small>
                    </div>
                </div>
                <div class="form-group mb-0">
                    <label for="<%= txtPersTitulo.ClientID %>" class="sgv-etiqueta">Título del reporte</label>
                    <asp:TextBox ID="txtPersTitulo" runat="server" CssClass="form-control"
                        Text="Reporte personalizado de órdenes de viaje"></asp:TextBox>
                    <small id="errPersTitulo" class="text-danger d-none"></small>
                </div>
            </div>
        </section>

        <section class="sgv-panel mb-4">
            <div class="sgv-panel-cab">
                <h2 class="sgv-panel-titulo">Columnas del reporte</h2>
                <div class="rpt-acciones">
                    <button type="button" class="btn btn-sm btn-outline-secondary" data-accion="marcarTodasColumnas" data-marcar="true">Marcar todas</button>
                    <button type="button" class="btn btn-sm btn-outline-secondary" data-accion="marcarTodasColumnas" data-marcar="false">Desmarcar todas</button>
                </div>
            </div>
            <div class="sgv-panel-cuerpo">
                <div class="row">
                    <div class="col-md-4">
                        <h6 class="columnas-grupo">Datos del conductor</h6>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="dni" checked /> DNI</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="conductor" checked /> Conductor</label>
                    </div>
                    <div class="col-md-4">
                        <h6 class="columnas-grupo">Fechas y horas</h6>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="fechaSalida" checked /> Fecha Salida</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="fechaLlegada" checked /> Fecha Llegada</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="horaSalida" /> Hora Salida</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="horaLlegada" /> Hora Llegada</label>
                    </div>
                    <div class="col-md-4">
                        <h6 class="columnas-grupo">Vehículo y viaje</h6>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="tracto" checked /> Placa Tracto</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="carreta" /> Placa Carreta</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="cliente" checked /> Cliente</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="destino" checked /> Destino</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="cantDespachos" /> N° Despachos</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="numero" checked /> N° Liquidación</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="estado" checked /> Estado de la liquidación</label>
                    </div>
                </div>
                <hr />
                <div class="row">
                    <div class="col-md-4">
                        <h6 class="columnas-grupo">Ingresos</h6>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="ingresosSoles" checked /> Total Ingresos S/</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="ingresosDolares" /> Total Ingresos $</label>
                    </div>
                    <div class="col-md-4">
                        <h6 class="columnas-grupo">Gastos</h6>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="gastosSoles" checked /> Total Gastos S/</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="gastosDolares" /> Total Gastos $</label>
                    </div>
                    <div class="col-md-4">
                        <h6 class="columnas-grupo">Descuentos, Reintegros y Balance</h6>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="descuentoSoles" checked /> Descuento S/</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="descuentoDolares" /> Descuento $</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="reintegroSoles" checked /> Reintegro S/</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="reintegroDolares" /> Reintegro $</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="balanceSoles" checked /> Balance Neto S/</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="balanceDolares" /> Balance Neto $</label>
                    </div>
                </div>
                <hr />
                <div class="row">
                    <div class="col-md-6">
                        <h6 class="columnas-grupo">Detalle de ingresos por categoría</h6>
                        <div class="row">
                            <div class="col-sm-6">
                                <label class="col-check"><input type="checkbox" class="col-pers" value="iDespachoS" /> Despacho S/</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="iDespachoD" /> Despacho $</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="iPrestamoS" /> Préstamo S/</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="iPrestamoD" /> Préstamo $</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="iMensualidadS" /> Mensualidad S/</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="iMensualidadD" /> Mensualidad $</label>
                            </div>
                            <div class="col-sm-6">
                                <label class="col-check"><input type="checkbox" class="col-pers" value="iOtrosS" /> Otros Autorizados S/</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="iOtrosD" /> Otros Autorizados $</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="iAdicionalesS" /> Ing. Adicionales S/</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="iAdicionalesD" /> Ing. Adicionales $</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="iAdicionalesDet" /> Detalle Ing. Adicionales</label>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-6">
                        <h6 class="columnas-grupo">Detalle de gastos por categoría</h6>
                        <div class="row">
                            <div class="col-sm-6">
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gPeajesS" /> Peajes S/</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gPeajesD" /> Peajes $</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gAlimentacionS" /> Alimentación S/</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gAlimentacionD" /> Alimentación $</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gCombustibleS" /> Combustible S/</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gCombustibleD" /> Combustible $</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gHospedajeS" /> Hospedaje S/</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gHospedajeD" /> Hospedaje $</label>
                            </div>
                            <div class="col-sm-6">
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gMovilidadS" /> Movilidad S/</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gMovilidadD" /> Movilidad $</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gApoyoSeguridadS" /> Apoyo Seguridad S/</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gApoyoSeguridadD" /> Apoyo Seguridad $</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gReparacionesS" /> Reparaciones S/</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gReparacionesD" /> Reparaciones $</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gEncarpadaS" /> Encarpada/Desenc. S/</label>
                                <label class="col-check"><input type="checkbox" class="col-pers" value="gEncarpadaD" /> Encarpada/Desenc. $</label>
                            </div>
                        </div>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="gAdicionalesS" /> Otros Gastos (Propina/Cochera/...) S/</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="gAdicionalesD" /> Otros Gastos $</label>
                        <label class="col-check"><input type="checkbox" class="col-pers" value="gAdicionalesDet" checked /> Detalle Otros Gastos (texto)</label>
                    </div>
                </div>
                <hr />
                <div class="row">
                    <div class="col-md-12">
                        <label class="col-check"><input type="checkbox" class="col-pers" value="observaciones" /> Incluir Observaciones</label>
                        <label class="col-check"><input type="checkbox" id="chkIncluirTotales" checked /> Incluir fila de totales</label>
                        <label class="col-check"><input type="checkbox" id="chkIncluirResumen" checked /> Incluir resumen ejecutivo</label>
                    </div>
                </div>
            </div>
        </section>

        <section class="sgv-panel">
            <div class="sgv-panel-cuerpo d-flex justify-content-between align-items-center flex-wrap rpt-acciones">
                <p class="sgv-panel-nota mb-0">
                    El reporte se genera con los filtros y columnas elegidos.
                    Incluye las liquidaciones en el estado elegido (con "Todos", en cualquier estado).
                </p>
                <div class="rpt-acciones">
                    <button type="button" class="btn btn-success" data-accion="generarPersonalizado" data-formato="excel">Generar Excel</button>
                    <button type="button" class="btn btn-outline-danger" data-accion="generarPersonalizado" data-formato="pdf">Generar PDF</button>
                </div>
            </div>
        </section>

    </div>

    <!-- Modal Detalle de Orden -->
    <div class="modal fade" id="modalDetalleOrden" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalDetalleOrdenTitulo">
        <div class="modal-dialog modal-xl" role="document">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title" id="modalDetalleOrdenTitulo">
                        Detalle de la orden de viaje
                    </h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <div id="detalleOrdenContent">
                        <div class="sgv-cargando" role="status">Cargando el detalle…</div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-outline-secondary" data-dismiss="modal">Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/ReportesOrdenesViaje.css") %>" rel="stylesheet" />

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            txtFechaDesde: '<%= txtFechaDesde.ClientID %>',
            txtFechaHasta: '<%= txtFechaHasta.ClientID %>',
            txtFactorConversion: '<%= txtFactorConversion.ClientID %>',
            txtBuscarConductor: '<%= txtBuscarConductor.ClientID %>',
            hdnPestana: '<%= hdnPestana.ClientID %>',
            txtPersFechaDesde: '<%= txtPersFechaDesde.ClientID %>',
            txtPersFechaHasta: '<%= txtPersFechaHasta.ClientID %>',
            ddlPersEstado: '<%= ddlPersEstado.ClientID %>',
            ddlPersConductor: '<%= ddlPersConductor.ClientID %>',
            ddlPersCliente: '<%= ddlPersCliente.ClientID %>',
            txtPersPlacaTracto: '<%= txtPersPlacaTracto.ClientID %>',
            txtPersCategoria: '<%= txtPersCategoria.ClientID %>',
            ddlPersOrden: '<%= ddlPersOrden.ClientID %>',
            txtPersFactor: '<%= txtPersFactor.ClientID %>',
            txtPersTitulo: '<%= txtPersTitulo.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/ReportesOrdenesViaje.js") %>"></script>

</asp:Content>

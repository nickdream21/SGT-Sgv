<%@ Page Title="Liquidaciones aprobadas" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="LiquidacionesAprobadasContabilidad.aspx.cs" Inherits="WebSGV.Views.LiquidacionesAprobadasContabilidad" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-3">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/LiquidacionesAprobadasContabilidad.css") %>" rel="stylesheet" />

    <div class="sgv-encabezado">
        <div class="sgv-encabezado-titulo">
            <h1 class="h2">Liquidaciones aprobadas</h1>
            <p>Consulta de liquidaciones aprobadas (solo lectura)</p>
        </div>
    </div>

    <details class="sgv-filtros" open data-plegar-movil>
        <summary>Filtros <span class="sgv-filtros-cuenta"></span></summary>
        <div class="sgv-filtros-cuerpo" data-enter="btnBuscarLiquidaciones">
            <div class="form-row">
                <div class="col-12 col-md-4 form-group">
                    <label for="txtConductor" class="sgv-etiqueta">Conductor</label>
                    <input type="text" id="txtConductor" class="form-control" placeholder="Buscar conductor..." autocomplete="off" />
                    <input type="hidden" id="hfConductorId" value="" />
                    <div id="conductorSugg" class="conductor-autocomplete-dropdown"></div>
                </div>
                <div class="col-12 col-md-4 form-group">
                    <label for="txtNumeroLiquidacion" class="sgv-etiqueta">N° de liquidación</label>
                    <input type="text" id="txtNumeroLiquidacion" class="form-control" placeholder="Ej. OV-000123" maxlength="30" />
                </div>
                <div class="col-6 col-md-2 form-group">
                    <label for="txtFechaDesde" class="sgv-etiqueta">Salida desde</label>
                    <input type="date" id="txtFechaDesde" class="form-control" min="2000-01-01" max="2100-12-31" />
                </div>
                <div class="col-6 col-md-2 form-group">
                    <label for="txtFechaHasta" class="sgv-etiqueta">Salida hasta</label>
                    <input type="date" id="txtFechaHasta" class="form-control" min="2000-01-01" max="2100-12-31" />
                </div>
            </div>
            <div class="d-flex flex-wrap align-items-center lac-acciones">
                <button type="button" id="btnBuscarLiquidaciones" class="btn btn-primary" data-accion="buscarLiquidaciones">Buscar</button>
                <button type="button" class="btn btn-outline-secondary" data-accion="limpiarFiltros">Limpiar</button>
                <button type="button" id="btnTodoHistorial" class="btn btn-outline-secondary" data-accion="verTodoHistorial">Todo el historial</button>
            </div>
        </div>
    </details>

    <section class="sgv-panel">
        <div class="sgv-panel-cab">
            <div>
                <h2 class="sgv-panel-titulo">Resultados</h2>
                <%-- Total y rango de la última búsqueda: los escribe LiquidacionesAprobadasContabilidad.js. --%>
                <p class="sgv-panel-nota mb-0" aria-live="polite">
                    Total: <strong id="lblTotalResultados">0</strong>. <span id="lblRangoFechas"></span>
                </p>
            </div>
        </div>
        <div class="table-responsive">
            <table class="table sgv-tabla mb-0">
                <thead>
                    <tr>
                        <th>N° de liquidación</th>
                        <th>Conductor</th>
                        <th>Periodo</th>
                        <th class="text-right">Balance S/</th>
                        <th class="text-right">Balance $</th>
                        <th class="text-center">Acciones</th>
                    </tr>
                </thead>
                <tbody id="tbodyLiquidaciones">
                    <tr><td colspan="6"><div class="sgv-cargando" role="status">Cargando liquidaciones…</div></td></tr>
                </tbody>
            </table>
        </div>
        <div class="lac-pie">
            <span class="sgv-panel-nota" id="lblMostrando" aria-live="polite"></span>
            <nav aria-label="Páginas de liquidaciones aprobadas">
                <div class="sgv-paginador" id="paginadorLiquidaciones"></div>
            </nav>
        </div>
    </section>

    <div class="modal fade" id="modalDetalleLiquidacion" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalDetalleLiquidacionTitulo" aria-hidden="true">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title" id="modalDetalleLiquidacionTitulo">Detalle de liquidación</h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <div class="row">
                        <div class="col-md-6"><strong>N° Liquidación:</strong> <span id="detalleNumero"></span></div>
                        <div class="col-md-6"><strong>Conductor:</strong> <span id="detalleConductor"></span></div>
                    </div>
                    <div class="row mt-2">
                        <div class="col-md-6"><strong>Tracto:</strong> <span id="detalleTracto"></span></div>
                        <div class="col-md-6"><strong>Carreta:</strong> <span id="detalleCarreta"></span></div>
                    </div>
                    <div class="row mt-2">
                        <div class="col-md-6"><strong>Fecha salida:</strong> <span id="detalleFechaSalida"></span></div>
                        <div class="col-md-6"><strong>Fecha llegada:</strong> <span id="detalleFechaLlegada"></span></div>
                    </div>
                    <div class="row mt-2">
                        <div class="col-md-6"><strong>Total ingresos S/:</strong> <span id="detalleIngresosSoles"></span></div>
                        <div class="col-md-6"><strong>Total Ingresos $:</strong> <span id="detalleIngresosDolares"></span></div>
                    </div>
                    <div class="row mt-2">
                        <div class="col-md-6"><strong>Total gastos S/:</strong> <span id="detalleGastosSoles"></span></div>
                        <div class="col-md-6"><strong>Total Gastos $:</strong> <span id="detalleGastosDolares"></span></div>
                    </div>

                    <hr class="my-3" />

                    <div class="mb-3">
                        <h6 class="mb-2">Ingresos por concepto</h6>
                        <div class="table-responsive">
                            <table class="table table-sm table-bordered mb-0">
                                <thead class="thead-light">
                                    <tr>
                                        <th>Concepto</th>
                                        <th class="text-right">S/</th>
                                        <th class="text-right">US$</th>
                                        <th>Detalle</th>
                                    </tr>
                                </thead>
                                <tbody id="tbodyDetalleIngresos"></tbody>
                            </table>
                        </div>
                    </div>

                    <div class="mb-3">
                        <h6 class="mb-2">Gastos por concepto</h6>
                        <div class="table-responsive">
                            <table class="table table-sm table-bordered mb-0">
                                <thead class="thead-light">
                                    <tr>
                                        <th>Concepto</th>
                                        <th class="text-right">S/</th>
                                        <th class="text-right">US$</th>
                                        <th>Detalle</th>
                                    </tr>
                                </thead>
                                <tbody id="tbodyDetalleGastos"></tbody>
                            </table>
                        </div>
                    </div>

                    <div class="mb-2">
                        <h6 class="mb-2">Detalle de peajes</h6>
                        <div class="table-responsive">
                            <table class="table table-sm table-striped mb-0">
                                <thead>
                                    <tr>
                                        <th>Estación</th>
                                        <th>Fecha</th>
                                        <th>Comprobante</th>
                                        <th class="text-right">S/</th>
                                        <th class="text-right">US$</th>
                                        <th>Observaciones</th>
                                    </tr>
                                </thead>
                                <tbody id="tbodyDetallePeajes"></tbody>
                            </table>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <div class="modal fade" id="modalPdfLiquidacion" tabindex="-1" role="dialog" aria-labelledby="modalPdfLiquidacionTitle" aria-hidden="true">
        <div class="modal-dialog modal-xl" role="document">
            <div class="modal-content shadow-sm">
                <div class="modal-header">
                    <h5 class="modal-title" id="modalPdfLiquidacionTitle">
                        PDF firmado de liquidación
                    </h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>
                <div class="modal-body p-0">
                    <div id="pdfContenedor" class="pdf-liquidacion-contenedor">
                        <div id="pdfCargando" class="pdf-liquidacion-loader">
                            <div class="sgv-cargando" role="status" aria-live="polite">Cargando el documento…</div>
                        </div>
                        <iframe id="iframePdfLiquidacion" class="pdf-liquidacion-iframe" title="Vista previa de PDF de liquidación"></iframe>
                    </div>
                </div>
                <div class="modal-footer d-flex justify-content-between">
                    <div class="text-muted small" id="lblEstadoPdfLiquidacion">Vista previa en línea del documento oficial.</div>
                    <div>
                        <button type="button" class="btn btn-outline-primary mr-2" id="btnAbrirPdfNuevaPestana">
                            Abrir en nueva pestaña
                        </button>
                        <button type="button" class="btn btn-primary mr-2" id="btnDescargarPdfLiquidacion">
                            Descargar PDF
                        </button>
                        <button type="button" class="btn btn-secondary" data-dismiss="modal">Cerrar</button>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/LiquidacionesAprobadasContabilidad.js") %>"></script>
</asp:Content>

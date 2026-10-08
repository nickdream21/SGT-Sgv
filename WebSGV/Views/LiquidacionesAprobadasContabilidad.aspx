<%@ Page Title="Liquidaciones Aprobadas" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="LiquidacionesAprobadasContabilidad.aspx.cs" Inherits="WebSGV.Views.LiquidacionesAprobadasContabilidad" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-3">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
</asp:Panel>

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/LiquidacionesAprobadasContabilidad.css") %>" rel="stylesheet" />

    <div class="container-fluid px-4">
        <div class="row mb-4">
            <div class="col-12">
                <h2 class="mb-1"><i class="fas fa-file-invoice-dollar mr-2"></i>Liquidaciones Aprobadas</h2>
                <p class="text-muted mb-0">Consulta de liquidaciones aprobadas (solo lectura)</p>
            </div>
        </div>

        <div class="card mb-4">
            <div class="card-header"><strong>Filtros de búsqueda</strong></div>
            <div class="card-body">
                <div class="row">
                    <div class="col-md-4">
                        <label>Conductor</label>
                        <input type="text" id="txtConductor" class="form-control" placeholder="Buscar conductor..." autocomplete="off" />
                        <input type="hidden" id="hfConductorId" value="" />
                        <div id="conductorSugg" class="conductor-autocomplete-dropdown"></div>
                    </div>
                    <div class="col-md-4">
                        <label>N° Liquidación</label>
                        <input type="text" id="txtNumeroLiquidacion" class="form-control" placeholder="Ej. OV-000123" maxlength="30" />
                    </div>
                    <div class="col-md-4 d-flex align-items-end">
                        <button type="button" class="btn btn-primary mr-2" onclick="buscarLiquidaciones(true)">Buscar</button>
                        <button type="button" class="btn btn-secondary" onclick="limpiarFiltros()">Limpiar</button>
                    </div>
                </div>
            </div>
        </div>

        <div class="card">
            <div class="card-header d-flex justify-content-between align-items-center">
                <strong>Resultados</strong>
                <span class="badge badge-success" id="lblTotalResultados">0</span>
            </div>
            <div class="card-body p-0">
                <div class="table-responsive">
                    <table class="table table-striped mb-0">
                        <thead>
                            <tr>
                                <th>N° Liquidación</th>
                                <th>Conductor</th>
                                <th>Periodo</th>
                                <th class="text-right">Balance S/</th>
                                <th class="text-right">Balance $</th>
                                <th class="text-center">Acciones</th>
                            </tr>
                        </thead>
                        <tbody id="tbodyLiquidaciones">
                            <tr><td colspan="6" class="text-center text-muted py-4">Use los filtros y pulse Buscar.</td></tr>
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    </div>

    <div class="modal fade" id="modalDetalleLiquidacion" tabindex="-1" role="dialog" aria-hidden="true">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title">Detalle de Liquidación</h5>
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
                        <div class="col-md-6"><strong>Fecha Salida:</strong> <span id="detalleFechaSalida"></span></div>
                        <div class="col-md-6"><strong>Fecha Llegada:</strong> <span id="detalleFechaLlegada"></span></div>
                    </div>
                    <div class="row mt-2">
                        <div class="col-md-6"><strong>Total Ingresos S/:</strong> <span id="detalleIngresosSoles"></span></div>
                        <div class="col-md-6"><strong>Total Ingresos $:</strong> <span id="detalleIngresosDolares"></span></div>
                    </div>
                    <div class="row mt-2">
                        <div class="col-md-6"><strong>Total Gastos S/:</strong> <span id="detalleGastosSoles"></span></div>
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
                        <i class="fas fa-file-pdf text-danger mr-2"></i>PDF firmado de liquidación
                    </h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>
                <div class="modal-body p-0">
                    <div id="pdfContenedor" class="pdf-liquidacion-contenedor">
                        <div id="pdfCargando" class="pdf-liquidacion-loader" role="status" aria-live="polite">
                            <div class="text-center">
                                <i class="fas fa-spinner fa-spin fa-2x mb-3"></i>
                                <div>Cargando documento...</div>
                            </div>
                        </div>
                        <iframe id="iframePdfLiquidacion" class="pdf-liquidacion-iframe" title="Vista previa de PDF de liquidación"></iframe>
                    </div>
                </div>
                <div class="modal-footer d-flex justify-content-between">
                    <div class="text-muted small" id="lblEstadoPdfLiquidacion">Vista previa en línea del documento oficial.</div>
                    <div>
                        <button type="button" class="btn btn-outline-primary mr-2" id="btnAbrirPdfNuevaPestana">
                            <i class="fas fa-external-link-alt mr-1"></i>Abrir en nueva pestaña
                        </button>
                        <button type="button" class="btn btn-primary mr-2" id="btnDescargarPdfLiquidacion">
                            <i class="fas fa-download mr-1"></i>Descargar PDF
                        </button>
                        <button type="button" class="btn btn-secondary" data-dismiss="modal">Cerrar</button>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/LiquidacionesAprobadasContabilidad.js") %>"></script>
</asp:Content>

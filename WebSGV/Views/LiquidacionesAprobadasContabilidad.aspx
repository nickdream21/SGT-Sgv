<%@ Page Title="Liquidaciones Aprobadas" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="LiquidacionesAprobadasContabilidad.aspx.cs" Inherits="WebSGV.Views.LiquidacionesAprobadasContabilidad" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="mb-3">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

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
                        <button type="button" class="btn btn-primary mr-2" onclick="buscarLiquidaciones()">Buscar</button>
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

    <script type="text/javascript">
        function htmlEncode(value) {
            return $('<div/>').text(value == null ? '' : value).html();
        }

        function valorTexto(value) {
            var v = (value || '').toString().trim();
            return v ? htmlEncode(v) : '—';
        }

        function money(value) {
            return Number(value || 0).toFixed(2);
        }

        function renderFilaConcepto(nombre, soles, dolares, detalle) {
            return '<tr>' +
                '<td>' + htmlEncode(nombre) + '</td>' +
                '<td class="text-right">' + money(soles) + '</td>' +
                '<td class="text-right">' + money(dolares) + '</td>' +
                '<td>' + valorTexto(detalle) + '</td>' +
                '</tr>';
        }

        function renderDetalleIngresos(d) {
            var html = '';
            html += renderFilaConcepto('Despacho', d.DespachoSoles, d.DespachoDolares, d.DescDespacho);
            html += renderFilaConcepto('Préstamo', d.PrestamoSoles, d.PrestamoDolares, d.DescPrestamo);
            html += renderFilaConcepto('Mensualidad', d.MensualidadSoles, d.MensualidadDolares, d.DescMensualidad);
            html += renderFilaConcepto('Otros autorizados', d.OtrosIngresosSoles, d.OtrosIngresosDolares, d.DescOtros);

            var adicionales = d.DetallesIngresosAdicionales || [];
            for (var i = 0; i < adicionales.length; i++) {
                var item = adicionales[i];
                html += renderFilaConcepto(item.Nombre || ('Ingreso adicional ' + (i + 1)), item.Soles, item.Dolares, item.Descripcion);
            }

            $('#tbodyDetalleIngresos').html(html || '<tr><td colspan="4" class="text-center text-muted">Sin ingresos registrados.</td></tr>');
        }

        function renderDetalleGastos(d) {
            var html = '';
            html += renderFilaConcepto('Peajes', d.GastosPeajesSoles, d.GastosPeajesDolares, d.DescPeajes);
            html += renderFilaConcepto('Alimentación', d.GastosAlimentacionSoles, d.GastosAlimentacionDolares, d.DescAlimentacion);
            html += renderFilaConcepto('Apoyo seguridad', d.GastosApoyoSeguridadSoles, d.GastosApoyoSeguridadDolares, d.DescApoyoSeguridad);
            html += renderFilaConcepto('Reparaciones/Varios', d.GastosReparacionesSoles, d.GastosReparacionesDolares, d.DescReparaciones);
            html += renderFilaConcepto('Movilidad', d.GastosMovilidadSoles, d.GastosMovilidadDolares, d.DescMovilidad);
            html += renderFilaConcepto('Encarpada/Desencarpada', d.GastosEncarpadaSoles, d.GastosEncarpadaDolares, d.DescEncarpada);
            html += renderFilaConcepto('Hospedaje', d.GastosHospedajeSoles, d.GastosHospedajeDolares, d.DescHospedaje);
            html += renderFilaConcepto('Combustible', d.GastosCombustibleSoles, d.GastosCombustibleDolares, d.DescCombustible);

            var adicionales = d.DetallesGastosAdicionales || [];
            for (var i = 0; i < adicionales.length; i++) {
                var item = adicionales[i];
                html += renderFilaConcepto(item.Nombre || ('Gasto adicional ' + (i + 1)), item.Soles, item.Dolares, item.Descripcion);
            }

            $('#tbodyDetalleGastos').html(html || '<tr><td colspan="4" class="text-center text-muted">Sin gastos registrados.</td></tr>');
        }

        function renderDetallePeajes(d) {
            var peajes = d.DetallesPeajes || [];
            if (!peajes.length) {
                $('#tbodyDetallePeajes').html('<tr><td colspan="6" class="text-center text-muted">Sin peajes detallados.</td></tr>');
                return;
            }

            var html = '';
            for (var i = 0; i < peajes.length; i++) {
                var p = peajes[i];
                html += '<tr>' +
                    '<td>' + valorTexto(p.Estacion) + '</td>' +
                    '<td>' + valorTexto(p.Fecha) + '</td>' +
                    '<td>' + valorTexto(p.Comprobante) + '</td>' +
                    '<td class="text-right">' + money(p.Soles) + '</td>' +
                    '<td class="text-right">' + money(p.Dolares) + '</td>' +
                    '<td>' + valorTexto(p.Observaciones) + '</td>' +
                    '</tr>';
            }
            $('#tbodyDetallePeajes').html(html);
        }

        function limpiarFiltros() {
            $('#txtConductor').val('');
            $('#hfConductorId').val('');
            $('#txtNumeroLiquidacion').val('');
            buscarLiquidaciones();
        }

        function buscarLiquidaciones() {
            var idConductor = parseInt($('#hfConductorId').val() || '0', 10);
            var numero = ($('#txtNumeroLiquidacion').val() || '').trim();

            $.ajax({
                type: 'POST',
                url: 'LiquidacionesPendientes.aspx/ObtenerLiquidacionesAprobadas',
                contentType: 'application/json; charset=utf-8',
                dataType: 'json',
                data: JSON.stringify({ idConductor: idConductor, fechaDesde: '', fechaHasta: '', numeroOrden: numero }),
                success: function (response) {
                    var data = response && response.d ? response.d : [];
                    $('#lblTotalResultados').text(data.length);

                    if (!data.length) {
                        $('#tbodyLiquidaciones').html('<tr><td colspan="6" class="text-center text-muted py-4">No se encontraron liquidaciones aprobadas.</td></tr>');
                        return;
                    }

                    var html = '';
                    for (var i = 0; i < data.length; i++) {
                        var item = data[i];
                        html += '<tr>' +
                            '<td><strong>' + htmlEncode(item.NumeroOrdenViaje) + '</strong></td>' +
                            '<td>' + htmlEncode(item.NombreConductor) + '</td>' +
                            '<td>' + htmlEncode(item.FechaSalida) + ' al ' + htmlEncode(item.FechaLlegada) + '</td>' +
                            '<td class="text-right">' + Number(item.BalanceSoles || 0).toFixed(2) + '</td>' +
                            '<td class="text-right">' + Number(item.BalanceDolares || 0).toFixed(2) + '</td>' +
                            '<td class="text-center">' +
                                '<button type="button" class="btn btn-info btn-sm mr-1" onclick="verPdfLiquidacion(' + item.IdOrdenViaje + ')"><i class="fas fa-eye"></i> Ver</button>' +
                                '<button type="button" class="btn btn-outline-secondary btn-sm" onclick="verDetalle(' + item.IdOrdenViaje + ')"><i class="fas fa-list"></i> Detalle</button>' +
                            '</td>' +
                            '</tr>';
                    }
                    $('#tbodyLiquidaciones').html(html);
                },
                error: function () {
                    $('#tbodyLiquidaciones').html('<tr><td colspan="6" class="text-center text-danger py-4">Error al obtener liquidaciones.</td></tr>');
                }
            });
        }

        function verDetalle(idOrdenViaje) {
            $.ajax({
                type: 'POST',
                url: 'LiquidacionesPendientes.aspx/ObtenerDetalleLiquidacion',
                contentType: 'application/json; charset=utf-8',
                dataType: 'json',
                data: JSON.stringify({ idOrdenViaje: idOrdenViaje }),
                success: function (response) {
                    var d = response && response.d ? response.d : null;
                    if (!d) return;

                    $('#detalleNumero').text(d.NumeroOrdenViaje || '');
                    $('#detalleConductor').text(d.NombreConductor || '');
                    $('#detalleTracto').text(d.PlacaTracto || '');
                    $('#detalleCarreta').text(d.PlacaCarreta || '');
                    $('#detalleFechaSalida').text(d.FechaSalida || '');
                    $('#detalleFechaLlegada').text(d.FechaLlegada || '');
                    $('#detalleIngresosSoles').text(money(d.TotalIngresosSoles));
                    $('#detalleIngresosDolares').text(money(d.TotalIngresosDolares));
                    $('#detalleGastosSoles').text(money(d.TotalGastosSoles));
                    $('#detalleGastosDolares').text(money(d.TotalGastosDolares));

                    renderDetalleIngresos(d);
                    renderDetalleGastos(d);
                    renderDetallePeajes(d);

                    $('#modalDetalleLiquidacion').modal('show');
                }
            });
        }

        function verPdfLiquidacion(idOrdenViaje) {
            if (!idOrdenViaje) {
                return;
            }

            $.ajax({
                type: 'POST',
                url: 'LiquidacionesPendientes.aspx/ObtenerUrlPdfOrdenViaje',
                contentType: 'application/json; charset=utf-8',
                dataType: 'json',
                data: JSON.stringify({ idOrdenViaje: idOrdenViaje }),
                success: function (response) {
                    var r = response && response.d ? response.d : null;
                    if (r && r.success && r.url) {
                        window.open(r.url, '_blank');
                        return;
                    }

                    alert(r && r.message
                        ? r.message
                        : 'No se encontró el PDF firmado para esta liquidación.');
                },
                error: function () {
                    alert('Error al abrir el PDF de la liquidación.');
                }
            });
        }

        var _timerConductor = null;
        $('#txtConductor').on('input', function () {
            clearTimeout(_timerConductor);
            var termino = ($(this).val() || '').trim();
            if (termino.length < 2) {
                $('#conductorSugg').hide().empty();
                $('#hfConductorId').val('');
                return;
            }

            _timerConductor = setTimeout(function () {
                $.ajax({
                    type: 'POST',
                    url: 'LiquidacionesPendientes.aspx/BuscarConductores',
                    contentType: 'application/json; charset=utf-8',
                    dataType: 'json',
                    data: JSON.stringify({ termino: termino }),
                    success: function (response) {
                        var data = response && response.d ? response.d : [];
                        if (!data.length) {
                            $('#conductorSugg').hide().empty();
                            return;
                        }

                        var html = '';
                        for (var i = 0; i < data.length; i++) {
                            var item = data[i];
                            html += '<div class="autocomplete-item" data-id="' + item.IdConductor + '" data-text="' + htmlEncode(item.NombreCompleto) + '">' + htmlEncode(item.NombreCompleto) + '</div>';
                        }
                        $('#conductorSugg').html(html).show();
                    }
                });
            }, 250);
        });

        $(document).on('click', '#conductorSugg .autocomplete-item', function () {
            $('#txtConductor').val($(this).attr('data-text'));
            $('#hfConductorId').val($(this).attr('data-id'));
            $('#conductorSugg').hide().empty();
        });

        $(document).ready(function () {
            buscarLiquidaciones();
        });
    </script>
</asp:Content>

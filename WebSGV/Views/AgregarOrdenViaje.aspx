    <%@ Page Title="Agregar Orden de Viaje" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AgregarOrdenViaje.aspx.cs" Inherits="WebSGV.Views.AgregarOrdenViaje" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="tabs-container">
        <!-- Pestañas -->
        <ul class="nav nav-tabs" id="ordenViajeTabs" role="tablist">
            <li class="nav-item">
                <a class="nav-link active" id="datos-tab" data-toggle="tab" href="#datos" role="tab" aria-controls="datos" aria-selected="true">Datos del Viaje</a>
            </li>
            <li class="nav-item">
                <a class="nav-link" id="segmentos-tab" data-toggle="tab" href="#segmentos" role="tab" aria-controls="segmentos" aria-selected="false">Trazabilidad</a>

            </li>
            <li class="nav-item">
                <a class="nav-link" id="liquidacion-tab" data-toggle="tab" href="#liquidacion" role="tab" aria-controls="liquidacion" aria-selected="false">Resumen Financiero</a>
            </li>
            <li class="nav-item">&nbsp;</li>
        </ul>

        <!-- Contenido de las pestañas -->
        <div class="tab-content" id="ordenViajeContent">

            <!-- Pestaña 1: Datos del Viaje - MANTENER IGUAL -->
            <div class="tab-pane fade show active" id="datos" role="tabpanel" aria-labelledby="datos-tab">
                <h3 class="tab-header text-center mb-4">Datos Generales del Viaje</h3>
                <div id="formDatosViaje">
                    <asp:HiddenField ID="hfValidationError" runat="server" />
                    <asp:HiddenField ID="hfNumeroOrdenViaje" runat="server" />

                    <!-- Mostrar número generado automáticamente -->
                    <div class="row mb-3">
                        <div class="col-md-6 form-group">
                            <label>N° Orden Viaje (Generado automáticamente):</label>
                            <asp:Label ID="lblNumeroOrdenViaje" runat="server" CssClass="form-control-plaintext font-weight-bold text-primary" Text="Se generará al guardar"></asp:Label>
                        </div>
                        <div class="col-md-6 form-group">
                            <label for="ddlConductor">Conductor:</label>
                            <asp:DropDownList ID="ddlConductor" runat="server" CssClass="form-control conductor-select" required>
                                <asp:ListItem Text="Seleccione un conductor" Value="" />
                            </asp:DropDownList>
                        </div>
                    </div>

                    <!-- Segunda fila: Fechas y horas -->
                    <div class="row mb-3">
                        <div class="col-md-3 form-group">
                            <label for="txtFechaSalida">Fecha de Salida:</label>
                            <input type="date" id="txtFechaSalida" runat="server" class="form-control" required />
                        </div>
                        <div class="col-md-3 form-group">
                            <label for="txtHoraSalida">Hora de Salida:</label>
                            <input type="time" id="txtHoraSalida" runat="server" class="form-control" required />
                        </div>
                        <div class="col-md-3 form-group">
                            <label for="txtFechaLlegada">Fecha de Llegada:</label>
                            <input type="date" id="txtFechaLlegada" runat="server" class="form-control" required />
                        </div>
                        <div class="col-md-3 form-group">
                            <label for="txtHoraLlegada">Hora de Llegada:</label>
                            <input type="time" id="txtHoraLlegada" runat="server" class="form-control" required />
                        </div>
                    </div>

                    <!-- Tercera fila: Vehículos -->
                    <div class="row mb-3">
                        <div class="col-md-6 form-group">
                            <label for="ddlPlacaTracto">Placa Tracto:</label>
                            <asp:DropDownList ID="ddlPlacaTracto" runat="server" CssClass="form-control tracto-select" required>
                                <asp:ListItem Text="Seleccione una placa" Value="" />
                            </asp:DropDownList>
                        </div>
                        <div class="col-md-6 form-group">
                            <label for="ddlPlacaCarreta">Placa Carreta:</label>
                            <asp:DropDownList ID="ddlPlacaCarreta" runat="server" CssClass="form-control carreta-select" required>
                                <asp:ListItem Text="Seleccione una placa" Value="" />
                            </asp:DropDownList>
                        </div>
                    </div>

                    <!-- Cuarta fila: Observaciones -->
                    <div class="row mb-4">
                        <div class="col-md-12 form-group">
                            <label for="txtObservaciones">Observaciones Generales:</label>
                            <textarea id="txtObservaciones" runat="server" class="form-control" rows="3" placeholder="Observaciones generales del viaje"></textarea>
                        </div>
                    </div>

                    <!-- Botón Siguiente -->
                    <div class="form-group text-end">
                        <button type="button" class="btn btn-primary px-4 py-2" onclick="showNextTab('segmentos')">Siguiente: Agregar Trazabilidad</button>

                    </div>

                    <!-- Etiqueta para mostrar errores -->
                    <div class="form-group">
                        <asp:Label ID="lblErrores" runat="server" CssClass="text-danger" EnableViewState="false"></asp:Label>
                    </div>
                </div>
            </div>

            <!-- Pestaña 2: NUEVA - Liquidaciones Mejoradas -->
            <div class="tab-pane fade" id="segmentos" role="tabpanel" aria-labelledby="segmentos-tab">
                <h3 class="tab-header">Trazabilidad del Viaje</h3>

                <div id="formSegmentos">
                    <!-- Contenedor de liquidaciones -->
                    <div id="liquidacionesContainer">
                        <!-- Las liquidaciones se agregarán dinámicamente aquí -->
                    </div>

                    <!-- BOTÓN AGREGAR LIQUIDACIÓN -->
                    <!-- Campo oculto para los datos de liquidaciones -->
                    <input type="hidden" id="hiddenLiquidacionesData" name="hiddenLiquidacionesData" value="[]" />

                    <!-- Botones de navegación -->
                    <div class="form-group text-right mt-4">
                        <button type="button" class="btn btn-secondary" onclick="showPreviousTab('datos')">Atrás</button>
                        <button type="button" class="btn btn-primary" onclick="validarLiquidacionesYContinuar()">Siguiente: Resumen Financiero</button>
                    </div>
                </div>
            </div>

            <!-- Pestaña 3: Resumen Financiero - MANTENER IGUAL -->
            <div class="tab-pane fade" id="liquidacion" role="tabpanel" aria-labelledby="liquidacion-tab">
                <h3 class="tab-header">Resumen Financiero</h3>
                <div id="formLiquidacion">

                    <!-- Campo oculto para gastos adicionales -->
                    <input type="hidden" id="hiddenGastosAdicionales" name="hiddenGastosAdicionales" value="[]" />
                    <input type="hidden" id="hiddenIngresosAdicionales" name="hiddenIngresosAdicionales" value="[]" />

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
                        <tbody id="ingresosFijosBody">
                            <tr>
                                <td>1</td>
                                <td>Despacho</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescDespacho" placeholder="Descripción"></td>
                                <td>
                                    <input type="number" class="form-control ingreso-soles" name="txtDespachoSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td>
                                    <input type="number" class="form-control ingreso-dolares" name="txtDespachoDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()"></td>
                            </tr>
                            <tr>
                                <td>2</td>
                                <td>Mensualidad</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescMensualidad" placeholder="Descripción"></td>
                                <td>
                                    <input type="number" class="form-control ingreso-soles" name="txtMensualidadSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td>
                                    <input type="number" class="form-control ingreso-dolares" name="txtMensualidadDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()"></td>
                            </tr>
                            <tr>
                                <td>3</td>
                                <td>Otros Autorizados</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescOtros" placeholder="Descripción"></td>
                                <td>
                                    <input type="number" class="form-control ingreso-soles" name="txtOtrosSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td>
                                    <input type="number" class="form-control ingreso-dolares" name="txtOtrosDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()"></td>
                            </tr>
                            <tr>
                                <td>4</td>
                                <td>Préstamo</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescPrestamo" placeholder="Descripción"></td>
                                <td>
                                    <input type="number" class="form-control ingreso-soles" name="txtPrestamoSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td>
                                    <input type="number" class="form-control ingreso-dolares" name="txtPrestamoDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()"></td>
                            </tr>
                        </tbody>
                        <tbody id="ingresosAdicionalesBody">
                            <!-- Aquí se añadirán dinámicamente los ingresos adicionales -->
                        </tbody>
                    </table>
                    <div class="text-right mb-4">
                        <button type="button" class="btn btn-success" onclick="agregarFilaIngreso()">Añadir Ingreso</button>
                    </div>

                    <!-- Tabla de Gastos -->
                    <h5>Gastos</h5>
                    <table class="table table-bordered liquidacion-tabla-gastos" id="tablaGastos">
                        <thead class="liquidacion-tabla-cabecera">
                            <tr>
                                <th>#</th>
                                <th>Gastos</th>
                                <th>Descripción</th>
                                <th>Soles (S/)</th>
                                <th>Dólares ($)</th>
                                <th>Acción</th>
                            </tr>
                        </thead>
                        <tbody id="gastosFijosBody">
                            <tr>
                                <td>1</td>
                                <td>Peajes</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescPeajes" placeholder="Descripción"></td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtPeajesSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtPeajesDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td></td>
                            </tr>
                            <tr>
                                <td>2</td>
                                <td>Alimentación</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescAlimentacion" placeholder="Descripción"></td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtAlimentacionSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtAlimentacionDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td></td>
                            </tr>
                            <tr>
                                <td>3</td>
                                <td>Apoyo-Seguridad</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescApoyoSeguridad" placeholder="Descripción"></td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtApoyoSeguridadSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtApoyoSeguridadDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td></td>
                            </tr>
                            <tr>
                                <td>4</td>
                                <td>Reparaciones Varios</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescReparaciones" placeholder="Descripción"></td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtReparacionesSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtReparacionesDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td></td>
                            </tr>
                            <tr>
                                <td>5</td>
                                <td>Movilidad</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescMovilidad" placeholder="Descripción"></td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtMovilidadSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtMovilidadDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td></td>
                            </tr>
                            <tr>
                                <td>6</td>
                                <td>Encapada/Descencarpada</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescEncapada" placeholder="Descripción"></td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtEncapadaSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtEncapadaDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td></td>
                            </tr>
                            <tr>
                                <td>7</td>
                                <td>Hospedaje</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescHospedaje" placeholder="Descripción"></td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtHospedajeSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtHospedajeDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td></td>
                            </tr>
                            <tr>
                                <td>8</td>
                                <td>Combustible</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescCombustible" placeholder="Descripción"></td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtCombustibleSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtCombustibleDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()"></td>
                                <td></td>
                            </tr>
                        </tbody>
                        <tbody id="gastosAdicionalesBody">
                            <!-- Aquí se añadirán dinámicamente los gastos adicionales -->
                        </tbody>
                    </table>

                    <!-- Botón para agregar filas -->
                    <div class="text-right">
                        <button type="button" class="btn btn-success" onclick="agregarFila()">Añadir Fila</button>
                    </div>

                    <!-- Resumen -->
                    <h5>Resumen</h5>
                    <div class="table-responsive">
                        <table class="table table-bordered">
                            <thead class="table-light">
                                <tr>
                                    <th>Concepto</th>
                                    <th>Soles (S/)</th>
                                    <th>Dólares ($)</th>
                                </tr>
                            </thead>
                            <tbody>
                                <tr>
                                    <td>Total Ingresos</td>
                                    <td id="totalIngresosSoles">0.00</td>
                                    <td id="totalIngresosDolares">0.00</td>
                                </tr>
                                <tr>
                                    <td>Total Gastos</td>
                                    <td id="totalGastosSoles">0.00</td>
                                    <td id="totalGastosDolares">0.00</td>
                                </tr>
                                <tr>
                                    <td>Diferencia de Saldo</td>
                                    <td id="diferenciaSaldoSoles">0.00</td>
                                    <td id="diferenciaSaldoDolares">0.00</td>
                                </tr>
                            </tbody>
                        </table>
                    </div>

                    <!-- Observaciones de Liquidación -->
                    <div class="form-group mt-3">
                        <label for="txtObservacionesLiquidacion">Observaciones de Liquidación:</label>
                        <textarea id="txtObservacionesLiquidacion" name="txtObservacionesLiquidacion" class="form-control" rows="3" placeholder="Añadir observaciones sobre la liquidación"></textarea>
                    </div>

                    <!-- Botones de navegación -->
                    <div class="form-group text-right mt-3">
                        <button type="button" class="btn btn-secondary" onclick="showPreviousTab('segmentos')">Atrás</button>
                        <asp:Button ID="btnGuardar" runat="server" Text="Guardar Orden de Viaje" CssClass="btn btn-success" OnClick="btnGuardar_Click" />
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- CSS MEJORADO para Liquidaciones Avanzadas -->
    <style>
        /* CSS DE SELECT2 */
        .select2-container {
            width: 100% !important;
        }

        .select2-container--default .select2-selection--single {
            height: 38px !important;
            border: 1px solid #ced4da !important;
            border-radius: 0.25rem !important;
            background-color: #fff !important;
        }

        .select2-container--default .select2-selection--single .select2-selection__rendered {
            color: #495057 !important;
            line-height: 36px !important;
            padding-left: 12px !important;
            padding-right: 20px !important;
        }

        .select2-container--default .select2-selection--single .select2-selection__arrow {
            height: 36px !important;
            top: 1px !important;
            right: 1px !important;
            width: 20px !important;
        }

        .select2-dropdown {
            border: 1px solid #ced4da !important;
            border-radius: 0.25rem !important;
        }

        .select2-container--default .select2-search--dropdown .select2-search__field {
            border: 1px solid #ced4da !important;
            border-radius: 0.25rem !important;
            padding: 4px 6px !important;
        }

        .select2-container--default .select2-results__option--highlighted[aria-selected] {
            background-color: #007bff !important;
            color: white !important;
        }

        .select2-container--default .select2-selection--single:focus {
            border-color: #80bdff !important;
            outline: 0 !important;
            box-shadow: 0 0 0 0.2rem rgba(0, 123, 255, 0.25) !important;
        }

        /* Estilos existentes de liquidación */
        .liquidacion-tabla-ingresos th:nth-child(4),
        .liquidacion-tabla-ingresos td:nth-child(4),
        .liquidacion-tabla-ingresos th:nth-child(5),
        .liquidacion-tabla-ingresos td:nth-child(5),
        .liquidacion-tabla-gastos th:nth-child(4),
        .liquidacion-tabla-gastos td:nth-child(4),
        .liquidacion-tabla-gastos th:nth-child(5),
        .liquidacion-tabla-gastos td:nth-child(5) {
            width: 15%;
            min-width: 120px;
        }

        /* ESTILOS MEJORADOS: Liquidaciones Avanzadas */
        .liquidacion-card {
            border: 3px solid #007bff;
            border-radius: 15px;
            margin-bottom: 30px;
            background: #f8f9fa;
            box-shadow: 0 8px 16px rgba(0,0,0,0.1);
            position: relative;
        }

        .liquidacion-header {
            background: linear-gradient(135deg, #007bff, #0056b3);
            color: white;
            padding: 18px 25px;
            border-radius: 12px 12px 0 0;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .liquidacion-body {
            padding: 25px;
            background: white;
            border-radius: 0 0 12px 12px;
        }

        .liquidacion-info {
            background: #e3f2fd;
            border: 1px solid #90caf9;
            border-radius: 8px;
            padding: 15px;
            margin-bottom: 20px;
        }

        /* ESTILOS SUB-TRAMOS MEJORADOS */
        .subtramos-section {
            background: #f1f8e9;
            border-radius: 10px;
            padding: 20px;
            margin-bottom: 20px;
            border: 2px solid #c8e6c9;
        }

        .subtramo-card {
            border: 2px solid #e0e0e0;
            border-radius: 12px;
            margin-bottom: 20px;
            background: white;
            box-shadow: 0 4px 8px rgba(0,0,0,0.1);
            transition: all 0.3s ease;
        }

        .subtramo-card:hover {
            transform: translateY(-2px);
            box-shadow: 0 6px 12px rgba(0,0,0,0.15);
        }

        .subtramo-header {
            background: linear-gradient(135deg, #4caf50, #388e3c);
            color: white;
            padding: 15px 20px;
            border-radius: 10px 10px 0 0;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .subtramo-body {
            padding: 20px;
        }

        .ruta-visual {
            background: linear-gradient(135deg, #fff3e0, #ffe0b2);
            border: 2px dashed #ff9800;
            border-radius: 10px;
            padding: 20px;
            margin-bottom: 20px;
            text-align: center;
            position: relative;
            overflow: hidden;
        }

        .ruta-visual::before {
            content: '';
            position: absolute;
            top: 0;
            left: -100%;
            width: 100%;
            height: 100%;
            background: linear-gradient(90deg, transparent, rgba(255,255,255,0.4), transparent);
            animation: shimmer 3s infinite;
        }

        @keyframes shimmer {
            0% { left: -100%; }
            100% { left: 100%; }
        }

        .ruta-visual .origen {
            display: inline-block;
            background: linear-gradient(135deg, #4caf50, #388e3c);
            color: white;
            padding: 10px 20px;
            border-radius: 25px;
            margin-right: 15px;
            font-weight: bold;
            box-shadow: 0 2px 4px rgba(0,0,0,0.2);
        }

        .ruta-visual .flecha {
            font-size: 24px;
            color: #ff9800;
            margin: 0 15px;
            animation: pulse 2s infinite;
        }

        @keyframes pulse {
            0%, 100% { transform: scale(1); }
            50% { transform: scale(1.1); }
        }

        .ruta-visual .destino {
            display: inline-block;
            background: linear-gradient(135deg, #f44336, #d32f2f);
            color: white;
            padding: 10px 20px;
            border-radius: 25px;
            margin-left: 15px;
            font-weight: bold;
            box-shadow: 0 2px 4px rgba(0,0,0,0.2);
        }

        /* BADGES DE OPERACIÓN MEJORADOS */
        .operacion-badge {
            padding: 8px 15px;
            border-radius: 20px;
            font-size: 13px;
            font-weight: bold;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        .badge-transito-vacio {
            background: linear-gradient(135deg, #9e9e9e, #757575);
            color: white;
        }

        .badge-transito-carga {
            background: linear-gradient(135deg, #2196f3, #1976d2);
            color: white;
        }

        .badge-solo-carga {
            background: linear-gradient(135deg, #4caf50, #388e3c);
            color: white;
        }

        .badge-solo-descarga {
            background: linear-gradient(135deg, #ff9800, #f57c00);
            color: white;
        }

        .badge-descarga-carga {
            background: linear-gradient(135deg, #9c27b0, #7b1fa2);
            color: white;
        }

        .badge-parada {
            background: linear-gradient(135deg, #607d8b, #455a64);
            color: white;
        }

        /* SECCIONES DE OPERACIONES */
        .operaciones-section {
            background: #fafafa;
            border: 1px solid #e0e0e0;
            border-radius: 10px;
            padding: 20px;
            margin-top: 20px;
        }

        .operacion-subsection {
            background: white;
            border: 1px solid #e0e0e0;
            border-radius: 8px;
            padding: 15px;
            margin-bottom: 15px;
        }

        .operacion-subsection-header {
            background: linear-gradient(135deg, #ff5722, #d84315);
            color: white;
            padding: 10px 15px;
            margin: -15px -15px 15px -15px;
            border-radius: 7px 7px 0 0;
            font-weight: bold;
        }

        .operacion-carga-header {
            background: linear-gradient(135deg, #4caf50, #388e3c) !important;
        }

        .operacion-descarga-header {
            background: linear-gradient(135deg, #ff9800, #f57c00) !important;
        }

        .badge-nacional {
            background: linear-gradient(135deg, #28a745, #1e7e34);
            color: white;
            padding: 6px 15px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: bold;
        }

        .badge-internacional {
            background: linear-gradient(135deg, #dc3545, #c82333);
            color: white;
            padding: 6px 15px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: bold;
        }

        .producto-row {
            background: #f5f5f5;
            border: 1px solid #e0e0e0;
            border-radius: 6px;
            padding: 12px;
            margin-bottom: 10px;
        }

        /* GUÍAS POR SUB-TRAMO - ESTILO MEJORADO */
        .guias-subtramo {
            background: linear-gradient(135deg, #e8f5e9, #f1f8e9);
            border: 2px solid #4caf50;
            border-radius: 10px;
            padding: 18px;
            margin-top: 20px;
            box-shadow: 0 2px 4px rgba(76, 175, 80, 0.1);
        }

        .guias-subtramo .alert-warning {
            background: rgba(76, 175, 80, 0.1);
            border-color: #4caf50;
            color: #2e7d32;
            margin-bottom: 15px;
            border-radius: 8px;
        }

        .btn-agregar-subtramo {
            border: 3px dashed #4caf50;
            background: linear-gradient(135deg, #f1f8e9, #e8f5e8);
            color: #2e7d32;
            padding: 18px 30px;
            border-radius: 12px;
            transition: all 0.3s ease;
            width: 100%;
            margin-bottom: 15px;
            font-weight: bold;
            text-transform: uppercase;
            letter-spacing: 1px;
        }

        .btn-agregar-subtramo:hover {
            background: linear-gradient(135deg, #4caf50, #388e3c);
            color: white;
            border-color: #4caf50;
            transform: translateY(-2px);
            box-shadow: 0 4px 8px rgba(76, 175, 80, 0.3);
        }

        input[type=number] {
            -moz-appearance: textfield;
        }

        input[type=number]::-webkit-inner-spin-button,
        input[type=number]::-webkit-outer-spin-button {
            -webkit-appearance: none;
            margin: 0;
        }

        .is-invalid {
            border-color: #dc3545 !important;
            box-shadow: 0 0 0 0.2rem rgba(220, 53, 69, 0.25) !important;
        }

        /* Mejoras visuales */
        .btn-lg {
            padding: 12px 24px;
            font-size: 16px;
        }

        /* Iconos para tipos de operación */
        .icon-transito-vacio:before { content: "🚛 "; }
        .icon-transito-carga:before { content: "🚚 "; }
        .icon-solo-carga:before { content: "📦 "; }
        .icon-solo-descarga:before { content: "📤 "; }
        .icon-descarga-carga:before { content: "🔄 "; }
        .icon-parada:before { content: "⏸️ "; }

        /* Tooltip personalizado */
        .tooltip-custom {
            position: relative;
            cursor: help;
        }

        .tooltip-custom:hover::after {
            content: attr(data-tooltip);
            position: absolute;
            top: -30px;
            left: 50%;
            transform: translateX(-50%);
            background: rgba(0,0,0,0.8);
            color: white;
            padding: 5px 10px;
            border-radius: 4px;
            font-size: 12px;
            white-space: nowrap;
            z-index: 1000;
        }

        /* Continuidad visual */
        .continuidad-origen {
            background: #e8f5e8 !important;
            border: 2px solid #4caf50 !important;
        }

        .continuidad-mensaje {
            background: #e8f5e8;
            border: 1px solid #4caf50;
            color: #2e7d32;
            padding: 8px 12px;
            border-radius: 6px;
            font-size: 12px;
            margin-top: 5px;
        }

        /* Indicador de productos transferidos */
        .productos-transferidos {
            background: #e3f2fd;
            border: 1px solid #2196f3;
            color: #1565c0;
            padding: 10px;
            border-radius: 6px;
            margin-bottom: 15px;
            font-size: 13px;
        }

        .productos-transferidos i {
            color: #2196f3;
            margin-right: 5px;
        }



         /* Indicadores de campos requeridos */
    .text-danger {
        font-weight: bold;
    }
    
    /* Mensaje informativo para productos */
    .alert-info {
        border-left: 4px solid #17a2b8;
        background-color: #d1ecf1;
        color: #0c5460;
    }
    
    /* Estilo para selects deshabilitados */
    .form-control[disabled] {
        background-color: #f8f9fa;
        opacity: 0.8;
    }
    
    /* Mejoras visuales para las guías */
    .guias-subtramo.disabled {
        opacity: 0.5;
        pointer-events: none;
        background-color: #f8f9fa !important;
    }
    
    /* Indicador de cliente requerido */
    .cliente-requerido {
        border: 2px solid #dc3545 !important;
        animation: pulse-border 1s infinite;
    }
    
    @keyframes pulse-border {
        0% { border-color: #dc3545; }
        50% { border-color: #ff6b7a; }
        100% { border-color: #dc3545; }
    }
    
    /* Productos filtrados */
    .productos-filtrados {
        border-left: 3px solid #28a745;
        background-color: #d4edda;
        padding: 10px;
        margin-bottom: 10px;
        border-radius: 4px;
    }
    </style>

    <!-- Scripts -->
    <script src="https://code.jquery.com/jquery-3.6.4.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/select2/4.1.0-rc.0/js/select2.min.js"></script>
    <link href="https://cdnjs.cloudflare.com/ajax/libs/select2/4.1.0-rc.0/css/select2.min.css" rel="stylesheet" />

    <!-- Script COMPLETAMENTE MEJORADO para Liquidaciones Avanzadas -->
    <script>
        // ========================================
        // VARIABLES GLOBALES - VERSIÓN CORREGIDA
        // ========================================
        let liquidacionesData = [];
        let contadorLiquidaciones = 0;
        let contadorSubTramosPorLiquidacion = {};
        let contadorIngresosAdicionales = 0;
        let contadorGastosAdicionales = 0;

        // Variables para el sistema de auto-llenado
        let productosCargadosGlobal = []; // Sistema principal
        let productosCargadosPorLiquidacion = {}; // Compatibilidad
        let historialOperaciones = [];

        // Variables de datos del sistema
        const productos = <%= ObtenerProductosJSON() %>;
        const clientes = <%= ObtenerClientesJSON() %>;
const cpics = <%= ObtenerCPICsJSON() %>;
        const facturas = <%= ObtenerFacturasJSON() %>;

        // ========================================
        // FUNCIONES DE SINCRONIZACIÓN
        // ========================================
        function sincronizarProductosCargados() {
            productosCargadosPorLiquidacion = {};
            if (productosCargadosGlobal.length > 0) {
                productosCargadosPorLiquidacion[1] = productosCargadosGlobal;
            }
        }

        // ========================================
        // FUNCIÓN PRINCIPAL: ACTUALIZAR PRODUCTOS CARGADOS
        // ========================================
        function actualizarProductosCargadosGlobal() {
            console.log("🔄 Actualizando productos cargados globalmente...");

            // Limpiar array global
            productosCargadosGlobal = [];

            // Recorrer TODOS los sub-tramos buscando operaciones de carga
            $('.subtramo-card').each(function () {
                const subTramoId = $(this).attr('id');
                const partes = subTramoId.split('_');
                const numeroLiquidacion = partes[1];
                const numeroSubTramo = partes[2];

                const tipoOperacion = $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_operacion`).val();

                if (tipoOperacion === 'SOLO_CARGA' || tipoOperacion === 'DESCARGA_Y_CARGA') {
                    console.log(`📦 Encontrada operación de carga en Sub-Tramo ${numeroSubTramo}`);

                    const productosDelSubTramo = recopilarProductosDeCarga(numeroLiquidacion, numeroSubTramo);

                    if (productosDelSubTramo.length > 0) {
                        productosDelSubTramo.forEach(producto => {
                            productosCargadosGlobal.push({
                                ...producto,
                                subTramoOrigen: numeroSubTramo,
                                liquidacionOrigen: numeroLiquidacion,
                                timestamp: new Date().getTime()
                            });
                        });
                        console.log(`✅ Agregados ${productosDelSubTramo.length} productos del Sub-Tramo ${numeroSubTramo}`);
                    }
                }
            });

            console.log("📊 Productos cargados globalmente:", productosCargadosGlobal);

            // Sincronizar para compatibilidad
            sincronizarProductosCargados();

            // Actualizar sub-tramos de descarga existentes
            actualizarTodosLosSubTramosDescarga();
        }

        // ========================================
        // FUNCIÓN CORREGIDA: RECOPILAR PRODUCTOS DE CARGA
        // ========================================
        function recopilarProductosDeCarga(numeroLiquidacion, numeroSubTramo) {
            const productosEncontrados = [];

            // Buscar todos los productos en la sección de carga
            $(`#productos_carga_${numeroLiquidacion}_${numeroSubTramo} .producto-row`).each(function () {
                const $row = $(this);
                const idProducto = $row.find('.producto-select').val();
                const cantidad = $row.find('.producto-cantidad').val();

                if (idProducto && cantidad && cantidad > 0) {
                    const productoInfo = productos.find(p => p.idProducto == idProducto);
                    const nombreProducto = productoInfo ? productoInfo.nombre : 'Producto no encontrado';

                    productosEncontrados.push({
                        idProducto: parseInt(idProducto),
                        nombre: nombreProducto,
                        cantidad: parseInt(cantidad)
                    });
                }
            });

            // Recopilar información del cliente y documentos
            const clienteInfo = recopilarInfoClienteCarga(numeroLiquidacion, numeroSubTramo);

            // Agregar info del cliente a cada producto
            if (productosEncontrados.length > 0) {
                productosEncontrados.forEach(producto => {
                    producto.clienteInfo = clienteInfo;
                });
            }

            return productosEncontrados;
        }

        function recopilarInfoClienteCarga(numeroLiquidacion, numeroSubTramo) {
            const esInternacional = $(`input[name="carga_${numeroLiquidacion}_${numeroSubTramo}_tipo"]:checked`).val() === '1';

            return {
                idCliente: $(`#carga_${numeroLiquidacion}_${numeroSubTramo}_cliente`).val(),
                idCPIC: esInternacional ? $(`#carga_${numeroLiquidacion}_${numeroSubTramo}_cpic`).val() : null,
                idFactura: !esInternacional ? $(`#carga_${numeroLiquidacion}_${numeroSubTramo}_factura`).val() : null,
                esInternacional: esInternacional
            };
        }

        // ========================================
        // FUNCIÓN PRINCIPAL: AUTO-LLENAR DESCARGA
        // ========================================
        function autoLlenarProductosDescarga(numeroLiquidacion, numeroSubTramo) {
            console.log(`🚀 INICIANDO auto-llenado para Sub-Tramo ${numeroSubTramo}`);

            // PASO 1: Buscar TODOS los productos cargados en sub-tramos anteriores
            const todosLosProductosCargados = buscarProductosCargadosEnSubTramosAnteriores(numeroLiquidacion, numeroSubTramo);

            console.log("📦 Productos encontrados:", todosLosProductosCargados);

            if (todosLosProductosCargados.length === 0) {
                console.log("⚠️ No hay productos cargados para transferir");
                return;
            }

            // PASO 2: Mostrar indicador de productos transferidos
            mostrarIndicadorDeTransferencia(numeroLiquidacion, numeroSubTramo, todosLosProductosCargados);

            // PASO 3: Llenar la tabla de descarga
            llenarTablaDescarga(numeroLiquidacion, numeroSubTramo, todosLosProductosCargados);

            // PASO 4: Transferir información del cliente
            const ultimoCliente = todosLosProductosCargados[todosLosProductosCargados.length - 1];
            if (ultimoCliente && ultimoCliente.clienteInfo) {
                transferirDatosCliente(numeroLiquidacion, numeroSubTramo, ultimoCliente.clienteInfo);
            }

            console.log("✅ Auto-llenado COMPLETADO");
        }

        function buscarProductosCargadosEnSubTramosAnteriores(numeroLiquidacion, numeroSubTramoActual) {
            const productosEncontrados = [];

            console.log(`🔍 Buscando productos SOLO del sub-tramo anterior más reciente a ${numeroSubTramoActual}`);

            // Recorrer HACIA ATRÁS para encontrar el sub-tramo anterior MÁS RECIENTE con carga
            for (let i = numeroSubTramoActual - 1; i >= 1; i--) {
                const tipoOperacion = $(`#subtramo_${numeroLiquidacion}_${i}_operacion`).val();

                console.log(`   📋 Revisando Sub-Tramo ${i}: ${tipoOperacion}`);

                // Si encontramos un sub-tramo con operación de carga
                if (tipoOperacion === 'SOLO_CARGA' || tipoOperacion === 'DESCARGA_Y_CARGA') {
                    console.log(`   ✅ ¡Encontrado! Sub-Tramo ${i} tiene operación de carga: ${tipoOperacion}`);

                    // Buscar productos SOLO en este sub-tramo (el más reciente con carga)
                    $(`#productos_carga_${numeroLiquidacion}_${i} .producto-row`).each(function () {
                        const $fila = $(this);
                        const idProducto = $fila.find('.producto-select').val();
                        const cantidad = $fila.find('.producto-cantidad').val();

                        if (idProducto && cantidad && cantidad > 0) {
                            // Buscar información completa del producto
                            const productoCompleto = productos.find(p => p.idProducto == idProducto);

                            // Recopilar info del cliente
                            const esInternacional = $(`input[name="carga_${numeroLiquidacion}_${i}_tipo"]:checked`).val() === '1';
                            const clienteInfo = {
                                idCliente: $(`#carga_${numeroLiquidacion}_${i}_cliente`).val(),
                                idCPIC: esInternacional ? $(`#carga_${numeroLiquidacion}_${i}_cpic`).val() : null,
                                idFactura: !esInternacional ? $(`#carga_${numeroLiquidacion}_${i}_factura`).val() : null,
                                esInternacional: esInternacional
                            };

                            productosEncontrados.push({
                                idProducto: parseInt(idProducto),
                                nombre: productoCompleto ? productoCompleto.nombre : 'Producto no encontrado',
                                cantidad: parseInt(cantidad),
                                subTramoOrigen: i,
                                clienteInfo: clienteInfo
                            });

                            console.log(`   📦 Producto agregado: ${productoCompleto?.nombre} (${cantidad} bolsas)`);
                        }
                    });

                    // *** IMPORTANTE: SALIR del bucle después de encontrar el primer sub-tramo con carga ***
                    console.log(`   🛑 Deteniendo búsqueda. Solo transferir del Sub-Tramo ${i}`);
                    break;
                }
            }

            console.log(`📊 Total productos encontrados: ${productosEncontrados.length}`);
            return productosEncontrados;
        }

        function mostrarIndicadorDeTransferencia(numeroLiquidacion, numeroSubTramo, productos) {
            const indicadorHtml = `
        <div class="productos-transferidos">
            <i class="fas fa-info-circle"></i>
            <strong>✅ Productos transferidos automáticamente:</strong>
            <ul class="mb-0 mt-2">
                ${productos.map(p =>
                `<li><strong>${p.nombre}</strong> - ${p.cantidad} bolsas (desde Sub-Tramo ${p.subTramoOrigen})</li>`
            ).join('')}
            </ul>
        </div>
    `;

            $(`#indicador_productos_transferidos_${numeroLiquidacion}_${numeroSubTramo}`).html(indicadorHtml);
        }

        function llenarTablaDescarga(numeroLiquidacion, numeroSubTramo, productosParaDescargar) {
            const contenedorDescarga = $(`#productos_descarga_${numeroLiquidacion}_${numeroSubTramo}`);

            // LIMPIAR contenido anterior
            contenedorDescarga.empty();

            // AGREGAR cada producto
            productosParaDescargar.forEach((producto, index) => {
                const numeroProducto = index + 1;

                const productoHtml = `
            <div class="producto-row" id="producto_descarga_${numeroLiquidacion}_${numeroSubTramo}_${numeroProducto}">
                <div class="row">
                    <div class="col-md-6">
                        <select class="form-control form-control-sm producto-select" required>
                            <option value="">Seleccione un producto</option>
                            ${productos.map(p =>
                    `<option value="${p.idProducto}" ${p.idProducto == producto.idProducto ? 'selected' : ''}>${p.nombre}</option>`
                ).join('')}
                        </select>
                    </div>
                    <div class="col-md-4">
                        <input type="number" class="form-control form-control-sm producto-cantidad" 
                               placeholder="Cantidad bolsas" min="1" required value="${producto.cantidad}">
                    </div>
                    <div class="col-md-2">
                        <button type="button" class="btn btn-sm btn-outline-danger" 
                                onclick="eliminarProductoOperacion(${numeroLiquidacion}, ${numeroSubTramo}, 'descarga', ${numeroProducto})">
                            <i class="fas fa-trash"></i>
                        </button>
                    </div>
                </div>
            </div>
        `;

                contenedorDescarga.append(productoHtml);

                console.log(`   📋 Agregado a tabla: ${producto.nombre} (${producto.cantidad})`);
            });
        }

        // FUNCIÓN NUEVA: Transferir datos del cliente
        function transferirDatosCliente(numeroLiquidacion, numeroSubTramo, clienteInfo) {
            if (!clienteInfo || !clienteInfo.idCliente) {
                console.log("⚠️ No hay información de cliente para transferir");
                return;
            }

            console.log("📋 Transfiriendo datos del cliente:", clienteInfo);

            // Establecer tipo de transporte
            const tipoRadio = clienteInfo.esInternacional ? '1' : '0';
            $(`input[name="descarga_${numeroLiquidacion}_${numeroSubTramo}_tipo"][value="${tipoRadio}"]`).prop('checked', true);

            // Activar/desactivar campos
            toggleClienteTipoOperacion(numeroLiquidacion, numeroSubTramo, 'descarga');

            // Transferir datos después de un pequeño delay
            setTimeout(() => {
                // Cliente
                $(`#descarga_${numeroLiquidacion}_${numeroSubTramo}_cliente`).val(clienteInfo.idCliente).trigger('change');

                // Documento
                if (clienteInfo.esInternacional && clienteInfo.idCPIC) {
                    $(`#descarga_${numeroLiquidacion}_${numeroSubTramo}_cpic`).val(clienteInfo.idCPIC).trigger('change');
                } else if (!clienteInfo.esInternacional && clienteInfo.idFactura) {
                    $(`#descarga_${numeroLiquidacion}_${numeroSubTramo}_factura`).val(clienteInfo.idFactura).trigger('change');
                }

                console.log("✅ Datos del cliente transferidos");
            }, 300);
        }

        function actualizarTodosLosSubTramosDescarga() {
            $('.subtramo-card').each(function () {
                const subTramoId = $(this).attr('id');
                const partes = subTramoId.split('_');
                const numeroLiquidacion = partes[1];
                const numeroSubTramo = partes[2];

                const tipoOperacion = $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_operacion`).val();

                // Si es un sub-tramo de descarga que ya existe
                if (tipoOperacion === 'SOLO_DESCARGA' || tipoOperacion === 'DESCARGA_Y_CARGA') {
                    // Solo actualizar si no tiene productos ya o si están vacíos
                    const tieneProductos = $(`#productos_descarga_${numeroLiquidacion}_${numeroSubTramo} .producto-row`).length > 0;
                    const primerProductoCompleto = $(`#productos_descarga_${numeroLiquidacion}_${numeroSubTramo} .producto-row:first .producto-select`).val();

                    if (!tieneProductos || !primerProductoCompleto) {
                        autoLlenarProductosDescarga(numeroLiquidacion, numeroSubTramo);
                    }
                }
            });
        }

        // ========================================
        // FUNCIONES DE MANEJO DE CAMBIOS
        // ========================================
        function onProductoChange(numeroLiquidacion, numeroSubTramo, tipoOperacion) {
            if (tipoOperacion === 'carga') {
                setTimeout(() => {
                    actualizarProductosCargadosGlobal();
                }, 100);
            }
        }

        function debugProductos() {
            console.log("=== DEBUG PRODUCTOS ===");
            console.log("Productos cargados globalmente:", productosCargadosGlobal);
            console.log("Productos por liquidación:", productosCargadosPorLiquidacion);
            console.log("Historial de operaciones:", historialOperaciones);

            $('.subtramo-card').each(function () {
                const subTramoId = $(this).attr('id');
                const tipoOperacion = $(this).find('select[id*="_operacion"]').val();
                console.log(`Sub-Tramo ${subTramoId}: ${tipoOperacion}`);
            });
        }

        // ========================================
        // FUNCIONES DE NAVEGACIÓN
        // ========================================
        function showNextTab(tabId) {
            $('#' + tabId + '-tab').tab('show');
        }

        function showPreviousTab(tabId) {
            $('#' + tabId + '-tab').tab('show');
        }

        // ========================================
        // FUNCIONES DE LIQUIDACIÓN
        // ========================================
        function agregarNuevaLiquidacion() {
            contadorLiquidaciones++;
            contadorSubTramosPorLiquidacion[contadorLiquidaciones] = 0;
            productosCargadosPorLiquidacion[contadorLiquidaciones] = [];

            const liquidacionHtml = generarLiquidacionHtml(contadorLiquidaciones);
            $('#liquidacionesContainer').append(liquidacionHtml);

            // Agregar el primer sub-tramo automáticamente
            agregarSubTramoALiquidacion(contadorLiquidaciones);
        }

        function generarLiquidacionHtml(numero) {
            return `
        <div class="liquidacion-card" id="liquidacion_${numero}">
            <div class="liquidacion-header">
                <div>
                    <h4 class="mb-0">
                        <i class="fas fa-clipboard-list"></i> 
                        Liquidación Única
                    </h4>
                    <small>Ciclo completo de operaciones con sub-tramos continuos</small>
                </div>
            </div>
            
            <div class="liquidacion-body">
                <div class="subtramos-section">
                    <h6 class="mb-3">
                        <i class="fas fa-route text-success"></i> 
                        Sub-Tramos con Continuidad Automática
                    </h6>
                    
                    <div id="subtramos_liquidacion_${numero}">
                        <!-- Los sub-tramos se agregarán dinámicamente aquí -->
                    </div>
                    
                    <button type="button" class="btn btn-agregar-subtramo" onclick="agregarSubTramoALiquidacion(${numero})">
                        <i class="fas fa-plus"></i> Agregar Siguiente Sub-Tramo
                    </button>
                </div>

                <!-- Observaciones de la liquidación -->
                <div class="row mt-3">
                    <div class="col-md-12">
                        <label for="liquidacion_${numero}_obs">Observaciones Generales:</label>
                        <textarea id="liquidacion_${numero}_obs" class="form-control" rows="2" 
                                  placeholder="Observaciones generales de todos los sub-tramos"></textarea>
                    </div>
                </div>
            </div>
        </div>
    `;
        }

        // ========================================
        // FUNCIONES DE SUB-TRAMOS
        // ========================================
        function agregarSubTramoALiquidacion(numeroLiquidacion) {
            contadorSubTramosPorLiquidacion[numeroLiquidacion]++;
            const numeroSubTramo = contadorSubTramosPorLiquidacion[numeroLiquidacion];

            // Obtener destino del sub-tramo anterior para continuidad
            let origenAutomatico = '';
            if (numeroSubTramo > 1) {
                const subTramoAnterior = numeroSubTramo - 1;
                origenAutomatico = $(`#subtramo_${numeroLiquidacion}_${subTramoAnterior}_destino`).val() || '';
            }

            const subTramoHtml = generarSubTramoMejoradoHtml(numeroLiquidacion, numeroSubTramo, origenAutomatico);
            $(`#subtramos_liquidacion_${numeroLiquidacion}`).append(subTramoHtml);

            // Inicializar Select2 para los nuevos dropdowns del sub-tramo
            inicializarSelect2SubTramo(numeroLiquidacion, numeroSubTramo);

            // Si hay origen automático, establecerlo y marcarlo visualmente
            if (origenAutomatico) {
                $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_origen`).val(origenAutomatico);
                $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_origen`).addClass('continuidad-origen');
                $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_origen`).after(
                    '<div class="continuidad-mensaje"><i class="fas fa-link"></i> Origen automático desde destino anterior</div>'
                );
                actualizarRutaVisual(numeroLiquidacion, numeroSubTramo);
            }
        }

        function generarSubTramoMejoradoHtml(numeroLiquidacion, numeroSubTramo, origenAutomatico = '') {
            return `
        <div class="subtramo-card" id="subtramo_${numeroLiquidacion}_${numeroSubTramo}">
            <div class="subtramo-header">
                <div>
                    <strong>Sub-Tramo ${numeroSubTramo}</strong>
                    <span class="operacion-badge ms-2" id="badge_subtramo_${numeroLiquidacion}_${numeroSubTramo}">Por definir</span>
                </div>
                <button type="button" class="btn btn-sm btn-outline-light" onclick="eliminarSubTramo(${numeroLiquidacion}, ${numeroSubTramo})">
                    <i class="fas fa-times"></i>
                </button>
            </div>
            
            <div class="subtramo-body">
                <!-- Visualización de la ruta -->
                <div class="ruta-visual" id="ruta_visual_${numeroLiquidacion}_${numeroSubTramo}">
                    <span class="origen">${origenAutomatico || 'Sin origen'}</span>
                    <span class="flecha">→</span>
                    <span class="destino">Sin destino</span>
                </div>

                <!-- Información del sub-tramo -->
                <div class="row mb-3">
                    <div class="col-md-3">
                        <label for="subtramo_${numeroLiquidacion}_${numeroSubTramo}_origen"><strong>Origen:</strong></label>
                        <input type="text" id="subtramo_${numeroLiquidacion}_${numeroSubTramo}_origen" 
                               class="form-control" placeholder="Ej: Base - Sullana" required 
                               value="${origenAutomatico}"
                               onchange="actualizarRutaVisual(${numeroLiquidacion}, ${numeroSubTramo})">
                    </div>
                    <div class="col-md-3">
                        <label for="subtramo_${numeroLiquidacion}_${numeroSubTramo}_destino"><strong>Destino:</strong></label>
                        <input type="text" id="subtramo_${numeroLiquidacion}_${numeroSubTramo}_destino" 
                               class="form-control" placeholder="Ej: Paita" required 
                               onchange="actualizarRutaYContinuidad(${numeroLiquidacion}, ${numeroSubTramo})">
                    </div>
                    <div class="col-md-6">
                        <label for="subtramo_${numeroLiquidacion}_${numeroSubTramo}_operacion"><strong>Tipo de Operación:</strong></label>
                        <select id="subtramo_${numeroLiquidacion}_${numeroSubTramo}_operacion" class="form-control" required 
                                onchange="actualizarOperacionSubTramoMejorada(${numeroLiquidacion}, ${numeroSubTramo})">
                            <option value="">Seleccione operación</option>
                            <option value="TRANSITO_VACIO">🚛 Tránsito Vacío</option>
                            <option value="TRANSITO_CARGA">🚚 Tránsito con Carga</option>
                            <option value="SOLO_CARGA">📦 Solo Carga</option>
                            <option value="SOLO_DESCARGA">📤 Solo Descarga</option>
                            <option value="DESCARGA_Y_CARGA">🔄 Descarga y Carga</option>
                            <option value="PARADA_OPERATIVA">⏸️ Parada Operativa</option>
                        </select>
                    </div>
                </div>

                <!-- SECCIÓN DE OPERACIONES -->
                <div class="operaciones-section" id="operaciones_section_${numeroLiquidacion}_${numeroSubTramo}" style="display: none;">
                    <!-- Contenido dinámico según tipo de operación -->
                </div>

                <!-- SECCIÓN: Guías del Sub-Tramo -->
                <div id="guias_subtramo_${numeroLiquidacion}_${numeroSubTramo}" class="guias-subtramo" style="display: none;">
                    <div class="alert alert-warning">
                        <strong>📋 Guías de Transporte del Sub-Tramo:</strong> Documentación específica para este tramo.
                    </div>
                    
                    <div class="row mb-3">
                        <div class="col-md-6">
                            <label for="subtramo_${numeroLiquidacion}_${numeroSubTramo}_guia_transportista">N° Guía Transportista:</label>
                            <input type="text" id="subtramo_${numeroLiquidacion}_${numeroSubTramo}_guia_transportista" class="form-control" 
                                   placeholder="Ej: GT-2025-0001" />
                        </div>
                        <div class="col-md-6">
                            <label for="subtramo_${numeroLiquidacion}_${numeroSubTramo}_guia_cliente">N° Guía Cliente:</label>
                            <input type="text" id="subtramo_${numeroLiquidacion}_${numeroSubTramo}_guia_cliente" class="form-control" 
                                   placeholder="Ej: GC-2025-0001" />
                        </div>
                    </div>

                    <div class="row mb-3">
                        <div class="col-md-6">
                            <div class="form-check">
                                <input class="form-check-input" type="checkbox" id="subtramo_${numeroLiquidacion}_${numeroSubTramo}_cruza_frontera" 
                                       onchange="toggleManifiestoSubTramo(${numeroLiquidacion}, ${numeroSubTramo})">
                                <label class="form-check-label" for="subtramo_${numeroLiquidacion}_${numeroSubTramo}_cruza_frontera">
                                    <strong>🌍 ¿Este sub-tramo cruza frontera?</strong>
                                </label>
                            </div>
                        </div>
                        <div class="col-md-6" id="campo_manifiesto_subtramo_${numeroLiquidacion}_${numeroSubTramo}" style="display: none;">
                            <label for="subtramo_${numeroLiquidacion}_${numeroSubTramo}_manifiesto">N° Manifiesto:</label>
                            <input type="text" id="subtramo_${numeroLiquidacion}_${numeroSubTramo}_manifiesto" class="form-control" 
                                   placeholder="Ej: MAN-2025-0001" />
                        </div>
                    </div>
                </div>

                <!-- Observaciones del sub-tramo -->
                <div class="row mt-3">
                    <div class="col-md-12">
                        <label for="subtramo_${numeroLiquidacion}_${numeroSubTramo}_obs">Observaciones del Sub-Tramo:</label>
                        <textarea id="subtramo_${numeroLiquidacion}_${numeroSubTramo}_obs" class="form-control" rows="2" 
                                  placeholder="Observaciones específicas de este sub-tramo"></textarea>
                    </div>
                </div>
            </div>
        </div>
    `;
        }

        function inicializarSelect2SubTramo(numeroLiquidacion, numeroSubTramo) {
            // Los Select2 se inicializarán dinámicamente cuando se creen las secciones de operaciones
        }

        function actualizarRutaYContinuidad(numeroLiquidacion, numeroSubTramo) {
            // Actualizar visualización de ruta actual
            actualizarRutaVisual(numeroLiquidacion, numeroSubTramo);

            // Propagar destino como origen del siguiente sub-tramo (si existe)
            const siguienteSubTramo = parseInt(numeroSubTramo) + 1;
            const destinoActual = $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_destino`).val();

            if (destinoActual && $(`#subtramo_${numeroLiquidacion}_${siguienteSubTramo}_origen`).length > 0) {
                $(`#subtramo_${numeroLiquidacion}_${siguienteSubTramo}_origen`).val(destinoActual);
                $(`#subtramo_${numeroLiquidacion}_${siguienteSubTramo}_origen`).addClass('continuidad-origen');
                actualizarRutaVisual(numeroLiquidacion, siguienteSubTramo);

                if ($(`#subtramo_${numeroLiquidacion}_${siguienteSubTramo}_origen`).next('.continuidad-mensaje').length === 0) {
                    $(`#subtramo_${numeroLiquidacion}_${siguienteSubTramo}_origen`).after(
                        '<div class="continuidad-mensaje"><i class="fas fa-link"></i> Origen automático desde destino anterior</div>'
                    );
                }
            }
        }

        function actualizarRutaVisual(numeroLiquidacion, numeroSubTramo) {
            const origen = $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_origen`).val() || 'Sin origen';
            const destino = $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_destino`).val() || 'Sin destino';

            $(`#ruta_visual_${numeroLiquidacion}_${numeroSubTramo} .origen`).text(origen);
            $(`#ruta_visual_${numeroLiquidacion}_${numeroSubTramo} .destino`).text(destino);
        }

        // ========================================
        // FUNCIÓN PRINCIPAL: ACTUALIZAR OPERACIÓN
        // ========================================
        function actualizarOperacionSubTramoMejorada(numeroLiquidacion, numeroSubTramo) {
            const operacion = $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_operacion`).val();
            const badge = $(`#badge_subtramo_${numeroLiquidacion}_${numeroSubTramo}`);
            const operacionesSection = $(`#operaciones_section_${numeroLiquidacion}_${numeroSubTramo}`);
            const guiasSection = $(`#guias_subtramo_${numeroLiquidacion}_${numeroSubTramo}`);

            let textoOperacion = 'Por definir';
            let claseOperacion = '';
            let necesitaGuias = false; // MODIFICADO: Nueva lógica para guías

            // Limpiar sección de operaciones
            operacionesSection.empty();

            switch (operacion) {
                case 'TRANSITO_VACIO':
                    textoOperacion = '🚛 Tránsito Vacío';
                    claseOperacion = 'badge-transito-vacio';
                    operacionesSection.hide();
                    necesitaGuias = false; // Sin guías
                    break;

                case 'TRANSITO_CARGA':
                    textoOperacion = '🚚 Tránsito con Carga';
                    claseOperacion = 'badge-transito-carga';
                    operacionesSection.hide();
                    necesitaGuias = true; // NUEVO: Habilitar guías para tránsito con carga
                    break;

                case 'SOLO_CARGA':
                    textoOperacion = '📦 Solo Carga';
                    claseOperacion = 'badge-solo-carga';
                    generarSeccionSoloCarga(numeroLiquidacion, numeroSubTramo);
                    necesitaGuias = false; // MODIFICADO: No mostrar guías para solo carga
                    break;

                case 'SOLO_DESCARGA':
                    textoOperacion = '📤 Solo Descarga';
                    claseOperacion = 'badge-solo-descarga';
                    generarSeccionSoloDescarga(numeroLiquidacion, numeroSubTramo);
                    necesitaGuias = true; // Sí mostrar guías
                    break;

                case 'DESCARGA_Y_CARGA':
                    textoOperacion = '🔄 Descarga y Carga';
                    claseOperacion = 'badge-descarga-carga';
                    generarSeccionDescargaYCarga(numeroLiquidacion, numeroSubTramo);
                    necesitaGuias = true; // Sí mostrar guías
                    break;

                case 'PARADA_OPERATIVA':
                    textoOperacion = '⏸️ Parada Operativa';
                    claseOperacion = 'badge-parada';
                    generarSeccionParadaOperativa(numeroLiquidacion, numeroSubTramo);
                    necesitaGuias = false; // Sin guías
                    break;

                default:
                    operacionesSection.hide();
                    necesitaGuias = false;
            }

            badge.text(textoOperacion).attr('class', `operacion-badge ms-2 ${claseOperacion}`);

            // NUEVA LÓGICA: Mostrar/ocultar guías según necesidad
            if (necesitaGuias) {
                guiasSection.show();
                console.log(`✅ Guías habilitadas para: ${textoOperacion}`);
            } else {
                guiasSection.hide();
                console.log(`❌ Guías deshabilitadas para: ${textoOperacion}`);
            }

            // Auto-llenar si es operación de descarga
            if (operacion === 'SOLO_DESCARGA' || operacion === 'DESCARGA_Y_CARGA') {
                console.log(`🎯 Operación de descarga detectada: ${operacion}`);
                setTimeout(() => {
                    autoLlenarProductosDescarga(numeroLiquidacion, numeroSubTramo);
                }, 800);
            }

            console.log(`🔄 Operación actualizada: ${textoOperacion}`);
        }


        // ========================================
        // 2. NUEVA FUNCIÓN: Filtrar productos por cliente
        // ========================================
        function filtrarProductosPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, idCliente) {
            console.log(`🔍 Filtrando productos para cliente: ${idCliente}`);

            // Obtener todos los selects de productos en esta operación
            const productosSelects = $(`#productos_${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo} .producto-select`);

            productosSelects.each(function () {
                const $select = $(this);
                const valorActual = $select.val(); // Guardar selección actual

                // Limpiar opciones
                $select.empty().append('<option value="">Seleccione un producto</option>');

                if (idCliente) {
                    // Filtrar productos por cliente
                    const productosDelCliente = productos.filter(p => p.idCliente == idCliente);

                    if (productosDelCliente.length > 0) {
                        productosDelCliente.forEach(producto => {
                            const selected = valorActual == producto.idProducto ? 'selected' : '';
                            $select.append(`<option value="${producto.idProducto}" ${selected}>${producto.nombre}</option>`);
                        });
                        console.log(`✅ ${productosDelCliente.length} productos encontrados para el cliente`);
                    } else {
                        $select.append('<option value="" disabled>No hay productos para este cliente</option>');
                        console.log(`⚠️ No se encontraron productos para el cliente ${idCliente}`);
                    }
                } else {
                    // Si no hay cliente seleccionado, mostrar todos los productos
                    productos.forEach(producto => {
                        const selected = valorActual == producto.idProducto ? 'selected' : '';
                        $select.append(`<option value="${producto.idProducto}" ${selected}>${producto.nombre}</option>`);
                    });
                }
            });
        }

        // ========================================
        // 3. NUEVA FUNCIÓN: Filtrar facturas por cliente
        // ========================================
        function filtrarFacturasPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, idCliente) {
            console.log(`💼 Filtrando facturas para cliente: ${idCliente}`);

            const facturaSelect = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_factura`);
            const valorActual = facturaSelect.val(); // Guardar selección actual

            // Limpiar opciones
            facturaSelect.empty().append('<option value="">Seleccione una factura</option>');

            if (idCliente) {
                // Filtrar facturas por cliente
                const facturasDelCliente = facturas.filter(f => f.idCliente == idCliente);

                if (facturasDelCliente.length > 0) {
                    facturasDelCliente.forEach(factura => {
                        const selected = valorActual == factura.idFactura ? 'selected' : '';
                        facturaSelect.append(`<option value="${factura.idFactura}" ${selected}>${factura.numeroFactura}</option>`);
                    });
                    console.log(`✅ ${facturasDelCliente.length} facturas encontradas para el cliente`);
                } else {
                    facturaSelect.append('<option value="" disabled>No hay facturas para este cliente</option>');
                    console.log(`⚠️ No se encontraron facturas para el cliente ${idCliente}`);
                }
            } else {
                // Si no hay cliente seleccionado, mostrar todas las facturas
                facturas.forEach(factura => {
                    const selected = valorActual == factura.idFactura ? 'selected' : '';
                    facturaSelect.append(`<option value="${factura.idFactura}" ${selected}>${factura.numeroFactura}</option>`);
                });
            }

            // Refrescar Select2 si está inicializado
            if (facturaSelect.hasClass('select2-hidden-accessible')) {
                facturaSelect.trigger('change.select2');
            }
        }

        // ========================================
        // 4. NUEVA FUNCIÓN: Manejar cambio de cliente
        // ========================================
        function onClienteChange(numeroLiquidacion, numeroSubTramo, tipoOperacion) {
            const idCliente = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cliente`).val();

            console.log(`👤 Cliente cambiado: ${idCliente} en ${tipoOperacion}`);

            // Filtrar productos y facturas según el cliente seleccionado
            if (idCliente) {
                filtrarProductosPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, idCliente);

                // Solo filtrar facturas si es transporte nacional
                const esInternacional = $(`input[name="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_tipo"]:checked`).val() === '1';
                if (!esInternacional) {
                    filtrarFacturasPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, idCliente);
                }
            } else {
                // Si no hay cliente, limpiar productos y facturas
                filtrarProductosPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, null);
                filtrarFacturasPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, null);
            }
        }



        // ========================================
        // FUNCIONES PARA GENERAR SECCIONES DE OPERACIÓN
        // ========================================
        function generarSeccionSoloCarga(numeroLiquidacion, numeroSubTramo) {
            const contenido = `
        <h6 class="mb-3"><i class="fas fa-box text-success"></i> Operación de Carga</h6>
        
        <div class="operacion-subsection">
            <div class="operacion-subsection-header operacion-carga-header">
                📦 Productos a Cargar
            </div>
            
            ${generarSeccionClienteMejorada(numeroLiquidacion, numeroSubTramo, 'carga')}
        </div>
    `;

            const operacionesSection = $(`#operaciones_section_${numeroLiquidacion}_${numeroSubTramo}`);
            operacionesSection.html(contenido).show();

            inicializarSelect2OperacionMejorada(numeroLiquidacion, numeroSubTramo, 'carga');
        }

        function generarSeccionSoloDescarga(numeroLiquidacion, numeroSubTramo) {
            const contenido = `
        <h6 class="mb-3"><i class="fas fa-shipping-fast text-warning"></i> Operación de Descarga</h6>
        
        <div id="indicador_productos_transferidos_${numeroLiquidacion}_${numeroSubTramo}"></div>
        
        <div class="operacion-subsection">
            <div class="operacion-subsection-header operacion-descarga-header">
                📤 Productos a Descargar
            </div>
            
            ${generarSeccionClienteMejorada(numeroLiquidacion, numeroSubTramo, 'descarga')}
        </div>
    `;

            const operacionesSection = $(`#operaciones_section_${numeroLiquidacion}_${numeroSubTramo}`);
            operacionesSection.html(contenido).show();

            inicializarSelect2OperacionMejorada(numeroLiquidacion, numeroSubTramo, 'descarga');
        }

        function generarSeccionDescargaYCarga(numeroLiquidacion, numeroSubTramo) {
            const contenido = `
        <h6 class="mb-3"><i class="fas fa-exchange-alt text-purple"></i> Operación de Cambio de Carga</h6>
        
        <div id="indicador_productos_transferidos_${numeroLiquidacion}_${numeroSubTramo}"></div>
        
        <div class="operacion-subsection">
            <div class="operacion-subsection-header operacion-descarga-header">
                📤 Productos a Descargar
            </div>
            
            ${generarSeccionClienteMejorada(numeroLiquidacion, numeroSubTramo, 'descarga')}
        </div>
        
        <div class="operacion-subsection">
            <div class="operacion-subsection-header operacion-carga-header">
                📦 Productos a Cargar
            </div>
            
            ${generarSeccionClienteMejorada(numeroLiquidacion, numeroSubTramo, 'carga')}
        </div>
    `;

            const operacionesSection = $(`#operaciones_section_${numeroLiquidacion}_${numeroSubTramo}`);
            operacionesSection.html(contenido).show();

            inicializarSelect2OperacionMejorada(numeroLiquidacion, numeroSubTramo, 'descarga');
            inicializarSelect2OperacionMejorada(numeroLiquidacion, numeroSubTramo, 'carga');
        }

        function generarSeccionParadaOperativa(numeroLiquidacion, numeroSubTramo) {
            const contenido = `
        <h6 class="mb-3"><i class="fas fa-pause-circle text-secondary"></i> Parada Operativa</h6>
        
        <div class="operacion-subsection">
            <div class="alert alert-info">
                <strong>ℹ️ Información:</strong> Esta es una parada sin operaciones de carga o descarga.
            </div>
            
            <div class="row">
                <div class="col-md-6">
                    <label for="parada_${numeroLiquidacion}_${numeroSubTramo}_motivo">Motivo de la Parada:</label>
                    <select id="parada_${numeroLiquidacion}_${numeroSubTramo}_motivo" class="form-control">
                        <option value="">Seleccione motivo</option>
                        <option value="DESCANSO">Descanso obligatorio</option>
                        <option value="ESPERA">Espera de programación</option>
                        <option value="MANTENIMIENTO">Mantenimiento del vehículo</option>
                        <option value="TRAMITES">Trámites documentarios</option>
                        <option value="OTROS">Otros motivos</option>
                    </select>
                </div>
                <div class="col-md-6">
                    <label for="parada_${numeroLiquidacion}_${numeroSubTramo}_duracion">Duración Estimada (horas):</label>
                    <input type="number" id="parada_${numeroLiquidacion}_${numeroSubTramo}_duracion" 
                           class="form-control" placeholder="Ej: 8" min="1" max="72">
                </div>
            </div>
        </div>
    `;

            const operacionesSection = $(`#operaciones_section_${numeroLiquidacion}_${numeroSubTramo}`);
            operacionesSection.html(contenido).show();
        }

        function generarSeccionClienteMejorada(numeroLiquidacion, numeroSubTramo, tipoOperacion) {
            return `
        <!-- Tipo de cliente -->
        <div class="row mb-3">
            <div class="col-md-12">
                <label>Tipo de Transporte:</label>
                <div class="form-check form-check-inline">
                    <input class="form-check-input" type="radio" 
                           name="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_tipo" 
                           id="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_nacional" value="0" checked 
                           onchange="toggleClienteTipoOperacionMejorada(${numeroLiquidacion}, ${numeroSubTramo}, '${tipoOperacion}')">
                    <label class="form-check-label" for="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_nacional">
                        Nacional (Perú)
                    </label>
                </div>
                <div class="form-check form-check-inline">
                    <input class="form-check-input" type="radio" 
                           name="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_tipo" 
                           id="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_internacional" value="1"
                           onchange="toggleClienteTipoOperacionMejorada(${numeroLiquidacion}, ${numeroSubTramo}, '${tipoOperacion}')">
                    <label class="form-check-label" for="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_internacional">
                        Internacional (Ecuador)
                    </label>
                </div>
            </div>
        </div>

        <!-- Cliente, Factura y CPIC -->
        <div class="row mb-3">
            <div class="col-md-4">
                <label for="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cliente">Cliente: <span class="text-danger">*</span></label>
                <select id="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cliente" 
                        class="form-control" required
                        onchange="onClienteChange(${numeroLiquidacion}, ${numeroSubTramo}, '${tipoOperacion}')">
                    <option value="">Seleccione un cliente</option>
                    ${clientes.map(c =>
                `<option value="${c.idCliente}">${c.nombre}</option>`
            ).join('')}
                </select>
            </div>
            <div class="col-md-4">
                <label for="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_factura">Factura: <span class="text-danger">*</span></label>
                <select id="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_factura" class="form-control" required>
                    <option value="">Primero seleccione un cliente</option>
                </select>
                <small class="text-muted">Solo facturas del cliente seleccionado</small>
            </div>
            <div class="col-md-4">
                <label for="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cpic">CPIC:</label>
                <select id="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cpic" class="form-control" disabled>
                    <option value="">No requerido para nacional</option>
                    ${cpics.map(c => `<option value="${c.idCPIC}">${c.numeroCPIC}</option>`).join('')}
                </select>
            </div>
        </div>

        <!-- Productos de la operación -->
        <div class="mb-3">
            <label>Productos ${tipoOperacion === 'carga' ? 'a Cargar' : 'a Descargar'}: <span class="text-danger">*</span></label>
            <div class="alert alert-info" id="info_productos_${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}">
                <i class="fas fa-info-circle"></i> 
                <strong>Seleccione primero un cliente</strong> para ver sus productos disponibles.
            </div>
            <div id="productos_${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}">
                <!-- Los productos se agregarán dinámicamente -->
            </div>
            <button type="button" class="btn btn-sm btn-outline-success" 
                    onclick="agregarProductoAOperacionMejorada(${numeroLiquidacion}, ${numeroSubTramo}, '${tipoOperacion}')">
                <i class="fas fa-plus"></i> Agregar Producto
            </button>
        </div>
    `;
        }
    


        // ========================================
        // 6. NUEVA FUNCIÓN: Toggle mejorado para cliente
        // ========================================
        function toggleClienteTipoOperacionMejorada(numeroLiquidacion, numeroSubTramo, tipoOperacion) {
            const esInternacional = $(`input[name="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_tipo"]:checked`).val() === '1';
            const cpicSelect = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cpic`);
            const facturaSelect = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_factura`);

            if (esInternacional) {
                cpicSelect.prop('disabled', false).prop('required', true);
                cpicSelect.find('option:first').text('Seleccione un CPIC');

                facturaSelect.prop('disabled', true).prop('required', false).val('');
                facturaSelect.empty().append('<option value="">No requerido para internacional</option>');
            } else {
                cpicSelect.prop('disabled', true).prop('required', false).val('');
                cpicSelect.find('option:first').text('No requerido para nacional');

                facturaSelect.prop('disabled', false).prop('required', true);

                // Restaurar facturas filtradas por cliente
                const idCliente = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cliente`).val();
                if (idCliente) {
                    filtrarFacturasPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, idCliente);
                } else {
                    facturaSelect.empty().append('<option value="">Primero seleccione un cliente</option>');
                }
            }
        }

        // ========================================
        // 7. NUEVA FUNCIÓN: Agregar producto mejorado
        // ========================================
        function agregarProductoAOperacionMejorada(numeroLiquidacion, numeroSubTramo, tipoOperacion) {
            const idCliente = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cliente`).val();

            if (!idCliente) {
                alert('Debe seleccionar primero un cliente para ver sus productos disponibles.');
                return;
            }

            const productosContainer = $(`#productos_${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}`);
            const numeroProducto = productosContainer.children().length + 1;

            // Filtrar productos del cliente seleccionado
            const productosDelCliente = productos.filter(p => p.idCliente == idCliente);

            if (productosDelCliente.length === 0) {
                alert('Este cliente no tiene productos registrados.');
                return;
            }

            const productoHtml = `
        <div class="producto-row" id="producto_${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_${numeroProducto}">
            <div class="row">
                <div class="col-md-6">
                    <select class="form-control form-control-sm producto-select" required 
                            onchange="onProductoChange(${numeroLiquidacion}, ${numeroSubTramo}, '${tipoOperacion}')">
                        <option value="">Seleccione un producto</option>
                        ${productosDelCliente.map(p =>
                `<option value="${p.idProducto}">${p.nombre}</option>`
            ).join('')}
                    </select>
                </div>
                <div class="col-md-4">
                    <input type="number" class="form-control form-control-sm producto-cantidad" 
                           placeholder="Cantidad bolsas" min="1" required
                           onchange="onProductoChange(${numeroLiquidacion}, ${numeroSubTramo}, '${tipoOperacion}')">
                </div>
                <div class="col-md-2">
                    <button type="button" class="btn btn-sm btn-outline-danger" 
                            onclick="eliminarProductoOperacion(${numeroLiquidacion}, ${numeroSubTramo}, '${tipoOperacion}', ${numeroProducto})">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
            </div>
        </div>
    `;

            productosContainer.append(productoHtml);

            // Ocultar mensaje informativo
            $(`#info_productos_${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}`).hide();
        }

        function inicializarSelect2OperacionMejorada(numeroLiquidacion, numeroSubTramo, tipoOperacion) {
            $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cliente`).select2({
                placeholder: "Buscar cliente...",
                allowClear: true,
                width: '100%'
            });

            $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_factura`).select2({
                placeholder: "Buscar factura...",
                allowClear: true,
                width: '100%'
            });

            $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cpic`).select2({
                placeholder: "Buscar CPIC...",
                allowClear: true,
                width: '100%'
            });

            toggleClienteTipoOperacionMejorada(numeroLiquidacion, numeroSubTramo, tipoOperacion);
        }

        function toggleClienteTipoOperacion(numeroLiquidacion, numeroSubTramo, tipoOperacion) {
            const esInternacional = $(`input[name="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_tipo"]:checked`).val() === '1';
            const cpicSelect = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cpic`);
            const facturaSelect = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_factura`);

            if (esInternacional) {
                cpicSelect.prop('disabled', false).prop('required', true);
                cpicSelect.find('option:first').text('Seleccione un CPIC');

                facturaSelect.prop('disabled', true).prop('required', false).val('');
                facturaSelect.find('option:first').text('No requerido para internacional');
            } else {
                cpicSelect.prop('disabled', true).prop('required', false).val('');
                cpicSelect.find('option:first').text('No requerido para nacional');

                facturaSelect.prop('disabled', false).prop('required', true);
                facturaSelect.find('option:first').text('Seleccione una factura');
            }
        }

        function agregarProductoAOperacion(numeroLiquidacion, numeroSubTramo, tipoOperacion) {
            const productosContainer = $(`#productos_${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}`);
            const numeroProducto = productosContainer.children().length + 1;

            const productoHtml = `
        <div class="producto-row" id="producto_${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_${numeroProducto}">
            <div class="row">
                <div class="col-md-6">
                    <select class="form-control form-control-sm producto-select" required 
                            onchange="onProductoChange(${numeroLiquidacion}, ${numeroSubTramo}, '${tipoOperacion}')">
                        <option value="">Seleccione un producto</option>
                        ${productos.map(p => `<option value="${p.idProducto}">${p.nombre}</option>`).join('')}
                    </select>
                </div>
                <div class="col-md-4">
                    <input type="number" class="form-control form-control-sm producto-cantidad" 
                           placeholder="Cantidad bolsas" min="1" required
                           onchange="onProductoChange(${numeroLiquidacion}, ${numeroSubTramo}, '${tipoOperacion}')">
                </div>
                <div class="col-md-2">
                    <button type="button" class="btn btn-sm btn-outline-danger" 
                            onclick="eliminarProductoOperacion(${numeroLiquidacion}, ${numeroSubTramo}, '${tipoOperacion}', ${numeroProducto})">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
            </div>
        </div>
    `;

            productosContainer.append(productoHtml);
        }

        // ========================================
        // FUNCIONES DE ELIMINACIÓN
        // ========================================
        function eliminarProductoOperacion(numeroLiquidacion, numeroSubTramo, tipoOperacion, numeroProducto) {
            $(`#producto_${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_${numeroProducto}`).remove();

            if (tipoOperacion === 'carga') {
                setTimeout(() => {
                    actualizarProductosCargadosGlobal();
                }, 100);
            }
        }

        function eliminarSubTramo(numeroLiquidacion, numeroSubTramo) {
            if (confirm('¿Está seguro de eliminar este sub-tramo?')) {
                $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}`).remove();
                recalcularContinuidadRutas(numeroLiquidacion);
            }
        }

        function recalcularContinuidadRutas(numeroLiquidacion) {
            $(`#subtramos_liquidacion_${numeroLiquidacion} .subtramo-card`).each(function (index) {
                const subTramoId = $(this).attr('id').split('_');
                const numSubTramo = subTramoId[subTramoId.length - 1];

                if (index > 0) {
                    const subTramoAnterior = $(`#subtramos_liquidacion_${numeroLiquidacion} .subtramo-card`).eq(index - 1);
                    const subTramoAnteriorId = subTramoAnterior.attr('id').split('_');
                    const numSubTramoAnterior = subTramoAnteriorId[subTramoAnteriorId.length - 1];

                    const destinoAnterior = $(`#subtramo_${numeroLiquidacion}_${numSubTramoAnterior}_destino`).val();

                    if (destinoAnterior) {
                        $(`#subtramo_${numeroLiquidacion}_${numSubTramo}_origen`).val(destinoAnterior);
                        actualizarRutaVisual(numeroLiquidacion, numSubTramo);
                    }
                }
            });
        }

        function toggleManifiestoSubTramo(numeroLiquidacion, numeroSubTramo) {
            const cruzaFrontera = $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_cruza_frontera`).is(':checked');
            const campoManifiesto = $(`#campo_manifiesto_subtramo_${numeroLiquidacion}_${numeroSubTramo}`);

            if (cruzaFrontera) {
                campoManifiesto.show();
                $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_manifiesto`).prop('required', true);
            } else {
                campoManifiesto.hide();
                $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_manifiesto`).val('').prop('required', false);
            }
        }

        // ========================================
        // FUNCIONES DE VALIDACIÓN Y RECOPILACIÓN
        // ========================================
        function validarLiquidacionesYContinuar() {
            const liquidaciones = recopilarDatosLiquidacionesMejoradas();

            if (liquidaciones.length === 0) {
                alert('Debe agregar al menos una liquidación.');
                return;
            }

            // Validaciones básicas...
            $('#hiddenLiquidacionesData').val(JSON.stringify(liquidaciones));
            showNextTab('liquidacion');
        }

        function recopilarDatosLiquidacionesMejoradas() {
            const liquidaciones = [];

            $('.liquidacion-card').each(function (index) {
                const numeroLiquidacion = $(this).attr('id').split('_')[1];

                const liquidacion = {
                    numeroLiquidacion: 1,
                    observaciones: $(`#liquidacion_${numeroLiquidacion}_obs`).val(),
                    subTramos: []
                };

                // Recopilar sub-tramos
                $(`#subtramos_liquidacion_${numeroLiquidacion} .subtramo-card`).each(function () {
                    const subTramoId = $(this).attr('id').split('_');
                    const numeroSubTramo = subTramoId[subTramoId.length - 1];
                    const tipoOperacion = $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_operacion`).val();

                    const subTramo = {
                        numeroSubTramo: numeroSubTramo,
                        origen: $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_origen`).val(),
                        destino: $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_destino`).val(),
                        tipoOperacion: tipoOperacion,
                        observaciones: $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_obs`).val(),
                        guiaTransportista: $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_guia_transportista`).val(),
                        guiaCliente: $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_guia_cliente`).val(),
                        cruzaFrontera: $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_cruza_frontera`).is(':checked'),
                        manifiesto: $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_cruza_frontera`).is(':checked') ?
                            $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_manifiesto`).val() : null
                    };

                    // Datos específicos según tipo de operación
                    if (tipoOperacion === 'SOLO_CARGA' || tipoOperacion === 'DESCARGA_Y_CARGA') {
                        subTramo.operacionCarga = recopilarDatosOperacion(numeroLiquidacion, numeroSubTramo, 'carga');
                    }

                    if (tipoOperacion === 'SOLO_DESCARGA' || tipoOperacion === 'DESCARGA_Y_CARGA') {
                        subTramo.operacionDescarga = recopilarDatosOperacion(numeroLiquidacion, numeroSubTramo, 'descarga');
                    }

                    if (tipoOperacion === 'PARADA_OPERATIVA') {
                        subTramo.parada = {
                            motivo: $(`#parada_${numeroLiquidacion}_${numeroSubTramo}_motivo`).val(),
                            duracion: $(`#parada_${numeroLiquidacion}_${numeroSubTramo}_duracion`).val()
                        };
                    }

                    liquidacion.subTramos.push(subTramo);
                });

                liquidaciones.push(liquidacion);
            });

            return liquidaciones;
        }

        function recopilarDatosOperacion(numeroLiquidacion, numeroSubTramo, tipoOperacion) {
            const esInternacional = $(`input[name="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_tipo"]:checked`).val() === '1';

            const operacion = {
                activa: true,
                idCliente: $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cliente`).val() || null,
                idCPIC: esInternacional ? $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cpic`).val() || null : null,
                idFactura: !esInternacional ? $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_factura`).val() || null : null,
                esInternacional: esInternacional,
                productos: []
            };

            // Recopilar productos
            $(`#productos_${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo} .producto-row`).each(function () {
                const idProducto = $(this).find('select').val();
                const cantidad = $(this).find('input[type="number"]').val();

                if (idProducto && cantidad) {
                    operacion.productos.push({
                        idProducto: parseInt(idProducto),
                        cantidad: parseInt(cantidad)
                    });
                }
            });

            return operacion;
        }

        // ========================================
        // FUNCIONES PARA RESUMEN FINANCIERO
        // ========================================
        function agregarFilaIngreso() {
            contadorIngresosAdicionales++;
            const numeroFila = 4 + contadorIngresosAdicionales;

            const nuevaFila = `
        <tr id="ingreso_adicional_${contadorIngresosAdicionales}">
            <td>${numeroFila}</td>
            <td>
                <input type="text" class="form-control" name="txtIngresoCategoria_${contadorIngresosAdicionales}" 
                       placeholder="Categoría de ingreso">
            </td>
            <td>
                <input type="text" class="form-control" name="txtDescIngreso_${contadorIngresosAdicionales}" 
                       placeholder="Descripción">
            </td>
            <td>
                <input type="number" class="form-control ingreso-soles" 
                       name="txtIngresoSoles_${contadorIngresosAdicionales}" 
                       placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()">
            </td>
            <td>
                <input type="number" class="form-control ingreso-dolares" 
                       name="txtIngresoDolares_${contadorIngresosAdicionales}" 
                       placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()">
            </td>
            <td>
                <button type="button" class="btn btn-sm btn-danger" 
                        onclick="eliminarFilaIngreso(${contadorIngresosAdicionales})">
                    <i class="fas fa-trash"></i>
                </button>
            </td>
        </tr>
    `;

            $('#ingresosAdicionalesBody').append(nuevaFila);
            calcularTotales();
        }

        function eliminarFilaIngreso(id) {
            $(`#ingreso_adicional_${id}`).remove();
            calcularTotales();
        }

        function agregarFila() {
            contadorGastosAdicionales++;
            const numeroFila = 8 + contadorGastosAdicionales;

            const nuevaFila = `
        <tr id="gasto_adicional_${contadorGastosAdicionales}">
            <td>${numeroFila}</td>
            <td>
                <input type="text" class="form-control" name="txtGastoCategoria_${contadorGastosAdicionales}" 
                       placeholder="Categoría de gasto">
            </td>
            <td>
                <input type="text" class="form-control" name="txtDescGasto_${contadorGastosAdicionales}" 
                       placeholder="Descripción">
            </td>
            <td>
                <input type="number" class="form-control gasto-soles" 
                       name="txtGastoSoles_${contadorGastosAdicionales}" 
                       placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()">
            </td>
            <td>
                <input type="number" class="form-control gasto-dolares" 
                       name="txtGastoDolares_${contadorGastosAdicionales}" 
                       placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()">
            </td>
            <td>
                <button type="button" class="btn btn-sm btn-danger" 
                        onclick="eliminarFilaGasto(${contadorGastosAdicionales})">
                    <i class="fas fa-trash"></i>
                </button>
            </td>
        </tr>
    `;

            $('#gastosAdicionalesBody').append(nuevaFila);
            calcularTotales();
        }

        function eliminarFilaGasto(id) {
            $(`#gasto_adicional_${id}`).remove();
            calcularTotales();
        }

        function calcularTotales() {
            let totalIngresosSoles = 0;
            let totalIngresosDolares = 0;

            $('.ingreso-soles').each(function () {
                const valor = parseFloat($(this).val()) || 0;
                totalIngresosSoles += valor;
            });

            $('.ingreso-dolares').each(function () {
                const valor = parseFloat($(this).val()) || 0;
                totalIngresosDolares += valor;
            });

            let totalGastosSoles = 0;
            let totalGastosDolares = 0;

            $('.gasto-soles').each(function () {
                const valor = parseFloat($(this).val()) || 0;
                totalGastosSoles += valor;
            });

            $('.gasto-dolares').each(function () {
                const valor = parseFloat($(this).val()) || 0;
                totalGastosDolares += valor;
            });

            const diferenciaSoles = totalIngresosSoles - totalGastosSoles;
            const diferenciaDolares = totalIngresosDolares - totalGastosDolares;

            $('#totalIngresosSoles').text(totalIngresosSoles.toFixed(2));
            $('#totalIngresosDolares').text(totalIngresosDolares.toFixed(2));
            $('#totalGastosSoles').text(totalGastosSoles.toFixed(2));
            $('#totalGastosDolares').text(totalGastosDolares.toFixed(2));
            $('#diferenciaSaldoSoles').text(diferenciaSoles.toFixed(2));
            $('#diferenciaSaldoDolares').text(diferenciaDolares.toFixed(2));

            $('#diferenciaSaldoSoles').css('color', diferenciaSoles < 0 ? 'red' : 'green');
            $('#diferenciaSaldoDolares').css('color', diferenciaDolares < 0 ? 'red' : 'green');

            guardarGastosAdicionalesJSON();
            guardarIngresosAdicionalesJSON();
        }

        function guardarGastosAdicionalesJSON() {
            const gastosAdicionales = [];

            $('#gastosAdicionalesBody tr').each(function () {
                const id = $(this).attr('id').split('_')[2];
                const categoria = $(`input[name="txtGastoCategoria_${id}"]`).val();
                const descripcion = $(`input[name="txtDescGasto_${id}"]`).val();
                const soles = parseFloat($(`input[name="txtGastoSoles_${id}"]`).val()) || 0;
                const dolares = parseFloat($(`input[name="txtGastoDolares_${id}"]`).val()) || 0;

                if (categoria || descripcion || soles > 0 || dolares > 0) {
                    gastosAdicionales.push({
                        categoria: categoria,
                        descripcion: descripcion,
                        soles: soles,
                        dolares: dolares
                    });
                }
            });

            $('#hiddenGastosAdicionales').val(JSON.stringify(gastosAdicionales));
        }

        function guardarIngresosAdicionalesJSON() {
            const ingresosAdicionales = [];

            $('#ingresosAdicionalesBody tr').each(function () {
                const id = $(this).attr('id').split('_')[2];
                const categoria = $(`input[name="txtIngresoCategoria_${id}"]`).val();
                const descripcion = $(`input[name="txtDescIngreso_${id}"]`).val();
                const soles = parseFloat($(`input[name="txtIngresoSoles_${id}"]`).val()) || 0;
                const dolares = parseFloat($(`input[name="txtIngresoDolares_${id}"]`).val()) || 0;

                if (categoria || descripcion || soles > 0 || dolares > 0) {
                    ingresosAdicionales.push({
                        categoria: categoria,
                        descripcion: descripcion,
                        soles: soles,
                        dolares: dolares
                    });
                }
            });

            $('#hiddenIngresosAdicionales').val(JSON.stringify(ingresosAdicionales));
        }

        function testearAutoLlenado() {
            console.log("🧪 PROBANDO AUTO-LLENADO MANUALMENTE");

            // Simular que tenemos productos en Sub-Tramo 1
            const productosTest = buscarProductosCargadosEnSubTramosAnteriores(1, 2);
            console.log("Productos encontrados:", productosTest);

            if (productosTest.length > 0) {
                autoLlenarProductosDescarga(1, 2);
            } else {
                console.log("❌ No se encontraron productos para transferir");
            }
        }

        // Hacer disponible para testing
        window.testearAutoLlenado = testearAutoLlenado;

        // ========================================
        // INICIALIZACIÓN
        // ========================================
        $(document).ready(function () {
            // Inicializar Select2 para campos principales
            $('.conductor-select').select2({
                placeholder: "Buscar conductor...",
                allowClear: true,
                width: '100%'
            });

            $('.tracto-select').select2({
                placeholder: "Buscar placa tracto...",
                allowClear: true,
                width: '100%'
            });

            $('.carreta-select').select2({
                placeholder: "Buscar placa carreta...",
                allowClear: true,
                width: '100%'
            });

            // Solo agregar UNA liquidación automáticamente
            agregarNuevaLiquidacion();

            // Calcular totales iniciales
            calcularTotales();

            // Función de debug disponible globalmente
            window.debugProductos = debugProductos;

            console.log("🚀 Sistema de auto-llenado inicializado correctamente");
        });
    </script>
</asp:Content>
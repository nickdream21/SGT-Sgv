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
                    <asp:HiddenField ID="hfValidationError" runat="server" ClientIDMode="Static" />
                    <asp:HiddenField ID="hfNumeroOrdenViaje" runat="server" ClientIDMode="Static" />

                    <!-- Número de orden manual -->
                    <div class="row mb-3">
                        <div class="col-md-6 form-group">
                            <label for="txtNumeroOrdenViaje">N° Orden Viaje: <span class="text-danger">*</span></label>
                            <div class="input-group">
                                <input type="text"
                                    id="txtNumeroOrdenViaje"
                                    runat="server"
                                    clientidmode="Static"
                                    class="form-control"
                                    placeholder="Ej: 280730"
                                    required
                                    maxlength="6"
                                    minlength="6"
                                    pattern="[0-9]{6}"
                                    style="font-weight: bold; font-size: 1.2em; color: #0066cc; text-align: center;"
                                    onblur="validarNumeroOrden()"
                                    onkeyup="formatearNumeroOrden(this)"
                                    onkeypress="return soloNumeros(event)">
                                <button type="button" class="btn btn-outline-secondary"
                                    onclick="mostrarSugerenciasNumero()"
                                    title="Ver sugerencias de números">
                                    <i class="fas fa-lightbulb"></i>
                                </button>
                            </div>
                            <small class="form-text text-muted">Ingrese exactamente 6 números  
           
                                <button type="button" class="btn btn-link btn-sm p-0" onclick="mostrarSugerenciasNumero()">
                                    (ver sugerencias)
           
                                </button>
                            </small>
                            <div id="mensajeValidacionOrden" class="mt-1"></div>
                        </div>
                        <div class="col-md-6 form-group">
                            <label for="ddlConductor">Conductor: <span class="text-danger">*</span></label>
                            <asp:DropDownList ID="ddlConductor" runat="server" ClientIDMode="Static" CssClass="form-control conductor-select" required>
                                <asp:ListItem Text="Seleccione un conductor" Value="" />
                            </asp:DropDownList>
                        </div>
                    </div>

                    <!-- Segunda fila: Fechas y horas -->
                    <div class="row mb-3">
                        <div class="col-md-3 form-group">
                            <label for="txtFechaSalida">Fecha de Salida:</label>
                            <input type="date" id="txtFechaSalida" runat="server" clientidmode="Static" class="form-control" required />
                        </div>
                        <div class="col-md-3 form-group">
                            <label for="txtHoraSalida">Hora de Salida:</label>
                            <input type="time" id="txtHoraSalida" runat="server" clientidmode="Static" class="form-control" required />
                        </div>
                        <div class="col-md-3 form-group">
                            <label for="txtFechaLlegada">Fecha de Llegada:</label>
                            <input type="date" id="txtFechaLlegada" runat="server" clientidmode="Static" class="form-control" required />
                        </div>
                        <div class="col-md-3 form-group">
                            <label for="txtHoraLlegada">Hora de Llegada:</label>
                            <input type="time" id="txtHoraLlegada" runat="server" clientidmode="Static" class="form-control" required />
                        </div>
                    </div>

                    <!-- Tercera fila: Vehículos -->
                    <div class="row mb-3">
                        <div class="col-md-6 form-group">
                            <label for="ddlPlacaTracto">Placa Tracto:</label>
                            <asp:DropDownList ID="ddlPlacaTracto" runat="server" ClientIDMode="Static" CssClass="form-control tracto-select" required>
                                <asp:ListItem Text="Seleccione una placa" Value="" />
                            </asp:DropDownList>
                        </div>
                        <div class="col-md-6 form-group">
                            <label for="ddlPlacaCarreta">Placa Carreta:</label>
                            <asp:DropDownList ID="ddlPlacaCarreta" runat="server" ClientIDMode="Static" CssClass="form-control carreta-select" required>
                                <asp:ListItem Text="Seleccione una placa" Value="" />
                            </asp:DropDownList>
                        </div>
                    </div>

                    <!-- Cuarta fila: Observaciones -->
                    <div class="row mb-4">
                        <div class="col-md-12 form-group">
                            <label for="txtObservaciones">Observaciones Generales:</label>
                            <textarea id="txtObservaciones" runat="server" clientidmode="Static" class="form-control" rows="3" placeholder="Observaciones generales del viaje"></textarea>
                        </div>
                    </div>

                    <!-- Botón Siguiente -->
                    <div class="form-group text-end">
                        <button type="button" class="btn btn-primary px-4 py-2" onclick="showNextTab('segmentos')">Siguiente: Agregar Trazabilidad</button>

                    </div>

                    <!-- Etiqueta para mostrar errores -->
                    <div class="form-group">
                        <asp:Label ID="lblErrores" runat="server" ClientIDMode="Static" CssClass="text-danger" EnableViewState="false"></asp:Label>
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

            <!-- Pestaña 3: Resumen Financiero - CON PEAJES MODIFICADOS -->
            <div class="tab-pane fade" id="liquidacion" role="tabpanel" aria-labelledby="liquidacion-tab">
                <h3 class="tab-header">Resumen Financiero</h3>
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

                    <!-- Tabla de Gastos CON TODAS LAS CATEGORÍAS DETALLADAS -->
                    <!-- Tabla de Gastos MODIFICADA - Solo algunos con modal -->
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
                            <!-- 1. PEAJES - CON MODAL -->
                            <tr>
                                <td>1</td>
                                <td>
                                    <div class="d-flex align-items-center">
                                        <span>Peajes</span>
                                        <button type="button" class="btn btn-sm btn-outline-primary ml-2"
                                            onclick="abrirModalPeajes()" title="Gestionar peajes detallados">
                                            <i class="fas fa-list-ul"></i>Detallar
                                        </button>
                                    </div>
                                </td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescPeajes"
                                        id="txtDescPeajes" readonly
                                        placeholder="Descripción automática basada en registros">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtPeajesSoles"
                                        id="txtPeajesSoles" readonly
                                        placeholder="Total automático" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtPeajesDolares"
                                        id="txtPeajesDolares" readonly
                                        placeholder="Total automático" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <span class="badge badge-info" id="contadorPeajes">0</span>
                                </td>
                            </tr>

                            <!-- 2. ALIMENTACIÓN - SIN MODAL (SIMPLE) -->
                            <tr>
                                <td>2</td>
                                <td>Alimentación</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescAlimentacion"
                                        id="txtDescAlimentacion" placeholder="Descripción de alimentación">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtAlimentacionSoles"
                                        id="txtAlimentacionSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtAlimentacionDolares"
                                        id="txtAlimentacionDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <span class="text-muted">Manual</span>
                                </td>
                            </tr>

                            <!-- 3. APOYO-SEGURIDAD - SIN MODAL (SIMPLE) -->
                            <tr>
                                <td>3</td>
                                <td>Apoyo-Seguridad</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescApoyoSeguridad"
                                        id="txtDescApoyoSeguridad" placeholder="Descripción de apoyo-seguridad">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtApoyoSeguridadSoles"
                                        id="txtApoyoSeguridadSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtApoyoSeguridadDolares"
                                        id="txtApoyoSeguridadDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <span class="text-muted">Manual</span>
                                </td>
                            </tr>

                            <!-- 4. REPARACIONES VARIOS - CON MODAL -->
                            <tr>
                                <td>4</td>
                                <td>
                                    <div class="d-flex align-items-center">
                                        <span>Reparaciones Varios</span>
                                        <button type="button" class="btn btn-sm btn-outline-primary ml-2"
                                            onclick="abrirModalReparaciones()" title="Gestionar reparaciones detalladas">
                                            <i class="fas fa-list-ul"></i>Detallar
                                        </button>
                                    </div>
                                </td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescReparaciones"
                                        id="txtDescReparaciones" readonly
                                        placeholder="Descripción automática basada en registros">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtReparacionesSoles"
                                        id="txtReparacionesSoles" readonly
                                        placeholder="Total automático" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtReparacionesDolares"
                                        id="txtReparacionesDolares" readonly
                                        placeholder="Total automático" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <span class="badge badge-info" id="contadorReparaciones">0</span>
                                </td>
                            </tr>

                            <!-- 5. MOVILIDAD - SIN MODAL (SIMPLE) -->
                            <tr>
                                <td>5</td>
                                <td>Movilidad</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescMovilidad"
                                        id="txtDescMovilidad" placeholder="Descripción de movilidad">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtMovilidadSoles"
                                        id="txtMovilidadSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtMovilidadDolares"
                                        id="txtMovilidadDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <span class="text-muted">Manual</span>
                                </td>
                            </tr>

                            <!-- 6. ENCAPADA/DESCENCARPADA - SIN MODAL (SIMPLE) -->
                            <tr>
                                <td>6</td>
                                <td>Encapada/Descencarpada</td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescEncapada"
                                        id="txtDescEncapada" placeholder="Descripción de encapada/descencarpada">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtEncapadaSoles"
                                        id="txtEncapadaSoles" placeholder="Soles" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtEncapadaDolares"
                                        id="txtEncapadaDolares" placeholder="Dólares" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <span class="text-muted">Manual</span>
                                </td>
                            </tr>

                            <!-- 7. HOSPEDAJE - CON MODAL -->
                            <tr>
                                <td>7</td>
                                <td>
                                    <div class="d-flex align-items-center">
                                        <span>Hospedaje</span>
                                        <button type="button" class="btn btn-sm btn-outline-primary ml-2"
                                            onclick="abrirModalHospedaje()" title="Gestionar hospedaje detallado">
                                            <i class="fas fa-list-ul"></i>Detallar
                                        </button>
                                    </div>
                                </td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescHospedaje"
                                        id="txtDescHospedaje" readonly
                                        placeholder="Descripción automática basada en registros">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtHospedajeSoles"
                                        id="txtHospedajeSoles" readonly
                                        placeholder="Total automático" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtHospedajeDolares"
                                        id="txtHospedajeDolares" readonly
                                        placeholder="Total automático" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <span class="badge badge-info" id="contadorHospedaje">0</span>
                                </td>
                            </tr>

                            <!-- 8. COMBUSTIBLE - CON MODAL -->
                            <tr>
                                <td>8</td>
                                <td>
                                    <div class="d-flex align-items-center">
                                        <span>Combustible</span>
                                        <button type="button" class="btn btn-sm btn-outline-primary ml-2"
                                            onclick="abrirModalCombustible()" title="Gestionar combustible detallado">
                                            <i class="fas fa-list-ul"></i>Detallar
                                        </button>
                                    </div>
                                </td>
                                <td>
                                    <input type="text" class="form-control" name="txtDescCombustible"
                                        id="txtDescCombustible" readonly
                                        placeholder="Descripción automática basada en registros">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-soles" name="txtCombustibleSoles"
                                        id="txtCombustibleSoles" readonly
                                        placeholder="Total automático" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <input type="number" class="form-control gasto-dolares" name="txtCombustibleDolares"
                                        id="txtCombustibleDolares" readonly
                                        placeholder="Total automático" min="0" step="0.01" onchange="calcularTotales()">
                                </td>
                                <td>
                                    <span class="badge badge-info" id="contadorCombustible">0</span>
                                </td>
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
                    <!-- Resumen MODIFICADO con Descuento y Reintegro -->
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
                                    <td><strong>Total Ingresos</strong></td>
                                    <td id="totalIngresosSoles">0.00</td>
                                    <td id="totalIngresosDolares">0.00</td>
                                </tr>
                                <tr>
                                    <td><strong>Total Gastos</strong></td>
                                    <td id="totalGastosSoles">0.00</td>
                                    <td id="totalGastosDolares">0.00</td>
                                </tr>
                                <tr class="table-warning">
                                    <td>
                                        <strong>Descuento</strong>
                                        <br>
                                        <small class="text-muted">Monto a descontar al chofer</small>
                                    </td>
                                    <td>
                                        <input type="number" class="form-control descuento-soles"
                                            name="txtDescuentoSoles" id="txtDescuentoSoles"
                                            placeholder="0.00" min="0" step="0.01"
                                            onchange="calcularTotales()" style="background-color: #fff3cd;">
                                    </td>
                                    <td>
                                        <input type="number" class="form-control descuento-dolares"
                                            name="txtDescuentoDolares" id="txtDescuentoDolares"
                                            placeholder="0.00" min="0" step="0.01"
                                            onchange="calcularTotales()" style="background-color: #fff3cd;">
                                    </td>
                                </tr>
                                <tr class="table-info">
                                    <td>
                                        <strong>Reintegro</strong>
                                        <br>
                                        <small class="text-muted">Monto a reintegrar al chofer</small>
                                    </td>
                                    <td>
                                        <input type="number" class="form-control reintegro-soles"
                                            name="txtReintegroSoles" id="txtReintegroSoles"
                                            placeholder="0.00" min="0" step="0.01"
                                            onchange="calcularTotales()" style="background-color: #d1ecf1;">
                                    </td>
                                    <td>
                                        <input type="number" class="form-control reintegro-dolares"
                                            name="txtReintegroDolares" id="txtReintegroDolares"
                                            placeholder="0.00" min="0" step="0.01"
                                            onchange="calcularTotales()" style="background-color: #d1ecf1;">
                                    </td>
                                </tr>
                                <tr class="table-success">
                                    <td><strong>Diferencia de Saldo Final</strong></td>
                                    <td id="diferenciaSaldoSoles" style="font-weight: bold; font-size: 1.1em;">0.00</td>
                                    <td id="diferenciaSaldoDolares" style="font-weight: bold; font-size: 1.1em;">0.00</td>
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
                        <asp:Button ID="btnGuardar" runat="server" ClientIDMode="Static" Text="Guardar Orden de Viaje" CssClass="btn btn-success"
                            OnClick="btnGuardar_Click" OnClientClick="return validarFormularioCompleto();" />
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- MODAL PARA GESTIÓN DE PEAJES -->
    <div class="modal fade" id="modalPeajes" tabindex="-1" role="dialog" aria-labelledby="modalPeajesLabel" aria-hidden="true">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content">
                <div class="modal-header bg-primary text-white">
                    <h5 class="modal-title" id="modalPeajesLabel">
                        <i class="fas fa-road"></i>Gestión Detallada de Peajes
                    </h5>
                    <button type="button" class="close text-white" data-dismiss="modal" aria-label="Close">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>

                <div class="modal-body">
                    <!-- Formulario para agregar nuevo peaje -->
                    <div class="card mb-3">
                        <div class="card-header bg-light">
                            <h6 class="mb-0"><i class="fas fa-plus-circle"></i>Agregar Nuevo Peaje</h6>
                        </div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoPeajeEstacion">Estación/Peaje: <span class="text-danger">*</span></label>
                                    <select class="form-control" id="nuevoPeajeEstacion" style="width: 100%;">
                                        <option value="">Seleccione una estación de peaje...</option>
                                    </select>
                                </div>
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoPeajeFecha">Fecha: <span class="text-danger">*</span></label>
                                    <input type="date" class="form-control" id="nuevoPeajeFecha" required>
                                </div>
                            </div>

                            <div class="row">
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoPeajeComprobante">N° Comprobante:</label>
                                    <input type="text" class="form-control" id="nuevoPeajeComprobante"
                                        placeholder="Ej: 001-123456">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoPeajeSoles">Monto Soles (S/):</label>
                                    <input type="number" class="form-control" id="nuevoPeajeSoles"
                                        placeholder="0.00" min="0" step="0.01">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoPeajeDolares">Monto Dólares ($):</label>
                                    <input type="number" class="form-control" id="nuevoPeajeDolares"
                                        placeholder="0.00" min="0" step="0.01">
                                </div>
                            </div>

                            <div class="row">
                                <div class="col-md-12 mb-3">
                                    <label for="nuevoPeajeObservaciones">Observaciones:</label>
                                    <input type="text" class="form-control" id="nuevoPeajeObservaciones"
                                        placeholder="Observaciones adicionales">
                                </div>
                            </div>

                            <div class="text-right">
                                <button type="button" class="btn btn-success" onclick="agregarPeaje()">
                                    <i class="fas fa-plus"></i>Agregar Peaje
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Lista de peajes registrados -->
                    <div class="card">
                        <div class="card-header bg-light d-flex justify-content-between align-items-center">
                            <h6 class="mb-0"><i class="fas fa-list"></i>Peajes Registrados</h6>
                            <span class="badge badge-primary" id="totalPeajesRegistrados">0 peajes</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-striped table-hover mb-0">
                                    <thead class="thead-light">
                                        <tr>
                                            <th>Estación</th>
                                            <th>Fecha</th>
                                            <th>N° Comprobante</th>
                                            <th>Soles (S/)</th>
                                            <th>Dólares ($)</th>
                                            <th>Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaPeajesBody">
                                        <!-- Los peajes se cargarán aquí dinámicamente -->
                                    </tbody>
                                </table>
                            </div>

                            <!-- Mensaje cuando no hay peajes -->
                            <div id="noPeajesMessage" class="text-center py-4 text-muted" style="display: none;">
                                <i class="fas fa-road fa-3x mb-3"></i>
                                <p>No hay peajes registrados aún.</p>
                            </div>
                        </div>

                        <!-- Totales -->
                        <div class="card-footer bg-light">
                            <div class="row">
                                <div class="col-md-6">
                                    <strong>Total en Soles: <span id="totalPeajesSoles" class="text-success">S/ 0.00</span></strong>
                                </div>
                                <div class="col-md-6 text-right">
                                    <strong>Total en Dólares: <span id="totalPeajesDolares" class="text-success">$ 0.00</span></strong>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">
                        <i class="fas fa-times"></i>Cerrar
                    </button>
                    <button type="button" class="btn btn-primary" onclick="aplicarPeajes()">
                        <i class="fas fa-check"></i>Aplicar y Cerrar
                    </button>
                </div>
            </div>
        </div>
    </div>


    <!-- MODAL PARA GESTIÓN DE ALIMENTACIÓN -->
    <div class="modal fade" id="modalAlimentacion" tabindex="-1" role="dialog" aria-labelledby="modalAlimentacionLabel" aria-hidden="true">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content">
                <div class="modal-header bg-primary text-white">
                    <h5 class="modal-title" id="modalAlimentacionLabel">
                        <i class="fas fa-utensils"></i>Gestión Detallada de Alimentación
                    </h5>
                    <button type="button" class="close text-white" data-dismiss="modal" aria-label="Close">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>

                <div class="modal-body">
                    <!-- Formulario para agregar nueva alimentación -->
                    <div class="card mb-3">
                        <div class="card-header bg-light">
                            <h6 class="mb-0"><i class="fas fa-plus-circle"></i>Agregar Nuevo Gasto de Alimentación</h6>
                        </div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoAlimentacionFecha">Fecha Comprobante: <span class="text-danger">*</span></label>
                                    <input type="date" class="form-control" id="nuevoAlimentacionFecha">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoAlimentacionComprobante">N° Comprobante:</label>
                                    <input type="text" class="form-control" id="nuevoAlimentacionComprobante" placeholder="Ej: 001-123456">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoAlimentacionObservaciones">Observaciones:</label>
                                    <input type="text" class="form-control" id="nuevoAlimentacionObservaciones" placeholder="Observaciones adicionales">
                                </div>
                            </div>

                            <div class="row">
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoAlimentacionSoles">Monto Soles (S/):</label>
                                    <input type="number" class="form-control" id="nuevoAlimentacionSoles" placeholder="0.00" min="0" step="0.01">
                                </div>
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoAlimentacionDolares">Monto Dólares ($):</label>
                                    <input type="number" class="form-control" id="nuevoAlimentacionDolares" placeholder="0.00" min="0" step="0.01">
                                </div>
                            </div>

                            <div class="text-right">
                                <button type="button" class="btn btn-success" onclick="agregarAlimentacion()">
                                    <i class="fas fa-plus"></i>Agregar
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Lista de alimentación registrada -->
                    <div class="card">
                        <div class="card-header bg-light d-flex justify-content-between align-items-center">
                            <h6 class="mb-0"><i class="fas fa-list"></i>Registros de Alimentación</h6>
                            <span class="badge badge-primary" id="totalAlimentacionRegistrados">0 registros</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-striped table-hover mb-0">
                                    <thead class="thead-light">
                                        <tr>
                                            <th>Fecha</th>
                                            <th>N° Comprobante</th>
                                            <th>Soles (S/)</th>
                                            <th>Dólares ($)</th>
                                            <th>Observaciones</th>
                                            <th>Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaAlimentacionBody">
                                        <!-- Los registros se cargarán aquí dinámicamente -->
                                    </tbody>
                                </table>
                            </div>

                            <!-- Mensaje cuando no hay registros -->
                            <div id="noAlimentacionMessage" class="text-center py-4 text-muted" style="display: none;">
                                <i class="fas fa-utensils fa-3x mb-3"></i>
                                <p>No hay registros de alimentación aún.</p>
                            </div>
                        </div>

                        <!-- Totales -->
                        <div class="card-footer bg-light">
                            <div class="row">
                                <div class="col-md-6">
                                    <strong>Total en Soles: <span id="totalAlimentacionSoles" class="text-success">S/ 0.00</span></strong>
                                </div>
                                <div class="col-md-6 text-right">
                                    <strong>Total en Dólares: <span id="totalAlimentacionDolares" class="text-success">$ 0.00</span></strong>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">
                        <i class="fas fa-times"></i>Cerrar
                    </button>
                    <button type="button" class="btn btn-primary" onclick="aplicarAlimentacion()">
                        <i class="fas fa-check"></i>Aplicar y Cerrar
                    </button>
                </div>
            </div>
        </div>
    </div>

    <!-- MODAL PARA GESTIÓN DE APOYO-SEGURIDAD -->
    <div class="modal fade" id="modalApoyoSeguridad" tabindex="-1" role="dialog" aria-labelledby="modalApoyoSeguridadLabel" aria-hidden="true">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content">
                <div class="modal-header bg-primary text-white">
                    <h5 class="modal-title" id="modalApoyoSeguridadLabel">
                        <i class="fas fa-shield-alt"></i>Gestión Detallada de Apoyo-Seguridad
                    </h5>
                    <button type="button" class="close text-white" data-dismiss="modal" aria-label="Close">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>

                <div class="modal-body">
                    <!-- Formulario para agregar nuevo apoyo-seguridad -->
                    <div class="card mb-3">
                        <div class="card-header bg-light">
                            <h6 class="mb-0"><i class="fas fa-plus-circle"></i>Agregar Nuevo Gasto de Apoyo-Seguridad</h6>
                        </div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoApoyoSeguridadFecha">Fecha Comprobante: <span class="text-danger">*</span></label>
                                    <input type="date" class="form-control" id="nuevoApoyoSeguridadFecha">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoApoyoSeguridadComprobante">N° Comprobante:</label>
                                    <input type="text" class="form-control" id="nuevoApoyoSeguridadComprobante" placeholder="Ej: 001-123456">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoApoyoSeguridadObservaciones">Observaciones:</label>
                                    <input type="text" class="form-control" id="nuevoApoyoSeguridadObservaciones" placeholder="Observaciones adicionales">
                                </div>
                            </div>

                            <div class="row">
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoApoyoSeguridadSoles">Monto Soles (S/):</label>
                                    <input type="number" class="form-control" id="nuevoApoyoSeguridadSoles" placeholder="0.00" min="0" step="0.01">
                                </div>
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoApoyoSeguridadDolares">Monto Dólares ($):</label>
                                    <input type="number" class="form-control" id="nuevoApoyoSeguridadDolares" placeholder="0.00" min="0" step="0.01">
                                </div>
                            </div>

                            <div class="text-right">
                                <button type="button" class="btn btn-success" onclick="agregarApoyoSeguridad()">
                                    <i class="fas fa-plus"></i>Agregar
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Lista de apoyo-seguridad registrado -->
                    <div class="card">
                        <div class="card-header bg-light d-flex justify-content-between align-items-center">
                            <h6 class="mb-0"><i class="fas fa-list"></i>Registros de Apoyo-Seguridad</h6>
                            <span class="badge badge-primary" id="totalApoyoSeguridadRegistrados">0 registros</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-striped table-hover mb-0">
                                    <thead class="thead-light">
                                        <tr>
                                            <th>Fecha</th>
                                            <th>N° Comprobante</th>
                                            <th>Soles (S/)</th>
                                            <th>Dólares ($)</th>
                                            <th>Observaciones</th>
                                            <th>Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaApoyoSeguridadBody">
                                        <!-- Los registros se cargarán aquí dinámicamente -->
                                    </tbody>
                                </table>
                            </div>

                            <!-- Mensaje cuando no hay registros -->
                            <div id="noApoyoSeguridadMessage" class="text-center py-4 text-muted" style="display: none;">
                                <i class="fas fa-shield-alt fa-3x mb-3"></i>
                                <p>No hay registros de apoyo-seguridad aún.</p>
                            </div>
                        </div>

                        <!-- Totales -->
                        <div class="card-footer bg-light">
                            <div class="row">
                                <div class="col-md-6">
                                    <strong>Total en Soles: <span id="totalApoyoSeguridadSoles" class="text-success">S/ 0.00</span></strong>
                                </div>
                                <div class="col-md-6 text-right">
                                    <strong>Total en Dólares: <span id="totalApoyoSeguridadDolares" class="text-success">$ 0.00</span></strong>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">
                        <i class="fas fa-times"></i>Cerrar
                    </button>
                    <button type="button" class="btn btn-primary" onclick="aplicarApoyoSeguridad()">
                        <i class="fas fa-check"></i>Aplicar y Cerrar
                    </button>
                </div>
            </div>
        </div>
    </div>

    <!-- MODAL PARA GESTIÓN DE REPARACIONES -->
    <div class="modal fade" id="modalReparaciones" tabindex="-1" role="dialog" aria-labelledby="modalReparacionesLabel" aria-hidden="true">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content">
                <div class="modal-header bg-primary text-white">
                    <h5 class="modal-title" id="modalReparacionesLabel">
                        <i class="fas fa-tools"></i>Gestión Detallada de Reparaciones Varios
                    </h5>
                    <button type="button" class="close text-white" data-dismiss="modal" aria-label="Close">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>

                <div class="modal-body">
                    <!-- Formulario para agregar nueva reparación -->
                    <div class="card mb-3">
                        <div class="card-header bg-light">
                            <h6 class="mb-0"><i class="fas fa-plus-circle"></i>Agregar Nuevo Gasto de Reparaciones</h6>
                        </div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoReparacionesFecha">Fecha Comprobante: <span class="text-danger">*</span></label>
                                    <input type="date" class="form-control" id="nuevoReparacionesFecha">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoReparacionesComprobante">N° Comprobante:</label>
                                    <input type="text" class="form-control" id="nuevoReparacionesComprobante" placeholder="Ej: 001-123456">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoReparacionesObservaciones">Observaciones:</label>
                                    <input type="text" class="form-control" id="nuevoReparacionesObservaciones" placeholder="Observaciones adicionales">
                                </div>
                            </div>

                            <div class="row">
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoReparacionesSoles">Monto Soles (S/):</label>
                                    <input type="number" class="form-control" id="nuevoReparacionesSoles" placeholder="0.00" min="0" step="0.01">
                                </div>
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoReparacionesDolares">Monto Dólares ($):</label>
                                    <input type="number" class="form-control" id="nuevoReparacionesDolares" placeholder="0.00" min="0" step="0.01">
                                </div>
                            </div>

                            <div class="text-right">
                                <button type="button" class="btn btn-success" onclick="agregarReparaciones()">
                                    <i class="fas fa-plus"></i>Agregar
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Lista de reparaciones registradas -->
                    <div class="card">
                        <div class="card-header bg-light d-flex justify-content-between align-items-center">
                            <h6 class="mb-0"><i class="fas fa-list"></i>Registros de Reparaciones</h6>
                            <span class="badge badge-primary" id="totalReparacionesRegistrados">0 registros</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-striped table-hover mb-0">
                                    <thead class="thead-light">
                                        <tr>
                                            <th>Fecha</th>
                                            <th>N° Comprobante</th>
                                            <th>Soles (S/)</th>
                                            <th>Dólares ($)</th>
                                            <th>Observaciones</th>
                                            <th>Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaReparacionesBody">
                                        <!-- Los registros se cargarán aquí dinámicamente -->
                                    </tbody>
                                </table>
                            </div>

                            <!-- Mensaje cuando no hay registros -->
                            <div id="noReparacionesMessage" class="text-center py-4 text-muted" style="display: none;">
                                <i class="fas fa-tools fa-3x mb-3"></i>
                                <p>No hay registros de reparaciones aún.</p>
                            </div>
                        </div>

                        <!-- Totales -->
                        <div class="card-footer bg-light">
                            <div class="row">
                                <div class="col-md-6">
                                    <strong>Total en Soles: <span id="totalReparacionesSoles" class="text-success">S/ 0.00</span></strong>
                                </div>
                                <div class="col-md-6 text-right">
                                    <strong>Total en Dólares: <span id="totalReparacionesDolares" class="text-success">$ 0.00</span></strong>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">
                        <i class="fas fa-times"></i>Cerrar
                    </button>
                    <button type="button" class="btn btn-primary" onclick="aplicarReparaciones()">
                        <i class="fas fa-check"></i>Aplicar y Cerrar
                    </button>
                </div>
            </div>
        </div>
    </div>

    <!-- MODAL PARA GESTIÓN DE MOVILIDAD -->
    <div class="modal fade" id="modalMovilidad" tabindex="-1" role="dialog" aria-labelledby="modalMovilidadLabel" aria-hidden="true">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content">
                <div class="modal-header bg-primary text-white">
                    <h5 class="modal-title" id="modalMovilidadLabel">
                        <i class="fas fa-car"></i>Gestión Detallada de Movilidad
                    </h5>
                    <button type="button" class="close text-white" data-dismiss="modal" aria-label="Close">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>

                <div class="modal-body">
                    <!-- Formulario para agregar nueva movilidad -->
                    <div class="card mb-3">
                        <div class="card-header bg-light">
                            <h6 class="mb-0"><i class="fas fa-plus-circle"></i>Agregar Nuevo Gasto de Movilidad</h6>
                        </div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoMovilidadFecha">Fecha Comprobante: <span class="text-danger">*</span></label>
                                    <input type="date" class="form-control" id="nuevoMovilidadFecha">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoMovilidadComprobante">N° Comprobante:</label>
                                    <input type="text" class="form-control" id="nuevoMovilidadComprobante" placeholder="Ej: 001-123456">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoMovilidadObservaciones">Observaciones:</label>
                                    <input type="text" class="form-control" id="nuevoMovilidadObservaciones" placeholder="Observaciones adicionales">
                                </div>
                            </div>

                            <div class="row">
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoMovilidadSoles">Monto Soles (S/):</label>
                                    <input type="number" class="form-control" id="nuevoMovilidadSoles" placeholder="0.00" min="0" step="0.01">
                                </div>
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoMovilidadDolares">Monto Dólares ($):</label>
                                    <input type="number" class="form-control" id="nuevoMovilidadDolares" placeholder="0.00" min="0" step="0.01">
                                </div>
                            </div>

                            <div class="text-right">
                                <button type="button" class="btn btn-success" onclick="agregarMovilidad()">
                                    <i class="fas fa-plus"></i>Agregar
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Lista de movilidad registrada -->
                    <div class="card">
                        <div class="card-header bg-light d-flex justify-content-between align-items-center">
                            <h6 class="mb-0"><i class="fas fa-list"></i>Registros de Movilidad</h6>
                            <span class="badge badge-primary" id="totalMovilidadRegistrados">0 registros</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-striped table-hover mb-0">
                                    <thead class="thead-light">
                                        <tr>
                                            <th>Fecha</th>
                                            <th>N° Comprobante</th>
                                            <th>Soles (S/)</th>
                                            <th>Dólares ($)</th>
                                            <th>Observaciones</th>
                                            <th>Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaMovilidadBody">
                                        <!-- Los registros se cargarán aquí dinámicamente -->
                                    </tbody>
                                </table>
                            </div>

                            <!-- Mensaje cuando no hay registros -->
                            <div id="noMovilidadMessage" class="text-center py-4 text-muted" style="display: none;">
                                <i class="fas fa-car fa-3x mb-3"></i>
                                <p>No hay registros de movilidad aún.</p>
                            </div>
                        </div>

                        <!-- Totales -->
                        <div class="card-footer bg-light">
                            <div class="row">
                                <div class="col-md-6">
                                    <strong>Total en Soles: <span id="totalMovilidadSoles" class="text-success">S/ 0.00</span></strong>
                                </div>
                                <div class="col-md-6 text-right">
                                    <strong>Total en Dólares: <span id="totalMovilidadDolares" class="text-success">$ 0.00</span></strong>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">
                        <i class="fas fa-times"></i>Cerrar
                    </button>
                    <button type="button" class="btn btn-primary" onclick="aplicarMovilidad()">
                        <i class="fas fa-check"></i>Aplicar y Cerrar
                    </button>
                </div>
            </div>
        </div>
    </div>

    <!-- MODAL PARA GESTIÓN DE ENCAPADA/DESCENCARPADA -->
    <div class="modal fade" id="modalEncapada" tabindex="-1" role="dialog" aria-labelledby="modalEncapadaLabel" aria-hidden="true">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content">
                <div class="modal-header bg-primary text-white">
                    <h5 class="modal-title" id="modalEncapadaLabel">
                        <i class="fas fa-tarp"></i>Gestión Detallada de Encapada/Descencarpada
                    </h5>
                    <button type="button" class="close text-white" data-dismiss="modal" aria-label="Close">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>

                <div class="modal-body">
                    <!-- Formulario para agregar nueva encapada -->
                    <div class="card mb-3">
                        <div class="card-header bg-light">
                            <h6 class="mb-0"><i class="fas fa-plus-circle"></i>Agregar Nuevo Gasto de Encapada/Descencarpada</h6>
                        </div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoEncapadaFecha">Fecha Comprobante: <span class="text-danger">*</span></label>
                                    <input type="date" class="form-control" id="nuevoEncapadaFecha">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoEncapadaComprobante">N° Comprobante:</label>
                                    <input type="text" class="form-control" id="nuevoEncapadaComprobante" placeholder="Ej: 001-123456">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoEncapadaObservaciones">Observaciones:</label>
                                    <input type="text" class="form-control" id="nuevoEncapadaObservaciones" placeholder="Observaciones adicionales">
                                </div>
                            </div>

                            <div class="row">
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoEncapadaSoles">Monto Soles (S/):</label>
                                    <input type="number" class="form-control" id="nuevoEncapadaSoles" placeholder="0.00" min="0" step="0.01">
                                </div>
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoEncapadaDolares">Monto Dólares ($):</label>
                                    <input type="number" class="form-control" id="nuevoEncapadaDolares" placeholder="0.00" min="0" step="0.01">
                                </div>
                            </div>

                            <div class="text-right">
                                <button type="button" class="btn btn-success" onclick="agregarEncapada()">
                                    <i class="fas fa-plus"></i>Agregar
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Lista de encapada registrada -->
                    <div class="card">
                        <div class="card-header bg-light d-flex justify-content-between align-items-center">
                            <h6 class="mb-0"><i class="fas fa-list"></i>Registros de Encapada/Descencarpada</h6>
                            <span class="badge badge-primary" id="totalEncapadaRegistrados">0 registros</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-striped table-hover mb-0">
                                    <thead class="thead-light">
                                        <tr>
                                            <th>Fecha</th>
                                            <th>N° Comprobante</th>
                                            <th>Soles (S/)</th>
                                            <th>Dólares ($)</th>
                                            <th>Observaciones</th>
                                            <th>Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaEncapadaBody">
                                        <!-- Los registros se cargarán aquí dinámicamente -->
                                    </tbody>
                                </table>
                            </div>

                            <!-- Mensaje cuando no hay registros -->
                            <div id="noEncapadaMessage" class="text-center py-4 text-muted" style="display: none;">
                                <i class="fas fa-tarp fa-3x mb-3"></i>
                                <p>No hay registros de encapada/descencarpada aún.</p>
                            </div>
                        </div>

                        <!-- Totales -->
                        <div class="card-footer bg-light">
                            <div class="row">
                                <div class="col-md-6">
                                    <strong>Total en Soles: <span id="totalEncapadaSoles" class="text-success">S/ 0.00</span></strong>
                                </div>
                                <div class="col-md-6 text-right">
                                    <strong>Total en Dólares: <span id="totalEncapadaDolares" class="text-success">$ 0.00</span></strong>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">
                        <i class="fas fa-times"></i>Cerrar
                    </button>
                    <button type="button" class="btn btn-primary" onclick="aplicarEncapada()">
                        <i class="fas fa-check"></i>Aplicar y Cerrar
                    </button>
                </div>
            </div>
        </div>
    </div>

    <!-- MODAL PARA GESTIÓN DE HOSPEDAJE -->
    <div class="modal fade" id="modalHospedaje" tabindex="-1" role="dialog" aria-labelledby="modalHospedajeLabel" aria-hidden="true">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content">
                <div class="modal-header bg-primary text-white">
                    <h5 class="modal-title" id="modalHospedajeLabel">
                        <i class="fas fa-bed"></i>Gestión Detallada de Hospedaje
                    </h5>
                    <button type="button" class="close text-white" data-dismiss="modal" aria-label="Close">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>

                <div class="modal-body">
                    <!-- Formulario para agregar nuevo hospedaje -->
                    <div class="card mb-3">
                        <div class="card-header bg-light">
                            <h6 class="mb-0"><i class="fas fa-plus-circle"></i>Agregar Nuevo Gasto de Hospedaje</h6>
                        </div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoHospedajeFecha">Fecha Comprobante: <span class="text-danger">*</span></label>
                                    <input type="date" class="form-control" id="nuevoHospedajeFecha">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoHospedajeComprobante">N° Comprobante:</label>
                                    <input type="text" class="form-control" id="nuevoHospedajeComprobante" placeholder="Ej: 001-123456">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoHospedajeObservaciones">Observaciones:</label>
                                    <input type="text" class="form-control" id="nuevoHospedajeObservaciones" placeholder="Observaciones adicionales">
                                </div>
                            </div>

                            <div class="row">
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoHospedajeSoles">Monto Soles (S/):</label>
                                    <input type="number" class="form-control" id="nuevoHospedajeSoles" placeholder="0.00" min="0" step="0.01">
                                </div>
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoHospedajeDolares">Monto Dólares ($):</label>
                                    <input type="number" class="form-control" id="nuevoHospedajeDolares" placeholder="0.00" min="0" step="0.01">
                                </div>
                            </div>

                            <div class="text-right">
                                <button type="button" class="btn btn-success" onclick="agregarHospedaje()">
                                    <i class="fas fa-plus"></i>Agregar
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Lista de hospedaje registrado -->
                    <div class="card">
                        <div class="card-header bg-light d-flex justify-content-between align-items-center">
                            <h6 class="mb-0"><i class="fas fa-list"></i>Registros de Hospedaje</h6>
                            <span class="badge badge-primary" id="totalHospedajeRegistrados">0 registros</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-striped table-hover mb-0">
                                    <thead class="thead-light">
                                        <tr>
                                            <th>Fecha</th>
                                            <th>N° Comprobante</th>
                                            <th>Soles (S/)</th>
                                            <th>Dólares ($)</th>
                                            <th>Observaciones</th>
                                            <th>Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaHospedajeBody">
                                        <!-- Los registros se cargarán aquí dinámicamente -->
                                    </tbody>
                                </table>
                            </div>

                            <!-- Mensaje cuando no hay registros -->
                            <div id="noHospedajeMessage" class="text-center py-4 text-muted" style="display: none;">
                                <i class="fas fa-bed fa-3x mb-3"></i>
                                <p>No hay registros de hospedaje aún.</p>
                            </div>
                        </div>

                        <!-- Totales -->
                        <div class="card-footer bg-light">
                            <div class="row">
                                <div class="col-md-6">
                                    <strong>Total en Soles: <span id="totalHospedajeSoles" class="text-success">S/ 0.00</span></strong>
                                </div>
                                <div class="col-md-6 text-right">
                                    <strong>Total en Dólares: <span id="totalHospedajeDolares" class="text-success">$ 0.00</span></strong>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">
                        <i class="fas fa-times"></i>Cerrar
                    </button>
                    <button type="button" class="btn btn-primary" onclick="aplicarHospedaje()">
                        <i class="fas fa-check"></i>Aplicar y Cerrar
                    </button>
                </div>
            </div>
        </div>
    </div>

    <!-- MODAL PARA GESTIÓN DE COMBUSTIBLE -->
    <div class="modal fade" id="modalCombustible" tabindex="-1" role="dialog" aria-labelledby="modalCombustibleLabel" aria-hidden="true">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content">
                <div class="modal-header bg-primary text-white">
                    <h5 class="modal-title" id="modalCombustibleLabel">
                        <i class="fas fa-gas-pump"></i>Gestión Detallada de Combustible
                    </h5>
                    <button type="button" class="close text-white" data-dismiss="modal" aria-label="Close">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>

                <div class="modal-body">
                    <!-- Formulario para agregar nuevo combustible -->
                    <div class="card mb-3">
                        <div class="card-header bg-light">
                            <h6 class="mb-0"><i class="fas fa-plus-circle"></i>Agregar Nuevo Gasto de Combustible</h6>
                        </div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoCombustibleFecha">Fecha Comprobante: <span class="text-danger">*</span></label>
                                    <input type="date" class="form-control" id="nuevoCombustibleFecha">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoCombustibleComprobante">N° Comprobante:</label>
                                    <input type="text" class="form-control" id="nuevoCombustibleComprobante" placeholder="Ej: 001-123456">
                                </div>
                                <div class="col-md-4 mb-3">
                                    <label for="nuevoCombustibleObservaciones">Observaciones:</label>
                                    <input type="text" class="form-control" id="nuevoCombustibleObservaciones" placeholder="Observaciones adicionales">
                                </div>
                            </div>

                            <div class="row">
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoCombustibleSoles">Monto Soles (S/):</label>
                                    <input type="number" class="form-control" id="nuevoCombustibleSoles" placeholder="0.00" min="0" step="0.01">
                                </div>
                                <div class="col-md-6 mb-3">
                                    <label for="nuevoCombustibleDolares">Monto Dólares ($):</label>
                                    <input type="number" class="form-control" id="nuevoCombustibleDolares" placeholder="0.00" min="0" step="0.01">
                                </div>
                            </div>

                            <div class="text-right">
                                <button type="button" class="btn btn-success" onclick="agregarCombustible()">
                                    <i class="fas fa-plus"></i>Agregar
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Lista de combustible registrado -->
                    <div class="card">
                        <div class="card-header bg-light d-flex justify-content-between align-items-center">
                            <h6 class="mb-0"><i class="fas fa-list"></i>Registros de Combustible</h6>
                            <span class="badge badge-primary" id="totalCombustibleRegistrados">0 registros</span>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                                <table class="table table-striped table-hover mb-0">
                                    <thead class="thead-light">
                                        <tr>
                                            <th>Fecha</th>
                                            <th>N° Comprobante</th>
                                            <th>Soles (S/)</th>
                                            <th>Dólares ($)</th>
                                            <th>Observaciones</th>
                                            <th>Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody id="tablaCombustibleBody">
                                        <!-- Los registros se cargarán aquí dinámicamente -->
                                    </tbody>
                                </table>
                            </div>

                            <!-- Mensaje cuando no hay registros -->
                            <div id="noCombustibleMessage" class="text-center py-4 text-muted" style="display: none;">
                                <i class="fas fa-gas-pump fa-3x mb-3"></i>
                                <p>No hay registros de combustible aún.</p>
                            </div>
                        </div>

                        <!-- Totales -->
                        <div class="card-footer bg-light">
                            <div class="row">
                                <div class="col-md-6">
                                    <strong>Total en Soles: <span id="totalCombustibleSoles" class="text-success">S/ 0.00</span></strong>
                                </div>
                                <div class="col-md-6 text-right">
                                    <strong>Total en Dólares: <span id="totalCombustibleDolares" class="text-success">$ 0.00</span></strong>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">
                        <i class="fas fa-times"></i>Cerrar
                    </button>
                    <button type="button" class="btn btn-primary" onclick="aplicarCombustible()">
                        <i class="fas fa-check"></i>Aplicar y Cerrar
                    </button>
                </div>
            </div>
        </div>
    </div>

    <!-- Campo oculto para almacenar datos JSON de peajes -->
    <!-- Campos ocultos para almacenar datos JSON de todas las categorías de gastos -->
    <input type="hidden" id="hiddenGastosAdicionales" name="hiddenGastosAdicionales" value="[]" />
<input type="hidden" id="hiddenIngresosAdicionales" name="hiddenIngresosAdicionales" value="[]" />" />
    <input type="hidden" id="hiddenPeajesDetalle" name="hiddenPeajesDetalle" value="[]" />
    <input type="hidden" id="hiddenAlimentacionDetalle" name="hiddenAlimentacionDetalle" value="[]" />
    <input type="hidden" id="hiddenApoyoSeguridadDetalle" name="hiddenApoyoSeguridadDetalle" value="[]" />
    <input type="hidden" id="hiddenReparacionesDetalle" name="hiddenReparacionesDetalle" value="[]" />
    <input type="hidden" id="hiddenMovilidadDetalle" name="hiddenMovilidadDetalle" value="[]" />
    <input type="hidden" id="hiddenEncapadaDetalle" name="hiddenEncapadaDetalle" value="[]" />
    <input type="hidden" id="hiddenHospedajeDetalle" name="hiddenHospedajeDetalle" value="[]" />
    <input type="hidden" id="hiddenCombustibleDetalle" name="hiddenCombustibleDetalle" value="[]" />

    <!-- CSS MEJORADO para Liquidaciones Avanzadas CON ESTILOS DE PEAJES -->
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

        /* ESTILOS PARA GESTIÓN DE PEAJES */

        /* Botón de detallar peajes */
        .btn-outline-primary.btn-sm {
            font-size: 11px;
            padding: 2px 8px;
            border-radius: 12px;
        }

        /* Campos de solo lectura para totales automáticos */
        input[readonly] {
            background-color: #f8f9fa !important;
            border-color: #dee2e6 !important;
            cursor: not-allowed;
        }

            input[readonly]:focus {
                box-shadow: none !important;
                border-color: #dee2e6 !important;
            }

        /* Badge contador de peajes */
        #contadorPeajes {
            font-size: 11px;
            min-width: 20px;
            height: 20px;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            background: linear-gradient(135deg, #007bff, #0056b3);
            border-radius: 10px;
            transition: all 0.3s ease;
        }

        /* Tabla de peajes en el modal */
        #tablaPeajesBody tr:hover {
            background-color: #f8f9fa;
        }

        /* Totales en el footer del modal */
        .modal-footer .text-success {
            font-weight: bold;
            font-size: 1.1em;
        }

        /* Campos del formulario de peajes */
        .modal-body .card {
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }

        .modal-body .card-header {
            border-bottom: 2px solid #e9ecef;
            font-weight: 600;
        }

        /* Mensaje cuando no hay peajes */
        #noPeajesMessage {
            padding: 2rem;
            color: #6c757d;
        }

            #noPeajesMessage .fa-road {
                color: #dee2e6;
            }

        /* Animación para el contador de peajes */
        #contadorPeajes:not(:empty) {
            animation: pulse-counter 1s ease-in-out;
        }

        @keyframes pulse-counter {
            0% {
                transform: scale(1);
            }

            50% {
                transform: scale(1.2);
            }

            100% {
                transform: scale(1);
            }
        }

        /* Responsive para móviles */
        @media (max-width: 768px) {
            .modal-lg {
                max-width: 95%;
                margin: 10px auto;
            }

            .modal-body .row > div {
                margin-bottom: 15px;
            }

            .btn-group-sm .btn {
                padding: 4px 8px;
                font-size: 10px;
            }
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
            0% {
                left: -100%;
            }

            100% {
                left: 100%;
            }
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
            0%, 100% {
                transform: scale(1);
            }

            50% {
                transform: scale(1.1);
            }
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
        .icon-transito-vacio:before {
            content: "🚛 ";
        }

        .icon-transito-carga:before {
            content: "🚚 ";
        }

        .icon-solo-carga:before {
            content: "📦 ";
        }

        .icon-solo-descarga:before {
            content: "📤 ";
        }

        .icon-descarga-carga:before {
            content: "🔄 ";
        }

        .icon-parada:before {
            content: "⏸️ ";
        }

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
            0% {
                border-color: #dc3545;
            }

            50% {
                border-color: #ff6b7a;
            }

            100% {
                border-color: #dc3545;
            }
        }

        /* Productos filtrados */
        .productos-filtrados {
            border-left: 3px solid #28a745;
            background-color: #d4edda;
            padding: 10px;
            margin-bottom: 10px;
            border-radius: 4px;
        }

        /* *** NUEVOS ESTILOS PARA PLANTAS *** */
        .planta-select {
            border-left: 4px solid #28a745;
        }

            .planta-select:focus {
                border-left-color: #155724;
                box-shadow: 0 0 0 0.2rem rgba(40, 167, 69, 0.25);
            }

        .planta-info {
            background-color: #d4edda;
            border: 1px solid #c3e6cb;
            color: #155724;
            padding: 8px 12px;
            border-radius: 4px;
            font-size: 12px;
            margin-top: 5px;
        }

        .planta-badge {
            background: linear-gradient(135deg, #28a745, #20c997);
            color: white;
            padding: 4px 8px;
            border-radius: 12px;
            font-size: 11px;
            font-weight: bold;
        }

        .operacion-con-planta {
            border-left: 5px solid #28a745;
            padding-left: 15px;
        }

            .operacion-con-planta .alert-info {
                background-color: #d1ecf1;
                border-color: #bee5eb;
                color: #0c5460;
            }

        .planta-requerida {
            border: 2px solid #ffc107 !important;
            animation: pulse-warning 1.5s infinite;
        }

        @keyframes pulse-warning {
            0% {
                border-color: #ffc107;
            }

            50% {
                border-color: #fd7e14;
            }

            100% {
                border-color: #ffc107;
            }
        }

        .planta-validada {
            border-left-color: #28a745 !important;
            background-color: #f8fff9;
        }

        /* Tooltip para plantas */
        .planta-tooltip {
            position: relative;
            cursor: help;
        }

            .planta-tooltip:hover::after {
                content: attr(data-planta-info);
                position: absolute;
                top: -40px;
                left: 50%;
                transform: translateX(-50%);
                background: rgba(0,0,0,0.8);
                color: white;
                padding: 8px 12px;
                border-radius: 6px;
                font-size: 12px;
                white-space: nowrap;
                z-index: 1000;
                box-shadow: 0 2px 8px rgba(0,0,0,0.3);
            }

            .planta-tooltip:hover::before {
                content: '';
                position: absolute;
                top: -10px;
                left: 50%;
                transform: translateX(-50%);
                border-left: 5px solid transparent;
                border-right: 5px solid transparent;
                border-top: 5px solid rgba(0,0,0,0.8);
                z-index: 1000;
            }

        /* Indicadores de plantas en sub-tramos */
        .subtramo-plantas-info {
            background: linear-gradient(135deg, #f8f9fa, #e9ecef);
            border: 1px solid #dee2e6;
            border-radius: 8px;
            padding: 10px;
            margin-top: 15px;
        }

            .subtramo-plantas-info h6 {
                color: #495057;
                font-weight: bold;
                margin-bottom: 8px;
            }

        .planta-item {
            display: inline-block;
            background: #ffffff;
            border: 1px solid #ced4da;
            border-radius: 15px;
            padding: 5px 12px;
            margin: 3px;
            font-size: 12px;
            color: #495057;
        }

            .planta-item.carga {
                border-color: #28a745;
                color: #155724;
                background-color: #d4edda;
            }

            .planta-item.descarga {
                border-color: #ffc107;
                color: #856404;
                background-color: #fff3cd;
            }
    </style>

    <!-- Scripts -->
    <script src="https://code.jquery.com/jquery-3.6.4.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/select2/4.1.0-rc.0/js/select2.min.js"></script>
    <link href="https://cdnjs.cloudflare.com/ajax/libs/select2/4.1.0-rc.0/css/select2.min.css" rel="stylesheet" />

    <script>

        // Variables de datos del sistema
        const productos = <%= ObtenerProductosJSON() %>;
        const clientes = <%= ObtenerClientesJSON() %>;
        const cpics = <%= ObtenerCPICsJSON() %>;
        const facturas = <%= ObtenerFacturasJSON() %>;

        // *** NUEVAS VARIABLES PARA PLANTAS ***
        const plantasCarga = <%= ObtenerPlantasCargaJSON() %>;
        const plantasDescarga = <%= ObtenerPlantasDescargaJSON() %>;
        const estacionesPeaje = <%= ObtenerEstacionesPeajeJSON() %>;

        // Variables para el sistema de auto-llenado (mantener existentes)
        let productosCargadosGlobal = [];
        let productosCargadosPorLiquidacion = {};
        let historialOperaciones = [];

        // Variables de liquidación (mantener existentes)
        let liquidacionesData = [];
        let contadorLiquidaciones = 0;
        let contadorSubTramosPorLiquidacion = {};
        let contadorIngresosAdicionales = 0;
        let contadorGastosAdicionales = 0;


        function validarFechaSegura(fechaString) {
            if (!fechaString || fechaString.trim() === '') {
                return null; // Retornar null en lugar de fecha vacía
            }

            try {
                const fecha = new Date(fechaString + 'T00:00:00'); // Agregar hora para evitar problemas de zona horaria

                // Verificar que sea una fecha válida
                if (isNaN(fecha.getTime())) {
                    console.warn(`Fecha inválida: ${fechaString}`);
                    return null;
                }

                // Verificar rango válido para SQL Server (1753-9999)
                const year = fecha.getFullYear();
                if (year < 1753 || year > 9999) {
                    console.warn(`Fecha fuera de rango SQL Server: ${fechaString} (año ${year})`);
                    return null;
                }

                // Retornar fecha en formato ISO válido
                return fechaString; // Mantener formato YYYY-MM-DD
            } catch (error) {
                console.error(`Error validando fecha: ${fechaString}`, error);
                return null;
            }
        }

        // ========================================
        // SISTEMA DE GESTIÓN DE PEAJES DETALLADOS
        // ========================================

        // Variables globales para gestión de peajes
        let peajesDetallados = [];
        let contadorPeajes = 0;

        /**
         * Abrir modal de gestión de peajes
         */
        function abrirModalPeajes() {
            // Establecer fecha actual por defecto
            document.getElementById('nuevoPeajeFecha').value = new Date().toISOString().split('T')[0];

            // Cargar peajes existentes
            cargarPeajesEnModal();

            // Mostrar modal
            $('#modalPeajes').modal('show');

            // INICIALIZAR Select2 DESPUÉS de mostrar el modal
            $('#modalPeajes').on('shown.bs.modal', function () {
                // Destruir Select2 anterior si existe
                if ($('#nuevoPeajeEstacion').hasClass('select2-hidden-accessible')) {
                    $('#nuevoPeajeEstacion').select2('destroy');
                }

                // Limpiar y cargar opciones
                $('#nuevoPeajeEstacion').empty();
                $('#nuevoPeajeEstacion').append('<option value="">Escriba o seleccione una estación...</option>');

                // CARGAR datos desde estacionesPeaje
                if (estacionesPeaje && estacionesPeaje.length > 0) {
                    estacionesPeaje.forEach(function (estacion) {
                        $('#nuevoPeajeEstacion').append(`<option value="${estacion.nombre}">${estacion.nombre}</option>`);
                    });
                }

                // Configuración IGUAL al de conductor
                $('#nuevoPeajeEstacion').select2({
                    placeholder: "Buscar estación...",
                    allowClear: true,
                    width: '100%',
                    dropdownParent: $('#modalPeajes'),
                    // SIN tags: true para que funcione como el de conductor
                    minimumInputLength: 0
                });

                // Enfocar el campo
                setTimeout(function () {
                    $('#nuevoPeajeEstacion').focus();
                }, 100);

                console.log('Select2 inicializado con', estacionesPeaje.length, 'estaciones');
            });

            console.log('Modal de peajes abierto');
        }

        /**
         * Agregar nuevo peaje
         */
        function agregarPeaje() {
            const estacion = $('#nuevoPeajeEstacion').val();
            const fechaRaw = document.getElementById('nuevoPeajeFecha').value;
            const comprobante = document.getElementById('nuevoPeajeComprobante').value.trim();
            const soles = parseFloat(document.getElementById('nuevoPeajeSoles').value) || 0;
            const dolares = parseFloat(document.getElementById('nuevoPeajeDolares').value) || 0;
            const observaciones = document.getElementById('nuevoPeajeObservaciones').value.trim();

            if (!estacion) {
                alert('Debe seleccionar una estación de peaje');
                $('#nuevoPeajeEstacion').focus();
                return;
            }

            const fechaValidada = validarFechaSegura(fechaRaw);
            if (!fechaValidada) {
                alert('Debe seleccionar una fecha válida (entre años 1753 y 9999)');
                document.getElementById('nuevoPeajeFecha').focus();
                return;
            }

            if (soles <= 0 && dolares <= 0) {
                alert('Debe ingresar al menos un monto en soles o dólares');
                document.getElementById('nuevoPeajeSoles').focus();
                return;
            }

            const nuevoPeaje = {
                id: ++contadorPeajes,
                estacion: estacion,
                // *** MAPPING CORRECTO PARA C# PeajeDetallado ***
                fecha: fechaValidada,                   // C# espera: fecha (DateTime)
                comprobante: comprobante || null,       // C# espera: comprobante
                soles: soles,                          // C# espera: soles
                dolares: dolares,                      // C# espera: dolares
                observaciones: observaciones || null,
                fechaCreacion: new Date().toISOString()
            };

            peajesDetallados.push(nuevoPeaje);
            actualizarTablaPeajes();
            limpiarFormularioPeaje();
            actualizarTotalesPeajes();

            console.log('✅ Peaje agregado con fecha validada:', estacion, '-', fechaValidada);
        }

        /**
         * Eliminar peaje por ID
         */
        function eliminarPeaje(id) {
            if (confirm('¿Está seguro de eliminar este peaje?')) {
                const index = peajesDetallados.findIndex(p => p.id === id);
                if (index > -1) {
                    const peajeEliminado = peajesDetallados[index];
                    peajesDetallados.splice(index, 1);

                    actualizarTablaPeajes();
                    actualizarTotalesPeajes();

                    console.log(`Peaje eliminado: ${peajeEliminado.estacion}`);
                }
            }
        }

        /**
         * Editar peaje existente
         */
        function editarPeaje(id) {
            const peaje = peajesDetallados.find(p => p.id === id);
            if (peaje) {
                // Cargar datos en el formulario
                $('#nuevoPeajeEstacion').val(peaje.estacion).trigger('change');
                document.getElementById('nuevoPeajeFecha').value = peaje.fecha;
                document.getElementById('nuevoPeajeComprobante').value = peaje.comprobante || '';
                document.getElementById('nuevoPeajeSoles').value = peaje.soles || '';
                document.getElementById('nuevoPeajeDolares').value = peaje.dolares || '';
                document.getElementById('nuevoPeajeObservaciones').value = peaje.observaciones || '';

                // Eliminar el peaje original (se reemplazará cuando agregue)
                eliminarPeaje(id);
            }
        }

        /**
         * Actualizar tabla de peajes en el modal
         */
        function actualizarTablaPeajes() {
            const tbody = document.getElementById('tablaPeajesBody');
            const noPeajesMessage = document.getElementById('noPeajesMessage');

            if (peajesDetallados.length === 0) {
                tbody.innerHTML = '';
                noPeajesMessage.style.display = 'block';
                document.getElementById('totalPeajesRegistrados').textContent = '0 peajes';
                return;
            }

            noPeajesMessage.style.display = 'none';

            // Generar filas de la tabla
            tbody.innerHTML = peajesDetallados.map(peaje => `
                <tr>
                    <td>
                        <strong>${peaje.estacion}</strong>
                        ${peaje.observaciones ? `<br><small class="text-muted">${peaje.observaciones}</small>` : ''}
                    </td>
                    <td>
                        <span class="badge badge-secondary">${formatearFecha(peaje.fecha)}</span>
                    </td>
                    <td>${peaje.comprobante || '<em class="text-muted">N/A</em>'}</td>
                    <td class="text-right">
                        ${peaje.soles > 0 ? `<span class="text-success">S/ ${peaje.soles.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
                    </td>
                    <td class="text-right">
                        ${peaje.dolares > 0 ? `<span class="text-success">$ ${peaje.dolares.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
                    </td>
                    <td>
                        <div class="btn-group btn-group-sm" role="group">
                            <button type="button" class="btn btn-outline-primary" onclick="editarPeaje(${peaje.id})" title="Editar">
                                <i class="fas fa-edit"></i>
                            </button>
                            <button type="button" class="btn btn-outline-danger" onclick="eliminarPeaje(${peaje.id})" title="Eliminar">
                                <i class="fas fa-trash"></i>
                            </button>
                        </div>
                    </td>
                </tr>
            `).join('');

            document.getElementById('totalPeajesRegistrados').textContent = `${peajesDetallados.length} peaje${peajesDetallados.length !== 1 ? 's' : ''}`;
        }

        /**
         * Actualizar totales de peajes en el modal y en la tabla principal
         */
        function actualizarTotalesPeajes() {
            const totalSoles = peajesDetallados.reduce((sum, peaje) => sum + (peaje.soles || 0), 0);
            const totalDolares = peajesDetallados.reduce((sum, peaje) => sum + (peaje.dolares || 0), 0);

            // Actualizar totales en el modal
            document.getElementById('totalPeajesSoles').textContent = `S/ ${totalSoles.toFixed(2)}`;
            document.getElementById('totalPeajesDolares').textContent = `$ ${totalDolares.toFixed(2)}`;

            // Actualizar campos principales en la tabla de gastos
            document.getElementById('txtPeajesSoles').value = totalSoles > 0 ? totalSoles.toFixed(2) : '';
            document.getElementById('txtPeajesDolares').value = totalDolares > 0 ? totalDolares.toFixed(2) : '';

            // Actualizar contador visual
            document.getElementById('contadorPeajes').textContent = peajesDetallados.length;

            // Actualizar descripción automática
            actualizarDescripcionPeajes();

            // Recalcular totales generales
            calcularTotales();
        }

        /**
         * Actualizar descripción automática basada en peajes
         */
        function actualizarDescripcionPeajes() {
            const descripcionField = document.getElementById('txtDescPeajes');

            if (peajesDetallados.length === 0) {
                descripcionField.value = '';
                return;
            }

            if (peajesDetallados.length === 1) {
                descripcionField.value = `Peaje: ${peajesDetallados[0].estacion}`;
            } else if (peajesDetallados.length <= 3) {
                const estaciones = peajesDetallados.map(p => p.estacion).join(', ');
                descripcionField.value = `Peajes: ${estaciones}`;
            } else {
                descripcionField.value = `${peajesDetallados.length} peajes registrados`;
            }
        }

        /**
         * Limpiar formulario de nuevo peaje
         */
        function limpiarFormularioPeaje() {
            $('#nuevoPeajeEstacion').val(null).trigger('change');
            document.getElementById('nuevoPeajeFecha').value = new Date().toISOString().split('T')[0];
            document.getElementById('nuevoPeajeComprobante').value = '';
            document.getElementById('nuevoPeajeSoles').value = '';
            document.getElementById('nuevoPeajeDolares').value = '';
            document.getElementById('nuevoPeajeObservaciones').value = '';
        }

        /**
         * Aplicar cambios y cerrar modal
         */
        function aplicarPeajes() {
            // Guardar en campo oculto
            guardarPeajesEnJSON();

            // Cerrar modal
            $('#modalPeajes').modal('hide');

            console.log(`Peajes aplicados: ${peajesDetallados.length} registros`);
        }

        /**
         * Guardar peajes en campo JSON oculto
         */
        function guardarPeajesEnJSON() {
            const hiddenField = document.getElementById('hiddenPeajesDetalle');
            hiddenField.value = JSON.stringify(peajesDetallados);
        }

        /**
         * Cargar peajes existentes en el modal
         */
        function cargarPeajesEnModal() {
            try {
                const hiddenField = document.getElementById('hiddenPeajesDetalle');
                const data = hiddenField.value;

                if (data && data !== '[]') {
                    peajesDetallados = JSON.parse(data);

                    // Actualizar contador para nuevos peajes
                    contadorPeajes = peajesDetallados.length > 0 ?
                        Math.max(...peajesDetallados.map(p => p.id)) : 0;

                    actualizarTablaPeajes();
                    actualizarTotalesPeajes();
                } else {
                    peajesDetallados = [];
                    contadorPeajes = 0;
                    actualizarTablaPeajes();
                }
            } catch (error) {
                console.error('Error cargando peajes:', error);
                peajesDetallados = [];
                contadorPeajes = 0;
            }
        }

        /**
         * Formatear fecha para mostrar
         */
        function formatearFecha(fechaStr) {
            try {
                const fecha = new Date(fechaStr + 'T00:00:00');
                return fecha.toLocaleDateString('es-PE', {
                    day: '2-digit',
                    month: '2-digit',
                    year: 'numeric'
                });
            } catch (error) {
                return fechaStr;
            }
        }

        // ========================================
        // SISTEMA DE GESTIÓN DE ALIMENTACIÓN DETALLADA
        // ========================================

        // Variables globales para gestión de alimentación
        let alimentacionDetallada = [];
        let contadorAlimentacion = 0;

        /**
         * Abrir modal de gestión de alimentación
         */
        function abrirModalAlimentacion() {
            document.getElementById('nuevoAlimentacionFecha').value = new Date().toISOString().split('T')[0];
            cargarAlimentacionEnModal();
            $('#modalAlimentacion').modal('show');
        }

        /**
         * Agregar nuevo registro de alimentación
         */
        function agregarAlimentacion() {
            const fechaRaw = document.getElementById('nuevoAlimentacionFecha').value;
            const comprobante = document.getElementById('nuevoAlimentacionComprobante').value.trim();
            const soles = parseFloat(document.getElementById('nuevoAlimentacionSoles').value) || 0;
            const dolares = parseFloat(document.getElementById('nuevoAlimentacionDolares').value) || 0;
            const observaciones = document.getElementById('nuevoAlimentacionObservaciones').value.trim();

            const fechaValidada = validarFechaSegura(fechaRaw);
            if (!fechaValidada) {
                alert('Debe seleccionar una fecha válida (entre años 1753 y 9999)');
                document.getElementById('nuevoAlimentacionFecha').focus();
                return;
            }

            if (soles <= 0 && dolares <= 0) {
                alert('Debe ingresar al menos un monto en soles o dólares');
                return;
            }

            const nuevoRegistro = {
                id: ++contadorAlimentacion,
                // *** MAPPING CORRECTO PARA C# ***
                fechaComprobante: fechaValidada,        // C# espera: fechaComprobante
                numeroComprobante: comprobante || null, // C# espera: numeroComprobante
                montoSoles: soles,                      // C# espera: montoSoles
                montoDolares: dolares,                  // C# espera: montoDolares
                observaciones: observaciones || null,
                fechaCreacion: new Date().toISOString()
            };

            alimentacionDetallada.push(nuevoRegistro);
            actualizarTablaAlimentacion();
            limpiarFormularioAlimentacion();
            actualizarTotalesAlimentacion();

            console.log(`✅ Alimentación agregada con fecha validada: ${fechaValidada}`);
        }

        /**
         * Eliminar registro de alimentación
         */
        function eliminarAlimentacion(id) {
            if (confirm('¿Está seguro de eliminar este registro?')) {
                const index = alimentacionDetallada.findIndex(p => p.id === id);
                if (index > -1) {
                    alimentacionDetallada.splice(index, 1);
                    actualizarTablaAlimentacion();
                    actualizarTotalesAlimentacion();
                }
            }
        }

        /**
         * Editar registro de alimentación
         */
        function editarAlimentacion(id) {
            const registro = alimentacionDetallada.find(p => p.id === id);
            if (registro) {
                document.getElementById('nuevoAlimentacionFecha').value = registro.fecha;
                document.getElementById('nuevoAlimentacionComprobante').value = registro.comprobante || '';
                document.getElementById('nuevoAlimentacionSoles').value = registro.soles || '';
                document.getElementById('nuevoAlimentacionDolares').value = registro.dolares || '';
                document.getElementById('nuevoAlimentacionObservaciones').value = registro.observaciones || '';
                eliminarAlimentacion(id);
            }
        }

        /**
         * Actualizar tabla de alimentación en el modal
         */
        function actualizarTablaAlimentacion() {
            const tbody = document.getElementById('tablaAlimentacionBody');
            const noMessage = document.getElementById('noAlimentacionMessage');

            if (alimentacionDetallada.length === 0) {
                tbody.innerHTML = '';
                noMessage.style.display = 'block';
                document.getElementById('totalAlimentacionRegistrados').textContent = '0 registros';
                return;
            }

            noMessage.style.display = 'none';

            tbody.innerHTML = alimentacionDetallada.map(registro => `
                <tr>
                    <td><span class="badge badge-secondary">${formatearFecha(registro.fecha)}</span></td>
                    <td>${registro.comprobante || '<em class="text-muted">N/A</em>'}</td>
                    <td class="text-right">
                        ${registro.soles > 0 ? `<span class="text-success">S/ ${registro.soles.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
                    </td>
                    <td class="text-right">
                        ${registro.dolares > 0 ? `<span class="text-success">$ ${registro.dolares.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
                    </td>
                    <td>${registro.observaciones || '<em class="text-muted">N/A</em>'}</td>
                    <td>
                        <div class="btn-group btn-group-sm" role="group">
                            <button type="button" class="btn btn-outline-primary" onclick="editarAlimentacion(${registro.id})" title="Editar">
                                <i class="fas fa-edit"></i>
                            </button>
                            <button type="button" class="btn btn-outline-danger" onclick="eliminarAlimentacion(${registro.id})" title="Eliminar">
                                <i class="fas fa-trash"></i>
                            </button>
                        </div>
                    </td>
                </tr>
            `).join('');

            document.getElementById('totalAlimentacionRegistrados').textContent = `${alimentacionDetallada.length} registro${alimentacionDetallada.length !== 1 ? 's' : ''}`;
        }

        /**
         * Actualizar totales de alimentación
         */
        function actualizarTotalesAlimentacion() {
            const totalSoles = alimentacionDetallada.reduce((sum, registro) => sum + (registro.soles || 0), 0);
            const totalDolares = alimentacionDetallada.reduce((sum, registro) => sum + (registro.dolares || 0), 0);

            document.getElementById('totalAlimentacionSoles').textContent = `S/ ${totalSoles.toFixed(2)}`;
            document.getElementById('totalAlimentacionDolares').textContent = `$ ${totalDolares.toFixed(2)}`;

            document.getElementById('txtAlimentacionSoles').value = totalSoles > 0 ? totalSoles.toFixed(2) : '';
            document.getElementById('txtAlimentacionDolares').value = totalDolares > 0 ? totalDolares.toFixed(2) : '';

            document.getElementById('contadorAlimentacion').textContent = alimentacionDetallada.length;

            actualizarDescripcionAlimentacion();
            calcularTotales();
        }

        function actualizarDescripcionAlimentacion() {
            const descripcionField = document.getElementById('txtDescAlimentacion');
            if (alimentacionDetallada.length === 0) {
                descripcionField.value = '';
                return;
            }
            descripcionField.value = `${alimentacionDetallada.length} registro${alimentacionDetallada.length !== 1 ? 's' : ''} de alimentación`;
        }

        function limpiarFormularioAlimentacion() {
            document.getElementById('nuevoAlimentacionFecha').value = new Date().toISOString().split('T')[0];
            document.getElementById('nuevoAlimentacionComprobante').value = '';
            document.getElementById('nuevoAlimentacionSoles').value = '';
            document.getElementById('nuevoAlimentacionDolares').value = '';
            document.getElementById('nuevoAlimentacionObservaciones').value = '';
        }

        function aplicarAlimentacion() {
            guardarAlimentacionEnJSON();
            $('#modalAlimentacion').modal('hide');
        }

        function guardarAlimentacionEnJSON() {
            document.getElementById('hiddenAlimentacionDetalle').value = JSON.stringify(alimentacionDetallada);
        }

        function cargarAlimentacionEnModal() {
            try {
                const data = document.getElementById('hiddenAlimentacionDetalle').value;
                if (data && data !== '[]') {
                    alimentacionDetallada = JSON.parse(data);
                    contadorAlimentacion = alimentacionDetallada.length > 0 ? Math.max(...alimentacionDetallada.map(p => p.id)) : 0;
                    actualizarTablaAlimentacion();
                    actualizarTotalesAlimentacion();
                } else {
                    alimentacionDetallada = [];
                    contadorAlimentacion = 0;
                    actualizarTablaAlimentacion();
                }
            } catch (error) {
                console.error('Error cargando alimentación:', error);
                alimentacionDetallada = [];
                contadorAlimentacion = 0;
            }
        }

        // ========================================
        // SISTEMA DE GESTIÓN DE APOYO-SEGURIDAD DETALLADA
        // ========================================

        let apoyoSeguridadDetallada = [];
        let contadorApoyoSeguridad = 0;

        function abrirModalApoyoSeguridad() {
            document.getElementById('nuevoApoyoSeguridadFecha').value = new Date().toISOString().split('T')[0];
            cargarApoyoSeguridadEnModal();
            $('#modalApoyoSeguridad').modal('show');
        }

        function agregarApoyoSeguridad() {
            const fecha = document.getElementById('nuevoApoyoSeguridadFecha').value;
            const comprobante = document.getElementById('nuevoApoyoSeguridadComprobante').value.trim();
            const soles = parseFloat(document.getElementById('nuevoApoyoSeguridadSoles').value) || 0;
            const dolares = parseFloat(document.getElementById('nuevoApoyoSeguridadDolares').value) || 0;
            const observaciones = document.getElementById('nuevoApoyoSeguridadObservaciones').value.trim();

            if (!fecha) {
                alert('Debe seleccionar una fecha');
                return;
            }

            if (soles <= 0 && dolares <= 0) {
                alert('Debe ingresar al menos un monto en soles o dólares');
                return;
            }

            const nuevoRegistro = {
                id: ++contadorApoyoSeguridad,
                fecha: fecha,
                comprobante: comprobante || null,
                soles: soles,
                dolares: dolares,
                observaciones: observaciones || null,
                fechaCreacion: new Date().toISOString()
            };

            apoyoSeguridadDetallada.push(nuevoRegistro);
            actualizarTablaApoyoSeguridad();
            limpiarFormularioApoyoSeguridad();
            actualizarTotalesApoyoSeguridad();
        }

        function eliminarApoyoSeguridad(id) {
            if (confirm('¿Está seguro de eliminar este registro?')) {
                const index = apoyoSeguridadDetallada.findIndex(p => p.id === id);
                if (index > -1) {
                    apoyoSeguridadDetallada.splice(index, 1);
                    actualizarTablaApoyoSeguridad();
                    actualizarTotalesApoyoSeguridad();
                }
            }
        }

        function editarApoyoSeguridad(id) {
            const registro = apoyoSeguridadDetallada.find(p => p.id === id);
            if (registro) {
                document.getElementById('nuevoApoyoSeguridadFecha').value = registro.fecha;
                document.getElementById('nuevoApoyoSeguridadComprobante').value = registro.comprobante || '';
                document.getElementById('nuevoApoyoSeguridadSoles').value = registro.soles || '';
                document.getElementById('nuevoApoyoSeguridadDolares').value = registro.dolares || '';
                document.getElementById('nuevoApoyoSeguridadObservaciones').value = registro.observaciones || '';
                eliminarApoyoSeguridad(id);
            }
        }

        function actualizarTablaApoyoSeguridad() {
            const tbody = document.getElementById('tablaApoyoSeguridadBody');
            const noMessage = document.getElementById('noApoyoSeguridadMessage');

            if (apoyoSeguridadDetallada.length === 0) {
                tbody.innerHTML = '';
                noMessage.style.display = 'block';
                document.getElementById('totalApoyoSeguridadRegistrados').textContent = '0 registros';
                return;
            }

            noMessage.style.display = 'none';

            tbody.innerHTML = apoyoSeguridadDetallada.map(registro => `
                <tr>
                    <td><span class="badge badge-secondary">${formatearFecha(registro.fecha)}</span></td>
                    <td>${registro.comprobante || '<em class="text-muted">N/A</em>'}</td>
                    <td class="text-right">
                        ${registro.soles > 0 ? `<span class="text-success">S/ ${registro.soles.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
                    </td>
                    <td class="text-right">
                        ${registro.dolares > 0 ? `<span class="text-success">$ ${registro.dolares.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
                    </td>
                    <td>${registro.observaciones || '<em class="text-muted">N/A</em>'}</td>
                    <td>
                        <div class="btn-group btn-group-sm" role="group">
                            <button type="button" class="btn btn-outline-primary" onclick="editarApoyoSeguridad(${registro.id})" title="Editar">
                                <i class="fas fa-edit"></i>
                            </button>
                            <button type="button" class="btn btn-outline-danger" onclick="eliminarApoyoSeguridad(${registro.id})" title="Eliminar">
                                <i class="fas fa-trash"></i>
                            </button>
                        </div>
                    </td>
                </tr>
            `).join('');

            document.getElementById('totalApoyoSeguridadRegistrados').textContent = `${apoyoSeguridadDetallada.length} registro${apoyoSeguridadDetallada.length !== 1 ? 's' : ''}`;
        }

        function actualizarTotalesApoyoSeguridad() {
            const totalSoles = apoyoSeguridadDetallada.reduce((sum, registro) => sum + (registro.soles || 0), 0);
            const totalDolares = apoyoSeguridadDetallada.reduce((sum, registro) => sum + (registro.dolares || 0), 0);

            document.getElementById('totalApoyoSeguridadSoles').textContent = `S/ ${totalSoles.toFixed(2)}`;
            document.getElementById('totalApoyoSeguridadDolares').textContent = `$ ${totalDolares.toFixed(2)}`;

            document.getElementById('txtApoyoSeguridadSoles').value = totalSoles > 0 ? totalSoles.toFixed(2) : '';
            document.getElementById('txtApoyoSeguridadDolares').value = totalDolares > 0 ? totalDolares.toFixed(2) : '';

            document.getElementById('contadorApoyoSeguridad').textContent = apoyoSeguridadDetallada.length;

            actualizarDescripcionApoyoSeguridad();
            calcularTotales();
        }

        function actualizarDescripcionApoyoSeguridad() {
            const descripcionField = document.getElementById('txtDescApoyoSeguridad');
            if (apoyoSeguridadDetallada.length === 0) {
                descripcionField.value = '';
                return;
            }
            descripcionField.value = `${apoyoSeguridadDetallada.length} registro${apoyoSeguridadDetallada.length !== 1 ? 's' : ''} de apoyo-seguridad`;
        }

        function limpiarFormularioApoyoSeguridad() {
            document.getElementById('nuevoApoyoSeguridadFecha').value = new Date().toISOString().split('T')[0];
            document.getElementById('nuevoApoyoSeguridadComprobante').value = '';
            document.getElementById('nuevoApoyoSeguridadSoles').value = '';
            document.getElementById('nuevoApoyoSeguridadDolares').value = '';
            document.getElementById('nuevoApoyoSeguridadObservaciones').value = '';
        }

        function aplicarApoyoSeguridad() {
            guardarApoyoSeguridadEnJSON();
            $('#modalApoyoSeguridad').modal('hide');
        }

        function guardarApoyoSeguridadEnJSON() {
            document.getElementById('hiddenApoyoSeguridadDetalle').value = JSON.stringify(apoyoSeguridadDetallada);
        }

        function cargarApoyoSeguridadEnModal() {
            try {
                const data = document.getElementById('hiddenApoyoSeguridadDetalle').value;
                if (data && data !== '[]') {
                    apoyoSeguridadDetallada = JSON.parse(data);
                    contadorApoyoSeguridad = apoyoSeguridadDetallada.length > 0 ? Math.max(...apoyoSeguridadDetallada.map(p => p.id)) : 0;
                    actualizarTablaApoyoSeguridad();
                    actualizarTotalesApoyoSeguridad();
                } else {
                    apoyoSeguridadDetallada = [];
                    contadorApoyoSeguridad = 0;
                    actualizarTablaApoyoSeguridad();
                }
            } catch (error) {
                console.error('Error cargando apoyo-seguridad:', error);
                apoyoSeguridadDetallada = [];
                contadorApoyoSeguridad = 0;
            }
        }

        // ========================================
        // SISTEMA DE GESTIÓN DE REPARACIONES DETALLADA
        // ========================================

        let reparacionesDetalladas = [];
        let contadorReparaciones = 0;

        function abrirModalReparaciones() {
            document.getElementById('nuevoReparacionesFecha').value = new Date().toISOString().split('T')[0];
            cargarReparacionesEnModal();
            $('#modalReparaciones').modal('show');
        }

        function agregarReparaciones() {
            const fechaRaw = document.getElementById('nuevoReparacionesFecha').value;
            const comprobante = document.getElementById('nuevoReparacionesComprobante').value.trim();
            const soles = parseFloat(document.getElementById('nuevoReparacionesSoles').value) || 0;
            const dolares = parseFloat(document.getElementById('nuevoReparacionesDolares').value) || 0;
            const observaciones = document.getElementById('nuevoReparacionesObservaciones').value.trim();

            const fechaValidada = validarFechaSegura(fechaRaw);
            if (!fechaValidada) {
                alert('Debe seleccionar una fecha válida (entre años 1753 y 9999)');
                document.getElementById('nuevoReparacionesFecha').focus();
                return;
            }

            if (soles <= 0 && dolares <= 0) {
                alert('Debe ingresar al menos un monto en soles o dólares');
                return;
            }

            const nuevoRegistro = {
                id: ++contadorReparaciones,
                // *** MAPPING CORRECTO PARA C# ***
                fechaComprobante: fechaValidada,        // C# espera: fechaComprobante
                numeroComprobante: comprobante || null, // C# espera: numeroComprobante
                montoSoles: soles,                      // C# espera: montoSoles
                montoDolares: dolares,                  // C# espera: montoDolares
                observaciones: observaciones || null,
                fechaCreacion: new Date().toISOString()
            };

            reparacionesDetalladas.push(nuevoRegistro);
            actualizarTablaReparaciones();
            limpiarFormularioReparaciones();
            actualizarTotalesReparaciones();

            console.log(`✅ Reparación agregada con fecha validada: ${fechaValidada}`);
        }

        function eliminarReparaciones(id) {
            if (confirm('¿Está seguro de eliminar este registro?')) {
                const index = reparacionesDetalladas.findIndex(p => p.id === id);
                if (index > -1) {
                    reparacionesDetalladas.splice(index, 1);
                    actualizarTablaReparaciones();
                    actualizarTotalesReparaciones();
                }
            }
        }

        function editarReparaciones(id) {
            const registro = reparacionesDetalladas.find(p => p.id === id);
            if (registro) {
                document.getElementById('nuevoReparacionesFecha').value = registro.fechaComprobante;
                document.getElementById('nuevoReparacionesComprobante').value = registro.numeroComprobante || '';
                document.getElementById('nuevoReparacionesSoles').value = registro.montoSoles || '';
                document.getElementById('nuevoReparacionesDolares').value = registro.montoDolares || '';
                document.getElementById('nuevoReparacionesObservaciones').value = registro.observaciones || '';
                eliminarReparaciones(id);
            }
        }

        function actualizarTablaReparaciones() {
            const tbody = document.getElementById('tablaReparacionesBody');
            const noMessage = document.getElementById('noReparacionesMessage');

            if (reparacionesDetalladas.length === 0) {
                tbody.innerHTML = '';
                noMessage.style.display = 'block';
                document.getElementById('totalReparacionesRegistrados').textContent = '0 registros';
                return;
            }

            noMessage.style.display = 'none';

            tbody.innerHTML = reparacionesDetalladas.map(registro => `
        <tr>
            <td><span class="badge badge-secondary">${formatearFecha(registro.fechaComprobante)}</span></td>
            <td>${registro.numeroComprobante || '<em class="text-muted">N/A</em>'}</td>
            <td class="text-right">
                ${registro.montoSoles > 0 ? `<span class="text-success">S/ ${registro.montoSoles.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
            </td>
            <td class="text-right">
                ${registro.montoDolares > 0 ? `<span class="text-success">$ ${registro.montoDolares.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
            </td>
            <td>${registro.observaciones || '<em class="text-muted">N/A</em>'}</td>
            <td>
                <div class="btn-group btn-group-sm" role="group">
                    <button type="button" class="btn btn-outline-primary" onclick="editarReparaciones(${registro.id})" title="Editar">
                        <i class="fas fa-edit"></i>
                    </button>
                    <button type="button" class="btn btn-outline-danger" onclick="eliminarReparaciones(${registro.id})" title="Eliminar">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
            </td>
        </tr>
    `).join('');

            document.getElementById('totalReparacionesRegistrados').textContent = `${reparacionesDetalladas.length} registro${reparacionesDetalladas.length !== 1 ? 's' : ''}`;
        }

        function actualizarTotalesReparaciones() {
            const totalSoles = reparacionesDetalladas.reduce((sum, registro) => sum + (registro.montoSoles || 0), 0);
            const totalDolares = reparacionesDetalladas.reduce((sum, registro) => sum + (registro.montoDolares || 0), 0);

            document.getElementById('totalReparacionesSoles').textContent = `S/ ${totalSoles.toFixed(2)}`;
            document.getElementById('totalReparacionesDolares').textContent = `$ ${totalDolares.toFixed(2)}`;

            document.getElementById('txtReparacionesSoles').value = totalSoles > 0 ? totalSoles.toFixed(2) : '';
            document.getElementById('txtReparacionesDolares').value = totalDolares > 0 ? totalDolares.toFixed(2) : '';

            document.getElementById('contadorReparaciones').textContent = reparacionesDetalladas.length;

            actualizarDescripcionReparaciones();
            calcularTotales();
        }

        function actualizarDescripcionReparaciones() {
            const descripcionField = document.getElementById('txtDescReparaciones');
            if (reparacionesDetalladas.length === 0) {
                descripcionField.value = '';
                return;
            }
            descripcionField.value = `${reparacionesDetalladas.length} registro${reparacionesDetalladas.length !== 1 ? 's' : ''} de reparaciones`;
        }

        function limpiarFormularioReparaciones() {
            document.getElementById('nuevoReparacionesFecha').value = new Date().toISOString().split('T')[0];
            document.getElementById('nuevoReparacionesComprobante').value = '';
            document.getElementById('nuevoReparacionesSoles').value = '';
            document.getElementById('nuevoReparacionesDolares').value = '';
            document.getElementById('nuevoReparacionesObservaciones').value = '';
        }

        function aplicarReparaciones() {
            guardarReparacionesEnJSON();
            $('#modalReparaciones').modal('hide');
        }

        function guardarReparacionesEnJSON() {
            document.getElementById('hiddenReparacionesDetalle').value = JSON.stringify(reparacionesDetalladas);
        }

        function cargarReparacionesEnModal() {
            try {
                const data = document.getElementById('hiddenReparacionesDetalle').value;
                if (data && data !== '[]') {
                    reparacionesDetalladas = JSON.parse(data);
                    contadorReparaciones = reparacionesDetalladas.length > 0 ? Math.max(...reparacionesDetalladas.map(p => p.id)) : 0;
                    actualizarTablaReparaciones();
                    actualizarTotalesReparaciones();
                } else {
                    reparacionesDetalladas = [];
                    contadorReparaciones = 0;
                    actualizarTablaReparaciones();
                }
            } catch (error) {
                console.error('Error cargando reparaciones:', error);
                reparacionesDetalladas = [];
                contadorReparaciones = 0;
            }
        }

        // ========================================
        // SISTEMA DE GESTIÓN DE MOVILIDAD DETALLADA
        // ========================================

        let movilidadDetallada = [];
        let contadorMovilidad = 0;

        function abrirModalMovilidad() {
            document.getElementById('nuevoMovilidadFecha').value = new Date().toISOString().split('T')[0];
            cargarMovilidadEnModal();
            $('#modalMovilidad').modal('show');
        }

        function agregarMovilidad() {
            const fechaRaw = document.getElementById('nuevoMovilidadFecha').value;
            const comprobante = document.getElementById('nuevoMovilidadComprobante').value.trim();
            const soles = parseFloat(document.getElementById('nuevoMovilidadSoles').value) || 0;
            const dolares = parseFloat(document.getElementById('nuevoMovilidadDolares').value) || 0;
            const observaciones = document.getElementById('nuevoMovilidadObservaciones').value.trim();

            // VALIDACIÓN CRÍTICA: Fecha
            const fechaValidada = validarFechaSegura(fechaRaw);
            if (!fechaValidada) {
                alert('Debe seleccionar una fecha válida (entre años 1753 y 9999)');
                document.getElementById('nuevoMovilidadFecha').focus();
                return;
            }

            if (soles <= 0 && dolares <= 0) {
                alert('Debe ingresar al menos un monto en soles o dólares');
                return;
            }

            const nuevoRegistro = {
                id: ++contadorMovilidad,
                fecha: fechaValidada, // ← MANTENER NOMBRE ORIGINAL
                comprobante: comprobante || null, // ← MANTENER NOMBRE ORIGINAL
                soles: soles, // ← MANTENER NOMBRE ORIGINAL
                dolares: dolares, // ← MANTENER NOMBRE ORIGINAL
                observaciones: observaciones || null,
                fechaCreacion: new Date().toISOString()
            };

            movilidadDetallada.push(nuevoRegistro);
            actualizarTablaMovilidad();
            limpiarFormularioMovilidad();
            actualizarTotalesMovilidad();

            console.log(`✅ Movilidad agregada con fecha validada: ${fechaValidada}`);
        }

        function eliminarMovilidad(id) {
            if (confirm('¿Está seguro de eliminar este registro?')) {
                const index = movilidadDetallada.findIndex(p => p.id === id);
                if (index > -1) {
                    movilidadDetallada.splice(index, 1);
                    actualizarTablaMovilidad();
                    actualizarTotalesMovilidad();
                }
            }
        }

        function editarMovilidad(id) {
            const registro = movilidadDetallada.find(p => p.id === id);
            if (registro) {
                document.getElementById('nuevoMovilidadFecha').value = registro.fecha;
                document.getElementById('nuevoMovilidadComprobante').value = registro.comprobante || '';
                document.getElementById('nuevoMovilidadSoles').value = registro.soles || '';
                document.getElementById('nuevoMovilidadDolares').value = registro.dolares || '';
                document.getElementById('nuevoMovilidadObservaciones').value = registro.observaciones || '';
                eliminarMovilidad(id);
            }
        }

        function actualizarTablaMovilidad() {
            const tbody = document.getElementById('tablaMovilidadBody');
            const noMessage = document.getElementById('noMovilidadMessage');

            if (movilidadDetallada.length === 0) {
                tbody.innerHTML = '';
                noMessage.style.display = 'block';
                document.getElementById('totalMovilidadRegistrados').textContent = '0 registros';
                return;
            }

            noMessage.style.display = 'none';

            tbody.innerHTML = movilidadDetallada.map(registro => `
                <tr>
                    <td><span class="badge badge-secondary">${formatearFecha(registro.fecha)}</span></td>
                    <td>${registro.comprobante || '<em class="text-muted">N/A</em>'}</td>
                    <td class="text-right">
                        ${registro.soles > 0 ? `<span class="text-success">S/ ${registro.soles.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
                    </td>
                    <td class="text-right">
                        ${registro.dolares > 0 ? `<span class="text-success">$ ${registro.dolares.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
                    </td>
                    <td>${registro.observaciones || '<em class="text-muted">N/A</em>'}</td>
                    <td>
                        <div class="btn-group btn-group-sm" role="group">
                            <button type="button" class="btn btn-outline-primary" onclick="editarMovilidad(${registro.id})" title="Editar">
                                <i class="fas fa-edit"></i>
                            </button>
                            <button type="button" class="btn btn-outline-danger" onclick="eliminarMovilidad(${registro.id})" title="Eliminar">
                                <i class="fas fa-trash"></i>
                            </button>
                        </div>
                    </td>
                </tr>
            `).join('');

            document.getElementById('totalMovilidadRegistrados').textContent = `${movilidadDetallada.length} registro${movilidadDetallada.length !== 1 ? 's' : ''}`;
        }

        function actualizarTotalesMovilidad() {
            const totalSoles = movilidadDetallada.reduce((sum, registro) => sum + (registro.soles || 0), 0);
            const totalDolares = movilidadDetallada.reduce((sum, registro) => sum + (registro.dolares || 0), 0);

            document.getElementById('totalMovilidadSoles').textContent = `S/ ${totalSoles.toFixed(2)}`;
            document.getElementById('totalMovilidadDolares').textContent = `$ ${totalDolares.toFixed(2)}`;

            document.getElementById('txtMovilidadSoles').value = totalSoles > 0 ? totalSoles.toFixed(2) : '';
            document.getElementById('txtMovilidadDolares').value = totalDolares > 0 ? totalDolares.toFixed(2) : '';

            document.getElementById('contadorMovilidad').textContent = movilidadDetallada.length;

            actualizarDescripcionMovilidad();
            calcularTotales();
        }

        function actualizarDescripcionMovilidad() {
            const descripcionField = document.getElementById('txtDescMovilidad');
            if (movilidadDetallada.length === 0) {
                descripcionField.value = '';
                return;
            }
            descripcionField.value = `${movilidadDetallada.length} registro${movilidadDetallada.length !== 1 ? 's' : ''} de movilidad`;
        }

        function limpiarFormularioMovilidad() {
            document.getElementById('nuevoMovilidadFecha').value = new Date().toISOString().split('T')[0];
            document.getElementById('nuevoMovilidadComprobante').value = '';
            document.getElementById('nuevoMovilidadSoles').value = '';
            document.getElementById('nuevoMovilidadDolares').value = '';
            document.getElementById('nuevoMovilidadObservaciones').value = '';
        }

        function aplicarMovilidad() {
            guardarMovilidadEnJSON();
            $('#modalMovilidad').modal('hide');
        }

        function guardarMovilidadEnJSON() {
            document.getElementById('hiddenMovilidadDetalle').value = JSON.stringify(movilidadDetallada);
        }

        function cargarMovilidadEnModal() {
            try {
                const data = document.getElementById('hiddenMovilidadDetalle').value;
                if (data && data !== '[]') {
                    movilidadDetallada = JSON.parse(data);
                    contadorMovilidad = movilidadDetallada.length > 0 ? Math.max(...movilidadDetallada.map(p => p.id)) : 0;
                    actualizarTablaMovilidad();
                    actualizarTotalesMovilidad();
                } else {
                    movilidadDetallada = [];
                    contadorMovilidad = 0;
                    actualizarTablaMovilidad();
                }
            } catch (error) {
                console.error('Error cargando movilidad:', error);
                movilidadDetallada = [];
                contadorMovilidad = 0;
            }
        }

        // ========================================
        // SISTEMA DE GESTIÓN DE ENCAPADA DETALLADA
        // ========================================

        let encapadaDetallada = [];
        let contadorEncapada = 0;

        function abrirModalEncapada() {
            document.getElementById('nuevoEncapadaFecha').value = new Date().toISOString().split('T')[0];
            cargarEncapadaEnModal();
            $('#modalEncapada').modal('show');
        }

        function agregarEncapada() {
            const fechaRaw = document.getElementById('nuevoEncapadaFecha').value;
            const comprobante = document.getElementById('nuevoEncapadaComprobante').value.trim();
            const soles = parseFloat(document.getElementById('nuevoEncapadaSoles').value) || 0;
            const dolares = parseFloat(document.getElementById('nuevoEncapadaDolares').value) || 0;
            const observaciones = document.getElementById('nuevoEncapadaObservaciones').value.trim();

            // VALIDACIÓN CRÍTICA: Fecha
            const fechaValidada = validarFechaSegura(fechaRaw);
            if (!fechaValidada) {
                alert('Debe seleccionar una fecha válida (entre años 1753 y 9999)');
                document.getElementById('nuevoEncapadaFecha').focus();
                return;
            }

            if (soles <= 0 && dolares <= 0) {
                alert('Debe ingresar al menos un monto en soles o dólares');
                return;
            }

            const nuevoRegistro = {
                id: ++contadorEncapada,
                fecha: fechaValidada, // ← MANTENER NOMBRE ORIGINAL
                comprobante: comprobante || null, // ← MANTENER NOMBRE ORIGINAL
                soles: soles, // ← MANTENER NOMBRE ORIGINAL
                dolares: dolares, // ← MANTENER NOMBRE ORIGINAL
                observaciones: observaciones || null,
                fechaCreacion: new Date().toISOString()
            };

            encapadaDetallada.push(nuevoRegistro);
            actualizarTablaEncapada();
            limpiarFormularioEncapada();
            actualizarTotalesEncapada();

            console.log(`✅ Encapada agregada con fecha validada: ${fechaValidada}`);
        }

        function eliminarEncapada(id) {
            if (confirm('¿Está seguro de eliminar este registro?')) {
                const index = encapadaDetallada.findIndex(p => p.id === id);
                if (index > -1) {
                    encapadaDetallada.splice(index, 1);
                    actualizarTablaEncapada();
                    actualizarTotalesEncapada();
                }
            }
        }

        function editarEncapada(id) {
            const registro = encapadaDetallada.find(p => p.id === id);
            if (registro) {
                document.getElementById('nuevoEncapadaFecha').value = registro.fecha;
                document.getElementById('nuevoEncapadaComprobante').value = registro.comprobante || '';
                document.getElementById('nuevoEncapadaSoles').value = registro.soles || '';
                document.getElementById('nuevoEncapadaDolares').value = registro.dolares || '';
                document.getElementById('nuevoEncapadaObservaciones').value = registro.observaciones || '';
                eliminarEncapada(id);
            }
        }

        function actualizarTablaEncapada() {
            const tbody = document.getElementById('tablaEncapadaBody');
            const noMessage = document.getElementById('noEncapadaMessage');

            if (encapadaDetallada.length === 0) {
                tbody.innerHTML = '';
                noMessage.style.display = 'block';
                document.getElementById('totalEncapadaRegistrados').textContent = '0 registros';
                return;
            }

            noMessage.style.display = 'none';

            tbody.innerHTML = encapadaDetallada.map(registro => `
                <tr>
                    <td><span class="badge badge-secondary">${formatearFecha(registro.fecha)}</span></td>
                    <td>${registro.comprobante || '<em class="text-muted">N/A</em>'}</td>
                    <td class="text-right">
                        ${registro.soles > 0 ? `<span class="text-success">S/ ${registro.soles.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
                    </td>
                    <td class="text-right">
                        ${registro.dolares > 0 ? `<span class="text-success">$ ${registro.dolares.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
                    </td>
                    <td>${registro.observaciones || '<em class="text-muted">N/A</em>'}</td>
                    <td>
                        <div class="btn-group btn-group-sm" role="group">
                            <button type="button" class="btn btn-outline-primary" onclick="editarEncapada(${registro.id})" title="Editar">
                                <i class="fas fa-edit"></i>
                            </button>
                            <button type="button" class="btn btn-outline-danger" onclick="eliminarEncapada(${registro.id})" title="Eliminar">
                                <i class="fas fa-trash"></i>
                            </button>
                        </div>
                    </td>
                </tr>
            `).join('');

            document.getElementById('totalEncapadaRegistrados').textContent = `${encapadaDetallada.length} registro${encapadaDetallada.length !== 1 ? 's' : ''}`;
        }

        function actualizarTotalesEncapada() {
            const totalSoles = encapadaDetallada.reduce((sum, registro) => sum + (registro.soles || 0), 0);
            const totalDolares = encapadaDetallada.reduce((sum, registro) => sum + (registro.dolares || 0), 0);

            document.getElementById('totalEncapadaSoles').textContent = `S/ ${totalSoles.toFixed(2)}`;
            document.getElementById('totalEncapadaDolares').textContent = `$ ${totalDolares.toFixed(2)}`;

            document.getElementById('txtEncapadaSoles').value = totalSoles > 0 ? totalSoles.toFixed(2) : '';
            document.getElementById('txtEncapadaDolares').value = totalDolares > 0 ? totalDolares.toFixed(2) : '';

            document.getElementById('contadorEncapada').textContent = encapadaDetallada.length;

            actualizarDescripcionEncapada();
            calcularTotales();
        }

        function actualizarDescripcionEncapada() {
            const descripcionField = document.getElementById('txtDescEncapada');
            if (encapadaDetallada.length === 0) {
                descripcionField.value = '';
                return;
            }
            descripcionField.value = `${encapadaDetallada.length} registro${encapadaDetallada.length !== 1 ? 's' : ''} de encapada/descencarpada`;
        }

        function limpiarFormularioEncapada() {
            document.getElementById('nuevoEncapadaFecha').value = new Date().toISOString().split('T')[0];
            document.getElementById('nuevoEncapadaComprobante').value = '';
            document.getElementById('nuevoEncapadaSoles').value = '';
            document.getElementById('nuevoEncapadaDolares').value = '';
            document.getElementById('nuevoEncapadaObservaciones').value = '';
        }

        function aplicarEncapada() {
            guardarEncapadaEnJSON();
            $('#modalEncapada').modal('hide');
        }

        function guardarEncapadaEnJSON() {
            document.getElementById('hiddenEncapadaDetalle').value = JSON.stringify(encapadaDetallada);
        }

        function cargarEncapadaEnModal() {
            try {
                const data = document.getElementById('hiddenEncapadaDetalle').value;
                if (data && data !== '[]') {
                    encapadaDetallada = JSON.parse(data);
                    contadorEncapada = encapadaDetallada.length > 0 ? Math.max(...encapadaDetallada.map(p => p.id)) : 0;
                    actualizarTablaEncapada();
                    actualizarTotalesEncapada();
                } else {
                    encapadaDetallada = [];
                    contadorEncapada = 0;
                    actualizarTablaEncapada();
                }
            } catch (error) {
                console.error('Error cargando encapada:', error);
                encapadaDetallada = [];
                contadorEncapada = 0;
            }
        }

        // ========================================
        // SISTEMA DE GESTIÓN DE HOSPEDAJE DETALLADA
        // ========================================

        let hospedajeDetallada = [];
        let contadorHospedaje = 0;

        function abrirModalHospedaje() {
            document.getElementById('nuevoHospedajeFecha').value = new Date().toISOString().split('T')[0];
            cargarHospedajeEnModal();
            $('#modalHospedaje').modal('show');
        }

        function agregarHospedaje() {
            const fechaRaw = document.getElementById('nuevoHospedajeFecha').value;
            const comprobante = document.getElementById('nuevoHospedajeComprobante').value.trim();
            const soles = parseFloat(document.getElementById('nuevoHospedajeSoles').value) || 0;
            const dolares = parseFloat(document.getElementById('nuevoHospedajeDolares').value) || 0;
            const observaciones = document.getElementById('nuevoHospedajeObservaciones').value.trim();

            const fechaValidada = validarFechaSegura(fechaRaw);
            if (!fechaValidada) {
                alert('Debe seleccionar una fecha válida (entre años 1753 y 9999)');
                document.getElementById('nuevoHospedajeFecha').focus();
                return;
            }

            if (soles <= 0 && dolares <= 0) {
                alert('Debe ingresar al menos un monto en soles o dólares');
                return;
            }

            const nuevoRegistro = {
                id: ++contadorHospedaje,
                // *** MAPPING CORRECTO PARA C# ***
                fechaComprobante: fechaValidada,        // C# espera: fechaComprobante
                numeroComprobante: comprobante || null, // C# espera: numeroComprobante
                montoSoles: soles,                      // C# espera: montoSoles
                montoDolares: dolares,                  // C# espera: montoDolares
                observaciones: observaciones || null,
                fechaCreacion: new Date().toISOString()
            };

            hospedajeDetallada.push(nuevoRegistro);
            actualizarTablaHospedaje();
            limpiarFormularioHospedaje();
            actualizarTotalesHospedaje();

            console.log(`✅ Hospedaje agregado con fecha validada: ${fechaValidada}`);
        }

        function eliminarHospedaje(id) {
            if (confirm('¿Está seguro de eliminar este registro?')) {
                const index = hospedajeDetallada.findIndex(p => p.id === id);
                if (index > -1) {
                    hospedajeDetallada.splice(index, 1);
                    actualizarTablaHospedaje();
                    actualizarTotalesHospedaje();
                }
            }
        }

        function editarHospedaje(id) {
            const registro = hospedajeDetallada.find(p => p.id === id);
            if (registro) {
                document.getElementById('nuevoHospedajeFecha').value = registro.fecha;
                document.getElementById('nuevoHospedajeComprobante').value = registro.comprobante || '';
                document.getElementById('nuevoHospedajeSoles').value = registro.soles || '';
                document.getElementById('nuevoHospedajeDolares').value = registro.dolares || '';
                document.getElementById('nuevoHospedajeObservaciones').value = registro.observaciones || '';
                eliminarHospedaje(id);
            }
        }

        function actualizarTablaHospedaje() {
            const tbody = document.getElementById('tablaHospedajeBody');
            const noMessage = document.getElementById('noHospedajeMessage');

            if (hospedajeDetallada.length === 0) {
                tbody.innerHTML = '';
                noMessage.style.display = 'block';
                document.getElementById('totalHospedajeRegistrados').textContent = '0 registros';
                return;
            }

            noMessage.style.display = 'none';

            tbody.innerHTML = hospedajeDetallada.map(registro => `
                <tr>
                    <td><span class="badge badge-secondary">${formatearFecha(registro.fechaComprobante)}</span></td>
                    <td>${registro.numeroComprobante || '<em class="text-muted">N/A</em>'}</td>
                    <td class="text-right">
                        ${registro.montoSoles > 0 ? `<span class="text-success">S/ ${registro.montoSoles.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
                    </td>
                    <td class="text-right">
                        ${registro.montoDolares > 0 ? `<span class="text-success">$ ${registro.montoDolares.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
                    </td>
                    <td>${registro.observaciones || '<em class="text-muted">N/A</em>'}</td>
                    <td>
                        <div class="btn-group btn-group-sm" role="group">
                            <button type="button" class="btn btn-outline-primary" onclick="editarHospedaje(${registro.id})" title="Editar">
                                <i class="fas fa-edit"></i>
                            </button>
                            <button type="button" class="btn btn-outline-danger" onclick="eliminarHospedaje(${registro.id})" title="Eliminar">
                                <i class="fas fa-trash"></i>
                            </button>
                        </div>
                    </td>
                </tr>
            `).join('');

            document.getElementById('totalHospedajeRegistrados').textContent = `${hospedajeDetallada.length} registro${hospedajeDetallada.length !== 1 ? 's' : ''}`;
        }

        function actualizarTotalesHospedaje() {
            const totalSoles = hospedajeDetallada.reduce((sum, registro) => sum + (registro.montoSoles || 0), 0);
            const totalDolares = hospedajeDetallada.reduce((sum, registro) => sum + (registro.montoDolares || 0), 0);

            document.getElementById('totalHospedajeSoles').textContent = `S/ ${totalSoles.toFixed(2)}`;
            document.getElementById('totalHospedajeDolares').textContent = `$ ${totalDolares.toFixed(2)}`;

            document.getElementById('txtHospedajeSoles').value = totalSoles > 0 ? totalSoles.toFixed(2) : '';
            document.getElementById('txtHospedajeDolares').value = totalDolares > 0 ? totalDolares.toFixed(2) : '';

            document.getElementById('contadorHospedaje').textContent = hospedajeDetallada.length;

            actualizarDescripcionHospedaje();
            calcularTotales();
        }

        function actualizarDescripcionHospedaje() {
            const descripcionField = document.getElementById('txtDescHospedaje');
            if (hospedajeDetallada.length === 0) {
                descripcionField.value = '';
                return;
            }
            descripcionField.value = `${hospedajeDetallada.length} registro${hospedajeDetallada.length !== 1 ? 's' : ''} de hospedaje`;
        }

        function limpiarFormularioHospedaje() {
            document.getElementById('nuevoHospedajeFecha').value = new Date().toISOString().split('T')[0];
            document.getElementById('nuevoHospedajeComprobante').value = '';
            document.getElementById('nuevoHospedajeSoles').value = '';
            document.getElementById('nuevoHospedajeDolares').value = '';
            document.getElementById('nuevoHospedajeObservaciones').value = '';
        }

        function aplicarHospedaje() {
            guardarHospedajeEnJSON();
            $('#modalHospedaje').modal('hide');
        }

        function guardarHospedajeEnJSON() {
            document.getElementById('hiddenHospedajeDetalle').value = JSON.stringify(hospedajeDetallada);
        }

        function cargarHospedajeEnModal() {
            try {
                const data = document.getElementById('hiddenHospedajeDetalle').value;
                if (data && data !== '[]') {
                    hospedajeDetallada = JSON.parse(data);
                    contadorHospedaje = hospedajeDetallada.length > 0 ? Math.max(...hospedajeDetallada.map(p => p.id)) : 0;
                    actualizarTablaHospedaje();
                    actualizarTotalesHospedaje();
                } else {
                    hospedajeDetallada = [];
                    contadorHospedaje = 0;
                    actualizarTablaHospedaje();
                }
            } catch (error) {
                console.error('Error cargando hospedaje:', error);
                hospedajeDetallada = [];
                contadorHospedaje = 0;
            }
        }

        // ========================================
        // SISTEMA DE GESTIÓN DE COMBUSTIBLE DETALLADA
        // ========================================

        let combustibleDetallada = [];
        let contadorCombustible = 0;

        function abrirModalCombustible() {
            document.getElementById('nuevoCombustibleFecha').value = new Date().toISOString().split('T')[0];
            cargarCombustibleEnModal();
            $('#modalCombustible').modal('show');
        }

        function agregarCombustible() {
            const fechaRaw = document.getElementById('nuevoCombustibleFecha').value;
            const comprobante = document.getElementById('nuevoCombustibleComprobante').value.trim();
            const soles = parseFloat(document.getElementById('nuevoCombustibleSoles').value) || 0;
            const dolares = parseFloat(document.getElementById('nuevoCombustibleDolares').value) || 0;
            const observaciones = document.getElementById('nuevoCombustibleObservaciones').value.trim();

            const fechaValidada = validarFechaSegura(fechaRaw);
            if (!fechaValidada) {
                alert('Debe seleccionar una fecha válida (entre años 1753 y 9999)');
                document.getElementById('nuevoCombustibleFecha').focus();
                return;
            }

            if (soles <= 0 && dolares <= 0) {
                alert('Debe ingresar al menos un monto en soles o dólares');
                return;
            }

            const nuevoRegistro = {
                id: ++contadorCombustible,
                // *** MAPPING CORRECTO PARA C# ***
                fechaComprobante: fechaValidada,        // C# espera: fechaComprobante
                numeroComprobante: comprobante || null, // C# espera: numeroComprobante
                montoSoles: soles,                      // C# espera: montoSoles
                montoDolares: dolares,                  // C# espera: montoDolares
                observaciones: observaciones || null,
                fechaCreacion: new Date().toISOString()
            };

            combustibleDetallada.push(nuevoRegistro);
            actualizarTablaCombustible();
            limpiarFormularioCombustible();
            actualizarTotalesCombustible();

            console.log(`✅ Combustible agregado con fecha validada: ${fechaValidada}`);
        }

        function eliminarCombustible(id) {
            if (confirm('¿Está seguro de eliminar este registro?')) {
                const index = combustibleDetallada.findIndex(p => p.id === id);
                if (index > -1) {
                    combustibleDetallada.splice(index, 1);
                    actualizarTablaCombustible();
                    actualizarTotalesCombustible();
                }
            }
        }

        function editarCombustible(id) {
            const registro = combustibleDetallada.find(p => p.id === id);
            if (registro) {
                document.getElementById('nuevoCombustibleFecha').value = registro.fecha;
                document.getElementById('nuevoCombustibleComprobante').value = registro.comprobante || '';
                document.getElementById('nuevoCombustibleSoles').value = registro.soles || '';
                document.getElementById('nuevoCombustibleDolares').value = registro.dolares || '';
                document.getElementById('nuevoCombustibleObservaciones').value = registro.observaciones || '';
                eliminarCombustible(id);
            }
        }

        function actualizarTablaCombustible() {
            const tbody = document.getElementById('tablaCombustibleBody');
            const noMessage = document.getElementById('noCombustibleMessage');

            if (combustibleDetallada.length === 0) {
                tbody.innerHTML = '';
                noMessage.style.display = 'block';
                document.getElementById('totalCombustibleRegistrados').textContent = '0 registros';
                return;
            }

            noMessage.style.display = 'none';

            tbody.innerHTML = combustibleDetallada.map(registro => `
                <tr>
                    <td><span class="badge badge-secondary">${formatearFecha(registro.fechaComprobante)}</span></td>
                    <td>${registro.numeroComprobante || '<em class="text-muted">N/A</em>'}</td>
                    <td class="text-right">
                       ${registro.montoSoles > 0 ? `<span class="text-success">S/ ${registro.montoSoles.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
                    </td>
                    <td class="text-right">
                        ${registro.montoDolares > 0 ? `<span class="text-success">${registro.montoDolares.toFixed(2)}</span>` : '<em class="text-muted">-</em>'}
                    </td>
                    <td>${registro.observaciones || '<em class="text-muted">N/A</em>'}</td>
                    <td>
                        <div class="btn-group btn-group-sm" role="group">
                            <button type="button" class="btn btn-outline-primary" onclick="editarCombustible(${registro.id})" title="Editar">
                                <i class="fas fa-edit"></i>
                            </button>
                            <button type="button" class="btn btn-outline-danger" onclick="eliminarCombustible(${registro.id})" title="Eliminar">
                                <i class="fas fa-trash"></i>
                            </button>
                        </div>
                    </td>
                </tr>
            `).join('');

            document.getElementById('totalCombustibleRegistrados').textContent = `${combustibleDetallada.length} registro${combustibleDetallada.length !== 1 ? 's' : ''}`;
        }

        function actualizarTotalesCombustible() {
            const totalSoles = combustibleDetallada.reduce((sum, registro) => sum + (registro.montoSoles || 0), 0);
            const totalDolares = combustibleDetallada.reduce((sum, registro) => sum + (registro.montoDolares || 0), 0);


            document.getElementById('totalCombustibleSoles').textContent = `S/ ${totalSoles.toFixed(2)}`;
            document.getElementById('totalCombustibleDolares').textContent = `$ ${totalDolares.toFixed(2)}`;

            document.getElementById('txtCombustibleSoles').value = totalSoles > 0 ? totalSoles.toFixed(2) : '';
            document.getElementById('txtCombustibleDolares').value = totalDolares > 0 ? totalDolares.toFixed(2) : '';

            document.getElementById('contadorCombustible').textContent = combustibleDetallada.length;

            actualizarDescripcionCombustible();
            calcularTotales();
        }

        function actualizarDescripcionCombustible() {
            const descripcionField = document.getElementById('txtDescCombustible');
            if (combustibleDetallada.length === 0) {
                descripcionField.value = '';
                return;
            }
            descripcionField.value = `${combustibleDetallada.length} registro${combustibleDetallada.length !== 1 ? 's' : ''} de combustible`;
        }

        function limpiarFormularioCombustible() {
            document.getElementById('nuevoCombustibleFecha').value = new Date().toISOString().split('T')[0];
            document.getElementById('nuevoCombustibleComprobante').value = '';
            document.getElementById('nuevoCombustibleSoles').value = '';
            document.getElementById('nuevoCombustibleDolares').value = '';
            document.getElementById('nuevoCombustibleObservaciones').value = '';
        }

        function aplicarCombustible() {
            guardarCombustibleEnJSON();
            $('#modalCombustible').modal('hide');
        }

        function guardarCombustibleEnJSON() {
            document.getElementById('hiddenCombustibleDetalle').value = JSON.stringify(combustibleDetallada);
        }

        function cargarCombustibleEnModal() {
            try {
                const data = document.getElementById('hiddenCombustibleDetalle').value;
                if (data && data !== '[]') {
                    combustibleDetallada = JSON.parse(data);
                    contadorCombustible = combustibleDetallada.length > 0 ? Math.max(...combustibleDetallada.map(p => p.id)) : 0;
                    actualizarTablaCombustible();
                    actualizarTotalesCombustible();
                } else {
                    combustibleDetallada = [];
                    contadorCombustible = 0;
                    actualizarTablaCombustible();
                }
            } catch (error) {
                console.error('Error cargando combustible:', error);
                combustibleDetallada = [];
                contadorCombustible = 0;
            }
        }




        // ========================================
        // FIN SISTEMA DE GESTIÓN DE PEAJES
        // ========================================





        // ========================================
        // FUNCIONES PARA NÚMERO DE ORDEN MANUAL (SOLO 6 NÚMEROS)
        // ========================================

        /**
         * Solo permitir números en el input
         */
        function soloNumeros(event) {
            try {
                const charCode = event.which ? event.which : event.keyCode;
                // Permitir solo números (48-57), backspace (8), delete (46), tab (9)
                if (charCode > 31 && (charCode < 48 || charCode > 57)) {
                    event.preventDefault();
                    return false;
                }
                return true;
            } catch (error) {
                console.error('Error en soloNumeros:', error);
                return true; // Permitir el carácter en caso de error
            }
        }

        /**
         * Formatear el número de orden mientras el usuario escribe
         */
        function formatearNumeroOrden(input) {
            try {
                // Validar que el input existe
                if (!input) {
                    console.warn('formatearNumeroOrden: input es null');
                    return;
                }

                // Solo números, eliminar todo lo demás
                let valor = (input.value || '').replace(/[^0-9]/g, '');

                // Limitar a 6 dígitos
                if (valor.length > 6) {
                    valor = valor.substring(0, 6);
                }

                // Actualizar el campo
                input.value = valor;

                // Limpiar mensaje de validación mientras escribe
                const mensajeDiv = encontrarContenedorMensajes();
                if (mensajeDiv && valor.length > 0) {
                    mensajeDiv.innerHTML = '';
                }

                // Validar automáticamente cuando llegue a 6 dígitos
                if (valor.length === 6) {
                    setTimeout(() => validarNumeroOrden(), 100);
                }

                console.log(`formatearNumeroOrden: valor actualizado a "${valor}"`);
            } catch (error) {
                console.error('Error en formatearNumeroOrden:', error);
            }
        }

        /**
         * Validar el número de orden cuando pierde el foco
         */
        function validarNumeroOrden() {
            try {
                const input = encontrarCampoNumeroOrden();
                const mensajeDiv = encontrarContenedorMensajes();

                if (!input) {
                    console.error('validarNumeroOrden: No se pudo encontrar el campo de número de orden');
                    return false;
                }

                const valor = (input.value || '').trim();

                // Limpiar mensaje anterior
                if (mensajeDiv) {
                    mensajeDiv.innerHTML = '';
                }

                // Limpiar clases CSS anteriores
                input.classList.remove('is-invalid', 'is-valid');

                if (!valor) {
                    mostrarMensajeValidacion('El número de orden de viaje es obligatorio', 'error');
                    input.classList.add('is-invalid');
                    return false;
                }

                if (!/^\d{6}$/.test(valor)) {
                    mostrarMensajeValidacion('Debe ser exactamente 6 números', 'error');
                    input.classList.add('is-invalid');
                    return false;
                }

                // Validar que no sea todo ceros
                if (valor === '000000') {
                    mostrarMensajeValidacion('El número no puede ser 000000', 'error');
                    input.classList.add('is-invalid');
                    return false;
                }

                // Si llegó hasta aquí, el formato es válido
                input.classList.add('is-valid');
                mostrarMensajeValidacion('✓ Formato válido', 'success');

                console.log(`validarNumeroOrden: validación exitosa para "${valor}"`);
                return true;

            } catch (error) {
                console.error('Error en validarNumeroOrden:', error);
                return false;
            }
        }

        /**
         * Mostrar mensaje de validación
         */
        function mostrarMensajeValidacion(mensaje, tipo) {
            try {
                const mensajeDiv = encontrarContenedorMensajes();

                if (!mensajeDiv) {
                    console.warn('mostrarMensajeValidacion: No se pudo encontrar/crear el contenedor de mensajes');
                    return;
                }

                let claseCSS = '';
                let icono = '';

                switch (tipo) {
                    case 'error':
                        claseCSS = 'text-danger';
                        icono = '❌ ';
                        break;
                    case 'warning':
                        claseCSS = 'text-warning';
                        icono = '⚠️ ';
                        break;
                    case 'success':
                        claseCSS = 'text-success';
                        icono = '✅ ';
                        break;
                    case 'info':
                        claseCSS = 'text-info';
                        icono = 'ℹ️ ';
                        break;
                    default:
                        claseCSS = 'text-muted';
                        icono = '';
                }

                mensajeDiv.innerHTML = `<small class="${claseCSS}"><strong>${icono}${mensaje}</strong></small>`;

                console.log(`mostrarMensajeValidacion: ${tipo} - ${mensaje}`);
            } catch (error) {
                console.error('Error en mostrarMensajeValidacion:', error);
            }
        }

        /**
         * Generar sugerencias de números de orden (6 dígitos)
         */
        function generarSugerenciasNumero() {
            const hoy = new Date();
            const año = hoy.getFullYear().toString().slice(-2); // Últimos 2 dígitos del año
            const mes = String(hoy.getMonth() + 1).padStart(2, '0');
            const dia = String(hoy.getDate()).padStart(2, '0');
            const hora = String(hoy.getHours()).padStart(2, '0');
            const minuto = String(hoy.getMinutes()).padStart(2, '0');

            const sugerencias = [
                `${año}${mes}${dia}`, // YYMMDD: 250104
                `${mes}${dia}${año}`, // MMDDYY: 010425  
                `${dia}${mes}${año}`, // DDMMYY: 040125
                `${año}${mes}${hora}`, // YYMDHH: 250104
                `${hora}${minuto}${año}`, // HHMMYY: 143025
                `1${año}${mes}${dia.charAt(0)}`, // 1 + YY + MM + D: 125010
            ];

            // Agregar algunos números secuenciales comunes
            sugerencias.push('100001', '200001', '300001');

            return sugerencias;
        }

        /**
         * Mostrar modal con sugerencias de números
         */
        function mostrarSugerenciasNumero() {
            const sugerencias = generarSugerenciasNumero();
            const sugerenciasHTML = sugerencias.map(num =>
                `<button type="button" class="btn btn-outline-primary btn-sm me-2 mb-2" 
                onclick="aplicarSugerencia('${num}')" 
                style="font-family: monospace; font-size: 1.1em;">${num}</button>`
            ).join('');

            const modalHTML = `
        <div class="modal fade" id="modalSugerencias" tabindex="-1">
            <div class="modal-dialog">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title">💡 Sugerencias de Números (6 dígitos)</h5>
                        <button type="button" class="close" data-dismiss="modal">
                            <span>&times;</span>
                        </button>
                    </div>
                    <div class="modal-body">
                        <p><strong>Formatos basados en la fecha de hoy:</strong></p>
                        ${sugerenciasHTML}
                        <hr>
                        <small class="text-muted">
                            <strong>Formatos disponibles:</strong><br>
                            • YYMMDD: Año, mes, día (250104)<br>
                            • MMDDYY: Mes, día, año (010425)<br>
                            • DDMMYY: Día, mes, año (040125)<br>
                            • Secuenciales: 100001, 200001, etc.
                        </small>
                        <hr>
                        <div class="form-group">
                            <label for="numeroPersonalizado">O ingrese un número personalizado:</label>
                            <input type="text" class="form-control" id="numeroPersonalizado" 
                                   placeholder="123456" maxlength="6" 
                                   onkeypress="return soloNumeros(event)"
                                   style="text-align: center; font-family: monospace; font-size: 1.2em;">
                            <button type="button" class="btn btn-success btn-sm mt-2" 
                                    onclick="aplicarNumeroPersonalizado()">
                                Usar este número
                            </button>
                        </div>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-dismiss="modal">Cerrar</button>
                    </div>
                </div>
            </div>
        </div>
    `;

            // Agregar modal al DOM si no existe
            if (!document.getElementById('modalSugerencias')) {
                document.body.insertAdjacentHTML('beforeend', modalHTML);
            }

            // Mostrar modal
            $('#modalSugerencias').modal('show');
        }

        /**
         * Aplicar sugerencia seleccionada
         */
        function aplicarSugerencia(numeroSugerido) {
            try {
                const input = encontrarCampoNumeroOrden();

                if (!input) {
                    console.error('aplicarSugerencia: No se pudo encontrar el campo de número de orden');
                    alert('Error: No se pudo encontrar el campo de número de orden');
                    return;
                }

                input.value = numeroSugerido;

                // Validar el número aplicado
                validarNumeroOrden();

                // Cerrar modal si existe
                if (typeof $ !== 'undefined' && $('#modalSugerencias').length > 0) {
                    $('#modalSugerencias').modal('hide');
                }

                // Enfocar el campo
                input.focus();

                console.log(`aplicarSugerencia: aplicado "${numeroSugerido}"`);
            } catch (error) {
                console.error('Error en aplicarSugerencia:', error);
                alert('Error al aplicar la sugerencia: ' + error.message);
            }
        }


        /**
        * Función auxiliar para encontrar el campo de número de orden de forma segura
        */
        function encontrarCampoNumeroOrden() {
            // Intentar múltiples formas de encontrar el elemento
            let campo = document.getElementById('txtNumeroOrdenViaje');

            if (!campo) {
                // Intentar con name attribute
                const campos = document.getElementsByName('txtNumeroOrdenViaje');
                if (campos.length > 0) {
                    campo = campos[0];
                }
            }

            if (!campo) {
                // Intentar con selector más específico
                campo = document.querySelector('input[id*="txtNumeroOrdenViaje"]');
            }

            if (!campo) {
                // Intentar con selector por placeholder o tipo
                campo = document.querySelector('input[placeholder*="orden"], input[placeholder*="número"]');
            }

            return campo;
        }


        /**
        * Función auxiliar para encontrar el contenedor de mensajes de forma segura
        */
        function encontrarContenedorMensajes() {
            let contenedor = document.getElementById('mensajeValidacionOrden');

            if (!contenedor) {
                // Crear el contenedor si no existe
                const campo = encontrarCampoNumeroOrden();
                if (campo && campo.parentNode) {
                    contenedor = document.createElement('div');
                    contenedor.id = 'mensajeValidacionOrden';
                    contenedor.className = 'mensaje-validacion-orden mt-1';
                    campo.parentNode.appendChild(contenedor);
                }
            }

            return contenedor;
        }

        /**
        * Validación antes de guardar (VERSIÓN SEGURA)
        */
        function validarFormularioAntesDeLGuardar() {
            try {
                const numeroValido = validarNumeroOrden();

                if (!numeroValido) {
                    alert('Por favor, ingrese un número de orden de viaje válido (6 números) antes de guardar.');
                    const campo = encontrarCampoNumeroOrden();
                    if (campo) {
                        campo.focus();
                    }
                    return false;
                }

                return true;
            } catch (error) {
                console.error('Error en validarFormularioAntesDeLGuardar:', error);
                return false;
            }
        }


        /**
        * Configurar eventos del campo número de orden de forma segura
        */
        function configurarCampoNumeroOrden() {
            try {
                const campo = encontrarCampoNumeroOrden();

                if (!campo) {
                    console.warn('configurarCampoNumeroOrden: Campo no encontrado, reintentando en 500ms...');
                    setTimeout(configurarCampoNumeroOrden, 500);
                    return;
                }

                console.log('configurarCampoNumeroOrden: Campo encontrado, configurando eventos...');

                // Limpiar eventos anteriores para evitar duplicados
                campo.removeEventListener('keypress', soloNumeros);
                campo.removeEventListener('input', function () { formatearNumeroOrden(campo); });
                campo.removeEventListener('blur', validarNumeroOrden);

                // Agregar eventos
                campo.addEventListener('keypress', soloNumeros);
                campo.addEventListener('input', function () { formatearNumeroOrden(campo); });
                campo.addEventListener('blur', validarNumeroOrden);

                // Configurar atributos
                campo.setAttribute('maxlength', '6');
                campo.setAttribute('placeholder', 'Ej: 280730');
                campo.style.textAlign = 'center';
                campo.style.fontFamily = 'monospace';
                campo.style.fontSize = '1.1em';
                campo.style.letterSpacing = '1px';

                // Crear contenedor de mensajes si no existe
                encontrarContenedorMensajes();

                console.log('configurarCampoNumeroOrden: Configuración completada exitosamente');

                // Si viene desde despacho, mostrar mensaje especial
                const urlParams = new URLSearchParams(window.location.search);
                if (urlParams.get('origen') === 'despacho') {
                    mostrarMensajeValidacion('Campo configurado desde despacho', 'info');
                }

            } catch (error) {
                console.error('Error en configurarCampoNumeroOrden:', error);
            }
        }

        /**
        * Detectar si viene desde despacho y aplicar configuraciones especiales
        */
        function manejarVenidaDesdeDespacho() {
            try {
                const urlParams = new URLSearchParams(window.location.search);

                if (urlParams.get('origen') === 'despacho') {
                    console.log('🚛 Detectado: Viene desde despacho');

                    // Configurar campo con delay extra para asegurar que el DOM esté listo
                    setTimeout(() => {
                        configurarCampoNumeroOrden();

                        // Mostrar información adicional
                        const campo = encontrarCampoNumeroOrden();
                        if (campo) {
                            // Agregar tooltip o ayuda visual
                            campo.title = 'Campo configurado automáticamente desde despacho programado';
                            campo.classList.add('campo-desde-despacho');
                        }

                        console.log('✅ Configuración especial para despacho aplicada');
                    }, 1000);

                    return true;
                }

                return false;
            } catch (error) {
                console.error('Error en manejarVenidaDesdeDespacho:', error);
                return false;
            }
        }

        /**
         * Aplicar número personalizado del modal
         */
        function aplicarNumeroPersonalizado() {
            const inputPersonalizado = document.getElementById('numeroPersonalizado');
            const numero = inputPersonalizado.value.trim();

            if (!/^\d{6}$/.test(numero)) {
                alert('Debe ingresar exactamente 6 números');
                inputPersonalizado.focus();
                return;
            }

            if (numero === '000000') {
                alert('El número no puede ser 000000');
                inputPersonalizado.focus();
                return;
            }

            aplicarSugerencia(numero);
        }

        /**
         * Validación antes de guardar
         */
        function validarFormularioAntesDeLGuardar() {
            const numeroValido = validarNumeroOrden();

            if (!numeroValido) {
                alert('Por favor, ingrese un número de orden de viaje válido (6 números) antes de guardar.');
                document.getElementById('txtNumeroOrdenViaje').focus();
                return false;
            }

            return true;
        }


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

            console.log("📋 Transfiriendo datos del cliente (incluyendo plantas):", clienteInfo);

            // Establecer tipo de transporte
            const tipoRadio = clienteInfo.esInternacional ? '1' : '0';
            $(`input[name="descarga_${numeroLiquidacion}_${numeroSubTramo}_tipo"][value="${tipoRadio}"]`).prop('checked', true);

            // Activar/desactivar campos
            toggleClienteTipoOperacionMejorada(numeroLiquidacion, numeroSubTramo, 'descarga');

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

                // *** NUEVO: Transferir planta de descarga si existe ***
                if (clienteInfo.idPlantaDescarga) {
                    $(`#descarga_${numeroLiquidacion}_${numeroSubTramo}_planta`).val(clienteInfo.idPlantaDescarga).trigger('change');
                }

                console.log("✅ Datos del cliente transferidos (incluyendo plantas)");
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

        function debugSistemaCompleto() {
            console.log("=== DEBUG SISTEMA COMPLETO ===");
            console.log("Productos cargados globalmente:", productosCargadosGlobal);
            console.log("Productos por liquidación:", productosCargadosPorLiquidacion);
            console.log("Plantas de carga disponibles:", plantasCarga.length);
            console.log("Plantas de descarga disponibles:", plantasDescarga.length);
            console.log("Clientes disponibles:", clientes.length);
            console.log("Facturas disponibles:", facturas.length);
            console.log("CPICs disponibles:", cpics.length);

            // Analizar plantas por cliente
            const plantasPorCliente = {};
            plantasCarga.forEach(p => {
                if (!plantasPorCliente[p.idCliente]) plantasPorCliente[p.idCliente] = { carga: [], descarga: [] };
                plantasPorCliente[p.idCliente].carga.push(p.nombre);
            });
            plantasDescarga.forEach(p => {
                if (!plantasPorCliente[p.idCliente]) plantasPorCliente[p.idCliente] = { carga: [], descarga: [] };
                plantasPorCliente[p.idCliente].descarga.push(p.nombre);
            });

            console.log("Plantas agrupadas por cliente:", plantasPorCliente);

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
            let necesitaGuias = false;

            // Limpiar sección de operaciones
            operacionesSection.empty();

            switch (operacion) {
                case 'TRANSITO_VACIO':
                    textoOperacion = '🚛 Tránsito Vacío';
                    claseOperacion = 'badge-transito-vacio';
                    operacionesSection.hide();
                    necesitaGuias = false;
                    break;

                case 'TRANSITO_CARGA':
                    textoOperacion = '🚚 Tránsito con Carga';
                    claseOperacion = 'badge-transito-carga';
                    operacionesSection.hide();
                    necesitaGuias = true;
                    break;

                case 'SOLO_CARGA':
                    textoOperacion = '📦 Solo Carga';
                    claseOperacion = 'badge-solo-carga';
                    generarSeccionSoloCarga(numeroLiquidacion, numeroSubTramo);
                    necesitaGuias = false;
                    break;

                case 'SOLO_DESCARGA':
                    textoOperacion = '📤 Solo Descarga';
                    claseOperacion = 'badge-solo-descarga';
                    generarSeccionSoloDescarga(numeroLiquidacion, numeroSubTramo);
                    necesitaGuias = true;
                    break;

                case 'DESCARGA_Y_CARGA':
                    textoOperacion = '🔄 Descarga y Carga';
                    claseOperacion = 'badge-descarga-carga';
                    generarSeccionDescargaYCarga(numeroLiquidacion, numeroSubTramo);
                    necesitaGuias = true;
                    break;

                case 'PARADA_OPERATIVA':
                    textoOperacion = '⏸️ Parada Operativa';
                    claseOperacion = 'badge-parada';
                    generarSeccionParadaOperativa(numeroLiquidacion, numeroSubTramo);
                    necesitaGuias = false;
                    break;

                default:
                    operacionesSection.hide();
                    necesitaGuias = false;
            }

            badge.text(textoOperacion).attr('class', `operacion-badge ms-2 ${claseOperacion}`);

            if (necesitaGuias) {
                guiasSection.show();
            } else {
                guiasSection.hide();
            }

            // Auto-llenar si es operación de descarga
            if (operacion === 'SOLO_DESCARGA' || operacion === 'DESCARGA_Y_CARGA') {
                setTimeout(() => {
                    autoLlenarProductosDescarga(numeroLiquidacion, numeroSubTramo);
                }, 800);
            }
        }

        // ========================================
        // FUNCIONES PARA GENERAR SECCIONES DE OPERACIÓN
        // ========================================
        function generarSeccionSoloCarga(numeroLiquidacion, numeroSubTramo) {
            const contenido = generarSeccionClienteMejorada(numeroLiquidacion, numeroSubTramo, 'carga');
            const operacionesSection = $(`#operaciones_section_${numeroLiquidacion}_${numeroSubTramo}`);
            operacionesSection.html(contenido).show();
            inicializarSelect2OperacionMejorada(numeroLiquidacion, numeroSubTramo, 'carga');
        }

        function generarSeccionSoloDescarga(numeroLiquidacion, numeroSubTramo) {
            const contenido = `
                <div id="indicador_productos_transferidos_${numeroLiquidacion}_${numeroSubTramo}"></div>
                ${generarSeccionClienteMejorada(numeroLiquidacion, numeroSubTramo, 'descarga')}
            `;
            const operacionesSection = $(`#operaciones_section_${numeroLiquidacion}_${numeroSubTramo}`);
            operacionesSection.html(contenido).show();
            inicializarSelect2OperacionMejorada(numeroLiquidacion, numeroSubTramo, 'descarga');
        }

        function generarSeccionDescargaYCarga(numeroLiquidacion, numeroSubTramo) {
            const contenido = `
                <div id="indicador_productos_transferidos_${numeroLiquidacion}_${numeroSubTramo}"></div>
                <h6>Descarga</h6>
                ${generarSeccionClienteMejorada(numeroLiquidacion, numeroSubTramo, 'descarga')}
                <h6>Carga</h6>
                ${generarSeccionClienteMejorada(numeroLiquidacion, numeroSubTramo, 'carga')}
            `;
            const operacionesSection = $(`#operaciones_section_${numeroLiquidacion}_${numeroSubTramo}`);
            operacionesSection.html(contenido).show();
            inicializarSelect2OperacionMejorada(numeroLiquidacion, numeroSubTramo, 'descarga');
            inicializarSelect2OperacionMejorada(numeroLiquidacion, numeroSubTramo, 'carga');
        }

        function generarSeccionParadaOperativa(numeroLiquidacion, numeroSubTramo) {
            const contenido = `
                <div class="alert alert-info">
                    <strong>Información:</strong> Esta es una parada sin operaciones de carga o descarga.
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
            `;
            const operacionesSection = $(`#operaciones_section_${numeroLiquidacion}_${numeroSubTramo}`);
            operacionesSection.html(contenido).show();
        }

        function generarSeccionClienteMejorada(numeroLiquidacion, numeroSubTramo, tipoOperacion) {
            return `
                <div class="operacion-subsection">
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

                    <div class="row mb-3">
                        <div class="col-md-3">
                            <label for="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cliente">Cliente:</label>
                            <select id="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cliente" 
                                    class="form-control" required
                                    onchange="onClienteChangeConPlantas(${numeroLiquidacion}, ${numeroSubTramo}, '${tipoOperacion}')">
                                <option value="">Seleccione un cliente</option>
                                ${clientes.map(c => `<option value="${c.idCliente}">${c.nombre}</option>`).join('')}
                            </select>
                        </div>
                        <div class="col-md-3">
                            <label for="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_factura">Factura:</label>
                            <select id="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_factura" class="form-control" required>
                                <option value="">Primero seleccione un cliente</option>
                            </select>
                        </div>
                        <div class="col-md-3">
                            <label for="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cpic">CPIC:</label>
                            <select id="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cpic" class="form-control" disabled>
                                <option value="">No requerido para nacional</option>
                                ${cpics.map(c => `<option value="${c.idCPIC}">${c.numeroCPIC}</option>`).join('')}
                            </select>
                        </div>
                        <div class="col-md-3">
                            <label for="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_planta">
                                ${tipoOperacion === 'carga' ? 'Planta de Carga' : 'Planta de Descarga'}:
                            </label>
                            <select id="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_planta" class="form-control" required>
                                <option value="">Primero seleccione un cliente</option>
                            </select>
                        </div>
                    </div>

                    <div class="mb-3">
                        <label>Productos ${tipoOperacion === 'carga' ? 'a Cargar' : 'a Descargar'}:</label>
                        <div class="alert alert-info" id="info_productos_${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}">
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
                </div>
            `;
        }

        function filtrarProductosPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, idCliente) {
            const productosSelects = $(`#productos_${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo} .producto-select`);
            productosSelects.each(function () {
                const $select = $(this);
                const valorActual = $select.val();
                $select.empty().append('<option value="">Seleccione un producto</option>');

                if (idCliente) {
                    const productosDelCliente = productos.filter(p => p.idCliente == idCliente);
                    productosDelCliente.forEach(producto => {
                        const selected = valorActual == producto.idProducto ? 'selected' : '';
                        $select.append(`<option value="${producto.idProducto}" ${selected}>${producto.nombre}</option>`);
                    });
                } else {
                    productos.forEach(producto => {
                        const selected = valorActual == producto.idProducto ? 'selected' : '';
                        $select.append(`<option value="${producto.idProducto}" ${selected}>${producto.nombre}</option>`);
                    });
                }
            });
        }

        function filtrarFacturasPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, idCliente) {
            const facturaSelect = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_factura`);
            const valorActual = facturaSelect.val();
            facturaSelect.empty().append('<option value="">Seleccione una factura</option>');

            if (idCliente) {
                const facturasDelCliente = facturas.filter(f => f.idCliente == idCliente);
                facturasDelCliente.forEach(factura => {
                    const selected = valorActual == factura.idFactura ? 'selected' : '';
                    facturaSelect.append(`<option value="${factura.idFactura}" ${selected}>${factura.numeroFactura}</option>`);
                });
            }
        }

        function filtrarPlantasCargaPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, idCliente) {
            const plantaSelect = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_planta`);
            plantaSelect.empty().append('<option value="">Seleccione una planta de carga</option>');

            if (idCliente) {
                const plantasDelCliente = plantasCarga.filter(p => p.idCliente == idCliente);
                plantasDelCliente.forEach(planta => {
                    plantaSelect.append(`<option value="${planta.idPlantaCarga}">${planta.nombre}</option>`);
                });
            }
        }

        function filtrarPlantasDescargaPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, idCliente) {
            const plantaSelect = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_planta`);
            plantaSelect.empty().append('<option value="">Seleccione una planta de descarga</option>');

            if (idCliente) {
                const plantasDelCliente = plantasDescarga.filter(p => p.idCliente == idCliente);
                plantasDelCliente.forEach(planta => {
                    plantaSelect.append(`<option value="${planta.idPlantaDescarga}">${planta.nombre}</option>`);
                });
            }
        }

        function onClienteChangeConPlantas(numeroLiquidacion, numeroSubTramo, tipoOperacion) {
            const idCliente = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cliente`).val();

            if (idCliente) {
                filtrarProductosPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, idCliente);

                if (tipoOperacion === 'carga') {
                    filtrarPlantasCargaPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, idCliente);
                } else if (tipoOperacion === 'descarga') {
                    filtrarPlantasDescargaPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, idCliente);
                }

                const esInternacional = $(`input[name="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_tipo"]:checked`).val() === '1';
                if (!esInternacional) {
                    filtrarFacturasPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, idCliente);
                }
            } else {
                filtrarProductosPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, null);
                filtrarFacturasPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, null);
            }
        }

        function toggleClienteTipoOperacionMejorada(numeroLiquidacion, numeroSubTramo, tipoOperacion) {
            const esInternacional = $(`input[name="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_tipo"]:checked`).val() === '1';
            const cpicSelect = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cpic`);
            const facturaSelect = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_factura`);

            if (esInternacional) {
                cpicSelect.prop('disabled', false).prop('required', true);
                facturaSelect.prop('disabled', true).prop('required', false).val('');
            } else {
                cpicSelect.prop('disabled', true).prop('required', false).val('');
                facturaSelect.prop('disabled', false).prop('required', true);
                const idCliente = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cliente`).val();
                if (idCliente) {
                    filtrarFacturasPorCliente(numeroLiquidacion, numeroSubTramo, tipoOperacion, idCliente);
                }
            }
        }

        function agregarProductoAOperacionMejorada(numeroLiquidacion, numeroSubTramo, tipoOperacion) {
            const idCliente = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cliente`).val();
            if (!idCliente) {
                alert('Debe seleccionar primero un cliente para ver sus productos disponibles.');
                return;
            }

            const productosContainer = $(`#productos_${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}`);
            const numeroProducto = productosContainer.children().length + 1;
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
                                ${productosDelCliente.map(p => `<option value="${p.idProducto}">${p.nombre}</option>`).join('')}
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

            $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_planta`).select2({
                placeholder: tipoOperacion === 'carga' ? "Buscar planta de carga..." : "Buscar planta de descarga...",
                allowClear: true,
                width: '100%'
            });

            toggleClienteTipoOperacionMejorada(numeroLiquidacion, numeroSubTramo, tipoOperacion);
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
            }
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
                idPlantaCarga: null,
                idPlantaDescarga: null,
                productos: []
            };

            if (tipoOperacion === 'carga') {
                operacion.idPlantaCarga = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_planta`).val() || null;
            } else if (tipoOperacion === 'descarga') {
                operacion.idPlantaDescarga = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_planta`).val() || null;
            }

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
            // ========================================
            // CALCULAR INGRESOS
            // ========================================
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

            // ========================================
            // CALCULAR GASTOS
            // ========================================
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
            //



            // 2. FUNCIÓN UTILITARIA PARA FECHA ACTUAL SEGURA
            function obtenerFechaActualSegura() {
                const hoy = new Date();
                const year = hoy.getFullYear();
                const month = String(hoy.getMonth() + 1).padStart(2, '0');
                const day = String(hoy.getDate()).padStart(2, '0');
                return `${year}-${month}-${day}`;
            }


            //
            // ========================================
            // CALCULAR DESCUENTOS Y REINTEGROS
            // ========================================
            let totalDescuentosSoles = 0;
            let totalDescuentosDolares = 0;
            let totalReintegrosSoles = 0;
            let totalReintegrosDolares = 0;

            // Descuentos
            $('.descuento-soles').each(function () {
                const valor = parseFloat($(this).val()) || 0;
                totalDescuentosSoles += valor;
            });

            $('.descuento-dolares').each(function () {
                const valor = parseFloat($(this).val()) || 0;
                totalDescuentosDolares += valor;
            });

            // Reintegros
            $('.reintegro-soles').each(function () {
                const valor = parseFloat($(this).val()) || 0;
                totalReintegrosSoles += valor;
            });

            $('.reintegro-dolares').each(function () {
                const valor = parseFloat($(this).val()) || 0;
                totalReintegrosDolares += valor;
            });

            // ========================================
            // CALCULAR DIFERENCIA FINAL
            // ========================================
            // Fórmula: Ingresos - Gastos - Descuentos + Reintegros
            const diferenciaSoles = totalIngresosSoles - totalGastosSoles - totalDescuentosSoles + totalReintegrosSoles;
            const diferenciaDolares = totalIngresosDolares - totalGastosDolares - totalDescuentosDolares + totalReintegrosDolares;

            // ========================================
            // ACTUALIZAR INTERFAZ
            // ========================================
            $('#totalIngresosSoles').text(totalIngresosSoles.toFixed(2));
            $('#totalIngresosDolares').text(totalIngresosDolares.toFixed(2));
            $('#totalGastosSoles').text(totalGastosSoles.toFixed(2));
            $('#totalGastosDolares').text(totalGastosDolares.toFixed(2));
            $('#diferenciaSaldoSoles').text(diferenciaSoles.toFixed(2));
            $('#diferenciaSaldoDolares').text(diferenciaDolares.toFixed(2));

            // ========================================
            // APLICAR COLORES SEGÚN EL SALDO
            // ========================================
            // Verde si es positivo, rojo si es negativo, negro si es cero
            const colorSoles = diferenciaSoles > 0 ? 'green' : (diferenciaSoles < 0 ? 'red' : 'black');
            const colorDolares = diferenciaDolares > 0 ? 'green' : (diferenciaDolares < 0 ? 'red' : 'black');

            $('#diferenciaSaldoSoles').css('color', colorSoles);
            $('#diferenciaSaldoDolares').css('color', colorDolares);

            // ========================================
            // GUARDAR DATOS EN CAMPOS OCULTOS
            // ========================================
            guardarGastosAdicionalesJSON();
            guardarIngresosAdicionalesJSON();

            // ========================================
            // LOG PARA DEBUGGING
            // ========================================
            console.log(`💰 Totales calculados:
    Ingresos: S/${totalIngresosSoles.toFixed(2)} - $${totalIngresosDolares.toFixed(2)}
    Gastos: S/${totalGastosSoles.toFixed(2)} - $${totalGastosDolares.toFixed(2)}
    Descuentos: S/${totalDescuentosSoles.toFixed(2)} - $${totalDescuentosDolares.toFixed(2)}
    Reintegros: S/${totalReintegrosSoles.toFixed(2)} - $${totalReintegrosDolares.toFixed(2)}
    Saldo Final: S/${diferenciaSoles.toFixed(2)} - $${diferenciaDolares.toFixed(2)}`);
        }

        function guardarGastosAdicionalesJSON() {
            const gastosAdicionales = [];

            $('#gastosAdicionalesBody tr').each(function () {
                const id = $(this).attr('id').split('_')[2];

                // USAR NOMBRES EXACTOS QUE ESPERA C#
                const categoria = $(`input[name="txtGastoCategoria_${id}"]`).val();
                const descripcion = $(`input[name="txtDescGasto_${id}"]`).val();
                const soles = parseFloat($(`input[name="txtGastoSoles_${id}"]`).val()) || 0;
                const dolares = parseFloat($(`input[name="txtGastoDolares_${id}"]`).val()) || 0;

                if (categoria || descripcion || soles > 0 || dolares > 0) {
                    gastosAdicionales.push({
                        // MAPPING CORRECTO PARA C# clase GastoAdicional
                        categoria: categoria,           // C# espera: categoria
                        nombreCategoria: categoria,     // C# espera: nombreCategoria
                        soles: soles,                  // C# espera: soles
                        dolares: dolares,              // C# espera: dolares
                        descripcion: descripcion       // C# espera: descripcion
                    });
                }
            });

            $('#hiddenGastosAdicionales').val(JSON.stringify(gastosAdicionales));

            // DEBUG
            console.log('💰 Gastos adicionales guardados:', gastosAdicionales);
            console.log('📝 JSON enviado a C#:', JSON.stringify(gastosAdicionales));
        }

        function guardarIngresosAdicionalesJSON() {
            const ingresosAdicionales = [];

            $('#ingresosAdicionalesBody tr').each(function () {
                const id = $(this).attr('id').split('_')[2];

                // USAR NOMBRES EXACTOS QUE ESPERA C#
                const categoria = $(`input[name="txtIngresoCategoria_${id}"]`).val();
                const descripcion = $(`input[name="txtDescIngreso_${id}"]`).val();
                const soles = parseFloat($(`input[name="txtIngresoSoles_${id}"]`).val()) || 0;
                const dolares = parseFloat($(`input[name="txtIngresoDolares_${id}"]`).val()) || 0;

                if (categoria || descripcion || soles > 0 || dolares > 0) {
                    ingresosAdicionales.push({
                        // MAPPING CORRECTO PARA C# clase IngresoAdicional
                        categoria: categoria,           // C# espera: categoria
                        nombreCategoria: categoria,     // C# espera: nombreCategoria
                        soles: soles,                  // C# espera: soles
                        dolares: dolares,              // C# espera: dolares
                        descripcion: descripcion       // C# espera: descripcion
                    });
                }
            });

            $('#hiddenIngresosAdicionales').val(JSON.stringify(ingresosAdicionales));

            // DEBUG
            console.log('💵 Ingresos adicionales guardados:', ingresosAdicionales);
            console.log('📝 JSON enviado a C#:', JSON.stringify(ingresosAdicionales));
        }
        /**
        * Inicializar Select2 para estaciones de peaje
        */
        /**
 * SOLUCIÓN DIRECTA - Reemplaza tu función actual
 */


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

            // Inicializar sistema de peajes
            cargarPeajesEnModal();

            // Inicializar todas las categorías de gastos
            cargarAlimentacionEnModal();
            cargarApoyoSeguridadEnModal();
            cargarReparacionesEnModal();
            cargarMovilidadEnModal();
            cargarEncapadaEnModal();
            cargarHospedajeEnModal();
            cargarCombustibleEnModal();

            // Configurar eventos del modal de peajes
            $('#modalPeajes').on('shown.bs.modal', function () {
                document.getElementById('nuevoPeajeEstacion').focus();
            });

            document.getElementById('nuevoPeajeEstacion').addEventListener('keypress', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    agregarPeaje();
                }
            });

            // Configurar eventos de tecla Enter para todas las categorías
            document.getElementById('nuevoAlimentacionFecha').addEventListener('keypress', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    agregarAlimentacion();
                }
            });

            document.getElementById('nuevoApoyoSeguridadFecha').addEventListener('keypress', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    agregarApoyoSeguridad();
                }
            });

            document.getElementById('nuevoReparacionesFecha').addEventListener('keypress', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    agregarReparaciones();
                }
            });

            document.getElementById('nuevoMovilidadFecha').addEventListener('keypress', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    agregarMovilidad();
                }
            });

            document.getElementById('nuevoEncapadaFecha').addEventListener('keypress', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    agregarEncapada();
                }
            });

            document.getElementById('nuevoHospedajeFecha').addEventListener('keypress', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    agregarHospedaje();
                }
            });

            document.getElementById('nuevoCombustibleFecha').addEventListener('keypress', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    agregarCombustible();
                }
            });

            // Función de debug disponible globalmente
            window.debugSistemaCompleto = debugSistemaCompleto;
            console.log("Sistema de auto-llenado inicializado correctamente");
            console.log("Sistema de plantas integrado correctamente");
            console.log("Sistema de peajes detallados inicializado correctamente");
            console.log("Sistema completo de gastos detallados inicializado correctamente");
        });

        // ========================================
        // SISTEMA DE VALIDACIÓN COMPLETA
        // ========================================

        function validarFormularioCompleto() {
            try {
                // PASO 1: Guardar datos adicionales ANTES de validar
                console.log('Guardando datos adicionales antes de validar...');

                if (typeof guardarIngresosAdicionalesJSON === 'function') {
                    guardarIngresosAdicionalesJSON();
                }
                if (typeof guardarGastosAdicionalesJSON === 'function') {
                    guardarGastosAdicionalesJSON();
                }

                // Verificar que se guardaron
                const ingresosData = $('#hiddenIngresosAdicionales').val();
                const gastosData = $('#hiddenGastosAdicionales').val();
                console.log('Ingresos adicionales:', ingresosData ? JSON.parse(ingresosData).length + ' registros' : 'Sin datos');
                console.log('Gastos adicionales:', gastosData ? JSON.parse(gastosData).length + ' registros' : 'Sin datos');

            } catch (error) {
                console.error('Error guardando datos adicionales:', error);
                alert('Error preparando datos adicionales: ' + error.message);
                return false;
            }

            // PASO 2: Validaciones existentes
            const errores = [];

            // VALIDAR PESTAÑA 1: Datos del Viaje
            const erroresPestana1 = validarDatosViaje();
            if (erroresPestana1.length > 0) {
                errores.push({
                    pestana: 'Datos del Viaje',
                    campos: erroresPestana1
                });
            }

            // VALIDAR PESTAÑA 2: Trazabilidad  
            const erroresPestana2 = validarTrazabilidad();
            if (erroresPestana2.length > 0) {
                errores.push({
                    pestana: 'Trazabilidad',
                    campos: erroresPestana2
                });
            }

            // VALIDAR PESTAÑA 3: Resumen Financiero
            const erroresPestana3 = validarResumenFinanciero();
            if (erroresPestana3.length > 0) {
                errores.push({
                    pestana: 'Resumen Financiero',
                    campos: erroresPestana3
                });
            }

            if (errores.length > 0) {
                mostrarErroresValidacion(errores);
                return false;
            }

            return true;
        }

        function validarDatosViaje() {
            const errores = [];

            // Número de orden
            const numeroOrden = document.getElementById('txtNumeroOrdenViaje').value.trim();
            if (!numeroOrden) {
                errores.push('Número de Orden de Viaje es obligatorio');
            } else if (!/^\d{6}$/.test(numeroOrden)) {
                errores.push('Número de Orden debe ser exactamente 6 números');
            }

            // Conductor
            const conductor = document.getElementById('ddlConductor').value;
            if (!conductor) {
                errores.push('Conductor es obligatorio');
            }

            // Fechas y horas
            if (!document.getElementById('txtFechaSalida').value) {
                errores.push('Fecha de Salida es obligatoria');
            }
            if (!document.getElementById('txtHoraSalida').value) {
                errores.push('Hora de Salida es obligatoria');
            }
            if (!document.getElementById('txtFechaLlegada').value) {
                errores.push('Fecha de Llegada es obligatoria');
            }
            if (!document.getElementById('txtHoraLlegada').value) {
                errores.push('Hora de Llegada es obligatoria');
            }

            // Vehículos
            if (!document.getElementById('ddlPlacaTracto').value) {
                errores.push('Placa Tracto es obligatoria');
            }
            if (!document.getElementById('ddlPlacaCarreta').value) {
                errores.push('Placa Carreta es obligatoria');
            }

            // Validar fechas lógicas
            const fechaSalida = new Date(document.getElementById('txtFechaSalida').value + 'T' + document.getElementById('txtHoraSalida').value);
            const fechaLlegada = new Date(document.getElementById('txtFechaLlegada').value + 'T' + document.getElementById('txtHoraLlegada').value);

            if (fechaSalida && fechaLlegada && fechaSalida >= fechaLlegada) {
                errores.push('La fecha/hora de llegada debe ser posterior a la de salida');
            }

            return errores;
        }

        function validarTrazabilidad() {
            const errores = [];

            // Verificar que existe al menos una liquidación
            const liquidaciones = $('.liquidacion-card');
            if (liquidaciones.length === 0) {
                errores.push('Debe agregar al menos una liquidación');
                return errores;
            }

            // Validar cada liquidación
            liquidaciones.each(function (index) {
                const numeroLiquidacion = $(this).attr('id').split('_')[1];
                const subTramos = $(`#subtramos_liquidacion_${numeroLiquidacion} .subtramo-card`);

                if (subTramos.length === 0) {
                    errores.push(`Liquidación ${index + 1}: Debe tener al menos un sub-tramo`);
                    return;
                }

                // Validar cada sub-tramo
                subTramos.each(function (subIndex) {
                    const subTramoId = $(this).attr('id').split('_');
                    const numeroSubTramo = subTramoId[subTramoId.length - 1];
                    const prefijo = `Sub-Tramo ${numeroSubTramo}`;

                    // Campos básicos del sub-tramo
                    const origen = $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_origen`).val();
                    const destino = $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_destino`).val();
                    const operacion = $(`#subtramo_${numeroLiquidacion}_${numeroSubTramo}_operacion`).val();

                    if (!origen) errores.push(`${prefijo}: Origen es obligatorio`);
                    if (!destino) errores.push(`${prefijo}: Destino es obligatorio`);
                    if (!operacion) errores.push(`${prefijo}: Tipo de Operación es obligatorio`);

                    // Validar operaciones específicas
                    if (operacion === 'SOLO_CARGA' || operacion === 'DESCARGA_Y_CARGA') {
                        const erroresCarga = validarOperacion(numeroLiquidacion, numeroSubTramo, 'carga', prefijo);
                        errores.push(...erroresCarga);
                    }

                    if (operacion === 'SOLO_DESCARGA' || operacion === 'DESCARGA_Y_CARGA') {
                        const erroresDescarga = validarOperacion(numeroLiquidacion, numeroSubTramo, 'descarga', prefijo);
                        errores.push(...erroresDescarga);
                    }
                });
            });

            return errores;
        }

        function validarOperacion(numeroLiquidacion, numeroSubTramo, tipoOperacion, prefijo) {
            const errores = [];
            const tipoTexto = tipoOperacion === 'carga' ? 'Carga' : 'Descarga';

            // Cliente
            const cliente = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cliente`).val();
            if (!cliente) {
                errores.push(`${prefijo} - ${tipoTexto}: Cliente es obligatorio`);
            }

            // Planta
            const planta = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_planta`).val();
            if (!planta) {
                errores.push(`${prefijo} - ${tipoTexto}: Planta es obligatoria`);
            }

            // Documento (Factura o CPIC)
            const esInternacional = $(`input[name="${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_tipo"]:checked`).val() === '1';
            if (esInternacional) {
                const cpic = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_cpic`).val();
                if (!cpic) {
                    errores.push(`${prefijo} - ${tipoTexto}: CPIC es obligatorio para transporte internacional`);
                }
            } else {
                const factura = $(`#${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo}_factura`).val();
                if (!factura) {
                    errores.push(`${prefijo} - ${tipoTexto}: Factura es obligatoria para transporte nacional`);
                }
            }

            // Productos
            const productos = $(`#productos_${tipoOperacion}_${numeroLiquidacion}_${numeroSubTramo} .producto-row`);
            if (productos.length === 0) {
                errores.push(`${prefijo} - ${tipoTexto}: Debe agregar al menos un producto`);
            } else {
                let tieneProductoValido = false;
                productos.each(function () {
                    const producto = $(this).find('.producto-select').val();
                    const cantidad = $(this).find('.producto-cantidad').val();
                    if (producto && cantidad && cantidad > 0) {
                        tieneProductoValido = true;
                    }
                });
                if (!tieneProductoValido) {
                    errores.push(`${prefijo} - ${tipoTexto}: Al menos un producto debe tener cantidad válida`);
                }
            }

            return errores;
        }

        function validarResumenFinanciero() {
            const errores = [];

            // Validar que hay al menos algunos datos financieros
            let tieneIngresos = false;
            let tieneGastos = false;

            $('.ingreso-soles, .ingreso-dolares').each(function () {
                if (parseFloat($(this).val()) > 0) {
                    tieneIngresos = true;
                }
            });

            $('.gasto-soles, .gasto-dolares').each(function () {
                if (parseFloat($(this).val()) > 0) {
                    tieneGastos = true;
                }
            });

            if (!tieneIngresos && !tieneGastos) {
                errores.push('Debe registrar al menos un ingreso o gasto en el resumen financiero');
            }

            return errores;
        }

        function mostrarErroresValidacion(errores) {
            let modalHtml = `
        <div class="modal fade" id="modalErroresValidacion" tabindex="-1" role="dialog">
            <div class="modal-dialog modal-lg" role="document">
                <div class="modal-content">
                    <div class="modal-header bg-danger text-white">
                        <h5 class="modal-title">
                            <i class="fas fa-exclamation-triangle"></i> 
                            Errores de Validación Encontrados
                        </h5>
                        <button type="button" class="close text-white" data-dismiss="modal">
                            <span>&times;</span>
                        </button>
                    </div>
                    <div class="modal-body">
                        <p><strong>Se encontraron ${errores.length} sección(es) con errores. Por favor corrija los siguientes campos:</strong></p>
    `;

            errores.forEach((seccion, index) => {
                modalHtml += `
            <div class="card mb-3">
                <div class="card-header bg-light">
                    <h6 class="mb-0 text-danger">
                        <i class="fas fa-times-circle"></i> 
                        ${seccion.pestana} (${seccion.campos.length} error${seccion.campos.length !== 1 ? 'es' : ''})
                    </h6>
                </div>
                <div class="card-body">
                    <ul class="mb-0">
        `;

                seccion.campos.forEach(campo => {
                    modalHtml += `<li class="text-danger">${campo}</li>`;
                });

                modalHtml += `
                    </ul>
                </div>
            </div>
        `;
            });

            modalHtml += `
                        <div class="alert alert-info mt-3">
                            <i class="fas fa-info-circle"></i>
                            <strong>Sugerencia:</strong> Los datos ya ingresados se mantienen guardados. 
                            Corrija los errores y vuelva a intentar guardar.
                        </div>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-dismiss="modal">
                            <i class="fas fa-edit"></i> Corregir Errores
                        </button>
                    </div>
                </div>
            </div>
        </div>
    `;

            // Remover modal anterior si existe
            $('#modalErroresValidacion').remove();

            // Agregar nuevo modal
            $('body').append(modalHtml);

            // Mostrar modal
            $('#modalErroresValidacion').modal('show');
        }

    </script>

</asp:Content>

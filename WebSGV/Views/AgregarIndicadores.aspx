<%@ Page Title="Registro de Indicadores" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AgregarIndicadores.aspx.cs" Inherits="WebSGV.Views.AgregarIndicadores" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .form-group {
            margin-bottom: 15px;
        }
        .form-label {
            font-weight: 500;
            margin-bottom: 5px;
        }
        .form-control {
            width: 100%;
            padding: 6px 12px;
            border: 1px solid #ced4da;
            border-radius: 4px;
        }
        .section-header {
            background-color: #0056b3;
            color: white;
            padding: 10px 15px;
            margin-bottom: 15px;
            border-radius: 4px;
            font-weight: 600;
        }
        .datetime-picker {
            display: flex;
            gap: 10px;
        }
        .date-input, .time-input {
            flex: 1;
        }
    </style>

    <div class="container">
        <h2 class="mt-4 mb-4">Registro de Indicadores de Operación</h2>
        
        <!-- Mensaje de alerta -->
        <asp:Panel ID="alertPanel" runat="server" CssClass="alert alert-info" Visible="false">
            <asp:Literal ID="alertMessage" runat="server"></asp:Literal>
        </asp:Panel>

        <!-- Datos Generales -->
        <div class="section-header">
            <i class="fas fa-info-circle mr-2"></i> Datos Generales
        </div>

        <div class="row">
            <div class="col-md-4">
                <div class="form-group">
                    <label for="txtNumeroPedido" class="form-label">Número de Pedido:</label>
                    <asp:TextBox ID="txtNumeroPedido" runat="server" CssClass="form-control" placeholder="Ej: 4400089246" required="required"></asp:TextBox>
                </div>
            </div>
            <div class="col-md-4">
                <div class="form-group">
                    <label for="txtConductorOrigen" class="form-label">Conductor Origen:</label>
                    <asp:TextBox ID="txtConductorOrigen" runat="server" CssClass="form-control" placeholder="Ingrese el nombre del conductor"></asp:TextBox>
                </div>
            </div>
            <div class="col-md-4">
                <div class="form-group">
                    <label for="txtTracto1" class="form-label">Tracto 1:</label>
                    <asp:TextBox ID="txtTracto1" runat="server" CssClass="form-control" placeholder="Ej: TBM-815"></asp:TextBox>
                </div>
            </div>
        </div>

        <div class="row">
            <div class="col-md-4">
                <div class="form-group">
                    <label for="txtCarreta" class="form-label">Carreta:</label>
                    <asp:TextBox ID="txtCarreta" runat="server" CssClass="form-control" placeholder="Ej: T0L-975"></asp:TextBox>
                </div>
            </div>
            <div class="col-md-4">
                <div class="form-group">
                    <label for="txtConductorDestino" class="form-label">Conductor Destino:</label>
                    <asp:TextBox ID="txtConductorDestino" runat="server" CssClass="form-control" placeholder="Ingrese el nombre del conductor"></asp:TextBox>
                </div>
            </div>
            <div class="col-md-4">
                <div class="form-group">
                    <label for="txtTracto2" class="form-label">Tracto 2:</label>
                    <asp:TextBox ID="txtTracto2" runat="server" CssClass="form-control" placeholder="Ej: AVN-717"></asp:TextBox>
                </div>
            </div>
        </div>

        <!-- Fechas y Horas - Salida y Llegada -->
        <div class="section-header mt-4">
            <i class="fas fa-calendar-alt mr-2"></i> Fechas y Horas - Salida y Llegada
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHSBase" class="form-label">F.H.S. Base:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHSBase_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHSBase_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHLLTrujillo" class="form-label">F.H.LL. Trujillo:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHLLTrujillo_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHLLTrujillo_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHRegistro" class="form-label">F.H. Registro:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHRegistro_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHRegistro_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHProgramacion" class="form-label">F.H. Programación:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHProgramacion_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHProgramacion_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <!-- Fechas y Horas - Planta y Carga -->
        <div class="section-header mt-4">
            <i class="fas fa-industry mr-2"></i> Fechas y Horas - Planta y Carga
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHIPlanta" class="form-label">F.H.I. Planta:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHIPlanta_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHIPlanta_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHInicioCarga" class="form-label">F.H. Inicio de Carga:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHInicioCarga_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHInicioCarga_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHTerminoCarga" class="form-label">F.H. Término Carga:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHTerminoCarga_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHTerminoCarga_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHSPlanta" class="form-label">F.H.S. Planta:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHSPlanta_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHSPlanta_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHLLBase" class="form-label">F.H.LL. Base:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHLLBase_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHLLBase_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <!-- Fechas y Horas - Depsa -->
        <div class="section-header mt-4">
            <i class="fas fa-warehouse mr-2"></i> Fechas y Horas - Depsa
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHSBaseDepsa" class="form-label">F.H.S. Base Depsa:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHSBaseDepsa_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHSBaseDepsa_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHLLDepsa" class="form-label">F.H.LL. Depsa:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHLLDepsa_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHLLDepsa_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHIDepsa" class="form-label">F.H.I. Depsa:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHIDepsa_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHIDepsa_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHSDepsa" class="form-label">F.H.S. Depsa:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHSDepsa_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHSDepsa_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtBodega" class="form-label">Bodega:</label>
                    <asp:TextBox ID="txtBodega" runat="server" CssClass="form-control" placeholder="Ingrese la bodega"></asp:TextBox>
                </div>
            </div>
        </div>

        <!-- Fechas y Horas - CEBAF -->
        <div class="section-header mt-4">
            <i class="fas fa-exchange-alt mr-2"></i> Fechas y Horas - CEBAF
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHLLCebafE" class="form-label">F.H.LL. CEBAF E:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHLLCebafE_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHLLCebafE_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHCruceE" class="form-label">F.H. CRUCE E:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHCruceE_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHCruceE_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHAutorizacionNacionalizacion" class="form-label">F.H. Autorización Nacionalización:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHAutorizacionNacionalizacion_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHAutorizacionNacionalizacion_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtBodegaEcuatoriana" class="form-label">Bodega Ecuatoriana:</label>
                    <asp:TextBox ID="txtBodegaEcuatoriana" runat="server" CssClass="form-control" placeholder="Ingrese la bodega ecuatoriana"></asp:TextBox>
                </div>
            </div>
        </div>

        <!-- Fechas y Horas - TCI y Descarga -->
        <div class="section-header mt-4">
            <i class="fas fa-dolly mr-2"></i> Fechas y Horas - TCI y Descarga
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHLLTCI" class="form-label">F.H.LL. TCI:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHLLTCI_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHLLTCI_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHSTCI" class="form-label">F.H.S. TCI:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHSTCI_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHSTCI_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtBodegaDescarga" class="form-label">Bodega Descarga:</label>
                    <asp:TextBox ID="txtBodegaDescarga" runat="server" CssClass="form-control" placeholder="Ingrese la bodega de descarga"></asp:TextBox>
                </div>
            </div>
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHLLPlanta" class="form-label">F.H.LL. Planta:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHLLPlanta_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHLLPlanta_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHLLAlmacen" class="form-label">F.H.LL. Almacén:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHLLAlmacen_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHLLAlmacen_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHIngreso" class="form-label">F.H. Ingreso:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHIngreso_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHIngreso_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHIDescarga" class="form-label">F.H.I. Descarga:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHIDescarga_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHIDescarga_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHTDescarga" class="form-label">F.H.T. Descarga:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHTDescarga_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHTDescarga_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <div class="row">
            <div class="col-md-6">
                <div class="form-group">
                    <label for="txtFHLLSalida" class="form-label">F.H.LL. Salida:</label>
                    <div class="datetime-picker">
                        <asp:TextBox ID="txtFHLLSalida_Date" runat="server" CssClass="form-control date-input" TextMode="Date"></asp:TextBox>
                        <asp:TextBox ID="txtFHLLSalida_Time" runat="server" CssClass="form-control time-input" TextMode="Time"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <!-- Botones de acción -->
        <div class="form-group mt-4 text-center">
            <asp:Button ID="btnLimpiar" runat="server" Text="Limpiar Formulario" CssClass="btn btn-secondary mr-2" OnClick="btnLimpiar_Click" />
            <asp:Button ID="btnGuardar" runat="server" Text="Guardar Indicador" CssClass="btn btn-primary" OnClick="btnGuardar_Click" />
        </div>
    </div>
</asp:Content>
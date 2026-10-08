<%@ Page Title="Detalle de Liquidación" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DetalleOrdenViaje.aspx.cs" Inherits="WebSGV.Views.DetalleOrdenViaje" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <div class="container-fluid px-4">

        <!-- Header -->
        <div class="row mb-4">
            <div class="col-12">
                <div class="page-header d-flex justify-content-between align-items-center flex-wrap">
                    <div>
                        <h2 class="page-title mb-1">
                            <i class="fas fa-file-invoice-dollar mr-2"></i>Detalle de Liquidación
                        </h2>
                        <p class="text-muted mb-0">
                            Orden: <strong><asp:Label ID="lblNumeroOrden" runat="server"></asp:Label></strong>
                        </p>
                    </div>
                    <div class="mt-2 mt-md-0">
                        <a href="DashboardConductor.aspx" class="btn btn-secondary-custom btn-sm">
                            <i class="fas fa-arrow-left mr-1"></i>Volver al Dashboard
                        </a>
                    </div>
                </div>
            </div>
        </div>

        <!-- Panel de mensajes de error -->
        <asp:Panel ID="pnlError" runat="server" Visible="false">
            <div class="alert alert-danger">
                <i class="fas fa-exclamation-circle mr-2"></i>
                <asp:Label ID="lblError" runat="server"></asp:Label>
            </div>
        </asp:Panel>

        <!-- Contenido principal -->
        <asp:Panel ID="pnlContenido" runat="server" Visible="false">

            <!-- Resumen General -->
            <div class="section-card mb-4">
                <div class="section-header section-header-info">
                    <h5 class="section-title">
                        <i class="fas fa-info-circle mr-2"></i>Información General
                    </h5>
                </div>
                <div class="section-body">
                    <div class="row">
                        <div class="col-6 col-md-2">
                            <label class="form-label-summary">N° Orden</label>
                            <p class="form-value-summary"><asp:Label ID="lblOrdenDetalle" runat="server"></asp:Label></p>
                        </div>
                        <div class="col-6 col-md-2">
                            <label class="form-label-summary">Conductor</label>
                            <p class="form-value-summary"><asp:Label ID="lblConductorDetalle" runat="server"></asp:Label></p>
                        </div>
                        <div class="col-6 col-md-2">
                            <label class="form-label-summary">Tracto</label>
                            <p class="form-value-summary">
                                <span class="badge badge-vehicle"><asp:Label ID="lblTractoDetalle" runat="server"></asp:Label></span>
                            </p>
                        </div>
                        <div class="col-6 col-md-2">
                            <label class="form-label-summary">Carreta</label>
                            <p class="form-value-summary">
                                <span class="badge badge-vehicle"><asp:Label ID="lblCarretaDetalle" runat="server"></asp:Label></span>
                            </p>
                        </div>
                        <div class="col-6 col-md-2">
                            <label class="form-label-summary">Fecha Salida</label>
                            <p class="form-value-summary"><asp:Label ID="lblFechaSalidaDetalle" runat="server"></asp:Label></p>
                        </div>
                        <div class="col-6 col-md-2">
                            <label class="form-label-summary">Fecha Llegada</label>
                            <p class="form-value-summary"><asp:Label ID="lblFechaLlegadaDetalle" runat="server"></asp:Label></p>
                        </div>
                    </div>
                    <div class="row mt-3">
                        <div class="col-6 col-md-3">
                            <label class="form-label-summary">Hora Salida</label>
                            <p class="form-value-summary"><asp:Label ID="lblHoraSalidaDetalle" runat="server" Text="-"></asp:Label></p>
                        </div>
                        <div class="col-6 col-md-3">
                            <label class="form-label-summary">Hora Llegada</label>
                            <p class="form-value-summary"><asp:Label ID="lblHoraLlegadaDetalle" runat="server" Text="-"></asp:Label></p>
                        </div>
                        <div class="col-6 col-md-3">
                            <label class="form-label-summary">Estado</label>
                            <p class="form-value-summary">
                                <asp:Label ID="lblEstadoDetalle" runat="server"></asp:Label>
                            </p>
                        </div>
                        <div class="col-12 col-md-6">
                            <label class="form-label-summary">Observaciones</label>
                            <p class="form-value-summary text-muted" style="font-size:0.95rem;font-weight:400;">
                                <asp:Label ID="lblObservacionesDetalle" runat="server" Text="-"></asp:Label>
                            </p>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Ingresos -->
            <div class="section-card mb-4">
                <div class="section-header section-header-success">
                    <h5 class="section-title">
                        <i class="fas fa-plus-circle mr-2"></i>Ingresos del Viaje
                    </h5>
                </div>
                <div class="section-body p-0">
                    <div class="table-responsive">
                        <table class="table table-financial mb-0">
                            <thead>
                                <tr>
                                    <th style="width:5%">#</th>
                                    <th style="width:22%">Concepto</th>
                                    <th style="width:35%">Descripción</th>
                                    <th class="text-right" style="width:18%">Soles (S/)</th>
                                    <th class="text-right" style="width:18%">Dólares ($)</th>
                                </tr>
                            </thead>
                            <tbody>
                                <asp:Repeater ID="rptIngresos" runat="server">
                                    <ItemTemplate>
                                        <tr>
                                            <td class="text-center"><%# Container.ItemIndex + 1 %></td>
                                            <td><strong><%# Eval("Concepto") %></strong></td>
                                            <td class="text-muted"><%# Eval("Descripcion") %></td>
                                            <td class="text-right"><%# FormatearMonto(Eval("Soles")) %></td>
                                            <td class="text-right"><%# FormatearMonto(Eval("Dolares")) %></td>
                                        </tr>
                                    </ItemTemplate>
                                </asp:Repeater>
                            </tbody>
                            <tfoot>
                                <tr class="total-row">
                                    <td colspan="3" class="text-right"><strong>Total Ingresos:</strong></td>
                                    <td class="text-right"><strong>S/ <asp:Label ID="lblTotalIngresosSoles" runat="server" Text="0.00"></asp:Label></strong></td>
                                    <td class="text-right"><strong>$ <asp:Label ID="lblTotalIngresosDolares" runat="server" Text="0.00"></asp:Label></strong></td>
                                </tr>
                            </tfoot>
                        </table>
                    </div>
                </div>
            </div>

            <!-- Gastos -->
            <div class="section-card mb-4">
                <div class="section-header section-header-danger">
                    <h5 class="section-title">
                        <i class="fas fa-minus-circle mr-2"></i>Gastos del Viaje
                    </h5>
                </div>
                <div class="section-body p-0">
                    <div class="table-responsive">
                        <table class="table table-financial mb-0">
                            <thead>
                                <tr>
                                    <th style="width:5%">#</th>
                                    <th style="width:22%">Concepto</th>
                                    <th style="width:35%">Descripción</th>
                                    <th class="text-right" style="width:18%">Soles (S/)</th>
                                    <th class="text-right" style="width:18%">Dólares ($)</th>
                                </tr>
                            </thead>
                            <tbody>
                                <asp:Repeater ID="rptGastos" runat="server">
                                    <ItemTemplate>
                                        <tr>
                                            <td class="text-center"><%# Container.ItemIndex + 1 %></td>
                                            <td><strong><%# Eval("Concepto") %></strong></td>
                                            <td class="text-muted"><%# Eval("Descripcion") %></td>
                                            <td class="text-right"><%# FormatearMonto(Eval("Soles")) %></td>
                                            <td class="text-right"><%# FormatearMonto(Eval("Dolares")) %></td>
                                        </tr>
                                    </ItemTemplate>
                                </asp:Repeater>
                            </tbody>
                            <tfoot>
                                <tr class="total-row">
                                    <td colspan="3" class="text-right"><strong>Total Gastos:</strong></td>
                                    <td class="text-right"><strong>S/ <asp:Label ID="lblTotalGastosSoles" runat="server" Text="0.00"></asp:Label></strong></td>
                                    <td class="text-right"><strong>$ <asp:Label ID="lblTotalGastosDolares" runat="server" Text="0.00"></asp:Label></strong></td>
                                </tr>
                            </tfoot>
                        </table>
                    </div>
                </div>
            </div>

            <!-- Detalle de Peajes -->
            <asp:Panel ID="pnlDetallePeajes" runat="server" Visible="false">
                <div class="section-card mb-4">
                    <div class="section-header">
                        <h5 class="section-title"><i class="fas fa-road mr-2"></i>Detalle de Peajes</h5>
                    </div>
                    <div class="section-body p-0">
                        <div class="table-responsive">
                            <asp:GridView ID="gvPeajes" runat="server"
                                CssClass="table table-conductor mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="Sin peajes registrados">
                                <Columns>
                                    <asp:BoundField DataField="Estacion" HeaderText="ESTACIÓN" />
                                    <asp:BoundField DataField="Fecha" HeaderText="FECHA" DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="text-center" />
                                    <asp:BoundField DataField="Comprobante" HeaderText="COMPROBANTE" ItemStyle-CssClass="text-center" />
                                    <asp:BoundField DataField="Soles" HeaderText="SOLES (S/)" DataFormatString="{0:N2}" ItemStyle-CssClass="text-right" />
                                    <asp:BoundField DataField="Dolares" HeaderText="DÓLARES ($)" DataFormatString="{0:N2}" ItemStyle-CssClass="text-right" />
                                    <asp:BoundField DataField="Observaciones" HeaderText="OBSERVACIONES" />
                                </Columns>
                            </asp:GridView>
                        </div>
                    </div>
                </div>
            </asp:Panel>

            <!-- Detalle de Reparaciones -->
            <asp:Panel ID="pnlDetalleReparaciones" runat="server" Visible="false">
                <div class="section-card mb-4">
                    <div class="section-header">
                        <h5 class="section-title"><i class="fas fa-wrench mr-2"></i>Detalle de Reparaciones</h5>
                    </div>
                    <div class="section-body p-0">
                        <div class="table-responsive">
                            <asp:GridView ID="gvReparaciones" runat="server"
                                CssClass="table table-conductor mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="Sin reparaciones registradas">
                                <Columns>
                                    <asp:BoundField DataField="Tipo" HeaderText="TIPO" />
                                    <asp:BoundField DataField="Fecha" HeaderText="FECHA" DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="text-center" />
                                    <asp:BoundField DataField="Comprobante" HeaderText="COMPROBANTE" ItemStyle-CssClass="text-center" />
                                    <asp:BoundField DataField="Soles" HeaderText="SOLES (S/)" DataFormatString="{0:N2}" ItemStyle-CssClass="text-right" />
                                    <asp:BoundField DataField="Dolares" HeaderText="DÓLARES ($)" DataFormatString="{0:N2}" ItemStyle-CssClass="text-right" />
                                    <asp:BoundField DataField="Observaciones" HeaderText="OBSERVACIONES" />
                                </Columns>
                            </asp:GridView>
                        </div>
                    </div>
                </div>
            </asp:Panel>

            <!-- Detalle de Hospedaje -->
            <asp:Panel ID="pnlDetalleHospedaje" runat="server" Visible="false">
                <div class="section-card mb-4">
                    <div class="section-header">
                        <h5 class="section-title"><i class="fas fa-bed mr-2"></i>Detalle de Hospedaje</h5>
                    </div>
                    <div class="section-body p-0">
                        <div class="table-responsive">
                            <asp:GridView ID="gvHospedaje" runat="server"
                                CssClass="table table-conductor mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="Sin hospedajes registrados">
                                <Columns>
                                    <asp:BoundField DataField="Lugar" HeaderText="LUGAR" />
                                    <asp:BoundField DataField="Fecha" HeaderText="FECHA" DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="text-center" />
                                    <asp:BoundField DataField="Comprobante" HeaderText="COMPROBANTE" ItemStyle-CssClass="text-center" />
                                    <asp:BoundField DataField="Soles" HeaderText="SOLES (S/)" DataFormatString="{0:N2}" ItemStyle-CssClass="text-right" />
                                    <asp:BoundField DataField="Dolares" HeaderText="DÓLARES ($)" DataFormatString="{0:N2}" ItemStyle-CssClass="text-right" />
                                    <asp:BoundField DataField="Observaciones" HeaderText="OBSERVACIONES" />
                                </Columns>
                            </asp:GridView>
                        </div>
                    </div>
                </div>
            </asp:Panel>

            <!-- Detalle de Combustible -->
            <asp:Panel ID="pnlDetalleCombustible" runat="server" Visible="false">
                <div class="section-card mb-4">
                    <div class="section-header">
                        <h5 class="section-title"><i class="fas fa-gas-pump mr-2"></i>Detalle de Combustible</h5>
                    </div>
                    <div class="section-body p-0">
                        <div class="table-responsive">
                            <asp:GridView ID="gvCombustible" runat="server"
                                CssClass="table table-conductor mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="Sin combustibles registrados">
                                <Columns>
                                    <asp:BoundField DataField="Lugar" HeaderText="LUGAR/GRIFO" />
                                    <asp:BoundField DataField="Fecha" HeaderText="FECHA" DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="text-center" />
                                    <asp:BoundField DataField="Comprobante" HeaderText="COMPROBANTE" ItemStyle-CssClass="text-center" />
                                    <asp:BoundField DataField="Soles" HeaderText="SOLES (S/)" DataFormatString="{0:N2}" ItemStyle-CssClass="text-right" />
                                    <asp:BoundField DataField="Dolares" HeaderText="DÓLARES ($)" DataFormatString="{0:N2}" ItemStyle-CssClass="text-right" />
                                    <asp:BoundField DataField="Observaciones" HeaderText="OBSERVACIONES" />
                                </Columns>
                            </asp:GridView>
                        </div>
                    </div>
                </div>
            </asp:Panel>

            <!-- Balance Final -->
            <div class="section-card mb-4">
                <div class="section-header section-header-neutral">
                    <h5 class="section-title">
                        <i class="fas fa-balance-scale mr-2"></i>Balance Final del Viaje
                    </h5>
                </div>
                <div class="section-body">
                    <div class="balance-final">
                        <div class="row">
                            <div class="col-12 col-md-6 mb-3 mb-md-0">
                                <div class="balance-item">
                                    <span class="balance-label">Balance en Soles:</span>
                                    <span class="balance-amount" id="spanBalanceSoles">
                                        <asp:Label ID="lblBalanceSoles" runat="server"></asp:Label>
                                    </span>
                                </div>
                            </div>
                            <div class="col-12 col-md-6">
                                <div class="balance-item">
                                    <span class="balance-label">Balance en Dólares:</span>
                                    <span class="balance-amount" id="spanBalanceDolares">
                                        <asp:Label ID="lblBalanceDolares" runat="server"></asp:Label>
                                    </span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Botón volver -->
            <div class="text-center mb-5">
                <a href="DashboardConductor.aspx" class="btn btn-primary-conductor btn-lg px-5">
                    <i class="fas fa-arrow-left mr-2"></i>Volver al Dashboard
                </a>
            </div>

        </asp:Panel>

    </div>

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/DetalleOrdenViaje.css") %>" rel="stylesheet" />

</asp:Content>

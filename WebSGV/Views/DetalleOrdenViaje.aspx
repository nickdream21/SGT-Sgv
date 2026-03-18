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

    <style>
        :root {
            --primary-color: #2563eb;
            --primary-dark: #1e40af;
            --success-color: #059669;
            --danger-color: #dc2626;
            --info-color: #0891b2;
            --neutral-color: #475569;
            --light-gray: #f8fafc;
            --medium-gray: #e2e8f0;
            --border-color: #cbd5e1;
            --text-primary: #1e293b;
            --text-secondary: #64748b;
        }

        .page-header { padding-bottom: 1rem; border-bottom: 2px solid var(--border-color); }
        .page-title { color: var(--text-primary); font-weight: 600; font-size: 1.75rem; margin: 0; }

        .btn-primary-conductor {
            background: linear-gradient(135deg, #2563eb 0%, #3b82f6 100%);
            border: none; color: white; font-weight: 600;
            transition: all 0.3s; box-shadow: 0 4px 15px rgba(37,99,235,0.3);
        }
        .btn-primary-conductor:hover { background: linear-gradient(135deg, #1e40af 0%, #2563eb 100%); color: white; }

        .btn-secondary-custom {
            background-color: white; border: 1px solid var(--border-color);
            color: var(--text-secondary); font-weight: 500;
        }

        .section-card { background-color: white; border: 1px solid var(--border-color); border-radius: 0.5rem; overflow: hidden; }
        .section-header { background-color: var(--light-gray); padding: 1rem 1.25rem; border-bottom: 1px solid var(--border-color); }
        .section-header-success { background-color: #f0fdf4; border-bottom-color: #86efac; }
        .section-header-danger  { background-color: #fef2f2; border-bottom-color: #fca5a5; }
        .section-header-info    { background-color: #e0f2fe; border-bottom-color: #7dd3fc; }
        .section-header-neutral { background-color: var(--light-gray); border-bottom-color: var(--border-color); }
        .section-title { color: var(--text-primary); font-size: 1.125rem; font-weight: 600; margin: 0; }
        .section-body  { padding: 1.5rem; }

        .form-label-summary { font-size: 0.75rem; font-weight: 600; color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.05em; margin-bottom: 0.25rem; display:block; }
        .form-value-summary  { font-size: 1rem; font-weight: 600; color: var(--text-primary); margin: 0; }

        .badge-vehicle { background-color: #3b82f6; color: white; padding: 0.375rem 0.75rem; border-radius: 0.25rem; font-weight: 600; font-size: 0.8125rem; }

        .table-financial { font-size: 0.875rem; margin-bottom: 0; }
        .table-financial thead th { background-color: var(--light-gray); color: var(--text-primary); font-weight: 600; font-size: 0.8125rem; padding: 0.75rem; border-bottom: 2px solid var(--border-color); }
        .table-financial tbody td { vertical-align: middle; padding: 0.625rem; border-bottom: 1px solid var(--medium-gray); }
        .table-financial tfoot .total-row td { background-color: var(--light-gray); font-weight: 600; padding: 1rem 0.75rem; border-top: 2px solid var(--border-color); }

        .table-conductor { font-size: 0.875rem; }
        .table-conductor thead th { background-color: var(--light-gray); color: var(--text-primary); font-weight: 600; font-size: 0.8125rem; text-transform: uppercase; letter-spacing: 0.025em; padding: 0.875rem 0.75rem; border-bottom: 2px solid var(--border-color); }
        .table-conductor tbody td { vertical-align: middle; padding: 0.75rem; border-bottom: 1px solid var(--medium-gray); }
        .table-conductor tbody tr:hover { background-color: var(--light-gray); }

        .balance-final { background-color: var(--light-gray); border: 2px solid var(--border-color); border-radius: 0.5rem; padding: 1.5rem; }
        .balance-item  { display: flex; justify-content: space-between; align-items: center; padding: 0.75rem 1rem; background-color: white; border: 1px solid var(--border-color); border-radius: 0.375rem; }
        .balance-label  { font-weight: 600; color: var(--text-primary); font-size: 1rem; }
        .balance-amount { font-size: 1.5rem; font-weight: 700; }

        .balance-positivo { color: #059669; }
        .balance-negativo { color: #dc2626; }
        .balance-neutro   { color: var(--primary-color); }

        @media (max-width: 768px) {
            .page-title { font-size: 1.375rem; }
            .section-body { padding: 1rem; }
            .balance-amount { font-size: 1.25rem; }
            .balance-item { flex-direction: column; text-align: center; gap: 0.5rem; }
        }
    </style>

</asp:Content>

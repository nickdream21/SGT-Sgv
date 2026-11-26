<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="HistorialViajes.ascx.cs" Inherits="WebSGV.Views.Controles.Despachos.HistorialViajes" %>
<%-- Modal del historial de viajes del conductor de RegistroDespacho (E6-26), con "Reabrir". Va fuera de UpdatePanelMain
     (un postback parcial de ese panel lo volvería a dibujar y rompería el modal abierto); lo abre "Ver historial" de
     ViajesConductor.ascx (la página llama a Abrir) con showHistorialModal de RegistroDespacho.js (id estático). --%>
<div class="modal fade" id="modalHistorialViajes" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalHistorialViajesLabel" aria-hidden="true">
    <div class="modal-dialog modal-xl">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title" id="modalHistorialViajesLabel">
                    Historial de viajes - <%-- Dentro de su UpdatePanel: si no, el postback parcial no refresca el nombre --%><asp:UpdatePanel ID="UpdatePanelTituloModal" runat="server" UpdateMode="Conditional" RenderMode="Inline"><ContentTemplate><span id="spanConductorModal" runat="server"></span></ContentTemplate></asp:UpdatePanel>
                </h5>
                <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>
            </div>
            <div class="modal-body">
                <asp:UpdatePanel ID="UpdatePanelModal" runat="server" UpdateMode="Conditional">
                    <ContentTemplate>
                        <div class="table-responsive">
                        <asp:GridView ID="gvHistorialViajes" runat="server"
                            CssClass="table table-striped table-hover"
                            AutoGenerateColumns="false" CellSpacing="-1"
                            EmptyDataText="No se encontraron viajes para este conductor"
                            OnRowCommand="gvHistorialViajes_RowCommand">
                            <Columns>
                                <asp:BoundField DataField="NumeroViajeProgreso" HeaderText="N° Viaje" />
                                <asp:BoundField DataField="FechaInicio" HeaderText="Fecha inicio" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                                <asp:BoundField DataField="FechaCierre" HeaderText="Fecha cierre" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                                <asp:BoundField DataField="CantidadDespachos" HeaderText="Despachos" />
                                <asp:BoundField DataField="TipoViaje" HeaderText="Tipo viaje" />
                                <asp:BoundField DataField="EstadoViaje" HeaderText="Estado" />
                                <asp:TemplateField HeaderText="Acciones">
                                    <ItemTemplate>
                                        <asp:Button runat="server"
                                            Text="Reabrir"
                                            CssClass="btn btn-warning btn-sm"
                                            CommandName="Reabrir"
                                            CommandArgument='<%# Eval("IdViajeProgreso") %>'
                                            Visible='<%# Eval("EstadoViaje").ToString() == "CERRADO" %>'
                                            data-confirmar="¿Reabrir este viaje para agregar más despachos?"
                                            data-confirmar-aceptar="Reabrir viaje" />
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                        </div>
                    </ContentTemplate>
                    <Triggers>
                        <asp:AsyncPostBackTrigger ControlID="gvHistorialViajes" EventName="RowCommand" />
                    </Triggers>
                </asp:UpdatePanel>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" data-dismiss="modal">Cerrar</button>
            </div>
        </div>
    </div>
</div>

<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="ViajesConductor.ascx.cs" Inherits="WebSGV.Views.Controles.Despachos.ViajesConductor" %>
<%-- Viajes en progreso del conductor elegido en RegistroDespacho (E6-26): el viaje al que irá el despacho, "Crear nuevo
     viaje" (envío único, E5-3) y "Ver historial" (evento VerHistorial: el modal es HistorialViajes.ascx, fuera de
     UpdatePanelMain). El selector de conductor es de la página (SelectorConductor), que también registra en código el
     disparador de este panel (RefrescarCon) y los de UpdatePanelMain (DisparadoresAsincronos).
     UpdatePanel propio: elegir un conductor solo refresca este bloque, en vez de
     re-renderizar todo el formulario (lo que reiniciaba los Select2 y movía la página). --%>
<asp:UpdatePanel ID="UpdatePanelViajes" runat="server" UpdateMode="Conditional">
    <ContentTemplate>
        <%-- Envío único de "Crear nuevo viaje" (E5-3): dentro de este panel (y fuera de
             pnlViajesProgreso, que puede estar oculto) para que el token nuevo llegue al navegador --%>
        <asp:HiddenField ID="hfEnvioUnicoViaje" runat="server" />
        <asp:Panel ID="pnlViajesProgreso" runat="server" CssClass="sgv-panel rd-panel-viaje mb-4" Visible="false">
            <div class="sgv-panel-cab">
                <h3 class="sgv-panel-titulo">Viajes en progreso de <span id="spanNombreConductor" runat="server"></span></h3>
            </div>
            <div class="sgv-panel-cuerpo">
                <div class="row">
                    <div class="col-md-8">
                        <asp:Panel ID="pnlSinViajes" runat="server" Visible="true">
                            <div class="rd-notice mb-0">
                                <strong>Sin viajes activos:</strong> Se creará automáticamente un nuevo viaje en progreso para este conductor.
                            </div>
                        </asp:Panel>

                        <asp:Panel ID="pnlConViajes" runat="server" Visible="false">
                            <span class="sgv-etiqueta d-inline-block mb-2">
                                <strong>Viaje activo encontrado:</strong>
                            </span>
                             <div class="rd-notice rd-notice-ok">
                                <div class="row align-items-center">
                                    <div class="col-md-12">
                                        <strong><asp:Label ID="lblNumeroViaje" runat="server"></asp:Label></strong><br>
                                        <small class="text-muted">
                                            Iniciado: <asp:Label ID="lblFechaInicio" runat="server"></asp:Label> |
                                            Despachos: <asp:Label ID="lblCantidadDespachos" runat="server"></asp:Label> |
                                            Tipo: <asp:Label ID="lblTipoViaje" runat="server"></asp:Label>
                                        </small>
                                    </div>
                                </div>
                            </div>
                        </asp:Panel>

                        <asp:Panel ID="pnlMultiplesViajes" runat="server" Visible="false">
                            <div class="rd-notice rd-notice-warn mb-3">
                                <i class="fas fa-exclamation-triangle mr-1"></i>
                                <strong>Múltiples viajes activos:</strong> Seleccione en cuál desea agregar este despacho.
                            </div>
                            <div class="row align-items-end">
                                <div class="col-md-8">
                                    <asp:DropDownList ID="ddlViajesActivos" runat="server"
                                        CssClass="custom-select" aria-label="Viaje activo al que se agrega el despacho"
                                        DataTextField="Display"
                                        DataValueField="IdViajeProgreso">
                                        <asp:ListItem Value="0" Text="-- Seleccione un viaje --"></asp:ListItem>
                                    </asp:DropDownList>
                                </div>
                                <div class="col-md-4">
                                    <asp:Button ID="btnCrearNuevoViaje" runat="server"
                                        Text="Crear nuevo viaje"
                                        CssClass="btn btn-outline-success btn-sm w-100"
                                        OnClick="btnCrearNuevoViaje_Click"
                                        data-confirmar="Se creará un viaje en progreso nuevo para este conductor, aparte de los que ya tiene abiertos.\n¿Desea crearlo?"
                                        data-confirmar-titulo="Crear nuevo viaje"
                                        data-confirmar-aceptar="Crear viaje"
                                        CausesValidation="false" />
                                </div>
                            </div>
                        </asp:Panel>
                    </div>
                    <div class="col-md-4">
                        <div class="text-center">
                            <asp:Button ID="btnVerHistorialViajes" runat="server"
                                Text="Ver historial"
                                CssClass="btn btn-outline-info w-100"
                                OnClick="btnVerHistorialViajes_Click"
                                CausesValidation="false" />
                        </div>
                    </div>
                </div>
            </div>
        </asp:Panel>
    </ContentTemplate>
    <%-- El disparador del selector de conductor (de la página) se registra en código: RefrescarCon. --%>
</asp:UpdatePanel>

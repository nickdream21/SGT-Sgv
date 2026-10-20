<%@ Page Title="Viaje de exportación" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SeguimientoViaje.aspx.cs" Inherits="WebSGV.Views.Exportacion.SeguimientoViaje" %>

<asp:Content ID="ContentViaje" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/Exportacion/SeguimientoExportacion.css") %>" rel="stylesheet" />

    <div class="sx-wrap">
        <a href="RegistroSeguimiento.aspx" class="sx-nota" style="display: inline-block; padding: 0.5rem 0;">← Volver al tablero</a>

        <div class="sgv-encabezado">
            <div>
                <h2><asp:Literal ID="litTitulo" runat="server" /> <asp:Literal ID="litFase" runat="server" /></h2>
                <p><asp:Literal ID="litSubtitulo" runat="server" /></p>
            </div>
            <asp:Panel ID="pnlAcciones" runat="server" CssClass="sx-barra-acciones">
                <button type="button" class="sx-btn" id="btnEditarDatos" aria-expanded="false" aria-controls="seccionDatos">Editar datos del viaje</button>
                <asp:Button ID="btnExportar" runat="server" Text="Exportar a STATUS GENERAL" CssClass="sx-btn" CausesValidation="false" OnClick="btnExportar_Click" />
                <asp:Button ID="btnEstado" runat="server" CssClass="sx-btn" CausesValidation="false" OnClick="btnEstado_Click" />
                <asp:Button ID="btnActualizarGps" runat="server" Text="Actualizar GPS" CssClass="sx-btn sx-btn-primario" CausesValidation="false"
                    OnClick="btnActualizarGps_Click" OnClientClick="this.value='Consultando el GPS…';" UseSubmitBehavior="false" />
            </asp:Panel>
        </div>

        <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
            <asp:Literal ID="litMensaje" runat="server" />
        </asp:Panel>

        <%-- Datos de cabecera: abierto al crear un viaje, plegado al editar. --%>
        <div class="sgv-panel" style="margin-bottom: 1rem;">
            <div id="seccionDatos" class='<%= ModoNuevo ? "sx-cabecera-datos sx-abierto" : "sx-cabecera-datos" %>' style="border-top: 0;">
                <div class="sx-rejilla" style="grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr));">
                    <label class="sx-campo">Pedido <asp:TextBox ID="txtPedido" runat="server" MaxLength="150" /></label>
                    <label class="sx-campo">Carreta <asp:TextBox ID="txtCarreta" runat="server" MaxLength="20" /></label>
                    <label class="sx-campo">F.H. Programación <asp:TextBox ID="txtProgramacion" runat="server" TextMode="DateTimeLocal" /></label>
                    <label class="sx-campo">Planta de descarga
                        <asp:DropDownList ID="ddlPlanta" runat="server">
                            <asp:ListItem Value="" Text="Por detectar" />
                            <asp:ListItem Value="JAVE" Text="Jave" />
                            <asp:ListItem Value="INBALNOR" Text="Inbalnor" />
                        </asp:DropDownList>
                    </label>
                    <label class="sx-campo">Tracto de carga <asp:TextBox ID="txtTracto1" runat="server" MaxLength="20" /></label>
                    <label class="sx-campo">Chofer de carga <asp:TextBox ID="txtChofer1" runat="server" MaxLength="150" /></label>
                    <label class="sx-campo">Tracto de descarga <asp:TextBox ID="txtTracto2" runat="server" MaxLength="20" /></label>
                    <label class="sx-campo">Chofer de descarga <asp:TextBox ID="txtChofer2" runat="server" MaxLength="150" /></label>
                </div>
                <div class="sx-barra-acciones" style="margin-top: 0.75rem;">
                    <asp:Button ID="btnGuardarDatos" runat="server" CssClass="sx-btn sx-btn-primario" CausesValidation="false" OnClick="btnGuardarDatos_Click" />
                    <span class="sx-nota"><asp:Literal ID="litNotaDatos" runat="server" /></span>
                </div>
            </div>
        </div>

        <asp:Panel ID="pnlViaje" runat="server">
            <div class="sx-leyenda">
                <span>Origen de cada hora:</span>
                <span class="sx-fuente sx-fuente-gps">GPS</span>
                <span class="sx-fuente sx-fuente-onway">Alerta Onway</span>
                <span class="sx-fuente sx-fuente-excel">Registro anterior</span>
                <span class="sx-fuente sx-fuente-manual">Corregido a mano</span>
            </div>

            <div class="sx-detalle">
                <div class="sx-tramos">
                    <asp:Repeater ID="rptTramos" runat="server">
                        <ItemTemplate>
                            <section class="sgv-panel sx-tramo">
                                <div class="sgv-panel-cab">
                                    <div>
                                        <span class="sgv-panel-titulo"><%#: Eval("Titulo") %></span>
                                        <span class="sx-sub"><span class="sx-mono"><%#: Eval("Tracto") %></span> · <%#: Eval("Chofer") %></span>
                                    </div>
                                    <span class="sx-nota"><%#: Eval("Resumen") %></span>
                                </div>
                                <ol class="sx-linea">
                                    <asp:Repeater runat="server" DataSource='<%# Eval("Hitos") %>'>
                                        <ItemTemplate>
                                            <li class="sx-hito" data-columna='<%#: Eval("Columna") %>' data-nombre='<%#: Eval("Nombre") %>'
                                                data-hora='<%#: Eval("HoraTxt") %>' data-hora-input='<%#: Eval("HoraInput") %>' data-origen='<%#: Eval("OrigenLargo") %>'
                                                data-regla='<%#: Eval("Regla") %>' data-coords='<%#: Eval("Coords") %>' data-mapa='<%#: Eval("Mapa") %>'
                                                data-gps='<%#: Eval("GpsTxt") %>' data-motivo='<%#: Eval("Motivo") %>'>
                                                <span class='<%# "sx-punto sx-punto-" + Eval("Css") %>' aria-hidden="true"></span>
                                                <div class="sx-hito-texto">
                                                    <span><%#: Eval("Nombre") %></span>
                                                    <div class="sx-hito-linea2">
                                                        <span class='<%# (bool)Eval("Vacio") ? "sx-mono sx-hito-vacio" : "sx-mono" %>'><%#: Eval("HoraMostrada") %></span>
                                                        <span class='<%# "sx-fuente sx-fuente-" + Eval("Css") %>' style='<%# (bool)Eval("Vacio") ? "display:none" : "" %>'><%#: Eval("OrigenCorto") %></span>
                                                        <span class="sx-nota" style='<%# string.IsNullOrEmpty((string)Eval("AltTxt")) ? "display:none" : "" %>'>GPS: <span class="sx-mono"><%#: Eval("AltTxt") %></span></span>
                                                    </div>
                                                </div>
                                                <div class="sx-hito-acciones">
                                                    <button type="button" class="sx-btn sx-btn-chico sx-ver" aria-label='<%#: "Ver evidencia de " + Eval("Nombre") %>'>Evidencia</button>
                                                    <button type="button" class="sx-btn sx-btn-chico sx-corregir" aria-label='<%#: "Corregir " + Eval("Nombre") %>'>Corregir</button>
                                                </div>
                                            </li>
                                        </ItemTemplate>
                                    </asp:Repeater>
                                </ol>
                            </section>
                        </ItemTemplate>
                    </asp:Repeater>
                </div>

                <aside class="sgv-panel sx-lateral" aria-live="polite">
                    <div class="sgv-panel-cab">
                        <div>
                            <span class="sgv-panel-titulo" id="lateralTitulo">Evidencia del hito</span>
                            <span class="sx-sub" id="lateralHito">Elige "Evidencia" en un hito</span>
                        </div>
                    </div>
                    <div class="sx-evidencia" id="panelEvidencia">
                        <dl>
                            <dt>Hora</dt><dd class="sx-mono" id="evHora">—</dd>
                            <dt>Origen</dt><dd id="evOrigen">—</dd>
                            <dt>Regla</dt><dd id="evRegla">—</dd>
                            <dt>GPS</dt><dd class="sx-mono" id="evGps">—</dd>
                            <dt>Coordenadas</dt><dd><span class="sx-mono" id="evCoords">—</span> <a id="evMapa" href="#" target="_blank" rel="noopener" hidden>Ver en el mapa</a></dd>
                        </dl>
                        <p class="sx-correccion-nota" id="evMotivo" hidden></p>
                    </div>
                    <div class="sx-evidencia" id="panelCorregir" hidden>
                        <p class="sx-nota" style="margin: 0;">Hora actual: <span class="sx-mono" id="corHoraActual"></span></p>
                        <label class="sx-campo">Nueva fecha y hora
                            <asp:TextBox ID="txtNuevaHora" runat="server" TextMode="DateTimeLocal" step="1" />
                        </label>
                        <label class="sx-campo">Motivo (queda en la auditoría)
                            <asp:TextBox ID="txtMotivo" runat="server" TextMode="MultiLine" Rows="3" MaxLength="300" placeholder="Ej.: el chofer reportó la hora por WhatsApp" />
                        </label>
                        <asp:HiddenField ID="hdnColumna" runat="server" />
                        <div class="sx-barra-acciones" style="justify-content: flex-end;">
                            <button type="button" class="sx-btn" id="btnCancelarCorreccion">Cancelar</button>
                            <asp:Button ID="btnGuardarCorreccion" runat="server" Text="Guardar corrección" CssClass="sx-btn sx-btn-alerta" CausesValidation="false" OnClick="btnGuardarCorreccion_Click" />
                        </div>
                        <p class="sx-nota" style="margin: 0;">Una hora corregida a mano no la vuelve a cambiar el GPS. Para que el GPS la vuelva a llenar, deja la fecha vacía.</p>
                    </div>
                </aside>
            </div>

            <%-- Lo que el GPS no sabe: un solo formulario para los dos tramos. --%>
            <div class="sgv-panel" style="margin-top: 1rem;">
                <div class="sgv-panel-cab"><span class="sgv-panel-titulo">Lo que el GPS no sabe</span></div>
                <div class="sgv-panel-cuerpo">
                    <div class="sx-rejilla" style="grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr));">
                        <label class='<%= CampoCss(txtRegistro) %>'>F.H. Registro (lo da el chofer) <asp:TextBox ID="txtRegistro" runat="server" TextMode="DateTimeLocal" /></label>
                        <label class='<%= CampoCss(txtInicioCarga) %>'>Inicio de carga <asp:TextBox ID="txtInicioCarga" runat="server" TextMode="DateTimeLocal" /></label>
                        <label class='<%= CampoCss(txtTerminoCarga) %>'>Término de carga <asp:TextBox ID="txtTerminoCarga" runat="server" TextMode="DateTimeLocal" /></label>
                        <label class='<%= CampoCss(txtAutorizacion) %>'>Autorización de nacionalización <asp:TextBox ID="txtAutorizacion" runat="server" TextMode="DateTimeLocal" /></label>
                        <label class='<%= CampoCss(txtInicioDescarga) %>'>Inicio de descarga <asp:TextBox ID="txtInicioDescarga" runat="server" TextMode="DateTimeLocal" /></label>
                        <label class='<%= CampoCss(txtTerminoDescarga) %>'>Término de descarga <asp:TextBox ID="txtTerminoDescarga" runat="server" TextMode="DateTimeLocal" /></label>
                        <label class="sx-campo">Bodega nacional
                            <asp:DropDownList ID="ddlBodegaNacional" runat="server">
                                <asp:ListItem Value="" Text="Sin indicar" />
                                <asp:ListItem Value="COMPLEX" Text="Complex" />
                                <asp:ListItem Value="DEPSA" Text="Depsa" />
                            </asp:DropDownList>
                        </label>
                        <label class="sx-campo">Sacos robados <asp:TextBox ID="txtSacosRobados" runat="server" TextMode="Number" min="0" step="1" /></label>
                        <label class="sx-campo">Sacos rotos <asp:TextBox ID="txtSacosRotos" runat="server" TextMode="Number" min="0" step="1" /></label>
                        <label class="sx-campo">Sacos mojados <asp:TextBox ID="txtSacosMojados" runat="server" TextMode="Number" min="0" step="1" /></label>
                    </div>
                    <label class="sx-campo" style="margin-top: 0.75rem;">Motivo de retraso o incidencias
                        <asp:TextBox ID="txtMotivoRetraso" runat="server" TextMode="MultiLine" Rows="2" MaxLength="1000" />
                    </label>
                    <div class="sx-barra-acciones" style="margin-top: 0.75rem;">
                        <asp:Button ID="btnGuardarManuales" runat="server" Text="Guardar" CssClass="sx-btn sx-btn-primario" CausesValidation="false" OnClick="btnGuardarManuales_Click" />
                    </div>
                </div>
            </div>
        </asp:Panel>
    </div>

    <script>
        var SGV = Object.assign(window.SGV || {}, {
            txtNuevaHora: '<%= txtNuevaHora.ClientID %>',
            txtMotivo: '<%= txtMotivo.ClientID %>',
            hdnColumna: '<%= hdnColumna.ClientID %>',
            columnaInicial: '<%= ColumnaInicial %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/Exportacion/SeguimientoViaje.js") %>"></script>
</asp:Content>

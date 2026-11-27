<%@ Page Title="Viaje de exportación" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SeguimientoViaje.aspx.cs" Inherits="WebSGV.Views.Exportacion.SeguimientoViaje" %>

<asp:Content ID="ContentViaje" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/Exportacion/SeguimientoExportacion.css") %>" rel="stylesheet" />

    <div class="sx-wrap">
        <div class="sgv-encabezado">
            <div class="sgv-encabezado-titulo">
                <a class="sgv-volver" href="RegistroSeguimiento.aspx">Volver al tablero de seguimiento</a>
                <h1 class="h2"><asp:Literal ID="litTitulo" runat="server" /> <asp:Literal ID="litFase" runat="server" /></h1>
                <p><asp:Literal ID="litSubtitulo" runat="server" /></p>
            </div>
            <%-- El "?" queda fuera de pnlAcciones: el panel se oculta en un viaje nuevo o inexistente. --%>
            <div class="sgv-encabezado-acciones">
                <asp:Panel ID="pnlAcciones" runat="server" CssClass="sx-barra-acciones sx-acciones-viaje">
                    <button type="button" class="sx-btn" id="btnEditarDatos" aria-expanded="false" aria-controls="seccionDatos">Editar datos del viaje</button>
                    <asp:Button ID="btnExportar" runat="server" Text="Exportar a STATUS GENERAL" CssClass="sx-btn" CausesValidation="false" OnClick="btnExportar_Click" data-descarga="true" />
                    <asp:Button ID="btnEstado" runat="server" CssClass="sx-btn" CausesValidation="false" OnClick="btnEstado_Click" />
                    <asp:Button ID="btnActualizarGps" runat="server" Text="Actualizar GPS" CssClass="sx-btn sx-btn-primario" CausesValidation="false"
                        OnClick="btnActualizarGps_Click" data-accion="avisarConsultaGps" UseSubmitBehavior="false" />
                </asp:Panel>
                <a class="sgv-ayuda" href="<%: ResolveUrl("~/Views/Ayuda.aspx?tema=seguimiento") %>" target="_blank" rel="noopener"
                    data-ayuda="seguimiento" aria-label="Ayuda de esta p&#xE1;gina">?</a>
            </div>
        </div>

        <asp:Panel ID="pnlMensaje" runat="server" Visible="false" EnableViewState="false">
            <asp:Literal ID="litMensaje" runat="server" />
        </asp:Panel>
        <%-- Viaje vencido cerrado desde la pestaña Vencidos del tablero (sp_SE_CerrarVencido). --%>
        <asp:Panel ID="pnlCierreAdministrativo" runat="server" Visible="false" CssClass="sx-mensaje sx-mensaje-alerta" role="status">
            <asp:Literal ID="litCierreAdministrativo" runat="server" />
        </asp:Panel>

        <%-- Datos de cabecera: abierto al crear un viaje, plegado al editar. --%>
        <div class="sgv-panel mb-3">
            <div id="seccionDatos" data-enter="<%= btnGuardarDatos.ClientID %>" class='<%= ModoNuevo ? "sx-cabecera-datos sx-cabecera-datos-sin-borde sx-abierto" : "sx-cabecera-datos sx-cabecera-datos-sin-borde" %>'>
                <div class="sx-rejilla sx-rejilla-12">
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
                <div class="sx-barra-acciones sx-mt">
                    <asp:Button ID="btnGuardarDatos" runat="server" CssClass="sx-btn sx-btn-primario" CausesValidation="false" OnClick="btnGuardarDatos_Click" />
                    <span class="sx-nota"><asp:Literal ID="litNotaDatos" runat="server" /></span>
                </div>
            </div>
        </div>

        <asp:Panel ID="pnlViaje" runat="server">
            <%-- Ruta del viaje: el recorrido GPS de cada tramo, con sus hitos y una reproducción en el tiempo. --%>
            <section class="sgv-panel sx-ruta" aria-labelledby="rutaTitulo">
                <div class="sgv-panel-cab">
                    <div>
                        <span class="sgv-panel-titulo" id="rutaTitulo">Ruta del viaje</span>
                        <span class="sx-sub" id="rutaSub">Recorrido del GPS de cada tracto</span>
                    </div>
                    <div class="sx-ruta-capas" role="group" aria-label="Capas del mapa">
                        <label class="sx-capa sx-capa-carga"><input type="checkbox" data-capa="carga" checked /> Carga <span class="sx-mono" id="rutaKmcarga"></span></label>
                        <label class="sx-capa sx-capa-descarga"><input type="checkbox" data-capa="descarga" checked /> Descarga <span class="sx-mono" id="rutaKmdescarga"></span></label>
                        <label class="sx-capa sx-capa-paradas"><input type="checkbox" data-capa="paradas" checked /> Paradas</label>
                        <label class="sx-capa"><input type="checkbox" data-capa="zonas" checked /> Zonas</label>
                    </div>
                </div>
                <div class="sx-ruta-caja">
                    <div id="rutaMapa" class="sx-ruta-mapa" role="region" aria-label="Mapa del recorrido"></div>
                    <div class="sx-ruta-estado" id="rutaEstado" role="status"><span class="sx-ruta-girando" aria-hidden="true"></span> Cargando el recorrido del GPS…</div>
                    <div class="sx-ruta-reloj" id="rutaReloj" hidden>
                        <span class="sx-mono" id="rutaHora"></span>
                        <span id="rutaDetalle"></span>
                    </div>
                </div>
                <%-- Resumen del recorrido (km, tiempo en marcha y detenido) y línea de tiempo con hitos y paradas. --%>
                <div class="sx-ruta-resumen" id="rutaResumen" hidden></div>
                <div class="sx-ruta-linea" id="rutaLinea" hidden>
                    <div class="sx-ruta-linea-pista" id="rutaPista" title="Haz clic o arrastra para ir a ese momento del viaje"></div>
                    <div class="sx-ruta-linea-dias" id="rutaDias" aria-hidden="true"></div>
                    <div class="sx-ruta-linea-leyenda">
                        <span><i class="sx-ley sx-ley-hito"></i> Hito</span>
                        <span><i class="sx-ley sx-ley-parada"></i> Parada de 30 min o más</span>
                        <span><i class="sx-ley sx-ley-cursor"></i> Momento que muestra el mapa</span>
                    </div>
                </div>
                <div class="sx-ruta-control" id="rutaControl" hidden>
                    <button type="button" class="sx-btn sx-btn-primario sx-btn-chico" id="rutaPlay"><i class="fas fa-play" aria-hidden="true"></i> <span>Reproducir</span></button>
                    <input type="range" id="rutaTiempo" min="0" max="1000" value="0" step="1" aria-label="Momento del viaje" />
                    <label class="sx-nota" for="rutaVelocidad">Velocidad</label>
                    <select id="rutaVelocidad" class="sx-ruta-select">
                        <option value="600">10 min por segundo</option>
                        <option value="1800">30 min por segundo</option>
                        <option value="3600" selected>1 h por segundo</option>
                        <option value="10800">3 h por segundo</option>
                    </select>
                    <label class="sx-ruta-seguir"><input type="checkbox" id="rutaSeguir" /> Seguir al camión</label>
                    <button type="button" class="sx-btn sx-btn-chico" id="rutaAjustar">Ver todo</button>
                </div>
                <ul class="sx-ruta-avisos" id="rutaAvisos"></ul>
            </section>

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
                                                <span class='<%#: "sx-punto sx-punto-" + Eval("Css") %>' aria-hidden="true"></span>
                                                <div class="sx-hito-texto">
                                                    <span><%#: Eval("Nombre") %></span>
                                                    <div class="sx-hito-linea2">
                                                        <span class='<%#: (bool)Eval("Vacio") ? "sx-mono sx-hito-vacio" : "sx-mono" %>'><%#: Eval("HoraMostrada") %></span>
                                                        <span class='<%#: "sx-fuente sx-fuente-" + Eval("Css") + ((bool)Eval("Vacio") ? " d-none" : "") %>'><%#: Eval("OrigenCorto") %></span>
                                                        <span class='<%#: string.IsNullOrEmpty((string)Eval("AltTxt")) ? "sx-nota d-none" : "sx-nota" %>'>GPS: <span class="sx-mono"><%#: Eval("AltTxt") %></span></span>
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
                        <p class="sx-nota m-0">Hora actual: <span class="sx-mono" id="corHoraActual"></span></p>
                        <label class="sx-campo">Nueva fecha y hora
                            <asp:TextBox ID="txtNuevaHora" runat="server" TextMode="DateTimeLocal" step="1" />
                        </label>
                        <label class="sx-campo">Motivo (queda en la auditoría)
                            <asp:TextBox ID="txtMotivo" runat="server" TextMode="MultiLine" Rows="3" MaxLength="300" placeholder="Ej.: el chofer reportó la hora por WhatsApp" />
                        </label>
                        <asp:HiddenField ID="hdnColumna" runat="server" />
                        <div class="sx-barra-acciones justify-content-end">
                            <button type="button" class="sx-btn" id="btnCancelarCorreccion">Cancelar</button>
                            <asp:Button ID="btnGuardarCorreccion" runat="server" Text="Guardar corrección" CssClass="sx-btn sx-btn-alerta" CausesValidation="false" OnClick="btnGuardarCorreccion_Click" />
                        </div>
                        <p class="sx-nota m-0">Una hora corregida a mano no la vuelve a cambiar el GPS. Para que el GPS la vuelva a llenar, deja la fecha vacía.</p>
                    </div>
                </aside>
            </div>

            <%-- Lo que el GPS no sabe: un solo formulario para los dos tramos. --%>
            <div class="sgv-panel mt-3">
                <div class="sgv-panel-cab"><span class="sgv-panel-titulo">Lo que el GPS no sabe</span></div>
                <div class="sgv-panel-cuerpo">
                    <div class="sx-rejilla sx-rejilla-13">
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
                        <label class="sx-campo">Sacos robados <asp:TextBox ID="txtSacosRobados" runat="server" TextMode="Number" min="0" step="1" inputmode="numeric" /></label>
                        <label class="sx-campo">Sacos rotos <asp:TextBox ID="txtSacosRotos" runat="server" TextMode="Number" min="0" step="1" inputmode="numeric" /></label>
                        <label class="sx-campo">Sacos mojados <asp:TextBox ID="txtSacosMojados" runat="server" TextMode="Number" min="0" step="1" inputmode="numeric" /></label>
                    </div>
                    <label class="sx-campo sx-mt">Motivo de retraso o incidencias
                        <asp:TextBox ID="txtMotivoRetraso" runat="server" TextMode="MultiLine" Rows="2" MaxLength="1000" />
                    </label>
                    <div class="sx-barra-acciones sx-mt">
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
            columnaInicial: '<%= HttpUtility.JavaScriptStringEncode(ColumnaInicial) %>',
            idSeguimiento: <%= ModoNuevo ? 0 : IdSeguimientoPublico %>
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/Exportacion/SeguimientoViaje.js") %>"></script>
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/lib/leaflet-1.9.4/leaflet.css") %>" rel="stylesheet" />
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/lib/leaflet-1.9.4.min.js") %>"></script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/Exportacion/SeguimientoViajeMapa.js") %>"></script>
</asp:Content>

<%@ Page Title="Tablero de seguimiento" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroSeguimiento.aspx.cs" Inherits="WebSGV.Views.Exportacion.RegistroSeguimiento" %>

<asp:Content ID="ContentSE" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/Exportacion/SeguimientoExportacion.css") %>" rel="stylesheet" />

    <div class="sx-wrap">
        <div class="sgv-encabezado">
            <div class="sgv-encabezado-titulo">
                <h1 class="h2">Tablero de seguimiento</h1>
                <p>Un viaje por cada carreta de los despachos de exportación y de los clientes terceros. Las horas salen del GPS; tú completas lo que el GPS no sabe.</p>
            </div>
            <div class="sx-barra-acciones sgv-encabezado-acciones">
                <span class="sx-nota"><asp:Literal ID="litUltimaConsulta" runat="server" /></span>
                <%-- A la vista, los reportes que se envían y Actualizar GPS; el resto queda en "Más". --%>
                <%-- Cada reporte en Excel y, al lado, el mismo como imagen (para mandarlo por WhatsApp desde el celular).
                     En el celular los reportes se pliegan bajo "Reportes"; en escritorio se ven en fila como siempre. --%>
                <details class="sgv-plegable sx-reportes" open data-plegar-movil>
                    <summary>Reportes</summary>
                    <div class="sgv-plegable-cuerpo">
                        <span class="sx-par">
                            <asp:Button ID="btnExportarStatus" runat="server" Text="Excel de STATUS" CssClass="sx-btn" CausesValidation="false" OnClick="btnExportarStatus_Click" data-descarga="true"
                                ToolTip="Baja un Excel liviano con el STATUS de las unidades que muestra la tabla (con el pedido en Buscar, solo ese pedido)" />
                            <asp:LinkButton ID="btnImagenStatus" runat="server" CssClass="sx-btn sx-btn-imagen" CausesValidation="false" OnClick="btnImagenStatus_Click" data-descarga="true"
                                ToolTip="El STATUS como imagen, para mandarlo por WhatsApp"><span aria-hidden="true">Imagen</span><span class="sr-only">STATUS como imagen</span></asp:LinkButton>
                        </span>
                        <asp:Button ID="btnExportar" runat="server" Text="Exportar STATUS GENERAL" CssClass="sx-btn" CausesValidation="false" OnClick="btnExportar_Click" data-descarga="true"
                            ToolTip="STATUS GENERAL de Vitapro con todas las horas (sin los clientes terceros)" />
                        <span class="sx-par">
                            <asp:Button ID="btnExportarStatusExtra" runat="server" Text="Excel STATUS EXTRA" CssClass="sx-btn" CausesValidation="false" OnClick="btnExportarStatusExtra_Click" data-descarga="true"
                                ToolTip="Cuadro de los clientes terceros (Crisar, Novopan, Polinplast) que muestra la tabla, con las columnas del STATUS EXTRA" />
                            <asp:LinkButton ID="btnImagenStatusExtra" runat="server" CssClass="sx-btn sx-btn-imagen" CausesValidation="false" OnClick="btnImagenStatusExtra_Click" data-descarga="true"
                                ToolTip="El STATUS EXTRA como imagen, para mandarlo por WhatsApp"><span aria-hidden="true">Imagen</span><span class="sr-only">STATUS EXTRA como imagen</span></asp:LinkButton>
                        </span>
                        <span class="sx-par">
                            <asp:Button ID="btnExportarGrifo" runat="server" Text="Cuadro grifo Santa Rosa" CssClass="sx-btn" CausesValidation="false" OnClick="btnExportarGrifo_Click" data-descarga="true"
                                ToolTip="Excel de los carros que bajan al grifo Santa Rosa a cargar combustible (Vitapro y Crisar de vuelta de Ecuador): en camino con su hora aproximada de llegada, en el grifo y descargando" />
                            <asp:LinkButton ID="btnImagenGrifo" runat="server" CssClass="sx-btn sx-btn-imagen" CausesValidation="false" OnClick="btnImagenGrifo_Click" data-descarga="true"
                                ToolTip="El cuadro del grifo Santa Rosa como imagen, para mandarlo por WhatsApp"><span aria-hidden="true">Imagen</span><span class="sr-only">Cuadro grifo como imagen</span></asp:LinkButton>
                        </span>
                        <span class="sx-par">
                            <asp:Button ID="btnExportarLlegadas" runat="server" Text="Excel de llegadas" CssClass="sx-btn" CausesValidation="false" OnClick="btnExportarLlegadas_Click" data-descarga="true"
                                ToolTip="Las unidades en camino a cargar a Trujillo o a descargar a Jave/Inbalnor, con ubicación, km restantes y llegada aproximada" />
                            <asp:LinkButton ID="btnImagenLlegadas" runat="server" CssClass="sx-btn sx-btn-imagen" CausesValidation="false" OnClick="btnImagenLlegadas_Click" data-descarga="true"
                                ToolTip="Las llegadas como imagen, para mandarlas por WhatsApp"><span aria-hidden="true">Imagen</span><span class="sr-only">Llegadas como imagen</span></asp:LinkButton>
                        </span>
                    </div>
                </details>
                <div class="dropdown sx-mas">
                    <button type="button" class="sx-btn dropdown-toggle" data-toggle="dropdown" aria-haspopup="true" aria-expanded="false">Más</button>
                    <div class="dropdown-menu dropdown-menu-right">
                        <button type="button" class="dropdown-item" id="btnReporteLlegadas" title="Copia, listo para enviar, el estado y la hora aproximada de llegada de las unidades que van en camino a cargar a Trujillo o a descargar a Jave/Inbalnor">Copiar reporte de llegadas</button>
                        <button type="button" class="dropdown-item" id="btnReporteGrifo" title="Copia, listo para enviar al grifo, cuántos y cuáles carros bajan al grifo Santa Rosa y a qué hora llegan aprox.">Copiar reporte grifo Santa Rosa</button>
                        <button type="button" class="dropdown-item" id="btnCopiarStatus" title="Copia el STATUS de cada fila de la tabla, en orden, para pegar en la columna STATUS del Excel">Copiar STATUS</button>
                        <div class="dropdown-divider"></div>
                        <a class="dropdown-item" href="SeguimientoViaje.aspx?nuevo=1">Nuevo viaje</a>
                        <a class="dropdown-item" href="ImportarSeguimiento.aspx">Importar Excel</a>
                        <a class="dropdown-item" href="DashboardExportacion.aspx">Dashboard</a>
                    </div>
                </div>
                <textarea id="txtReporteLlegadas" hidden readonly aria-hidden="true"><asp:Literal ID="litReporteLlegadas" runat="server" /></textarea>
                <textarea id="txtReporteGrifo" hidden readonly aria-hidden="true"><asp:Literal ID="litReporteGrifo" runat="server" /></textarea>
                <asp:Button ID="btnActualizarGps" runat="server" Text="Actualizar GPS de viajes abiertos" CssClass="sx-btn sx-btn-primario" CausesValidation="false"
                    OnClick="btnActualizarGps_Click" data-accion="actualizarGps" data-espera="300" UseSubmitBehavior="false" />
                <a class="sgv-ayuda" href="<%: ResolveUrl("~/Views/Ayuda.aspx?tema=seguimiento") %>" target="_blank" rel="noopener"
                    data-ayuda="seguimiento" aria-label="Ayuda de esta p&#xE1;gina">?</a>
            </div>
        </div>

        <asp:Panel ID="pnlMensaje" runat="server" EnableViewState="false" Visible="false">
            <asp:Literal ID="litMensaje" runat="server" EnableViewState="false" />
        </asp:Panel>
        <%-- La actualización automática trajo datos nuevos mientras la página estaba abierta. --%>
        <div class="sx-mensaje sx-mensaje-info sx-aviso-gps" id="avisoGpsNuevo" role="status" hidden>
            <span>El GPS se actualizó a las <strong id="avisoGpsHora"></strong>.</span>
            <button type="button" class="sx-btn sx-btn-chico" id="btnVerDatosNuevos">Ver datos nuevos</button>
        </div>

        <asp:HiddenField ID="hdnFase" runat="server" Value="encurso" />
        <%-- "Cerrar" de una fila de Vencidos: SGV.acciones.enviarFila deja el id aquí y pulsa btnAccionFila. El servidor lo revalida. --%>
        <asp:HiddenField ID="hfArgumentoFila" runat="server" />
        <asp:Button ID="btnAccionFila" runat="server" Text="Cerrar viaje vencido" CssClass="d-none" CausesValidation="false"
            OnClick="btnAccionFila_Click" tabindex="-1" aria-hidden="true" />

        <details class="sgv-filtros" open data-plegar-movil>
            <summary>Filtros <span class="sgv-filtros-cuenta"></span></summary>
            <div class="sgv-filtros-cuerpo sx-filtros" data-enter="<%= btnFiltrar.ClientID %>">
                <label class="sx-campo">Buscar
                    <asp:TextBox ID="txtBuscar" runat="server" placeholder="Pedido, carreta, placa o chofer" CssClass="sx-buscar" />
                </label>
                <label class="sx-campo">Programación desde
                    <asp:TextBox ID="txtDesde" runat="server" TextMode="Date" />
                </label>
                <label class="sx-campo">hasta
                    <asp:TextBox ID="txtHasta" runat="server" TextMode="Date" />
                </label>
                <label class="sx-campo">Cliente
                    <asp:DropDownList ID="ddlCliente" runat="server">
                        <asp:ListItem Value="" Text="Todos" />
                        <asp:ListItem Value="VITAPRO" Text="Vitapro" />
                        <asp:ListItem Value="TERCEROS" Text="Terceros (todos)" />
                        <asp:ListItem Value="CRISAR" Text="Crisar" />
                        <asp:ListItem Value="NOVOPAN" Text="Novopan" />
                        <asp:ListItem Value="POLINPLAST" Text="Polinplast" />
                        <asp:ListItem Value="AFRECHO" Text="Afrecho (Paita)" />
                    </asp:DropDownList>
                </label>
                <label class="sx-campo">Planta de descarga
                    <asp:DropDownList ID="ddlPlanta" runat="server">
                        <asp:ListItem Value="" Text="Todas" />
                        <asp:ListItem Value="JAVE" Text="Jave" />
                        <asp:ListItem Value="INBALNOR" Text="Inbalnor" />
                    </asp:DropDownList>
                </label>
                <label class="sx-campo sx-campo-casilla">
                    <asp:CheckBox ID="chkHistorial" runat="server" />
                    Incluir los importados del Excel
                </label>
                <asp:Button ID="btnFiltrar" runat="server" Text="Buscar" CssClass="sx-btn" CausesValidation="false" OnClick="btnFiltrar_Click" />
            </div>
        </details>
        <p class="sx-nota sx-filtro-cuenta" id="sxFiltroCuenta" role="status"></p>

        <%-- Con más viajes que el tope de la tabla (p. ej. "Incluir los importados" y un año entero): avisa que no están todos. --%>
        <asp:Panel ID="pnlTope" runat="server" Visible="false" CssClass="sx-mensaje sx-mensaje-alerta" role="status">
            <asp:Literal ID="litTope" runat="server" />
        </asp:Panel>

        <div class="sgv-tabs" role="tablist" aria-label="Fase del viaje">
            <asp:Repeater ID="rptFases" runat="server" OnItemCommand="rptFases_ItemCommand">
                <ItemTemplate>
                    <asp:LinkButton runat="server" CommandName="Fase" CommandArgument='<%# Eval("Grupo") %>' CausesValidation="false"
                        CssClass='<%# (bool)Eval("Activo") ? "sgv-tab activo" : "sgv-tab" %>' role="tab" aria-selected='<%# (bool)Eval("Activo") ? "true" : "false" %>'>
                        <%#: Eval("Nombre") %> <span class='<%#: Eval("ContadorCss") %>'><%#: Eval("Cantidad") %></span>
                    </asp:LinkButton>
                </ItemTemplate>
            </asp:Repeater>
        </div>

        <%-- Cierre administrativo de los vencidos: solo en la pestaña Vencidos y para un administrador (el servidor lo revisa). --%>
        <asp:Panel ID="pnlCierre" runat="server" Visible="false" CssClass="sgv-panel sx-cierre">
            <p class="sx-cierre-nota">Un viaje vencido sigue abierto hace más de 10 días (un viaje normal dura hasta 9). Si ya terminó, ciérralo
                aquí: pasa a <strong>FINALIZADO</strong> con el motivo, la fecha y tu usuario, y queda en la auditoría. Marca los que quieras cerrar
                o usa <strong>Cerrar</strong> en cada fila.</p>
            <div class="sx-cierre-campos">
                <label class="sx-campo">Motivo del cierre
                    <asp:DropDownList ID="ddlMotivoCierre" runat="server" data-accion-change="mostrarMotivoOtro" />
                </label>
                <label class="sx-campo sx-cierre-otro" id="campoMotivoOtro">Escribe el motivo
                    <asp:TextBox ID="txtMotivoOtro" runat="server" MaxLength="200" placeholder="Hasta 200 caracteres" />
                </label>
                <label class="sx-campo sx-campo-casilla sx-cierre-todos">
                    <input type="checkbox" id="chkCierreTodos" data-accion-change="marcarVencidosPagina" />
                    Marcar los de esta página
                </label>
                <asp:Button ID="btnCerrarSeleccionados" runat="server" Text="Cerrar los seleccionados" CssClass="sx-btn sx-btn-alerta"
                    ValidationGroup="cierre" data-validar-grupo="cierre" OnClick="btnCerrarSeleccionados_Click"
                    data-confirmar="¿Cerrar administrativamente los viajes vencidos marcados?\nPasan a FINALIZADO con el motivo elegido y queda en la auditoría."
                    data-confirmar-titulo="Cerrar viajes vencidos" data-confirmar-aceptar="Cerrar" data-confirmar-peligro="true" />
            </div>
            <asp:CustomValidator ID="cvMotivoCierre" runat="server" ControlToValidate="ddlMotivoCierre" ValidateEmptyText="true"
                ClientValidationFunction="sxValidarMotivoCierre" ErrorMessage="Elige el motivo del cierre (con &quot;Otro&quot;, escríbelo)."
                Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="cierre" />
        </asp:Panel>

        <div class="sgv-panel sx-tabla-caja">
            <%-- El mismo paginador arriba de la tabla, solo en el celular (CSS); el aviso para el lector de pantalla queda en el de abajo. --%>
            <nav class="sx-paginador sx-paginador-arriba" aria-label="Páginas del tablero (arriba)" hidden>
                <span class="sx-paginador-texto"></span>
                <button type="button" class="sx-btn" data-pagina="anterior">Anterior</button>
                <button type="button" class="sx-btn" data-pagina="siguiente">Siguiente</button>
            </nav>
            <table class="table sgv-tabla sx-tablero">
                <thead>
                    <tr><th>Pedido / carreta</th><th>Carga</th><th>Descarga</th><th>Fase y avance</th><th>STATUS y último hito</th><th>Pendiente</th></tr>
                </thead>
                <tbody>
                    <asp:Repeater ID="rptViajes" runat="server" EnableViewState="false">
                        <ItemTemplate>
                            <tr class="sx-fila" data-pedido='<%#: Eval("Pedido") %>' data-prog='<%#: Eval("Programacion") %>'>
                                <td data-label="Pedido / carreta"><div class="sx-celda">
                                    <a class="sx-mono sx-pedido" href='SeguimientoViaje.aspx?id=<%#: Eval("Id") %>'><%#: Eval("Pedido") %></a>
                                    <span class="sx-sub">Carreta <span class="sx-mono"><%#: Eval("Carreta") %></span></span>
                                    <span class="sx-sub sx-prog">Prog. <%#: Eval("Programacion") %></span>
                                </div></td>
                                <td data-label="Carga"><div class="sx-celda"><span class="sx-mono"><%#: Eval("Tracto1") %></span><span class="sx-sub"><%#: Eval("Chofer1") %></span></div></td>
                                <td data-label="Descarga"><div class="sx-celda"><span class='<%#: Convert.ToString(Eval("Tracto2")) == "Sin asignar" ? "sx-sin-asignar" : "sx-mono" %>'><%#: Eval("Tracto2") %></span><span class="sx-sub"><%#: Eval("Chofer2") %></span></div></td>
                                <td data-label="Fase y avance"><div class="sx-celda">
                                    <span class='<%#: "sx-fase sx-fase-" + Eval("Grupo") %>'><%#: Eval("Fase") %></span><span class="sx-sub"><%#: Eval("Planta") %></span>
                                    <div class="sx-avance">
                                        <div class="sx-avance-fila"><span>Carga</span><progress class="sx-avance-barra sx-avance-carga" max="100" value='<%#: Eval("CargaPct") %>' aria-hidden="true"></progress><span class="sx-mono"><%#: Eval("CargaTxt") %></span></div>
                                        <div class="sx-avance-fila"><span>Descarga</span><progress class="sx-avance-barra sx-avance-descarga" max="100" value='<%#: Eval("DescargaPct") %>' aria-hidden="true"></progress><span class="sx-mono"><%#: Eval("DescargaTxt") %></span></div>
                                    </div>
                                </div></td>
                                <td class="sx-status" data-label="STATUS"><div class="sx-celda">
                                    <span class="sx-status-texto"><%#: Eval("Status") %></span>
                                    <button type="button" class="sx-copiar" data-copiar title="Copiar este STATUS" aria-label="Copiar este STATUS"><i class="fas fa-copy" aria-hidden="true"></i></button>
                                    <span class='<%#: "sx-sub sx-mono " + Eval("UbicacionClase") %>'><%#: Eval("Ubicacion") %></span>
                                    <span class="sx-sub sx-llegada"><%#: Eval("Llegada") %></span>
                                    <span class="sx-sub sx-ultimo">Último hito: <%#: Eval("UltimoHito") %> <span class="sx-mono"><%#: Eval("UltimoHora") %></span></span>
                                </div></td>
                                <td data-label="Pendiente"><div class="sx-celda">
                                    <div class="sx-avisos">
                                        <asp:Repeater runat="server" DataSource='<%# Eval("Avisos") %>'>
                                            <ItemTemplate><span class='<%#: "sx-aviso sx-aviso-" + Eval("Css") %>'><%#: Eval("Texto") %></span></ItemTemplate>
                                        </asp:Repeater>
                                    </div>
                                    <asp:PlaceHolder runat="server" Visible='<%# (bool)Eval("PuedeCerrar") %>'>
                                        <div class="sx-cierre-fila">
                                            <label class="sx-cierre-sel"><input type="checkbox" name="cierreSel" value='<%#: Eval("Id") %>' data-accion-change="contarVencidosMarcados" /> Marcar</label>
                                            <button type="button" class="sx-btn sx-btn-chico sx-btn-cerrar" data-accion="enviarFila" data-argumento='<%#: Eval("Id") %>'
                                                data-validar-grupo="cierre" data-confirmar='<%#: Eval("ConfirmarCierre") %>' data-confirmar-titulo="Cerrar viaje vencido"
                                                data-confirmar-aceptar="Cerrar" data-confirmar-peligro="true">Cerrar</button>
                                        </div>
                                    </asp:PlaceHolder>
                                </div></td>
                            </tr>
                        </ItemTemplate>
                    </asp:Repeater>
                </tbody>
            </table>
            <%-- Paginación en el navegador (RegistroSeguimiento.js): 25 viajes por página sin partir un pedido; solo con más de 25. --%>
            <nav class="sx-paginador" id="sxPaginador" aria-label="Páginas del tablero" hidden>
                <span class="sx-paginador-texto" id="sxPaginadorTexto" role="status"></span>
                <button type="button" class="sx-btn" id="sxPaginaAnterior" data-pagina="anterior">Anterior</button>
                <button type="button" class="sx-btn" id="sxPaginaSiguiente" data-pagina="siguiente">Siguiente</button>
            </nav>
            <asp:Panel ID="pnlVacio" runat="server" Visible="false" CssClass="sgv-vacio">
                No hay viajes en esta fase con los filtros elegidos.
            </asp:Panel>
        </div>

        <p class="sx-nota mt-3"><asp:Literal ID="litResumen" runat="server" /></p>
    </div>
    <script>var SGV = Object.assign(window.SGV || {}, { autoActualizarGps: <%= AutoActualizarGps ? "true" : "false" %>, btnActualizarGps: '<%= btnActualizarGps.ClientID %>',
        btnFiltrar: '<%= btnFiltrar.ClientID %>', txtBuscar: '<%= txtBuscar.ClientID %>', ultimaConsultaGps: '<%= HttpUtility.JavaScriptStringEncode(UltimaConsultaGps) %>', urlEstadoGps: '<%= ResolveUrl("~/Views/Exportacion/GpsAutomatico.ashx?estado=1") %>',
        urlIniciarGps: '<%= ResolveUrl("~/Views/Exportacion/GpsAutomatico.ashx?iniciar=1") %>',
        hfArgumentoFila: '<%= hfArgumentoFila.ClientID %>', btnAccionFila: '<%= btnAccionFila.ClientID %>', ddlMotivoCierre: '<%= ddlMotivoCierre.ClientID %>',
        txtMotivoOtro: '<%= txtMotivoOtro.ClientID %>', btnCerrarSeleccionados: '<%= btnCerrarSeleccionados.ClientID %>', motivoOtro: '<%= HttpUtility.JavaScriptStringEncode(WebSGV.Services.Exportacion.ViajeVencido.MotivoOtro) %>' });</script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/Exportacion/RegistroSeguimiento.js") %>"></script>
</asp:Content>

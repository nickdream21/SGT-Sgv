<%@ Page Title="Mi viaje - Conductor" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="DashboardConductor.aspx.cs" Inherits="WebSGV.Views.DashboardConductor" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdConductor" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdViajeActivo" runat="server" ClientIDMode="Static" />
    <asp:HiddenField ID="hfIdsViajesActivos" runat="server" ClientIDMode="Static" Value="" />
    <asp:HiddenField ID="hfEsInternacional" runat="server" ClientIDMode="Static" Value="0" />
    <asp:HiddenField ID="hfGastosFinancieros" runat="server" ClientIDMode="Static" Value="[]" />
    <asp:HiddenField ID="hfGastosAdicionales" runat="server" ClientIDMode="Static" Value="[]" />
    <asp:HiddenField ID="hfNumeroOrdenExistente" runat="server" ClientIDMode="Static" Value="" />

    <div class="dc">

        <!-- Encabezado -->
        <header class="dc-cab">
            <div class="dc-cab-texto">
                <div class="dc-nombre"><asp:Label ID="lblNombreConductor" runat="server"></asp:Label></div>
                <div class="dc-dni">DNI <asp:Label ID="lblDNIConductor" runat="server"></asp:Label></div>
            </div>
            <asp:Panel ID="pnlEstadoViaje" runat="server" CssClass="dc-estado dc-estado-sin">
                <asp:Label ID="lblEstadoViaje" runat="server" Text="Sin viaje activo"></asp:Label>
            </asp:Panel>
        </header>

        <!-- Mensajes del servidor (errores, rechazo) -->
        <asp:Panel ID="pnlMensajes" runat="server" Visible="false" CssClass="dc-mensajes">
            <asp:Label ID="lblMensaje" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Pestañas -->
        <nav class="nav dc-tabs" id="conductorTabs" role="tablist">
            <a class="dc-tab active" id="viajesActivos-tab" data-toggle="tab" href="#viajesActivos" role="tab">
                <i class="fas fa-truck"></i><span>Mi viaje</span>
                <asp:Panel ID="pnlBadgeActivos" runat="server" CssClass="dc-tab-num" Visible="false">
                    <asp:Label ID="lblCantidadActivos" runat="server"></asp:Label>
                </asp:Panel>
            </a>
            <a class="dc-tab" id="liquidarViaje-tab" data-toggle="tab" href="#liquidarViaje" role="tab">
                <i class="fas fa-receipt"></i><span>Liquidar</span>
                <asp:Panel ID="pnlBadgeLiquidar" runat="server" CssClass="dc-tab-punto" Visible="false"></asp:Panel>
            </a>
            <a class="dc-tab" id="historial-tab" data-toggle="tab" href="#historial" role="tab">
                <i class="fas fa-history"></i><span>Historial</span>
            </a>
        </nav>

        <div class="tab-content">

            <!-- ============ MI VIAJE ============ -->
            <div class="tab-pane fade show active" id="viajesActivos" role="tabpanel">

                <asp:Panel ID="pnlViajeActivo" runat="server" Visible="false">
                    <section class="dc-card">
                        <div class="dc-card-tit">Viaje en curso</div>
                        <dl class="dc-datos">
                            <div><dt>N° de viaje</dt><dd><asp:Label ID="lblNumeroViaje" runat="server"></asp:Label></dd></div>
                            <div><dt>Inicio</dt><dd><asp:Label ID="lblFechaInicio" runat="server"></asp:Label></dd></div>
                            <div><dt>Días en ruta</dt><dd><asp:Label ID="lblDiasRuta" runat="server"></asp:Label></dd></div>
                            <div><dt>Despachos</dt><dd><asp:Label ID="lblCantidadDespachos" runat="server"></asp:Label></dd></div>
                        </dl>
                        <button type="button" class="dc-btn dc-btn-primario dc-btn-bloque" onclick="irALiquidar()">
                            Liquidar mi viaje
                        </button>
                    </section>
                </asp:Panel>

                <asp:Panel ID="pnlSinViajes" runat="server" Visible="true">
                    <div class="dc-vacio">
                        <i class="fas fa-truck"></i>
                        <p class="dc-vacio-tit">No tienes viajes activos</p>
                        <p>Cuando la oficina te programe un despacho aparecerá aquí.</p>
                    </div>
                </asp:Panel>

                <asp:Panel ID="pnlDespachos" runat="server" Visible="false">
                    <div class="dc-subtit">Despachos de este viaje</div>
                    <asp:Repeater ID="rptDespachosActivos" runat="server">
                        <ItemTemplate>
                            <article class="dc-item">
                                <div class="dc-item-fila">
                                    <strong><%#: Eval("NumeroDespacho") %></strong>
                                    <span class='dc-pill dc-pill-<%# ObtenerClaseEstado(Eval("EstadoDespacho")) %>'><%#: Eval("EstadoDespacho") %></span>
                                </div>
                                <div class="dc-item-cliente"><%#: Eval("Cliente") %></div>
                                <div class="dc-item-meta">
                                    <span><%# Eval("FechaDespacho", "{0:dd/MM/yyyy}") %></span>
                                    <span><%#: Eval("TipoOperacion") %></span>
                                    <span><%#: Eval("LugarOperacion") %></span>
                                </div>
                                <div class="dc-item-placas">
                                    <span class="dc-placa"><%#: Eval("PlacaTracto") %></span>
                                    <span class="dc-placa"><%#: Eval("PlacaCarreta") %></span>
                                </div>
                            </article>
                        </ItemTemplate>
                    </asp:Repeater>
                </asp:Panel>
            </div>

            <!-- ============ LIQUIDAR ============ -->
            <div class="tab-pane fade" id="liquidarViaje" role="tabpanel">

                <asp:Panel ID="pnlSinViajeParaLiquidar" runat="server" Visible="true">
                    <div class="dc-vacio">
                        <i class="fas fa-receipt"></i>
                        <p class="dc-vacio-tit">No hay viajes para liquidar</p>
                        <p>Necesitas un viaje activo para registrar tu liquidación.</p>
                    </div>
                </asp:Panel>

                <asp:Panel ID="pnlFormularioLiquidacion" runat="server" Visible="false" CssClass="dc-form">

                    <!-- 1. Viaje -->
                    <section class="dc-card">
                        <div class="dc-card-tit"><span class="dc-paso">1</span>Tu viaje</div>
                        <dl class="dc-datos">
                            <div><dt>N° de viaje</dt><dd><asp:Label ID="lblNumeroViajeResumen" runat="server"></asp:Label></dd></div>
                            <div><dt>Despachos</dt><dd><asp:Label ID="lblDespachosResumen" runat="server"></asp:Label></dd></div>
                        </dl>

                        <div class="dc-hito">
                            <div class="dc-hito-tit">Salida de base</div>
                            <asp:Panel ID="pnlSalidaGps" runat="server" Visible="false" CssClass="dc-hito-gps">
                                <asp:Label ID="lblSalidaGps" runat="server" CssClass="dc-hito-valor"></asp:Label>
                                <span class="dc-gps"><i class="fas fa-satellite-dish"></i>GPS</span>
                            </asp:Panel>
                            <asp:Panel ID="pnlSalidaManual" runat="server" CssClass="dc-hito-manual">
                                <p class="dc-ayuda">El GPS no registró tu salida. Indica cuándo saliste de base.</p>
                                <div class="dc-par">
                                    <asp:TextBox ID="txtFechaSalida" runat="server" CssClass="form-control" TextMode="Date" aria-label="Fecha de salida"></asp:TextBox>
                                    <asp:TextBox ID="txtHoraSalida" runat="server" CssClass="form-control" TextMode="Time" aria-label="Hora de salida"></asp:TextBox>
                                </div>
                            </asp:Panel>
                        </div>

                        <div class="dc-hito">
                            <div class="dc-hito-tit">Llegada a base</div>
                            <asp:Panel ID="pnlLlegadaGps" runat="server" Visible="false" CssClass="dc-hito-gps">
                                <asp:Label ID="lblLlegadaGps" runat="server" CssClass="dc-hito-valor"></asp:Label>
                                <span class="dc-gps"><i class="fas fa-satellite-dish"></i>GPS</span>
                            </asp:Panel>
                            <asp:Panel ID="pnlLlegadaManual" runat="server" CssClass="dc-hito-manual">
                                <p class="dc-ayuda">El GPS aún no registra tu llegada. Indica cuándo llegaste a base.</p>
                                <div class="dc-par">
                                    <asp:TextBox ID="txtFechaLlegada" runat="server" CssClass="form-control" TextMode="Date" aria-label="Fecha de llegada"></asp:TextBox>
                                    <asp:TextBox ID="txtHoraLlegadaDeclarada" runat="server" CssClass="form-control" TextMode="Time" aria-label="Hora de llegada"></asp:TextBox>
                                </div>
                            </asp:Panel>
                        </div>
                    </section>

                    <!-- 2. Gastos -->
                    <section class="dc-card">
                        <div class="dc-card-tit"><span class="dc-paso">2</span>Tus gastos</div>
                        <p class="dc-ayuda">Escribe el total de cada gasto. Deja en blanco lo que no gastaste. Entrega tus comprobantes en la oficina.</p>

                        <div class="dc-gastos" id="listaGastos">

                            <div class="dc-gasto">
                                <div class="dc-gasto-cab">
                                    <label for="combustibleSoles">Combustible</label>
                                    <button type="button" class="dc-nota-btn" onclick="toggleNota(this)">Nota</button>
                                </div>
                                <div class="dc-montos">
                                    <div class="dc-monto"><span>S/</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-soles" id="combustibleSoles" name="combustibleSoles"></div>
                                    <div class="dc-monto solo-intl"><span>$</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-dolares" id="combustibleDolares" name="combustibleDolares" aria-label="Combustible en dólares"></div>
                                </div>
                                <input type="text" class="form-control dc-nota" name="descCombustible" id="descCombustible" maxlength="250" placeholder="Detalle (opcional)" hidden>
                            </div>

                            <div class="dc-gasto">
                                <div class="dc-gasto-cab">
                                    <label for="alimentacionSoles">Alimentación</label>
                                    <button type="button" class="dc-nota-btn" onclick="toggleNota(this)">Nota</button>
                                </div>
                                <div class="dc-montos">
                                    <div class="dc-monto"><span>S/</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-soles" id="alimentacionSoles" name="alimentacionSoles"></div>
                                    <div class="dc-monto solo-intl"><span>$</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-dolares" id="alimentacionDolares" name="alimentacionDolares" aria-label="Alimentación en dólares"></div>
                                </div>
                                <input type="text" class="form-control dc-nota" name="descAlimentacion" id="descAlimentacion" maxlength="250" placeholder="Detalle (opcional)" hidden>
                            </div>

                            <div class="dc-gasto solo-intl">
                                <div class="dc-gasto-cab">
                                    <label for="peajesNacSoles">Peajes en Perú</label>
                                    <button type="button" class="dc-nota-btn" onclick="toggleNota(this)">Nota</button>
                                </div>
                                <div class="dc-montos">
                                    <div class="dc-monto"><span>S/</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-soles" id="peajesNacSoles" name="peajesNacSoles"></div>
                                </div>
                                <input type="text" class="form-control dc-nota" name="descPeajesNacionales" id="descPeajesNacionales" maxlength="250" placeholder="Detalle (opcional)" hidden>
                            </div>

                            <div class="dc-gasto solo-intl">
                                <div class="dc-gasto-cab">
                                    <label for="peajesExtDolares">Peajes en Ecuador</label>
                                    <button type="button" class="dc-nota-btn" onclick="toggleNota(this)">Nota</button>
                                </div>
                                <div class="dc-montos">
                                    <div class="dc-monto"><span>$</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-dolares" id="peajesExtDolares" name="peajesExtDolares"></div>
                                </div>
                                <input type="text" class="form-control dc-nota" name="descPeajesExtranjeros" id="descPeajesExtranjeros" maxlength="250" placeholder="Detalle (opcional)" hidden>
                            </div>

                            <div class="dc-gasto">
                                <div class="dc-gasto-cab">
                                    <label for="hospedajeSoles">Hospedaje</label>
                                    <button type="button" class="dc-nota-btn" onclick="toggleNota(this)">Nota</button>
                                </div>
                                <div class="dc-montos">
                                    <div class="dc-monto"><span>S/</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-soles" id="hospedajeSoles" name="hospedajeSoles"></div>
                                    <div class="dc-monto solo-intl"><span>$</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-dolares" id="hospedajeDolares" name="hospedajeDolares" aria-label="Hospedaje en dólares"></div>
                                </div>
                                <input type="text" class="form-control dc-nota" name="descHospedaje" id="descHospedaje" maxlength="250" placeholder="Detalle (opcional)" hidden>
                            </div>

                            <div class="dc-gasto">
                                <div class="dc-gasto-cab">
                                    <label for="movilidadSoles">Movilidad</label>
                                    <button type="button" class="dc-nota-btn" onclick="toggleNota(this)">Nota</button>
                                </div>
                                <div class="dc-montos">
                                    <div class="dc-monto"><span>S/</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-soles" id="movilidadSoles" name="movilidadSoles"></div>
                                    <div class="dc-monto solo-intl"><span>$</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-dolares" id="movilidadDolares" name="movilidadDolares" aria-label="Movilidad en dólares"></div>
                                </div>
                                <input type="text" class="form-control dc-nota" name="descMovilidad" id="descMovilidad" maxlength="250" placeholder="Detalle (opcional)" hidden>
                            </div>

                            <div class="dc-gasto">
                                <div class="dc-gasto-cab">
                                    <label for="encapadaSoles">Encarpada / desencarpada</label>
                                    <button type="button" class="dc-nota-btn" onclick="toggleNota(this)">Nota</button>
                                </div>
                                <div class="dc-montos">
                                    <div class="dc-monto"><span>S/</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-soles" id="encapadaSoles" name="encapadaSoles"></div>
                                    <div class="dc-monto solo-intl"><span>$</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-dolares" id="encapadaDolares" name="encapadaDolares" aria-label="Encarpada en dólares"></div>
                                </div>
                                <input type="text" class="form-control dc-nota" name="descEncapada" id="descEncapada" maxlength="250" placeholder="Detalle (opcional)" hidden>
                            </div>

                            <div class="dc-gasto">
                                <div class="dc-gasto-cab">
                                    <label for="reparacionesSoles">Reparaciones y varios</label>
                                    <button type="button" class="dc-nota-btn" onclick="toggleNota(this)">Nota</button>
                                </div>
                                <div class="dc-montos">
                                    <div class="dc-monto"><span>S/</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-soles" id="reparacionesSoles" name="reparacionesSoles"></div>
                                    <div class="dc-monto solo-intl"><span>$</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-dolares" id="reparacionesDolares" name="reparacionesDolares" aria-label="Reparaciones en dólares"></div>
                                </div>
                                <input type="text" class="form-control dc-nota" name="descReparaciones" id="descReparaciones" maxlength="250" placeholder="Qué se reparó (opcional)" hidden>
                            </div>

                            <div class="dc-gasto">
                                <div class="dc-gasto-cab">
                                    <label for="apoyoSeguridadSoles">Apoyo / seguridad</label>
                                    <button type="button" class="dc-nota-btn" onclick="toggleNota(this)">Nota</button>
                                </div>
                                <div class="dc-montos">
                                    <div class="dc-monto"><span>S/</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-soles" id="apoyoSeguridadSoles" name="apoyoSeguridadSoles"></div>
                                    <div class="dc-monto solo-intl"><span>$</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" class="gasto-dolares" id="apoyoSeguridadDolares" name="apoyoSeguridadDolares" aria-label="Apoyo en dólares"></div>
                                </div>
                                <input type="text" class="form-control dc-nota" name="descApoyoSeguridad" id="descApoyoSeguridad" maxlength="250" placeholder="Detalle (opcional)" hidden>
                            </div>
                        </div>

                        <div id="gastosAdicionalesBody"></div>

                        <button type="button" class="dc-btn dc-btn-linea dc-btn-bloque" onclick="agregarGasto()">
                            <i class="fas fa-plus"></i>Agregar otro gasto
                        </button>
                    </section>

                    <!-- 3. Observaciones -->
                    <section class="dc-card">
                        <div class="dc-card-tit"><span class="dc-paso">3</span>Observaciones <span class="dc-opcional">(opcional)</span></div>
                        <asp:TextBox ID="txtObservaciones" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3"
                            MaxLength="1000" placeholder="Algo que la oficina deba saber del viaje"></asp:TextBox>
                    </section>

                    <!-- Barra fija con el total -->
                    <div class="dc-barra" id="barraTotal">
                        <div class="dc-barra-total">
                            <span class="dc-barra-lbl">Total gastos</span>
                            <span class="dc-barra-num"><span id="totalGastosSoles">S/ 0.00</span><span id="totalGastosDolares" class="solo-intl"> · $ 0.00</span></span>
                        </div>
                        <button type="button" class="dc-btn dc-btn-primario" onclick="abrirRevision()">Revisar y enviar</button>
                    </div>

                    <!-- Revisión antes de enviar -->
                    <div class="modal fade" id="modalRevision" tabindex="-1" role="dialog" aria-labelledby="tituloRevision">
                        <div class="modal-dialog modal-dialog-centered modal-dialog-scrollable dc-hoja" role="document">
                            <div class="modal-content">
                                <div class="modal-header">
                                    <h5 class="modal-title" id="tituloRevision">Revisa tu liquidación</h5>
                                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>
                                </div>
                                <div class="modal-body">
                                    <dl class="dc-datos dc-datos-revision" id="revisionViaje"></dl>
                                    <div class="dc-subtit">Gastos</div>
                                    <ul class="dc-resumen" id="revisionGastos"></ul>
                                    <p class="dc-ayuda mb-0">Al enviar pasarás a firmar. El dinero que te entregaron lo registra la administración al revisar.</p>
                                </div>
                                <div class="modal-footer">
                                    <button type="button" class="dc-btn dc-btn-linea" data-dismiss="modal">Corregir</button>
                                    <asp:Button ID="btnEnviarLiquidacion" runat="server" Text="Enviar y firmar"
                                        CssClass="dc-btn dc-btn-primario" OnClick="btnEnviarLiquidacion_Click"
                                        OnClientClick="return confirmarEnvioLiquidacion(this);" />
                                </div>
                            </div>
                        </div>
                    </div>

                </asp:Panel>
            </div>

            <!-- ============ HISTORIAL ============ -->
            <div class="tab-pane fade" id="historial" role="tabpanel">

                <details class="dc-filtros">
                    <summary>Filtrar <asp:Label ID="lblTotalHistorial" runat="server" CssClass="dc-filtros-num" Text="0 liquidaciones"></asp:Label></summary>
                    <div class="dc-filtros-cuerpo">
                        <div class="dc-par">
                            <div>
                                <label class="dc-lbl" for="<%= txtFechaDesde.ClientID %>">Desde</label>
                                <asp:TextBox ID="txtFechaDesde" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                            </div>
                            <div>
                                <label class="dc-lbl" for="<%= txtFechaHasta.ClientID %>">Hasta</label>
                                <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                            </div>
                        </div>
                        <label class="dc-lbl" for="<%= ddlEstadoFiltro.ClientID %>">Estado</label>
                        <asp:DropDownList ID="ddlEstadoFiltro" runat="server" CssClass="form-control">
                            <asp:ListItem Value="">Todos</asp:ListItem>
                            <asp:ListItem Value="PENDIENTE">En revisión</asp:ListItem>
                            <asp:ListItem Value="APROBADO">Aprobada</asp:ListItem>
                            <asp:ListItem Value="REABIERTO">Rechazada</asp:ListItem>
                        </asp:DropDownList>
                        <asp:Button ID="btnBuscarHistorial" runat="server" Text="Buscar"
                            CssClass="dc-btn dc-btn-primario dc-btn-bloque" OnClick="btnBuscarHistorial_Click" />
                    </div>
                </details>

                <asp:Repeater ID="rptHistorial" runat="server">
                    <ItemTemplate>
                        <article class="dc-item">
                            <div class="dc-item-fila">
                                <strong><%#: Eval("NumeroViaje") %></strong>
                                <span class='dc-pill dc-pill-<%# ObtenerClaseEstado(Eval("Estado")) %>'><%#: TextoEstado(Eval("Estado")) %></span>
                            </div>
                            <div class="dc-item-meta">
                                <span><%# Eval("FechaSalida", "{0:dd/MM/yyyy}") %> → <%# Eval("FechaLlegada", "{0:dd/MM/yyyy}") %></span>
                            </div>
                            <div class="dc-item-fila dc-item-montos">
                                <span>Gastos <strong>S/ <%# Eval("GastosSoles", "{0:N2}") %></strong><%# Convert.ToDecimal(Eval("GastosDolares")) > 0 ? " · $ " + Eval("GastosDolares", "{0:N2}") : "" %></span>
                            </div>
                            <div class="dc-item-acciones">
                                <asp:PlaceHolder runat="server" Visible='<%# !(bool)Eval("Firmada") && (string)Eval("Estado") == "PENDIENTE" %>'>
                                    <a class="dc-btn dc-btn-primario dc-btn-chico" href='FirmarLiquidacion.aspx?id=<%# Eval("IdOrdenViaje") %>'>Firmar</a>
                                </asp:PlaceHolder>
                                <button type="button" class="dc-btn dc-btn-linea dc-btn-chico" onclick='verDetalleLiquidacion(<%# Eval("IdOrdenViaje") %>)'>Ver</button>
                                <asp:PlaceHolder runat="server" Visible='<%# (string)Eval("Estado") == "PENDIENTE" %>'>
                                    <button type="button" class="dc-btn dc-btn-alerta dc-btn-chico" onclick='abrirModalRetirar(<%# Eval("IdOrdenViaje") %>)'>Retirar</button>
                                </asp:PlaceHolder>
                            </div>
                        </article>
                    </ItemTemplate>
                </asp:Repeater>

                <asp:Panel ID="pnlHistorialVacio" runat="server" Visible="false">
                    <div class="dc-vacio">
                        <i class="fas fa-history"></i>
                        <p class="dc-vacio-tit">Sin liquidaciones</p>
                        <p>Aquí verás las liquidaciones que envíes.</p>
                    </div>
                </asp:Panel>
            </div>
        </div>
    </div>

    <!-- Retirar liquidación -->
    <div class="modal fade" id="modalRetirarLiquidacion" tabindex="-1" role="dialog" aria-labelledby="modalRetirarLabel" aria-hidden="true">
        <div class="modal-dialog modal-dialog-centered dc-hoja" role="document">
            <div class="modal-content">
                <div class="modal-header dc-modal-alerta">
                    <h5 class="modal-title" id="modalRetirarLabel">Retirar liquidación</h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar"><span aria-hidden="true">&times;</span></button>
                </div>
                <div class="modal-body">
                    <p>Se borrará la liquidación enviada y tu viaje volverá a quedar abierto para que la corrijas y la envíes de nuevo.</p>
                    <p class="dc-ayuda mb-0">Solo puedes retirarla mientras la administración no la haya revisado.</p>
                </div>
                <div class="modal-footer">
                    <button type="button" class="dc-btn dc-btn-linea" data-dismiss="modal">Cancelar</button>
                    <button type="button" id="btnConfirmarRetirar" class="dc-btn dc-btn-alerta" onclick="confirmarRetirar()">Sí, retirar</button>
                </div>
            </div>
        </div>
    </div>

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/DashboardConductor.css") %>" rel="stylesheet" />
    <script>
        var SGV = Object.assign(window.SGV || {}, {
            pnlFormularioLiquidacion: '<%= pnlFormularioLiquidacion.ClientID %>',
            txtFechaSalida: '<%= txtFechaSalida.ClientID %>',
            txtHoraSalida: '<%= txtHoraSalida.ClientID %>',
            txtFechaLlegada: '<%= txtFechaLlegada.ClientID %>',
            txtHoraLlegada: '<%= txtHoraLlegadaDeclarada.ClientID %>',
            lblSalidaGps: '<%= lblSalidaGps.ClientID %>',
            lblLlegadaGps: '<%= lblLlegadaGps.ClientID %>',
            lblNumeroViaje: '<%= lblNumeroViajeResumen.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/DashboardConductor.js") %>"></script>

</asp:Content>

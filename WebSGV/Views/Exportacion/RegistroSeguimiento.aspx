<%@ Page Title="Registro de Seguimiento de Exportación" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroSeguimiento.aspx.cs" Inherits="WebSGV.Views.Exportacion.RegistroSeguimiento" %>

<asp:Content ID="ContentSE" ContentPlaceHolderID="MainContent" runat="server">
    <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap" rel="stylesheet" />
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/Exportacion/RegistroSeguimiento.css") %>" rel="stylesheet" />

    <%-- Datos de autocompletado emitidos desde el code-behind --%>
    <asp:Literal ID="litAutoComplete" runat="server" />

    <%-- Datalist elements para autocompletado HTML5 nativo --%>
    <datalist id="seListClientes"></datalist>
    <datalist id="seListConductores"></datalist>
    <datalist id="seListTractos"></datalist>
    <datalist id="seListCarretas"></datalist>

    <div class="se-wrap">
        <div class="se-header">
            <div>
                <h1 class="se-title">Seguimiento de Exportación</h1>
                <div class="se-subtitle">Registro de viajes Perú → Ecuador | Reemplazo digital del Excel STATUS GENERAL VIVIANA</div>
            </div>
            <a href="DashboardExportacion.aspx" class="se-btn-dashboard">
                <i class="fas fa-chart-line"></i> Ver Dashboard Analytics
            </a>
        </div>

        <!-- Tabs -->
        <div class="se-tabs" role="tablist">
            <button type="button" class="se-tab" data-target="panel-bandeja">🚚 Viajes en curso</button>
            <button type="button" class="se-tab active" data-target="panel-import">📂 Importar Excel</button>
            <button type="button" class="se-tab"        data-target="panel-list">📋 Historial</button>
            <%-- Botón oculto: no es navegable a mano, solo existe para que ActivarTab("panel-form") (code-behind)
                 pueda "hacer clic" en él por JS y mostrar el formulario tras Nuevo Viaje / Editar / Verificar GPS. --%>
            <button type="button" class="se-tab" data-target="panel-form" style="display:none;" aria-hidden="true"></button>
        </div>

        <asp:HiddenField ID="hdnIdSeguimiento" runat="server" Value="0" />

        <asp:Panel ID="pnlAlert" runat="server" Visible="false" CssClass="se-alert se-alert-info">
            <asp:Literal ID="litAlert" runat="server"></asp:Literal>
        </asp:Panel>

        <!-- ========== TAB: BANDEJA DE VIAJES EN CURSO ========== -->
        <div id="panel-bandeja" class="se-tab-panel">
            <div class="se-card">
                <div class="se-section-title">Viajes en curso (registro progresivo)</div>
                <div class="se-alert se-alert-info" style="margin-bottom:14px;">
                    💡 <strong>Captura progresiva:</strong> No necesitas llenar todo un viaje de golpe. Crea uno con los datos mínimos y ve agregando los hitos conforme avanza el viaje.
                </div>

                <div class="se-bandeja-toolbar">
                    <asp:TextBox ID="txtFiltroBandeja" runat="server" placeholder="Buscar por cliente, conductor o tracto..." AutoCompleteType="Disabled" />
                    <asp:Button ID="btnBuscarBandeja" runat="server" Text="🔍 Buscar" CssClass="se-btn se-btn-ghost" OnClick="btnBuscarBandeja_Click" CausesValidation="false" />
                    <asp:Button ID="btnRefrescarBandeja" runat="server" Text="↻ Refrescar" CssClass="se-btn se-btn-ghost" OnClick="btnRefrescarBandeja_Click" CausesValidation="false" />
                    <asp:Button ID="btnNuevoViaje" runat="server" Text="➕ Nuevo viaje" CssClass="se-btn se-btn-primary" OnClick="btnNuevoViaje_Click" CausesValidation="false" />
                    <div class="se-bandeja-counter">
                        <strong><asp:Literal ID="litCountBandeja" runat="server" Text="0" /></strong> abierto(s)
                    </div>
                </div>

                <asp:Panel ID="pnlBandejaVacia" runat="server" Visible="false" CssClass="se-empty">
                    <div class="se-empty-icon">📭</div>
                    <h4 style="color: var(--se-ink); font-weight: 700;">No hay viajes en curso</h4>
                    <p>Crea un nuevo viaje o importa el Excel masivo para empezar.</p>
                </asp:Panel>

                <asp:Repeater ID="rptBandeja" runat="server" OnItemCommand="rptBandeja_ItemCommand">
                    <HeaderTemplate><div class="se-bandeja-grid"></HeaderTemplate>
                    <ItemTemplate>
                        <div class='se-trip-card <%# (int)Eval("porcentaje") >= 90 ? "complete" : ((int)Eval("porcentaje") <= 10 ? "pending" : "") %>'>
                            <div class="se-trip-head">
                                <div>
                                    <div class="se-trip-client"><%# Eval("cliente") %></div>
                                    <div class="se-trip-meta">Programación: <%# Eval("fhProgramacionFmt") %></div>
                                </div>
                                <span class='se-badge se-badge-<%# (Eval("estado") ?? "").ToString().ToLower().Replace("_","") %>'><%# Eval("estado") %></span>
                            </div>
                            <div class="se-trip-info">
                                <span>Tracto 1</span><span><%# Eval("tracto1") %></span>
                                <span>Conductor</span><span><%# Eval("conductorOrigen") %></span>
                                <span>Carreta</span><span><%# Eval("carreta") %></span>
                                <span>Destino</span><span><%# Eval("bodegaDescarga") %></span>
                            </div>
                            <div>
                                <div class="se-progress-label">
                                    <span>Avance del viaje</span>
                                    <span><%# Eval("hitosCompletados") %>/<%# Eval("hitosTotales") %> hitos · <%# Eval("porcentaje") %>%</span>
                                </div>
                                <div class="se-progress"><div class="se-progress-bar" style='width: <%# Eval("porcentaje") %>%;'></div></div>
                            </div>
                            <div class="se-trip-next">
                                <small>Siguiente hito por registrar</small>
                                <%# Eval("siguienteHito") %>
                            </div>
                            <div class="se-trip-actions">
                                <asp:LinkButton runat="server" CssClass="se-btn se-btn-primary"
                                    CommandName="Continuar"
                                    CommandArgument='<%# Eval("idSeguimiento") %>'
                                    CausesValidation="false">▶ Continuar registro</asp:LinkButton>
                                <asp:LinkButton runat="server" CssClass="se-btn se-btn-ghost"
                                    CommandName="Finalizar"
                                    CommandArgument='<%# Eval("idSeguimiento") %>'
                                    CausesValidation="false"
                                    OnClientClick="return confirm('¿Marcar este viaje como FINALIZADO?');">✓ Finalizar</asp:LinkButton>
                            </div>
                        </div>
                    </ItemTemplate>
                    <FooterTemplate></div></FooterTemplate>
                </asp:Repeater>
            </div>
        </div>

        <!-- ========== TAB: FORMULARIO (registro/edición) ========== -->
        <div id="panel-form" class="se-tab-panel">
            <asp:Panel ID="pnlFormBanner" runat="server" CssClass="se-form-banner" Visible="false">
                <i class="fas fa-edit"></i>
                <div>
                    <strong><asp:Literal ID="litFormBannerTitle" runat="server" /></strong>
                    <small><asp:Literal ID="litFormBannerSub" runat="server" /></small>
                </div>
                <asp:Literal ID="litEstadoRegistro" runat="server" />
                <asp:Button ID="btnCancelarEdicion" runat="server" Text="✕ Cancelar edición" CssClass="se-btn se-btn-ghost se-btn-cancel" CausesValidation="false" OnClick="btnCancelarEdicion_Click" />
            </asp:Panel>

            <%-- Sección ①: Vincular despachos reales (el viaje se CREA en Despacho, acá solo se selecciona) --%>
            <details class="se-accordion" open>
                <summary>① Despachos del viaje (obligatorio: al menos el nacional)</summary>
                <div class="se-acc-body">
                    <div class="se-alert se-alert-info" style="margin-bottom:14px;">
                        💡 Los viajes se crean en <strong>Despacho</strong>. Acá solo buscas y seleccionas los despachos reales que corresponden a este viaje de exportación — cliente, conductor y placa se traen automáticamente, no se re-escriben.
                    </div>
                    <div class="se-grid" style="grid-template-columns: 1fr 1fr;">
                        <div>
                            <div class="se-field" style="position:relative;"><label>Buscar despacho Nacional (Trujillo) — Tracto 1 *</label>
                                <asp:TextBox ID="txtBuscarDespachoNacional" runat="server" placeholder="Escribe cliente, conductor o placa..." autocomplete="off" onkeyup="seAutocompletarDespacho(this, false)" />
                                <div id="acDespachoNacional" class="se-ac-list"></div>
                            </div>
                            <asp:Panel ID="pnlResumenNacional" runat="server" Visible="false" CssClass="se-alert se-alert-success" style="margin-top:10px;">
                                <asp:Literal ID="litResumenNacional" runat="server" Mode="PassThrough" />
                            </asp:Panel>
                        </div>
                        <div>
                            <div class="se-field" style="position:relative;"><label>Buscar despacho Internacional (Ecuador) — Tracto 2</label>
                                <asp:TextBox ID="txtBuscarDespachoInternacional" runat="server" placeholder="Escribe cliente, conductor o placa..." autocomplete="off" onkeyup="seAutocompletarDespacho(this, true)" />
                                <div id="acDespachoInternacional" class="se-ac-list"></div>
                            </div>
                            <asp:Panel ID="pnlResumenInternacional" runat="server" Visible="false" CssClass="se-alert se-alert-success" style="margin-top:10px;">
                                <asp:Literal ID="litResumenInternacional" runat="server" Mode="PassThrough" />
                            </asp:Panel>
                        </div>
                        <%-- Ganchos de postback: el clic en un ítem del autocompletado (JS) guarda el id en el
                             hidden field correspondiente y dispara estos LinkButton ocultos vía __doPostBack. --%>
                        <asp:LinkButton ID="lnkAplicarDespachoNacional" runat="server" OnClick="lnkAplicarDespachoNacional_Click" style="display:none;" CausesValidation="false" />
                        <asp:LinkButton ID="lnkAplicarDespachoInternacional" runat="server" OnClick="lnkAplicarDespachoInternacional_Click" style="display:none;" CausesValidation="false" />
                    </div>

                    <%-- Portadores de datos para el guardado/carga existente — no se editan a mano, los llena la selección de arriba. --%>
                    <div style="display:none;">
                        <asp:TextBox ID="txtCliente" runat="server" />
                        <asp:RequiredFieldValidator ID="rfvCliente" runat="server" ControlToValidate="txtCliente" ValidationGroup="vgGuardarSeguimiento" Display="Dynamic" CssClass="text-danger" ErrorMessage="Selecciona el despacho Nacional (Tracto 1)." />
                        <asp:TextBox ID="txtConductorOrigen" runat="server" />
                        <asp:TextBox ID="txtTracto1" runat="server" />
                        <asp:RequiredFieldValidator ID="rfvTracto1" runat="server" ControlToValidate="txtTracto1" ValidationGroup="vgGuardarSeguimiento" Display="Dynamic" CssClass="text-danger" ErrorMessage="Selecciona el despacho Nacional (Tracto 1)." />
                        <asp:TextBox ID="txtCarreta" runat="server" />
                        <asp:TextBox ID="txtConductorDestino" runat="server" />
                        <asp:TextBox ID="txtTracto2" runat="server" />
                    </div>
                    <asp:HiddenField ID="hdnIdDespachoOrigen" runat="server" Value="0" />
                    <asp:HiddenField ID="hdnIdDespachoDestino" runat="server" Value="0" />

                    <div class="se-grid" style="margin-top:16px;">
                        <div class="se-field"><label>F.H. Programación *</label>
                            <asp:TextBox ID="txtFhProgramacion" runat="server" TextMode="DateTimeLocal" />
                            <asp:RequiredFieldValidator ID="rfvFhProgramacion" runat="server" ControlToValidate="txtFhProgramacion" ValidationGroup="vgGuardarSeguimiento" Display="Dynamic" CssClass="text-danger" ErrorMessage="F.H. Programación es obligatoria." />
                        </div>
                        <div class="se-field"><label>Estado</label>
                            <asp:DropDownList ID="ddlEstado" runat="server">
                                <asp:ListItem Text="En curso"   Value="EN_CURSO" />
                                <asp:ListItem Text="Pendiente"  Value="PENDIENTE" />
                                <asp:ListItem Text="Finalizado" Value="FINALIZADO" />
                                <asp:ListItem Text="Completado" Value="COMPLETADO" />
                                <asp:ListItem Text="Retrasado"  Value="RETRASADO" />
                                <asp:ListItem Text="Cancelado"  Value="CANCELADO" />
                            </asp:DropDownList>
                        </div>
                    </div>
                </div>
            </details>

            <%-- Sección ②-⑤: Línea de tiempo del recorrido (Base → Trujillo → Base → Ecuador → Base) --%>
            <div class="se-card" style="padding:20px 20px 6px 20px;">
                <div class="se-section-title" style="margin-bottom:4px;">② Línea de tiempo del recorrido</div>
                <p style="color:var(--se-muted);font-size:12.5px;margin:-6px 0 16px 0;">
                    <span class="se-timeline-tag gps">GPS</span> se llena solo al presionar "Verificar GPS" abajo (puedes corregirlo si hace falta) ·
                    <span class="se-timeline-tag manual">MANUAL</span> siempre se escribe a mano.
                </p>

                <%-- Tramo Nacional (Tracto 1): Base → Trujillo → Base --%>
                <div class="se-timeline-tramo">
                    <div class="se-timeline-head">
                        <div class="se-timeline-head-left">
                            <div class="se-timeline-head-icon">1</div>
                            <div><h4>Tramo Nacional — Base ⇄ Trujillo</h4><small>Tracto 1 · <asp:Literal ID="litTracto1Resumen" runat="server" /></small></div>
                        </div>
                        <asp:Literal ID="litProgresoNacional" runat="server" Mode="PassThrough" />
                    </div>
                    <div class="se-timeline-body">
                        <div class="se-timeline-steps">
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Salida Base</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhSalidaBase1" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada Trujillo</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaTrujillo" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Ingreso Planta</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhIngresoPlanta" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Inicio Carga</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:TextBox ID="txtFhInicioCarga" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Término Carga</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:TextBox ID="txtFhTerminoCarga" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Salida Planta</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhSalidaPlanta" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada Base</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaBase2" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Registro (sistema)</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:TextBox ID="txtFhRegistro" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                        </div>
                    </div>
                </div>

                <%-- Tramo Internacional (Tracto 2): Base → Bodega Nacional --%>
                <div class="se-timeline-tramo">
                    <div class="se-timeline-head">
                        <div class="se-timeline-head-left">
                            <div class="se-timeline-head-icon">2</div>
                            <div><h4>Salida a Ecuador — Bodega Nacional</h4><small>Tracto 2 · <asp:Literal ID="litTracto2Resumen" runat="server" /></small></div>
                        </div>
                        <asp:Literal ID="litProgresoBodegaNacional" runat="server" Mode="PassThrough" />
                    </div>
                    <div class="se-timeline-body">
                        <div class="se-timeline-steps">
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Salida Base (hacia Ecuador)</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhSalidaBase2" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada Bodega Nacional</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaBodegaNacional" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Ingreso Bodega Nacional</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhIngresoBodegaNacional" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Salida Bodega Nacional</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhSalidaBodegaNacional" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>Bodega Nacional</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:DropDownList ID="ddlBodegaNacional" runat="server">
                                        <asp:ListItem Text="-- Seleccionar --" Value="" />
                                        <asp:ListItem Text="DEPSA"    Value="DEPSA" />
                                        <asp:ListItem Text="COMPLEX"  Value="COMPLEX" />
                                    </asp:DropDownList>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <%-- Frontera --%>
                <div class="se-timeline-tramo">
                    <div class="se-timeline-head">
                        <div class="se-timeline-head-left">
                            <div class="se-timeline-head-icon">3</div>
                            <div><h4>Frontera — CEBAF / Nacionalización</h4><small>Tracto 2</small></div>
                        </div>
                        <asp:Literal ID="litProgresoFrontera" runat="server" Mode="PassThrough" />
                    </div>
                    <div class="se-timeline-body">
                        <div class="se-timeline-steps">
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada CEBAF</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaCEBAF" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Cruce Ecuador</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhCruceEcuador" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Autorización Nacionalización</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:TextBox ID="txtFhAutorizacionNacionalizacion" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                        </div>
                    </div>
                </div>

                <%-- Ecuador --%>
                <div class="se-timeline-tramo">
                    <div class="se-timeline-head">
                        <div class="se-timeline-head-left">
                            <div class="se-timeline-head-icon">4</div>
                            <div><h4>Ecuador — TCI → Jave / Inbalnor</h4><small>Tracto 2 · la ruta (Jave/Inbalnor) se detecta sola por GPS</small></div>
                        </div>
                        <asp:Literal ID="litProgresoEcuador" runat="server" Mode="PassThrough" />
                    </div>
                    <div class="se-timeline-body">
                        <div class="se-timeline-steps">
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada TCI</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaTCI" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Salida TCI</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhSalidaTCI" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>Bodega Ecuatoriana</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:DropDownList ID="ddlBodegaEcuatoriana" runat="server">
                                        <asp:ListItem Text="-- Seleccionar --" Value="" />
                                        <asp:ListItem Text="TCI"     Value="TCI" />
                                        <asp:ListItem Text="PUYANGO" Value="PUYANGO" />
                                    </asp:DropDownList>
                                </div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>Bodega Descarga</label><span class="se-timeline-tag manual">Manual (o auto por ruta GPS)</span></div>
                                    <asp:DropDownList ID="ddlBodegaDescarga" runat="server">
                                        <asp:ListItem Text="-- Seleccionar --" Value="" />
                                        <asp:ListItem Text="INBALNOR" Value="INBALNOR" />
                                        <asp:ListItem Text="JAVE"     Value="JAVE" />
                                        <asp:ListItem Text="OREMANS"  Value="OREMANS" />
                                    </asp:DropDownList>
                                </div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada Planta Ecuador</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaPlantaEcuador" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada Almacén</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaAlmacen" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Ingreso</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhIngreso" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Inicio Descarga</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:TextBox ID="txtFhInicioDescarga" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Término Descarga</label><span class="se-timeline-tag manual">Manual</span></div>
                                    <asp:TextBox ID="txtFhTerminoDescarga" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Salida</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhSalida" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                        </div>
                    </div>
                </div>

                <%-- Regreso final --%>
                <div class="se-timeline-tramo">
                    <div class="se-timeline-head">
                        <div class="se-timeline-head-left">
                            <div class="se-timeline-head-icon">5</div>
                            <div><h4>Regreso Final a Base</h4><small>Tracto 2 · cierra el recorrido</small></div>
                        </div>
                        <asp:Literal ID="litProgresoRegreso" runat="server" Mode="PassThrough" />
                    </div>
                    <div class="se-timeline-body">
                        <div class="se-timeline-steps">
                            <div class="se-timeline-step gps">
                                <div class="se-timeline-dot"></div>
                                <div><div class="se-timeline-step-label"><label>F.H. Llegada Base</label><span class="se-timeline-tag gps">GPS</span></div>
                                    <asp:TextBox ID="txtFhLlegadaBaseFinal" runat="server" TextMode="DateTimeLocal" /></div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <%-- Sección ⑥: Incidencias --%>
            <details class="se-accordion">
                <summary>⑥ Incidencias y Observaciones</summary>
                <div class="se-acc-body">
                    <div class="se-grid">
                        <div class="se-field"><label>Sacos Robados</label>
                            <asp:TextBox ID="txtSacosRobados" runat="server" TextMode="Number" Text="0" min="0" step="1" />
                            <asp:RegularExpressionValidator ID="revSacosRobados" runat="server" ControlToValidate="txtSacosRobados" ValidationGroup="vgGuardarSeguimiento" Display="Dynamic" CssClass="text-danger" ValidationExpression="^\d+$" ErrorMessage="Sacos Robados debe ser un número entero mayor o igual a 0." />
                        </div>
                        <div class="se-field"><label>Sacos Rotos</label>
                            <asp:TextBox ID="txtSacosRotos" runat="server" TextMode="Number" Text="0" min="0" step="1" />
                            <asp:RegularExpressionValidator ID="revSacosRotos" runat="server" ControlToValidate="txtSacosRotos" ValidationGroup="vgGuardarSeguimiento" Display="Dynamic" CssClass="text-danger" ValidationExpression="^\d+$" ErrorMessage="Sacos Rotos debe ser un número entero mayor o igual a 0." />
                        </div>
                        <div class="se-field"><label>Sacos Mojados</label>
                            <asp:TextBox ID="txtSacosMojados" runat="server" TextMode="Number" Text="0" min="0" step="1" />
                            <asp:RegularExpressionValidator ID="revSacosMojados" runat="server" ControlToValidate="txtSacosMojados" ValidationGroup="vgGuardarSeguimiento" Display="Dynamic" CssClass="text-danger" ValidationExpression="^\d+$" ErrorMessage="Sacos Mojados debe ser un número entero mayor o igual a 0." />
                        </div>
                    </div>
                    <div class="se-field" style="margin-top:14px;"><label>Motivo de retraso / Comentario</label>
                        <asp:TextBox ID="txtMotivoRetraso" runat="server" TextMode="MultiLine" Rows="3" />
                    </div>
                </div>
            </details>

            <div class="se-card">
                <div class="se-actions">
                    <asp:Button ID="btnLimpiar" runat="server" Text="Limpiar" CssClass="se-btn se-btn-ghost" CausesValidation="false" OnClick="btnLimpiar_Click" />
                    <asp:Button ID="btnVerificarGps" runat="server" Text="🛰️ Verificar GPS" CssClass="se-btn se-btn-ghost" CausesValidation="false" OnClick="btnVerificarGps_Click" ToolTip="Consulta el historial GPS del Tracto 1 y Tracto 2 y llena automáticamente los campos de fecha/hora detectables. Puede tardar 1-2 minutos." />
                    <asp:Button ID="btnGuardarBorrador" runat="server" Text="📝 Guardar borrador" CssClass="se-btn se-btn-ghost" OnClick="btnGuardarBorrador_Click" CausesValidation="false" />
                    <asp:Button ID="btnGuardarFinal" runat="server" Text="✅ Guardar final" CssClass="se-btn se-btn-primary" OnClick="btnGuardarFinal_Click" ValidationGroup="vgGuardarSeguimiento" />
                </div>
                <asp:Panel ID="pnlResultadoGps" runat="server" Visible="false" CssClass="se-alert se-alert-info" style="margin-top:10px;">
                    <asp:Literal ID="litResultadoGps" runat="server" Mode="PassThrough" />
                </asp:Panel>
                <asp:ValidationSummary ID="vsGuardarSeguimiento" runat="server" ValidationGroup="vgGuardarSeguimiento" DisplayMode="BulletList" CssClass="se-alert se-alert-warning" HeaderText="Corrige los siguientes campos:" />
                <div style="text-align:right;font-size:12px;color:var(--se-muted);margin-top:8px;">
                    Borrador permite avance parcial con validaciones de formato. Guardado final exige todos los campos obligatorios del seguimiento completos.
                </div>
            </div>
        </div>

        <!-- ========== TAB: IMPORT EXCEL ========== -->
        <div id="panel-import" class="se-tab-panel active">
            <div class="se-card">
                <div class="se-section-title">Importación masiva desde Excel</div>
                <div class="se-alert se-alert-info" style="border-color: var(--se-primary);">
                    <strong>Paso 1:</strong> descarga la plantilla y complétala con los datos del viaje.
                    <strong>Paso 2:</strong> sube el archivo completado. El sistema mapea automáticamente las columnas reconocidas.
                </div>

                <div class="se-actions" style="justify-content:flex-start; margin-top:0; margin-bottom:20px;">
                    <asp:Button ID="btnDescargarPlantilla" runat="server" Text="📄 Descargar plantilla Excel" CssClass="se-btn se-btn-ghost" CausesValidation="false" OnClick="btnDescargarPlantilla_Click" />
                </div>

                <div id="dropZone" class="se-import-zone" onclick="document.getElementById('<%= fileExcel.ClientID %>').click();">
                    <div class="se-import-icon"><i class="fas fa-file-excel"></i></div>
                    <h4>Arrastra tu archivo aquí o haz clic para seleccionar</h4>
                    <p>Acepta .xlsx y .xls — máximo 10MB</p>
                    <asp:FileUpload ID="fileExcel" runat="server" CssClass="d-none" accept=".xlsx,.xls" onchange="seUpdateFileName(this);" />
                    <div id="fileInfo" style="margin-top:14px;font-style:italic;color:var(--se-muted);">Ningún archivo seleccionado</div>
                </div>

                <div class="se-actions">
                    <asp:Button ID="btnImportar" runat="server" Text="📂 Procesar Archivo" CssClass="se-btn se-btn-primary" OnClick="btnImportar_Click" />
                </div>
            </div>
        </div>

        <!-- ========== TAB: HISTORIAL ========== -->
        <div id="panel-list" class="se-tab-panel">
            <div class="se-card">
                <div class="se-section-title">Últimos registros</div>
                <div class="se-table">
                    <asp:GridView ID="gvRecientes" runat="server" AutoGenerateColumns="false" AllowPaging="true" PageSize="10"
                        GridLines="None" CssClass=""
                        EmptyDataText="No hay registros aún. Comienza usando el formulario o importando un Excel."
                        OnPageIndexChanging="gvRecientes_PageIndexChanging">
                        <Columns>
                            <asp:BoundField DataField="idSeguimiento"   HeaderText="ID" ItemStyle-Width="60px" />
                            <asp:BoundField DataField="cliente"          HeaderText="Cliente" />
                            <asp:BoundField DataField="conductorOrigen"  HeaderText="Conductor Origen" />
                            <asp:BoundField DataField="tracto1"          HeaderText="Tracto 1" />
                            <asp:BoundField DataField="bodegaDescarga"   HeaderText="Bodega Destino" />
                            <asp:TemplateField HeaderText="Estado">
                                <ItemTemplate>
                                    <span class='se-badge se-badge-<%# (Eval("estado") ?? "").ToString().ToLower().Replace("_","") %>'>
                                        <%# Eval("estado") %>
                                    </span>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="fechaRegistroFmt" HeaderText="Registrado" />
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hdnIdSeguimiento: '<%= hdnIdSeguimiento.ClientID %>',
            txtBuscarDespachoInternacional: '<%= txtBuscarDespachoInternacional.ClientID %>',
            hdnIdDespachoDestino: '<%= hdnIdDespachoDestino.ClientID %>',
            lnkAplicarDespachoInternacionalUniqueID: '<%= lnkAplicarDespachoInternacional.UniqueID %>',
            txtBuscarDespachoNacional: '<%= txtBuscarDespachoNacional.ClientID %>',
            hdnIdDespachoOrigen: '<%= hdnIdDespachoOrigen.ClientID %>',
            lnkAplicarDespachoNacionalUniqueID: '<%= lnkAplicarDespachoNacional.UniqueID %>',
            fileExcel: '<%= fileExcel.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/Exportacion/RegistroSeguimiento.js") %>"></script>
</asp:Content>

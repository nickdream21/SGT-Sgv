<%@ Page Title="Seguimiento de exportación" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroSeguimiento.aspx.cs" Inherits="WebSGV.Views.Exportacion.RegistroSeguimiento" %>

<asp:Content ID="ContentSE" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/Exportacion/SeguimientoExportacion.css") %>" rel="stylesheet" />

    <div class="sx-wrap">
        <div class="sgv-encabezado">
            <div>
                <h2>Seguimiento de exportación</h2>
                <p>Un viaje por cada carreta de los despachos de exportación y de los clientes terceros. Las horas salen del GPS; tú completas lo que el GPS no sabe.</p>
            </div>
            <div class="sx-barra-acciones">
                <span class="sx-nota"><asp:Literal ID="litUltimaConsulta" runat="server" /></span>
                <a href="DashboardExportacion.aspx" class="sx-btn"><i class="fas fa-chart-line" aria-hidden="true"></i>Dashboard</a>
                <a href="ImportarSeguimiento.aspx" class="sx-btn"><i class="fas fa-file-import" aria-hidden="true"></i>Importar Excel</a>
                <asp:Button ID="btnExportarStatus" runat="server" Text="Excel de STATUS" CssClass="sx-btn" CausesValidation="false" OnClick="btnExportarStatus_Click"
                    ToolTip="Baja un Excel liviano con el STATUS de las unidades que muestra la tabla (con el pedido en Buscar, solo ese pedido)" />
                <asp:Button ID="btnExportar" runat="server" Text="Exportar STATUS GENERAL" CssClass="sx-btn" CausesValidation="false" OnClick="btnExportar_Click"
                    ToolTip="STATUS GENERAL de Vitapro con todas las horas (sin los clientes terceros)" />
                <asp:Button ID="btnExportarStatusExtra" runat="server" Text="Excel STATUS EXTRA" CssClass="sx-btn" CausesValidation="false" OnClick="btnExportarStatusExtra_Click"
                    ToolTip="Cuadro de los clientes terceros (Crisar, Novopan, Polinplast) que muestra la tabla, con las columnas del STATUS EXTRA" />
                <asp:Button ID="btnExportarGrifo" runat="server" Text="Cuadro grifo Santa Rosa" CssClass="sx-btn" CausesValidation="false" OnClick="btnExportarGrifo_Click"
                    ToolTip="Excel de los carros que bajan al grifo Santa Rosa a cargar combustible (Vitapro y Crisar de vuelta de Ecuador): en camino con su hora aproximada de llegada, en el grifo y descargando" />
                <button type="button" class="sx-btn" id="btnReporteGrifo" title="Copia, listo para enviar al grifo, cuántos y cuáles carros bajan al grifo Santa Rosa y a qué hora llegan aprox."><i class="fas fa-gas-pump" aria-hidden="true"></i>Reporte grifo Santa Rosa</button>
                <textarea id="txtReporteGrifo" hidden readonly><asp:Literal ID="litReporteGrifo" runat="server" /></textarea>
                <a href="SeguimientoViaje.aspx?nuevo=1" class="sx-btn">Nuevo viaje</a>
                <button type="button" class="sx-btn" id="btnCopiarStatus" title="Copia el STATUS de cada fila de la tabla, en orden, para pegar en la columna STATUS del Excel"><i class="fas fa-copy" aria-hidden="true"></i>Copiar STATUS</button>
                <button type="button" class="sx-btn" id="btnReporteLlegadas" title="Copia, listo para enviar, el estado y la hora aproximada de llegada de las unidades que van en camino a cargar a Trujillo o a descargar a Jave/Inbalnor"><i class="fas fa-clock" aria-hidden="true"></i>Reporte de llegadas</button>
                <textarea id="txtReporteLlegadas" hidden readonly><asp:Literal ID="litReporteLlegadas" runat="server" /></textarea>
                <asp:Button ID="btnActualizarGps" runat="server" Text="Actualizar GPS de viajes abiertos" CssClass="sx-btn sx-btn-primario" CausesValidation="false"
                    OnClick="btnActualizarGps_Click" OnClientClick="this.value='Consultando el GPS…';" UseSubmitBehavior="false" />
            </div>
        </div>

        <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
            <asp:Literal ID="litMensaje" runat="server" />
        </asp:Panel>

        <asp:HiddenField ID="hdnFase" runat="server" Value="todos" />

        <div class="sx-filtros">
            <label class="sx-campo">Buscar
                <asp:TextBox ID="txtBuscar" runat="server" placeholder="Pedido, carreta, placa o chofer" style="width: 16rem; max-width: 70vw;" />
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
                </asp:DropDownList>
            </label>
            <label class="sx-campo">Planta de descarga
                <asp:DropDownList ID="ddlPlanta" runat="server">
                    <asp:ListItem Value="" Text="Todas" />
                    <asp:ListItem Value="JAVE" Text="Jave" />
                    <asp:ListItem Value="INBALNOR" Text="Inbalnor" />
                </asp:DropDownList>
            </label>
            <label class="sx-campo" style="flex-direction: row; align-items: center; gap: 0.375rem; height: 2.25rem; font-size: 0.8125rem;">
                <asp:CheckBox ID="chkHistorial" runat="server" />
                Incluir los importados del Excel
            </label>
            <asp:Button ID="btnFiltrar" runat="server" Text="Buscar" CssClass="sx-btn" CausesValidation="false" OnClick="btnFiltrar_Click" />
        </div>

        <div class="sgv-tabs" role="tablist" aria-label="Fase del viaje">
            <asp:Repeater ID="rptFases" runat="server" OnItemCommand="rptFases_ItemCommand">
                <ItemTemplate>
                    <asp:LinkButton runat="server" CommandName="Fase" CommandArgument='<%# Eval("Grupo") %>' CausesValidation="false"
                        CssClass='<%# (bool)Eval("Activo") ? "sgv-tab activo" : "sgv-tab" %>' role="tab" aria-selected='<%# (bool)Eval("Activo") ? "true" : "false" %>'>
                        <%#: Eval("Nombre") %> <span class="sgv-tab-contador"><%# Eval("Cantidad") %></span>
                    </asp:LinkButton>
                </ItemTemplate>
            </asp:Repeater>
        </div>

        <div class="sgv-panel sx-tabla-caja">
            <table class="table sgv-tabla">
                <thead>
                    <tr><th>Pedido / carreta</th><th>Carga</th><th>Descarga</th><th>Fase</th><th>STATUS</th><th>Último hito</th><th>Avance</th><th>Pendiente</th></tr>
                </thead>
                <tbody>
                    <asp:Repeater ID="rptViajes" runat="server">
                        <ItemTemplate>
                            <tr>
                                <td>
                                    <a class="sx-mono" style="font-weight: 600;" href='SeguimientoViaje.aspx?id=<%# Eval("Id") %>'><%#: Eval("Pedido") %></a>
                                    <span class="sx-sub">Carreta <span class="sx-mono"><%#: Eval("Carreta") %></span></span>
                                    <span class="sx-sub">Prog. <%#: Eval("Programacion") %></span>
                                </td>
                                <td><span class="sx-mono"><%#: Eval("Tracto1") %></span><span class="sx-sub"><%#: Eval("Chofer1") %></span></td>
                                <td><span class="sx-mono"><%#: Eval("Tracto2") %></span><span class="sx-sub"><%#: Eval("Chofer2") %></span></td>
                                <td><span class='<%# "sx-fase sx-fase-" + Eval("Grupo") %>'><%#: Eval("Fase") %></span><span class="sx-sub"><%#: Eval("Planta") %></span></td>
                                <td class="sx-status">
                                    <span class="sx-status-texto"><%#: Eval("Status") %></span>
                                    <span class="sx-sub sx-mono sx-ubicacion"><%#: Eval("Ubicacion") %></span>
                                    <span class="sx-sub sx-llegada"><%#: Eval("Llegada") %></span>
                                    <button type="button" class="sx-copiar" data-copiar title="Copiar este STATUS" aria-label="Copiar este STATUS"><i class="fas fa-copy" aria-hidden="true"></i></button>
                                </td>
                                <td><%#: Eval("UltimoHito") %><span class="sx-sub sx-mono"><%#: Eval("UltimoHora") %></span></td>
                                <td>
                                    <div class="sx-avance">
                                        <div class="sx-avance-fila"><span>Carga</span><span class="sx-mono"><%# Eval("CargaTxt") %></span></div>
                                        <div class="sx-avance-barra sx-avance-carga"><span style='<%# "width:" + Eval("CargaPct") + "%" %>'></span></div>
                                        <div class="sx-avance-fila"><span>Descarga</span><span class="sx-mono"><%# Eval("DescargaTxt") %></span></div>
                                        <div class="sx-avance-barra sx-avance-descarga"><span style='<%# "width:" + Eval("DescargaPct") + "%" %>'></span></div>
                                    </div>
                                </td>
                                <td>
                                    <div class="sx-avisos">
                                        <asp:Repeater runat="server" DataSource='<%# Eval("Avisos") %>'>
                                            <ItemTemplate><span class='<%# "sx-aviso sx-aviso-" + Eval("Css") %>'><%#: Eval("Texto") %></span></ItemTemplate>
                                        </asp:Repeater>
                                    </div>
                                </td>
                            </tr>
                        </ItemTemplate>
                    </asp:Repeater>
                </tbody>
            </table>
            <asp:Panel ID="pnlVacio" runat="server" Visible="false" CssClass="sgv-vacio">
                No hay viajes en esta fase con los filtros elegidos.
            </asp:Panel>
        </div>

        <p class="sx-nota" style="margin-top: 0.75rem;"><asp:Literal ID="litResumen" runat="server" /></p>
    </div>
    <script>var SGV = Object.assign(window.SGV || {}, { autoActualizarGps: <%= AutoActualizarGps ? "true" : "false" %>, btnActualizarGps: '<%= btnActualizarGps.ClientID %>' });</script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/Exportacion/RegistroSeguimiento.js") %>"></script>
</asp:Content>

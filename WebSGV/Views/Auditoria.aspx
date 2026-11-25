<%@ Page Title="Auditoría del sistema" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Auditoria.aspx.cs" Inherits="WebSGV.Views.Auditoria" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <div class="sgv-encabezado">
        <div class="sgv-encabezado-titulo">
            <h1 class="h2">Auditoría del sistema</h1>
            <p>Registro de todos los cambios realizados en el sistema</p>
        </div>
        <div class="sgv-encabezado-acciones">
            <asp:Button ID="btnExportarExcel" runat="server" Text="Exportar Excel"
                CssClass="btn btn-success btn-sm" OnClick="btnExportarExcel_Click" data-descarga="true" />
            <a class="sgv-ayuda" href="<%: ResolveUrl("~/Views/Ayuda.aspx?tema=auditoria") %>" target="_blank" rel="noopener"
                data-ayuda="auditoria" aria-label="Ayuda de esta p&#xE1;gina">?</a>
        </div>
    </div>

    <details class="sgv-filtros" open data-plegar-movil>
        <summary>Filtros <span class="sgv-filtros-cuenta"></span></summary>
        <div class="sgv-filtros-cuerpo" data-enter="<%= btnBuscar.ClientID %>">
            <div class="form-row align-items-end">
                <div class="col-6 col-md-2 form-group">
                    <label for="<%= txtFechaDesde.ClientID %>" class="sgv-etiqueta">Desde</label>
                    <asp:TextBox ID="txtFechaDesde" runat="server" CssClass="form-control form-control-sm" TextMode="Date"></asp:TextBox>
                </div>
                <div class="col-6 col-md-2 form-group">
                    <label for="<%= txtFechaHasta.ClientID %>" class="sgv-etiqueta">Hasta</label>
                    <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control form-control-sm" TextMode="Date"></asp:TextBox>
                </div>
                <div class="col-12 col-md-2 form-group">
                    <label for="<%= ddlAccion.ClientID %>" class="sgv-etiqueta">Acción</label>
                    <%-- Las acciones que hay en la auditoría (ConsultaAuditoriaService.ObtenerAcciones, M-135). --%>
                    <asp:DropDownList ID="ddlAccion" runat="server" CssClass="form-control form-control-sm" />
                </div>
                <div class="col-12 col-md-2 form-group">
                    <label for="<%= ddlTabla.ClientID %>" class="sgv-etiqueta">Tabla</label>
                    <asp:DropDownList ID="ddlTabla" runat="server" CssClass="form-control form-control-sm">
                        <asp:ListItem Value="">Todas</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="col-12 col-md-2 form-group">
                    <label for="<%= txtUsuario.ClientID %>" class="sgv-etiqueta">Usuario</label>
                    <asp:TextBox ID="txtUsuario" runat="server" CssClass="form-control form-control-sm"
                        placeholder="Nombre de usuario"></asp:TextBox>
                </div>
                <div class="col-12 col-md-2 form-group">
                    <asp:Button ID="btnBuscar" runat="server" Text="Buscar"
                        CssClass="btn btn-primary btn-sm btn-block" OnClick="btnBuscar_Click" />
                </div>
            </div>
        </div>
    </details>

    <section class="sgv-panel">
        <div class="sgv-panel-cab">
            <div>
                <h2 class="sgv-panel-titulo">Registros de auditoría</h2>
                <p class="sgv-panel-nota mb-0" aria-live="polite"><asp:Literal ID="litResumen" runat="server" Mode="Encode" EnableViewState="false" /></p>
            </div>
            <%-- Cifras de toda la auditoría (antes, cuatro tarjetas): se calculan al entrar y al buscar. --%>
            <asp:Literal ID="litCifras" runat="server" />
        </div>
        <div class="table-responsive">
            <asp:GridView ID="gvAuditoria" runat="server"
                CssClass="table sgv-tabla mb-0 aud-tabla"
                AutoGenerateColumns="false"
                EmptyDataText="No se encontraron registros de auditoría con los filtros elegidos."
                AllowPaging="true"
                AllowCustomPaging="true"
                EnableViewState="false"
                PageSize="25"
                GridLines="None" CellSpacing="-1"
                OnPageIndexChanging="gvAuditoria_PageIndexChanging">
                <PagerSettings Mode="NumericFirstLast" FirstPageText="Primera"
                    LastPageText="Última" PageButtonCount="5" />
                <PagerStyle CssClass="sgv-paginador" />
                <EmptyDataRowStyle CssClass="sgv-vacio" />
                <Columns>
                    <asp:BoundField DataField="IdAuditoria" HeaderText="ID"
                        HeaderStyle-CssClass="sgv-col-id d-none d-md-table-cell" ItemStyle-CssClass="sgv-col-id d-none d-md-table-cell sgv-suave" />
                    <asp:TemplateField HeaderText="Fecha y hora" HeaderStyle-CssClass="aud-col-fecha" ItemStyle-CssClass="aud-col-fecha">
                        <ItemTemplate><div class="aud-fecha"><span class="aud-fecha-dia"><%#: Eval("FechaHora", "{0:dd/MM/yyyy}") %></span><span class="aud-fecha-hora"><%#: Eval("FechaHora", "{0:HH:mm:ss}") %></span></div></ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Usuario">
                        <ItemTemplate><div class="font-weight-bold"><%#: Eval("NombreUsuario") %></div><div class="sgv-suave"><%#: Eval("RolUsuario") %></div></ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Acción" HeaderStyle-CssClass="aud-col-accion" ItemStyle-CssClass="aud-col-accion">
                        <ItemTemplate><%# SelloAccion(Eval("Accion")) %></ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="TablaAfectada" HeaderText="Tabla"
                        HeaderStyle-CssClass="aud-col-tabla" ItemStyle-CssClass="aud-col-tabla" />
                    <asp:BoundField DataField="IdRegistroAfectado" HeaderText="ID reg."
                        HeaderStyle-CssClass="aud-col-idreg d-none d-md-table-cell" ItemStyle-CssClass="aud-col-idreg d-none d-md-table-cell" />
                    <asp:TemplateField HeaderText="Descripción" HeaderStyle-CssClass="d-none d-md-table-cell" ItemStyle-CssClass="d-none d-md-table-cell">
                        <ItemTemplate><div class="aud-descripcion" title='<%#: Eval("Descripcion") %>'><%#: TruncateText(Eval("Descripcion"), 100) %></div></ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Detalle" HeaderStyle-CssClass="sgv-col-acciones" ItemStyle-CssClass="sgv-col-acciones">
                        <ItemTemplate><button type="button" class="sgv-btn-icono info aud-btn-detalle" data-accion="mostrarDetalle" data-id='<%#: Eval("IdAuditoria") %>' title="Ver detalle" aria-label='<%#: "Ver detalle del registro " + Eval("IdAuditoria") %>'><i class="fas fa-search-plus" aria-hidden="true"></i></button></ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </section>

    <!-- Modal Detalle -->
    <div class="modal fade" id="modalDetalle" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalDetalleTitulo">
        <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title" id="modalDetalleTitulo">Detalle del registro de auditoría #<span id="detalleId"></span></h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <!-- Cargando: el detalle se pide al abrir (WebMethod ObtenerDetalle) -->
                    <div id="detalleCargando" class="sgv-cargando d-none" role="status">Cargando el detalle…</div>
                    <div id="detalleAviso" class="alert alert-warning d-none" role="alert"></div>
                    <div id="detalleContenido">
                        <div class="mb-3">
                            <div class="sgv-etiqueta">Descripción</div>
                            <div id="detalleDescripcion" class="aud-pre"></div>
                        </div>

                        <div class="row mb-3">
                            <div class="col-sm-6 mb-3 mb-sm-0">
                                <div class="sgv-etiqueta">Dirección IP</div>
                                <div id="detalleIP" class="aud-pre"></div>
                            </div>
                            <div class="col-sm-6">
                                <div class="sgv-etiqueta">Navegador</div>
                                <div id="detalleNavegador" class="aud-pre aud-pre-partir"></div>
                            </div>
                        </div>

                        <div class="mb-3 d-none" id="seccionAnteriores">
                            <div class="sgv-etiqueta">Valores anteriores</div>
                            <pre id="detalleAnteriores" class="aud-json aud-json-antes"></pre>
                        </div>

                        <div class="mb-3 d-none" id="seccionNuevos">
                            <div class="sgv-etiqueta">Valores nuevos</div>
                            <pre id="detalleNuevos" class="aud-json aud-json-despues"></pre>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary btn-sm" data-dismiss="modal">Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/Auditoria.css") %>" rel="stylesheet" />
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/Auditoria.js") %>"></script>
</asp:Content>

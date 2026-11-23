<%@ Page Title="Auditoría del sistema" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Auditoria.aspx.cs" Inherits="WebSGV.Views.Auditoria" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <div class="container-fluid px-4">

        <div class="sgv-encabezado">
            <div class="sgv-encabezado-titulo">
                <h1 class="h2">Auditoría del sistema</h1>
                <p>Registro de todos los cambios realizados en el sistema</p>
            </div>
            <div class="sgv-encabezado-acciones">
                <asp:Button ID="btnExportarExcel" runat="server" Text="Exportar Excel"
                    CssClass="btn btn-success btn-sm" OnClick="btnExportarExcel_Click" data-descarga="true" />
            </div>
        </div>

        <!-- Estadísticas rápidas -->
        <div class="row mb-4">
            <div class="col-6 col-md-3 mb-3 mb-md-0">
                <div class="aud-stat-card aud-stat-primary">
                    <div class="aud-stat-icon"><i class="fas fa-clipboard-list"></i></div>
                    <div class="aud-stat-info">
                        <div class="aud-stat-number">
                            <asp:Label ID="lblTotalRegistros" runat="server" Text="0"></asp:Label>
                        </div>
                        <div class="aud-stat-label">Total de registros</div>
                    </div>
                </div>
            </div>
            <div class="col-6 col-md-3 mb-3 mb-md-0">
                <div class="aud-stat-card aud-stat-info">
                    <div class="aud-stat-icon"><i class="fas fa-calendar-day"></i></div>
                    <div class="aud-stat-info">
                        <div class="aud-stat-number">
                            <asp:Label ID="lblRegistrosHoy" runat="server" Text="0"></asp:Label>
                        </div>
                        <div class="aud-stat-label">Hoy</div>
                    </div>
                </div>
            </div>
            <div class="col-6 col-md-3">
                <div class="aud-stat-card aud-stat-warning">
                    <div class="aud-stat-icon"><i class="fas fa-users"></i></div>
                    <div class="aud-stat-info">
                        <div class="aud-stat-number">
                            <asp:Label ID="lblUsuariosActivos" runat="server" Text="0"></asp:Label>
                        </div>
                        <div class="aud-stat-label">Usuarios activos</div>
                    </div>
                </div>
            </div>
            <div class="col-6 col-md-3">
                <div class="aud-stat-card aud-stat-danger">
                    <div class="aud-stat-icon"><i class="fas fa-database"></i></div>
                    <div class="aud-stat-info">
                        <div class="aud-stat-number">
                            <asp:Label ID="lblTablasAfectadas" runat="server" Text="0"></asp:Label>
                        </div>
                        <div class="aud-stat-label">Tablas afectadas</div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Filtros -->
        <div class="aud-section-card mb-4">
            <div class="aud-section-header">
                <h5 class="aud-section-title">
                    <i class="fas fa-filter mr-2"></i>Filtros de Búsqueda
                </h5>
            </div>
            <div class="aud-section-body" data-enter="<%= btnBuscar.ClientID %>">
                <div class="row">
                    <div class="col-12 col-md-2">
                        <div class="form-group">
                            <label for="<%= txtFechaDesde.ClientID %>" class="aud-form-label">Desde</label>
                            <asp:TextBox ID="txtFechaDesde" runat="server" CssClass="form-control form-control-sm" TextMode="Date"></asp:TextBox>
                        </div>
                    </div>
                    <div class="col-12 col-md-2">
                        <div class="form-group">
                            <label for="<%= txtFechaHasta.ClientID %>" class="aud-form-label">Hasta</label>
                            <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control form-control-sm" TextMode="Date"></asp:TextBox>
                        </div>
                    </div>
                    <div class="col-12 col-md-2">
                        <div class="form-group">
                            <label for="<%= ddlAccion.ClientID %>" class="aud-form-label">Acción</label>
                            <asp:DropDownList ID="ddlAccion" runat="server" CssClass="form-control form-control-sm">
                                <asp:ListItem Value="">-- Todas --</asp:ListItem>
                                <asp:ListItem Value="INSERT">Inserción</asp:ListItem>
                                <asp:ListItem Value="UPDATE">Actualización</asp:ListItem>
                                <asp:ListItem Value="DELETE">Eliminación</asp:ListItem>
                                <asp:ListItem Value="LOGIN">Inicio de sesión</asp:ListItem>
                                <asp:ListItem Value="LOGIN_FALLIDO">Inicio de sesión fallido</asp:ListItem>
                                <asp:ListItem Value="LOGIN_BLOQUEADO">Acceso bloqueado</asp:ListItem>
                                <asp:ListItem Value="LOGOUT">Cierre de sesión</asp:ListItem>
                                <asp:ListItem Value="APROBAR">Aprobación</asp:ListItem>
                                <asp:ListItem Value="RECHAZAR">Rechazo</asp:ListItem>
                                <asp:ListItem Value="LIQUIDAR">Liquidación</asp:ListItem>
                                <asp:ListItem Value="RETIRAR">Retiro</asp:ListItem>
                            </asp:DropDownList>
                        </div>
                    </div>
                    <div class="col-12 col-md-2">
                        <div class="form-group">
                            <label for="<%= ddlTabla.ClientID %>" class="aud-form-label">Tabla</label>
                            <asp:DropDownList ID="ddlTabla" runat="server" CssClass="form-control form-control-sm">
                                <asp:ListItem Value="">-- Todas --</asp:ListItem>
                            </asp:DropDownList>
                        </div>
                    </div>
                    <div class="col-12 col-md-2">
                        <div class="form-group">
                            <label for="<%= txtUsuario.ClientID %>" class="aud-form-label">Usuario</label>
                            <asp:TextBox ID="txtUsuario" runat="server" CssClass="form-control form-control-sm"
                                placeholder="Nombre de usuario"></asp:TextBox>
                        </div>
                    </div>
                    <div class="col-12 col-md-2">
                        <div class="form-group">
                            <span class="aud-form-label d-none d-md-block" aria-hidden="true">&nbsp;</span>
                            <div>
                                <asp:Button ID="btnBuscar" runat="server" Text="Buscar"
                                    CssClass="btn btn-primary btn-sm btn-block" OnClick="btnBuscar_Click" />
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Tabla de resultados -->
        <div class="aud-section-card">
            <div class="aud-section-header aud-section-header-info">
                <div class="d-flex justify-content-between align-items-center flex-wrap">
                    <h5 class="aud-section-title mb-2 mb-md-0">
                        <i class="fas fa-list mr-2"></i>Registros de Auditoría
                    </h5>
                    <span class="badge badge-primary">
                        <asp:Label ID="lblCantidadResultados" runat="server" Text="0 registros"></asp:Label>
                    </span>
                </div>
            </div>
            <div class="aud-section-body p-0">
                <div class="table-responsive">
                    <asp:GridView ID="gvAuditoria" runat="server"
                        CssClass="table table-sm table-hover mb-0 aud-table"
                        AutoGenerateColumns="false"
                        EmptyDataText="No se encontraron registros de auditoría"
                        AllowPaging="true"
                        AllowCustomPaging="true"
                        EnableViewState="false"
                        PageSize="25"
                        OnPageIndexChanging="gvAuditoria_PageIndexChanging">
                        <PagerSettings Mode="NumericFirstLast" FirstPageText="« Primera"
                            LastPageText="Última »" PageButtonCount="5" />
                        <PagerStyle CssClass="aud-pager" HorizontalAlign="Center" />
                        <Columns>
                            <asp:BoundField DataField="IdAuditoria" HeaderText="ID"
                                ItemStyle-CssClass="text-center text-muted" HeaderStyle-Width="60" />
                            <asp:TemplateField HeaderText="FECHA / HORA" HeaderStyle-Width="140">
                                <ItemTemplate><div class="aud-fecha"><span class="aud-fecha-date"><%#: Eval("FechaHora", "{0:dd/MM/yyyy}") %></span><span class="aud-fecha-time"><%#: Eval("FechaHora", "{0:HH:mm:ss}") %></span></div></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="USUARIO">
                                <ItemTemplate><div><strong><%#: Eval("NombreUsuario") %></strong><br /><small class="text-muted"><%#: Eval("RolUsuario") %></small></div></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="ACCIÓN" HeaderStyle-Width="110">
                                <ItemTemplate><span class='aud-badge-accion aud-badge-<%#: ObtenerClaseAccion(Eval("Accion")) %>'><i class='<%#: ObtenerIconoAccion(Eval("Accion")) %> mr-1'></i><%#: Eval("Accion") %></span></ItemTemplate>
                                <ItemStyle CssClass="text-center" />
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="TABLA" HeaderStyle-Width="130">
                                <ItemTemplate><span class="aud-badge-tabla"><i class="fas fa-table mr-1"></i><%#: Eval("TablaAfectada") %></span></ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="IdRegistroAfectado" HeaderText="ID REG."
                                ItemStyle-CssClass="text-center" HeaderStyle-Width="70" />
                            <asp:TemplateField HeaderText="DESCRIPCIÓN">
                                <ItemTemplate><div class="aud-descripcion" title='<%#: Eval("Descripcion") %>'><%#: TruncateText(Eval("Descripcion"), 100) %></div></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="DETALLE" HeaderStyle-Width="80">
                                <ItemTemplate><button type="button" class="btn btn-outline-info btn-sm aud-btn-detalle" data-accion="mostrarDetalle" data-id='<%#: Eval("IdAuditoria") %>' title="Ver detalle" aria-label='<%#: "Ver detalle del registro " + Eval("IdAuditoria") %>'><i class="fas fa-search-plus" aria-hidden="true"></i></button></ItemTemplate>
                                <ItemStyle CssClass="text-center" />
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
        </div>

    </div>

    <!-- Modal Detalle -->
    <div class="modal fade" id="modalDetalle" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalDetalleTitulo">
        <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
            <div class="modal-content">
                <div class="modal-header" style="background: #1e293b; color: white;">
                    <h5 class="modal-title" id="modalDetalleTitulo"><i class="fas fa-search-plus mr-2" aria-hidden="true"></i>Detalle de Auditoría #<span id="detalleId"></span></h5>
                    <button type="button" class="close" data-dismiss="modal" style="color: white;" aria-label="Cerrar">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <!-- Cargando: el detalle se pide al abrir (WebMethod ObtenerDetalle) -->
                    <div id="detalleCargando" class="text-center py-4 d-none" role="status">
                        <div class="spinner-border text-primary" aria-hidden="true"></div>
                        <p class="mt-2 mb-0 text-muted">Cargando detalle...</p>
                    </div>
                    <div id="detalleAviso" class="alert alert-warning d-none" role="alert"></div>
                    <div id="detalleContenido">
                    <!-- Descripción -->
                    <div class="mb-3">
                        <div class="font-weight-bold text-muted small text-uppercase mb-2">Descripción</div>
                        <div id="detalleDescripcion" class="p-2 bg-light rounded" style="white-space: pre-wrap;"></div>
                    </div>

                    <!-- Info técnica -->
                    <div class="row mb-3">
                        <div class="col-6">
                            <div class="font-weight-bold text-muted small text-uppercase mb-2">Dirección IP</div>
                            <div id="detalleIP" class="p-2 bg-light rounded"></div>
                        </div>
                        <div class="col-6">
                            <div class="font-weight-bold text-muted small text-uppercase mb-2">Navegador</div>
                            <div id="detalleNavegador" class="p-2 bg-light rounded" style="word-break: break-all;"></div>
                        </div>
                    </div>

                    <!-- Valores anteriores -->
                    <div class="mb-3" id="seccionAnteriores" style="display:none;">
                        <div class="font-weight-bold small text-uppercase mb-2" style="color:#dc2626;">
                            <i class="fas fa-minus-circle mr-1"></i>Valores Anteriores
                        </div>
                        <pre id="detalleAnteriores" class="p-3 rounded" style="background:#fef2f2; border:1px solid #fca5a5; max-height:250px; overflow:auto; font-size:0.8rem;"></pre>
                    </div>

                    <!-- Valores nuevos -->
                    <div class="mb-3" id="seccionNuevos" style="display:none;">
                        <div class="font-weight-bold small text-uppercase mb-2" style="color:#059669;">
                            <i class="fas fa-plus-circle mr-1"></i>Valores Nuevos
                        </div>
                        <pre id="detalleNuevos" class="p-3 rounded" style="background:#f0fdf4; border:1px solid #86efac; max-height:250px; overflow:auto; font-size:0.8rem;"></pre>
                    </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary btn-sm" data-dismiss="modal">Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <!-- CSS -->
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/Auditoria.css") %>" rel="stylesheet" />

    <!-- JavaScript -->
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/Auditoria.js") %>"></script>
</asp:Content>

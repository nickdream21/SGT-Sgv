<%@ Page Title="Auditoría del Sistema" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Auditoria.aspx.cs" Inherits="WebSGV.Views.Auditoria" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <div class="container-fluid px-4">

        <!-- Header -->
        <div class="row mb-4">
            <div class="col-12">
                <div class="aud-page-header">
                    <div class="d-flex justify-content-between align-items-center flex-wrap">
                        <div>
                            <h2 class="aud-page-title mb-1">
                                <i class="fas fa-shield-alt mr-2"></i>Auditoría del Sistema
                            </h2>
                            <p class="text-muted mb-0">
                                Registro de todos los cambios realizados en el sistema
                            </p>
                        </div>
                        <div class="mt-2 mt-md-0">
                            <asp:Button ID="btnExportarExcel" runat="server" Text="Exportar Excel"
                                CssClass="btn btn-success btn-sm" OnClick="btnExportarExcel_Click" />
                        </div>
                    </div>
                </div>
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
                        <div class="aud-stat-label">Total Registros</div>
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
                        <div class="aud-stat-label">Usuarios Activos</div>
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
                        <div class="aud-stat-label">Tablas Afectadas</div>
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
            <div class="aud-section-body">
                <div class="row">
                    <div class="col-12 col-md-2">
                        <div class="form-group">
                            <label class="aud-form-label">Desde</label>
                            <asp:TextBox ID="txtFechaDesde" runat="server" CssClass="form-control form-control-sm" TextMode="Date"></asp:TextBox>
                        </div>
                    </div>
                    <div class="col-12 col-md-2">
                        <div class="form-group">
                            <label class="aud-form-label">Hasta</label>
                            <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control form-control-sm" TextMode="Date"></asp:TextBox>
                        </div>
                    </div>
                    <div class="col-12 col-md-2">
                        <div class="form-group">
                            <label class="aud-form-label">Acción</label>
                            <asp:DropDownList ID="ddlAccion" runat="server" CssClass="form-control form-control-sm">
                                <asp:ListItem Value="">-- Todas --</asp:ListItem>
                                <asp:ListItem Value="INSERT">Insercion</asp:ListItem>
                                <asp:ListItem Value="UPDATE">Actualizacion</asp:ListItem>
                                <asp:ListItem Value="DELETE">Eliminacion</asp:ListItem>
                                <asp:ListItem Value="LOGIN">Inicio Sesion</asp:ListItem>
                                <asp:ListItem Value="LOGIN_FALLIDO">Login Fallido</asp:ListItem>
                                <asp:ListItem Value="LOGOUT">Cierre Sesion</asp:ListItem>
                                <asp:ListItem Value="APROBAR">Aprobacion</asp:ListItem>
                                <asp:ListItem Value="RECHAZAR">Rechazo</asp:ListItem>
                                <asp:ListItem Value="LIQUIDAR">Liquidacion</asp:ListItem>
                                <asp:ListItem Value="RETIRAR">Retiro</asp:ListItem>
                            </asp:DropDownList>
                        </div>
                    </div>
                    <div class="col-12 col-md-2">
                        <div class="form-group">
                            <label class="aud-form-label">Tabla</label>
                            <asp:DropDownList ID="ddlTabla" runat="server" CssClass="form-control form-control-sm">
                                <asp:ListItem Value="">-- Todas --</asp:ListItem>
                            </asp:DropDownList>
                        </div>
                    </div>
                    <div class="col-12 col-md-2">
                        <div class="form-group">
                            <label class="aud-form-label">Usuario</label>
                            <asp:TextBox ID="txtUsuario" runat="server" CssClass="form-control form-control-sm"
                                placeholder="Nombre de usuario"></asp:TextBox>
                        </div>
                    </div>
                    <div class="col-12 col-md-2">
                        <div class="form-group">
                            <label class="aud-form-label d-none d-md-block">&nbsp;</label>
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
                        PageSize="25"
                        OnPageIndexChanging="gvAuditoria_PageIndexChanging">
                        <PagerSettings Mode="NumericFirstLast" FirstPageText="« Primera"
                            LastPageText="Última »" PageButtonCount="5" />
                        <PagerStyle CssClass="aud-pager" HorizontalAlign="Center" />
                        <Columns>
                            <asp:BoundField DataField="IdAuditoria" HeaderText="ID"
                                ItemStyle-CssClass="text-center text-muted" HeaderStyle-Width="60" />
                            <asp:TemplateField HeaderText="FECHA / HORA" HeaderStyle-Width="140">
                                <ItemTemplate>
                                    <div class="aud-fecha">
                                        <span class="aud-fecha-date"><%# Eval("FechaHora", "{0:dd/MM/yyyy}") %></span>
                                        <span class="aud-fecha-time"><%# Eval("FechaHora", "{0:HH:mm:ss}") %></span>
                                    </div>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="USUARIO">
                                <ItemTemplate>
                                    <div>
                                        <strong><%# Eval("NombreUsuario") %></strong>
                                        <br />
                                        <small class="text-muted"><%# Eval("RolUsuario") %></small>
                                    </div>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="ACCIÓN" HeaderStyle-Width="110">
                                <ItemTemplate>
                                    <span class='aud-badge-accion aud-badge-<%# ObtenerClaseAccion(Eval("Accion")) %>'>
                                        <i class='<%# ObtenerIconoAccion(Eval("Accion")) %> mr-1'></i><%# Eval("Accion") %>
                                    </span>
                                </ItemTemplate>
                                <ItemStyle CssClass="text-center" />
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="TABLA" HeaderStyle-Width="130">
                                <ItemTemplate>
                                    <span class="aud-badge-tabla">
                                        <i class="fas fa-table mr-1"></i><%# Eval("TablaAfectada") %>
                                    </span>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="IdRegistroAfectado" HeaderText="ID REG."
                                ItemStyle-CssClass="text-center" HeaderStyle-Width="70" />
                            <asp:TemplateField HeaderText="DESCRIPCIÓN">
                                <ItemTemplate>
                                    <div class="aud-descripcion" title='<%# Eval("Descripcion") %>'>
                                        <%# TruncateText(Eval("Descripcion"), 100) %>
                                    </div>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="DETALLE" HeaderStyle-Width="80">
                                <ItemTemplate>
                                    <button type="button" class="btn btn-outline-info btn-sm aud-btn-detalle"
                                        onclick='mostrarDetalle(<%# Eval("IdAuditoria") %>)'
                                        data-anterior='<%# Server.HtmlEncode(Eval("ValoresAnteriores")?.ToString() ?? "") %>'
                                        data-nuevo='<%# Server.HtmlEncode(Eval("ValoresNuevos")?.ToString() ?? "") %>'
                                        data-desc='<%# Server.HtmlEncode(Eval("Descripcion")?.ToString() ?? "") %>'
                                        data-ip='<%# Eval("DireccionIP") %>'
                                        data-nav='<%# Server.HtmlEncode(TruncateText(Eval("Navegador"), 80)) %>'>
                                        <i class="fas fa-search-plus"></i>
                                    </button>
                                </ItemTemplate>
                                <ItemStyle CssClass="text-center" />
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
        </div>

    </div>

    <!-- Modal Detalle -->
    <div class="modal fade" id="modalDetalle" tabindex="-1">
        <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
            <div class="modal-content">
                <div class="modal-header" style="background: #1e293b; color: white;">
                    <h5 class="modal-title"><i class="fas fa-search-plus mr-2"></i>Detalle de Auditoría #<span id="detalleId"></span></h5>
                    <button type="button" class="close" data-dismiss="modal" style="color: white;">
                        <span>&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <!-- Descripción -->
                    <div class="mb-3">
                        <label class="font-weight-bold text-muted small text-uppercase">Descripción</label>
                        <div id="detalleDescripcion" class="p-2 bg-light rounded" style="white-space: pre-wrap;"></div>
                    </div>

                    <!-- Info técnica -->
                    <div class="row mb-3">
                        <div class="col-6">
                            <label class="font-weight-bold text-muted small text-uppercase">Dirección IP</label>
                            <div id="detalleIP" class="p-2 bg-light rounded"></div>
                        </div>
                        <div class="col-6">
                            <label class="font-weight-bold text-muted small text-uppercase">Navegador</label>
                            <div id="detalleNavegador" class="p-2 bg-light rounded" style="word-break: break-all;"></div>
                        </div>
                    </div>

                    <!-- Valores anteriores -->
                    <div class="mb-3" id="seccionAnteriores" style="display:none;">
                        <label class="font-weight-bold small text-uppercase" style="color:#dc2626;">
                            <i class="fas fa-minus-circle mr-1"></i>Valores Anteriores
                        </label>
                        <pre id="detalleAnteriores" class="p-3 rounded" style="background:#fef2f2; border:1px solid #fca5a5; max-height:250px; overflow:auto; font-size:0.8rem;"></pre>
                    </div>

                    <!-- Valores nuevos -->
                    <div class="mb-3" id="seccionNuevos" style="display:none;">
                        <label class="font-weight-bold small text-uppercase" style="color:#059669;">
                            <i class="fas fa-plus-circle mr-1"></i>Valores Nuevos
                        </label>
                        <pre id="detalleNuevos" class="p-3 rounded" style="background:#f0fdf4; border:1px solid #86efac; max-height:250px; overflow:auto; font-size:0.8rem;"></pre>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary btn-sm" data-dismiss="modal">Cerrar</button>
                </div>
            </div>
        </div>
    </div>

    <!-- CSS -->
    <style>
        .aud-page-header {
            padding-bottom: 1rem;
            border-bottom: 2px solid #cbd5e1;
        }
        .aud-page-title {
            color: #1e293b;
            font-weight: 600;
            font-size: 1.75rem;
            margin: 0;
        }

        /* Estadísticas */
        .aud-stat-card {
            display: flex;
            align-items: center;
            padding: 1rem;
            border-radius: 0.5rem;
            background: white;
            border: 1px solid #e2e8f0;
            box-shadow: 0 1px 3px rgba(0,0,0,0.06);
        }
        .aud-stat-icon {
            width: 48px;
            height: 48px;
            border-radius: 0.5rem;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 1.25rem;
            margin-right: 1rem;
            flex-shrink: 0;
        }
        .aud-stat-primary .aud-stat-icon { background: #dbeafe; color: #2563eb; }
        .aud-stat-info .aud-stat-icon { background: #e0f2fe; color: #0891b2; }
        .aud-stat-warning .aud-stat-icon { background: #fef3c7; color: #d97706; }
        .aud-stat-danger .aud-stat-icon { background: #fef2f2; color: #dc2626; }
        .aud-stat-number { font-size: 1.5rem; font-weight: 700; color: #1e293b; line-height: 1; }
        .aud-stat-label { font-size: 0.75rem; color: #64748b; font-weight: 500; text-transform: uppercase; letter-spacing: 0.03em; }

        /* Secciones */
        .aud-section-card {
            background: white;
            border: 1px solid #cbd5e1;
            border-radius: 0.5rem;
            overflow: hidden;
        }
        .aud-section-header {
            background: #f8fafc;
            padding: 1rem 1.25rem;
            border-bottom: 1px solid #cbd5e1;
        }
        .aud-section-header-info {
            background: #e0f2fe;
            border-bottom-color: #7dd3fc;
        }
        .aud-section-title {
            color: #1e293b;
            font-size: 1.125rem;
            font-weight: 600;
            margin: 0;
        }
        .aud-section-body { padding: 1.25rem; }
        .aud-form-label {
            color: #1e293b;
            font-weight: 500;
            font-size: 0.8125rem;
            margin-bottom: 0.25rem;
        }

        /* Tabla */
        .aud-table thead th {
            background: #f8fafc;
            color: #1e293b;
            font-weight: 600;
            font-size: 0.75rem;
            text-transform: uppercase;
            letter-spacing: 0.03em;
            padding: 0.75rem 0.625rem;
            border-bottom: 2px solid #cbd5e1;
            white-space: nowrap;
        }
        .aud-table tbody td {
            vertical-align: middle;
            padding: 0.5rem 0.625rem;
            font-size: 0.8125rem;
            border-bottom: 1px solid #e2e8f0;
        }
        .aud-table tbody tr:hover { background: #f8fafc; }

        .aud-fecha { line-height: 1.3; }
        .aud-fecha-date { display: block; font-weight: 600; font-size: 0.8125rem; }
        .aud-fecha-time { display: block; color: #64748b; font-size: 0.75rem; }

        .aud-descripcion {
            max-width: 300px;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
            font-size: 0.8125rem;
        }

        /* Badges de acción */
        .aud-badge-accion {
            display: inline-block;
            padding: 0.25rem 0.625rem;
            border-radius: 0.25rem;
            font-size: 0.7rem;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.03em;
        }
        .aud-badge-insert { background: #dcfce7; color: #166534; }
        .aud-badge-update { background: #dbeafe; color: #1e40af; }
        .aud-badge-delete { background: #fef2f2; color: #991b1b; }
        .aud-badge-login { background: #e0f2fe; color: #0c4a6e; }
        .aud-badge-login_fallido { background: #fef2f2; color: #991b1b; }
        .aud-badge-logout { background: #f1f5f9; color: #475569; }
        .aud-badge-aprobar { background: #dcfce7; color: #166534; }
        .aud-badge-rechazar { background: #fef3c7; color: #92400e; }
        .aud-badge-liquidar { background: #ede9fe; color: #5b21b6; }
        .aud-badge-retirar { background: #fff7ed; color: #9a3412; }
        .aud-badge-default { background: #f1f5f9; color: #475569; }

        .aud-badge-tabla {
            display: inline-block;
            padding: 0.2rem 0.5rem;
            background: #f1f5f9;
            border: 1px solid #cbd5e1;
            border-radius: 0.25rem;
            font-size: 0.7rem;
            font-weight: 600;
            color: #475569;
        }

        .aud-btn-detalle {
            padding: 0.25rem 0.5rem;
            font-size: 0.75rem;
        }

        /* Pager */
        .aud-pager { padding: 1rem; }
        .aud-pager td { padding: 0; }
        .aud-pager a, .aud-pager span {
            display: inline-block;
            padding: 0.375rem 0.75rem;
            margin: 0 0.125rem;
            border: 1px solid #cbd5e1;
            border-radius: 0.25rem;
            font-size: 0.8125rem;
            color: #2563eb;
            text-decoration: none;
        }
        .aud-pager span {
            background: #2563eb;
            color: white;
            border-color: #2563eb;
        }
        .aud-pager a:hover {
            background: #f1f5f9;
        }

        @media (max-width: 768px) {
            .aud-page-title { font-size: 1.375rem; }
            .aud-stat-number { font-size: 1.25rem; }
            .aud-stat-icon { width: 40px; height: 40px; font-size: 1rem; }
            .aud-descripcion { max-width: 150px; }
        }
    </style>

    <!-- JavaScript -->
    <script>
        function mostrarDetalle(id) {
            var btn = $('[onclick="mostrarDetalle(' + id + ')"]');
            var anterior = btn.data('anterior') || '';
            var nuevo = btn.data('nuevo') || '';
            var desc = btn.data('desc') || 'Sin descripción';
            var ip = btn.data('ip') || 'N/A';
            var nav = btn.data('nav') || 'N/A';

            $('#detalleId').text(id);
            $('#detalleDescripcion').text(desc);
            $('#detalleIP').text(ip);
            $('#detalleNavegador').text(nav);

            if (anterior) {
                try { anterior = JSON.stringify(JSON.parse(anterior), null, 2); } catch (e) { }
                $('#detalleAnteriores').text(anterior);
                $('#seccionAnteriores').show();
            } else {
                $('#seccionAnteriores').hide();
            }

            if (nuevo) {
                try { nuevo = JSON.stringify(JSON.parse(nuevo), null, 2); } catch (e) { }
                $('#detalleNuevos').text(nuevo);
                $('#seccionNuevos').show();
            } else {
                $('#seccionNuevos').hide();
            }

            $('#modalDetalle').modal('show');
        }
    </script>
</asp:Content>

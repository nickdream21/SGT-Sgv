<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ListaDespachos.aspx.cs" Inherits="WebSGV.Views.ListaDespachos" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .main-container {
            max-width: 1400px;
            margin: 0 auto;
        }

        .page-header {
            margin-bottom: 40px;
            text-align: center;
        }

        .page-title {
            color: #1e293b;
            font-size: 2.2rem;
            font-weight: 700;
            margin-bottom: 12px;
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 16px;
        }

        .page-subtitle {
            color: #64748b;
            font-size: 1.15rem;
            font-weight: 400;
        }

        .filters-card {
            background: white;
            border-radius: 16px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.06);
            border: 1px solid #e2e8f0;
            margin-bottom: 32px;
            overflow: hidden;
        }

        .filters-header {
            background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%);
            color: white;
            padding: 24px 32px;
            font-weight: 600;
            font-size: 1.15rem;
            display: flex;
            align-items: center;
            gap: 12px;
        }

        .filters-body {
            padding: 32px;
            background: #fafbfc;
        }

        .filter-section {
            background: white;
            border-radius: 12px;
            padding: 28px;
            margin-bottom: 20px;
            border: 1px solid #e2e8f0;
            box-shadow: 0 2px 8px rgba(0,0,0,0.02);
        }

        .filter-section-title {
            color: #374151;
            font-size: 1rem;
            font-weight: 600;
            margin-bottom: 20px;
            padding-bottom: 12px;
            border-bottom: 1px solid #f1f5f9;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .filter-section-title i {
            color: #2563eb;
            font-size: 1.1rem;
        }

        /* FILTROS ADICIONALES CORREGIDOS */
        .additional-filters {
            background: white;
            border-radius: 12px;
            border: 1px solid #e2e8f0;
            box-shadow: 0 2px 8px rgba(0,0,0,0.02);
            margin-bottom: 20px;
            overflow: hidden;
        }

        .additional-filters-toggle {
            background: #f8fafc;
            padding: 20px 28px;
            cursor: pointer;
            transition: all 0.3s ease;
            border: none;
            width: 100%;
            text-align: left;
            display: flex;
            align-items: center;
            justify-content: space-between;
            border-bottom: 1px solid #e2e8f0;
            font-family: inherit;
        }

        .additional-filters-toggle:hover {
            background: #f1f5f9;
        }

        .additional-filters-title {
            color: #374151;
            font-size: 1rem;
            font-weight: 600;
            display: flex;
            align-items: center;
            gap: 8px;
            margin: 0;
        }

        .toggle-icon {
            color: #2563eb;
            transition: transform 0.3s ease;
            font-size: 1rem;
        }

        .toggle-icon.rotated {
            transform: rotate(180deg);
        }

        .additional-filters-content {
            padding: 0;
            max-height: 0;
            overflow: hidden;
            transition: all 0.4s ease;
            opacity: 0;
        }

        .additional-filters-content.show {
            padding: 28px;
            max-height: 500px;
            opacity: 1;
        }

        .filter-label {
            color: #374151;
            font-weight: 600;
            font-size: 0.9rem;
            margin-bottom: 8px;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            display: block;
        }

        .form-control, .form-select {
            border: 2px solid #e2e8f0;
            border-radius: 8px;
            padding: 12px 16px;
            font-size: 0.95rem;
            transition: all 0.3s ease;
            background: white;
            min-height: 48px;
            width: 100%;
        }

        .form-control:focus, .form-select:focus {
            border-color: #2563eb;
            box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
            outline: none;
        }

        .filter-field {
            margin-bottom: 24px;
        }

        .buttons-section {
            background: white;
            border-radius: 12px;
            padding: 24px 28px;
            border: 1px solid #e2e8f0;
            box-shadow: 0 2px 8px rgba(0,0,0,0.02);
            display: flex;
            justify-content: center;
            gap: 16px;
            flex-wrap: wrap;
        }

        /* BOTONES SIMPLIFICADOS */
        .btn-filter {
            background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%);
            border: none;
            padding: 14px 28px;
            font-weight: 600;
            border-radius: 8px;
            color: white !important;
            font-size: 0.95rem;
            transition: all 0.3s ease;
            min-width: 140px;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            gap: 8px;
            text-decoration: none;
            cursor: pointer;
        }

        .btn-filter:hover {
            background: linear-gradient(135deg, #1d4ed8 0%, #1e40af 100%);
            transform: translateY(-2px);
            box-shadow: 0 8px 25px rgba(37, 99, 235, 0.3);
            color: white !important;
            text-decoration: none;
        }

        .btn-clear {
            background: #64748b;
            border: none;
            padding: 14px 28px;
            font-weight: 600;
            border-radius: 8px;
            color: white !important;
            font-size: 0.95rem;
            transition: all 0.3s ease;
            min-width: 140px;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            gap: 8px;
            text-decoration: none;
            cursor: pointer;
        }

        .btn-clear:hover {
            background: #475569;
            color: white !important;
            transform: translateY(-2px);
            box-shadow: 0 6px 20px rgba(100, 116, 139, 0.3);
            text-decoration: none;
        }

        .btn-nuevo {
            background: linear-gradient(135deg, #059669 0%, #047857 100%);
            border: none;
            padding: 14px 28px;
            font-weight: 600;
            border-radius: 8px;
            color: white !important;
            font-size: 0.95rem;
            transition: all 0.3s ease;
            min-width: 170px;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            gap: 8px;
            text-decoration: none;
            cursor: pointer;
        }

        .btn-nuevo:hover {
            background: linear-gradient(135deg, #047857 0%, #065f46 100%);
            color: white !important;
            transform: translateY(-2px);
            box-shadow: 0 8px 25px rgba(5, 150, 105, 0.3);
            text-decoration: none;
        }

        .results-card {
            background: white;
            border-radius: 16px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.06);
            border: 1px solid #e2e8f0;
            overflow: hidden;
        }

        .results-header {
            background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%);
            color: white;
            padding: 24px 32px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .results-title {
            font-weight: 600;
            font-size: 1.15rem;
            margin: 0;
            display: flex;
            align-items: center;
            gap: 12px;
        }

        .results-count {
            background: rgba(255,255,255,0.2);
            padding: 8px 16px;
            border-radius: 20px;
            font-size: 0.9rem;
            font-weight: 600;
        }

        .table-container {
            padding: 0;
            overflow-x: auto;
        }

        .table {
            margin: 0;
            font-size: 0.95rem;
        }

        .table thead th {
            background: #f8fafc;
            border-bottom: 2px solid #e2e8f0;
            color: #374151;
            font-weight: 600;
            font-size: 0.9rem;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            padding: 18px 16px;
            border-top: none;
            white-space: nowrap;
        }

        .table tbody td {
            padding: 16px;
            border-bottom: 1px solid #f1f5f9;
            vertical-align: middle;
        }

        .table tbody tr:hover {
            background: #f8fafc;
            transition: background 0.3s ease;
        }

        .status-badge {
            padding: 8px 16px;
            border-radius: 20px;
            font-size: 0.8rem;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            display: inline-block;
            min-width: 100px;
            text-align: center;
        }

        .status-programado {
            background: #dbeafe;
            color: #1d4ed8;
        }

        .status-en_proceso {
            background: #fef3c7;
            color: #d97706;
        }

        .status-completado {
            background: #d1fae5;
            color: #059669;
        }

        .status-cancelado {
            background: #fee2e2;
            color: #dc2626;
        }

        .btn-action {
            padding: 8px 12px;
            border-radius: 8px;
            font-size: 0.85rem;
            font-weight: 500;
            border: none;
            margin: 3px;
            transition: all 0.2s ease;
            min-width: 40px;
            display: inline-flex;
            align-items: center;
            justify-content: center;
        }

        .btn-view {
            background: #e0f2fe;
            color: #0369a1;
        }

        .btn-view:hover {
            background: #0369a1;
            color: white;
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(3, 105, 161, 0.3);
        }

        .btn-edit {
            background: #fef3c7;
            color: #d97706;
        }

        .btn-edit:hover {
            background: #d97706;
            color: white;
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(217, 119, 6, 0.3);
        }

        .btn-delete {
            background: #fee2e2;
            color: #dc2626;
        }

        .btn-delete:hover {
            background: #dc2626;
            color: white;
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(220, 38, 38, 0.3);
        }

        .pagination-container {
            padding: 24px 32px;
            background: #fafbfc;
            border-top: 1px solid #e2e8f0;
            display: flex;
            justify-content: center;
            align-items: center;
        }

        .no-data {
            text-align: center;
            padding: 80px 20px;
            color: #64748b;
        }

        .no-data i {
            font-size: 4rem;
            margin-bottom: 20px;
            opacity: 0.4;
            color: #94a3b8;
        }

        .no-data h5 {
            font-size: 1.4rem;
            margin-bottom: 12px;
            color: #475569;
        }

        .no-data p {
            font-size: 1rem;
            opacity: 0.8;
        }

        .btn-orden {
            background: linear-gradient(135deg, #10b981 0%, #059669 100%);
            color: white;
        }

        .btn-orden:hover {
            background: linear-gradient(135deg, #059669 0%, #047857 100%);
            color: white;
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(16, 185, 129, 0.3);
        }

        /* RESPONSIVE */
        @media (max-width: 992px) {
            .filters-body {
                padding: 24px;
            }

            .filter-section, .additional-filters-content.show {
                padding: 20px;
            }

            .buttons-section {
                padding: 20px;
                justify-content: center;
            }

            .page-title {
                font-size: 1.8rem;
            }
        }

        @media (max-width: 768px) {
            .filters-body {
                padding: 20px;
            }

            .filter-section, .additional-filters-content.show {
                padding: 16px;
                margin-bottom: 16px;
            }

            .additional-filters-toggle {
                padding: 16px 20px;
            }

            .buttons-section {
                padding: 16px;
                flex-direction: column;
            }

            .btn-filter, .btn-clear, .btn-nuevo {
                width: 100%;
                justify-content: center;
            }

            .page-title {
                font-size: 1.6rem;
                flex-direction: column;
                gap: 8px;
            }

            .table-container {
                font-size: 0.85rem;
            }

            .table thead th,
            .table tbody td {
                padding: 12px 8px;
            }
        }
    </style>

    <div class="container-fluid">
        <div class="main-container">

            <!-- Header -->
            <div class="page-header">
                <h1 class="page-title">
                    <i class="fas fa-list-alt"></i>
                    Despachos Programados
                </h1>
                <p class="page-subtitle">Gestiona y supervisa todos los despachos del sistema</p>
            </div>

            <!-- Filtros -->
            <div class="filters-card">
                <div class="filters-header">
                    <i class="fas fa-filter"></i>
                    Filtros de Búsqueda
                </div>
                <div class="filters-body">

                    <!-- Filtros Principales -->
                    <div class="filter-section">
                        <div class="filter-section-title">
                            <i class="fas fa-calendar-alt"></i>
                            Filtros Principales
                        </div>
                        <div class="row">
                            <div class="col-lg-3 col-md-6">
                                <div class="filter-field">
                                    <label class="filter-label">Estado</label>
                                    <asp:DropDownList ID="ddlEstadoFiltro" runat="server" CssClass="form-select">
                                        <asp:ListItem Value="" Selected="True">Todos los Estados</asp:ListItem>
                                        <asp:ListItem Value="PROGRAMADO">Programado</asp:ListItem>
                                        <asp:ListItem Value="EN_PROCESO">En Proceso</asp:ListItem>
                                        <asp:ListItem Value="COMPLETADO">Completado</asp:ListItem>
                                        <asp:ListItem Value="CANCELADO">Cancelado</asp:ListItem>
                                    </asp:DropDownList>
                                </div>
                            </div>

                            <div class="col-lg-3 col-md-6">
                                <div class="filter-field">
                                    <label class="filter-label">Fecha Desde</label>
                                    <asp:TextBox ID="txtFechaDesde" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                </div>
                            </div>

                            <div class="col-lg-3 col-md-6">
                                <div class="filter-field">
                                    <label class="filter-label">Fecha Hasta</label>
                                    <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                </div>
                            </div>

                            <div class="col-lg-3 col-md-6">
                                <div class="filter-field">
                                    <label class="filter-label">Lugar</label>
                                    <asp:DropDownList ID="ddlLugarFiltro" runat="server" CssClass="form-select">
                                        <asp:ListItem Value="" Selected="True">Todos los Lugares</asp:ListItem>
                                        <asp:ListItem Value="TRUJILLO">Trujillo</asp:ListItem>
                                        <asp:ListItem Value="GUAYAQUIL">Guayaquil</asp:ListItem>
                                        <asp:ListItem Value="LIMA">Lima</asp:ListItem>
                                        <asp:ListItem Value="QUITO">Quito</asp:ListItem>
                                        <asp:ListItem Value="PIURA">Piura</asp:ListItem>
                                        <asp:ListItem Value="CHICLAYO">Chiclayo</asp:ListItem>
                                        <asp:ListItem Value="MACHALA">Machala</asp:ListItem>
                                    </asp:DropDownList>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Filtros Adicionales -->
                    <div class="additional-filters">
                        <button type="button" class="additional-filters-toggle" onclick="toggleAdditionalFilters()">
                            <div class="additional-filters-title">
                                <i class="fas fa-users"></i>
                                Filtros Adicionales
                            </div>
                            <i class="fas fa-chevron-down toggle-icon" id="toggleIcon"></i>
                        </button>
                        <div class="additional-filters-content" id="additionalFiltersContent">
                            <div class="row">
                                <div class="col-lg-4 col-md-6">
                                    <div class="filter-field">
                                        <label class="filter-label">Conductor</label>
                                        <asp:DropDownList ID="ddlConductorFiltro" runat="server" CssClass="form-select">
                                            <asp:ListItem Value="" Selected="True">Todos los Conductores</asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>

                                <div class="col-lg-4 col-md-6">
                                    <div class="filter-field">
                                        <label class="filter-label">Cliente</label>
                                        <asp:DropDownList ID="ddlClienteFiltro" runat="server" CssClass="form-select">
                                            <asp:ListItem Value="" Selected="True">Todos los Clientes</asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>

                                <div class="col-lg-4 col-md-6">
                                    <div class="filter-field">
                                        <label class="filter-label">Operación</label>
                                        <asp:DropDownList ID="ddlOperacionFiltro" runat="server" CssClass="form-select">
                                            <asp:ListItem Value="" Selected="True">Todas las Operaciones</asp:ListItem>
                                            <asp:ListItem Value="CARGA">Carga</asp:ListItem>
                                            <asp:ListItem Value="DESCARGA">Descarga</asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Botones -->
                    <div class="buttons-section">
                        <asp:Button ID="btnBuscar" runat="server" CssClass="btn-filter"
                            Text="🔍 Buscar" OnClick="btnBuscar_Click" />

                        <asp:Button ID="btnLimpiar" runat="server" CssClass="btn-clear"
                            Text="🧹 Limpiar" OnClick="btnLimpiar_Click" CausesValidation="false" />

                        <asp:Button ID="btnNuevoDespacho" runat="server" CssClass="btn-nuevo"
                            Text="➕ Nuevo Despacho" OnClick="btnNuevoDespacho_Click" CausesValidation="false" />
                    </div>

                </div>
            </div>

            <!-- Resultados -->
            <div class="results-card">
                <div class="results-header">
                    <h5 class="results-title">
                        <i class="fas fa-truck"></i>
                        Lista de Despachos
                    </h5>
                    <div class="results-count">
                        <asp:Literal ID="litTotalRegistros" runat="server" Text="0 registros"></asp:Literal>
                    </div>
                </div>

                <div class="table-container">
                    <asp:GridView ID="gvDespachos" runat="server"
                        CssClass="table table-hover"
                        AutoGenerateColumns="false"
                        AllowPaging="true"
                        PageSize="15"
                        OnPageIndexChanging="gvDespachos_PageIndexChanging"
                        OnRowCommand="gvDespachos_RowCommand"
                        DataKeyNames="idDespacho"
                        EmptyDataText="">
                        <Columns>

                            <asp:BoundField DataField="fechaDespacho" HeaderText="Fecha"
                                DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="fw-bold" />

                            <asp:TemplateField HeaderText="Estado">
                                <ItemTemplate>
                                    <span class='status-badge status-<%# Eval("estadoDespacho").ToString().ToLower() %>'>
                                        <%# ObtenerTextoEstado(Eval("estadoDespacho").ToString()) %>
                                    </span>
                                </ItemTemplate>
                            </asp:TemplateField>

                            <asp:BoundField DataField="conductorNombre" HeaderText="Conductor" />

                            <asp:BoundField DataField="clienteNombre" HeaderText="Cliente" />

                            <asp:BoundField DataField="tractoPlaca" HeaderText="Tracto"
                                ItemStyle-Font-Names="Consolas,Monaco,monospace" ItemStyle-Font-Bold="true" />

                            <asp:BoundField DataField="carretaPlaca" HeaderText="Carreta"
                                ItemStyle-Font-Names="Consolas,Monaco,monospace" ItemStyle-Font-Bold="true" />

                            <asp:BoundField DataField="lugarOperacion" HeaderText="Lugar" />

                            <asp:BoundField DataField="tipoOperacion" HeaderText="Operación" />

                            <asp:BoundField DataField="fechaCreacion" HeaderText="Creado"
                                DataFormatString="{0:dd/MM/yyyy HH:mm}" ItemStyle-Font-Size="0.85em" ItemStyle-CssClass="text-muted" />

                            <asp:TemplateField HeaderText="Acciones" ItemStyle-Width="150px" ItemStyle-CssClass="text-center">
                                <ItemTemplate>
                                    <asp:LinkButton ID="btnVer" runat="server" CssClass="btn-action btn-view"
                                        CommandName="Ver" CommandArgument='<%# Eval("idDespacho") %>'
                                        ToolTip="Ver detalles">
                                        <i class="fas fa-eye"></i>
                                    </asp:LinkButton>

                                    <asp:LinkButton ID="btnEditar" runat="server" CssClass="btn-action btn-edit"
                                        CommandName="Editar" CommandArgument='<%# Eval("idDespacho") %>'
                                        Visible='<%# Eval("estadoDespacho").ToString() == "PROGRAMADO" %>'
                                        ToolTip="Editar">
                                        <i class="fas fa-edit"></i>
                                    </asp:LinkButton>

                                    <asp:LinkButton ID="btnCrearOrdenViaje" runat="server"
                                        CssClass="btn-action btn-orden"
                                        CommandName="CrearOrdenViaje"
                                        CommandArgument='<%# Eval("idDespacho") %>'
                                        Visible='<%# Eval("estadoDespacho").ToString() == "PROGRAMADO" %>'
                                        ToolTip="Crear Orden de Viaje">
                                        <i class="fas fa-route"></i>
                                    </asp:LinkButton>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>

                        <EmptyDataTemplate>
                            <div class="no-data">
                                <i class="fas fa-search"></i>
                                <h5>No hay despachos</h5>
                                <p>No se encontraron despachos con los criterios de búsqueda especificados.</p>
                            </div>
                        </EmptyDataTemplate>

                        <PagerTemplate>
                            <div class="pagination-container">
                                <asp:LinkButton ID="lnkPrevious" runat="server" CssClass="btn btn-outline-secondary btn-sm me-3"
                                    CommandName="Page" CommandArgument="Prev"
                                    Visible='<%# ((GridView)Container.NamingContainer).PageIndex > 0 %>'>
                                    <i class="fas fa-chevron-left"></i> Anterior
                                </asp:LinkButton>

                                <span class="mx-4 align-self-center text-muted fs-6">Página <%# ((GridView)Container.NamingContainer).PageIndex + 1 %> de 
                                    <%# ((GridView)Container.NamingContainer).PageCount %>
                                </span>

                                <asp:LinkButton ID="lnkNext" runat="server" CssClass="btn btn-outline-secondary btn-sm ms-3"
                                    CommandName="Page" CommandArgument="Next"
                                    Visible='<%# ((GridView)Container.NamingContainer).PageIndex < ((GridView)Container.NamingContainer).PageCount - 1 %>'>
                                    Siguiente <i class="fas fa-chevron-right"></i>
                                </asp:LinkButton>
                            </div>
                        </PagerTemplate>

                    </asp:GridView>
                </div>

            </div>

            <!-- Panel de Mensajes -->
            <asp:Panel ID="pnlMensaje" runat="server" Visible="false" CssClass="mt-4">
                <div class="alert alert-dismissible fade show" role="alert" id="divMensaje" runat="server"
                    style="border-radius: 12px; box-shadow: 0 6px 25px rgba(0,0,0,0.1);">
                    <asp:Literal ID="litMensaje" runat="server"></asp:Literal>
                    <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
                </div>
            </asp:Panel>

        </div>
    </div>

    <!-- Modal para Ver Detalles -->
    <div class="modal fade" id="modalDetalles" tabindex="-1" aria-hidden="true">
        <div class="modal-dialog modal-lg">
            <div class="modal-content" style="border-radius: 16px; border: none; box-shadow: 0 15px 50px rgba(0,0,0,0.15);">
                <div class="modal-header" style="background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%); color: white; border-radius: 16px 16px 0 0;">
                    <h5 class="modal-title">
                        <i class="fas fa-info-circle me-2"></i>
                        Detalles del Despacho
                    </h5>
                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
                </div>
                <div class="modal-body" style="padding: 32px;">
                    <asp:Panel ID="pnlDetallesDespacho" runat="server">
                        <div class="row">
                            <div class="col-md-6 mb-3">
                                <label class="filter-label">Estado</label>
                                <div>
                                    <asp:Literal ID="litEstadoDespacho" runat="server"></asp:Literal>
                                </div>
                            </div>
                            <div class="col-md-6 mb-3">
                                <label class="filter-label">Fecha</label>
                                <div>
                                    <asp:Literal ID="litFechaDespacho" runat="server"></asp:Literal>
                                </div>
                            </div>
                        </div>
                        <div class="row">
                            <div class="col-md-6 mb-3">
                                <label class="filter-label">Lugar</label>
                                <div>
                                    <asp:Literal ID="litLugarDetalle" runat="server"></asp:Literal>
                                </div>
                            </div>
                            <div class="col-md-6 mb-3">
                                <label class="filter-label">Operación</label>
                                <div>
                                    <asp:Literal ID="litOperacionDetalle" runat="server"></asp:Literal>
                                </div>
                            </div>
                        </div>
                        <div class="row">
                            <div class="col-md-12 mb-3">
                                <label class="filter-label">Conductor</label>
                                <div>
                                    <asp:Literal ID="litConductorDetalle" runat="server"></asp:Literal>
                                </div>
                            </div>
                        </div>
                        <div class="row">
                            <div class="col-md-4 mb-3">
                                <label class="filter-label">Cliente</label>
                                <div>
                                    <asp:Literal ID="litClienteDetalle" runat="server"></asp:Literal>
                                </div>
                            </div>
                            <div class="col-md-4 mb-3">
                                <label class="filter-label">Tracto</label>
                                <div>
                                    <asp:Literal ID="litTractoDetalle" runat="server"></asp:Literal>
                                </div>
                            </div>
                            <div class="col-md-4 mb-3">
                                <label class="filter-label">Carreta</label>
                                <div>
                                    <asp:Literal ID="litCarretaDetalle" runat="server"></asp:Literal>
                                </div>
                            </div>
                        </div>
                    </asp:Panel>
                </div>
                <div class="modal-footer" style="border-top: 1px solid #e2e8f0; padding: 20px 32px;">
                    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                        <i class="fas fa-times me-2"></i>Cerrar
                    </button>
                </div>
            </div>
        </div>
    </div>

    <script>
        // Toggle para filtros adicionales
        function toggleAdditionalFilters() {
            const content = document.getElementById('additionalFiltersContent');
            const icon = document.getElementById('toggleIcon');

            if (content && icon) {
                content.classList.toggle('show');
                icon.classList.toggle('rotated');
            }
        }

        // Modal de detalles
        function showDetallesModal() {
            if (typeof bootstrap !== 'undefined') {
                const modal = new bootstrap.Modal(document.getElementById('modalDetalles'));
                modal.show();
            }
        }

        // Auto-hide alerts
        document.addEventListener('DOMContentLoaded', function () {
            setTimeout(function () {
                const alerts = document.querySelectorAll('.alert');
                alerts.forEach(function (alert) {
                    alert.style.transition = 'opacity 0.5s';
                    alert.style.opacity = '0';
                    setTimeout(function () {
                        alert.style.display = 'none';
                    }, 500);
                });
            }, 6000);
        });
    </script>
</asp:Content>
<%@ Page Title="Catálogo del Cliente" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="CatalogoCliente.aspx.cs" Inherits="WebSGV.Views.CatalogoCliente" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdItem" runat="server" />

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-3">

        <div class="row mb-4">
            <div class="col-12">
                <div class="d-flex justify-content-between align-items-center border-bottom pb-3">
                    <div>
                        <h2 class="mb-1" style="color:#1e293b;font-weight:600;">
                            <i class="fas fa-boxes mr-2" style="color:#2563eb;"></i>Cat&#xE1;logo del Cliente
                        </h2>
                        <p class="text-muted mb-0">Productos, plantas y rutas propias de cada cliente</p>
                    </div>
                    <span class="badge badge-primary px-3 py-2" style="font-size:0.9rem;">
                        <asp:Label ID="lblResumen" runat="server" Text="Seleccione un cliente"></asp:Label>
                    </span>
                </div>
            </div>
        </div>

        <!-- Selector de cliente -->
        <div class="row mb-4">
            <div class="col-12 col-md-6 col-lg-5">
                <div class="card shadow-sm">
                    <div class="card-body py-3">
                        <label class="font-weight-bold mb-2">Cliente:</label>
                        <asp:DropDownList ID="ddlCliente" runat="server" CssClass="form-control"
                            AutoPostBack="true" OnSelectedIndexChanged="ddlCliente_SelectedIndexChanged">
                        </asp:DropDownList>
                        <small class="form-text text-muted">
                            Cada cliente tiene sus propios productos, plantas y rutas.
                        </small>
                    </div>
                </div>
            </div>
        </div>

        <asp:Panel ID="pnlCatalogo" runat="server" Visible="false">

            <!-- Pestañas de catálogo -->
            <ul class="nav nav-tabs mb-0" role="tablist">
                <li class="nav-item">
                    <asp:LinkButton ID="lnkProductos" runat="server" CssClass="nav-link"
                        CommandArgument="Producto" OnCommand="CambiarTab_Command" CausesValidation="false">
                        <i class="fas fa-box mr-1"></i>Productos
                        <span class="badge badge-light ml-1"><asp:Literal ID="litCntProductos" runat="server" Text="0" /></span>
                    </asp:LinkButton>
                </li>
                <li class="nav-item">
                    <asp:LinkButton ID="lnkPlantasCarga" runat="server" CssClass="nav-link"
                        CommandArgument="PlantaCarga" OnCommand="CambiarTab_Command" CausesValidation="false">
                        <i class="fas fa-truck-loading mr-1"></i>Plantas de carga
                        <span class="badge badge-light ml-1"><asp:Literal ID="litCntCarga" runat="server" Text="0" /></span>
                    </asp:LinkButton>
                </li>
                <li class="nav-item">
                    <asp:LinkButton ID="lnkPlantasDescarga" runat="server" CssClass="nav-link"
                        CommandArgument="PlantaDescarga" OnCommand="CambiarTab_Command" CausesValidation="false">
                        <i class="fas fa-dolly mr-1"></i>Plantas de descarga
                        <span class="badge badge-light ml-1"><asp:Literal ID="litCntDescarga" runat="server" Text="0" /></span>
                    </asp:LinkButton>
                </li>
                <li class="nav-item">
                    <asp:LinkButton ID="lnkRutas" runat="server" CssClass="nav-link"
                        CommandArgument="Ruta" OnCommand="CambiarTab_Command" CausesValidation="false">
                        <i class="fas fa-route mr-1"></i>Rutas
                        <span class="badge badge-light ml-1"><asp:Literal ID="litCntRutas" runat="server" Text="0" /></span>
                    </asp:LinkButton>
                </li>
            </ul>

            <div class="card shadow-sm" style="border-top-left-radius:0;border-top-right-radius:0;">
                <div class="card-body">
                    <div class="row">

                        <!-- Alta -->
                        <div class="col-12 col-lg-4 mb-4">
                            <div class="border rounded p-3 h-100" style="background:#f8fafc;">
                                <h6 class="font-weight-bold mb-3">
                                    <i class="fas fa-plus-circle mr-1" style="color:#2563eb;"></i>
                                    Agregar <asp:Literal ID="litEtiquetaAlta" runat="server" Text="elemento" />
                                </h6>
                                <asp:ValidationSummary ID="vsAlta" runat="server" ValidationGroup="vgAlta"
                                    CssClass="text-danger small mb-3" DisplayMode="BulletList" />
                                <div class="form-group">
                                    <label class="font-weight-bold">Nombre <span class="text-danger">*</span></label>
                                    <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control" MaxLength="200"></asp:TextBox>
                                    <asp:RequiredFieldValidator ID="rfvNombre" runat="server" ValidationGroup="vgAlta"
                                        ControlToValidate="txtNombre" ErrorMessage="El nombre es obligatorio."
                                        CssClass="text-danger small" Display="Dynamic" />
                                </div>
                                <div class="form-group">
                                    <label class="font-weight-bold"><asp:Literal ID="litEtiquetaDetalle" runat="server" Text="Descripción" />:</label>
                                    <asp:TextBox ID="txtDetalle" runat="server" CssClass="form-control" MaxLength="300"
                                        TextMode="MultiLine" Rows="2"></asp:TextBox>
                                </div>
                                <asp:Button ID="btnAgregar" runat="server" CssClass="btn btn-primary btn-block"
                                    Text="Agregar" ValidationGroup="vgAlta" OnClick="btnAgregar_Click" />
                            </div>
                        </div>

                        <!-- Listado -->
                        <div class="col-12 col-lg-8">
                            <div class="table-responsive">
                                <asp:GridView ID="gvCatalogo" runat="server"
                                    CssClass="table table-hover mb-0"
                                    AutoGenerateColumns="false"
                                    OnRowCommand="gvCatalogo_RowCommand">
                                    <Columns>
                                        <asp:TemplateField HeaderText="NOMBRE">
                                            <ItemTemplate>
                                                <span class="font-weight-bold"><%# Eval("nombre") %></span>
                                                <asp:Literal ID="litGeneral" runat="server"
                                                    Text='<%# Convert.ToInt32(Eval("esGeneral")) == 1 ? " <span class=\"badge badge-secondary ml-1\">General</span>" : "" %>' />
                                            </ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:TemplateField HeaderText="DETALLE">
                                            <ItemTemplate>
                                                <small class="text-muted"><%# Eval("detalle") %></small>
                                            </ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:TemplateField HeaderText="ESTADO" ItemStyle-CssClass="text-center" ItemStyle-Width="90">
                                            <ItemTemplate>
                                                <span class='badge <%# ObtenerClaseEstado(Eval("activo")) %>'>
                                                    <%# ObtenerTextoEstado(Eval("activo")) %>
                                                </span>
                                            </ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:TemplateField HeaderText="" ItemStyle-CssClass="text-center" ItemStyle-Width="40">
                                            <ItemTemplate>
                                                <button type="button" class="btn btn-outline-info btn-sm btn-editar" title="Editar"
                                                    data-id='<%# Eval("idItem") %>'
                                                    data-nombre='<%# AttrEncode(Eval("nombre")) %>'
                                                    data-detalle='<%# AttrEncode(Eval("detalle")) %>'>
                                                    <i class="fas fa-edit"></i>
                                                </button>
                                            </ItemTemplate>
                                        </asp:TemplateField>
                                        <asp:TemplateField HeaderText="ACCI&#xD3;N" ItemStyle-CssClass="text-center" ItemStyle-Width="100">
                                            <ItemTemplate>
                                                <asp:LinkButton ID="lbToggle" runat="server"
                                                    CommandName="ToggleActivo"
                                                    CommandArgument='<%# Eval("idItem") %>'
                                                    CssClass='<%# ObtenerClaseBoton(Eval("activo")) %>'
                                                    OnClientClick="return confirm('&#xBF;Cambiar el estado de este elemento?');">
                                                    <%# ObtenerTextoBoton(Eval("activo")) %>
                                                </asp:LinkButton>
                                            </ItemTemplate>
                                        </asp:TemplateField>
                                    </Columns>
                                    <EmptyDataTemplate>
                                        <div class="text-center text-muted py-4">
                                            <i class="fas fa-inbox fa-2x mb-2 d-block"></i>
                                            Este cliente todav&#xED;a no tiene nada cargado en esta secci&#xF3;n.
                                        </div>
                                    </EmptyDataTemplate>
                                </asp:GridView>
                            </div>
                        </div>

                    </div>
                </div>
            </div>
        </asp:Panel>

    </div>

    <!-- Modal Editar -->
    <div class="modal fade" id="modalEditar" tabindex="-1" role="dialog">
        <div class="modal-dialog" role="document">
            <div class="modal-content">
                <div class="modal-header" style="background:#0ea5e9;color:#fff;">
                    <h5 class="modal-title"><i class="fas fa-edit mr-2"></i>Editar</h5>
                    <button type="button" class="close" style="color:#fff;" data-dismiss="modal">&times;</button>
                </div>
                <div class="modal-body">
                    <div class="form-group">
                        <label class="font-weight-bold">Nombre <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtEditarNombre" runat="server" CssClass="form-control" MaxLength="200"></asp:TextBox>
                    </div>
                    <div class="form-group mb-0">
                        <label class="font-weight-bold"><asp:Literal ID="litEtiquetaDetalleEditar" runat="server" Text="Descripción" />:</label>
                        <asp:TextBox ID="txtEditarDetalle" runat="server" CssClass="form-control" MaxLength="300"
                            TextMode="MultiLine" Rows="2"></asp:TextBox>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnActualizar" runat="server" CssClass="btn btn-info"
                        Text="Guardar Cambios" OnClick="btnActualizar_Click" />
                </div>
            </div>
        </div>
    </div>

    <script>
        document.addEventListener('click', function (e) {
            var btn = e.target.closest('.btn-editar');
            if (!btn) return;
            document.getElementById('<%= hfIdItem.ClientID %>').value = btn.dataset.id;
            document.getElementById('<%= txtEditarNombre.ClientID %>').value = btn.dataset.nombre || '';
            document.getElementById('<%= txtEditarDetalle.ClientID %>').value = btn.dataset.detalle || '';
            $('#modalEditar').modal('show');
        });

        document.addEventListener('DOMContentLoaded', function () {
            var hf = document.getElementById('<%= hfIdItem.ClientID %>');
            var msgPanel = document.getElementById('<%= pnlMensaje.ClientID %>');
            if (hf && hf.value > 0 && msgPanel && msgPanel.querySelector('.alert-danger'))
                $('#modalEditar').modal('show');
        });
    </script>

</asp:Content>

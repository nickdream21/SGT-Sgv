<%@ Page Title="Registro de Semiremolques" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroSemiremolques.aspx.cs" Inherits="WebSGV.Views.RegistroSemiremolques" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdCarreta" runat="server" />

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-3">

        <div class="row mb-4">
            <div class="col-12">
                <div class="d-flex justify-content-between align-items-center border-bottom pb-3">
                    <div>
                        <h2 class="mb-1" style="color:#1e293b;font-weight:600;">
                            <i class="fas fa-trailer mr-2" style="color:#2563eb;"></i>Registro de Semiremolques
                        </h2>
                        <p class="text-muted mb-0">Administra los semiremolques disponibles para las operaciones</p>
                    </div>
                    <span class="badge badge-primary px-3 py-2" style="font-size:0.9rem;">
                        <asp:Label ID="lblTotalSemiremolques" runat="server" Text="0 registro(s)"></asp:Label>
                    </span>
                </div>
            </div>
        </div>

        <div class="row">

            <div class="col-12 col-md-4 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-primary text-white">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Agregar Semiremolque</h5>
                    </div>
                    <div class="card-body">
                        <asp:ValidationSummary ID="vsRegistroSemi" runat="server" ValidationGroup="vgRegistroSemi" CssClass="text-danger small mb-3" DisplayMode="BulletList" />
                        <div class="form-group">
                            <label class="font-weight-bold">N&#xBA; de Placa <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtPlaca" runat="server" CssClass="form-control"
                                placeholder="Ej: ABC-123" MaxLength="20"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvPlaca" runat="server" ControlToValidate="txtPlaca" ValidationGroup="vgRegistroSemi"
                                ErrorMessage="Placa: este campo es obligatorio." CssClass="text-danger small" Display="Dynamic" />
                            <asp:RegularExpressionValidator ID="revPlaca" runat="server" ControlToValidate="txtPlaca" ValidationGroup="vgRegistroSemi"
                                ValidationExpression="^[A-Za-z0-9\-]{6,10}$" ErrorMessage="Placa: use 6 a 10 caracteres (letras, números o guion)." CssClass="text-danger small" Display="Dynamic" />
                        </div>
                        <div class="form-group">
                            <label class="font-weight-bold">Marca <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtMarca" runat="server" CssClass="form-control"
                                placeholder="Ej: RANDON" MaxLength="100"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvMarca" runat="server" ControlToValidate="txtMarca" ValidationGroup="vgRegistroSemi"
                                ErrorMessage="Marca: este campo es obligatorio." CssClass="text-danger small" Display="Dynamic" />
                            <asp:RegularExpressionValidator ID="revMarca" runat="server" ControlToValidate="txtMarca" ValidationGroup="vgRegistroSemi"
                                ValidationExpression="^[A-Za-zÁÉÍÓÚÑáéíóú0-9\s\-\.]{2,100}$" ErrorMessage="Marca: use entre 2 y 100 caracteres válidos." CssClass="text-danger small" Display="Dynamic" />
                        </div>
                        <div class="form-group">
                            <label class="font-weight-bold">Modelo <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtModelo" runat="server" CssClass="form-control"
                                placeholder="Ej: SR GR 3E" MaxLength="100"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvModelo" runat="server" ControlToValidate="txtModelo" ValidationGroup="vgRegistroSemi"
                                ErrorMessage="Modelo: este campo es obligatorio." CssClass="text-danger small" Display="Dynamic" />
                            <asp:RegularExpressionValidator ID="revModelo" runat="server" ControlToValidate="txtModelo" ValidationGroup="vgRegistroSemi"
                                ValidationExpression="^[A-Za-zÁÉÍÓÚÑáéíóú0-9\s\-\.]{2,100}$" ErrorMessage="Modelo: use entre 2 y 100 caracteres válidos." CssClass="text-danger small" Display="Dynamic" />
                        </div>
                        <asp:Button ID="btnRegistrar" runat="server" Text="Registrar Semiremolque" ValidationGroup="vgRegistroSemi"
                            CssClass="btn btn-primary btn-block" OnClick="btnRegistrar_Click" />
                    </div>
                </div>
                <div class="alert alert-info mt-3">
                    <i class="fas fa-lightbulb mr-2"></i>
                    <strong>Informaci&#xF3;n:</strong> Los semiremolques <strong>activos</strong> aparecer&#xE1;n en los formularios de operaci&#xF3;n.
                    Los inactivos quedan ocultos pero sus datos hist&#xF3;ricos se conservan.
                </div>
            </div>

            <div class="col-12 col-md-8 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light d-flex justify-content-between align-items-center">
                        <h5 class="mb-0"><i class="fas fa-list mr-2"></i>Semiremolques Registrados</h5>
                        <input type="text" id="txtBuscar" class="form-control form-control-sm ml-3"
                            style="max-width:200px;" placeholder="Buscar..." autocomplete="off" spellcheck="false" autocapitalize="off" autocorrect="off" name="filtro_semiremolques"
                            oninput="filtrarTabla(this.value,'contenedorSemi')">
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive" id="contenedorSemi">
                            <asp:GridView ID="gvSemiremolques" runat="server"
                                CssClass="table table-hover mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No hay semiremolques registrados."
                                OnRowCommand="gvSemiremolques_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="idCarreta" HeaderText="ID"
                                        ItemStyle-CssClass="text-center text-muted" ItemStyle-Width="55" />
                                    <asp:BoundField DataField="placaCarreta" HeaderText="PLACA" ItemStyle-CssClass="font-weight-bold" />
                                    <asp:BoundField DataField="marca" HeaderText="MARCA" />
                                    <asp:BoundField DataField="modelo" HeaderText="MODELO" />
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
                                                data-id='<%# Eval("idCarreta") %>'
                                                data-placa='<%# AttrEncode(Eval("placaCarreta")) %>'
                                                data-marca='<%# AttrEncode(Eval("marca")) %>'
                                                data-modelo='<%# AttrEncode(Eval("modelo")) %>'>
                                                <i class="fas fa-edit"></i>
                                            </button>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="ACCION" ItemStyle-CssClass="text-center" ItemStyle-Width="100">
                                        <ItemTemplate>
                                            <asp:LinkButton ID="lbToggle" runat="server"
                                                CommandName="ToggleActivo"
                                                CommandArgument='<%# Eval("idCarreta") %>'
                                                CssClass='<%# ObtenerClaseBoton(Eval("activo")) %>'
                                                OnClientClick="return confirm('&#xBF;Cambiar el estado de este semiremolque?');">
                                                <%# ObtenerTextoBoton(Eval("activo")) %>
                                            </asp:LinkButton>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                </Columns>
                            </asp:GridView>
                        </div>
                    </div>
                </div>
            </div>

        </div>
    </div>

    <!-- Modal Editar Semiremolque -->
    <div class="modal fade" id="modalEditar" tabindex="-1" role="dialog">
        <div class="modal-dialog" role="document">
            <div class="modal-content">
                <div class="modal-header" style="background:#0ea5e9;color:#fff;">
                    <h5 class="modal-title"><i class="fas fa-trailer mr-2"></i>Editar Semiremolque</h5>
                    <button type="button" class="close" style="color:#fff;" data-dismiss="modal">&times;</button>
                </div>
                <div class="modal-body">
                    <div class="form-group">
                        <label class="font-weight-bold">N&#xBA; de Placa <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtEditarPlaca" runat="server" CssClass="form-control text-uppercase" MaxLength="20"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label class="font-weight-bold">Marca <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtEditarMarca" runat="server" CssClass="form-control text-uppercase" MaxLength="100"></asp:TextBox>
                    </div>
                    <div class="form-group mb-0">
                        <label class="font-weight-bold">Modelo <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtEditarModelo" runat="server" CssClass="form-control text-uppercase" MaxLength="100"></asp:TextBox>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnActualizarSemiremolque" runat="server" CssClass="btn btn-info"
                        Text="Guardar Cambios" OnClick="btnActualizarSemiremolque_Click" />
                </div>
            </div>
        </div>
    </div>

    <script>
        document.addEventListener('click', function (e) {
            var btn = e.target.closest('.btn-editar');
            if (!btn) return;
            document.getElementById('<%= hfIdCarreta.ClientID %>').value = btn.dataset.id;
            document.getElementById('<%= txtEditarPlaca.ClientID %>').value = btn.dataset.placa;
            document.getElementById('<%= txtEditarMarca.ClientID %>').value = btn.dataset.marca;
            document.getElementById('<%= txtEditarModelo.ClientID %>').value = btn.dataset.modelo;
            $('#modalEditar').modal('show');
        });

        document.addEventListener('DOMContentLoaded', function () {
            var hf = document.getElementById('<%= hfIdCarreta.ClientID %>');
            var msgPanel = document.getElementById('<%= pnlMensaje.ClientID %>');
            if (hf && hf.value > 0 && msgPanel && msgPanel.querySelector('.alert-danger'))
                $('#modalEditar').modal('show');
            if (msgPanel && msgPanel.querySelector('.alert-success')) {
                var ok = msgPanel.querySelector('.alert-success');
                setTimeout(function () {
                    ok.style.transition = 'opacity .5s'; ok.style.opacity = '0';
                    setTimeout(function () { msgPanel.style.display = 'none'; }, 500);
                }, 4000);
            }
        });

        function filtrarTabla(valor, id) {
            var filas = document.querySelectorAll('#' + id + ' table tr');
            valor = valor.toLowerCase();
            filas.forEach(function (f, i) { if (i > 0) f.style.display = f.textContent.toLowerCase().includes(valor) ? '' : 'none'; });
        }
    </script>

</asp:Content>

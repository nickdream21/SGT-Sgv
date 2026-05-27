<%@ Page Title="Registro de Plantas" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroPlantas.aspx.cs" Inherits="WebSGV.Views.RegistroPlantas" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdPlanta" runat="server" />

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-3">

        <div class="row mb-4">
            <div class="col-12">
                <div class="d-flex justify-content-between align-items-center border-bottom pb-3">
                    <div>
                        <h2 class="mb-1" style="color:#1e293b;font-weight:600;">
                            <i class="fas fa-industry mr-2" style="color:#2563eb;"></i>Registro de Plantas
                        </h2>
                        <p class="text-muted mb-0">Administra las plantas de operaci&#xF3;n disponibles para los despachos</p>
                    </div>
                    <span class="badge badge-primary px-3 py-2" style="font-size:0.9rem;">
                        <asp:Label ID="lblTotalPlantas" runat="server" Text="0 registro(s)"></asp:Label>
                    </span>
                </div>
            </div>
        </div>

        <div class="row">

            <div class="col-12 col-md-4 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-primary text-white">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Agregar Planta</h5>
                    </div>
                    <div class="card-body">
                        <asp:ValidationSummary ID="vsRegistroPlanta" runat="server" ValidationGroup="vgRegistroPlanta" CssClass="text-danger small mb-3" DisplayMode="BulletList" />
                        <div class="form-group">
                            <label class="font-weight-bold">Nombre de la Planta <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control"
                                placeholder="Ej: PLANTA TRUJILLO" MaxLength="200"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvNombre" runat="server" ValidationGroup="vgRegistroPlanta"
                                ControlToValidate="txtNombre"
                                ErrorMessage="El nombre de la planta es requerido."
                                CssClass="text-danger small" Display="Dynamic" />
                            <asp:RegularExpressionValidator ID="revNombrePlanta" runat="server" ValidationGroup="vgRegistroPlanta"
                                ControlToValidate="txtNombre" ValidationExpression="^[A-Za-zÁÉÍÓÚÑáéíóú0-9\s\-\.,]{3,200}$" ErrorMessage="Nombre de planta: use entre 3 y 200 caracteres válidos." CssClass="text-danger small" Display="Dynamic" />
                            <small class="form-text text-muted">
                                <i class="fas fa-info-circle mr-1"></i>El nombre se guardar&#xE1; en may&#xFA;sculas.
                            </small>
                        </div>
                        <div class="form-group">
                            <label class="font-weight-bold">&#xC1;mbito <span class="text-danger">*</span></label>
                            <asp:DropDownList ID="ddlAmbito" runat="server" CssClass="form-control">
                                <asp:ListItem Value="0" Text="Nacional (Per&#xFA;)"></asp:ListItem>
                                <asp:ListItem Value="1" Text="Internacional"></asp:ListItem>
                            </asp:DropDownList>
                            <small class="form-text text-muted">
                                Define si la planta se muestra en operaciones nacionales o internacionales.
                            </small>
                        </div>
                        <asp:Button ID="btnRegistrar" runat="server" Text="Registrar Planta" ValidationGroup="vgRegistroPlanta"
                            CssClass="btn btn-primary btn-block" OnClick="btnRegistrar_Click" />
                    </div>
                </div>
                <div class="alert alert-info mt-3">
                    <i class="fas fa-lightbulb mr-2"></i>
                    <strong>Informaci&#xF3;n:</strong> Las plantas <strong>activas</strong> aparecer&#xE1;n en el selector de Planta de Operaci&#xF3;n al registrar despachos.
                    Las inactivas quedan ocultas pero sus datos hist&#xF3;ricos se conservan.
                </div>
            </div>

            <div class="col-12 col-md-8 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light d-flex justify-content-between align-items-center">
                        <h5 class="mb-0"><i class="fas fa-list mr-2"></i>Plantas Registradas</h5>
                        <input type="text" id="txtBuscar" class="form-control form-control-sm ml-3"
                            style="max-width:200px;" placeholder="Buscar..." autocomplete="off" spellcheck="false" autocapitalize="off" autocorrect="off" name="filtro_plantas"
                            oninput="filtrarTabla(this.value,'contenedorPlantas')">
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive" id="contenedorPlantas">
                            <asp:GridView ID="gvPlantas" runat="server"
                                CssClass="table table-hover mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No hay plantas registradas."
                                OnRowCommand="gvPlantas_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="idPlanta" HeaderText="ID"
                                        ItemStyle-CssClass="text-center text-muted" ItemStyle-Width="55" />
                                    <asp:BoundField DataField="nombre" HeaderText="NOMBRE DE LA PLANTA" />
                                    <asp:TemplateField HeaderText="&#xC1;MBITO" ItemStyle-CssClass="text-center" ItemStyle-Width="120">
                                        <ItemTemplate>
                                            <span class='badge <%# ObtenerClaseAmbito(Eval("esInternacional")) %>'>
                                                <%# ObtenerTextoAmbito(Eval("esInternacional")) %>
                                            </span>
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
                                                data-id='<%# Eval("idPlanta") %>'
                                                data-nombre='<%# AttrEncode(Eval("nombre")) %>'
                                                data-internacional='<%# Eval("esInternacional") %>'>
                                                <i class="fas fa-edit"></i>
                                            </button>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="ACCI&#xD3;N" ItemStyle-CssClass="text-center" ItemStyle-Width="100">
                                        <ItemTemplate>
                                            <asp:LinkButton ID="lbToggle" runat="server"
                                                CommandName="ToggleActivo"
                                                CommandArgument='<%# Eval("idPlanta") %>'
                                                CssClass='<%# ObtenerClaseBoton(Eval("activo")) %>'
                                                OnClientClick="return confirm('&#xBF;Cambiar el estado de esta planta?');">
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

    <!-- Modal Editar Planta -->
    <div class="modal fade" id="modalEditar" tabindex="-1" role="dialog">
        <div class="modal-dialog" role="document">
            <div class="modal-content">
                <div class="modal-header" style="background:#0ea5e9;color:#fff;">
                    <h5 class="modal-title"><i class="fas fa-industry mr-2"></i>Editar Planta</h5>
                    <button type="button" class="close" style="color:#fff;" data-dismiss="modal">&times;</button>
                </div>
                <div class="modal-body">
                    <div class="form-group">
                        <label class="font-weight-bold">Nombre de la Planta <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtEditarNombre" runat="server" CssClass="form-control text-uppercase" MaxLength="200"></asp:TextBox>
                    </div>
                    <div class="form-group mb-0">
                        <label class="font-weight-bold">&#xC1;mbito <span class="text-danger">*</span></label>
                        <asp:DropDownList ID="ddlEditarAmbito" runat="server" CssClass="form-control">
                            <asp:ListItem Value="0" Text="Nacional (Per&#xFA;)"></asp:ListItem>
                            <asp:ListItem Value="1" Text="Internacional"></asp:ListItem>
                        </asp:DropDownList>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnActualizarPlanta" runat="server" CssClass="btn btn-info"
                        Text="Guardar Cambios" OnClick="btnActualizarPlanta_Click" />
                </div>
            </div>
        </div>
    </div>

    <script>
        document.addEventListener('click', function (e) {
            var btn = e.target.closest('.btn-editar');
            if (!btn) return;
            document.getElementById('<%= hfIdPlanta.ClientID %>').value = btn.dataset.id;
            document.getElementById('<%= txtEditarNombre.ClientID %>').value = btn.dataset.nombre;
            var ddl = document.getElementById('<%= ddlEditarAmbito.ClientID %>');
            ddl.value = (btn.dataset.internacional === 'True' || btn.dataset.internacional === 'true') ? '1' : '0';
            $('#modalEditar').modal('show');
        });

        document.addEventListener('DOMContentLoaded', function () {
            var hf = document.getElementById('<%= hfIdPlanta.ClientID %>');
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

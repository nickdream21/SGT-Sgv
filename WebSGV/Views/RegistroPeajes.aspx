<%@ Page Title="Registro de Peajes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroPeajes.aspx.cs" Inherits="WebSGV.Views.RegistroPeajes" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdEstacion" runat="server" />

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-3">

        <div class="row mb-4">
            <div class="col-12">
                <div class="d-flex justify-content-between align-items-center border-bottom pb-3">
                    <div>
                        <h2 class="mb-1" style="color:#1e293b;font-weight:600;">
                            <i class="fas fa-road mr-2" style="color:#2563eb;"></i>Registro de Estaciones de Peaje
                        </h2>
                        <p class="text-muted mb-0">Administra las estaciones de peaje disponibles para las liquidaciones</p>
                    </div>
                    <span class="badge badge-primary px-3 py-2" style="font-size:0.9rem;">
                        <asp:Label ID="lblTotalPeajes" runat="server" Text="0 registro(s)"></asp:Label>
                    </span>
                </div>
            </div>
        </div>

        <div class="row">

            <div class="col-12 col-md-4 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-primary text-white">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Agregar Estaci&#xF3;n</h5>
                    </div>
                    <div class="card-body">
                        <div class="form-group">
                            <label class="font-weight-bold">Nombre de la Estaci&#xF3;n <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control"
                                placeholder="Ej: ESTACION DE PEAJE CHICAMA" MaxLength="200"></asp:TextBox>
                            <small class="form-text text-muted">
                                <i class="fas fa-info-circle mr-1"></i>El nombre se guardar&#xE1; en may&#xFA;sculas.
                            </small>
                        </div>
                        <asp:Button ID="btnRegistrar" runat="server" Text="Registrar Peaje"
                            CssClass="btn btn-primary btn-block" OnClick="btnRegistrar_Click" />
                    </div>
                </div>
                <div class="alert alert-info mt-3">
                    <i class="fas fa-lightbulb mr-2"></i>
                    <strong>Informaci&#xF3;n:</strong> Las estaciones <strong>activas</strong> aparecer&#xE1;n en el selector de peajes al registrar liquidaciones.
                    Las inactivas quedan ocultas pero sus datos hist&#xF3;ricos se conservan.
                </div>
            </div>

            <div class="col-12 col-md-8 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light d-flex justify-content-between align-items-center">
                        <h5 class="mb-0"><i class="fas fa-list mr-2"></i>Estaciones Registradas</h5>
                        <input type="text" id="txtBuscar" class="form-control form-control-sm ml-3"
                            style="max-width:200px;" placeholder="Buscar..."
                            oninput="filtrarTabla(this.value,'contenedorPeajes')">
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive" id="contenedorPeajes">
                            <asp:GridView ID="gvPeajes" runat="server"
                                CssClass="table table-hover mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No hay estaciones de peaje registradas."
                                OnRowCommand="gvPeajes_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="idEstacion" HeaderText="ID"
                                        ItemStyle-CssClass="text-center text-muted" ItemStyle-Width="55" />
                                    <asp:BoundField DataField="nombre" HeaderText="NOMBRE DE LA ESTACI&#xD3;N" />
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
                                                data-id='<%# Eval("idEstacion") %>'
                                                data-nombre='<%# AttrEncode(Eval("nombre")) %>'>
                                                <i class="fas fa-edit"></i>
                                            </button>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="ACCI&#xD3;N" ItemStyle-CssClass="text-center" ItemStyle-Width="100">
                                        <ItemTemplate>
                                            <asp:LinkButton ID="lbToggle" runat="server"
                                                CommandName="ToggleActivo"
                                                CommandArgument='<%# Eval("idEstacion") %>'
                                                CssClass='<%# ObtenerClaseBoton(Eval("activo")) %>'
                                                OnClientClick="return confirm('&#xBF;Cambiar el estado de este peaje?');">
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

    <!-- Modal Editar Peaje -->
    <div class="modal fade" id="modalEditar" tabindex="-1" role="dialog">
        <div class="modal-dialog" role="document">
            <div class="modal-content">
                <div class="modal-header" style="background:#0ea5e9;color:#fff;">
                    <h5 class="modal-title"><i class="fas fa-road mr-2"></i>Editar Estaci&#xF3;n de Peaje</h5>
                    <button type="button" class="close" style="color:#fff;" data-dismiss="modal">&times;</button>
                </div>
                <div class="modal-body">
                    <div class="form-group mb-0">
                        <label class="font-weight-bold">Nombre de la Estaci&#xF3;n <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtEditarNombre" runat="server" CssClass="form-control text-uppercase" MaxLength="200"></asp:TextBox>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnActualizarPeaje" runat="server" CssClass="btn btn-info"
                        Text="Guardar Cambios" OnClick="btnActualizarPeaje_Click" />
                </div>
            </div>
        </div>
    </div>

    <script>
        document.addEventListener('click', function (e) {
            var btn = e.target.closest('.btn-editar');
            if (!btn) return;
            document.getElementById('<%= hfIdEstacion.ClientID %>').value = btn.dataset.id;
            document.getElementById('<%= txtEditarNombre.ClientID %>').value = btn.dataset.nombre;
            $('#modalEditar').modal('show');
        });

        document.addEventListener('DOMContentLoaded', function () {
            var hf = document.getElementById('<%= hfIdEstacion.ClientID %>');
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

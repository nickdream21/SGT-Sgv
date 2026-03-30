<%@ Page Title="Asignaciones de Maquinaria" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AsignacionesMaquinaria.aspx.cs" Inherits="WebSGV.Views.AsignacionesMaquinariaPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-3">

        <!-- Encabezado -->
        <div class="row mb-4">
            <div class="col-12">
                <div class="d-flex justify-content-between align-items-center border-bottom pb-3">
                    <div>
                        <h2 class="mb-1" style="color:#1e293b;font-weight:600;">
                            <i class="fas fa-project-diagram mr-2" style="color:#2d6a4f;"></i>Asignaciones de Maquinaria
                        </h2>
                        <p class="text-muted mb-0">Asigna operadores con equipos a obras espec&#xED;ficas</p>
                    </div>
                    <span class="badge px-3 py-2" style="font-size:0.9rem;background-color:#2d6a4f;color:white;">
                        <asp:Label ID="lblTotalAsignaciones" runat="server" Text="0 asignaci&#xF3;n(es)"></asp:Label>
                    </span>
                </div>
            </div>
        </div>

        <div class="row">

            <!-- Panel Nueva Asignacion -->
            <div class="col-12 col-md-5 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header text-white" style="background-color:#2d6a4f;">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Nueva Asignaci&#xF3;n</h5>
                    </div>
                    <div class="card-body">
                        <asp:Panel ID="pnlFormulario" runat="server">
                            <div class="form-group">
                                <label class="font-weight-bold">Operador <span class="text-danger">*</span></label>
                                <asp:DropDownList ID="ddlOperador" runat="server" CssClass="form-control">
                                </asp:DropDownList>
                                <asp:RequiredFieldValidator ID="rfvOperador" runat="server" ControlToValidate="ddlOperador"
                                    InitialValue="" ErrorMessage="Seleccione un operador" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Equipo / M&#xE1;quina <span class="text-danger">*</span></label>
                                <asp:DropDownList ID="ddlEquipo" runat="server" CssClass="form-control">
                                </asp:DropDownList>
                                <asp:RequiredFieldValidator ID="rfvEquipo" runat="server" ControlToValidate="ddlEquipo"
                                    InitialValue="" ErrorMessage="Seleccione un equipo" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Obra <span class="text-danger">*</span></label>
                                <asp:DropDownList ID="ddlObra" runat="server" CssClass="form-control">
                                </asp:DropDownList>
                                <asp:RequiredFieldValidator ID="rfvObra" runat="server" ControlToValidate="ddlObra"
                                    InitialValue="" ErrorMessage="Seleccione una obra" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Fecha Asignaci&#xF3;n <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtFechaAsignacion" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvFecha" runat="server" ControlToValidate="txtFechaAsignacion"
                                    ErrorMessage="La fecha es requerida" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Observaciones</label>
                                <asp:TextBox ID="txtObservaciones" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3"
                                    MaxLength="500" placeholder="Observaciones adicionales..."></asp:TextBox>
                            </div>
                            <asp:Button ID="btnAsignar" runat="server" CssClass="btn btn-block text-white"
                                style="background-color:#2d6a4f;" Text="Crear Asignaci&#xF3;n" OnClick="btnAsignar_Click" />
                        </asp:Panel>
                    </div>
                </div>
                <div class="alert alert-info mt-3">
                    <i class="fas fa-info-circle mr-2"></i>
                    <strong>Nota:</strong> Solo se muestran operadores, equipos y obras <strong>activos</strong>.
                    Un operador puede tener solo una asignaci&#xF3;n activa a la vez.
                </div>
            </div>

            <!-- Lista de Asignaciones -->
            <div class="col-12 col-md-7 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light">
                        <div class="d-flex justify-content-between align-items-center">
                            <h5 class="mb-0"><i class="fas fa-list mr-2"></i>Asignaciones</h5>
                            <div>
                                <asp:DropDownList ID="ddlFiltroEstado" runat="server" CssClass="form-control form-control-sm d-inline-block" 
                                    style="width:auto;" AutoPostBack="true" OnSelectedIndexChanged="ddlFiltroEstado_SelectedIndexChanged">
                                    <asp:ListItem Value="ACTIVA" Text="Activas"></asp:ListItem>
                                    <asp:ListItem Value="TODAS" Text="Todas"></asp:ListItem>
                                    <asp:ListItem Value="FINALIZADA" Text="Finalizadas"></asp:ListItem>
                                    <asp:ListItem Value="CANCELADA" Text="Canceladas"></asp:ListItem>
                                </asp:DropDownList>
                            </div>
                        </div>
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive">
                            <asp:GridView ID="gvAsignaciones" runat="server"
                                CssClass="table table-hover mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No hay asignaciones registradas."
                                OnRowCommand="gvAsignaciones_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="operadorNombre" HeaderText="OPERADOR" />
                                    <asp:BoundField DataField="equipoPlaca" HeaderText="EQUIPO" />
                                    <asp:BoundField DataField="obraNombre" HeaderText="OBRA" />
                                    <asp:BoundField DataField="clienteNombre" HeaderText="CLIENTE" />
                                    <asp:BoundField DataField="fechaAsignacion" HeaderText="FECHA" DataFormatString="{0:dd/MM/yyyy}" ItemStyle-CssClass="text-center" />
                                    <asp:TemplateField HeaderText="ESTADO" ItemStyle-CssClass="text-center" ItemStyle-Width="110">
                                        <ItemTemplate>
                                            <span class='badge <%# ObtenerClaseEstado(Eval("estado")) %>'>
                                                <%# Eval("estado") %>
                                            </span>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="ACCI&#xD3;N" ItemStyle-CssClass="text-center" ItemStyle-Width="120">
                                        <ItemTemplate>
                                            <asp:LinkButton ID="lbFinalizar" runat="server"
                                                CommandName="FinalizarAsignacion"
                                                CommandArgument='<%# Eval("idAsignacion") %>'
                                                CssClass="btn btn-outline-danger btn-sm"
                                                OnClientClick="return confirm('&#xBF;Finalizar esta asignaci&#xF3;n?');"
                                                Visible='<%# Eval("estado").ToString() == "ACTIVA" %>'>
                                                <i class="fas fa-stop-circle mr-1"></i>Finalizar
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

</asp:Content>

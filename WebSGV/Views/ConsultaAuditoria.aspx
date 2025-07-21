<%@ Page Title="Consulta de Auditoría" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ConsultaAuditoria.aspx.cs" Inherits="WebSGV.Views.ConsultaAuditoria" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="container-fluid">
        <h2 class="mt-4 mb-4"><i class="fas fa-search-plus me-2"></i>Consulta de Registros de Auditoría</h2>
        
        <!-- Panel de filtros -->
        <div class="card mb-4">
            <div class="card-header bg-primary text-white">
                <h5 class="mb-0"><i class="fas fa-filter me-2"></i>Filtros de Búsqueda</h5>
            </div>
            <div class="card-body">
                <div class="row">
                    <div class="col-md-3">
                        <div class="form-group mb-3">
                            <label for="ddlTabla">Tabla Afectada:</label>
                            <asp:DropDownList ID="ddlTabla" runat="server" CssClass="form-control">
                                <asp:ListItem Text="Todas" Value="" Selected="True"></asp:ListItem>
                                <asp:ListItem Text="Conductor" Value="Conductor"></asp:ListItem>
                                <asp:ListItem Text="Tracto" Value="Tracto"></asp:ListItem>
                                <asp:ListItem Text="Carreta" Value="Carreta"></asp:ListItem>
                                <asp:ListItem Text="AbastecimientoCombustible" Value="AbastecimientoCombustible"></asp:ListItem>
                                <asp:ListItem Text="CPIC" Value="CPIC"></asp:ListItem>
                                <asp:ListItem Text="OrdenViaje" Value="OrdenViaje"></asp:ListItem>
                                <asp:ListItem Text="Factura" Value="Factura"></asp:ListItem>
                                <asp:ListItem Text="Indicadores" Value="Indicadores"></asp:ListItem>
                            </asp:DropDownList>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="form-group mb-3">
                            <label for="ddlOperacion">Tipo de Operación:</label>
                            <asp:DropDownList ID="ddlOperacion" runat="server" CssClass="form-control">
                                <asp:ListItem Text="Todas" Value="" Selected="True"></asp:ListItem>
                                <asp:ListItem Text="INSERT" Value="INSERT"></asp:ListItem>
                                <asp:ListItem Text="UPDATE" Value="UPDATE"></asp:ListItem>
                                <asp:ListItem Text="DELETE" Value="DELETE"></asp:ListItem>
                            </asp:DropDownList>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="form-group mb-3">
                            <label for="txtFechaDesde">Fecha Desde:</label>
                            <asp:TextBox ID="txtFechaDesde" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="form-group mb-3">
                            <label for="txtFechaHasta">Fecha Hasta:</label>
                            <asp:TextBox ID="txtFechaHasta" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                        </div>
                    </div>
                </div>
                <div class="row">
                    <div class="col-md-3">
                        <div class="form-group mb-3">
                            <label for="txtUsuario">Usuario:</label>
                            <asp:TextBox ID="txtUsuario" runat="server" CssClass="form-control" placeholder="Nombre de usuario"></asp:TextBox>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="form-group mb-3">
                            <label for="txtIdRegistro">ID Registro:</label>
                            <asp:TextBox ID="txtIdRegistro" runat="server" CssClass="form-control" placeholder="ID del registro"></asp:TextBox>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="form-group mb-3">
                            <label for="txtCampo">Campo:</label>
                            <asp:TextBox ID="txtCampo" runat="server" CssClass="form-control" placeholder="Nombre del campo"></asp:TextBox>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="form-group mb-3">
                            <label for="txtValor">Valor:</label>
                            <asp:TextBox ID="txtValor" runat="server" CssClass="form-control" placeholder="Valor anterior o nuevo"></asp:TextBox>
                        </div>
                    </div>
                </div>
                <div class="row mt-3">
                    <div class="col-md-12 text-center">
                        <asp:Button ID="btnBuscar" runat="server" CssClass="btn btn-primary" Text="Buscar" OnClick="btnBuscar_Click" />
                        <asp:Button ID="btnLimpiar" runat="server" CssClass="btn btn-secondary ms-2" Text="Limpiar Filtros" OnClick="btnLimpiar_Click" />
                        <asp:Button ID="btnExportar" runat="server" CssClass="btn btn-success ms-2" Text="Exportar a Excel" OnClick="btnExportar_Click" />
                    </div>
                </div>
            </div>
        </div>
        
        <!-- Resultados -->
        <div class="card">
            <div class="card-header bg-primary text-white">
                <h5 class="mb-0"><i class="fas fa-clipboard-list me-2"></i>Resultados de Auditoría</h5>
            </div>
            <div class="card-body">
                <div class="table-responsive">
                    <asp:GridView ID="gvAuditoria" runat="server" CssClass="table table-striped table-bordered table-hover" 
                        AutoGenerateColumns="false" AllowPaging="true" PageSize="20" OnPageIndexChanging="gvAuditoria_PageIndexChanging"
                        EmptyDataText="No se encontraron registros de auditoría con los criterios especificados." OnRowCommand="gvAuditoria_RowCommand">
                        <Columns>
                            <asp:BoundField DataField="idAuditoria" HeaderText="ID" />
                            <asp:BoundField DataField="TablaAfectada" HeaderText="Tabla" />
                            <asp:BoundField DataField="TipoOperacion" HeaderText="Operación" />
                            <asp:BoundField DataField="IdRegistro" HeaderText="ID Registro" />
                            <asp:BoundField DataField="Campo" HeaderText="Campo" />
                            <asp:BoundField DataField="ValorAnterior" HeaderText="Valor Anterior" ItemStyle-CssClass="text-wrap" />
                            <asp:BoundField DataField="ValorNuevo" HeaderText="Valor Nuevo" ItemStyle-CssClass="text-wrap" />
                            <asp:BoundField DataField="Usuario" HeaderText="Usuario" />
                            <asp:BoundField DataField="FechaHora" HeaderText="Fecha y Hora" DataFormatString="{0:dd/MM/yyyy HH:mm:ss}" />
                            <asp:TemplateField HeaderText="Detalles">
                                <ItemTemplate>
                                    <asp:LinkButton ID="btnVerDetalles" runat="server" CssClass="btn btn-sm btn-info" 
                                        CommandName="VerDetalles" CommandArgument='<%# Eval("idAuditoria") %>'
                                        data-bs-toggle="tooltip" data-bs-placement="top" title="Ver detalles completos">
                                        <i class="fas fa-search-plus"></i>
                                    </asp:LinkButton>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                        <PagerStyle CssClass="pagination-ys" />
                    </asp:GridView>
                </div>
                
                <!-- Paginación con números -->
                <div class="pagination-container">
                    <asp:Repeater ID="rptPager" runat="server" OnItemCommand="rptPager_ItemCommand">
                        <ItemTemplate>
                            <asp:LinkButton ID="lnkPage" runat="server" Text='<%#Eval("Text") %>' CommandArgument='<%#Eval("Value") %>'
                                Enabled='<%#Eval("Enabled") %>' CssClass='<%# Convert.ToBoolean(Eval("Active")) ? "active-page" : "page-item" %>'></asp:LinkButton>
                        </ItemTemplate>
                    </asp:Repeater>
                </div>
                
                <!-- Totales y estadísticas -->
                <div class="row mt-3">
                    <div class="col-md-6">
                        <div class="alert alert-info">
                            <asp:Literal ID="litEstadisticas" runat="server"></asp:Literal>
                        </div>
                    </div>
                </div>
            </div>
        </div>
        
        <!-- Modal para detalles -->
        <div class="modal fade" id="detallesModal" tabindex="-1" aria-labelledby="detallesModalLabel" aria-hidden="true">
            <div class="modal-dialog modal-lg">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title" id="detallesModalLabel">Detalles de Auditoría</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                    </div>
                    <div class="modal-body">
                        <asp:Panel ID="pnlDetalles" runat="server">
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="mb-3">
                                        <label class="form-label fw-bold">ID Auditoría:</label>
                                        <div><asp:Literal ID="litIdAuditoria" runat="server"></asp:Literal></div>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="mb-3">
                                        <label class="form-label fw-bold">Fecha y Hora:</label>
                                        <div><asp:Literal ID="litFechaHora" runat="server"></asp:Literal></div>
                                    </div>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="mb-3">
                                        <label class="form-label fw-bold">Tabla Afectada:</label>
                                        <div><asp:Literal ID="litTabla" runat="server"></asp:Literal></div>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="mb-3">
                                        <label class="form-label fw-bold">Tipo de Operación:</label>
                                        <div><asp:Literal ID="litOperacion" runat="server"></asp:Literal></div>
                                    </div>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="mb-3">
                                        <label class="form-label fw-bold">ID del Registro:</label>
                                        <div><asp:Literal ID="litIdRegistro" runat="server"></asp:Literal></div>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="mb-3">
                                        <label class="form-label fw-bold">Campo:</label>
                                        <div><asp:Literal ID="litCampo" runat="server"></asp:Literal></div>
                                    </div>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="mb-3">
                                        <label class="form-label fw-bold">Usuario:</label>
                                        <div><asp:Literal ID="litUsuario" runat="server"></asp:Literal></div>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="mb-3">
                                        <label class="form-label fw-bold">Estación:</label>
                                        <div><asp:Literal ID="litEstacion" runat="server"></asp:Literal></div>
                                    </div>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-12">
                                    <div class="mb-3">
                                        <label class="form-label fw-bold">Dirección IP:</label>
                                        <div><asp:Literal ID="litIP" runat="server"></asp:Literal></div>
                                    </div>
                                </div>
                            </div>
                            <hr />
                            <div class="row">
                                <div class="col-md-12">
                                    <label class="form-label fw-bold">Valor Anterior:</label>
                                    <div class="p-2 bg-light border rounded">
                                        <asp:Literal ID="litValorAnterior" runat="server"></asp:Literal>
                                    </div>
                                </div>
                            </div>
                            <div class="row mt-3">
                                <div class="col-md-12">
                                    <label class="form-label fw-bold">Valor Nuevo:</label>
                                    <div class="p-2 bg-light border rounded">
                                        <asp:Literal ID="litValorNuevo" runat="server"></asp:Literal>
                                    </div>
                                </div>
                            </div>
                        </asp:Panel>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cerrar</button>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <script type="text/javascript">
        // Inicializar tooltips de Bootstrap
        document.addEventListener('DOMContentLoaded', function() {
            var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
            var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
                return new bootstrap.Tooltip(tooltipTriggerEl);
            });
        });
        
        // Función para mostrar modal de detalles
        function showDetallesModal() {
            var myModal = new bootstrap.Modal(document.getElementById('detallesModal'));
            myModal.show();
        }
    </script>
</asp:Content>
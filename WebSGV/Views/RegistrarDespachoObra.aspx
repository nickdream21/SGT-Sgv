<%@ Page Title="Registrar Despacho a Obra" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistrarDespachoObra.aspx.cs" Inherits="WebSGV.Views.RegistrarDespachoObra" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/RegistrarDespachoObra.css") %>" rel="stylesheet" />

    <div class="do-container">
        <div class="do-header">
            <h3 style="margin:0;"><i class="fas fa-truck-moving mr-2"></i>Registrar Despacho de Combustible a Obra</h3>
            <small class="text-muted">Cisterna con combustible para maquinaria en obra</small>
        </div>

        <!-- Datos del despacho -->
        <div class="do-section">
            <h5 style="color:#e65100; margin-bottom:12px;"><i class="fas fa-info-circle mr-1"></i>Datos del Despacho</h5>
            <div class="do-grid">
                <div>
                    <div class="do-label">Cisterna (Tracto)</div>
                    <asp:DropDownList ID="ddlTracto" runat="server" CssClass="form-control"></asp:DropDownList>
                </div>
                <div>
                    <div class="do-label">Conductor</div>
                    <asp:DropDownList ID="ddlConductor" runat="server" CssClass="form-control"></asp:DropDownList>
                </div>
                <div>
                    <div class="do-label">Obra</div>
                    <asp:DropDownList ID="ddlObra" runat="server" CssClass="form-control"></asp:DropDownList>
                </div>
                <div>
                    <div class="do-label">Fecha / Hora Salida Grifo</div>
                    <asp:TextBox ID="txtFechaSalida" runat="server" CssClass="form-control" TextMode="DateTimeLocal"></asp:TextBox>
                </div>
                <div>
                    <div class="do-label">Fecha / Hora Llegada Obra (opcional)</div>
                    <asp:TextBox ID="txtFechaLlegada" runat="server" CssClass="form-control" TextMode="DateTimeLocal"></asp:TextBox>
                </div>
                <div>
                    <div class="do-label">Fecha / Hora Retorno al Grifo (opcional)</div>
                    <asp:TextBox ID="txtFechaRetorno" runat="server" CssClass="form-control" TextMode="DateTimeLocal"></asp:TextBox>
                </div>
            </div>
        </div>

        <!-- Medicion de combustible -->
        <div class="do-section">
            <h5 style="color:#e65100; margin-bottom:12px;"><i class="fas fa-gas-pump mr-1"></i>Medición de Combustible</h5>
            <div class="do-grid">
                <div>
                    <div class="do-label">Galones de Salida (llenado cisterna)</div>
                    <asp:TextBox ID="txtGalonesSalida" runat="server" CssClass="form-control"
                        TextMode="Number" step="0.01" onchange="recalcularTotales()" oninput="recalcularTotales()"></asp:TextBox>
                </div>
                <div>
                    <div class="do-label">Galones de Retorno (al volver)</div>
                    <asp:TextBox ID="txtGalonesRetorno" runat="server" CssClass="form-control"
                        TextMode="Number" step="0.01" onchange="recalcularTotales()" oninput="recalcularTotales()"></asp:TextBox>
                </div>
            </div>
            <div class="totales-box" style="margin-top:12px;">
                <i class="fas fa-calculator mr-1"></i>
                Combustible abastecido en obra: <strong id="lblAbastecido">0.00</strong> GL
                <small class="text-muted d-block mt-1">
                    (salida − retorno = combustible entregado a las máquinas)
                </small>
            </div>
            <div style="margin-top:12px;">
                <div class="do-label">Observaciones</div>
                <asp:TextBox ID="txtObservaciones" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="2"></asp:TextBox>
            </div>
        </div>

        <div style="text-align:right; margin-top:18px;">
            <asp:Button ID="btnCancelar" runat="server" Text="Cancelar" CssClass="btn btn-outline-secondary mr-2"
                OnClick="btnCancelar_Click" CausesValidation="false" />
            <asp:Button ID="btnGuardar" runat="server" Text="Registrar Despacho" CssClass="btn btn-warning"
                OnClick="btnGuardar_Click" />
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            txtGalonesSalida: '<%= txtGalonesSalida.ClientID %>',
            txtGalonesRetorno: '<%= txtGalonesRetorno.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistrarDespachoObra.js") %>"></script>
</asp:Content>

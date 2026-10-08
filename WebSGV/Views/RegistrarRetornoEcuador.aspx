<%@ Page Title="Registrar Retorno Ecuador" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistrarRetornoEcuador.aspx.cs" Inherits="WebSGV.Views.RegistrarRetornoEcuador" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/RegistrarRetornoEcuador.css") %>" rel="stylesheet" />

    <div class="re-wrap">
        <h3><i class="fas fa-globe-americas"></i> Registrar Retorno Ecuador</h3>
        <div class="sub">Ingreso informativo de combustible comprado en Ecuador y recibido en el grifo.</div>

        <div class="re-info">
            <i class="fas fa-info-circle"></i>
            Este registro <strong>no es un abastecimiento al vehículo</strong>: son los galones que llegaron físicamente al grifo en los tanques del tracto y quedan como stock hasta la próxima programación.
        </div>

        <asp:Panel ID="pnlMensaje" runat="server" Visible="false" CssClass="alert alert-success" />
        <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="alert alert-danger" />

        <div class="re-banner">
            <div><div class="label">Viaje</div><div class="val"><asp:Literal ID="litNumeroViaje" runat="server" Text="-" /></div></div>
            <div><div class="label">Conductor</div><div class="val"><asp:Literal ID="litConductor" runat="server" Text="-" /></div></div>
            <div><div class="label">Tracto</div><div class="val"><asp:Literal ID="litTracto" runat="server" Text="-" /></div></div>
            <div><div class="label">Fecha Recepción</div><div class="val"><asp:TextBox ID="txtFecha" runat="server" TextMode="DateTimeLocal" CssClass="form-control" /></div></div>
        </div>

        <div class="tickets-head">
            <div>Nº Ticket</div>
            <div>Proveedor</div>
            <div>Galones</div>
            <div>Precio USD</div>
            <div></div>
        </div>
        <div id="ticketsContainer">
            <!-- filas dinamicas -->
        </div>
        <button type="button" class="btn-add" onclick="agregarTicket()">
            <i class="fas fa-plus"></i> Agregar ticket
        </button>

        <div class="tot-box">
            <span>Total galones: <strong id="totalGal">0.00</strong> GL</span>
            <span>Total USD: <strong>$<span id="totalUsd">0.00</span></strong></span>
        </div>

        <div style="margin-top:16px;">
            <label style="font-size:.85rem; color:#555;">Observaciones</label>
            <asp:TextBox ID="txtObservaciones" runat="server" TextMode="MultiLine" Rows="2" CssClass="form-control" />
        </div>

        <asp:HiddenField ID="hfIdViaje" runat="server" />
        <asp:HiddenField ID="hfIdConductor" runat="server" />
        <asp:HiddenField ID="hfIdTracto" runat="server" />
        <asp:HiddenField ID="hfTicketsJson" runat="server" />

        <div class="re-actions">
            <a href="DashboardGrifo.aspx" class="btn-cancel" style="text-decoration:none; display:inline-block;">Cancelar</a>
            <asp:Button ID="btnGuardar" runat="server" Text="Guardar Retorno" CssClass="btn-save"
                OnClientClick="return prepararEnvio();" OnClick="btnGuardar_Click" />
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hfTicketsJson: '<%= hfTicketsJson.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistrarRetornoEcuador.js") %>"></script>
</asp:Content>

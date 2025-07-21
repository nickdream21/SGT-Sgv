<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="WebForm2.aspx.cs" Inherits="WebSGV.Views.WebForm2" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div style="max-width: 800px; margin: auto; padding: 20px; background-color: #f9f9f9; border-radius: 10px; box-shadow: 0px 0px 10px rgba(0, 0, 0, 0.1);">
        <h2 style="text-align: center; color: #333;">Registro de Datos</h2>
        <table style="width: 100%;">
            <tr><td><strong>PEDIDO:</strong></td><td><asp:TextBox ID="txtPedido" runat="server" CssClass="form-control"></asp:TextBox></td></tr>
            <tr><td><strong>Conductor Origen:</strong></td><td><asp:TextBox ID="txtConductorOrigen" runat="server" CssClass="form-control"></asp:TextBox></td></tr>
            <tr><td><strong>Tracto 1:</strong></td><td><asp:TextBox ID="txtTracto1" runat="server" CssClass="form-control"></asp:TextBox></td></tr>
            <tr><td><strong>Carreta:</strong></td><td><asp:TextBox ID="txtCarreta" runat="server" CssClass="form-control"></asp:TextBox></td></tr>
            <tr><td><strong>Conductor Destino:</strong></td><td><asp:TextBox ID="txtConductorDestino" runat="server" CssClass="form-control"></asp:TextBox></td></tr>
            <tr><td><strong>Tracto 2:</strong></td><td><asp:TextBox ID="txtTracto2" runat="server" CssClass="form-control"></asp:TextBox></td></tr>
            <tr><td><strong>F.H.S.Base:</strong></td><td><asp:TextBox ID="txtFHSBase" runat="server" TextMode="DateTimeLocal" CssClass="form-control"></asp:TextBox></td></tr>
            <tr><td><strong>F.H.LL. Trujillo:</strong></td><td><asp:TextBox ID="txtFHLLTrujillo" runat="server" TextMode="DateTimeLocal" CssClass="form-control"></asp:TextBox></td></tr>
            <tr><td><strong>F.H. Registro:</strong></td><td><asp:TextBox ID="txtFHRegistro" runat="server" TextMode="DateTimeLocal" CssClass="form-control"></asp:TextBox></td></tr>
            <tr><td><strong>F.H. PROGRAMACION:</strong></td><td><asp:TextBox ID="txtFHProgramacion" runat="server" TextMode="DateTimeLocal" CssClass="form-control"></asp:TextBox></td></tr>
            <tr><td><strong>KPI-Depsa:</strong></td><td><asp:TextBox ID="txtKPIDepsa" runat="server" CssClass="form-control"></asp:TextBox></td></tr>
            <tr><td><strong>KPI-TCI:</strong></td><td><asp:TextBox ID="txtKPITCI" runat="server" CssClass="form-control"></asp:TextBox></td></tr>
            <tr><td><strong>Motivo de Retraso:</strong></td><td><asp:TextBox ID="txtMotivoRetraso" runat="server" CssClass="form-control"></asp:TextBox></td></tr>
            <tr><td><strong>Motivo de Retraso / Comentario:</strong></td><td><asp:TextBox ID="txtMotivoComentario" runat="server" CssClass="form-control"></asp:TextBox></td></tr>
            <tr><td colspan="2" style="text-align: center; padding-top: 10px;"><asp:Button ID="btnGuardar" runat="server" Text="Guardar" CssClass="btn btn-primary" OnClick="btnGuardar_Click" /></td></tr>
        </table>
    </div>
</asp:Content>

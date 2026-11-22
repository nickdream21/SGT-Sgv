<%@ Page Title="Código de verificación" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SegundoFactor.aspx.cs" Inherits="WebSGV.Views.SegundoFactor" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/SegundoFactor.css") %>" rel="stylesheet" />

    <div class="sf-contenedor">
        <h2 class="sf-titulo">Verificación en dos pasos</h2>

        <asp:Panel ID="pnlMensaje" runat="server" Visible="false" EnableViewState="false" CssClass="mb-3">
            <asp:Label ID="lblMensaje" runat="server" />
        </asp:Panel>

        <%-- Entrada con el factor activo: código de la app o de recuperación. Enter envía (data-enter). --%>
        <asp:Panel ID="pnlCodigo" runat="server" Visible="false">
            <p class="sf-subtitulo">Abra la app autenticadora de su celular y escriba el código de 6 dígitos de <strong>SGV</strong>.</p>
            <div data-enter="<%= btnVerificar.ClientID %>">
                <div class="form-group">
                    <label for="<%= txtCodigo.ClientID %>">Código</label>
                    <asp:TextBox ID="txtCodigo" runat="server" CssClass="form-control sf-codigo" MaxLength="24"
                        autocomplete="one-time-code" inputmode="numeric" autocapitalize="characters" spellcheck="false" />
                    <small class="form-text text-muted">
                        ¿No tiene el celular? Escriba uno de sus códigos de recuperación (16 letras y números; cada uno sirve una sola vez).
                    </small>
                </div>
                <asp:Button ID="btnVerificar" runat="server" CssClass="btn btn-primary btn-block" Text="Verificar" OnClick="btnVerificar_Click" />
            </div>
        </asp:Panel>

        <%-- Alta obligatoria (interruptor Seguridad.ExigirSegundoFactor): sin ella no se entra. --%>
        <asp:Panel ID="pnlAlta" runat="server" Visible="false">
            <p class="sf-subtitulo">Su cuenta debe usar un código del celular para entrar. Configúrelo ahora:</p>
            <ol class="sf-pasos">
                <li>Instale en su celular una app autenticadora (Google Authenticator, Microsoft Authenticator u otra).</li>
                <li>En la app, agregue una cuenta escaneando este código QR, o escriba la clave a mano.</li>
                <li>Escriba el código de 6 dígitos que muestra la app.</li>
            </ol>
            <div class="sf-qr text-center">
                <asp:Literal ID="litQr" runat="server" Mode="PassThrough" EnableViewState="false" />
            </div>
            <p class="text-center mb-1"><small class="text-muted">Clave para escribir a mano:</small></p>
            <p class="text-center sf-secreto"><asp:Label ID="lblSecreto" runat="server" EnableViewState="false" /></p>
            <p class="text-center"><asp:HyperLink ID="lnkOtpAuth" runat="server" Text="Abrir en la app de este celular" EnableViewState="false" /></p>
            <div data-enter="<%= btnConfirmarAlta.ClientID %>">
                <div class="form-group">
                    <label for="<%= txtCodigoAlta.ClientID %>">Código de la app</label>
                    <asp:TextBox ID="txtCodigoAlta" runat="server" CssClass="form-control sf-codigo" MaxLength="7"
                        autocomplete="one-time-code" inputmode="numeric" />
                </div>
                <asp:Button ID="btnConfirmarAlta" runat="server" CssClass="btn btn-primary btn-block" Text="Activar y entrar" OnClick="btnConfirmarAlta_Click" />
            </div>
        </asp:Panel>

        <asp:Button ID="btnCancelar" runat="server" CssClass="btn btn-link btn-block mt-2" Text="Cancelar y volver al inicio de sesión"
            OnClick="btnCancelar_Click" CausesValidation="false" />
    </div>

    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/SegundoFactor.js") %>"></script>
</asp:Content>

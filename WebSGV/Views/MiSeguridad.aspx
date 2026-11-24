<%@ Page Title="Mi seguridad" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MiSeguridad.aspx.cs" Inherits="WebSGV.Views.MiSeguridad" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/MiSeguridad.css") %>" rel="stylesheet" />

    <div class="container ms-contenedor">
        <div class="sgv-encabezado">
            <div class="sgv-encabezado-titulo">
                <h1 class="h2">Mi seguridad</h1>
                <p>Verificación en dos pasos: además de la contraseña, al entrar se pide un código de 6 dígitos que genera una app de su celular.</p>
            </div>
        </div>

        <asp:Panel ID="pnlMensaje" runat="server" Visible="false" EnableViewState="false" CssClass="mb-3">
            <asp:Label ID="lblMensaje" runat="server" />
        </asp:Panel>

        <%-- Códigos de recuperación recién creados: se muestran una sola vez. --%>
        <asp:Panel ID="pnlCodigosNuevos" runat="server" Visible="false" EnableViewState="false" CssClass="card border-warning mb-3">
            <div class="card-header bg-light"><strong>Sus códigos de recuperación</strong></div>
            <div class="card-body">
                <p class="mb-2">Guárdelos en un lugar seguro (impresos o anotados, fuera del celular). Cada uno sirve <strong>una sola vez</strong>
                    para entrar si no tiene el celular. <strong>Cuando pulse "Ya los guardé" no se volverán a mostrar.</strong></p>
                <ul id="listaCodigos" class="ms-codigos list-unstyled">
                    <asp:Repeater ID="rptCodigos" runat="server" EnableViewState="false">
                        <ItemTemplate><li><%#: Container.DataItem %></li></ItemTemplate>
                    </asp:Repeater>
                </ul>
                <button type="button" class="btn btn-outline-secondary btn-sm js-copiar-codigos">Copiar los códigos</button>
                <button type="button" class="btn btn-outline-secondary btn-sm js-imprimir-codigos">Imprimir</button>
                <asp:Button ID="btnCodigosGuardados" runat="server" CssClass="btn btn-primary btn-sm" Text="Ya los guardé"
                    OnClick="btnCodigosGuardados_Click" CausesValidation="false"
                    data-confirmar="¿Ya guardó los códigos en un lugar seguro?\nDespués no se volverán a mostrar."
                    data-confirmar-aceptar="Sí, ya los guardé" />
            </div>
        </asp:Panel>

        <div class="card mb-3">
            <div class="card-header"><strong>Estado</strong></div>
            <div class="card-body">
                <asp:Label ID="lblEstado" runat="server" />
            </div>
        </div>

        <%-- Alta o cambio de celular: QR + clave en texto + código para confirmar. --%>
        <asp:Panel ID="pnlAlta" runat="server" Visible="false" CssClass="card mb-3">
            <div class="card-header"><strong>Configurar la app autenticadora</strong></div>
            <div class="card-body">
                <ol class="ms-pasos">
                    <li>Instale en su celular una app autenticadora (Google Authenticator, Microsoft Authenticator u otra).</li>
                    <li>En la app, agregue una cuenta escaneando este código QR, o escriba la clave a mano.</li>
                    <li>Escriba el código de 6 dígitos que muestra la app.</li>
                </ol>
                <div class="ms-qr text-center">
                    <asp:Literal ID="litQr" runat="server" Mode="PassThrough" EnableViewState="false" />
                </div>
                <p class="text-center mb-1"><small class="text-muted">Clave para escribir a mano:</small></p>
                <p class="text-center ms-secreto"><asp:Label ID="lblSecreto" runat="server" EnableViewState="false" /></p>
                <p class="text-center"><asp:HyperLink ID="lnkOtpAuth" runat="server" Text="Abrir en la app de este celular" EnableViewState="false" /></p>
                <div data-enter="<%= btnConfirmarAlta.ClientID %>" class="ms-confirmar">
                    <div class="form-group">
                        <label for="<%= txtCodigoAlta.ClientID %>">Código de la app</label>
                        <asp:TextBox ID="txtCodigoAlta" runat="server" CssClass="form-control ms-codigo" MaxLength="7"
                            autocomplete="one-time-code" inputmode="numeric" />
                    </div>
                    <asp:Button ID="btnConfirmarAlta" runat="server" CssClass="btn btn-primary" Text="Confirmar" OnClick="btnConfirmarAlta_Click" />
                    <asp:Button ID="btnCancelarAlta" runat="server" CssClass="btn btn-secondary ml-1" Text="Cancelar" OnClick="btnCancelarAlta_Click" CausesValidation="false" />
                </div>
            </div>
        </asp:Panel>

        <%-- Acciones que piden la contraseña (Enter no envía: no hay data-enter). --%>
        <asp:Panel ID="pnlAcciones" runat="server" CssClass="card mb-3">
            <div class="card-header"><strong>Cambios</strong></div>
            <div class="card-body">
                <div class="form-group ms-contrasena">
                    <label for="<%= txtContrasena.ClientID %>">Su contraseña</label>
                    <asp:TextBox ID="txtContrasena" runat="server" CssClass="form-control" TextMode="Password" MaxLength="200"
                        autocomplete="current-password" />
                    <small class="form-text text-muted">Para cualquier cambio de esta página se pide su contraseña.</small>
                </div>
                <asp:Button ID="btnEmpezarAlta" runat="server" CssClass="btn btn-primary mb-1" Text="Activar la verificación en dos pasos"
                    OnClick="btnEmpezarAlta_Click" />
                <asp:Button ID="btnRegenerar" runat="server" CssClass="btn btn-outline-warning mb-1" Text="Generar códigos de recuperación nuevos"
                    OnClick="btnRegenerar_Click"
                    data-confirmar="Se crearán 10 códigos de recuperación nuevos y los anteriores dejarán de valer.\n¿Continuar?"
                    data-confirmar-titulo="Códigos de recuperación" data-confirmar-aceptar="Generar" />
            </div>
        </asp:Panel>
    </div>

    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/MiSeguridad.js") %>"></script>
</asp:Content>

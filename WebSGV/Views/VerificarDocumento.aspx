<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="VerificarDocumento.aspx.cs" Inherits="WebSGV.Views.VerificarDocumento" ResponseEncoding="utf-8" %>
<!DOCTYPE html>
<html lang="es">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <meta name="robots" content="noindex" />
    <title>Verificar documento · SGV</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@4.6.2/dist/css/bootstrap.min.css" rel="stylesheet" />
    <link href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/5.15.4/css/all.min.css" rel="stylesheet" />
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/VerificarDocumento.css") %>" rel="stylesheet" />
</head>
<body>
<form id="form1" runat="server">
    <header class="vd-header">
        <div class="vd-logo">SGV</div>
        <div>
            <h1>Verificación de documentos firmados</h1>
            <div class="vd-sub"><asp:Label ID="lblEmpresa" runat="server" /> · RUC <asp:Label ID="lblRuc" runat="server" /></div>
        </div>
    </header>

    <main class="vd-wrap">
        <section class="vd-card">
            <p class="mb-2">Ingrese el <strong>código de verificación</strong> que aparece en el pie de página del documento.</p>
            <asp:Panel runat="server" DefaultButton="btnVerificar" CssClass="vd-busqueda">
                <asp:TextBox ID="txtCodigo" runat="server" CssClass="form-control" MaxLength="20"
                    placeholder="XXXX-XXXX-XXXX" autocomplete="off" spellcheck="false" />
                <asp:Button ID="btnVerificar" runat="server" Text="Verificar" CssClass="btn btn-primary" OnClick="btnVerificar_Click" />
            </asp:Panel>
        </section>

        <asp:Panel ID="pnlNoEncontrado" runat="server" Visible="false" CssClass="vd-card vd-estado vd-estado-alerta">
            <i class="fas fa-exclamation-triangle mr-1"></i><asp:Label ID="lblNoEncontrado" runat="server" />
        </asp:Panel>

        <asp:Panel ID="pnlResultado" runat="server" Visible="false">
            <asp:Panel ID="pnlEstado" runat="server" CssClass="vd-estado vd-estado-ok">
                <i class="fas fa-shield-alt mr-1"></i><asp:Label ID="lblEstado" runat="server" />
            </asp:Panel>

            <section class="vd-card">
                <dl class="vd-datos">
                    <dt>Código</dt><dd><asp:Label ID="lblCodigo" runat="server" /></dd>
                    <dt>Documento</dt><dd><asp:Label ID="lblDocumento" runat="server" /></dd>
                    <dt>Firmado por</dt><dd><asp:Label ID="lblFirmante" runat="server" /></dd>
                    <dt>Fecha y hora</dt><dd><asp:Label ID="lblFecha" runat="server" /></dd>
                    <dt>Huella SHA-256</dt><dd class="vd-hash"><asp:Label ID="lblHash" runat="server" /></dd>
                </dl>
            </section>

            <section class="vd-card">
                <h2>Comprobar el archivo</h2>
                <p class="vd-ayuda">Seleccione el PDF que recibió. Su huella se calcula en este navegador (el archivo no se envía)
                   y se compara con la registrada: si coinciden, el archivo es exactamente el documento firmado.</p>
                <asp:HiddenField ID="hfHash" runat="server" ClientIDMode="Static" />
                <input type="file" id="archivoPdf" accept="application/pdf,.pdf" class="form-control-file" />
                <div id="resultadoArchivo" class="vd-estado mt-3" style="display:none;"></div>
            </section>

            <section class="vd-card">
                <h2>Firmas de la orden</h2>
                <table class="table table-sm vd-tabla mb-0">
                    <thead><tr><th>Etapa</th><th>Firmante</th><th>Fecha</th><th>Estado</th><th>Código</th></tr></thead>
                    <tbody>
                        <asp:Repeater ID="rptFirmas" runat="server">
                            <ItemTemplate>
                                <tr>
                                    <td><%#: Eval("Etapa") %></td>
                                    <td><%#: Eval("Firmante") %></td>
                                    <td class="text-nowrap"><%#: Eval("Fecha") %></td>
                                    <td><%#: Eval("Estado") %></td>
                                    <td class="text-nowrap"><%#: Eval("Codigo") %></td>
                                </tr>
                            </ItemTemplate>
                        </asp:Repeater>
                    </tbody>
                </table>
            </section>
        </asp:Panel>

        <p class="vd-legal"><%: WebSGV.Services.Liquidaciones.TextosFirma.BaseLegal %></p>
    </main>

    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/VerificarDocumento.js") %>"></script>
</form>
</body>
</html>

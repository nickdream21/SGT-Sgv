<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="FirmarLiquidacion.aspx.cs" Inherits="WebSGV.Views.FirmarLiquidacion" ResponseEncoding="utf-8" %>
<!DOCTYPE html>
<html lang="es"<%= WebSGV.Helpers.SesionRestauradaHelper.AtributosHtml(Session) %>>
<head runat="server">
    <meta charset="utf-8" />
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <meta name="theme-color" content="#f8f9fa" />
    <title>Firmar Liquidación · SGV</title>

    <%-- Mismas librerías y versiones que Site.Master (Bootstrap 4.6.2, Font Awesome 5.15.4). --%>
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/lib/bootstrap-4.6.2/bootstrap.min.css") %>" rel="stylesheet" />
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/lib/fontawesome-5.15.4/css/all.min.css") %>" rel="stylesheet" />

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/sgv-base.css") %>" rel="stylesheet" />
    <%-- Página restaurada con Atrás tras cerrar sesión (bfcache): se comprueba la sesión antes de mostrarla. --%>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/SesionRestaurada.js") %>"></script>
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/FirmarLiquidacion.css") %>" rel="stylesheet" />
</head>
<body>
<form id="form1" runat="server">
    <asp:HiddenField ID="hfIdOrdenViaje" runat="server" ClientIDMode="Static" />

    <header class="fl-header">
        <div class="logo">SGV</div>
        <div>
            <%-- Sin Site.Master (ni su modal): el "?" abre la ayuda en otra pestaña y la firma a medias no se pierde. --%>
            <div class="fl-titulo-fila">
                <h1>Orden de viaje</h1>
                <a class="sgv-ayuda" href="<%: ResolveUrl("~/Views/Ayuda.aspx?tema=conductor-liquidar-firmar") %>" target="_blank" rel="noopener"
                    data-ayuda="conductor-liquidar-firmar" aria-label="Ayuda de esta p&#xE1;gina">?</a>
            </div>
            <div class="sub">Firma del conductor - declaración jurada</div>
        </div>
        <div class="fl-format-chip">SGV-CDF-F-05 v01</div>
    </header>

    <main class="fl-wrap">

        <!-- Pantalla principal -->
        <div id="pantallaFirma">

            <div id="alertBox" class="fl-alert"></div>

            <!-- Resumen de la liquidación -->
            <section class="fl-card">
                <h2>Resumen de la liquidación</h2>
                <div class="fl-grid">
                    <div>
                        <div class="lbl">N° Orden</div>
                        <div class="val" id="vNumero">-</div>
                    </div>
                    <div>
                        <div class="lbl">Fecha</div>
                        <div class="val" id="vFechas">-</div>
                    </div>
                    <div>
                        <div class="lbl">Conductor</div>
                        <div class="val" id="vConductor">-</div>
                    </div>
                    <div>
                        <div class="lbl">Unidades</div>
                        <div class="val" id="vUnidades">-</div>
                    </div>
                </div>
                <div class="fl-monto">
                    <span class="label">Total de mis gastos</span>
                    <span class="valor" id="vGastos">S/ 0.00</span>
                </div>
                <div class="fl-monto" id="vGastosDolaresFila" style="display:none;">
                    <span class="label">Gastos en dólares</span>
                    <span class="valor" id="vGastosDolares">$ 0.00</span>
                </div>
                <p class="fl-nota">El dinero que te entregaron para el viaje lo registra la administración al revisar tu liquidación.</p>
            </section>

            <!-- Declaración jurada -->
            <div class="fl-declaracion">
                <strong>Declaración jurada:</strong>
                <%: WebSGV.Services.Liquidaciones.TextosFirma.Declaracion %>
            </div>

            <!-- Checkbox de consentimiento -->
            <label class="fl-check">
                <input type="checkbox" id="chkAcepto" />
                <label for="chkAcepto">He leído y acepto la declaración jurada arriba descrita</label>
            </label>

            <!-- Canvas de firma -->
            <div class="fl-canvas-label">
                Firma con el dedo o el mouse dentro del recuadro:
            </div>
            <div id="canvasBox" class="fl-canvas-box">
                <div class="fl-canvas-hint">
                    <span>Dibuja aquí tu firma</span>
                </div>
                <div class="fl-canvas-baseline"></div>
                <div class="fl-canvas-x">&times;</div>
                <canvas id="canvasFirma" role="img" aria-label="Área de firma: dibuje su firma con el dedo o el mouse"></canvas>
            </div>

            <div class="fl-canvas-actions">
                <button type="button" class="fl-btn" id="btnLimpiar">
                    Limpiar
                </button>
                <button type="button" class="fl-btn" id="btnDeshacer" disabled>
                    Deshacer
                </button>
            </div>

            <!-- Confirmación de identidad -->
            <div class="fl-clave">
                <label for="txtContrasena">Tu contraseña, para confirmar que eres tú quien firma</label>
                <input type="password" id="txtContrasena" class="form-control" autocomplete="current-password" maxlength="100" />
            </div>

            <!-- Botón enviar -->
            <button type="button" class="fl-submit" id="btnFirmar" disabled>
                Firmar y enviar
            </button>

            <p class="fl-legal">
                <%: WebSGV.Services.Liquidaciones.TextosFirma.DatosPersonales %>
                <%: WebSGV.Services.Liquidaciones.TextosFirma.BaseLegal %>
            </p>
        </div>

        <!-- Pantalla de éxito -->
        <div id="pantallaExito" class="fl-success-screen">
            <h3>Firma registrada correctamente</h3>
            <p>Tu declaración fue firmada y enviada a la administración. Cuando la revise, recibirás la
               liquidación final para dar tu conformidad.</p>
            <div class="fl-ok-bloque-codigo">
                <div class="fl-ok-etiqueta">
                    Código de verificación del documento
                </div>
                <div class="hash fl-ok-codigo" id="okCodigo">-</div>
            </div>
            <div class="fl-ok-bloque-hash">
                <div class="fl-ok-etiqueta">
                    Hash SHA-256 del documento
                </div>
                <div class="hash" id="okHash">-</div>
            </div>
            <div class="fl-ok-bloque-pdf">
                <a href="#" id="okVerPdf" class="fl-btn fl-btn-ancho">
                    Ver PDF firmado
                </a>
            </div>
            <div class="fl-ok-bloque-volver">
                <a href="<%= ResolveUrl("~/Views/DashboardConductor.aspx") %>" class="fl-btn fl-btn-ancho fl-btn-primario">
                    Volver al inicio
                </a>
            </div>
        </div>

    </main>

    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/lib/jquery-3.6.4.min.js") %>"></script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/FirmaCanvas.js") %>"></script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/FirmarLiquidacion.js") %>"></script>
</form>
</body>
</html>

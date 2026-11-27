<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="ConformarLiquidacion.aspx.cs" Inherits="WebSGV.Views.ConformarLiquidacion" ResponseEncoding="utf-8" %>
<!DOCTYPE html>
<html lang="es"<%= WebSGV.Helpers.SesionRestauradaHelper.AtributosHtml(Session) %>>
<head runat="server">
    <meta charset="utf-8" />
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <meta name="theme-color" content="#f8f9fa" />
    <title>Conformidad de la liquidación · SGV</title>

    <%-- Mismas librerías y versiones que Site.Master (Bootstrap 4.6.2, Font Awesome 5.15.4). --%>
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/lib/bootstrap-4.6.2/bootstrap.min.css") %>" rel="stylesheet" />
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/lib/fontawesome-5.15.4/css/all.min.css") %>" rel="stylesheet" />

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/sgv-base.css") %>" rel="stylesheet" />
    <%-- Página restaurada con Atrás tras cerrar sesión (bfcache): se comprueba la sesión antes de mostrarla. --%>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/SesionRestaurada.js") %>"></script>
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/FirmarLiquidacion.css") %>" rel="stylesheet" />
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/ConformarLiquidacion.css") %>" rel="stylesheet" />
</head>
<body>
<form id="form1" runat="server">
    <asp:HiddenField ID="hfIdOrdenViaje" runat="server" ClientIDMode="Static" />

    <header class="fl-header">
        <div class="logo">SGV</div>
        <div>
            <%-- Sin Site.Master (ni su modal): el "?" abre la ayuda en otra pestaña y la firma a medias no se pierde. --%>
            <div class="fl-titulo-fila">
                <h1>Liquidación final</h1>
                <a class="sgv-ayuda" href="<%: ResolveUrl("~/Views/Ayuda.aspx?tema=conductor-conformidad") %>" target="_blank" rel="noopener"
                    data-ayuda="conductor-conformidad" aria-label="Ayuda de esta p&#xE1;gina">?</a>
            </div>
            <div class="sub">Revisa y firma tu conformidad</div>
        </div>
        <div class="fl-format-chip">SGV-CDF-F-05 v01</div>
    </header>

    <main class="fl-wrap">
        <div id="pantallaConformidad">
            <a href="<%= ResolveUrl("~/Views/DashboardConductor.aspx") %>" class="cl-volver">Volver</a>
            <div id="alertBox" class="fl-alert"></div>

            <div id="cargando" class="cl-cargando"><i class="fas fa-spinner fa-spin mr-2"></i>Cargando la liquidación...</div>

            <div id="contenido" style="display:none;">
                <section class="fl-card">
                    <h2>Orden <span id="vNumero">-</span></h2>
                    <div class="fl-grid">
                        <div><div class="lbl">Fecha</div><div class="val" id="vPeriodo">-</div></div>
                        <div><div class="lbl">Unidades</div><div class="val" id="vUnidades">-</div></div>
                        <div><div class="lbl">Conductor</div><div class="val" id="vConductor">-</div></div>
                        <div><div class="lbl">Revisó</div><div class="val" id="vRevision">-</div></div>
                    </div>
                </section>

                <section class="fl-card">
                    <h2>Cómo queda tu liquidación</h2>
                    <table class="cl-tabla">
                        <thead><tr><th>Concepto</th><th class="num">S/</th><th class="num">US$</th></tr></thead>
                        <tbody id="tbLiquidacion"></tbody>
                    </table>
                    <div class="cl-letras" id="vLetras"></div>
                    <a id="lnkPdf" href="#" target="_blank" rel="noopener" class="cl-pdf">Ver el documento completo (PDF)</a>
                </section>

                <section class="fl-card" id="secEntregas" style="display:none;">
                    <h2>Cómo te entregaron el dinero</h2>
                    <table class="cl-tabla"><tbody id="tbEntregas"></tbody></table>
                </section>

                <section class="fl-card" id="secPagosDirectos" style="display:none;">
                    <h2>Pagado por la empresa directamente</h2>
                    <p class="fl-nota mt-0 mb-2">La empresa le pagó al proveedor: no pasó por ti y no cambia tu saldo.</p>
                    <table class="cl-tabla"><tbody id="tbPagosDirectos"></tbody></table>
                </section>

                <section class="fl-card cl-ajustes" id="secAjustes" style="display:none;">
                    <h2>Ajustes de la administración</h2>
                    <div id="vAjustes"></div>
                </section>

                <section class="fl-card" id="secNota" style="display:none;">
                    <h2>Nota de la administración</h2>
                    <p class="mb-0" id="vNota"></p>
                </section>

                <!-- Conformidad -->
                <div class="fl-declaracion">
                    <strong>Conformidad:</strong>
                    <%: WebSGV.Services.Liquidaciones.TextosFirma.Conformidad %>
                </div>

                <label class="fl-check">
                    <input type="checkbox" id="chkAcepto" />
                    <label for="chkAcepto">Revisé la liquidación final y estoy conforme</label>
                </label>

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
                    <button type="button" class="fl-btn" id="btnLimpiar">Limpiar</button>
                    <button type="button" class="fl-btn" id="btnDeshacer" disabled>Deshacer</button>
                </div>

                <div class="fl-clave">
                    <label for="txtContrasena">Tu contraseña, para confirmar que eres tú quien firma</label>
                    <input type="password" id="txtContrasena" class="form-control" autocomplete="current-password" maxlength="100" />
                </div>

                <button type="button" class="fl-submit" id="btnFirmar" disabled>
                    Firmar mi conformidad
                </button>

                <!-- Desacuerdo -->
                <div class="cl-desacuerdo">
                    <button type="button" class="fl-btn" id="btnMostrarObservar">
                        No estoy de acuerdo
                    </button>
                    <div id="panelObservar" style="display:none;">
                        <label for="txtObservacion">Explica con qué no estás de acuerdo. La liquidación volverá a la administración para revisarla.</label>
                        <textarea id="txtObservacion" class="form-control" rows="3" maxlength="500"
                            placeholder="Ej.: El adelanto que recibí fue S/ 800, no S/ 900."></textarea>
                        <button type="button" class="fl-btn cl-btn-observar" id="btnObservar">
                            Enviar mi observación
                        </button>
                    </div>
                </div>

                <p class="fl-legal">
                    <%: WebSGV.Services.Liquidaciones.TextosFirma.DatosPersonales %>
                    <%: WebSGV.Services.Liquidaciones.TextosFirma.BaseLegal %>
                </p>
            </div>
        </div>

        <!-- Resultado -->
        <div id="pantallaExito" class="fl-success-screen">
            <h3 id="okTitulo">Conformidad registrada</h3>
            <p id="okTexto">Tu liquidación quedó aprobada y el viaje, completado.</p>
            <div id="okCodigoBloque" class="fl-ok-bloque">
                <div class="fl-ok-etiqueta">
                    Código de verificación del documento
                </div>
                <div class="hash fl-ok-codigo" id="okCodigo">-</div>
            </div>
            <div class="fl-ok-bloque-pdf" id="okPdfBloque">
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
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/ConformarLiquidacion.js") %>"></script>
</form>
</body>
</html>

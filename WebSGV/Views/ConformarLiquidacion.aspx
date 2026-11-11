<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="ConformarLiquidacion.aspx.cs" Inherits="WebSGV.Views.ConformarLiquidacion" ResponseEncoding="utf-8" %>
<!DOCTYPE html>
<html lang="es">
<head runat="server">
    <meta charset="utf-8" />
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <meta name="theme-color" content="#0B3D91" />
    <title>Conformidad de la liquidación · SGV</title>

    <%-- Mismas librerías y versiones que Site.Master (Bootstrap 4.6.2, Font Awesome 5.15.4). --%>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@4.6.2/dist/css/bootstrap.min.css" rel="stylesheet" />
    <link href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/5.15.4/css/all.min.css" rel="stylesheet" />
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Montserrat:wght@500;600;700&family=Open+Sans:wght@400;600&display=swap" rel="stylesheet" />

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/FirmarLiquidacion.css") %>" rel="stylesheet" />
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/ConformarLiquidacion.css") %>" rel="stylesheet" />
</head>
<body>
<form id="form1" runat="server">
    <asp:HiddenField ID="hfIdOrdenViaje" runat="server" ClientIDMode="Static" />

    <header class="fl-header">
        <div class="logo">SGV</div>
        <div>
            <h1>Liquidación final</h1>
            <div class="sub">Revise y firme su conformidad</div>
        </div>
        <div class="fl-format-chip">SGV-CDF-F-05 v01</div>
    </header>

    <main class="fl-wrap">
        <div id="pantallaConformidad">
            <div id="alertBox" class="fl-alert"></div>

            <div id="cargando" class="cl-cargando"><i class="fas fa-spinner fa-spin mr-2"></i>Cargando la liquidación...</div>

            <div id="contenido" style="display:none;">
                <section class="fl-card">
                    <h2><i class="fas fa-file-invoice mr-2"></i>Orden <span id="vNumero">-</span></h2>
                    <div class="fl-grid">
                        <div><div class="lbl">Fecha</div><div class="val" id="vPeriodo">-</div></div>
                        <div><div class="lbl">Unidades</div><div class="val" id="vUnidades">-</div></div>
                        <div><div class="lbl">Conductor</div><div class="val" id="vConductor">-</div></div>
                        <div><div class="lbl">Revisó</div><div class="val" id="vRevision">-</div></div>
                    </div>
                </section>

                <section class="fl-card">
                    <h2><i class="fas fa-calculator mr-2"></i>Cómo queda su liquidación</h2>
                    <table class="cl-tabla">
                        <thead><tr><th>Concepto</th><th class="num">S/</th><th class="num">US$</th></tr></thead>
                        <tbody id="tbLiquidacion"></tbody>
                    </table>
                    <div class="cl-letras" id="vLetras"></div>
                    <a id="lnkPdf" href="#" target="_blank" class="cl-pdf"><i class="fas fa-file-pdf mr-1"></i>Ver el documento completo (PDF)</a>
                </section>

                <section class="fl-card" id="secEntregas" style="display:none;">
                    <h2><i class="fas fa-hand-holding-usd mr-2"></i>Cómo le entregaron el dinero</h2>
                    <table class="cl-tabla"><tbody id="tbEntregas"></tbody></table>
                </section>

                <section class="fl-card" id="secPagosDirectos" style="display:none;">
                    <h2><i class="fas fa-store mr-2"></i>Pagado por la empresa directamente</h2>
                    <p class="fl-nota mt-0 mb-2">La empresa le pagó al proveedor: no pasó por usted y no cambia su saldo.</p>
                    <table class="cl-tabla"><tbody id="tbPagosDirectos"></tbody></table>
                </section>

                <section class="fl-card cl-ajustes" id="secAjustes" style="display:none;">
                    <h2><i class="fas fa-balance-scale mr-2"></i>Ajustes de la administración</h2>
                    <div id="vAjustes"></div>
                </section>

                <section class="fl-card" id="secNota" style="display:none;">
                    <h2><i class="fas fa-comment-alt mr-2"></i>Nota de la administración</h2>
                    <p class="mb-0" id="vNota"></p>
                </section>

                <!-- Conformidad -->
                <div class="fl-declaracion">
                    <i class="fas fa-gavel mr-1"></i>
                    <strong>Conformidad:</strong>
                    <%: WebSGV.Services.Liquidaciones.TextosFirma.Conformidad %>
                </div>

                <label class="fl-check">
                    <input type="checkbox" id="chkAcepto" />
                    <label for="chkAcepto">Revisé la liquidación final y estoy conforme</label>
                </label>

                <div class="fl-canvas-label">
                    <i class="fas fa-signature mr-1"></i>Firme con el dedo o mouse dentro del recuadro:
                </div>
                <div id="canvasBox" class="fl-canvas-box">
                    <div class="fl-canvas-hint">
                        <i class="fas fa-pen-nib"></i>
                        <span>Dibuje aquí su firma</span>
                    </div>
                    <div class="fl-canvas-baseline"></div>
                    <div class="fl-canvas-x">&times;</div>
                    <canvas id="canvasFirma"></canvas>
                </div>
                <div class="fl-canvas-actions">
                    <button type="button" class="fl-btn" id="btnLimpiar"><i class="fas fa-eraser"></i>Limpiar</button>
                    <button type="button" class="fl-btn" id="btnDeshacer" disabled><i class="fas fa-undo"></i>Deshacer</button>
                </div>

                <div class="fl-clave">
                    <label for="txtContrasena"><i class="fas fa-lock mr-1"></i>Su contraseña, para confirmar que es usted quien firma</label>
                    <input type="password" id="txtContrasena" class="form-control" autocomplete="current-password" maxlength="100" />
                </div>

                <button type="button" class="fl-submit" id="btnFirmar" disabled>
                    <i class="fas fa-check-circle"></i>Firmar mi conformidad
                </button>

                <!-- Desacuerdo -->
                <div class="cl-desacuerdo">
                    <button type="button" class="fl-btn" id="btnMostrarObservar">
                        <i class="fas fa-comment-dots"></i>No estoy de acuerdo
                    </button>
                    <div id="panelObservar" style="display:none;">
                        <label for="txtObservacion">Explique con qué no está de acuerdo. La liquidación volverá a la administración para revisarla.</label>
                        <textarea id="txtObservacion" class="form-control" rows="3" maxlength="500"
                            placeholder="Ej.: El adelanto que recibí fue S/ 800, no S/ 900."></textarea>
                        <button type="button" class="fl-btn cl-btn-observar" id="btnObservar">
                            <i class="fas fa-paper-plane"></i>Enviar mi observación
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
            <div class="icon"><i class="fas fa-check" id="okIcono"></i></div>
            <h3 id="okTitulo">Conformidad registrada</h3>
            <p id="okTexto">Su liquidación quedó aprobada y el viaje, completado.</p>
            <div id="okCodigoBloque" style="margin: 18px 0;">
                <div style="font-size: 11px; color: var(--gris-600); text-transform: uppercase; margin-bottom: 6px;">
                    Código de verificación del documento
                </div>
                <div class="hash" id="okCodigo" style="font-size: 16px; letter-spacing: 1px;">-</div>
            </div>
            <div style="margin-top: 18px;" id="okPdfBloque">
                <a href="#" id="okVerPdf" class="fl-btn" style="text-decoration:none; display:inline-block; max-width:240px;">
                    <i class="fas fa-file-pdf"></i>Ver PDF firmado
                </a>
            </div>
            <div style="margin-top: 12px;">
                <a href="<%= ResolveUrl("~/Views/DashboardConductor.aspx") %>" class="fl-btn" style="text-decoration:none; display:inline-block; max-width:240px; background: var(--azul); color:#fff; border-color: var(--azul);">
                    <i class="fas fa-home"></i>Volver al inicio
                </a>
            </div>
        </div>
    </main>

    <script src="https://code.jquery.com/jquery-3.6.4.min.js"></script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/FirmaCanvas.js") %>"></script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/ConformarLiquidacion.js") %>"></script>
</form>
</body>
</html>

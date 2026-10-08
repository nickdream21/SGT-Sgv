<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="FirmarLiquidacion.aspx.cs" Inherits="WebSGV.Views.FirmarLiquidacion" ResponseEncoding="utf-8" %>
<!DOCTYPE html>
<html lang="es">
<head runat="server">
    <meta charset="utf-8" />
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no" />
    <meta name="theme-color" content="#0B3D91" />
    <title>Firmar Liquidación · SGV</title>

    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/bootstrap.min.css") %>" />
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/fontawesome/css/all.min.css") %>" />
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
    <link href="https://fonts.googleapis.com/css2?family=Montserrat:wght@500;600;700&family=Open+Sans:wght@400;600&display=swap" rel="stylesheet" />

    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/FirmarLiquidacion.css") %>" rel="stylesheet" />
</head>
<body>
<form id="form1" runat="server">
    <asp:HiddenField ID="hfIdOrdenViaje" runat="server" ClientIDMode="Static" />

    <header class="fl-header">
        <div class="logo">SGV</div>
        <div>
            <h1>Orden de Viaje</h1>
            <div class="sub">Firma del Conductor - Declaración Jurada</div>
        </div>
        <div class="fl-format-chip">SGV-CDF-F-05 v01</div>
    </header>

    <main class="fl-wrap">

        <!-- Pantalla principal -->
        <div id="pantallaFirma">

            <div id="alertBox" class="fl-alert"></div>

            <!-- Resumen de la liquidación -->
            <section class="fl-card">
                <h2><i class="fas fa-file-invoice mr-2"></i>Resumen de la Liquidación</h2>
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
                    <span class="label">Total ingresos declarados</span>
                    <span class="valor" id="vTotal">S/ 0.00</span>
                </div>
                <div class="fl-monto" style="border-left-color: var(--rojo); background: #FEF2F2;">
                    <span class="label">Total gastos sustentados</span>
                    <span class="valor" style="color: var(--rojo);" id="vGastos">S/ 0.00</span>
                </div>
            </section>

            <!-- Declaración jurada -->
            <div class="fl-declaracion">
                <i class="fas fa-gavel mr-1"></i>
                <strong>Declaración Jurada:</strong>
                Declaro bajo juramento que los datos y montos de esta liquidación son verdaderos,
                que los comprobantes entregados son auténticos y que autorizo su uso para los
                fines administrativos, contables y legales correspondientes. Esta firma tiene pleno
                valor probatorio y constituye mi manifestación de voluntad.
            </div>

            <!-- Checkbox de consentimiento -->
            <label class="fl-check">
                <input type="checkbox" id="chkAcepto" />
                <label for="chkAcepto">He leído y acepto la declaración jurada arriba descrita</label>
            </label>

            <!-- Canvas de firma -->
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
                <button type="button" class="fl-btn" id="btnLimpiar">
                    <i class="fas fa-eraser"></i>Limpiar
                </button>
                <button type="button" class="fl-btn" id="btnDeshacer" disabled>
                    <i class="fas fa-undo"></i>Deshacer
                </button>
            </div>

            <!-- Botón enviar -->
            <button type="button" class="fl-submit" id="btnFirmar" disabled>
                <i class="fas fa-check-circle"></i>Firmar y Enviar
            </button>

            <p style="text-align:center; font-size: 11px; color: var(--gris-600); margin-top: 16px;">
                Al firmar, acepta que se registre la fecha, hora, IP y navegador como
                evidencia de trazabilidad (append-only, no modificable).
            </p>
        </div>

        <!-- Pantalla de éxito -->
        <div id="pantallaExito" class="fl-success-screen">
            <div class="icon"><i class="fas fa-check"></i></div>
            <h3>Firma registrada correctamente</h3>
            <p>Su liquidación ha sido enviada y firmada digitalmente.</p>
            <div style="margin: 18px 0;">
                <div style="font-size: 11px; color: var(--gris-600); text-transform: uppercase; margin-bottom: 6px;">
                    Hash SHA-256 del documento
                </div>
                <div class="hash" id="okHash">-</div>
            </div>
            <div style="margin-top: 18px;">
                <a href="#" id="okVerPdf" class="fl-btn" style="text-decoration:none; display:inline-block; max-width:220px;">
                    <i class="fas fa-file-pdf"></i>Ver PDF firmado
                </a>
            </div>
            <div style="margin-top: 12px;">
                <a href="<%= ResolveUrl("~/Views/DashboardConductor.aspx") %>" class="fl-btn" style="text-decoration:none; display:inline-block; max-width:220px; background: var(--azul); color:#fff; border-color: var(--azul);">
                    <i class="fas fa-home"></i>Volver al inicio
                </a>
            </div>
        </div>

    </main>

    <script src="<%= ResolveUrl("~/Scripts/jquery-3.7.0.min.js") %>"></script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/FirmarLiquidacion.js") %>"></script>
</form>
</body>
</html>

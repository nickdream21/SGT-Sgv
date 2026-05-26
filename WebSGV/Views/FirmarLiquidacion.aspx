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

    <style>
        :root {
            --azul: #0B3D91;
            --rojo: #C8102E;
            --gris-900: #1F2937;
            --gris-600: #6B7280;
            --gris-300: #D1D5DB;
            --gris-100: #F3F4F6;
            --azul-claro: #E8EEF7;
        }
        *, *::before, *::after { box-sizing: border-box; }
        html, body {
            margin: 0; padding: 0;
            height: 100%;
            font-family: 'Open Sans', system-ui, -apple-system, sans-serif;
            background: var(--gris-100);
            color: var(--gris-900);
            overscroll-behavior: none;
            -webkit-tap-highlight-color: transparent;
        }
        h1, h2, h3, h4 { font-family: 'Montserrat', sans-serif; margin: 0; }

        /* ---------- Header corporativo ---------- */
        .fl-header {
            background: linear-gradient(135deg, var(--azul) 0%, #153fa3 100%);
            color: #fff;
            padding: 14px 16px;
            display: flex;
            align-items: center;
            gap: 12px;
            box-shadow: 0 2px 6px rgba(0,0,0,.12);
        }
        .fl-header .logo {
            width: 40px; height: 40px;
            background: #fff;
            border-radius: 6px;
            display: flex; align-items: center; justify-content: center;
            font-weight: 700; color: var(--azul);
            font-size: 14px; letter-spacing: .5px;
        }
        .fl-header h1 {
            font-size: 16px; font-weight: 700; line-height: 1.2;
        }
        .fl-header .sub {
            font-size: 11px; opacity: .85;
        }
        .fl-format-chip {
            margin-left: auto;
            font-size: 10px;
            background: rgba(255,255,255,.16);
            padding: 4px 8px;
            border-radius: 4px;
            letter-spacing: .5px;
            white-space: nowrap;
        }

        /* ---------- Contenedor principal ---------- */
        .fl-wrap { max-width: 720px; margin: 0 auto; padding: 14px; }

        /* ---------- Tarjeta de resumen ---------- */
        .fl-card {
            background: #fff;
            border: 1px solid var(--gris-300);
            border-radius: 8px;
            padding: 14px 16px;
            margin-bottom: 14px;
        }
        .fl-card h2 {
            font-size: 13px; color: var(--azul); font-weight: 700;
            text-transform: uppercase; letter-spacing: .5px;
            margin-bottom: 10px;
            border-bottom: 2px solid var(--azul);
            padding-bottom: 6px;
        }
        .fl-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 8px 12px;
            font-size: 13px;
        }
        .fl-grid .lbl { color: var(--gris-600); font-size: 11px; text-transform: uppercase; }
        .fl-grid .val { font-weight: 600; color: var(--gris-900); word-break: break-word; }

        .fl-monto {
            display: flex; justify-content: space-between; align-items: center;
            padding: 10px 12px;
            background: var(--azul-claro);
            border-left: 4px solid var(--azul);
            border-radius: 4px;
            margin-top: 10px;
            font-family: 'Montserrat', sans-serif;
        }
        .fl-monto .label { font-size: 11px; color: var(--gris-600); text-transform: uppercase; }
        .fl-monto .valor { font-size: 18px; font-weight: 700; color: var(--azul); }

        /* ---------- Declaración jurada ---------- */
        .fl-declaracion {
            background: #FFFBEB;
            border: 1px solid #F59E0B;
            border-left: 4px solid #F59E0B;
            border-radius: 6px;
            padding: 12px 14px;
            font-size: 12.5px;
            line-height: 1.5;
            margin-bottom: 14px;
        }
        .fl-declaracion strong { color: #92400E; }

        .fl-check {
            display: flex; align-items: flex-start; gap: 10px;
            padding: 10px 12px;
            background: #fff;
            border: 2px solid var(--gris-300);
            border-radius: 6px;
            margin-bottom: 14px;
            cursor: pointer;
            transition: border-color .15s;
        }
        .fl-check:has(input:checked) { border-color: var(--azul); background: var(--azul-claro); }
        .fl-check input { width: 20px; height: 20px; margin-top: 2px; accent-color: var(--azul); cursor: pointer; }
        .fl-check label { font-size: 13px; font-weight: 600; color: var(--gris-900); cursor: pointer; margin: 0; line-height: 1.4; }

        /* ---------- Canvas de firma ---------- */
        .fl-canvas-label {
            font-size: 12px; color: var(--gris-600);
            text-transform: uppercase; letter-spacing: .5px;
            margin-bottom: 6px; font-weight: 600;
        }
        .fl-canvas-box {
            position: relative;
            background: #fff;
            border: 2px dashed var(--gris-300);
            border-radius: 8px;
            height: 240px;
            overflow: hidden;
            touch-action: none;
            margin-bottom: 8px;
        }
        .fl-canvas-box.filled { border-style: solid; border-color: var(--azul); }
        .fl-canvas-box canvas {
            display: block;
            width: 100%;
            height: 100%;
            cursor: crosshair;
        }
        .fl-canvas-hint {
            position: absolute;
            inset: 0;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            pointer-events: none;
            color: var(--gris-600);
            font-size: 13px;
            text-align: center;
            padding: 20px;
        }
        .fl-canvas-hint i { font-size: 32px; color: var(--gris-300); margin-bottom: 6px; }
        .fl-canvas-box.filled .fl-canvas-hint { display: none; }
        .fl-canvas-baseline {
            position: absolute;
            left: 10%; right: 10%;
            bottom: 38px;
            border-top: 1px dashed var(--gris-300);
            pointer-events: none;
        }
        .fl-canvas-x {
            position: absolute;
            left: 8%;
            bottom: 32px;
            color: var(--gris-600);
            font-size: 20px;
            font-weight: 700;
            pointer-events: none;
        }

        .fl-canvas-actions {
            display: flex; gap: 8px; margin-bottom: 16px;
        }
        .fl-btn {
            flex: 1;
            padding: 10px 14px;
            font-size: 13px; font-weight: 600;
            border-radius: 6px;
            border: 1px solid var(--gris-300);
            background: #fff;
            color: var(--gris-900);
            cursor: pointer;
            transition: all .15s;
        }
        .fl-btn:hover:not(:disabled) { background: var(--gris-100); }
        .fl-btn:disabled { opacity: .5; cursor: not-allowed; }
        .fl-btn i { margin-right: 6px; }

        .fl-submit {
            width: 100%;
            padding: 14px;
            font-size: 15px; font-weight: 700;
            background: linear-gradient(135deg, var(--azul) 0%, #153fa3 100%);
            color: #fff;
            border: none;
            border-radius: 8px;
            cursor: pointer;
            letter-spacing: .5px;
            font-family: 'Montserrat', sans-serif;
            box-shadow: 0 2px 6px rgba(11,61,145,.25);
            transition: all .15s;
        }
        .fl-submit:hover:not(:disabled) { box-shadow: 0 4px 12px rgba(11,61,145,.35); transform: translateY(-1px); }
        .fl-submit:disabled { opacity: .5; cursor: not-allowed; background: var(--gris-600); box-shadow: none; }
        .fl-submit.loading i { animation: spin 0.8s linear infinite; }
        @keyframes spin { 100% { transform: rotate(360deg); } }

        /* ---------- Feedback ---------- */
        .fl-alert {
            padding: 12px 14px;
            border-radius: 6px;
            font-size: 13px;
            margin-bottom: 14px;
            display: none;
        }
        .fl-alert.show { display: block; }
        .fl-alert.error   { background: #FEF2F2; border: 1px solid var(--rojo); color: #991B1B; }
        .fl-alert.success { background: #F0FDF4; border: 1px solid #16A34A; color: #166534; }

        /* ---------- Pantalla de éxito ---------- */
        .fl-success-screen {
            text-align: center;
            padding: 30px 20px;
            display: none;
        }
        .fl-success-screen.show { display: block; }
        .fl-success-screen .icon {
            width: 80px; height: 80px;
            margin: 0 auto 16px;
            border-radius: 50%;
            background: #16A34A;
            color: #fff;
            display: flex; align-items: center; justify-content: center;
            font-size: 42px;
        }
        .fl-success-screen h3 { color: #166534; font-size: 18px; margin-bottom: 8px; }
        .fl-success-screen p { color: var(--gris-600); font-size: 13px; margin-bottom: 20px; }
        .fl-success-screen .hash {
            display: inline-block;
            font-family: 'Courier New', monospace;
            font-size: 11px;
            background: var(--gris-100);
            padding: 6px 10px;
            border-radius: 4px;
            color: var(--gris-900);
            word-break: break-all;
            max-width: 100%;
        }

        /* ---------- Responsive ---------- */
        @media (max-width: 480px) {
            .fl-grid { grid-template-columns: 1fr; }
            .fl-canvas-box { height: 200px; }
        }
        @media (min-width: 768px) {
            .fl-canvas-box { height: 280px; }
        }
    </style>
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
    <script>
    (function () {
        'use strict';

        // =================== Canvas de firma (implementación propia, sin libs) ===================
        var canvas = document.getElementById('canvasFirma');
        var box    = document.getElementById('canvasBox');
        var ctx    = canvas.getContext('2d');
        var trazos = []; // array de arrays de puntos {x,y}
        var trazoActual = null;
        var dibujando = false;

        function resizeCanvas() {
            var dpr = window.devicePixelRatio || 1;
            var r = canvas.getBoundingClientRect();
            canvas.width  = Math.floor(r.width  * dpr);
            canvas.height = Math.floor(r.height * dpr);
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
            redraw();
        }

        function redraw() {
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            ctx.strokeStyle = '#0B3D91';
            ctx.lineWidth = 2.2;
            ctx.lineCap = 'round';
            ctx.lineJoin = 'round';
            trazos.forEach(function(t) {
                if (t.length < 2) return;
                ctx.beginPath();
                ctx.moveTo(t[0].x, t[0].y);
                for (var i = 1; i < t.length; i++) ctx.lineTo(t[i].x, t[i].y);
                ctx.stroke();
            });
        }

        function puntoFrom(ev) {
            var r = canvas.getBoundingClientRect();
            var touch = ev.touches && ev.touches[0];
            var cx = touch ? touch.clientX : ev.clientX;
            var cy = touch ? touch.clientY : ev.clientY;
            return { x: cx - r.left, y: cy - r.top };
        }

        function start(ev) {
            ev.preventDefault();
            dibujando = true;
            trazoActual = [puntoFrom(ev)];
            trazos.push(trazoActual);
            box.classList.add('filled');
            actualizarEstadoBotones();
        }
        function move(ev) {
            if (!dibujando) return;
            ev.preventDefault();
            trazoActual.push(puntoFrom(ev));
            redraw();
        }
        function end(ev) {
            if (!dibujando) return;
            ev.preventDefault();
            dibujando = false;
            trazoActual = null;
        }

        canvas.addEventListener('mousedown',  start);
        canvas.addEventListener('mousemove',  move);
        window.addEventListener('mouseup',    end);
        canvas.addEventListener('touchstart', start, { passive: false });
        canvas.addEventListener('touchmove',  move,  { passive: false });
        canvas.addEventListener('touchend',   end,   { passive: false });
        canvas.addEventListener('touchcancel',end,   { passive: false });

        window.addEventListener('resize', resizeCanvas);
        window.addEventListener('orientationchange', function() { setTimeout(resizeCanvas, 200); });
        setTimeout(resizeCanvas, 50);

        // =================== Controles ===================
        var chkAcepto = document.getElementById('chkAcepto');
        var btnFirmar = document.getElementById('btnFirmar');
        var btnLimpiar = document.getElementById('btnLimpiar');
        var btnDeshacer = document.getElementById('btnDeshacer');

        function estaFirmado() {
            return trazos.length > 0 && trazos.some(function(t){ return t.length >= 3; });
        }
        function actualizarEstadoBotones() {
            btnFirmar.disabled = !(chkAcepto.checked && estaFirmado());
            btnDeshacer.disabled = trazos.length === 0;
            if (!estaFirmado()) box.classList.remove('filled');
        }

        chkAcepto.addEventListener('change', actualizarEstadoBotones);

        btnLimpiar.addEventListener('click', function() {
            trazos = [];
            redraw();
            box.classList.remove('filled');
            actualizarEstadoBotones();
        });

        btnDeshacer.addEventListener('click', function() {
            trazos.pop();
            redraw();
            actualizarEstadoBotones();
        });

        // =================== Cargar resumen ===================
        var idOrden = parseInt(document.getElementById('hfIdOrdenViaje').value || '0', 10);

        function fmtDinero(n, sym) {
            sym = sym || 'S/';
            var v = (typeof n === 'number') ? n : parseFloat(n || 0);
            return sym + ' ' + v.toLocaleString('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        }

        function mostrarAlerta(tipo, msg) {
            var box = document.getElementById('alertBox');
            box.className = 'fl-alert show ' + tipo;
            box.innerHTML = '<i class="fas fa-' + (tipo === 'error' ? 'exclamation-circle' : 'check-circle') + ' mr-1"></i>' + msg;
            window.scrollTo({ top: 0, behavior: 'smooth' });
        }

        function cargarResumen() {
            if (!idOrden || idOrden <= 0) {
                mostrarAlerta('error', 'No se especificó una orden de viaje válida.');
                btnFirmar.disabled = true;
                return;
            }
            $.ajax({
                type: 'POST',
                url: 'LiquidacionesPendientes.aspx/ObtenerDetalleLiquidacion',
                data: JSON.stringify({ idOrdenViaje: idOrden }),
                contentType: 'application/json; charset=utf-8',
                dataType: 'json'
            }).done(function(resp) {
                var d = resp && resp.d;
                if (!d) { mostrarAlerta('error', 'No se pudo cargar la información de la liquidación.'); return; }
                document.getElementById('vNumero').textContent    = d.NumeroOrdenViaje || '-';
                document.getElementById('vFechas').textContent    = (d.FechaSalida || '') + ' → ' + (d.FechaLlegada || '');
                document.getElementById('vConductor').textContent = d.NombreConductor || '-';
                document.getElementById('vUnidades').textContent  = (d.PlacaTracto || '') + ' / ' + (d.PlacaCarreta || '');
                document.getElementById('vTotal').textContent     = fmtDinero(d.TotalIngresosSoles, 'S/');
                document.getElementById('vGastos').textContent    = fmtDinero(d.TotalGastosSoles, 'S/');
            }).fail(function(xhr) {
                mostrarAlerta('error', 'Error al cargar: ' + (xhr.status || '') + ' ' + (xhr.statusText || ''));
            });
        }
        cargarResumen();

        // =================== Enviar firma ===================
        btnFirmar.addEventListener('click', function() {
            if (btnFirmar.disabled) return;
            if (!chkAcepto.checked) { mostrarAlerta('error', 'Debe aceptar la declaración jurada.'); return; }
            if (!estaFirmado())     { mostrarAlerta('error', 'Debe dibujar su firma antes de continuar.'); return; }

            btnFirmar.disabled = true;
            btnFirmar.classList.add('loading');
            btnFirmar.innerHTML = '<i class="fas fa-spinner"></i>Registrando firma...';

            var pngBase64 = canvas.toDataURL('image/png');

            $.ajax({
                type: 'POST',
                url: 'LiquidacionesPendientes.aspx/RegistrarFirmaConductor',
                data: JSON.stringify({ idOrdenViaje: idOrden, firmaPngBase64: pngBase64 }),
                contentType: 'application/json; charset=utf-8',
                dataType: 'json'
            }).done(function(resp) {
                var r = resp && resp.d;
                if (r && r.success) {
                    document.getElementById('pantallaFirma').style.display = 'none';
                    var exito = document.getElementById('pantallaExito');
                    exito.classList.add('show');
                    document.getElementById('okHash').textContent = r.hashPdf || '(sin hash)';
                    var verPdf = document.getElementById('okVerPdf');
                    verPdf.href = 'TestGenerarPdfOrdenViaje.aspx?id=' + idOrden;
                    verPdf.target = '_blank';
                    window.scrollTo({ top: 0, behavior: 'smooth' });
                } else {
                    mostrarAlerta('error', (r && r.message) ? r.message : 'No fue posible registrar la firma.');
                    btnFirmar.disabled = false;
                    btnFirmar.classList.remove('loading');
                    btnFirmar.innerHTML = '<i class="fas fa-check-circle"></i>Firmar y Enviar';
                }
            }).fail(function(xhr) {
                var msg = 'Error de comunicación';
                try {
                    var e = JSON.parse(xhr.responseText);
                    if (e && e.Message) msg = e.Message;
                } catch (ex) {}
                mostrarAlerta('error', msg);
                btnFirmar.disabled = false;
                btnFirmar.classList.remove('loading');
                btnFirmar.innerHTML = '<i class="fas fa-check-circle"></i>Firmar y Enviar';
            });
        });
    })();
    </script>
</form>
</body>
</html>

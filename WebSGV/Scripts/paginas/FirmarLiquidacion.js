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
                verPdf.href = 'DescargarPdfOrdenViaje.aspx?id=' + idOrden;
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

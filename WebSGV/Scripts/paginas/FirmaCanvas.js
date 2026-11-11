// Lienzo de firma manuscrita (dedo o mouse), compartido por FirmarLiquidacion (declaración jurada)
// y ConformarLiquidacion (conformidad con la liquidación final). Sin librerías.
//   var firma = SGVFirmaCanvas.crear('canvasFirma', 'canvasBox', alCambiar);
//   firma.estaFirmado(); firma.limpiar(); firma.deshacer(); firma.hayTrazos(); firma.png();
(function (global) {
    'use strict';

    function crear(idCanvas, idCaja, alCambiar) {
        var canvas = document.getElementById(idCanvas);
        var box = document.getElementById(idCaja);
        var ctx = canvas.getContext('2d');
        var trazos = []; // arreglo de trazos; cada trazo, arreglo de puntos {x, y}
        var trazoActual = null;
        var dibujando = false;
        var notificar = typeof alCambiar === 'function' ? alCambiar : function () { };

        function redimensionar() {
            var dpr = window.devicePixelRatio || 1;
            var r = canvas.getBoundingClientRect();
            canvas.width = Math.floor(r.width * dpr);
            canvas.height = Math.floor(r.height * dpr);
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
            redibujar();
        }

        function redibujar() {
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            ctx.strokeStyle = '#0B3D91';
            ctx.lineWidth = 2.2;
            ctx.lineCap = 'round';
            ctx.lineJoin = 'round';
            trazos.forEach(function (t) {
                if (t.length < 2) return;
                ctx.beginPath();
                ctx.moveTo(t[0].x, t[0].y);
                for (var i = 1; i < t.length; i++) ctx.lineTo(t[i].x, t[i].y);
                ctx.stroke();
            });
        }

        function punto(ev) {
            var r = canvas.getBoundingClientRect();
            var touch = ev.touches && ev.touches[0];
            var cx = touch ? touch.clientX : ev.clientX;
            var cy = touch ? touch.clientY : ev.clientY;
            return { x: cx - r.left, y: cy - r.top };
        }

        function inicio(ev) {
            ev.preventDefault();
            dibujando = true;
            trazoActual = [punto(ev)];
            trazos.push(trazoActual);
            box.classList.add('filled');
            notificar();
        }
        function mover(ev) {
            if (!dibujando) return;
            ev.preventDefault();
            trazoActual.push(punto(ev));
            redibujar();
        }
        function fin(ev) {
            if (!dibujando) return;
            ev.preventDefault();
            dibujando = false;
            trazoActual = null;
            // Al soltar, el trazo ya tiene puntos suficientes para habilitar el botón de firma.
            notificar();
        }

        canvas.addEventListener('mousedown', inicio);
        canvas.addEventListener('mousemove', mover);
        window.addEventListener('mouseup', fin);
        canvas.addEventListener('touchstart', inicio, { passive: false });
        canvas.addEventListener('touchmove', mover, { passive: false });
        canvas.addEventListener('touchend', fin, { passive: false });
        canvas.addEventListener('touchcancel', fin, { passive: false });
        window.addEventListener('resize', redimensionar);
        window.addEventListener('orientationchange', function () { setTimeout(redimensionar, 200); });
        setTimeout(redimensionar, 50);

        var api = {
            estaFirmado: function () {
                return trazos.some(function (t) { return t.length >= 3; });
            },
            hayTrazos: function () { return trazos.length > 0; },
            limpiar: function () {
                trazos = [];
                redibujar();
                box.classList.remove('filled');
                notificar();
            },
            deshacer: function () {
                trazos.pop();
                redibujar();
                if (!api.estaFirmado()) box.classList.remove('filled');
                notificar();
            },
            png: function () { return canvas.toDataURL('image/png'); },
            redimensionar: redimensionar
        };
        return api;
    }

    global.SGVFirmaCanvas = { crear: crear };
})(window);

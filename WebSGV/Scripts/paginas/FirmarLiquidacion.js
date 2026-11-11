(function () {
    'use strict';

    // =================== Controles ===================
    var chkAcepto = document.getElementById('chkAcepto');
    var txtClave = document.getElementById('txtContrasena');
    var btnFirmar = document.getElementById('btnFirmar');
    var btnLimpiar = document.getElementById('btnLimpiar');
    var btnDeshacer = document.getElementById('btnDeshacer');

    var firma = SGVFirmaCanvas.crear('canvasFirma', 'canvasBox', actualizarEstadoBotones);

    function actualizarEstadoBotones() {
        btnFirmar.disabled = !(chkAcepto.checked && firma.estaFirmado() && txtClave.value.length > 0);
        btnDeshacer.disabled = !firma.hayTrazos();
    }

    chkAcepto.addEventListener('change', actualizarEstadoBotones);
    txtClave.addEventListener('input', actualizarEstadoBotones);
    btnLimpiar.addEventListener('click', function () { firma.limpiar(); });
    btnDeshacer.addEventListener('click', function () { firma.deshacer(); });

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
        box.innerHTML = '';
        var i = document.createElement('i');
        i.className = 'fas fa-' + (tipo === 'error' ? 'exclamation-circle' : 'check-circle') + ' mr-1';
        box.appendChild(i);
        box.appendChild(document.createTextNode(msg));
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
        }).done(function (resp) {
            var d = resp && resp.d;
            if (!d) { mostrarAlerta('error', 'No se pudo cargar la información de la liquidación.'); return; }
            document.getElementById('vNumero').textContent = d.NumeroOrdenViaje || '-';
            document.getElementById('vFechas').textContent = (d.FechaSalida || '') + ' → ' + (d.FechaLlegada || '');
            document.getElementById('vConductor').textContent = (d.NombreConductor || '-') + (d.DocumentoConductor ? ' · ' + d.DocumentoConductor : '');
            document.getElementById('vUnidades').textContent = (d.PlacaTracto || '') + ' / ' + (d.PlacaCarreta || '');
            document.getElementById('vGastos').textContent = fmtDinero(d.TotalGastosSoles, 'S/');
            if (parseFloat(d.TotalGastosDolares) > 0) {
                document.getElementById('vGastosDolares').textContent = fmtDinero(d.TotalGastosDolares, '$');
                document.getElementById('vGastosDolaresFila').style.display = '';
            }
        }).fail(function (xhr) {
            mostrarAlerta('error', 'Error al cargar: ' + (xhr.status || '') + ' ' + (xhr.statusText || ''));
        });
    }
    cargarResumen();

    // =================== Enviar firma ===================
    function restaurarBoton() {
        btnFirmar.classList.remove('loading');
        btnFirmar.innerHTML = '<i class="fas fa-check-circle"></i>Firmar y Enviar';
        actualizarEstadoBotones();
    }

    btnFirmar.addEventListener('click', function () {
        if (btnFirmar.disabled) return;
        if (!chkAcepto.checked) { mostrarAlerta('error', 'Debe aceptar la declaración jurada.'); return; }
        if (!firma.estaFirmado()) { mostrarAlerta('error', 'Debe dibujar su firma antes de continuar.'); return; }
        if (!txtClave.value) { mostrarAlerta('error', 'Escriba su contraseña para confirmar que es usted quien firma.'); return; }

        btnFirmar.disabled = true;
        btnFirmar.classList.add('loading');
        btnFirmar.innerHTML = '<i class="fas fa-spinner"></i>Registrando firma...';

        $.ajax({
            type: 'POST',
            url: 'LiquidacionesPendientes.aspx/RegistrarFirmaConductor',
            data: JSON.stringify({ idOrdenViaje: idOrden, firmaPngBase64: firma.png(), contrasena: txtClave.value }),
            contentType: 'application/json; charset=utf-8',
            dataType: 'json'
        }).done(function (resp) {
            var r = resp && resp.d;
            if (r && r.success) {
                txtClave.value = '';
                document.getElementById('pantallaFirma').style.display = 'none';
                var exito = document.getElementById('pantallaExito');
                exito.classList.add('show');
                document.getElementById('okCodigo').textContent = r.codigo || '-';
                document.getElementById('okHash').textContent = r.hashPdf || '(sin hash)';
                var verPdf = document.getElementById('okVerPdf');
                verPdf.href = 'DescargarPdfOrdenViaje.aspx?id=' + idOrden;
                verPdf.target = '_blank';
                window.scrollTo({ top: 0, behavior: 'smooth' });
            } else {
                if (r && r.contrasenaIncorrecta) { txtClave.value = ''; txtClave.focus(); }
                mostrarAlerta('error', (r && r.message) ? r.message : 'No fue posible registrar la firma.');
                restaurarBoton();
            }
        }).fail(function (xhr) {
            var msg = 'Error de comunicación';
            try {
                var e = JSON.parse(xhr.responseText);
                if (e && e.Message) msg = e.Message;
            } catch (ex) { }
            mostrarAlerta('error', msg);
            restaurarBoton();
        });
    });
})();

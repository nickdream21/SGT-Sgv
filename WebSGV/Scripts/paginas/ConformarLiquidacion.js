(function () {
    'use strict';

    var idOrden = parseInt(document.getElementById('hfIdOrdenViaje').value || '0', 10);
    var huella = null;

    var chkAcepto = document.getElementById('chkAcepto');
    var txtClave = document.getElementById('txtContrasena');
    var btnFirmar = document.getElementById('btnFirmar');
    var btnDeshacer = document.getElementById('btnDeshacer');

    var firma = SGVFirmaCanvas.crear('canvasFirma', 'canvasBox', actualizarEstadoBotones);

    function actualizarEstadoBotones() {
        btnFirmar.disabled = !(huella && chkAcepto.checked && firma.estaFirmado() && txtClave.value.length > 0);
        btnDeshacer.disabled = !firma.hayTrazos();
    }
    chkAcepto.addEventListener('change', actualizarEstadoBotones);
    txtClave.addEventListener('input', actualizarEstadoBotones);
    document.getElementById('btnLimpiar').addEventListener('click', function () { firma.limpiar(); });
    btnDeshacer.addEventListener('click', function () { firma.deshacer(); });

    // =================== Utilidades ===================
    function monto(v) {
        var n = parseFloat(v || 0);
        if (n === 0) return '—';
        return n.toLocaleString('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }
    function celda(tag, texto, clase) {
        var c = document.createElement(tag);
        if (clase) c.className = clase;
        c.textContent = texto;
        return c;
    }
    function fila(tbody, concepto, soles, dolares, clase) {
        var tr = document.createElement('tr');
        if (clase) tr.className = clase;
        tr.appendChild(celda('td', concepto));
        tr.appendChild(celda('td', monto(soles), 'num'));
        tr.appendChild(celda('td', monto(dolares), 'num'));
        tbody.appendChild(tr);
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
    function llamar(metodo, datos) {
        return $.ajax({
            type: 'POST',
            url: 'ConformarLiquidacion.aspx/' + metodo,
            data: JSON.stringify(datos),
            contentType: 'application/json; charset=utf-8',
            dataType: 'json'
        });
    }

    // =================== Cargar la liquidación final ===================
    function cargar() {
        llamar('ObtenerLiquidacion', { idOrdenViaje: idOrden }).done(function (resp) {
            var r = resp && resp.d;
            document.getElementById('cargando').style.display = 'none';
            if (!r || !r.success) { mostrarAlerta('error', (r && r.message) || 'No se pudo cargar la liquidación.'); return; }
            var d = r.datos;
            huella = d.Huella;

            document.getElementById('vNumero').textContent = d.NumeroOrdenViaje;
            document.getElementById('vPeriodo').textContent = d.Periodo;
            document.getElementById('vUnidades').textContent = d.Unidades;
            document.getElementById('vConductor').textContent = d.NombreConductor + (d.DocumentoConductor ? ' · ' + d.DocumentoConductor : '');
            document.getElementById('vRevision').textContent = (d.RevisadoPor || '-') + (d.FechaRevision ? ' · ' + d.FechaRevision : '');

            var tb = document.getElementById('tbLiquidacion');
            tb.innerHTML = '';
            fila(tb, 'Dinero que te entregaron', d.EntregadoSoles, d.EntregadoDolares, 'total');
            (d.Entregado || []).forEach(function (c) {
                fila(tb, c.Concepto + (c.Detalle ? ' (' + c.Detalle + ')' : ''), c.Soles, c.Dolares, 'sub');
            });
            fila(tb, '(−) Tus gastos declarados', d.GastosSoles, d.GastosDolares, 'total');
            var hayDesc = d.DescuentoSoles !== 0 || d.DescuentoDolares !== 0;
            var hayReint = d.ReintegroSoles !== 0 || d.ReintegroDolares !== 0;
            if (hayDesc || hayReint) {
                fila(tb, 'Balance del viaje', d.BalanceSoles, d.BalanceDolares);
                if (hayDesc) fila(tb, '(+) Descuento', d.DescuentoSoles, d.DescuentoDolares, 'desc');
                if (hayReint) fila(tb, '(−) Reintegro', d.ReintegroSoles, d.ReintegroDolares, 'reint');
            }
            var tr = document.createElement('tr');
            tr.className = 'saldo';
            tr.appendChild(celda('td', 'Saldo final'));
            tr.appendChild(celda('td', parseFloat(d.SaldoSoles).toLocaleString('es-PE', { minimumFractionDigits: 2 }), 'num'));
            tr.appendChild(celda('td', parseFloat(d.SaldoDolares).toLocaleString('es-PE', { minimumFractionDigits: 2 }), 'num'));
            tb.appendChild(tr);

            document.getElementById('vLetras').textContent = d.SaldoTexto + ' Son S/: ' + d.SaldoSolesEnLetras +
                (d.SaldoDolaresEnLetras ? ' · Son US$: ' + d.SaldoDolaresEnLetras : '');

            function listaMovimientos(idTabla, idSeccion, items) {
                var t = document.getElementById(idTabla);
                t.innerHTML = '';
                (items || []).forEach(function (c) {
                    fila(t, c.Concepto, c.Soles, c.Dolares);
                    if (c.Detalle) fila(t, c.Detalle, 0, 0, 'sub');
                });
                document.getElementById(idSeccion).style.display = (items && items.length) ? '' : 'none';
            }
            listaMovimientos('tbEntregas', 'secEntregas', d.DetalleEntregas);
            listaMovimientos('tbPagosDirectos', 'secPagosDirectos', d.PagosDirectos);

            var aj = document.getElementById('vAjustes');
            aj.innerHTML = '';
            function tarjeta(tipo, clase, soles, dolares, motivo) {
                var div = document.createElement('div');
                div.className = 'cl-ajuste ' + clase;
                var m = document.createElement('span');
                m.className = 'monto';
                m.textContent = (soles !== 0 ? 'S/ ' + monto(soles) : '') + (soles !== 0 && dolares !== 0 ? ' · ' : '') + (dolares !== 0 ? 'US$ ' + monto(dolares) : '');
                div.appendChild(m);
                div.appendChild(celda('div', tipo, 'tipo'));
                div.appendChild(celda('div', 'Motivo: ' + (motivo || '(sin motivo)'), 'motivo'));
                aj.appendChild(div);
            }
            if (hayDesc) tarjeta('Descuento', '', d.DescuentoSoles, d.DescuentoDolares, d.DescuentoMotivo);
            if (hayReint) tarjeta('Reintegro', 'reintegro', d.ReintegroSoles, d.ReintegroDolares, d.ReintegroMotivo);
            document.getElementById('secAjustes').style.display = (hayDesc || hayReint) ? '' : 'none';

            if (d.NotaAdministracion) {
                document.getElementById('vNota').textContent = d.NotaAdministracion;
                document.getElementById('secNota').style.display = '';
            }

            document.getElementById('lnkPdf').href = 'DescargarPdfOrdenViaje.aspx?id=' + idOrden;
            document.getElementById('contenido').style.display = '';
            firma.redimensionar();
            actualizarEstadoBotones();
        }).fail(function () {
            document.getElementById('cargando').style.display = 'none';
            mostrarAlerta('error', 'Error de comunicación al cargar la liquidación.');
        });
    }
    cargar();

    function mostrarResultado(titulo, texto, codigo, conPdf) {
        document.getElementById('pantallaConformidad').style.display = 'none';
        document.getElementById('okTitulo').textContent = titulo;
        document.getElementById('okTexto').textContent = texto;
        document.getElementById('okCodigoBloque').style.display = codigo ? '' : 'none';
        document.getElementById('okCodigo').textContent = codigo || '';
        document.getElementById('okPdfBloque').style.display = conPdf ? '' : 'none';
        var verPdf = document.getElementById('okVerPdf');
        verPdf.href = 'DescargarPdfOrdenViaje.aspx?id=' + idOrden;
        verPdf.target = '_blank';
        document.getElementById('pantallaExito').classList.add('show');
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    // =================== Firmar la conformidad ===================
    function restaurarBoton() {
        btnFirmar.classList.remove('loading');
        btnFirmar.innerHTML = '<i class="fas fa-check-circle"></i>Firmar mi conformidad';
        actualizarEstadoBotones();
    }

    btnFirmar.addEventListener('click', function () {
        if (btnFirmar.disabled) return;
        btnFirmar.disabled = true;
        btnFirmar.classList.add('loading');
        btnFirmar.innerHTML = '<i class="fas fa-spinner"></i>Registrando tu conformidad...';

        llamar('RegistrarConformidad', {
            idOrdenViaje: idOrden, firmaPngBase64: firma.png(), contrasena: txtClave.value, huella: huella
        }).done(function (resp) {
            var r = resp && resp.d;
            if (r && r.success) {
                txtClave.value = '';
                mostrarResultado('Conformidad registrada', 'Tu liquidación quedó aprobada y el viaje, completado.', r.codigo, true);
            } else {
                if (r && r.contrasenaIncorrecta) { txtClave.value = ''; txtClave.focus(); }
                mostrarAlerta('error', (r && r.message) || 'No se pudo registrar tu conformidad.');
                restaurarBoton();
            }
        }).fail(function () {
            mostrarAlerta('error', 'Error de comunicación. Inténtalo de nuevo.');
            restaurarBoton();
        });
    });

    // =================== Observar (no estoy de acuerdo) ===================
    document.getElementById('btnMostrarObservar').addEventListener('click', function () {
        var p = document.getElementById('panelObservar');
        p.style.display = p.style.display === 'none' ? '' : 'none';
        if (p.style.display === '') document.getElementById('txtObservacion').focus();
    });

    document.getElementById('btnObservar').addEventListener('click', function () {
        var btn = this;
        var texto = document.getElementById('txtObservacion').value.trim();
        if (texto.length < 10) { mostrarAlerta('error', 'Explica con qué no estás de acuerdo (al menos 10 caracteres).'); return; }
        btn.disabled = true;
        llamar('ObservarLiquidacion', { idOrdenViaje: idOrden, observacion: texto }).done(function (resp) {
            var r = resp && resp.d;
            if (r && r.success) {
                mostrarResultado('Observación enviada', 'La administración revisará tu liquidación y te enviará una nueva versión para tu conformidad.', null, false);
            } else {
                mostrarAlerta('error', (r && r.message) || 'No se pudo enviar tu observación.');
                btn.disabled = false;
            }
        }).fail(function () {
            mostrarAlerta('error', 'Error de comunicación. Inténtalo de nuevo.');
            btn.disabled = false;
        });
    });
})();

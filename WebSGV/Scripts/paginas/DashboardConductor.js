// Panel del conductor: su viaje, la liquidación (solo gastos, pensada para el celular) y su historial.
// Las fechas de salida y llegada las pone el servidor desde el GPS; el dinero entregado lo registra
// la administración al revisar.

function escHtml(str) {
    if (str == null) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

let contadorGastosAdicionales = 0;
let liquidacionModificada = false;
let formularioEnviandose = false;

function esInternacional() {
    return $('#hfEsInternacional').val() === '1';
}

function num(v) {
    const n = parseFloat(v);
    return isFinite(n) && n > 0 ? n : 0;
}

function dinero(n, sim) {
    return sim + ' ' + n.toLocaleString('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

$(document).ready(function () {
    const $form = $('#' + SGV.pnlFormularioLiquidacion);

    // Los modales se pasan al final del <form> para que el fondo oscuro no quede debajo de las
    // pestañas; siguen dentro del form, así el botón Enviar hace su postback.
    $('#modalRevision, #modalRetirarLiquidacion').appendTo($('form').first());

    // Viajes nacionales: sin dólares ni peajes (la empresa paga los peajes a la concesionaria).
    $('body').toggleClass('dc-nacional', !esInternacional());
    if (!esInternacional()) {
        $('.solo-intl input').val('');
    }

    $form.on('input change', 'input, textarea, select', function () {
        liquidacionModificada = true;
        if ($(this).is('[type=number]')) calcularTotales();
        guardarBorradorLuego();
    });

    // El borrador se aplica después de los $(function) del servidor (orden rechazada o envío fallido),
    // que mandan sobre lo guardado en el celular.
    setTimeout(function () { Borrador.alAbrir($form); }, 0);

    $('form').on('submit', function () { formularioEnviandose = true; });

    // La barra con el total solo se ve en la pestaña Liquidar.
    $('a[data-toggle="tab"]').on('shown.bs.tab', function (e) {
        actualizarBarra($(e.target).attr('href') === '#liquidarViaje');
    });
    if (location.hash === '#liquidar') $('#liquidarViaje-tab').tab('show');
    actualizarBarra($('#liquidarViaje').hasClass('active'));

    calcularTotales();

    // Renueva la sesión mientras el conductor completa la liquidación.
    window.setInterval(function () {
        if (!$form.is(':visible')) return;
        $.ajax({
            type: 'POST',
            url: 'DashboardConductor.aspx/MantenerSesionActiva',
            data: '{}',
            contentType: 'application/json; charset=utf-8',
            dataType: 'json'
        });
    }, 5 * 60 * 1000);
});

window.addEventListener('beforeunload', function (event) {
    if (!liquidacionModificada || formularioEnviandose) return;
    event.preventDefault();
    event.returnValue = '';
});

function actualizarBarra(visible) {
    const hay = $('#' + SGV.pnlFormularioLiquidacion).length > 0;
    $('body').toggleClass('dc-con-barra', visible && hay);
}

function irALiquidar() {
    $('#liquidarViaje-tab').tab('show');
    window.scrollTo(0, 0);
}

// === Gastos ===

function toggleNota(btn) {
    const $nota = $(btn).closest('.dc-gasto').find('.dc-nota');
    const abrir = $nota.prop('hidden');
    $nota.prop('hidden', !abrir);
    $(btn).toggleClass('activo', abrir);
    if (abrir) $nota.trigger('focus');
}

function agregarGasto(datos) {
    datos = datos || {};
    const n = ++contadorGastosAdicionales;
    const $fila = $(`
        <div class="dc-gasto dc-gasto-extra" id="gastoAdicional_${n}">
            <div class="dc-gasto-cab">
                <input type="text" class="form-control dc-concepto" name="conceptoGasto_${n}" maxlength="100"
                       placeholder="¿En qué gastaste?" aria-label="Concepto del gasto">
                <button type="button" class="dc-quitar" onclick="eliminarGasto(${n})" aria-label="Quitar gasto">
                    <i class="fas fa-times"></i>
                </button>
            </div>
            <div class="dc-montos">
                <div class="dc-monto"><span>S/</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00"
                     class="gasto-soles" name="gastoSoles_${n}" aria-label="Monto en soles"></div>
                <div class="dc-monto solo-intl"><span>$</span><input type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00"
                     class="gasto-dolares" name="gastoDolares_${n}" aria-label="Monto en dólares"></div>
            </div>
            <input type="text" class="form-control dc-nota" name="descGasto_${n}" maxlength="250" placeholder="Detalle (opcional)">
        </div>`);
    $fila.find('.dc-concepto').val(datos.concepto || '');
    $fila.find('[name^="gastoSoles_"]').val(datos.soles > 0 ? Number(datos.soles).toFixed(2) : '');
    $fila.find('[name^="gastoDolares_"]').val(datos.dolares > 0 ? Number(datos.dolares).toFixed(2) : '');
    $fila.find('.dc-nota').val(datos.descripcion || '');
    $('#gastosAdicionalesBody').append($fila);
    if (!datos.concepto) $fila.find('.dc-concepto').trigger('focus');
    calcularTotales();
}

function eliminarGasto(id) {
    const $fila = $('#gastoAdicional_' + id);
    const vacio = !$fila.find('.dc-concepto').val() && !num($fila.find('.gasto-soles').val()) && !num($fila.find('.gasto-dolares').val());
    const quitar = function () { $fila.remove(); calcularTotales(); guardarBorradorLuego(); };
    if (vacio) { quitar(); return; }
    SGV.confirmar('¿Quitar este gasto?', quitar, { peligro: true, textoAceptar: 'Quitar' });
}

function calcularTotales() {
    let soles = 0, dolares = 0;
    $('.gasto-soles').each(function () { soles += num($(this).val()); });
    if (esInternacional()) $('.gasto-dolares').each(function () { dolares += num($(this).val()); });

    $('#totalGastosSoles').text(dinero(soles, 'S/'));
    $('#totalGastosDolares').text(' · ' + dinero(dolares, '$'));
    $('.dc-gasto').each(function () {
        const lleno = num($(this).find('.gasto-soles').val()) > 0 || num($(this).find('.gasto-dolares').val()) > 0;
        $(this).toggleClass('con-monto', lleno);
    });
    return { soles: soles, dolares: dolares };
}

// === Borrador en el celular ===
// Lo que el conductor va escribiendo se guarda en el navegador (por conductor y viaje), así no lo pierde
// si se le cierra el navegador, se queda sin batería o sin señal. Se borra cuando el viaje ya no se está
// liquidando (se envió) y se puede descartar a mano.
const Borrador = (function () {
    const PREFIJO = 'sgv-borrador-liquidacion-';
    const DIAS_MAXIMO = 45;
    let restauradoPorServidor = false;

    function prefijoConductor() { return PREFIJO + ($('#hfIdConductor').val() || '0') + '-'; }
    function clave() {
        const viaje = $('#hfIdViajeActivo').val();
        return viaje ? prefijoConductor() + viaje : null;
    }
    function leer(k) {
        try { const t = window.localStorage.getItem(k); return t ? JSON.parse(t) : null; } catch (e) { return null; }
    }
    function quitar(k) { try { window.localStorage.removeItem(k); } catch (e) { /* sin almacenamiento */ } }
    function claves() {
        const lista = [];
        try {
            for (let i = 0; i < window.localStorage.length; i++) {
                const k = window.localStorage.key(i);
                if (k && k.indexOf(PREFIJO) === 0) lista.push(k);
            }
        } catch (e) { /* sin almacenamiento */ }
        return lista;
    }

    function recoger($form) {
        const campos = {};
        $form.find('input[name], textarea[name], select[name]').each(function () {
            const $c = $(this);
            if ($c.is('[type=hidden],[type=submit],[type=button],[type=password]') || $c.closest('.dc-gasto-extra').length) return;
            if (this.name.indexOf('__') === 0) return;
            const v = $c.val();
            if (v) campos[this.name] = v;
        });
        const gastosAdicionales = [];
        $('#gastosAdicionalesBody .dc-gasto-extra').each(function () {
            const g = {
                concepto: ($(this).find('.dc-concepto').val() || '').trim(),
                soles: num($(this).find('.gasto-soles').val()),
                dolares: num($(this).find('.gasto-dolares').val()),
                descripcion: ($(this).find('.dc-nota').val() || '').trim()
            };
            if (g.concepto || g.soles || g.dolares || g.descripcion) gastosAdicionales.push(g);
        });
        return { campos: campos, gastosAdicionales: gastosAdicionales };
    }

    return {
        marcarRestauradoPorServidor: function () { restauradoPorServidor = true; },

        guardar: function () {
            const k = clave();
            const $form = $('#' + SGV.pnlFormularioLiquidacion);
            if (!k || !$form.length || formularioEnviandose) return;
            const datos = recoger($form);
            try {
                if ($.isEmptyObject(datos.campos) && !datos.gastosAdicionales.length) window.localStorage.removeItem(k);
                else window.localStorage.setItem(k, JSON.stringify($.extend(datos, { guardado: Date.now() })));
            } catch (e) { /* almacenamiento lleno o bloqueado: el formulario sigue funcionando */ }
        },

        descartar: function () { const k = clave(); if (k) quitar(k); },

        alAbrir: function ($form) {
            const actual = $form.length ? clave() : null;
            const limite = Date.now() - DIAS_MAXIMO * 24 * 3600 * 1000;
            // Borradores de otros viajes de este conductor (ya enviados) o muy antiguos: fuera.
            claves().forEach(function (k) {
                const d = leer(k);
                if (k === actual && d && (d.guardado || 0) > limite) return;
                if (k.indexOf(prefijoConductor()) === 0 || !d || (d.guardado || 0) < limite) quitar(k);
            });
            if (!actual || restauradoPorServidor) return;
            const datos = leer(actual);
            if (!datos) return;
            aplicarDatos(datos);
            liquidacionModificada = true;
            const hora = new Date(datos.guardado || Date.now());
            SGV.avisar('Recuperamos lo que habías escrito en tu liquidación (guardado el ' +
                hora.toLocaleDateString('es-PE') + ' a las ' + hora.toLocaleTimeString('es-PE', { hour: '2-digit', minute: '2-digit' }) +
                '). Revísalo y envíalo cuando termines.', { tipo: 'info' });
        }
    };
})();

let temporizadorBorrador = null;
function guardarBorradorLuego() {
    clearTimeout(temporizadorBorrador);
    temporizadorBorrador = setTimeout(Borrador.guardar, 600);
}
window.addEventListener('pagehide', function () { Borrador.guardar(); });
document.addEventListener('visibilitychange', function () { if (document.visibilityState === 'hidden') Borrador.guardar(); });

// Gastos que el formulario restaura por nombre de campo: orden rechazada o envío fallido.
function restaurarLiquidacion(datos) {
    Borrador.marcarRestauradoPorServidor();
    aplicarDatos(datos);
}

function aplicarDatos(datos) {
    if (!datos) return;
    $.each(datos.campos || {}, function (nombre, valor) {
        const $c = $('[name="' + nombre + '"]');
        if (!$c.length) return;
        $c.val(valor);
        if ($c.hasClass('dc-nota') && valor) {
            $c.prop('hidden', false);
            $c.closest('.dc-gasto').find('.dc-nota-btn').addClass('activo');
        }
    });
    $('#gastosAdicionalesBody').empty();
    (datos.gastosAdicionales || []).forEach(function (g) { agregarGasto(g); });
    calcularTotales();
}

// === Envío ===

function prepararDatosFinancieros() {
    const intl = esInternacional();
    const gastosFinancieros = [];

    // Peajes: solo en viajes internacionales (nacionales en S/, Ecuador en $).
    const pn = intl ? num($('#peajesNacSoles').val()) : 0;
    if (pn > 0) gastosFinancieros.push({ categoria: 'Peajes', estacion: 'Peajes Nacionales (Perú)', soles: pn, dolares: 0, observaciones: $('#descPeajesNacionales').val() || '' });
    const pe = intl ? num($('#peajesExtDolares').val()) : 0;
    if (pe > 0) gastosFinancieros.push({ categoria: 'Peajes', estacion: 'Peajes Extranjeros (Ecuador)', soles: 0, dolares: pe, observaciones: $('#descPeajesExtranjeros').val() || '' });
    $('#hfGastosFinancieros').val(JSON.stringify(gastosFinancieros));

    const adicionales = [];
    $('#gastosAdicionalesBody .dc-gasto-extra').each(function () {
        const concepto = ($(this).find('.dc-concepto').val() || '').trim();
        const soles = num($(this).find('.gasto-soles').val());
        const dolares = intl ? num($(this).find('.gasto-dolares').val()) : 0;
        if (concepto && (soles > 0 || dolares > 0)) {
            adicionales.push({ categoria: concepto, nombreCategoria: concepto, descripcion: ($(this).find('.dc-nota').val() || '').trim(), soles: soles, dolares: dolares });
        }
    });
    $('#hfGastosAdicionales').val(JSON.stringify(adicionales));
}

// Revisa lo mínimo en el celular y muestra el resumen antes de enviar (el servidor valida igual).
function abrirRevision() {
    const $fs = $('#' + SGV.txtFechaSalida), $hs = $('#' + SGV.txtHoraSalida);
    const $fl = $('#' + SGV.txtFechaLlegada), $hl = $('#' + SGV.txtHoraLlegada);

    if ($fs.length && (!$fs.val() || !$hs.val())) {
        SGV.avisar('Indica la fecha y la hora en que saliste de base.');
        ($fs.val() ? $hs : $fs).trigger('focus');
        return;
    }
    if ($fl.length && (!$fl.val() || !$hl.val())) {
        SGV.avisar('Indica la fecha y la hora en que llegaste a base.');
        ($fl.val() ? $hl : $fl).trigger('focus');
        return;
    }

    let sinConcepto = false;
    $('#gastosAdicionalesBody .dc-gasto-extra').each(function () {
        const monto = num($(this).find('.gasto-soles').val()) + num($(this).find('.gasto-dolares').val());
        if (monto > 0 && !($(this).find('.dc-concepto').val() || '').trim()) {
            sinConcepto = true;
            $(this).find('.dc-concepto').trigger('focus');
            return false;
        }
    });
    if (sinConcepto) { SGV.avisar('Escribe en qué fue el gasto que agregaste.'); return; }

    const salida = $fs.length ? formatoLocal($fs.val(), $hs.val()) : $('#' + SGV.lblSalidaGps).text() + ' (GPS)';
    const llegada = $fl.length ? formatoLocal($fl.val(), $hl.val()) : $('#' + SGV.lblLlegadaGps).text() + ' (GPS)';
    $('#revisionViaje').html(
        '<div><dt>Viaje</dt><dd>' + escHtml($('#' + SGV.lblNumeroViaje).text()) + '</dd></div>' +
        '<div><dt>Salida</dt><dd>' + escHtml(salida) + '</dd></div>' +
        '<div><dt>Llegada</dt><dd>' + escHtml(llegada) + '</dd></div>');

    const intl = esInternacional();
    let html = '';
    $('.dc-gasto').filter(function () { return $(this).css('display') !== 'none'; }).each(function () {
        const s = num($(this).find('.gasto-soles').val());
        const d = intl ? num($(this).find('.gasto-dolares').val()) : 0;
        if (s <= 0 && d <= 0) return;
        const nombre = $(this).hasClass('dc-gasto-extra')
            ? $(this).find('.dc-concepto').val()
            : $(this).find('.dc-gasto-cab label').text();
        const montos = [s > 0 ? dinero(s, 'S/') : '', d > 0 ? dinero(d, '$') : ''].filter(Boolean).join(' · ');
        html += '<li><span>' + escHtml(nombre) + '</span><strong>' + montos + '</strong></li>';
    });
    const t = calcularTotales();
    if (!html) {
        html = '<li class="dc-resumen-vacio">No registraste gastos.</li>';
    }
    html += '<li class="dc-resumen-total"><span>Total</span><strong>' + dinero(t.soles, 'S/') + (intl ? ' · ' + dinero(t.dolares, '$') : '') + '</strong></li>';
    $('#revisionGastos').html(html);

    $('#modalRevision').modal('show');
}

function formatoLocal(fecha, hora) {
    const p = (fecha || '').split('-');
    return p.length === 3 ? p[2] + '/' + p[1] + '/' + p[0] + ' · ' + hora : fecha + ' ' + hora;
}

// OnClientClick del botón "Enviar y firmar" del resumen: arma los datos y evita el doble envío.
function confirmarEnvioLiquidacion(btn) {
    if (formularioEnviandose) return false;
    prepararDatosFinancieros();
    formularioEnviandose = true;
    setTimeout(function () {
        $(btn).prop('disabled', true).val('Enviando...');
    }, 0);
    return true;
}

// === Historial ===

function verDetalleLiquidacion(idOrdenViaje) {
    window.location.href = 'DetalleOrdenViaje.aspx?id=' + encodeURIComponent(idOrdenViaje);
}

var idOrdenViajeARetirar = 0;

function abrirModalRetirar(idOrdenViaje) {
    idOrdenViajeARetirar = idOrdenViaje;
    $('#modalRetirarLiquidacion').modal('show');
}

function confirmarRetirar() {
    if (idOrdenViajeARetirar === 0) return;

    const $btn = $('#btnConfirmarRetirar');
    $btn.prop('disabled', true).text('Retirando...');

    $.ajax({
        type: 'POST',
        url: 'DashboardConductor.aspx/RetirarLiquidacion',
        data: JSON.stringify({ idOrdenViaje: idOrdenViajeARetirar }),
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            const data = response.d;
            $('#modalRetirarLiquidacion').modal('hide');
            if (data.success) {
                SGV.avisar(data.message, { tipo: 'success', trasRecargar: true });
                location.reload();
            } else {
                SGV.avisar(data.message);
            }
        },
        error: function (xhr) {
            $('#modalRetirarLiquidacion').modal('hide');
            SGV.avisar('No se pudo comunicar con el servidor. Intenta de nuevo.');
            console.error(xhr.responseText);
        },
        complete: function () {
            $btn.prop('disabled', false).text('Sí, retirar');
            idOrdenViajeARetirar = 0;
        }
    });
}

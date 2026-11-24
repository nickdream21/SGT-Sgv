// Detalle de un registro de auditoría: la grilla solo lleva data-id; al abrir se pide el detalle
// al servidor (WebMethod ObtenerDetalle, E5-8) y todo se pinta con .text(), nunca con .html().
var pedidoDetalle = 0;

function formatearJson(texto) {
    try { return JSON.stringify(JSON.parse(texto), null, 2); } catch (e) { return texto; }
}

// La sección va con d-none en el marcado (sin style=, E5-17): se muestra u oculta con la clase.
function pintarValores(seccion, pre, texto) {
    $(pre).text(texto ? formatearJson(texto) : '');
    $(seccion).toggleClass('d-none', !texto);
}

function mostrarCargandoDetalle(cargando) {
    $('#detalleCargando').toggleClass('d-none', !cargando);
    $('#detalleContenido').toggleClass('d-none', cargando);
}

// El error se muestra dentro del modal: cerrarlo durante su animación de apertura (Bootstrap ignora
// hide() mientras dura) lo dejaría abierto con "Cargando" para siempre.
function errorDetalle(mensaje) {
    $('#detalleCargando').addClass('d-none');
    $('#detalleContenido').addClass('d-none');
    $('#detalleAviso').text(mensaje).removeClass('d-none');
}

function mostrarDetalle(id) {
    var numero = ++pedidoDetalle;
    $('#detalleId').text(id);
    $('#detalleAviso').addClass('d-none').text('');
    $('#detalleDescripcion, #detalleIP, #detalleNavegador, #detalleAnteriores, #detalleNuevos').text('');
    mostrarCargandoDetalle(true);
    $('#modalDetalle').modal('show');

    $.ajax({
        url: 'Auditoria.aspx/ObtenerDetalle',
        type: 'POST',
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        data: JSON.stringify({ id: id }),
        success: function (respuesta) {
            if (numero !== pedidoDetalle) return;   // ya se pidió otro detalle
            var d = respuesta && respuesta.d;
            if (!d || !d.Ok) {
                errorDetalle((d && d.Mensaje) || 'No se pudo cargar el detalle.');
                return;
            }
            $('#detalleDescripcion').text(d.Descripcion || 'Sin descripción');
            $('#detalleIP').text(d.DireccionIP || 'N/A');
            $('#detalleNavegador').text(d.Navegador || 'N/A');
            pintarValores('#seccionAnteriores', '#detalleAnteriores', d.ValoresAnteriores);
            pintarValores('#seccionNuevos', '#detalleNuevos', d.ValoresNuevos);
            if (d.AvisoRecorte) $('#detalleAviso').text(d.AvisoRecorte).removeClass('d-none');
            mostrarCargandoDetalle(false);
        },
        error: function () {
            if (numero !== pedidoDetalle) return;
            errorDetalle('No se pudo cargar el detalle del registro. Intente de nuevo.');
        }
    });
}

// Acciones del marcado (data-accion / data-accion-input / data-accion-change): las despacha
// SiteMaster.js. Devolver false cancela el clic (y el postback del botón).
window.SGV = window.SGV || {};
SGV.acciones = Object.assign(SGV.acciones || {}, {
    mostrarDetalle: function (el) { mostrarDetalle(Number(el.getAttribute('data-id'))); }
});

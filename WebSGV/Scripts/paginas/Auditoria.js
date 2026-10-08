function mostrarDetalle(id) {
    var btn = $('[onclick="mostrarDetalle(' + id + ')"]');
    var anterior = btn.data('anterior') || '';
    var nuevo = btn.data('nuevo') || '';
    var desc = btn.data('desc') || 'Sin descripción';
    var ip = btn.data('ip') || 'N/A';
    var nav = btn.data('nav') || 'N/A';

    $('#detalleId').text(id);
    $('#detalleDescripcion').text(desc);
    $('#detalleIP').text(ip);
    $('#detalleNavegador').text(nav);

    if (anterior) {
        try { anterior = JSON.stringify(JSON.parse(anterior), null, 2); } catch (e) { }
        $('#detalleAnteriores').text(anterior);
        $('#seccionAnteriores').show();
    } else {
        $('#seccionAnteriores').hide();
    }

    if (nuevo) {
        try { nuevo = JSON.stringify(JSON.parse(nuevo), null, 2); } catch (e) { }
        $('#detalleNuevos').text(nuevo);
        $('#seccionNuevos').show();
    } else {
        $('#seccionNuevos').hide();
    }

    $('#modalDetalle').modal('show');
}

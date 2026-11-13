// Descuentos y reintegros a conductores: elegir las liquidaciones pendientes de un conductor y
// registrar cómo se saldaron; anular un registro con su motivo.
$(function () {
    function num(t) { return parseFloat(String(t).replace(/,/g, '')) || 0; }
    function dinero(n) { return 'S/ ' + Math.abs(n).toLocaleString('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }

    $('#modalSaldar, #modalAnular').appendTo($('form').first());

    $('.sc-btn-saldar').on('click', function () {
        const id = $(this).data('conductor');
        const $panel = $('.sc-conductor[data-conductor="' + id + '"]');
        const ids = [];
        let neto = 0;
        $panel.find('.sc-sel:checked').each(function () {
            ids.push(this.value);
            neto += num($(this).closest('tr').find('td').eq(6).text());
        });
        if (!ids.length) { SGV.avisar('Marca al menos una liquidación pendiente de este conductor.'); return; }
        $('#' + SGV.hfConductor).val(id);
        $('#' + SGV.hfOrdenes).val(ids.join(','));
        $('#saldarNombre').text($(this).data('nombre'));
        $('#saldarResumen').text(ids.length + ' liquidación(es): ' +
            (neto > 0 ? 'se le descuenta ' + dinero(neto) : neto < 0 ? 'se le reintegra ' + dinero(neto) : 'quedan cuadradas') + '.');
        $('#modalSaldar').modal('show');
    });

    $('.sc-btn-anular').on('click', function () {
        $('#' + SGV.hfPagoAnular).val($(this).data('pago'));
        $('#' + SGV.txtMotivoAnular).val('');
        $('#modalAnular').modal('show');
    });
});

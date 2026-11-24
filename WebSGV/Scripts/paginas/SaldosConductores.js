// Descuentos y reintegros a conductores: elegir las liquidaciones pendientes de un conductor y
// registrar cómo se saldaron; anular un registro con su motivo.
$(function () {
    // data-saldo-soles / data-saldo-dolares: saldo de la fila con punto decimal (positivo = se le descuenta).
    function num(t) { return parseFloat(t) || 0; }
    function monto(moneda, n) { return moneda + ' ' + Math.abs(n).toLocaleString('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
    // Mismo texto que SaldosConductorService.TextoSaldo, en minúscula tras los dos puntos (M-070: por moneda).
    function textoSaldo(s, d) {
        s = Math.round(s * 100) / 100; d = Math.round(d * 100) / 100;
        if (!s && !d) return 'quedan cuadradas';
        function sentido(n) { return n > 0 ? 'se le descuenta' : 'se le reintegra'; }
        if (!d) return sentido(s) + ' ' + monto('S/', s);
        if (!s) return sentido(d) + ' ' + monto('US$', d);
        return (s > 0) === (d > 0)
            ? sentido(s) + ' ' + monto('S/', s) + ' y ' + monto('US$', d)
            : sentido(s) + ' ' + monto('S/', s) + ' y ' + sentido(d) + ' ' + monto('US$', d);
    }

    $('#modalSaldar, #modalAnular').appendTo($('form').first());

    $('.sc-btn-saldar').on('click', function () {
        const id = $(this).data('conductor');
        const $panel = $('.sc-conductor[data-conductor="' + id + '"]');
        const ids = [];
        let soles = 0, dolares = 0;
        $panel.find('.sc-sel:checked').each(function () {
            ids.push(this.value);
            const fila = $(this).closest('tr')[0];
            soles += num(fila.getAttribute('data-saldo-soles'));
            dolares += num(fila.getAttribute('data-saldo-dolares'));
        });
        if (!ids.length) { SGV.avisar('Marca al menos una liquidación pendiente de este conductor.'); return; }
        $('#' + SGV.hfConductor).val(id);
        $('#' + SGV.hfOrdenes).val(ids.join(','));
        $('#saldarNombre').text($(this).data('nombre'));
        $('#saldarResumen').text(ids.length + ' liquidación(es): ' + textoSaldo(soles, dolares) + '.');
        $('#modalSaldar').modal('show');
    });

    $('.sc-btn-anular').on('click', function () {
        $('#' + SGV.hfPagoAnular).val($(this).data('pago'));
        $('#' + SGV.txtMotivoAnular).val('');
        $('#modalAnular').modal('show');
    });
});

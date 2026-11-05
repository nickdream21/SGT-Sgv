// Facturas de flete: abrir la pestaña guardada y recordarla en el postback.
(function () {
    var hf = document.getElementById(SGV.hfPestana);
    var pestana = (hf && hf.value) || 'conciliacion';
    var enlace = document.querySelector('.ff-pestanas a[data-pestana="' + pestana + '"]') ||
                 document.querySelector('.ff-pestanas a[data-pestana="conciliacion"]');
    if (!window.jQuery || !enlace) return;

    jQuery('.ff-pestanas a[data-toggle="tab"]').on('shown.bs.tab', function (e) {
        if (hf) hf.value = e.target.getAttribute('data-pestana');
    });
    jQuery(enlace).tab('show');
})();

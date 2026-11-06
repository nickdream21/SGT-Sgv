// Facturas de flete: abrir la pestaña guardada y recordarla en el postback; cuenta de filas en cada pestaña;
// filtro por estado (todos / para revisar / conformes) y búsqueda de texto sobre las tablas.
(function () {
    var hf = document.getElementById(SGV.hfPestana);
    var pestana = (hf && hf.value) || 'conciliacion';
    var enlace = document.querySelector('.ff-pestanas a[data-pestana="' + pestana + '"]') ||
                 document.querySelector('.ff-pestanas a[data-pestana="conciliacion"]');
    if (!window.jQuery || !enlace) return;

    var filtro = 'todos';
    var buscar = document.querySelector('.ff-buscar input');
    var contador = document.querySelector('.ff-contador');

    function filas(panel) { return panel ? Array.prototype.slice.call(panel.querySelectorAll('table.ff-tabla > tbody > tr')) : []; }
    function panelActivo() { return document.querySelector('.ff-contenido .tab-pane.active'); }
    function normalizar(s) { return (s || '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, ''); }

    function aplicar() {
        var texto = normalizar(buscar ? buscar.value.trim() : '');
        document.querySelectorAll('.ff-contenido .tab-pane').forEach(function (panel) {
            var lista = filas(panel), visibles = 0;
            lista.forEach(function (tr) {
                var estado = tr.getAttribute('data-estado') || 'ok';
                var pasa = (filtro === 'todos' || filtro === estado) && (!texto || normalizar(tr.textContent).indexOf(texto) >= 0);
                tr.classList.toggle('ff-oculta', !pasa);
                if (pasa) visibles++;
            });
            var aviso = panel.querySelector('.ff-sin-coincidencias');
            if (lista.length > 0 && visibles === 0) {
                if (!aviso) {
                    aviso = document.createElement('div');
                    aviso.className = 'ff-sin-coincidencias';
                    aviso.textContent = 'Ninguna fila coincide con el filtro.';
                    var tabla = panel.querySelector('table.ff-tabla');
                    tabla.parentNode.insertBefore(aviso, tabla.nextSibling);
                }
            } else if (aviso) aviso.remove();

            var tab = document.querySelector('.ff-pestanas a[href="#' + panel.id + '"] .ff-cuenta');
            if (tab) tab.textContent = lista.length === 0 ? '' : (visibles === lista.length ? lista.length : visibles + '/' + lista.length);
        });
        var activo = filas(panelActivo());
        if (contador) {
            var n = activo.filter(function (tr) { return !tr.classList.contains('ff-oculta'); }).length;
            contador.textContent = activo.length === 0 ? '' : 'Mostrando ' + n + ' de ' + activo.length + ' fila(s)';
        }
    }

    document.querySelectorAll('.ff-filtro [data-filtro]').forEach(function (b) {
        b.addEventListener('click', function () {
            filtro = b.getAttribute('data-filtro');
            document.querySelectorAll('.ff-filtro [data-filtro]').forEach(function (x) { x.classList.toggle('active', x === b); });
            aplicar();
        });
    });
    if (buscar) {
        buscar.addEventListener('input', aplicar);
        // Enter en la búsqueda no debe enviar el formulario (postback).
        buscar.addEventListener('keydown', function (e) { if (e.key === 'Enter') e.preventDefault(); });
    }

    jQuery('.ff-pestanas a[data-toggle="tab"]').on('shown.bs.tab', function (e) {
        if (hf) hf.value = e.target.getAttribute('data-pestana');
        aplicar();
    });
    jQuery(enlace).tab('show');
    aplicar();
})();

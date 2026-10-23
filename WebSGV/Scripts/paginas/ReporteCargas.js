// Reporte de cargas: expandir / contraer todos los pedidos
(function () {
    function marcar(abrir) {
        document.querySelectorAll('details.rc-grupo').forEach(function (d) { d.open = abrir; });
    }
    var expandir = document.getElementById('rcExpandir');
    var contraer = document.getElementById('rcContraer');
    if (expandir) expandir.addEventListener('click', function () { marcar(true); });
    if (contraer) contraer.addEventListener('click', function () { marcar(false); });
})();

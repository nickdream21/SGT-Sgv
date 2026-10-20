// Detalle del viaje: evidencia de cada hito, corrección manual y datos de cabecera.
(function () {
    var lateralTitulo = document.getElementById('lateralTitulo');
    var lateralHito = document.getElementById('lateralHito');
    var panelEvidencia = document.getElementById('panelEvidencia');
    var panelCorregir = document.getElementById('panelCorregir');

    function texto(id, valor) {
        var el = document.getElementById(id);
        if (el) el.textContent = valor || '—';
    }

    function seleccionar(li) {
        document.querySelectorAll('.sx-hito.sx-sel').forEach(function (x) { x.classList.remove('sx-sel'); });
        li.classList.add('sx-sel');
        lateralHito.textContent = li.dataset.nombre;
    }

    function verEvidencia(li) {
        seleccionar(li);
        lateralTitulo.textContent = 'Evidencia del hito';
        panelCorregir.hidden = true;
        panelEvidencia.hidden = false;
        texto('evHora', li.dataset.hora);
        texto('evOrigen', li.dataset.origen);
        texto('evRegla', li.dataset.regla);
        texto('evGps', li.dataset.gps);
        texto('evCoords', li.dataset.coords);
        var mapa = document.getElementById('evMapa');
        mapa.hidden = !li.dataset.mapa;
        if (li.dataset.mapa) mapa.href = li.dataset.mapa;
        var motivo = document.getElementById('evMotivo');
        motivo.hidden = !li.dataset.motivo;
        motivo.textContent = li.dataset.motivo ? 'Corregido a mano: ' + li.dataset.motivo + '. El GPS ya no cambia esta hora.' : '';
    }

    function corregir(li) {
        seleccionar(li);
        lateralTitulo.textContent = 'Corregir hora';
        panelEvidencia.hidden = true;
        panelCorregir.hidden = false;
        document.getElementById('corHoraActual').textContent = (li.dataset.hora || 'sin hora') + ' (' + li.dataset.origen + ')';
        document.getElementById(SGV.hdnColumna).value = li.dataset.columna;
        var hora = document.getElementById(SGV.txtNuevaHora);
        hora.value = li.dataset.horaInput || '';
        document.getElementById(SGV.txtMotivo).value = '';
        hora.focus();
    }

    document.querySelectorAll('.sx-hito').forEach(function (li) {
        li.querySelector('.sx-ver').addEventListener('click', function () { verEvidencia(li); });
        li.querySelector('.sx-corregir').addEventListener('click', function () { corregir(li); });
    });

    var cancelar = document.getElementById('btnCancelarCorreccion');
    if (cancelar) cancelar.addEventListener('click', function () {
        var sel = document.querySelector('.sx-hito.sx-sel');
        if (sel) verEvidencia(sel);
    });

    // Abre el primer hito con hora (o el que se acaba de corregir) para que el panel no quede vacío.
    var inicial = (SGV.columnaInicial && document.querySelector('.sx-hito[data-columna="' + SGV.columnaInicial + '"]'))
        || document.querySelector('.sx-hito[data-hora]:not([data-hora=""])');
    if (inicial && panelEvidencia) verEvidencia(inicial);

    var editar = document.getElementById('btnEditarDatos');
    var seccion = document.getElementById('seccionDatos');
    if (editar && seccion) editar.addEventListener('click', function () {
        var abierto = seccion.classList.toggle('sx-abierto');
        editar.setAttribute('aria-expanded', abierto ? 'true' : 'false');
    });
})();

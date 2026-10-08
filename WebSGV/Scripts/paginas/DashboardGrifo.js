function cambiarTab(tab) {
    var viajes = document.getElementById('seccionViajes');
    var historial = document.getElementById('seccionHistorial');
    var retornos = document.getElementById('seccionRetornos');
    var tabV = document.getElementById('tabViajes');
    var tabH = document.getElementById('tabHistorial');
    var tabR = document.getElementById('tabRetornos');
    var hf = document.getElementById('hfTabActiva');

    viajes.className = 'gf-tab-content';
    historial.className = 'gf-tab-content';
    retornos.className = 'gf-tab-content';
    tabV.className = 'gf-tab';
    tabH.className = 'gf-tab';
    tabR.className = 'gf-tab';

    if (tab === 'historial') {
        historial.className = 'gf-tab-content active';
        tabH.className = 'gf-tab active';
    } else if (tab === 'retornos') {
        retornos.className = 'gf-tab-content active';
        tabR.className = 'gf-tab active';
    } else {
        viajes.className = 'gf-tab-content active';
        tabV.className = 'gf-tab active';
    }
    hf.value = tab;
}

// Restore tab after postback
(function () {
    var hf = document.getElementById('hfTabActiva');
    if (hf && (hf.value === 'historial' || hf.value === 'retornos')) {
        cambiarTab(hf.value);
    }
})();

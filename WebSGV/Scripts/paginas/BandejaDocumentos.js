// Bandeja de documentos: zona de arrastre (muestra los archivos elegidos), datos del despacho nuevo y aviso
// mientras se lee o registra.
(function () {
    // "+ Crear despacho nuevo": muestra chofer, tracto, carreta y fecha de esa guía.
    document.addEventListener('change', function (e) {
        var sel = e.target;
        if (!sel.classList || !sel.classList.contains('bd-selector') || !sel.hasAttribute('data-indice')) return;
        var fila = document.querySelector('.bd-fila-nuevo[data-indice="' + sel.getAttribute('data-indice') + '"]');
        if (fila) fila.hidden = sel.value !== 'nuevo';
    });
})();

(function () {
    var zona = document.getElementById('bdZona');
    var input = document.querySelector('.bd-zona-input');
    var lista = document.getElementById('bdArchivos');
    if (!zona || !input || !lista) return;

    function mostrar() {
        lista.innerHTML = '';
        Array.prototype.forEach.call(input.files || [], function (f) {
            var s = document.createElement('span');
            s.textContent = f.name;
            lista.appendChild(s);
        });
    }

    input.addEventListener('change', mostrar);
    ['dragenter', 'dragover'].forEach(function (ev) {
        zona.addEventListener(ev, function () { zona.classList.add('bd-zona-activa'); });
    });
    ['dragleave', 'drop'].forEach(function (ev) {
        zona.addEventListener(ev, function () { zona.classList.remove('bd-zona-activa'); });
    });
    // El input cubre toda la zona: al soltar, el navegador pone los archivos en él.
    zona.addEventListener('drop', function () { setTimeout(mostrar, 0); });

    function ocupado(selector, texto) {
        var b = document.querySelector(selector);
        if (!b) return;
        b.form && b.form.addEventListener('submit', function (e) {
            if (e.submitter && e.submitter !== b) return;
            setTimeout(function () { b.disabled = true; b.value = texto; }, 0);
        });
    }
    ocupado('.bd-btn-leer', 'Leyendo…');
    ocupado('.bd-btn-registrar', 'Registrando…');
})();

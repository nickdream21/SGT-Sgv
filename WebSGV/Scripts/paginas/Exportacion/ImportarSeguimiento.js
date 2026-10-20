// Importación del seguimiento: elegir o arrastrar el Excel.
(function () {
    var zona = document.getElementById('zonaArchivo');
    var input = document.getElementById(SGV.fileExcel);
    var info = document.getElementById('infoArchivo');
    if (!zona || !input) return;

    function mostrarNombre() {
        if (input.files && input.files[0]) {
            var f = input.files[0];
            info.textContent = 'Archivo: ' + f.name + ' (' + (f.size / 1024 / 1024).toFixed(2) + ' MB)';
        } else {
            info.textContent = 'Ningún archivo seleccionado';
        }
    }

    zona.addEventListener('click', function () { input.click(); });
    zona.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); input.click(); }
    });
    input.addEventListener('change', mostrarNombre);
    ['dragover', 'dragenter'].forEach(function (ev) {
        zona.addEventListener(ev, function (e) { e.preventDefault(); zona.classList.add('sx-arrastrando'); });
    });
    ['dragleave', 'drop'].forEach(function (ev) {
        zona.addEventListener(ev, function (e) { e.preventDefault(); zona.classList.remove('sx-arrastrando'); });
    });
    zona.addEventListener('drop', function (e) {
        if (e.dataTransfer.files.length) { input.files = e.dataTransfer.files; mostrarNombre(); }
    });
})();

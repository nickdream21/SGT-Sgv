function updateFileName(input) {
    const fileInfo = document.getElementById('fileInfo');
    const btnProcesar = document.getElementById(SGV.btnProcesar);

    if (input.files && input.files[0]) {
        const fileName = input.files[0].name;
        fileInfo.innerHTML = 'Archivo seleccionado: <strong>' + SGV.escaparHtml(fileName) + '</strong>';
        btnProcesar.disabled = false;
    } else {
        fileInfo.innerHTML = 'Ningún archivo seleccionado';
        btnProcesar.disabled = true;
    }
}

// Funcionalidad de drag & drop
const dropZone = document.getElementById('dropZone');

dropZone.addEventListener('dragover', function(e) {
    e.preventDefault();
    e.stopPropagation();
    this.style.backgroundColor = '#f8f9fa';
});

dropZone.addEventListener('dragleave', function(e) {
    e.preventDefault();
    e.stopPropagation();
    this.style.backgroundColor = '';
});

dropZone.addEventListener('drop', function(e) {
    e.preventDefault();
    e.stopPropagation();
    this.style.backgroundColor = '';

    const fileUpload = document.getElementById(SGV.fileUpload);
    fileUpload.files = e.dataTransfer.files;
    updateFileName(fileUpload);
});

// Acciones del marcado (data-accion / data-accion-input / data-accion-change): las despacha
// SiteMaster.js. Devolver false cancela el clic (y el postback del botón).
window.SGV = window.SGV || {};
SGV.acciones = Object.assign(SGV.acciones || {}, {
    // Clic en la zona: abre el selector. El clic que eso produce en el propio input no se repite.
    elegirArchivo: function (el, e) {
        var input = document.getElementById(SGV.fileUpload);
        if (input && e.target !== input) input.click();
    },
    mostrarArchivoElegido: function (el) { updateFileName(el); }
});

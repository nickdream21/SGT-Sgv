function updateFileName(input) {
    const fileInfo = document.getElementById('fileInfo');
    const btnProcesar = document.getElementById(SGV.btnProcesar);

    if (input.files && input.files[0]) {
        const fileName = input.files[0].name;
        fileInfo.innerHTML = 'Archivo seleccionado: <strong>' + fileName + '</strong>';
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

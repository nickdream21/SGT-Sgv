// Inicializar funcionalidad de carga de archivos
document.addEventListener("DOMContentLoaded", function() {
    initFileUpload();
});

function initFileUpload() {
    const fileInput = document.getElementById(SGV.fileUploadCPIC);

    if (fileInput) {
        fileInput.addEventListener('change', function (e) {
            const file = e.target.files[0];
            const preview = document.getElementById('file-preview');
            const fileName = document.getElementById('file-name');
            const fileSize = document.getElementById('file-size');

            if (file) {
                // Validar tamaño (50MB máximo)
                const maxSize = 50 * 1024 * 1024; // 50MB en bytes
                if (file.size > maxSize) {
                    alert('El archivo es demasiado grande. El tamaño máximo permitido es 50MB.');
                    clearFileSelection();
                    return;
                }

                // Validar tipo de archivo
                const allowedTypes = ['.pdf', '.doc', '.docx', '.jpg', '.jpeg', '.png'];
                const fileExtension = '.' + file.name.split('.').pop().toLowerCase();

                if (!allowedTypes.includes(fileExtension)) {
                    alert('Tipo de archivo no permitido. Use: PDF, DOC, DOCX, JPG, PNG');
                    clearFileSelection();
                    return;
                }

                // Mostrar información del archivo
                fileName.textContent = file.name;
                fileSize.textContent = `(${formatFileSize(file.size)})`;
                preview.style.display = 'block';
            } else {
                preview.style.display = 'none';
            }
        });
    }
}

// Limpiar selección de archivo
function clearFileSelection() {
    const fileInput = document.getElementById(SGV.fileUploadCPIC);
    const preview = document.getElementById('file-preview');

    if (fileInput) fileInput.value = '';
    if (preview) preview.style.display = 'none';
}

// Formatear tamaño de archivo
function formatFileSize(bytes) {
    if (bytes === 0) return '0 Bytes';

    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

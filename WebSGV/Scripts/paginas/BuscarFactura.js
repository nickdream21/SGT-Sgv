// Script para validar números de pedido al editar y funcionalidad de upload
document.addEventListener('DOMContentLoaded', function () {
    // Funcionalidad existente para validación de números de pedido
    const inputNumPedido = document.getElementById(SGV.txtNumPedido);
    if (inputNumPedido) {
        inputNumPedido.addEventListener('keypress', function (e) {
            const key = e.key;
            // Permitir solo números y teclas de control
            if (!/[0-9]/.test(key) && e.keyCode !== 8) {
                e.preventDefault();
            }
        });
        inputNumPedido.addEventListener('input', function () {
            // Eliminar caracteres no numéricos
            this.value = this.value.replace(/[^0-9]/g, '');
            // Limitar a 10 dígitos
            if (this.value.length > 10) {
                this.value = this.value.slice(0, 10);
            }
        });
    }

    // NUEVA: Funcionalidad para upload de archivos
    setupFileUpload();
});

// NUEVA FUNCIÓN: Configurar funcionalidad de upload de archivos
function setupFileUpload() {
    const fileInput = document.getElementById(SGV.fileUploadFactura);

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
                if (fileName && fileSize && preview) {
                    fileName.textContent = file.name;
                    fileSize.textContent = `(${formatFileSize(file.size)})`;
                    preview.style.display = 'block';
                }
            } else {
                if (preview) {
                    preview.style.display = 'none';
                }
            }
        });
    }
}

// NUEVA FUNCIÓN: Limpiar selección de archivo
function clearFileSelection() {
    const fileInput = document.getElementById(SGV.fileUploadFactura);
    const preview = document.getElementById('file-preview');

    if (fileInput) {
        fileInput.value = '';
    }
    if (preview) {
        preview.style.display = 'none';
    }
}

// NUEVA FUNCIÓN: Formatear tamaño de archivo
function formatFileSize(bytes) {
    if (bytes === 0) return '0 Bytes';

    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

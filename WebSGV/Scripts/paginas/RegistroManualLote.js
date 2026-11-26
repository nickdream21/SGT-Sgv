// RegistroManualLote (E6-27, antes en ListaDespachos.js).
// ---- Documentos del lote: nombre del archivo elegido, botón Adjuntar y aviso de guías sin guardar ----
function iniciarDocumentosLote() {
    $('.ld-doc-subir').each(function () {
        var zona = $(this);
        var input = zona.find('input[type=file]');
        var etiqueta = zona.find('.ld-doc-elegir');
        var texto = zona.find('.ld-doc-elegir-texto');
        var boton = zona.find('.ld-doc-btn');
        if (!input.length) return;

        function actualizar() {
            var f = input[0].files && input[0].files[0];
            etiqueta.toggleClass('ld-con-archivo', !!f);
            texto.text(f ? f.name : 'Elegir PDF o foto (máx. 10 MB)');
            boton.toggleClass('ld-desactivado', !f);
        }
        input.off('change.ld').on('change.ld', actualizar);
        actualizar();
    });

    $('.ld-guia-input').each(function () {
        var campo = $(this);
        campo.data('original', campo.val());
        campo.off('input.ld').on('input.ld', function () {
            campo.toggleClass('ld-modificado', campo.val() !== campo.data('original'));
        });
    });
}

$(document).ready(iniciarDocumentosLote);
Sys.WebForms.PageRequestManager.getInstance().add_endRequest(function () {
    iniciarDocumentosLote();
    var panel = document.querySelector('.ld-docs-page');
    if (panel && panel.getBoundingClientRect().top < 0) panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
});

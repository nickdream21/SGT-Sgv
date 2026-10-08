// Hook: sincroniza la clase Bootstrap is-invalid con el resultado de cada validator de ASP.NET
(function () {
    function hookValidatorDisplay() {
        if (typeof ValidatorUpdateDisplay === 'undefined') return;
        var _orig = ValidatorUpdateDisplay;
        ValidatorUpdateDisplay = function (val) {
            _orig(val);
            var ctrl = document.getElementById(val.controltovalidate);
            if (!ctrl) return;
            if (!val.isvalid) {
                ctrl.classList.add('is-invalid');
            } else {
                ctrl.classList.remove('is-invalid');
            }
            // Select2: propagar al contenedor visual
            if (ctrl.classList.contains('select2-searchable') || ctrl.tagName === 'SELECT') {
                var container = ctrl.nextElementSibling;
                if (container && container.classList.contains('select2-container')) {
                    var selection = container.querySelector('.select2-selection');
                    if (selection) {
                        selection.style.borderColor = val.isvalid ? '' : '#dc3545';
                        selection.style.boxShadow = val.isvalid ? '' : '0 0 0 0.2rem rgba(220,53,69,.15)';
                    }
                }
            }
        };
    }

    // Aplicar al cargar y tras cada postback parcial del UpdatePanel
    hookValidatorDisplay();
    if (typeof Sys !== 'undefined') {
        Sys.WebForms.PageRequestManager.getInstance().add_endRequest(function () {
            hookValidatorDisplay();
        });
    }
})();

// Limpiar is-invalid cuando el usuario empieza a corregir un campo
$(document).on('input change', '.form-control.is-invalid, .form-select.is-invalid', function () {
    if (this.value.trim() !== '') {
        this.classList.remove('is-invalid');
        // Select2
        var container = this.nextElementSibling;
        if (container && container.classList.contains('select2-container')) {
            var selection = container.querySelector('.select2-selection');
            if (selection) { selection.style.borderColor = ''; selection.style.boxShadow = ''; }
        }
    }
});

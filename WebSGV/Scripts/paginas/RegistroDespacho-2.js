// Hook: sincroniza la clase Bootstrap is-invalid con el resultado de cada validator de ASP.NET.
// Select2 se pinta solo: RegistroDespacho.css tiene "select.is-invalid + .select2-container" (M-143: sin colores en línea).
(function () {
    function hookValidatorDisplay() {
        if (typeof ValidatorUpdateDisplay === 'undefined') return;
        var _orig = ValidatorUpdateDisplay;
        ValidatorUpdateDisplay = function (val) {
            _orig(val);
            var ctrl = document.getElementById(val.controltovalidate);
            if (!ctrl) return;
            ctrl.classList.toggle('is-invalid', !val.isvalid);
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
$(document).on('input change', '.form-control.is-invalid, .custom-select.is-invalid', function () {
    if (this.value.trim() !== '') {
        this.classList.remove('is-invalid');
    }
});

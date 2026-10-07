// Recurso "jquery" del ScriptManager (App_Start/BundleConfig.cs).
// La validación unobtrusive de WebForms lo inyecta en las páginas con validadores, DESPUÉS de los
// scripts del head. Site.Master ya cargó jQuery 3.6.4 + Bootstrap 4.6.2 + jQuery UI: una segunda copia
// de jQuery reemplazaría $ y se perderían .modal(), .datepicker(), etc.
// Por eso solo se carga jQuery si la página todavía no lo tiene (p. ej. una página sin Site.Master).
if (!window.jQuery) {
    document.write('<script src="https://code.jquery.com/jquery-3.6.4.min.js"><\/script>');
}

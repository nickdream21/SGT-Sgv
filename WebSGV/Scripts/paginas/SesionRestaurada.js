// ── Página restaurada con "Atrás" tras cerrar sesión ───────────────
// Chrome (sobre todo Android) guarda la página entera en memoria (bfcache) y la muestra al pulsar
// Atrás aunque la respuesta tenga no-store: después de "Cerrar sesión" se volvían a ver el nombre y
// los datos. Las páginas con sesión traen en el <html> data-sgv-usuario, data-sgv-sesion y
// data-sgv-login (SesionRestauradaHelper; las públicas no, y aquí no se hace nada). Al salir
// (pagehide guardada en bfcache) la página se tapa con una capa opaca, para que vuelva ya tapada; al
// volver (pageshow persisted) se pregunta a SesionActiva.ashx:
//   204 → se destapa;
//   401 (sin sesión) → Login?error=sesion;
//   409 (otro usuario), 403 (cambio de contraseña), error o demora → Login sin parámetro: con la
//   sesión viva lleva al inicio del rol sin pasar por el rompe-bucles de ?error=sesion, que la cerraría.
// Siempre location.replace, nunca reload (volvería a enviar el formulario). Una carga normal desde el
// historial no se comprueba: con no-store la página vino del servidor, que ya validó la sesión.
(function () {
    var html = document.documentElement;
    var usuario = html.getAttribute('data-sgv-usuario');
    var urlSesion = html.getAttribute('data-sgv-sesion');
    var urlLogin = html.getAttribute('data-sgv-login');
    if (!usuario || !urlSesion || !urlLogin) return;

    var CLASE = 'sgv-comprobando-sesion';
    var ID_CAPA = 'sgvCapaSesion';

    // Capa opaca encima de todo (también de los hijos con visibility: visible y de los iframes), con
    // estilos puestos desde el script: no depende de ninguna hoja de estilos ni suma !important.
    function ocultar() {
        html.classList.add(CLASE);
        if (document.getElementById(ID_CAPA)) return;
        var capa = document.createElement('div');
        capa.id = ID_CAPA;
        capa.setAttribute('aria-hidden', 'true');
        var s = capa.style;
        s.position = 'fixed';
        s.top = s.left = s.right = s.bottom = '0';
        s.zIndex = '2147483647';
        s.background = '#fff';
        s.visibility = 'visible';
        s.display = 'block';
        html.appendChild(capa);
    }
    function mostrar() {
        html.classList.remove(CLASE);
        var capa = document.getElementById(ID_CAPA);
        if (capa) capa.parentNode.removeChild(capa);
    }
    function irA(url) {
        location.replace(url);
    }

    function comprobar() {
        ocultar();
        var xhr = new XMLHttpRequest();
        try {
            xhr.open('GET', urlSesion + '?u=' + encodeURIComponent(usuario) + '&t=' + Date.now(), true);
            xhr.timeout = 10000;
            xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
            xhr.onload = function () {
                if (xhr.status === 204) mostrar();
                else if (xhr.status === 401) irA(urlLogin + '?error=sesion');
                else irA(urlLogin);
            };
            xhr.onerror = function () { irA(urlLogin); };
            xhr.ontimeout = function () { irA(urlLogin); };
            xhr.send();
        } catch (e) {
            irA(urlLogin);
        }
    }

    window.addEventListener('pagehide', function (e) { if (e.persisted) ocultar(); });
    window.addEventListener('pageshow', function (e) { if (e.persisted) comprobar(); });
})();

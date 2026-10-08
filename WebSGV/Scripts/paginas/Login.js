function togglePassword() {
    var input = document.getElementById(SGV.txtPassword);
    var icon = document.getElementById('iconTogglePass');
    if (input.type === 'password') {
        input.type = 'text';
        icon.classList.replace('fa-eye', 'fa-eye-slash');
    } else {
        input.type = 'password';
        icon.classList.replace('fa-eye-slash', 'fa-eye');
    }
}

// Autofocus en el primer campo vacío
(function () {
    var user = document.getElementById(SGV.txtUsername);
    var pass = document.getElementById(SGV.txtPassword);
    if (user && !user.value) {
        user.focus();
    } else if (pass) {
        pass.focus();
    }
})();

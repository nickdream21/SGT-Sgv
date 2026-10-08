function calcularDiferencias() {
    var odoIni = parseFloat(document.getElementById(SGV.txtOdometroComienzo).value) || 0;
    var odoFin = parseFloat(document.getElementById(SGV.txtOdometroTermino).value) || 0;
    var odoResult = document.getElementById(SGV.txtOdometroKmHoras);
    if (odoIni > 0 && odoFin > 0 && odoFin >= odoIni) {
        odoResult.value = (odoFin - odoIni).toFixed(1);
    } else {
        odoResult.value = '';
    }

    var horoIni = parseFloat(document.getElementById(SGV.txtHorometroComienzo).value) || 0;
    var horoFin = parseFloat(document.getElementById(SGV.txtHorometroTermino).value) || 0;
    var horoResult = document.getElementById(SGV.txtHorometroHoras);
    if (horoIni > 0 && horoFin > 0 && horoFin >= horoIni) {
        horoResult.value = (horoFin - horoIni).toFixed(1);
    } else {
        horoResult.value = '';
    }
}

function validarFormulario() {
    var fecha = document.getElementById(SGV.txtFechaParte).value;
    if (!fecha) {
        alert('Por favor, ingrese la fecha del parte.');
        return false;
    }

    var horoIni = document.getElementById(SGV.txtHorometroComienzo).value;
    var horoFin = document.getElementById(SGV.txtHorometroTermino).value;
    if (horoIni && horoFin) {
        if (parseFloat(horoFin) < parseFloat(horoIni)) {
            alert('El hor\u00f3metro de t\u00e9rmino no puede ser menor al de comienzo.');
            return false;
        }
    }

    var odoIni = document.getElementById(SGV.txtOdometroComienzo).value;
    var odoFin = document.getElementById(SGV.txtOdometroTermino).value;
    if (odoIni && odoFin) {
        if (parseFloat(odoFin) < parseFloat(odoIni)) {
            alert('El od\u00f3metro de t\u00e9rmino no puede ser menor al de comienzo.');
            return false;
        }
    }

    return confirm('\u00bfEst\u00e1 seguro de registrar este Parte Diario de Trabajo?');
}

// Touch: al enfocar un input, scroll suave al campo
document.addEventListener('DOMContentLoaded', function () {
    var inputs = document.querySelectorAll('.dash-op input[type="text"], .dash-op textarea, .dash-op input[type="number"], .dash-op input[type="date"]');
    inputs.forEach(function (input) {
        input.addEventListener('focus', function () {
            var self = this;
            setTimeout(function () {
                self.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }, 300);
        });
    });
});

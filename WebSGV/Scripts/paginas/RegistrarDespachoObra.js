function recalcularTotales() {
    var s = parseFloat(document.getElementById(SGV.txtGalonesSalida).value);
    var r = parseFloat(document.getElementById(SGV.txtGalonesRetorno).value);
    if (isNaN(s)) s = 0;
    if (isNaN(r)) r = 0;
    var abast = s - r;
    if (abast < 0) abast = 0;
    document.getElementById('lblAbastecido').innerText = abast.toFixed(2);
}

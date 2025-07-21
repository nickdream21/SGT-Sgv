function toggleRutaDetails() {
    console.log("toggleRutaDetails llamada");
    const ruta = document.getElementById('ddlRuta').value;
    const rutaDetails = document.getElementById('rutaDetails');

    if (ruta === "2") {
        console.log("Mostrando rutaDetails");
        rutaDetails.style.display = "block";
    } else {
        console.log("Ocultando rutaDetails");
        rutaDetails.style.display = "none";
    }
}


success: function (response) {
    console.log("Respuesta del servidor:", response); // Log para depuración
    const valorTotal = response.d;
    if (valorTotal) {
        txtTotalFlete.value = valorTotal.toFixed(2); // Mostrar el valor con dos decimales
    } else {
        lblErrorFactura.textContent = "El número de factura no es válido o no existe.";
    }
}

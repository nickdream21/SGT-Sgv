using System;
using System.Collections.Generic;
using System.Data;
using WebSGV.Services.Despachos;

namespace WebSGV.Views.Controles.Despachos
{
    /// <summary>
    /// Catálogos de la vista previa de la descarga (<see cref="DescargaPedido"/>, E6-24): una consulta por petición
    /// aunque las filas se enlacen dos veces (Page_Load, sin ViewState, y luego un evento). Las plantas se guardan por
    /// ámbito y los destinos por pedido: un evento puede mostrar otro pedido que el enlazado en Page_Load.
    /// </summary>
    internal sealed class CatalogosDescarga : IDisposable
    {
        private DataTable _conductores, _tractos, _carretas, _plantas;
        private bool _plantasInternacional;
        private string _pedidoDestinos;
        private Dictionary<string, DestinoDescargaService.DestinoVigente> _destinos;

        public DataTable Conductores => _conductores ?? (_conductores = RegistroDespachoService.ObtenerConductoresActivos());
        public DataTable Tractos => _tractos ?? (_tractos = RegistroDespachoService.ObtenerTractosActivos());
        public DataTable Carretas => _carretas ?? (_carretas = RegistroDespachoService.ObtenerCarretasActivas());

        /// <summary>Plantas del ámbito (internacional o nacional).</summary>
        public DataTable Plantas(bool internacional)
        {
            if (_plantas != null && _plantasInternacional == internacional) return _plantas;
            _plantas?.Dispose();
            _plantas = RegistroDespachoService.ObtenerPlantasPorAmbito(internacional);
            _plantasInternacional = internacional;
            return _plantas;
        }

        /// <summary>Destinos vigentes del pedido por carreta (sin guion), de la lista del cliente. Sin lista o con error, vacío.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types",
            Justification = "Los destinos solo proponen la planta de la vista previa: con error se registra y se sigue con la lista vacía (el usuario elige a mano).")]
        public Dictionary<string, DestinoDescargaService.DestinoVigente> Destinos(string pedido)
        {
            if (_destinos != null && _pedidoDestinos == pedido) return _destinos;
            try { _destinos = DestinoDescargaService.ObtenerVigentesDelPedido(pedido); }
            catch (Exception ex)
            {
                ControlesFormulario.RegistrarError("DestinosDelPedido", ex);
                _destinos = new Dictionary<string, DestinoDescargaService.DestinoVigente>();
            }
            _pedidoDestinos = pedido;
            return _destinos;
        }

        public void Dispose()
        {
            _conductores?.Dispose();
            _tractos?.Dispose();
            _carretas?.Dispose();
            _plantas?.Dispose();
        }
    }
}

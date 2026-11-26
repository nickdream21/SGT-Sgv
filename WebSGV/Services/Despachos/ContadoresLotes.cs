using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Models.Despachos;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Contadores de los lotes activos (E6-29, M-138): pestaña "Lotes registrados" y resumen de ListaDespachos, y el
    /// aviso de Inicio. Los calcula <c>sp_LD_ContarLotesActivos</c> en una fila (<see cref="ListaDespachosService.ContarLotesActivos"/>);
    /// antes la página traía todos los lotes activos y contaba aquí, que es lo que sigue haciendo <see cref="DesdeLotes"/>
    /// (el oráculo con el que ContadoresLotesBdTests compara el SP).
    /// </summary>
    public sealed class ContadoresLotes
    {
        /// <summary>Lotes con algún despacho activo no anulado.</summary>
        public int LotesActivos { get; set; }

        /// <summary>Lotes activos a los que les falta algún documento (<see cref="EstadoDocumental.Completo"/> = false).</summary>
        public int ConDocumentosPendientes { get; set; }

        /// <summary>De los pendientes, los programados hace más de <see cref="EstadoDocumental.DiasTolerancia"/> días.</summary>
        public int Atrasados { get; set; }

        /// <summary>
        /// Lotes activos con el manifiesto "Pendiente" o "Parcial N/M" (<see cref="ListaDespachosService.EstadoManifiesto(LoteRegistrado)"/>):
        /// algún despacho que espera MCI sin ninguno, de cruce o de retorno (M-242).
        /// </summary>
        public int ConManifiestosPendientes { get; set; }

        /// <summary>
        /// El cálculo de antes, sobre los lotes de <c>sp_LD_ObtenerLotesRegistrados</c> con estado ACTIVO (los anulados
        /// que lleguen no cuentan).
        /// </summary>
        public static ContadoresLotes DesdeLotes(IEnumerable<LoteRegistrado> lotes, DateTime hoy)
        {
            var activos = (lotes ?? Enumerable.Empty<LoteRegistrado>()).Where(l => l.EstadoLote != "ANULADO").ToList();
            var pendientes = activos
                .Select(l => new { Lote = l, Documentos = EstadoDocumental.DeLote(l) })
                .Where(x => !x.Documentos.Completo)
                .ToList();
            return new ContadoresLotes
            {
                LotesActivos = activos.Count,
                ConDocumentosPendientes = pendientes.Count,
                Atrasados = pendientes.Count(x => x.Documentos.Atrasado(x.Lote.FechaProgramacion, hoy)),
                ConManifiestosPendientes = activos.Select(ListaDespachosService.EstadoManifiesto)
                    .Count(v => v == "Pendiente" || v.StartsWith("Parcial", StringComparison.Ordinal))
            };
        }

        /// <summary>La fila de <c>sp_LD_ContarLotesActivos</c> (todo en cero si no vino ninguna).</summary>
        internal static ContadoresLotes DeFila(DataRow fila) => fila == null ? new ContadoresLotes() : new ContadoresLotes
        {
            LotesActivos = Convert.ToInt32(fila["LotesActivos"]),
            ConDocumentosPendientes = Convert.ToInt32(fila["LotesDocumentosPendientes"]),
            Atrasados = Convert.ToInt32(fila["LotesAtrasados"]),
            ConManifiestosPendientes = Convert.ToInt32(fila["LotesManifiestosPendientes"])
        };

        public override bool Equals(object obj) =>
            obj is ContadoresLotes o && o.LotesActivos == LotesActivos && o.ConDocumentosPendientes == ConDocumentosPendientes
            && o.Atrasados == Atrasados && o.ConManifiestosPendientes == ConManifiestosPendientes;

        public override int GetHashCode() =>
            ((LotesActivos * 397 ^ ConDocumentosPendientes) * 397 ^ Atrasados) * 397 ^ ConManifiestosPendientes;

        public override string ToString() =>
            $"activos {LotesActivos}, pendientes {ConDocumentosPendientes}, atrasados {Atrasados}, manifiestos {ConManifiestosPendientes}";
    }
}

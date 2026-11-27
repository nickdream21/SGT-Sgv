using System;
using System.Collections.Generic;
using System.Linq;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Pestañas del tablero de seguimiento (RegistroSeguimiento). "En curso" (la de inicio) son los abiertos no vencidos y
    /// las de fase cuentan solo esos; "Vencidos" junta los abiertos hace más de <see cref="ViajeVencido.DiasVencido"/>
    /// días (<see cref="ViajeVencido"/>); siguen "Cerrados" y "Todos". El antiguo <c>?fase=activos</c> (enlaces guardados)
    /// abre "En curso".
    /// </summary>
    public static class PestanasSeguimiento
    {
        public const string EnCurso = "encurso";
        public const string Vencidos = "vencidos";
        public const string Cerrados = "cerrado";
        public const string Todos = "todos";

        /// <summary>Nombre anterior de "En curso" (antes incluía los vencidos).</summary>
        public const string AliasActivos = "activos";

        public static readonly IReadOnlyList<(string Grupo, string Nombre)> Lista = new[]
        {
            (EnCurso, "En curso"), ("carga", "En carga"), ("base", "En base"),
            ("ecuador", "Rumbo a Ecuador / a la descarga"), ("retorno", "Retorno"),
            (Vencidos, "Vencidos"), (Cerrados, "Cerrados"), (Todos, "Todos"),
        };

        /// <summary>La pestaña de <paramref name="fase"/> (querystring o campo oculto); "activos", vacío o desconocido = En curso.</summary>
        public static string Normalizar(string fase)
        {
            string f = (fase ?? "").Trim().ToLowerInvariant();
            if (f == AliasActivos) return EnCurso;
            return Lista.Any(p => p.Grupo == f) ? f : EnCurso;
        }

        /// <summary>True si <paramref name="fase"/> es una pestaña conocida (o su alias).</summary>
        public static bool Existe(string fase)
        {
            string f = (fase ?? "").Trim().ToLowerInvariant();
            return f == AliasActivos || Lista.Any(p => p.Grupo == f);
        }

        /// <summary>Los viajes de la pestaña, en el mismo orden.</summary>
        public static List<ViajeResumen> Filtrar(IEnumerable<ViajeResumen> viajes, string fase)
        {
            string f = Normalizar(fase);
            var lista = viajes ?? Enumerable.Empty<ViajeResumen>();
            switch (f)
            {
                case Todos: return lista.ToList();
                case Vencidos: return lista.Where(v => v.Vencido).ToList();
                case Cerrados: return lista.Where(v => Grupo(v) == Cerrados).ToList();
                case EnCurso: return lista.Where(v => Grupo(v) != Cerrados && !v.Vencido).ToList();
                default: return lista.Where(v => Grupo(v) == f && !v.Vencido).ToList();
            }
        }

        public static string NombreDe(string fase)
        {
            string f = Normalizar(fase);
            return Lista.First(p => p.Grupo == f).Nombre;
        }

        private static string Grupo(ViajeResumen v) => v.Fase?.Grupo;
    }
}

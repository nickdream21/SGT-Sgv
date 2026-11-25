using System;
using System.Collections.Concurrent;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Lectura tolerante de una columna de un resultado (el <c>GetSafeValue</c> de ListaDespachos y de su servicio):
    /// <c>DBNull</c>, una columna que no está en el resultado o un valor que no se puede convertir devuelven el valor
    /// por defecto, sin lanzar, como antes. E6-13: antes eso pasaba en silencio (un nombre de columna mal escrito dejaba
    /// el flete o el total de la factura en blanco sin rastro); ahora deja una advertencia en el log, una sola vez por
    /// origen y columna mientras viva la aplicación, no una por fila.
    /// </summary>
    internal static class LecturaSegura
    {
        /// <summary>Tope de pares origen/columna recordados: un error en bucle con nombres variables no crece sin fin.</summary>
        internal const int TopeAvisos = 200;

        private static readonly ConcurrentDictionary<string, byte> Avisadas =
            new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Valor de <paramref name="columna"/> leído con <paramref name="leer"/> (p. ej. <c>c =&gt; reader[c]</c>), convertido
        /// a <typeparamref name="T"/>; <paramref name="porDefecto"/> si es nulo o si falla la lectura o la conversión.
        /// <paramref name="origen"/> nombra quién lee (va en la advertencia).
        /// </summary>
        public static T Leer<T>(Func<string, object> leer, string columna, string origen, T porDefecto = default(T))
        {
            object valor;
            try
            {
                valor = leer(columna);
            }
            catch (Exception ex)
            {
                // SqlDataReader lanza IndexOutOfRangeException y DataRow ArgumentException si la columna no existe.
                AvisarUnaVez(ex, origen, columna, "no está en el resultado");
                return porDefecto;
            }

            if (valor == null || valor == DBNull.Value)
                return porDefecto;

            try
            {
                return Convertir<T>(valor);
            }
            catch (Exception ex)
            {
                AvisarUnaVez(ex, origen, columna, "no se pudo convertir a " + typeof(T).Name);
                return porDefecto;
            }
        }

        private static T Convertir<T>(object valor)
        {
            if (typeof(T) == typeof(string))
                return (T)(object)valor.ToString();
            if (typeof(T) == typeof(DateTime?) || typeof(T) == typeof(DateTime))
                return (T)(object)Convert.ToDateTime(valor);
            if (typeof(T) == typeof(decimal?))
                return (T)(object)Convert.ToDecimal(valor);
            if (typeof(T) == typeof(bool))
                return (T)(object)Convert.ToBoolean(valor);
            if (typeof(T) == typeof(int))
                return (T)(object)Convert.ToInt32(valor);
            return (T)Convert.ChangeType(valor, typeof(T));
        }

        /// <summary>Advierte una sola vez por origen y columna. Devuelve si dejó la advertencia (para las pruebas).</summary>
        internal static bool AvisarUnaVez(Exception ex, string origen, string columna, string problema)
        {
            string clave = (origen ?? "") + "|" + (columna ?? "");
            if (Avisadas.Count >= TopeAvisos || !Avisadas.TryAdd(clave, 0))
                return false;
            LogSGV.Advertencia(ex, "{Origen}: la columna {Columna} {Problema}; se devuelve el predeterminado (aviso único por columna)",
                origen, columna, problema);
            return true;
        }
    }
}

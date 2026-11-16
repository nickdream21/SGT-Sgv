using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Límite de consultas por clave (normalmente la IP) con ventana deslizante: como mucho
    /// <c>maximo</c> consultas en cualquier lapso de <c>ventana</c>. Pensado para páginas públicas
    /// (VerificarDocumento) donde hay que evitar el barrido de códigos.
    ///
    /// Memoria acotada: como mucho una vez por ventana se recorren todas las claves, se vacían las
    /// marcas vencidas y se quita la clave cuya cola quedó vacía. Sin esa purga el diccionario crecía
    /// con cada IP que alguna vez consultó y nunca se liberaba.
    ///
    /// Carrera aceptada: entre que la purga ve una cola vacía y la quita, otra petición pudo haberla
    /// obtenido con GetOrAdd y encolar en ella después de soltar el lock; esa marca se pierde con la
    /// cola. Se quita solo si el diccionario aún tiene ESA MISMA cola (Remove del par clave/valor), y
    /// el peor caso es una consulta de más para esa IP en esa ventana: aceptable para un límite
    /// anti-barrido, a cambio de no bloquear todo el diccionario en cada consulta.
    /// </summary>
    public sealed class LimiteConsultas
    {
        private readonly int _maximo;
        private readonly TimeSpan _ventana;
        private readonly Func<DateTime> _relojUtc;
        private readonly ConcurrentDictionary<string, Queue<DateTime>> _consultas =
            new ConcurrentDictionary<string, Queue<DateTime>>();
        private readonly object _candadoPurga = new object();
        private DateTime _ultimaPurga;

        public LimiteConsultas(int maximo, TimeSpan ventana, Func<DateTime> relojUtc)
        {
            if (maximo < 1) throw new ArgumentOutOfRangeException(nameof(maximo));
            if (ventana <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ventana));
            _maximo = maximo;
            _ventana = ventana;
            _relojUtc = relojUtc ?? throw new ArgumentNullException(nameof(relojUtc));
            _ultimaPurga = _relojUtc();
        }

        /// <summary>Cantidad de claves con historial guardado (para pruebas y diagnóstico).</summary>
        public int ClavesGuardadas => _consultas.Count;

        /// <summary>True y registra la consulta si la clave no llegó al máximo en la ventana; si no, false.</summary>
        public bool Permitir(string clave)
        {
            var ahora = _relojUtc();
            PurgarSiToca(ahora);

            var cola = _consultas.GetOrAdd(clave ?? "unknown", _ => new Queue<DateTime>());
            lock (cola)
            {
                while (cola.Count > 0 && ahora - cola.Peek() > _ventana) cola.Dequeue();
                if (cola.Count >= _maximo) return false;
                cola.Enqueue(ahora);
                return true;
            }
        }

        private void PurgarSiToca(DateTime ahora)
        {
            lock (_candadoPurga)
            {
                if (ahora - _ultimaPurga < _ventana) return;
                _ultimaPurga = ahora;
            }

            foreach (var par in _consultas)
            {
                bool vacia;
                lock (par.Value)
                {
                    while (par.Value.Count > 0 && ahora - par.Value.Peek() > _ventana) par.Value.Dequeue();
                    vacia = par.Value.Count == 0;
                }
                // Quita la clave solo si sigue apuntando a esta misma cola (no a una creada después).
                if (vacia) ((ICollection<KeyValuePair<string, Queue<DateTime>>>)_consultas).Remove(par);
            }
        }
    }
}

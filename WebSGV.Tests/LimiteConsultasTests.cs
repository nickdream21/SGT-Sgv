using System;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    public class LimiteConsultasTests
    {
        private static readonly TimeSpan Ventana = TimeSpan.FromMinutes(10);

        private sealed class RelojFalso
        {
            public DateTime Ahora = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
            public void Avanzar(TimeSpan lapso) => Ahora = Ahora.Add(lapso);
        }

        private static LimiteConsultas Crear(RelojFalso reloj) => new LimiteConsultas(30, Ventana, () => reloj.Ahora);

        [Fact]
        public void TreintaPasan_LaTreintaYUnoNo()
        {
            var reloj = new RelojFalso();
            var limite = Crear(reloj);
            for (int i = 0; i < 30; i++)
            {
                Assert.True(limite.Permitir("1.2.3.4"), "consulta " + (i + 1));
                reloj.Avanzar(TimeSpan.FromSeconds(1));
            }
            Assert.False(limite.Permitir("1.2.3.4"));
        }

        [Fact]
        public void PasadaLaVentana_VuelveAPermitir()
        {
            var reloj = new RelojFalso();
            var limite = Crear(reloj);
            for (int i = 0; i < 30; i++) limite.Permitir("1.2.3.4");
            Assert.False(limite.Permitir("1.2.3.4"));

            reloj.Avanzar(Ventana + TimeSpan.FromSeconds(1));
            Assert.True(limite.Permitir("1.2.3.4"));
        }

        [Fact]
        public void IpsIndependientes()
        {
            var reloj = new RelojFalso();
            var limite = Crear(reloj);
            for (int i = 0; i < 30; i++) limite.Permitir("1.2.3.4");
            Assert.False(limite.Permitir("1.2.3.4"));
            Assert.True(limite.Permitir("5.6.7.8"));
        }

        [Fact]
        public void ClaveNula_CuentaComoUnknown()
        {
            var reloj = new RelojFalso();
            var limite = Crear(reloj);
            for (int i = 0; i < 30; i++) Assert.True(limite.Permitir(null));
            Assert.False(limite.Permitir("unknown"));
        }

        [Fact]
        public void TrasLaVentana_PurgaLasClavesVencidas()
        {
            var reloj = new RelojFalso();
            var limite = Crear(reloj);
            for (int i = 0; i < 50; i++) limite.Permitir("10.0.0." + i);
            Assert.Equal(50, limite.ClavesGuardadas);

            reloj.Avanzar(Ventana + TimeSpan.FromMinutes(1));
            Assert.True(limite.Permitir("192.168.1.1"));
            Assert.Equal(1, limite.ClavesGuardadas);
        }
    }
}

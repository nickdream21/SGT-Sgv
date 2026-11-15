using System.Collections.Specialized;
using System.Web;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// La IP del cliente sale siempre de REMOTE_ADDR: X-Forwarded-For lo escribe el cliente y no
    /// debe decidir nada (bloqueo del Login, FirmaDigital.ipOrigen, límite de VerificarDocumento).
    /// </summary>
    public class IpClienteHelperTests
    {
        /// <summary>Request mínimo: solo lo que lee IpClienteHelper / AuditoriaHelper.</summary>
        private sealed class RequestFalso : HttpRequestBase
        {
            private readonly string _ip;
            private readonly NameValueCollection _headers = new NameValueCollection();
            private readonly NameValueCollection _serverVariables = new NameValueCollection();

            public RequestFalso(string ip, string xForwardedFor = null)
            {
                _ip = ip;
                if (ip != null) _serverVariables["REMOTE_ADDR"] = ip;
                if (xForwardedFor != null)
                {
                    _headers["X-Forwarded-For"] = xForwardedFor;
                    _serverVariables["HTTP_X_FORWARDED_FOR"] = xForwardedFor;
                }
            }

            public override string UserHostAddress => _ip;
            public override NameValueCollection Headers => _headers;
            public override NameValueCollection ServerVariables => _serverVariables;
        }

        [Fact]
        public void ConXForwardedForFalso_DevuelveRemoteAddr()
        {
            var req = new RequestFalso("200.48.12.7", "10.0.0.1, 1.2.3.4");
            Assert.Equal("200.48.12.7", IpClienteHelper.Obtener(req));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SinIp_DevuelveUnknown(string ip)
        {
            Assert.Equal("unknown", IpClienteHelper.Obtener(new RequestFalso(ip, "1.2.3.4")));
        }

        [Fact]
        public void RequestNulo_DevuelveUnknown()
        {
            Assert.Equal("unknown", IpClienteHelper.Obtener((HttpRequestBase)null));
            Assert.Equal("unknown", IpClienteHelper.Obtener((HttpRequest)null));
        }

        [Fact]
        public void IpConEspacios_SeRecorta()
        {
            Assert.Equal("190.42.1.9", IpClienteHelper.Obtener(new RequestFalso("  190.42.1.9 ")));
        }

        [Theory]
        [InlineData("2001:db8:85a3::8a2e:370:7334")]
        [InlineData("::1")]
        [InlineData("fe80::1%12")]
        public void IPv6_SeConserva(string ipv6)
        {
            Assert.Equal(ipv6, IpClienteHelper.Obtener(new RequestFalso(ipv6, "8.8.8.8")));
        }

        [Fact]
        public void IPv6Maxima_CabeEnFirmaDigitalIpOrigen()
        {
            // La forma textual más larga de una IPv6 (IPv4 embebida) mide justo 45.
            const string ipv6 = "ffff:ffff:ffff:ffff:ffff:ffff:255.255.255.255";
            Assert.Equal(45, ipv6.Length);
            Assert.Equal(ipv6, IpClienteHelper.Obtener(new RequestFalso(ipv6), IpClienteHelper.LargoMaximoFirma));
        }

        [Fact]
        public void IpMasLargaQueLaColumna_SeRecortaAlLargoMaximo()
        {
            string larga = "ffff:ffff:ffff:ffff:ffff:ffff:255.255.255.255%interfaz-larga";
            string r = IpClienteHelper.Obtener(new RequestFalso(larga), IpClienteHelper.LargoMaximoFirma);
            Assert.Equal(IpClienteHelper.LargoMaximoFirma, r.Length);
            Assert.StartsWith(r, larga);
        }

        // ---------------- AuditoriaHelper.ComponerDireccionIP (AuditoriaLog.DireccionIP NVARCHAR(50)) ----------------

        [Fact]
        public void Auditoria_SinXff_SoloLaIp()
        {
            Assert.Equal("200.48.12.7", AuditoriaHelper.ComponerDireccionIP("200.48.12.7", null));
            Assert.Equal("200.48.12.7", AuditoriaHelper.ComponerDireccionIP("200.48.12.7", "  "));
        }

        [Fact]
        public void Auditoria_ConXff_GuardaLaPrimeraEntradaComoReferencia()
        {
            Assert.Equal("200.48.12.7 (XFF: 1.2.3.4)",
                AuditoriaHelper.ComponerDireccionIP("200.48.12.7", "1.2.3.4, 10.0.0.1"));
        }

        [Fact]
        public void Auditoria_IPv6ConXffLargo_NoPasaDe50YConservaLaIp()
        {
            const string ipv6 = "2001:0db8:85a3:0000:0000:8a2e:0370:7334";
            string xff = "ffff:ffff:ffff:ffff:ffff:ffff:255.255.255.255, 10.0.0.1";

            string r = AuditoriaHelper.ComponerDireccionIP(ipv6, xff);

            Assert.True(r.Length <= 50, $"Mide {r.Length}: {r}");
            Assert.StartsWith(ipv6, r);
        }

        [Fact]
        public void Auditoria_IPv6Maxima_SinEspacioParaXff_SoloLaIp()
        {
            const string ipv6 = "ffff:ffff:ffff:ffff:ffff:ffff:255.255.255.255";
            Assert.Equal(ipv6, AuditoriaHelper.ComponerDireccionIP(ipv6, "1.2.3.4"));
        }

        [Fact]
        public void Auditoria_IpVacia_Unknown()
        {
            Assert.Equal("unknown", AuditoriaHelper.ComponerDireccionIP(null, null));
        }
    }
}

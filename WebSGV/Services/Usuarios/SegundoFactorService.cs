using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using WebSGV.Helpers;

namespace WebSGV.Services.Usuarios
{
    /// <summary>Fila de <c>UsuarioSegundoFactor</c> (sp_US_ObtenerSegundoFactor).</summary>
    public sealed class EstadoSegundoFactor
    {
        public bool Activo { get; set; }
        public string SecretoCifrado { get; set; }
        public long? UltimoPasoUsado { get; set; }
        public string SecretoPendienteCifrado { get; set; }
        public DateTime? PendienteDesdeUtc { get; set; }
        public DateTime? ActivadoUtc { get; set; }
        public int CodigosRestantes { get; set; }
    }

    /// <summary>Lo que se muestra para dar de alta la app autenticadora.</summary>
    public sealed class AltaSegundoFactor
    {
        /// <summary>Secreto Base32 en grupos de 4 (para escribirlo a mano).</summary>
        public string SecretoAgrupado { get; set; }
        public string UriOtpAuth { get; set; }
        /// <summary>Código QR en SVG (QRCoder); null si no se pudo generar (queda el secreto en texto y el enlace).</summary>
        public string QrSvg { get; set; }
    }

    /// <summary>Resultado de verificar un código o confirmar un alta.</summary>
    public sealed class ResultadoSegundoFactor
    {
        public bool Ok { get; set; }
        /// <summary>Mensaje para el usuario si no fue correcto (texto plano).</summary>
        public string Mensaje { get; set; }
        /// <summary>Falta la clave Seguridad.ClaveSegundoFactor o no corresponde: falla cerrada.</summary>
        public bool NoConfigurado { get; set; }
        /// <summary>El paso ya no sirve (sin factor activo, alta vencida): volver a empezar.</summary>
        public bool Reiniciar { get; set; }
        public bool PorCodigoRecuperacion { get; set; }
        public int? CodigosRestantes { get; set; }
        /// <summary>Alta: los 10 códigos de recuperación nuevos (XXXX-XXXX-XXXX-XXXX), para mostrarlos una vez.</summary>
        public string[] CodigosNuevos { get; set; }
    }

    /// <summary>
    /// Segundo factor en uso (E4-14) sobre el núcleo de E4-13 (<see cref="Totp"/>, <see cref="CifradoSecreto"/>):
    /// alta con secreto pendiente que solo se activa con un código válido, verificación al entrar (TOTP con
    /// anti-reuso atómico en la BD o código de recuperación de un solo uso), 5 fallos → 15 min con el almacén de
    /// E4-4 (clave <see cref="SegundoFactorReglas.ClaveIntentos"/>, también para los códigos de recuperación),
    /// regeneración de códigos y baja. Audita cada paso. Sin la clave de cifrado falla cerrado.
    /// </summary>
    public static class SegundoFactorService
    {
        public const string MensajeNoConfigurado =
            "El segundo factor no está configurado en este servidor. Avise al administrador del sistema.";
        public const string MensajeNoDisponible =
            "No se pudo verificar el código en este momento. Inténtelo de nuevo en unos minutos.";
        public const string MensajeIncorrecto =
            "El código no es correcto o ya se usó. Escriba el código que muestra ahora la app.";
        public const string MensajeFormato =
            "Escriba los 6 dígitos que muestra la app, o uno de sus códigos de recuperación (16 letras y números).";
        public const string MensajeSinFactor =
            "Su cuenta ya no tiene el segundo factor activo. Vuelva a ingresar con su usuario y contraseña.";
        public const string MensajeAltaVencida =
            "El alta venció o se empezó otra en otra ventana. Vuelva a empezar.";

        public const string AccionOk = "SEGUNDO_FACTOR_OK";
        public const string AccionCodigoRecuperacion = "SEGUNDO_FACTOR_CODIGO_RECUPERACION";
        public const string AccionFallido = "SEGUNDO_FACTOR_FALLIDO";
        public const string AccionBloqueado = "SEGUNDO_FACTOR_BLOQUEADO";
        public const string AccionAltaIniciada = "SEGUNDO_FACTOR_ALTA_INICIADA";
        public const string AccionActivado = "SEGUNDO_FACTOR_ACTIVADO";
        public const string AccionCodigosRegenerados = "SEGUNDO_FACTOR_CODIGOS_REGENERADOS";
        public const string AccionQuitado = "SEGUNDO_FACTOR_QUITADO";

        /// <summary>Dependencias inyectables (pruebas); <see cref="Sitio"/> es la del sitio.</summary>
        internal sealed class Dependencias
        {
            public Func<int, EstadoSegundoFactor> Leer;
            /// <summary>Lanza <see cref="InvalidOperationException"/> si falta la clave (CifradoSecreto.DesdeConfiguracion).</summary>
            public Func<CifradoSecreto> Cifrado;
            public Func<int, string, long, bool> RegistrarPaso;
            public Func<int, IList<KeyValuePair<int, string>>> LeerCodigos;
            /// <summary>(idUsuario, idCodigo) → (aceptado, restantes).</summary>
            public Func<int, int, Tuple<bool, int>> UsarCodigo;
            public ControlIntentosLogin Control;
            /// <summary>(accion, idUsuario, nombreUsuario, descripcion).</summary>
            public Action<string, int, string, string> Auditar;
            /// <summary>Hora UTC para Totp.Verificar: en el sitio siempre DateTime.UtcNow.</summary>
            public Func<DateTime> RelojUtc;
        }

        internal static readonly Dependencias Sitio = new Dependencias
        {
            Leer = Obtener,
            Cifrado = CifradoSecreto.DesdeConfiguracion,
            RegistrarPaso = RegistrarPasoBd,
            LeerCodigos = LeerCodigosBd,
            UsarCodigo = UsarCodigoBd,
            Control = new ControlIntentosLogin(AlmacenBdIntentos.Sitio, () => DateTime.UtcNow, porIp: null,
                porCuenta: new ControlIntentosLogin.Limite(SegundoFactorReglas.MaxIntentos, SegundoFactorReglas.Bloqueo, SegundoFactorReglas.Ventana)),
            Auditar = AuditarSitio,
            RelojUtc = () => DateTime.UtcNow
        };

        // ── Configuración ──────────────────────────────────────────────────

        /// <summary>True si <c>Seguridad.ClaveSegundoFactor</c> existe y es una clave válida (32 bytes en base64).</summary>
        public static bool ClaveConfigurada() => ClaveConfigurada(ConfigurationManager.AppSettings[CifradoSecreto.ClaveConfiguracion]);

        public static bool ClaveConfigurada(string valor)
        {
            try { CifradoSecreto.ClaveDesdeTexto(valor); return true; }
            catch (InvalidOperationException) { return false; }
        }

        // ── Lectura ────────────────────────────────────────────────────────

        /// <summary>Estado del usuario, o null si nunca empezó el alta.</summary>
        public static EstadoSegundoFactor Obtener(int idUsuario)
        {
            DataTable dt = DbHelper.ConsultarTablaSp("sp_US_ObtenerSegundoFactor", DbHelper.Param("@idUsuario", idUsuario));
            if (dt.Rows.Count == 0) return null;
            DataRow r = dt.Rows[0];
            return new EstadoSegundoFactor
            {
                Activo = Convert.ToBoolean(r["activo"]),
                SecretoCifrado = r["secretoCifrado"] as string,
                UltimoPasoUsado = r["ultimoPasoUsado"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["ultimoPasoUsado"]),
                SecretoPendienteCifrado = r["secretoPendienteCifrado"] as string,
                PendienteDesdeUtc = r["pendienteDesdeUtc"] == DBNull.Value ? (DateTime?)null
                    : DateTime.SpecifyKind((DateTime)r["pendienteDesdeUtc"], DateTimeKind.Utc),
                ActivadoUtc = r["activadoUtc"] == DBNull.Value ? (DateTime?)null
                    : DateTime.SpecifyKind((DateTime)r["activadoUtc"], DateTimeKind.Utc),
                CodigosRestantes = Convert.ToInt32(r["codigosRestantes"])
            };
        }


        // ── Alta ───────────────────────────────────────────────────────────

        /// <summary>
        /// Empieza un alta (o un cambio de celular): secreto nuevo, cifrado y guardado como pendiente. El factor
        /// vigente, si lo hay, sigue valiendo hasta <see cref="ConfirmarAlta"/>. Lanza
        /// <see cref="InvalidOperationException"/> si falta la clave de cifrado.
        /// </summary>
        public static AltaSegundoFactor IniciarAlta(int idUsuario, string nombreUsuario)
        {
            CifradoSecreto cifrado = CifradoSecreto.DesdeConfiguracion();
            byte[] secreto = Totp.GenerarSecreto();
            DbHelper.EjecutarNonQuerySp("sp_US_GuardarSecretoPendiente",
                DbHelper.Param("@idUsuario", idUsuario),
                DbHelper.Param("@secretoCifrado", cifrado.Cifrar(secreto)));
            Sitio.Auditar(AccionAltaIniciada, idUsuario, nombreUsuario, "Alta del segundo factor iniciada (falta confirmar con un código)");
            return Armar(nombreUsuario, secreto);
        }

        /// <summary>
        /// El alta pendiente para volver a mostrarla (código mal escrito, recarga, "Cambiar de celular" pulsado otra
        /// vez), o null si no hay, tiene 14 minutos o más (<see cref="SegundoFactorReglas.AltaPendienteRetomable"/>) o
        /// no se descifra.
        /// </summary>
        public static AltaSegundoFactor AltaPendiente(int idUsuario, string nombreUsuario)
        {
            EstadoSegundoFactor estado = Obtener(idUsuario);
            if (estado == null || string.IsNullOrEmpty(estado.SecretoPendienteCifrado)) return null;
            // Cerca de los 15 min del SP: mejor un QR nuevo que uno que el SP rechazará al confirmar.
            if (!SegundoFactorReglas.AltaPendienteRetomable(estado.PendienteDesdeUtc, DateTime.UtcNow)) return null;
            try
            {
                return Armar(nombreUsuario, CifradoSecreto.DesdeConfiguracion().Descifrar(estado.SecretoPendienteCifrado));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is CryptographicException)
            {
                LogSGV.Error(ex, "No se pudo descifrar el alta pendiente del segundo factor (usuario {IdUsuario})", idUsuario);
                return null;
            }
        }

        private static AltaSegundoFactor Armar(string nombreUsuario, byte[] secreto)
        {
            string cuenta = string.IsNullOrWhiteSpace(nombreUsuario) ? "usuario" : nombreUsuario.Trim();
            string uri = Totp.UriOtpAuth(SegundoFactorReglas.Emisor, cuenta, secreto);
            return new AltaSegundoFactor
            {
                SecretoAgrupado = SegundoFactorReglas.AgruparSecreto(Base32.Codificar(secreto)),
                UriOtpAuth = uri,
                QrSvg = QrSvg(uri)
            };
        }

        /// <summary>QR en SVG (QRCoder, corrección M, escalable por viewBox). Null si falla (se registra).</summary>
        public static string QrSvg(string texto)
        {
            try
            {
                using (var generador = new QRCoder.QRCodeGenerator())
                using (QRCoder.QRCodeData datos = generador.CreateQrCode(texto, QRCoder.QRCodeGenerator.ECCLevel.M))
                using (var svg = new QRCoder.SvgQRCode(datos))
                    return svg.GetGraphic(6, "#000000", "#ffffff", true, QRCoder.SvgQRCode.SizingMode.ViewBoxAttribute);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "No se pudo generar el código QR del segundo factor");
                return null;
            }
        }

        /// <summary>
        /// Confirma el alta con un código de la app: si es válido para el secreto pendiente, este pasa a ser el
        /// vigente (con su paso como último usado) y se crean 10 códigos de recuperación nuevos.
        /// </summary>
        public static ResultadoSegundoFactor ConfirmarAlta(int idUsuario, string nombreUsuario, string codigo) =>
            ConfirmarAlta(idUsuario, nombreUsuario, codigo, Sitio, ActivarBd);

        internal static ResultadoSegundoFactor ConfirmarAlta(int idUsuario, string nombreUsuario, string codigo,
            Dependencias d, Func<int, string, long, string, bool> activar)
        {
            string totp = SegundoFactorReglas.NormalizarCodigoTotp(codigo);
            if (totp == null)
                return Falla("Escriba los 6 dígitos que muestra la app.");

            var reserva = d.Control.Reservar(null, SegundoFactorReglas.ClaveIntentos(idUsuario));
            if (reserva.Rechazado)
                return Falla(MensajeEspera(reserva.SegundosRestantes));

            long paso;
            string pendiente;
            try
            {
                EstadoSegundoFactor estado = d.Leer(idUsuario);
                pendiente = estado?.SecretoPendienteCifrado;
                if (string.IsNullOrEmpty(pendiente))
                {
                    d.Control.ErrorAlValidar(reserva);
                    return new ResultadoSegundoFactor { Mensaje = MensajeAltaVencida, Reiniciar = true };
                }

                byte[] secreto;
                ResultadoSegundoFactor sinClave = Descifrar(d, reserva, idUsuario, pendiente, out secreto);
                if (sinClave != null) return sinClave;

                if (!Totp.Verificar(secreto, totp, d.RelojUtc(), null, out paso))
                    return Fallo(d, reserva, idUsuario, nombreUsuario, "Código incorrecto al confirmar el alta del segundo factor");
            }
            catch (Exception ex)
            {
                d.Control.ErrorAlValidar(reserva);
                LogSGV.Error(ex, "No se pudo confirmar el alta del segundo factor (usuario {IdUsuario})", idUsuario);
                return Falla(MensajeNoDisponible);
            }

            string[] codigos = GenerarCodigos(out string hashes);
            bool activado;
            try
            {
                activado = activar(idUsuario, pendiente, paso, hashes);
            }
            catch (Exception ex)
            {
                d.Control.ErrorAlValidar(reserva);
                LogSGV.Error(ex, "No se pudo activar el segundo factor (usuario {IdUsuario})", idUsuario);
                return Falla(MensajeNoDisponible);
            }
            if (!activado)
            {
                d.Control.ErrorAlValidar(reserva);
                return new ResultadoSegundoFactor { Mensaje = MensajeAltaVencida, Reiniciar = true };
            }

            d.Control.LoginCorrecto(reserva);
            d.Auditar(AccionActivado, idUsuario, nombreUsuario,
                "Segundo factor activado (" + SegundoFactorReglas.CantidadCodigosRecuperacion + " códigos de recuperación nuevos)");
            SesionVigenteHelper.InvalidarCache(idUsuario);
            return new ResultadoSegundoFactor { Ok = true, CodigosNuevos = codigos };
        }

        // ── Entrada ────────────────────────────────────────────────────────

        /// <summary>
        /// Verifica el código al entrar: 6 dígitos (TOTP con el reloj UTC, anti-reuso registrado en la BD) o un
        /// código de recuperación (se gasta). Cuenta los fallos (5 → 15 min). Sin la clave de cifrado, un
        /// usuario enrolado no entra (falla cerrada) y no se cuenta como intento.
        /// </summary>
        public static ResultadoSegundoFactor VerificarEntrada(int idUsuario, string nombreUsuario, string escrito) =>
            VerificarEntrada(idUsuario, nombreUsuario, escrito, Sitio);

        internal static ResultadoSegundoFactor VerificarEntrada(int idUsuario, string nombreUsuario, string escrito, Dependencias d)
        {
            string totp = SegundoFactorReglas.NormalizarCodigoTotp(escrito);
            string recuperacion = totp == null ? SegundoFactorReglas.NormalizarCodigoRecuperacion(escrito) : null;
            if (totp == null && recuperacion == null)
                return Falla(MensajeFormato);

            var reserva = d.Control.Reservar(null, SegundoFactorReglas.ClaveIntentos(idUsuario));
            if (reserva.Rechazado)
                return Falla(MensajeEspera(reserva.SegundosRestantes));

            bool correcto = false;
            int? restantes = null;
            try
            {
                EstadoSegundoFactor estado = d.Leer(idUsuario);
                if (estado == null || !estado.Activo || string.IsNullOrEmpty(estado.SecretoCifrado))
                {
                    d.Control.ErrorAlValidar(reserva);
                    return new ResultadoSegundoFactor { Mensaje = MensajeSinFactor, Reiniciar = true };
                }

                if (totp != null)
                {
                    byte[] secreto;
                    ResultadoSegundoFactor sinClave = Descifrar(d, reserva, idUsuario, estado.SecretoCifrado, out secreto);
                    if (sinClave != null) return sinClave;

                    // Totp.Verificar ya rechaza un paso <= al último usado; la BD lo vuelve a exigir de forma atómica
                    // (dos peticiones con el mismo código a la vez: solo una registra el paso).
                    correcto = Totp.Verificar(secreto, totp, d.RelojUtc(), estado.UltimoPasoUsado, out long paso)
                               && d.RegistrarPaso(idUsuario, estado.SecretoCifrado, paso);
                }
                else
                {
                    foreach (KeyValuePair<int, string> c in d.LeerCodigos(idUsuario))
                    {
                        if (!SegundoFactorReglas.VerificarCodigoRecuperacion(recuperacion, c.Value)) continue;
                        Tuple<bool, int> uso = d.UsarCodigo(idUsuario, c.Key);
                        correcto = uso.Item1;
                        restantes = uso.Item2;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                d.Control.ErrorAlValidar(reserva);
                LogSGV.Error(ex, "No se pudo verificar el segundo factor (usuario {IdUsuario})", idUsuario);
                return Falla(MensajeNoDisponible);
            }

            if (!correcto)
                return Fallo(d, reserva, idUsuario, nombreUsuario,
                    recuperacion != null ? "Código de recuperación incorrecto o ya usado" : "Código del segundo factor incorrecto o repetido");

            d.Control.LoginCorrecto(reserva);
            if (recuperacion != null)
                d.Auditar(AccionCodigoRecuperacion, idUsuario, nombreUsuario,
                    "Entrada con un código de recuperación (quedan " + restantes + ")");
            else
                d.Auditar(AccionOk, idUsuario, nombreUsuario, "Código del segundo factor correcto");
            return new ResultadoSegundoFactor { Ok = true, PorCodigoRecuperacion = recuperacion != null, CodigosRestantes = restantes };
        }

        // ── Códigos de recuperación y baja ─────────────────────────────────

        /// <summary>Reemplaza los códigos de recuperación (la página ya pidió la contraseña). Null si no tiene factor activo.</summary>
        public static string[] RegenerarCodigos(int idUsuario, string nombreUsuario)
        {
            string[] codigos = GenerarCodigos(out string hashes);
            object r = DbHelper.EjecutarEscalarSp("sp_US_ReemplazarCodigosRecuperacion",
                DbHelper.Param("@idUsuario", idUsuario),
                DbHelper.Param("@hashesCodigos", hashes));
            if (!(r is bool b && b)) return null;
            Sitio.Auditar(AccionCodigosRegenerados, idUsuario, nombreUsuario,
                "Códigos de recuperación del segundo factor generados de nuevo (los anteriores dejan de valer)");
            return codigos;
        }

        /// <summary>
        /// Quita el factor y sus códigos y renueva el sello (cierra las sesiones del usuario). True si había algo
        /// que quitar. La auditoría la registra quien llama (GestionUsuarios, el script de emergencia).
        /// </summary>
        public static bool Quitar(int idUsuario)
        {
            object r = DbHelper.EjecutarEscalarSp("sp_US_QuitarSegundoFactor", DbHelper.Param("@idUsuario", idUsuario));
            bool quitado = r is bool b && b;
            if (quitado)
                SesionVigenteHelper.InvalidarCache(idUsuario);
            return quitado;
        }

        // ── Acceso a la BD ─────────────────────────────────────────────────

        internal static bool RegistrarPasoBd(int idUsuario, string secretoCifrado, long paso) =>
            DbHelper.EjecutarEscalarSp("sp_US_RegistrarPasoTotp",
                DbHelper.Param("@idUsuario", idUsuario),
                DbHelper.Param("@secretoCifrado", secretoCifrado),
                DbHelper.Param("@paso", paso)) is bool b && b;

        internal static bool ActivarBd(int idUsuario, string secretoPendienteCifrado, long paso, string hashes) =>
            DbHelper.EjecutarEscalarSp("sp_US_ActivarSegundoFactor",
                DbHelper.Param("@idUsuario", idUsuario),
                DbHelper.Param("@secretoPendienteCifrado", secretoPendienteCifrado),
                DbHelper.Param("@paso", paso),
                DbHelper.Param("@hashesCodigos", hashes)) is bool b && b;

        internal static IList<KeyValuePair<int, string>> LeerCodigosBd(int idUsuario)
        {
            DataTable dt = DbHelper.ConsultarTablaSp("sp_US_ObtenerCodigosRecuperacion", DbHelper.Param("@idUsuario", idUsuario));
            return dt.Rows.Cast<DataRow>()
                .Select(r => new KeyValuePair<int, string>(Convert.ToInt32(r["id"]), Convert.ToString(r["hash"])))
                .ToList();
        }

        internal static Tuple<bool, int> UsarCodigoBd(int idUsuario, int idCodigo)
        {
            DataTable dt = DbHelper.ConsultarTablaSp("sp_US_UsarCodigoRecuperacion",
                DbHelper.Param("@idUsuario", idUsuario),
                DbHelper.Param("@idCodigo", idCodigo));
            DataRow r = dt.Rows[0];
            return Tuple.Create(Convert.ToBoolean(r["aceptado"]), Convert.ToInt32(r["restantes"]));
        }

        // ── Utilidades ─────────────────────────────────────────────────────

        /// <summary>10 códigos formateados para mostrar y sus hashes separados por '|' para el SP.</summary>
        internal static string[] GenerarCodigos(out string hashes)
        {
            var codigos = new string[SegundoFactorReglas.CantidadCodigosRecuperacion];
            var lista = new string[codigos.Length];
            for (int i = 0; i < codigos.Length; i++)
            {
                string c = SegundoFactorReglas.GenerarCodigoRecuperacion();
                codigos[i] = SegundoFactorReglas.FormatearCodigoRecuperacion(c);
                lista[i] = SegundoFactorReglas.HashCodigoRecuperacion(c);
            }
            hashes = string.Join("|", lista);
            return codigos;
        }

        /// <summary>Descifra el secreto; si falta la clave o no corresponde, devuelve el resultado de falla cerrada (sin contar el intento).</summary>
        private static ResultadoSegundoFactor Descifrar(Dependencias d, ControlIntentosLogin.Reserva reserva, int idUsuario,
            string cifradoTexto, out byte[] secreto)
        {
            secreto = null;
            try
            {
                secreto = d.Cifrado().Descifrar(cifradoTexto);
                return null;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is CryptographicException)
            {
                d.Control.ErrorAlValidar(reserva);
                LogSGV.Error(ex, "Segundo factor sin clave de cifrado válida (Seguridad.ClaveSegundoFactor): el usuario {IdUsuario} no puede entrar",
                    idUsuario);
                return new ResultadoSegundoFactor { Mensaje = MensajeNoConfigurado, NoConfigurado = true };
            }
        }

        private static ResultadoSegundoFactor Fallo(Dependencias d, ControlIntentosLogin.Reserva reserva, int idUsuario,
            string nombreUsuario, string descripcion)
        {
            bool bloqueado = reserva.BloqueoCuenta;
            d.Auditar(bloqueado ? AccionBloqueado : AccionFallido, idUsuario, nombreUsuario,
                bloqueado ? descripcion + ": bloqueado temporalmente" : descripcion);
            return Falla(bloqueado
                ? SegundoFactorReglas.MensajeBloqueo((int)SegundoFactorReglas.Bloqueo.TotalSeconds)
                : MensajeIncorrecto);
        }

        private static ResultadoSegundoFactor Falla(string mensaje) => new ResultadoSegundoFactor { Mensaje = mensaje };

        private static string MensajeEspera(int segundos) => SegundoFactorReglas.MensajeBloqueo(segundos);

        /// <summary>Con sesión iniciada (MiSeguridad), auditoría normal; en el paso pendiente (sin sesión), como acceso.</summary>
        private static void AuditarSitio(string accion, int idUsuario, string nombreUsuario, string descripcion)
        {
            if (SesionHelper.HaySesionActual)
                AuditoriaHelper.Registrar(accion, "Usuarios", idUsuario, descripcion);
            else
                AuditoriaHelper.RegistrarAcceso(accion, idUsuario, nombreUsuario, descripcion);
        }
    }
}

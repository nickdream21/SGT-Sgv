using System;
using System.Data.SqlClient;
using System.Transactions;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Transacción ambiente para los tests que ESCRIBEN en la BD de pruebas: todo lo que se
    /// ejecute dentro (servicios, SPs, DbHelper) se deshace al hacer Dispose, porque nunca se
    /// llama a Complete(). Las conexiones que abren los servicios se enlistan solas y, al ser
    /// secuenciales con la misma cadena, reutilizan la misma sesión sin escalar a MSDTC.
    /// Las transacciones propias de los servicios (BeginTransaction) y de los SPs quedan
    /// anidadas: su COMMIT no confirma nada hasta que se confirme la de afuera.
    ///
    /// Ojo: si un SP hace ROLLBACK en su CATCH, anula también esta transacción y los comandos
    /// siguientes fallan. Por eso los casos de error deben ser el último paso del test.
    /// </summary>
    public sealed class TransaccionDePrueba : IDisposable
    {
        private readonly TransactionScope _scope;

        public TransaccionDePrueba()
        {
            // Con Enlist=false las conexiones ignorarían la transacción y los datos quedarían
            // guardados de verdad: mejor fallar antes de escribir nada.
            if (!new SqlConnectionStringBuilder(BaseDePruebas.Cadena).Enlist)
                throw new InvalidOperationException("La cadena de conexión de pruebas tiene Enlist=false; los tests que escriben no se pueden deshacer.");

            _scope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions
            {
                // ReadCommitted (no Serializable, el valor por defecto) para no bloquear rangos
                // de tablas que pueda estar usando la app en pruebas mientras corre el test.
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TimeSpan.FromMinutes(5)
            });
        }

        public void Dispose() => _scope.Dispose(); // sin Complete(): rollback
    }
}

using System.IO;
using System.Net;
using System.Web.Script.Serialization;
using WebSGV.Models;

namespace WebSGV.Views
{
    public class DniService
    {
        public static DniResponse ConsultarPorDNI(string numero)
        {
            string url = $"https://api.apis.net.pe/v1/dni?numero={numero}";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                string json = reader.ReadToEnd();
                JavaScriptSerializer js = new JavaScriptSerializer();
                var resultado = js.Deserialize<DniResponse>(json);
                return resultado;
            }
        }
    }
}

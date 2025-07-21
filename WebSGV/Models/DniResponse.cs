namespace WebSGV.Models
{
    public class DniResponse
    {
        public string nombres { get; set; }
        public string apellidoPaterno { get; set; }
        public string apellidoMaterno { get; set; }
        public bool error { get; set; } = false;
        public string message { get; set; }
    }
}

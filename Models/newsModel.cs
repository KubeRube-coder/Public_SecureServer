namespace SecureServer.Models
{
    public class newsModel
    {
        public int id { get; set; }
        public int byWhoNews { get; set; }
        public string? images { get; set; }
        public string title { get; set; }
        public DateTime dateTime { get; set; }
    }
}

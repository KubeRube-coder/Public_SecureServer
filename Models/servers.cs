using System.ComponentModel.DataAnnotations;

namespace SecureServer.Models
{
    public class Servers
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int id { get; set; }
        public int owner_id { get; set; }
        public string name { get; set; }
        public string ip { get; set; }
        public string port { get; set; }
        public string mods { get; set; }
    }
}

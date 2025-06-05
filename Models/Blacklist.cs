using System.ComponentModel.DataAnnotations;

namespace SecureServer.Models
{
    public class Blacklist // Надо ли БЛ?
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int Id { get; set; }
        public string Login { get; set; }
        public string SteamId { get; set; }
        public string DiscordId { get; set; }
        public string Reason { get; set; }
    }
}

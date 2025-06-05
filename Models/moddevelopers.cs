using System.ComponentModel.DataAnnotations;

namespace SecureServer.Models
{
    public class moddevelopers
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int Id { get; set; }
        public string? url_image { get; set; }
        public string nameOfMod { get; set; }
        public string mods { get; set; }
        public string modsby { get; set; }
    }
}

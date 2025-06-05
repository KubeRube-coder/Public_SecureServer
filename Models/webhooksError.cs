using System.ComponentModel.DataAnnotations;

namespace SecureServer.Models
{
    public class webhooksError
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int id {  get; set; }
        public string NameMod { get; set; }
        public string? Discord_web {  get; set; }
        public string? Discord_web_OK {  get; set; }
        public string? Discord_web_ER {  get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace SecureServer.Models
{
    public interface IModProvider
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int Id { get; set; }
        public string Name { get; set; }
        public string NameDWS { get; set; }
        public string? Description { get; set; }
        public string? smallDescription { get; set; }
        public string? categories { get; set; }
        public string? required { get; set; }
        public int price { get; set; }
        public string? image_url { get; set; }
    }

    public class Mod : IModProvider
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string NameDWS { get; set; }
        public string? Description { get; set; }
        public string? smallDescription { get; set; }
        public string? categories { get; set; }
        public string? required { get; set; }
        public int price { get; set; }
        public string? image_url { get; set; }

        public string modsby { get; set; }
    }

    public class PendingMod : IModProvider
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string NameDWS { get; set; }
        public string? Description { get; set; }
        public string? smallDescription { get; set; }
        public string? categories { get; set; }
        public string? required { get; set; }
        public int price { get; set; }
        public string? image_url { get; set; }

        public string? Developer { get; set; }
        public string? refused { get; set; }
    }

    public class ModUser
    {
        public int Id { get; set; }
        public string modsby { get; set; }
        public string? Name { get; set; }
        public string? NameDWS { get; set; }
        public string? Description { get; set; }
        public string? smallDescription { get; set; }
        public string? categories { get; set; }
        public string? required { get; set; }
        public string? image_url { get; set; }
        public int price { get; set; }
        public DateTime expires_date { get; set; }
    }

    public class premmods
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int Id { get; set; }
        public string modsby { get; set; }
        public string mods { get; set; }
        public int premPrice { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace SecureServer.Models
{
    public class subscription
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int Id { get; set; }
        public string login { get; set; }
        public string steamid { get; set; }
        public string subscriptionMods { get; set; }
        public bool subActive { get; set; }
        public bool BuyWhenExpires { get; set; }
        public DateTime boughtDate { get; set; }
        public DateTime expireData { get; set; }
    }
}

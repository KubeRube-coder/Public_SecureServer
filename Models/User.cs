using System.ComponentModel.DataAnnotations;

namespace SecureServer.Models
{
    public class User
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int Id { get; set; }
        public string Login { get; set; }
        public string SteamId {  get; set; }
        public string DiscordId {  get; set; }
        public string Password {  get; set; }
        public bool Banned {  get; set; }
        public string ClaimedMods {  get; set; }
        public string lastip { get; set; }
        public string role { get; set; }
        public float balance { get; set; }
    }

    public class purchasesInfo
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int Id { get; set; } 
        public int whoBuyed { get; set; }
        public int serverId { get; set; }
        public int modId { get; set; }
        public DateTime date { get; set; }
        public bool BuyWhenExpires { get; set; }
        public DateTime expires_date { get; set; }
    }

}

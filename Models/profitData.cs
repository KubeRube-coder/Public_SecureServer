namespace SecureServer.Models
{
    public class profitdata
    {
        public int id {  get; set; }
        public int WhoBought { get; set; }
        public string WhatBought { get; set; }
        public float Amount { get; set; }
        public int WhoEarn {  get; set; }
        public float Profit { get; set; }
        public DateTime Date { get; set; }
        public bool CashedOut { get; set; }
    }
}

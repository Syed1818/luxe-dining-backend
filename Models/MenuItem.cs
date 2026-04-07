using System.ComponentModel.DataAnnotations;

namespace QRMenuAPI.Models
{
    public class MenuItem
    {
        [Key]
        public int ItemID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public bool IsAvailable { get; set; } = true;
    }
}
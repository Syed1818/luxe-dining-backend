using System.ComponentModel.DataAnnotations;

namespace QRMenuAPI.Models
{
    public class OrderItem
    {
        [Key]
        public int OrderItemID { get; set; }
        public int OrderID { get; set; }
        public string ItemID { get; set; }
        public int Quantity { get; set; }
        public string? SpecialInstructions { get; set; }
    }
}
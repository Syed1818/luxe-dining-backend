using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace QRMenuAPI.Models
{
    public class Order
    {
        [Key]
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string OrderID { get; set; } = ObjectId.GenerateNewId().ToString();

        public int TableID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string Status { get; set; } = "Received";
        public DateTime OrderTime { get; set; } = DateTime.UtcNow;

        // Ensure the list is never null
        public List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace QRMenuAPI.Models
{
    public class Order
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string OrderID { get; set; } // Keeps string type for Next.js

        public int TableID { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string Status { get; set; }
        public DateTime OrderTime { get; set; }

        public List<OrderItem> OrderItems { get; set; }
    }
}
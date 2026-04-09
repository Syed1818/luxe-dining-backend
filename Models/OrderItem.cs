using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace QRMenuAPI.Models
{
    public class OrderItem
    {
        [Key]
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        // This generates a unique ID for this specific row in the cart
        public string OrderItemID { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonRepresentation(BsonType.ObjectId)]
        public string ItemID { get; set; } = string.Empty;

        public int Quantity { get; set; }
    }
}
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace QRMenuAPI.Models
{
    public class MenuItem
    {
        // 1. Tell EF Core to map MongoDB's internal _id to this property
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ItemID { get; set; } // Changed from int to string!

        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public bool IsAvailable { get; set; } = true;
    }
}
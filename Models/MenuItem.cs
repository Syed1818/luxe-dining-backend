using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations; // <-- This is the missing line!
using System.ComponentModel.DataAnnotations.Schema;

namespace QRMenuAPI.Models
{
    public class MenuItem
    {
        [Key] 
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ItemID { get; set; } 

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("price")]
        public decimal Price { get; set; }

        [Column("category")]
        public string Category { get; set; } = string.Empty;

        [Column("imageUrl")]
        public string? ImageUrl { get; set; }

        [Column("isAvailable")]
        public bool IsAvailable { get; set; } = true;
    }
}
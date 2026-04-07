using Microsoft.EntityFrameworkCore;
using QRMenuAPI.Models;

namespace QRMenuAPI.Data
{
    public class RestaurantContext : DbContext
    {
        public RestaurantContext(DbContextOptions<RestaurantContext> options) : base(options) { }

        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        // Optional: Map 'RestaurantTables' if you want to query them directly in C#
    }
}
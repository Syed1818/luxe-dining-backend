using Microsoft.EntityFrameworkCore;
using QRMenuAPI.Models;
using MongoDB.EntityFrameworkCore.Extensions;

namespace QRMenuAPI.Data
{
    public class RestaurantContext : DbContext
    {
        public RestaurantContext(DbContextOptions<RestaurantContext> options) : base(options) { }

        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<Order> Orders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<MenuItem>().ToCollection("MenuItems");
            modelBuilder.Entity<Order>().ToCollection("Orders");

            // CRITICAL FIX: Tell C# that OrderItems live INSIDE the Order document!
            modelBuilder.Entity<Order>().OwnsMany(o => o.OrderItems);
        }
    }
}
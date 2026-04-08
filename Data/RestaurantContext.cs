using Microsoft.EntityFrameworkCore;
using QRMenuAPI.Models;
using MongoDB.EntityFrameworkCore.Extensions; // Required for EF Core MongoDB

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

            // FORCE EF Core to look at the exact collections where you pasted the 50 items
            modelBuilder.Entity<MenuItem>().ToCollection("MenuItems");
            modelBuilder.Entity<Order>().ToCollection("Orders");
        }
    }
}
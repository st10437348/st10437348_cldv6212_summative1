using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using ABCRetailers.Models;

namespace ABCRetailers.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<CartRecord> Cart { get; set; } = null!;
    }

    [Table("Cart")]
    public class CartRecord
    {
        public int Id { get; set; }
        public string? CustomerUsername { get; set; }
        public string? ProductId { get; set; }
        public int Quantity { get; set; }
    }
}





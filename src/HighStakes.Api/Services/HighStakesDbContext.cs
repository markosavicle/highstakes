using HighStakes.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HighStakes.Api.Services;

public class HighStakesDbContext : DbContext
{
    public HighStakesDbContext(DbContextOptions<HighStakesDbContext> options) : base(options) { }

    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
    public DbSet<GameRoundEntity> GameRounds => Set<GameRoundEntity>();
    public DbSet<RoundActivityEntity> RoundActivity => Set<RoundActivityEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerEntity>().HasKey(p => p.Id);
        modelBuilder.Entity<GameRoundEntity>().HasKey(g => g.Id);
        modelBuilder.Entity<RoundActivityEntity>().HasKey(t => t.Id);
    }
}

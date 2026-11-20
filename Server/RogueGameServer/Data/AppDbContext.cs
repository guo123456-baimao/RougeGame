using Microsoft.EntityFrameworkCore;
using RogueGameServer.Models;

namespace RogueGameServer.Data;

/// <summary>
/// 数据库上下文。表结构由 Docs/init_database.sql 创建，这里只做映射、不用 EF Migration，
/// 避免出现"迁移脚本与文档里的人工建表 SQL 两份真相"。
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<CharacterOwnership> CharacterOwnerships => Set<CharacterOwnership>();
    public DbSet<EngravingUnlocked> EngravingsUnlocked => Set<EngravingUnlocked>();
    public DbSet<EngravingSelected> EngravingsSelected => Set<EngravingSelected>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 复合主键：这些表没有自增单列主键，必须在这里声明
        modelBuilder.Entity<CharacterOwnership>().HasKey(e => new { e.AccountId, e.CharacterUid });
        modelBuilder.Entity<EngravingUnlocked>().HasKey(e => new { e.AccountId, e.EngravingId });
        modelBuilder.Entity<EngravingSelected>().HasKey(e => new { e.AccountId, e.EngravingId });

        modelBuilder.Entity<EngravingSelected>()
            .HasIndex(e => new { e.AccountId, e.SlotIndex })
            .IsUnique();

        modelBuilder.Entity<Account>()
            .HasIndex(a => a.Username)
            .IsUnique();
    }
}

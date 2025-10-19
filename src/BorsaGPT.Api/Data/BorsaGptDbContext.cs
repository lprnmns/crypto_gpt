using Microsoft.EntityFrameworkCore;
using BorsaGPT.Api.Models;

namespace BorsaGPT.Api.Data;

/// <summary>
/// EF Core veritabanı bağlamı (Database Context).
/// PostgreSQL ile iletişim için tüm tablolar burada tanımlanır.
/// </summary>
public class BorsaGptDbContext : DbContext
{
    /// <summary>
    /// Constructor: EF Core configuration'ı alır (connection string vs.)
    /// </summary>
    /// <param name="options">DbContext ayarları (appsettings.json'dan gelir)</param>
    public BorsaGptDbContext(DbContextOptions<BorsaGptDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// candidate_wallets tablosuna erişim için DbSet.
    /// Kullanımı: _context.CandidateWallets.Where(x => !x.Analyzed).ToList()
    /// </summary>
    public DbSet<CandidateWallet> CandidateWallets { get; set; }

    /// <summary>
    /// candidate_analysis tablosuna erişim için DbSet.
    /// Flash crash (10 Ekim 2025) sırasında cüzdan performans analizleri.
    /// </summary>
    public DbSet<CandidateAnalysis> CandidateAnalysis { get; set; }

    /// <summary>
    /// Model oluşturma sırasında ek ayarlar (index, constraints vs.)
    /// </summary>
    /// <param name="modelBuilder">EF Core model builder</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // CandidateWallet tablosu için ek ayarlar
        modelBuilder.Entity<CandidateWallet>(entity =>
        {
            // wallet_address kolonuna UNIQUE index
            // Aynı cüzdanı iki kez eklememek için
            entity.HasIndex(e => e.WalletAddress)
                .IsUnique()
                .HasDatabaseName("ix_candidate_wallets_wallet_address");

            // analyzed kolonuna index (hızlı "analiz edilmemiş kayıtlar" sorgusu için)
            entity.HasIndex(e => e.Analyzed)
                .HasDatabaseName("ix_candidate_wallets_analyzed");

            // block_number kolonuna index (blok numarasına göre sıralama/filtreleme için)
            entity.HasIndex(e => e.BlockNumber)
                .HasDatabaseName("ix_candidate_wallets_block_number");

            // detected_at kolonuna index (tarih bazlı sorgular için)
            entity.HasIndex(e => e.DetectedAt)
                .HasDatabaseName("ix_candidate_wallets_detected_at");
        });

        // CandidateAnalysis tablosu için ek ayarlar
        modelBuilder.Entity<CandidateAnalysis>(entity =>
        {
            // wallet_address + t0_timestamp + t1_timestamp için UNIQUE index
            // Aynı cüzdan için aynı zaman aralığında birden fazla analiz yapılmaması için
            entity.HasIndex(e => new { e.CandidateWalletId, e.T0Timestamp, e.T1Timestamp })
                .IsUnique()
                .HasDatabaseName("ix_candidate_analysis_unique_timeframe");

            // simple_return kolonuna index (PnL'ye göre sıralama için)
            entity.HasIndex(e => e.SimpleReturn)
                .HasDatabaseName("ix_candidate_analysis_simple_return");

            // adjusted_return kolonuna index (düzeltilmiş PnL sıralaması için)
            entity.HasIndex(e => e.AdjustedReturn)
                .HasDatabaseName("ix_candidate_analysis_adjusted_return");

            // Foreign key relationship
            entity.HasOne(e => e.CandidateWallet)
                .WithMany()
                .HasForeignKey(e => e.CandidateWalletId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

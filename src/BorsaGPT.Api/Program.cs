using Microsoft.EntityFrameworkCore;
using BorsaGPT.Api.Data;
using BorsaGPT.Api.Models;
using BorsaGPT.Api.Models.Dtos;
using BorsaGPT.Api.Services;
using BorsaGPT.Api.Exceptions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// HttpClient factory (TokenPriceService için CoinGecko API çağrıları)
builder.Services.AddHttpClient();

// PostgreSQL veritabanı bağlantısını kaydet
// Connection string'i appsettings.json'dan oku
builder.Services.AddDbContext<BorsaGptDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString); // Npgsql = PostgreSQL sağlayıcısı
});

// Singleton servis: Token fiyatları (cache paylaşımlı)
builder.Services.AddSingleton<TokenPriceService>();

// Singleton servis: Progress tracker (checkpoint yönetimi)
builder.Services.AddSingleton<ProgressTrackerService>();

// Singleton servis: External API servisleri
builder.Services.AddSingleton<EtherscanService>();
builder.Services.AddSingleton<AlchemyHistoricalService>();
builder.Services.AddSingleton<PriceHistoryService>();

// Scoped servis: Wallet analyzer (per-request)
builder.Services.AddScoped<WalletAnalyzerService>();

// Background Service: Blockchain Spider
if (builder.Configuration.GetValue<bool?>("Spider:Enabled") ?? true)
{
    builder.Services.AddHostedService<BlockchainSpiderService>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ==================== API Endpoint'leri ====================

// GET /api/candidates - Tüm aday cüzdanları listele
app.MapGet("/api/candidates", async (BorsaGptDbContext db) =>
{
    // Veritabanından tüm kayıtları çek (en yeni kayıtlar önce)
    var candidates = await db.CandidateWallets
        .OrderByDescending(c => c.DetectedAt) // En yeni kayıtlar üstte
        .ToListAsync();

    // JSON olarak döndür
    return Results.Ok(candidates);
})
.WithName("GetAllCandidates")
.WithTags("Candidates") // Swagger'da gruplandırma için
.WithOpenApi();

// GET /api/candidates/unanalyzed - Henüz analiz edilmemiş kayıtlar
app.MapGet("/api/candidates/unanalyzed", async (BorsaGptDbContext db) =>
{
    // analyzed = false olan kayıtları çek
    var unanalyzed = await db.CandidateWallets
        .Where(c => !c.Analyzed) // Analiz edilmemiş
        .OrderBy(c => c.DetectedAt) // En eski kayıtlar önce (FIFO)
        .ToListAsync();

    return Results.Ok(unanalyzed);
})
.WithName("GetUnanalyzedCandidates")
.WithTags("Candidates")
.WithOpenApi();

// GET /api/candidates/{id} - Belirli bir kaydı ID ile getir
app.MapGet("/api/candidates/{id}", async (long id, BorsaGptDbContext db) =>
{
    // ID'ye göre kayıt ara
    var candidate = await db.CandidateWallets.FindAsync(id);

    // Kayıt yoksa 404 döndür
    if (candidate == null)
    {
        return Results.NotFound(new { message = $"ID={id} bulunamadı" });
    }

    return Results.Ok(candidate);
})
.WithName("GetCandidateById")
.WithTags("Candidates")
.WithOpenApi();

// POST /api/candidates - Yeni aday cüzdan ekle
app.MapPost("/api/candidates", async (CreateCandidateDto dto, BorsaGptDbContext db) =>
{
    // DTO validasyonu otomatik olarak yapılır ([Required], [Range] vs.)

    // Aynı cüzdan adresi zaten var mı kontrol et (unique index sayesinde)
    var existingWallet = await db.CandidateWallets
        .FirstOrDefaultAsync(c => c.WalletAddress == dto.WalletAddress);

    if (existingWallet != null)
    {
        // Eğer varsa, 409 Conflict döndür
        return Results.Conflict(new
        {
            message = "Bu cüzdan adresi zaten kayıtlı",
            existingId = existingWallet.Id
        });
    }

    // Yeni entity oluştur (DTO'dan entity'e mapping)
    var candidate = new CandidateWallet
    {
        WalletAddress = dto.WalletAddress,
        DetectedAt = DateTime.UtcNow, // Şu anki zaman (UTC)
        FirstTransferAmountEth = dto.FirstTransferAmountEth,
        FirstTransferToken = dto.FirstTransferToken,
        BlockNumber = dto.BlockNumber,
        Analyzed = false // Varsayılan olarak analiz edilmemiş
    };

    // Veritabanına ekle
    db.CandidateWallets.Add(candidate);
    await db.SaveChangesAsync(); // Değişiklikleri kaydet (INSERT sorgusu çalışır)

    // 201 Created döndür (Location header ile birlikte)
    return Results.Created($"/api/candidates/{candidate.Id}", candidate);
})
.WithName("CreateCandidate")
.WithTags("Candidates")
.WithOpenApi();

// ==================== Analiz Endpoint'leri ====================

// POST /api/analysis/start - Cüzdan analizini başlat
app.MapPost("/api/analysis/start", async (
    WalletAnalyzerService analyzer, 
    ILogger<Program> logger) =>
{
    try
    {
        logger.LogWarning("🚀 [ENDPOINT] Analiz endpoint'i çağrıldı");
        
        // Analizi başlat (arka planda değil, blokleyici)
        // Not: Bu endpoint uzun sürebilir (1-3 saat)
        await analyzer.AnalyzeAllWalletsAsync();
        
        logger.LogInformation("✅ [ENDPOINT] Analiz başarıyla tamamlandı");
        return Results.Ok(new { message = "Analiz tamamlandı", success = true });
    }
    catch (RateLimitException ex)
    {
        logger.LogError("❌ [ENDPOINT] Rate limit hatası: {Message}", ex.Message);
        return Results.StatusCode(429); // Too Many Requests
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
})
.WithName("StartAnalysis")
.WithTags("Analysis")
.WithOpenApi();

// GET /api/analysis/results - Analiz sonuçlarını getir (PnL + toplam value ile sıralı)
app.MapGet("/api/analysis/results", async (BorsaGptDbContext db, decimal? minValueT0, decimal? minSimpleReturn, bool? excludeStableHeavy, int top = 100) =>
{
    var query = db.CandidateAnalysis.AsQueryable();

    if (minValueT0.HasValue)
    {
        query = query.Where(a => a.ValueT0Usd >= minValueT0.Value);
    }

    if (minSimpleReturn.HasValue)
    {
        query = query.Where(a => (a.SimpleReturn ?? decimal.MinValue) >= minSimpleReturn.Value);
    }

    if (excludeStableHeavy == true)
    {
        query = query.Where(a => !a.StableHeavy);
    }

    var limit = Math.Clamp(top, 1, 1000);

    var results = await query
        .OrderByDescending(a => a.SimpleReturn ?? decimal.MinValue)
        .Take(limit)
        .ToListAsync();

    return Results.Ok(results);
})
.WithName("GetAnalysisResults")
.WithTags("Analysis")
.WithOpenApi();

// GET /api/analysis/export-csv - Analiz sonuçlarını CSV olarak indir (PnL sıralı)
app.MapGet("/api/analysis/export-csv", async (BorsaGptDbContext db) =>
{
    // Tüm analiz kayıtlarını PnL sıralamasıyla çek
    var results = await db.CandidateAnalysis
        .OrderByDescending(a => a.SimpleReturn ?? decimal.MinValue) // En kârlılar üstte
        .ToListAsync();
    
    // CSV header ve satırlarını oluştur
    var csv = new System.Text.StringBuilder();
    csv.AppendLine("WalletAddress,T0_Block,T1_Block,ValueT0_USD,ValueT1_USD,SimpleReturn_Percent,TokenCount,PriceMissing,AnalyzedAt,Notes");
    
    foreach (var row in results)
    {
        // CSV'de virgül içeren notlar için escape (çift tırnak sarmalama)
        var notes = row.Notes?.Replace("\"", "\"\"") ?? "";
        
        // ⚠️ CultureInfo: Decimal sayılar için nokta kullan (virgül yerine)
        var v0 = row.ValueT0Usd?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0";
        var v1 = row.ValueT1Usd?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0";
        var pnl = row.SimpleReturn?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0";
        
        csv.AppendLine($"{row.WalletAddress},{row.T0Block},{row.T1Block},{v0},{v1},{pnl},{row.TokenCount},{row.PriceMissing},\"{row.AnalyzedAt:yyyy-MM-dd HH:mm:ss}\",\"{notes}\"");
    }
    
    // CSV dosyası olarak döndür (Content-Disposition header ile indirme tetikle)
    var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
    return Results.File(bytes, "text/csv", $"candidate_analysis_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
})
.WithName("ExportAnalysisCSV")
.WithTags("Analysis")
.WithOpenApi();

app.Run();





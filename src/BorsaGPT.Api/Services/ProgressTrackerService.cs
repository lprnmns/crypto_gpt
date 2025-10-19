using System.Text.Json;
using BorsaGPT.Api.Models;

namespace BorsaGPT.Api.Services;

/// <summary>
/// Manages persisting analysis checkpoints to progress.json so long running jobs can resume safely.
/// Also guards against stale checkpoints by tagging them with an analysis window key.
/// </summary>
public class ProgressTrackerService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _progressFilePath;
    private readonly ILogger<ProgressTrackerService> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    private string ProgressTextPath => Path.ChangeExtension(_progressFilePath, ".txt");

    public ProgressTrackerService(ILogger<ProgressTrackerService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _progressFilePath = configuration["ProgressTracker:FilePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "progress.json");

        _logger.LogInformation("Progress tracker initialized: {FilePath}", _progressFilePath);
    }

    /// <summary>
    /// Load an existing checkpoint. If the stored analysis window does not match, the checkpoint is discarded.
    /// </summary>
    public async Task<AnalysisProgress?> LoadProgressAsync(string windowKey)
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_progressFilePath))
            {
                _logger.LogInformation("No progress.json found. A new analysis run will start from scratch.");
                return null;
            }

            var json = await File.ReadAllTextAsync(_progressFilePath);
            var progress = JsonSerializer.Deserialize<AnalysisProgress>(json, SerializerOptions);

            if (progress == null)
            {
                _logger.LogWarning("progress.json could not be parsed. Resetting checkpoint file.");
                DeleteCheckpointFiles_NoLock();
                return null;
            }

            if (!string.Equals(progress.AnalysisWindowKey, windowKey, StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "Stale checkpoint detected. Stored window {StoredWindow}, expected {Window}. Resetting progress.",
                    progress.AnalysisWindowKey,
                    windowKey);

                DeleteCheckpointFiles_NoLock();
                return null;
            }

            _logger.LogInformation(
                "Checkpoint loaded: {Processed}/{Total} wallets processed. Last wallet id {LastId}.",
                progress.ProcessedCount,
                progress.TotalWallets,
                progress.LastProcessedWalletId);

            return progress;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read progress.json");
            return null;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Create a brand new checkpoint instance for the current analysis window.
    /// </summary>
    public AnalysisProgress CreateNew(int totalWallets, string windowKey)
    {
        var now = DateTime.UtcNow;
        var progress = new AnalysisProgress
        {
            LastProcessedWalletId = 0,
            LastProcessedAddress = string.Empty,
            TotalWallets = totalWallets,
            ProcessedCount = 0,
            AnalysisWindowKey = windowKey,
            StartedAt = now,
            LastCheckpoint = now,
            RateLimitHit = false,
            IsCompleted = false
        };

        _logger.LogInformation("New analysis started: {Total} wallets queued for window {Window}.", totalWallets, windowKey);
        return progress;
    }

    /// <summary>
    /// Persist the current checkpoint to disk and emit a human readable summary alongside it.
    /// </summary>
    public async Task SaveProgressAsync(AnalysisProgress progress)
    {
        await _fileLock.WaitAsync();
        try
        {
            progress.LastCheckpoint = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(progress, SerializerOptions);
            await File.WriteAllTextAsync(_progressFilePath, json);
            await File.WriteAllTextAsync(ProgressTextPath, BuildTextSummary(progress), System.Text.Encoding.UTF8);

            _logger.LogDebug("Checkpoint saved: {Processed}/{Total}", progress.ProcessedCount, progress.TotalWallets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write progress checkpoint");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task MarkRateLimitAsync(AnalysisProgress progress, string provider, TimeSpan retryAfter)
    {
        progress.RateLimitHit = true;
        progress.RateLimitProvider = provider;
        progress.NextRetryAfter = DateTime.UtcNow.Add(retryAfter);

        await SaveProgressAsync(progress);

        _logger.LogWarning("Rate limit detected for {Provider}. Next retry at {RetryTime}.", provider, progress.NextRetryAfter);
    }

    public async Task MarkCompletedAsync(AnalysisProgress progress)
    {
        progress.IsCompleted = true;
        progress.LastCheckpoint = DateTime.UtcNow;

        await SaveProgressAsync(progress);

        _logger.LogInformation("Analysis completed: {Total} wallets processed.", progress.TotalWallets);
    }

    public async Task ClearProgressAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            DeleteCheckpointFiles_NoLock();
            _logger.LogInformation("progress.json cleared manually.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete progress checkpoint");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Emit a log-based progress bar without risking divide-by-zero when the run just started.
    /// </summary>
    public void LogProgress(AnalysisProgress progress)
    {
        if (progress.TotalWallets <= 0)
        {
            _logger.LogInformation("Progress: no wallets queued yet.");
            return;
        }

        if (progress.ProcessedCount <= 0)
        {
            _logger.LogInformation("Progress: 0/{Total} processed (0%).", progress.TotalWallets);
            return;
        }

        var percentage = (double)progress.ProcessedCount / progress.TotalWallets * 100d;
        var elapsed = DateTime.UtcNow - progress.StartedAt;
        var averageSecondsPerWallet = elapsed.TotalSeconds / progress.ProcessedCount;
        var estimatedTotalSeconds = averageSecondsPerWallet * progress.TotalWallets;
        var remainingSeconds = Math.Max(0, estimatedTotalSeconds - elapsed.TotalSeconds);
        var remaining = TimeSpan.FromSeconds(remainingSeconds);

        _logger.LogInformation(
            "Progress: {Processed}/{Total} ({Percentage:F1}%) | ETA ~{Remaining}",
            progress.ProcessedCount,
            progress.TotalWallets,
            percentage,
            remaining.ToString(@"hh\:mm\:ss"));
    }

    private void DeleteCheckpointFiles_NoLock()
    {
        try
        {
            if (File.Exists(_progressFilePath))
            {
                File.Delete(_progressFilePath);
            }

            var txtPath = ProgressTextPath;
            if (!string.IsNullOrEmpty(txtPath) && File.Exists(txtPath))
            {
                File.Delete(txtPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete checkpoint files");
        }
    }

    private static string BuildTextSummary(AnalysisProgress progress)
    {
        var status = progress.IsCompleted ? "TAMAMLANDI" : "DEVAM EDIYOR";
        var errorLine = string.IsNullOrWhiteSpace(progress.ErrorMessage)
            ? string.Empty
            : $"Hata: {progress.ErrorMessage}{Environment.NewLine}";

        return $"=== BorsaGPT Analysis Progress ==={Environment.NewLine}" +
               $"Window: {progress.AnalysisWindowKey}{Environment.NewLine}" +
               $"Last Update: {progress.LastCheckpoint:yyyy-MM-dd HH:mm:ss} UTC{Environment.NewLine}" +
               $"Total Wallets: {progress.TotalWallets}{Environment.NewLine}" +
               $"Processed: {progress.ProcessedCount}{Environment.NewLine}" +
               $"Remaining: {progress.TotalWallets - progress.ProcessedCount}{Environment.NewLine}" +
               $"Last Wallet Id: {progress.LastProcessedWalletId}{Environment.NewLine}" +
               $"Last Wallet Address: {progress.LastProcessedAddress}{Environment.NewLine}" +
               $"Status: {status}{Environment.NewLine}" +
               errorLine;
    }
}


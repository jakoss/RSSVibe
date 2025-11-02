using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RSSVibe.Data;
using RSSVibe.Data.Entities;
using RSSVibe.Data.Models;
using System.Net;

namespace RSSVibe.Services.FeedAnalyses;

/// <summary>
/// Service implementation for feed analysis operations.
/// </summary>
internal sealed class FeedAnalysisService(
    RssVibeDbContext dbContext,
    ILogger<FeedAnalysisService> logger,
    IPreflightService preflightService) : IFeedAnalysisService
{
    private const int _reanalysisCooldownMinutes = 15;

    public async Task<CreateFeedAnalysisResult> CreateFeedAnalysisAsync(
        CreateFeedAnalysisCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Normalize URL
            var normalizedUrl = NormalizeUrl(command.TargetUrl);

            // SSRF protection
            var targetUri = new Uri(command.TargetUrl);
            if (IsInternalUrl(targetUri))
            {
                logger.LogWarning("SSRF protection triggered for URL: {Url}", normalizedUrl);
                return new CreateFeedAnalysisResult
                {
                    Success = false,
                    Error = FeedAnalysisError.ForbiddenUrl,
                    ErrorDetail = "Target URL points to restricted network"
                };
            }

            // Check for duplicate analysis
            var existingAnalysis = await dbContext.FeedAnalyses
                .AsNoTracking()
                .Where(a => a.UserId == command.UserId && a.NormalizedUrl == normalizedUrl)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new { a.Id, a.CreatedAt, a.AnalysisStatus }) // Only select needed fields
                .FirstOrDefaultAsync(cancellationToken);

            if (existingAnalysis is not null)
            {
                if (!command.ForceReanalysis)
                {
                    logger.LogInformation(
                        "Duplicate analysis detected for user {UserId}, URL: {Url}",
                        command.UserId, normalizedUrl);

                    return new CreateFeedAnalysisResult
                    {
                        Success = false,
                        Error = FeedAnalysisError.DuplicateAnalysis,
                        ErrorDetail = "An analysis for this URL already exists"
                    };
                }

                // Check cooldown period
                var cooldownEnd = existingAnalysis.CreatedAt.AddMinutes(_reanalysisCooldownMinutes);
                if (DateTimeOffset.UtcNow < cooldownEnd)
                {
                    logger.LogInformation(
                        "Reanalysis cooldown active for user {UserId}, URL: {Url}",
                        command.UserId, normalizedUrl);

                    return new CreateFeedAnalysisResult
                    {
                        Success = false,
                        Error = FeedAnalysisError.ReanalysisCooldown,
                        ErrorDetail = $"Please wait until {cooldownEnd:u} before requesting reanalysis"
                    };
                }

                // Mark existing analysis as superseded
                var existingEntity = await dbContext.FeedAnalyses
                    .Where(a => a.Id == existingAnalysis.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existingEntity is not null)
                {
                    existingEntity.AnalysisStatus = (FeedAnalysisStatus)Contracts.FeedAnalyses.FeedAnalysisStatus.Superseded;
                }
            }

            // Perform synchronous preflight checks
            var preflightResult = await preflightService.PerformPreflightChecksAsync(
                command.TargetUrl,
                cancellationToken);

            // If preflight checks indicate critical failure, reject the request
            if (preflightResult.IsCriticalFailure)
            {
                logger.LogWarning(
                    "Preflight checks failed for URL: {Url}, Checks: {Checks}",
                    normalizedUrl, preflightResult.Checks);

                return new CreateFeedAnalysisResult
                {
                    Success = false,
                    Error = FeedAnalysisError.PreflightFailed,
                    ErrorDetail = $"Preflight validation failed: {string.Join(", ", preflightResult.Warnings)}"
                };
            }

            // Create new analysis entity
            var analysis = new FeedAnalysis
            {
                Id = Guid.CreateVersion7(),
                UserId = command.UserId,
                TargetUrl = command.TargetUrl,
                NormalizedUrl = normalizedUrl,
                AnalysisStatus = (FeedAnalysisStatus)Contracts.FeedAnalyses.FeedAnalysisStatus.Pending,
                PreflightChecks = preflightResult.Checks,
                PreflightDetails = preflightResult.Details,
                Warnings = preflightResult.Warnings,
                AiModel = command.AiModel,
                CreatedAt = DateTimeOffset.UtcNow,
                Selectors = new FeedSelectors()
            };

            dbContext.FeedAnalyses.Add(analysis);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Feed analysis created: {AnalysisId} for user {UserId}, URL: {Url}, PreflightChecks: {Checks}",
                analysis.Id, command.UserId, normalizedUrl, analysis.PreflightChecks);

            // TODO: Enqueue background job for AI analysis

            return new CreateFeedAnalysisResult
            {
                Success = true,
                AnalysisId = analysis.Id,
                NormalizedUrl = analysis.NormalizedUrl,
                Status = analysis.AnalysisStatus.ToString(),
                PreflightChecks = analysis.PreflightChecks,
                PreflightDetails = analysis.PreflightDetails,
                CreatedAt = analysis.CreatedAt
            };
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("unique constraint") == true)
        {
            logger.LogWarning(ex, "Unique constraint violation during analysis creation");
            return new CreateFeedAnalysisResult
            {
                Success = false,
                Error = FeedAnalysisError.DuplicateAnalysis,
                ErrorDetail = "Duplicate analysis detected"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating feed analysis for user {UserId}", command.UserId);
            return new CreateFeedAnalysisResult
            {
                Success = false,
                Error = FeedAnalysisError.DatabaseError,
                ErrorDetail = "An error occurred while creating the analysis"
            };
        }
    }

    private static string NormalizeUrl(string url)
    {
        var uri = new Uri(url);
        var normalized = $"{uri.Scheme}://{uri.Host.ToLowerInvariant()}{uri.AbsolutePath.TrimEnd('/')}";
        if (!string.IsNullOrEmpty(uri.Query))
        {
            normalized += uri.Query;
        }
        return normalized;
    }

    private static bool IsInternalUrl(Uri uri)
    {
        if (uri.IsLoopback)
        {
            return true;
        }

        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(uri.Host, out var ipAddress))
        {
            return false;
        }

        var bytes = ipAddress.GetAddressBytes();
        if (bytes.Length != 4) // IPv4
        {
            return false;
        }

        switch (bytes[0])
        {
            case 10:
            // 172.16.0.0/12
            case 172 when bytes[1] is >= 16 and <= 31:
                return true; // 10.0.0.0/8
        }

        if (bytes[0] == 192 && bytes[1] == 168)
        {
            return true; // 192.168.0.0/16
        }

        if (bytes[0] == 169 && bytes[1] == 254)
        {
            return true; // 169.254.0.0/16 (link-local)
        }

        return false;
    }
}

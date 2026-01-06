using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleExtract.Helpers;

/// <summary>
/// Helper class for cleaning up unwanted subtitle files based on language filters.
/// </summary>
public static class DoxExtractor
{
    /// <summary>
    /// Removes unwanted subtitle files that don't match the specified language filters.
    /// </summary>
    /// <param name="encoder">The subtitle encoder instance.</param>
    /// <param name="mediaSource">The media source containing subtitle streams.</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    /// <param name="extractSpanish">Whether to keep Spanish subtitles.</param>
    /// <param name="extractEnglish">Whether to keep English subtitles.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task CleanupUnwantedSubtitles(
        ISubtitleEncoder encoder,
        MediaSourceInfo mediaSource,
        ILogger logger,
        bool extractSpanish,
        bool extractEnglish,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("======= DOX extraction start =======");
        logger.LogInformation("[Language Filter] Active filters - Spanish: {Spanish}, English: {English}", extractSpanish, extractEnglish);

        var unwantedMedia = new HashSet<string>();
        foreach (var stream in mediaSource.MediaStreams)
        {
            if (stream.Type == MediaStreamType.Subtitle)
            {
                if (!LanguageFilter.ShouldExtractSubtitle(stream, extractSpanish, extractEnglish))
                {
                    var path = await encoder.GetSubtitleFilePath(stream, mediaSource, cancellationToken).ConfigureAwait(false);
                    logger.LogInformation("[Language Filter] Path unwanted: {Path}", path);
                    unwantedMedia.Add(path);
                }
            }
        }

        if (unwantedMedia.Count == 0)
        {
            logger.LogInformation("[Language Filter] No unwanted subtitle files to clean up");
            return;
        }

        /*string? subtitleDir = GetSubtitleDirectory(unwantedMedia, logger);

        if (string.IsNullOrEmpty(subtitleDir) || !Directory.Exists(subtitleDir))
        {
            logger.LogWarning("[Language Filter] Subtitle directory not found: {Dir}", subtitleDir);
            return;
        }
        */

        await ReplaceUnwantedFilesWithDummies(unwantedMedia, logger, cancellationToken).ConfigureAwait(false);
    }

   /* /// <summary>
    /// Determines the subtitle directory from the unwanted media paths.
    /// </summary>
    /// <param name="unwantedMedia">Set of unwanted media file paths.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>The subtitle directory path.</returns>
    private static string? GetSubtitleDirectory(HashSet<string> unwantedMedia, ILogger logger)
    {
        string? subtitleDir = null;

        if (unwantedMedia.Count > 0)
        {
            subtitleDir = Path.GetDirectoryName(unwantedMedia.First());
            logger.LogInformation("[Language Filter] First directory: {Dir}", subtitleDir);

            if (!string.IsNullOrEmpty(subtitleDir))
            {
                subtitleDir = Path.GetDirectoryName(subtitleDir); // Go up one level to /config/data/subtitles
                logger.LogInformation("[Language Filter] Using subtitle directory: {Dir}", subtitleDir);
            }
        }

        if (string.IsNullOrEmpty(subtitleDir))
        {
            subtitleDir = "/config/data/subtitles";
            logger.LogInformation("[Language Filter] Using default subtitle directory: {Dir}", subtitleDir);
        }

        return subtitleDir;
    }*/

    /// <summary>
    /// Replaces recently created unwanted subtitle files with empty dummy files.
    /// </summary>
    /// <param name="unwantedMedia">Set of unwanted media file paths.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task ReplaceUnwantedFilesWithDummies(
        HashSet<string> unwantedMedia,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var filePath in unwantedMedia)
        {
            try
            {
                File.Delete(filePath);
                await File.WriteAllTextAsync(filePath, string.Empty, cancellationToken).ConfigureAwait(false);
                logger.LogInformation("[Language Filter] Replaced unwanted subtitle with empty dummy: {Path}", filePath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Language Filter] Failed to replace file with dummy: {Path}", filePath);
            }
        }
    }
}

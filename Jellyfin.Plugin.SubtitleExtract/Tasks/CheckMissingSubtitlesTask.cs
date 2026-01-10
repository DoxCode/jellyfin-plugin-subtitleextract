using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Extensions;
using Jellyfin.Plugin.SubtitleExtract.Helpers;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleExtract.Tasks;

/// <summary>
/// Scheduled task to check for episodes missing extracted subtitles.
/// </summary>
public class CheckMissingSubtitlesTask : IScheduledTask
{
    private const int QueryPageLimit = 250;
    private const string SubtitlesBasePath = "/config/data/subtitles";

    private readonly ILibraryManager _libraryManager;
    private readonly ILocalizationManager _localization;
    private readonly ILogger<CheckMissingSubtitlesTask> _logger;

    private readonly ISubtitleEncoder _encoder;

    private static readonly BaseItemKind[] _itemTypes = [BaseItemKind.Episode];
    private static readonly MediaType[] _mediaTypes = [MediaType.Video];
    private static readonly SourceType[] _sourceTypes = [SourceType.Library];
    private static readonly DtoOptions _dtoOptions = new(false);

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckMissingSubtitlesTask" /> class.
    /// </summary>
    /// <param name="libraryManager">Instance of <see cref="ILibraryManager"/> interface.</param>
    /// <param name="localization">Instance of <see cref="ILocalizationManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
    /// <param name="subtitleEncoder"><see cref="ISubtitleEncoder"/> instance.</param>
    public CheckMissingSubtitlesTask(
        ILibraryManager libraryManager,
        ILocalizationManager localization,
        ILogger<CheckMissingSubtitlesTask> logger,
        ISubtitleEncoder subtitleEncoder)
    {
        _libraryManager = libraryManager;
        _localization = localization;
        _logger = logger;
        _encoder = subtitleEncoder;
    }

    /// <inheritdoc />
    public string Key => "CheckMissingSubtitles";

    /// <inheritdoc />
    public string Name => "[== DOX ==] Check Missing Subtitles";

    /// <inheritdoc />
    public string Description => "Checks for episodes without extracted subtitles.";

    /// <inheritdoc />
    public string Category => _localization.GetLocalizedString("TasksLibraryCategory");

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return [];
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var startProgress = 0d;

        var config = SubtitleExtractPlugin.Current.Configuration;
        var libs = config.SelectedSubtitlesLibraries;

        _logger.LogInformation("====== DOX: Task iniciada ======");
        _logger.LogInformation("Librerías configuradas: {Count} - {Libs}", libs.Length, string.Join(", ", libs));

        Guid[] parentIds = [];
        if (libs.Length > 0)
        {
            // Try to get parent ids from the selected libraries
            parentIds = _libraryManager.GetVirtualFolders()
                .Where(vf => libs.Contains(vf.Name))
                .Select(vf => Guid.Parse(vf.ItemId))
                .ToArray();

            _logger.LogInformation("ParentIds encontrados: {Count}", parentIds.Length);
        }

        if (parentIds.Length > 0)
        {
            // In case parent ids are found, run the check on each found library
            foreach (var parentId in parentIds)
            {
                startProgress = await RunCheckWithProgress(progress, parentId, parentIds, startProgress, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            // Otherwise run it on everything
            _logger.LogInformation("No se encontraron librerías específicas, ejecutando en todo");
            await RunCheckWithProgress(progress, null, [], startProgress, cancellationToken).ConfigureAwait(false);
        }

        progress.Report(100);
        _logger.LogInformation("====== DOX: Task finalizada ======");
    }

    private async Task<double> RunCheckWithProgress(
        IProgress<double> progress,
        Guid? parentId,
        IReadOnlyCollection<Guid> parentIds,
        double startProgress,
        CancellationToken cancellationToken)
    {
        var libsCount = parentIds.Count > 0 ? parentIds.Count : 1;

        _logger.LogInformation("====== DOX: Starting RunCheckWithProgress ======");
        _logger.LogInformation("ParentId: {ParentId}", parentId?.ToString() ?? "NULL (todas las librerías)");

        var query = new InternalItemsQuery
        {
            Recursive = true,
            IsVirtualItem = false,
            IncludeItemTypes = _itemTypes,
            DtoOptions = _dtoOptions,
            MediaTypes = _mediaTypes,
            SourceTypes = _sourceTypes,
            Limit = QueryPageLimit
        };

        if (!parentId.IsNullOrEmpty())
        {
            query.ParentId = parentId.Value;
        }

        var numberOfEpisodes = _libraryManager.GetCount(query);
        _logger.LogInformation("Total de episodios encontrados: {Count}", numberOfEpisodes);

        var startIndex = 0;
        var completedEpisodes = 0;

        while (startIndex < numberOfEpisodes)
        {
            query.StartIndex = startIndex;
            var episodes = _libraryManager.GetItemList(query);

            foreach (var episode in episodes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var episodeId = episode.Id.ToString("D");

                // Check if subtitles exist for this episode
                if (!HasExtractedSubtitles(episodeId))
                {
                    _logger.LogInformation("Episode {EpisodeName} (ID: {EpisodeId}) is missing extracted subtitles", episode.Name, episodeId);

                    foreach (var mediaSource in episode.GetMediaSources(false))
                    {
                        await _encoder.ExtractAllExtractableSubtitles(mediaSource, cancellationToken).ConfigureAwait(false);
                        await DoxExtractor.CleanupUnwantedSubtitles(
                            _encoder,
                            mediaSource,
                            _logger,
                            true,
                            true,
                            cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    _logger.LogInformation("Episode {EpisodeName} (ID: {EpisodeId}) YA tiene los subs exportados... skip...", episode.Name, episodeId);
                }

                completedEpisodes++;

                // Report the progress using "startProgress" that allows to track progress across multiple libraries
                progress.Report(startProgress + (100d * completedEpisodes / numberOfEpisodes / libsCount));
            }

            startIndex += QueryPageLimit;
        }

        // When done, update the startProgress to the current progress for next libraries
        startProgress += 100d * completedEpisodes / numberOfEpisodes / libsCount;
        return startProgress;
    }

    /// <summary>
    /// Checks if an episode has extracted subtitles.
    /// </summary>
    /// <param name="episodeId">The episode ID (with dashes, format: 047cd2da-002a-2bd0-eab6-aaaccbed3dd2).</param>
    /// <returns>True if extracted subtitles exist and have valid size, false otherwise.</returns>
    private bool HasExtractedSubtitles(string episodeId)
    {
        // Get the first two characters of the ID for the subdirectory
        var subDir = episodeId.Substring(0, 2);
        // Build the path: /config/data/subtitles/XX/<ID>
        var subtitlePath = Path.Combine(SubtitlesBasePath, subDir, episodeId);

        // Check if the directory exists
        if (!Directory.Exists(subtitlePath))
        {
            return false;
        }

        // Check if there are any files with size > 0
        try
        {
            var files = Directory.GetFiles(subtitlePath);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.Length > 0)
                {
                    // Found at least one valid file
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking subtitles directory for episode {EpisodeId}", episodeId);
            return false;
        }

        // No valid files found
        return false;
    }
}

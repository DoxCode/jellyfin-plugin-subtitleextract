using System;
using System.Collections.Generic;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.SubtitleExtract.Helpers;

/// <summary>
/// Helper class for filtering subtitle streams by language.
/// </summary>
public static class LanguageFilter
{
    private static readonly HashSet<string> SpanishLanguageCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "spa", // Spanish (ISO 639-2)
        "es", // Spanish (ISO 639-1)
        "es-ES", // Spanish (Spain)
        "es-MX", // Spanish (Mexico)
        "es-AR", // Spanish (Argentina)
        "es-CO", // Spanish (Colombia)
        "es-CL", // Spanish (Chile)
        "es-VE", // Spanish (Venezuela)
        "es-PE", // Spanish (Peru)
        "es-UY", // Spanish (Uruguay)
        "es-EC", // Spanish (Ecuador)
        "es-GT", // Spanish (Guatemala)
        "es-CU", // Spanish (Cuba)
        "es-BO", // Spanish (Bolivia)
        "es-DO", // Spanish (Dominican Republic)
        "es-HN", // Spanish (Honduras)
        "es-PY", // Spanish (Paraguay)
        "es-SV", // Spanish (El Salvador)
        "es-NI", // Spanish (Nicaragua)
        "es-CR", // Spanish (Costa Rica)
        "es-PA", // Spanish (Panama)
        "es-PR", // Spanish (Puerto Rico)
        "es-US" // Spanish (United States)
    };

    private static readonly HashSet<string> EnglishLanguageCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "eng", // English (ISO 639-2)
        "en", // English (ISO 639-1)
        "en-US", // English (United States)
        "en-GB", // English (United Kingdom)
        "en-CA", // English (Canada)
        "en-AU", // English (Australia)
        "en-NZ", // English (New Zealand)
        "en-IE", // English (Ireland)
        "en-ZA", // English (South Africa)
        "en-IN" // English (India)
    };

    /// <summary>
    /// Checks if a subtitle stream should be extracted based on language filters.
    /// </summary>
    /// <param name="stream">The media stream to check.</param>
    /// <param name="extractSpanish">Whether to extract Spanish subtitles.</param>
    /// <param name="extractEnglish">Whether to extract English subtitles.</param>
    /// <returns>True if the subtitle should be extracted, false otherwise.</returns>
    public static bool ShouldExtractSubtitle(MediaStream stream, bool extractSpanish, bool extractEnglish)
    {
        // If no language filter is active, extract all subtitles
        if (!extractSpanish && !extractEnglish)
        {
            return true;
        }

        // If the stream has no language information, skip it when filters are active
        if (string.IsNullOrEmpty(stream.Language))
        {
            return false;
        }

        bool isSpanish = IsSpanish(stream.Language);
        bool isEnglish = IsEnglish(stream.Language);

        // Extract if the language matches any active filter
        return (extractSpanish && isSpanish) || (extractEnglish && isEnglish);
    }

    /// <summary>
    /// Checks if the given language code is Spanish.
    /// </summary>
    /// <param name="languageCode">The language code to check.</param>
    /// <returns>True if the language is Spanish, false otherwise.</returns>
    public static bool IsSpanish(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
        {
            return false;
        }

        return SpanishLanguageCodes.Contains(languageCode);
    }

    /// <summary>
    /// Checks if the given language code is English.
    /// </summary>
    /// <param name="languageCode">The language code to check.</param>
    /// <returns>True if the language is English, false otherwise.</returns>
    public static bool IsEnglish(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
        {
            return false;
        }

        return EnglishLanguageCodes.Contains(languageCode);
    }
}

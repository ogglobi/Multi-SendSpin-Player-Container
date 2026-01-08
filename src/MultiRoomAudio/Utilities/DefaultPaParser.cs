using System.Text.RegularExpressions;
using MultiRoomAudio.Models;

namespace MultiRoomAudio.Utilities;

/// <summary>
/// Parses and modifies /etc/pulse/default.pa for sink import functionality.
/// </summary>
public partial class DefaultPaParser
{
    private readonly ILogger<DefaultPaParser> _logger;
    private readonly string _defaultPaPath;

    /// <summary>
    /// Regex pattern to match load-module lines for combine or remap sinks.
    /// Captures: (combine|remap) and the arguments.
    /// </summary>
    [GeneratedRegex(@"^\s*load-module\s+module-(combine|remap)-sink\s+(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex LoadModulePattern();

    /// <summary>
    /// Regex pattern to extract key=value pairs from module arguments.
    /// Handles quoted values for properties like device.description="My Sink"
    /// </summary>
    [GeneratedRegex(@"(\w+)=(?:""([^""]*)""|([^\s]+))", RegexOptions.Compiled)]
    private static partial Regex KeyValuePattern();

    /// <summary>
    /// Marker comment added when we comment out a line.
    /// </summary>
    private const string CommentMarker = "# [MRA-IMPORTED] ";

    public DefaultPaParser(ILogger<DefaultPaParser> logger, string? defaultPaPath = null)
    {
        _logger = logger;
        _defaultPaPath = defaultPaPath ?? "/etc/pulse/default.pa";
    }

    /// <summary>
    /// Scan default.pa for combine-sink and remap-sink module load lines.
    /// </summary>
    /// <returns>List of detected sinks with their configuration.</returns>
    public List<DetectedSink> ScanForSinks()
    {
        var detected = new List<DetectedSink>();

        if (!File.Exists(_defaultPaPath))
        {
            _logger.LogDebug("default.pa not found at {Path}", _defaultPaPath);
            return detected;
        }

        try
        {
            var lines = File.ReadAllLines(_defaultPaPath);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineNumber = i + 1; // 1-based line numbers

                // Skip already commented lines
                if (line.TrimStart().StartsWith('#'))
                    continue;

                // Handle line continuations (\)
                var fullLine = line;
                while (fullLine.TrimEnd().EndsWith('\\') && i + 1 < lines.Length)
                {
                    fullLine = fullLine.TrimEnd().TrimEnd('\\') + " " + lines[++i].Trim();
                }

                var match = LoadModulePattern().Match(fullLine);
                if (!match.Success)
                    continue;

                var moduleType = match.Groups[1].Value.ToLowerInvariant();
                var arguments = match.Groups[2].Value;

                var sink = ParseModuleArguments(moduleType, arguments, lineNumber, fullLine);
                if (sink != null)
                {
                    detected.Add(sink);
                    _logger.LogDebug("Found {Type}-sink '{Name}' at line {Line}", moduleType, sink.SinkName, lineNumber);
                }
            }

            _logger.LogInformation("Scanned default.pa: found {Count} importable sinks", detected.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan default.pa at {Path}", _defaultPaPath);
        }

        return detected;
    }

    private DetectedSink? ParseModuleArguments(string moduleType, string arguments, int lineNumber, string rawLine)
    {
        var keyValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in KeyValuePattern().Matches(arguments))
        {
            var key = match.Groups[1].Value;
            // Use quoted value if present, otherwise use unquoted
            var value = !string.IsNullOrEmpty(match.Groups[2].Value)
                ? match.Groups[2].Value
                : match.Groups[3].Value;
            keyValues[key] = value;
        }

        // Extract sink name (required)
        if (!keyValues.TryGetValue("sink_name", out var sinkName) || string.IsNullOrWhiteSpace(sinkName))
        {
            _logger.LogDebug("Skipping line {Line}: no sink_name found", lineNumber);
            return null;
        }

        // Extract description from sink_properties
        string? description = null;
        if (keyValues.TryGetValue("sink_properties", out var properties))
        {
            var descMatch = Regex.Match(properties, @"device\.description=""?([^""]+)""?");
            if (descMatch.Success)
            {
                description = descMatch.Groups[1].Value;
            }
        }

        var type = moduleType == "combine" ? CustomSinkType.Combine : CustomSinkType.Remap;

        // Parse type-specific properties
        List<string>? slaves = null;
        string? masterSink = null;
        int? channels = null;
        string? channelMap = null;
        string? masterChannelMap = null;
        bool? remix = null;

        if (type == CustomSinkType.Combine)
        {
            if (keyValues.TryGetValue("slaves", out var slavesValue))
            {
                slaves = slavesValue.Split(',').Select(s => s.Trim()).ToList();
            }
        }
        else // Remap
        {
            keyValues.TryGetValue("master", out masterSink);

            if (keyValues.TryGetValue("channels", out var channelsValue) && int.TryParse(channelsValue, out var ch))
            {
                channels = ch;
            }

            keyValues.TryGetValue("channel_map", out channelMap);
            keyValues.TryGetValue("master_channel_map", out masterChannelMap);

            if (keyValues.TryGetValue("remix", out var remixValue))
            {
                remix = remixValue.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                        remixValue.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }

        return new DetectedSink(
            LineNumber: lineNumber,
            RawLine: rawLine,
            Type: type,
            SinkName: sinkName,
            Description: description,
            Slaves: slaves,
            MasterSink: masterSink,
            Channels: channels,
            ChannelMap: channelMap,
            MasterChannelMap: masterChannelMap,
            Remix: remix
        );
    }

    /// <summary>
    /// Comment out a specific line in default.pa by prepending our marker.
    /// </summary>
    /// <param name="lineNumber">1-based line number to comment out.</param>
    /// <returns>True if successfully commented out.</returns>
    public bool CommentOutLine(int lineNumber)
    {
        if (!File.Exists(_defaultPaPath))
        {
            _logger.LogWarning("Cannot comment out line: default.pa not found at {Path}", _defaultPaPath);
            return false;
        }

        try
        {
            var lines = File.ReadAllLines(_defaultPaPath).ToList();
            var index = lineNumber - 1; // Convert to 0-based

            if (index < 0 || index >= lines.Count)
            {
                _logger.LogWarning("Line number {Line} is out of range (file has {Total} lines)", lineNumber, lines.Count);
                return false;
            }

            // Check if already commented by us
            if (lines[index].StartsWith(CommentMarker))
            {
                _logger.LogDebug("Line {Line} is already commented out by us", lineNumber);
                return true;
            }

            // Add our comment marker
            lines[index] = CommentMarker + lines[index];

            File.WriteAllLines(_defaultPaPath, lines);
            _logger.LogInformation("Commented out line {Line} in default.pa", lineNumber);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogError("Permission denied: cannot modify default.pa at {Path}", _defaultPaPath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to comment out line {Line} in default.pa", lineNumber);
            return false;
        }
    }

    /// <summary>
    /// Restore a line that was previously commented out by us.
    /// </summary>
    /// <param name="lineNumber">1-based line number to restore.</param>
    /// <returns>True if successfully restored.</returns>
    public bool UncommentLine(int lineNumber)
    {
        if (!File.Exists(_defaultPaPath))
        {
            _logger.LogWarning("Cannot uncomment line: default.pa not found at {Path}", _defaultPaPath);
            return false;
        }

        try
        {
            var lines = File.ReadAllLines(_defaultPaPath).ToList();
            var index = lineNumber - 1;

            if (index < 0 || index >= lines.Count)
            {
                _logger.LogWarning("Line number {Line} is out of range", lineNumber);
                return false;
            }

            // Only remove our marker, not other comments
            if (!lines[index].StartsWith(CommentMarker))
            {
                _logger.LogDebug("Line {Line} was not commented out by us", lineNumber);
                return false;
            }

            lines[index] = lines[index][CommentMarker.Length..];

            File.WriteAllLines(_defaultPaPath, lines);
            _logger.LogInformation("Restored line {Line} in default.pa", lineNumber);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogError("Permission denied: cannot modify default.pa at {Path}", _defaultPaPath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to uncomment line {Line} in default.pa", lineNumber);
            return false;
        }
    }

    /// <summary>
    /// Check if the default.pa file exists and is readable.
    /// </summary>
    public bool IsAvailable()
    {
        return File.Exists(_defaultPaPath);
    }

    /// <summary>
    /// Check if we have write permission to default.pa.
    /// </summary>
    public bool IsWritable()
    {
        if (!File.Exists(_defaultPaPath))
            return false;

        try
        {
            using var stream = File.Open(_defaultPaPath, FileMode.Open, FileAccess.Write, FileShare.None);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Represents a sink detected in default.pa.
/// </summary>
public record DetectedSink(
    int LineNumber,
    string RawLine,
    CustomSinkType Type,
    string SinkName,
    string? Description,
    // Combine-specific
    List<string>? Slaves,
    // Remap-specific
    string? MasterSink,
    int? Channels,
    string? ChannelMap,
    string? MasterChannelMap,
    bool? Remix
);

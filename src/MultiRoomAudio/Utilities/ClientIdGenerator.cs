using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MultiRoomAudio.Utilities;

/// <summary>
/// Generates deterministic client IDs from player names using MD5 hashing.
/// Provides consistent identifiers across restarts for the Sendspin SDK.
/// </summary>
public static class ClientIdGenerator
{
    private const string Prefix = "sendspin";
    private const int NameMaxLength = 20;
    private const int HashSuffixLength = 8;

    /// <summary>
    /// Generate a unique client ID from player name.
    /// Creates a deterministic ID based on the player name, prefixed with 'sendspin-'.
    /// </summary>
    /// <param name="playerName">Player name to generate ID for.</param>
    /// <returns>Client ID in format: sendspin-{safe_name}-{hash}</returns>
    public static string Generate(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            throw new ArgumentException("Player name cannot be empty", nameof(playerName));

        // Create a short hash suffix for uniqueness
        var hashSuffix = ComputeMd5Prefix(playerName, HashSuffixLength);

        // Sanitize name for use in ID (lowercase, replace spaces with dashes)
        var safeName = SanitizeName(playerName);

        return $"{Prefix}-{safeName}-{hashSuffix}";
    }

    /// <summary>
    /// Generate a MAC address from player name.
    /// Uses MD5 hash to generate a locally-administered unicast MAC address.
    /// </summary>
    /// <param name="playerName">Player name to generate MAC for.</param>
    /// <returns>MAC address in format: XX:XX:XX:XX:XX:XX</returns>
    public static string GenerateMac(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            throw new ArgumentException("Player name cannot be empty", nameof(playerName));

        // Generate MD5 hash of the player name
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(playerName));

        // Extract first 6 bytes for MAC address
        var macBytes = new byte[6];
        Array.Copy(hashBytes, macBytes, 6);

        // Set locally-administered bit (bit 1 of first octet)
        // Clear multicast bit (bit 0 of first octet)
        macBytes[0] = (byte)((macBytes[0] | 0x02) & 0xFE);

        // Format as MAC address
        return string.Join(":", macBytes.Select(b => b.ToString("X2")));
    }

    /// <summary>
    /// Compute MD5 hash prefix of a string.
    /// </summary>
    private static string ComputeMd5Prefix(string input, int prefixLength)
    {
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        var fullHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return fullHash[..Math.Min(prefixLength, fullHash.Length)];
    }

    /// <summary>
    /// Sanitize a player name for use in an identifier.
    /// </summary>
    private static string SanitizeName(string name)
    {
        // Convert to lowercase
        var sanitized = name.ToLowerInvariant();

        // Replace spaces and underscores with dashes
        sanitized = Regex.Replace(sanitized, @"[\s_]+", "-");

        // Remove any non-alphanumeric characters except dashes
        sanitized = Regex.Replace(sanitized, @"[^a-z0-9\-]", "");

        // Remove consecutive dashes
        sanitized = Regex.Replace(sanitized, @"-+", "-");

        // Trim dashes from start/end
        sanitized = sanitized.Trim('-');

        // Limit length
        if (sanitized.Length > NameMaxLength)
            sanitized = sanitized[..NameMaxLength].TrimEnd('-');

        return sanitized;
    }
}

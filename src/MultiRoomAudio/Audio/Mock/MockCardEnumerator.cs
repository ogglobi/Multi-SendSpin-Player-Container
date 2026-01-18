using MultiRoomAudio.Models;

namespace MultiRoomAudio.Audio.Mock;

/// <summary>
/// Mock card enumerator for testing without real PulseAudio hardware.
/// Provides fake sound cards with typical USB DAC profiles.
/// </summary>
public static class MockCardEnumerator
{
    private static ILogger? _logger;
    private static readonly Dictionary<int, string> _activeProfiles = new();
    private static readonly Dictionary<int, bool> _muteStates = new();

    /// <summary>
    /// Profile sets for different card types - based on real PulseAudio profile names.
    /// </summary>
    private static readonly (string Name, string Description, int Sinks, int Priority, bool Available)[] IntelHdaProfiles = new[]
    {
        ("output:analog-stereo+input:analog-stereo", "Analog Stereo Duplex", 1, 6565, true),
        ("output:analog-stereo", "Analog Stereo Output", 1, 6500, true),
        ("output:hdmi-stereo", "Digital Stereo (HDMI)", 1, 5900, true),
        ("output:hdmi-stereo-extra1", "Digital Stereo (HDMI 2)", 1, 5700, true),
        ("off", "Off", 0, 0, true)
    };

    private static readonly (string Name, string Description, int Sinks, int Priority, bool Available)[] XonarProfiles = new[]
    {
        ("output:analog-surround-71+input:analog-stereo", "Analog Surround 7.1 Output + Analog Stereo Input", 1, 7171, true),
        ("output:analog-surround-71", "Analog Surround 7.1 Output", 1, 7100, true),
        ("output:analog-surround-51", "Analog Surround 5.1 Output", 1, 6100, true),
        ("output:analog-stereo+input:analog-stereo", "Analog Stereo Duplex", 1, 6065, true),
        ("output:analog-stereo", "Analog Stereo Output", 1, 6000, true),
        ("output:iec958-stereo+input:analog-stereo", "Digital Stereo (S/PDIF) Output + Analog Stereo Input", 1, 5565, true),
        ("output:iec958-stereo", "Digital Stereo (S/PDIF) Output", 1, 5500, true),
        ("off", "Off", 0, 0, true)
    };

    private static readonly (string Name, string Description, int Sinks, int Priority, bool Available)[] UsbDacProfiles = new[]
    {
        ("output:analog-stereo", "Analog Stereo Output", 1, 6500, true),
        ("output:iec958-stereo", "Digital Stereo (IEC958) Output", 1, 5500, true),
        ("off", "Off", 0, 0, true)
    };

    private static readonly (string Name, string Description, int Sinks, int Priority, bool Available)[] UsbMultichannelProfiles = new[]
    {
        ("output:analog-surround-71", "Analog Surround 7.1 Output", 1, 7100, true),
        ("output:analog-surround-51", "Analog Surround 5.1 Output", 1, 6100, true),
        ("output:analog-stereo", "Analog Stereo Output", 1, 6000, true),
        ("off", "Off", 0, 0, true)
    };

    private static readonly (string Name, string Description, int Sinks, int Priority, bool Available)[] BluetoothA2dpProfiles = new[]
    {
        ("a2dp-sink", "High Fidelity Playback (A2DP Sink)", 1, 40, true),
        ("headset-head-unit", "Headset Head Unit (HSP/HFP)", 1, 30, true),
        ("off", "Off", 0, 0, true)
    };

    private static readonly (string Name, string Description, int Sinks, int Priority, bool Available)[] HdmiProfiles = new[]
    {
        ("output:hdmi-stereo", "Digital Stereo (HDMI)", 1, 5900, true),
        ("output:hdmi-surround", "Digital Surround 5.1 (HDMI)", 1, 5800, true),
        ("output:hdmi-surround71", "Digital Surround 7.1 (HDMI)", 1, 5700, true),
        ("off", "Off", 0, 0, true)
    };

    /// <summary>
    /// Pre-configured mock cards simulating real hardware.
    /// Based on actual ALSA/PulseAudio card names and profiles.
    /// </summary>
    private static readonly List<MockCardConfig> MockCardConfigs = new()
    {
        // Intel HDA onboard audio (very common)
        new(0, "alsa_card.pci-0000_00_1f.3", "Built-in Audio", "module-alsa-card.c", IntelHdaProfiles),

        // ASUS Xonar 7.1 (popular enthusiast card)
        new(1, "alsa_card.pci-0000_05_04.0", "Xonar DX", "module-alsa-card.c", XonarProfiles),

        // USB DAC - Schiit Modi (popular audiophile DAC)
        new(2, "alsa_card.usb-Schiit_Audio_Schiit_Modi_3-00", "Schiit Modi 3", "module-alsa-card.c", UsbDacProfiles),

        // USB multichannel - Focusrite Scarlett (popular audio interface)
        new(3, "alsa_card.usb-Focusrite_Scarlett_2i2_USB-00", "Scarlett 2i2 USB", "module-alsa-card.c", UsbMultichannelProfiles),

        // Bluetooth speaker - JBL (common BT speaker)
        new(4, "bluez_card.00_1A_7D_DA_71_13", "JBL Flip 5", "module-bluez5-device.c", BluetoothA2dpProfiles),

        // Bluetooth headphones - Sony WH-1000XM4
        new(5, "bluez_card.38_18_4C_E9_85_B2", "WH-1000XM4", "module-bluez5-device.c", BluetoothA2dpProfiles),

        // HDMI output (NVIDIA GPU)
        new(6, "alsa_card.pci-0000_01_00.1", "HDA NVidia", "module-alsa-card.c", HdmiProfiles),
    };

    private record MockCardConfig(
        int Index,
        string Name,
        string Description,
        string Driver,
        (string Name, string Description, int Sinks, int Priority, bool Available)[] Profiles
    );

    /// <summary>
    /// Configures the logger for card enumeration diagnostics.
    /// </summary>
    public static void SetLogger(ILogger? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets all available mock sound cards with their profiles.
    /// </summary>
    public static IEnumerable<PulseAudioCard> GetCards()
    {
        _logger?.LogDebug("Returning {Count} mock cards", MockCardConfigs.Count);

        return MockCardConfigs.Select(config =>
        {
            var profiles = config.Profiles.Select(p => new CardProfile(
                Name: p.Name,
                Description: p.Description,
                Sinks: p.Sinks,
                Sources: 0,
                Priority: p.Priority,
                IsAvailable: p.Available
            )).ToList();

            var activeProfile = _activeProfiles.GetValueOrDefault(config.Index)
                ?? profiles.FirstOrDefault(p => p.Name != "off")?.Name
                ?? "output:analog-stereo";

            var isMuted = _muteStates.GetValueOrDefault(config.Index, false);
            var maxVolume = _maxVolumes.TryGetValue(config.Index, out var vol) ? vol : (int?)null;

            return new PulseAudioCard(
                Index: config.Index,
                Name: config.Name,
                Driver: config.Driver,
                Description: config.Description,
                Profiles: profiles,
                ActiveProfile: activeProfile,
                IsMuted: isMuted,
                BootMuted: null,
                BootMuteMatchesCurrent: false,
                MaxVolume: maxVolume
            );
        }).ToList();
    }

    /// <summary>
    /// Gets a specific card by name or index.
    /// </summary>
    public static PulseAudioCard? GetCard(string cardNameOrIndex)
    {
        if (int.TryParse(cardNameOrIndex, out var index))
        {
            return GetCards().FirstOrDefault(c => c.Index == index);
        }

        return GetCards()
            .FirstOrDefault(c =>
                c.Name.Equals(cardNameOrIndex, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains(cardNameOrIndex, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Sets the active profile for a mock card.
    /// </summary>
    public static bool SetCardProfile(string cardNameOrIndex, string profileName, out string? errorMessage)
    {
        var card = GetCard(cardNameOrIndex);
        if (card == null)
        {
            errorMessage = $"Card '{cardNameOrIndex}' not found.";
            return false;
        }

        var profile = card.Profiles.FirstOrDefault(p =>
            p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));

        if (profile == null)
        {
            var availableProfiles = string.Join(", ", card.Profiles.Select(p => p.Name));
            errorMessage = $"Invalid profile '{profileName}'. Available profiles: {availableProfiles}";
            return false;
        }

        _activeProfiles[card.Index] = profileName;
        _logger?.LogInformation("Mock card '{Card}' profile set to '{Profile}'", card.Name, profileName);

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Gets all sink names belonging to a specific card.
    /// </summary>
    public static List<string> GetSinksByCard(int cardIndex)
    {
        var config = MockCardConfigs.FirstOrDefault(c => c.Index == cardIndex);
        if (config == null)
            return new List<string>();

        // Return the corresponding mock sink name
        var sinkId = config.Name.Replace("alsa_card.", "mock_");
        return new List<string> { sinkId };
    }

    /// <summary>
    /// Gets mute state for all sinks.
    /// </summary>
    public static Dictionary<string, bool> GetSinksMuteStates()
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var config in MockCardConfigs)
        {
            var sinkId = config.Name.Replace("alsa_card.", "mock_");
            result[sinkId] = _muteStates.GetValueOrDefault(config.Index, false);
        }
        return result;
    }

    /// <summary>
    /// Sets mute state for a card.
    /// </summary>
    public static bool SetMute(int cardIndex, bool muted)
    {
        if (!MockCardConfigs.Any(c => c.Index == cardIndex))
            return false;

        _muteStates[cardIndex] = muted;
        _logger?.LogDebug("Mock card {Index} mute set to {Muted}", cardIndex, muted);
        return true;
    }

    /// <summary>
    /// Sets mute state for a sink by name.
    /// </summary>
    public static bool SetMuteBySink(string sinkName, bool muted)
    {
        // Find the card that owns this sink
        // Sink names from GetSinksByCard are like "mock_pci-0000_00_1f.3"
        // Card names are like "alsa_card.pci-0000_00_1f.3"
        foreach (var config in MockCardConfigs)
        {
            var mockSinkId = config.Name.Replace("alsa_card.", "mock_");
            var cardSuffix = config.Name.Replace("alsa_card.", "");

            if (mockSinkId.Equals(sinkName, StringComparison.OrdinalIgnoreCase) ||
                sinkName.Contains(cardSuffix, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogDebug("Mock: Setting mute for card {Index} (sink '{Sink}') to {Muted}",
                    config.Index, sinkName, muted);
                return SetMute(config.Index, muted);
            }
        }

        _logger?.LogWarning("Mock: Sink '{Sink}' not found for mute operation", sinkName);
        return false;
    }

    #region Max Volume Tracking

    private static readonly Dictionary<int, int> _maxVolumes = new();

    /// <summary>
    /// Gets the max volume for a card, if configured.
    /// </summary>
    public static int? GetMaxVolume(int cardIndex)
    {
        return _maxVolumes.TryGetValue(cardIndex, out var vol) ? vol : null;
    }

    /// <summary>
    /// Sets the max volume for a card.
    /// </summary>
    public static bool SetMaxVolume(int cardIndex, int? maxVolume)
    {
        if (!MockCardConfigs.Any(c => c.Index == cardIndex))
            return false;

        if (maxVolume.HasValue)
        {
            _maxVolumes[cardIndex] = Math.Clamp(maxVolume.Value, 0, 100);
            _logger?.LogDebug("Mock card {Index} max volume set to {MaxVolume}%", cardIndex, maxVolume.Value);
        }
        else
        {
            _maxVolumes.Remove(cardIndex);
            _logger?.LogDebug("Mock card {Index} max volume cleared", cardIndex);
        }
        return true;
    }

    /// <summary>
    /// Sets max volume for a sink by name.
    /// </summary>
    public static bool SetMaxVolumeBySink(string sinkName, int? maxVolume)
    {
        // Find the card that owns this sink
        // Sink names from GetSinksByCard are like "mock_pci-0000_00_1f.3"
        // Card names are like "alsa_card.pci-0000_00_1f.3"
        foreach (var config in MockCardConfigs)
        {
            var mockSinkId = config.Name.Replace("alsa_card.", "mock_");
            var cardSuffix = config.Name.Replace("alsa_card.", "");

            if (mockSinkId.Equals(sinkName, StringComparison.OrdinalIgnoreCase) ||
                sinkName.Contains(cardSuffix, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogDebug("Mock: Setting max volume for card {Index} (sink '{Sink}') to {MaxVolume}",
                    config.Index, sinkName, maxVolume?.ToString() ?? "null");
                return SetMaxVolume(config.Index, maxVolume);
            }
        }

        _logger?.LogWarning("Mock: Sink '{Sink}' not found for max volume operation", sinkName);
        return false;
    }

    #endregion

    #region Test Support

    /// <summary>
    /// Resets all mock state. Call this between tests to ensure isolation.
    /// </summary>
    public static void ResetState()
    {
        _activeProfiles.Clear();
        _muteStates.Clear();
        _maxVolumes.Clear();
        _logger?.LogDebug("Mock card state reset");
    }

    #endregion
}

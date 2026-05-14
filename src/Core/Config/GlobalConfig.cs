using System.Xml.Serialization;

namespace DynamicControls.Config;

/// <summary>
/// Plugin configuration deserialized from GlobalConfig.xml by ConfigLoader.
/// Controls the default controller template name and debug logging behaviour.
/// Consumed by DynamicControlsPlugin at startup to configure the resolver and logger.
/// </summary>
[XmlRoot("Config")]
public class GlobalConfig
{
    /// <summary>Name of the template to use when no per-platform template is matched. Null uses no template.</summary>
    public string? DefaultTemplate { get; set; }

    /// <summary>When true, verbose debug logging is enabled and the log file is cleared on startup.</summary>
    public bool Debug { get; set; }

    /// <summary>When true, MAME controls XML is used as a label source.</summary>
    public bool EnableMame { get; set; }

    /// <summary>When true, RetroArch cfg/remap files are used as a mapping source.</summary>
    public bool EnableRetroArch { get; set; }
}

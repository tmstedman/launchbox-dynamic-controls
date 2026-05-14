using System.Diagnostics.CodeAnalysis;
using System.Xml;
using System.Xml.Serialization;
using DynamicControls.Config;

namespace DynamicControls.Composition;

/// <summary>
/// Loads the plugin configuration from GlobalConfig.xml using XmlSerializer. Used by the root
/// composer to read settings before wiring the rest of the subsystems. Returns a default
/// <see cref="GlobalConfig"/> when no defaults file is present.
/// Merges Defaults\GlobalConfig.xml with User\GlobalConfig.xml: settings present in the user
/// file override the defaults; absent settings retain the shipped default value.
/// </summary>
internal static class ConfigLoader
{
    /// <summary>Production entry point — creates real I/O dependencies.</summary>
    [ExcludeFromCodeCoverage]
    public static GlobalConfig Load(LayeredFileSystem lfs)
    {
        var fs = new SystemFileSystem();
        return Load(lfs, new Logger(fs, lfs.DefaultsDir));
    }

    /// <summary>
    /// Reads GlobalConfig.xml from the layered paths and returns the merged configuration.
    /// Loads Defaults\GlobalConfig.xml as the base, then overlays any settings present in
    /// User\GlobalConfig.xml. Returns a default <see cref="GlobalConfig"/> when the defaults
    /// file is missing.
    /// </summary>
    public static GlobalConfig Load(LayeredFileSystem lfs, ILogger logger)
    {
        var serializer = new XmlSerializer(typeof(GlobalConfig));
        serializer.UnknownElement += (sender, e) =>
            logger.Error($"Unknown config element: {e.Element.Name}");

        string defaultsPath = Path.Combine(lfs.DefaultsDir, "GlobalConfig.xml");
        bool defaultsExists = lfs.FileExists(defaultsPath);
        logger.Info($"Defaults config path: {defaultsPath}, Exists: {defaultsExists}");

        GlobalConfig baseConfig = defaultsExists
            ? (GlobalConfig)serializer.Deserialize(lfs.OpenRead(defaultsPath))!
            : new GlobalConfig();

        string userPath = Path.Combine(lfs.UserDir, "GlobalConfig.xml");
        bool userExists = lfs.FileExists(userPath);
        logger.Info($"User config path: {userPath}, Exists: {userExists}");
        if (!userExists) return baseConfig;

        // Collect which elements the user file actually contains — absent elements must not
        // override the base (XmlSerializer fills absent bools with false, not the default value).
        var userDoc = new XmlDocument();
        using (Stream docStream = lfs.OpenRead(userPath))
            userDoc.Load(docStream);

        var present = userDoc.DocumentElement!.ChildNodes
            .OfType<XmlElement>()
            .Select(e => e.Name)
            .ToHashSet();

        GlobalConfig userConfig;
        using (Stream cfgStream = lfs.OpenRead(userPath))
            userConfig = (GlobalConfig)serializer.Deserialize(cfgStream)!;

        if (present.Contains(nameof(GlobalConfig.DefaultTemplate)))
            baseConfig.DefaultTemplate = userConfig.DefaultTemplate;
        if (present.Contains(nameof(GlobalConfig.Debug)))
            baseConfig.Debug = userConfig.Debug;
        if (present.Contains(nameof(GlobalConfig.EnableMame)))
            baseConfig.EnableMame = userConfig.EnableMame;
        if (present.Contains(nameof(GlobalConfig.EnableRetroArch)))
            baseConfig.EnableRetroArch = userConfig.EnableRetroArch;

        return baseConfig;
    }
}

using System.Xml;

namespace DynamicControls.Plugins.RetroArch;

public interface IRetroArchCoreLoader
{
    RetroArchCoreConfig? Load(string coreDisplayName);
}

/// <summary>
/// Loads <c>Emulators\RetroArch\{coreDisplayName}.xml</c> into a <see cref="RetroArchCoreConfig"/>.
/// The display name comes from the core's <c>.info</c> file (see <see cref="RetroArchCoreInfo"/>).
/// Returns null when no file exists — the caller treats that as "this core is not configured for
/// the dynamic-controls plugin" and contributes no mapping.
/// </summary>
public class RetroArchCoreLoader(ILogger logger, LayeredFileSystem lfs) : IRetroArchCoreLoader
{
    private readonly ILogger _logger = logger;
    private readonly LayeredFileSystem _lfs = lfs;

    public RetroArchCoreConfig? Load(string coreDisplayName)
    {
        string path = _lfs.Resolve("Emulators", "RetroArch", coreDisplayName + ".xml");
        _logger.Debug($"RetroArch core config path: {path}, Exists: {_lfs.FileExists(path)}");
        if (!_lfs.FileExists(path)) return null;

        using Stream stream = _lfs.OpenRead(path);
        var doc = new XmlDocument();
        doc.Load(stream);
        XmlElement root = doc.DocumentElement!;
        if (root.Name != "RetroArchCore")
        {
            _logger.Error($"Invalid RetroArch core file (expected <RetroArchCore> root): {path}");
            return null;
        }

        var config = new RetroArchCoreConfig();
        XmlElement? controllersNode = root["Controllers"];
        if (controllersNode == null)
        {
            _logger.Error($"RetroArch core file missing <Controllers>: {path}");
            return null;
        }

        foreach (XmlElement node in controllersNode.ChildNodes.OfType<XmlElement>())
        {
            if (node.Name != "Controller")
            {
                _logger.Error($"Unexpected <{node.Name}> in {path} under <Controllers>: expected <Controller>");
                continue;
            }
            RetroArchControllerConfig? controller = ParseController(node, path);
            if (controller != null) config.Controllers.Add(controller);
        }

        if (config.Controllers.Count == 0)
        {
            _logger.Error($"RetroArch core file has no controllers: {path}");
            return null;
        }

        _logger.Debug($"RetroArch core '{coreDisplayName}': {config.Controllers.Count} controllers");
        return config;
    }

    private RetroArchControllerConfig? ParseController(XmlElement node, string path)
    {
        string? name = node.Attributes["name"]?.Value;
        if (string.IsNullOrEmpty(name))
        {
            _logger.Error($"Skipping <Controller> in {path}: missing 'name' attribute");
            return null;
        }

        var controller = new RetroArchControllerConfig { Name = name };
        foreach (XmlElement child in node.ChildNodes.OfType<XmlElement>())
        {
            if (child.Name != "Retropad")
            {
                _logger.Error($"Unexpected <{child.Name}> in {path} under <Controller name='{name}'>: expected <Retropad>");
                continue;
            }
            string? idAttr = child.Attributes["id"]?.Value;
            if (idAttr != null && int.TryParse(idAttr, out int id))
                controller.RetropadIds.Add(id);
            else
                _logger.Error($"Invalid <Retropad> in {path}: id='{idAttr}'");
        }

        _logger.Debug($"RetroArch controller '{controller.Name}': retropadIds=[{string.Join(",", controller.RetropadIds)}]");
        return controller;
    }
}

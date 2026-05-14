using System.Diagnostics.CodeAnalysis;

namespace DynamicControls.Infrastructure;

public interface IApplicationData
{
    string Path { get; }
}

[ExcludeFromCodeCoverage]
public class SystemApplicationData : IApplicationData
{
    public string Path =>
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
}

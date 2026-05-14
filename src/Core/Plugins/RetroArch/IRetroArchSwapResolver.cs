namespace DynamicControls.Plugins.RetroArch;

public interface IRetroArchSwapResolver
{
    Dictionary<string, int> ResolveSwaps(RetroArchGameData data);
}

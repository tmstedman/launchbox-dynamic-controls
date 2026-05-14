using NSubstitute;

namespace DynamicControls.Core.Tests.Infrastructure;

internal static class TestFs
{
    internal static IFileSystem Create() => Substitute.For<IFileSystem>();
}

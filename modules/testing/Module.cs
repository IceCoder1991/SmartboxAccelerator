using Smartboxx.Platform.Contracts;

namespace Smartboxx.Modules.Testing;

/// <summary>Composition marker for the Testing capability.</summary>
public static class Module
{
    public static ModuleDescriptor Descriptor { get; } = new("Testing");
}

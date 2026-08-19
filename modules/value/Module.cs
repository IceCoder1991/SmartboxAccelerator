using Smartboxx.Platform.Contracts;

namespace Smartboxx.Modules.Value;

/// <summary>Composition marker for the Value capability.</summary>
public static class Module
{
    public static ModuleDescriptor Descriptor { get; } = new("Value");
}

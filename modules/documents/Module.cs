using Smartboxx.Platform.Contracts;

namespace Smartboxx.Modules.Documents;

/// <summary>Composition marker for the Documents capability.</summary>
public static class Module
{
    public static ModuleDescriptor Descriptor { get; } = new("Documents");
}

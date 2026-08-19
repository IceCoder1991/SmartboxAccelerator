using Smartboxx.Platform.Contracts;

namespace Smartboxx.Modules.ApiIntelligence;

/// <summary>Composition marker for the API Intelligence capability.</summary>
public static class Module
{
    public static ModuleDescriptor Descriptor { get; } = new("API Intelligence");
}

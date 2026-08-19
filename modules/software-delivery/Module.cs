using Smartboxx.Platform.Contracts;

namespace Smartboxx.Modules.SoftwareDelivery;

/// <summary>Composition marker for the Software Delivery capability.</summary>
public static class Module
{
    public static ModuleDescriptor Descriptor { get; } = new("Software Delivery");
}

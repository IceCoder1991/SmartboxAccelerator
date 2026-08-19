using Smartboxx.Platform.Contracts;

namespace Smartboxx.Modules.ContactCentre;

/// <summary>Composition marker for the Contact Centre capability.</summary>
public static class Module
{
    public static ModuleDescriptor Descriptor { get; } = new("Contact Centre");
}

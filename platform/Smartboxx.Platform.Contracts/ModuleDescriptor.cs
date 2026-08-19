namespace Smartboxx.Platform.Contracts;

/// <summary>Describes an in-process capability module for host composition.</summary>
public sealed record ModuleDescriptor(string Name)
{
    public string Id { get; } = Name switch
    {
        "Contact Centre" => "contact-centre",
        "Software Delivery" => "software-delivery",
        "API Intelligence" => "api-intelligence",
        _ => Name.ToLowerInvariant()
    };
}

using Microsoft.AspNetCore.Identity;

namespace Lamort.Shared.Auth;

public sealed class LamortUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
}

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Gatehouse.Accounts.Tests;

/// <summary>An Identity user with two app-specific fields, like a real app has.</summary>
public sealed class TestUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }

    public bool IsDisabled { get; set; }
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options)
    : IdentityDbContext<TestUser, IdentityRole<Guid>, Guid>(options);

using backend.Common.Auth;
using backend.Domain.Entities;
using backend.Features.AdminUserFeature.ChangeAdminUserPassword;
using backend.Features.AdminUserFeature.ChangeAdminUserStatus;
using backend.Features.AdminUserFeature.CreateAdminUser;
using backend.Features.AdminUserFeature.DeleteAdminUser;
using backend.Features.AdminUserFeature.GetByIdAdminUser;
using backend.Features.AdminUserFeature.ListAdminUserPaginated;
using backend.Features.AdminUserFeature.ResetAdminUserPassword;
using backend.Features.AdminUserFeature.UpdateAdminUser;
using backend.Infrastructure.Persistence;
using backend.Tests.Infrastructure;

namespace backend.Tests.Features.AdminUserFeature;

[Collection("Database")]
public class AdminUserManagementHandlerTests(DatabaseFixture fixture)
{
    private static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@test.com";

    private static async Task<AdminUser> SeedAsync(
        AppDbContext context,
        string email,
        string role = AdminUserRoles.Staff,
        bool isActive = true,
        string password = "SeedPass123!")
    {
        var user = new AdminUser
        {
            Name = $"Seed {role}",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role,
            IsActive = isActive,
            PasswordChangedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        };
        context.AdminUsers.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    // ── Create ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAdminUser_WithoutPassword_GeneratesTemporaryAndForcesChange()
    {
        await using var context = fixture.CreateContext();
        var email = UniqueEmail("create-generated");

        var handler = new CreateAdminUserHandler(context);
        var result = await handler.Handle(
            new CreateAdminUserCommand("New Admin", email, AdminUserRoles.Manager),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.MustChangePassword);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.TemporaryPassword));
        Assert.Null(AdminPasswordPolicy.Validate(result.Value.TemporaryPassword));

        var stored = await context.AdminUsers.FindAsync(result.Value.Id);
        Assert.NotNull(stored);
        Assert.True(BCrypt.Net.BCrypt.Verify(result.Value.TemporaryPassword, stored!.PasswordHash));
    }

    [Fact]
    public async Task CreateAdminUser_WithSuppliedPassword_DoesNotEchoIt()
    {
        await using var context = fixture.CreateContext();

        var handler = new CreateAdminUserHandler(context);
        var result = await handler.Handle(
            new CreateAdminUserCommand(
                "Chosen Password",
                UniqueEmail("create-supplied"),
                AdminUserRoles.Staff,
                "ChosenPass123"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.TemporaryPassword);
        Assert.True(result.Value.MustChangePassword);
    }

    [Fact]
    public async Task CreateAdminUser_DuplicateEmail_ReturnsAlreadyExists()
    {
        await using var context = fixture.CreateContext();
        var email = UniqueEmail("create-duplicate");
        await SeedAsync(context, email);

        var handler = new CreateAdminUserHandler(context);
        var result = await handler.Handle(
            new CreateAdminUserCommand("Duplicate", email, AdminUserRoles.Staff),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("already exists", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAdminUser_UnknownRole_ReturnsFailure()
    {
        await using var context = fixture.CreateContext();

        var handler = new CreateAdminUserHandler(context);
        var result = await handler.Handle(
            new CreateAdminUserCommand("Bad Role", UniqueEmail("create-role"), "owner"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Role must be one of", result.Error);
    }

    [Fact]
    public async Task CreateAdminUser_WeakPassword_ReturnsPolicyFailure()
    {
        await using var context = fixture.CreateContext();

        var handler = new CreateAdminUserHandler(context);
        var result = await handler.Handle(
            new CreateAdminUserCommand("Weak", UniqueEmail("create-weak"), AdminUserRoles.Staff, "short1A"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("at least", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Read ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAdminUser_ExistingId_ReturnsUser()
    {
        await using var context = fixture.CreateContext();
        var user = await SeedAsync(context, UniqueEmail("get-by-id"));

        var handler = new GetByIdAdminUserHandler(context);
        var result = await handler.Handle(new GetByIdAdminUserQuery(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Email, result.Value!.Email);
    }

    [Fact]
    public async Task GetByIdAdminUser_MissingId_ReturnsNotFound()
    {
        await using var context = fixture.CreateContext();

        var handler = new GetByIdAdminUserHandler(context);
        var result = await handler.Handle(new GetByIdAdminUserQuery(999_999), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListAdminUsers_SearchesByEmail()
    {
        await using var context = fixture.CreateContext();
        var email = UniqueEmail("list-search");
        await SeedAsync(context, email);

        var handler = new ListAdminUserPaginatedHandler(context);
        var result = await handler.Handle(
            new ListAdminUserPaginatedQuery(Search: email),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Total);
        Assert.Equal(email, result.Value.Items[0].Email);
    }

    // ── Update ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAdminUser_ChangesNameEmailAndRole()
    {
        await using var context = fixture.CreateContext();
        var actor = await SeedAsync(context, UniqueEmail("update-actor"), AdminUserRoles.SuperAdmin);
        var target = await SeedAsync(context, UniqueEmail("update-target"));
        var newEmail = UniqueEmail("update-renamed");

        var handler = new UpdateAdminUserHandler(context);
        var result = await handler.Handle(
            new UpdateAdminUserCommand(target.Id, "Renamed", newEmail, AdminUserRoles.Manager, actor.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", result.Value!.Name);
        Assert.Equal(newEmail, result.Value.Email);
        Assert.Equal(AdminUserRoles.Manager, result.Value.Role);
    }

    [Fact]
    public async Task UpdateAdminUser_DemotingSelf_IsRejected()
    {
        await using var context = fixture.CreateContext();
        var actor = await SeedAsync(context, UniqueEmail("update-self"), AdminUserRoles.SuperAdmin);

        var handler = new UpdateAdminUserHandler(context);
        var result = await handler.Handle(
            new UpdateAdminUserCommand(actor.Id, actor.Name, actor.Email, AdminUserRoles.Manager, actor.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("your own role", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Status ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangeAdminUserStatus_Deactivates()
    {
        await using var context = fixture.CreateContext();
        var actor = await SeedAsync(context, UniqueEmail("status-actor"), AdminUserRoles.SuperAdmin);
        var target = await SeedAsync(context, UniqueEmail("status-target"));

        var handler = new ChangeAdminUserStatusHandler(context);
        var result = await handler.Handle(
            new ChangeAdminUserStatusCommand(target.Id, false, actor.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsActive);
    }

    [Fact]
    public async Task ChangeAdminUserStatus_DeactivatingSelf_IsRejected()
    {
        await using var context = fixture.CreateContext();
        var actor = await SeedAsync(context, UniqueEmail("status-self"), AdminUserRoles.SuperAdmin);

        var handler = new ChangeAdminUserStatusHandler(context);
        var result = await handler.Handle(
            new ChangeAdminUserStatusCommand(actor.Id, false, actor.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("your own account", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Reset ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetAdminUserPassword_ReplacesHashAndMovesStamp()
    {
        await using var context = fixture.CreateContext();
        var target = await SeedAsync(context, UniqueEmail("reset-target"));
        var originalHash = target.PasswordHash;
        var originalStamp = target.PasswordChangedAt;

        var handler = new ResetAdminUserPasswordHandler(context);
        var result = await handler.Handle(
            new ResetAdminUserPasswordCommand(target.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.MustChangePassword);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.TemporaryPassword));

        var reloaded = await context.AdminUsers.FindAsync(target.Id);
        Assert.NotEqual(originalHash, reloaded!.PasswordHash);
        Assert.NotEqual(originalStamp, reloaded.PasswordChangedAt);
        Assert.True(BCrypt.Net.BCrypt.Verify(result.Value.TemporaryPassword, reloaded.PasswordHash));
    }

    [Fact]
    public async Task ResetAdminUserPassword_MissingUser_ReturnsNotFound()
    {
        await using var context = fixture.CreateContext();

        var handler = new ResetAdminUserPasswordHandler(context);
        var result = await handler.Handle(
            new ResetAdminUserPasswordCommand(999_999),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Self-service change ─────────────────────────────────────────────────

    [Fact]
    public async Task ChangeAdminUserPassword_ValidCurrentPassword_ClearsMustChange()
    {
        await using var context = fixture.CreateContext();
        var user = await SeedAsync(context, UniqueEmail("change-ok"), password: "OldPass123!");
        user.MustChangePassword = true;
        await context.SaveChangesAsync();

        var handler = new ChangeAdminUserPasswordHandler(context);
        var result = await handler.Handle(
            new ChangeAdminUserPasswordCommand(user.Id, "OldPass123!", "BrandNewPass123"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var reloaded = await context.AdminUsers.FindAsync(user.Id);
        Assert.False(reloaded!.MustChangePassword);
        Assert.True(BCrypt.Net.BCrypt.Verify("BrandNewPass123", reloaded.PasswordHash));
    }

    [Fact]
    public async Task ChangeAdminUserPassword_WrongCurrentPassword_IsRejected()
    {
        await using var context = fixture.CreateContext();
        var user = await SeedAsync(context, UniqueEmail("change-wrong"), password: "OldPass123!");

        var handler = new ChangeAdminUserPasswordHandler(context);
        var result = await handler.Handle(
            new ChangeAdminUserPasswordCommand(user.Id, "NotThePassword1", "BrandNewPass123"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Current password", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangeAdminUserPassword_ReusingCurrentPassword_IsRejected()
    {
        await using var context = fixture.CreateContext();
        var user = await SeedAsync(context, UniqueEmail("change-reuse"), password: "SamePass123!");

        var handler = new ChangeAdminUserPasswordHandler(context);
        var result = await handler.Handle(
            new ChangeAdminUserPasswordCommand(user.Id, "SamePass123!", "SamePass123!"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("different", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Delete ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAdminUser_UnusedAccount_IsRemoved()
    {
        await using var context = fixture.CreateContext();
        var actor = await SeedAsync(context, UniqueEmail("delete-actor"), AdminUserRoles.SuperAdmin);
        var target = await SeedAsync(context, UniqueEmail("delete-target"));

        var handler = new DeleteAdminUserHandler(context);
        var result = await handler.Handle(
            new DeleteAdminUserCommand(target.Id, actor.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(await context.AdminUsers.FindAsync(target.Id));
    }

    [Fact]
    public async Task DeleteAdminUser_LinkedToActivity_IsRejected()
    {
        await using var context = fixture.CreateContext();
        var actor = await SeedAsync(context, UniqueEmail("delete-linked-actor"), AdminUserRoles.SuperAdmin);
        var target = await SeedAsync(context, UniqueEmail("delete-linked-target"));

        context.ActivityLogs.Add(new ActivityLog
        {
            AdminUserId = target.Id,
            Action = "test.linked",
            CreatedAt = DateTime.Now,
        });
        await context.SaveChangesAsync();

        var handler = new DeleteAdminUserHandler(context);
        var result = await handler.Handle(
            new DeleteAdminUserCommand(target.Id, actor.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("deactivate", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAdminUser_Self_IsRejected()
    {
        await using var context = fixture.CreateContext();
        var actor = await SeedAsync(context, UniqueEmail("delete-self"), AdminUserRoles.SuperAdmin);

        var handler = new DeleteAdminUserHandler(context);
        var result = await handler.Handle(
            new DeleteAdminUserCommand(actor.Id, actor.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("your own account", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}

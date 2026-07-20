using FinanceManager.Application.Shared.Maintenance;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Shared.Maintenance.Entities;
using FinanceManager.Domain.Shared.Maintenance.Repositories;
using Microsoft.Extensions.Options;

namespace FinanceManager.Tests.Unit.Application.Services.Maintenance;

[Trait("Category", "Unit")]
public class MaintenanceKeyServiceTests
{
    private readonly InMemoryMaintenanceKeyRepository _repository = new();

    private MaintenanceKeyService CreateSut(string? configuredKey = null) => new(
        _repository,
        Options.Create(new MaintenanceOptions { ApiKey = configuredKey ?? string.Empty }));

    [Fact]
    public async Task Generate_StoresHashOnly_AndGeneratedKeyValidates()
    {
        var sut = CreateSut();

        var plaintext = await sut.GenerateAsync(TestContext.Current.CancellationToken);

        Assert.StartsWith("fmk_", plaintext);
        Assert.NotNull(_repository.Stored);
        Assert.DoesNotContain(plaintext, _repository.Stored.KeyHash);
        Assert.True(await sut.ValidateAsync(plaintext, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Generate_RollsExistingKey_OldKeyStopsValidating()
    {
        var sut = CreateSut();
        var oldKey = await sut.GenerateAsync(TestContext.Current.CancellationToken);

        var newKey = await sut.GenerateAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(oldKey, newKey);
        Assert.False(await sut.ValidateAsync(oldKey, TestContext.Current.CancellationToken));
        Assert.True(await sut.ValidateAsync(newKey, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Revoke_RemovesKey_AndEndpointBecomesUnconfigured()
    {
        var sut = CreateSut();
        var key = await sut.GenerateAsync(TestContext.Current.CancellationToken);

        Assert.True(await sut.RevokeAsync(TestContext.Current.CancellationToken));
        Assert.False(await sut.ValidateAsync(key, TestContext.Current.CancellationToken));
        Assert.False(await sut.IsConfiguredAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Revoke_WithoutKey_ReturnsFalse()
    {
        Assert.False(await CreateSut().RevokeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Validate_AcceptsConfiguredFallbackKey_EvenAfterDbKeyRevoked()
    {
        var sut = CreateSut(configuredKey: "config-break-glass-key");
        await sut.GenerateAsync(TestContext.Current.CancellationToken);
        await sut.RevokeAsync(TestContext.Current.CancellationToken);

        Assert.True(await sut.IsConfiguredAsync(TestContext.Current.CancellationToken));
        Assert.True(await sut.ValidateAsync("config-break-glass-key", TestContext.Current.CancellationToken));
        Assert.False(await sut.ValidateAsync("wrong", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetStatus_ReflectsActiveKey()
    {
        var sut = CreateSut();
        Assert.Equal(new MaintenanceKeyStatus(false, null), await sut.GetStatusAsync(TestContext.Current.CancellationToken));

        await sut.GenerateAsync(TestContext.Current.CancellationToken);

        var status = await sut.GetStatusAsync(TestContext.Current.CancellationToken);
        Assert.True(status.HasKey);
        Assert.NotNull(status.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Validate_RejectsEmptyKey(string? providedKey)
    {
        var sut = CreateSut(configuredKey: "config-key");

        Assert.False(await sut.ValidateAsync(providedKey!, TestContext.Current.CancellationToken));
    }

    private sealed class InMemoryMaintenanceKeyRepository : IMaintenanceKeyRepository
    {
        public MaintenanceApiKey? Stored { get; private set; }

        public Task<MaintenanceApiKey?> Get(CancellationToken cancellationToken = default) => Task.FromResult(Stored);

        public Task Save(MaintenanceApiKey key, CancellationToken cancellationToken = default)
        {
            Stored = key;
            return Task.CompletedTask;
        }

        public Task<bool> Delete(CancellationToken cancellationToken = default)
        {
            var existed = Stored is not null;
            Stored = null;
            return Task.FromResult(existed);
        }
    }
}
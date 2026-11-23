using FinanceManager.Api.Features.FinancialAccounts.Currencies.Hubs;
using FinanceManager.Api.Features.FinancialAccounts.Currencies.Services;
using Microsoft.AspNetCore.SignalR;
using Moq;
using System.Security.Claims;
using Xunit;

namespace FinanceManager.Tests.Unit.Api.Features.FinancialAccounts.Currencies.Hubs;

public class CurrencyImportHubTests
{
    private const string _connectionId = "connection-1";
    private readonly CurrencyImportJobStore _store = new();
    private readonly Mock<IGroupManager> _groups = new();

    private CurrencyImportHub CreateHub(string? userId)
    {
        var identity = new ClaimsIdentity(userId is null ? [] : [new Claim(ClaimTypes.NameIdentifier, userId)], "test");
        var context = new Mock<HubCallerContext>();
        context.SetupGet(x => x.User).Returns(new ClaimsPrincipal(identity));
        context.SetupGet(x => x.ConnectionId).Returns(_connectionId);
        return new CurrencyImportHub(_store) { Context = context.Object, Groups = _groups.Object };
    }

    [Fact]
    public async Task JoinJob_AddsTheOwnerOfAnOwnerJob_IncludingAGuestWithANegativeId()
    {
        var job = _store.CreateOwnerJob(userId: -7, currencyId: 0, totalEntries: 1);

        await CreateHub("-7").JoinJob(job.ToString());

        _groups.Verify(x => x.AddToGroupAsync(_connectionId, CurrencyImportHub.GetJobGroupName(job), default), Times.Once);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("not-a-number")]
    [InlineData(null)]
    public async Task JoinJob_RejectsAZeroForeignOrMalformedPrincipal(string? principalId)
    {
        var job = _store.CreateOwnerJob(userId: 1, currencyId: 0, totalEntries: 1);

        await Assert.ThrowsAsync<HubException>(() => CreateHub(principalId).JoinJob(job.ToString()));

        _groups.Verify(x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
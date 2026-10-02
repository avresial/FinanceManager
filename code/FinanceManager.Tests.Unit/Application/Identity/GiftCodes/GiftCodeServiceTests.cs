using FinanceManager.Application.Identity.GiftCodes;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.GiftCodes;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Identity.GiftCodes;

public sealed class GiftCodeServiceTests
{
    private readonly Mock<IGiftCodeRepository> _repository = new();

    [Theory]
    [InlineData(PricingLevel.Basic)]
    [InlineData(PricingLevel.Premium)]
    public async Task Generate_ReturnsCodeOnceAndPersistsOnlyHashAndSuffix(PricingLevel level)
    {
        var ct = TestContext.Current.CancellationToken;
        GiftCode? stored = null;
        _repository.Setup(repository => repository.Add(It.IsAny<GiftCode>(), It.IsAny<CancellationToken>()))
            .Callback<GiftCode, CancellationToken>((code, _) => stored = code)
            .ReturnsAsync((GiftCode code, CancellationToken _) => ToDto(code));

        var result = await new GiftCodeService(_repository.Object).Generate(new(level, "  promo  "), 17, ct);

        Assert.Matches("^[A-F0-9]{4}(-[A-F0-9]{4}){7}$", result.Code);
        Assert.Equal(level, result.Details.PricingLevel);
        Assert.Equal("promo", result.Details.Note);
        Assert.NotNull(stored);
        Assert.Equal(result.Code[^4..], stored.CodeSuffix);
        Assert.Equal(64, stored.CodeHash.Length);
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(result.Code.Replace("-", "", StringComparison.Ordinal))));
        Assert.Equal(expectedHash, stored.CodeHash);
        _repository.Verify(repository => repository.Add(It.IsAny<GiftCode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(PricingLevel.Free)]
    [InlineData((PricingLevel)99)]
    public async Task Generate_RejectsPricingLevelsThatCannotBeGranted(PricingLevel level)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new GiftCodeService(_repository.Object)
            .Generate(new(level), 17, TestContext.Current.CancellationToken));

        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Redeem_NormalizesCodeBeforeHashing()
    {
        const string normalized = "0123456789ABCDEF0123456789ABCDEF";
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(normalized)));
        _repository.Setup(repository => repository.Redeem(expectedHash, 24, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GiftCodeRedemptionResult.Redeemed);

        var result = await new GiftCodeService(_repository.Object)
            .Redeem(" 0123-4567-89ab-cdef-0123-4567-89ab-cdef ", 24, TestContext.Current.CancellationToken);

        Assert.Equal(GiftCodeRedemptionResult.Redeemed, result);
        _repository.Verify(repository => repository.Redeem(expectedHash, 24, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-code")]
    [InlineData("00000000000000000000000000000000000000000000000000000000000000000")]
    public async Task Redeem_RejectsMalformedCodeWithoutRepositoryCall(string? code)
    {
        var result = await new GiftCodeService(_repository.Object).Redeem(code, 24, TestContext.Current.CancellationToken);

        Assert.Equal(GiftCodeRedemptionResult.Unavailable, result);
        _repository.VerifyNoOtherCalls();
    }

    private static GiftCodeDto ToDto(GiftCode code) => new(
        1, code.CodeSuffix, code.PricingLevel, code.Note, code.State, code.CreatedAtUtc,
        code.CreatedByUserId, code.RedeemedAtUtc, code.RedeemedByUserId, code.RevokedAtUtc, code.RevokedByUserId);
}
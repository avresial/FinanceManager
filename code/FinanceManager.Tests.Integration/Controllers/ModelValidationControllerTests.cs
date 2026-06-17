using FinanceManager.Api.Controllers.Admin;
using FinanceManager.Application.Commands.Bonds;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Bond.Commands;
using FinanceManager.Domain.FinancialAccounts.Bond.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Commands;
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Commands;
using FinanceManager.Domain.FinancialAccounts.Stock.Dtos;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.Identity.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class ModelValidationControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    public static TheoryData<string, HttpMethod, object, UserRole?> InvalidPayloads => new()
    {
        { "api/Login", HttpMethod.Post, new LoginRequestModel("user@example.com", ""), null },
        { "api/Login", HttpMethod.Post, new LoginRequestModel("not-an-email", "password"), null },
        { "api/User/Add", HttpMethod.Post, new AddUser("not-an-email", "password", PricingLevel.Basic), null },
        { "api/User/UpdatePassword", HttpMethod.Put, new UpdatePassword(0, "password"), UserRole.Admin },
        { "api/User/UpdatePricingPlan", HttpMethod.Put, new UpdatePricingPlan(0, PricingLevel.Basic), UserRole.Admin },
        { "api/PasswordReset/forgot-password", HttpMethod.Post, new ForgotPasswordRequest("not-an-email"), null },
        { "api/PasswordReset/reset-password", HttpMethod.Post, new ResetPasswordRequest("token", "short"), null },
        { "api/StockAccount/Add", HttpMethod.Post, new AddAccount(""), UserRole.User },
        { "api/StockAccount/Update", HttpMethod.Put, new UpdateAccount(0, "", AccountLabel.Stock), UserRole.User },
        { "api/CurrencyEntry", HttpMethod.Post, new AddCurrencyAccountEntry(0, 0, new DateTime(1899, 12, 31), 0, -10, "", null), UserRole.User },
        { "api/CurrencyEntry", HttpMethod.Put, new UpdateCurrencyAccountEntry(1, 0, DateTime.UtcNow.AddYears(2), 0, -10, "", null, [new UpdateFinancialLabel(0, "")]), UserRole.User },
        { "api/BondEntry", HttpMethod.Post, new AddBondAccountEntry(0, 0, new DateTime(1899, 12, 31), 0, -10, 0), UserRole.User },
        { "api/BondEntry", HttpMethod.Put, new UpdateBondAccountEntry(0, 0, DateTime.UtcNow.AddYears(2), 0, -10, 0), UserRole.User },
        { "api/StockEntry/Add", HttpMethod.Post, new AddStockAccountEntry(null!), UserRole.User },
        { "api/StockEntry/Update", HttpMethod.Put, new UpdateStockAccountEntry(0, 0, DateTime.UtcNow.AddYears(2), 0, -10, new string('T', 10_000), InvestmentType.Stock, []), UserRole.User },
        { "api/FinancialLabel/add", HttpMethod.Post, new AddFinancialLabel(""), null },
        { "api/FinancialLabel/add-classification", HttpMethod.Post, new AddFinancialLabelClassification(0, "", ""), null },
        { "api/StockPrice/add-stock-details", HttpMethod.Post, new AddStockRequest(new string('T', 10_000), "Name", "ETF", "US", "USD"), UserRole.Admin },
        { "api/StockPrice/update-stock-details", HttpMethod.Put, new UpdateStockRequest("", "Name", "ETF", "US", "USD"), UserRole.Admin },
        { "api/BondDetails", HttpMethod.Put, new UpdateBondDetails(0, "", "", -1), UserRole.Admin },
        { "api/admin/ai-providers", HttpMethod.Post, new AddProviderRequest(""), UserRole.Admin },
        { "api/admin/ai-providers/OpenRouter/models", HttpMethod.Post, new AddModelRequest(""), UserRole.Admin },
        { "api/admin/ai-providers/OpenRouter", HttpMethod.Put, new UpdateProviderRequest("not-a-url", null, 0, true), UserRole.Admin },
        { "api/admin/ai-providers/OpenRouter/models/1", HttpMethod.Put, new UpdateModelRequest("", true), UserRole.Admin },
        { "api/admin/ai-providers/fallback", HttpMethod.Put, new UpdateFallbackRequest([]), UserRole.Admin },
        { "api/admin/service-keys/AlphaVantage", HttpMethod.Put, new UpdateServiceKeyRequest("not-a-url", null, true), UserRole.Admin },
        { "api/StockAccountImport/ImportStockEntries", HttpMethod.Post, new StockDataImportDto(0, [new StockEntryImportRecordDto(DateTime.UtcNow, 1, "")]), UserRole.User },
        { "api/CurrencyAccountImport/ImportCurrencyEntries", HttpMethod.Post, new CurrencyDataImportDto(0, [new CurrencyEntryImportRecordDto(DateTime.UtcNow.AddYears(2), 1)]), UserRole.User },
        { "api/BondAccountImport/ImportBondEntries", HttpMethod.Post, new BondDataImportDto(0, [new BondEntryImportRecordDto(DateTime.UtcNow, 1, 0)]), UserRole.User },
        { "api/CurrencyAccountImport/ResolveAsyncImportConflicts", HttpMethod.Post, new CurrencyImportResolveConflictsRequestDto { JobId = Guid.NewGuid(), Decisions = [new CurrencyImportConflictResolutionDecisionDto("", true, true)] }, UserRole.User },
        { "api/CsvHeaderMapping/SaveMappings", HttpMethod.Post, new SaveMappingRequestDto([new HeaderMappingRequestItemDto("", "")]), UserRole.User },
    };

    [Theory]
    [MemberData(nameof(InvalidPayloads))]
    public async Task InvalidRequestPayload_ReturnsValidationProblemDetails(
        string url,
        HttpMethod method,
        object payload,
        UserRole? role)
    {
        if (role is not null)
            Authorize("validator@example.com", 1, role.Value);

        using var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(payload),
        };

        using var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.NotEmpty(problem.Errors);
    }
}
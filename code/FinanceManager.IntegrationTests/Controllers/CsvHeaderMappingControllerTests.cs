using FinanceManager.Application.Services;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class CsvHeaderMappingControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private readonly Mock<ICsvHeaderMappingService> _mappingServiceMock = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        _mappingServiceMock
            .Setup(x => x.GetSuggestedMappingsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([new HeaderMappingResultDto("Posting Date", "PostingDate")]);
        services.AddSingleton(_mappingServiceMock.Object);
    }

    [Fact]
    public async Task SuggestMappings_WithHeaders_ReturnsSuggestions()
    {
        Authorize("user", 1, UserRole.User);

        var response = await Client.PostAsJsonAsync("api/CsvHeaderMapping/SuggestMappings", new[] { "Posting Date" }, TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("PostingDate", content, StringComparison.OrdinalIgnoreCase);
    }
}
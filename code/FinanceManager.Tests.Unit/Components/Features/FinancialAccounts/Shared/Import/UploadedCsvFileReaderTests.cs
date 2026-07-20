using FinanceManager.Components.Components.Features.FinancialAccounts.Shared.Import;
using Microsoft.AspNetCore.Components.Forms;
using Moq;
using System.Text;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Shared.Import;

[Trait("Category", "Unit")]
public class UploadedCsvFileReaderTests
{
    [Fact]
    public async Task ReadAsync_DecodesWindows1250Content()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = Encoding.GetEncoding(1250).GetBytes("Data księgowania,Nazwa odbiorcy\n2026-07-20,Mikołaj");
        var file = new Mock<IBrowserFile>();
        file.SetupGet(x => x.Name).Returns("transactions.csv");
        file.SetupGet(x => x.Size).Returns(bytes.Length);
        file.Setup(x => x.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(() => new MemoryStream(bytes));

        var result = await UploadedCsvFileReader.ReadAsync(file.Object);

        Assert.True(result.Success);
        Assert.Equal("Data księgowania,Nazwa odbiorcy\n2026-07-20,Mikołaj", result.Content);
    }
}
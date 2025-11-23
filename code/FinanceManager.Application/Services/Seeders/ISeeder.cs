namespace FinanceManager.Application.Services.Seeders;

public interface ISeeder
{
    Task Seed(CancellationToken cancellationToken = default);
}

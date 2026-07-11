namespace FinanceManager.Application.Shared.Seeders;

public interface ISeeder
{
    Task Seed(CancellationToken cancellationToken = default);
}
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Infrastructure.Contexts.Configurations;

public class StockPriceDtoConfiguration : IEntityTypeConfiguration<StockPriceDto>
{
    public void Configure(EntityTypeBuilder<StockPriceDto> builder)
    {
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();
    }
}
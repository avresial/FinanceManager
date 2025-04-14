using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Providers;
public class PricingProvider
{
    public int GetMaxAllowedEntries(PricingLevel pricingLevel) => pricingLevel switch
    {
        PricingLevel.Free => 1000,
        PricingLevel.Basic => 10000,
        PricingLevel.Premium => 100000,
        _ => 1000,
    };

    public int GetMaxAccountCount(PricingLevel pricingLevel) => pricingLevel switch
    {
        PricingLevel.Free => 10,
        PricingLevel.Basic => 30,
        PricingLevel.Premium => 100,
        _ => 1000,
    };


}

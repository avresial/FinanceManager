using FinanceManager.Domain.Entities.User;

namespace FinanceManager.Components.Services;
public class AdministrationUsersService
{

    public async Task<int> GetUsersCount()
    {
        return 5;
    }
    public async Task<IEnumerable<UserDetails>> GetUsers()
    {

        return new List<UserDetails>()
        {
            new UserDetails()
            {
                Id = 0,
                Login = "Krishna",
                PricingLevel = Domain.Enums.PricingLevel.Basic,
                RecordCapacity = new Domain.Entities.Login.RecordCapacity()
                {
                    UsedCapacity = 5,
                    TotalCapacity = 10
                }
            },
            new UserDetails()
            {
                Id = 1,
                Login = "Webb",
                PricingLevel = Domain.Enums.PricingLevel.Basic,
                RecordCapacity = new Domain.Entities.Login.RecordCapacity()
                {
                    UsedCapacity = 5,
                    TotalCapacity = 100
                }
            },

        };

    }
}




using FinanceManager.Core.Entities;
using FinanceManager.Core.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages
{
	public partial class AddAccount : ComponentBase
	{
		[Inject]
		public IAccountService AccountService { get; set; }
		public string AccountName { get; set; }

		public void Add()
		{
			if (!AccountService.Exists(AccountName))
			{
				AccountService.AddBankAccount(new BankAccount(AccountName, Core.Enums.AccountType.Asset));
				StateHasChanged();
			}
			else
			{
				return;
			}
			AccountName = "";
		}
	}
}

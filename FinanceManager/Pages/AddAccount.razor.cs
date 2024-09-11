using FinanceManager.Core.Entities;
using FinanceManager.Core.Repositories;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages
{
	public partial class AddAccount : ComponentBase
	{
		[Inject]
		public IBankAccountRepository AccountsService { get; set; }
		public string AccountName { get; set; }

		public void Add()
		{
			if (!AccountsService.Exists(AccountName))
			{
				AccountsService.AddBankAccount(new BankAccount(AccountName, new List<BankAccountEntry>() { new BankAccountEntry() { SenderName = "xd" } }, Core.Enums.AccountType.Asset));
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

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
				AccountsService.AddBankAccountEntry(AccountName, new List<BankAccountEntry>());
			}
			else
			{
				return;
			}
			AccountName = "";
		}
	}
}

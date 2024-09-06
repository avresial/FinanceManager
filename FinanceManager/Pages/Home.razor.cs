using FinanceManager.Core.Entities;
using FinanceManager.Core.Repositories;
using Microsoft.AspNetCore.Components;
using System.Collections.ObjectModel;

namespace FinanceManager.Pages
{
	public partial class Home : ComponentBase
	{
		[Inject]
		public IBankAccountRepository AccountsService { get; set; }


		public List<BankAccount> Accounts = [];
		public ObservableCollection<BankAccountEntry> AccountEntries { get; set; }

		public string AccountName { get; set; }
		public bool isLoading { get; set; }
		public string ErrorMessage { get; set; } = string.Empty;


		protected override async Task OnInitializedAsync()
		{
			GetAllTime();
		}


		private void SetAccountsWithinTimeSpan(TimeSpan timeSpan)
		{
			try
			{
				Accounts.Clear();

				foreach (BankAccount account in AccountsService.Get())
				{
					List<BankAccountEntry> entries = account.Entries.Where(x => (x.PostingDate - DateTime.Now).Duration() < timeSpan).ToList();
					if (!entries.Any()) continue;

					Accounts.Add(account);
				}
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
			}
		}
		public async Task GetAllTime()
		{
			try
			{
				Accounts = AccountsService.Get().ToList();
				StateHasChanged();
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
			}
		}
		public async Task GetThisMonth()
		{
			var date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

			SetAccountsWithinTimeSpan((DateTime.Now - date).Duration());
		}

		public async Task GetThisYear()
		{
			var date = new DateTime(DateTime.Now.Year, 1, 1);
			SetAccountsWithinTimeSpan((DateTime.Now - date).Duration());
		}


	}
}


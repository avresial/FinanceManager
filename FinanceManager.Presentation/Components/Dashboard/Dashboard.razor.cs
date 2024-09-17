using FinanceManager.Core.Entities;
using FinanceManager.Core.Repositories;
using Microsoft.AspNetCore.Components;
using System.Collections.ObjectModel;

namespace FinanceManager.Presentation.Components.Dashboard
{
	public partial class Dashboard : ComponentBase
	{
		const int UnitHeight = 130;
		[Inject]
		public IBankAccountRepository AccountsService { get; set; }


		public List<BankAccount> Accounts;
		public ObservableCollection<BankAccountEntry> AccountEntries { get; set; }

		public DateTime StartDateTime { get; set; }
		public string AccountName { get; set; }
		public bool isLoading { get; set; }
		public string ErrorMessage { get; set; } = string.Empty;

		protected override async Task OnInitializedAsync()
		{
			await GetThisMonth();
		}

		public async Task GetAllTime()
		{
			StartDateTime = new DateTime();
		}
		public async Task GetThisMonth()
		{
			StartDateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
		}

		public async Task GetThisYear()
		{
			StartDateTime = new DateTime(DateTime.Now.Year, 1, 1);
		}

	}
}
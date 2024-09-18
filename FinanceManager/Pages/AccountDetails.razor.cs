using FinanceManager.Core.Entities;
using FinanceManager.Core.Repositories;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages
{
	public partial class AccountDetails : ComponentBase
	{
		private const int maxTableSize = 500;

		[Parameter]
		public string AccountName { get; set; }

		[Inject]
		public IBankAccountRepository BankAccountRepository { get; set; }

		public string ErrorMessage { get; set; } = string.Empty;

		public IEnumerable<BankAccountEntry> Entries { get; set; }

		protected override async Task OnInitializedAsync()
		{
			UpdateEntries();
		}

		protected override async Task OnParametersSetAsync()
		{
			UpdateEntries();
		}

		private void UpdateEntries()
		{
			try
			{
				DateTime dateStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
				if (BankAccountRepository.Exists(AccountName))
					Entries = BankAccountRepository.Get(AccountName, dateStart, DateTime.Now).Entries.Take(maxTableSize).OrderByDescending(x => x.PostingDate);
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
			}
		}
	}
}

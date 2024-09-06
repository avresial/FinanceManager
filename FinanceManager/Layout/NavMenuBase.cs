using FinanceManager.Core.Repositories;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Layout
{
	public class NavMenuBase : ComponentBase
	{
		[Inject]
		public IBankAccountRepository AccountsService { get; set; }

		public List<string> AccountNames = new List<string>();

		public string ErrorMessage { get; set; } = string.Empty;

		protected override async Task OnInitializedAsync()
		{
			try
			{
				AccountNames = [.. AccountsService.Get().Select(x => x.Name).OrderBy(x => x)];
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
			}
		}

		private void AccountsService_AccountsChanged(string arg1)
		{
			try
			{
				AccountNames = [.. AccountsService.Get().Select(x => x.Name).OrderBy(x => x)];
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
			}

			StateHasChanged();
		}
	}
}

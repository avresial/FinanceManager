using FinanceManager.Models;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Pages.Dashboard
{
	public partial class AccountDetailsPreview : ComponentBase
	{
		[Parameter]
		public AccountModel AccountModel { get; set; }
	}
}

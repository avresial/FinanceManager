using FinanceManager.Domain.Dashboard.Dtos;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Transactions.Components;

public partial class AddTransactionDialog : ComponentBase
{
    private TransactionKind _kind = TransactionKind.Cash;
    private bool _wasVisible;

    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>The kind the dialog opens on.</summary>
    [Parameter] public TransactionKind InitialKind { get; set; } = TransactionKind.Cash;

    /// <summary>
    /// Raised after an investment trade is saved. Cash and bond forms announce their saves through
    /// <c>AccountDataSynchronizationService</c>; the investment form does not, so the host reloads on this.
    /// </summary>
    [Parameter] public EventCallback OnSaved { get; set; }

    protected override void OnParametersSet()
    {
        if (Visible && !_wasVisible) _kind = InitialKind;
        _wasVisible = Visible;
    }

    private void OnKindChanged(TransactionKind kind) => _kind = kind;

    private Task CloseAsync() => VisibleChanged.InvokeAsync(false);

    private async Task OnInvestmentSavedAsync()
    {
        await OnSaved.InvokeAsync();
        await CloseAsync();
    }
}
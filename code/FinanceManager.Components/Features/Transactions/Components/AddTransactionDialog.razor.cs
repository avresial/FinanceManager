using FinanceManager.Domain.Dashboard.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace FinanceManager.Components.Features.Transactions.Components;

public partial class AddTransactionDialog : ComponentBase
{
    private TransactionKind _kind = TransactionKind.Cash;
    private bool _wasVisible;
    private bool _focusPending;
    private ElementReference _dialogBody;

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
        if (Visible && !_wasVisible)
        {
            _kind = InitialKind;
            _focusPending = true;
        }
        _wasVisible = Visible;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusPending || !Visible) return;
        _focusPending = false;
        // The key handler only hears Escape while focus is inside the dialog, so move it there on open.
        try
        {
            await _dialogBody.FocusAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSException or TaskCanceledException)
        {
            // Focus is a convenience; the dialog works without it.
        }
    }

    private Task OnKeyDown(KeyboardEventArgs args) => args.Key == "Escape" ? CloseAsync() : Task.CompletedTask;

    private void OnKindChanged(TransactionKind kind) => _kind = kind;

    private Task CloseAsync() => VisibleChanged.InvokeAsync(false);

    private async Task OnInvestmentSavedAsync()
    {
        await OnSaved.InvokeAsync();
        await CloseAsync();
    }
}
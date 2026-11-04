using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FinanceManager.Components.Shared.Components;

/// <summary>
/// Shows a skeleton of a fixed height until the placeholder comes near the viewport, then renders
/// its content and keeps it. Off-screen cards therefore pay their first-render cost (chart setup,
/// data requests) only when the user scrolls towards them instead of all at once on page load.
/// </summary>
public partial class LazyMount : IAsyncDisposable
{
    // How far outside the viewport content starts to render, so it is usually ready before it scrolls in.
    public const string PreloadMargin = "200px 0px";

    // Below md, cards size to their content (.fm-phone-auto-height in cardStyle.css), so a card can be
    // shorter or taller than its placeholder and moves the cards below it when it mounts. Rendering further
    // ahead keeps that shift off screen, including on fast scrolls, at the cost of mounting more cards on load.
    public const string AutoHeightMediaQuery = "(max-width: 959.98px)";
    public const string AutoHeightPreloadMargin = "150% 0px";

    private ElementReference _placeholder;
    private DotNetObjectReference<LazyMount>? _selfReference;
    private bool _isRevealed;

    [Inject] public required IJSRuntime JSRuntime { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Placeholder height; match the content's height so the layout does not shift on reveal.</summary>
    [Parameter, EditorRequired] public string Height { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _isRevealed)
            return;

        _selfReference = DotNetObjectReference.Create(this);
        await JSRuntime.InvokeVoidAsync("financeManager.observeLazyMount", _placeholder, _selfReference,
            new { margin = PreloadMargin, autoHeightMediaQuery = AutoHeightMediaQuery, autoHeightMargin = AutoHeightPreloadMargin });
    }

    /// <summary>Called by the browser observer once the placeholder is near the viewport.</summary>
    [JSInvokable]
    public void Reveal()
    {
        if (_isRevealed)
            return;

        _isRevealed = true;
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (_selfReference is null)
            return;

        try
        {
            if (!_isRevealed)
                await JSRuntime.InvokeVoidAsync("financeManager.unobserveLazyMount", _placeholder);
        }
        catch (JSDisconnectedException)
        {
            // The browser is gone, so there is no observer left to release.
        }
        finally
        {
            _selfReference.Dispose();
        }
    }
}
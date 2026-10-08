using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FinanceManager.Components.Shared.Components;

/// <summary>
/// Scroll container that fades its bottom edge while more content is hidden below. The fade is
/// toggled by a browser-side observer on a trailing sentinel, so it disappears when the list fits
/// or is scrolled to the end.
/// </summary>
public partial class ScrollFadeArea : IAsyncDisposable
{
    private ElementReference _element;
    private bool _isObserved;

    [Inject] public required IJSRuntime JSRuntime { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Extra CSS classes for the scroll container, such as padding and flex sizing.</summary>
    [Parameter] public string? Class { get; set; }

    [Parameter] public string? Style { get; set; }

    private string ResolvedClass => string.IsNullOrWhiteSpace(Class) ? "fm-scroll-fade" : $"fm-scroll-fade {Class}";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        await JSRuntime.InvokeVoidAsync("financeManager.observeScrollFade", _element);
        _isObserved = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_isObserved)
            return;

        try
        {
            await JSRuntime.InvokeVoidAsync("financeManager.unobserveScrollFade", _element);
        }
        catch (JSDisconnectedException)
        {
            // The browser is gone, so there is no observer left to release.
        }
    }
}
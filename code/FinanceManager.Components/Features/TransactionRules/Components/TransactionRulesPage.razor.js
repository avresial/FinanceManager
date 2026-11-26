// Moves keyboard focus to a native control rendered by a MudBlazor wrapper that exposes no ElementReference.
export function focusInside(containerId) {
    document.getElementById(containerId)?.querySelector("input, button")?.focus();
}

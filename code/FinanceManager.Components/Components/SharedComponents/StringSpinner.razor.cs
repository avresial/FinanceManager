using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.SharedComponents;

public partial class StringSpinner : IDisposable
{
    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private string _spinnerString = "?";
    private char _defaultFallbackCharacter = '?';

    [Inject] required public ILogger<StringSpinner> Logger { get; set; }
    [Parameter] public string Characters { get; set; } = "!@#$%^&*";
    [Parameter] public int CharactersCount { get; set; } = 1;

    private char GetRandomCharacter()
    {
        if (string.IsNullOrEmpty(Characters)) return _defaultFallbackCharacter;

        return Characters[Random.Shared.Next(Characters.Length)];
    }

    protected override async Task OnInitializedAsync()
    {
        var token = _cancellationTokenSource.Token;
        while (!token.IsCancellationRequested)
        {
            _spinnerString = string.Empty;
            for (int i = 0; i < CharactersCount; i++)
                _spinnerString += GetRandomCharacter().ToString();
            StateHasChanged();
            try
            {
                await Task.Delay(Random.Shared.Next(1000, 5000), token);
            }
            catch (TaskCanceledException)
            {

            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in StringSpinner: {Message}", ex.Message);
                break;
            }
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
    }
}
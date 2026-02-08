using System.Threading.Tasks;

/// <summary>
/// Abstraction over platform-specific clipboard access.
/// </summary>
internal interface IClipboardProvider
{
    Task<string?> GetTextAsync();
    Task SetTextAsync(string text);
}

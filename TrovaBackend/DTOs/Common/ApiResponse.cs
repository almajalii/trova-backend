namespace TrovaBackend.DTOs.Common;

/// <summary>
/// Consistent envelope for every API response, so the frontend always
/// knows where to look for success/message/data.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
}

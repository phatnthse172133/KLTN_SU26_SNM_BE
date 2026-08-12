namespace ApplicationLayer.DTOs.Requests;

/// <summary>
/// Payment-provider credentials submitted by the currently authenticated booth owner.
/// The booth identifier is deliberately omitted so an owner cannot configure another booth.
/// Empty fields preserve an existing value.
/// </summary>
public sealed class UpsertMyBoothPayOsCredentialRequest
{
    public string? ClientId { get; init; }
    public string? ApiKey { get; init; }
    public string? ChecksumKey { get; init; }
    public string? PayoutClientId { get; init; }
    public string? PayoutApiKey { get; init; }
    public string? PayoutChecksumKey { get; init; }
}

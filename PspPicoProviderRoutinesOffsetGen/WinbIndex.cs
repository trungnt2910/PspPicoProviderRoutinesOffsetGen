using System.Text.Json.Serialization;

namespace PspPicoProviderRoutinesOffsetGen;

public class WinbIndex
{
    [JsonPropertyName("fileInfo")]
    public BinaryInfo? FileInfo { get; set; }
    [JsonPropertyName("windowsVersions")]
    public Dictionary<string, object>? WindowsVersions { get; set; }
}

public class BinaryInfo
{
    [JsonPropertyName("size")]
    public ulong? Size { get; set; }
    [JsonPropertyName("md5")]
    public string? Md5 { get; set; }
    [JsonPropertyName("sha1")]
    public string? Sha1 { get; set; }
    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }
    [JsonPropertyName("machineType")]
    public uint MachineType { get; set; }
    [JsonPropertyName("timestamp")]
    public ulong? Timestamp { get; set; }
    [JsonPropertyName("virtualSize")]
    public ulong? VirtualSize { get; set; }
    [JsonPropertyName("signingStatus")]
    public string? SigningStatus { get; set; }
    [JsonPropertyName("version")]
    public string? Version { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("signatureType")]
    public string? SignatureType { get; set; }
    [JsonPropertyName("signingDate")]
    public DateTime[]? SigningDate { get; set; }
    [JsonPropertyName("lastSectionVirtualAddress")]
    public ulong? LastSectionVirtualAddress { get; set; }
    [JsonPropertyName("lastSectionPointerToRawData")]
    public ulong? LastSectionPointerToRawData { get; set; }
}

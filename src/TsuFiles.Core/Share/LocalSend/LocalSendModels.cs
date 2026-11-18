// Thông điệp của giao thức LocalSend v2 (ADR-0004 S3, https://github.com/localsend/protocol) - chỉ những trường
// TsuFiles dùng. Viết lại từ mô tả giao thức để nói chuyện được với app LocalSend (Android, iOS 15+, desktop); không
// chép mã của LocalSend. JSON qua source generation (không reflection, an toàn khi cắt tỉa).

using System.Text.Json.Serialization;

namespace TsuFiles.Core.Share.LocalSend;

/// <summary>Thông tin một thiết bị: gói multicast, thân /register, phần "info" của /prepare-upload.</summary>
public sealed record LocalSendInfo
{
    public string Alias { get; init; } = "";
    public string Version { get; init; } = LocalSendProtocol.Version;
    public string? DeviceModel { get; init; }
    public string? DeviceType { get; init; }
    public string Fingerprint { get; init; } = "";
    public int? Port { get; init; }
    public string? Protocol { get; init; }
    public bool? Download { get; init; }

    /// <summary>Chỉ có trong gói multicast: true = thông báo (cần trả lời), false = trả lời.</summary>
    public bool? Announce { get; init; }
}

public sealed record LocalSendFile
{
    public string Id { get; init; } = "";
    public string FileName { get; init; } = "";
    public long Size { get; init; }
    public string FileType { get; init; } = "application/octet-stream";
    public string? Sha256 { get; init; }
    public string? Preview { get; init; }
}

public sealed record LocalSendPrepareUpload
{
    public LocalSendInfo Info { get; init; } = new();
    public Dictionary<string, LocalSendFile> Files { get; init; } = [];
}

public sealed record LocalSendPrepareResponse
{
    public string SessionId { get; init; } = "";
    public Dictionary<string, string> Files { get; init; } = [];
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    // Máy lạ gửi "alias": null / "info": null thì là JSON sai (400), không phải NullReferenceException.
    RespectNullableAnnotations = true,
    MaxDepth = 8)]
[JsonSerializable(typeof(LocalSendInfo))]
[JsonSerializable(typeof(LocalSendPrepareUpload))]
[JsonSerializable(typeof(LocalSendPrepareResponse))]
internal sealed partial class LocalSendJsonContext : JsonSerializerContext;

public static class LocalSendProtocol
{
    public const string Version = "2.1";
    public const int DefaultPort = 53317;
    public const string MulticastGroup = "224.0.0.167";
    public const string ApiPrefix = "/api/localsend/v2/";

    /// <summary>Thân JSON của /register, /prepare-upload tối đa (danh sách tệp lớn vẫn vừa).</summary>
    public const int MaxJsonBytes = 1 << 20;

    /// <summary>Số tệp tối đa trong một lần gửi.</summary>
    public const int MaxFilesPerSession = 10_000;
}

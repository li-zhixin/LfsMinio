using System.Text.Json.Serialization;

namespace LfsMinio.Protocol;

[JsonSerializable(typeof(InitEvent))]
[JsonSerializable(typeof(UploadEvent))]
[JsonSerializable(typeof(DownloadEvent))]
[JsonSerializable(typeof(TerminateEvent))]
[JsonSerializable(typeof(LfsEvent))]
[JsonSerializable(typeof(InitOkResponse))]
[JsonSerializable(typeof(InitErrorResponse))]
[JsonSerializable(typeof(ProgressResponse))]
[JsonSerializable(typeof(CompleteResponse))]
[JsonSerializable(typeof(LfsResponse))]
[JsonSerializable(typeof(LfsError))]
[JsonSerializable(typeof(object))]
public partial class LfsJsonContext : JsonSerializerContext
{
}
namespace MMRCPlayer.Models;

public enum ContentType
{
    Video,
    Audio,
    Image,
    Pdf,
    Pptx,
    Folder,
    Streaming,
    Placeholder,
    Unknown
}

public static class ContentTypeExtensions
{
    public static ContentType FromString(string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "video" => ContentType.Video,
            "audio" => ContentType.Audio,
            "image" => ContentType.Image,
            "pdf" => ContentType.Pdf,
            "pptx" => ContentType.Pptx,
            "folder" => ContentType.Folder,
            "streaming" => ContentType.Streaming,
            "placeholder" => ContentType.Placeholder,
            _ => ContentType.Unknown
        };
    }
}

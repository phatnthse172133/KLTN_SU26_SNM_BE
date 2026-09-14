using System.IO.Compression;
using System.Text;
using ApplicationLayer.Exceptions;

namespace ApplicationLayer.Services.Chats;

public enum ChatAttachmentKind
{
    Image,
    Pdf,
    Word,
    Spreadsheet,
    Presentation,
    Text
}

public sealed record ChatAttachmentDescriptor(
    ChatAttachmentKind Kind,
    string Extension,
    string ContentType,
    IReadOnlySet<string> AllowedContentTypes)
{
    public bool IsImage => Kind == ChatAttachmentKind.Image;
}

public static class ChatAttachmentPolicy
{
    public const long MaxFileSize = 10 * 1024 * 1024;

    private static readonly HashSet<string> GenericContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty,
        "application/octet-stream"
    };

    private static readonly IReadOnlyDictionary<string, ChatAttachmentDescriptor> ByExtension =
        new Dictionary<string, ChatAttachmentDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = Image(".jpg", "image/jpeg"),
            [".jpeg"] = Image(".jpeg", "image/jpeg"),
            [".png"] = Image(".png", "image/png"),
            [".webp"] = Image(".webp", "image/webp"),
            [".heic"] = Image(".heic", "image/heic", "image/heif"),
            [".heif"] = Image(".heif", "image/heif", "image/heic"),
            [".pdf"] = File(ChatAttachmentKind.Pdf, ".pdf", "application/pdf", "application/x-pdf"),
            [".doc"] = File(ChatAttachmentKind.Word, ".doc", "application/msword"),
            [".docx"] = File(
                ChatAttachmentKind.Word,
                ".docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            [".xls"] = File(ChatAttachmentKind.Spreadsheet, ".xls", "application/vnd.ms-excel"),
            [".xlsx"] = File(
                ChatAttachmentKind.Spreadsheet,
                ".xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            [".ppt"] = File(ChatAttachmentKind.Presentation, ".ppt", "application/vnd.ms-powerpoint"),
            [".pptx"] = File(
                ChatAttachmentKind.Presentation,
                ".pptx",
                "application/vnd.openxmlformats-officedocument.presentationml.presentation"),
            [".txt"] = File(ChatAttachmentKind.Text, ".txt", "text/plain")
        };

    public static ChatAttachmentDescriptor ValidateMetadata(string fileName, string? contentType, long length)
    {
        if (length <= 0)
            throw AppException.BadRequest("Attachment file is required.", "CHAT_ATTACHMENT_REQUIRED");
        if (length > MaxFileSize)
            throw AppException.PayloadTooLarge(
                "The attachment must be 10 MB or smaller.",
                "CHAT_ATTACHMENT_TOO_LARGE");

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !ByExtension.TryGetValue(extension, out var descriptor))
            throw TypeNotAllowed();

        var normalizedContentType = contentType?.Trim() ?? string.Empty;
        if (!GenericContentTypes.Contains(normalizedContentType)
            && !descriptor.AllowedContentTypes.Contains(normalizedContentType))
        {
            throw TypeNotAllowed();
        }

        return descriptor;
    }

    public static async Task<MemoryStream> BufferAndValidateContentAsync(
        Stream source,
        ChatAttachmentDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        var buffered = new MemoryStream();
        try
        {
            var copyBuffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(copyBuffer.AsMemory(), cancellationToken)) > 0)
            {
                if (buffered.Length + read > MaxFileSize)
                    throw AppException.PayloadTooLarge(
                        "The attachment must be 10 MB or smaller.",
                        "CHAT_ATTACHMENT_TOO_LARGE");
                await buffered.WriteAsync(copyBuffer.AsMemory(0, read), cancellationToken);
            }

            if (buffered.Length == 0)
                throw AppException.BadRequest("Attachment file is required.", "CHAT_ATTACHMENT_REQUIRED");

            buffered.Position = 0;
            ValidateContent(buffered, descriptor);
            buffered.Position = 0;
            return buffered;
        }
        catch
        {
            await buffered.DisposeAsync();
            throw;
        }
    }

    public static void ValidateContent(Stream stream, ChatAttachmentDescriptor descriptor)
    {
        if (!stream.CanSeek)
            throw InvalidFile();

        stream.Position = 0;
        var header = new byte[12];
        var bytesRead = stream.Read(header, 0, header.Length);
        stream.Position = 0;

        var valid = descriptor.Extension switch
        {
            ".jpg" or ".jpeg" => bytesRead >= 3
                && header.AsSpan(0, 3).SequenceEqual(new byte[] { 0xff, 0xd8, 0xff }),
            ".png" => bytesRead >= 8
                && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            ".webp" => bytesRead >= 12
                && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            ".heic" or ".heif" => IsHeifFamily(header, bytesRead),
            ".pdf" => bytesRead >= 4 && header.AsSpan(0, 4).SequenceEqual("%PDF"u8),
            ".doc" or ".xls" or ".ppt" => bytesRead >= 8
                && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 }),
            ".docx" => IsOpenXmlPackage(stream, "word/document.xml"),
            ".xlsx" => IsOpenXmlPackage(stream, "xl/workbook.xml"),
            ".pptx" => IsOpenXmlPackage(stream, "ppt/presentation.xml"),
            ".txt" => IsUtf8Text(stream),
            _ => false
        };

        stream.Position = 0;
        if (!valid) throw InvalidFile();
    }

    private static ChatAttachmentDescriptor Image(string extension, string contentType, params string[] aliases)
        => Create(ChatAttachmentKind.Image, extension, contentType, aliases);

    private static ChatAttachmentDescriptor File(
        ChatAttachmentKind kind,
        string extension,
        string contentType,
        params string[] aliases)
        => Create(kind, extension, contentType, aliases);

    private static ChatAttachmentDescriptor Create(
        ChatAttachmentKind kind,
        string extension,
        string contentType,
        params string[] aliases)
        => new(
            kind,
            extension,
            contentType,
            new HashSet<string>(new[] { contentType }.Concat(aliases), StringComparer.OrdinalIgnoreCase));

    private static bool IsHeifFamily(byte[] header, int bytesRead)
    {
        if (bytesRead < 12 || !header.AsSpan(4, 4).SequenceEqual("ftyp"u8)) return false;
        var brand = Encoding.ASCII.GetString(header, 8, 4);
        return brand is "heic" or "heix" or "hevc" or "hevx" or "heim" or "heis" or "mif1" or "msf1";
    }

    private static bool IsOpenXmlPackage(Stream stream, string requiredEntry)
    {
        try
        {
            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            return archive.Entries.Any(entry =>
                string.Equals(entry.FullName, requiredEntry, StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidDataException)
        {
            return false;
        }
        finally
        {
            stream.Position = 0;
        }
    }

    private static bool IsUtf8Text(Stream stream)
    {
        try
        {
            stream.Position = 0;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            if (bytes.Contains((byte)0)) return false;
            _ = new UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
        finally
        {
            stream.Position = 0;
        }
    }

    private static AppException TypeNotAllowed()
        => AppException.BadRequest(
            "The attachment format is not supported.",
            "CHAT_ATTACHMENT_TYPE_NOT_ALLOWED");

    private static AppException InvalidFile()
        => AppException.BadRequest(
            "The attachment content does not match its file type.",
            "CHAT_ATTACHMENT_INVALID_FILE");
}

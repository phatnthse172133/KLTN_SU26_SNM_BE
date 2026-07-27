using ApplicationLayer.Exceptions;
using InfrastructureLayer.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Moq;

namespace TestingLayer;

public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly LocalFileStorageService _storage;

    public LocalFileStorageServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"snm-avatar-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootPath);

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.ContentRootPath).Returns(_rootPath);
        environment.SetupGet(value => value.WebRootPath).Returns(Path.Combine(_rootPath, "wwwroot"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UploadStorage:RootPath"] = Path.Combine(_rootPath, "wwwroot", "uploads")
            })
            .Build();
        _storage = new LocalFileStorageService(environment.Object, configuration);
    }

    [Fact]
    public async Task SaveAvatarAsync_ValidPng_UsesGeneratedManagedPath()
    {
        var pngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x00
        };

        var url = await _storage.SaveAvatarAsync(
            new MemoryStream(pngBytes),
            "client-name.png",
            "image/png",
            pngBytes.Length);

        Assert.StartsWith("/uploads/avatars/", url, StringComparison.Ordinal);
        Assert.EndsWith(".png", url, StringComparison.Ordinal);
        Assert.DoesNotContain("client-name", url, StringComparison.Ordinal);
        Assert.True(Guid.TryParseExact(Path.GetFileNameWithoutExtension(url), "N", out _));
        var storedFile = Path.Combine(
            _rootPath,
            "wwwroot",
            url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(storedFile));
        Assert.Equal(pngBytes, await File.ReadAllBytesAsync(storedFile));
    }

    [Theory]
    [InlineData("photo.jpg", "image/jpeg", "jpeg")]
    [InlineData("photo.jpeg", "image/jpeg", "jpeg")]
    [InlineData("photo.png", "image/png", "png")]
    [InlineData("photo.webp", "image/webp", "webp")]
    public async Task SaveAvatarAsync_AllSupportedFormats_AreAccepted(
        string fileName,
        string contentType,
        string format)
    {
        var bytes = format switch
        {
            "jpeg" => new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 },
            "png" => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0 },
            "webp" => new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 },
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        var url = await _storage.SaveAvatarAsync(
            new MemoryStream(bytes), fileName, contentType, bytes.Length);

        Assert.StartsWith("/uploads/avatars/", url, StringComparison.Ordinal);
        Assert.True(Guid.TryParseExact(Path.GetFileNameWithoutExtension(url), "N", out _));
    }

    [Fact]
    public async Task SaveAvatarAsync_MimeDoesNotMatchMagicBytes_IsRejected()
    {
        var jpegBytes = new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        var exception = await Assert.ThrowsAsync<AppException>(() => _storage.SaveAvatarAsync(
            new MemoryStream(jpegBytes),
            "spoofed.png",
            "image/png",
            jpegBytes.Length));

        Assert.Equal("INVALID_AVATAR_FILE", exception.ErrorCode);
        Assert.Empty(GetStoredAvatars());
    }

    [Fact]
    public async Task SaveAvatarAsync_JpgExtensionWithPngMimeAndBytes_IsRejected()
    {
        var pngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0
        };

        var exception = await Assert.ThrowsAsync<AppException>(() => _storage.SaveAvatarAsync(
            new MemoryStream(pngBytes), "spoofed.jpg", "image/png", pngBytes.Length));

        Assert.Equal("INVALID_AVATAR_FILE", exception.ErrorCode);
    }

    [Theory]
    [InlineData("invalid.jpg", "image/jpeg", "not an image")]
    [InlineData("renamed-svg.png", "image/png", "<svg></svg>")]
    public async Task SaveAvatarAsync_InvalidOrRenamedActiveContent_IsRejected(
        string fileName,
        string contentType,
        string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        var exception = await Assert.ThrowsAsync<AppException>(() => _storage.SaveAvatarAsync(
            new MemoryStream(bytes), fileName, contentType, bytes.Length));

        Assert.Equal("INVALID_AVATAR_FILE", exception.ErrorCode);
    }

    [Fact]
    public async Task SaveAvatarAsync_EmptyFile_IsRejected()
    {
        var exception = await Assert.ThrowsAsync<AppException>(() => _storage.SaveAvatarAsync(
            new MemoryStream(), "empty.png", "image/png", 0));

        Assert.Equal("AVATAR_FILE_REQUIRED", exception.ErrorCode);
    }

    [Theory]
    [InlineData("../../escape.png")]
    [InlineData("..\\..\\escape.png")]
    [InlineData("C:\\temp\\escape.png")]
    [InlineData("/tmp/escape.png")]
    [InlineData("crafted-name.png")]
    public async Task SaveAvatarAsync_ClientPathNeverControlsFinalStoragePath(string clientFileName)
    {
        var pngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0
        };

        var url = await _storage.SaveAvatarAsync(
            new MemoryStream(pngBytes), clientFileName, "image/png", pngBytes.Length);

        var storedName = Path.GetFileNameWithoutExtension(url);
        Assert.True(Guid.TryParseExact(storedName, "N", out _));
        Assert.DoesNotContain("..", url, StringComparison.Ordinal);
        Assert.DoesNotContain("escape", url, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("crafted", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveAvatarAsync_ActualStreamExceedsCapDespiteDeclaredLength_IsRejected()
    {
        var oversizedBytes = new byte[(5 * 1024 * 1024) + 1];
        oversizedBytes[0] = 0x89;
        oversizedBytes[1] = 0x50;
        oversizedBytes[2] = 0x4E;
        oversizedBytes[3] = 0x47;

        var exception = await Assert.ThrowsAsync<AppException>(() => _storage.SaveAvatarAsync(
            new MemoryStream(oversizedBytes),
            "oversized.png",
            "image/png",
            1));

        Assert.Equal("AVATAR_FILE_TOO_LARGE", exception.ErrorCode);
        Assert.Equal(413, exception.StatusCode);
        Assert.Empty(GetStoredAvatars());
    }

    [Fact]
    public async Task DeleteAvatarIfManagedAsync_DoesNotDeleteExternalOrUnmanagedFiles()
    {
        var unrelatedPath = Path.Combine(_rootPath, "wwwroot", "unrelated.png");
        Directory.CreateDirectory(Path.GetDirectoryName(unrelatedPath)!);
        await File.WriteAllBytesAsync(unrelatedPath, new byte[] { 1, 2, 3 });

        await _storage.DeleteAvatarIfManagedAsync("https://example.com/avatar.png");
        await _storage.DeleteAvatarIfManagedAsync("/unrelated.png");

        Assert.True(File.Exists(unrelatedPath));
    }

    [Fact]
    public async Task DeleteAvatarIfManagedAsync_DeletesOnlyGuidFileInsideManagedPrefix()
    {
        var avatarDirectory = Path.Combine(_rootPath, "wwwroot", "uploads", "avatars");
        Directory.CreateDirectory(avatarDirectory);
        var managedName = $"{Guid.NewGuid():N}.png";
        var managedPath = Path.Combine(avatarDirectory, managedName);
        var craftedPath = Path.Combine(avatarDirectory, "crafted.png");
        var outsidePath = Path.Combine(_rootPath, "wwwroot", "outside.png");
        await File.WriteAllBytesAsync(managedPath, new byte[] { 1 });
        await File.WriteAllBytesAsync(craftedPath, new byte[] { 2 });
        await File.WriteAllBytesAsync(outsidePath, new byte[] { 3 });

        await _storage.DeleteAvatarIfManagedAsync($"/uploads/avatars/{managedName}");
        await _storage.DeleteAvatarIfManagedAsync("/uploads/avatars/crafted.png");
        await _storage.DeleteAvatarIfManagedAsync("/uploads/avatars/../../outside.png");
        await _storage.DeleteAvatarIfManagedAsync("/uploads/avatars/..\\..\\outside.png");
        await _storage.DeleteAvatarIfManagedAsync("/uploads/avatars/C:\\temp\\outside.png");

        Assert.False(File.Exists(managedPath));
        Assert.True(File.Exists(craftedPath));
        Assert.True(File.Exists(outsidePath));
    }

    private string[] GetStoredAvatars()
    {
        var directory = Path.Combine(_rootPath, "wwwroot", "uploads", "avatars");
        return Directory.Exists(directory) ? Directory.GetFiles(directory) : Array.Empty<string>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}

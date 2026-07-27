using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.Packages;
using ApplicationLayer.Services.Storage;
using AutoMapper;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using Moq;

namespace TestingLayer;

public class PackageImageServiceTests
{
    private readonly Mock<IGenericRepository<Package>> _mockPackages;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IPackagePriceRepository> _mockPackagePrices;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IFileStorageService> _mockFileStorage;
    private readonly Mock<ILogger<PackageService>> _mockLogger;
    private readonly PackageService _service;

    private const string OldImageUrl = "/uploads/images/packages/old.png";
    private const string NewImageUrl = "/uploads/images/packages/new.png";

    public PackageImageServiceTests()
    {
        _mockPackages = new Mock<IGenericRepository<Package>>();
        _mockMapper = new Mock<IMapper>();
        _mockPackagePrices = new Mock<IPackagePriceRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockFileStorage = new Mock<IFileStorageService>();
        _mockLogger = new Mock<ILogger<PackageService>>();
        _service = new PackageService(_mockPackages.Object, _mockPackagePrices.Object, _mockUnitOfWork.Object, _mockMapper.Object, _mockFileStorage.Object, _mockLogger.Object);

        _mockMapper.Setup(m => m.Map<PackageResponse>(It.IsAny<Package>()))
            .Returns((Package p) => new PackageResponse { Id = p.Id, PackageName = p.PackageName, ImageUrl = p.ImageUrl });
    }

    private static Package CreatePackage(Guid id, string? imageUrl = null)
    {
        return new Package { Id = id, PackageName = "Market Basic", Code = "MARKET_BASIC", Price = 500000, DurationDays = 30, ImageUrl = imageUrl };
    }

    private static Stream CreateStream() => new MemoryStream(new byte[16]);

    [Fact]
    public async Task UploadImageAsync_ReplaceExisting_SavesNewUpdatesEntityThenDeletesOldFile()
    {
        var packageId = Guid.NewGuid();
        var package = CreatePackage(packageId, OldImageUrl);
        var callOrder = new List<string>();

        _mockPackages.Setup(r => r.GetByIdAsync(packageId)).ReturnsAsync(package);
        _mockFileStorage.Setup(s => s.SaveImageAsync("packages", It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("save-file"))
            .ReturnsAsync(NewImageUrl);
        _mockPackages.Setup(r => r.SaveChangesAsync())
            .Callback(() => callOrder.Add("save-db"))
            .ReturnsAsync(1);
        _mockFileStorage.Setup(s => s.DeleteImageIfManagedAsync(OldImageUrl, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("delete-old"))
            .Returns(Task.CompletedTask);

        using var stream = CreateStream();
        var result = await _service.UploadImageAsync(packageId, stream, "photo.png", "image/png", 16);

        Assert.Equal(NewImageUrl, package.ImageUrl);
        Assert.Equal(NewImageUrl, result.Data.ImageUrl);
        _mockPackages.Verify(r => r.Update(package), Times.Once);
        _mockFileStorage.Verify(s => s.DeleteImageIfManagedAsync(OldImageUrl, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(new List<string> { "save-file", "save-db", "delete-old" }, callOrder);
    }

    [Fact]
    public async Task UploadImageAsync_FirstUpload_NoOldFile_DoesNotCallDelete()
    {
        var packageId = Guid.NewGuid();
        var package = CreatePackage(packageId, imageUrl: null);

        _mockPackages.Setup(r => r.GetByIdAsync(packageId)).ReturnsAsync(package);
        _mockFileStorage.Setup(s => s.SaveImageAsync("packages", It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewImageUrl);

        using var stream = CreateStream();
        var result = await _service.UploadImageAsync(packageId, stream, "photo.png", "image/png", 16);

        Assert.Equal(NewImageUrl, package.ImageUrl);
        _mockPackages.Verify(r => r.SaveChangesAsync(), Times.Once);
        _mockFileStorage.Verify(s => s.DeleteImageIfManagedAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadImageAsync_SaveChangesThrows_DeletesNewFileAndKeepsOldImageUrl()
    {
        var packageId = Guid.NewGuid();
        var package = CreatePackage(packageId, OldImageUrl);

        _mockPackages.Setup(r => r.GetByIdAsync(packageId)).ReturnsAsync(package);
        _mockFileStorage.Setup(s => s.SaveImageAsync("packages", It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewImageUrl);
        _mockPackages.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new Exception("Database error"));

        using var stream = CreateStream();
        await Assert.ThrowsAsync<Exception>(() => _service.UploadImageAsync(packageId, stream, "photo.png", "image/png", 16));

        Assert.Equal(OldImageUrl, package.ImageUrl);
        _mockFileStorage.Verify(s => s.DeleteImageIfManagedAsync(NewImageUrl, It.IsAny<CancellationToken>()), Times.Once);
        _mockFileStorage.Verify(s => s.DeleteImageIfManagedAsync(OldImageUrl, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadImageAsync_SaveChangesThrows_NewFileDeleteFailure_StillRethrowsOriginal()
    {
        var packageId = Guid.NewGuid();
        var package = CreatePackage(packageId, OldImageUrl);

        _mockPackages.Setup(r => r.GetByIdAsync(packageId)).ReturnsAsync(package);
        _mockFileStorage.Setup(s => s.SaveImageAsync("packages", It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewImageUrl);
        _mockPackages.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new Exception("Database error"));
        _mockFileStorage.Setup(s => s.DeleteImageIfManagedAsync(NewImageUrl, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk error"));

        using var stream = CreateStream();
        var ex = await Assert.ThrowsAsync<Exception>(() => _service.UploadImageAsync(packageId, stream, "photo.png", "image/png", 16));

        Assert.Equal("Database error", ex.Message);
        Assert.Equal(OldImageUrl, package.ImageUrl);
    }

    [Fact]
    public async Task UploadImageAsync_PackageNotFound_Throws404_WithoutSavingFile()
    {
        var packageId = Guid.NewGuid();
        _mockPackages.Setup(r => r.GetByIdAsync(packageId)).ReturnsAsync((Package?)null);

        using var stream = CreateStream();
        var ex = await Assert.ThrowsAsync<AppException>(() => _service.UploadImageAsync(packageId, stream, "photo.png", "image/png", 16));

        Assert.Equal(404, ex.StatusCode);
        _mockFileStorage.Verify(s => s.SaveImageAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteImageAsync_NullsUrlThenBestEffortDeletesFile()
    {
        var packageId = Guid.NewGuid();
        var package = CreatePackage(packageId, OldImageUrl);
        var callOrder = new List<string>();

        _mockPackages.Setup(r => r.GetByIdAsync(packageId)).ReturnsAsync(package);
        _mockPackages.Setup(r => r.SaveChangesAsync())
            .Callback(() => callOrder.Add("save-db"))
            .ReturnsAsync(1);
        _mockFileStorage.Setup(s => s.DeleteImageIfManagedAsync(OldImageUrl, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("delete-file"))
            .Returns(Task.CompletedTask);

        var result = await _service.DeleteImageAsync(packageId);

        Assert.Null(package.ImageUrl);
        _mockPackages.Verify(r => r.Update(package), Times.Once);
        _mockFileStorage.Verify(s => s.DeleteImageIfManagedAsync(OldImageUrl, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(new List<string> { "save-db", "delete-file" }, callOrder);
    }

    [Fact]
    public async Task DeleteImageAsync_FileDeleteFailure_DoesNotThrow()
    {
        var packageId = Guid.NewGuid();
        var package = CreatePackage(packageId, OldImageUrl);

        _mockPackages.Setup(r => r.GetByIdAsync(packageId)).ReturnsAsync(package);
        _mockFileStorage.Setup(s => s.DeleteImageIfManagedAsync(OldImageUrl, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk error"));

        var result = await _service.DeleteImageAsync(packageId);

        Assert.True(result.Success);
        Assert.Null(package.ImageUrl);
        _mockPackages.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteImageAsync_PackageNotFound_Throws404()
    {
        var packageId = Guid.NewGuid();
        _mockPackages.Setup(r => r.GetByIdAsync(packageId)).ReturnsAsync((Package?)null);

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.DeleteImageAsync(packageId));

        Assert.Equal(404, ex.StatusCode);
    }
}

namespace ApplicationLayer.DTOs.Responses;

public class ManagedUserResponse
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BoothOwnerAccountDetailResponse
{
    public ManagedUserResponse Account { get; set; } = new();
    public List<AdminOwnedBoothResponse> OwnedBooths { get; set; } = [];
}

public class AdminOwnedBoothResponse
{
    public Guid Id { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public string? BoothCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string? NightMarketName { get; set; }
    public string? ZoneName { get; set; }
    public string? SlotNumber { get; set; }
    public decimal? MapPositionX { get; set; }
    public decimal? MapPositionY { get; set; }
    public string? ActivePackageName { get; set; }
    public DateTime? PackageExpiryDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<BoothDocumentResponse> Documents { get; set; } = [];
}

public class BoothRegistrationResponse
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public Guid RequestedNightMarketId { get; set; }
    public Guid? PreferredZoneId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RejectReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<BoothDocumentResponse> Documents { get; set; } = [];
    public Guid? BoothId { get; set; }
}

public class BoothDocumentResponse
{
    public Guid Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

namespace InfrastructureLayer.Cores.Notifications;

public class FirebaseSettings
{
    public const string SectionName = "Firebase";
    public bool Enabled { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string? CredentialsPath { get; set; }
    public string? CredentialsJson { get; set; }
}

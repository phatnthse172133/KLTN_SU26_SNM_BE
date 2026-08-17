using InfrastructureLayer.Cores.External;
using Microsoft.Extensions.Configuration;

namespace TestingLayer;

public class GoogleTokenValidatorTests
{
    [Fact]
    public async Task ValidateAsync_MissingClientIds_Throws()
    {
        var validator = new GoogleTokenValidator(new ConfigurationBuilder().Build());

        await Assert.ThrowsAsync<InvalidOperationException>(() => validator.ValidateAsync("id-token"));
    }

    [Fact]
    public async Task ValidateAsync_InvalidJwt_ReturnsNull()
    {
        var validator = new GoogleTokenValidator(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Google:WebClientId"] = "web-client.apps.googleusercontent.com",
                ["Google:AndroidClientId"] = "android-client.apps.googleusercontent.com",
                ["Google:IosClientId"] = "ios-client.apps.googleusercontent.com"
            })
            .Build());

        Assert.Null(await validator.ValidateAsync("not-a-jwt"));
        Assert.Null(await validator.ValidateAsync("expired-token"));
        Assert.Null(await validator.ValidateAsync("wrong-audience-token"));
        Assert.Null(await validator.ValidateAsync("wrong-issuer-token"));
        Assert.Null(await validator.ValidateAsync(string.Empty));
    }

    [Fact]
    public void ReadAllowedClientIds_CollectsWebAndroidIosAndLegacyKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Google:ClientId"] = "legacy-web.apps.googleusercontent.com",
                ["Google:WebClientId"] = "web.apps.googleusercontent.com",
                ["Google:AndroidClientId"] = "android.apps.googleusercontent.com",
                ["Google:IosClientId"] = "ios.apps.googleusercontent.com",
                ["Google:AllowedClientIds:0"] = "extra.apps.googleusercontent.com",
                ["Google:AllowedClientIds:1"] = "web.apps.googleusercontent.com"
            })
            .Build();

        var ids = GoogleTokenValidator.ReadAllowedClientIds(configuration);

        Assert.Equal(5, ids.Length);
        Assert.Contains("legacy-web.apps.googleusercontent.com", ids);
        Assert.Contains("web.apps.googleusercontent.com", ids);
        Assert.Contains("android.apps.googleusercontent.com", ids);
        Assert.Contains("ios.apps.googleusercontent.com", ids);
        Assert.Contains("extra.apps.googleusercontent.com", ids);
    }
}

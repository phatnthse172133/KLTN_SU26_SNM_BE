using InfrastructureLayer.Data;

namespace TestingLayer;

public class SNMContextFactoryTests
{
    [Fact]
    public void LoadDotEnv_DoesNotOverrideExplicitEnvironmentValue()
    {
        var key = $"SNM_TEST_DOTENV_{Guid.NewGuid():N}";
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.env");
        try
        {
            File.WriteAllText(path, $"{key}=dotenv-value");
            Environment.SetEnvironmentVariable(key, "explicit-value");

            SNMContextFactory.LoadDotEnv(path);

            Assert.Equal("explicit-value", Environment.GetEnvironmentVariable(key));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadDotEnv_FillsMissingEnvironmentValue()
    {
        var key = $"SNM_TEST_DOTENV_{Guid.NewGuid():N}";
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.env");
        try
        {
            File.WriteAllText(path, $"{key}=dotenv-value");
            Environment.SetEnvironmentVariable(key, null);

            SNMContextFactory.LoadDotEnv(path);

            Assert.Equal("dotenv-value", Environment.GetEnvironmentVariable(key));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
            File.Delete(path);
        }
    }

}

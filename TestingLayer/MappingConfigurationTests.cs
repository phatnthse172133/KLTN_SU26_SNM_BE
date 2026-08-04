using ApplicationLayer.Mappings;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;

namespace TestingLayer;

public class MappingConfigurationTests
{
    [Fact]
    public void ApplicationMappings_AreValid()
    {
        var configuration = new MapperConfiguration(
            expression => expression.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance);

        configuration.AssertConfigurationIsValid();
    }
}

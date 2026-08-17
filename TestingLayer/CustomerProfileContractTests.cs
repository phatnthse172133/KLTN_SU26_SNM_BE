using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;

namespace TestingLayer;

public sealed class CustomerProfileContractTests
{
    private static readonly string[] PrivateProfileProperties =
    {
        nameof(UserResponse.Email),
        nameof(UserResponse.Phone),
        nameof(UserResponse.Address),
        nameof(UserResponse.DoB)
    };

    [Fact]
    public void OwnProfileResponse_ContainsExpectedPersonalFields()
    {
        var properties = PropertyNames(typeof(UserResponse));

        Assert.Contains(nameof(UserResponse.FullName), properties);
        Assert.Contains(nameof(UserResponse.AvatarUrl), properties);
        Assert.All(PrivateProfileProperties, property => Assert.Contains(property, properties));
    }

    [Fact]
    public void ChatCustomerResponse_ContainsOnlyPublicIdentityFields()
    {
        var properties = PropertyNames(typeof(ConversationUserResponse));

        Assert.Equal(
            new[]
            {
                nameof(ConversationUserResponse.AvatarUrl),
                nameof(ConversationUserResponse.FullName),
                nameof(ConversationUserResponse.UserId)
            }.OrderBy(value => value),
            properties.OrderBy(value => value));
    }

    [Theory]
    [InlineData(typeof(ReviewResponse))]
    [InlineData(typeof(CustomerReviewResponse))]
    public void PublicReviewResponses_DoNotContainPrivateProfileFields(Type responseType)
    {
        var properties = PropertyNames(responseType);

        Assert.DoesNotContain(nameof(UserResponse.DoB), properties);
        Assert.DoesNotContain(nameof(UserResponse.Email), properties);
        Assert.DoesNotContain(nameof(UserResponse.Phone), properties);
        Assert.DoesNotContain(nameof(UserResponse.Address), properties);
    }

    [Fact]
    public void DoB_IsAbsentFromNotificationAndPublicResponseContracts()
    {
        var applicationAssembly = typeof(UserResponse).Assembly;
        var protectedTypes = applicationAssembly.GetTypes()
            .Where(type =>
                type.IsPublic &&
                (type.Namespace == "ApplicationLayer.DTOs.Admin" ||
                 type.Name.Contains("Notification", StringComparison.Ordinal) ||
                 type == typeof(ConversationUserResponse) ||
                 type == typeof(ReviewResponse) ||
                 type == typeof(CustomerReviewResponse)));

        Assert.All(
            protectedTypes,
            type => Assert.DoesNotContain(
                type.GetProperties(),
                property => property.Name == nameof(UserResponse.DoB)));
    }

    [Fact]
    public void UserResponse_IsNotNestedInsidePublicDtoContracts()
    {
        var consumers = typeof(UserResponse).Assembly.GetTypes()
            .Where(type => type.IsPublic)
            .Where(type => type.GetProperties().Any(property => property.PropertyType == typeof(UserResponse)))
            .ToArray();

        Assert.Equal(new[] { typeof(AuthResponse) }, consumers);
    }

    private static string[] PropertyNames(Type type)
        => type.GetProperties().Select(property => property.Name).ToArray();
}

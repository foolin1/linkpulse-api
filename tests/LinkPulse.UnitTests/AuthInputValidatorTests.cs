using LinkPulse.Api.Features.Auth;

namespace LinkPulse.UnitTests;

public sealed class AuthInputValidatorTests
{
    [Fact]
    public void ValidateRegister_ShouldAcceptValidCredentials()
    {
        var request = new RegisterRequest(
            "owner@example.com",
            "LinkPulse123");

        var errors =
            AuthInputValidator.ValidateRegister(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRegister_ShouldRejectInvalidEmail()
    {
        var request = new RegisterRequest(
            "invalid-email",
            "LinkPulse123");

        var errors =
            AuthInputValidator.ValidateRegister(request);

        Assert.Contains("email", errors.Keys);
    }

    [Fact]
    public void ValidateRegister_ShouldRejectWeakPassword()
    {
        var request = new RegisterRequest(
            "owner@example.com",
            "password");

        var errors =
            AuthInputValidator.ValidateRegister(request);

        Assert.Contains("password", errors.Keys);
        Assert.True(errors["password"].Length >= 2);
    }

    [Fact]
    public void NormalizeEmail_ShouldTrimAndUseUppercase()
    {
        var normalizedEmail =
            AuthInputValidator.NormalizeEmail(
                "  Owner@Example.com ");

        Assert.Equal(
            "OWNER@EXAMPLE.COM",
            normalizedEmail);
    }
}
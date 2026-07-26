using System.Net.Mail;
using LinkPulse.Api.Data;

namespace LinkPulse.Api.Features.Auth;

public static class AuthInputValidator
{
    public const int PasswordMinLength = 8;

    public const int PasswordMaxLength = 128;

    public static Dictionary<string, string[]> ValidateRegister(
        RegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = ValidateCredentials(
            request.Email,
            request.Password);

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return errors;
        }

        var passwordErrors = new List<string>();
        var password = request.Password;

        if (password.Length < PasswordMinLength)
        {
            passwordErrors.Add(
                $"Password must contain at least {PasswordMinLength} characters.");
        }

        if (password.Length > PasswordMaxLength)
        {
            passwordErrors.Add(
                $"Password cannot exceed {PasswordMaxLength} characters.");
        }

        if (!password.Any(char.IsUpper))
        {
            passwordErrors.Add(
                "Password must contain an uppercase letter.");
        }

        if (!password.Any(char.IsLower))
        {
            passwordErrors.Add(
                "Password must contain a lowercase letter.");
        }

        if (!password.Any(char.IsDigit))
        {
            passwordErrors.Add(
                "Password must contain a digit.");
        }

        if (passwordErrors.Count > 0)
        {
            errors["password"] = passwordErrors.ToArray();
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateLogin(
        LoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ValidateCredentials(
            request.Email,
            request.Password);
    }

    public static string NormalizeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return email.Trim().ToUpperInvariant();
    }

    private static Dictionary<string, string[]> ValidateCredentials(
        string? email,
        string? password)
    {
        var errors = new Dictionary<string, string[]>(
            StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(email))
        {
            errors["email"] =
            [
                "Email is required."
            ];
        }
        else if (email.Length > EntityConstraints.EmailMaxLength)
        {
            errors["email"] =
            [
                $"Email cannot exceed {EntityConstraints.EmailMaxLength} characters."
            ];
        }
        else if (!IsValidEmail(email))
        {
            errors["email"] =
            [
                "Email has an invalid format."
            ];
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            errors["password"] =
            [
                "Password is required."
            ];
        }

        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var trimmedEmail = email.Trim();
            var mailAddress = new MailAddress(trimmedEmail);

            return string.Equals(
                mailAddress.Address,
                trimmedEmail,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
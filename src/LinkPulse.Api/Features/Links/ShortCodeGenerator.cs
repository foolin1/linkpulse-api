using System.Security.Cryptography;

namespace LinkPulse.Api.Features.Links;

public interface IShortCodeGenerator
{
    string Generate(int length = 8);
}

public sealed class ShortCodeGenerator
    : IShortCodeGenerator
{
    private const string Alphabet =
        "23456789abcdefghjkmnpqrstuvwxyz";

    public string Generate(int length = 8)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                "Short code length must be positive.");
        }

        return string.Create(
            length,
            Alphabet,
            static (buffer, alphabet) =>
            {
                for (var index = 0;
                     index < buffer.Length;
                     index++)
                {
                    var randomIndex =
                        RandomNumberGenerator.GetInt32(
                            alphabet.Length);

                    buffer[index] = alphabet[randomIndex];
                }
            });
    }
}
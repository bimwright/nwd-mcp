using System;
using System.Security.Cryptography;

namespace Bimwright.Nwd.Server;

public static class AuthToken
{
    public static string Generate(int bytes = 32)
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();
}

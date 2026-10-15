namespace LarpStrap.Core.Updates;

/// <summary>
/// Public half of LarpStrap's update-signing key (ECDSA P-256, SubjectPublicKeyInfo; docs/adr/0009). The private half
/// lives only on the owner's PC, outside the repository (eng/New-UpdateSigningKey.ps1).
/// </summary>
public static class UpdateTrust
{
    public const string PublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE3lqecxzoS5YfetSTVuo1L4D4r4V9pQivK//GHhdLtVCchP7zxyaUM123NNTZJk1eWEx6LJEoNcKACDZkVYSlvw==";

    public static byte[] PublicKey => Convert.FromBase64String(PublicKeyBase64);
}

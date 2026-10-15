using System.Security.Cryptography;
using System.Text;
using SplitProxy.Core.Models;
using SplitProxy.Core.Security;

namespace SplitProxy.Core.Devices;

public sealed class DeviceIdentityManager(SessionStore store)
{
    public DeviceIdentity GetOrCreate()
    {
        var current = store.LoadIdentity();
        if (current is not null) return current;

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privatePkcs8 = key.ExportPkcs8PrivateKey();
        var publicSpki = key.ExportSubjectPublicKeyInfo();
        var identity = new DeviceIdentity(
            Guid.NewGuid().ToString(),
            Pem("PUBLIC KEY", publicSpki),
            Convert.ToBase64String(ProtectPrivateKey(privatePkcs8)),
            DateTimeOffset.UtcNow);
        CryptographicOperations.ZeroMemory(privatePkcs8);
        store.SaveIdentity(identity);
        return identity;
    }

    public string SignActivation(DeviceIdentity identity, string responseCode)
    {
        var normalized = responseCode.Replace("ACT-", "", StringComparison.OrdinalIgnoreCase)
            .Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
        var payload = $"SP-ACTIVATE-V1\n{identity.InstallId}\n{normalized}";
        var protectedKey = Convert.FromBase64String(identity.ProtectedPrivateKeyBase64);
        var keyBytes = UnprotectPrivateKey(protectedKey);
        try
        {
            using var key = ECDsa.Create();
            key.ImportPkcs8PrivateKey(keyBytes, out _);
            var sig = key.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            if (sig.Length != 64) throw new CryptographicException("ECDSA signature is not P1363/64 bytes.");
            return Convert.ToBase64String(sig);
        }
        finally { CryptographicOperations.ZeroMemory(keyBytes); }
    }

    private static byte[] ProtectPrivateKey(byte[] plain) => ProtectedData.Protect(plain,
        Encoding.UTF8.GetBytes("SplitProxy-ECDSA-P256"), DataProtectionScope.CurrentUser);
    private static byte[] UnprotectPrivateKey(byte[] enc) => ProtectedData.Unprotect(enc,
        Encoding.UTF8.GetBytes("SplitProxy-ECDSA-P256"), DataProtectionScope.CurrentUser);

    private static string Pem(string label, byte[] data)
    {
        var b64 = Convert.ToBase64String(data, Base64FormattingOptions.InsertLineBreaks);
        return $"-----BEGIN {label}-----\n{b64}\n-----END {label}-----";
    }
}

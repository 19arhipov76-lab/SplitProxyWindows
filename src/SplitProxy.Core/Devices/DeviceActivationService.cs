using SplitProxy.Core.Api;
using SplitProxy.Core.Models;
using SplitProxy.Core.Security;

namespace SplitProxy.Core.Devices;

public sealed class DeviceActivationService(ServerGateway gateway, SessionStore store, DeviceIdentityManager identities)
{
    public async Task<DeviceCredentials> EnsureActivatedAsync(AccountSession session, CancellationToken ct = default)
    {
        var existing = store.LoadDeviceCredentials();
        if (existing is not null)
        {
            if (!existing.AccountEmail.Equals(session.Email, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Это устройство уже связано с другой учётной записью.");
            if (!await gateway.AccountOwnsActiveInstallAsync(existing.InstallId, session.Token, ct))
                throw new InvalidOperationException("Эта установка Windows отозвана, неактивна или принадлежит другой учётной записи.");
            return existing;
        }

        var identity = identities.GetOrCreate();
        var name = Environment.MachineName;
        var (requestCode, installId) = await gateway.RequestActivationAsync(identity, name, ct);
        var responseCode = await gateway.ApproveActivationAsync(requestCode, session.Token, ct);
        var signature = identities.SignActivation(identity, responseCode);
        var credentials = await gateway.FinishActivationAsync(installId, responseCode, signature, session.Email, ct);
        store.SaveDeviceCredentials(credentials);
        return credentials;
    }
}

using SplitProxy.Core.Models;

namespace SplitProxy.Core.Security;

public sealed class SessionStore
{
    private readonly ProtectedJsonStore _store = new();
    private static string SessionPath => Path.Combine(AppConstants.UserDataDirectory, "account.dat");
    private static string IdentityPath => Path.Combine(AppConstants.UserDataDirectory, "identity.dat");
    private static string DevicePath => Path.Combine(AppConstants.UserDataDirectory, "device.dat");

    public AccountSession? LoadSession() => _store.Load<AccountSession>(SessionPath);
    public void SaveSession(AccountSession session) => _store.Save(SessionPath, session);
    public void ClearSession() => _store.Delete(SessionPath);

    public DeviceIdentity? LoadIdentity() => _store.Load<DeviceIdentity>(IdentityPath);
    public void SaveIdentity(DeviceIdentity identity) => _store.Save(IdentityPath, identity);

    public DeviceCredentials? LoadDeviceCredentials() => _store.Load<DeviceCredentials>(DevicePath);
    public void SaveDeviceCredentials(DeviceCredentials credentials) => _store.Save(DevicePath, credentials);
    public void ClearDeviceCredentials() => _store.Delete(DevicePath);
}

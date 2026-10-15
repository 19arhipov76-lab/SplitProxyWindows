namespace SplitProxy.Core;

public static class AppConstants
{
    public const string ApiBaseUrl = "https://split-proxy.ru/api/v1/";
    public const string Platform = "windows";
    public const string DefaultCountry = "NL";
    public const string UserAgentProduct = "SplitProxyWindows";
    public const string ServiceName = "SplitProxyService";

    public static string UserDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SplitProxy");

    public static string MachineDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SplitProxy");

    public static string AppStatePath => Path.Combine(MachineDataDirectory, "client-state.json");
    public static string ServiceStatusPath => Path.Combine(MachineDataDirectory, "service-status.json");
}

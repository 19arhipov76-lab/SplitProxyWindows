using System.Diagnostics;

static string? Arg(string[] args, string name)
{
    var i = Array.FindIndex(args, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

var pidText = Arg(args, "--pid");
var installer = Arg(args, "--installer");
var app = Arg(args, "--app");
if (!int.TryParse(pidText, out var pid) || string.IsNullOrWhiteSpace(installer) || !File.Exists(installer) || string.IsNullOrWhiteSpace(app))
    return;

try
{
    var parent = Process.GetProcessById(pid);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    await parent.WaitForExitAsync(timeout.Token);
}
catch { }

try
{
    var psi = new ProcessStartInfo
    {
        FileName = installer,
        Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
        UseShellExecute = true,
        Verb = "runas"
    };
    using var setup = Process.Start(psi);
    if (setup is null) return;
    await setup.WaitForExitAsync();
    if (setup.ExitCode != 0) return;
}
catch { return; }

try
{
    await Task.Delay(800);
    Process.Start(new ProcessStartInfo { FileName = app, UseShellExecute = true });
}
catch { }

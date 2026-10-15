using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SplitProxy.Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "Split Proxy Service");
builder.Services.AddSingleton<RouteEngineManager>();
builder.Services.AddHostedService<Worker>();
await builder.Build().RunAsync();

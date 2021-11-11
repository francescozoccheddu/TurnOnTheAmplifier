using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(_options => _options.ServiceName = "TurnOnTheAmplifier")
    .ConfigureServices(_services => _services.AddHostedService<TurnOnTheAmplifier.Service>())
    .Build();

await host.RunAsync();
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "TurnOnTheAmplifier";
    })
    .ConfigureServices(services =>
    {
        services.AddHostedService<TurnOnTheAmplifier.Service>();
    })
    .Build();

await host.RunAsync();
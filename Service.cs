using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TurnOnTheAmplifier
{
    public sealed class Service : IHostedService
    {

        private const string c_configurationJsonFile = "configuration.json";

        private readonly ILogger<Service> m_logger;
        private Controller? m_controller;

        public Service(ILogger<Service> _logger)
        {
            m_logger = _logger;
        }

        public Task StartAsync(CancellationToken _cancellationToken)
        {
            m_logger.LogInformation($"{nameof(Service)} starting.");
            string actualConfigurationJsonFile = Path.GetFullPath(c_configurationJsonFile, AppContext.BaseDirectory);
            m_logger.LogInformation($"{nameof(Service)} is loading configuration file '{actualConfigurationJsonFile}'.");
            Configuration config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(actualConfigurationJsonFile))!;
            m_logger.LogInformation($"{nameof(Service)} loaded configuration '{config with { TapoPassword = "<private>" }}'.");
            m_controller = new(m_logger, config);
            m_logger.LogInformation($"{nameof(Service)} started.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken _cancellationToken)
        {
            m_logger.LogInformation($"{nameof(Service)} is stopping.");
            m_controller?.Dispose();
            m_controller = null;
            m_logger.LogInformation($"{nameof(Service)} stopped.");
            return Task.CompletedTask;
        }

    }
}
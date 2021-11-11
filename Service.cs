using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Threading;
using System.Threading.Tasks;

namespace TurnOnTheAmplifier
{
    internal sealed class Service : IHostedService
    {

        private readonly ILogger<Service> m_logger;
        private Controller? m_controller;

        public Service(ILogger<Service> logger)
        {
            m_logger = logger;
        }

        public Task StartAsync(CancellationToken _cancellationToken)
        {
            m_logger.LogInformation($"{nameof(TurnOnTheAmplifier)} is starting.");
            m_controller = new("Speakers", "C:/");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken _cancellationToken)
        {
            m_logger.LogInformation($"{nameof(TurnOnTheAmplifier)} is stopping.");
            m_controller?.Dispose();
            m_controller = null;
            return Task.CompletedTask;
        }

    }
}
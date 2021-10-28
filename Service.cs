using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TurnOnTheAmplifier
{
    internal sealed class Service : IHostedService, IAsyncDisposable
    {

        private static readonly Regex s_deviceRegex = new(@"^Headphones \([^\)]*\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        const int c_cooldownTime = 10;

        private readonly ILogger<Service> m_logger;
        private readonly object m_lock;
        private Timer? m_timer;
        private bool m_localOn;
        private bool? m_remoteOn;
        private bool m_waiting;
        private bool m_stopped;
        private DefaultOutputAudioDeviceListener? m_listener;

        public Service(ILogger<Service> logger)
        {
            m_logger = logger;
            m_lock = new();
            m_waiting = false;
            m_remoteOn = null;
            m_localOn = false;
            m_stopped = false;
        }

        public Task StartAsync(CancellationToken _cancellationToken)
        {
            m_logger.LogInformation($"{nameof(TurnOnTheAmplifier)} is starting.");
            lock (m_lock)
            {
                m_timer = new(TimerTicked, null, Timeout.Infinite, Timeout.Infinite);
                m_listener = new();
                m_localOn = false;
                m_remoteOn = null;
                m_waiting = false;
                m_stopped = false;
            }
            DeviceChanged(m_listener.Current);
            m_listener.OnChanged += DeviceChanged;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken _cancellationToken)
        {
            m_logger.LogInformation($"{nameof(TurnOnTheAmplifier)} is stopping.");
            lock (m_lock)
            {
                m_timer?.Change(Timeout.Infinite, Timeout.Infinite);
                if (m_listener is not null)
                {
                    m_listener.OnChanged -= DeviceChanged;
                }
                m_localOn = false;
                m_remoteOn = null;
                m_waiting = false;
                m_stopped = true;
                Send();
            }
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            m_listener?.Dispose();
            m_listener = null;
            if (m_timer is IAsyncDisposable timer)
            {
                await timer.DisposeAsync();
            }
            m_timer = null;
        }

        private void DeviceChanged(string? _device)
        {
            lock (m_lock)
            {
                if (!m_stopped)
                {
                    m_localOn = _device is not null && s_deviceRegex.IsMatch(_device);
                    if (!m_waiting)
                    {
                        m_waiting = true;
                        m_timer?.Change(c_cooldownTime * 1000, Timeout.Infinite);
                        if (m_localOn != m_remoteOn)
                        {
                            m_remoteOn = m_localOn;
                            Send();
                        }
                    }
                }
            }
        }

        private void TimerTicked(object? _state)
        {
            lock (m_lock)
            {
                if (!m_stopped)
                {
                    m_waiting = false;
                    if (m_localOn != m_remoteOn)
                    {
                        m_remoteOn = m_localOn;
                        Send();
                    }
                }
            }
        }

        private void Send()
        {
            m_logger.LogInformation($"{nameof(TurnOnTheAmplifier)} turned {(m_localOn ? "on" : "off")}.");
            Process.Start("C:/Users/franc/AppData/Local/Programs/Python/Python310/python.exe", $"\"c:/users/franc/amplifier.py\" {(m_localOn ? "on" : "off")}");
        }

    }
}
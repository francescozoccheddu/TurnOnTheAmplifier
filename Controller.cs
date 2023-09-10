using Microsoft.Extensions.Logging;

using NAudio.CoreAudioApi;

using System;
using System.Net;
using System.Threading;

namespace TurnOnTheAmplifier
{
    public sealed partial class Controller : IDisposable
    {

        private const int c_pythonMajorVerion = 3;

        private readonly DefaultOutputAudioDeviceListener m_listener;
        private readonly Timer m_timer;
        private readonly object m_lock;
        private readonly ILogger? m_logger;
        private readonly TapoPlug m_plug;

        private bool m_localOn;
        private bool? m_remoteOn;
        private bool m_waiting;
        private bool m_disposed;

        public Configuration Config { get; }

        public Controller(Configuration _config)
            : this(null, _config)
        { }

        public Controller(ILogger? _logger, Configuration _config)
        {
            _config.Validate();
            Config = _config;
            m_plug = new(IPAddress.Parse(_config.TapoDeviceIpAddress), Config.TapoUsername, Config.TapoPassword, Config.TimeoutTime);
            m_lock = new();
            m_logger = _logger;
            m_waiting = false;
            m_remoteOn = null;
            m_localOn = false;
            m_disposed = false;
            m_timer = new(TimerTicked, null, Timeout.Infinite, Timeout.Infinite);
            m_listener = new();
            m_listener.OnChanged += DeviceChanged;
            DeviceChanged(m_listener.Current);
            m_logger?.LogInformation($"{nameof(Controller)} created.");
        }

        public void Dispose()
        {
            bool wasDisposed;
            lock (m_lock)
            {
                m_logger?.LogInformation($"{nameof(Controller)} is stopping.");
                _ = m_timer.Change(Timeout.Infinite, Timeout.Infinite);
                m_localOn = false;
                m_remoteOn = null;
                m_waiting = false;
                wasDisposed = m_disposed;
                m_disposed = true;
                UpdateRemote();
                m_logger?.LogInformation($"{nameof(Controller)} stopped.");
            }
            if (!wasDisposed)
            {
                m_logger?.LogInformation($"{nameof(Controller)} is being disposed.");
                m_timer.Dispose();
                m_listener.Dispose();
                m_logger?.LogInformation($"{nameof(Controller)} disposed.");
            }
        }

        private bool IsTargetDevice(MMDevice _device)
        {
            return _device.FriendlyName == $"{Config.AudioOutputFriendlyName} ({_device.DeviceFriendlyName})";
        }

        private void DeviceChanged(MMDevice? _device)
        {
            lock (m_lock)
            {
                if (_device is not null)
                {
                    m_logger?.LogInformation($"{nameof(Controller)} detected a new default output device '{_device.FriendlyName}'.");
                }
                if (!m_disposed)
                {
                    m_localOn = _device is not null && IsTargetDevice(_device);
                    if (m_localOn)
                    {
                        m_logger?.LogInformation($"{nameof(Controller)} detected the target default output device.");
                        AudioEndpointVolume volume = _device!.AudioEndpointVolume;
                        for (int c = 0; c < volume.Channels.Count; c++)
                        {
                            volume.Channels[c].VolumeLevelScalar = volume.MasterVolumeLevelScalar;
                        }
                        if (volume.MasterVolumeLevelScalar > Config.MaxVolume)
                        {
                            volume.MasterVolumeLevelScalar = Config.MaxVolume;
                            m_logger?.LogInformation($"{nameof(Controller)} turned down the volume.");
                        }
                    }
                    if (!m_waiting)
                    {
                        m_waiting = true;
                        _ = m_timer.Change(Config.CooldownTime * 1000, Timeout.Infinite);
                        UpdateRemote();
                    }
                    else
                    {
                        m_logger?.LogInformation($"{nameof(Controller)} delayed script execution due to cooldown.");
                    }
                }
            }
        }

        private void TimerTicked(object? _state)
        {
            lock (m_lock)
            {
                if (!m_disposed)
                {
                    m_waiting = false;
                    UpdateRemote();
                }
            }
        }

        private void UpdateRemote()
        {
            if (!Monitor.IsEntered(m_lock))
            {
                throw new SynchronizationLockException("Caller does not own lock");
            }
            if (m_remoteOn != m_localOn)
            {
                m_remoteOn = null;
                m_logger?.LogInformation($"{nameof(Controller)} is turning {(m_localOn ? "on" : "off")} the amplifier.");
                bool done = false;
                try
                {
                    m_plug.SetState(m_localOn);
                    m_remoteOn = m_localOn;
                    done = true;
                }
                catch (TapoPlug.LoginException)
                {
                    m_logger?.LogError($"{nameof(Controller)} failed to login to device.");

                }
                catch (TapoPlug.StateChangeException)
                {
                    m_logger?.LogError($"{nameof(Controller)} failed to change the device state.");
                }
                if (done)
                {
                    m_logger?.LogInformation($"{nameof(Controller)} successfully turned {(m_localOn ? "on" : "off")} the amplifier.");
                }
            }
        }

    }
}
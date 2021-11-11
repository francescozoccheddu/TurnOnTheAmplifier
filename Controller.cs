
using NAudio.CoreAudioApi;

using System;
using System.IO;
using System.Threading;

namespace TurnOnTheAmplifier
{
    internal sealed class Controller : IDisposable
    {

        private readonly DefaultOutputAudioDeviceListener m_listener;
        private readonly Timer m_timer;
        private readonly object m_lock;

        private bool m_localOn;
        private bool? m_remoteOn;
        private bool m_waiting;
        private bool m_disposed;

        public string OutputFriendlyName { get; }
        public float CooldownTime { get; }
        public float MaxVolume { get; }
        public string PythonScriptFile { get; }

        public Controller(string _outputFriendlyName, string _pythonScriptFile, float _cooldownTime = 5, float _maxVolume = 0.25f)
        {
            if (_maxVolume is < 0 or > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(_maxVolume), _maxVolume, "Not in [0,1]");
            }
            if (_cooldownTime is < 0 or > 60)
            {
                throw new ArgumentOutOfRangeException(nameof(_cooldownTime), _cooldownTime, "Not in [0,60]");
            }
            _ = Path.GetFullPath(_pythonScriptFile);
            m_lock = new();
            m_waiting = false;
            m_remoteOn = null;
            m_localOn = false;
            m_disposed = false;
            m_timer = new(TimerTicked, null, Timeout.Infinite, Timeout.Infinite);
            m_listener = new();
            m_listener.OnChanged += DeviceChanged;
            CooldownTime = _cooldownTime;
            OutputFriendlyName = _outputFriendlyName;
            MaxVolume = _maxVolume;
            PythonScriptFile = _pythonScriptFile;
        }

        public void Dispose()
        {
            bool wasDisposed;
            lock (m_lock)
            {
                _ = m_timer.Change(Timeout.Infinite, Timeout.Infinite);
                m_localOn = false;
                m_remoteOn = null;
                m_waiting = false;
                wasDisposed = m_disposed;
                m_disposed = true;
                UpdateRemote();
            }
            if (!wasDisposed)
            {
                m_timer.Dispose();
                m_listener.Dispose();
            }
        }

        private bool IsTargetDevice(MMDevice _device)
        {
            return _device.FriendlyName == $"{OutputFriendlyName} ({_device.DeviceFriendlyName})";
        }

        private void DeviceChanged(MMDevice? _device)
        {
            lock (m_lock)
            {
                if (!m_disposed)
                {
                    m_localOn = _device is not null && IsTargetDevice(_device);
                    if (m_localOn)
                    {
                        AudioEndpointVolume volume = _device!.AudioEndpointVolume;
                        for (int c = 0; c < volume.Channels.Count; c++)
                        {
                            volume.Channels[c].VolumeLevelScalar = volume.MasterVolumeLevelScalar;
                        }
                        if (volume.MasterVolumeLevelScalar > MaxVolume)
                        {
                            volume.MasterVolumeLevelScalar = MaxVolume;
                        }
                    }
                    if (!m_waiting)
                    {
                        m_waiting = true;
                        _ = m_timer.Change((int)Math.Round(CooldownTime * 1000), Timeout.Infinite);
                        UpdateRemote();
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
                m_remoteOn = m_localOn;
                //Process.Start("C:/Users/franc/AppData/Local/Programs/Python/Python310/python.exe", $"\"c:/users/franc/amplifier.py\" {(m_localOn ? "on" : "off")}");
            }
        }

    }
}
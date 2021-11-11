
using LostTech.WhichPython;

using Microsoft.Extensions.Logging;

using NAudio.CoreAudioApi;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace TurnOnTheAmplifier
{
    internal sealed class Controller : IDisposable
    {

        private const int c_pythonMajorVerion = 3;

        private readonly DefaultOutputAudioDeviceListener m_listener;
        private readonly Timer m_timer;
        private readonly object m_lock;
        private readonly ILogger? m_logger;

        private bool m_localOn;
        private bool? m_remoteOn;
        private bool m_waiting;
        private bool m_disposed;

        public string OutputFriendlyName { get; }
        public float ScriptCooldownTime { get; }
        public float ScriptTimeoutTime { get; }
        public float MaxVolume { get; }
        public string PythonScriptFile { get; }

        public Controller(string _outputFriendlyName, string _pythonScriptFile, float _maxVolume = 0.25f, float _scriptCooldownTime = 5, float _scriptTimeoutTime = 5)
            : this(null, _outputFriendlyName, _pythonScriptFile, _maxVolume, _scriptCooldownTime, _scriptTimeoutTime)
        { }

        public Controller(ILogger? _logger, string _outputFriendlyName, string _pythonScriptFile, float _maxVolume = 0.25f, float _scriptCooldownTime = 5, float _scriptTimeoutTime = 5)
        {
            if (_maxVolume is < 0 or > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(_maxVolume), _maxVolume, "Not in [0,1]");
            }
            if (_scriptCooldownTime is < 0 or > 60)
            {
                throw new ArgumentOutOfRangeException(nameof(_scriptCooldownTime), _scriptCooldownTime, "Not in [0,60]");
            }
            if (_scriptTimeoutTime is <= 0 or > 60)
            {
                throw new ArgumentOutOfRangeException(nameof(_scriptTimeoutTime), _scriptTimeoutTime, "Not in (0,60]");
            }
            _ = Path.GetFullPath(_pythonScriptFile);
            m_lock = new();
            m_logger = _logger;
            m_waiting = false;
            m_remoteOn = null;
            m_localOn = false;
            m_disposed = false;
            m_timer = new(TimerTicked, null, Timeout.Infinite, Timeout.Infinite);
            m_listener = new();
            m_listener.OnChanged += DeviceChanged;
            ScriptCooldownTime = _scriptCooldownTime;
            ScriptCooldownTime = _scriptTimeoutTime;
            OutputFriendlyName = _outputFriendlyName;
            MaxVolume = _maxVolume;
            PythonScriptFile = _pythonScriptFile;
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
            return _device.FriendlyName == $"{OutputFriendlyName} ({_device.DeviceFriendlyName})";
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
                        if (volume.MasterVolumeLevelScalar > MaxVolume)
                        {
                            volume.MasterVolumeLevelScalar = MaxVolume;
                            m_logger?.LogInformation($"{nameof(Controller)} turned down the volume.");
                        }
                    }
                    if (!m_waiting)
                    {
                        m_waiting = true;
                        _ = m_timer.Change((int)Math.Round(ScriptCooldownTime * 1000), Timeout.Infinite);
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
                m_remoteOn = m_localOn;
                m_logger?.LogInformation($"{nameof(Controller)} is turning {(m_localOn ? "on" : "off")} the amplifier.");
                string pythonExecutable = PythonEnvironment
                    .EnumerateEnvironments()
                    .First(_e => _e.LanguageVersion?.Major == c_pythonMajorVerion)
                    .InterpreterPath
                    .FullName;
                m_logger?.LogInformation($"{nameof(Controller)} is using Python {c_pythonMajorVerion} interpreter '{pythonExecutable}'");
                Process process = Process.Start(pythonExecutable, $"\"{PythonScriptFile}\" {(m_localOn ? "on" : "off")}");
                bool exited = process.WaitForExit((int)Math.Round(ScriptCooldownTime * 1000));
                if (exited)
                {
                    m_logger?.LogInformation($"{nameof(Controller)} script exited with code {process.ExitCode}.");
                }
                else
                {
                    m_logger?.LogInformation($"{nameof(Controller)} script execution timed out.");
                }
            }
        }

    }
}
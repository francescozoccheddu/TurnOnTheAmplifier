using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

using System;
using System.Runtime.InteropServices;

namespace TurnOnTheAmplifier
{

    internal sealed class DefaultOutputAudioDeviceListener : DefaultOutputAudioDeviceListener.NotificationClient.IListener, IDisposable
    {

        private sealed class NotificationClient : IMMNotificationClient
        {

            public interface IListener
            {

                void NotifyDefaultOutputDeviceChanged(string _deviceId);

            }

            private readonly IListener m_listener;

            public NotificationClient(IListener _listener) => m_listener = _listener;

            public void OnDefaultDeviceChanged(DataFlow _dataFlow, Role _deviceRole, string _defaultDeviceId)
            {
                if (_dataFlow is DataFlow.Render or DataFlow.All)
                {
                    m_listener.NotifyDefaultOutputDeviceChanged(_defaultDeviceId);
                }
            }

            public void OnDeviceAdded(string _deviceId)
            { }

            public void OnDeviceRemoved(string _deviceId)
            { }

            public void OnDeviceStateChanged(string _deviceId, DeviceState _newState)
            { }

            public void OnPropertyValueChanged(string _deviceId, PropertyKey _propertyKey)
            { }

        }

        public delegate void ChangeEventHandler(string _name);

        private int RegisterEndpointNotificationCallback([In][MarshalAs(UnmanagedType.Interface)] IMMNotificationClient _client)
        {
            return m_deviceEnum.RegisterEndpointNotificationCallback(_client);
        }

        private int UnregisterEndpointNotificationCallback([In][MarshalAs(UnmanagedType.Interface)] IMMNotificationClient _client)
        {
            return m_deviceEnum.UnregisterEndpointNotificationCallback(_client);
        }

        private readonly MMDeviceEnumerator m_deviceEnum;
        private readonly NotificationClient m_notificationClient;
        private bool m_disposed;

        public DefaultOutputAudioDeviceListener()
        {
            if (Environment.OSVersion.Version.Major < 6)
            {
                throw new NotSupportedException("This functionality is only supported on Windows Vista or newer.");
            }
            m_deviceEnum = new MMDeviceEnumerator();
            m_notificationClient = new NotificationClient(this);
            Current = null;
            foreach (Role role in Enum.GetValues<Role>())
            {
                foreach (DataFlow flow in new[] { DataFlow.Render, DataFlow.All })
                {
                    if (m_deviceEnum.HasDefaultAudioEndpoint(flow, role))
                    {
                        UpdateCurrent(m_deviceEnum.GetDefaultAudioEndpoint(flow, role));
                        break;
                    }
                }
            }
            RegisterEndpointNotificationCallback(m_notificationClient);
        }

        public string? Current { get; private set; }
        public event ChangeEventHandler? OnChanged;

        private void UpdateCurrent(MMDevice _device)
        {
            if (_device.FriendlyName != Current)
            {
                Current = _device.FriendlyName;
                OnChanged?.Invoke(Current);
            }
        }

        void NotificationClient.IListener.NotifyDefaultOutputDeviceChanged(string _deviceId)
        {
            UpdateCurrent(m_deviceEnum.GetDevice(_deviceId));
        }

        public void Dispose()
        {
            if (!m_disposed)
            {
                m_disposed = true;
                UnregisterEndpointNotificationCallback(m_notificationClient);
                m_deviceEnum.Dispose();
            }
        }

    }

}
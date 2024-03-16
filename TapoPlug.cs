using System;
using System.Net;

using TapoConnect;


namespace TurnOnTheAmplifier
{
    public sealed class TapoPlug
    {

        public abstract class TapoException : Exception
        {

            protected TapoException(string _message) : base(_message) { }

        }

        public sealed class LoginException : TapoException
        {
            public LoginException() : base("Login failed") { }
        }

        public sealed class StateChangeException : TapoException
        {
            public StateChangeException() : base("State change failed") { }
        }

        public IPAddress Address { get; }
        public string Username { get; }
        public string Password { get; }
        public int Timeout { get; }

        public TapoPlug(IPAddress _address, string _username, string _password, int _timeout = 10)
        {
            Address = _address;
            Username = _username;
            Password = _password;
            if (_timeout < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(_timeout), _timeout, $"{nameof(_timeout)} < 1");
            }
            Timeout = _timeout;
        }

        public async void SetState(bool _on)
        {
            TapoDeviceKey key;
            TapoDeviceClient client;
            try
            {
                client = new();
                key = await client.LoginByIpAsync(Address.ToString(), Username, Password);
            }
            catch
            {
                throw new LoginException();
            }
            try
            {
                await client.SetPowerAsync(key, _on);

            }
            catch
            {
                throw new StateChangeException();
            }
        }

    }
}

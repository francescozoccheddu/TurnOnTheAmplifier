using System;
using System.Net;

namespace TurnOnTheAmplifier
{
    public sealed record Configuration(string AudioOutputFriendlyName, string TapoDeviceIpAddress, string TapoUsername, string TapoPassword, float MaxVolume = 0.25f, int CooldownTime = 5, int TimeoutTime = 10)
    {

        public void Validate()
        {
            if (MaxVolume is < 0 or > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxVolume), MaxVolume, "Not in [0,1]");
            }
            if (CooldownTime is < 0 or > 60)
            {
                throw new ArgumentOutOfRangeException(nameof(CooldownTime), CooldownTime, "Not in [0,60]");
            }
            if (TimeoutTime is < 1 or > 60)
            {
                throw new ArgumentOutOfRangeException(nameof(TimeoutTime), TimeoutTime, "Not in [1,60]");
            }
            if (!IPAddress.TryParse(TapoDeviceIpAddress, out _))
            {
                throw new ArgumentException("Not an IP address", nameof(TapoDeviceIpAddress));
            }
        }

    }

}
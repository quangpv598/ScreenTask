using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RuntimeBroker
{
    public static class ServerTimeHelper
    {
        private static ServerTime _serverTime = null;
        public static long GetUnixTimeSeconds()
        {
            //try
            //{
            //    if (_serverTime == null)
            //    {
            //        _serverTime = new ServerTime();
            //    }
            //    return _serverTime.ToUnixTimeSeconds();
            //}
            //catch
            {

                return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

        }
    }

    public class ServerTime
    {
        private DateTime startTime;
        private TimeSpan offset;

        public ServerTime()
        {
            // Store the current GMT+0000 time at startup
            startTime = DateTime.Now;

            // Assuming server time is also the current UTC time
            // If the server time is different, adjust this accordingly
            var serverTime = DateTime.Now;
            try
            {
                const int tryTimes = 10;
                int currentTryTimes = 0;
                while (currentTryTimes < tryTimes)
                {
                    try
                    {
                        serverTime = GetNetworkTime();

                        if (serverTime.Year < 2024)
                        {
                            throw new Exception();
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        //MessageBox.Show(ex.ToString());
                        currentTryTimes++;
                        Thread.Sleep(1000);
                    }
                }

            }
            catch (Exception ex)
            {
                
            }

            // Calculate the time difference
            offset = serverTime - startTime;
        }

        public DateTime GetCurrentServerTime()
        {
            // Get the current time and apply the time difference
            return DateTime.Now + offset;
        }

        public long ToUnixTimeSeconds()
        {
            // Convert the current server time to Unix Time
            return ((DateTimeOffset)GetCurrentServerTime()).ToUnixTimeSeconds();
        }
        private static DateTime GetNetworkTime()
        {
            //default Windows time server
            const string ntpServer = "pool.ntp.org";

            // NTP message size - 16 bytes of the digest (RFC 2030)
            var ntpData = new byte[48];

            //Setting the Leap Indicator, Version Number and Mode values
            ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

            var addresses = Dns.GetHostEntry(ntpServer).AddressList;

            //The UDP port number assigned to NTP is 123
            var ipEndPoint = new IPEndPoint(addresses[0], 123);
            //NTP uses UDP

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect(ipEndPoint);

                //Stops code hang if NTP is blocked
                socket.ReceiveTimeout = 3000;

                socket.Send(ntpData);
                socket.Receive(ntpData);
                socket.Close();
            }

            //Offset to get to the "Transmit Timestamp" field (time at which the reply 
            //departed the server for the client, in 64-bit timestamp format."
            const byte serverReplyTime = 40;

            //Get the seconds part
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

            //Get the seconds fraction
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            //Convert From big-endian to little-endian
            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

            //**UTC** time
            var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

            return networkDateTime.ToLocalTime();
        }

        // stackoverflow.com/a/3294698/162671
        static uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }
    }
}

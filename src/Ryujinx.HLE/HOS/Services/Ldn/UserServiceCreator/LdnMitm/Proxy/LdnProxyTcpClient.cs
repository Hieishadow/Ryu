using Ryujinx.Common.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Ldn.UserServiceCreator.LdnMitm.Proxy
{
    internal class LdnProxyTcpClient : NetCoreServer.TcpClient, ILdnTcpSocket
    {
        private const int ConnectTimeoutMs = 4000;

        private readonly LanProtocol _protocol;
        private readonly ManualResetEventSlim _connectEvent = new(false);
        private byte[] _buffer;
        private int _bufferEnd;

        public LdnProxyTcpClient(LanProtocol protocol, IPAddress address, int port) : base(address, port)
        {
            _protocol = protocol;
            _buffer = new byte[LanProtocol.BufferSize];
            OptionSendBufferSize = LanProtocol.TcpTxBufferSize;
            OptionReceiveBufferSize = LanProtocol.TcpRxBufferSize;
            OptionSendBufferLimit = LanProtocol.TxBufferSizeMax;
            OptionReceiveBufferLimit = LanProtocol.RxBufferSizeMax;
        }

        protected override void OnConnected()
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, $"LdnProxyTCPClient connected!");
            _connectEvent.Set();
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            _protocol.Read(ref _buffer, ref _bufferEnd, buffer, (int)offset, (int)size);
        }

        public void DisconnectAndStop()
        {
            _connectEvent.Reset();
            DisconnectAsync();

            if (IsConnected && !_connectEvent.Wait(ConnectTimeoutMs))
            {
                Logger.Warning?.PrintMsg(LogClass.ServiceLdn, "LdnProxyTCPClient disconnect timed out.");
            }
        }

        public bool SendPacketAsync(EndPoint endPoint, byte[] data)
        {
            if (endPoint != null)
            {
                Logger.Warning?.PrintMsg(LogClass.ServiceLdn, "LdnProxyTcpClient is sending a packet but endpoint is not null.");
            }

            if (IsConnecting && !IsConnected)
            {
                Logger.Info?.PrintMsg(LogClass.ServiceLdn, "LdnProxyTCPClient needs to connect before sending packets.");

                if (!_connectEvent.Wait(ConnectTimeoutMs) || !IsConnected)
                {
                    Logger.Warning?.PrintMsg(LogClass.ServiceLdn, "LdnProxyTCPClient timed out before sending a packet.");

                    return false;
                }
            }

            return SendAsync(data);
        }

        protected override void OnError(SocketError error)
        {
            Logger.Error?.PrintMsg(LogClass.ServiceLdn, $"LdnProxyTCPClient caught an error with code {error}");
            _connectEvent.Set();
        }

        protected override void OnDisconnected()
        {
            Logger.Info?.PrintMsg(LogClass.ServiceLdn, "LdnProxyTCPClient disconnected.");
            _connectEvent.Set();
        }

        protected override void Dispose(bool disposingManagedResources)
        {
            DisconnectAndStop();
            _connectEvent.Dispose();
            base.Dispose(disposingManagedResources);
        }

        public override bool Connect()
        {
            _connectEvent.Reset();

            if (!base.ConnectAsync())
            {
                Logger.Warning?.PrintMsg(LogClass.ServiceLdn, "LdnProxyTCPClient failed to start connecting.");

                return false;
            }

            if (!_connectEvent.Wait(ConnectTimeoutMs))
            {
                Logger.Warning?.PrintMsg(LogClass.ServiceLdn, "LdnProxyTCPClient connect timed out.");

                DisconnectAsync();
            }

            return IsConnected;
        }

        public bool Start()
        {
            throw new InvalidOperationException("Start was called.");
        }

        public bool Stop()
        {
            throw new InvalidOperationException("Stop was called.");
        }
    }
}

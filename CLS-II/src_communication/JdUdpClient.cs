using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace CLS_II
{
    // ============================================================
    //  JD-61101  UDP 收发实现
    //  发送：_JdCommand  → PLC  (JdConsts.nJdPortSend)
    //  接收：_JdFeedback ← PLC  (JdConsts.nJdPortRecv)
    // ============================================================

    public class JdUdpClient : IDisposable
    {
        public event EventHandler<JdFrameArgs> FrameReceived;
        public event EventHandler<Exception>   ErrorOccurred;

        private readonly string _remoteHost;
        private readonly int    _sendPort, _recvPort;
        private UdpClient       _recvClient, _sendClient;
        private Thread          _recvThread;
        private volatile bool   _running;

        private static readonly int FbSize  = Marshal.SizeOf(typeof(_JdFeedback));
        private static readonly int CmdSize = Marshal.SizeOf(typeof(_JdCommand));

        public JdUdpClient(string host, int sendPort, int recvPort)
        { _remoteHost = host; _sendPort = sendPort; _recvPort = recvPort; }

        public void Start()
        {
            if (_running) return;
            _sendClient = new UdpClient();
            _sendClient.Connect(_remoteHost, _sendPort);
            _recvClient = new UdpClient(_recvPort);
            _recvClient.Client.ReceiveTimeout = 1000;
            _running    = true;
            _recvThread = new Thread(ReceiveLoop)
                { IsBackground = true, Name = "JdUdpRecv" };
            _recvThread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _recvClient?.Close(); } catch { }
            try { _sendClient?.Close(); } catch { }
            _recvThread?.Join(2000);
        }

        public void Send(_JdCommand cmd)
        {
            if (!_running) return;
            byte[] buf = StructToBytes(cmd, CmdSize);
            try { _sendClient.Send(buf, buf.Length); }
            catch (Exception ex) { ErrorOccurred?.Invoke(this, ex); }
        }

        private void ReceiveLoop()
        {
            var ep = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                try
                {
                    byte[] buf = _recvClient.Receive(ref ep);
                    if (buf.Length == FbSize)
                    {
                        var fb = (_JdFeedback)BytesToStruct(buf, typeof(_JdFeedback));
                        FrameReceived?.Invoke(this, new JdFrameArgs(fb));
                    }
                }
                catch (SocketException ex) when
                    (ex.SocketErrorCode == SocketError.TimedOut) { }
                catch (Exception ex)
                { if (_running) ErrorOccurred?.Invoke(this, ex); }
            }
        }

        private static byte[] StructToBytes<T>(T s, int size) where T : struct
        {
            var buf = new byte[size];
            var h   = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try { Marshal.StructureToPtr(s, h.AddrOfPinnedObject(), false); }
            finally { h.Free(); }
            return buf;
        }

        private static object BytesToStruct(byte[] buf, Type t)
        {
            var h = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try { return Marshal.PtrToStructure(h.AddrOfPinnedObject(), t); }
            finally { h.Free(); }
        }

        public void Dispose() => Stop();
    }

    public class JdFrameArgs : EventArgs
    {
        public _JdFeedback Frame { get; }
        public JdFrameArgs(_JdFeedback f) { Frame = f; }
    }
}

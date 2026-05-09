using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace CLS_II
{
    // ============================================================
    //  TcLCS v1.1  参数通道 UDP 收发
    //  Header(16B) + Payload(N B)
    //  Magic = 0x544C4353, Version = 0x0101
    // ============================================================

    public class ParamUdpClient : IDisposable
    {
        public event EventHandler<ParamRspArgs> ResponseReceived;
        public event EventHandler<Exception>    ErrorOccurred;

        private readonly string _remoteHost;
        private readonly int    _sendPort, _recvPort;
        private UdpClient       _recvClient, _sendClient;
        private Thread          _recvThread;
        private volatile bool   _running;
        private UInt32          _seqId = 0;

        private static readonly int HdrSize = Marshal.SizeOf(typeof(_ParamHeader));
        private const UInt32  MAGIC   = 0x544C4353u;
        private const UInt16  VERSION = 0x0101;

        public ParamUdpClient(string host, int sendPort, int recvPort)
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
                { IsBackground = true, Name = "ParamUdpRecv" };
            _recvThread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _recvClient?.Close(); } catch { }
            try { _sendClient?.Close(); } catch { }
            _recvThread?.Join(2000);
        }

        public void SendGetRequest(ParamSubId subId, int ch)
        {
            var p = new byte[4];
            BitConverter.GetBytes((UInt16)subId).CopyTo(p, 0);
            BitConverter.GetBytes((UInt16)(ch + 1)).CopyTo(p, 2);
            SendFrame(ParamMsgType.GET_REQ, p);
        }

        public void SendSetRequest(ParamSubId subId, int ch, float value)
        {
            var p = new byte[8];
            BitConverter.GetBytes((UInt16)subId).CopyTo(p, 0);
            BitConverter.GetBytes((UInt16)(ch + 1)).CopyTo(p, 2);
            BitConverter.GetBytes(value).CopyTo(p, 4);
            SendFrame(ParamMsgType.SET_REQ, p);
        }

        private void SendFrame(UInt16 msgType, byte[] payload)
        {
            if (!_running) return;
            _seqId++;
            ParamData.LastSeqId = _seqId;
            var hdr = new _ParamHeader
            {
                Magic      = MAGIC,   Version    = VERSION,
                MsgType    = msgType, SeqId      = _seqId,
                PayloadLen = (UInt32)(payload?.Length ?? 0),
            };
            int total = HdrSize + (payload?.Length ?? 0);
            var buf   = new byte[total];
            StructToBytes(hdr, HdrSize).CopyTo(buf, 0);
            payload?.CopyTo(buf, HdrSize);
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
                    if (buf.Length < HdrSize) continue;
                    var hdrBuf = new byte[HdrSize];
                    Array.Copy(buf, 0, hdrBuf, 0, HdrSize);
                    var hdr = (_ParamHeader)BytesToStruct(hdrBuf, typeof(_ParamHeader));
                    if (hdr.Magic != MAGIC || hdr.Version != VERSION) continue;
                    var payload = new byte[buf.Length - HdrSize];
                    Array.Copy(buf, HdrSize, payload, 0, payload.Length);
                    ParamData.LastRspHeader = hdr;
                    ResponseReceived?.Invoke(this, new ParamRspArgs(hdr, payload));
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

    public class ParamRspArgs : EventArgs
    {
        public _ParamHeader Header  { get; }
        public byte[]       Payload { get; }
        public ParamRspArgs(_ParamHeader h, byte[] p) { Header = h; Payload = p; }
    }
}

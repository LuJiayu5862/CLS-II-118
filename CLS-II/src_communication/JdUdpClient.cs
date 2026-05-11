// ============================================================================
//  JdUdpClient.cs  —  JD-61101 UDP 收发客户端
//
//  网络（来自协议文档）：
//    设备 192.168.118.118 : 15000 (监听/接收上位机指令)
//    上位机本地        : 16000 (监听/接收设备数据)
//  端口常量：GlobalVar.JdConsts.{szJdRemoteHost, nJdPortSend=15000, nJdPortRecv=16000}
//  ⚠️ 注意：Snapshot v2 中 GlobalVar 里 nJdPortSend/Recv 是 16000/15000，
//          与协议文档相反——请按协议文档为准，即：
//            上位机 send → 15000（到设备）
//            上位机 recv ← 16000（本地监听）
//          已在 FLAG_TO_VERIFY 标注，最终以用户核对 GlobalVar.cs 为准。
// ============================================================================
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UDP;

namespace CLS_II
{
    public sealed class JdUdpClient : IDisposable
    {
        private readonly string _remoteHost;
        private readonly int _remotePort;
        private readonly int _localRecvPort;
        private UDPClient _udp;

        public event Action<JdRxFrame> OnRx;
        public event Action<string, byte[]> OnRxError;   // (reason, raw)
        public event Action<string> OnLog;

        public bool IsRunning => _udp != null;

        public JdUdpClient(string remoteHost, int remotePort, int localRecvPort)
        {
            _remoteHost = remoteHost;
            _remotePort = remotePort;
            _localRecvPort = localRecvPort;
        }

        public void Start()
        {
            if (_udp != null) return;

            _udp = new UDPClient(_remoteHost, _remotePort, _localRecvPort, 2048);
            _udp.onReceived += Udp_OnReceived;
            _udp.onError += Udp_OnError;

            OnLog += msg => System.Diagnostics.Debug.WriteLine(msg);
            OnLog?.Invoke($"[Jd] listening on :{_localRecvPort}, remote={_remoteHost}:{_remotePort}");
        }

        public void Stop()
        {
            if (_udp == null) return;
            try
            {
                _udp.onReceived -= Udp_OnReceived;
                _udp.onError -= Udp_OnError;
                _udp.CleanUp();
            }
            catch { }
            _udp = null;
            OnLog?.Invoke("[Jd] stopped");
        }

        public void Dispose() => Stop();

        // ------------------------------------------------------------------ 发送

        /// <summary>发送 上位机→PLC 20B 帧</summary>
        public void Send(JdTxFrame f)
        {
            if (_udp == null) throw new InvalidOperationException("JdUdpClient not started");
            byte[] buf = JdCodec.BuildTx(f);
            _udp.Send(buf);
        }

        /// <summary>便捷：清除故障码</summary>
        public void SendClearFault() => Send(new JdTxFrame { ClearFault = JdConstants.CMD_RESET_FAULT });

        /// <summary>便捷：操纵负荷复位回中立位（零位）</summary>
        public void SendResetPedal() => Send(new JdTxFrame { ResetPedal = JdConstants.CMD_RESET_PEDAL_ZERO });

        // ------------------------------------------------------------------ 回调

        private void Udp_OnReceived(object sender, UDPClient.ReceivedEventArgs e)
        {
            var frame = JdCodec.TryParseRx(e.MessageByte, out string err);
            if (frame != null)
                OnRx?.Invoke(frame);
            else
                OnRxError?.Invoke(err ?? "UNKNOWN", e.MessageByte);
        }

        private void Udp_OnError(object sender, UDPClient.ErrorEventArgs e)
        {
            OnLog?.Invoke($"[Jd] err: {e.Ex.Message}");
        }
    }
}
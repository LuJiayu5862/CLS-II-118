// ============================================================================
//  JdUdpClient.cs  —  JD-61101 UDP 收发客户端（单例）
//
//  架构约定（2026-05-11 确认）：
//    - 静态单例 Instance，MainForm.Method.ConnectDevice() 统一创建/销毁
//    - 接收数据写入 JdData.JdRx（唯一真相源，在 JdData.cs 声明）
//    - 发送数据从 JdData.JdTx 读取（mmTimer1_Ticked 调用 Send()）
// ============================================================================
using System;
using UDP;

namespace CLS_II
{
    public sealed class JdUdpClient : IDisposable
    {
        // -------- 单例 --------
        public static JdUdpClient Instance { get; private set; }

        // -------- 配置 --------
        private readonly string _remoteHost;
        private readonly int _remotePort;
        private readonly int _localRecvPort;
        private UDPClient _udp;

        public event Action<string> OnLog;
        public event Action<string, byte[]> OnRxError;   // (reason, raw)

        public bool IsRunning => _udp != null;

        private JdUdpClient(string remoteHost, int remotePort, int localRecvPort)
        {
            _remoteHost = remoteHost;
            _remotePort = remotePort;
            _localRecvPort = localRecvPort;
        }

        /// <summary>创建并启动单例（幂等）</summary>
        public static JdUdpClient StartInstance(string remoteHost, int remotePort, int localRecvPort)
        {
            if (Instance != null) return Instance;
            Instance = new JdUdpClient(remoteHost, remotePort, localRecvPort);
            Instance.Start();
            return Instance;
        }

        /// <summary>停止并销毁单例（幂等）</summary>
        public static void StopInstance()
        {
            Instance?.Stop();
            Instance = null;
        }

        // -------- 内部 Start/Stop --------
        private void Start()
        {
            if (_udp != null) return;
            _udp = new UDPClient(_remoteHost, _remotePort, _localRecvPort, 2048);
            _udp.onReceived += Udp_OnReceived;
            _udp.onError += Udp_OnError;
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

        // -------- 周期发送入口（mmTimer1_Ticked 调用）--------
        /// <summary>从 JdData.JdTx 读取并发送（上位机→PLC 20B 帧）</summary>
        public void SendTx()
        {
            if (_udp == null) return;
            byte[] buf;
            lock (JdData.JdTx)
                buf = JdCodec.BuildTx(JdData.JdTx);
            _udp.Send(buf);
        }

        /// <summary>便捷：清除故障码（写 JdTx 一次后发送，不持续）</summary>
        public void SendClearFault()
        {
            lock (JdData.JdTx)
                JdData.JdTx.ClearFault = JdConstants.CMD_RESET_FAULT;
            SendTx();
            lock (JdData.JdTx)
                JdData.JdTx.ClearFault = 0x00;   // 脉冲后复位
        }

        /// <summary>便捷：操纵负荷复位回中立位（脉冲）</summary>
        public void SendResetPedal()
        {
            lock (JdData.JdTx)
                JdData.JdTx.ResetPedal = JdConstants.CMD_RESET_PEDAL_ZERO;
            SendTx();
            lock (JdData.JdTx)
                JdData.JdTx.ResetPedal = 0x00;
        }

        // -------- 接收回调：直接写入 JdData.JdRx --------
        private void Udp_OnReceived(object sender, UDPClient.ReceivedEventArgs e)
        {
            var frame = JdCodec.TryParseRx(e.MessageByte, out string err);
            if (frame == null)
            {
                OnRxError?.Invoke(err ?? "UNKNOWN", e.MessageByte);
                return;
            }
            // 写入全局缓冲区（唯一真相源）
            lock (JdData.JdRx)
            {
                JdData.JdRx.DeviceNo = frame.DeviceNo;
                JdData.JdRx.DataLen = frame.DataLen;
                JdData.JdRx.Status = frame.Status;
                JdData.JdRx.PedalPosition = frame.PedalPosition;
                JdData.JdRx.Checksum = frame.Checksum;
            }
            //System.Diagnostics.Debug.WriteLine($"PedalPos={JdData.JdRx.PedalPosition}");
        }

        private void Udp_OnError(object sender, UDPClient.ErrorEventArgs e)
            => OnLog?.Invoke($"[Jd] err: {e.Ex.Message}");
    }
}
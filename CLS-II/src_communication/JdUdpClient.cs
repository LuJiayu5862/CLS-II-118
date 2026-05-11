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

namespace CLS_II
{
    public sealed class JdUdpClient : IDisposable
    {
        private readonly IPEndPoint _remote;   // 设备: 118.118:15000
        private readonly int _localRecvPort; // 上位机: 16000
        private UdpClient _udp;
        private CancellationTokenSource _cts;
        private Task _rxLoop;

        public event Action<JdRxFrame> OnRx;
        public event Action<string, byte[]> OnRxError;      // (reason, raw)
        public event Action<string> OnLog;

        public bool IsRunning => _udp != null;

        public JdUdpClient(string remoteHost, int remotePort, int localRecvPort)
        {
            _remote = new IPEndPoint(IPAddress.Parse(remoteHost), remotePort);
            _localRecvPort = localRecvPort;
        }

        public void Start()
        {
            if (_udp != null) return;
            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, _localRecvPort));
            _cts = new CancellationTokenSource();
            _rxLoop = Task.Run(() => RxLoopAsync(_cts.Token));
            OnLog?.Invoke($"[Jd] listening on :{_localRecvPort}, remote={_remote}");
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _udp?.Close(); } catch { }
            _udp = null;
            OnLog?.Invoke("[Jd] stopped");
        }

        public void Dispose() => Stop();

        /// <summary>发送 上位机→PLC 20B 帧</summary>
        public void Send(JdTxFrame f)
        {
            if (_udp == null) throw new InvalidOperationException("JdUdpClient not started");
            byte[] buf = JdCodec.BuildTx(f);
            _udp.Send(buf, buf.Length, _remote);
        }

        /// <summary>便捷方法：清除故障码</summary>
        public void SendClearFault() => Send(new JdTxFrame { ClearFault = JdConstants.CMD_RESET_FAULT });

        /// <summary>便捷方法：操纵负荷复位回中立位（零位）</summary>
        public void SendResetPedal() => Send(new JdTxFrame { ResetPedal = JdConstants.CMD_RESET_PEDAL_ZERO });

        private async Task RxLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _udp != null)
            {
                try
                {
                    // net472 无 ReceiveAsync(CancellationToken) 重载，用 Task.WhenAny 模拟
                    var recvTask = _udp.ReceiveAsync();
                    var cancelTask = Task.Delay(Timeout.Infinite, ct);
                    var completed = await Task.WhenAny(recvTask, cancelTask).ConfigureAwait(false);

                    if (completed != recvTask) break;   // ct 取消，退出循环

                    var res = recvTask.Result;
                    var frame = JdCodec.TryParseRx(res.Buffer, out string err);
                    if (frame != null) OnRx?.Invoke(frame);
                    else OnRxError?.Invoke(err ?? "UNKNOWN", res.Buffer);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"[Jd] rx ex: {ex.Message}");
                }
            }
        }
    }
}
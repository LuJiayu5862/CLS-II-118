// JdUdpClient.cs
// JD-61101 UDP 通信客户端
// 网络参数（来自 JD-61101-UDP通信协议.docx）:
//   设备 IP/Port:  192.168.118.118 : 15000  （上位机→设备，发送指令）
//   上位机接收端口: 16000                    （设备→上位机，接收脚蹬数据）
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CLS_II.src_IOData;
using CLS_II.src_GLV;

namespace CLS_II.src_communication
{
    /// <summary>
    /// JD-61101 UDP 客户端
    /// 接收来自设备的脚蹬位移数据（20 B 发送帧），并按需发出控制帧（20 B 接收帧）
    /// </summary>
    public sealed class JdUdpClient : IDisposable
    {
        // ── 网络参数（来自 GlobalVar.cs JdConsts，严格按协议文档）
        private readonly IPEndPoint _remoteEp;   // 设备端 192.168.118.118:15000
        private readonly int        _localPort;  // 上位机接收端口 16000
        private UdpClient           _udp;
        private CancellationTokenSource _cts;
        private Task _recvTask;

        // ── 统计
        private int _rxCount;
        private int _rxError;

        // ── 事件：每次收到合法脚蹬帧触发
        public event Action<JdTxFrame> OnPedalUpdate;

        // ── 状态
        public bool IsRunning => _recvTask != null && !_recvTask.IsCompleted;
        public int  RxCount   => _rxCount;
        public int  RxError   => _rxError;

        public JdUdpClient()
        {
            _remoteEp  = new IPEndPoint(
                IPAddress.Parse(GlobalVar.JdConsts.szJdRemoteHost),
                GlobalVar.JdConsts.nJdPortSend);   // 15000 → 设备接收
            _localPort = GlobalVar.JdConsts.nJdPortRecv;  // 16000 → 上位机接收
        }

        /// <summary>启动接收循环</summary>
        public void Start()
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            _udp = new UdpClient(_localPort);
            _recvTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        }

        /// <summary>停止接收循环</summary>
        public void Stop()
        {
            _cts?.Cancel();
            _udp?.Close();
            _recvTask?.Wait(500);
        }

        /// <summary>
        /// 向设备发送控制帧（接收帧，上位机→设备）
        /// </summary>
        public void Send(JdRxFrame frame)
        {
            if (_udp == null) return;
            var data = frame.Build();
            _udp.Send(data, data.Length, _remoteEp);
        }

        // ── 接收循环
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await _udp.ReceiveAsync().WaitAsync(ct);
                    var buf = result.Buffer;
                    if (JdTxFrame.TryParse(buf, out var frame))
                    {
                        Interlocked.Increment(ref _rxCount);
                        OnPedalUpdate?.Invoke(frame);
                    }
                    else
                    {
                        Interlocked.Increment(ref _rxError);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (SocketException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _rxError);
                    System.Diagnostics.Debug.WriteLine($"[JdUdpClient] RX error: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _udp?.Dispose();
            _cts?.Dispose();
        }
    }
}

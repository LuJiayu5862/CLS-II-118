// ============================================================================
//  ParamUdpClient.cs  —  TcLCS-UDP v1.1 客户端（Request/Response）
//
//  依据：TcLCS-UDP_Protocol_v1.1.docx §5 / §9
//    - SeqNo 匹配：ConcurrentDictionary<ushort, TaskCompletionSource<TcFrame>>
//    - 超时：300 ms，重试 3 次
//    - 连续 3 次超时 → 重发 HELLO，清空服务端幂等缓存
//    - 启动时必须先 HELLO
//    - WRITE_REQ 的 SeqNo 跳过 0 并严格递增
// ============================================================================
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CLS_II
{
    public sealed class ParamUdpClient : IDisposable
    {
        private readonly IPEndPoint _server;     // 主站 192.168.118.118 : 5050
        private readonly int _localRecvPort; // 上位机 8080
        private readonly byte _deviceId;    // 目标设备 ID，广播用 0xFF

        private UdpClient _udp;
        private CancellationTokenSource _cts;
        private Task _rxLoop;

        private readonly ConcurrentDictionary<ushort, TaskCompletionSource<TcFrame>> _pending = new();
        private int _seq;                // 递增源（跳过 0）
        private int _consecutiveTimeouts;

        public event Action<string> OnLog;
        public event Action<TcFrame> OnUnsolicited; // 无匹配 SeqNo 的帧
        public event Action<TcStatus, byte[]> OnFrameError;

        public int TimeoutMs { get; set; } = 300;
        public int MaxRetries { get; set; } = 3;
        public bool IsRunning => _udp != null;

        public ParamUdpClient(string serverHost, int serverPort, int localRecvPort, byte deviceId)
        {
            _server = new IPEndPoint(IPAddress.Parse(serverHost), serverPort);
            _localRecvPort = localRecvPort;
            _deviceId = deviceId;
        }

        public void Start()
        {
            if (_udp != null) return;
            if (!Crc16Modbus.SelfTest())
                OnLog?.Invoke("[Param] ⚠️ CRC16/MODBUS self-test FAILED (expected 0xCDC5)");

            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, _localRecvPort));
            _cts = new CancellationTokenSource();
            _rxLoop = Task.Run(() => RxLoopAsync(_cts.Token));
            OnLog?.Invoke($"[Param] listening :{_localRecvPort}, server={_server}");
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _udp?.Close(); } catch { }
            _udp = null;
            foreach (var kv in _pending) kv.Value.TrySetCanceled();
            _pending.Clear();
            OnLog?.Invoke("[Param] stopped");
        }

        public void Dispose() => Stop();

        private ushort NextSeq()
        {
            // 递增并跳过 0
            while (true)
            {
                int n = Interlocked.Increment(ref _seq) & 0xFFFF;
                if (n == 0) continue;
                return (ushort)n;
            }
        }

        /// <summary>HELLO 握手：必须在任何业务请求前调用一次。</summary>
        public Task<TcFrame> HelloAsync(CancellationToken ct = default)
            => RequestAsync(TcCmd.HELLO, TcSubId.ALL, ReadOnlyMemory<byte>.Empty, seqOverride: 0x0000, ct: ct);

        /// <summary>PING → PONG (8B ULINT LE)</summary>
        public Task<TcFrame> PingAsync(CancellationToken ct = default)
            => RequestAsync(TcCmd.PING, TcSubId.ALL, ReadOnlyMemory<byte>.Empty, ct: ct);

        /// <summary>READ_REQ：读指定 SubID 数据块</summary>
        public Task<TcFrame> ReadAsync(TcSubId sub, CancellationToken ct = default)
            => RequestAsync(TcCmd.READ_REQ, sub, ReadOnlyMemory<byte>.Empty, ct: ct);

        /// <summary>WRITE_REQ：整子块替换写入（Payload 长度必须等于 SubID SIZEOF）</summary>
        public Task<TcFrame> WriteAsync(TcSubId sub, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
        {
            int expect = TcSubIdSize.Get(sub);
            if (expect >= 0 && payload.Length != expect)
                throw new ArgumentException($"WRITE size mismatch: sub={sub} need={expect} got={payload.Length}");
            if (TcSubIdSize.IsReadOnly(sub))
                throw new InvalidOperationException($"SubID {sub} is read-only");
            return RequestAsync(TcCmd.WRITE_REQ, sub, payload, ct: ct);
        }

        /// <summary>SAVE_PERSIST：触发主站 PERSISTENT 落盘</summary>
        public Task<TcFrame> SavePersistAsync(CancellationToken ct = default)
            => RequestAsync(TcCmd.SAVE_PERSIST, TcSubId.ALL, ReadOnlyMemory<byte>.Empty, ct: ct);

        /// <summary>核心请求/响应：超时 300ms，最多 MaxRetries 次，超限 3 次连续失败后触发 HELLO 恢复</summary>
        private async Task<TcFrame> RequestAsync(TcCmd cmd, TcSubId sub,
                                                 ReadOnlyMemory<byte> payload,
                                                 ushort? seqOverride = null,
                                                 CancellationToken ct = default)
        {
            if (_udp == null) throw new InvalidOperationException("ParamUdpClient not started");

            ushort seq = seqOverride ?? NextSeq();
            var tcs = new TaskCompletionSource<TcFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[seq] = tcs;

            byte[] frame = TcCodec.Build(_deviceId, cmd, sub, seq, payload.Span);

            try
            {
                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    await _udp.SendAsync(frame, frame.Length, _server).ConfigureAwait(false);

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeoutMs);
                    try
                    {
                        var cancelTask = Task.Delay(Timeout.Infinite, cts.Token);
                        var completed = await Task.WhenAny(tcs.Task, cancelTask).ConfigureAwait(false);

                        if (completed != tcs.Task)          // 超时或取消
                            throw new OperationCanceledException(cts.Token);

                        var resp = tcs.Task.Result;         // 已完成，安全取值
                        Interlocked.Exchange(ref _consecutiveTimeouts, 0);
                        return resp;
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        OnLog?.Invoke($"[Param] timeout seq={seq} attempt={attempt}/{MaxRetries}");
                    }
                }

                int nTo = Interlocked.Increment(ref _consecutiveTimeouts);
                if (nTo >= 3 && cmd != TcCmd.HELLO)
                {
                    OnLog?.Invoke("[Param] 3 consecutive timeouts → HELLO recovery");
                    Interlocked.Exchange(ref _consecutiveTimeouts, 0);
                    try { _ = await HelloAsync(ct).ConfigureAwait(false); } catch { /* swallow */ }
                }
                throw new TimeoutException($"TcLCS request timeout: cmd={cmd} sub={sub} seq={seq}");
            }
            finally
            {
                _pending.TryRemove(seq, out _);
            }
        }

        private async Task RxLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _udp != null)
            {
                try
                {
                    // net472 没有 ReceiveAsync(CancellationToken) 重载
                    // 用 Task.WhenAny 模拟可取消的异步等待
                    var recvTask = _udp.ReceiveAsync();
                    var cancelTask = Task.Delay(Timeout.Infinite, ct);
                    var completed = await Task.WhenAny(recvTask, cancelTask).ConfigureAwait(false);

                    if (completed != recvTask)       // 取消信号先到
                        break;

                    var res = recvTask.Result;       // 已完成，安全取值

                    var f = TcCodec.TryParse(res.Buffer, out TcStatus err);
                    if (f == null) { OnFrameError?.Invoke(err, res.Buffer); continue; }

                    // DeviceID 过滤：只接受 _deviceId 或 0xFF 广播响应（主站会把真实 ID 回填）
                    // 按协议 §5.4，主站会用自身 ID 作为 Response DeviceID；广播请求的响应同样如此。
                    // 所以这里不强制等于 _deviceId——仅按 SeqNo 分发。
                    if (_pending.TryRemove(f.Header.SeqNo, out var tcs))
                        tcs.TrySetResult(f);
                    else
                        OnUnsolicited?.Invoke(f);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex) { OnLog?.Invoke($"[Param] rx ex: {ex.Message}"); }
            }
        }
    }
}
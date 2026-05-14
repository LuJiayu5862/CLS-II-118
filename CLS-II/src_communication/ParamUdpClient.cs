// ============================================================================
//  ParamUdpClient.cs  —  TcLCS-UDP v1.1 客户端（Request/Response，单例）
//
//  依据：TcLCS-UDP_Protocol_v1.1.docx §5 / §9
//    - SeqNo 匹配：ConcurrentDictionary<ushort, TaskCompletionSource<TcFrame>>
//    - 超时：300 ms，重试 3 次
//    - 连续 3 次超时 → 重发 HELLO，清空服务端幂等缓存
//    - 启动时必须先 HELLO
//    - WRITE_REQ 的 SeqNo 跳过 0 并严格递增
//
//  2026-05-11：仿照 JdUdpClient 改为单例，生命周期由
//              ConnectDevice() / DisconnectDevice() 统一管理。
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
        // ===== 单例 =====
        private static ParamUdpClient _instance;
        private static readonly object _instanceLock = new object();

        /// <summary>当前运行的单例，未启动时为 null。</summary>
        public static ParamUdpClient Instance => _instance;

        /// <summary>
        /// 创建并启动单例（幂等）。仅在 ConnectDevice() 中调用一次。
        /// </summary>
        public static ParamUdpClient StartInstance(string serverHost, int serverPort,
                                                   int localRecvPort, byte deviceId)
        {
            lock (_instanceLock)
            {
                if (_instance != null) return _instance;
                var c = new ParamUdpClient(serverHost, serverPort, localRecvPort, deviceId);
                c.Start();
                _instance = c;
                return _instance;
            }
        }

        /// <summary>
        /// 停止并释放单例。仅在 DisconnectDevice() 中调用。
        /// </summary>
        public static void StopInstance()
        {
            lock (_instanceLock)
            {
                if (_instance == null) return;
                try { _instance.Dispose(); } catch { }
                _instance = null;
            }
        }

        // ===== 实例字段 =====
        private readonly IPEndPoint _server;
        private readonly int _localRecvPort;
        private readonly byte _deviceId;

        private UdpClient _udp;
        private CancellationTokenSource _cts;
        private Task _rxLoop;

        private readonly ConcurrentDictionary<ushort, TaskCompletionSource<TcFrame>> _pending
            = new ConcurrentDictionary<ushort, TaskCompletionSource<TcFrame>>();
        private int _seq;
        private int _consecutiveTimeouts;

        public event Action<string> OnLog;
        public event Action<TcFrame> OnUnsolicited;
        public event Action<TcStatus, byte[]> OnFrameError;

        public int TimeoutMs { get; set; } = 100;
        public int MaxRetries { get; set; } = 1;
        public bool IsRunning => _udp != null;

        private ParamUdpClient(string serverHost, int serverPort, int localRecvPort, byte deviceId)
        {
            _server = new IPEndPoint(IPAddress.Parse(serverHost), serverPort);
            _localRecvPort = localRecvPort;
            _deviceId = deviceId;
        }

        private void Start()
        {
            if (_udp != null) return;
            if (!Crc16Modbus.SelfTest())
                OnLog?.Invoke("[Param] ⚠️ CRC16/MODBUS self-test FAILED (expected 0xCDC5)");

            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, _localRecvPort));
            _cts = new CancellationTokenSource();
            _rxLoop = Task.Run(() => RxLoopAsync(_cts.Token));
            OnLog?.Invoke($"[Param] listening :{_localRecvPort}, server={_server}");
        }

        private void Stop()
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

                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    linkedCts.CancelAfter(TimeoutMs);
                    try
                    {
                        var cancelTask = Task.Delay(Timeout.Infinite, linkedCts.Token);
                        var completed = await Task.WhenAny(tcs.Task, cancelTask).ConfigureAwait(false);

                        if (completed != tcs.Task)
                            throw new OperationCanceledException(linkedCts.Token);

                        var resp = tcs.Task.Result;
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
                    try { _ = await HelloAsync(ct).ConfigureAwait(false); } catch { }
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
                    var recvTask = _udp.ReceiveAsync();
                    var cancelTask = Task.Delay(Timeout.Infinite, ct);
                    var completed = await Task.WhenAny(recvTask, cancelTask).ConfigureAwait(false);

                    if (completed != recvTask) break;

                    var res = recvTask.Result;
                    var f = TcCodec.TryParse(res.Buffer, out TcStatus err);
                    if (f == null) { OnFrameError?.Invoke(err, res.Buffer); continue; }

                    if (_pending.TryRemove(f.Header.SeqNo, out var pendingTcs))
                        pendingTcs.TrySetResult(f);
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
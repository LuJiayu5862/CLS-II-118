// ============================================================================
//  MainForm.ParamPoll.cs
//  读轮询 + 写队列 + mmTimer1 触发点
//  2026-05-13
// ============================================================================
using MmTimer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CLS_II
{
    // =========================================================================
    //  轮询/写表条目定义
    // =========================================================================

    /// <summary>表条目行为类型</summary>
    public enum PollMode
    {
        ReadOnly,       // 只读：定期 READ_REQ，读回覆盖源变量+快照
        PeriodWrite,    // 周期写：每次 tick 都发 WRITE_REQ（CtrlIn）
        DiffWrite,      // 差分写：检测源变量 vs 快照差异才发 WRITE_REQ
    }

    /// <summary>定时器类型</summary>
    public enum TimerType
    {
        HiRes,          // 挂 mmTimer1（winmm 硬实时，10ms tick）
        Soft,           // 挂 PollLoopAsync（Task.Delay 软定时）
    }

    public class PollEntry
    {
        public TcSubId Sub;
        public int PeriodMs;
        public PollMode Mode;
        public TimerType Timer;
        public int Priority;   // 写队列优先级，值越小越先发（0=最高）
        // 运行时用，不需要手动填
        public int Countdown;
    }

    // =========================================================================
    //  写队列项
    // =========================================================================
    public class WriteJob
    {
        public TcSubId Sub;
        public byte[] Payload;
        public int Priority;
    }

    // =========================================================================
    //  MainForm 分部类
    // =========================================================================
    public partial class MainForm
    {
        // ── 轮询/写表 ─────────────────────────────────────────────────────────
        // 新增 SubID：只需在这里加一行，其他代码不用动。
        private static readonly PollEntry[] _pollTable = new PollEntry[]
        {
            // 周期写：CtrlIn，10ms，挂硬实时定时器，最高优先级
            new PollEntry { Sub=TcSubId.TcLCS_CtrlIn,  PeriodMs=10,   Mode=PollMode.PeriodWrite, Timer=TimerType.HiRes, Priority=0 },

            // 差分写：参数写，硬实时检测（10ms检测周期），高优先级
            new PollEntry { Sub=TcSubId.CLSModel,       PeriodMs=10,   Mode=PollMode.DiffWrite,   Timer=TimerType.HiRes, Priority=1 },
            new PollEntry { Sub=TcSubId.CLSParam,       PeriodMs=10,   Mode=PollMode.DiffWrite,   Timer=TimerType.HiRes, Priority=1 },
            new PollEntry { Sub=TcSubId.CLS5K,          PeriodMs=10,   Mode=PollMode.DiffWrite,   Timer=TimerType.HiRes, Priority=1 },
            new PollEntry { Sub=TcSubId.CLSConsts,      PeriodMs=10,   Mode=PollMode.DiffWrite,   Timer=TimerType.HiRes, Priority=1 },
            new PollEntry { Sub=TcSubId.TestMDL,        PeriodMs=10,   Mode=PollMode.DiffWrite,   Timer=TimerType.HiRes, Priority=1 },
            new PollEntry { Sub=TcSubId.CLSEnum,        PeriodMs=10,   Mode=PollMode.DiffWrite,   Timer=TimerType.HiRes, Priority=1 },
            new PollEntry { Sub=TcSubId.XT,             PeriodMs=10,   Mode=PollMode.DiffWrite,   Timer=TimerType.HiRes, Priority=1 },
            new PollEntry { Sub=TcSubId.YT,             PeriodMs=10,   Mode=PollMode.DiffWrite,   Timer=TimerType.HiRes, Priority=1 },

            // 只读：CtrlOut，10ms，挂硬实时
            new PollEntry { Sub=TcSubId.TcLCS_CtrlOut,  PeriodMs=10,   Mode=PollMode.ReadOnly,    Timer=TimerType.HiRes, Priority=99 },

            // 只读：全部参数，1s，软定时
            new PollEntry { Sub=TcSubId.ALL,             PeriodMs=1000, Mode=PollMode.ReadOnly,    Timer=TimerType.Soft,  Priority=99 },

            // 只读：配置，2s，软定时
            new PollEntry { Sub=TcSubId.DeviceInfo,      PeriodMs=2000, Mode=PollMode.ReadOnly,    Timer=TimerType.Soft,  Priority=99 },
            new PollEntry { Sub=TcSubId.UdpDataCfg,      PeriodMs=2000, Mode=PollMode.ReadOnly,    Timer=TimerType.Soft,  Priority=99 },
            new PollEntry { Sub=TcSubId.UdpParamCfg,     PeriodMs=2000, Mode=PollMode.ReadOnly,    Timer=TimerType.Soft,  Priority=99 },
        };

        // ── 写队列（优先级队列，线程安全）────────────────────────────────────
        // 用 ConcurrentQueue 存，消费时按 Priority 排序后取最高优先级
        private readonly ConcurrentQueue<WriteJob> _writeQueue = new ConcurrentQueue<WriteJob>();
        private int _writeRunning = 0;  // 1=写队列消费循环运行中

        // ── 软定时轮询 CTS ────────────────────────────────────────────────────
        private CancellationTokenSource _pollCts;
        private Task _pollTask;
        private Task _writeTask;

        // =========================================================================
        //  mmTimer1 中的硬实时触发（增加到 mmTimer1_Ticked 末尾，见第三节）
        // =========================================================================
        private void OnHiResTick()
        {
            if (ParamUdpClient.Instance == null) return;

            foreach (var e in _pollTable)
            {
                if (e.Timer != TimerType.HiRes) continue;

                e.Countdown -= 10;  // 每次 tick = 10ms
                if (e.Countdown > 0) continue;
                e.Countdown = e.PeriodMs;

                switch (e.Mode)
                {
                    case PollMode.PeriodWrite:
                        EnqueueWrite(e.Sub, e.Priority);
                        break;

                    case PollMode.DiffWrite:
                        if (HasChanged(e.Sub))
                            EnqueueWrite(e.Sub, e.Priority);
                        break;

                    case PollMode.ReadOnly:
                        // 读操作 fire-and-forget，不阻塞 mmTimer1
                        _ = PollReadOnceAsync(e.Sub);
                        break;
                }
            }
        }

        // =========================================================================
        //  差分检测：序列化源变量 vs 快照，字节级对比
        // =========================================================================
        private static bool HasChanged(TcSubId sub)
        {
            switch (sub)
            {
                case TcSubId.CLSModel:
                    return !StructBytesEqual(ParamData.CLS_Model, ParamData.Snap.CLS_Model);
                case TcSubId.CLSParam:
                    return !StructBytesEqual(ParamData.CLS_Param, ParamData.Snap.CLS_Param);
                case TcSubId.CLS5K:
                    return !StructBytesEqual(ParamData.CLS_5K, ParamData.Snap.CLS_5K);
                case TcSubId.CLSConsts:
                    return !StructBytesEqual(ParamData.CLS_Consts, ParamData.Snap.CLS_Consts);
                case TcSubId.TestMDL:
                    return !StructBytesEqual(ParamData.Test_MDL, ParamData.Snap.Test_MDL);
                case TcSubId.CLSEnum:
                    return !StructBytesEqual(ParamData.CLS_Enum, ParamData.Snap.CLS_Enum);
                case TcSubId.XT:
                    return !StructBytesEqual(ParamData.Param_XT, ParamData.Snap.Param_XT);
                case TcSubId.YT:
                    return !StructBytesEqual(ParamData.Param_YT, ParamData.Snap.Param_YT);
                default:
                    return false;
            }
        }

        private static bool StructBytesEqual(object a, object b)
        {
            byte[] ba = Struct_Func.StructToBytes(a);
            byte[] bb = Struct_Func.StructToBytes(b);
            if (ba.Length != bb.Length) return false;
            for (int i = 0; i < ba.Length; i++)
                if (ba[i] != bb[i]) return false;
            return true;
        }

        // =========================================================================
        //  入写队列
        // =========================================================================
        private void EnqueueWrite(TcSubId sub, int priority)
        {
            byte[] payload;
            switch (sub)
            {
                case TcSubId.TcLCS_CtrlIn:
                    // 周期写：直接序列化源变量
                    lock (ParamData.LockCtrlIn)
                        payload = Struct_Func.StructToBytes(ParamData.CtrlIn);
                    break;
                case TcSubId.CLSModel: payload = Struct_Func.StructToBytes(ParamData.CLS_Model); break;
                case TcSubId.CLSParam: payload = Struct_Func.StructToBytes(ParamData.CLS_Param); break;
                case TcSubId.CLS5K: payload = Struct_Func.StructToBytes(ParamData.CLS_5K); break;
                case TcSubId.CLSConsts: payload = Struct_Func.StructToBytes(ParamData.CLS_Consts); break;
                case TcSubId.TestMDL: payload = Struct_Func.StructToBytes(ParamData.Test_MDL); break;
                case TcSubId.CLSEnum: payload = Struct_Func.StructToBytes(ParamData.CLS_Enum); break;
                case TcSubId.XT: payload = Struct_Func.StructToBytes(ParamData.Param_XT); break;
                case TcSubId.YT: payload = Struct_Func.StructToBytes(ParamData.Param_YT); break;
                default: return;
            }
            _writeQueue.Enqueue(new WriteJob { Sub = sub, Payload = payload, Priority = priority });
        }

        // =========================================================================
        //  写队列消费循环（后台 Task，串行）
        // =========================================================================
        private async Task WriteQueueLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                // 收集当前队列中所有任务，按优先级排序后取最高
                var batch = new List<WriteJob>();
                while (_writeQueue.TryDequeue(out var job))
                    batch.Add(job);

                if (batch.Count == 0)
                {
                    try { await Task.Delay(1, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                    continue;
                }

                // 按优先级升序，取最高优先级（Priority 值最小）的第一个
                batch.Sort((a, b) => a.Priority.CompareTo(b.Priority));

                // 同优先级去重：同一 SubID 只发最新一条（取 batch 最后一条）
                var deduped = new Dictionary<TcSubId, WriteJob>();
                foreach (var j in batch)
                    deduped[j.Sub] = j;  // 后来的覆盖先来的

                // 再按优先级排序后串行发
                var toSend = deduped.Values.OrderBy(j => j.Priority).ToList();

                foreach (var job in toSend)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        var resp = await ParamUdpClient.Instance
                            .WriteAsync(job.Sub, job.Payload)
                            .ConfigureAwait(false);

                        if (!IsErrFrame(resp, $"Write_{job.Sub}"))
                        {
                            // 写成功 → 更新快照
                            UpdateSnap(job.Sub, job.Payload);
                            Debug.WriteLine($"[Write] {job.Sub} ok ✅");
                        }
                        else
                        {
                            Debug.WriteLine($"[Write] {job.Sub} ERR, snap not updated");
                        }
                    }
                    catch (TimeoutException)
                    {
                        Debug.WriteLine($"[Write] {job.Sub} timeout, will retry next diff");
                        // 不更新快照 → 下次 diff 检测仍有差异 → 自动重试
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Write] {job.Sub} error: {ex.Message}");
                    }
                }
            }
            Debug.WriteLine("[WriteQueue] loop stopped");
        }

        // =========================================================================
        //  写成功后更新快照
        // =========================================================================
        private static void UpdateSnap(TcSubId sub, byte[] payload)
        {
            switch (sub)
            {
                case TcSubId.CLSModel:
                    ParamData.Snap.CLS_Model = (ST_CLSModel)Struct_Func.BytesToStruct(payload, new ST_CLSModel()); break;
                case TcSubId.CLSParam:
                    ParamData.Snap.CLS_Param = (ST_CLSParam)Struct_Func.BytesToStruct(payload, new ST_CLSParam()); break;
                case TcSubId.CLS5K:
                    ParamData.Snap.CLS_5K = (ST_CLS5K)Struct_Func.BytesToStruct(payload, new ST_CLS5K()); break;
                case TcSubId.CLSConsts:
                    ParamData.Snap.CLS_Consts = (ST_CLSConsts)Struct_Func.BytesToStruct(payload, new ST_CLSConsts()); break;
                case TcSubId.TestMDL:
                    ParamData.Snap.Test_MDL = (ST_TestMDL)Struct_Func.BytesToStruct(payload, new ST_TestMDL()); break;
                case TcSubId.CLSEnum:
                    ParamData.Snap.CLS_Enum = (ST_CLSEnum)Struct_Func.BytesToStruct(payload, new ST_CLSEnum()); break;
                case TcSubId.XT:
                    ParamData.Snap.Param_XT = (ST_XT)Struct_Func.BytesToStruct(payload, new ST_XT()); break;
                case TcSubId.YT:
                    ParamData.Snap.Param_YT = (ST_YT)Struct_Func.BytesToStruct(payload, new ST_YT()); break;
                case TcSubId.TcLCS_CtrlIn:
                    // 周期写不维护快照（每次都发）
                    break;
            }
        }

        // =========================================================================
        //  读操作（fire-and-forget，供 mmTimer1 硬实时触发）
        // =========================================================================
        private async Task PollReadOnceAsync(TcSubId sub)
        {
            var client = ParamUdpClient.Instance;
            if (client == null) return;
            try
            {
                var resp = await client.ReadAsync(sub).ConfigureAwait(false);
                if (!IsErrFrame(resp, $"Read_{sub}"))
                {
                    ParamData.TryDeserialize(resp);
                    // 只读参数读回后，同步快照（只读参数的快照不参与差分，此处为了只读回滚）
                    SyncSnapFromSource(sub);
                }
            }
            catch (TimeoutException) { Debug.WriteLine($"[Read] timeout sub={sub}"); }
            catch (Exception ex) { Debug.WriteLine($"[Read] error sub={sub}: {ex.Message}"); }
        }

        /// <summary>只读参数读回后将快照与源变量对齐（防止只读参数触发差分写）</summary>
        private static void SyncSnapFromSource(TcSubId sub)
        {
            // ALL 或任意只读帧读回后，把 DiffWrite 变量的快照同步到源变量
            // 防止轮询读覆盖源变量后，快照与源产生虚假差异触发误写
            if (sub == TcSubId.ALL || sub == TcSubId.XT || sub == TcSubId.YT
                || sub == TcSubId.CLSModel || sub == TcSubId.CLSParam
                || sub == TcSubId.CLS5K || sub == TcSubId.CLSConsts
                || sub == TcSubId.TestMDL || sub == TcSubId.CLSEnum)
            {
                SyncAllDiffSnaps();
            }
        }

        /// <summary>将所有 DiffWrite 变量的快照与源变量对齐（读回后调用）</summary>
        private static void SyncAllDiffSnaps()
        {
            ParamData.Snap.CLS_Model = ParamData.CLS_Model;
            ParamData.Snap.CLS_Param = ParamData.CLS_Param;
            ParamData.Snap.CLS_5K = ParamData.CLS_5K;
            ParamData.Snap.CLS_Consts = ParamData.CLS_Consts;
            ParamData.Snap.Test_MDL = ParamData.Test_MDL;
            ParamData.Snap.CLS_Enum = ParamData.CLS_Enum;
            ParamData.Snap.Param_XT = ParamData.Param_XT;
            ParamData.Snap.Param_YT = ParamData.Param_YT;
        }

        // =========================================================================
        //  软定时轮询（Task.Delay，处理低频只读参数）
        // =========================================================================
        private async Task SoftPollLoopAsync(CancellationToken ct)
        {
            var entries = _pollTable.Where(e => e.Timer == TimerType.Soft).ToArray();
            foreach (var e in entries) e.Countdown = 0;

            const int TICK_MS = 100;  // 软定时 tick = 100ms，低频参数无需更精细

            while (!ct.IsCancellationRequested)
            {
                foreach (var e in entries)
                {
                    e.Countdown -= TICK_MS;
                    if (e.Countdown > 0) continue;
                    e.Countdown = e.PeriodMs;
                    _ = PollReadOnceAsync(e.Sub);
                }
                try { await Task.Delay(TICK_MS, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        // =========================================================================
        //  启动/停止（由 StartParamUdpAsync / StopParamUdp 调用）
        // =========================================================================
        private void StartPollAndWrite()
        {
            foreach (var e in _pollTable) e.Countdown = 0;

            _pollCts = new CancellationTokenSource();
            _pollTask = Task.Run(() => SoftPollLoopAsync(_pollCts.Token));
            _writeTask = Task.Run(() => WriteQueueLoopAsync(_pollCts.Token));
        }

        private void StopPollAndWrite()
        {
            try { _pollCts?.Cancel(); } catch { }
            try { _pollTask?.Wait(200); } catch { }
            try { _writeTask?.Wait(200); } catch { }
            _pollCts = null;
            _pollTask = null;
            _writeTask = null;
        }
    }
}
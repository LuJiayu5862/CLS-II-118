// ============================================================================
//  MainForm.ParamUDP.cs  —  TcLCS-Param 通道的 MainForm 高层包装
//
//  2026-05-11：ParamUdpClient 已单例化，本文件不再持有 _param 字段。
//              生命周期由 ConnectDevice() / DisconnectDevice() 管理。
// ============================================================================
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CLS_II
{
    public partial class MainForm
    {
        // 工具方法：检查响应是否是 ERR 帧，是则打印错误码
        private bool IsErrFrame(TcFrame resp, string context)
        {
            if (resp.Header.Cmd != TcCmd.ERR) return false;
            TcStatus code = resp.Payload.Length > 0 ? (TcStatus)resp.Payload[0] : TcStatus.INTERNAL;
            Debug.WriteLine($"[Param] ERR in {context}: {code}");
            return true;
        }

        /// <summary>
        /// 启动 Param 通道并执行 HELLO 握手。由 ConnectDevice() 调用。
        /// </summary>
        private async Task StartParamUdpAsync()
        {
            if (ParamUdpClient.Instance != null) return;

            var c = ParamUdpClient.StartInstance(
                ParamConsts.szParamRemoteHost,
                ParamConsts.nParamPortSend,
                ParamConsts.nParamPortRecv,
                ParamConsts.byParamDeviceId);

            c.OnLog += msg => Debug.WriteLine(msg);
            c.OnFrameError += (st, buf) => Debug.WriteLine($"[Param] frame err: {st}");
            c.OnUnsolicited += f => Debug.WriteLine($"[Param] unsolicited sub={f.Header.SubId} cmd={f.Header.Cmd}");

            try
            {
                await c.HelloAsync().ConfigureAwait(false);
                Debug.WriteLine("[Param] HELLO ok ✅");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Param] HELLO failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止 Param 通道。由 DisconnectDevice() 调用。
        /// </summary>
        private void StopParamUdp()
        {
            ParamUdpClient.StopInstance();
        }

        // ===== 高层 API 包装（供 UI / 业务层调用）=====

        public Task<TcFrame> ParamReadAsync(TcSubId sub)
            => ParamUdpClient.Instance?.ReadAsync(sub)
               ?? Task.FromException<TcFrame>(
                   new InvalidOperationException("Param channel not running"));

        public Task<TcFrame> ParamWriteAsync(TcSubId sub, byte[] payload)
            => ParamUdpClient.Instance?.WriteAsync(sub, payload)
               ?? Task.FromException<TcFrame>(
                   new InvalidOperationException("Param channel not running"));

        public Task<TcFrame> ParamPingAsync()
            => ParamUdpClient.Instance?.PingAsync()
               ?? Task.FromException<TcFrame>(
                   new InvalidOperationException("Param channel not running"));

        public Task<TcFrame> ParamSavePersistAsync()
            => ParamUdpClient.Instance?.SavePersistAsync()
               ?? Task.FromException<TcFrame>(
                   new InvalidOperationException("Param channel not running"));
    }
}
// ============================================================================
//  MainForm.ParamUDP.cs  —  MainForm 部分类：TcLCS-UDP v1.1 集成层
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CLS_II
{
    public partial class MainForm
    {
        private ParamUdpClient _param;

        /// <summary>目标设备 ID（默认 0x01；广播请求可用 0xFF）</summary>
        public byte ParamDeviceId { get; set; } = 0x01;

        private async void StartParamUdp()
        {
            if (_param != null) return;
            _param = new ParamUdpClient(
                serverHost: ParamConsts.szParamRemoteHost, // 192.168.118.118
                serverPort: ParamConsts.nParamPortSend,    // 5050 (server)
                localRecvPort: ParamConsts.nParamPortRecv,    // 8080 (local)
                deviceId: ParamDeviceId);

            _param.OnLog += s => System.Diagnostics.Debug.WriteLine(s);
            _param.OnUnsolicited += OnParamUnsolicited;
            _param.OnFrameError += (st, raw) =>
                System.Diagnostics.Debug.WriteLine($"[Param] parse err {st}, raw={BitConverter.ToString(raw)}");

            _param.Start();

            // 协议 §5.3：任何业务前必须先 HELLO
            try
            {
                var ack = await _param.HelloAsync();
                System.Diagnostics.Debug.WriteLine($"[Param] HELLO_ACK status={(ack.Payload.Length > 0 ? ack.Payload[0] : 0xFF):X2}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Param] HELLO failed: {ex.Message}");
            }
        }

        private void StopParamUdp()
        {
            if (_param == null) return;
            _param.Stop();
            _param.Dispose();
            _param = null;
        }

        // ---- 高层便捷 API ----
        public Task<TcFrame> ParamReadAsync(TcSubId sub)
            => _param?.ReadAsync(sub) ?? Task.FromException<TcFrame>(new InvalidOperationException("Param not started"));

        public Task<TcFrame> ParamWriteAsync(TcSubId sub, byte[] payload)
            => _param?.WriteAsync(sub, payload) ?? Task.FromException<TcFrame>(new InvalidOperationException("Param not started"));

        public Task<TcFrame> ParamPingAsync()
            => _param?.PingAsync() ?? Task.FromException<TcFrame>(new InvalidOperationException("Param not started"));

        public Task<TcFrame> ParamSavePersistAsync()
            => _param?.SavePersistAsync() ?? Task.FromException<TcFrame>(new InvalidOperationException("Param not started"));

        private void OnParamUnsolicited(TcFrame f)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Param] unsolicited cmd={f.Header.Cmd} sub={f.Header.SubId} seq={f.Header.SeqNo} len={f.Payload.Length}");
        }
    }
}
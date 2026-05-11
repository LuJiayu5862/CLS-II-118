// ============================================================================
//  MainForm.JdUDP.cs  —  MainForm 部分类：JD-61101 集成层
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
        private JdUdpClient? _jd;

        /// <summary>最近一次收到的脚蹬状态</summary>
        public JdRxFrame? LastJdRx { get; private set; }

        private void StartJdUdp()
        {
            if (_jd != null) return;
            _jd = new JdUdpClient(
                remoteHost: JdConsts.szJdRemoteHost,
                remotePort: JdConsts.nJdPortSend,  // 到设备 15000
                localRecvPort: JdConsts.nJdPortRecv); // 本地 16000

            _jd.OnRx += OnJdRx;
            _jd.OnRxError += OnJdRxError;
            _jd.OnLog += OnJdLog;
            _jd.Start();
        }

        private void StopJdUdp()
        {
            if (_jd == null) return;
            _jd.OnRx -= OnJdRx;
            _jd.OnRxError -= OnJdRxError;
            _jd.OnLog -= OnJdLog;
            _jd.Stop();
            _jd.Dispose();
            _jd = null;
        }

        private void OnJdRx(JdRxFrame f)
        {
            LastJdRx = f;
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(new Action(() => OnJdRxUi(f)));
        }

        /// <summary>UI 线程：更新 Jd 状态显示（由主 MainForm 扩展）</summary>
        partial void OnJdRxUi(JdRxFrame f);

        private void OnJdRxError(string reason, byte[] raw)
        {
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(new Action(() =>
                {
                    // 可转发到日志面板；默认输出到 Debug
                    System.Diagnostics.Debug.WriteLine($"[Jd] parse err: {reason}, raw={BitConverter.ToString(raw)}");
                }));
        }

        private void OnJdLog(string msg) =>
            System.Diagnostics.Debug.WriteLine(msg);

        /// <summary>便捷：发送清除故障码</summary>
        public void JdSendClearFault() => _jd?.SendClearFault();

        /// <summary>便捷：发送脚蹬复位回中立位</summary>
        public void JdSendResetPedal() => _jd?.SendResetPedal();
    }
}
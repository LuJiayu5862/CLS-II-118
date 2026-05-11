// MainForm.JdUDP.cs
// MainForm 分部类 — JD-61101 UDP 集成层
using System;
using System.Windows.Forms;
using CLS_II.src_IOData;
using CLS_II.src_communication;

namespace CLS_II
{
    public partial class MainForm : Form
    {
        // ── JD-61101 UDP 客户端实例
        private JdUdpClient _jdUdp;

        // ── 最近一次脚蹬数据（供 UI 刷新使用）
        private JdTxFrame _lastJdFrame;

        /// <summary>启动 JD-61101 UDP 接收/发送</summary>
        private void StartJdUdp()
        {
            if (_jdUdp != null && _jdUdp.IsRunning) return;
            _jdUdp = new JdUdpClient();
            _jdUdp.OnPedalUpdate += OnJdPedalUpdate;
            _jdUdp.Start();
            AppendJdLog("JD-61101 UDP 已启动");
        }

        /// <summary>停止 JD-61101 UDP</summary>
        private void StopJdUdp()
        {
            _jdUdp?.OnPedalUpdate -= OnJdPedalUpdate;
            _jdUdp?.Stop();
            _jdUdp?.Dispose();
            _jdUdp = null;
            AppendJdLog("JD-61101 UDP 已停止");
        }

        /// <summary>
        /// 脚蹬数据回调（在接收线程触发，需 Invoke 到 UI 线程）
        /// </summary>
        private void OnJdPedalUpdate(JdTxFrame frame)
        {
            _lastJdFrame = frame;
            if (InvokeRequired)
                BeginInvoke(new Action(() => RefreshJdDisplay(frame)));
            else
                RefreshJdDisplay(frame);
        }

        /// <summary>刷新 UI 显示（UI 线程执行）</summary>
        private void RefreshJdDisplay(JdTxFrame frame)
        {
            // TODO: 将 frame.PedalPos / frame.Status 绑定到界面控件
            // 示例（控件名请按实际替换）：
            // lblPedalPos.Text = frame.PedalPos.ToString();
            // lblJdStatus.Text = frame.Status == 0x00 ? "正常" : $"故障 0x{frame.Status:X2}";
            AppendJdLog($"脚蹬位移={frame.PedalPos}  状态=0x{frame.Status:X2}  " +
                        $"RX总计={_jdUdp?.RxCount}  错误={_jdUdp?.RxError}");
        }

        /// <summary>发送故障复位指令</summary>
        private void SendJdClearFault()
        {
            _jdUdp?.Send(JdRxFrame.ClearFaultFrame());
            AppendJdLog("已发送：故障复位（DATA5=0xAA）");
        }

        /// <summary>发送负荷复位指令（回中立位）</summary>
        private void SendJdResetPedal()
        {
            _jdUdp?.Send(JdRxFrame.ResetPedalFrame());
            AppendJdLog("已发送：操纵负荷复位（DATA7=0xAA）");
        }

        /// <summary>发送正常帧（保持在线心跳用）</summary>
        private void SendJdNormal()
        {
            _jdUdp?.Send(JdRxFrame.Normal());
        }

        /// <summary>日志输出（预留，绑定到 ListBox 或 RichTextBox）</summary>
        private void AppendJdLog(string msg)
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [JD] {msg}";
            System.Diagnostics.Debug.WriteLine(line);
            // TODO: 绑定到 UI 日志控件，示例：
            // if (lstJdLog.InvokeRequired)
            //     lstJdLog.BeginInvoke(new Action(() => lstJdLog.Items.Add(line)));
            // else
            //     lstJdLog.Items.Add(line);
        }
    }
}

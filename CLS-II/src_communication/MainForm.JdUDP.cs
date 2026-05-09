using System;
using System.Windows.Forms;

namespace CLS_II
{
    // ============================================================
    //  MainForm  partial — JD-61101 通道
    //  依赖：JdData.cs / JdUdpClient.cs / JdConsts (GlobalVar.cs)
    //  与旧 UDP（MainForm.UDP.cs）完全独立，互不干扰
    // ============================================================

    public partial class MainForm
    {
        private JdUdpClient _jdUdpClient;

        // ── 初始化 ────────────────────────────────────────────────
        private void InitJdUDP()
        {
            if (JdConsts.isJdUdpConnected) return;
            try
            {
                _jdUdpClient = new JdUdpClient(
                    JdConsts.szJdRemoteHost,
                    JdConsts.nJdPortSend,
                    JdConsts.nJdPortRecv);
                _jdUdpClient.FrameReceived += JdClient_FrameReceived;
                _jdUdpClient.ErrorOccurred += JdClient_ErrorOccurred;
                _jdUdpClient.Start();
                JdConsts.isJdUdpConnected = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("JD UDP init failed: " + ex.Message);
            }
        }

        // ── 释放 ──────────────────────────────────────────────────
        private void DisposeJdUDP()
        {
            if (!JdConsts.isJdUdpConnected) return;
            JdConsts.isJdUdpConnected = false;
            _jdUdpClient?.Stop();
            _jdUdpClient = null;
        }

        // ── 发送指令（外部调用） ───────────────────────────────────
        /// <summary>
        /// 发送 JD 控制指令
        /// cmdCode: 0=Idle, 1=Reset, 2=Enable, 3=Disable
        /// </summary>
        public void JdSendCommand(UInt32 cmdCode, float setPoint = 0f)
        {
            if (!JdConsts.isJdUdpConnected) return;
            lock (JdData.Command)
            {
                JdData.Command.cmdId++;
                JdData.Command.cmdCode  = cmdCode;
                JdData.Command.setPoint = setPoint;
                _jdUdpClient?.Send(JdData.Command);
            }
        }

        // ── 收帧回调 ──────────────────────────────────────────────
        private void JdClient_FrameReceived(object sender, JdFrameArgs e)
        {
            lock (JdData.Feedback)
            {
                JdData.Feedback         = e.Frame;
                JdData.LastReceivedTime = DateTime.Now;
            }
            // TODO: 新窗口完成后取消注释
            // if (IsHandleCreated)
            //     BeginInvoke(new Action(() => _jdTestForm?.RefreshData()));
        }

        // ── 错误回调 ──────────────────────────────────────────────
        private void JdClient_ErrorOccurred(object sender, Exception ex)
        {
            if (IsHandleCreated)
                BeginInvoke(new Action(() =>
                    MessageBox.Show(ex.Message, "JD UDP Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)));
        }
    }
}

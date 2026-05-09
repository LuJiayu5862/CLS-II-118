using System;
using System.Windows.Forms;

namespace CLS_II
{
    // ============================================================
    //  MainForm  partial — TcLCS v1.1 参数通道
    //  依赖：ParamData.cs / ParamUdpClient.cs / ParamConsts (GlobalVar.cs)
    //  与旧 UDP（MainForm.UDP.cs）完全独立，互不干扰
    // ============================================================

    public partial class MainForm
    {
        private ParamUdpClient _paramUdpClient;

        // ── 初始化 ────────────────────────────────────────────────
        private void InitParamUDP()
        {
            if (ParamConsts.isParamUdpConnected) return;
            try
            {
                ParamData.Init(); // 预分配缓存
                _paramUdpClient = new ParamUdpClient(
                    ParamConsts.szParamRemoteHost,
                    ParamConsts.nParamPortSend,
                    ParamConsts.nParamPortRecv);
                _paramUdpClient.ResponseReceived += ParamClient_ResponseReceived;
                _paramUdpClient.ErrorOccurred    += ParamClient_ErrorOccurred;
                _paramUdpClient.Start();
                ParamConsts.isParamUdpConnected = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Param UDP init failed: " + ex.Message);
            }
        }

        // ── 释放 ──────────────────────────────────────────────────
        private void DisposeParamUDP()
        {
            if (!ParamConsts.isParamUdpConnected) return;
            ParamConsts.isParamUdpConnected = false;
            _paramUdpClient?.Stop();
            _paramUdpClient = null;
        }

        // ── 外部调用：GET 单个参数 ─────────────────────────────────
        public void ParamGetRequest(ParamSubId subId, int channelId)
        {
            if (!ParamConsts.isParamUdpConnected) return;
            _paramUdpClient?.SendGetRequest(subId, channelId);
        }

        // ── 外部调用：SET 单个参数 ─────────────────────────────────
        public void ParamSetRequest(ParamSubId subId, int channelId, float value)
        {
            if (!ParamConsts.isParamUdpConnected) return;
            ParamData.SetDirty(subId, channelId, value);
            _paramUdpClient?.SendSetRequest(subId, channelId, value);
        }

        // ── 收帧回调 ──────────────────────────────────────────────
        private void ParamClient_ResponseReceived(object sender, ParamRspArgs e)
        {
            // 解析 GET_RSP / SET_RSP：Payload = SubId(2B) + ChId(2B) + Value(4B)
            if (e.Header.MsgType == ParamMsgType.GET_RSP ||
                e.Header.MsgType == ParamMsgType.SET_RSP)
            {
                if (e.Payload.Length >= 8)
                {
                    var   sid = (ParamSubId)BitConverter.ToUInt16(e.Payload, 0);
                    int   ch  = BitConverter.ToUInt16(e.Payload, 2) - 1; // 转0-based
                    float val = BitConverter.ToSingle(e.Payload, 4);
                    ParamData.UpdateFromResponse(sid, ch, val);
                }
            }
            // TODO: 新参数窗口完成后取消注释
            // if (IsHandleCreated)
            //     BeginInvoke(new Action(() => _paramTestForm?.RefreshData()));
        }

        // ── 错误回调 ──────────────────────────────────────────────
        private void ParamClient_ErrorOccurred(object sender, Exception ex)
        {
            if (IsHandleCreated)
                BeginInvoke(new Action(() =>
                    MessageBox.Show(ex.Message, "Param UDP Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)));
        }
    }
}

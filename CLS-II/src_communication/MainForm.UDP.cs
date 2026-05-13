using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using UDP;

namespace CLS_II
{
    public partial class MainForm
    {
        string szRemoteHost = "127.0.0.1";
        private int nPortIn = 1703, nPortOut1 = 1702, nPortOut2 = 1704;
        private UDPClient udpClient;

        public void SetDefaultRemoteHost(string remoteHost, int receivePort, int controlPort, int paramPort)
        {
            this.szRemoteHost = remoteHost;
            this.nPortIn = receivePort;
            this.nPortOut1 = controlPort;
            this.nPortOut2 = paramPort;
        }

        private void InitUDP()
        {
            if (!GlobalVar.isUdpConnceted)
            {
                try
                {
                    udpClient = new UDPClient(szRemoteHost, nPortOut1, nPortIn, 2048);
                    udpClient.onError += new UDPClient.ErrorEventHandler(client_onError);
                    udpClient.onReceived += new UDPClient.ReceivedEventHandler(client_onReceived);
                    GlobalVar.isUdpConnceted = true;
                }
                catch (Exception err)
                {
                    throw new InvalidOperationException(err.Message);
                }
            }

        }

        private void DisposeUDP()
        {
            udpClient.CleanUp();
            udpClient = null;
        }

        private void client_onError(object sender, UDPClient.ErrorEventArgs e)
        {
            MessageBox.Show(e.Ex.Message, "Error");
        }

        private void client_onReceived(object sender, UDPClient.ReceivedEventArgs e)
        {
            int len = e.MessageByte.Length;
            int nlen = Marshal.SizeOf(UdpData.LCSInfos);
            byte[] data = new byte[nlen];
            e.MessageByte.CopyTo(data, 0);

            if (len == nlen)
            {
                lock (UdpData.LCSInfos)
                {
                    UdpData.LCSInfos = (_LCSInfos)Struct_Func.BytesToStruct(data, UdpData.LCSInfos);
                }
            }
        }
    }
}

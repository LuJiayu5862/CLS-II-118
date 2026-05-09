using INIFileRW;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace CLS_II
{
    public partial class DeviceInfo : Form
    {
        private bool isInited = false;

        public DeviceInfo()
        {
            InitializeComponent();
        }

        private void ControlSizeLoad()
        {
            ucSplitLine_H1.Width = this.Width - 3;
            Size size = ucSplitLabel1.MinimumSize;
            size.Width = this.Width - 3;
            ucSplitLabel1.MinimumSize = size;
            ucSplitLabel2.MinimumSize = size;
        }

        private void DeviceInfo_Load(object sender, EventArgs e)
        {
            ControlSizeLoad();
            textBox1.Text = GlobalVar.DeviceName;
            textBox2.Text = GlobalVar.szRemoteHost;
            textBox4.Text = CLSConsts.EnabledChannels.ToString();
            timer1.Enabled = true;
            timer1.Start();
            isInited = true;
        }

        private void DeviceInfo_SizeChanged(object sender, EventArgs e)
        {
            ControlSizeLoad();
        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {
            if (isInited)
            {
                if (RegexMatch.isPositiveInteger(textBox4.Text))
                {
                    int i = int.Parse(textBox4.Text);
                    ((TextBox)sender).BackColor = (i >= 1 && i <= 10) ? Color.PaleGreen : Color.Yellow;
                }
                else
                {
                    ((TextBox)sender).BackColor = Color.Yellow;
                }
            }
        }

        private void textBox4_KeyDown(object sender, KeyEventArgs e)
        {
            if (!isInited || GlobalVar.isUdpConnceted) return;
            if (e.KeyCode == Keys.Enter)
            {
                ((TextBox)sender).BackColor = SystemColors.Window;
                if (RegexMatch.isPositiveInteger(textBox4.Text))
                {
                    int i = int.Parse(textBox4.Text);
                    if (i >= 1 && i <= 10)
                    {
                        CLSConsts.EnabledChannels = i;
                        GlobalVar.isProjectFileChanged = true;
                    }
                    else
                        textBox4.Text = CLSConsts.EnabledChannels.ToString();
                }
                else
                    textBox4.Text = CLSConsts.EnabledChannels.ToString();
            }
        }

        private void textBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = GlobalVar.isUdpConnceted;
        }

        private void DeviceInfo_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer1.Stop();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            // Reserved for future UDP status display
        }
    }
}
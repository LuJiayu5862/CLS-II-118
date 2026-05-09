using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CLS_II
{
    public partial class ProjectInfo : Form
    {
        public ProjectInfo()
        {
            InitializeComponent();
        }

        private void ProjectInfo_Load(object sender, EventArgs e)
        {
            ControlSizeLoad();
            textBox2.Text = GlobalVar.ProjectFile;
            string ProjectName = Path.GetFileName(GlobalVar.ProjectFile);
            ProjectName = ProjectName.Substring(0, ProjectName.LastIndexOf("."));
            textBox1.Text = ProjectName;
            textBox3.Text = GlobalVar.DeviceName;
            textBox3.BackColor = SystemColors.Window;
            textBox4.Text = GlobalVar.szRemoteHost;
            textBox4.BackColor = SystemColors.Window;
            textBox5.Text = string.Empty;  // AmsNetID removed
            textBox5.BackColor = SystemColors.Window;
        }

        private void ControlSizeLoad()
        {
            ucSplitLine_H1.Width = this.Width - 3;
            Size size = ucSplitLabel1.MinimumSize;
            size.Width = this.Width - 3;
            ucSplitLabel1.MinimumSize = size;
            size.Height = ucSplitLabel2.Height;
            ucSplitLabel2.MinimumSize = size;
            size.Height = ucSplitLabel3.Height;
            ucSplitLabel3.MinimumSize = size;
        }

        private void ProjectInfo_SizeChanged(object sender, EventArgs e)
        {
            ControlSizeLoad();
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            ((TextBox)sender).BackColor = Color.PaleGreen;
        }

        private void textBox3_KeyDown(object sender, KeyEventArgs e)
        {
            if (GlobalVar.isUdpConnceted) return;
            if (e.KeyCode == Keys.Enter)
            {
                ((TextBox)sender).BackColor = SystemColors.Window;
                if (string.IsNullOrEmpty(textBox3.Text))
                    textBox3.Text = GlobalVar.DeviceName;
                else
                {
                    GlobalVar.DeviceName = textBox3.Text;
                    GlobalVar.isProjectFileChanged = true;
                }
            }
        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {
            ((TextBox)sender).BackColor = RegexMatch.isIP(textBox4.Text) ? Color.PaleGreen : Color.Yellow;
        }

        private void textBox5_TextChanged(object sender, EventArgs e)
        {
            // AmsNetID hidden and unused
        }

        private void textBox4_KeyDown(object sender, KeyEventArgs e)
        {
            if (GlobalVar.isUdpConnceted) return;
            if (e.KeyCode == Keys.Enter)
            {
                ((TextBox)sender).BackColor = SystemColors.Window;
                if (RegexMatch.isIP(textBox4.Text))
                {
                    GlobalVar.szRemoteHost = textBox4.Text;
                    GlobalVar.isProjectFileChanged = true;
                }
                else
                    textBox4.Text = GlobalVar.szRemoteHost;
            }
        }

        private void textBox5_KeyDown(object sender, KeyEventArgs e)
        {
            // AmsNetID hidden and unused
        }

        private void textBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = GlobalVar.isUdpConnceted;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Reserved
        }
    }
}
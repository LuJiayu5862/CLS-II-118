using INIFileRW;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CLS_II
{
    public partial class DeviceInfo : Form
    {
        private bool isInited = false;
        private bool isParamChanged = false;

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
                    if (i >= 1 && i <= 10)
                    {
                        ((TextBox)sender).BackColor = Color.PaleGreen;
                    }
                    else
                    {
                        ((TextBox)sender).BackColor = Color.Yellow;
                    }
                }
                else
                {
                    ((TextBox)sender).BackColor = Color.Yellow;
                }
            }
        }

        private void textBox4_KeyDown(object sender, KeyEventArgs e)
        {
            if (isInited)
            {
                if (GlobalVar.isUdpConnceted)
                    return;
                if (e.KeyCode == Keys.Enter)
                {
                    ((TextBox)sender).BackColor = SystemColors.Window;
                    if (RegexMatch.isPositiveInteger(textBox4.Text))
                    {
                        int i = int.Parse(textBox4.Text);
                        if (i >= 1 && i <= 10)
                        {
                            CLSConsts.EnabledChannels = int.Parse(textBox4.Text);
                            TreeViewLoad();
                            GlobalVar.isProjectFileChanged = true;                          
                        }
                        else
                        {
                            textBox4.Text = CLSConsts.EnabledChannels.ToString();
                        }
                    }
                    else
                    {
                        textBox4.Text = CLSConsts.EnabledChannels.ToString();
                    }
                }
            }
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            tabControl1.Visible = true;
            TreeNode node = ((TreeView)sender).SelectedNode;
            if (node != null)
            {
                if (node.Parent == null)
                {
                    tabControl1.SelectedTab = tabPage1;
                    int nodeIndex = (int)((TreeView)sender).SelectedNode.Tag;
                    toolStripStatusLabel1.Text = ((TreeView)sender).SelectedNode.Text;
                    readNodeInfo(nodeIndex);
                }
                else
                {
                    tabControl1.SelectedTab = tabPage2;
                    int parentIndex = (int)((TreeView)sender).SelectedNode.Parent.Tag;
                    int nodeIndex = (int)((TreeView)sender).SelectedNode.Tag;
                    string Title = ((TreeView)sender).SelectedNode.Parent.Text + " - " + ((TreeView)sender).SelectedNode.Text;
                    string SymbolName = ((TreeView)sender).SelectedNode.Text;
                    int SymbolSize = 0;
                    switch (SymbolName)
                    {
                        case "CLSModel":
                            SymbolSize = Marshal.SizeOf(new _CLSModel());
                            break;
                        case "CLSParam":
                            SymbolSize = Marshal.SizeOf(new _CLSParam());
                            break;
                        case "CLS5K":
                            SymbolSize = Marshal.SizeOf(new _CLS5K());
                            break;
                        case "CLSConsts":
                            SymbolSize = Marshal.SizeOf(new _CLSConsts());
                            break;
                        case "TestMDL":
                            SymbolSize = Marshal.SizeOf(new _TestMDL());
                            break;
                    }
                    toolStripStatusLabel1.Text = Title + "(" + SymbolSize + " BYTEs)";
                    readNodeInfo(parentIndex, nodeIndex);
                }
            }
        }

        private void readNodeInfo(int rootIndex,int nodeIndex = -1)
        {
            if (nodeIndex == -1)
            {
                cbxAdsEnabled.SelectedIndex = AdsInfo.AdsObject.AdsPort[rootIndex].Enabled ? 0 : 1;
                tbxAdsName.Text = AdsInfo.AdsObject.AdsPort[rootIndex].Name;
                tbxAdsPort.Text = AdsInfo.AdsObject.AdsPort[rootIndex].Port.ToString();
                tbxAdsName.BackColor = SystemColors.Window;
                tbxAdsPort.BackColor = SystemColors.Window;
            }
            else
            {
                tbxAdsPort2.Text = AdsInfo.AdsObject.AdsPort[rootIndex].Port.ToString();
                tbxIndexGroup.Text = AdsInfo.AdsObject.AdsPort[rootIndex].AdsSymbols[nodeIndex].IndexGroup.ToString();
                tbxHandle.Text = AdsInfo.AdsObject.AdsPort[rootIndex].AdsSymbols[nodeIndex].Handle.ToString();
                //tbxIndexGroup.BackColor = SystemColors.Window;
                tbxHandle.BackColor = SystemColors.Window;
            }
        }

        private void writeNodeInfo(int rootIndex, int nodeIndex = -1)
        {
            if (nodeIndex == -1)
            {
                AdsInfo.AdsObject.AdsPort[rootIndex].Enabled = cbxAdsEnabled.SelectedIndex == 0 ? true : false;
                AdsInfo.AdsObject.AdsPort[rootIndex].Name = tbxAdsName.Text;
                AdsInfo.AdsObject.AdsPort[rootIndex].Port = int.Parse(tbxAdsPort.Text);
            }
            else
            {
                AdsInfo.AdsObject.AdsPort[rootIndex].AdsSymbols[nodeIndex].Handle = int.Parse(tbxHandle.Text);
            }
        }

        private void textBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (GlobalVar.isUdpConnceted)
                e.Handled = true;
            else
                e.Handled = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (GlobalVar.isUdpConnceted)
                return;
            if (sender == btnAdsSymbolSearch)
            {
                AdsSymbolSample dlg = new AdsSymbolSample(toolStripStatusLabel1.Text, GlobalVar.AmsNetID, int.Parse(tbxAdsPort2.Text));
                //dlg.AdsConnect();
                dlg.StartPosition = FormStartPosition.CenterParent;
                DialogResult dr = dlg.ShowDialog();
                if (dr == DialogResult.OK)
                {
                    tbxHandle.Text = dlg.SymbolHandle.ToString();
                    int parentIndex = (int)treeView1.SelectedNode.Parent.Tag;
                    int nodeIndex = (int)treeView1.SelectedNode.Tag;
                    AdsInfo.AdsObject.AdsPort[parentIndex].AdsSymbols[nodeIndex].Handle = int.Parse(tbxHandle.Text);
                    tbxHandle.BackColor = SystemColors.Window;
                    GlobalVar.isProjectFileChanged = true;
                }
            }
            else if (sender == btnAdsPortSearch)
            {
                AdsPortSample dlg = new AdsPortSample(toolStripStatusLabel1.Text, GlobalVar.AmsNetID);
                dlg.StartPosition = FormStartPosition.CenterParent;
                DialogResult dr = dlg.ShowDialog();
                if (dr == DialogResult.OK)
                {
                    tbxAdsName.Text = dlg.PortName;
                    tbxAdsPort.Text = dlg.PortID.ToString();
                    int nodeIndex = (int)treeView1.SelectedNode.Tag;
                    AdsInfo.AdsObject.AdsPort[nodeIndex].Name = tbxAdsName.Text;
                    AdsInfo.AdsObject.AdsPort[nodeIndex].Port = int.Parse(tbxAdsPort.Text);
                    tbxAdsName.BackColor = tbxAdsPort.BackColor = SystemColors.Window;
                    GlobalVar.isProjectFileChanged = true;
                }
            }
        }

        private void tbxAdsName_KeyDown(object sender, KeyEventArgs e)
        {
            if (GlobalVar.isUdpConnceted)
                return;
            if (e.KeyCode == Keys.Enter)
            {
                int rootIndex = (int)treeView1.SelectedNode.Tag;
                AdsInfo.AdsObject.AdsPort[rootIndex].Name = tbxAdsName.Text;
                tbxAdsName.BackColor = SystemColors.Window;
                GlobalVar.isProjectFileChanged = true;
            }
        }

        

        private void tbxIndexGroup_KeyDown(object sender, KeyEventArgs e)
        {

        }

        private void tbxHandle_KeyDown(object sender, KeyEventArgs e)
        {
            if (GlobalVar.isUdpConnceted)
                return;
            if (e.KeyCode == Keys.Enter)
            {
                if (!RegexMatch.isPositiveInteger(tbxHandle.Text))
                {
                    int parentIndex = (int)treeView1.SelectedNode.Parent.Tag;
                    int nodeIndex = (int)treeView1.SelectedNode.Tag;
                    tbxHandle.Text = AdsInfo.AdsObject.AdsPort[parentIndex].AdsSymbols[nodeIndex].Handle.ToString();
                    tbxHandle.BackColor = SystemColors.Window;
                }
                else
                {
                    int parentIndex = (int)treeView1.SelectedNode.Parent.Tag;
                    int nodeIndex = (int)treeView1.SelectedNode.Tag;
                    AdsInfo.AdsObject.AdsPort[parentIndex].AdsSymbols[nodeIndex].Handle = int.Parse(tbxHandle.Text);
                    tbxHandle.BackColor = SystemColors.Window;
                    GlobalVar.isProjectFileChanged = true;
                }                
            }
        }

        private void textBox_TextChanged(object sender, EventArgs e)
        {
            if (isInited)
            {
                ((TextBox)sender).BackColor = Color.PaleGreen;
            }
        }

        private void tbxAdsPort_TextChanged(object sender, EventArgs e)
        {
            if (isInited)
            {
                if (RegexMatch.isPositiveInteger(tbxAdsPort.Text))
                {
                    ((TextBox)sender).BackColor = Color.PaleGreen;
                }
                else
                {
                    ((TextBox)sender).BackColor = Color.Yellow;
                }
            }
        }

        private void cbxAdsEnabled_MouseDown(object sender, MouseEventArgs e)
        {
            //if(GlobalVar.isUdpConnceted)
                
        }

        private void cbxAdsEnabled_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isInited)
            {
                if (cbxAdsEnabled.SelectedIndex == 0 || cbxAdsEnabled.SelectedIndex == 1)
                {
                    int rootIndex = (int)treeView1.SelectedNode.Tag;
                    AdsInfo.AdsObject.AdsPort[rootIndex].Enabled = cbxAdsEnabled.SelectedIndex == 0 ? true : false;
                    GlobalVar.isProjectFileChanged = true;
                }
            }
        }

        private void DeviceInfo_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer1.Stop();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (GlobalVar.isUdpConnceted)
            {
                this.BeginInvoke(new Action(() =>
                {
                    cbxAdsEnabled.Enabled = false;                   
                }));
            }
            else
            {
                this.BeginInvoke(new Action(() =>
                {
                    cbxAdsEnabled.Enabled = true;
                }));
            }
        }

        private void treeView1_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            if (treeView1.SelectedNode != null)
            {
                treeView1.SelectedNode.BackColor = SystemColors.Window;
            }
        }

        private void treeView1_Leave(object sender, EventArgs e)
        {
            if (treeView1.SelectedNode != null)
            {
                treeView1.SelectedNode.BackColor = Color.SkyBlue;
            }
        }

        private void tbxAdsPort_Leave(object sender, EventArgs e)
        {
            if (isInited)
            {
                if (!RegexMatch.isPositiveInteger(tbxAdsPort.Text))
                {
                    int rootIndex = (int)treeView1.SelectedNode.Tag;
                    tbxAdsPort.Text = AdsInfo.AdsObject.AdsPort[rootIndex].Port.ToString();
                    tbxAdsPort.BackColor = SystemColors.Window;
                }
            }
        }

        private void tbxHandle_TextChanged(object sender, EventArgs e)
        {
            if (isInited)
            {
                if (RegexMatch.isPositiveInteger(((TextBox)sender).Text))
                {
                    ((TextBox)sender).BackColor = Color.PaleGreen;
                }
                else
                {
                    ((TextBox)sender).BackColor = Color.Yellow;
                }
            }
        }
    }
}

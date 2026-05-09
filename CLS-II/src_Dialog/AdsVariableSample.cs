using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TwinCAT;
using TwinCAT.Ads;
using TwinCAT.Ads.TypeSystem;
using TwinCAT.Ads.ValueAccess;
using TwinCAT.TypeSystem;

namespace CLS_II
{
    public partial class AdsVariableSample : Form
    {
        #region Private Variable
        private int _ChannelCount = 0;
        private string _AmsNetID = "127.0.0.1.1.1";
        private int _Port = 0;
        private int R1 = 350, R2 = 365, R3 = 851, R4 = 860;
        private string _PortName = string.Empty;

        private TcAdsClient adsClient = new TcAdsClient();
        private ISymbolLoader symbolLoader;
        private const String DEFAULT_TEXT = "Enter Name...";
        #endregion

        #region Public Variable
        public List<_WatchVarietyInfo> WatchVarietyInfos = new List<_WatchVarietyInfo>();
        #endregion

        public AdsVariableSample(string AmsNetID, int ChannelCount, string UDPItem1, object UDPStruct1)
        {
            InitializeComponent();

            //this._AmsNetID = AmsNetID;
            this._ChannelCount = ChannelCount;
            //adsClient.Timeout = 300;

            SetUDPTreeNode(UDPItem1, UDPStruct1);
        }

        private void SetUDPTreeNode(string Item1, object Struct1)
        {
            TreeNode node = new TreeNode("UDP");
            List<string> Struct1Items = new List<string>();
            List<Type> Struct1Type = new List<Type>();
            List<int> Struct1Size = new List<int>();
            treeView3.Nodes.Clear();
            foreach (_FieldInfo field in Struct1.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                string itemName = field.Name;
                Struct1Items.Add(itemName);
                Type type = field.FieldType;
                Struct1Type.Add(type);
                int size = 0;
                size = Marshal.SizeOf(type);
                Struct1Size.Add(size);
            }
            for (int i = 0; i < this._ChannelCount; i++)
            {
                string str = "Channel " + (i + 1);
                node.Nodes.Add(str);
                TreeNode node2 = new TreeNode(str);
                node2.Nodes.Add(Item1);
                node2.Nodes[node2.Nodes.Count - 1].Tag = 0;
                int index = 0;
                foreach (string s in Struct1Items)
                {
                    TreeNode tmpNode = node2.Nodes[node2.Nodes.Count - 1].Nodes.Add(s);
                    _WatchVarietyInfo varietyInfo = new _WatchVarietyInfo();
                    varietyInfo.Name = s;
                    varietyInfo.Category = "UDP";
                    varietyInfo.Port = (i + 1).ToString();
                    varietyInfo.Source = Item1 + "." + s;
                    varietyInfo.Type = Struct1Type[index].Name;
                    varietyInfo.Size = Struct1Size[index].ToString();
                    varietyInfo.Comment = "";
                    tmpNode.Tag = varietyInfo;
                    index++;
                }
                node.Nodes[node.Nodes.Count - 1].Tag = node2;
            }
            treeView2.Nodes.Add(node);
            treeView2.ExpandAll();
        }

        #region Textbox1

        #region =★*★*★= 字符串可能的格式 =★*★*★=
        static FontStyle fonts0 = FontStyle.Bold | FontStyle.Italic;
        static FontStyle fonts1 = FontStyle.Bold | FontStyle.Underline;
        static FontStyle fonts2 = FontStyle.Bold | FontStyle.Strikeout;
        static FontStyle fonts3 = FontStyle.Italic | FontStyle.Underline;
        static FontStyle fonts4 = FontStyle.Italic | FontStyle.Strikeout;
        static FontStyle fonts5 = FontStyle.Underline | FontStyle.Strikeout;
        static FontStyle fonts6 = FontStyle.Bold | FontStyle.Italic | FontStyle.Underline;
        static FontStyle fonts = FontStyle.Bold | FontStyle.Italic | FontStyle.Underline | FontStyle.Strikeout;
        #endregion

        private void SetDefaultText()
        {
            textBox1.Text = DEFAULT_TEXT;
            textBox1.ForeColor = Color.Gray;
            textBox1.Font = new Font(Font, FontStyle.Italic);
        }

        //获取焦点事件 Enter
        private void textBox1_Enter(object sender, EventArgs e)
        {
            if (textBox1.Text == DEFAULT_TEXT)
            {
                textBox1.Text = "";
                textBox1.ForeColor = Color.Black;
                textBox1.Font = new Font(Font, FontStyle.Regular);
            }
        }

        //失去焦点事件 Leave
        private void textBox1_Leave(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(textBox1.Text))
                SetDefaultText();
        }
        #endregion

        private void AdsVariableSample_Load(object sender, EventArgs e)
        {
            SetDefaultText();
            dataGridView1.Rows.Clear();
            WatchVarietyInfos.Clear();
            _WatchVarietyInfo vi = new _WatchVarietyInfo("(None)", "", "", "", "", "");
            WatchVarietyInfos.Add(vi);
            dataGridView1.DataSource = new BindingList<_WatchVarietyInfo>(WatchVarietyInfos);
            dataGridView1.Rows[0].DefaultCellStyle.BackColor = SystemColors.Control;

            //SearchPorts(R1, R2, R3, R4);
        }

        //private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        //{
        //    if (treeView1.SelectedNode != null)
        //    {
        //        if (treeView1.SelectedNode.Tag != null)
        //        {    
        //            int port = (int)treeView1.SelectedNode.Tag;
        //            AdsNodeCreate(port);
        //        }
        //        else
        //        {
        //            treeView3.Nodes.Clear();
        //        }
        //    }
        //}

        private void AdsNodeCreate(int port)
        {
            this._Port = port;
            treeView3.Nodes.Clear();
            if (adsClient.IsConnected)
            {
                adsClient.Disconnect();
            }
            adsClient.Connect(this._AmsNetID, this._Port);
            StateInfo adsDevice = new StateInfo();
            AdsErrorCode errorinfo = adsClient.TryReadState(out adsDevice);
            bool result = errorinfo == AdsErrorCode.NoError;
            if (result)
            {
                SymbolLoaderSettings settings = new SymbolLoaderSettings(SymbolsLoadMode.VirtualTree, ValueAccessMode.IndexGroupOffsetPreferred);
                symbolLoader = SymbolLoaderFactory.Create(adsClient, settings);
                if (symbolLoader.Symbols.Count > 0)
                {
                    TreeNode portNode = treeView3.Nodes.Add(port.ToString());
                    foreach (ISymbol symbol in symbolLoader.Symbols)
                    {
                        portNode.Nodes.Add(CreateNewNode(symbol));
                    }
                }
            }
        }

        private TreeNode CreateNewNode(ISymbol symbol)
        {
            TreeNode node = new TreeNode(symbol.InstanceName);
            if (CheckSymbolType(symbol))
            {
                _WatchVarietyInfo vi = SetADSTreeNode(symbol);
                node.Tag = vi;
            }

            foreach (ISymbol subsymbol in symbol.SubSymbols)
            {
                node.Nodes.Add(CreateNewNode(subsymbol));
            }
            return node;
        }

        string[] ValidType = { "SINT", "INT", "DINT", "LINT", "USINT", "UINT", "UDINT", "ULINT", "REAL", "LREAL", "BOOL", "STRING", "BIT" };
        private bool CheckSymbolType(ISymbol symbol)
        {
            if (symbol.SubSymbols.Count > 0)
                return false;
            foreach (string vType in ValidType)
            {
                if (RegexMatch.isSameName(vType, symbol.DataType.ToString()))
                {
                    return true;
                }
            }
            return false;
        }

        private _WatchVarietyInfo SetADSTreeNode(ISymbol symbol)
        {
            _WatchVarietyInfo result = new _WatchVarietyInfo();

            result.Name = symbol.InstanceName;
            result.Category = "ADS";
            result.Port = this._Port.ToString();
            result.Source = symbol.InstancePath;
            result.Type = symbol.DataType.ToString();
            result.Size = symbol.Size.ToString();
            result.Comment = string.IsNullOrEmpty(symbol.Comment) ? "" : symbol.Comment;

            return result;
        }

        //private void SearchPorts(int R1, int R2, int R3, int R4)
        //{
        //    treeView1.Nodes.Clear();
        //    treeView1.Nodes.Add("ADS");
        //    if (R1 != -1)
        //    {
        //        for (int port = R1; port <= R2; port++)
        //        {
        //            CreatePortNode(port);
        //        }
        //    }
        //    if (R3 != -1)
        //    {
        //        for (int port = R3; port <= R4; port++)
        //        {
        //            CreatePortNode(port);
        //        }
        //    }
        //}

        //private void CreatePortNode(int port)
        //{
        //    if (adsClient.IsConnected)
        //        adsClient.Disconnect();
        //    adsClient.Connect(this._AmsNetID, port);
        //    StateInfo adsDevice = new StateInfo();
        //    AdsErrorCode errorinfo = adsClient.TryReadState(out adsDevice);
        //    bool result = errorinfo == AdsErrorCode.NoError;
        //    if (result)
        //    {
        //        SymbolLoaderSettings settings = new SymbolLoaderSettings(SymbolsLoadMode.VirtualTree, ValueAccessMode.IndexGroupOffsetPreferred);
        //        symbolLoader = SymbolLoaderFactory.Create(adsClient, settings);
        //        TreeNode portNode = treeView1.Nodes[0];
        //        if (symbolLoader.Symbols.Count > 0)
        //        {
        //            portNode.Nodes.Add(port.ToString());
        //            portNode.Nodes[portNode.Nodes.Count - 1].Tag = port;
        //        }
        //    }
        //    adsClient.Disconnect();
        //}

        private void treeView2_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (treeView2.SelectedNode != null)
            {
                if (treeView2.SelectedNode.Tag != null)
                {
                    treeView3.Nodes.Clear();
                    treeView3.Nodes.Add((TreeNode)treeView2.SelectedNode.Tag);
                }
                else
                {
                    treeView3.Nodes.Clear();
                }
            }
        }

        private void treeView_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            TreeView treeView = (TreeView)sender;
            if (treeView.SelectedNode != null)
            {
                if (treeView.SelectedNode.Parent is null)
                    return;
                treeView.SelectedNode.BackColor = SystemColors.Window;
            }
        }

        private void treeView_Leave(object sender, EventArgs e)
        {
            TreeView treeView = (TreeView)sender;
            if (treeView.SelectedNode != null)
            {
                if (treeView.SelectedNode.Parent is null)
                    return;
                treeView.SelectedNode.BackColor = Color.SkyBlue;
            }
        }

        private void treeView3_AfterSelect(object sender, TreeViewEventArgs e)
        {
            TreeNode node = treeView3.SelectedNode;
            if (node != null)
            {
                if (node.Tag is _WatchVarietyInfo)
                {
                    if (!(WatchVarietyInfos.Count > 0))
                    {
                        WatchVarietyInfos.Clear();
                        _WatchVarietyInfo vi = new _WatchVarietyInfo("(None)", "", "", "", "", "");
                        WatchVarietyInfos.Add(vi);
                    }
                    else
                    {
                        _WatchVarietyInfo vi = (_WatchVarietyInfo)node.Tag;
                        WatchVarietyInfos[0] = vi;
                    }
                    dataSourceBind();
                }
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.SelectedRows.Count;
            if (count > 0)
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    if (dataGridView1.SelectedRows[i].Index == 0)
                        continue;
                    WatchVarietyInfos.RemoveAt(dataGridView1.SelectedRows[i].Index);
                }
            }
            dataSourceBind();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            WatchVarietyInfos.Clear();
            _WatchVarietyInfo vi = new _WatchVarietyInfo("(None)", "", "", "", "", "");
            WatchVarietyInfos.Add(vi);
            dataSourceBind();
        }

        private void dataSourceBind()
        {
            dataGridView1.DataSource = new BindingList<_WatchVarietyInfo>(WatchVarietyInfos);
            if(dataGridView1.Rows.Count > 0)
                dataGridView1.Rows[0].DefaultCellStyle.BackColor = SystemColors.Control;
        }

        private void dataSourceAdd(_WatchVarietyInfo vi)
        {
            if (dataGridView1.Rows.Count <= 0)
            {
                WatchVarietyInfos.Clear();
                _WatchVarietyInfo v = new _WatchVarietyInfo("(None)", "", "", "", "", "");
                WatchVarietyInfos.Add(v);
            }
            else
            {
                bool isExisted = false;
                for (int i = 1; i < WatchVarietyInfos.Count; i++)
                {
                    _WatchVarietyInfo item = WatchVarietyInfos[i];
                    if (vi == item)
                    {
                        isExisted = true;
                        break;
                    }
                }
                if (!isExisted)
                {
                    WatchVarietyInfos.Add(vi);
                }
            }
            dataSourceBind();
        }

        private void treeView3_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            TreeNode node = treeView3.SelectedNode;
            if (node != null)
            {
                if (node.Tag is _WatchVarietyInfo)
                {
                    dataSourceAdd((_WatchVarietyInfo)node.Tag);
                    dataSourceBind();
                }
            }
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //treeView3.Nodes.Clear();
            //if (tabControl1.SelectedIndex == 0)
            //{
            //    SearchPorts(R1, R2, R3, R4);
            //}
        }
    }

    public class _WatchVarietyInfo
    {
        public string Name { get; set; }    // Node.Name
        public string Category { get; set; }// ADS or UDP
        public string Port { get; set; }    // AdsPort, UdpChannel        
        public string Source { get; set; }  // AdsPath, UdpItem.Name
        public string Type { get; set; }    // SINT INT DINT LINT REAL LREAL BOOL STRING
        public string Size { get; set; }
        public string Comment { get; set; }

        public _WatchVarietyInfo() { }
        public _WatchVarietyInfo(string _VarName, string _Category, string _Port, string _Source, string _Type, string _Size, string _Comment = "")
        {
            this.Name = _VarName;
            this.Category = _Category;
            this.Port = _Port;
            this.Source = _Source;
            this.Type = _Type;
            this.Size = _Size;
            this.Comment = _Comment;
        }
    }
}

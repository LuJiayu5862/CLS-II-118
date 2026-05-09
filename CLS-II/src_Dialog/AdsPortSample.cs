using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
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
    public partial class AdsPortSample : Form
    {
        #region Private Variable
        private string _object = string.Empty;
        private string _AmsNetID = "127.0.0.1.1.1";
        private int _Port = 0;
        private string _PortName = string.Empty;

        private TcAdsClient adsClient = new TcAdsClient();
        private ISymbolLoader symbolLoader;
        #endregion

        #region Public Variable
        public int PortID
        {
            get
            {
                return _Port;
            }
        }
        public string PortName
        {
            get
            {
                return _PortName;
            }
        }
        #endregion

        public AdsPortSample(string Object, string AmsNetID)
        {
            InitializeComponent();

            this._object = Object;
            this._AmsNetID = AmsNetID;

            lblObject.Text = this._object;
            lblAmsNetID.Text = this._AmsNetID;
            adsClient.Timeout = 300;
        }

        private bool TryAdsConnect(int Port)
        {
            adsClient.Connect(this._AmsNetID, Port);
            StateInfo adsDevice = new StateInfo();
            AdsErrorCode errorinfo = adsClient.TryReadState(out adsDevice);
            bool result = errorinfo == AdsErrorCode.NoError;
            
            adsClient.Disconnect();
            return result;
        }

        private void SearchPorts(int R1, int R2, int R3, int R4)
        {
            treeViewSymbols.Nodes.Clear();
            if (R1 != -1)
            {
                for (int port = R1; port <= R2; port++)
                {
                    CreateNewNode(port);
                }
            }
            if (R3 != -1)
            {
                for (int port = R3; port <= R4; port++)
                {
                    CreateNewNode(port);
                }
            }
        }

        private void CreateNewNode(int port)
        {
            adsClient.Connect(this._AmsNetID, port);
            StateInfo adsDevice = new StateInfo();
            AdsErrorCode errorinfo = adsClient.TryReadState(out adsDevice);
            bool result = errorinfo == AdsErrorCode.NoError;
            if (result)
            {
                SymbolLoaderSettings settings = new SymbolLoaderSettings(SymbolsLoadMode.VirtualTree, ValueAccessMode.IndexGroupOffsetPreferred);
                symbolLoader = SymbolLoaderFactory.Create(adsClient, settings);
                if (symbolLoader.Symbols.Count > 0)
                {
                    treeViewSymbols.Nodes.Add(port.ToString());
                    foreach (ISymbol symbol in symbolLoader.Symbols)
                    {
                        TreeNode node = new TreeNode(symbol.InstanceName);
                        node.Tag = symbol;
                        treeViewSymbols.Nodes[treeViewSymbols.Nodes.Count-1].Nodes.Add(node);
                    }
                }
            }
            adsClient.Disconnect();
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

        private void btnSearch_Click(object sender, EventArgs e)
        {
            int R1, R2, R3, R4;
            string S1, S2, S3, S4;
            S1 = tbxRange1A.Text;
            S2 = tbxRange1B.Text;
            S3 = tbxRange2A.Text;
            S4 = tbxRange2B.Text;
            R1 = string.IsNullOrEmpty(S1) ? -1 : int.Parse(S1);
            R2 = string.IsNullOrEmpty(S2) ? -1 : int.Parse(S2);
            R3 = string.IsNullOrEmpty(S3) ? -1 : int.Parse(S3);
            R4 = string.IsNullOrEmpty(S4) ? -1 : int.Parse(S4);
            if (((R1 != -1 && R2 != -1) || (R3 != -1 && R4 != -1)) &&
                (R2 - R1) >= 0 && (R4 - R3) >= 0)
            {
                if ((R1 < R3 && R2 < R3) || (R2 > R4 && R1 > R4))
                {
                    tbxRange1A.BackColor = tbxRange1B.BackColor = tbxRange2A.BackColor = tbxRange2B.BackColor = SystemColors.Window;
                    SearchPorts(R1, R2, R3, R4);
                }
                else
                {
                    tbxRange1A.BackColor = tbxRange1B.BackColor = tbxRange2A.BackColor = tbxRange2B.BackColor = Color.Yellow;
                }
            }
            else
            {
                tbxRange1A.BackColor = tbxRange1B.BackColor = tbxRange2A.BackColor = tbxRange2B.BackColor = Color.Yellow;
            }
        }

        private void tbxRange1A_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || e.KeyChar == 8 || e.KeyChar == 32)
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }

        private void treeViewSymbols_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Text.Length > 0)
            {
                if (e.Node.Tag is ISymbol)
                {
                    SetSymbolInfo(e.Node, (ISymbol)e.Node.Tag);
                }
            }
        }

        private void SetSymbolInfo(TreeNode node, ISymbol symbol)
        {
            try
            {
                tbPortID.Text = node.Parent.Text;
                tbName.Text = symbol.InstancePath;
                this._Port = int.Parse(tbPortID.Text);
                this._PortName = tbName.Text;
            }
            catch (Exception err)
            {
                //MessageBox.Show("Unable to read Symbol Info. " + err.Message);
                this._Port = 0;
                this._PortName = string.Empty;
            }
        }
    }
}

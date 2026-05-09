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
    public partial class AdsSymbolSample : Form
    {
        #region Private Variable
        private string _object = string.Empty;
        private string _AmsNetID = "127.0.0.1.1.1";
        private int _Port = 0;
        private int _SymbolHandle = 0;

        private TcAdsClient adsClient = new TcAdsClient();
        private ISymbolLoader symbolLoader;
        #endregion

        #region Public Variable
        public int SymbolHandle
        {
            get
            {
                return _SymbolHandle;
            }
        }
        #endregion

        public AdsSymbolSample(string Object, string AmsNetID, int Port)
        {
            InitializeComponent();
            this._object = Object;
            this._AmsNetID = AmsNetID;
            this._Port = Port;

            lblObject.Text = this._object;
            lblAmsNetID.Text = this._AmsNetID;
            lblPort.Text = this._Port.ToString();
            AdsConnect();
        }

        private void AdsConnect()
        {
            adsClient.Connect(this._AmsNetID, this._Port);
            treeViewSymbols.Nodes.Clear();
            SymbolLoaderSettings settings = new SymbolLoaderSettings(SymbolsLoadMode.VirtualTree, ValueAccessMode.IndexGroupOffsetPreferred);
            try
            {
                symbolLoader = SymbolLoaderFactory.Create(adsClient, settings);
                foreach (ISymbol symbol in symbolLoader.Symbols)
                {
                    treeViewSymbols.Nodes.Add(CreateNewNode(symbol));
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private TreeNode CreateNewNode(ISymbol symbol)
        {
            TreeNode node = new TreeNode(symbol.InstanceName);
            node.Tag = symbol;

            foreach (ISymbol subsymbol in symbol.SubSymbols)
            {
                node.Nodes.Add(CreateNewNode(subsymbol));
            }
            return node;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void treeViewSymbols_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Text.Length > 0)
            {
                if (e.Node.Tag is ISymbol)
                {
                    SetSymbolInfo((ISymbol)e.Node.Tag);
                }
            }
        }

        private void SetSymbolInfo(ISymbol symbol)
        {
            try
            {
                tbName.Text = symbol.InstancePath;
                tbIndexOffset.Text = symbol.Comment == null ? string.Empty : symbol.Comment.ToString();
                tbSize.Text = symbol.Size.ToString();
                tbDatatype.Text = symbol.DataType == null ? string.Empty : symbol.DataType.ToString(); 
                tbHandle.Text = adsClient.CreateVariableHandle(tbName.Text).ToString();
                this._SymbolHandle = adsClient.CreateVariableHandle(tbName.Text);
            }
            catch (Exception err)
            {
                //MessageBox.Show("Unable to read Symbol Info. " + err.Message);
                tbHandle.Text = string.Empty;
                this._SymbolHandle = 0;
            }
        }
    }
}

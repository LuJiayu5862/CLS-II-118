using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using MmTimer;

namespace CLS_II
{
    public partial class Watch : Form
    {
        HPTimer mmTimer1;
        private const int mmInterval = 10;
        private const int timerInterval = 100, timer2Interval = 1000;
        private bool _isInited = false;
        private bool isUpdateOnce = false;
        private bool isWatchChanged = false;
        //private List<AdsClient> adsClients = new List<AdsClient>();
        private BindingSource source = new BindingSource();
        //private bool _isAdsConnected = false;
        //private List<string> adsPortList = new List<string>();
        //private List<Variables> adsVariables = new List<Variables>();
        private List<int[]> activeVariablesIndex = new List<int[]>();
        private List<VariableInfo[]> activeVariables = new List<VariableInfo[]>();
        private List<byte[]> activeNextValue = new List<byte[]>();
        //private bool _isAdsTargetChanged = false;
        //private bool _isAdsSymbolNotFound = false;
        private bool isTextBoxHide1 = false, isTextBoxHide2 = false;

        public class Record
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Value{ get; set; }
        }
        List<Record> records = new List<Record>();
        public static Watch _Watch = null;
        public bool isInited { get { return this._isInited; } }
        

        public Watch()
        {
            InitializeComponent();
            _Watch = this;

            // 打开普通定时器(100ms)
            timer1.Interval = timerInterval;
            timer1.Enabled = true;
            timer1.Start();

            // 打开普通定时器2(1000ms)
            timer2.Interval = timer2Interval;
            timer2.Enabled = true;
            timer2.Start();

            // 打开高精度定时器(10ms)
            mmTimer1 = new HPTimer(mmInterval);
            mmTimer1.Ticked += new EventHandler(mmTimer1_Ticked);

            // 双缓冲防止闪烁
            dataGridView1.DoubleBufferedDataGirdView(true);
            dataGridView1.Controls.Add(textBox1);

            // 隐藏
            ;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (!isTextBoxHide1)
            {
                isTextBoxHide1 = true;
                return;
            }
            else
            {
                if (!isTextBoxHide2)
                {
                    isTextBoxHide2 = true;
                    dataGridView1.CurrentCell = null;
                    textBox1.Visible = false;
                    return;
                }
            }
            if (textBox1.Visible)
                textBox1.Focus();
            // 连接成功后，启用10ms定时器
            //if (GlobalVar.isUdpConnceted)
            //{
            //    if (!this._isAdsConnected)
            //    {
            //        mmTimer1.Start();
            //        InitADS();
            //    }
            //    this._isAdsConnected = true;
            //}
            //else
            //{
            //    if (this._isAdsConnected)
            //    {
            //        mmTimer1.Stop();
            //        DisposeADS();
            //        for (int i = 0; i < dataGridView1.Rows.Count; i++)
            //        {
            //            dataGridView1.Rows[i].Cells[(int)Columns.Value].Value = "";
            //            dataGridView1.Rows[i].DefaultCellStyle.BackColor = SystemColors.Window;
            //        }
            //        this.isWatchChanged = false;
            //    }
            //    this._isAdsConnected = false;
            //}             
            if (this.isWatchChanged)
            {
                this.isWatchChanged = false;
                for (int i = 0; i < dataGridView1.Rows.Count; i++)
                {
                    dataGridView1.Rows[i].Cells[(int)Columns.Value].Value = this.records[i].Value;
                    if (this.records[i].Value == "(Not Found)")
                    {
                        dataGridView1.Rows[i].DefaultCellStyle.BackColor = Color.OrangeRed;
                        dataGridView1.Rows[i].Cells[(int)Columns.Scope].Value = "False";
                        //if (!_isAdsSymbolNotFound && (string)dataGridView1.Rows[i].Cells[(int)Columns.Category].Value == "ADS")
                        //{
                        //    _isAdsSymbolNotFound = true;
                        //}
                    }
                    else
                    {
                        dataGridView1.Rows[i].DefaultCellStyle.BackColor = SystemColors.Window; ;
                    }
                }
            }
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            //if (this._isAdsConnected)
            //{
            //    if (_isAdsSymbolNotFound)
            //    {
            //        _isAdsSymbolNotFound = false;
            //        adsHandleCreate();
            //    }
            //}
        }

        private void mmTimer1_Ticked(object sender, EventArgs e)
        {
            // 自动监测模式
            if (MainConfig.ConfigInfo.DebugItems.isAutoWatch)
            {
                updateUdpDataOnce();
                //updateAdsDataOnce();
                updateScopeDataOnce();
                this.isWatchChanged = true;
            }
            // 手动监测模式
            else
            {
                if (this.isUpdateOnce)
                {
                    this.isUpdateOnce = false;
                    updateUdpDataOnce();
                    //updateAdsDataOnce();
                    updateScopeDataOnce();
                    this.isWatchChanged = true;
                }
            }
        }

        private void addToolStripButton_Click(object sender, EventArgs e)
        {
            // 此处是产生新变量对象的代码
            AdsVariableSample dlg = new AdsVariableSample(GlobalVar.AmsNetID, CLSConsts.EnabledChannels, "Feedback", new _Feedback());
            dlg.StartPosition = FormStartPosition.CenterParent;
            DialogResult dr = dlg.ShowDialog();
            if (dr == DialogResult.OK)
            {
                List<_WatchVarietyInfo> VariableSamples = dlg.WatchVarietyInfos;
                if (VariableSamples.Count > 0)
                    VariableSamples.RemoveAt(0);
                if (VariableSamples.Count > 0)
                {
                    foreach (_WatchVarietyInfo item in VariableSamples)
                    {
                        WatchConfig._VarietyInfo variety = new WatchConfig._VarietyInfo();
                        variety.VarName = item.Name;
                        variety.Category = item.Category;
                        variety.Port = item.Port;
                        variety.Source = item.Source;
                        variety.Type = item.Type;
                        variety.Comment = item.Comment;
                        // 此处是添加watch对象的代码，已经实现功能
                        bool isExisted = false;
                        foreach (WatchConfig._VarietyInfo vi in WatchConfig.VarietyInfos)
                        {
                            if (vi.Category == variety.Category)
                                if (vi.Port == variety.Port)
                                    if (vi.Source == variety.Source)
                                    {
                                        isExisted = true;
                                        break;
                                    }
                        }
                        if (isExisted)
                            continue;
                        
                        lock (this.records)
                        {
                            this.source.Add(variety);

                            for (int i = 0; i < WatchConfig.VarietyInfos.Count; i++)
                            {
                                int tmpCount = 0;
                                string tmpName = WatchConfig.VarietyInfos[i].VarName;
                                for (int j = i + 1; j < WatchConfig.VarietyInfos.Count; j++)
                                {
                                    if (RegexMatch.isSameName(tmpName, WatchConfig.VarietyInfos[j].VarName))
                                    {
                                        tmpCount++;
                                        dataGridView1.Rows[j].Cells[(int)Columns.Name].Value = tmpName + "(" + tmpCount + ")";
                                    }
                                }
                            }
                            this.records.Clear();
                            for (int i = 0; i < WatchConfig.VarietyInfos.Count; i++)
                            {
                                Record record = new Record()
                                {
                                    Name = dataGridView1.Rows[i].Cells[(int)Columns.Name].Value.ToString(),
                                    Type = dataGridView1.Rows[i].Cells[(int)Columns.Type].Value.ToString(),
                                    Value = "(Not Found)"
                                };
                                this.records.Add(record);
                            }
                        }
                    }
                    //InitADS();
                    dataGridView1.CurrentCell = null;
                    textBox1.Visible = false;
                }                          
            }  
        }

        private void clearToolStripButton_Click(object sender, EventArgs e)
        {           
            lock (this.records)
            {
                lock (activeVariables)
                {
                    dataGridView1.Rows.Clear();
                    this.records.Clear();
                    textBox1.Visible = false;
                    //DisposeADS();
                }
            }
        }

        private void deleteToolStripButton_Click(object sender, EventArgs e)
        {           
            lock (this.records)
            {
                int count = dataGridView1.SelectedRows.Count;
                if (count > 0)
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        dataGridView1.Rows.RemoveAt(dataGridView1.SelectedRows[i].Index);
                    }
                }
                this.records.Clear();
                for (int i = 0; i < WatchConfig.VarietyInfos.Count; i++)
                {
                    Record record = new Record()
                    {
                        Name = dataGridView1.Rows[i].Cells[(int)Columns.Name].Value.ToString(),
                        Type = dataGridView1.Rows[i].Cells[(int)Columns.Type].Value.ToString(),
                        Value = "(Not Found)"
                    };
                    this.records.Add(record);
                }
            }
            //InitADS();
        }

        private void Watch_Load(object sender, EventArgs e)
        {
            lock (WatchConfig.VarietyInfos)
            {
                WatchConfig.ConfigFileInit();
                dataGridView1.Rows.Clear();
                this.source.DataSource = WatchConfig.VarietyInfos;
                dataGridView1.DataSource = this.source;
                lock (this.records)
                {
                    this.records.Clear();
                    for (int i = 0; i < WatchConfig.VarietyInfos.Count; i++)
                    {
                        Record record = new Record()
                        {
                            Name = dataGridView1.Rows[i].Cells[(int)Columns.Name].Value.ToString(),
                            Type = dataGridView1.Rows[i].Cells[(int)Columns.Type].Value.ToString(),
                            Value = "(Not Found)"
                        };
                        this.records.Add(record);
                    }
                }
                //dataGridView1.DataSource = new BindingList<WatchConfig._VarietyInfo>(WatchConfig.VarietyInfos);
                dataGridView1.CurrentCell = null;
                textBox1.Visible = false;
                this._isInited = true;
            }
            
        }

        private void Watch_FormClosing(object sender, FormClosingEventArgs e)
        {
            mmTimer1.Stop();
            timer1.Stop();
            WatchConfig.WriteConfigFile();
        }

        private void refreshToolStripButton_Click(object sender, EventArgs e)
        {
            isUpdateOnce = true;
        }

        private void dataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            DataGridViewCell currentCell = dataGridView1.CurrentCell;
            if (currentCell.RowIndex < 0 || currentCell.ColumnIndex < 0)
                return;
            if (currentCell.ColumnIndex == (int)Columns.Scope)
            {
                if (dataGridView1.IsCurrentCellDirty)
                {
                    dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
                    if (WatchConfig.ScopeVarieties.Count >= 20)
                    {
                        currentCell.Value = "False";
                    }
                    else if (myToString(dataGridView1.Rows[currentCell.RowIndex].Cells[(int)Columns.Value].Value) == "(Not Found)")
                    {
                        currentCell.Value = "False";
                    }
                    else if (validScopeType.IndexOf(myToString(dataGridView1.Rows[currentCell.RowIndex].Cells[(int)Columns.Type].Value)) == -1)
                    {
                        currentCell.Value = "False";
                    }
                    lock (WatchConfig.ScopeVarieties)
                    {
                        WatchConfig.ScopeVarieties.Clear();
                        lock (WatchConfig.VarietyInfos)
                        {
                            foreach (WatchConfig._VarietyInfo variety in WatchConfig.VarietyInfos)
                            {
                                if (variety.Scope == "True")
                                {
                                    WatchConfig._ScopeVariety v = new WatchConfig._ScopeVariety();
                                    v.VarName = variety.VarName;
                                    v.Type = variety.Type;
                                    v.Value = 0;
                                    WatchConfig.ScopeVarieties.Add(v);
                                }
                            }
                        }
                    }
#if DEBUG
                    //string str = string.Empty;
                    //foreach (WatchConfig._ScopeVariety sv in WatchConfig.ScopeVarieties)
                    //{
                    //    str = str + sv.VarName + "\r";

                    //}
                    //MessageBox.Show("Count:" + WatchConfig.ScopeVarieties.Count.ToString() + "\r" + str);
#endif
                }
            }
            
        }

        private void dataGridView1_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;
            if (e.ColumnIndex == (int)Columns.Name)
            {
                if (String.IsNullOrEmpty(RegexMatch.StringDeleteBlank(e.FormattedValue.ToString())))
                {

                    if (MultiLanguage.DefaultLanguage == "zh")
                        dataGridView1.Rows[e.RowIndex].ErrorText = "单元格第一列值不能为空。";
                    else
                        dataGridView1.Rows[e.RowIndex].ErrorText = "The first column value of a cell cannot be empty.";
                    dataGridView1.EditingControl.Text = dataGridView1.CurrentCell.Value.ToString();
                    if (dataGridView1.EditingControl != null)
                    {
                        dataGridView1.EditingControl.Text = myToString(dataGridView1.CurrentCell.Value);
                        textBox1.Text = dataGridView1.EditingControl.Text;
                        textBox1.Visible = true;
                        textBox1.Focus();
                        e.Cancel = true;
                    }
                }
                else
                {
                    if (this._isInited )
                    {
                        lock (WatchConfig.VarietyInfos)
                        {
                            string nextName = RegexMatch.StringDeleteBlank(e.FormattedValue.ToString());
                            if (dataGridView1.RowCount < 0)
                                return;
                            for (int i = 0; i < dataGridView1.RowCount; i++)
                            {
                                if (e.RowIndex == i)
                                    continue;
                                string getName = dataGridView1.Rows[i].Cells[(int)Columns.Name].Value.ToString();
                                if (nextName == RegexMatch.StringDeleteBlank(getName))
                                {
                                    if (MultiLanguage.DefaultLanguage == "zh")
                                        dataGridView1.Rows[e.RowIndex].ErrorText = "命名与其他变量冲突。";
                                    else
                                        dataGridView1.Rows[e.RowIndex].ErrorText = "Naming conflicts with other variables.";
                                    dataGridView1.EditingControl.Text = dataGridView1.CurrentCell.Value.ToString();
                                    if (dataGridView1.EditingControl != null)
                                    {
                                        dataGridView1.EditingControl.Text = myToString(dataGridView1.CurrentCell.Value);
                                        textBox1.Text = dataGridView1.EditingControl.Text;
                                        textBox1.Visible = true;
                                        textBox1.Focus();
                                        e.Cancel = true;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else if (e.ColumnIndex == (int)Columns.NextValue)
            {
                //dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
                
                object category = dataGridView1.Rows[dataGridView1.CurrentCell.RowIndex].Cells[(int)Columns.Category].Value;
                object type = dataGridView1.Rows[dataGridView1.CurrentCell.RowIndex].Cells[(int)Columns.Type].Value;
                object nextValue = e.FormattedValue.ToString();
                
                // 类别栏目为空的处理方法
                if (String.IsNullOrEmpty(RegexMatch.StringDeleteBlank(category.ToString())))
                {
                    //matchDefaultValue(myToString(type), myToString(nextValue));
                }
                // 类别为ADS
                //else if (RegexMatch.StringDeleteBlank(category.ToString()) == "ADS")
                //{
                //    if (string.IsNullOrEmpty(myToString(nextValue)))
                //        return;
                //    if (!matchADSValue(myToString(type), myToString(nextValue)))
                //    {
                //        if (MultiLanguage.DefaultLanguage == "zh")
                //            dataGridView1.Rows[e.RowIndex].ErrorText = "错误的输入内容。";
                //        else
                //            dataGridView1.Rows[e.RowIndex].ErrorText = "Bad input.";
                //        if (dataGridView1.EditingControl != null)
                //        {
                //            dataGridView1.EditingControl.Text = myToString(dataGridView1.CurrentCell.Value);
                //            textBox1.Text = dataGridView1.EditingControl.Text;
                //            textBox1.Visible = true;
                //            textBox1.Focus();
                //            e.Cancel = true;
                //        }
                //    }
                //}
                // 类别为UDP
                else if (RegexMatch.StringDeleteBlank(category.ToString()) == "UDP")
                {
                    if (string.IsNullOrEmpty(myToString(nextValue)))
                        return;
                    if (!matchUDPValue(myToString(type), myToString(nextValue)))
                    {
                        if (MultiLanguage.DefaultLanguage == "zh")
                            dataGridView1.Rows[e.RowIndex].ErrorText = "错误的输入内容。";
                        else
                            dataGridView1.Rows[e.RowIndex].ErrorText = "Bad input.";
                        if (dataGridView1.EditingControl != null)
                        {
                            dataGridView1.EditingControl.Text = myToString(dataGridView1.CurrentCell.Value);
                            textBox1.Text = dataGridView1.EditingControl.Text;
                            textBox1.Visible = true;
                            textBox1.Focus();
                            e.Cancel = true;
                        }
                    }
                }
                // 未定义的类别
                else
                {

                }
            }
        }

        private void dataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;
            dataGridView1.Rows[e.RowIndex].ErrorText = String.Empty;
            //if (e.ColumnIndex == (int)Columns.Name)
            //    InitADS();
            //SendKeys.Send("{ESC}");
        }

        private void writeToolStripButton_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.SelectedRows.Count;
            if (count > 0)
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    
                }
            }
        }

        private void RedrawTextBox()
        {
            if (dataGridView1.CurrentCell is null)
                return;
            DataGridViewCell currentCell = dataGridView1.CurrentCell;
            if (currentCell.ColumnIndex == (int)Columns.NextValue || currentCell.ColumnIndex == (int)Columns.Name)
            {
                if (textBox1.Visible)
                {
                    int columnIndex = dataGridView1.CurrentCell.ColumnIndex;
                    int rowIndex = dataGridView1.CurrentCell.RowIndex;
                    Rectangle rect = dataGridView1.GetCellDisplayRectangle(columnIndex, rowIndex, false);

                    textBox1.Left = rect.Left;
                    textBox1.Top = rect.Top;
                    textBox1.Width = rect.Width - 1;
                    textBox1.Height = rect.Height - 1;
                    textBox1.Visible = true;
                    textBox1.Focus();
                }
            }
        }

        private void dataGridView1_CurrentCellChanged(object sender, EventArgs e)
        {
            //if (dataGridView1.CurrentCell is null)
            //{
            //    textBox1.Visible = false;
            //    return;
            //}
            //DataGridViewCell currentCell = dataGridView1.CurrentCell;
            //if (currentCell.ColumnIndex == (int)Columns.NextValue || currentCell.ColumnIndex == (int)Columns.Name)
            //{
            //    int columnIndex = dataGridView1.CurrentCell.ColumnIndex;
            //    int rowIndex = dataGridView1.CurrentCell.RowIndex;
            //    Rectangle rect = dataGridView1.GetCellDisplayRectangle(columnIndex, rowIndex, false);

            //    textBox1.Left = rect.Left;
            //    textBox1.Top = rect.Top;
            //    textBox1.Width = rect.Width - 1;
            //    textBox1.Height = rect.Height - 1;
            //    string consultingRoom = myToString(dataGridView1.Rows[rowIndex].Cells[columnIndex].Value);
            //    textBox1.Text = consultingRoom;
            //    textBox1.Visible = true;
            //    textBox1.Focus();
            //}
            //else
            //{
            //    textBox1.Visible = false;
            //}
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {             
                if (dataGridView1.CurrentCell != null)
                    dataGridView1.CurrentCell.Value = textBox1.Text;
                e.SuppressKeyPress = true;
            }
        }

        private void dataGridView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (dataGridView1.CurrentCell is null)
                return;
            DataGridViewCell currentCell = dataGridView1.CurrentCell;
            if (currentCell.ColumnIndex == (int)Columns.NextValue)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (dataGridView1.CurrentCell != null)
                        dataGridView1.CurrentCell.Value = textBox1.Text;
                    e.SuppressKeyPress = true;
                }
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter && textBox1.Visible)
            {
                if (dataGridView1.CurrentCell != null)
                {
                    dataGridView1.BeginEdit(true);
                    //if (myToString(dataGridView1.Rows[dataGridView1.CurrentCell.RowIndex].Cells[(int)Columns.Category].Value) == "ADS")
                        dataGridView1.EditingControl.Text = textBox1.Text;
                }
                textBox1.Visible = false;
            }
            else if (keyData == Keys.Escape && textBox1.Visible)
            {
                textBox1.Visible = false;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void dataGridView1_MouseClick(object sender, MouseEventArgs e)
        {
            //DataGridViewCell currentCell = dataGridView1.CurrentCell;
            //if (currentCell is null)
            //{

            //    return;
            //}
            if (dataGridView1.CurrentCell is null)
            {
                textBox1.Visible = false;
                return;
            }
            DataGridViewCell currentCell = dataGridView1.CurrentCell;
            if (currentCell.ColumnIndex == (int)Columns.NextValue || currentCell.ColumnIndex == (int)Columns.Name)
            {
                int columnIndex = dataGridView1.CurrentCell.ColumnIndex;
                int rowIndex = dataGridView1.CurrentCell.RowIndex;
                Rectangle rect = dataGridView1.GetCellDisplayRectangle(columnIndex, rowIndex, false);

                textBox1.Left = rect.Left;
                textBox1.Top = rect.Top;
                textBox1.Width = rect.Width - 1;
                textBox1.Height = rect.Height - 1;
                string consultingRoom = myToString(dataGridView1.Rows[rowIndex].Cells[columnIndex].Value);
                textBox1.Text = consultingRoom;
                textBox1.Visible = true;
                textBox1.Focus();
            }
            else
            {
                textBox1.Visible = false;
            }
        }

        private void dataGridView1_Scroll(object sender, ScrollEventArgs e)
        {
            RedrawTextBox();
        }

        private void writeAllToolStripButton1_Click(object sender, EventArgs e)
        {
            //adsHandleCreate2();
            //writeAdsDataOnce();
            isUpdateOnce = true;
        }
    }
}

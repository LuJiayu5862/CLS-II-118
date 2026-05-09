using CsvHelper;
using CsvHelper.Configuration;
using MmTimer;
using NPOI.XSSF.UserModel;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Waveform;

namespace CLS_II
{
    public partial class UdpTest : Form
    {
        HPTimer mmTimer1;
        private const int mmInterval = 10;
        private bool isInit = false;

        #region ScopeView
        private readonly ScottPlot.Plottable.SignalPlot loggerSignal, loggerWatch;
        private readonly ScottPlot.Plot scopeView;
        private const int ScopeBuffer = (1000 * 60 / mmInterval) * 30;   // N分钟缓存区
        private int ScopeBufferCount = 0;
        private double[] SignalValue = new double[ScopeBuffer];
        private double[] WatchValue = new double[ScopeBuffer];

        private int ChannelID = 0;
        private bool isScopeStart = false;
        private bool isScopePause = false;
        private bool isScopeStop = false;

        private int checkedID = 0;
        private string transName = string.Empty;
        private string watchName = string.Empty;

        #endregion

        #region WaveForm
        private int nWaveForm = 0;
        private int periodT = 0;
        private double AmplitudeA = 0;
        private int RiseTimeRT = 0;
        private int Duration = 0;
        private double OffsetA = 0;
        private double beginFreq = 0;
        private double endFreq = 0;
        private bool isPeriodicalExtension = false;

        private bool isWaveFormChecked = false;

        private double nowFreq = 0;
        #endregion

        
        

        public UdpTest()
        {
            InitializeComponent();

            scopeView = formsPlot1.Plot;
            loggerSignal = scopeView.AddSignal(SignalValue, sampleRate: 1 / (double)mmInterval, label: "Signal");
            loggerWatch = scopeView.AddSignal(WatchValue, sampleRate: 1 / (double)mmInterval, label: "Watch");
            InitScopeView();
            
        }

        private void UdpTest_Load(object sender, EventArgs e)
        {
            // 初始化配置
            UdpWatch.Init();
            UdpConfig.ConfigFileInit();

            // 列表绑定与设置
            dataGridView1.DataSource = new BindingList<_UDPTransmit>(UdpWatch.UDPControls);
            dataGridView2.DataSource = new BindingList<_UDPTransmit>(UdpWatch.UDPParams);
            SetComboBox();

            // 树状图菜单设置
            InitTreeView(CLSConsts.EnabledChannels);
            treeView.ExpandAll();
            treeView.Nodes[0].Nodes[0].BackColor = Color.GreenYellow;
            ChannelID = 0;

            // 波形发生页面初始化
            InitToolStrip(CLSConsts.EnabledChannels);
            if (MultiLanguage.DefaultLanguage == "zh")
            {
                chartPreview.Series[0].Name = "单周期预览";
                scopeView.XLabel("时间轴(单位: ms)");
            }
            else
            {
                chartPreview.Series[0].Name = "Single Cycle\nPreview";
                scopeView.XLabel("Time Axis(Unit: ms)");
            }
            toolStripProgressBar1.Maximum = ScopeBuffer;

            // 打开普通定时器(30ms)
            timer1.Interval = 30;
            timer1.Tick += new EventHandler(HandleTime);
            timer1.Enabled = true;
            timer1.Start();

            // 打开高精度定时器(10ms)
            mmTimer1 = new HPTimer(mmInterval);
            mmTimer1.Ticked += new EventHandler(mmTimer1_Ticked);
            mmTimer1.Start();

            // 窗体排列设置
            ControlSizeLoad();
            

            this.isInit = true;
        }

        private void ControlSizeLoad()
        {
            ucSplitLine_H1.Width = this.Width - 3;
            tabControl1.Height = this.Height - ucSplitLine_H1.Location.Y - 2;
        }


        #region Timer1
        private void HandleTime(Object myObject, EventArgs myEventArgs)
        {
            // 更新操纵负荷状态显示
            read_UDPInfos(ChannelID);

            // 将准备值全部写入
            if (GlobalVar.isUdpAllAccept)
            {
                GlobalVar.isUdpAllAccept = false;
                this.BeginInvoke(new Action(() =>
                {
                    for (int rowIndex = 0; rowIndex < dataGridView1.RowCount; rowIndex++)
                    {
                        if (!String.IsNullOrEmpty(Convert.ToString(dataGridView1.Rows[rowIndex].Cells[2].Value)))
                        {
                            dataGridView1.Rows[rowIndex].Cells[1].Value = dataGridView1.Rows[rowIndex].Cells[2].Value;
                            dataGridView1.Rows[rowIndex].Cells[2].Value = String.Empty;
                        }
                    }
                    for (int rowIndex = 0; rowIndex < dataGridView2.RowCount; rowIndex++)
                    {
                        if (!String.IsNullOrEmpty(Convert.ToString(dataGridView2.Rows[rowIndex].Cells[2].Value)))
                        {
                            dataGridView2.Rows[rowIndex].Cells[1].Value = dataGridView2.Rows[rowIndex].Cells[2].Value;
                            dataGridView2.Rows[rowIndex].Cells[2].Value = String.Empty;
                            GlobalVar.isParamChanged = true;
                        }
                    }
                }));
                // 将datagridview中的周期数据与非周期数据导入UDP发送列表
                
            }
            if (!isScopeStart)
            {
                UdpWatch.write_UDPControls(ChannelID);
                UdpWatch.write_UDPParams(ChannelID);
            }

            // 处理全部复位与全部停止功能
            if (GlobalVar.isAllChannelStop || GlobalVar.isAllChannelReset)
            {
                if (GlobalVar.isAllChannelStop)
                {
                    GlobalVar.isAllChannelStop = false;
                    GlobalVar.isAllChannelReset = false;
                    UdpWatch.UDPControls[0].Value = "0";
                    UdpWatch.UDPControls[9].Value = "0";
                    comboBox1.SelectedIndex = 0;
                    comboBox2.SelectedIndex = 0;
                    lock (UdpData.LCSControls)
                    {
                        for (int id = 0; id < CLSConsts.TotalChannels; id++)
                        {
                            UdpData.LCSControls.Controls[id].CtrlCmd = 0;
                            UdpData.LCSControls.Controls[id].FnSwitch = 0;
                        }
                    }
                    dataGridView1.DataSource = new BindingList<_UDPTransmit>(UdpWatch.UDPControls);
                }
                else
                {
                    GlobalVar.isAllChannelStop = false;
                    GlobalVar.isAllChannelReset = false;
                    UdpWatch.UDPControls[0].Value = "10";
                    UdpWatch.UDPControls[9].Value = "0";
                    comboBox1.SelectedIndex = 9;
                    comboBox2.SelectedIndex = 0;
                    lock (UdpData.LCSControls)
                    {
                        for (int id = 0; id < CLSConsts.TotalChannels; id++)
                        {
                            UdpData.LCSControls.Controls[id].CtrlCmd = 10;
                            UdpData.LCSControls.Controls[id].FnSwitch = 0;
                        }
                    }
                    dataGridView1.DataSource = new BindingList<_UDPTransmit>(UdpWatch.UDPControls);
                }
            }

            // 保存周期参数与非周期参数到配置文件
            if (GlobalVar.isSaveAperiod)
            {
                GlobalVar.isSaveAperiod = false;
                UdpConfig.WriteConfigFile();
                if (MultiLanguage.DefaultLanguage == "zh")
                    MessageBox.Show("参数保存成功", "提示",MessageBoxButtons.OK);
                else
                    MessageBox.Show("Parameters saved successfully", "Info", MessageBoxButtons.OK);
            }

            // Scope刷新与相关数据显示刷新
            toolStripProgressBar1.Value = ScopeBufferCount > toolStripProgressBar1.Maximum ? toolStripProgressBar1.Maximum : ScopeBufferCount;
            if (isScopeStart)
            {
                if(ScopeBufferCount>0)
                    loggerSignal.MaxRenderIndex = loggerWatch.MaxRenderIndex = ScopeBufferCount - 1;
                if(nWaveForm == 3)
                {
                    nowFreqToolStripStatusLabel.Text = nowFreq.ToString("F2") + " Hz";
                }
                if (!isScopePause)
                {
                    if (ScopeBufferCount >= 5000 / mmInterval)
                    {
                        scopeView.SetAxisLimits(xMin: ScopeBufferCount * mmInterval - 5000, xMax: ScopeBufferCount * mmInterval);
                        loggerWatch.MinRenderIndex = loggerSignal.MinRenderIndex = ScopeBufferCount - 5000 / mmInterval;
                    }
                    else
                    {
                        scopeView.SetAxisLimits(xMin: 0, xMax: 5000);
                        loggerWatch.MinRenderIndex = loggerSignal.MinRenderIndex = 0;
                    }
                    scopeView.AxisAutoY();
                    if (ChannelID == checkedID)
                    {
                        UdpWatch.read_UDPControls(ChannelID);
                        dataGridView1.DataSource = new BindingList<_UDPTransmit>(UdpWatch.UDPControls);
                    }
                }
                else
                {
                    loggerWatch.MinRenderIndex = loggerSignal.MinRenderIndex = 0;
                }
            }
            formsPlot1.Refresh();
        }

        private void read_UDPInfos(int id)
        {
            lock (UdpData.LCSInfos)
            {
                tbState.Text = Convert.ToString(UdpData.LCSInfos.Infos[id].state);
                tbIsFading.Text = Convert.ToString(UdpData.LCSInfos.Infos[id].isFading);
                tbSafety.Text = Convert.ToString(UdpData.LCSInfos.Infos[id].safety);
                tbFwdPosition.Text = Convert.ToString(UdpData.LCSInfos.Infos[id].fwdPosition);
                tbFwdVelocity.Text = Convert.ToString(UdpData.LCSInfos.Infos[id].fwdVelocity);
                tbFwdForce.Text = Convert.ToString(UdpData.LCSInfos.Infos[id].fwdForce);
                tbCableForce.Text = Convert.ToString(UdpData.LCSInfos.Infos[id].cableForce);
                tbTrimPosition.Text = Convert.ToString(UdpData.LCSInfos.Infos[id].trimPosition);
                tbAftPosition.Text = Convert.ToString(UdpData.LCSInfos.Infos[id].aftPosition);
                tbReserve1.Text = Convert.ToString(UdpData.LCSInfos.Infos[id].reserve1);
                tbReserve2.Text = Convert.ToString(UdpData.LCSInfos.Infos[id].reserve2);
                tbReserve3.Text = Convert.ToString(UdpData.LCSInfos.Infos[id].reserve3);
                tbReserve4.Text = Convert.ToString(UdpData.LCSInfos.Infos[id].reserve4);

                ucMeter.Value = (decimal)UdpData.LCSInfos.Infos[id].fwdPosition;
            }
        }
        #endregion

        #region mmTimer1
        private void mmTimer1_Ticked(object sender, EventArgs e)
        {
            // 信号输出功能
            if (isScopeStart)
            {
                double outValue = 0;
                double watchValue = 0;
                if (!isScopePause)
                {
                    #region SignalValue[]
                    if (isWaveFormChecked)
                    {
                        switch (nWaveForm)
                        {
                            case 0:
                                outValue = WaveGenerator.SinForm(periodT, AmplitudeA, ScopeBufferCount * mmInterval);
                                outValue += OffsetA;
                                break;
                            case 1:
                                outValue = WaveGenerator.TriangleForm(periodT, AmplitudeA, ScopeBufferCount * mmInterval);
                                outValue += OffsetA;
                                break;
                            case 2:
                                outValue = WaveGenerator.TrapezoidForm(periodT, AmplitudeA, RiseTimeRT, ScopeBufferCount * mmInterval);
                                outValue += OffsetA;
                                break;
                            case 3:
                                outValue = WaveGenerator.SineSweepForm(Duration, AmplitudeA, beginFreq, endFreq, ScopeBufferCount * mmInterval, ref nowFreq, isPeriodicalExtension);
                                outValue += OffsetA;
                                break;
                        }
                    }
                    SignalValue[ScopeBufferCount] = outValue;
                    #endregion

                    #region WatchValue[]
                    if (checkedID >= 0)
                    {
                        string varName = watchName;
                        int channelID = checkedID;
                        lock (UdpData.LCSInfos)
                        {
                            foreach (FieldInfo field in typeof(_Feedback).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                            {
                                if (field.Name == varName)
                                {
                                    watchValue = Convert.ToDouble(field.GetValue(UdpData.LCSInfos.Infos[channelID]));
                                }
                            }
                        }
                    }
                    WatchValue[ScopeBufferCount] = watchValue;
                    #endregion

                    ScopeBufferCount++;
                    if (ScopeBufferCount >= ScopeBuffer)
                    {
                        startToolStripButton_Click(stopToolStripButton, null);
                    }

                    #region SetLCSControls
                    if (checkedID >= 0)
                    {
                        string varName = transName;
                        int channelID = checkedID;
                        lock (UdpData.LCSControls)
                        {
                            foreach (FieldInfo field in typeof(_Control).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                            {
                                if (field.Name == varName)
                                {
                                    object box = UdpData.LCSControls.Controls[channelID];
                                    field.SetValue(box, (Single)outValue);
                                    UdpData.LCSControls.Controls[channelID] = (_Control)box;
                                }
                            }
                        }                       
                    }
                    #endregion

                }
            }
            
            // 曲线绘制功能 (在Form.Timer中实现)
        }
        #endregion

        private void InitTreeView(int ChannelCount = CLSConsts.TotalChannels)
        {
            string NodeName = string.Empty;
            for (int i = 0; i < ChannelCount; i++)
            {
                if (MultiLanguage.DefaultLanguage == "zh")
                {
                    NodeName = "通道" + (i + 1);
                }
                else
                {
                    NodeName = "Channel" + (i + 1);
                }
                treeView.Nodes[0].Nodes.Add(NodeName);
            }            
        }

        private void InitToolStrip(int ChannelCount = CLSConsts.TotalChannels)
        {
            cbxChannelID.Items.Clear();
            cbxTransObj.Items.Clear();
            cbxWatchObj.Items.Clear();
            cbxTransObj.Items.Add("(None)");
            cbxWatchObj.Items.Add("(None)");
            for (int i = 0; i < ChannelCount; i++)
            {
                cbxChannelID.Items.Add((i + 1).ToString());
            }
            foreach (FieldInfo field in typeof(_Control).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.FieldType.Name == "Single")
                {
                    string name = field.Name;
                    cbxTransObj.Items.Add(name);
                }             
            }
            foreach (FieldInfo field in typeof(_Feedback).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                string name = field.Name;
                cbxWatchObj.Items.Add(name);
            }
            cbxChannelID.SelectedIndex = cbxTransObj.SelectedIndex = cbxWatchObj.SelectedIndex = 0;
        }

        private void InitScopeView()
        {
            loggerSignal.LineWidth = 2;
            loggerSignal.MarkerSize = 6;
            loggerSignal.StepDisplay = true;

            loggerWatch.LineWidth = 2;
            loggerWatch.MarkerSize = 6;
            loggerWatch.StepDisplay = true;

            scopeView.SetAxisLimits(xMin: 0, xMax: 5000);           
            scopeView.Legend();
        }

        private void SetComboBox()
        {
            SetComboBox1();
            SetComboBox2();
        }

        private void SetComboBox1()
        {
            DataGridViewCell CmdCell = dataGridView1.Rows[0].Cells[2];
            CmdCell.ReadOnly = true;
            int columnIndex = CmdCell.ColumnIndex;
            int rowIndex = CmdCell.RowIndex;
            Point p = this.dataGridView1.Location;
            Rectangle rect = dataGridView1.GetCellDisplayRectangle(columnIndex, rowIndex, false);
            comboBox1.Left = rect.Left + p.X;
            comboBox1.Top = rect.Top + p.Y + 1;
            comboBox1.Width = rect.Width;
            comboBox1.Height = rect.Height;
            comboBox1.Visible = true;
            comboBox1.SelectedIndex = 0;
            AdjustComboBoxDropDownListWidth(ref comboBox1);
        }

        private void SetComboBox2()
        {
            DataGridViewCell CmdCell = dataGridView1.Rows[9].Cells[2];
            CmdCell.ReadOnly = true;
            int columnIndex = CmdCell.ColumnIndex;
            int rowIndex = CmdCell.RowIndex;
            Point p = this.dataGridView1.Location;
            Rectangle rect = dataGridView1.GetCellDisplayRectangle(columnIndex, rowIndex, false);
            comboBox2.Left = rect.Left + p.X;
            comboBox2.Top = rect.Top + p.Y + 1;
            comboBox2.Width = rect.Width;
            comboBox2.Height = rect.Height;
            comboBox2.Visible = true;
            comboBox2.SelectedIndex = 0;
            AdjustComboBoxDropDownListWidth(ref comboBox2);
        }

        private void AdjustComboBoxDropDownListWidth(ref ComboBox comboBox)
        {
            int vertScrollBarWidth = (comboBox.Items.Count > comboBox.MaxDropDownItems) ? SystemInformation.VerticalScrollBarWidth : 0;

            int maxWidth = comboBox.DropDownWidth;
            foreach (var layouts in comboBox.Items)
            {
                int measureTextWidth = TextRenderer.MeasureText(layouts.ToString(), this.Font).Width;
                maxWidth = maxWidth < measureTextWidth ? measureTextWidth : maxWidth;
            }

            comboBox.DropDownWidth = maxWidth + vertScrollBarWidth;
        }

        #region SaveFile
        private bool isNewRecordData = false;
        private string startDateString = string.Empty;
        private string startTimeString = string.Empty;
        private bool SaveRecordData()
        {
            if (isNewRecordData)
            {
                string localFilePath = string.Empty;
                string fileNameExt = string.Empty;
                string dateString = startDateString;
                string timeString = startTimeString;
                string date_time = dateString + " " + timeString;
                DateTime dateTime = DateTime.ParseExact(date_time, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "csv file(*.csv)|*.csv|xlsx file(*.xlsx)|*.xlsx";
                saveFileDialog.FileName = dateTime.ToString("yyyyMMddHHmmssfff");
                saveFileDialog.DefaultExt = "csv";
                saveFileDialog.AddExtension = true;
                saveFileDialog.RestoreDirectory = true;
                isNewRecordData = false;

                DialogResult result = saveFileDialog.ShowDialog();
                if (result == DialogResult.OK)
                {
                    localFilePath = saveFileDialog.FileName;
                    fileNameExt = Path.GetExtension(localFilePath);
                    if (fileNameExt.ToLower() == ".csv")
                    {
                        WriteFile_CSV(localFilePath);

                    }
                    else if (fileNameExt.ToLower() == ".xlsx")
                    {
                        WriteFile_XLSX(localFilePath);
                    }
                    isNewRecordData = false;
                    return true;
                }

            }
            else
            {
                if (MultiLanguage.DefaultLanguage == "zh")
                {
                    MessageBox.Show("暂无需要保存的录制数据。", "保存");
                }
                else
                {
                    MessageBox.Show("No recording data needs to be saved temporarily.", "Save");
                }
            }
            return false;
        }
        #region csvWrite Class
        public class CsvInfo
        {
            public List<string> Info { get; set; }
        }

        public class CsvValue
        {
            public List<double> Value { get; set; }
        }

        public class CsvInfoDeepCopy
        {
            public List<string> Info { get; set; }
            public CsvInfoDeepCopy(CsvInfo source)
            {
                this.Info = source.Info;
            }
        }

        public class CsvValueDeepCopy
        {
            public List<double> Value { get; set; }
            public CsvValueDeepCopy(CsvValue source)
            {
                this.Value = source.Value;
            }
        }
        #endregion
        private void WriteFile_CSV(string filePath)
        {
            int pointCount = ScopeBufferCount;
            int pointInterval = mmInterval;
            string dateString = startDateString;
            string timeString = startTimeString;
            string date_time = dateString + " " + timeString;
            if (pointCount <= 0)
                return;
            var csvInfo = new List<CsvInfo>();
            var csvValue = new List<CsvValue>();
            CsvInfo info = new CsvInfo()
            {
                Info = new List<string>(),
            };
            csvInfo.Add(new CsvInfo() { Info = new List<string>() { "Start Time", date_time } });
            csvInfo.Add(new CsvInfo() { Info = new List<string>() { "Scope Points", pointCount.myToString() } });
            csvInfo.Add(new CsvInfo() { Info = new List<string>() { "Scope Interval(ms)", pointInterval.myToString() } });
            info.Info.Add(cbxTransObj.Text);
            info.Info.Add(cbxWatchObj.Text);
            csvInfo.Add(info);
            for (int i = 0; i < pointCount; i++)
            {
                CsvValue value = new CsvValue()
                {
                    Value = new List<double>(),
                };
                value.Value.Add(SignalValue[i]);
                value.Value.Add(WatchValue[i]);
                csvValue.Add(value);
            }

            // 创建StreamWriter对象来写入CSV文件
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
            };
            using (var writer = new StreamWriter(filePath))
            {
                // 创建CsvWriter实例，并传入StreamWriter对象
                using (var csv = new CsvWriter(writer, config))
                {
                    // csvWriter.Configuration.HasHeader = true; // 指定是否包含标题行
                    csv.WriteRecords(csvInfo);
                    csv.WriteRecords(csvValue);
                }
            }
            if (MultiLanguage.DefaultLanguage == "zh")
            {
                MessageBox.Show("保存成功。", "保存");
            }
            else
            {
                MessageBox.Show("Saved successfully.", "Save");
            }
        }


        //  工作表名称：ScopeDate
        //  Start Time          |   2023-12-12 9:39:39.039
        //  Scope Points        |   1212
        //  Scope Interval(ms)  |   10
        //  Var1                |   Var2    |   Var3    |   ...
        //  1.21                |   0.79    |   2.35    |   ...
        //  .                   |   .       |   .       |   ...
        //  .                   |   .       |   .       |   ...
        //  .                   |   .       |   .       |   ...
        private void WriteFile_XLSX(string filePath)
        {
            int pointCount = ScopeBufferCount;
            int pointInterval = mmInterval;
            string dateString = startDateString;
            string timeString = startTimeString;
            string date_time = dateString + " " + timeString;
            if (pointCount <= 0)
                return;
            // 新建工作簿对象
            XSSFWorkbook workBook = new XSSFWorkbook();
            // 为工作簿创建工作表
            XSSFSheet newSheet = (XSSFSheet)workBook.CreateSheet("ScopeDate");
            // 创建并修改单元格的值,创建行后，如果继续对行操作，需要GetRow()，否则会丢失原先行数据
            int rowOffset = 0;
            newSheet.CreateRow(rowOffset).CreateCell(0).SetCellValue("Start Time");
            newSheet.GetRow(rowOffset).CreateCell(1).SetCellValue(date_time);
            rowOffset++;
            newSheet.CreateRow(rowOffset).CreateCell(0).SetCellValue("Scope Points");
            newSheet.GetRow(rowOffset).CreateCell(1).SetCellValue(pointCount);
            rowOffset++;
            newSheet.CreateRow(rowOffset).CreateCell(0).SetCellValue("Scope Interval(ms)");
            newSheet.GetRow(rowOffset).CreateCell(1).SetCellValue(pointInterval);
            rowOffset++;

            newSheet.CreateRow(rowOffset).CreateCell(0).SetCellValue(cbxTransObj.Text);
            newSheet.GetRow(rowOffset).CreateCell(1).SetCellValue(cbxWatchObj.Text);
            
            rowOffset++;
            for (int i = 0; i < pointCount; i++)
            {
                newSheet.CreateRow(rowOffset).CreateCell(0).SetCellValue(SignalValue[i]);
                newSheet.GetRow(rowOffset).CreateCell(1).SetCellValue(WatchValue[i]);
                rowOffset++;
            }
            // 写入文件
            workBook.Write(new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite));
            if (MultiLanguage.DefaultLanguage == "zh")
            {
                MessageBox.Show("保存成功。", "保存");
            }
            else
            {
                MessageBox.Show("Saved successfully.", "Save");
            }
        }

        #endregion
        private void comboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentCell != null)
            {
                if (sender == comboBox1)
                {
                    dataGridView1.CurrentCell = dataGridView1.Rows[0].Cells[2];
                    if (comboBox1.SelectedIndex >= 0 && comboBox1.SelectedIndex <= 8)
                    {
                        dataGridView1.CurrentCell.Value = comboBox1.SelectedIndex.ToString();
                    }
                    else
                    {
                        if(comboBox1.SelectedIndex == 9)
                            dataGridView1.CurrentCell.Value = "10";
                    }
                }
                else if (sender == comboBox2)
                {
                    dataGridView1.CurrentCell = dataGridView1.Rows[9].Cells[2];
                    if (comboBox2.SelectedIndex >= 0 && comboBox2.SelectedIndex <= 4)
                    {
                        dataGridView1.CurrentCell.Value = comboBox2.SelectedIndex.ToString();
                    }
                    else
                    {
                        switch (comboBox2.SelectedIndex)
                        {
                            case 5:
                                dataGridView1.CurrentCell.Value = "65536";
                                break;
                            case 6:
                                dataGridView1.CurrentCell.Value = "131072";
                                break;
                        }
                    }
                }
                dataGridView1.Focus();
            }
        }

        private void comboBox_Click(object sender, EventArgs e)
        {
            if (sender == comboBox1)
            {
                dataGridView1.CurrentCell = dataGridView1.Rows[0].Cells[2];
            }
            else if (sender == comboBox2)
            {
                dataGridView1.CurrentCell = dataGridView1.Rows[9].Cells[2];
            }
        }

        private void dataGridView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender == dataGridView1)
            {
                if (dataGridView1.CurrentCell != null && e.KeyCode == Keys.Enter)
                {
                    int rowIndex = dataGridView1.CurrentCell.RowIndex;
                    if (!String.IsNullOrEmpty(Convert.ToString(dataGridView1.Rows[rowIndex].Cells[2].Value)))
                    {
                        dataGridView1.Rows[rowIndex].Cells[1].Value = dataGridView1.Rows[rowIndex].Cells[2].Value;
                        dataGridView1.Rows[rowIndex].Cells[2].Value = String.Empty;
                        UdpWatch.write_UDPControls(ChannelID);
                    }
                }
            }
            else if (sender == dataGridView2)
            {
                if (dataGridView2.CurrentCell != null && e.KeyCode == Keys.Enter)
                {
                    int rowIndex = dataGridView2.CurrentCell.RowIndex;
                    if (!String.IsNullOrEmpty(Convert.ToString(dataGridView2.Rows[rowIndex].Cells[2].Value)))
                    {
                        dataGridView2.Rows[rowIndex].Cells[1].Value = dataGridView2.Rows[rowIndex].Cells[2].Value;
                        dataGridView2.Rows[rowIndex].Cells[2].Value = String.Empty;
                        GlobalVar.isParamChanged = true;
                        UdpWatch.write_UDPParams(ChannelID);
                    }
                }
            }
        }

        private void dataGridView1_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (sender == dataGridView1)
            {
                if (e.ColumnIndex == 2)
                {
                    int index = e.RowIndex;
                    string type = UdpWatch.UDPControlsType[index].Name;
                    if (!String.IsNullOrEmpty(e.FormattedValue.ToString()))
                    {
                        switch (type)
                        {
                            case "Single":
                                if (!RegexMatch.isFloatingNum(e.FormattedValue.ToString()))
                                {
                                    dataGridView1.EditingControl.Text = Convert.ToString(dataGridView1.CurrentCell.Value);
                                    e.Cancel = true;
                                }
                                break;
                            case "Int32":
                                if (!RegexMatch.isInteger(e.FormattedValue.ToString()))
                                {
                                    dataGridView1.EditingControl.Text = Convert.ToString(dataGridView1.CurrentCell.Value);
                                    e.Cancel = true;
                                }
                                break;
                            case "UInt32":
                                if (!RegexMatch.isPositiveInteger(e.FormattedValue.ToString()))
                                {
                                    dataGridView1.EditingControl.Text = Convert.ToString(dataGridView1.CurrentCell.Value);
                                    e.Cancel = true;
                                }
                                break;

                        }
                    }                 
                }
            }
            else if (sender == dataGridView2)
            {
                if (e.ColumnIndex == 2)
                {
                    int index = e.RowIndex;
                    string type = UdpWatch.UDPParamsType[index].Name;
                    if (!String.IsNullOrEmpty(e.FormattedValue.ToString()))
                    {
                        switch (type)
                        {
                            case "Single":
                                if (!RegexMatch.isFloatingNum(e.FormattedValue.ToString()))
                                {
                                    dataGridView2.EditingControl.Text = Convert.ToString(dataGridView2.CurrentCell.Value);
                                    e.Cancel = true;
                                }
                                break;
                            case "Int32":
                                if (!RegexMatch.isInteger(e.FormattedValue.ToString()))
                                {
                                    dataGridView2.EditingControl.Text = Convert.ToString(dataGridView2.CurrentCell.Value);
                                    e.Cancel = true;
                                }
                                break;
                            case "UInt32":
                                if (!RegexMatch.isPositiveInteger(e.FormattedValue.ToString()))
                                {
                                    dataGridView2.EditingControl.Text = Convert.ToString(dataGridView2.CurrentCell.Value);
                                    e.Cancel = true;
                                }
                                break;

                        }
                    }
                }
            }
        }

        private void UdpTest_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 关闭定时器
            timer1.Stop();
            mmTimer1.Stop();
        }

        private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            int id = treeView.SelectedNode.Index;
            if (e.Node.Parent == null)
            {
                return;
            }
            if (id >= 0 && id < CLSConsts.TotalChannels)
            {
                if (ChannelID != id)
                {
                    treeView.Nodes[0].Nodes[ChannelID].BackColor = SystemColors.Window;
                    ChannelID = id;
                    
                    treeView.Nodes[0].Nodes[ChannelID].BackColor = Color.GreenYellow;
                    comboBox1.SelectedIndex = comboBox2.SelectedIndex = -1;
                    UdpWatch.read_UDPControls(ChannelID);
                    UdpWatch.read_UDPParams(ChannelID);
                    dataGridView1.DataSource = new BindingList<_UDPTransmit>(UdpWatch.UDPControls);
                    dataGridView2.DataSource = new BindingList<_UDPTransmit>(UdpWatch.UDPParams);
                    read_UDPInfos(ChannelID);
                }
            }
        }

        private void startToolStripButton_Click(object sender, EventArgs e)
        {
            // 开始按钮，可以通过isScopeStart == true && isScopePause == false来开启波形输出
            if (sender == startToolStripButton)
            {
                checkedID = cbxChannelID.SelectedIndex;
                transName = cbxTransObj.Text;
                watchName = cbxWatchObj.Text;

                if (!isScopeStart)
                {
                    DateTime currentDate = DateTime.Now;
                    startDateString = currentDate.ToString("yyyy-MM-dd");
                    startTimeString = currentDate.ToString("HH:mm:ss.fff");
                }
                isScopeStart = true;
                isScopePause = false;
                isScopeStop = false;
                startToolStripButton.BackColor = Color.LightGray;
                pauseToolStripButton.BackColor = Color.Transparent;
            }

            // 暂停按钮，在已经开始记录/输出的情况下生效，按下后可以继续进行波形记录，但停止波形输出
            else if (sender == pauseToolStripButton)
            {
                if (isScopeStart)
                {
                    isScopePause = true;
                    startToolStripButton.BackColor = Color.Transparent;
                    pauseToolStripButton.BackColor = Color.LightGray;
                }
            }
            // 停止按钮，在已经开始记录/输出的情况下生效，按下后停止记录与输出，并将波形记录显示为full模式
            else if (sender == stopToolStripButton)
            {
                if (isScopeStart)
                {
                    UdpWatch.read_UDPControls(ChannelID);
                    isScopeStop = true;
                    isScopeStart = false;
                    isScopePause = false;
                }
                if (ScopeBufferCount > 0)
                {
                    isNewRecordData = true;
                }
                //ScopeBufferCount = 0;
                loggerWatch.MinRenderIndex = loggerSignal.MinRenderIndex = 0;
                startToolStripButton.BackColor = Color.Transparent;
                pauseToolStripButton.BackColor = Color.Transparent;
            }

            if (isScopeStart)
            {
                tabControl2.Enabled = false;
                cbxChannelID.Enabled = false;
                cbxTransObj.Enabled = false;
                cbxWatchObj.Enabled = false;
                nowWaveToolStripStatusLabel.Text = tabControl2.TabPages[nWaveForm].Text;
                if (nWaveForm < 3)
                {
                    double nowFreq = 1000d / periodT;
                    this.nowFreq = nowFreq;
                    nowFreqToolStripStatusLabel.Text = nowFreq.ToString("F2") + " Hz";
                }
                else
                {
                    nowFreqToolStripStatusLabel.Text = "- Hz";
                }
            }
            else
            {
                tabControl2.Enabled = true;
                cbxChannelID.Enabled = true;
                cbxTransObj.Enabled = true;
                cbxWatchObj.Enabled = true;
                nowFreqToolStripStatusLabel.Text = "- Hz";
                nowWaveToolStripStatusLabel.Text = "-";
            }
        }

        private void cbxPeriodicExtension_CheckedChanged(object sender, EventArgs e)
        {
            isPeriodicalExtension = cbxPeriodicExtension.Checked;
        }

        private void btnWaveConfirm_Click(object sender, EventArgs e)
        {
            int index = tabControl2.SelectedIndex;
            chartPreview.Series[0].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            chartPreview.Series[0].MarkerSize = 2;
            isWaveFormChecked = false;
            switch (index)
            {
                case 0:
                    if (tbxSinPeriod.Text.Trim() != string.Empty && tbxSinAmplitude.Text.Trim() != string.Empty)
                    {
                        chartPreview.Series[0].Points.Clear();
                        int T = int.Parse(tbxSinPeriod.Text.Trim());
                        double A = double.Parse(tbxSinAmplitude.Text.Trim());
                        double offset = tbxSinOffset.Text.Trim() != string.Empty ? double.Parse(tbxSinOffset.Text.Trim()) : 0;
                        for (int i = 0; i <= 100; i++)
                        {
                            chartPreview.Series[0].Points.AddXY(T * i / 100, WaveGenerator.SinForm(T, A, T * i / 100) + offset);
                        }
                        periodT = T;
                        AmplitudeA = A;
                        OffsetA = offset;
                        RiseTimeRT = 0;
                        nWaveForm = index;
                        chartPreview.Show();
                        isWaveFormChecked = true;
                    }
                    break;
                case 1:
                    if (tbxTrianglePeriod.Text.Trim() != string.Empty && tbxTriangleAmplitude.Text.Trim() != string.Empty)
                    {
                        chartPreview.Series[0].Points.Clear();
                        int T = int.Parse(tbxTrianglePeriod.Text.Trim());
                        double A = double.Parse(tbxTriangleAmplitude.Text.Trim());
                        double offset = tbxTriangleOffset.Text.Trim() != string.Empty ? double.Parse(tbxTriangleOffset.Text.Trim()) : 0;
                        for (int i = 0; i <= 100; i++)
                        {
                            chartPreview.Series[0].Points.AddXY(T * i / 100, WaveGenerator.TriangleForm(T, A, T * i / 100) + offset);
                        }
                        periodT = T;
                        AmplitudeA = A;
                        OffsetA = offset;
                        RiseTimeRT = 0;
                        nWaveForm = index;
                        chartPreview.Show();
                        isWaveFormChecked = true;
                    }
                    break;
                case 2:
                    if (tbxStepPeriod.Text.Trim() != string.Empty && tbxStepAmplitude.Text.Trim() != string.Empty)
                    {
                        chartPreview.Series[0].Points.Clear();
                        int T = int.Parse(tbxStepPeriod.Text.Trim());
                        double A = double.Parse(tbxStepAmplitude.Text.Trim());
                        double offset = tbxStepOffset.Text.Trim() != string.Empty ? double.Parse(tbxStepOffset.Text.Trim()) : 0;
                        int RT = 0;
                        if(tbxStepRiseTime.Text.Trim() != string.Empty)
                            RT = int.Parse(tbxStepRiseTime.Text.Trim());
                        for (int i = 0; i <= 100; i++)
                        {
                            chartPreview.Series[0].Points.AddXY(T * i / 100, WaveGenerator.TrapezoidForm(T, A, RT, T * i / 100) + offset);
                        }
                        periodT = T;
                        AmplitudeA = A;
                        OffsetA = offset;
                        RiseTimeRT = RT;
                        nWaveForm = index;
                        chartPreview.Show();
                        isWaveFormChecked = true;
                    }
                    break;
                case 3:
                    if (tbxSweepDuration.Text.Trim() != string.Empty && tbxSweepAmplitude.Text.Trim() != string.Empty &&
                        tbxSweepBeginFreq.Text.Trim() != string.Empty && tbxSweepEndFreq.Text.Trim() != string.Empty)
                    {
                        chartPreview.Series[0].Points.Clear();
                        int T = int.Parse(tbxSweepDuration.Text.Trim());
                        double A = double.Parse(tbxSweepAmplitude.Text.Trim());
                        double offset = tbxSweepOffset.Text.Trim() != string.Empty ? double.Parse(tbxSweepOffset.Text.Trim()) : 0;
                        double f1 = double.Parse(tbxSweepBeginFreq.Text.Trim());
                        double f2 = double.Parse(tbxSweepEndFreq.Text.Trim());
                        bool bPeriodical = cbxPeriodicExtension.Checked;
                        double tmp = 0;
                        for (int i = 0; i <= T/20; i++)
                        {
                            chartPreview.Series[0].Points.AddXY(i * 20, WaveGenerator.SineSweepForm(T, A, f1, f2, i * 20, ref tmp) + offset);
                        }
                        Duration = T;
                        AmplitudeA = A;
                        OffsetA = offset;
                        beginFreq = f1;
                        endFreq = f2;
                        isPeriodicalExtension = bPeriodical;
                        nWaveForm = index;
                        chartPreview.Show();
                        isWaveFormChecked = true;
                    }
                    break;
            }
        }

        private void dataGridView1_SizeChanged(object sender, EventArgs e)
        {
            if(isInit)
                SetComboBox();
        }

        private void UdpTest_SizeChanged(object sender, EventArgs e)
        {
            ControlSizeLoad();
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            loggerSignal.IsHighlighted = !loggerSignal.IsHighlighted;
        }

        private void btnSaveData_Click(object sender, EventArgs e)
        {
            SaveRecordData();
        }

        private void enter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SendKeys.Send("{Tab}");
            }
        }
    }
}

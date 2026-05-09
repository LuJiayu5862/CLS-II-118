using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MmTimer;
using NPOI.XSSF.UserModel;
using ScottPlot;
using ScottPlot.Plottable;
using CsvHelper;
using CsvHelper.Configuration;

namespace CLS_II
{
    public partial class ScopeView_XY : Form
    {
        #region Winform
        private enum _SourceMode
        {
            RealTimeRecording = 0,
            FromFileData = 1
        }

        private enum _WaveStyle
        {
            Vector = 0,
            Scatter = 1,
            VectorScatter = 2,
        }

        private int SourceMode = (int)_SourceMode.RealTimeRecording;
        private int WaveStyle = (int)_WaveStyle.Vector;
        #endregion

        #region ScopeView  
        HPTimer mmTimer1;
        private const int mmInterval = 10;
        private const int timerInterval = 30;
        private readonly ScottPlot.Plot scopeView, scopeViewXY;
        private const int ScopeBuffer = (1000 * 60 / mmInterval) * 30;   // N分钟缓存区
        private int ScopeBufferCount = 0;
        private int ScopeBufferLength_XY = (1000 / mmInterval) * 10;   // N秒缓存区
        private bool isScopeStart = false;
        private bool isScopePause = false;
        private bool isScopeStop = false;
        private bool isNewRecordData = false;
        private bool isScopeStart2 = false;
        private bool isScopePause2 = false;
        private double ScopeBufferCount2 = 0;
        private double CountSpan = 1;
        private List<Double[]> ScopeVariableValueList = new List<Double[]>();
        private double[] timeValue = new double[ScopeBuffer];
        #region YT_Scope
        private List<SignalPlot> ScopeSignalList = new List<SignalPlot>();
        #endregion

        private List<ScatterPlot> ScopeSignalList_XY = new List<ScatterPlot>();
        private List<ArrowCoordinated> ArrowList_XY = new List<ArrowCoordinated>();
        private List<ArrowCoordinated> BaseList_XY = new List<ArrowCoordinated>();
        string[] customColors = {
            "#3fd9e4",
            "#e8128d",
            "#79a900",
            "#2350d4",
            "#d7c946",
            "#a661eb",
            "#187000",
            "#aa18a5",
            "#89d97b",
            "#b90039",
            "#00d7b7",
            "#f58011",
            "#006fc8",
            "#ffb338",
            "#b0a4ff",
            "#ad9200",
            "#ff9aef",
            "#00b07d",
            "#95265a",
            "#017d68",
            "#ff805e",
            "#019bc3",
            "#952f1b",
            "#88c8ff",
            "#866900",
            "#938cc1",
            "#4a5901",
            "#7d3b6d",
            "#d7c687",
            "#b98264"
        };
        #endregion

        #region VariableGridView
        public class _ScopeVariety
        {
            public string Visible { get; set; }
            public string Name { get; set; }
            public Double Value { get; set; }
            public string Color { get; set; }

            public _ScopeVariety() { }
        }

        public class _ScopeVariety2
        {
            public string Visible { get; set; }
            public string X_Series { get; set; }
            public string Y_Series { get; set; }
            public _ScopeVariety2() { }
        }

        public class _ScopeValue
        {
            public string Name { get; set; }
            public Double Value { get; set; }

            public _ScopeValue() { }
        }

        public enum Columns
        {
            Visible = 0,
            Name = 1,
            Value = 2,
            Color = 3


        }
        private List<_ScopeVariety> ScopeVariableList = new List<_ScopeVariety>();
        private List<_ScopeValue> ScopeValues = new List<_ScopeValue>();
        private BindingSource source = new BindingSource();
        private List<_ScopeVariety2> ScopeVariableList2 = new List<_ScopeVariety2>();
        private BindingSource source2 = new BindingSource();
        private BindingSource source_combo = new BindingSource();
        private List<string> comboItems = new List<string>();

        #endregion

        public ScopeView_XY()
        {
            InitializeComponent();

            scopeView = formsPlot1.Plot;
            scopeViewXY = formsPlot2.Plot;
        }

        private void ScopeView_Load(object sender, EventArgs e)
        {
            // 窗体排列设置
            ControlSizeLoad();
            RealTimeScopeMode();
            scopeView.XLabel("Time Axis(Unit: ms)");
            ToolStripControlHost controlHost1 = new ToolStripControlHost(maskedTextBox1);
            ToolStripControlHost controlHost2 = new ToolStripControlHost(maskedTextBox2);
            maskedTextBox1.Mask = "00:00.000";  // 设置掩码，按照"mm:ss.fff"格式
            maskedTextBox2.Mask = "00:00.000";
            maskedTextBox1.ValidatingType = typeof(DateTime);   // 允许用户输入有效日期时间值
            maskedTextBox2.ValidatingType = typeof(DateTime);
            maskedTextBox1.CausesValidation = true; // 启用输入掩码控件验证
            maskedTextBox2.CausesValidation = true;
            int index1 = toolStrip2.Items.IndexOf(toolStripTextBox1);
            int index2 = toolStrip2.Items.IndexOf(toolStripTextBox2);
            toolStrip2.Items.Remove(toolStripTextBox1);
            toolStrip2.Items.Remove(toolStripTextBox2);
            toolStrip2.Items.Insert(index1, controlHost1);
            toolStrip2.Items.Insert(index2, controlHost2);
            toolStripComboBox1.SelectedIndex = 2;

            // 数据列表初始化
            this.source.DataSource = ScopeVariableList;
            dataGridView1.DataSource = this.source;
            this.source2.DataSource = ScopeVariableList2;
            dataGridView2.DataSource = this.source2;
            this.source_combo.DataSource = comboItems;
            for (int i = 0; i < ScopeBuffer; i++)
            {
                timeValue[i] = mmInterval * i;
            }
            // 获取comboBoxColumn的引用
            DataGridViewComboBoxColumn comboBoxColumn1 = dataGridView2.Columns["X_Series"] as DataGridViewComboBoxColumn;
            comboBoxColumn1.DataSource = this.source_combo;
            DataGridViewComboBoxColumn comboBoxColumn2 = dataGridView2.Columns["Y_Series"] as DataGridViewComboBoxColumn;
            comboBoxColumn2.DataSource = this.source_combo;
            this.source_combo.Clear();
            this.source_combo.Add("(None)");

            // 双缓冲防止闪烁
            dataGridView1.DoubleBufferedDataGirdView(true);
            dataGridView2.DoubleBufferedDataGirdView(true);

            // 窗体显示设置
            DateTime currentDate = DateTime.Now;
            lblDate.Text = currentDate.ToString("yyyy-MM-dd");

            // 波形显示相关初始化
            toolStripProgressBar1.Maximum = ScopeBuffer;

            // 打开普通定时器
            timer1.Interval = timerInterval;
            timer1.Tick += new EventHandler(HandleTime);
            timer1.Enabled = true;
            timer1.Start();

            // 打开高精度定时器(10ms)
            mmTimer1 = new HPTimer(mmInterval);
            mmTimer1.Ticked += new EventHandler(mmTimer1_Ticked);
            mmTimer1.Start();

            // 隐藏
            btnPause.Visible = false;
        }

        private void ControlSizeLoad()
        {
            ucSplitLine_H1.Width = this.Width - 3;
            panel1.Height = this.Height - ucSplitLine_H1.Location.Y - 2;
        }

        #region Timer1
        private int TimerCount1 = 0, TimerCount2 = 0;
        private const int TimerPeriod1 = 3;
        private int TimerPeriod2 = 6;
        private int lstWaveMode = 0;
        const int AxisXLength = 10000;
        private void HandleTime(Object myObject, EventArgs myEventArgs)
        {
            if (SourceMode == (int)_SourceMode.RealTimeRecording)
            {
                // 每90ms刷新一次datagridview数据显示
                if (++TimerCount1 == TimerPeriod1)
                {
                    updateDataGridView();
                    TimerCount1 = 0;
                }
                // 每180ms刷新一次scopeView显示
                if (++TimerCount2 == TimerPeriod2)
                {
                    if (isScopeStart)
                        formsPlot1.Refresh();
                    TimerCount2 = 0;
                }
                // Scope刷新与相关数据显示刷新
                if (isScopeStart)
                {
                    toolStripProgressBar1.Value = ScopeBufferCount > toolStripProgressBar1.Maximum ? toolStripProgressBar1.Maximum : ScopeBufferCount;
                    TimeSpan timeSpan = TimeSpan.FromMilliseconds((ScopeBufferCount - 1) * mmInterval);
                    string formattedTime = timeSpan.ToString(@"mm\:ss\.fff");
                    lblPosition.Text = formattedTime;

                    #region YT_Scope
                    lock (ScopeSignalList)
                    {
                        if (ScopeBufferCount > 0)
                        {
                            for (int i = 0; i < ScopeSignalList.Count; i++)
                            {
                                ScopeSignalList[i].MaxRenderIndex = ScopeBufferCount - 1;
                            }
                        }
                        if (!isScopePause)
                        {
                            if (ScopeBufferCount >= AxisXLength / mmInterval)
                            {
                                scopeView.SetAxisLimits(xMin: ScopeBufferCount * mmInterval - AxisXLength, xMax: ScopeBufferCount * mmInterval);
                                for (int i = 0; i < ScopeSignalList.Count; i++)
                                {
                                    ScopeSignalList[i].MinRenderIndex = ScopeBufferCount - AxisXLength / mmInterval;
                                }
                            }
                            else
                            {
                                scopeView.SetAxisLimits(xMin: 0, xMax: AxisXLength);
                                for (int i = 0; i < ScopeSignalList.Count; i++)
                                {
                                    ScopeSignalList[i].MinRenderIndex = 0;
                                }
                            }
                            scopeView.AxisAutoY();
                        }
                        else
                        {
                            for (int i = 0; i < ScopeSignalList.Count; i++)
                            {
                                ScopeSignalList[i].MinRenderIndex = 0;
                            }
                        }
                    }
                    #endregion
                    lock (ScopeSignalList_XY)
                    {
                        if (ScopeBufferCount > 0)
                        {
                            for (int i = 0; i < ScopeSignalList_XY.Count; i++)
                            {
                                ScopeSignalList_XY[i].MaxRenderIndex = ScopeBufferCount - 1;
                                ArrowList_XY[i].Tip = new Coordinate(ScopeSignalList_XY[i].Xs[ScopeBufferCount - 1], ScopeSignalList_XY[i].Ys[ScopeBufferCount - 1]);
                                int index = (ScopeBufferCount - 2) >= 0 ? (ScopeBufferCount - 2) : 0;
                                ArrowList_XY[i].Base = new Coordinate(ScopeSignalList_XY[i].Xs[index], ScopeSignalList_XY[i].Ys[index]);
                            }
                        }
                        if (!isScopePause)
                        {
                            if (ScopeBufferCount >= ScopeBufferLength_XY)
                            {
                                for (int i = 0; i < ScopeSignalList_XY.Count; i++)
                                {
                                    ScopeSignalList_XY[i].MinRenderIndex = ScopeBufferCount - ScopeBufferLength_XY;
                                    int index = (ScopeBufferCount - ScopeBufferLength_XY - 1) >= 0 ? (ScopeBufferCount - ScopeBufferLength_XY - 1) : 0;
                                    BaseList_XY[i].Tip = BaseList_XY[i].Base = new Coordinate(ScopeSignalList_XY[i].Xs[index], ScopeSignalList_XY[i].Ys[index]);
                                    BaseList_XY[i].MarkerSize = 15;
                                }
                            }
                            else
                            {
                                for (int i = 0; i < ScopeSignalList_XY.Count; i++)
                                {
                                    ScopeSignalList_XY[i].MinRenderIndex = 0;
                                    BaseList_XY[i].Tip = BaseList_XY[i].Base = new Coordinate(ScopeSignalList_XY[i].Xs[0], ScopeSignalList_XY[i].Ys[0]);
                                    BaseList_XY[i].MarkerSize = 15;
                                }
                            }
                            scopeViewXY.AxisAuto();
                        }
                        else
                        {
                            for (int i = 0; i < ScopeSignalList_XY.Count; i++)
                            {
                                ScopeSignalList_XY[i].MinRenderIndex = 0;
                            }
                        }
                    }
                    formsPlot2.Refresh();
                }
            }
            else if (SourceMode == (int)_SourceMode.FromFileData)
            {
                // 每30ms刷新一次datagridview数据显示
                updateDataGridView2();
                // 每30ms刷新一次scopeView显示
                formsPlot1.Refresh();
                // Scope刷新与相关数据显示刷新
                if (isScopeStart2 && !isScopePause2)
                {
                    int minTime = 0, maxTime = 0;
                    DateTime dateTimeMin = DateTime.ParseExact(maskedTextBox1.Text, "mm:ss.fff", CultureInfo.InvariantCulture);
                    DateTime dateTimeMax = DateTime.ParseExact(maskedTextBox2.Text, "mm:ss.fff", CultureInfo.InvariantCulture);
                    minTime = dateTimeMin.Minute * 60 * 1000 + dateTimeMin.Second * 1000 + dateTimeMin.Millisecond;
                    maxTime = dateTimeMax.Minute * 60 * 1000 + dateTimeMax.Second * 1000 + dateTimeMax.Millisecond;

                    TimeSpan timeSpan = TimeSpan.FromMilliseconds((ScopeBufferCount2 - 1) * mmInterval);
                    string formattedTime = timeSpan.ToString(@"mm\:ss\.fff");
                    tbxNowTime.Text = formattedTime;
                    lock (ScopeSignalList)
                    {
                        if (ScopeBufferCount2 > 0)
                        {
                            int floor = (int)Math.Floor(ScopeBufferCount2);
                            int maxIdx = Math.Max(0, floor - 1);
                            for (int i = 0; i < ScopeSignalList.Count; i++)
                            {
                                ScopeSignalList[i].MaxRenderIndex = maxIdx;
                            }
                        }
                        if (ScopeBufferCount2 - minTime / mmInterval >= AxisXLength / mmInterval)
                        {
                            scopeView.SetAxisLimits(xMin: ScopeBufferCount2 * mmInterval - AxisXLength, xMax: ScopeBufferCount2 * mmInterval);
                            for (int i = 0; i < ScopeSignalList.Count; i++)
                            {
                                ScopeSignalList[i].MinRenderIndex = (int)Math.Floor(ScopeBufferCount2) - AxisXLength / mmInterval;
                            }
                        }
                        else
                        {
                            scopeView.SetAxisLimits(xMin: minTime, xMax: minTime + AxisXLength);
                            for (int i = 0; i < ScopeSignalList.Count; i++)
                            {
                                ScopeSignalList[i].MinRenderIndex = minTime / mmInterval;
                            }
                        }
                        scopeView.AxisAutoY();
                    }
                    lock (ScopeSignalList_XY)
                    {
                        if (ScopeBufferCount2 > 0)
                        {
                            int floor = (int)Math.Floor(ScopeBufferCount2);
                            int tipIndex = Math.Max(0, floor - 1);
                            int baseIndex = Math.Max(0, floor - 2);
                            int maxIdx = Math.Max(0, floor - 1);

                            for (int i = 0; i < ScopeSignalList_XY.Count; i++)
                            {
                                ScopeSignalList_XY[i].MaxRenderIndex = maxIdx;
                                ArrowList_XY[i].Tip = new Coordinate(ScopeSignalList_XY[i].Xs[tipIndex], ScopeSignalList_XY[i].Ys[tipIndex]);
                                ArrowList_XY[i].Base = new Coordinate(ScopeSignalList_XY[i].Xs[baseIndex], ScopeSignalList_XY[i].Ys[baseIndex]);
                            }
                        }
                        if (ScopeBufferCount2 - minTime / mmInterval >= ScopeBufferLength_XY)
                        {
                            for (int i = 0; i < ScopeSignalList_XY.Count; i++)
                            {
                                ScopeSignalList_XY[i].MinRenderIndex = (int)Math.Floor(ScopeBufferCount2) - ScopeBufferLength_XY;
                                int index = ((int)Math.Floor(ScopeBufferCount2) - ScopeBufferLength_XY - 1) >= 0 ? ((int)Math.Floor(ScopeBufferCount2) - ScopeBufferLength_XY - 1) : 0;
                                BaseList_XY[i].Tip = BaseList_XY[i].Base = new Coordinate(ScopeSignalList_XY[i].Xs[index], ScopeSignalList_XY[i].Ys[index]);
                                BaseList_XY[i].MarkerSize = 15;
                            }
                        }
                        else
                        {
                            for (int i = 0; i < ScopeSignalList_XY.Count; i++)
                            {
                                ScopeSignalList_XY[i].MinRenderIndex = minTime / mmInterval;
                                int index = (minTime / mmInterval) >= 0 ? (minTime / mmInterval) : 0;
                                BaseList_XY[i].Tip = BaseList_XY[i].Base = new Coordinate(ScopeSignalList_XY[i].Xs[index], ScopeSignalList_XY[i].Ys[index]);
                                BaseList_XY[i].MarkerSize = 15;
                            }
                        }
                        scopeViewXY.AxisAuto();
                    }
                    formsPlot2.Refresh();
                }
            }
        }


        private void updateDataGridView()
        {
            lock (ScopeValues)
            {
                lock (this.source)
                {
                    for (int i = 0; i < dataGridView1.Rows.Count; i++)
                    {
                        string name = dataGridView1.Rows[i].Cells[(int)Columns.Name].Value.ToString();
                        for (int j = 0; j < ScopeValues.Count; j++)
                        {
                            if (name == ScopeValues[j].Name)
                            {
                                dataGridView1.Rows[i].Cells[(int)Columns.Value].Value = ScopeValues[j].Value;
                            }
                        }
                    }
                }
            }
        }

        private void updateDataGridView2()
        {
            lock (ScopeVariableValueList)
            {
                lock (this.source)
                {
                    for (int j = 0; j < ScopeVariableValueList.Count; j++)
                    {
                        dataGridView1.Rows[j].Cells[(int)Columns.Value].Value = ScopeVariableValueList[j][(int)Math.Floor(ScopeBufferCount2)];
                    }
                }
            }
        }
        #endregion

        #region mmTimer1
        static Stopwatch watch = Stopwatch.StartNew();
        private void mmTimer1_Ticked(object sender, EventArgs e)
        {
            //while (watch.Elapsed.TotalMilliseconds * 1000 < mmTimer1.Interval * 999.9) ;   // 可以极大增加精准度
            //watch.Stop();
            //TimeSpan ticktok = watch.Elapsed;
            //watch.Reset();
            //watch.Restart();
            // 当在实时录制模式下时，进行数据实时读取，若开始录制，则实时将数据存入数组列表中
            #region RealTimeRecording
            if (SourceMode == (int)_SourceMode.RealTimeRecording)
            {
                // 更新数据
                readScopeValueOnce();

                // 若开始录制，则实时将数据放入数组列表中
                if (isScopeStart)
                {
                    lock (ScopeVariableValueList)
                    {
                        lock (ScopeValues)
                        {
                            for (int i = 0; i < ScopeVariableValueList.Count; i++)
                            {
                                ScopeVariableValueList[i][ScopeBufferCount] = ScopeValues[i].Value;
                            }
                            ScopeBufferCount++;
                            if (ScopeBufferCount >= ScopeBuffer)
                            {
                                this.BeginInvoke(new Action(() =>
                                {
                                    btnStart_Click(btnStop, null);
                                }));
                            }
                        }
                    }
                }
            }
            #endregion
            #region FromFileData
            else if (SourceMode == (int)_SourceMode.FromFileData)
            {
                // 若开始播放，则实时将数据放入数组列表中
                if (isScopeStart2)
                {
                    int minTime = 0, maxTime = 0;
                    DateTime dateTimeMin = DateTime.ParseExact(maskedTextBox1.Text, "mm:ss.fff", CultureInfo.InvariantCulture);
                    DateTime dateTimeMax = DateTime.ParseExact(maskedTextBox2.Text, "mm:ss.fff", CultureInfo.InvariantCulture);
                    minTime = dateTimeMin.Minute * 60 * 1000 + dateTimeMin.Second * 1000 + dateTimeMin.Millisecond;
                    maxTime = dateTimeMax.Minute * 60 * 1000 + dateTimeMax.Second * 1000 + dateTimeMax.Millisecond;
                    if (isScopePause2)
                        return;
                    ScopeBufferCount2 += CountSpan;

                    if (ScopeBufferCount2 >= maxTime / mmInterval)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            isScopeStart2 = false;
                            isScopePause2 = false;
                            btnStart2.BackColor = SystemColors.Control;
                            btnPause2.BackColor = SystemColors.Control;
                            maskedTextBox1.ReadOnly = maskedTextBox2.ReadOnly = false;
                        }));
                        ScopeBufferCount2 = minTime / mmInterval;
                        isScopeStart2 = false;
                        lock (ScopeSignalList)
                        {
                            for (int i = 0; i < ScopeSignalList.Count; i++)
                            {
                                ScopeSignalList[i].MinRenderIndex = minTime / mmInterval;
                            }
                        }
                    }
                }
            }
            #endregion
        }
        #endregion

        private void ScopeView_SizeChanged(object sender, EventArgs e)
        {
            ControlSizeLoad();
        }

        private void btnSource_Click(object sender, EventArgs e)
        {
            btnStart_Click(btnStop, null);
            btnStart2_Click(btnPause2, null);
            ScopeBufferCount = 0;
            ScopeBufferCount2 = 0;
            if (sender == btnRealTimeRecording)
            {
                btnRealTimeRecording.Checked = true;
                btnFromFileData.Checked = false;
                if (SourceMode == (int)_SourceMode.RealTimeRecording)
                    return;
                RealTimeScopeMode();
            }
            else if (sender == btnFromFileData)
            {
                btnRealTimeRecording.Checked = false;
                btnFromFileData.Checked = true;
                if (SourceMode == (int)_SourceMode.FromFileData)
                    return;
                FromFileDataMode();
            }
        }

        private void btnWaveStyle_Click(object sender, EventArgs e)
        {
            if (sender == btnVector)
            {
                btnVector.Checked = true;
                btnScatter.Checked = false;
                btnVecSca.Checked = false;
                btnWaveStyle.Text = btnVector.Text;
                WaveStyle = (int)_WaveStyle.Vector;
            }
            else if (sender == btnScatter)
            {
                btnVector.Checked = false;
                btnScatter.Checked = true;
                btnVecSca.Checked = false;
                btnWaveStyle.Text = btnScatter.Text;
                WaveStyle = (int)_WaveStyle.Scatter;
            }
            else if (sender == btnVecSca)
            {
                btnVector.Checked = false;
                btnScatter.Checked = false;
                btnVecSca.Checked = true;
                btnWaveStyle.Text = btnVecSca.Text;
                WaveStyle = (int)_WaveStyle.VectorScatter;
            }

            lock (ScopeSignalList_XY)
            {
                if (WaveStyle == (int)_WaveStyle.Vector)
                {
                    for (int i = 0; i < ScopeSignalList_XY.Count; i++)
                    {
                        ScopeSignalList_XY[i].LineWidth = 1;
                        ScopeSignalList_XY[i].MarkerSize = 0;
                    }
                }
                else if (WaveStyle == (int)_WaveStyle.Scatter)
                {
                    for (int i = 0; i < ScopeSignalList_XY.Count; i++)
                    {
                        ScopeSignalList_XY[i].LineWidth = 0;
                        ScopeSignalList_XY[i].MarkerSize = 5;
                    }
                }
                else if (WaveStyle == (int)_WaveStyle.VectorScatter)
                {
                    for (int i = 0; i < ScopeSignalList_XY.Count; i++)
                    {
                        ScopeSignalList_XY[i].LineWidth = 1;
                        ScopeSignalList_XY[i].MarkerSize = 5;
                    }
                }
            }
            formsPlot2.Refresh();
        }

        private void RealTimeScopeMode()
        {
            // 隐藏无关控件，显示相关控件，修改状态栏内容，清空示波器窗口
            toolStrip2.Visible = false;
            btnStart.Visible = btnPause.Visible = btnStop.Visible = btnSave.Visible = true;
            btnOpen.Visible = tbxFilePath.Visible = false;
            //btnStart2.Visible = btnPause2.Visible = false;
            btnSave.Visible = true;
            btnRefreshVariable.Visible = true;

            btnFromFileData.Checked = false;
            btnSource.Text = btnRealTimeRecording.Text;
            lblSource.Text = "(" + btnSource.Text + ")";
            SourceMode = (int)_SourceMode.RealTimeRecording;
        }

        private void FromFileDataMode()
        {
            // 判断是否已经记录了波形，若已经记录，则询问是否保存
            if (isNewRecordData)
            {
                DialogResult result;
                if (MultiLanguage.DefaultLanguage == "zh")
                {
                    result = MessageBox.Show("是否将录制的数据保存到文件？", "保存", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                }
                else
                {
                    result = MessageBox.Show("No recording data needs to be saved temporarily.", "Save", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                }
                if (result == DialogResult.Yes)
                {
                    if (!SaveRecordData())
                    {
                        return;
                    }
                }
                else if (result == DialogResult.No)
                {
                    isNewRecordData = false;
                }
                else if (result == DialogResult.Cancel)
                {
                    return;
                }
                else
                {
                    return;
                }
            }
            // 隐藏无关控件，显示相关控件，修改状态栏内容，清空示波器窗口
            toolStrip2.Visible = true;
            btnStart.Visible = btnPause.Visible = btnStop.Visible = btnSave.Visible = false;
            btnOpen.Visible = tbxFilePath.Visible = true;
            //btnStart2.Visible = btnPause2.Visible = true;
            btnSave.Visible = false;
            btnRefreshVariable.Visible = false;

            btnRealTimeRecording.Checked = false;
            btnSource.Text = btnFromFileData.Text;
            lblSource.Text = "(" + btnSource.Text + ")";
            SourceMode = (int)_SourceMode.FromFileData;
        }

        private void btnDataPool_Click(object sender, EventArgs e)
        {
            if (panel3.Visible)
                panel3.Visible = false;
            else
                panel3.Visible = true;
        }

        private void btnRefreshVariable_Click(object sender, EventArgs e)
        {
            // 尚未开始录制
            if (!isScopeStart)
            {
                // 判断是否已经记录了波形，若已经记录，则询问是否保存
                if (isNewRecordData)
                {
                    DialogResult result;
                    if (MultiLanguage.DefaultLanguage == "zh")
                    {
                        result = MessageBox.Show("是否将录制的数据保存到文件？", "保存", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                    }
                    else
                    {
                        result = MessageBox.Show("No recording data needs to be saved temporarily.", "Save", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                    }
                    if (result == DialogResult.Yes)
                    {
                        if (!SaveRecordData())
                        {
                            RefreshVariableList();
                            return;
                        }
                        RefreshVariableList();
                    }
                    else if (result == DialogResult.No)
                    {
                        isNewRecordData = false;
                        RefreshVariableList();
                    }
                    else if (result == DialogResult.Cancel)
                    {
                        return;
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    RefreshVariableList();
                }
            }
            else
            {
                if (MultiLanguage.DefaultLanguage == "zh")
                {
                    MessageBox.Show("正在录制，不可刷新参数。", "警告");
                }
                else
                {
                    MessageBox.Show("Recording in progress, parameters cannot be refreshed.", "Warning");
                }
            }
        }

        private void RefreshVariableList(int mode = 0)
        {
            if (mode == 0)
            {
                Watch._Watch.updateScopeListOnce();
                lock (WatchConfig.ScopeVarieties)
                {
                    lock (ScopeVariableList)
                    {
                        lock (ScopeValues)
                        {
                            lock (this.source)
                            {
                                this.source.Clear();
                                scopeView.Clear();
                                #region YT_Scope
                                ScopeSignalList.Clear();
                                #endregion
                                ScopeVariableValueList.Clear();
                                ScopeValues.Clear();
                                ScopeBufferCount = 0;
                                this.source2.Clear();
                                this.source_combo.Clear();
                                for (int i = 0; i < WatchConfig.ScopeVarieties.Count; i++)
                                {
                                    // 添加示波器变量
                                    _ScopeVariety scopeVariety = new _ScopeVariety()
                                    {
                                        Visible = "True",
                                        Name = WatchConfig.ScopeVarieties[i].VarName,
                                        Value = WatchConfig.ScopeVarieties[i].Value,
                                        Color = ""
                                    };
                                    this.source_combo.Add(WatchConfig.ScopeVarieties[i].VarName);
                                    _ScopeValue scopeValue = new _ScopeValue()
                                    {
                                        Name = WatchConfig.ScopeVarieties[i].VarName,
                                        Value = WatchConfig.ScopeVarieties[i].Value
                                    };
                                    this.source.Add(scopeVariety);
                                    ScopeValues.Add(scopeValue);
                                    dataGridView1.Rows[i].Cells[(int)Columns.Color].Style.BackColor = ColorTranslator.FromHtml(customColors[i]);

                                    // 添加信号存储数组
                                    double[] loggerValue = new double[ScopeBuffer];
                                    ScopeVariableValueList.Add(loggerValue);

                                    // 添加信号
                                    #region YT_Scope
                                    SignalPlot loggerSignal = scopeView.AddSignal(ScopeVariableValueList[i], sampleRate: 1 / (double)mmInterval, label: scopeVariety.Name);
                                    loggerSignal.Color = ColorTranslator.FromHtml(customColors[i]);
                                    loggerSignal.LineWidth = 2;
                                    loggerSignal.MarkerSize = 6;
                                    ScopeSignalList.Add(loggerSignal);
                                    #endregion
                                }
                                this.source_combo.Add("(None)");
                                formsPlot1.Refresh();
                            }
                        }
                    }
                }
            }
            else if (mode == 1)
            {
                lock (ScopeVariableList)
                {
                    this.source_combo.Clear();
                    for (int i = 0; i < ScopeVariableList.Count; i++)
                    {
                        // 添加示波器变量
                        this.source_combo.Add(ScopeVariableList[i].Name);
                    }
                    this.source_combo.Add("(None)");
                }  
            }
        }

        private void ScopeView_YT_FormClosing(object sender, FormClosingEventArgs e)
        {   
            if (isNewRecordData)
            {
                DialogResult result;
                if (MultiLanguage.DefaultLanguage == "zh")
                {
                    result = MessageBox.Show("是否将录制的数据保存到文件？", "保存", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                }
                else
                {
                    result = MessageBox.Show("No recording data needs to be saved temporarily.", "Save", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                }
                if (result == DialogResult.Yes)
                {
                    SaveRecordData();
                }
            } 
            mmTimer1.Stop();
            timer1.Stop();
            scopeView.Clear();
            scopeViewXY.Clear();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            // 开始按钮，可以通过isScopeStart == true来开启录制，当isScopePause == true时，按下的作用是使isScopePause = false从而继续波形显示
            if (sender == btnStart)
            {
                // 若尚未开始录制，则按下后开始录制
                if (!isScopeStart)
                {
                    // 判断是否已经记录了波形，若已经记录，则询问是否保存
                    if (isNewRecordData)
                    {
                        DialogResult result;
                        if (MultiLanguage.DefaultLanguage == "zh")
                        {
                            result = MessageBox.Show("是否将录制的数据保存到文件？", "保存", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                        }
                        else
                        {
                            result = MessageBox.Show("No recording data needs to be saved temporarily.", "Save", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                        }
                        if (result == DialogResult.Yes)
                        {
                            if (!SaveRecordData())
                            {
                                return;
                            }
                        }
                        else if (result == DialogResult.No)
                        {
                            isNewRecordData = false;
                        }
                        else if (result == DialogResult.Cancel)
                        {
                            return;
                        }
                        else
                        {
                            return;
                        }
                    }

                    // 刷新录制列表，显示开始录制时间，并令开始标志为true
                    ScopeBufferCount = 0;
                    //RefreshVariableList();
                    #region YT_Scope
                    if (ScopeSignalList.Count == 0)
                        return;
                    #endregion
                    if (!CreateScopeSignalXY())
                    {
                        return;
                    }
                    updateStartTime();
                    isScopeStart = true;
                    isScopePause = false;
                    isScopeStop = false;
                    btnStart.BackColor = Color.LightGray;
                    btnPause.BackColor = SystemColors.Control;
                }
                // 若已经开始录制
                else
                {
                    // 若已经开始录制，但按下了暂停，则取消暂停
                    if (isScopePause)
                    {
                        isScopePause = false;
                        btnStart.BackColor = Color.LightGray;
                        btnPause.BackColor = SystemColors.Control;
                    }
                }
            }
            // 暂停按钮，在已经开始录制的情况下生效，按下后可以继续进行波形录制，但停止波形显示
            else if (sender == btnPause)
            {
                if (isScopeStart)
                {
                    isScopePause = true;
                    btnStart.BackColor = SystemColors.Control;
                    btnPause.BackColor = Color.LightGray;
                }
            }
            // 停止按钮，在已经开始录制的情况下生效，按下后停止记录与输出，并将录制的信号显示为full模式
            else if (sender == btnStop)
            {
                if (isScopeStart)
                {
                    isScopeStop = true;
                    isScopeStart = false;
                    isScopePause = false;
                    if (ScopeBufferCount > 0 && ScopeVariableList.Count > 0)
                    {
                        isNewRecordData = true;
                    }
                    btnStart.BackColor = SystemColors.Control;
                    btnPause.BackColor = SystemColors.Control;
                    #region YT_Scope
                    lock (ScopeSignalList)
                    {
                        for (int i = 0; i < ScopeSignalList.Count; i++)
                        {
                            ScopeSignalList[i].MinRenderIndex = 0;
                        }
                    }
                    #endregion
                    maskedTextBox1.Text = "00:00.000";
                    maskedTextBox2.Text = lblPosition.Text;
                }
            }
        }

        private bool CreateScopeSignalXY()
        {
            List<_ScopeVariety2> XYSignal = new List<_ScopeVariety2>();
            lock (ScopeVariableList2)
            {
                if (ScopeVariableList2.Count == 0)
                {
                    lock (ScopeSignalList_XY)
                    {
                        lock (ArrowList_XY)
                        {
                            lock (BaseList_XY)
                            {
                                ScopeVariableList2.Clear();
                                ArrowList_XY.Clear();
                                BaseList_XY.Clear();
                            }
                        }
                    }
                    return false;
                }
                else
                {
                    XYSignal.AddRange(ScopeVariableList2);
                }
            }
            lock (ScopeSignalList_XY)
            {
                ScopeSignalList_XY.Clear();
                scopeViewXY.Clear();
                for (int i = 0; i < XYSignal.Count; i++)
                {     
                    int X = 0, Y = 0;
                    DataGridViewComboBoxCell cell1 = (DataGridViewComboBoxCell)dataGridView2.Rows[i].Cells[1];
                    DataGridViewComboBoxCell cell2 = (DataGridViewComboBoxCell)dataGridView2.Rows[i].Cells[2];
                    string value1 = cell1.Value.ToString();
                    string value2 = cell2.Value.ToString();
                    X = comboItems.IndexOf(value1);
                    Y = comboItems.IndexOf(value2);
                    if (X == -1 || Y == -1)
                        continue;
                    else
                    {
                        ScatterPlot scatterPlot = scopeViewXY.AddScatter(X == comboItems.Count - 1 ? timeValue : ScopeVariableValueList[X],
                                                                         Y == comboItems.Count - 1 ? timeValue : ScopeVariableValueList[Y]);
                        if (WaveStyle == (int)_WaveStyle.Vector)
                        {
                            scatterPlot.LineWidth = 1;
                            scatterPlot.MarkerSize = 0;
                        }
                        else if (WaveStyle == (int)_WaveStyle.Scatter)
                        {
                            scatterPlot.LineWidth = 0;
                            scatterPlot.MarkerSize = 5;
                        }
                        else if (WaveStyle == (int)_WaveStyle.VectorScatter)
                        {
                            scatterPlot.LineWidth = 1;
                            scatterPlot.MarkerSize = 5;
                        }
                        // Visible
                        if (XYSignal[i].Visible == "True")
                        {
                            scatterPlot.IsVisible = true;
                        }
                        else
                        {
                            scatterPlot.IsVisible = false;
                        }
                        ScopeSignalList_XY.Add(scatterPlot);
                    }
                }
            }
            lock (ArrowList_XY)
            {
                ArrowList_XY.Clear();
                if (ScopeSignalList_XY.Count > 0)
                {
                    int signalCount = ScopeSignalList_XY.Count;
                    for (int i = 0; i < signalCount; i++)
                    {
                        ArrowCoordinated arrowPoint = scopeViewXY.AddArrow(0, 0, 0, 0);
                        // Visible
                        if (XYSignal[i].Visible == "True")
                        {
                            arrowPoint.IsVisible = true;
                        }
                        else
                        {
                            arrowPoint.IsVisible = false;
                        }
                        ArrowList_XY.Add(arrowPoint);
                    }
                }
            }
            lock (BaseList_XY)
            {
                BaseList_XY.Clear();
                if (ScopeSignalList_XY.Count > 0)
                {
                    int signalCount = ScopeSignalList_XY.Count;
                    for (int i = 0; i < signalCount; i++)
                    {
                        ArrowCoordinated arrowPoint = scopeViewXY.AddArrow(0, 0, 0, 0);
                        arrowPoint.MarkerSize = 10;
                        // Visible
                        if (XYSignal[i].Visible == "True")
                        {
                            arrowPoint.IsVisible = true;
                        }
                        else
                        {
                            arrowPoint.IsVisible = false;
                        }
                        BaseList_XY.Add(arrowPoint);
                    }
                }
            }
            return true;
        }

        private void readScopeValueOnce()
        {
            lock (ScopeValues)
            {
                lock (WatchConfig.ScopeVarieties)
                {
                    if (ScopeValues.Count == 0)
                        return;
                    for (int i = 0; i < ScopeValues.Count; i++)
                    {
                        string name = ScopeValues[i].Name;
                        for (int j = 0; j < WatchConfig.ScopeVarieties.Count; j++)
                        {
                            if (name == WatchConfig.ScopeVarieties[j].VarName)
                            {
                                ScopeValues[i].Value = WatchConfig.ScopeVarieties[j].Value;
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void updateStartTime()
        {
            DateTime currentDate = DateTime.Now;
            lblDate.Text = currentDate.ToString("yyyy-MM-dd");
            lblStart.Text = currentDate.ToString("HH:mm:ss.fff");   // "HH"代表24小时制
        }

        private bool SaveRecordData()
        {
            if (isNewRecordData)
            {
                string localFilePath = string.Empty;
                string fileNameExt = string.Empty;
                string dateString = lblDate.Text;
                string timeString = lblStart.Text;
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
            string dateString = lblDate.Text;
            string timeString = lblStart.Text;
            string date_time = dateString + " " + timeString;
            if (pointCount <= 0)
                return;
            if (ScopeVariableList.Count <= 0)
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
            lock (ScopeVariableList)
            {
                lock (ScopeVariableValueList)
                {
                    for (int i = 0; i < ScopeVariableList.Count; i++)
                    {
                        info.Info.Add(ScopeVariableList[i].Name);
                    }
                    csvInfo.Add(info);
                    for (int i = 0; i < pointCount; i++)
                    {
                        CsvValue value = new CsvValue()
                        {
                            Value = new List<double>(),
                        };
                        for (int j = 0; j < ScopeVariableList.Count; j++)
                        {
                            value.Value.Add(ScopeVariableValueList[j][i]);
                        }
                        csvValue.Add(value);
                    }
                }
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
            string dateString = lblDate.Text;
            string timeString = lblStart.Text;
            string date_time = dateString + " " + timeString;
            if (pointCount <= 0)
                return;
            if (ScopeVariableList.Count <= 0)
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

            lock (ScopeVariableList)
            {
                lock (ScopeVariableValueList)
                {
                    for (int i = 0; i < ScopeVariableList.Count; i++)
                    {
                        if (i == 0)
                        {
                            newSheet.CreateRow(rowOffset).CreateCell(i).SetCellValue(ScopeVariableList[i].Name);
                        }
                        else
                        {
                            newSheet.GetRow(rowOffset).CreateCell(i).SetCellValue(ScopeVariableList[i].Name);
                        }
                    }
                    rowOffset++;
                    for (int i = 0; i < pointCount; i++)
                    {
                        for (int j = 0; j < ScopeVariableList.Count; j++)
                        {
                            if (j == 0)
                            {
                                newSheet.CreateRow(rowOffset).CreateCell(j).SetCellValue(ScopeVariableValueList[j][i]);
                            }
                            else
                            {
                                newSheet.GetRow(rowOffset).CreateCell(j).SetCellValue(ScopeVariableValueList[j][i]);
                            }
                        }
                        rowOffset++;
                    }
                }
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

        private bool OpenRecordData()
        {
            string fileName;
            string fileNameExt = string.Empty;
            using (OpenFileDialog OpenFD = new OpenFileDialog())
            {
                OpenFD.Filter = "csv file(*.csv)|*.csv|xlsx file(*.xlsx)|*.xlsx";
                OpenFD.CheckFileExists = true;
                OpenFD.CheckPathExists = true;
                OpenFD.DefaultExt = "csv";
                OpenFD.RestoreDirectory = true;
                //定义打开的默认文件夹位置
                //OpenFD.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                DialogResult result = OpenFD.ShowDialog();
                if (result != DialogResult.OK)
                {
                    return false;
                }
                else
                {
                    fileName = OpenFD.FileName;
                    if (fileName == String.Empty)
                        return false;
                    fileNameExt = Path.GetExtension(fileName);
                    if (fileNameExt.ToLower() == ".csv")
                    {
                        return ReadFile_CSV(fileName);
                    }
                    else if (fileNameExt.ToLower() == ".xlsx")
                    {
                        return ReadFile_XLSX(fileName);
                    }
                }
            }
            return false;
        }

        //  Start Time          |   2023-12-12 9:39:39.039
        //  Scope Points        |   1212
        //  Scope Interval(ms)  |   10
        //  Var1                |   Var2    |   Var3    |   ...
        //  1.21                |   0.79    |   2.35    |   ...
        //  .                   |   .       |   .       |   ...
        //  .                   |   .       |   .       |   ...
        //  .                   |   .       |   .       |   ...
        private bool ReadFile_CSV(string filePath)
        {
            DateTime dateTime;
            int pointCount = 0;
            int scopeInterval = 0;
            TimeSpan timeSpan;
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
            };
            using (var reader = new StreamReader(filePath))
            {
                using (var csv = new CsvReader(reader, config))
                {
                    // 存储行数据
                    List<string[]> EveryRowData = new List<string[]>();
                    List<string> variableNames = new List<string>();
                    List<double[]> variableValues = new List<double[]>();
                    // 读前四行（表格信息）
                    for (int i=0;i<4;i++)
                    {
                        if (!csv.Read() || csv.Parser.Record.Length < 2)
                        {
                            MessageBox.Show("Invalid Sheet Info(Row:" + (i + 1) + ")", "Error");
                            return false;
                        }
                        EveryRowData.Add(csv.Parser.Record);
                    }
                    #region 检查表头
                    if (!DateTime.TryParseExact(EveryRowData[0][1], "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime)
                    || EveryRowData[0][0] != "Start Time")
                    {
                        MessageBox.Show("Invalid Start Time.", "Error");
                        return false;
                    }
                    if (!RegexMatch.isPositiveInteger(EveryRowData[1][1])
                        || EveryRowData[1][0] != "Scope Points")
                    {
                        MessageBox.Show("Invalid Scope Points.", "Error");
                        return false;
                    }
                    if (!RegexMatch.isPositiveInteger(EveryRowData[2][1])
                        || EveryRowData[2][0] != "Scope Interval(ms)")
                    {
                        MessageBox.Show("Invalid Scope Interval(ms).", "Error");
                        return false;
                    }
                    pointCount = int.Parse(EveryRowData[1][1]);
                    scopeInterval = int.Parse(EveryRowData[2][1]);
                    timeSpan = TimeSpan.FromMilliseconds((pointCount - 1) * scopeInterval);
                    string formattedTime0 = string.Empty;
                    string formattedTime1 = string.Empty;
                    string formattedTime2 = string.Empty;
                    try
                    {
                        formattedTime0 = dateTime.ToString("yyyy-MM-dd");
                        formattedTime1 = dateTime.ToString("HH:mm:ss.fff");
                        formattedTime2 = timeSpan.ToString(@"mm\:ss\.fff");
                    }
                    catch
                    {
                        MessageBox.Show("Invalid Scope Information.", "Error");
                        return false;
                    }
                    for (int i = 0; i< EveryRowData[3].Length; i++)
                    {
                        if (string.IsNullOrEmpty(EveryRowData[3][i]))
                            break;
                        variableNames.Add(EveryRowData[3][i]);
                        variableValues.Add(new double[ScopeBuffer]);
                    }
                    if (variableNames.Count == 0)
                    {
                        MessageBox.Show("Invalid Variable Name.", "Error");
                        return false;
                    }
                    #endregion
                    // 存放表数据
                    for (int i = 0; i < pointCount; i++)
                    {
                        if (!csv.Read() || csv.Parser.Record.Length != variableNames.Count)
                        {
                            MessageBox.Show("Invalid Variable Value(Row:" + (i + 4 + 1) + ")", "Error");
                            return false;
                        }
                        string[] valueData = csv.Parser.Record;
                        for (int j = 0; j < variableNames.Count; j++)
                        {
                            if (!double.TryParse(valueData[j], out variableValues[j][i]))
                            {
                                MessageBox.Show("Invalid Variable Value(" + (i + 4 + 1) + ", " + (j + 1) + ")", "Error");
                                return false;
                            }
                        }
                    }
                    // 示波器数据显示
                    lock (ScopeVariableList)
                    {
                        lock (ScopeVariableValueList)
                        {
                            #region YT_Scope
                            lock (ScopeSignalList)
                            {
                                #endregion
                                // 清空相关列表
                                this.source.Clear();
                                ScopeVariableValueList.Clear();
                                #region YT_Scope
                                //ScopeSignalList.Clear();
                                #endregion
                                scopeView.Clear();
                                // 显示文件记录的信息
                                lblDate.Text = formattedTime0;
                                lblStart.Text = formattedTime1;
                                maskedTextBox1.Text = "00:00.000";
                                maskedTextBox2.Text = lblPosition.Text = formattedTime2;
                                tbxFilePath.Text = filePath;
                                // 将数据显示到示波器和列表框
                                for (int i = 0; i < variableNames.Count; i++)
                                {
                                    // 添加示波器变量
                                    _ScopeVariety scopeVariety = new _ScopeVariety()
                                    {
                                        Visible = "True",
                                        Name = variableNames[i],
                                        Value = variableValues[i][0],
                                        Color = ""
                                    };
                                    this.source.Add(scopeVariety);
                                    dataGridView1.Rows[i].Cells[(int)Columns.Color].Style.BackColor = ColorTranslator.FromHtml(customColors[i]);

                                    // 添加信号存储数组
                                    double[] loggerValue = variableValues[i];
                                    ScopeVariableValueList.Add(loggerValue);

                                    // 添加信号
                                    #region YT_Scope
                                    SignalPlot loggerSignal = scopeView.AddSignal(ScopeVariableValueList[i], sampleRate: 1 / (double)scopeInterval, label: scopeVariety.Name);
                                    loggerSignal.Color = ColorTranslator.FromHtml(customColors[i]);
                                    loggerSignal.LineWidth = 2;
                                    loggerSignal.MarkerSize = 6;
                                    loggerSignal.MinRenderIndex = 0;
                                    loggerSignal.MaxRenderIndex = pointCount - 1;
                                    ScopeSignalList.Add(loggerSignal);
                                    #endregion
                                }
                                // 刷新显示
                                //scopeView.SetAxisLimits(xMin: 0, xMax: 5000);
                                scopeView.AxisAuto();
                                formsPlot1.Refresh();
                                return true;
                            #region YT_Scope
                            }
                            #endregion
                        }
                    }
                }
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
        private bool ReadFile_XLSX(string filePath)
        {
            DateTime dateTime;
            int pointCount = 0;
            int scopeInterval = 0;
            TimeSpan timeSpan;
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                XSSFWorkbook workBook = new XSSFWorkbook(fs);
                XSSFSheet sheet = (XSSFSheet)workBook.GetSheetAt(0);
                #region validate
                //if (sheet.SheetName != "ScopeDate")
                //{
                //    MessageBox.Show("Invalid Sheet Name.", "Error");
                //    return false;
                //}
                if (!DateTime.TryParseExact(sheet.GetRow(0).GetCell(1).myToString(), "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime) 
                    || sheet.GetRow(0).GetCell(0).myToString() != "Start Time")
                {
                    MessageBox.Show("Invalid Start Time.", "Error");
                    return false;
                }
                if (!RegexMatch.isPositiveInteger(sheet.GetRow(1).GetCell(1).myToString())
                    || sheet.GetRow(1).GetCell(0).myToString() != "Scope Points")
                {
                    MessageBox.Show("Invalid Scope Points.", "Error");
                    return false;
                }
                if (!RegexMatch.isPositiveInteger(sheet.GetRow(2).GetCell(1).myToString())
                    || sheet.GetRow(2).GetCell(0).myToString() != "Scope Interval(ms)")
                {
                    MessageBox.Show("Invalid Scope Interval(ms).", "Error");
                    return false;
                }
                #endregion

                #region ScopeInfo
                pointCount = int.Parse(sheet.GetRow(1).GetCell(1).myToString());
                scopeInterval = int.Parse(sheet.GetRow(2).GetCell(1).myToString());
                timeSpan = TimeSpan.FromMilliseconds((pointCount - 1) * scopeInterval);
                string formattedTime0 = string.Empty;
                string formattedTime1 = string.Empty;
                string formattedTime2 = string.Empty;
                try
                {
                    formattedTime0 = dateTime.ToString("yyyy-MM-dd");
                    formattedTime1 = dateTime.ToString("HH:mm:ss.fff");
                    formattedTime2 = timeSpan.ToString(@"mm\:ss\.fff");
                }
                catch
                {
                    MessageBox.Show("Invalid Scope Information.", "Error");
                    return false;
                }
                List<string> variableNames = new List<string>();
                List<double[]> variableValues = new List<double[]>();
                for (int i = 0; !string.IsNullOrEmpty(sheet.GetRow(3).GetCell(i).myToString()); i++)
                {
                    variableNames.Add(sheet.GetRow(3).GetCell(i).myToString());
                    variableValues.Add(new double[ScopeBuffer]);
                }
                if (variableNames.Count == 0)
                {
                    MessageBox.Show("Invalid Variable Name.", "Error");
                    return false;
                }
                #endregion

                for (int i = 0; i < pointCount; i++)
                {
                    for (int j = 0; j < variableNames.Count; j++)
                    {
                        if (string.IsNullOrEmpty(sheet.GetRow(i+4).GetCell(j).myToString()))
                        {
                            MessageBox.Show("Invalid Variable Value(" + (i + 4) + ", " + (j + 1) + ")", "Error");
                            return false;
                        }
                        if (!double.TryParse(sheet.GetRow(i + 4).GetCell(j).myToString(), out variableValues[j][i]))
                        {
                            MessageBox.Show("Invalid Variable Value(" + (i + 4) + ", " + (j + 1) + ")", "Error");
                            return false;
                        }
                    }
                }

                lock (ScopeVariableList)
                {
                    lock (ScopeVariableValueList)
                    {
                        #region YT_Scope
                        lock (ScopeSignalList)
                        {
                            #endregion
                            // 清空相关列表
                            this.source.Clear();
                            ScopeVariableValueList.Clear();
                            #region YT_Scope
                            ScopeSignalList.Clear();
                            #endregion
                            scopeView.Clear();
                            // 显示文件记录的信息
                            lblDate.Text = formattedTime0;
                            lblStart.Text = formattedTime1;
                            maskedTextBox1.Text = "00:00.000";
                            maskedTextBox2.Text = lblPosition.Text = formattedTime2;
                            tbxFilePath.Text = filePath;
                            // 将数据显示到示波器和列表框
                            for (int i = 0; i < variableNames.Count; i++)
                            {
                                // 添加示波器变量
                                _ScopeVariety scopeVariety = new _ScopeVariety()
                                {
                                    Visible = "True",
                                    Name = variableNames[i],
                                    Value = variableValues[i][0],
                                    Color = ""
                                };
                                this.source.Add(scopeVariety);
                                dataGridView1.Rows[i].Cells[(int)Columns.Color].Style.BackColor = ColorTranslator.FromHtml(customColors[i]);

                                // 添加信号存储数组
                                double[] loggerValue = variableValues[i];
                                ScopeVariableValueList.Add(loggerValue);

                                // 添加信号
                                #region YT_Scope
                                SignalPlot loggerSignal = scopeView.AddSignal(ScopeVariableValueList[i], sampleRate: 1 / (double)scopeInterval, label: scopeVariety.Name);
                                loggerSignal.Color = ColorTranslator.FromHtml(customColors[i]);
                                loggerSignal.LineWidth = 2;
                                loggerSignal.MarkerSize = 6;
                                loggerSignal.MinRenderIndex = 0;
                                loggerSignal.MaxRenderIndex = pointCount - 1;
                                ScopeSignalList.Add(loggerSignal);
                                #endregion
                            }
                            // 刷新显示
                            //scopeView.SetAxisLimits(xMin: 0, xMax: 5000);
                            scopeView.AxisAuto();
                            formsPlot1.Refresh();
                            return true;
                            #region YT_Scope
                        }
                        #endregion
                    }
                }
            }
        }

        private void dataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            DataGridViewCell currentCell = dataGridView1.CurrentCell;
            if (currentCell.RowIndex < 0 || currentCell.ColumnIndex < 0)
                return;
            if (currentCell.ColumnIndex == (int)Columns.Visible)
            {
                if (dataGridView1.IsCurrentCellDirty)
                {
                    dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            }
            #region YT_Scope
            lock (ScopeSignalList)
            {
                for (int i = 0; i < ScopeSignalList.Count; i++)
                {
                    if (dataGridView1.Rows[i].Cells[(int)Columns.Visible].Value.ToString() == "True")
                    {
                        ScopeSignalList[i].IsVisible = true;
                    }
                    else
                    {
                        ScopeSignalList[i].IsVisible = false;
                    }
                }
            }
            formsPlot1.Refresh();
            #endregion
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            if (OpenRecordData())
            {
                RefreshVariableList(1);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveRecordData();
        }

        private void btnStart2_Click(object sender, EventArgs e)
        {
            if (sender == btnStart2)
            {
                // 若尚未开始播放，则按下后开始播放
                if (!isScopeStart2)
                {
                    if (ScopeSignalList.Count == 0)
                        return;
                    if (!CreateScopeSignalXY())
                    {
                        return;
                    }
                    isScopeStart2 = true;
                    isScopePause2 = false;
                    btnStart2.BackColor = Color.LightGray;
                    btnPause2.BackColor = SystemColors.Control;
                }
                else if (isScopePause2)
                {
                    isScopePause2 = false;
                    btnStart2.BackColor = Color.LightGray;
                    btnPause2.BackColor = SystemColors.Control;
                }
            }
            else if (sender == btnPause2)
            {
                if (isScopeStart2)
                {
                    int minTime = 0, maxTime = 0;
                    DateTime dateTimeMin = DateTime.ParseExact(maskedTextBox1.Text, "mm:ss.fff", CultureInfo.InvariantCulture);
                    DateTime dateTimeMax = DateTime.ParseExact(maskedTextBox2.Text, "mm:ss.fff", CultureInfo.InvariantCulture);
                    minTime = dateTimeMin.Minute * 60 * 1000 + dateTimeMin.Second * 1000 + dateTimeMin.Millisecond;
                    maxTime = dateTimeMax.Minute * 60 * 1000 + dateTimeMax.Second * 1000 + dateTimeMax.Millisecond;

                    lock (ScopeSignalList)
                    {
                        for (int i = 0; i < ScopeSignalList.Count; i++)
                        {
                            ScopeSignalList[i].MinRenderIndex = minTime / mmInterval;
                        }
                    }
                    isScopeStart2 = false;
                    btnStart2.BackColor = SystemColors.Control;
                    btnPause2.BackColor = Color.LightGray;
                }
            }
            if (isScopeStart2)
            {
                maskedTextBox1.ReadOnly = maskedTextBox2.ReadOnly = true;
            }
            else
            {
                maskedTextBox1.ReadOnly = maskedTextBox2.ReadOnly = false;
            }
        }

        private void ScopeView_YT_FormClosed(object sender, FormClosedEventArgs e)
        {
        }

        private void addToolStripButton_Click(object sender, EventArgs e)
        {
            if (isScopeStart)
            {
                if (MultiLanguage.DefaultLanguage == "zh")
                {
                    MessageBox.Show("正在录制，不可刷新参数。", "警告");
                }
                else
                {
                    MessageBox.Show("Recording in progress, parameters cannot be refreshed.", "Warning");
                }
                return;
            }
            lock (this.source2)
            {
                _ScopeVariety2 sc = new _ScopeVariety2()
                {
                    Visible = "True",
                    X_Series = "(None)",
                    Y_Series = "(None)"
                };
                this.source2.Add(sc);
            }
        }

        private void deleteToolStripButton_Click(object sender, EventArgs e)
        {
            if (isScopeStart)
            {
                if (MultiLanguage.DefaultLanguage == "zh")
                {
                    MessageBox.Show("正在录制，不可刷新参数。", "警告");
                }
                else
                {
                    MessageBox.Show("Recording in progress, parameters cannot be refreshed.", "Warning");
                }
                return;
            }
            lock (this.source2)
            {
                int count = dataGridView2.SelectedRows.Count;
                if (count > 0)
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        dataGridView2.Rows.RemoveAt(dataGridView2.SelectedRows[i].Index);
                    }
                }
                
            }
        }

        private void dataGridView2_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            DataGridViewCell currentCell = dataGridView2.CurrentCell;
            if (currentCell.RowIndex < 0 || currentCell.ColumnIndex < 0)
                return;
            if (dataGridView2.IsCurrentCellDirty)
            {
                dataGridView2.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
            lock (ScopeSignalList_XY)
            {
                for (int i = 0; i < ScopeSignalList_XY.Count; i++)
                {
                    if (dataGridView2.Rows[i].Cells[(int)Columns.Visible].Value.ToString() == "True")
                    {
                        ScopeSignalList_XY[i].IsVisible = true;
                    }
                    else
                    {
                        ScopeSignalList_XY[i].IsVisible = false;
                    }
                }
            }
            lock (ArrowList_XY)
            {
                for (int i = 0; i < ArrowList_XY.Count; i++)
                {
                    if (dataGridView2.Rows[i].Cells[(int)Columns.Visible].Value.ToString() == "True")
                    {
                        ArrowList_XY[i].IsVisible = true;
                    }
                    else
                    {
                        ArrowList_XY[i].IsVisible = false;
                    }
                }
            }
            lock (BaseList_XY)
            {
                for (int i = 0; i < BaseList_XY.Count; i++)
                {
                    if (dataGridView2.Rows[i].Cells[(int)Columns.Visible].Value.ToString() == "True")
                    {
                        BaseList_XY[i].IsVisible = true;
                    }
                    else
                    {
                        BaseList_XY[i].IsVisible = false;
                    }
                }
            }
            formsPlot2.Refresh();
        }

        private void dataGridView2_MouseClick(object sender, MouseEventArgs e)
        {
            DataGridViewSelectedRowCollection currentRows = dataGridView2.SelectedRows;
            DataGridViewCell currentCell = dataGridView2.CurrentCell;
            if (currentCell is null)
            {
                lock (ScopeSignalList_XY)
                {
                    for (int i = 0; i < ScopeSignalList_XY.Count; i++)
                    {
                        ScopeSignalList_XY[i].IsHighlighted = false;
                        dataGridView2.Rows[i].HeaderCell.Style.BackColor = SystemColors.Window;
                    }
                }
                formsPlot2.Refresh();
                return;
            }
            if (currentCell.RowIndex < 0 || currentCell.ColumnIndex < 0)
                return;
            if (currentRows.Count == 1)
            {
                int index = currentRows[0].Index;
                lock (ScopeSignalList_XY)
                {
                    for (int i = 0; i < ScopeSignalList_XY.Count; i++)
                    {
                        ScopeSignalList_XY[i].IsHighlighted = false;
                        dataGridView2.Rows[i].HeaderCell.Style.BackColor = SystemColors.Window;
                    }
                    ScopeSignalList_XY[index].IsHighlighted = true;
                    currentRows[0].HeaderCell.Style.BackColor = Color.SkyBlue;
                }
            }
            else
            {
                lock (ScopeSignalList_XY)
                {
                    for (int i = 0; i < ScopeSignalList_XY.Count; i++)
                    {
                        ScopeSignalList_XY[i].IsHighlighted = false;
                        dataGridView2.Rows[i].HeaderCell.Style.BackColor = SystemColors.Window;
                    }
                }
            }
            dataGridView2.CurrentRow.Selected = false;
            dataGridView2.CurrentCell.Selected = false;
            formsPlot2.Refresh();
        }

        private void btnAG_Click(object sender, EventArgs e)
        {
            btnAG_2s.Checked = false;
            btnAG_5s.Checked = false;
            btnAG_10s.Checked = false;
            btnAG_30s.Checked = false;
            btnAG_60s.Checked = false;
            if (sender == btnAG_2s)
            {
                btnAG_2s.Checked = true;
                ScopeBufferLength_XY = (1000 / mmInterval) * 2;   // N秒缓存区
            }
            else if (sender == btnAG_5s)
            {
                btnAG_5s.Checked = true;
                ScopeBufferLength_XY = (1000 / mmInterval) * 5;   // N秒缓存区
            }
            else if (sender == btnAG_10s)
            {
                btnAG_10s.Checked = true;
                ScopeBufferLength_XY = (1000 / mmInterval) * 10;   // N秒缓存区
            }
            else if (sender == btnAG_30s)
            {
                btnAG_30s.Checked = true;
                ScopeBufferLength_XY = (1000 / mmInterval) * 30;   // N秒缓存区
            }
            else if (sender == btnAG_60s)
            {
                btnAG_60s.Checked = true;
                ScopeBufferLength_XY = (1000 / mmInterval) * 60;   // N秒缓存区
            }
            btnAfterGlow.Text = ((ToolStripMenuItem)sender).Text;
        }

        private void maskedTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            MaskedTextBox maskedTextBox = (MaskedTextBox)sender;
            if (maskedTextBox.ReadOnly)
                return;
            if (e.KeyCode == Keys.Enter)
            {
                DateTime dateTime = new DateTime();
                if (!IsValidInput(maskedTextBox.Text, out dateTime))
                {
                    maskedTextBox.Focus(); // 将焦点重新设置回 MaskedTextBox
                    if (sender == maskedTextBox1)
                        maskedTextBox.Text = "00:00.000";
                    else
                        maskedTextBox.Text = lblPosition.Text;
                    maskedTextBox.BackColor = SystemColors.Window;
                }
                else
                {
                    if (sender == maskedTextBox2)
                    {
                        DateTime dateTime2 = new DateTime();
                        DateTime dateTime3 = new DateTime();
                        IsValidInput(lblPosition.Text, out dateTime2);
                        IsValidInput(maskedTextBox1.Text, out dateTime3);
                        if (dateTime > dateTime2 || dateTime <= dateTime3)
                        {
                            maskedTextBox.Text = lblPosition.Text;
                        }
                        tbxNowTime.Text = maskedTextBox.Text;
                    }
                    else
                    {
                        DateTime dateTime2 = new DateTime();
                        IsValidInput(maskedTextBox2.Text, out dateTime2);
                        if (dateTime >= dateTime2)
                        {
                            maskedTextBox.Text = "00:00.000";
                        }
                    }
                    maskedTextBox.BackColor = SystemColors.Window;
                }
                int minTime = 0, maxTime = 0;
                DateTime dateTimeMin = DateTime.ParseExact(maskedTextBox1.Text, "mm:ss.fff", CultureInfo.InvariantCulture);
                DateTime dateTimeMax = DateTime.ParseExact(maskedTextBox2.Text, "mm:ss.fff", CultureInfo.InvariantCulture);
                minTime = dateTimeMin.Minute * 60 * 1000 + dateTimeMin.Second * 1000 + dateTimeMin.Millisecond;
                maxTime = dateTimeMax.Minute * 60 * 1000 + dateTimeMax.Second * 1000 + dateTimeMax.Millisecond;
                lock (ScopeSignalList)
                {
                    for (int i = 0; i < ScopeSignalList.Count; i++)
                    {
                        ScopeSignalList[i].MinRenderIndex = minTime / mmInterval;
                        ScopeSignalList[i].MaxRenderIndex = maxTime / mmInterval;
                    }
                }
                ScopeBufferCount2 = minTime / mmInterval;
                scopeView.SetAxisLimitsX(minTime, maxTime);
                scopeView.AxisAutoY();
                formsPlot1.Refresh();
                btnStart2.Enabled = btnPause2.Enabled = true;
            }
            else
            {
                maskedTextBox.BackColor = Color.Yellow;
                btnStart2.Enabled = btnPause2.Enabled = false;
                return;
            }
        }

        private bool IsValidInput(string str, out DateTime time)
        {
            DateTime dateTime = new DateTime();
            CultureInfo culture = CultureInfo.InvariantCulture; // 使用不依赖于区域的文化信息
            DateTimeStyles style = DateTimeStyles.None; // 不使用特殊格式选项
            if (DateTime.TryParseExact(str, "mm:ss.fff", culture, style, out dateTime))
            {
                time = dateTime;
                return true;
            }
            else
            {
                time = DateTime.ParseExact("00:00.000", "mm:ss.fff", culture);
                return false;
            }
        }

        private readonly double[] TimeSpans = new double[] { 0.2, 0.5, 1, 2, 5 };
        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (toolStripComboBox1.SelectedIndex == -1)
            {
                CountSpan = 1;
            }
            else
            {
                CountSpan = TimeSpans[toolStripComboBox1.SelectedIndex];
                ScopeBufferCount2 = Math.Floor(ScopeBufferCount2);
            }
        }

        private void clearToolStripButton_Click(object sender, EventArgs e)
        {
            if (isScopeStart)
            {
                if (MultiLanguage.DefaultLanguage == "zh")
                {
                    MessageBox.Show("正在录制，不可刷新参数。", "警告");
                }
                else
                {
                    MessageBox.Show("Recording in progress, parameters cannot be refreshed.", "Warning");
                }
                return;
            }
            lock (this.source2)
            {
                dataGridView2.Rows.Clear();
                this.source2.Clear();
            }
        }

        private void dataGridView1_MouseClick(object sender, MouseEventArgs e)
        {
            #region YT_Scope
            DataGridViewCell currentCell = dataGridView1.CurrentCell;
            if (currentCell is null)
            {
                lock (ScopeSignalList)
                {
                    for (int i = 0; i < ScopeSignalList.Count; i++)
                    {
                        dataGridView1.Rows[i].Cells[(int)Columns.Name].Style.BackColor = SystemColors.Window;
                        ScopeSignalList[i].IsHighlighted = false;
                    }
                }
                formsPlot1.Refresh();
                return;
            }
            if (currentCell.RowIndex < 0 || currentCell.ColumnIndex < 0)
                return;
            if (currentCell.ColumnIndex == (int)Columns.Name)
            {
                lock (ScopeSignalList)
                {
                    for (int i = 0; i < ScopeSignalList.Count; i++)
                    {
                        if (i == currentCell.RowIndex)
                        {
                            continue;
                        }
                        dataGridView1.Rows[i].Cells[(int)Columns.Name].Style.BackColor = SystemColors.Window;
                        ScopeSignalList[i].IsHighlighted = false;
                    }
                    if (ScopeSignalList[currentCell.RowIndex].IsHighlighted)
                    {
                        ScopeSignalList[currentCell.RowIndex].IsHighlighted = false;
                        currentCell.Style.BackColor = SystemColors.Window;
                    }
                    else
                    {
                        ScopeSignalList[currentCell.RowIndex].IsHighlighted = true;
                        currentCell.Style.BackColor = Color.SkyBlue;
                    }
                }
            }
            else
            {
                lock (ScopeSignalList)
                {
                    for (int i = 0; i < ScopeSignalList.Count; i++)
                    {
                        dataGridView1.Rows[i].Cells[(int)Columns.Name].Style.BackColor = SystemColors.Window;
                        ScopeSignalList[i].IsHighlighted = false;
                    }
                }
            }
            currentCell.Selected = false;
            formsPlot1.Refresh();
            #endregion
        }
    }
}

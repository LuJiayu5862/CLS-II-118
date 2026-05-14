using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static CLS_II.MainConfig;

namespace CLS_II
{
    public static class DoubleBufferDataGridView
    {
        /// <summary>
        /// 双缓冲，解决闪烁问题
        /// </summary>
        public static void DoubleBufferedDataGirdView(this DataGridView dgv, bool flag)
        {
            Type dgvType = dgv.GetType();
            PropertyInfo pi = dgvType.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            pi.SetValue(dgv, flag, null);
        }
    }

    public partial class Watch : Form
    {
        public enum Columns
        {
            Name = 0,
            Scope = 1,
            Category = 2,
            Type = 3,
            Value = 4,
            NextValue = 5,
            Port = 6,
            Source = 7,
            Comment = 8
        }

        private class TypeSize
        {
            public string type { get; set; }
            public int size { get; set; }
        }

        private readonly List<TypeSize> ST_TypeSizeList = new List<TypeSize>()
        {
            new TypeSize(){type = "BOOL",   size = 1 },
            new TypeSize(){type = "BYTE",   size = 1 },
            new TypeSize(){type = "WORD",   size = 2 },
            new TypeSize(){type = "DWORD",  size = 4 },
            new TypeSize(){type = "SINT",   size = 1 },
            new TypeSize(){type = "USINT",  size = 1 },
            new TypeSize(){type = "INT",    size = 2 },
            new TypeSize(){type = "UINT",   size = 2 },
            new TypeSize(){type = "DINT",   size = 4 },
            new TypeSize(){type = "UDINT",  size = 4 },
            new TypeSize(){type = "LINT",   size = 8 },
            new TypeSize(){type = "ULINT",  size = 8 },
            new TypeSize(){type = "REAL",   size = 4 },
            new TypeSize(){type = "LREAL",  size = 8 },
        };

        private List<string> validScopeType = new List<string>()
        {
            "SINT","USINT","INT","UINT","DINT","UDINT","LINT","ULINT","REAL","LREAL","BOOL",
            "Int16","Int32","Int64","UInt16","UInt32","UInt64","Single","Double","Boolean"
        };

        private string BytesToVarString(string Type, byte[] bytes)
        {
            switch (Type)
            {
                case "BOOL":
                    return myToString(BitConverter.ToBoolean(bytes, 0));
                case "BYTE":
                    byte _byte = bytes[0];
                    string result = "0x" + _byte.ToString("X2");
                    return result;
                case "WORD":
                    return bytes.ToHexStrFromByte();
                case "DWORD":
                    return bytes.ToHexStrFromByte();
                case "SINT":
                    return myToString((sbyte)bytes[0]);
                case "USINT":
                    return myToString(bytes[0]);
                case "INT":
                    return myToString(BitConverter.ToInt16(bytes, 0));
                case "UINT":
                    return myToString(BitConverter.ToUInt16(bytes, 0));
                case "DINT":
                    return myToString(BitConverter.ToInt32(bytes, 0));
                case "UDINT":
                    return myToString(BitConverter.ToUInt32(bytes, 0));
                case "LINT":
                    return myToString(BitConverter.ToInt64(bytes, 0));
                case "ULINT":
                    return myToString(BitConverter.ToUInt64(bytes, 0));
                case "REAL":
                    return myToString(BitConverter.ToSingle(bytes, 0));
                case "LREAL":
                    return myToString(BitConverter.ToDouble(bytes, 0));
                case "STRING":
                    {
                        List<byte> byteList = new List<byte>(bytes);
                        int index = byteList.IndexOf(0);
                        if (index != (bytes.Length - 1))
                        {
                            byte[] newBytes = new byte[index + 1];
                            Array.Copy(bytes, newBytes, newBytes.Length);
                            return System.Text.Encoding.ASCII.GetString(newBytes);
                        }
                        return System.Text.Encoding.ASCII.GetString(bytes);
                    }
                    
            }
            return "(Not Found)";
        }

        private byte[] VarStringToBytes(string Type, string Value)
        {
            byte[] result = Array.Empty<byte>();
            switch (Type)
            {
                case "BOOL":
                    if (Value.ToLower() == "true")
                        return new byte[] { 0x01 };
                    else
                        return new byte[] { 0x00 };
                case "SINT":
                    return new byte[] { (byte)sbyte.Parse(Value) };
                case "USINT":
                    return new byte[] { byte.Parse(Value) };
                case "INT":
                    return BitConverter.GetBytes(Int16.Parse(Value));
                case "UINT":
                    return BitConverter.GetBytes(UInt16.Parse(Value));
                case "DINT":
                    return BitConverter.GetBytes(Int32.Parse(Value));
                case "UDINT":
                    return BitConverter.GetBytes(UInt32.Parse(Value));
                case "LINT":
                    return BitConverter.GetBytes(Int64.Parse(Value));
                case "ULINT":
                    return BitConverter.GetBytes(UInt64.Parse(Value));
                case "REAL":
                    return BitConverter.GetBytes(Single.Parse(Value));
                case "LREAL":
                    return BitConverter.GetBytes(Double.Parse(Value));
                case "STRING":
                    return Value.ASCIIStringToByteArray();

            }
            return Array.Empty<byte>();
        }

        private class VariableInfo
        {
            public int indexGroup;
            public int indexOffset;
            public int length;
        }

        private class SymbolInfo
        {
            public string symbolName;
            public string variableName;
            public int variableLength;
            public byte[] value;
            public string variableType;
        }

        private class Variables
        {
            public List<SymbolInfo> symbols;
            public List<VariableInfo> variables;
        }

        private int STRING_Size(string STRING)
        {
            if (!RegexMatch.isSameName("STRING", STRING))
            {
                return -1;
            }
            if (STRING.Length <= "STRING".Length)
            {
                return -1;
            }
            string stringType = RegexMatch.StringDeleteBlank(STRING);
            string size = stringType.Substring(stringType.LastIndexOf("(") + 1,
                stringType.LastIndexOf(")") - stringType.LastIndexOf("(") - 1);
            int stringSize = int.Parse(size);
            return stringSize;
        }

        public string updateStyle
        {
            get { return toolStripLabel2.Text; }
            set { toolStripLabel2.Text = updateStyle; }
        }

        public void writeUpdateStyle(string str)
        {
            toolStripLabel2.Text = str;
            if (str == "Single")
                refreshToolStripButton.Visible = true;
            else
                refreshToolStripButton.Visible = false;
        }

        private void updateUdpDataOnce()
        {
            lock (this.records)
            {
                for (int i = 0; i < WatchConfig.VarietyInfos.Count; i++)
                {
                    if (WatchConfig.VarietyInfos[i].Category == "UDP")
                    {
                        int channelID = int.Parse(WatchConfig.VarietyInfos[i].Port) - 1;
                        string varName = WatchConfig.VarietyInfos[i].Source;
                        varName = varName.Substring(varName.LastIndexOf(".") + 1, varName.Count() - varName.LastIndexOf(".") - 1);
                        string value = "(Not Found)";
                        foreach (_FieldInfo field in typeof(_Feedback).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                        {
                            if (varName == field.Name)
                            {
                                value = Convert.ToString(field.GetValue(UdpData.LCSInfos.Infos[channelID]));
                            }
                        }
                        this.records[i].Value = value;
                    }
                }
            }
        }

        private void updateParamDataOnce()
        {
            lock (this.records)
            {
                for (int i = 0; i < WatchConfig.VarietyInfos.Count; i++)
                {
                    if (WatchConfig.VarietyInfos[i].Category != "Param") continue;

                    string subName = WatchConfig.VarietyInfos[i].Port;
                    string source = WatchConfig.VarietyInfos[i].Source;
                    string tail = source.Substring(source.LastIndexOf('.') + 1);

                    // 解析是否带下标
                    string fieldName;
                    int arrIndex = -1;
                    int bracket = tail.IndexOf('[');
                    if (bracket >= 0)
                    {
                        fieldName = tail.Substring(0, bracket);
                        int rbr = tail.IndexOf(']', bracket);
                        if (rbr > bracket)
                            int.TryParse(tail.Substring(bracket + 1, rbr - bracket - 1), out arrIndex);
                    }
                    else
                    {
                        fieldName = tail;
                    }

                    object structObj = GetParamStruct(subName);
                    string value = "(Not Found)";

                    if (structObj != null)
                    {
                        var field = structObj.GetType().GetField(fieldName,
                            BindingFlags.Instance | BindingFlags.Public);
                        if (field != null)
                        {
                            object raw = field.GetValue(structObj);
                            if (arrIndex >= 0 && raw is Array arr)
                            {
                                if (arrIndex < arr.Length)
                                    value = FormatByMode(arr.GetValue(arrIndex));
                            }
                            // TcString 字段显示为字符串
                            else if (raw is byte[] rawBytes
                                     && field.GetCustomAttribute<TcStringAttribute>() != null)
                            {
                                int nullIdx = Array.IndexOf(rawBytes, (byte)0);
                                value = Encoding.ASCII.GetString(rawBytes, 0,
                                            nullIdx < 0 ? rawBytes.Length : nullIdx);
                            }
                            else
                            {
                                value = FormatByMode(raw);
                            }
                        }
                    }
                    this.records[i].Value = value;
                }
            }
        }

        private object GetParamStruct(string subName)
        {
            switch (subName)
            {
                case "CLSModel": lock (ParamData.LockCLSModel) return ParamData.CLS_Model;
                case "CLSParam": lock (ParamData.LockCLSParam) return ParamData.CLS_Param;
                case "CLS5K": lock (ParamData.LockCLS5K) return ParamData.CLS_5K;
                case "CLSConsts": lock (ParamData.LockCLSConsts) return ParamData.CLS_Consts;
                case "TestMDL": lock (ParamData.LockTestMDL) return ParamData.Test_MDL;
                case "CLSEnum": lock (ParamData.LockCLSEnum) return ParamData.CLS_Enum;
                case "XT": lock (ParamData.LockXT) return ParamData.Param_XT;
                case "YT": lock (ParamData.LockYT) return ParamData.Param_YT;
                case "CtrlIn": lock (ParamData.LockCtrlIn) return ParamData.CtrlIn;
                case "CtrlOut": lock (ParamData.LockCtrlOut) return ParamData.CtrlOut;
                case "DeviceInfo": lock (ParamData.LockDevInfo) return ParamData.Device_Info;
                case "UdpDataCfg": lock (ParamData.LockUdpDataCfg) return ParamData.UdpData_Cfg;
                case "UdpParamCfg": lock (ParamData.LockUdpParamCfg) return ParamData.UdpParam_Cfg;
                default: return null;
            }
        }

        /// <summary>
        /// 从 JdData.JdTx / JdData.JdRx 刷新 Watch 显示值。
        /// 在 mmTimer1_Ticked 中独立调用，不混入 updateParamDataOnce。
        /// </summary>
        private void updateJdDataOnce()
        {
            lock (this.records)
            {
                for (int i = 0; i < WatchConfig.VarietyInfos.Count; i++)
                {
                    if (WatchConfig.VarietyInfos[i].Category != "Jd") continue;

                    string subName = WatchConfig.VarietyInfos[i].Port;   // "JdTx" / "JdRx"
                    string source = WatchConfig.VarietyInfos[i].Source;
                    string propName = source.Substring(source.LastIndexOf('.') + 1);

                    object frameObj = GetJdFrame(subName);
                    string value = "(Not Found)";

                    if (frameObj != null)
                    {
                        var prop = frameObj.GetType().GetProperty(propName,
                            BindingFlags.Instance | BindingFlags.Public);
                        if (prop != null)
                            value = FormatByMode(prop.GetValue(frameObj));
                    }
                    this.records[i].Value = value;
                }
            }
        }

        /// <summary>返回 JdTx 或 JdRx 帧对象（class，加锁后直接返回引用）</summary>
        private object GetJdFrame(string subName)
        {
            switch (subName)
            {
                case "JdTx": lock (JdData.JdTx) return JdData.JdTx;
                case "JdRx": lock (JdData.JdRx) return JdData.JdRx;
                default: return null;
            }
        }

        //private void updateAdsDataOnce()
        //{
        //    lock (this.records)
        //    {
        //        Stopwatch stopwatch = new Stopwatch();
        //        stopwatch.Start();
        //        lock (activeVariables)
        //        {
        //            if (activeVariablesIndex.Count == 0)
        //                return;
        //            for (int i = 0; i < activeVariables.Count; i++)
        //            {
        //                int size = 0;
        //                BinaryReader reader = new BinaryReader(BlockRead(adsClients[i], activeVariables[i], ref size));
        //                if (size == -1)
        //                {
        //                    _isAdsSymbolNotFound = true;
        //                    continue;
        //                }
        //                else
        //                {
        //                    for (int j = 0; j < activeVariables[i].Length; j++)
        //                    {
        //                        int error = reader.ReadInt32();
        //                        size = size - 4;
        //                        if (error != (int)AdsErrorCode.NoError)
        //                            System.Diagnostics.Debug.WriteLine(
        //                                String.Format("Unable to read variable {0} (Error = {1})", i, error));
        //                    }
        //                    for (int k = 0; k < activeVariablesIndex[i].Length; k++)
        //                    {
        //                        int varSize = adsVariables[i].symbols[activeVariablesIndex[i][k]].variableLength;
        //                        adsVariables[i].symbols[activeVariablesIndex[i][k]].value = reader.ReadBytes(varSize);
        //                    }
        //                }
        //            }
        //        }                

        //        for (int i = 0; i < WatchConfig.VarietyInfos.Count; i++)
        //        {
        //            if (WatchConfig.VarietyInfos[i].Category == "ADS")
        //            {
        //                string varName = WatchConfig.VarietyInfos[i].VarName;
        //                string value = "(Not Found)";
        //                int index = adsPortList.IndexOf(WatchConfig.VarietyInfos[i].Port);
        //                if (index != -1)
        //                {
        //                    for (int j = 0; j < adsVariables[index].symbols.Count; j++)
        //                    {
        //                        if (adsVariables[index].variables[j].indexOffset == -1)
        //                            continue;
        //                        if (varName == adsVariables[index].symbols[j].symbolName)
        //                        {
        //                            value = BytesToVarString(adsVariables[index].symbols[j].variableType, adsVariables[index].symbols[j].value);
        //                        }
        //                    }
        //                }
        //                this.records[i].Value = value;
        //            }
        //        }
        //        stopwatch.Stop();
        //        TimeSpan elapsedTime = stopwatch.Elapsed;
        //        Debug.WriteLine("ADS读取用时: " + elapsedTime.TotalMilliseconds + " ms");
        //    }
        //}

        public void updateScopeListOnce()
        {
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
                            double value = 0;
                            if (variety.Value == "True" || variety.Value == "False")
                            {
                                value = variety.Value == "True" ? 1 : 0;
                            }
                            else
                            {
                                double value2;
                                if (double.TryParse(variety.Value, out value2))
                                    value = value2;
                                else
                                    value = 0;
                            }
                            v.Value = value;
                            WatchConfig.ScopeVarieties.Add(v);
                        }
                    }
                }
                // 重建索引
                _scopeRecordIndex.Clear();
                lock (this.records)
                {
                    for (int j = 0; j < this.records.Count; j++)
                        _scopeRecordIndex[this.records[j].Name] = j;
                }
            }
        }

        private void updateScopeDataOnce()
        {
            lock (WatchConfig.ScopeVarieties)
            {
                lock (this.records)
                {
                    for (int i = 0; i < WatchConfig.ScopeVarieties.Count; i++)
                    {
                        if (!_scopeRecordIndex.TryGetValue(WatchConfig.ScopeVarieties[i].VarName, out int j))
                            continue;

                        string val = this.records[j].Value;
                        if (val == "True" || val == "False")
                            WatchConfig.ScopeVarieties[i].Value = val == "True" ? 1 : 0;
                        else
                            WatchConfig.ScopeVarieties[i].Value =
                                double.TryParse(val, out double d) ? d : 0;
                    }
                }
            }
        }

        //private void writeAdsDataOnce()
        //{
        //    if (!GlobalVar.isUdpConnceted)
        //        return;
        //    lock (this.records)
        //    {
        //        Stopwatch stopwatch = new Stopwatch();
        //        stopwatch.Start();
        //        lock (activeVariables)
        //        {
        //            if (activeVariablesIndex.Count == 0)
        //                return;
        //            for (int i = 0; i < activeVariables.Count; i++)
        //            {
        //                BinaryReader reader = new BinaryReader(BlockRead2(adsClients[i], activeVariables[i],activeNextValue[i]));
        //                for (int j = 0; j < activeVariables[i].Length; j++)
        //                {
        //                    int error = reader.ReadInt32();
        //                    if (error != (int)AdsErrorCode.NoError)
        //                        System.Diagnostics.Debug.WriteLine(
        //                            String.Format("Unable to read variable {0} (Error = {1})", i, error));
        //                }
        //            }
                        
        //        }
        //        stopwatch.Stop();
        //        TimeSpan elapsedTime = stopwatch.Elapsed;
        //        Debug.WriteLine("ADS写入用时: " + elapsedTime.TotalMilliseconds + " ms");
        //    }
        //}

        //private void adsHandleCreate()
        //{
        //    lock (adsClients)
        //    {
        //        lock (activeVariables)
        //        {
        //            activeVariablesIndex.Clear();
        //            activeVariables.Clear();
        //            for (int i = 0; i < adsVariables.Count; i++)
        //            {
        //                List<int> activeIndex = new List<int>();
        //                List<VariableInfo> variables = new List<VariableInfo>();
        //                for (int j = 0; j < adsVariables[i].symbols.Count; j++)
        //                {
        //                    string variableName = adsVariables[i].symbols[j].variableName;
        //                    adsVariables[i].variables[j].indexOffset = adsClients[i].CreateVariableHandle(variableName);
        //                    if (adsVariables[i].variables[j].indexOffset != -1)
        //                    {
        //                        activeIndex.Add(j);
        //                        VariableInfo variable = adsVariables[i].variables[j];
        //                        variables.Add(variable);
        //                    }
        //                }
        //                int[] indexArray = activeIndex.ToArray();
        //                VariableInfo[] variableArray = variables.ToArray();
        //                activeVariablesIndex.Add(indexArray);
        //                activeVariables.Add(variableArray);
        //            }
        //        }
        //    }
        //}

        //private void adsHandleCreate2()
        //{
        //    lock (adsClients)
        //    {
        //        lock (activeVariables)
        //        {
        //            activeVariablesIndex.Clear();
        //            activeNextValue.Clear();
        //            activeVariables.Clear();
        //            for (int i = 0; i < adsVariables.Count; i++)
        //            {
        //                List<int> activeIndex = new List<int>();
        //                List<VariableInfo> variables = new List<VariableInfo>();
        //                List<byte> nextValueList = new List<byte>();
        //                for (int j = 0; j < adsVariables[i].symbols.Count; j++)
        //                {
        //                    byte[] nextValue = Array.Empty<byte>();
        //                    string variableName = adsVariables[i].symbols[j].variableName;
        //                    adsVariables[i].variables[j].indexOffset = adsClients[i].CreateVariableHandle(variableName);
        //                    if (adsVariables[i].variables[j].indexOffset != -1)
        //                    {
        //                        activeIndex.Add(j);
        //                        VariableInfo variable = adsVariables[i].variables[j];
        //                        variables.Add(variable);
        //                        string varValue = string.Empty;
        //                        for (int k = 0; k < dataGridView1.Rows.Count; k++)
        //                        {
        //                            if (adsVariables[i].symbols[j].symbolName == dataGridView1.Rows[k].Cells[(int)Columns.Name].Value.ToString())
        //                            {
        //                                if (string.IsNullOrEmpty(myToString(dataGridView1.Rows[k].Cells[(int)Columns.NextValue].Value)))
        //                                {
        //                                    varValue = myToString(dataGridView1.Rows[k].Cells[(int)Columns.Value].Value);
        //                                }
        //                                else
        //                                {
        //                                    varValue = myToString(dataGridView1.Rows[k].Cells[(int)Columns.NextValue].Value);
        //                                    dataGridView1.Rows[k].Cells[(int)Columns.NextValue].Value = "";
        //                                }
        //                                break;
        //                            }
        //                        }
        //                        nextValue = VarStringToBytes(adsVariables[i].symbols[j].variableType, varValue);
        //                        nextValueList.AddRange(nextValue);
        //                        if (adsVariables[i].symbols[j].variableType == "STRING")
        //                        {
        //                            if (adsVariables[i].symbols[j].variableLength > nextValue.Length)
        //                            {
        //                                for (int g = 0; g < adsVariables[i].symbols[j].variableLength - nextValue.Length; g++)
        //                                {
        //                                    nextValueList.Add(0);
        //                                }
        //                            }
        //                            else if (adsVariables[i].symbols[j].variableLength < nextValue.Length)
        //                            {
        //                                nextValueList.RemoveRange(adsVariables[i].symbols[j].variableLength - 1, nextValue.Length - adsVariables[i].symbols[j].variableLength + 1);
        //                                nextValueList.Add(0);
        //                            }
        //                        }
        //                    }
        //                }
        //                int[] indexArray = activeIndex.ToArray();
        //                VariableInfo[] variableArray = variables.ToArray();
        //                activeVariablesIndex.Add(indexArray);
        //                activeVariables.Add(variableArray);
        //                activeNextValue.Add(nextValueList.ToArray());
        //            }
        //        }
        //    }
        //}

        //private AdsStream BlockRead(AdsClient adsClient, VariableInfo[] variables, ref int size)
        //{
        //    // Allocate memory
        //    int rdLength = variables.Length * 4;
        //    int wrLength = variables.Length * 12;

        //    // Write data for handles into the ADS Stream
        //    BinaryWriter writer = new BinaryWriter(new AdsStream(wrLength));
        //    for (int i = 0; i < variables.Length; i++)
        //    {
        //        writer.Write(variables[i].indexGroup);
        //        writer.Write(variables[i].indexOffset);
        //        writer.Write(variables[i].length);
        //        rdLength += variables[i].length;
        //    }

        //    // Sum command to read variables from the PLC
        //    AdsStream rdStream = new AdsStream(rdLength);
        //    size = adsClient.ReadWrite(0xF080, variables.Length, rdStream, (AdsStream)writer.BaseStream);

        //    // Return the ADS error codes
        //    return rdStream;
        //}

        //private AdsStream BlockRead2(AdsClient adsClient, VariableInfo[] variables, byte[] bytes)
        //{
        //    // Allocate memory
        //    int rdLength = variables.Length * 4;
        //    int wrLength = variables.Length * 12 + bytes.Length;

        //    // Write data for handles into the ADS stream
        //    BinaryWriter writer = new BinaryWriter(new AdsStream(wrLength));
        //    for (int i = 0; i < variables.Length; i++)
        //    {
        //        writer.Write(variables[i].indexGroup);
        //        writer.Write(variables[i].indexOffset);
        //        writer.Write(variables[i].length);
        //    }

        //    // Write data to send to PLC behind the structure
        //    if (wrLength != variables.Length * 12 + bytes.Length)
        //        return null;
        //    writer.Write(bytes);

        //    // Sum command to write the data into the PLC
        //    AdsStream rdStream = new AdsStream(rdLength);
        //    adsClient.ReadWrite(0xF081, variables.Length, rdStream, (AdsStream)writer.BaseStream);

        //    // Return the ADS error codes
        //    return rdStream;
        //}

        private bool matchDefaultValue(string Type, string Value)
        {
            string type = RegexMatch.StringDeleteBlank(Type);
            string value = RegexMatch.StringDeleteBlank(Value);
            switch (type)
            {

            }
            return false;
        }

        //private bool matchADSValue(string Type, string Value)
        //{
        //    string type = RegexMatch.StringDeleteBlank(Type);
        //    string value = RegexMatch.StringDeleteBlank(Value);
        //    int stringSize = 0;

        //    switch (type)
        //    {
        //        case "SINT":
        //        case "INT":
        //        case "DINT":
        //        case "LINT":
        //            if (RegexMatch.isInteger(value))
        //                return true;
        //            else
        //                return false;
        //        case "USINT":
        //        case "UINT":
        //        case "UDINT":
        //        case "ULINT":
        //            if (RegexMatch.isPositiveInteger(value))
        //                return true;
        //            else
        //                return false;
        //        case "BOOL":
        //            if (value.ToLower() == "true")
        //                return true;
        //            else if (value.ToLower() == "false")
        //                return true;
        //            else
        //                return false;
        //        case "REAL":
        //        case "LREAL":
        //            if (RegexMatch.isFloatingNum(value))
        //                return true;
        //            else
        //                return false;
        //        default:
        //            if (RegexMatch.isSameName("STRING", type))
        //            {
        //                type = "STRING";
        //                string stringType = RegexMatch.StringDeleteBlank(Type);
        //                string size = stringType.Substring(stringType.LastIndexOf("(") + 1,
        //                    stringType.LastIndexOf(")") - stringType.LastIndexOf("(") - 1);
        //                stringSize = int.Parse(size);
        //                if (Value.Count() <= stringSize)
        //                    return true;
        //                else
        //                    return false;
        //            }
        //            break;
        //    }
        //    return false;
        //}

        private bool matchUDPValue(string Type, string Value)
        {
            string type = RegexMatch.StringDeleteBlank(Type);
            string value = RegexMatch.StringDeleteBlank(Value);

            // ── 整数类型：若带 0x/0o/0b 前缀，按 ConvertStringToTargetType 同款规则验证 ──
            if (IsIntegerTypeName(type) && HasIntPrefix(value))
            {
                return TryParseIntegerWithPrefix(type, value);
            }

            switch (type)
            {
                case "Int16":
                case "Int32":
                case "Int64":
                    if (RegexMatch.isInteger(value))
                        return true;
                    else
                        return false;
                case "UInt16":
                case "UInt32":
                case "UInt64":
                    if (RegexMatch.isPositiveInteger(value))
                        return true;
                    else
                        return false;
                case "Single":
                case "Double":
                    if (RegexMatch.isFloatingNum(value))
                        return true;
                    else
                        return false;
                case "Boolean":
                    if (value.ToLower() == "true")
                        return true;
                    else if (value.ToLower() == "false")
                        return true;
                    else
                        return false;
                case "Byte":
                    if (RegexMatch.isPositiveInteger(value))
                    {
                        int intValue = int.Parse(value);
                        if (intValue >= 0 && intValue <= 255)
                            return true;
                        else
                            return false;
                    }
                    else
                        return false;
                default:
                    if (RegexMatch.isSameName("STRING", type))
                    {
                        string sizeStr = type.Substring(type.LastIndexOf('(') + 1,
                            type.LastIndexOf(')') - type.LastIndexOf('(') - 1);
                        if (!int.TryParse(sizeStr, out int maxLen)) return false;

                        // 长度检查：不超过 STRING(N) 声明的最大字符数
                        if (Value.Length > maxLen) return false;

                        // ASCII 合法性检查：只允许可打印 ASCII（0x20-0x7E）
                        foreach (char c in Value)
                            if (c < 0x20 || c > 0x7E) return false;

                        return true;
                    }
                    break;

            }
            return false;
        }

        private static bool IsIntegerTypeName(string typeName)
        {
            switch (typeName)
            {
                case "Byte":
                case "SByte":
                case "Int16":
                case "UInt16":
                case "Int32":
                case "UInt32":
                case "Int64":
                case "UInt64":
                    return true;
                default: return false;
            }
        }

        private static bool HasIntPrefix(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 3) return false;
            string p = s.Substring(0, 2);
            return p == "0x" || p == "0X" || p == "0b" || p == "0B" || p == "0o" || p == "0O";
        }

        /// <summary>验证带前缀的整数字面量是否能装入目标类型（范围检查）</summary>
        private static bool TryParseIntegerWithPrefix(string typeName, string s)
        {
            int radix;
            string p = s.Substring(0, 2);
            if (p == "0x" || p == "0X") radix = 16;
            else if (p == "0b" || p == "0B") radix = 2;
            else if (p == "0o" || p == "0O") radix = 8;
            else return false;

            string body = s.Substring(2);
            ulong u;
            try { u = Convert.ToUInt64(body, radix); }
            catch { return false; }

            // 范围校验（按补码视为合法位模式即可，不做有符号溢出限制）
            switch (typeName)
            {
                case "Byte": case "SByte": return u <= 0xFFUL;
                case "Int16": case "UInt16": return u <= 0xFFFFUL;
                case "Int32": case "UInt32": return u <= 0xFFFFFFFFUL;
                case "Int64": case "UInt64": return true;  // ulong 上限即帧上限
                default: return false;
            }
        }

        public static string myToString(object obj)
        {
            if (obj is null)
                return String.Empty;
            else
                return obj.ToString();
        }

        // ══════════════════════════════════════════════════════════════════
        //  按 WatchDataMode 格式化显示值（DEC/HEX/OCT/BIN，固定位宽）
        //  - 整数类型受影响；浮点/Bool/String 始终走 Convert.ToString
        //  - 有符号负数按二进制补码显示（与硬件调试惯例一致）
        // ══════════════════════════════════════════════════════════════════
        private static string FormatByMode(object raw)
        {
            if (raw == null) return string.Empty;
            Type t = raw.GetType();

            int mode = MainConfig.ConfigInfo.DebugItems.WatchMode;
            if (mode == (int)WatchDataMode.DEC) return Convert.ToString(raw);

            // 仅整数类型走非十进制；其他类型直接 ToString
            int byteSize;
            ulong u;   // 无符号统一容器（按补码）

            if (t == typeof(byte)) { byteSize = 1; u = (byte)raw; }
            else if (t == typeof(sbyte)) { byteSize = 1; u = (byte)(sbyte)raw; }
            else if (t == typeof(short)) { byteSize = 2; u = (ushort)(short)raw; }
            else if (t == typeof(ushort)) { byteSize = 2; u = (ushort)raw; }
            else if (t == typeof(int)) { byteSize = 4; u = (uint)(int)raw; }
            else if (t == typeof(uint)) { byteSize = 4; u = (uint)raw; }
            else if (t == typeof(long)) { byteSize = 8; u = (ulong)(long)raw; }
            else if (t == typeof(ulong)) { byteSize = 8; u = (ulong)raw; }
            else return Convert.ToString(raw);   // 浮点/Bool/String/其他

            switch (mode)
            {
                case (int)WatchDataMode.HEX:
                    // 位宽 = 字节数 × 2
                    return "0x" + u.ToString("X" + (byteSize * 2));

                case (int)WatchDataMode.OCT:
                    {
                        // 八进制位宽：1B=3, 2B=6, 4B=11, 8B=22
                        int octWidth = byteSize == 1 ? 3 : byteSize == 2 ? 6 : byteSize == 4 ? 11 : 22;
                        string s = Convert.ToString((long)u, 8);
                        // ulong 在 8 字节时可能超出 long 范围，单独处理
                        if (byteSize == 8) s = ToOctalUInt64(u);
                        return "0o" + s.PadLeft(octWidth, '0');
                    }

                case (int)WatchDataMode.BIN:
                    {
                        int binWidth = byteSize * 8;
                        string s = byteSize == 8
                            ? ToBinaryUInt64(u)
                            : Convert.ToString((long)u, 2);
                        return "0b" + s.PadLeft(binWidth, '0');
                    }

                default:
                    return Convert.ToString(raw);
            }
        }

        /// <summary>ulong → 八进制字符串（避免 Convert.ToString(long,8) 在 ulong 上溢出）</summary>
        private static string ToOctalUInt64(ulong v)
        {
            if (v == 0) return "0";
            var sb = new StringBuilder();
            while (v > 0) { sb.Insert(0, (char)('0' + (int)(v & 7))); v >>= 3; }
            return sb.ToString();
        }

        /// <summary>ulong → 二进制字符串（避免 long 符号位问题）</summary>
        private static string ToBinaryUInt64(ulong v)
        {
            if (v == 0) return "0";
            var sb = new StringBuilder();
            while (v > 0) { sb.Insert(0, (v & 1) == 1 ? '1' : '0'); v >>= 1; }
            return sb.ToString();
        }

        // ────────────────────────────────────────────────
        //  写回：UDP
        // ────────────────────────────────────────────────
        private bool TryWriteUdpValue(WatchConfig._VarietyInfo variety, string input)
        {
            try
            {
                int channelID = int.Parse(variety.Port) - 1;
                string varName = variety.Source;
                varName = varName.Substring(varName.LastIndexOf(".") + 1);

                // _Feedback 是值类型struct，必须装箱→改字段→拆箱回写
                object boxed = UdpData.LCSInfos.Infos[channelID];
                FieldInfo field = typeof(_Feedback).GetField(varName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field == null) return false;

                object value = ConvertStringToTargetType(field.FieldType, input);
                field.SetValue(boxed, value);
                UdpData.LCSInfos.Infos[channelID] = (_Feedback)boxed;
                return true;
            }
            catch { return false; }
        }

        // ────────────────────────────────────────────────
        //  写回：Param（支持带下标的数组字段）
        // ────────────────────────────────────────────────
        private bool TryWriteParamValue(WatchConfig._VarietyInfo variety, string input)
        {
            try
            {
                string subName = variety.Port;
                string tail = variety.Source.Substring(variety.Source.LastIndexOf('.') + 1);

                string fieldName = tail;
                int arrIndex = -1;
                int lb = tail.IndexOf('[');
                if (lb >= 0)
                {
                    int rb = tail.IndexOf(']', lb + 1);
                    if (rb > lb)
                    {
                        fieldName = tail.Substring(0, lb);
                        int.TryParse(tail.Substring(lb + 1, rb - lb - 1), out arrIndex);
                    }
                }

                switch (subName)
                {
                    case "CLSModel": lock (ParamData.LockCLSModel) { object b = ParamData.CLS_Model; if (!SetField(ref b, fieldName, arrIndex, input)) return false; ParamData.CLS_Model = (ST_CLSModel)b; return true; }
                    case "CLSParam": lock (ParamData.LockCLSParam) { object b = ParamData.CLS_Param; if (!SetField(ref b, fieldName, arrIndex, input)) return false; ParamData.CLS_Param = (ST_CLSParam)b; return true; }
                    case "CLS5K": lock (ParamData.LockCLS5K) { object b = ParamData.CLS_5K; if (!SetField(ref b, fieldName, arrIndex, input)) return false; ParamData.CLS_5K = (ST_CLS5K)b; return true; }
                    case "CLSConsts": lock (ParamData.LockCLSConsts) { object b = ParamData.CLS_Consts; if (!SetField(ref b, fieldName, arrIndex, input)) return false; ParamData.CLS_Consts = (ST_CLSConsts)b; return true; }
                    case "TestMDL": lock (ParamData.LockTestMDL) { object b = ParamData.Test_MDL; if (!SetField(ref b, fieldName, arrIndex, input)) return false; ParamData.Test_MDL = (ST_TestMDL)b; return true; }
                    case "CLSEnum": lock (ParamData.LockCLSEnum) { object b = ParamData.CLS_Enum; if (!SetField(ref b, fieldName, arrIndex, input)) return false; ParamData.CLS_Enum = (ST_CLSEnum)b; return true; }
                    case "XT": lock (ParamData.LockXT) { object b = ParamData.Param_XT; if (!SetField(ref b, fieldName, arrIndex, input)) return false; ParamData.Param_XT = (ST_XT)b; return true; }
                    case "YT": lock (ParamData.LockYT) { object b = ParamData.Param_YT; if (!SetField(ref b, fieldName, arrIndex, input)) return false; ParamData.Param_YT = (ST_YT)b; return true; }
                    case "CtrlIn": lock (ParamData.LockCtrlIn) { object b = ParamData.CtrlIn; if (!SetField(ref b, fieldName, arrIndex, input)) return false; ParamData.CtrlIn = (ST_TcLCS_U)b; return true; }
                    case "CtrlOut": lock (ParamData.LockCtrlOut) { object b = ParamData.CtrlOut; if (!SetField(ref b, fieldName, arrIndex, input)) return false; ParamData.CtrlOut = (ST_TcLCS_Y)b; return true; }
                    case "DeviceInfo": lock (ParamData.LockDevInfo) { object b = ParamData.Device_Info; if (!SetField(ref b, fieldName, arrIndex, input)) return false; ParamData.Device_Info = (ST_DeviceInfo)b; return true; }
                    case "UdpDataCfg": lock (ParamData.LockUdpDataCfg) { object b = ParamData.UdpData_Cfg; if (!SetField(ref b, fieldName, arrIndex, input)) return false; ParamData.UdpData_Cfg = (ST_UDP_Parameter)b; return true; }
                    case "UdpParamCfg": lock (ParamData.LockUdpParamCfg) { object b = ParamData.UdpParam_Cfg; if (!SetField(ref b, fieldName, arrIndex, input)) return false; ParamData.UdpParam_Cfg = (ST_UDP_Parameter)b; return true; }
                    default: return false;
                }
            }
            catch { return false; }
        }

        // ────────────────────────────────────────────────
        //  反射写字段/数组元素（通用）
        // ────────────────────────────────────────────────
        private bool SetField(ref object boxed, string fieldName, int arrIndex, string input)
        {
            FieldInfo field = boxed.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) return false;

            // ★ 新增：TcString 字段写入
            var tcStr = field.GetCustomAttribute<TcStringAttribute>();
            if (tcStr != null && field.FieldType == typeof(byte[]))
            {
                byte[] buf = new byte[tcStr.MaxLen + 1];   // 16 字节，含终止符
                byte[] src = Encoding.ASCII.GetBytes(input.Trim());
                Array.Copy(src, buf, Math.Min(src.Length, tcStr.MaxLen));
                // 末位保持 0（终止符），已由 new byte[] 保证
                field.SetValue(boxed, buf);
                return true;
            }

            if (arrIndex >= 0)
            {
                Array arr = field.GetValue(boxed) as Array;
                if (arr == null || arrIndex >= arr.Length) return false;
                Type elemType = field.FieldType.GetElementType();
                arr.SetValue(ConvertStringToTargetType(elemType, input), arrIndex);
                field.SetValue(boxed, arr);
            }
            else
            {
                field.SetValue(boxed, ConvertStringToTargetType(field.FieldType, input));
            }
            return true;
        }

        // ────────────────────────────────────────────────
        //  字符串 → 目标类型转换（兼容 BOOL→byte 1/0）
        // ────────────────────────────────────────────────
        private object ConvertStringToTargetType(Type t, string s)
        {
            s = s.Trim();

            // ── Bool/Byte 的 true/false 兼容（保留原行为）──
            if (t == typeof(bool))
                return s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
            if (t == typeof(byte))
            {
                if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return (byte)1;
                if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return (byte)0;
            }

            // ── 整数类型：尝试识别 0x/0o/0b 前缀（按补码） ──
            if (IsIntegerType(t) && TryParseWithPrefix(s, out ulong u, out bool prefixed) && prefixed)
            {
                if (t == typeof(byte)) return (byte)u;
                if (t == typeof(sbyte)) return (sbyte)(byte)u;
                if (t == typeof(short)) return (short)(ushort)u;
                if (t == typeof(ushort)) return (ushort)u;
                if (t == typeof(int)) return (int)(uint)u;
                if (t == typeof(uint)) return (uint)u;
                if (t == typeof(long)) return (long)u;
                if (t == typeof(ulong)) return u;
            }

            // ── 无前缀：原有十进制解析 ──
            if (t == typeof(byte)) return byte.Parse(s);
            if (t == typeof(sbyte)) return sbyte.Parse(s);
            if (t == typeof(short)) return short.Parse(s);
            if (t == typeof(ushort)) return ushort.Parse(s);
            if (t == typeof(int)) return int.Parse(s);
            if (t == typeof(uint)) return uint.Parse(s);
            if (t == typeof(long)) return long.Parse(s);
            if (t == typeof(ulong)) return ulong.Parse(s);
            if (t == typeof(float)) return float.Parse(s);
            if (t == typeof(double)) return double.Parse(s);
            return Convert.ChangeType(s, t);
        }

        /// <summary>判断是否为整数类型（参与进制前缀解析）</summary>
        private static bool IsIntegerType(Type t)
        {
            return t == typeof(byte) || t == typeof(sbyte)
                || t == typeof(short) || t == typeof(ushort)
                || t == typeof(int) || t == typeof(uint)
                || t == typeof(long) || t == typeof(ulong);
        }

        /// <summary>
        /// 尝试按 0x/0o/0b 前缀解析整数。
        /// 返回值：是否解析成功；prefixed=true 表示识别到前缀（无论成败），用于上层决定是否回退到十进制。
        /// 输出 ulong（按补码），调用方根据目标类型自行截断。
        /// </summary>
        private static bool TryParseWithPrefix(string s, out ulong result, out bool prefixed)
        {
            result = 0;
            prefixed = false;
            if (string.IsNullOrEmpty(s) || s.Length < 3) return false;

            string p = s.Substring(0, 2);
            string body = s.Substring(2);
            int radix;

            if (p == "0x" || p == "0X") radix = 16;
            else if (p == "0b" || p == "0B") radix = 2;
            else if (p == "0o" || p == "0O") radix = 8;
            else return false;

            prefixed = true;
            try
            {
                result = Convert.ToUInt64(body, radix);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Watch 写回 JdTx 字段。
        /// JdRx 为只读，直接返回 false。
        /// JdTxFrame 是 class（引用类型），反射写 Property 即可，无需装箱。
        /// </summary>
        private bool TryWriteJdValue(WatchConfig._VarietyInfo variety, string input)
        {
            try
            {
                if (variety.Port != "JdTx") return false;  // JdRx 只读

                string source = variety.Source;
                string propName = source.Substring(source.LastIndexOf('.') + 1);

                var prop = typeof(JdTxFrame).GetProperty(propName,
                    BindingFlags.Instance | BindingFlags.Public);
                if (prop == null || !prop.CanWrite) return false;

                object value = ConvertStringToTargetType(prop.PropertyType, input);
                lock (JdData.JdTx)
                    prop.SetValue(JdData.JdTx, value);
                return true;
            }
            catch { return false; }
        }

        // 在 Watch 类头部添加（一次性初始化）：
        private static readonly Dictionary<(Type, string), FieldInfo> _fieldCache
            = new Dictionary<(Type, string), FieldInfo>();

        private Dictionary<string, int> _scopeRecordIndex = new Dictionary<string, int>();

        private static FieldInfo GetCachedField(Type t, string name)
        {
            var key = (t, name);
            if (!_fieldCache.TryGetValue(key, out var fi))
            {
                fi = t.GetField(name, BindingFlags.Instance | BindingFlags.Public);
                _fieldCache[key] = fi;
            }
            return fi;
        }



        //private void InitADS()
        //{
        //    if (!GlobalVar.isUdpConnceted)
        //        return;
        //    lock (activeVariables)
        //    {
        //        lock (adsClients)
        //        {
        //            adsClients.Clear();
        //            adsPortList.Clear();
        //            adsVariables.Clear();
        //            activeVariablesIndex.Clear();
        //            for (int i = 0; i < WatchConfig.VarietyInfos.Count; i++)
        //            {
        //                if (WatchConfig.VarietyInfos[i].Category == "ADS")
        //                {
        //                    if (adsClients.Count == 0)
        //                    {
        //                        string Port = WatchConfig.VarietyInfos[i].Port;
        //                        adsPortList.Add(Port);
        //                        AdsClient adsClient = new AdsClient(Port, GlobalVar.AmsNetID, int.Parse(Port));
        //                        adsClient.Init();
        //                        adsClients.Add(adsClient);

        //                        int size = STRING_Size(WatchConfig.VarietyInfos[i].Type);
        //                        string type = WatchConfig.VarietyInfos[i].Type;
        //                        if (size == -1)
        //                        {
        //                            foreach (TypeSize ts in ST_TypeSizeList)
        //                            {
        //                                if (ts.type == WatchConfig.VarietyInfos[i].Type)
        //                                {
        //                                    size = ts.size;
        //                                    break;
        //                                }
        //                            }
        //                        }
        //                        else
        //                        {
        //                            size = size + 1;
        //                            type = "STRING";
        //                        }

        //                        SymbolInfo symbol = new SymbolInfo()
        //                        {
        //                            symbolName = WatchConfig.VarietyInfos[i].VarName,
        //                            variableName = WatchConfig.VarietyInfos[i].Source,
        //                            variableLength = size,
        //                            value = new byte[size],
        //                            variableType = type
        //                        };

        //                        VariableInfo variableInfo = new VariableInfo()
        //                        {
        //                            indexGroup = (int)AdsReservedIndexGroups.SymbolValueByHandle,
        //                            indexOffset = -1,
        //                            length = size
        //                        };

        //                        Variables variables = new Variables()
        //                        {
        //                            symbols = new List<SymbolInfo>(),
        //                            variables = new List<VariableInfo>()
        //                        };
        //                        variables.symbols.Add(symbol);
        //                        variables.variables.Add(variableInfo);
        //                        adsVariables.Add(variables);
        //                    }
        //                    else
        //                    {
        //                        string Port = WatchConfig.VarietyInfos[i].Port;
        //                        if (!adsPortList.Contains(Port))
        //                        {
        //                            adsPortList.Add(Port);
        //                            AdsClient adsClient = new AdsClient(Port, GlobalVar.AmsNetID, int.Parse(Port));
        //                            adsClient.Init();
        //                            adsClients.Add(adsClient);

        //                            int size = STRING_Size(WatchConfig.VarietyInfos[i].Type);
        //                            string type = WatchConfig.VarietyInfos[i].Type;
        //                            if (size == -1)
        //                            {
        //                                foreach (TypeSize ts in ST_TypeSizeList)
        //                                {
        //                                    if (ts.type == WatchConfig.VarietyInfos[i].Type)
        //                                    {
        //                                        size = ts.size;
        //                                        break;
        //                                    }
        //                                }
        //                            }
        //                            else
        //                            {
        //                                size = size + 1;
        //                                type = "STRING";
        //                            }

        //                            SymbolInfo symbol = new SymbolInfo()
        //                            {
        //                                symbolName = WatchConfig.VarietyInfos[i].VarName,
        //                                variableName = WatchConfig.VarietyInfos[i].Source,
        //                                variableLength = size,
        //                                value = new byte[size],
        //                                variableType = type
        //                            };

        //                            VariableInfo variableInfo = new VariableInfo()
        //                            {
        //                                indexGroup = (int)AdsReservedIndexGroups.SymbolValueByHandle,
        //                                indexOffset = -1,
        //                                length = size
        //                            };

        //                            Variables variables = new Variables()
        //                            {
        //                                symbols = new List<SymbolInfo>(),
        //                                variables = new List<VariableInfo>()
        //                            };
        //                            variables.symbols.Add(symbol);
        //                            variables.variables.Add(variableInfo);
        //                            adsVariables.Add(variables);
        //                        }
        //                        else
        //                        {
        //                            int id = adsPortList.IndexOf(Port);
        //                            int size = STRING_Size(WatchConfig.VarietyInfos[i].Type);
        //                            string type = WatchConfig.VarietyInfos[i].Type;
        //                            if (size == -1)
        //                            {
        //                                foreach (TypeSize ts in ST_TypeSizeList)
        //                                {
        //                                    if (ts.type == WatchConfig.VarietyInfos[i].Type)
        //                                    {
        //                                        size = ts.size;
        //                                        break;
        //                                    }
        //                                }
        //                            }
        //                            else
        //                            {
        //                                size = size + 1;
        //                                type = "STRING";
        //                            }

        //                            SymbolInfo symbol = new SymbolInfo()
        //                            {
        //                                symbolName = WatchConfig.VarietyInfos[i].VarName,
        //                                variableName = WatchConfig.VarietyInfos[i].Source,
        //                                variableLength = size,
        //                                value = new byte[size],
        //                                variableType = type
        //                            };

        //                            VariableInfo variableInfo = new VariableInfo()
        //                            {
        //                                indexGroup = (int)AdsReservedIndexGroups.SymbolValueByHandle,
        //                                indexOffset = -1,
        //                                length = size
        //                            };

        //                            adsVariables[id].symbols.Add(symbol);
        //                            adsVariables[id].variables.Add(variableInfo);
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }        
        //    adsHandleCreate();
        //    _isAdsTargetChanged = true;
        //    dataGridView1.CurrentCell = null;
        //    textBox1.Visible = false;
        //}

        //private void DisposeADS()
        //{
        //    lock (adsClients)
        //    {
        //        adsPortList.Clear();
        //        adsClients.Clear();
        //        adsVariables.Clear();
        //        activeVariablesIndex.Clear();
        //        activeVariables.Clear();
        //    }
        //    _isAdsTargetChanged = true;
        //}
    }
}

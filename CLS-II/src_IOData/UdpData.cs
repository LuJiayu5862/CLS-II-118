using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CLS_II
{
    static class UdpData
    {
        public static _LCSControls LCSControls = new _LCSControls()
        {
            Controls = new _Control[CLSConsts.TotalChannels]
        };
        public static _LCSParams LCSParams = new _LCSParams()
        {
            wDataTP = 1,
            Params = new _Params[CLSConsts.TotalChannels]
        };
        public static _LCSInfos LCSInfos = new _LCSInfos()
        {
            Infos = new _Feedback[CLSConsts.TotalChannels]
        };
    }

    static class UdpWatch
    {
        public static List<_UDPTransmit> UDPControls = new List<_UDPTransmit>();
        public static List<_UDPTransmit> UDPParams = new List<_UDPTransmit>();
        public static List<_UDPReceive> UDPInfos = new List<_UDPReceive>();

        public static List<Type> UDPControlsType { get; } = new List<Type>();
        public static List<Type> UDPParamsType { get; } = new List<Type>();
        public static List<Type> UDPInfosType { get; } = new List<Type>();

        private static List<string> UDPControlsValue = new List<string>();
        private static List<string> UDPParamsValue = new List<string>();
        private static List<string> UDPInfosValue = new List<string>();

        public static void Init()
        {
            lock (UDPControls)
            {
                if (UDPControls.Count == 0)
                {
                    foreach (_FieldInfo field in typeof(_Control).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                    {
                        _UDPTransmit obj = new _UDPTransmit();
                        obj.Variable = field.Name;
                        UDPControls.Add(obj);
                        Type type = field.FieldType;
                        UDPControlsType.Add(type);
                    }
                }
            }

            lock (UDPParams)
            {
                if (UDPParams.Count == 0)
                {
                    foreach (_FieldInfo field in typeof(_Params).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                    {
                        _UDPTransmit obj = new _UDPTransmit();
                        obj.Variable = field.Name;
                        UDPParams.Add(obj);
                        Type type = field.FieldType;
                        UDPParamsType.Add(type);
                    }
                }
            }

            lock (UDPInfos)
            {
                if (UDPInfos.Count == 0)
                {
                    foreach (_FieldInfo field in typeof(_Feedback).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                    {
                        _UDPReceive obj = new _UDPReceive();
                        obj.Variable = field.Name;
                        UDPInfos.Add(obj);
                        Type type = field.FieldType;
                        UDPInfosType.Add(type);
                    }
                }
            }                 
        }

        public static void read_UDPControls(int id)
        {
            int i = 0;
            UDPControlsValue.Clear();
            foreach (_FieldInfo field in typeof(_Control).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                string value = string.Empty;
                try
                {
                    value = Convert.ToString(field.GetValue(UdpData.LCSControls.Controls[id]));
                }
                catch (NullReferenceException)
                {
                    value = null;
                }
                if (!string.IsNullOrEmpty(value))
                {
                    if (UDPControls.Count > 0)
                    {
                        UDPControls[i].Value = value;
                    }
                }
                else
                {
                    UDPControls[i].Value = string.Empty;
                }
                UDPControls[i].NextValue = string.Empty;
                i++;
            }
        }

        public static void read_UDPParams(int id)
        {
            int i = 0;
            UDPParamsValue.Clear();
            foreach (_FieldInfo field in typeof(_Params).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                string value = string.Empty;
                try
                {
                    value = Convert.ToString(field.GetValue(UdpData.LCSParams.Params[id]));
                }
                catch (NullReferenceException)
                {
                    value = null;
                }
                if (!string.IsNullOrEmpty(value))
                {
                    if (UDPParams.Count > 0)
                    {
                        UDPParams[i].Value = value;
                    }
                }
                else
                {
                    UDPParams[i].Value = string.Empty;
                }
                UDPParams[i].NextValue = string.Empty;
                i++;
            }
        }

        public static void write_UDPControls(int id)
        {
            lock (UdpData.LCSControls)
            {
                int index = 0;
                foreach (FieldInfo field in typeof(_Control).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    string type = field.FieldType.Name;
                    switch (type)
                    {
                        case "Single":
                            {
                                Single value = Single.Parse(UDPControls[index].Value);
                                if (id == -1)
                                {
                                    for (int i = 0; i < CLSConsts.EnabledChannels; i++)
                                    {
                                        object box = UdpData.LCSControls.Controls[i];
                                        field.SetValue(box, value);
                                        UdpData.LCSControls.Controls[i] = (_Control)box;
                                    }
                                }
                                else
                                {
                                    object box = UdpData.LCSControls.Controls[id];
                                    field.SetValue(box, value);
                                    UdpData.LCSControls.Controls[id] = (_Control)box;
                                }
                            }
                            break;
                        case "Int32":
                            {
                                Int32 value = Int32.Parse(UDPControls[index].Value);
                                if (id == -1)
                                {
                                    for (int i = 0; i < CLSConsts.EnabledChannels; i++)
                                    {
                                        object box = UdpData.LCSControls.Controls[i];
                                        field.SetValue(box, value);
                                        UdpData.LCSControls.Controls[i] = (_Control)box;
                                    }
                                }
                                else
                                {
                                    object box = UdpData.LCSControls.Controls[id];
                                    field.SetValue(box, value);
                                    UdpData.LCSControls.Controls[id] = (_Control)box;
                                }
                            }
                            break;
                        case "UInt32":
                            {
                                UInt32 value = UInt32.Parse(UDPControls[index].Value);
                                if (id == -1)
                                {
                                    for (int i = 0; i < CLSConsts.EnabledChannels; i++)
                                    {
                                        object box = UdpData.LCSControls.Controls[i];
                                        field.SetValue(box, value);
                                        UdpData.LCSControls.Controls[i] = (_Control)box;
                                    }
                                }
                                else
                                {
                                    object box = UdpData.LCSControls.Controls[id];
                                    field.SetValue(box, value);
                                    UdpData.LCSControls.Controls[id] = (_Control)box;
                                }
                            }
                            break;
                    }
                    index++;
                }
            }
        }

        public static void write_UDPParams(int id)
        {
            lock (UdpData.LCSParams)
            {
                int index = 0;
                foreach (FieldInfo field in typeof(_Params).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    string type = field.FieldType.Name;
                    switch (type)
                    {
                        case "Single":
                            {
                                Single value = Single.Parse(UDPParams[index].Value);
                                if (id == -1)
                                {
                                    for (int i = 0; i < CLSConsts.EnabledChannels; i++)
                                    {
                                        object box = UdpData.LCSParams.Params[i];
                                        field.SetValue(box, value);
                                        UdpData.LCSParams.Params[i] = (_Params)box;
                                    }
                                }
                                else
                                {
                                    object box = UdpData.LCSParams.Params[id];
                                    field.SetValue(box, value);
                                    UdpData.LCSParams.Params[id] = (_Params)box;
                                }
                            }
                            break;
                        case "Int32":
                            {
                                Int32 value = Int32.Parse(UDPParams[index].Value);
                                if (id == -1)
                                {
                                    for (int i = 0; i < CLSConsts.EnabledChannels; i++)
                                    {
                                        object box = UdpData.LCSParams.Params[i];
                                        field.SetValue(box, value);
                                        UdpData.LCSParams.Params[i] = (_Params)box;
                                    }
                                }
                                else
                                {
                                    object box = UdpData.LCSParams.Params[id];
                                    field.SetValue(box, value);
                                    UdpData.LCSParams.Params[id] = (_Params)box;
                                }
                            }
                            break;
                        case "UInt32":
                            {
                                UInt32 value = UInt32.Parse(UDPParams[index].Value);
                                if (id == -1)
                                {
                                    for (int i = 0; i < CLSConsts.EnabledChannels; i++)
                                    {
                                        object box = UdpData.LCSParams.Params[i];
                                        field.SetValue(box, value);
                                        UdpData.LCSParams.Params[i] = (_Params)box;
                                    }
                                }
                                else
                                {
                                    object box = UdpData.LCSParams.Params[id];
                                    field.SetValue(box, value);
                                    UdpData.LCSParams.Params[id] = (_Params)box;
                                }
                            }
                            break;
                    }
                    index++;
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct _Params
    {
        public Single ShakerA;
        public Single ShakerF;
        public Single CblStiff;
        public Single CblDamp;
        public Single AftDeadZ;
        public Single AftTravA;
        public Single AftTravB;
        public Single AftFric;
        public Single AftDamp;
        public Single K1;
        public Single K2;
        public Single K3;
        public Single X1;
        public Single X2;
        public Single BkLevel;
        public Single BkGrad;
        public Single K4;
        public Single K5;
        public Single X5;
        public Single X6;
        public Single L0Trim;
        public Int32 VPOS;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct _Feedback
    {
        public Int32 state;
        public Int32 isFading;
        public Int32 safety;
        public Single fwdPosition;
        public Single fwdVelocity;
        public Single fwdForce;
        public Single cableForce;
        public Single trimPosition;
        public Single aftPosition;

        public Single reserve1;
        public Single reserve2;
        public Single reserve3;
        public Single reserve4;
    }

    //[StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct _Control
    {
        public UInt32 CtrlCmd;
        public Single fwdFric;
        public Single jamPos;
        public Single TravA;
        public Single TravB;
        public Single fwdMassD;
        public Single fwdDampD;
        public Single FInput;
        public Single Vap;
        public UInt32 FnSwitch;
        public Single VTrim;
        public Single FaOffset;
        public Single FaGrad;
        public Single trimInitP;
        public Single Spare1;
        public Single Spare2;
        public Single Spare3;
    }

    /// <summary>
    /// 上位机发送给操纵负荷控制器的非周期参数内容
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public class _LCSParams
    {
        public UInt32 wChannel;
        public UInt32 wDataTP;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CLSConsts.TotalChannels)]
        public _Params[] Params;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public class _LCSInfos
    {
        public Int32 systemState;
        public Int32 safeStatus;
        public Int32 statusFail1;
        public Int32 statusFail2;
        public Int32 statusFail3;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CLSConsts.TotalChannels)]
        public _Feedback[] Infos;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public class _LCSControls
    {
        public UInt32 wTotalNum;
        public UInt32 wSystemFn;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CLSConsts.TotalChannels)]
        public _Control[] Controls;
    }

    public class _UDPTransmit
    {
        public string Variable { get; set; }
        public string Value { get; set; }
        public string NextValue { get; set; }
    }

    public class _UDPReceive
    {
        public string Variable { get; set; }
        public string Value { get; set; }
    }
}

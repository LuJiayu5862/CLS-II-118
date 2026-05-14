using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CLS_II
{
    public static class GlobalVar
    {
        public static bool isAdministrator = false;
        public static bool isSingleWatch = true;              // 当为true时，刷新一次Watch窗口数值
        public static bool isSimulation = false;
        public static bool isUdpConnceted = false;
        public static bool isSendUdp = false;
        public static bool isUdpAllAccept = false;
        public static bool isParamChanged = false;
        public static bool isSaveAperiod = false;
        public static bool isAllChannelStop = false;
        public static bool isAllChannelReset = false;
        public static bool isProjectFileChanged = false;
        public static bool isTmpProjectFile = false;
        public static readonly int MainPeriod = 5;

        public static string ProjectFile = string.Empty;
        public static string DeviceName = "Untitled Device";
        public static string tmpProjectFile
        {
            get
            {
                if (string.IsNullOrEmpty(ProjectFile))
                {
                    return string.Empty;
                }
                else
                {
                    string ProjectName = Path.GetFileName(ProjectFile);
                    string ProjectDirectory = Path.GetDirectoryName(ProjectFile);
                    return ProjectDirectory + "\\~" + ProjectName;
                }
            }
        }
        public static string ProjectName
        {
            get
            {
                if (string.IsNullOrEmpty(ProjectFile))
                {
                    return string.Empty;
                }
                else
                {
                    string ProjectName = Path.GetFileName(ProjectFile);
                    ProjectName = ProjectName.Substring(0, ProjectName.LastIndexOf("."));
                    return ProjectName;
                }
            }
        }
        public static string szRemoteHost = "127.0.0.1";
        public static int nPortIn = 1703, nPortOut1 = 1702, nPortOut2 = 1704;
    }

    public static class CLSConsts
    {
        public const int TotalChannels = 10;
        public static int EnabledChannels = TotalChannels;
        public const int TotalSymbols = 5;
    }

    // ============================================================
    //  JD-61101 通道配置常量
    //  与旧通道 GlobalVar 完全独立，不修改原字段
    // ============================================================
    public static class JdConsts
    {
        public static bool   isJdUdpConnected = false;
        public static string szJdRemoteHost   = "192.168.118.118";
        public static int    nJdPortSend      = 15000;  // PC → PLC
        public static int    nJdPortRecv      = 16000;  // PLC → PC
    }

    // ============================================================
    //  TcLCS v1.1 参数通道配置常量
    //  与旧通道 GlobalVar 完全独立，不修改原字段
    // ============================================================
    public static class ParamConsts
    {
        public static bool   isParamUdpConnected = false;
        public static string szParamRemoteHost   = "192.168.118.118";
        public static int    nParamPortSend      = 5050;  // PC → 控制器
        public static int    nParamPortRecv      = 8080;  // 控制器 → PC
        public static byte   byParamDeviceId     = 0x01;  // 设备号  
    }
}

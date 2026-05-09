using INIFileRW;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CLS_II
{
    class UdpConfig
    {
        public static string udpConfigFile = @".\UdpConfig.ini";

        public static void SetDefaultWatchConfigFile(string file)
        {
            udpConfigFile = file;
        }

        public static void ConfigFileInit()
        {
            if (File.Exists(udpConfigFile))
            {
                ReadConfigFile();
            }
            else
            {
                CreateConfigFile();
                ReadConfigFile();
            }
        }

        private static void CreateConfigFile()
        {
            // Info 
            for (int i = 0; i < CLSConsts.TotalChannels; i++)
            {
                string section1 = "Channel" + (i + 1) + ".Period";                
                iniFileRW.INIWriteValue(udpConfigFile, section1, "TravA", "0");
                iniFileRW.INIWriteValue(udpConfigFile, section1, "TravB", "0");
                iniFileRW.INIWriteValue(udpConfigFile, section1, "fwdMassD", "0");

                string section2 = "Channel" + (i + 1) + ".Param";
                foreach (FieldInfo field in typeof(_Params).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    string key = field.Name;
                    iniFileRW.INIWriteValue(udpConfigFile, section2, key, "0");
                }
            }
        }

        private static void ReadConfigFile()
        {
            lock (UdpData.LCSControls)
            {
                for (int i = 0; i < CLSConsts.TotalChannels; i++)
                {
                    string section1 = "Channel" + (i + 1) + ".Period";
                    UdpData.LCSControls.Controls[i].TravA = Single.Parse(iniFileRW.INIGetStringValue(udpConfigFile, section1, "TravA", "0"));
                    UdpData.LCSControls.Controls[i].TravB = Single.Parse(iniFileRW.INIGetStringValue(udpConfigFile, section1, "TravB", "0"));
                    UdpData.LCSControls.Controls[i].fwdMassD = Single.Parse(iniFileRW.INIGetStringValue(udpConfigFile, section1, "fwdMassD", "0"));
                }
                UdpWatch.read_UDPControls(0);
            }

            lock (UdpData.LCSParams)
            {
                for (int i = 0; i < CLSConsts.TotalChannels; i++)
                {
                    string section2 = "Channel" + (i + 1) + ".Param";
                    foreach (FieldInfo field in typeof(_Params).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                    {
                        string key = field.Name;
                        object box = UdpData.LCSParams.Params[i];
                        string value = iniFileRW.INIGetStringValue(udpConfigFile, section2, key, "0");
                        field.SetValue(box, Struct_Func.Format(value, field.FieldType));
                        UdpData.LCSParams.Params[i] = (_Params)box;
                    }
                }
                UdpWatch.read_UDPParams(0);
            }
        }

        public static void WriteConfigFile()
        {
            lock (UdpData.LCSControls)
            {
                for (int i = 0; i < CLSConsts.EnabledChannels; i++)
                {
                    string section1 = "Channel" + (i + 1) + ".Period";
                    string value1 = Convert.ToString(UdpData.LCSControls.Controls[i].TravA);
                    string value2 = Convert.ToString(UdpData.LCSControls.Controls[i].TravB);
                    string value3 = Convert.ToString(UdpData.LCSControls.Controls[i].fwdMassD);
                    iniFileRW.INIWriteValue(udpConfigFile, section1, "TravA", value1);
                    iniFileRW.INIWriteValue(udpConfigFile, section1, "TravB", value2);
                    iniFileRW.INIWriteValue(udpConfigFile, section1, "fwdMassD", value2);
                }
            }

            lock (UdpData.LCSParams)
            {
                for (int i = 0; i < CLSConsts.EnabledChannels; i++)
                {
                    string section2 = "Channel" + (i + 1) + ".Param";
                    foreach (FieldInfo field in typeof(_Params).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                    {
                        string key = field.Name;
                        string value = string.Empty;
                        try
                        {
                            value = Convert.ToString(field.GetValue(UdpData.LCSParams.Params[i]));
                        }
                        catch (NullReferenceException)
                        {
                            value = string.Empty;
                        }
                        iniFileRW.INIWriteValue(udpConfigFile, section2, key, value);
                    }
                }
            }
        }
    }
}

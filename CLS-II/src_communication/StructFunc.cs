using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CLS_II
{
    public static class Struct_Func
    {
        public static string ToHexStrFromByte(this byte[] byteDatas)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < byteDatas.Length; i++)
            {
                builder.Append(string.Format("{0:X2} ", byteDatas[i]));
            }
            return builder.ToString().Replace(" ","");
        }

        public static string myToString(this object obj)
        {
            if (obj is null)
                return String.Empty;
            else
                return obj.ToString();
        }

        public static int GetIndexOf(this byte[] b, byte[] bb)
        {
            if (b == null || bb == null || b.Length == 0 || bb.Length == 0 || b.Length < bb.Length)
                return -1;
            int i, j;
            for (i = 0; i < b.Length - bb.Length + 1; i++)
            {
                if (b[i] == bb[0])
                {
                    for (j = 1; j < bb.Length; j++)
                    {
                        if (b[i + j] != bb[j])
                            break;
                    }
                    if (j == bb.Length)
                        return i;
                }
            }
            return -1;
        }

        public static byte[] HexStringToByteArray(this string s)
        {
            s = s.Replace(" ", "");
            byte[] buffer = new byte[s.Length / 2];
            for (int i = 0; i < s.Length; i += 2)
            {
                buffer[i / 2] = (byte)Convert.ToByte(s.Substring(i, 2), 16);
            }
            return buffer;
        }

        public static byte[] ASCIIStringToByteArray(this string s)
        {
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(s);
            List<byte> result = new List<byte>(bytes);
            result.Add(0);
            return result.ToArray();
        }

        /// <summary>
        /// 将常见的数据类型数组转为二进制数组
        /// </summary>
        /// <typeparam name="T">数据类行</typeparam>
        /// <param name="data">数据</param>
        /// <returns>byte数组</returns>
        public static byte[] datas_to_bytes<T>(T[] data)
        {
            byte[] bydata;
            if (typeof(T) == typeof(int))
            {
                bydata = new byte[data.Length * 4];
                for (int i = 0; i < data.Length; i++)
                {
                    int tem = Convert.ToInt32(data[i]);
                    byte[] b = BitConverter.GetBytes(tem);
                    for (int j = 0; j < 4; j++)
                    {
                        bydata[i * 4 + j] = b[j];
                    }
                }
            }
            else if (typeof(T) == typeof(long))
            {
                bydata = new byte[data.Length * 8];
                for (int i = 0; i < data.Length; i++)
                {
                    long tem = (long)Convert.ToInt64(data[i]);
                    byte[] b = BitConverter.GetBytes(tem);
                    for (int j = 0; j < 8; j++)
                    {
                        bydata[i * 8 + j] = b[j];
                    }
                }
            }
            else if (typeof(T) == typeof(short))
            {
                bydata = new byte[data.Length * 2];
                for (int i = 0; i < data.Length; i++)
                {
                    short tem = (short)Convert.ToInt16(data[i]);
                    byte[] b = BitConverter.GetBytes(tem);
                    for (int j = 0; j < 2; j++)
                    {
                        bydata[i * 2 + j] = b[j];
                    }
                }
            }
            else if (typeof(T) == typeof(uint))
            {
                bydata = new byte[data.Length * 4];
                for (int i = 0; i < data.Length; i++)
                {
                    uint tem = Convert.ToUInt32(data[i]);
                    byte[] b = BitConverter.GetBytes(tem);
                    for (int j = 0; j < 4; j++)
                    {
                        bydata[i * 4 + j] = b[j];
                    }
                }
            }
            else if (typeof(T) == typeof(ulong))
            {
                bydata = new byte[data.Length * 8];
                for (int i = 0; i < data.Length; i++)
                {
                    ulong tem = (ulong)Convert.ToUInt64(data[i]);
                    byte[] b = BitConverter.GetBytes(tem);
                    for (int j = 0; j < 8; j++)
                    {
                        bydata[i * 8 + j] = b[j];
                    }
                }
            }
            else if (typeof(T) == typeof(ushort))
            {
                bydata = new byte[data.Length * 2];
                for (int i = 0; i < data.Length; i++)
                {
                    ushort tem = (ushort)Convert.ToUInt16(data[i]);
                    byte[] b = BitConverter.GetBytes(tem);
                    for (int j = 0; j < 2; j++)
                    {
                        bydata[i * 2 + j] = b[j];
                    }
                }
            }
            else if (typeof(T) == typeof(float))
            {
                bydata = new byte[data.Length * 4];
                for (int i = 0; i < data.Length; i++)
                {
                    float tem = (float)Convert.ToDouble(data[i]);
                    byte[] b = BitConverter.GetBytes(tem);
                    for (int j = 0; j < 4; j++)
                    {
                        bydata[i * 4 + j] = b[j];
                    }
                }
            }
            else if (typeof(T) == typeof(double))
            {
                bydata = new byte[data.Length * 8];
                for (int i = 0; i < data.Length; i++)
                {
                    double tem = Convert.ToDouble(data[i]);
                    byte[] b = BitConverter.GetBytes(tem);
                    for (int j = 0; j < 8; j++)
                    {
                        bydata[i * 8 + j] = b[j];
                    }
                }
            }
            else if (typeof(T) == typeof(char))
            {
                bydata = new byte[data.Length * 2];
                for (int i = 0; i < data.Length; i++)
                {
                    char tem = Convert.ToChar(data[i]);
                    byte[] b = BitConverter.GetBytes(tem);
                    for (int j = 0; j < 2; j++)
                    {
                        bydata[i * 2 + j] = b[j];
                    }
                }
            }
            else
            {
                bydata = new byte[1];
            }
            return bydata;
        }

        /// <summary>
        /// 将byte数组转为指定的数据类型
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="bydata">输入数组</param>
        /// <returns数组></returns>
        public static T[] bytes_to_datas<T>(byte[] bydata)
        {
            T[] data;
            if (typeof(T) == typeof(int))
            {
                data = new T[bydata.Length / 4];
                for (int i = 0; i < data.Length; i++)
                {
                    byte[] b = new byte[4];
                    for (int j = 0; j < 4; j++)
                    {
                        b[j] = bydata[i * 4 + j];
                    }
                    object tem = BitConverter.ToInt32(b, 0);
                    data[i] = (T)tem;
                }
            }
            else if (typeof(T) == typeof(long))
            {
                data = new T[bydata.Length / 8];
                for (int i = 0; i < data.Length; i++)
                {
                    byte[] b = new byte[8];
                    for (int j = 0; j < 8; j++)
                    {
                        b[j] = bydata[i * 8 + j];
                    }
                    object tem = BitConverter.ToInt64(b, 0);
                    data[i] = (T)tem;
                }
            }
            else if (typeof(T) == typeof(short))
            {
                data = new T[bydata.Length / 2];
                for (int i = 0; i < data.Length; i++)
                {
                    byte[] b = new byte[2];
                    for (int j = 0; j < 2; j++)
                    {
                        b[j] = bydata[i * 2 + j];
                    }
                    object tem = BitConverter.ToInt16(b, 0);
                    data[i] = (T)tem;
                }
            }
            else if (typeof(T) == typeof(uint))
            {
                data = new T[bydata.Length / 4];
                for (int i = 0; i < data.Length; i++)
                {
                    byte[] b = new byte[4];
                    for (int j = 0; j < 4; j++)
                    {
                        b[j] = bydata[i * 4 + j];
                    }
                    object tem = BitConverter.ToUInt32(b, 0);
                    data[i] = (T)tem;
                }
            }
            else if (typeof(T) == typeof(ulong))
            {
                data = new T[bydata.Length / 8];
                for (int i = 0; i < data.Length; i++)
                {
                    byte[] b = new byte[8];
                    for (int j = 0; j < 8; j++)
                    {
                        b[j] = bydata[i * 8 + j];
                    }
                    object tem = BitConverter.ToUInt64(b, 0);
                    data[i] = (T)tem;
                }
            }
            else if (typeof(T) == typeof(ushort))
            {
                data = new T[bydata.Length / 2];
                for (int i = 0; i < data.Length; i++)
                {
                    byte[] b = new byte[2];
                    for (int j = 0; j < 2; j++)
                    {
                        b[j] = bydata[i * 2 + j];
                    }
                    object tem = BitConverter.ToUInt16(b, 0);
                    data[i] = (T)tem;
                }
            }
            else if (typeof(T) == typeof(float))
            {
                data = new T[bydata.Length / 4];
                for (int i = 0; i < data.Length; i++)
                {
                    byte[] b = new byte[4];
                    for (int j = 0; j < 4; j++)
                    {
                        b[j] = bydata[i * 4 + j];
                    }
                    object tem = (float)BitConverter.ToDouble(b, 0);
                    data[i] = (T)tem;
                }
            }
            else if (typeof(T) == typeof(double))
            {
                data = new T[bydata.Length / 8];
                for (int i = 0; i < data.Length; i++)
                {
                    byte[] b = new byte[8];
                    for (int j = 0; j < 8; j++)
                    {
                        b[j] = bydata[i * 8 + j];
                    }
                    object tem = BitConverter.ToDouble(b, 0);
                    data[i] = (T)tem;
                }
            }
            else if (typeof(T) == typeof(char))
            {
                data = new T[bydata.Length / 2];
                for (int i = 0; i < data.Length; i++)
                {
                    byte[] b = new byte[2];
                    for (int j = 0; j < 2; j++)
                    {
                        b[j] = bydata[i * 2 + j];
                    }
                    object tem = BitConverter.ToChar(b, 0);
                    data[i] = (T)tem;
                }
            }
            else
            {
                data = new T[1];
            }
            return data;
        }

        public static object BytesToStruct(byte[] bytes, object ob)
        {
            Type type = ob.GetType();
            int size = Marshal.SizeOf(type);
            if (size != bytes.Length)
            {
                return null;
            }
            IntPtr structPtr = Marshal.AllocHGlobal(size);
            Marshal.Copy(bytes, 0, structPtr, size);
            object obj = Marshal.PtrToStructure(structPtr, type);
            Marshal.FreeHGlobal(structPtr);
            return obj;
        }

        public static byte[] StructToBytes(object structObj)
        {
            int size = Marshal.SizeOf(structObj);
            byte[] bytes = new byte[size];
            IntPtr structPtr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(structObj, structPtr, false);
            Marshal.Copy(structPtr, bytes, 0, size);
            Marshal.FreeHGlobal(structPtr);
            return bytes;
        }

        public static T FromType<T, TK>(TK text)
        {
            try
            {
                return (T)Convert.ChangeType(text, typeof(T), CultureInfo.InvariantCulture);
            }
            catch
            {
                return default(T);
            }
        }

        public static Object Format(String str, Type type)
        {
            if (String.IsNullOrEmpty(str))
                return null;
            if (type == null)
                return str;
            if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                String[] strs = str.Split(new char[] { ';' });
                Array array = Array.CreateInstance(elementType, strs.Length);
                for (int i = 0, c = strs.Length; i < c; ++i)
                {
                    array.SetValue(ConvertSimpleType(strs[i], elementType), i);
                }
                return array;
            }
            return ConvertSimpleType(str, type);
        }

        private static object ConvertSimpleType(object value, Type destinationType)
        {
            object returnValue;
            if ((value == null) || destinationType.IsInstanceOfType(value))
            {
                return value;
            }
            string str = value as string;
            if ((str != null) && (str.Length == 0))
            {
                return null;
            }
            TypeConverter converter = TypeDescriptor.GetConverter(destinationType);
            bool flag = converter.CanConvertFrom(value.GetType());
            if (!flag)
            {
                converter = TypeDescriptor.GetConverter(value.GetType());
            }
            if (!flag && !converter.CanConvertTo(destinationType))
            {
                throw new InvalidOperationException("无法转换成类型：" + value.ToString() + "==>" + destinationType);
            }
            try
            {
                returnValue = flag ? converter.ConvertFrom(null, null, value) : converter.ConvertTo(null, null, value, destinationType);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("类型转换出错：" + value.ToString() + "==>" + destinationType, e);
            }
            return returnValue;
        }
    }
}

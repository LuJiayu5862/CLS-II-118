using System;

namespace CLS_II
{
    /// <summary>
    /// 标记 byte[] 字段在 Watch 中应以 ASCII 字符串方式显示/编辑。
    /// maxLen：有效字符最大长度（不含终止符），对应 PLC STRING(N) 中的 N。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TcStringAttribute : Attribute
    {
        public int MaxLen { get; }
        public TcStringAttribute(int maxLen) { MaxLen = maxLen; }
    }
}
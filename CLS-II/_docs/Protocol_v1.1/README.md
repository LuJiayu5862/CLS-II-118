# TcLCS-UDP Communication Protocol v1.1

> 本目录存放 TcLCS-UDP 通信协议 **v1.1** 的所有相关文档。
> 该协议已在 TwinCAT 3（ST）主站与 CLS-II 上位机（C#）之间完成实现并验证。

## 已验证功能

- 周期写：5ms 周期无丢包，最快 2ms（winmm 定时器）
- 差分写：100ms 检测快照差异，入队异步写入
- 周期读：实时数据 10ms，非实时 100ms / 1s / 2s 分级读取

## 文档列表

| 文件 | 说明 |
|---|---|
| `TcLCS-UDP_Protocol_v1.1.docx` | 主协议规范文档 |
| `TcLCS-UDP_Protocol_v1.1_AppendixC.docx` | 附录C |
| `TcLCS-UDP_TestCard_v1.1.docx` | 测试卡 |
| `TcLCS-UDP_v1.1_Handoff_Snapshot.md` | 交接快照 |
| `JD-61101-UDP通信协议.docx` | 参考协议（JD-61101）|

> 原始文档位于 `CLS-II/_docs/` 根目录，本目录为归档索引。后续 v1.1 相关文档请统一放置于此。

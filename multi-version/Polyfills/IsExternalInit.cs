// Polyfill: .NET Framework 4.6.1 缺少 IsExternalInit 类型，
// 导致 C# 9 的 init 访问器和 record 类型无法编译。
// 定义此类型后编译器自动识别，运行时无实际调用。

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

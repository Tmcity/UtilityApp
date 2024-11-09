using System;
using WinFormium;

class Program
{
    [STAThread]
    // Main函数中的代码应该在这里，该函数只在主进程中运行。这样可以防止子进程运行一些不正确的初始化代码。
    static void Main(string[] args)
    {
        // 创建WinFormiumApp的构建器
        var builder = WinFormiumApp.CreateBuilder();

        // 使用MyApp类作为应用程序的入口点
        builder.UseWinFormiumApp<MyApp>();

        // 构建应用程序
        var app = builder.Build();

        // 运行应用程序
        app.Run();
    }
}
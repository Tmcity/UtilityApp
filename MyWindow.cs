using WinFormium;
using WinFormium.Forms;

class MyWindow : Formium
{
    public MyWindow()
    {
        // 设置主页地址
        Url = "https://cloud.tmcity233.com";
    }

    protected override FormStyle ConfigureWindowStyle(WindowStyleBuilder builder)
    {
        // 此处配置窗体的样式和属性，或不继承此方法以使用默认样式。

        var style = builder.UseSystemForm();

        style.TitleBar = true;

        style.DefaultAppTitle = "天幕の云盘";

        return style;
    }
}
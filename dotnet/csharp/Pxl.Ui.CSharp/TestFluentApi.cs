using Pxl;
using Pxl.Ui;

foreach (var frame in Scene.Frames)
{
    frame
        .DrawLine(5, 3, 15, 13)
        .Color(Colors.black)
        .Thickness(2.0)
        .AntiAlias(true);

    frame
        .DrawLine(10, 10, 50, 50)
        .Color(Colors.red)
        .Thickness(3.0)
        .AntiAlias(true);
}

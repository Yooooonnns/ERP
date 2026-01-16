using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Threading.Tasks;

namespace DigitalisationERP.Launcher;

public class Program
{
    private static readonly string LogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "DigitalisationERP_Launcher.log");

    [STAThread]
    public static void Main(string[] args)
    {
        var app = new App();
        app.Run();
    }
}

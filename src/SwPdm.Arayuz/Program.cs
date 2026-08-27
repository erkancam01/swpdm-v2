using System;
using System.Windows.Forms;

namespace SwPdm.Arayuz;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Bu ucu ILK pencereden ONCE cagrilmali.
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // CLAUDE.md 3: hata sebebi EKRANDA gosterilir, yalnizca gunluge degil.
        //              Sessiz olum yasak.
        // CLAUDE.md 6: iki kanca AYNI SEY DEGIL -
        //   ThreadException  -> sonlanmayi GERCEKTEN engelliyor,
        //   UnhandledException -> engelleyemiyor, yalnizca olumu seyrediyor.
        // Bu yuzden ikisi de kuruluyor ve ikisi de sebebi ekrana yaziyor.
        Application.ThreadException += (_, e) => Bildir(e.Exception, kurtarilamaz: false);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Bildir(e.ExceptionObject as Exception, kurtarilamaz: true);

        Application.Run(new AnaForm());
    }

    private static void Bildir(Exception? hata, bool kurtarilamaz)
    {
        string metin = hata?.ToString()
                       ?? "Sebebi okunamayan bir hata oluştu. Ayrıntı elde yok.";
        string baslik = kurtarilamaz
            ? "SW PDM — kurtarılamayan hata"
            : "SW PDM — hata";

        MessageBox.Show(metin, baslik, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

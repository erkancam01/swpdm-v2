using System;
using System.Windows.Forms;

namespace SwPdm.Arayuz;

internal static class Program
{
    [STAThread]
    private static void Main(string[] argumanlar)
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

        Application.Run(new AnaForm(KokArgumani(argumanlar)));
    }

    /// <summary>
    /// "--klasor &lt;yol&gt;" argumanini okur. Uygulamanin belirli bir kokle
    /// acilabilmesi hem Gezgin'den acmayi mumkun kilar hem de CALISTIRMA
    /// KAPISININ dolu bir agaci gorebilmesini saglar - yoksa kapi yalnizca
    /// bos pencereyi olcerdi.
    /// </summary>
    private static string? KokArgumani(string[] argumanlar)
    {
        for (int i = 0; i < argumanlar.Length - 1; i++)
        {
            if (string.Equals(argumanlar[i], "--klasor", StringComparison.OrdinalIgnoreCase))
            {
                return argumanlar[i + 1];
            }
        }

        return null;
    }

    private static void Bildir(Exception? hata, bool kurtarilamaz)
    {
        string metin = hata?.ToString()
                       ?? "Sebebi okunamayan bir hata oluştu. Ayrıntı elde yok.";
        string baslik = kurtarilamaz
            ? "SW PDM — kurtarılamayan hata"
            : "SW PDM — hata";

        // CLAUDE.md 4: cikti bir yere DE yazilmali. Kutu kapaninca kullanicinin
        // elinde hicbir kanit kalmiyor; hata akisa da dusuyor ki hem gunluge
        // yonlendirilebilsin hem de calistirma kapisi okuyabilsin.
        Console.Error.WriteLine(baslik + ": " + metin);

        MessageBox.Show(metin, baslik, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

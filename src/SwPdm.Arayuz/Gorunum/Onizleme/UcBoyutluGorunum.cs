using System;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// 3B ONIZLEME - SOLIDWORKS'un kendi eDrawings denetimi panele gomulur.
/// eDrawings'e dair HER SEY bu dosyada (CLAUDE.md 1b): silmek = bu dosyayi
/// sil + Onizleme'deki tek dallanmayi kes.
///
/// NEDEN eDrawings: 3B'nin baska gercekci yolu yok - kendi motorumuz,
/// dosyadaki geometri akislarini cozmek demek (olculmemis aylik arastirma).
/// eDrawings kullanicinin makinesinden gelir: pakete bayt eklemez ve
/// "SOLIDWORKS ne gosteriyorsa onu goster" sozunu bozmaz (PDF/WinRT'nin
/// geri cekilme sebeplerinin ikisi de burada yok).
///
/// BU DOSYA BURADA HIC OLCULEMEZ (CLAUDE.md 2, 11): Wine'da eDrawings yok.
/// O yuzden her adim tek tek yakalanir ve SEBEP dondurulur; cokme asla
/// sizmaz. Ilk gercek olcum Erkan'in makinesi - SURUM-NOTU soyluyor.
///
/// DOSYA KILIDI (CLAUDE.md 1a): eDrawings actigi dosyayi kilitli tutabilir;
/// tasima/ad degistirme o kilide carpar. Bu yuzden <see cref="BelgeyiKapat"/>
/// var ve HER islem baslamadan cagriliyor (AnaForm baglar).
///
/// COM COZUMLEMESI - olculmus tuzaklara gore (CLAUDE.md 5):
///   - CLSID ELLE YAZILMIYOR (tahmin olurdu): ProgID kayit defterinden
///     cozuluyor; yoksa "eDrawings bulunamadi" denir.
///   - COM sarmalayicisinda GetType().GetMethod HER ZAMAN null (olculdu,
///     v1'de iki tur yedirdi). Cagri Type.InvokeMember ile IDispatch
///     uzerinden yapiliyor - o yol __ComObject'te CALISIYOR.
/// </summary>
internal sealed class UcBoyutluGorunum : IDisposable
{
    private const string ProgId = "EModelView.EModelViewControl";

    private AxKonak? _konak;
    private string? _acikYol;

    /// <summary>Kurulamadiysa sebebi; kuruluysa null.</summary>
    internal string? KurulamamaSebebi { get; private set; }

    /// <summary>
    /// Denetimi kurar ve <paramref name="yuva"/>'ya yerlestirir. Bir kez
    /// kurulur; tutmazsa sebep saklanir ve bir daha DENENMEZ - her secimde
    /// ayni hatayla ugrasmak durum cubugunu spamlardi.
    /// </summary>
    internal bool Kur(Control yuva)
    {
        if (_konak is not null)
        {
            return true;
        }

        if (KurulamamaSebebi is not null)
        {
            return false;
        }

        try
        {
            string? clsid = ClsidBul();
            if (clsid is null)
            {
                KurulamamaSebebi = "eDrawings bu bilgisayarda bulunamadı (kurulu mu?)";
                return false;
            }

            var konak = new AxKonak(clsid) { Dock = DockStyle.Fill };

            // ActiveX asil burada dogar (tanitici yaratilirken); kurulu ama
            // bozuk bir eDrawings burada patlar ve sebebiyle yakalanir.
            yuva.Controls.Add(konak);
            konak.BringToFront();

            _konak = konak;
            return true;
        }
        catch (Exception hata)
        {
            KurulamamaSebebi = "eDrawings denetimi kurulamadı: " + hata.Message;
            return false;
        }
    }

    /// <summary>Kurulmus denetim; panel gosterip gizlerken kullanir.</summary>
    internal Control? Denetim => _konak;

    /// <summary>
    /// Dosyayi acar. eDrawings yuklemeyi kendi icinde surdurur ve kendi
    /// ilerlemesini cizer; biz beklemeyiz.
    /// </summary>
    internal bool Ac(string yol, out string? sebep)
    {
        sebep = null;
        if (_konak is null)
        {
            sebep = KurulamamaSebebi ?? "eDrawings kurulmadı";
            return false;
        }

        if (string.Equals(_acikYol, yol, StringComparison.OrdinalIgnoreCase))
        {
            return true;   // ayni dosya zaten acik; yeniden acmak titremeye yol acar
        }

        try
        {
            BelgeyiKapat();

            // Imza eDrawings API belgesinden: OpenDoc(ad, geciciMi, kaydetmeyiSor,
            // saltOkunur, komut). SALT OKUNUR aciyoruz - hicbir sey yazmayiz.
            // Imza bir surumde farkliysa istisna SEBEBIYLE ekrana gider ve
            // ikinci turda o rapora gore duzeltilir (CLAUDE.md 2).
            Cagir("OpenDoc", yol, false, false, true, string.Empty);
            _acikYol = yol;
            return true;
        }
        catch (Exception hata)
        {
            sebep = "eDrawings dosyayı açamadı: " + Ozu(hata);
            return false;
        }
    }

    /// <summary>
    /// Acik belgeyi birakir - dosya kilidi kalkmali diye her ISLEMDEN ONCE
    /// cagrilir. Tutmazsa sessizce gecilmez ama islem de durdurulmaz:
    /// asil islem kilide carparsa kendi sebebini zaten yazar.
    /// </summary>
    internal void BelgeyiKapat()
    {
        if (_konak is null || _acikYol is null)
        {
            return;
        }

        _acikYol = null;
        try
        {
            Cagir("CloseActiveDoc", string.Empty);
        }
        catch (Exception)
        {
            // Kapatma tutmadiysa yapabilecegimiz bir sey yok; islem kilide
            // carparsa DosyaIslemleri sebebini soyluyor (IslemRaporu.Sebebi).
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        BelgeyiKapat();
        _konak?.Dispose();
        _konak = null;
    }

    /// <summary>IDispatch uzerinden gec baglamali cagri (bkz. sinif belgesi).</summary>
    private void Cagir(string uye, params object[] degerler)
    {
        object ocx = _konak!.Ocx ?? throw new InvalidOperationException("denetim boş");
        ocx.GetType().InvokeMember(
            uye, BindingFlags.InvokeMethod, binder: null, target: ocx, args: degerler);
    }

    /// <summary>ProgID -> CLSID, kayit defterinden. Suslu parantezler atilir.</summary>
    private static string? ClsidBul()
    {
        using RegistryKey? anahtar = Registry.ClassesRoot.OpenSubKey(ProgId + "\\CLSID");
        return anahtar?.GetValue(null) is string deger && deger.Length > 2
            ? deger.Trim('{', '}')
            : null;
    }

    /// <summary>COM istisnasinin okunur ozu (HRESULT dahil - teshis icin).</summary>
    private static string Ozu(Exception hata)
        => hata is System.Runtime.InteropServices.COMException com
            ? $"{com.Message} (0x{com.HResult:X8})"
            : hata.Message;

    /// <summary>
    /// AxHost'un CLSID dizesiyle kurulan en kucuk turevi. Ayri sinif cunku
    /// AxHost'un kurucusu korumali.
    /// </summary>
    private sealed class AxKonak : AxHost
    {
        internal AxKonak(string clsid)
            : base(clsid)
        {
        }

        internal object? Ocx => GetOcx();
    }
}

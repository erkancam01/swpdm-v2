using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// SAG TIK MENUSU. Menuyu <see cref="AgacIslemleri.Tumu"/>'den URETIR ve
/// hicbir islemi ADIYLA bilmez (CLAUDE.md 1b) - islem eklendiginde/
/// kaldirildiginda bu dosya degismez.
///
/// Kisayollari da ayni listeden kurar; boylece menudeki yazi ile calisan tus
/// AYRISAMAZ.
///
/// IKI YERDE KULLANILIYOR: agac ve referans paneli (30.08.2026). Bu yuzden
/// tasiyici <see cref="Control"/>; bu sinifin agactan kullandigi tek sey
/// zaten <see cref="Control.ContextMenuStrip"/> ile
/// <see cref="Control.FindForm"/> idi. Panelin KENDI kararlari (satir nasil
/// secime cevrilir, ust bilgide ne yazar) burada DEGIL, kendi dosyasinda -
/// <c>ReferansMenusu</c> (CLAUDE.md 1b).
/// </summary>
internal sealed class AgacMenusu
{
    private readonly Control _tasiyici;
    private readonly ContextMenuStrip _menu = new();
    private readonly Dictionary<ToolStripMenuItem, IAgacIslemi> _islemler = [];

    private Func<SecimBaglami>? _secimKaynagi;
    private Func<SecimBaglami>? _sahipKaynagi;
    private Func<Keys, bool>? _kisayolGizle;
    private ToolStripMenuItem? _ustBilgi;
    private Func<string>? _ustBilgiMetni;
    private IIlerlemeYuzeyi? _ilerleme;
    private Action? _agaciKapat;
    private ReferansSurucusu? _referanslar;

    internal AgacMenusu(Control tasiyici)
    {
        _tasiyici = tasiyici;

        foreach (IAgacIslemi? islem in AgacIslemleri.Tumu)
        {
            if (islem is null)
            {
                _menu.Items.Add(new ToolStripSeparator());
                continue;
            }

            var oge = new ToolStripMenuItem(islem.Ad)
            {
                ShortcutKeys = islem.Kisayol,
                ShowShortcutKeys = islem.Kisayol != Keys.None,
            };

            // KAYDEDILMEYEN AMA YAZILAN TUS (bkz. IAgacIslemi.KisayolYazisi):
            // Enter gibi tuslar ShortcutKeys'e yazilamaz - orasi patlar.
            if (islem.KisayolYazisi is { Length: > 0 } kisayolYazisi)
            {
                oge.ShortcutKeyDisplayString = kisayolYazisi;
                oge.ShowShortcutKeys = true;
            }
            oge.Click += (_, _) => Calistir(islem);

            // GRI OGENIN SEBEBI DURUM CUBUGUNA DA DUSER. Once sebep yalnizca
            // IPUCUNDAYDI: kullanici gri ogenin ustune gelip bekliyor,
            // ipucu cikmazsa (Wine'da cikmiyor, dar ekranda kirpiliyor)
            // "neden calismiyor" sorusu cevapsiz kaliyordu. Ipucu KALDI;
            // bu, ikinci bir kanal (CLAUDE.md 3).
            oge.MouseEnter += (_, _) =>
            {
                if (!oge.Enabled && oge.ToolTipText is string neden && neden.Length > 0)
                {
                    Durum?.Invoke(this, neden);
                }
            };

            _islemler[oge] = islem;
            _menu.Items.Add(oge);
        }

        _menu.Opening += MenuAcilirken;
        _tasiyici.ContextMenuStrip = _menu;
    }

    /// <summary>Islem bittiginde agac tazelenir; yol verilirse orasi secilir.</summary>
    internal event EventHandler<string?>? Tazele;

    /// <summary>Durum cubuguna yazilacak cumle.</summary>
    internal event EventHandler<string>? Durum;

    /// <summary>Secimi nereden okuyacagini soyler.</summary>
    internal void SecimKaynagi(Func<SecimBaglami> kaynak) => _secimKaynagi = kaynak;

    /// <summary>Uzun islerin ilerlemeyi bildirecegi yuzey.</summary>
    internal void IlerlemeYuzeyi(IIlerlemeYuzeyi yuzey) => _ilerleme = yuzey;

    /// <summary>Butun dallari kapatan isi.</summary>
    internal void AgaciKapatan(Action is_) => _agaciKapat = is_;

    /// <summary>Referans indeksini islemlere ulastirir.</summary>
    internal void ReferansSurucusunu(ReferansSurucusu surucu) => _referanslar = surucu;

    /// <summary>
    /// <see cref="IslemHedefi.Sahip"/> diyen islemlerin okuyacagi
    /// AYRI secim. Kurulmazsa o islemler de olagan secimi okur - yani agacta
    /// hicbir sey degismez.
    /// </summary>
    internal void SahipSecimi(Func<SecimBaglami> kaynak) => _sahipKaynagi = kaynak;

    /// <summary>
    /// Menunun en ustune DEVRE DISI bir hedef satiri koyar: "islem KIME
    /// uygulanacak". Referans paneli icin sart - orada tiklanan satirin
    /// dosyasi agacta secili olandan BASKADIR ve gorunmeyen bir hedefe
    /// islem uygulatmak, yanlis dosyayi sildirmenin ta kendisidir
    /// (CLAUDE.md 3). Metin menu her acildiginda yeniden soruluyor.
    /// </summary>
    internal void UstBilgi(Func<string> metin)
    {
        _ustBilgiMetni = metin;
        _ustBilgi = new ToolStripMenuItem(metin()) { Enabled = false };
        _menu.Items.Insert(0, _ustBilgi);
        _menu.Items.Insert(1, new ToolStripSeparator());
    }

    /// <summary>
    /// Kisayollari KAYDETMEZ, yalnizca YAZAR.
    ///
    /// NEDEN: ayni kisayolu iki ayri ContextMenuStrip'e kaydetmenin ne
    /// yapacagi OLCULMEDI (cift tetiklenebilir). Tuslari zaten AnaForm
    /// odaga gore dagitiyor; menunun kaydetmesine gerek yok. Etiket
    /// kaliyor - kullanici tusu ogrenmeye devam ediyor.
    ///
    /// <paramref name="gizle"/> true dedigi tusun ETIKETI de yazilmaz:
    /// panelde Ctrl+C "yolu kopyala"dir, oysa KopyalaIslemi de Ctrl+C.
    /// Menude "Kopyala  Ctrl+C" yazmak YALAN olurdu (CLAUDE.md 3).
    /// </summary>
    internal void KisayollariYalnizYaz(Func<Keys, bool> gizle)
    {
        _kisayolGizle = gizle;

        foreach ((ToolStripMenuItem oge, IAgacIslemi islem) in _islemler)
        {
            oge.ShortcutKeys = Keys.None;

            bool yazilir = islem.Kisayol != Keys.None && !gizle(islem.Kisayol);
            oge.ShortcutKeyDisplayString = yazilir ? TusMetni(islem.Kisayol) : null;
            oge.ShowShortcutKeys = yazilir;
        }
    }

    /// <summary>Bu menunun kaydetmedigi, yalnizca yazdigi kisayollar var mi.</summary>
    private bool KisayolGizli(Keys tuslar) => _kisayolGizle?.Invoke(tuslar) == true;

    private static string TusMetni(Keys tuslar)
        => System.ComponentModel.TypeDescriptor.GetConverter(typeof(Keys))
               .ConvertToString(tuslar) ?? tuslar.ToString();

    /// <summary>
    /// Her islemden HEMEN ONCE kosacak kanca. Bugunku tek musterisi 3B
    /// onizleme: eDrawings actigi dosyayi kilitli tutabilir ve islem o
    /// kilide carpardi (CLAUDE.md 1a); belge islem baslamadan birakilir.
    /// Buraya baglaniyor cunku 14 islemin TAMAMI (menu + kisayol + arac
    /// dugmesi) bu siniftaki Calistir'dan gecer - tek nokta.
    /// </summary>
    internal void IslemOncesi(Action kanca) => _islemOncesi = kanca;

    private Action? _islemOncesi;

    /// <summary>Menu ogelerinin yazilarini ve durumlarini tazeler.</summary>
    internal void YazilariTazele() => YazilariKur(Secim());

    /// <summary>
    /// Bir tusa basildi; listedeki bir islemin kisayoluysa calistirir.
    /// Doner deger: islendi mi.
    ///
    /// <paramref name="suzgec"/> verilirse yalnizca ondan gecen islemler
    /// denenir. Referans paneli bunu kullanir: satira uygulanan isler
    /// (F2, Delete, Ctrl+X...) panelin menusunden, sahibe uygulananlar ve
    /// genel isler agacin menusunden gecsin diye. Menudeki yazi ile calisan
    /// tus boylece AYRISMAZ.
    /// </summary>
    internal bool TusaBasildi(Keys tuslar, Func<IAgacIslemi, bool>? suzgec = null)
    {
        foreach (IAgacIslemi? islem in AgacIslemleri.Tumu)
        {
            if (islem is null || islem.Kisayol == Keys.None || islem.Kisayol != tuslar)
            {
                continue;
            }

            // Etiketi bile yazilmayan bir kisayolu bu menu CALISTIRMAZ:
            // o tus baska bir sahibin (panelde Ctrl+C = yolu kopyala).
            if (KisayolGizli(tuslar) || (suzgec is not null && !suzgec(islem)))
            {
                continue;
            }

            Calistir(islem);
            return true;
        }

        return false;
    }

    private void MenuAcilirken(object? gonderen, System.ComponentModel.CancelEventArgs e)
        => YazilariKur(Secim());

    private void YazilariKur(SecimBaglami secim)
    {
        if (_ustBilgi is not null && _ustBilgiMetni is not null)
        {
            _ustBilgi.Text = _ustBilgiMetni();
        }

        foreach ((ToolStripMenuItem oge, IAgacIslemi islem) in _islemler)
        {
            SecimBaglami hedef = Secimi(islem, secim);

            // KILIT ONCE SORULUYOR: kilitli bir klasorde islemin kendi
            // "uygulanabilir mi"si EVET diyebilir; sebep o degil, kilit.
            bool olur = !Kilitler.Engel(islem, hedef, out string neden)
                && islem.Uygulanabilir(hedef, out neden);

            // Yazi her acilista islemden YENIDEN soruluyor: "Geri al" ve
            // "Yapistir" ne yapacaklarini adlarinda soyluyor.
            oge.Text = islem.Ad;

            // CLAUDE.md 3: uygulanamayan oge GIZLENMEZ. Gizlemek "boyle bir sey
            // yok" demektir; gri durup sebebini soylemek dogrudur.
            oge.Enabled = olur;
            oge.ToolTipText = olur ? string.Empty : neden;
        }
    }

    private SecimBaglami Secim()
        => _secimKaynagi?.Invoke() ?? new SecimBaglami([], null, AramaKipinde: false, Kok: null, CopKlasoru: null);

    /// <summary>
    /// Bu ISLEM hangi secim uzerinde calisir. Kararin kendisi islemin
    /// dosyasinda (<see cref="IAgacIslemi.Hedef"/>); burada
    /// yalnizca uygulaniyor.
    /// </summary>
    private SecimBaglami Secimi(IAgacIslemi islem, SecimBaglami olagan)
    {
        if (_sahipKaynagi is null)
        {
            return olagan;   // agacin menusu: sahip ile olagan zaten ayni
        }

        return islem.Hedef switch
        {
            IslemHedefi.Sahip => _sahipKaynagi(),

            // SATIR VARSA SATIR: "olagan" panelde tiklanan satirdir ve
            // cozulemeyen satirda BOS gelir - o zaman sahibe dusuluyor.
            IslemHedefi.SatirYoksaSahip when olagan.Ogeler.Count == 0 => _sahipKaynagi(),
            _ => olagan,
        };
    }

    private void Calistir(IAgacIslemi islem)
    {
        SecimBaglami secim = Secimi(islem, Secim());

        if (Kilitler.Engel(islem, secim, out string neden)
            || !islem.Uygulanabilir(secim, out neden))
        {
            // Kisayolla gelindiyse menu gorunmedi; sebep yine SOYLENIR.
            Durum?.Invoke(this, neden);
            return;
        }

        if (_ilerleme is null || _referanslar is null)
        {
            return;
        }

        _islemOncesi?.Invoke();

        islem.Uygula(new IslemBaglami(
            Sahip: _tasiyici.FindForm() ?? (IWin32Window)_tasiyici,
            Secim: secim,
            Tazele: yol => Tazele?.Invoke(this, yol),
            Bildir: cumle => Durum?.Invoke(this, cumle),
            Ilerleme: _ilerleme,
            AgaciKapat: _agaciKapat ?? (() => { }),
            Referanslar: _referanslar));
    }
}

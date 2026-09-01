using System;
using System.Windows.Forms;
using SwPdm.Arayuz.Gorunum;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz;

/// <summary>
/// ANA PENCERENIN REFERANS PANELI BAGLANTILARI - "onizlemenin altindaki
/// liste kime bagli" sorusunun cevabi.
///
/// NEDEN AYRI DOSYA: AnaForm 593 satira cikmisti ve boyut kapisinin siniri
/// 600 - yani bir sonraki dokunus kapiyi kiracakti. Kesme yeri satir
/// sayisina gore DEGIL KONUYA gore secildi (CLAUDE.md 1b): buradaki her sey
/// tek bir konuya bakiyor - panelin sag tiki, seridi, tek tiki, cift tiki
/// ve "satira git".
///
/// TASINDI, DEGISMEDI: bu dosyadaki hicbir satirin davranisi degismedi;
/// kurucudaki blok bir metoda alindi ve ayni yerden cagriliyor. Sira
/// KORUNDU - CLAUDE.md 6'nin kurucu tuzagi: cagri "_onizleme" atamasindan
/// SONRA duruyor, cunku IslemOncesi kancasi onu okuyor.
/// </summary>
internal sealed partial class AnaForm
{
    /// <summary>
    /// Panelin butun baglantilarini kurar ve sag tik menusunu DONDURUR.
    ///
    /// DONDURUYOR, ATAMIYOR - bilincli: "_referansMenusu" readonly ve
    /// readonly alan yalnizca kurucuda atanabilir. Alani metottan atamak
    /// icin readonly'yi kaldirmak, CLAUDE.md 6'daki "kurucu bitmeden alan
    /// null" tuzagina kapi acardi; deger kurucuya donuyor.
    /// </summary>
    private ReferansMenusu ReferansPaneliniKur()
    {
        // --- referans listesinde cift tik: dosyayi AC
        //
        // ERKAN, 31.08.2026: "önizleme alanındaki dosyaya çift tıklayınca
        // SOLIDWORKS'te açsın, dosya ağacında o dosyaya gitmesine gerek yok."
        // ONCEDEN buradan agaca GIDILIYORDU; artik aciyor.
        //
        // "GIT" KAYBOLMADI, ENTER'A GECTI (ReferansPaneliTuslari) - iki
        // yetenek de duruyor, yalnizca yer degistirdi. VERSIYONLAR sekmesi
        // zaten cift tikta aciyordu; panelin tamami artik tutarli.
        _referanslar.MouseDoubleClick += (_, e) =>
        {
            string? hedef = _referanslar.TiklananHedef(e.Location);

            // Versiyon satirinin ek cumlesi (salt-okunur arsiv) SurumBolumu'nde.
            _durum.Bilgi(
                _referansSeridi.SeciliBolum == ReferansBolumu.Surumler
                    ? SurumBolumu.Ac(this, hedef)
                    : DosyaAcici.YoluAc(this, hedef));
        };

        // --- referans panelinde SAG TIK (Erkan, 30.08.2026: "ordakilerde
        // parça"). Karari ReferansMenusu veriyor; burada yalnizca kuruluyor
        // ve agacinkiyle ayni baglantilar veriliyor. "Sahip secimi" =
        // agactaki secim: ElleBagla satira degil ona uygulanir.
        // _onizleme'den SONRA duruyor - IslemOncesi kancasi onu okuyor
        // (CLAUDE.md 6'nin kurucu sirasi tuzagi).
        // --- referans seridi: uc bolum (Erkan, 30.08.2026). Karari serit ve
        // surucu veriyor; burada yalnizca bagliyoruz.
        _referansSeridi.SecimDegisti += (_, bolum) =>
        {
            _referansSurucusu.Bolum = bolum;
            ReferanslariGoster(_referansYolu);
        };
        _referansSeridi.Durum += (_, cumle) => _durum.Bilgi(cumle);

        var menu = new ReferansMenusu(_referanslar);
        menu.Bagla(
            _ilerleme,
            _doldurucu.HepsiniKapat,
            _referansSurucusu,
            SecimBaglamiKur,
            () => _onizleme.BelgeyiBirak(),
            (_, yol) => AgaciTazele(yol),
            (_, cumle) => _durum.Bilgi(cumle),
            hedef => ReferansaGit(hedef));

        // --- referans satirina TEK TIK: o dosyanin onizlemesi (Erkan,
        // 29.08.2026: "13 kullananin resmine yerinden kipirdamadan bakayim").
        // Hedefsiz satirda (bolum basligi, gizlenen ozeti) onizleme DEGISMEZ -
        // pasif secim; aktif islemler (Enter, cift tik) zaten sebep yaziyor.
        // "_onizleme" atamasindan SONRA duruyor - CLAUDE.md 6'nin kurucu
        // sirasi tuzagi; derleyici de ayni sebepten uyardi.
        _referanslar.SecimDegisti += (_, _) =>
        {
            if (_referanslar.SeciliHedef is string hedef)
            {
                _onizleme.KomsuGoster(hedef);
            }
        };

        return menu;
    }

    /// <summary>
    /// Referans listesinden bir dosyaya gider.
    ///
    /// GIDILEMEZSE SEBEBI YAZILIR (CLAUDE.md 3). Sessizce hicbir sey
    /// yapmamak, kullaniciya cift tiklamanin bozuk oldugunu dusundurur;
    /// oysa sebep genelde belli: dosya taranan kokun disinda ya da
    /// referans cozulememis.
    /// </summary>
    private bool ReferansaGit(string? hedef)
    {
        if (hedef is null)
        {
            _durum.Bilgi("Bu satırda gidilecek bir dosya yok — referans çözülemedi.");
            return false;
        }

        // SUZGEC TUZAGI (Erkan, 31.08.2026: "montaj filtresi açıkken montajın
        // içindekiler bölümündeki parçaya çift tıkladığımda dosya bulunamadı
        // diyor"): dosya kokun ICINDE, yalnizca tur suzgeci onu gizlemis.
        // Eski hal iki kez yaniltiyordu - gidilemiyordu VE sebep olarak
        // "açık kökün dışında olabilir" yaziliyordu; YANLIS SEBEP, sebep
        // gostermemekten kotudur (CLAUDE.md 3).
        //
        // Iki ozelligin BILESIMI, o yuzden burada: suzgeci kaldirmak
        // seridin isi (SuzgecSeridi.Sifirla), gitmek doldurucunun.
        string? not = null;
        if (!_doldurucu.YoluAcVeSec(hedef))
        {
            if (!_suzgecler.Sifirla() || !_doldurucu.YoluAcVeSec(hedef))
            {
                _durum.Bilgi("Dosya ağaçta bulunamadı (açık kökün dışında olabilir): " + hedef);
                return false;
            }

            not = "Tür süzgeci kaldırıldı — aranan dosya süzgecin dışındaydı.";
        }

        SecimiGoster();
        _agac.Focus();

        // SecimiGoster'DEN SONRA: o da durum cubuguna yaziyor ve notu
        // ezerdi. Gidildi ama BIR SEY DEGISTI ise son soz bu olmali.
        if (not is not null)
        {
            _durum.Bilgi(not);
        }

        return true;
    }

    /// <summary>
    /// Referans panelini gosterir: serit sayilari + acik bolumun listesi.
    /// TEK KAPI - dort cagri yerinin hepsi buradan geciyor, yoksa biri
    /// serit sayilarini tazelemeyi unutur ve sayilar SESSIZCE bayatlar
    /// (CLAUDE.md 3: kullanici o sayiya bakip dosya siliyor).
    /// </summary>
    private void ReferanslariGoster(string? yol)
    {
        _referansYolu = yol;
        _referansSeridi.Sayilari(bolum => _referansSurucusu.Sayi(bolum, yol));
        _referansSurucusu.Doldur(_referanslar, yol);
    }
}

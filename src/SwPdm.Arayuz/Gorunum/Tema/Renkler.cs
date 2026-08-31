using System.Drawing;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Ekranin tum renkleri TEK yerde. CLAUDE.md 8: ayni mantigin ikinci kopyasi
/// yazilmaz - v1'de boyut bicimlendirmesi uc yerdeydi ve biri FARKLI sayi
/// gosteriyordu. Renk de ayni tuzagin adayi.
/// </summary>
internal static class Renkler
{
    // Ust baslik seridi (koyu)
    internal static readonly Color BaslikArkaPlan = Color.FromArgb(0x2F, 0x53, 0x75);
    internal static readonly Color BaslikYazi = Color.FromArgb(0xFF, 0xFF, 0xFF);
    internal static readonly Color BaslikDugmeVurgu = Color.FromArgb(0x1F, 0x6F, 0xEB);
    internal static readonly Color BaslikDugmeUzerinde = Color.FromArgb(0x3F, 0x66, 0x88);

    // Govde
    internal static readonly Color GovdeArkaPlan = Color.FromArgb(0xF0, 0xF0, 0xF0);
    internal static readonly Color AgacArkaPlan = Color.FromArgb(0xFF, 0xFF, 0xFF);
    internal static readonly Color AyracCizgi = Color.FromArgb(0xC8, 0xC8, 0xC8);

    // Agacta secim (Gezgin'in mavisi). Odak baska bir denetimdeyken vurgu
    // SOLAR ama KAYBOLMAZ: kullanici neyi sildigini gormeden onaylamamali.
    internal static readonly Color SecimArkaPlan = Color.FromArgb(0x33, 0x99, 0xFF);
    internal static readonly Color SecimArkaPlanPasif = Color.FromArgb(0xD5, 0xE5, 0xF7);
    internal static readonly Color SecimYazi = Color.FromArgb(0xFF, 0xFF, 0xFF);

    // Surukleme sirasinda uzerinde bulunulan klasor. Secim renginden AYRI:
    // "neyi sectim" ile "nereye birakiyorum" ayni anda gorunmeli.
    internal static readonly Color BirakmaHedefiZemin = Color.FromArgb(0xFF, 0xE1, 0x8A);

    // Suzgec dugmeleri
    internal static readonly Color SuzgecSeciliArkaPlan = Color.FromArgb(0xDC, 0xE6, 0xF2);
    internal static readonly Color SuzgecSeciliKenar = Color.FromArgb(0x8F, 0xA8, 0xC2);
    internal static readonly Color SuzgecYazi = Color.FromArgb(0x1F, 0x1F, 0x1F);

    // Onizleme alti ust bilgi yazisi
    internal static readonly Color UstBilgiYazi = Color.FromArgb(0x5B, 0x7C, 0x99);
    internal static readonly Color BolumBasligiYazi = Color.FromArgb(0x3D, 0x3D, 0x3D);
    internal static readonly Color OnizlemeArkaPlan = Color.FromArgb(0xFF, 0xFF, 0xFF);

    // Referans listesindeki YON ayrimi. Iki isaret birden: satirin rol
    // kelimesi ve rengi. (Ucuncusu bolum basligiydi; 30.08.2026'da yerini
    // ReferansSeridi aldi - baslik liste kayinca gorunmez oluyordu, serit
    // olmuyor.)
    internal static readonly Color ReferansAsagiYazi = Color.FromArgb(0x1B, 0x4F, 0x8A);
    internal static readonly Color ReferansYukariYazi = Color.FromArgb(0x1E, 0x63, 0x45);

    // Dosya BULUNDU ama dosyanin ICINDEKI yol baska yeri gosteriyor - yani
    // SOLIDWORKS onu acamaz. Kirmizi degil cunku dosya kayip degil; ayirt
    // edici bir uyari rengi.
    internal static readonly Color YolBayatYazi = Color.FromArgb(0xB0, 0x5A, 0x00);

    // Kutulardaki "olmaz" yazisi (gecersiz ad, cakisma, uzanti uyarisi).
    // TEK KOPYA (CLAUDE.md 8): once ad kutusunun icinde elle yazilmisti.
    internal static readonly Color UyariYazi = Color.FromArgb(0xB0, 0x30, 0x30);

    // Agacta kilit dosyasi durumu ("~$"). Hem yazi hem ZEMIN rengi var.
    //
    // ZEMIN NEDEN SART - OLCULDU (28.08.2026): yalnizca yazi rengiyle
    // isaretlemek Wine'da OLCULEMIYOR. ClearType alt-piksel cizim yaptigi
    // icin metnin hicbir pikseli saf renge esit cikmiyor; kirpmada
    // "#A64B00" ARANDI ve SIFIR bulundu, oysa yazi ekranda turuncuydu.
    // Dolu bir dikdortgen tam renk veriyor - referans bolum basliklarindaki
    // teknigin aynisi.
    internal static readonly Color AcikDosyaYazi = Color.FromArgb(0xA6, 0x4B, 0x00);
    internal static readonly Color AcikDosyaZemin = Color.FromArgb(0xFF, 0xE3, 0xC8);
    internal static readonly Color SahipsizKilitYazi = Color.FromArgb(0x8A, 0x1F, 0x5C);
    internal static readonly Color SahipsizKilitZemin = Color.FromArgb(0xF7, 0xDC, 0xEC);

    /// <summary>
    /// "Bu islem sunlari da etkiliyor" bloklarinin zemini (versiyona donus
    /// kutusu). Palette BASKA HICBIR YERDE yok - bilincli: cizilmis dolu bir
    /// dikdortgen TAM rengi verir, ClearType'la cizilen metin vermez
    /// (CLAUDE.md 11'de olculdu). Kapi bu sayede blogun cizilip cizilmedigini
    /// ekran goruntusunden koordinatsiz sayabiliyor.
    /// </summary>
    internal static readonly Color EtkiZemin = Color.FromArgb(0xFF, 0xF3, 0xD9);
}

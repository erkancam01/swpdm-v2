using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// "BU VERSIYONA DON" AKISI - VERSIYONLAR sekmesinde secili satira Enter.
///
/// IAgacIslemi DEGIL: islem listesindekiler agactaki secimle calisir; bunun
/// hedefi ise paneldeki VERSIYON SATIRIDIR (hangi dosya + hangi numara).
/// AnaForm yalnizca baglar; akisin karari burada (CLAUDE.md 1b).
///
/// GUVENLIK (CLAUDE.md 1a) cekirdekte: <see cref="Surumler.Don"/> once
/// bugunku hali otomatik arsivler, SOLIDWORKS'te acik dosyaya dokunmaz.
///
/// KUTUYA "KIMLER ETKILENIYOR" DA VERILIYOR (Erkan, 31.08.2026: "versiyon
/// secince o parcanin kullanildigi tum montajlar degissin"). Cevap: zaten
/// oyle oluyor - donus dosyanin KENDI yoluna yaziyor, montajlar ona yol
/// uzerinden bakiyor. Eksik olan GOSTERMEKTI; bu yorum bir sure kutunun
/// bunu soyledigini YAZIYORDU ama kutu soylemiyordu - bayat uyari, fazla
/// uyaridan tehlikeli (CLAUDE.md 6). Artik gercekten soyluyor.
///
/// Cevabin GUVENILIRLIGI de tasiniyor: taranmamis kokte liste bos doner ve
/// "hicbir montaj etkilenmiyor" demek yalan olurdu (CLAUDE.md 3). Karari
/// kutu veriyor; burasi yalnizca indekse soruyor.
/// </summary>
internal static class SurumeDonusu
{
    /// <summary>
    /// Akisi kosturur. Doner: tus kullanildi mi (satir versiyon degilse
    /// false doner ki Enter'in olagan anlami bozulmasin).
    /// </summary>
    internal static bool Calistir(
        IWin32Window sahip,
        SurumKaydi? kayit,
        SecimBaglami secim,
        ReferansIndeksi? indeks,
        Action belgeyiBirak,
        Action<string?> tazele,
        Action<string> bildir)
    {
        ArgumentNullException.ThrowIfNull(secim);
        ArgumentNullException.ThrowIfNull(belgeyiBirak);
        ArgumentNullException.ThrowIfNull(tazele);
        ArgumentNullException.ThrowIfNull(bildir);

        if (kayit is null)
        {
            return false;   // secili satir bir versiyon degil (aciklama satiri vb.)
        }

        if (secim.TekOge is not DosyaOgesi dosya || secim.Kok is not string kok)
        {
            bildir("Önce ağaçta tek bir dosya seçin.");
            return true;
        }

        // MONTAJDA VERSIYON SECME (Erkan'in ilk isteginin 3. maddesi):
        // versiyon artik o gunku COCUKLARI da tasiyor. Kutu, hangilerinin
        // geri yazilacagini SORUYOR - yalniz montaji yazmak "eski versiyona
        // dondum" sanisi yaratirdi, oysa parcalar bugunku halinde kalirdi
        // (CLAUDE.md 3). Karar kullanicinin; kutunun kendisi kendi dosyasinda.
        IReadOnlyList<string>? cocuklar = DonusSecimKutusu.Sor(
            sahip, dosya.Ad, kayit.No,
            Surumler.DonusListesi(kok, dosya.Yol, kayit.No),
            indeks?.Kullananlar(dosya.Yol));

        if (cocuklar is null)
        {
            bildir("Versiyona dönme iptal edildi.");
            return true;
        }

        // 3B onizleme belgeyi kilitli tutabilir (eDrawings); islemden once
        // birakilir - AgacMenusu.IslemOncesi'nin buradaki karsiligi.
        belgeyiBirak();

        IslemRaporu rapor = Surumler.Don(kok, dosya.Yol, kayit.No, cocuklar);

        if (!rapor.Oldu)
        {
            // CLAUDE.md 6: gorulmeden gecilemeyecek bir HATA - kutu mesru.
            MessageBox.Show(
                sahip, rapor.Sebebi,
                "Versiyona dönülemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            bildir("Versiyona dönülemedi — " + dosya.Ad);
            return true;
        }

        // Cekirdegin notlari (kayit-boyut farki, "zaten arsivli") GIZLENMEZ -
        // ozellikle kayit farki, kok sebep avinda Erkan'in gorecegi tek iz.
        bildir(
            $"{dosya.Ad} → v{kayit.No} içeriğine dönüldü."
            + (rapor.Sebep is { Length: > 0 } not_ ? not_ : " (önceki hâl arşivde)"));
        tazele(dosya.Yol);
        return true;
    }
}

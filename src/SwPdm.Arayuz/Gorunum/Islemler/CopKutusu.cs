using System;
using System.IO;
using Microsoft.VisualBasic.FileIO;
using SwPdm.Cekirdek;

namespace SwPdm.Arayuz.Gorunum;

/// <summary>
/// Windows Cop Kutusu'na gonderme. Cekirdekte DEGIL cunku bu Windows
/// kabugunun isi ve Linux'ta test edilemez; cekirdek platformdan bagimsiz
/// kalmali (CLAUDE.md 11).
///
/// OLCULEMEYEN: Wine'in cop kutusu gercek Windows kabugu degil. Burada
/// calismasi Windows'ta calisacagini KANITLAMAZ; tersi de dogru.
/// </summary>
internal static class CopKutusu
{
    /// <summary>Bir dosya ya da klasoru cop kutusuna gonderir.</summary>
    internal static IslemRaporu Gonder(string yol, bool klasorMu)
    {
        try
        {
            if (klasorMu)
            {
                FileSystem.DeleteDirectory(
                    yol,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin,
                    UICancelOption.ThrowException);
            }
            else
            {
                FileSystem.DeleteFile(
                    yol,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin,
                    UICancelOption.ThrowException);
            }

            return IslemRaporu.Basarili(yol);
        }
        catch (OperationCanceledException)
        {
            return new IslemRaporu(IslemSonucu.Bilinmeyen, null, "Kullanıcı vazgeçti.");
        }
        catch (Exception hata)
        {
            // CLAUDE.md 4 - OLCULDU: Windows bir klasoru UC ayri sebeple
            // sildirmiyor ve ex.Message bunlari AYIRT EDEMIYOR (yerellestirilmis
            // metin). Win32 kodu HResult'in dusuk 16 bitinde.
            int win32 = hata.HResult & 0xFFFF;
            (IslemSonucu sonuc, string aciklama) = win32 switch
            {
                5 => (IslemSonucu.Erisim, "İzin yok ya da salt-okunur."),
                32 => (IslemSonucu.Kilitli,
                    "Başka bir program açık tutuyor (SOLIDWORKS açıksa kapatın)."),
                145 => (IslemSonucu.Dolu,
                    "Klasörün içi boş değil. SOLIDWORKS'ün gizli \"~$\" kilit "
                    + "dosyaları Gezgin'de görünmez ama klasörü doldurur."),
                2 or 3 => (IslemSonucu.Bulunamadi, "Bulunamadı."),
                _ => (IslemSonucu.Bilinmeyen, string.Empty),
            };

            if (hata is PlatformNotSupportedException or NotSupportedException)
            {
                return new IslemRaporu(
                    IslemSonucu.Bilinmeyen, null,
                    "Bu sistemde çöp kutusu yok. " + hata.Message);
            }

            if (sonuc == IslemSonucu.Bilinmeyen && hata is UnauthorizedAccessException)
            {
                sonuc = IslemSonucu.Erisim;
                aciklama = "İzin yok ya da salt-okunur.";
            }

            if (sonuc == IslemSonucu.Bilinmeyen
                && hata is FileNotFoundException or DirectoryNotFoundException)
            {
                sonuc = IslemSonucu.Bulunamadi;
                aciklama = "Bulunamadı.";
            }

            string sebep = aciklama.Length > 0 ? aciklama + " " + hata.Message : hata.Message;
            return new IslemRaporu(sonuc, null, sebep);
        }
    }
}

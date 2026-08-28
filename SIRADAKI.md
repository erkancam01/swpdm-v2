# SIRADAKİ — yeni bir oturum buradan devam eder

> Bu dosya **CLAUDE.md değildir.** Oradakiler ölçülmüş, bayatlamayan
> gerçekler; burası **bugünün açık işleri**. Bir iş bitince bu dosyadan
> silinir; hepsi bitince dosya silinir.
>
> Son durum: dal `claude/v2-pdm-start-u058ey`, commit `0609632`.
> 222 test · dört kapı TEMİZ (çalıştırma kapısı 13 ölçüm).
> Erkan denedi ve *"her şey gayet düzgün"* dedi.

---

## Bitmiş olan (yeniden yapılmayacak)

Dosya yöneticisi tarafı **ve** PDM tarafı çalışıyor: referans indeksi
(kim kimi kullanıyor), beş rapor, SOLIDWORKS'süz önizleme, belge ve özel
özellikler, silmeden önce uyarı, referansa çift tıklayıp gitme, taşırken
bağımlıları da götürme.

SOLIDWORKS 2022 dosya biçimi **çözüldü ve CLAUDE.md §5'e yazıldı** —
adlandırılmış deflate akışları, nibble takaslı adlar, MFC dizeleri,
`Header2` = doğrudan referanslar. Bir daha araştırılmayacak.

---

## A — YAPILMAMIŞ TEK İŞ: referans onarımı (dosyaya yazma)

Bugün uygulama hiçbir SOLIDWORKS dosyasına **yazmıyor**. Taşıma/adlandırma
sonrası onarım gerekiyorsa söylüyor, yapmıyor.

Yazmadan önce ölçülmesi gereken üç bilinmeyen:

1. Dosya başındaki 4 bayt her dosyada farklı — **sağlama toplamı olabilir**.
   Öyleyse yeniden hesaplamadan yazılan dosyayı SOLIDWORKS reddeder.
2. Aynı yol **birden çok akışta** yazılı (`Header2` dışında
   `Config-0-ModelHeader`, `DisplayLists`, `Definition`, `VBLists`,
   `SwDocContentMgrInfo`). Yalnız birini değiştirmek eskisini geride bırakır.
3. Dize boyu değişince **sonraki akışların yeri kayar**; içeride mutlak
   konum tutan bir şey varsa kırılır.

**Bu ortamda doğrulanamaz — SOLIDWORKS yok.** Kapı şöyle kurulur:
kopya bir dosyada yol değiştirilir, Erkan'a gönderilir, **o** SOLIDWORKS'te
açıp doğrular. Açılırsa yol açılır; açılmazsa kâğıt üzerinde kalır ve
hiçbir şey kaybedilmez (§1a: KOPYALA → ONAR → DOĞRULA → SİL).

> Not: çoğu durumda yazmaya gerek yok. §5'te ölçüldü — SOLIDWORKS önce
> ebeveynin yanına bakıyor, o yüzden birlikte taşınan montaj+parça zaten
> çalışıyor (`BagimlilariEkle.cs` bunu kullanıyor).

## B — YAPILDI ama ÖLÇÜLEMEDİ (hepsi Erkan'ın makinesinde)

- **Ağ sürücüsünde ilk taramanın süresi.** Buradaki 0,1 sn yerel diskte
  7 dosya. Uygulama kendi hızını durum çubuğuna yazıyor; Erkan'dan o
  satır gelirse gerçek sayı öğrenilir.
- **100 MB+ montajda okuma maliyeti.** Beklenti ~66 KB ve boyuttan
  bağımsız; doğrulanmadı, iddia edilmiyor.
- **2022 dışındaki SOLIDWORKS sürümleri.** 2015 öncesi büyük ihtimalle
  OLE — `BilesikDosya.cs` o yüzden duruyor.
- **254 karakterden uzun yollar.** MFC kaçış biçimi görülmedi. Kod böyle
  bir yolu **atlıyor** ve sonucun eksik olduğunu **söylüyor**.

Bunlar kapanmadan A'ya girmek erken.

## C — Bekleyen fikir: özelliklere göre süzme

Özellikler (Malzeme, Ağırlık, Revizyon) şu an yalnızca **seçili dosyada**
görünüyor. İndekse alınırsa *"Malzeme = Pirinç olanlar"*, *"Revizyonu boş
olanlar"* gibi süzme ve arama olur. `SwBelgeBilgisi.Oku` hazır; iş
indekse bir alan eklemek ve süzgeci kurmak.

---

## Yeni oturum nasıl başlar

1. **CLAUDE.md okunur** — kurallar ve ölçülmüş gerçekler orada.
2. `bash araclar/kapilar.sh` — dördü de TEMİZ olmalı (Wine kapısı ~2 dk).
3. İş bitince `bash araclar/paket.sh` → zip, ve `SURUM-NOTU.txt`'de
   **neyin çalıştığı ve neyin bilerek çalışmadığı** yazılır (§1a, §3).

Erkan'ın kalıcı kararları: §1b (bir özellik tek yerde yaşar), §1c
(takılınca bir kez dene, geç, söyle — *bu kural bir turda bilerek
kaldırıldı, varsayılan yine geçerli*), ve dosyaya yazma konusunda
"önce ölç, doğrulamayı bana bırak".

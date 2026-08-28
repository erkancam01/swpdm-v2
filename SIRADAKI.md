# SIRADAKİ — yeni bir oturum buradan devam eder

> Bu dosya **CLAUDE.md değildir.** Oradakiler ölçülmüş, bayatlamayan
> gerçekler; burası **bugünün açık işleri**. Bir iş bitince bu dosyadan
> silinir; hepsi bitince dosya silinir.
>
> Son durum: dal `claude/v2-pdm-start-u058ey`, commit `e860c09`.
> `main` de aynı yerde (ileri sarma, 28.08.2026).
> 272 test · 267 geçti · 5 atlandı (Windows'a özel) · dört kapı TEMİZ
> (çalıştırma kapısı 13 ölçüm).
> Erkan denedi: *"her şey mükemmel."*

---

## Bitmiş olan (yeniden yapılmayacak)

Dosya yöneticisi tarafı **ve** PDM tarafı çalışıyor: referans indeksi
(kim kimi kullanıyor), altı rapor, SOLIDWORKS'süz önizleme, belge ve özel
özellikler, silmeden önce uyarı, referansa çift tıklayıp gitme, taşırken
bağımlıları da götürme.

SOLIDWORKS 2022 dosya biçimi **çözüldü ve CLAUDE.md §5'e yazıldı** —
adlandırılmış deflate akışları, nibble takaslı adlar, MFC dizeleri,
`Header2` = doğrudan referanslar. Bir daha araştırılmayacak.

**REFERANS ONARIMI BİTTİ** — dosyanın içine **yerinde yama** yazılıyor
(hiçbir bayt kaymıyor). Dört yol da çalışıyor ve dördü de Erkan'ın
makinesinde SOLIDWORKS'le doğrulandı:

- **ad değişimi** (F2) · **taşıma** (sürükle / Ctrl+X-V) → ebeveynler onarılır
- **toplu onarım** — "Bayat yollar" raporu + *"Bulunanları düzelt"* düğmesi
- **elle bağlama** (`Ctrl+Shift+L`) — hedefi kullanıcı seçer, seçim **kendi
  ağacımızdan** yapılır (Windows dosya kutusu kullanılmıyor)

Yazmadan önce sayılan üç bilinmeyenin **üçü de kapandı**: baştaki 4 bayt
engel değil · yol yazan **bütün** akışlar değiştiriliyor · uzunluk farkı
klasör kısmından karşılanıyor (kısa ad · dolgulu ad · göreli yol, üçü de
kabul edildi). Ayrıntı CLAUDE.md §5'te; burada tekrarlanmıyor.

---

## A — YAPILDI ama ÖLÇÜLEMEDİ (hepsi Erkan'ın makinesinde ölçülebilir)

- **Ağ sürücüsünde ilk taramanın süresi.** Buradaki 0,1 sn yerel diskte
  7 dosya. Uygulama kendi hızını durum çubuğuna yazıyor; Erkan'dan o
  satır gelirse gerçek sayı öğrenilir.
- **100 MB+ montajda okuma maliyeti.** Beklenti ~66 KB ve boyuttan
  bağımsız; doğrulanmadı, iddia edilmiyor.
- **2022 dışındaki SOLIDWORKS sürümleri.** 2015 öncesi büyük ihtimalle
  OLE — `BilesikDosya.cs` o yüzden duruyor.
- **254 karakterden uzun yollar.** MFC kaçış biçimi görülmedi. Kod böyle
  bir yolu **atlıyor** ve sonucun eksik olduğunu **söylüyor**.
- **Farklı ADLA elle bağlanan dosyayı SOLIDWORKS açıyor mu.** Aynı kod
  yolu ad değiştirmede doğrulandı; bu senaryo ayrıca denenmedi.

## B — KAPI BORCU (bilerek eksik bırakıldı, sebebiyle)

- **Elle bağlamanın kapıda kalıcı ölçümü YOK.** Wine'da uçtan uca bir kez
  ölçüldü (pencere açıldı · kendi ağacımızdan dosya seçildi · yama yazıldı),
  ama kapıya girmedi: çalıştırma kapısının bazı ölçümleri örnek klasördeki
  **mutlak satır numaralarına** bağlı ve yeni dosya eklemek onları kaydırıyor
  (11. ölçüm "satır 13 = Parça1.SLDPRT" diyor). Eklenecekse önce o ölçümler
  satır numarasından kurtarılmalı.
- **`BaslikSeridi.Bagli` hiç atanmıyor** → başlıkta her zaman
  *"SOLIDWORKS: kapalı"* yazıyor, gerçek durumdan bağımsız. §3 borcu,
  küçük iş: ya doğru gösterilir ya da yazı kaldırılır.

## C — Bekleyen fikirler (Erkan'a sunuldu, seçilmedi)

- **Özelliklere göre süzme/arama.** Özellikler (Malzeme, Ağırlık, Revizyon)
  şu an yalnızca **seçili dosyada** görünüyor. İndekse alınırsa
  *"Malzeme = Pirinç olanlar"* gibi süzme olur. `SwBelgeBilgisi.Oku` hazır;
  iş indekse bir alan eklemek ve süzgeci kurmak.
- **Pack and Go.** Bir montajı kullandıklarıyla birlikte başka klasöre
  kopyala, kopyadaki referanslar doğru olsun. Parçalar zaten var:
  `ReferansIndeksi.ZincirdekiEksikler` + `YolBaglama.Bagla`.
- **Toplu ad değiştirme.** Kural/şablonla çok dosya, referanslar korunarak.
  `ReferansOnarimi` hazır; iş kuralı ve önizlemeli onayı yazmak.

---

## Yeni oturum nasıl başlar

1. **CLAUDE.md okunur** — kurallar ve ölçülmüş gerçekler orada.
2. `bash araclar/kapilar.sh` — dördü de TEMİZ olmalı (Wine kapısı ~2 dk).
3. İş bitince `bash araclar/paket.sh` → zip, ve `SURUM-NOTU.txt`'de
   **neyin çalıştığı ve neyin bilerek çalışmadığı** yazılır (§1a, §3).

Erkan'ın kalıcı kararları: §1b (bir özellik tek yerde yaşar), §1c
(takılınca bir kez dene, geç, söyle), §1d (kısa yaz, madde madde), mesaj
kutusu kuralı (§6: yalnızca onay ve hata), ve dosyaya yazma konusunda
"önce ölç, doğrulamayı bana bırak".

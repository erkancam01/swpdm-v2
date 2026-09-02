# SW PDM — referans bozulma sınavı

**Amaç (Erkan):** *"benim için her ne olursa olsun dosya referansı bozulmaması çok önemli."*
**Yöntem:** 10 dosyalık gerçek takım (1 montaj · 2 teknik resim · 7 parça) üzerinde
adlandırma / taşıma / versiyon işlemleri **her üç dosya türünde** ve farklı
kombinasyonlarla denendi. Her adımdan sonra ölçüm aracı **`Ctrl+Shift+D`
raporları**: *Kırık referanslar* ve *Bayat yollar* sayıları.

**Başlangıç (referans noktası):** Kırık **3** · Bayat **0** · Yetim 0 ·
Teknik resmi olmayan 6 · Taşınmış 10 · Okunamayan 0
(o üç kırık gerçek: Toolbox vidası, `V-01.SLDPRT`, in-context montajın eski adı)

**Sınav sonunda:** Kırık **3** · Bayat **0** → *klasör başladığı hâle döndü,
kalıcı hiçbir referans kaybı yok.*

---

## SONUÇ TABLOSU

| # | Ne denendi | Sonuç |
|---|---|---|
| 1 | **Parça** adı değiştir (montaj + kendi teknik resmi kullanıyor) | ✅ Kırık 3, Bayat 0 |
| 2 | **Teknik resim** adı değiştir | ✅ — resim, adı değişmiş parçayı doğru gösteriyor |
| 3 | **Montaj** adı değiştir (3 kullanan: 2 in-context parça + teknik resim) | ✅ |
| 4 | Yeni klasör (`Ctrl+Shift+N`) | ✅ |
| 5 | **Parçayı** alt klasöre taşı (kullananlar dışarıda kaldı) | ✅ |
| 6 | **Klasör adını** değiştir (içindeki dosyayı dışarıdan kullananlar var) | ✅ |
| 7 | **Montaj + teknik resmi BİRLİKTE** alt klasöre taşı | ❌ **1 referans onarılamadı** |
| 8 | Raporlar → *Bayat yollar* → **"Bulunanları düzelt"** (8 bulgu) | ⚠️ **6 düzeldi, 2 düzeltilemedi** |
| 9 | `Ctrl+Z` ile zinciri geri alma | ❌ **iki adım yarım kaldı** |

---

# ⛔ ANA BULGU — referans onarımı, YENİ YOL ESKİSİNDEN UZUNSA yapılamıyor

**Hata metni (birebir):**

> `SINAV-MONTAJ.SLDASM` — `1-YMB.00900…GÖBEK YATAĞI.SLDPRT` onarılamadı:
> **'SwDocMgrTempStorage/ReplaceRef' akışı yuvaya sığmıyor (152 > 150 bayt).
> Dosya büyütülmedi; hiçbir şey değiştirilmedi.**

**Sebep — kodda bulundu:** `SwYazici.cs:296`

```csharp
byte[] sikisik = Sikistir(yenisi);
if (sikisik.Length > a.SikisikBoyut)
{
    // Buyutup zinciri kaydirmak yerine YAPMIYORUZ ve SOYLUYORUZ.
    return (0, 0, $"\"{a.Ad}\" akışı yuvaya sığmıyor …");
}
```

Yeni yol dosyanın **içine yazılıyor** ve akış **yerinde** güncelleniyor.
Sıkıştırılmış yeni içerik eski yuvadan **2 bayt bile** büyükse işlem
tümüyle reddediliyor. Sıkıştırma zaten en iyi seviyede
(`CompressionLevel.SmallestSize`) — kazanılacak bayt yok.

**Ne zaman tetiklenir:** yeni yol eskisinden uzun olduğunda —
- dosyayı **daha derin** bir klasöre taşımak,
- klasöre **daha uzun bir ad** vermek,
- dosyaya **daha uzun bir ad** vermek.

**İyi haber (bunu ayrıca ölçtüm):** uygulama **dosyayı bozmuyor**. "Hiçbir şey
değiştirilmedi" doğru; yarım yazılmış dosya yok, kopyala→yama→doğrula→değiştir
zinciri tutuyor ve sebebi ekranda söylüyor.

**Kötü haber:** işlem yine de **yarım kalıyor** — dosyalar taşınmış, bir
referans eski yolu göstermeye devam ediyor. Kullanıcı `Tamam` deyip geçerse
montaj o parçayı yazılı yoldan bulamaz.

## Çözüm için iki yol (kararı sen vermelisin)

**A — Akışı büyütebilmek (asıl çözüm, riskli).**
Dosyanın içindeki akış zincirini kaydırıp uzunluk tablosunu yeniden yazmak
gerekiyor. Biçim tersine mühendislikle çözülmüş; yanlış yapılırsa **dosya
açılmaz hale gelir**. Bunu senin SOLIDWORKS'ünde adım adım doğrulamadan
yazmam doğru olmaz — bir turluk iş ve her adımı senin açıp denemen gerekir.

**B — Yapamayacağını ÖNCEDEN söylemek (ucuz, güvenli).**
Taşıma/adlandırma başlamadan önce "bu onarım sığmayacak" denemesi yapılır ve
kutuda **işlemden önce** yazar: *"Bu taşıma sonrası 2 referans onarılamayacak —
devam edilsin mi?"*. Böylece dosyalar hiç taşınmaz ya da kullanıcı bilerek
devam eder. Yarım kalmış durum ortadan kalkar.

**Ara çözüm (bugün geçerli):** kısa adlar ve sığ klasörler bu hatayı hiç
tetiklemiyor — 1–6 numaralı sınavların hepsi temiz geçti.

---

# ⚠️ İKİNCİ BULGU — `Ctrl+Z` zinciri klasör adı değişince kopuyor

Sınav 9'da art arda geri alırken:

1. `"SINAV-KLASOR-2 — 'SINAV-KLASOR-2' zaten bu klasörde."` → klasör adı geri
   alınamadı.
2. Bir sonraki adım: `"SINAV-PARCA.SLDPRT — Kaynak bulunamadı: …\SINAV-KLASOR\SINAV-PARCA.SLDPRT"`
   → yığın hâlâ **eski klasör adını** tutuyor.

Yani bir klasör adı değişikliği geri alınamayınca, ondan **sonraki** geri alma
adımları da eski yolu aradığı için düşüyor. Geri alma yığınındaki yollar
klasör adı değişikliğini takip etmiyor.

Dosya kaybı **olmadı** (uygulama "yarım kaldı" deyip durdu, sessizce yanlış
yere dokunmadı), ama kullanıcı elle toparlamak zorunda kaldı — ben de öyle yaptım.

---

# ✅ DOĞRU ÇALIŞTIĞI ÖLÇÜLENLER

- **Adlandırma onarımı üç türde de tam:** parça, montaj ve teknik resim adı
  değişince onları kullanan bütün belgeler yeni adı gösteriyor. Onay kutusu
  kullananları doğru sayıyor ve adlarını yazıyor.
- **Klasör adı değişince** içindeki dosyaları **dışarıdan** kullananlar onarılıyor.
- **Tek dosya taşıma** (kes-yapıştır ve sürükle-bırak) referansı koruyor.
- **Çoklu taşımada** onay kutusu *"Kullandığı 7 dosya taşınmıyor; referansları
  onarılacak"* diyor — doğru ve yerinde uyarı.
- **Raporlar** durumu doğru gösteriyor: taşımadan sonra 8 bayat yol çıktı,
  "Bulunanları düzelt" 6'sını düzeltti, düzeltemediği 2'sinin **sebebini yazdı**.
- **Hiçbir aşamada dosya bozulmadı**; sınav sonunda klasör başlangıç durumuna
  döndü (Kırık 3, Bayat 0).

---

# YENİ ÖZELLİK — açılışta otomatik tarama ✅

İsteğin üzerine eklendi: kök açılır açılmaz referans taraması **kendiliğinden**
başlıyor. Ölçüm: uygulama açıldığında durum çubuğunda
*"Referans taraması — 10 dosya · 0 okundu · 10 değişmemiş · 0,0 sn"*.

- Tarama **arka planda** koşuyor; ilerleme çubuğu, **İptal** düğmesi ve `Esc` var —
  ağ sürücüsünde uzun sürerse uygulama kilitlenmiyor.
- İkinci ve sonraki taramalar **artımlı**: boyutu/tarihi değişmeyen dosya bir
  daha açılmıyor.
- Menü, kısayol ve otomatik başlatma **aynı koddan** geçiyor — ikinci kopya yok.

Değişen: `AnaForm.KokuAc` (bir satır) + `ReferansTaramaIslemi` başlık yorumu.

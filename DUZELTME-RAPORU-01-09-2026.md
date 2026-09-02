# SW PDM — düzeltme turu raporu

**Tarih:** 01.09.2026 · **Ortam:** Windows, SOLIDWORKS 2022 + eDrawings
**Yöntem:** her düzeltme derlenip **uygulamada elle ölçüldü**; hiçbir madde
"yazdım, herhalde çalışır" değil.

**Kapılar:** derleme `-warnaserror` TEMİZ · testler **392 başarılı, 0 başarısız, 1 atlandı**

---

# DÜZELTİLDİ

## 1. Tam ekranda KIRIK ve VERSİYONLAR sekmeleri artık tıklanıyor  ✔

**Sebep:** `ReferansSeridi.Sayilari()` düğme yazılarını **sonradan** uzatıyor
("KIRIK" → "KIRIK  2 dosya"). Düğmeler `AutoSize`; yazı hemen yeni haliyle
çiziliyor ama panelin yerleşimi yenilenmediği için **tıklama dikdörtgenleri
eski (kısa) yazıya göre kalıyordu**. Bu yüzden "KIRIK" yazısına tıklayınca
yanındaki sekme seçiliyor, VERSİYONLAR'a hiç ulaşılamıyordu. Dar pencerede
şerit iki satıra sardığı için tesadüfen doğru çalışıyordu.

**Düzeltme:** `Sayilari()` sonunda `PerformLayout()`; ayrıca şeridin kendi
`AutoSize`'ı kapatıldı ve yüksekliği yerleşimden **sonra** ölçülüyor (çalışan
kardeşi `SuzgecSeridi`'nde de AutoSize kapalı).

**Ölçüm:** tam ekranda "KIRIK 2 dosya" → KIRIK açıldı; "VERSİYONLAR yok" →
VERSİYONLAR açıldı.

> Ara denemede düğmelere elle ölçü vermeyi denedim; **daha kötü oldu** (hiçbir
> sekme tıklanmadı) ve geri aldım. CLAUDE.md §1a.

## 2. "Kırık referans" yanlış alarmı bitti — 9 bulgu → 3  ✔

**Sebep:** `SwReferans.Oku`, belgenin kendi yolunu eliyordu ama **diskteki
bugünkü adla** karşılaştırarak. Dosya dışarıda yeniden adlandırılmışsa
(bu dosyalara "1-" öneki eklenmiş) içeride yazan eski ad elenemiyor ve bir
çocuk referansı sanılıyordu. `KendiYolunuBul` de aynı ad şartını taşıdığı için
"kendi yolu bilinmiyor" diyordu.

**Düzeltme:** `KendiYolunuBul` artık adı değişmiş dosyayı da buluyor (önce
bugünkü adla tam eşleşme, yoksa **aynı uzantıyı** taşıyan son yol — uzantı
şartı, bir montajın parçasının yanlışlıkla "kendi yolu" sayılmasını engelliyor).
`Oku` ise **iki adı birden** eliyor: bugünkü ad + dosyanın içinde yazan eski ad.

**Ölçüm (aynı 10 dosyalık takım, yeniden tarama):**

| | önce | sonra |
|---|---|---|
| Kırık referanslar | **9** | **3** |
| Taşınmış dosyalar | 4 | **10** |
| GÖBEK YATAĞI parçası | KIRIK 1 | **KIRIK yok** |

Kalan 3 bulgu gerçek: Toolbox vidası (kök dışında), `V-01.SLDPRT` (yok) ve
in-context montajın eski adı. **Bilgi kaybolmadı, doğru rapora taşındı:**
adı/yeri değişen 10 dosya artık "Taşınmış dosyalar"da — korkutmadan.

## 3. Açılışta artık EN SON kök açılıyor  ✔

**Sebep:** `Ayarlar.Oku` her `kok=` satırı için `KokEkle` çağırıyordu, o da
listenin **başına** ekliyor → dosya en yeni önce yazılıyor, okurken **ters**
çevriliyor ve `SonKok` en **eski** kök oluyordu. "Son açılanlar" menüsü
`Insert(0)` ile bir kez daha ters çevirdiği için gözle doğru görünüyordu —
iki hata birbirini örtüyordu.

**Düzeltme:** okurken sona ekleyen ayrı bir yol (`KokSonaEkle`) + menü
listeyi tersten geziyor. **Ölçüm:** uygulama test klasörüyle açıldı, menü sırası da doğru.

## 4. Bölücü artık açılışta sıkışmıyor  ✔

**Sebep:** kırpma vardı ama `Panel2MinSize` WinForms varsayılanı (25 px);
kayıtlı değer geniş pencerede tam da oraya kırpılıyor, referans paneline
25 piksel kalıyordu.

**Düzeltme:** panellere gerçek en küçük ölçüler — ama **kurucuda değil**:
orada vermek `InvalidOperationException` atıp uygulamayı hiç açtırmıyor
(bunu bu turda bizzat yaşadım ve düzelttim). Ölçüler `Yerlesim.Uygula`'da,
pencere boyutu konduktan **sonra** ve güvenli sırayla veriliyor.

**Ölçüm:** hem tam ekranda hem küçük pencerede referans paneli okunabilir açılıyor.

## 5. Windows'ta 42 test başarısızlığı → 0  ✔ (bonus)

`SurumlerTestleri.Dispose()` geçici klasörü silerken
`UnauthorizedAccessException` alıyordu: arşiv kopyaları **bilerek salt-okunur**
yazılıyor, Windows salt-okunur dosyayı sildirmiyor. 42 testin **kendi
doğrulamaları geçiyordu**, yalnız temizlik düşüyordu — ve Linux'ta aynı silme
çalıştığı için kapı orada yeşil, Windows'ta kırmızı görünüyordu.

**Düzeltme:** temizlikten önce salt-okunur bayrakları kaldırılıyor.
**Ölçüm:** 393 testin 392'si geçti, 0 başarısız, 1 atlandı (Linux'a özel).

## 6. Kısayol kanalı tek yere alındı (bonus)

Bütün kısayollar artık `Form.KeyDown` yerine **`ProcessCmdKey`**'den geçiyor —
bu dosyanın kendi içinde zaten ölçülmüş olan kanal (CLAUDE.md §6: *"ToolStrip
Escape'i yutuyor, çalışan tek kanca ProcessCmdKey"*). Odak şartları aynen
duruyor, yani arama kutusuna yazarken `Delete` hâlâ dosya silmiyor.

---

# DÜZELMEDİ — ve neden

## `Ctrl+Shift+E` ve `Ctrl+Shift+U` (A2/A3)

Kanalı `ProcessCmdKey`'e taşımak **sonucu değiştirmedi**: iki tuş hâlâ hiçbir
şey yapmıyor, aynı satırların hemen yanındaki `Ctrl+Shift+F` çalışıyor.

**Bunu senin elinle denemen gerekiyor.** Sebebi: uzaktan kumandanın klavyesi
`Escape`'i de hiç iletmiyor (rapor penceresinde, ad kutusunda, aramada — üçünde
de etkisiz). Yani "uygulama tuşu almıyor" ile "benim klavyem o akoru
göndermiyor" arasını buradan ayıramıyorum. Kodda üç kez okudum, yol doğru.

**Denemesi 10 saniye:** uygulamada bir parça seç → `Ctrl+Shift+U`. Kutu
açılıyorsa sorun bendeydi; açılmıyorsa bir sonraki turda peşine düşerim.

## Sıraya kalanlar

3B önizleme açıkken silinen dosyanın eDrawings hata kutusu · aynı klasöre
yapıştırmada çakışma kutusunun çıkmaması · başarılı işlemlerden sonra durum
çubuğunun sessiz kalması · `Ctrl+Z` sonrası solda bayat ad · taşıma onayının
onarımdan söz etmemesi · versiyon önizleme başlığı.

---

# DEĞİŞEN DOSYALAR

```
src/SwPdm.Cekirdek/SwDosyasi/SwReferans.cs        (2)
src/SwPdm.Cekirdek/Ortak/Ayarlar.cs               (3)
src/SwPdm.Arayuz/Gorunum/Serit/KokSecici.cs       (3)
src/SwPdm.Arayuz/Gorunum/Tema/Yerlesim.cs         (4)
src/SwPdm.Arayuz/Gorunum/Referans/ReferansSeridi.cs (1)
src/SwPdm.Arayuz/AnaForm.Kisayollar.cs            (6)
src/SwPdm.Arayuz/AnaForm.cs                       (6, tek satır)
src/SwPdm.Arayuz/AnaForm.Tasarim.cs               (4, yorum)
testler/SwPdm.Cekirdek.Testler/SurumlerTestleri.cs (5)
```

Klasörde ayrıca ben ekledim: `DERLE.bat` (derle + test + çalıştır),
`TEST-LOG.bat` (testleri `test-log.txt`'ye yazar), `CALISTIR.bat`.
Depo bir git çalışma kopyası değil (zip'ten açılmış), o yüzden commit atamadım —
değişiklikler doğrudan klasörde.

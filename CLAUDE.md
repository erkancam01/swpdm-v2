# CLAUDE.md — SW PDM v2

Bu depo **SW PDM v2**: SOLIDWORKS dosyalarını taşırken/adlandırırken montaj ve
teknik resim referanslarını koruyan masaüstü uygulaması.

> **BU DOSYA v2'NİN YAPISINI ANLATMIYOR — daha yazılmadı.** İçinde yalnızca
> v1'de (`erkancam01/swpdm`) **ölçülmüş** gerçekler ve bedeli ödenmiş çalışma
> kuralları var. Hiçbiri bu depodaki bir dosyaya, sınıfa ya da projeye işaret
> etmiyor; o yüzden hiçbiri bayatlayamaz.
>
> Yapıya dair maddeler (katmanlar, kapılar, paketleme) kapsam belirlendikçe
> **ölçülerek** eklenecek — tahminle değil.
>
> **27.08.2026 güncellemesi:** ilk ölçülmüş yapı maddeleri geldi (§11). Artık
> §11 depodaki gerçek dosyalara işaret ediyor; oradaki adlar değişirse §11 de
> değişmeli. §1–§10 hâlâ mimariden bağımsız ve bayatlayamaz.

---

## 1. Altın kural

**Çalışan hiçbir şeyi bozma.** Bu bir CAD dosya yöneticisi: hatalı bir
değişiklik gerçek montaj ve teknik resimlerin referanslarını kırar, en kötü
ihtimalle dosya kaybettirir.

Dal **`main`**. Commit mesajları Türkçe ve **neden** değiştiğini yazar.
`--force`, `rebase`, geçmişi yeniden yazma **YOK**. Depo private.

**Her adımın sonunda çalıştırılabilir bir zip verilir** (`araclar/paket.sh`).
Erkan uygulamayı kendi makinesinde deniyor; Wine'ın ölçemediği her şeyi
(yazı tipi, tema, ağ sürücüsü hızı, gerçek SOLIDWORKS simgeleri) yalnızca o
görebiliyor. Pakete giren `SURUM-NOTU.txt` **neyin çalıştığını ve neyin
bilerek çalışmadığını** yazar — §3'ün gereği: çalışmayan bir şeyi
söylememek, kullanıcının onu denemesine ve bozuk sanmasına yol açar.

---

## 2. ÖLÇ, TAHMİN ETME — en pahalı ders

v1'de doğrulanmamış üç SOLIDWORKS API varsayımı **üç tur** yedirdi.

- **Yeni bir API'ye dayanmadan önce bir "Kapı" yaz:** geçici klasörde kendi
  test dosyasını üretir, çağrıyı yapar, sonucu **doğrular**, temizler,
  raporlar. Kapı geçmeden asıl özellik yazılmaz.
- **İmzadan emin değilsen yansımayla çağır**, tutan varyantı raporla.
- **Dönüş değerine GÜVENME.** `IDrawingDoc.ReplaceViewModel` `true` döndü ve
  **hiçbir şey yapmadı**. Başarı bir iddia değil **ölçüm**: işlemden sonra
  sonucu diskten/bellekten yeniden oku ve karşılaştır.
- **Belirtiyi tek sebebe bağlama.** "Klasör silinmiyor" dört tur yedi çünkü
  aynı belirtinin **iki ayrı sebebi** vardı. İki ucuz hipotezden birini
  seçmek yerine ikisini birden kapat.
- **Sayıyı belgeden okuma, çalıştır.** v1'de bu dosyanın kendisi iki kez
  yalan söyledi (test sayısı, dosya satır sayısı).

---

## 3. Dürüstlük — kullanıcı buna bakıp dosya SİLİYOR

- **Boş liste "yok" demek DEĞİLDİR.** Referans indeksi yalnızca taranmış
  kökleri bilir; taranmamış klasörde sorgu boş döner. Bunu "bu parçayı kimse
  kullanmıyor" diye göstermek **sağlam dosya sildirir**. Tarama yoksa hiçbir
  sayı ve hiçbir liste gösterilmez, sebebi yazılır.
- **Kısmi başarısızlıkta eski dosyayı KORU:** `KOPYALA → ONAR → SİL`. Bir
  onarım tutmazsa kaynak silinmez. `File.Move` bu yüzden kullanılmıyor.
- **İndekse yalan yazma.** v1'de başarısız bir onarımdan sonra indeks
  "düzeldi" diye güncellendi; teknik resim ilişkisi sessizce kayboldu.
- **Hata sebebini EKRANDA göster**, yalnızca günlüğe değil.
- **Uydurma ya da donmuş ilerleme gösterme.** Sayılabilir ilerleme yoksa
  yüzde uydurma; animasyon işletilemiyorsa hiç gösterme.
- **Sessiz başarı ve sessiz askıda kalma YASAK.** Her istek bir yanıt alır,
  her terminal hâl sebebini yazar. Bir işlemin yapılıp yapılmadığını
  **bilmiyorsak** "yapılmadı" demek yalandır — kullanıcı ikinci kez yapar.

---

## 4. ÖLÇÜLMÜŞ GERÇEKLER — .NET ve Windows

### `System.IO.Path`'in yol parçalama üyeleri Linux'ta Windows yolunu YANLIŞ parçalıyor

```
Path.GetExtension(@"C:\Proje 2.0\parca")  ->  ".0\parca"     (dogrusu "")
Path.GetFileName(@"C:\a\b.SLDPRT")        ->  yolun TAMAMI    (dogrusu "b.SLDPRT")
Path.GetDirectoryName(@"C:\a\b.SLDPRT")   ->  ""              (dogrusu "C:\a")
```

Linux'ta `\` ayırıcı **sayılmıyor**. Testler Linux'ta koşuyorsa test **yanlış
sonucu doğrular** ve hata kullanıcının makinesine kalır. Windows'a bağımlı
olmayan her katmanda bu dört üye kullanılmaz; kendi yol yardımcın olur.

> Kendi yardımcını yazarken **sürücü kökü** tuzağı:
> `Path.GetDirectoryName(@"C:\a.SLDPRT")` → `"C:\"`. `"C:"` döndüren bir
> yardımcı, `Path.Combine("C:", "x")` ile sürücüye **göreli** `"C:x"` üretir.

### Diğerleri

- **`Path.GetInvalidFileNameChars()` Linux'ta yalnızca `/` ve `\0` döndürür.**
  Windows'ta geçersiz bir adı testler kabul eder. Geçersiz karakter listeleri
  **elle** yazılır.
- **Windows bir klasörü ÜÇ ayrı sebeple sildirmiyor** ve üçünün çözümü farklı:
  `145 ERROR_DIR_NOT_EMPTY` (içinde bir şey var — **gizli** dosyalar dahil) ·
  `32 ERROR_SHARING_VIOLATION` (açık tutamak) · `5 ERROR_ACCESS_DENIED`
  (salt-okunur ya da izin). **`ex.Message` bunları ayırt edemiyor** —
  yerelleştirilmiş metin. Win32 kodunu oku.
- **`pkill -f` / `pgrep -f` KOMUT SATIRININ TAMAMINA bakıyor**, yalnızca süreç
  adına değil. Bir betikteki `pkill -f "SwPdm.exe"`, komut satırında o metin
  geçtiği için **çağıran kabuğu öldürdü** — metin uzun bir commit mesajının
  içindeydi. Belirti tamamen sessiz: komut `exit 144` ile düşüyor, hiçbir hata
  yazmıyor. `pgrep -x` (yalnızca süreç adı) eşlemiyor; ölçüldü. Eski süreçler
  **ada** göre aranır, gerekiyorsa `/proc/<pid>/cmdline` ayrıca süzülür.
- **Kabuk dosya iletişim kutuları sürecin çalışma klasörünü kaydırıyor** ve o
  klasör bir daha silinemiyor. `RestoreDirectory = true` + kutu kapandıktan
  sonra çalışma klasörünü sabitle.
- **Kabuk önizleme sağlayıcıları STA ister.** `ThreadPool` (MTA) içinden
  çağırınca `E_FAIL` (0x80004005). → Önizleme yüklemek için **kendi STA iş
  parçacığını** kur; `Task.Run` ile olmuyor.
- **`Image.FromStream` akışı SAHİPLENİYOR.** Çözümlemeyi tembel yapıyor; akış
  `using` ile kapanınca resim **çizilmiyor** — ama `null` da olmuyor, yani
  "önizleme yok" dalına da girilmiyor. Belirti tamamen sessiz: **boş kutu,
  sebep yok.** Bağımsız bir kopya alınmalı (`new Bitmap(resim)`).
- **`Image.FromHbitmap` ALFA KANALINI yok sayıyor.** Kabuk küçük resimleri
  32 bit **önçarpımlı alfa** ile döndürüyor; `FromHbitmap` ile alınanda saydam
  kısımlar çöpe dönüyor (Wine'da gri gradyan, Windows'ta genelde siyah köşe).
  `GetObject` ile `bmBits` okunup `Format32bppPArgb` olarak kopyalanmalı.
- **Kabuk `S_OK` dönüp TAMAMEN SAYDAM bir bit eşlem verebiliyor** (ölçüldü).
  Bunu "önizleme var" saymak iki kez yanlış: boş kutu önizleme diye gösterilir
  **ve** dosyanın içindeki gömülü önizlemeye hiç geçilmez. Gelen resmin en az
  bir saydam olmayan pikseli var mı, bakılmalı.
- **`.bat` iki ayrı şekilde SESSİZCE ölüyor** — ikisinde de görülen aynı:
  *pencere açılıyor, hiçbir şey yazmadan kapanıyor.* Hata yok, günlük yok.
  1. **CRLF şart.** LF'e düşen bir `.bat`'ı `cmd.exe` yarıda kesiyor.
     `.gitattributes`'ta `*.bat -text`; `.bat` üreten/kopyalayan her betik de
     CRLF'i korumalı (Python'da `newline=""`).
  2. **Blok içinde kaçışsız parantez.** `if ... ( … )` içindeki `(`/`)` —
     **tırnak içinde bile** — cmd'nin ayrıştırıcısını yanıltıyor
     (`SOLIDWORKS (2)` yazmak bu tuzağa düştü). Doğrusu `^(` / `^)`, ya da
     blok yerine `goto`.
  3. `.bat` çıktısı bir dosyaya da yazılmalı — pencere kapandığında
     kullanıcının elinde hiçbir kanıt kalmıyor.
- **Verbatim string (`@"..."`) içine ASLA çift tırnak yazma.** Oradaki bir `"`
  string'i **o noktada bitirir**; kaçış `\"` değil `""`. Bir SQL yorumuna
  tırnaklı cümle yazmak **230 derleme hatası** üretti ve iki statik denetim de
  "TEMİZ" dedi — stray tırnaklar çift sayıda olunca parantez dengesi bozulmuyor.
  **DDL yorumlarında tırnak kullanma.**

---

## 5. ÖLÇÜLMÜŞ GERÇEKLER — SOLIDWORKS ve COM

### CS0104 — takma ad ŞART

`SolidWorks.Interop.sldworks` kendi `Environment`, `View`, `Timer`,
`Application`, `Color`, `Point`, `Component`, `Attribute`, `Feature`
tiplerini tanımlıyor. Çıplak kullanmak derlemeyi kırıyor.

```csharp
using Environment = System.Environment;
```

Tek tek `System.` öneki eklemek **YETMEZ**: bugünü düzeltir, o dosyaya
yazılacak bir sonraki kullanım hatayı geri getirir. v1'de `Environment` tam bu
yüzden **aylarca** her pakette elle düzeltildi.

→ Sonucu bir tasarım kuralı: **interop'a dokunan dosya sayısını az tut.** v1'de
tüm interop dört dosyadaydı ve en büyük dosya bu riskin tamamen dışındaydı.

### `GetType()` — iki tuzak, biri SESSİZ

1. **Derleme kırar:** interop arayüzlerinin KENDİ `GetType()` üyesi var.
   `IModelDoc2.GetType()` belge türünü **`int`** döndürüyor ve
   `object.GetType()`'ı gölgeliyor.
2. **Derlenir ama HER ZAMAN yanlış çalışır:** COM sarmalayıcısında `GetType()`
   **`System.__ComObject`** döndürüyor; o tipte arayüzün hiçbir üyesi yok, yani
   `.GetType().GetMethod("X")` **her zaman `null`**. Kod derlenir, çalışır ve
   *"üye bulunamadı"* raporlar — hiç denemeden. v1'de **iki tur üst üste** oldu.

```csharp
belge.GetType().GetMethod("IsOpenedReadOnly");     // YANLIS: her zaman null
typeof(ModelDoc2).GetMethod("IsOpenedReadOnly");   // DOGRU
```

- **`Type.GetMethod` bir ARAYÜZ tipinde miras alınan üyeleri DÖNDÜRMÜYOR**
  (sınıflardan farklı) — tip + `GetInterfaces()` birlikte taranmalı.
- **Arayüz adını metin yazma, `typeof(T)` geçir.** Var olmayan bir tip adı
  yazmak v1'de iki kez derlemeyi kırdı.

### SOLIDWORKS davranışı

- **Kendi çalışma klasörü var** (`Get/SetCurrentWorkingDirectory`). `OpenDoc6`
  onu dosyanın klasörüne kaydırıyor ve **orada bırakıyor**; `CloseDoc` geri
  almıyor. *"Bütün belgeleri kapattım ama klasör hâlâ kilitli"*nin sebebi bu.
- **Her açılan belge için aynı klasöre gizli `~$Parca1.SLDPRT` kilit dosyası
  yazıyor.** Temiz kapanmazsa geride kalıyor: kullanıcı Gezgin'de göremiyor
  (gizli), Windows "dizin boş değil" diyor.
- **Kapatılan belgeyi oturumda tutuyor** (görünmez belge). Açtığımız belgeleri
  kapatırken kullanıcının kendi açtıklarına dokunma: yalnızca görünmez **ve**
  kaydedilmemiş değişikliği olmayanlar kapatılır.
- **Diskteki `ReadOnly` bitini kaldırmak AÇIK bir belgenin oturum içi durumunu
  DEĞİŞTİRMİYOR** — SOLIDWORKS onu açılışta önbelleğe alıyor.

### ÖLÇÜLDÜ — klasör taşınınca İÇ referanslar YAŞIYOR

Bir montaj + aynı klasördeki alt montajları geçici klasöre kopyalandı, klasör
adı `Directory.Move` ile değiştirildi, `GetDocumentDependencies2` okundu:
SOLIDWORKS çocukları **YENİ** klasörde buldu.

Beklenenden güçlü sonuç: dosyanın içinde yazan eski yol o an **hâlâ
geçerliydi** ve SOLIDWORKS yine **yanındaki kopyayı** seçti. Yani *"ebeveynin
yanındaki dosya"* kuralı, yazılı mutlak yolun **önüne geçiyor**.

→ Klasör taşınırken yalnızca **DIŞARIDAN verilen referanslar** kırılıyor;
`Directory.Move` + yalnızca dış ebeveyn onarımı meşru bir hızlı yol.

> **HENÜZ ÖLÇÜLMEDİ — teknik resim → model.** Ölçüm montaj→montaj zincirinde
> koştu. Teknik resmin model referansı bu kuralı izliyor mu **bilinmiyor**.
> Ölçülmeden hızlı yol teknik resimleri kapsamaz.

---

## 6. WinForms kullanılırsa — ölçülmüş tuzaklar

- **`ToolStrip*` öğeleri `Control` DEĞİL.** `ToolStripLabel`,
  `ToolStripButton`, `ToolStripStatusLabel`, `ToolStripMenuItem` →
  `ToolStripItem`. `Refresh`, `Invalidate`, `Focus`, `Controls`, `Parent`
  **yok**; taşıyıcıya `Owner` / `GetCurrentParent()` ile çıkılır. v1'de 18
  çağrı toplu değiştirildi, üç statik denetim TEMİZ dedi, derleme **17
  hatayla** kırıldı.
- **`ToolStripItem.Width`, `AutoSize` açıkken YOK SAYILIYOR.** `AutoSize = false`
  yazmadan verilen genişlik hiçbir şey yapmıyor.
- **İlerleme çubuğu ileri giderken ANİMASYONLU** (kendi zamanlayıcısıyla). İş
  parçacığı bloke ve mesaj pompalanmıyorsa çubuk **boş oluk** gibi görünür.
  Geriye giden değer **anında** uygulanıyor → önce hedef+1, hemen sonra hedef.
- **`Refresh()` çocuk denetimi boyamıyor.** `Update()` → `UpdateWindow` yalnızca
  kendi penceresine `WM_PAINT` yolluyor. Barındırılan bir denetim (örn. bir
  `ToolStripControlHost` içindeki gerçek `ProgressBar`) ayrıca tazelenmeli.
- **Kurucudan çağrılan sanal metot, alanlar atanmadan çalışıyor.** `Height`,
  `Dock`, `BorderStyle` gibi **boyut değiştiren** her atama, temel sınıfın
  `OnResize`'ını **o anda** çağırıyor. Alan bildirimi kurucunun daha aşağısındaysa
  orada `null` oluyor. **27.08.2026'da uygulama Windows'ta HİÇ AÇILMADI** bu
  yüzden; ve derleyici **hiç uyarmadı** — alanlar `readonly` olduğu için
  "atanacak" sayılıyor, `TreatWarningsAsErrors` açıkken bile 0 uyarı.
  → Alanlar boyut değiştiren her şeyden **önce** atanır, **ve** kurucu bitene
  kadar `OnResize`'ı susturan bir bayrak konur. İkisi birden (§2).
  → Aynı hata `ReferansListesi`'nde de vardı ve yalnızca uygulama daha erken
  öldüğü için görülmemişti. **Bir `On*` ezmesi yazan her sınıf bu açıdan
  denetlenir.**
- **`TreeNode`'un BÜTÜN çocukları silinince düğüm DARALIYOR** ve çocuk geri
  eklendiğinde açık hâli **geri gelmiyor**. Süzgeç uygularken yalnızca dosya
  içeren bir klasör bir an sıfır çocuklu kalıyor ve kullanıcının açtığı dal
  kendiliğinden kapanıyor. `IsExpanded` silmeden **önce** okunup sonra geri
  konmalı. *"Ağacı yeniden kurmuyorum"* demek tek başına yetmiyor.
- **Modal pencere mesaj kuyruğunu POMPALIYOR** — yani modal açıkken
  zamanlayıcılar tetiklenir ve olay işleyicileri **yeniden girer**. Yeniden
  giriş kilidi şart, ve kilit iş **okunmadan önce** alınmalı.

### Barındırılan süreçte (SOLIDWORKS'ün içinde) çalışılırsa

Bunlar yalnızca kod **başka bir uygulamanın süreci içinde** koşuyorsa geçerli:

- **`Application.DoEvents` YASAK.** Sürecin mesaj kuyruğunun **tamamını**
  pompalıyor. Kendi `.exe`'mizde zararsız; barındırıcının içinde, yarım kalmış
  bir API çağrısının üstüne yeniden giriş demek — ve çökme. Etiket güncellemek
  için `<denetim>.Refresh()`.
- **İSTİSNA SIZDIRMAK BARINDIRICIYI ÖLDÜRÜR.** Olay işleyicisinden ya da
  `BeginInvoke` delegesinden sızan .NET istisnası **yerli** mesaj döngüsünde
  çözülemiyor ve süreci indiriyor. Kendi `.exe`'mizde aynı istisna zararsız —
  `Main` kancaları kuruyor. *"exe'de çalışıyor ama barındırıcıda çöküyor"*
  tablosunun sebebi bu asimetri.
- **İki kanca aynı şey değil:** `AppDomain.UnhandledException` sonlanmayı
  **engelleyemiyor** (yalnızca ölümü seyrediyor); `Application.ThreadException`
  **gerçekten engelliyor**.

---

## 7. v1'den SAYILAR — v2'nin gerekçesi

| ölçüm | sonuç |
|---|---|
| Ürün kodu | **15.231 satır** |
| Bunun *"referans bağını koru"* kısmı | **2.769 · %18** |
| Arayüz kabuğu | 7.113 · %47 |
| **Tek bir arayüz sınıfı** | **9.918 satır · ürün kodunun %38'i** (ikinci en büyük dosya 526 satır) |

- O sınıf **bölünemedi**, çünkü SOLIDWORKS eklentisi onu barındırıyordu ve
  `internal` yüzeyini bölmek o yüzeyi bir sözleşmeye çevirirdi.
  → **Bir arayüz sınıfı hem ekran hem iş akışı sürücüsü olmaz.**
- **Eklenti üç kez yazıldı ve dört tur çökme yedirdi** — dört **ayrı** sebep,
  ve her seferinde "bu sonuncusu" sanıldı. *"Kalan sebep yok"* diyen bir ölçüm
  hiçbir zaman olmadı. Onu kurtarmak için yazılan süreçler-arası köprü ise
  **kendi hata sınıfını** doğurdu (son iki hata da köprü hatasıydı: durum
  yazmanın iki yolundan biri köprüden geçmiyordu; gizli süreçte açılan bir
  bilgi kutusu paneli **sonsuza kadar** bekletiyordu).

---

## 8. Kod değiştirirken

- **Bir satırı silmeden önce o satırın BİLDİRDİĞİ her adı ara** — yalnızca
  çağırdıklarını değil, `=` solundaki adı da. v1'de silinen bir **yerel
  değişken** bildirimi üç denetimden geçti ve derlemeyi kırdı.
- **Toplu değişiklikte alıcının tipini VARSAYMA — bildirimini ara.**
- **Aynı mantığın ikinci kopyasını yazma.** v1'de "yolun son parçası" mantığı
  **dokuz** yerde elle yazılmıştı ve üç ayrı biçimde ayrışmıştı: üçü boş
  girdide `NullReferenceException` atıyordu, bir kısmı sondaki ayırıcıyı
  kırpmıyordu, ikisi `/` tanımıyordu. Boyut biçimlendirmesi üç yerdeydi ve biri
  **farklı sayı** gösteriyordu — aynı dosya iki ekranda farklı boyutta.
- **Bir belge yorumunu sahibinden ayırma.** Araya üye eklerken en sık yapılan
  kaza bu; sonuç, artık orada olmayan bir üyeyi anlatan bir yorum.

## 9. Kapı disiplini

- **Yanlış alarm veren bir kapı, kapı olmaktan çıkar.** v1'de genel bir
  "bildirilmemiş ad" denetimi denendi: 125 dosyada 129 bulgunun **tamamı**
  yanlış alarmdı. Eklenmedi.
- **Bir kapı ÖLÇÜLEREK eklenir:** gerçek depoda TEMİZ · hata geri konunca
  YAKALIYOR · geri alınca yine TEMİZ. Bu üçü gösterilmeden kapı eklenmez.
  v1'de bir kapı yazıldığı anda **inert**ti (yanlış metni okuyordu) ve hep
  "TEMİZ" diyordu; ancak bilerek bir ihlal konunca anlaşıldı.
- **Depoda ve CI'da olmayan denetim, denetim değildir.** v1'de geçici bir
  dosya olarak duran bir denetim yüzünden kırık kod **üç commit** yayınlandı.
- **Kapının kapsamı ADLARA değil AĞACA bağlanır.** v1'de bir kapı iki proje
  adına bakıyordu; üçüncü bir proje eklenseydi **sessizce** atlanırdı.
- **Kurulu olmayan bir kapı "geçti" sayılmaz** — atlamaz, hata verir.

## 10. Bitirmeden önce

- **Test ETMEDİĞİN ve riskli noktaları açıkça yaz.** "Oldu" deyip geçme.
- Sayıları belgeden okuma — **çalıştır**.

---

## 11. ÖLÇÜLMÜŞ YAPI — derleme, çalıştırma, kapılar

> Bu bölüm §1–§10'dan farklı: **depodaki gerçek dosyalara işaret ediyor.**
> Oradaki adlar değişirse burası da değişmeli.

### Bu depo Linux'ta derleniyor VE çalışıyor — ölçüldü (27.08.2026)

Bulut oturumu Linux. Buna rağmen:

| ölçüm | sonuç |
|---|---|
| `dotnet publish -r win-x64 --self-contained` | **gerçek Windows PE32+ `.exe`** üretiyor |
| Wine 9.0 + Xvfb altında o `.exe` | **açılıyor**, pencere doğuyor, görüntü alınabiliyor |
| `net8.0` çekirdek + xunit | Linux'ta **koşuyor** |

→ *"Ölçemiyorum"* artık geçerli bir mazeret değil. v1'de ölçülemeyen alanın
bedeli §7'de duruyor.

### Ubuntu deposundaki .NET SDK, WindowsDesktop bileşenini TAŞIMIYOR

`Sdks/Microsoft.NET.Sdk.WindowsDesktop` klasörü **yok** (`ls` ile doğrulandı).
Sonuç: `UseWindowsForms` burada **MSB4019** ile kırılıyor. Microsoft'un kendi
deposu `noble` için `dotnet-sdk-8.0` yayınlamıyor, `builds.dotnet.microsoft.com`
vekil tarafından **403**. Yani resmî SDK yolu da kapalı.

**Çalışan yol** — `UseWindowsForms` yerine doğrudan:

```xml
<EnableWindowsTargeting>true</EnableWindowsTargeting>
<FrameworkReference Include="Microsoft.WindowsDesktop.App.WindowsForms" />
```

Bu, `UseWindowsForms`'un altta yaptığı şeyin ta kendisi ve **hem Windows'ta hem
Linux'ta** tutuyor. Bedeli: `ApplicationConfiguration.Initialize()` üreteci
gelmiyor, üç çağrı elle yazılıyor (`SetHighDpiMode` · `EnableVisualStyles` ·
`SetCompatibleTextRenderingDefault`).

### Wine: `mscoree` devre dışı bırakılırsa .NET 8 AÇILMIYOR

`WINEDLLOVERRIDES="mscoree,mshtml="` yazmak — .NET Framework için yaygın bir
tarif — .NET 8 uygulamasını **kendi klasöründeki** `System.Runtime.dll`'i bile
reddeder hâle getiriyor:

```
FileNotFoundException: Could not load file or assembly '...\System.Runtime.dll'. Module not found.
```

Belirti yanıltıcı: dosya **oradadır**. Korelasyon birebir ölçüldü — bu değişkenin
olduğu her koşu kırıldı, olmadığı her koşu çalıştı. Yolda **iki yanlış hipotez**
kuruldu (eski derleme · yol tuhaflığı); ikisi de 2×2 ölçümle elendi.

### WinRT kullanılabiliyor — hedef çerçeve `net8.0-windows10.0.19041.0`

`Windows.Data.Pdf` gibi WinRT API'leri için hedef çerçeveyi sürümlemek gerekiyor.
**Ölçüldü (27.08.2026): bu, Linux derlemesini ve üç kapıyı KIRMIYOR.**

```
<TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
<TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
```

`EnableWindowsTargeting` + `FrameworkReference` aynen kalıyor; SDK
`Microsoft.Windows.SDK.NET.Ref`'i NuGet'ten çekiyor. `TreatWarningsAsErrors`
açıkken **0 uyarı** — CA1416 çıkmadı, `TargetPlatformMinVersion` doğru kurulduğu için.

→ Windows'un içindeki motorlara (PDF, medya, OCR) **pakete tek bayt eklemeden**
ulaşılabiliyor. Alternatifi PDFium gibi yerli bir kütüphaneydi: paket 136 KB'den
~10 MB'a çıkardı.

### Wine WinRT TAŞIMIYOR — ve belirtisi yanıltıcı

Wine'da hiçbir `Windows.*` çalışma zamanı sınıfı yok. Ama eksiklik
`TypeLoadException` olarak **gelmiyor**:

```
COMException 0x80040154 (REGDB_E_CLASSNOTREG)
```

Yalnızca tip yükleme hatalarını yakalayan bir denetim bunu kaçırır ve kullanıcıya
ham HRESULT gösterir (ölçüldü, ilk yazışta öyle oldu).

→ **WinRT'ye dayanan hiçbir yol burada ölçülemez.** Ölçülebilen tek şey:
çökmediği ve sebebini söylediği. Gerçek davranış yalnızca Windows'ta görülür.

### Wine'ın ÖLÇMEDİĞİ

- **Segoe UI kurulu değil** → yazı ölçüleri ve hizalamalar Windows'takinden farklı.
- Sekme, kaydırma çubuğu, kenarlık çizimi Wine'ın kendi teması.
- → Wine *"açılıyor mu, çöküyor mu"* sorusunu **kesin** cevaplar;
  *"piksel piksel aynı mı"* sorusunu **cevaplamaz**.

### Kabuk simgesi: kayıt yoksa hepsi AYNI genel simge

`SHGetFileInfo` + `SHGFI_USEFILEATTRIBUTES` ile uzantıya kayıtlı simge alınıyor —
SOLIDWORKS kurulu makinede **gerçek** simgeler geliyor. Ama kurulu **değilse**
kabuk `.SLDPRT`/`.SLDASM`/`.SLDDRW` için **aynı boş sayfa simgesini** dönüyor;
yani "gerçek simge" isteği, gerçekte üç türü **ayırt edilemez** yapıyor.

→ Gelen simge, kayıtlı olmadığı **kesin** olan uydurma bir uzantının simgesiyle
piksel piksel karşılaştırılır. Aynıysa kayıt yok demektir → çizilmiş yedeğe düşülür.

### Kapılar

```
araclar/paket.sh               # Erkan'in deneyecegi zip (~120 KB)
araclar/kapilar.sh [--kur]     # üçünü sırayla koşar
├── kapi_derleme.sh            # ağaçtaki her .csproj, uyarılar hata sayılarak
├── kapi_test.sh               # ağaçtaki her test projesi; SIFIR test GEÇTİ değildir
└── kapi_calistir.sh [--kur]   # uygulamayı Wine'da GERÇEKTEN açar
```

Kapsam **adlara değil ağaca** bağlı (§9): proje `find` ile, WinExe içerikten
(`OutputType`), test projesi içerikten (`Microsoft.NET.Test.Sdk`), uygulama adı
`runtimeconfig`'ten bulunur. Hiçbirine dosya/proje adı **yazılmamıştır**.

**Çalıştırma kapısı dört şey ölçer:** süreç ayakta mı · hata akışı temiz mi ·
çökme penceresi var mı · ana pencere doğdu mu. Ekran görüntüsünü `.kapi/ekran.png`
olarak bırakır; CI'da yapıt olarak saklanır.

> **Derleme kapısının GÖRMEDİĞİ sınıf vardır.** 27.08.2026'daki kurucu hatasında
> derleme "0 uyarı 0 hata" diyordu ve uygulama hiç açılmıyordu. Çalıştırma kapısı
> tam olarak bunun için var. **Yeşil derleme, çalışıyor demek değildir.**

Üçü de §9'a göre ölçülerek eklendi — TEMİZ → hata konunca YAKALADI → geri
alınca TEMİZ. Yakaladıkları gerçek hatalardı: `ToolStripLabel.Refresh()` (§6),
sürücü kökünde kırpılan ters bölü (§4), kurucudan çağrılan `OnResize` (§6).

CI (`.github/workflows/kapilar.yml`) **aynı betikleri** koşar — ikinci kopya yok
(§8). Üç iş: Linux derleme+test · Windows derleme+test (gerçek SDK) · Wine
çalıştırma.

### Testler Windows'ta gerçek `Path`'e karşı koşuyor

`WindowsYolu` Linux'ta elle yazılmış beklenen değerlerle, **Windows'ta ise
`System.IO.Path`'in kendisiyle** karşılaştırılıyor.

> **Bu karşılaştırma ilk koşuşunda İKİ TEST KIRDI** ve iyi ki kırdı — burada
> ölçülemeyecek bir farkı buldu: **.NET'in `Path`'i sondaki ayırıcıyı
> KIRPMIYOR.** `GetFileName(@"C:\a\b\")` → **boş**, `GetDirectoryName` →
> yolun kendisi. Bir dosya yöneticisinde bu yanlış: kullanıcının gördüğü
> klasörün adı "" olamaz. §8 zaten kırpmamayı v1'in **kusuru** olarak sayıyor.
> → Kırpmak **bilinçli** bir ayrılmadır; tek ayrılma budur ve ayrı bir testle
> görünür tutulur.
>
> Kapının kendi kusuru da o turda çıktı: `dotnet test -v q` kırılma
> **ayrıntısını yutuyor**, geriye yalnızca `[FAIL] testAdı` kalıyor ve kırılma
> CI günlüğünden teşhis edilemiyor. `--logger "console;verbosity=normal"`
> gerekli (§3: hata sebebi gösterilir). Windows dışında o testler
**sebebiyle** atlanır (sessizce değil — §3). Linux tarafında ayrıca `Path`'in
bozukluğu belgeleniyor: .NET bir gün düzeltirse test haber verir.

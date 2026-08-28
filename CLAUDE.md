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
>
> **§1b ve §1c Erkan'ın koyduğu kalıcı varsayılanlardır** — §1b her yeni kodun
> tasarımı, §1c takılınca ne yapılacağı. İkisi de ayrıca sorulmaz.

> **AÇIK İŞLER BURADA DEĞİL — [`SIRADAKI.md`](SIRADAKI.md)'de.** Bu dosya
> bayatlamayan gerçekleri tutar; günün yapılacakları ayrı bir dosyada durur ve
> iş bitince oradan silinir (§1b'nin kendisi: değişen şey tek yerde yaşar).
> Yeni bir oturum önce burayı, sonra `SIRADAKI.md`'yi okur.

---

## 1. Altın kurallar

### 1a — Çalışan hiçbir şeyi bozma

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

### 1b — Bir özellik tek yerde yaşar ve TEK HAMLEDE silinebilir

Erkan, 27.08.2026: *"ileride bir özelliği değiştirmek ya da silmek istediğimde
minimum sayıda koda ve dosyaya dokunmak isterim; bunu bundan sonra yapacağın
tüm kodlarda varsayılan olarak kabul et."*

**Bu bir tercih değil, kabul edilmiş varsayılan.** Her yeni kod buna göre
yazılır; ayrıca istenmez.

Ölçütü tek soru: **"Bu özelliği kaldır" dendiğinde kaç dosyaya dokunurum?**
Cevap *bir dosyayı sil + bir satır bağlantıyı kes*'ten fazlaysa yapı yanlıştır.

Bundan çıkan üç kural:

1. **Özelliğin BÜTÜN kararı kendi dosyasında durur.** Hangi kaynak, hangi
   sıra, hangi mesaj, hangi iş parçacığı, hangi hata metni — hepsi orada.
   Yarısı burada yarısı çağıranda olan bir özellik, silinemeyen bir özelliktir.
2. **Hiçbir özellik başka bir özelliğin dosyasına satır ekletmez.** Merkezî
   listeler (bir enum + bir simge listesi + bir menü listesi) bu kuralın en
   sık ihlali: yeni bir şey eklemek dört dosyaya satır ekletir, **silmek de
   dört dosyadan satır sildirir** ve biri unutulur. Liste tek yerde durur,
   ötekiler ondan **türetilir**.
3. **Ortak araç ≠ özellik.** Renk, boyut/tarih biçimi, yol mantığı tek kopya
   kalır (§8); bunlar silinecek özellikler değil, herkesin kullandığı
   araçlardır. Karışmasın: özellik **dikey** (kendi dosyası), araç **yatay**
   (tek kopya).

> **Türetmek, yorumla hizalamaktan üstündür.** İki listenin sırasının aynı
> olmasını *bir yorum satırı* sağlıyorsa, o hizalama er geç kayar ve hata
> **sessizdir** — yanlış simge çizilir, hiçbir şey patlamaz. İkinci listeyi
> birinciden üret; o zaman kayacak bir şey kalmaz.

### 1d — KISA YAZ, madde madde

Erkan, 28.08.2026: *"uzun uzun açıklıyorsun okumuyorum, kısa kısa maddeler
hâlinde açıkla ve bunu hep yap."*

**Bu da kabul edilmiş varsayılan.** Sohbetteki her cevap madde madde ve kısa.
Uzun gerekçe **koda ve commit mesajına** yazılır, sohbete değil — orada
kalıcıdır ve arandığında bulunur; sohbette okunmuyor.

Not: bu §3'ü (dürüstlük) **kaldırmaz**. Çalışmayan bir şey yine söylenir —
ama bir satırda.

### 1c — Takılınca BİR kez dene, sonra GEÇ ve söyle

Erkan, 27.08.2026: *"takıldığın yeri düzeltmek için birden fazla deneme yapma,
atla sonraki özelliğe geç. En son tüm paketi ver ve çalışmayabilecek
özellikleri söyle — belki bende çalışır, veya göz ardı edilebilir bir
özelliktir, pas geçerim. Bunu tüm projeler için yap."*

**Bu da kabul edilmiş varsayılan.** Gerekçesi sağlam: bu ortam Wine, Erkan'ın
makinesi gerçek Windows. Burada saatlerce kovaladığım bir belirti orada hiç
olmayabilir (§11'de Wine'ın ölçemedikleri sayılı). Kovalamanın bedeli kesin,
getirisi belirsiz.

Uygulaması:

1. **Bir onarım denemesi.** Tutmazsa kovalama durur. Sebebi bulmuş olmak
   yetmez — *tutmayan* onarım da denemedir.
2. **Bulunanlar yazılır, atılmaz.** Elenen hipotezler ve sıradaki ölçüm
   commit mesajına geçer; sonra bakan (ya da ben) sıfırdan başlamaz.
3. **Sonraki özelliğe geçilir.** Takılan yer bir sonrakini engellemiyorsa
   sıra beklemez.
4. **Paket YİNE DE verilir** — bu §1a'yı bozmaz, çünkü:
5. **`SURUM-NOTU.txt` çalışmayabilecekleri AÇIKÇA sayar.** "Şu özellik bende
   çalışmadı, sende çalışabilir" demek §3'ün ta kendisidir; sessizce koymak
   yalandır.

> **Bu kural §1a'nın yerine geçmez.** *Çalışan* bir şeyi bozan bir değişiklik
> yine geri alınır. Atlanan şey **yeni ve hiç çalışmamış** bir özelliktir;
> bozulmuş bir özellik değil.

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

> **ÖLÇÜLDÜ (28.08.2026) — teknik resim → model de okunuyor.** Yukarıdaki
> "bilinmiyor" kapandı: `Parça1.SLDDRW → Parça1.SLDPRT` ve
> `Montaj2.SLDDRW → Montaj2.SLDASM` gerçek dosyalarda ölçüldü. Ama bu,
> *dosyanın içinde yazan yolun okunabildiği*; SOLIDWORKS'ün klasör taşınınca
> teknik resmi nasıl bulduğu **hâlâ ölçülmedi**.

### SOLIDWORKS 2022 DOSYA BİÇİMİ — ölçüldü, OLE değil

Yedi gerçek dosyayla ölçüldü (`araclar/ornek-veri/tertemiz/`). **Dosyalar OLE
Bileşik Belge DEĞİL** — bu önce yanlış varsayıldı ve ilk ölçüm "hiçbir şey
bulunamadı" dedi. Entropi 7,9/8: dosya baştan sona sıkıştırılmış.

Gerçek yapı, ardarda dizilmiş **adlandırılmış akışlar**:

```
uint32 sikisikBoyut · uint32 acilmisBoyut · uint32 adUzunlugu
ad   (her baytın NIBBLE'ları takas edilmiş ASCII: 34 F6 E6 47 -> "Cont")
veri (ham deflate, zlib başlığı YOK)
```

Dizeler MFC Unicode `CString`: `FF FE FF <uzunluk:1 bayt> <UTF-16LE>` —
52 yolun 52'sinde tuttu. SOLIDWORKS bir MFC uygulaması; okunan şey onların
kendi serileştirme biçimi.

| akış | ne tutuyor |
|---|---|
| `Header2` | **doğrudan** referanslar |
| `Contents/DisplayLists`, `Contents/Definition` | bütün ağaç (torunlar dahil) |
| `_MO_VERSION_15000/Biography` | belgenin **kendi** yolu |
| `PreviewPNG` | **gerçek PNG** (`89 50 4E 47`) |
| `docProps/custom.xml` | kullanıcının özellikleri (Malzeme, Ağırlık…) |
| `docProps/ISolidWorksInformation.xml` | 46 sistem özelliği |
| `docProps/core.xml` | kim, ne zaman kaydetti |

**Doğrudan/dolaylı ayrımını `Header2` yapıyor.** Akış ayrımı yapılmazsa
`Montaj2.SLDASM` torunlarını da doğrudan çocuğu gibi gösteriyor.

> **SAYISAL DENETİM YETMİYOR — akış başlığı AÇILARAK doğrulanır.** Zincir
> yürütücüsünün ilk hâli sahte başlıklar kabul ediyordu: `Preview 364 → 728`,
> `PreviewPNG 455 → 910` gibi, hepsinde açılmış boyut sıkışığın tam iki katı
> ve **adları gerçek akış adlarıydı** (gerçek kaydın adına denk geliyorlardı).
> Zincir oraya atlıyor, gerçek `Header2` bir daha bulunamıyordu. Belirti
> sessiz: istisna yok, yalnızca "Header2 okunamadı" — yedi dosyanın DÖRDÜNDE.
> *"Sonraki başlık da makul mü"* denetimi **denendi, YETMEDİ**. Veriyi
> gerçekten açmak ayırdı; birkaç on bayt yetiyor, yani hız korunuyor.

**HIZ ÖLÇÜLDÜ:** akışların verisi okunmuyor, **atlanıyor**. Dosya başına
~66 KB ve 2,8–10 ms — ve bu sayı **dosya boyutundan bağımsız**. "Binlerce
parçalı montajda uzun sürer mi" sorusunun cevabı bu: montajın kullandığı
parçaları öğrenmek için parçalar açılmıyor, montajın kendi `Header2`'si
zaten hepsini yazıyor.

> **ÖLÇÜLMEDİ:** 100 MB+ bir montajda akış *sayısı* artıyor mu; eski
> SOLIDWORKS sürümlerinin biçimi (2015 öncesi büyük ihtimalle OLE, o yüzden
> `BilesikDosya` duruyor); 254 karakterden uzun yolların MFC kaçış biçimi.

### DOSYAYA YAZMA — ölçüldü (28.08.2026), yeniden kurma DEĞİL YERİNDE yama

Ad değişince komşuluk kuralı kurtarmıyor: ebeveyn **eski adı** arar. Tek
çözüm ebeveynin içindeki yazıyı değiştirmek. Ölçülenler:

- **Akış zinciri dosyanın TAMAMINI kapsamıyor — %86–94.** Sekiz dosyada:
  başta ~300–850, akışlar arasında ~1200–2200, sonda ~3000–4300 bayt
  **bilmediğimiz veri** var. → Dosyayı **yeniden kurmak yasak**: o baytları
  doğru yerine koyduğumuzu asla kanıtlayamayız.
- **Ama içeride mutlak ofset tutan bir dizin GÖRÜNMÜYOR.** Kuyruktaki 4211
  bayt ve aralardaki 1747 bayt bilinen akış ofsetlerine karşı tarandı:
  **0 eşleşme** (kuyrukta 1 rastlantı).
- **Yeniden sıkıştırılan veri ESKİ YUVAYA SIĞIYOR.** `Montaj1.SLDASM`'ın
  `Header2`'si 923 baytlık yuvada; ad değişiminden sonra seviye 9'da **880**.
  → **YERİNDE YAMA mümkün:** `sıkışıkBoyut` aynı kalır, artan yer sıfırlanır,
  **dosyada tek bayt kaymaz**, boyut bile değişmez, bilinmeyen %13'e hiç
  dokunulmaz.
- **AMA HER AKIŞ SIĞMIYOR:** `PreviewPNG` zaten sıkıştırılmış bir PNG;
  yeniden deflate edilince **büyüyor** (13085 > 13081). Sığmayan akış
  **yazılmaz**, işlem reddedilir.
- **İlk 4 bayt her kayıtta değişiyor** (aynı parçanın iki sürümü farklı) ama
  **standart bir sağlama toplamı DEĞİL**: CRC32/Adler32/bayt toplamı, üç
  ayrı başlangıçla, sekiz dosyada **0/8**. Ne olduğu **BİLİNMİYOR**.
  4–7. baytlar dört dosyada da sabit: `00 00 00 04`.
- **Aynı yol BİRDEN ÇOK AKIŞTA yazılı.** `Header2` ile
  `Contents/Config-0-ModelHeader` **birebir aynı** (ikisi de 2558 bayt
  açılmış, aynı ofsetlerde aynı dizeler). Montajda yol **4**, teknik resimde
  **3** akışta geçiyor (`Contents/DisplayLists`,
  `SwDocContentMgr/SwDocContentMgrInfo`, `Contents/Definition`,
  `Contents/VBLists`). Yalnız birini değiştirmek **eskisini geride bırakır**;
  `SwYazici` bunu `KalanAkislar` ile **söylüyor**, sessiz kalmıyor.

> **ÖLÇÜLDÜ — SOLIDWORKS YAMALI DOSYAYI AÇIYOR (28.08.2026, Erkan'ın
> makinesi).** Dört deney, değişkenler tek tek ayrılarak:
>
> | deney | sonuç |
> |---|---|
> | metin aynı, yalnızca yeniden sıkıştırma + dolgu | **açıldı** |
> | yalnız `Header2` değişti | açıldı ama **içi boş** — bir akış yetmiyor |
> | yol yazan **bütün** akışlar, **aynı uzunlukta** ad | **açıldı, parçalar yerinde** |
> | daha **uzun** ad | **HATA, açılmadı** |
>
> Yani: baştaki 4 bayt engel değil, yeniden sıkıştırma ve sıfır dolgu kabul
> ediliyor, **ad değiştirme onarımı çalışıyor** — tek şartla: yazılan dizenin
> **karakter sayısı değişmemeli**.
>
> **UZUNLUK NEDEN ÖNEMLİ — BULUNAMADI.** Akışın içinde ne toplam boy ne de
> dize ofseti yazıyor (`Header2`, `DisplayLists`, `SwDocContentMgrInfo`
> tarandı: 0 eşleşme). Sebep kovalanmadı (§1c); yerine **şart sağlanıyor**:
> `SwYazici.UzunlukKorunanYol` farkı klasör kısmından karşılıyor — ad
> kısalırsa araya `.\`, uzarsa soldan klasör atılıp yol göreli yapılıyor.
> Bu meşru, çünkü §5'te ölçüldü: SOLIDWORKS önce ebeveynin yanına bakıyor.
>
> **İKİNCİ TUR ÖLÇÜLDÜ (28.08.2026) — DOLGULU VE GÖRELİ YOL DA KABUL
> EDİLİYOR.** `4-kisa-ad-dolgulu` ve `5-uzun-ad-goreli` **ikisi de açıldı**;
> ayrıca Erkan uygulamanın kendisinde uzun bir adla deneyip **çalıştığını**
> gördü. Yani uzunluk sınırı **kalktı**: `UzunlukKorunanYol` farkı klasör
> kısmından karşıladığı sürece ad kısalabilir de uzayabilir de.
>
> Kalan tek sınır: adın kendisi eski **yolun tamamından** uzunsa dolgu
> yapılamaz — o zaman onarım **reddedilir ve sebebi yazılır**.
>
> **ÜÇÜNCÜ TUR ÖLÇÜLDÜ (28.08.2026) — TAŞIMA ONARIMI DA ÇALIŞIYOR.** Buraya
> kadar ölçülen hep **ad değişimi**ydi; taşımanın yazdığı **göreli yol**
> (`..\3\Parça1.SLDPRT` gibi) ölçülmemişti ve pakette *"bunu deneyin"* diye
> duruyordu. Erkan uygulamanın kendisinde denedi: parça alt klasöre taşındı,
> teknik resim **açıldı ve parçayı buldu**.
>
> → Yani üç yolun üçü de kabul ediliyor: **aynı uzunlukta ad · dolgulu ad ·
> göreli yol**. Yazma tarafında ölçülmemiş bir yol kalmadı.

### Dosyaların içindeki yollar MUTLAK ve YAZARIN makinesine ait

Ölçüldü: `C:\Users\PC\Desktop\tertemiz\Parça1.SLDPRT`. Aynı dosyalar başka
bir makinede `\\sunucu\ortak\...` altında duruyor — yani **tam yol
eşleştirmesi çalışmaz**. Eşleştirme ADA göre yapılır ve birden çok aday
varsa **komşu kazanır**; bu tahmin değil, yukarıda ölçülen "yanındaki dosya
yazılı yolun önüne geçer" davranışının kendisi. Karar verilemiyorsa
**BELİRSİZ** denir, tek cevap uydurulmaz — yanlış bir "bu dosya" cevabı
kullanıcıya yanlış dosyayı sildirir.

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
- **Devre dışı bırakılan denetim ODAĞI KAYBEDİYOR.** Uzun bir iş sırasında
  ağacı `Enabled = false` yapıp sonra geri açmak odağı geri getirmiyor; odağa
  bağlı kısayollar (`Ctrl+Z`, `Delete`, `F2`) sessizce çalışmaz oluyor.
  Belirti sinsi: işlem çalışıyor, hemen ardından kısayol hiçbir şey yapmıyor.
  → İş bitince odak elle geri verilir.
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
- **`TreeView` ÇOKLU SEÇİMİ DESTEKLEMİYOR.** `SelectedNode` tektir;
  `MultiSelect` özelliği **yoktur** (o `ListView`'de var), dikdörtgenle seçim
  de yoktur. Çoklu seçim isteniyorsa yazılacak: seçimi kendi tutan, seçili
  satırı kendi boyayan bir alt sınıf. `DrawMode = OwnerDrawText` yeter —
  çizgileri, `+/-` kutularını ve simgeleri denetim çizmeye devam eder, yalnızca
  metin alanı bizim olur; `OwnerDrawAll` gereksiz risktir.
  → Denetimin kendi `SelectedNode`'u **odak** olarak yaşatılır ve her zaman
  kümenin içinde tutulur; böylece ona bakan her şey (önizleme, durum çubuğu,
  arama, süzgeç) hiçbir şey bilmeden çalışmaya devam eder.
- **Modal pencere mesaj kuyruğunu POMPALIYOR** — yani modal açıkken
  zamanlayıcılar tetiklenir ve olay işleyicileri **yeniden girer**. Yeniden
  giriş kilidi şart, ve kilit iş **okunmadan önce** alınmalı.

### Mesaj kutusu ne zaman çıkar — Erkan'ın kuralı (28.08.2026)

*"Tüm işlemler mükemmel, sadece gösterdiği mesaj kutuları gereksiz."*

> **Kutu yalnızca iki şey için çıkar:**
> 1. geri alması zor bir işlemin **ONAYI**
> 2. kullanıcının görmeden geçemeyeceği bir **HATA**
>
> Bilgi (kaç dosya, kaç MB, ne kadar sürdü) **durum çubuğuna** yazılır.

Onaylar tek yerden geçer: `Arayuz/Gorunum/Islemler/OnayKutusu.cs` — düğmeler
**Evet / Vazgeç**. (`MessageBox`'ın düğme yazıları Windows'tan geliyor;
`YesNo` "Evet/Hayır", `OKCancel` "Tamam/İptal" veriyor — istenen ikisinde de
yok.)

**Bu kural bir sadeleştirme değil, §3'ün devamı.** 28.08.2026'da taşıma
kutusu hâlâ *"onları şu an ONARAMIYORUZ"* ve *"bu ölçüm HENÜZ YAPILMADI"*
diyordu; ikisi de bir önceki sürümde geçersiz kalmıştı. **Bayat uyarı, fazla
uyarıdan daha tehlikelidir** — kullanıcı ona bakıp karar veriyor. Kutu
sayısını az tutmanın asıl faydası, kalan kutuların **doğru** kalması.

Aynı turda kalkanlar: bağımlılık sorusu (onarım geldi, gereksizleşti) ·
klasör boyutu sonucu (durum çubuğunda zaten var) · üst üste çıkan iki hata
kutusu (birleşti) · ad değiştirmede ayrı uzantı uyarısı (tek kutuya girdi).

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
- **TANI SATIRI ÜRETİM KODUYLA AYNI SATIRA YAZILMAZ.** 27.08.2026'da tam
  bunun bedeli ödendi. Bir düğmenin bağlantısı geçici olarak şuna çevrildi:

  ```csharp
  d.Click += (_, _) => { Console.Error.WriteLine("TANI ..."); Sec(d); };
  ```

  Sonra tanılar `"TANI" geçen satırları sil` diye temizlendi — ve **`Sec(d)`
  de gitti.** Sonuç: süzgeç düğmeleri çiziliyor, odağı alıyor, üstüne gelince
  renk değiştiriyor ama **hiçbir şey yapmıyordu**. Derleme TEMİZ, testler
  TEMİZ, uygulama açılıyor; hata kullanıcıya kadar gitti.
  → Tanı **kendi satırında** durur. Temizlik gözle değil, **bir önceki
  commit'e karşı `git diff` ile** doğrulanır.

- **"Önceden çalışıyordu" ise ÖNCE ESKİ SÜRÜMÜ DERLE.** Yukarıdaki hatada
  belirti derin bir WinForms/Wine sorunu gibi görünüyordu ve **beş hipotez**
  (fare yakalaması · `base` çağrı sırası · `Focus()` · owner-draw · DPI kipi)
  ölçülerek elendi. Hepsi boşa gitti. `git archive <eski-commit>` ile eski
  sürümü derleyip aynı ölçümü koşmak **tek adımda** doğru commit'i gösterdi;
  oradan `git diff` bir satırlık sebebi verdi. Bu, tahmin kovalamaktan
  **önce** yapılacak iştir.

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

→ Windows'un içindeki motorlara (PDF, medya, OCR) **yerli bir kütüphane
eklemeden** ulaşılabiliyor.

> **DENENDİ, ÇALIŞTI, YİNE DE KULLANILMADI — karar 27.08.2026.**
> PDF önizlemesi `Windows.Data.Pdf` ile yazıldı ve çalıştı; sonra **geri alındı.**
> İki sebep, birincisi daha önemli:
>
> 1. **İlke:** bu uygulamanın önizleme sözü *"Windows ne gösteriyorsa onu
>    göster"*. Kendi PDF motorumuzu gömünce Gezgin'in **göstermediği** bir
>    önizlemeyi gösterir olduk — sözü kendimiz bozduk.
> 2. **Bedel:** aşağıdaki ölçüm; paket 120 KB → 6,5 MB.
>
> Yerine kullanıcıya **ne yapacağı** söyleniyor (bir PDF okuyucu kurup Gezgin
> küçük resimlerini açması). Bir kez kurar, hem burada hem Gezgin'de görür.
> Yani bu bölüm *"WinRT kullanılabilir"* diye duruyor — **kullanılıyor diye
> değil.**

> **BEDELİ ÖNCE YANLIŞ YAZDIM** — §2'nin ihlali. *"Pakete tek bayt eklemez"*
> dedim; sonra ölçtüm:
>
> | | WinRT'den önce | sonra |
> |---|---|---|
> | zip | 120.343 bayt | **6.486.532 bayt** |
> | diskte | ~264 KB | **25 MB** |
>
> Motorun kendisi Windows'ta, **ama köprüsü değil**:
> `Microsoft.Windows.SDK.NET.dll` **24,9 MB** ve `WinRT.Runtime.dll` 529 KB
> çıktıya kopyalanıyor. Kıyas hâlâ WinRT'nin lehine ama sandığım kadar değil —
> PDFium ~10 MB olur **ve depoya ikili dosya sokardı**; burada depoya ikili
> girmiyor, DLL NuGet'ten geliyor.
>
> Ağırlık tek bir köprü derlemesinde. İlke olarak CsWinRT ile yalnızca
> kullanılan ad alanlarına indirgenebilir — **denenmedi.**

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

### Wine bir uygulama için bir sürü 1×1 pencere açıyor — `head -1` yanıltıyor

`xwininfo -root -children` çıktısında uygulamanın adı **birden çok** satırda
geçiyor: `Default IME`, `.NET-BroadcastEventWindow`, adsız `1x1+0+0` pencereler.

```
0xc00005 (has no name): ("swpdm.exe" ...)  1x1+0+0      +0+0     <- head -1 BUNU aliyor
0xc00002 "SW PDM ...":  ("swpdm.exe" ...)  572x880+314+123  +314+123
```

Bir kapıda pencere konumu `head -1` ile okunmuştu: `+0+0` döndü, `xdotool`
pencerenin **dışına** tıkladı ve kapı *"hiçbir şey seçili değil"* dedi.
Belirti doğruydu, **sebep başkaydı** — §2'nin "belirtiyi tek sebebe bağlama"
maddesi. Konum, boyutu bulan **aynı satırdan** okunmalı.

> `ControlPaint.DrawReversibleFrame` (dikdörtgen seçim çerçevesi) Wine'da
> **çalışıyor ve iz bırakmıyor** — ölçüldü: fare bırakıldıktan sonra ağaç
> alanında çerçeve renginden tek piksel kalmadı.

### Wine'da `ContextMenuStrip` uygulamayı ÇÖKERTİYOR

Sağ tık menüsü açılınca:

```
Win32Exception 0x80004005  "Failed to get thread's DpiAwareness context"
```

Wine `GetThreadDpiAwarenessContext`'i taşımıyor; WinForms'un `ToolStripDropDown`
ölçekleme yolu onu çağırıyor. Gerçek Windows'ta böyle bir sorun yok.

`HighDpiMode.DpiUnaware`'e düşürmek **denendi, ÇÖZMEDİ** (§1c: bir deneme).
Düşürme yine de kodda duruyor ama **yalnızca Wine'da** — gerçek Windows
`PerMonitorV2` ile çalışmaya devam ediyor, yani orada tek satır davranış
değişmiyor.

→ **Sağ tık menüsü burada ÖLÇÜLEMEZ.** Aynı işlemlerin kısayolları
(`Ctrl+Shift+N` · `Delete` · `F2` · `Ctrl+X` · `Ctrl+V`) ölçüldü ve **hepsi
çalıştı**; yani menü öğelerinin çağırdığı kod doğru, ölçülemeyen yalnızca
menünün kendisi.

> **İKİNCİ KURBAN ÖLÇÜLDÜ (27.08.2026):** sıralama düğmesinin açılır listesi
> de bir `ContextMenuStrip` ve Wine'da **aynı** yığın izini verip uygulamayı
> indirdi. Yani bu, sağ tık menüsüne özgü değil: **her `ToolStripDropDown`.**
> Sonuç bir kural oldu — *bir açılır menüye bağlanan her özellik, aynı kodu
> çağıran bir **kısayol** da alır.* Kısayol hem kullanıcıya yarar hem de
> özelliği ölçülebilir kılar; menüsüz kalan bir özellik burada **kör noktadır**.

### Wine + ASCII yerel ayar TÜRKÇE DOSYA ADINI BOZUYOR — ölçüldü (28.08.2026)

Kapsayıcının varsayılanı `LC_CTYPE=POSIX`. Wine dosya adlarını o kod sayfasına
çevirmeye çalışıyor ve **`Parça1.SLDPRT` uygulamaya `ParC'a1.SLDPRT` olarak
geliyor** (indeks dosyasından `od -c` ile okundu).

Belirti sinsi ve **yanıltıcı**: dosya ağaçta görünüyor, açılıyor, her şey
normal — ama dosyanın **içinde** yazan `Parça1.SLDPRT` ile eşleşmiyor ve
referans **`BULUNAMADI`** çıkıyor. Yani kapı, uygulamada **olmayan** bir hatayı
ölçmüş olur; §9'un "yanlış alarm veren kapı, kapı olmaktan çıkar" maddesinin
tam örneği. Gerçek Windows'ta adlar UTF-16, böyle bir çevrim yok.

İlk görülüşü: elle bağlama penceresi ölçülürken liste iki satırı da
`BULUNAMADI` gösterdi; `LANG=C.UTF-8` konunca **`yol BAYAT`** oldu — yani kod
doğruydu, **ölçüm ortamı** yanlıştı. Çalıştırma kapısı artık `LANG`/`LC_ALL`
ayarlıyor.

### Wine'ın ÖLÇMEDİĞİ

- **Segoe UI kurulu değil** → yazı ölçüleri ve hizalamalar Windows'takinden farklı.
- Sekme, kaydırma çubuğu, kenarlık çizimi Wine'ın kendi teması.
- → Wine *"açılıyor mu, çöküyor mu"* sorusunu **kesin** cevaplar;
  *"piksel piksel aynı mı"* sorusunu **cevaplamaz**.

### Windows Çöp Kutusu AĞ SÜRÜCÜSÜNDE YOKTUR

Bir dosyayı `\\sunucu\ortak` üzerinden silmek onu çöp kutusuna **göndermez**;
kalıcı siler. Bu uygulamanın asıl çalışma yeri ağ sürücüsü olduğu için
*"çöp kutusundan geri alınabilir"* demek orada **yalandır** (§3).

→ Silme, Windows kabuğuna değil **kökün içindeki kendi çöp klasörümüze**
taşıyor (`.SwPdmCop`). Aynı disk olduğu için `Directory.Move` anlık — 1 GB'lık
bir montaj kopyalanmıyor — ve davranış her sürücüde aynı. Yan kazanç: Windows
kabuğu çağrısı kalktığı için silme/geri yükleme **Linux'ta test edilebiliyor**
(11 test).

> Kabuğun "Geri Yükle" komutunun adı Windows'un diline göre değişiyor
> ("Restore"/"Geri Yükle"); ona dayanan bir geri yükleme bir makinede çalışıp
> ötekinde çalışmazdı. Kendi klasörümüzde geri yükleme yalnızca bir
> `Directory.Move`.

### Kabuk simgesi: kayıt yoksa hepsi AYNI genel simge

`SHGetFileInfo` + `SHGFI_USEFILEATTRIBUTES` ile uzantıya kayıtlı simge alınıyor —
SOLIDWORKS kurulu makinede **gerçek** simgeler geliyor. Ama kurulu **değilse**
kabuk `.SLDPRT`/`.SLDASM`/`.SLDDRW` için **aynı boş sayfa simgesini** dönüyor;
yani "gerçek simge" isteği, gerçekte üç türü **ayırt edilemez** yapıyor.

→ Gelen simge, kayıtlı olmadığı **kesin** olan uydurma bir uzantının simgesiyle
piksel piksel karşılaştırılır. Aynıysa kayıt yok demektir → çizilmiş yedeğe düşülür.

### Bir konu = bir dosya — "nereye dokunacağım" sorusunun cevabı

v1'in §7'deki hastalığı bir günde olmadı: her özellik kendi kararını
`AnaForm`'a bir parça daha ekleyerek koydu. v2'de karar **konunun kendi
dosyasında** duruyor:

| konuya müdahale | dokunulacak TEK dosya |
|---|---|
| önizleme (kaynak, sıra, mesaj, iş parçacığı) | `Arayuz/Gorunum/Onizleme/Onizleme.cs` |
| arama (ne zaman başlar, gecikme, iptal) | `Arayuz/Gorunum/AramaSurucusu.cs` |
| ağaç (doldurma, süzgeç, arama sonucu) | `Arayuz/Gorunum/AgacDoldurucu.cs` |
| çoklu seçim (Ctrl · Shift · dikdörtgen) | `Arayuz/Gorunum/Agac/SecimliAgac.cs` |
| açık dalların ve seçimin korunması | `Arayuz/Gorunum/Agac/AgacDurumu.cs` |
| sürükleyerek taşıma | `Arayuz/Gorunum/Agac/SurukleBirak.cs` |
| sağ tık menüsü (üretim) | `Arayuz/Gorunum/Islemler/AgacMenusu.cs` |
| **hangi işlemler var, hangi sırada** | `Arayuz/Gorunum/Islemler/AgacIslemleri.cs` |
| yeni klasör · adlandır · sil · taşı | `Islemler/<işlem>.cs` (her biri tek dosya) |
| diskteki dosya işlemleri + hata sebebi | `Cekirdek/DosyaIslemleri.cs` |
| çöp kutusu (sil · listele · geri yükle) | `Cekirdek/Cop.cs` |
| çöp kutusu penceresi | `Arayuz/Gorunum/Islemler/CopKutusuPenceresi.cs` |
| kes · kopyala · yapıştır (pano) | `Arayuz/Gorunum/Islemler/PanoIslemleri.cs` |
| taşıma/kopyalama motoru + onay | `Arayuz/Gorunum/Islemler/TasiIslemi.cs` |
| **ad çakışmasında ne olur** (çekirdek) | `Cekirdek/Cakisma.cs` + `DosyaIslemleri.cs` |
| ad çakışması penceresi | `Arayuz/Gorunum/Islemler/CakismaKutusu.cs` |
| geri alma (yığın + `Ctrl+Z`) | `Arayuz/Gorunum/Islemler/GeriAlIslemi.cs` |
| **sıralama kuralı** (ölçüt, yön, klasörler önce) | `Cekirdek/Siralama.cs` |
| sıralama düğmesi + `Ctrl+Shift+S` | `Arayuz/Gorunum/SiralamaSecici.cs` |
| klasör boyutu hesabı (gezme, iptal) | `Cekirdek/KlasorBoyutu.cs` |
| klasör boyutu işlemi (`Ctrl+Shift+B`) | `Arayuz/Gorunum/Islemler/BoyutHesaplaIslemi.cs` |
| otomatik tazeleme (izleme, gecikme) | `Arayuz/Gorunum/DiskIzleyici.cs` |
| alttaki ilerleme çubuğu | `Arayuz/Gorunum/IlerlemeYuzeyi.cs` |
| **`~$` kilit dosyaları** (gizle · işaretle · kalıntı) | `Cekirdek/Kilit.cs` |
| bir dosya satırının görünüşü (simge, metin, renk, ipucu) | `Arayuz/Gorunum/Agac/DosyaSatiri.cs` |
| çift tıklamayla dosya açma | `Arayuz/Gorunum/DosyaAcici.cs` |
| klasör seçme + son açılanlar | `Arayuz/Gorunum/KokSecici.cs` |
| kabuk kutuları (çalışma klasörünü koruma) | `Arayuz/Gorunum/KabukKutusu.cs` |
| yol çubuğu (ağacın üstü, tıklanabilir) | `Arayuz/Gorunum/YolCubugu.cs` |
| kalıcı ayarlar (son kök, çöp yeri) | `Cekirdek/Ayarlar.cs` |
| Ayarlar sekmesi | `Arayuz/Gorunum/AyarlarSayfasi.cs` |
| alttaki durum yazıları | `Arayuz/Gorunum/DurumCubugu.cs` |
| **SOLIDWORKS dosya kabı** (akışlar) | `Cekirdek/SwPaket.cs` |
| MFC dize biçimi (oku **ve** yaz) | `Cekirdek/MfcDize.cs` |
| **dosyanın içine yazma** (yerinde yama) | `Cekirdek/SwYazici.cs` |
| **ad değişince / taşınınca ebeveynleri onarma** | `Cekirdek/ReferansOnarimi.cs` |
| **bayat yolu gerçek dosyaya bağlama** (toplu + elle) | `Cekirdek/YolBaglama.cs` |
| **referansı elle bağlama** (hangi yol, hangi dosya) | `Arayuz/Gorunum/Islemler/ElleBaglaIslemi.cs` |
| **işlem öncesi/sonrası referans taraması** | `Arayuz/Gorunum/Islemler/ReferansTazeleme.cs` |
| dosyaya yazılacak yolun şekli (uzunluk, göreli) | `Cekirdek/YazilacakYol.cs` |
| onarım onay kutusu | `Arayuz/Gorunum/Islemler/OnarimKutusu.cs` |
| yazma deneyi paketi | `araclar/DeneyUretici/` |
| belgenin **doğrudan referansları** | `Cekirdek/SwReferans.cs` |
| belgenin içindeki önizleme | `Cekirdek/SwOnizleme.cs` |
| belge özellikleri (Malzeme, Kaydeden…) | `Cekirdek/SwBelgeBilgisi.cs` |
| **yazılı yol hangi gerçek dosya** | `Cekirdek/ReferansCozucu.cs` |
| referans indeksi (veri + sorgu) | `Cekirdek/ReferansIndeksi.cs` |
| indeks taraması (artımlı, iptal) | `Cekirdek/IndeksTarama.cs` |
| indeksin diskteki hâli | `Cekirdek/IndeksDosyasi.cs` |
| **hangi raporlar var** | `Cekirdek/Raporlar/Rapor.cs` |
| tek tek raporlar | `Cekirdek/Raporlar/<rapor>.cs` |
| referansların arayüzde görünmesi | `Arayuz/Gorunum/ReferansSurucusu.cs` |
| referans taraması (`Ctrl+Shift+R`) | `Arayuz/Gorunum/Islemler/ReferansTaramaIslemi.cs` |
| rapor penceresi (`Ctrl+Shift+D`) | `Arayuz/Gorunum/Islemler/RaporPenceresi.cs` |
| **taşırken bağımlıları da al** | `Arayuz/Gorunum/Islemler/BagimlilariEkle.cs` |
| **tanınan dosya türleri** | `Cekirdek/DosyaTuru.cs` |
| denetimlerin yerleşimi | `Arayuz/AnaForm.Tasarim.cs` |

`AnaForm` yalnızca **bağlar**: olayları ilgili sınıfa yollar, iş mantığı
bilmez. Ölçüldü: bu ayrımdan sonra `AnaForm.cs` **493 → 160 satır**.

> **Tür kaydı ÖLÇÜLDÜ (27.08.2026).** Önce yeni bir tür **4 dosyada 5 yere**
> satır ekletiyordu (enum · simge sırası sabiti · simge listesi · süzgeç
> listesi) ve iki listenin aynı sırada kalmasını yalnızca **bir yorum satırı**
> sağlıyordu — kaysa hata sessizdi.
>
> Şimdi simge listesi, simge sıraları ve süzgeç şeridi `DosyaTurleri.Tumu`'den
> **türetiliyor**. Ölçüm: çekirdeğe `Step` türü eklendi, **başka hiçbir dosyaya
> dokunulmadı**; Wine'da süzgeç şeridinde `STEP` düğmesi belirdi ve
> `Kaide.STEP` ağaçta kendi sırasıyla çizildi. Sonra geri alındı.

> **Klasör adı ile tip adı çakışırsa derleme kırılır.** `Onizleme/` klasörü
> içindeki `Onizleme` sınıfına ad alanı da verilseydi (`...Gorunum.Onizleme`)
> `Onizleme` adı hem ad alanı hem tip olurdu ve her kullanım belirsiz olurdu.
> Klasör **ad alanı değil**: dosyalar `SwPdm.Arayuz.Gorunum` içinde kalıyor.

### Kapılar

```
araclar/paket.sh               # Erkan'in deneyecegi zip (~120 KB)
araclar/kapilar.sh [--kur]     # dördünü sırayla koşar
├── kapi_boyut.sh              # ağaçtaki her .cs; satır sınırı 600
├── kapi_derleme.sh            # ağaçtaki her .csproj, uyarılar hata sayılarak
├── kapi_test.sh               # ağaçtaki her test projesi; SIFIR test GEÇTİ değildir
└── kapi_calistir.sh [--kur]   # uygulamayı Wine'da GERÇEKTEN açar
```

Kapsam **adlara değil ağaca** bağlı (§9): proje `find` ile, WinExe içerikten
(`OutputType`), test projesi içerikten (`Microsoft.NET.Test.Sdk`), uygulama adı
`runtimeconfig`'ten bulunur. Hiçbirine dosya/proje adı **yazılmamıştır**.

> **Kapı ekran koordinatı kullanıyor — yerleşim değişince kırılır.** Yol
> çubuğu ağacın üstüne eklenince ağaç 26 piksel aşağı kaydı ve dört ölçümden
> üçü birden kırıldı. Bu, kapının **doğru** davranışıdır (ekranı ölçüyor), ama
> onarımı kolay olmalı: bütün koordinatlar artık betiğin başında tek bir
> blokta (`AGAC_ILK_SATIR`, `SUZGEC_Y`…), gövdede çıplak sayı yok.
> Ayrıca kırpma alanı **ağacın içinde** kalmalı: altındaki bölen çizgisi ve
> "Önizleme ve Referanslar" başlığı da bant sayılıp satır sayısını şişiriyor.

**Boyut kapısı §7'nin sayısı için var.** v1'de tek bir arayüz sınıfı 9.918
satıra çıktı, ürün kodunun %38'i oldu ve **bölünemedi**; kimse zamanında
görmedi çünkü bakan bir şey yoktu. Sınır (600) bugünün ölçümüyle seçildi:
27.08.2026'da ağaçtaki en büyük dosya **536** satır. `KAPI_BOYUT_SINIRI` ile
değiştirilebilir ama varsayılan belgeden değil **ölçümden** gelir.

**Çalıştırma kapısı on üç şey ölçer:** süreç ayakta mı · hata akışı temiz mi ·
çökme penceresi var mı · ana pencere doğdu mu · **çoklu seçim çalışıyor mu** ·
**`Ctrl+A` yalnızca bir klasörü mü kapsıyor** · **sıralama gerçekten sıralıyor
mu** · **tür süzgeci gerçekten süzüyor mu** · **`Ctrl+Z` geri alıyor mu** ·
**önizleme dosyadan çıkıyor mu** · **referans listesi doluyor mu** ·
**referanslarda yön ayrımı duruyor mu** · **`~$` kilit dosyaları gizlenip
sahibi işaretleniyor mu**.
Ekran görüntüsünü `.kapi/ekran.png` olarak bırakır; CI'da yapıt olarak saklanır.

> **Onuncusu neden var (önizleme):** bu alan **bugüne kadar hiç ölçülemedi**.
> Önizleme yalnızca Windows kabuğundan geliyordu; Wine'da kabuk sağlayıcısı
> yok, SOLIDWORKS kurulu olmayan Windows'ta da `.SLDPRT` için resim gelmiyor.
> Yani "önizleme boş" hatası sessizce pakete girebilirdi — boş bir kutu,
> hiçbir sebep. Artık dosyanın kendi `PreviewPNG` akışı okunuyor ve burada
> ölçülebiliyor.
> **EŞİK ÖLÇÜLEREK SEÇİLDİ ve ilk hâli YAKALAMADI:** eşik 500'dü, kaynak
> devre dışı bırakılınca kapı yine "EVET" dedi — çünkü *"Önizleme yok"*
> yazısı da 612 piksel üretiyor. Renk sayısı da ayırt etmedi (yazı kenar
> yumuşatmayla 62 renk veriyor). Ayıran tek şey yirmi katlık fark: gerçek
> önizleme **12665**, yazı **612**. Eşik 3000'e çekilince YAKALADI.

> **ODAK: `Control.Focus()` PENCERE GORUNMEDEN CALISMIYOR.** Kisayollar
> `_agac.Focused` sartina bagli; kok acilista `OnLoad` icinde aciliyor ve
> orada verilen odak SESSIZCE kayboluyordu — `Focus()` hicbir sey yapmadan
> `false` donuyor. Belirti sinsi: uygulama aciliyor, her sey normal
> gorunuyor, ama **hicbir kisayol calismiyor**; once agaca tiklamak
> gerekiyor. Hata yok, sebep yok. Odak `OnShown`'a tasindi.
> (Bu, §6'daki "devre disi birakilan denetim odagi kaybediyor" maddesinin
> kardesi: odak her zaman **elle** ve **dogru anda** verilir.)

> **On birincisi neden var (referans listesi):** bu uygulamanın varlık sebebi.
> Liste sessizce boş kalırsa kullanıcı "bu parçayı kimse kullanmıyor" sanıp
> **siler** (§3). Ölçüm SAYIYLA olmuyor: liste tarama öncesi de boş değil,
> içinde *"Bilinmiyor / taranmadı"* satırı duruyor. Ayırt eden şey içeriğin
> **değişmesi** — aynı kırpmanın parmak izi alınıyor, `Ctrl+Shift+R`
> öncesi ve sonrası farklı olmalı.
>
> **ÖLÇÜMÜN VARSAYIMI BAYATLADI — ve bunu KAPI söyledi (28.08.2026).**
> Ölçüm önce *"Ctrl+Shift+R'dan önce ve sonra iz değişti mi"*ydi. Tarama
> **her işlemden önce kendiliğinden** koşmaya başlayınca indeks tuşa
> basılmadan dolar oldu, iz değişmedi ve kapı **HAYIR** dedi. Kapı doğru
> davrandı: kırılan kod değil, ölçümün **varsayımı**ydı. Yeni ölçüm
> varsayımsız — *referansı olan* bir dosya ile *referansı olmayan* bir
> dosyanın listesi **farklı** olmalı. Liste hiç dolmazsa ikisi de boş çıkar
> ve yakalanır (§9 döngüsüyle doğrulandı).
>
> **KAPI YİNELENEBİLİR DEĞİLDİ — ve bunu ancak ikinci koşu gösterdi.**
> Uygulama indeksi `%APPDATA%`'ya yazıyor; önceki koşudan kalan indeks
> yüzünden ikinci koşu daha baştan "taranmış" hâlde açılıyor ve ölçüm aynı
> şeyi iki kez ölçüyordu. Kapı artık başlarken o klasörü siliyor.

> **On ikincisi neden var (yön ayrımı):** referans listesi iki ayrı soruya
> birden cevap veriyor — *bu dosya neyi kullanıyor* (aşağı) ve *bu dosyayı kim
> kullanıyor* (yukarı). 28.08.2026'ya kadar ikisini ayıran tek şey rol
> sütunundaki **bir ok işaretiydi** (`kullanıyor →` ile `kullanıyor`) ve Erkan
> gerçek veride okuyamadı. Aynı ad **iki bölümde birden** çıkabiliyor ve bu
> hata değil: montaj bağlamında (in-context) yapılmış bir parça montajı
> referans verir, montaj da o parçayı — ilişki gerçekten çift yönlü. Yönü
> karıştırmak bu üründe tehlikeli: yanlış yönü okuyan kullanıcı *"bunu kimse
> kullanmıyor"* sanıp **sağlam dosya siler** (§3).
>
> **ÖLÇÜLDÜ — 11. ölçüm bunu YAKALAMIYOR.** §9 döngüsünde iki
> `liste.Baslik(...)` çağrısı bilerek silindi: kapı *"[11/12] referans listesi
> …… EVET"* dedi, çünkü onun ölçtüğü şey içeriğin **değişmesi** ve başlıklar
> gitse de içerik yine değişiyor. Ayırt eden tek şey başlığın **zemin rengi**
> (`#E4EAF1`, o panelde başka hiçbir şeyde yok): o renkten kaç ayrı **bant**
> çıktığı sayılıyor — çoklu seçim ölçümündeki tekniğin aynısı. TEMİZ (2 bant)
> → başlıklar silinince **YAKALADI** (0 bant) → geri konunca TEMİZ.
>
> **Başlıktaki sayı sağ sütuna alındı — bu da ölçülerek.** Önce başlık tek
> metindi (`▼ KULLANDIKLARI — 9 dosya`); dar pencerede tam da **sayı**
> kırpılıyordu: `▼ KULLANDIKLARI …`. Ad sütunu solda, sayı sağ sütunda
> durunca her genişlikte görünüyor.

> **On üçüncüsü neden var (kilit dosyaları):** SOLIDWORKS her açtığı belge
> için klasöre gizli bir `~$<ad>` dosyası yazıyor (§5) ve bunlar ağaçta
> gerçek dosyalarla yan yana duruyordu. **Ama körlemesine gizlemek yanlış
> olurdu ve bu bilinçli bir karardı** — Windows bir klasörü sildirmiyorsa
> sebep çoğu zaman tam da o görünmeyen dosyadır (§4). Kural bu yüzden iki
> yanlı: sahibi **varsa** kilit gizlenir ve sahibi *"açık"* işaretlenir,
> sahibi **yoksa** görünür kalır. `Kilit.Coz` bunu klasör bazında yapıyor;
> arama sonucu birden çok klasörü kapsadığı için yalnızca ada bakan bir
> eşleşme yanlış dosyayı gizlerdi.
>
> **ÖLÇÜM SAYIYLA OLMUYOR:** kilit zaten gizli olduğu için satır sayısı
> değişmiyor — bu bilerek böyle, örnek klasördeki çift kök seviyesine
> konuldu ki 5/6/8/9. ölçümlerin tabanları kaymasın.
>
> **İLK HÂLİ ÖLÇEMEDİ ve sebebi ClearType.** İşaret önce yalnızca **yazı
> rengiyle** (`#A64B00`) veriliyordu; kapı o rengi aradı ve **sıfır** buldu,
> oysa yazı ekranda turuncuydu. Sebep alt-piksel çizim: metnin hiçbir
> pikseli saf renge eşit çıkmıyor, kenarlar `#C3543C`, `#B66A79` gibi
> kaymış tonlara dönüyor. **Dolu bir dikdörtgen tam renk veriyor** — o
> yüzden işaretli satıra ayrıca bir **zemin rengi** (`#FFE3C8`) kondu;
> referans bölüm başlıklarındaki tekniğin aynısı. Yan kazanç: ekranda da
> daha okunur.
>
> Ölçüm **hiçbir şey seçili değilken** alınan görüntüden yapılıyor
> (`ilk.png`): seçim boyası satırın zeminini örter ve ölçüm boş dönerdi.
> §9 döngüsü: TEMİZ (1 bant) → `Kilit.Coz` etkisiz bırakılınca **YAKALADI**
> (0 bant) → geri konunca TEMİZ.

> **Yedincisi neden var (sıralama):** sıralamanın kendi menüsü Wine'da
> **çökertiyor** (yukarıdaki `ContextMenuStrip` maddesi), yani menü yoluyla
> hiç ölçülemez. Aynı kodu çağıran `Ctrl+Shift+S` ölçülüyor. Ölçüm **sayıyla
> olmuyor** — sıralama satır *sayısını* değil *sırasını* değiştirir; bu yüzden
> **parmak izi** (aynı kırpmanın md5'i) alınıyor: bir basış → iz **değişmeli**;
> yedi basış daha (dört ölçüt × iki yön = sekiz hâl, başa döner) → iz **ilk
> izle aynı** olmalı. İkinci koşul şart: yalnızca "değişti" demek, düğmenin
> *etiketi* değiştiği için de sağlanabilirdi.
>
> **İLK HÂLİ DUYARSIZDI — ve bunu ancak ölçüm gösterdi.** Kırpma bütün ağacı
> kapsıyordu. `Siralama`'daki gerçek hata (aşağıda) bilerek geri konulup kapı
> koşuldu: kapı **"TEMİZ" dedi.** Sebep, hatanın yalnızca *dosyaları*
> etkilemesi; klasörler ayrı bir yoldan sıralanıyor ve onlar hâlâ ters
> dönüyordu, yani iz yine değişiyordu. Kırpma **dosya satırlarına** indirilince
> aynı bozuk yapı **YAKALANDI** → düzeltilince yine TEMİZ.
> Ders §9'un kendisi: *bir kapı, yazıldığı anda inert olabilir ve hep "TEMİZ"
> der; ancak bilerek bir ihlal konunca anlaşılır.*
>
> **Yakalanan gerçek hata:** `Siralama`'da *ad* ölçütünde karşılaştırma her
> zaman `0` dönüyordu ve yön **hiç uygulanmıyordu** — düğmede `Ad ↓` yazıyor,
> ağaç artan duruyordu. Hiçbir istisna yok, yalnızca yanlış sıra. Birim testi
> de aynı hatayı yakalıyor (`Ad_AZALAN_TERSINE_DIZER`); kapı **bağlantıyı**,
> test **karşılaştırıcıyı** koruyor.

> **Satır sayacı ÖLÇÜLEREK düzeltildi (27.08.2026).** `agac_satir_say`
> noktalı bağlantı çizgilerinin her noktasını **ayrı satır** sayıyordu: aynı
> ağaç 12 yerine **17** görünüyordu. Hata o güne kadar ortaya çıkmadı çünkü
> ölçüm hep `Ctrl+A`'dan **sonra** alınıyordu ve seçim boyası noktaları
> örtüyordu — yani sayı doğru değildi, **doğru görünüyordu**. Bant en az
> 3 piksel şartı kondu; metin satırları ~9 piksel, noktalar 1. Bugünkü
> gerçek sayılar: ağaç **12**, "Parça" süzgeciyle **8**.

> **Sekizincisi neden var:** geri alma **dosya siliyor**. Sessizce bozulursa
> kullanıcı "geri aldım" sanıp devam eder. Kapı `Ctrl+Shift+N` ile ağaca bir
> satır ekletir, `Ctrl+Z` ile geri aldırır; ikisini de sayar (11 → 12 → 11).
> §9'a göre eklendi: TEMİZ → geri alma etkisiz bırakılınca **YAKALADI**
> (11 → 12 → 12) → geri alınca TEMİZ. (Sayaç düzeltilince aynı ölçüm
> 8 → 9 → 8 okuyor; ölçülen davranış aynı.)

> **Altıncısı neden var:** `Ctrl+A` bütün ağacı seçiyordu ve bu bir rahatsızlık
> değil **tehlikeydi** — ardından `Delete`, kullanıcı bir klasörü temizlediğini
> sanırken **kökün tamamını** çöpe atardı (§1a). Kapı köke tıklayıp `Ctrl+A`
> basar ve seçili satırın **görünen satır − 1** olduğunu ölçer (kökün kendisi
> seçime girmemeli). §9'a göre eklendi: TEMİZ (11/12) → eski davranış geri
> konunca **YAKALADI** (12) → geri alınca TEMİZ.

> **Yedincisi neden var:** süzgeç düğmesinin `Click` bağlantısı bir tanı
> temizliğinde silindi (§8) ve **kimse görmeden pakete girdi** — bakan bir şey
> yoktu. Kapı "Parça"ya tıklar ve ağaçtaki satır sayısının **azaldığını**
> ölçer. §9'a göre eklendi: TEMİZ → o satır tekrar silinince **YAKALADI**
> (15 → 15, süzülmedi) → geri konunca TEMİZ (15 → 11). (O günkü sayılar
> düzeltilmemiş sayaçtan; bugün aynı ölçüm 12 → 8.)

> **Beşincisi neden var:** çoklu seçim WinForms'ta yok, elle yazıldı (§6) ve
> arayüz kodu olduğu için **birim testi yazılamıyor**. Tek ölçüm yolu gerçek
> tıklama: `xdotool` ile `Ctrl`+tık atılır, seçili satır sayısı ekran
> görüntüsünden **sayılır** (seçim rengi taşıyan piksellerin kaç ayrı *bant*
> oluşturduğu — piksel *sayısı* işe yaramaz, satır genişliği ada göre değişir).
> §9'a göre ölçülerek eklendi: TEMİZ → `Ctrl`+tık tek seçim gibi davranınca
> **YAKALADI** (2 yerine 1) → geri alınca TEMİZ.

> **Derleme kapısının GÖRMEDİĞİ sınıf vardır.** 27.08.2026'daki kurucu hatasında
> derleme "0 uyarı 0 hata" diyordu ve uygulama hiç açılmıyordu. Çalıştırma kapısı
> tam olarak bunun için var. **Yeşil derleme, çalışıyor demek değildir.**

Bu maddelerin numaraları kapının **çıktısındaki** sıradır; sıralama ölçümü
yedinci olarak araya girdiği için süzgeç sekizinci, geri alma dokuzuncu oldu.

Dördü de §9'a göre ölçülerek eklendi — TEMİZ → hata konunca YAKALADI → geri
alınca TEMİZ. İlk üçünün yakaladıkları gerçek hatalardı:
`ToolStripLabel.Refresh()` (§6), sürücü kökünde kırpılan ters bölü (§4),
kurucudan çağrılan `OnResize` (§6). Boyut kapısı ölçülürken ağaçta **olmayan**
bir klasöre (`src/YeniProje/Alt/`) 601 satırlık dosya konuldu ve yakalandı —
kapsamın ada değil ağaca bağlı olduğu böyle görüldü; 600 satırda **yakalamadı**
(sınır doğru yerde), dosya silinince yine TEMİZ.

CI (`.github/workflows/kapilar.yml`) **aynı betikleri** koşar — ikinci kopya yok
(§8). Üç iş: Linux derleme+test · Windows derleme+test (gerçek SDK) · Wine
çalıştırma. Boyut kapısı ayrı bir iş **değil** — işletim sisteminden bağımsız ve
bir saniye sürüyor; Linux işinin ilk adımı olarak koşuyor.

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

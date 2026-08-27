# CLAUDE.md — SW PDM v2 Çalışma Kuralları

Bu depo **SW PDM v2**: SOLIDWORKS dosyalarını taşırken/adlandırırken montaj ve
teknik resim referanslarını koruyan bağımsız bir masaüstü uygulaması.

**v1 `erkancam01/swpdm` deposunda ve BİTTİ.** Orada dokunulmuyor. Bu dosya
v1'in 160+ commit'lik geçmişinden **ölçülerek** çıkan derslerin devri; v1'in
kendi CLAUDE.md'si o depoda duruyor ve ayrıntı gerekirse oraya bakılır.

> **Bu dosya KISA başlıyor ve bilerek.** v1'inki 500 satırdı çünkü her satırı
> yaşanmış bir hatadan doğdu. Buraya yalnızca **v1'de ÖLÇÜLMÜŞ** olanlar
> geldi. Yeni bir madde ancak yeni bir hata yaşandığında eklenir — tahminle
> değil.

---

## 0. KAPSAM HENÜZ BELİRLENMEDİ

Erkan v2'nin neyi kapsayacağını **anlatacak**. O gelene kadar bu depoda
**ürün kodu yazılmaz** — yalnızca kurallar ve kapılar var.

Karara bağlanmış tek şey: **SOLIDWORKS eklentisi (Görev Bölmesi paneli)
v2'de ŞİMDİLİK YOK.** Sebebi §4'te, ölçüsüyle.

---

## 1. Altın kural

**Çalışan hiçbir şeyi bozma.** Bu bir CAD dosya yöneticisi: hatalı bir
değişiklik gerçek montaj ve teknik resimlerin referanslarını kırar, en kötü
ihtimalle dosya kaybettirir.

## 2. Dal ve commit

Çalışma dalı **`main`**. Commit mesajları Türkçe ve açıklayıcı: **ne**
değiştiğini değil **neden** değiştiğini yazar.

`git push --force`, `git rebase`, geçmişi yeniden yazma **YOK**. v1'de
160 commit'lik geçmiş "bu kod neden böyle" sorusunun tek cevabıydı ve
defalarca tur kazandırdı.

Depo **private**.

---

## 3. ÖLÇ, TAHMİN ETME — v1'in en pahalı dersi

Doğrulanmamış bir SOLIDWORKS API varsayımı v1'de **üç kez** tur yedirdi
(`ReplaceComponents2`, `ReplaceViewModel` iki kez).

- **Yeni bir SOLIDWORKS API'sine dayanmadan önce bir "Kapı" tanı düğmesi
  yaz:** geçici klasörde kendi test dosyasını üretir, çağrıyı yapar, sonucu
  **doğrular**, temizler, raporlar. Kapı geçmeden asıl özellik yazılmaz.
- **İmzasından emin değilsen yansımayla çağır** ve tutan varyantı raporla.
- **API'nin dönüş değerine GÜVENME.** `IDrawingDoc.ReplaceViewModel` `true`
  döndü ve **hiçbir şey yapmadı**. Başarı bir iddia değil, bir **ölçüm**:
  işlemden sonra sonucu diskten ya da bellekten yeniden oku ve karşılaştır.
- **Belirtiyi tek sebebe bağlama.** "Klasör silinmiyor" hatası dört tur yedi
  çünkü aynı belirtinin **iki ayrı sebebi** vardı. Ucuz iki hipotezi
  seçmek yerine ikisini birden kapat.

### v1'de ÖLÇÜLEN ve v2'de geçerli olan tek somut SOLIDWORKS gerçeği

**Kapı 12 (27.08.2026, Erkan'ın makinesi) — klasör taşınınca İÇ referanslar
YAŞIYOR.** Bir montaj + aynı klasördeki iki alt montajı geçici klasöre
kopyalandı, klasör adı `Directory.Move` ile değiştirildi, `GetDocumentDependencies2`
okundu: SOLIDWORKS çocukları **YENİ** klasörde buldu.

Beklenenden güçlü sonuç: dosyanın içinde yazan eski yol o an **hâlâ
geçerliydi** ve SOLIDWORKS yine **yanındaki kopyayı** seçti. Yani *"ebeveynin
yanındaki dosya"* kuralı, yazılı mutlak yolun **önüne geçiyor**.

**Sonucu:** bir klasör taşınırken yalnızca **DIŞARIDAN verilen referanslar**
kırılıyor → `Directory.Move` + yalnızca dış ebeveyn onarımı meşru bir hızlı yol.

> **HENÜZ ÖLÇÜLMEDİ — teknik resim → model.** Kapı montaj→montaj zincirinde
> koştu. Teknik resimlerin model referansı bu kuralı izliyor mu **bilinmiyor**.
> Ölçülmeden hızlı yol teknik resimleri kapsamaz.

---

## 4. v1'den ÖLÇÜLMÜŞ MİMARİ dersler

Bunlar fikir değil, sayılmış sonuçlar. v2'nin var oluş sebebi büyük ölçüde bunlar.

### 4.1 Asıl iş, kodun küçük bir parçasıydı

v1'de ürün kodu **15.231 satır**dı. Ölçüldüğünde:

| ne | satır | pay |
|---|---:|---:|
| **Referans bağını koru** (indeks, taşıma/ad planı, onarım, SW bağlantısı, yedek) | 2.769 | **%18** |
| Arayüz — dosya yöneticisi kabuğu | 7.113 | %47 |
| gerisi (köprü, varyant, çöp, önizleme, rapor, ayarlar…) | 5.349 | %35 |

**Ders:** uygulamanın adı "referans koruyucu" ama kodun %82'si onun etrafına
kurulmuş bir dosya yöneticisi. v2'de her özellik, çekirdeğe ne kattığıyla
hesap verir.

### 4.2 Tek bir sınıf ürün kodunun %38'i oldu

`PdmPaneli.cs` **9.918 satır**a çıktı; depodaki ikinci en büyük dosya 526
satırdı — yani **on kat** fark. Bölünemedi, çünkü SOLIDWORKS eklentisi onu
barındırıyordu ve `internal` yüzeyini bölmek o yüzeyi bir sözleşmeye
çevirirdi.

**Kural:** bir arayüz sınıfı hem ekranı hem iş akışlarının sürücüsü olmaz.
Akışlar test edilebilir katmanda durur; arayüz onları **çağırır**.

### 4.3 Eklenti (panelin SOLIDWORKS içinde çalışması) en pahalı karardı

Eklenti **üç kez** yazıldı. Panel SOLIDWORKS'ün içindeyken dosya taşırken /
ad değiştirirken SOLIDWORKS **kendini kapatıyordu**. Dört tur boyunca dört
**ayrı** sebep bulundu (`Application.DoEvents` ×18 · olay işleyicisinden sızan
istisna · ekranda duran GDI+ resminin `Dispose` edilmesi · SOLIDWORKS'ün
klasör tutuşu) ve her seferinde "bu sonuncusu" sanıldı. **Elimizde hiçbir
zaman "kalan sebep yok" diyen bir ölçüm olmadı.**

Çözüm tehlikeli işi ayrı sürece taşımaktı — ve o köprü (istek/yanıt dosyaları,
bekleyici, yürütücü, kalp atışı, gizli pencere) **kendi hata sınıfını** doğurdu.
Son iki hata da köprü hatasıydı:

- `.exe` şeridine yazmanın iki yolu vardı; biri köprüden **geçmiyordu** →
  eklenti dakikalarca bayat metinde asılı kaldı.
- Gizli `.exe`'de açılan bir bilgi kutusu paneli **sonsuza kadar** bekletti:
  kutu mesaj kuyruğunu pompalıyor → kalp atışı sürekli yazıyor → sessizlik
  saati sürekli sıfırlanıyor → zaman aşımı bile **oluşmuyor**.

**v2 kararı:** eklenti şimdilik yok. Yalnızca `.exe`. Köprünün tamamı
yazılmaz. Gerekirse sonra ve **ölçerek** eklenir.

**Bunun ikinci bir kazancı var ve büyük:** eklenti olmayınca net472 zorunlu
değil. Proje bu ortamda **derlenebilir ve testleri koşabilir** — v1'de
"ölçülemeyen" alanın çoğu buradan geliyordu.

### 4.4 Ortak mantığı iki kez yazma

v1'de "yolun son parçası" mantığı **dokuz** ayrı yerde elle yazılmıştı ve üç
ayrı boyutta ayrışmıştı: üçü boş girdide `NullReferenceException` atıyordu,
bir kısmı sondaki ayırıcıyı kırpmıyordu, ikisi `/` tanımıyordu. Boyut
biçimlendirmesi üç yerdeydi ve biri **farklı sayı** gösteriyordu — aynı dosya
iki ekranda farklı boyutta görünüyordu.

**Kural:** aynı mantığın ikinci kopyası yazılmaz. Tek kaynak + CI kapısı.

---

## 5. Dürüstlük kuralları — kullanıcı buna bakıp dosya SİLİYOR

- **Boş liste "yok" demek DEĞİLDİR.** Referans indeksi yalnızca taranmış
  kökleri bilir. Taranmamış bir klasörde sorgu boş döner; bunu "bu parçayı
  kimse kullanmıyor" diye göstermek sağlam dosyaların silinmesine yol açar.
  Tarama yapılmadıysa **hiçbir sayı ve hiçbir liste gösterilmez**, sebebi yazılır.
- **Kısmi başarısızlıkta eski dosyayı KORU.** Akış `KOPYALA → ONAR → SİL`;
  bir onarım başarısızsa kaynak silinmez. `File.Move` yerine bu sıra seçilir.
- **İndekse yalan yazma.** v1'de başarısız bir onarımdan sonra indeks
  "düzeldi" diye güncellendi ve teknik resim ilişkisi sessizce kayboldu.
- **Hata sebebini EKRANDA göster**, yalnızca günlüğe yazma.
- **Ekranda donmuş/uydurma ilerleme gösterme.** Sayılabilir bir ilerleme yoksa
  yüzde uydurma; animasyon işletilemiyorsa hiç gösterme. Bir işlemin
  yapılıp yapılmadığını **bilmiyorsak** "yapılmadı" demek yalandır — kullanıcı
  aynı işlemi ikinci kez yapar.
- **Sessiz başarı ve sessiz askıda kalma YASAK.** Her istek bir yanıt alır,
  her terminal hâl sebebini yazar.

---

## 6. Platform tuzakları — hepsi ÖLÇÜLDÜ

### `System.IO.Path`'in yol parçalama üyeleri Linux'ta Windows yolunu YANLIŞ parçalıyor

```
Path.GetExtension(@"C:\Proje 2.0\parca")  ->  ".0\parca"     (dogrusu "")
Path.GetFileName(@"C:\a\b.SLDPRT")        ->  yolun TAMAMI    (dogrusu "b.SLDPRT")
Path.GetDirectoryName(@"C:\a\b.SLDPRT")   ->  ""              (dogrusu "C:\a")
```

Linux'ta `\` ayırıcı **sayılmıyor**. Testler Linux CI'da koşuyorsa test
**yanlış sonucu doğrular** ve hata kullanıcının makinesine kalır.

→ `GetFileName` · `GetFileNameWithoutExtension` · `GetExtension` ·
`GetDirectoryName` **çekirdek katmanda yasak** (`csdenge.py`, `CORE_YASAK`).
Kendi yol yardımcın olacak.

> **Windows'a özgü katmanda yasak DEĞİL** ve bu bilerek: orada `Path` doğru
> çalışıyor ve `Path.Combine("C:", "x")` sürücüye **göreli** `"C:x"` üretiyor —
> yani kendi yardımcına geçmek orada davranışı değiştirir.

### Diğerleri

- **`Path.GetInvalidFileNameChars()` Linux'ta yalnızca `/` ve `\0` döndürür.**
  Windows'ta geçersiz olan bir adı testler kabul eder. Geçersiz karakter
  listeleri **elle** yazılır.
- **`.bat` dosyaları iki ayrı şekilde SESSİZCE ölüyor** — ikisinde de görülen
  şey aynı: *pencere açılıyor, hiçbir şey yazmadan kapanıyor.*
  1. **CRLF şart.** LF'e düşen bir `.bat`'ı `cmd.exe` yarıda kesiyor.
     `.gitattributes`'ta `*.bat -text`.
  2. **Blok içinde kaçışsız parantez.** `if ... ( … )` içinde geçen `(`/`)` —
     **tırnak içinde bile** — cmd'nin ayrıştırıcısını yanıltıyor. Doğrusu
     `^(` / `^)`, ya da blok yerine `goto`.

  İkisi de `tools/bat_kapisi.py` ile yasak.
- **`.bat` çıktısı bir dosyaya da yazılmalı.** Pencere kapandığında
  kullanıcının elinde hiçbir kanıt kalmıyordu.
- **Kabuk dosya iletişim kutuları sürecin çalışma klasörünü kaydırıyor** ve o
  klasör bir daha silinemiyor. `RestoreDirectory = true` + kutu kapandıktan
  sonra çalışma klasörünü sabitle.
- **SOLIDWORKS'ün de kendi çalışma klasörü var** (`Get/SetCurrentWorkingDirectory`).
  `OpenDoc6` onu dosyanın klasörüne kaydırıyor ve orada bırakıyor; `CloseDoc`
  geri almıyor. "Bütün belgeleri kapattım ama klasör hâlâ kilitli"nin sebebi buydu.
- **Windows bir klasörü ÜÇ ayrı sebeple sildirmiyor** ve üçünün çözümü farklı:
  `145 ERROR_DIR_NOT_EMPTY` (içinde bir şey var — **gizli** dosyalar dahil) ·
  `32 ERROR_SHARING_VIOLATION` (açık tutamak) · `5 ERROR_ACCESS_DENIED`
  (salt-okunur ya da izin). `ex.Message` (yerelleştirilmiş metin) bunları
  **ayırt edemiyor** — Win32 kodunu oku.
- **En olası sebep SOLIDWORKS kilit dosyaları:** her açılan belge için aynı
  klasöre `~$Parca1.SLDPRT` adlı **gizli** bir dosya yazılıyor, temiz
  kapanmazsa geride kalıyor. Kullanıcı Gezgin'de göremiyor, Windows "dizin boş
  değil" diyor.
- **Kabuk önizleme sağlayıcıları STA ister.** `ThreadPool` (MTA) içinden
  çağırınca `E_FAIL` (0x80004005).
- **SOLIDWORKS kapatılan belgeyi oturumda tutuyor** (görünmez belge). Açtığımız
  belgeleri kapatırken kullanıcının kendi açtıklarına dokunma: yalnızca
  görünmez **ve** kaydedilmemiş değişikliği olmayanlar kapatılır.
- **Diskteki `ReadOnly` bitini kaldırmak, AÇIK bir belgenin oturum içi
  durumunu değiştirmiyor** — SOLIDWORKS onu açılışta önbelleğe alıyor.

---

## 7. SOLIDWORKS COM tuzakları

### CS0104 — takma ad ŞART

`SolidWorks.Interop.sldworks` kendi `Environment`, `View`, `Timer`,
`Application`, `Color`, `Point`, `Component`, `Attribute`, `Feature`
tiplerini tanımlıyor. Bu adları çıplak kullanmak derlemeyi kırıyor.

```csharp
using Environment = System.Environment;
```

Tek tek `System.` öneki eklemek **YETMEZ** — bugünü düzeltir, o dosyaya
yazılacak bir sonraki kullanım hatayı geri getirir. v1'de `Environment` tam
bu yüzden **aylarca** her pakette elle düzeltildi.

**Bunun v2'deki asıl karşılığı:** interop'a dokunan dosya sayısını **az**
tut. v1'de tüm SOLIDWORKS interop'u dört dosyadaydı ve 9.918 satırlık panel
bu riskin tamamen dışındaydı — bu, tesadüf değil karardı.

### `GetType()` — iki ayrı tuzak, biri SESSİZ

1. **Derleme kırar:** interop arayüzlerinin KENDİ `GetType()` üyesi var.
   `IModelDoc2.GetType()` belge türünü **`int`** döndürüyor ve
   `object.GetType()`'ı gölgeliyor.
2. **Derlenir ama HER ZAMAN yanlış çalışır:** bir COM sarmalayıcısında
   `GetType()` **`System.__ComObject`** döndürüyor; o tipte arayüzün hiçbir
   üyesi yok, yani `.GetType().GetMethod("X")` **her zaman `null`**. Kod
   derlenir, çalışır ve *"üye bulunamadı"* raporlar — hiç denemeden. v1'de bu
   hata **iki kez** yapıldı.

```csharp
// YANLIS: __ComObject -> her zaman null
belge.GetType().GetMethod("IsOpenedReadOnly");

// DOGRU: derleme-zamani arayuz tipi
typeof(ModelDoc2).GetMethod("IsOpenedReadOnly");
```

İki incelik:
- **`Type.GetMethod` bir ARAYÜZ tipinde miras alınan üyeleri DÖNDÜRMÜYOR**
  (sınıflardan farklı) — tip + `GetInterfaces()` birlikte taranmalı.
- **Arayüz adını metin olarak yazma, `typeof(T)` geçir.** Var olmayan bir tip
  adı yazmak v1'de iki kez derlemeyi kırdı; jenerik parametre bunu yapısal
  olarak imkânsız kılıyor.

`tools/interop_denetim.py` `.GetType().GetMethod|GetProperty|…` kalıbını
CI'da **yasaklıyor**.

---

## 8. Kapılar

```
python3 tools/csdenge.py          # denge, CS0128, CS0111, cift bildirim, sarkan belge, Core/Path
python3 tools/interop_denetim.py  # CS0104 ad cakismalari + COM yansimasi
python3 tools/sozdizim.py         # GERCEK ayristirici (tree-sitter) - dosya gecerli C# mi
python3 tools/bat_kapisi.py       # .bat SESSIZ olum sebepleri (CRLF, parantez)
```

`sozdizim.py` `pip install tree_sitter tree_sitter_c_sharp` ister ve kurulu
değilse **atlamıyor, hata veriyor** — kurulu olmayan bir kapıyı "geçti" saymak
kapıyı anlamsızlaştırır.

### v1'den TAŞINMAYAN iki kapı — ve neden

| kapı | neden gelmedi |
|---|---|
| `eklenti_kapisi.py` | İşi köprü uçlarını denetlemek ve `Application.DoEvents`'i yasaklamaktı. Eklenti yok → köprü yok. **`DoEvents` yasağı da kalktı**: o, panelin SOLIDWORKS sürecinde çalışmasından doğuyordu; kendi `.exe`'mizde v1'in kendi belgesi onu **zararsız** diyor. Eklenti geri gelirse kapı da geri gelir. |
| `sonda_derle.py` | net472'yi Linux'ta derletmek için vardı. Eklenti yoksa net472 zorunlu değil ve `dotnet build` gerçek derleyiciyi zaten doğrudan çalıştırıyor — vekil gerekmiyor. |

### Kapı disiplini — v1'de iki kez öğrenildi

- **Yanlış alarm veren bir kapı, kapı olmaktan çıkar.** v1'de genel bir
  "bildirilmemiş ad" denetimi denendi: 125 dosyada 129 bulgunun **tamamı**
  yanlış alarmdı (lambda parametreleri, adlandırılmış argümanlar, `nameof`,
  `using` ad alanları). Eklenmedi. Aynı gerekçeyle bir DDL tırnak sezgiseli de
  eklenmedi.
- **Bir kapı ölçülerek eklenir:** gerçek depoda TEMİZ · hata geri konunca
  YAKALIYOR · geri alınca yine TEMİZ. Bu üçü gösterilmeden kapı eklenmez.
  v1'de bir kapı yazıldığı anda **inert**ti (yanlış metni okuyordu) ve hep
  "TEMİZ" diyordu; ancak bilerek bir ihlal koyunca anlaşıldı.
- **Kapıların kapsamı ADLARA değil AĞACA bağlı.** v1'de bir kapı iki proje
  adına bakıyordu; üçüncü bir proje eklenseydi sessizce atlanırdı. Buradaki
  sürümler `src/` altını tarıyor.

---

## 9. Kod SİLERKEN — sarkan ad kuralı

v1'de bir dosyadan 2.546 satır silindi, üç denetim de TEMİZ dedi ve derleme
yine de kırıldı: `CS0103 'rev00' adı geçerli bağlamda yok`. Silinen satır bir
**yerel değişken bildirimi**ydi; sarkan-referans taraması yalnızca metot ve
alan adlarına bakıyordu.

**Kural: bir satırı silmeden önce o satırın BİLDİRDİĞİ her adı ara.** Yalnızca
çağırdığı şeyleri değil — `=` solundaki adı da.

### Bir üyeyi çağırmadan önce alanın BİLDİRİLEN tipine bak

v1'de 18 çağrı toplu değiştirildi, üç denetim TEMİZ dedi, derleme **17
hatayla** kırıldı: `ToolStripStatusLabel` bir `ToolStripItem`'dır, **`Control`
değil** — `Refresh`/`Invalidate`/`Focus`/`Controls`/`Parent` yok. WinForms'ta
en sık tuzak bu ikili; taşıyıcıya `Owner` / `GetCurrentParent()` ile çıkılır.

**Toplu değişiklikte alıcının tipini VARSAYMA — bildirimini ara.**

### Verbatim string (`@"..."`) içine ASLA çift tırnak yazma

SQL DDL blokları verbatim string ise oradaki bir `"` stringi **o noktada
bitirir** (kaçış `\"` değil `""`). v1'de bir SQL yorumuna tırnaklı bir cümle
yazmak **230 derleme hatası** üretti ve iki statik denetim de TEMİZ dedi —
stray tırnaklar çift sayıda olduğunda süslü parantez dengesi bozulmuyor.

**Kural: DDL yorumlarında tırnak kullanma.** Vurgu gerekiyorsa BÜYÜK HARF.

---

## 10. Bitirmeden önce

- Dört kapı TEMİZ
- Çekirdeğe dokunduysan testleri **çalıştır** — sayıyı bu dosyadan okuma
- **Test ETMEDİĞİN ve riskli noktaları açıkça yaz.** "Oldu" deyip geçme.

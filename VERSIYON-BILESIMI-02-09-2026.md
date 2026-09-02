# Versiyon kurgusu — iki aşama (02.09.2026)

Erkan: *"oluşturulan versiyon açılıyor ama içi boş geliyor (montaj ve teknik
resim). aynı mekanizma versiyonlardada calışması lazım. belkide versiyon
kurgusu yanlış."*

Kurgu gerçekten yanlıştı. İkisi de yapıldı.

---

## Neden boş açılıyordu (ölçüldü)

1. Arşiv kopyası **izole** bir klasörde duruyor:
   `kök\.SwPdmSurum\<göreli>\<ad>\vN\<ad>` — ve çift tıkta **oradan** açılıyordu.
2. O klasörde **hiçbir komşu yok**.
3. Montajın içindeki çocuk yolları **komşuluğa bağlı**: `YazilacakYol` önce
   göreli yol yazıyor; parçalar aynı klasördeyse bu **çıplak ad** oluyor
   (`parça.SLDPRT`) — yani *"yanıma bak"* demek.
4. Yanında kimse olmayınca SOLIDWORKS hiçbirini çözemiyor → **boş açılıyor.**

Yani hata "versiyon bozuk" değil, **versiyon yanlış yerde açılıyor**du.

---

## Aşama 1 — Sahne (`SurumSahnesi.cs`)

Çözüm yolu **yamalamak değil, düzeni kurmak**. Gerçek PDM'ler (SOLIDWORKS PDM,
Autodesk Vault) bir versiyonu arşivden **açmaz**: seçilen versiyonu kasa
görünümünde **kendi normal yoluna** yazar ve dosya her zaman olağan klasör
düzeninde açılır. Dosyanın **içindeki** yollara hiçbiri dokunmaz.

Burada da aynısı: geçici bir klasörde (`%TEMP%\SwPdmSahne\<id>`) **kökün klasör
yapısı** taklit ediliyor, versiyonun dosyası ve çocukları kendi **göreli**
yerlerine diziliyor, açma oradan yapılıyor.

Kazancı:

- Çıplak ad da, `..\` de, mutlak yol da **doğru çözülüyor**.
- Dosyanın içine **hiç dokunulmuyor** — "yazılan dizenin uzunluğu değişirse
  SOLIDWORKS açmıyor" mayın tarlasına hiç girilmiyor.
- Sahnedeki her dosya **salt-okunur**: geçmişe bakarken bugünkü iş bozulamaz.

## Aşama 2 — Bileşim (`Surumler.Bilesim.cs`)

Aşama 1 montajı açtırıyordu ama **bugünkü** parçalarla. Geçmişe bakmanın anlamı
bu değil. Gerçek PDM'lerde her dosya kendi geçmişini tutar ve montajın her
versiyonu, o gün kullandığı çocukların **versiyon numaralarını** kaydeder
("referenced version"); `Get` o numaraları geri getirir, istenirse "latest"
seçilir.

Aynısı yapıldı:

- Montaj/teknik resmin `vN`'i oluşurken her çocuk **KENDİ arşivinde**
  sürümlenir; numarası `<yuva>\vN.cocuklar.txt` kaydına yazılır.
- **Erkan'ın 01.09.2026 kararı ayakta**: çocuklar montajın arşivine
  **kopyalanmıyor**. Ayrıca içerik **önce kıyaslanıyor** — bugünkü hâli daha
  önce arşivlenmiş bir versiyonla birebir aynıysa yeni kopya **yazılmaz**, o
  numara kullanılır. Değişmeyen parça, kaç montaj versiyonu olursa olsun
  diskte **bir kere** durur.
- Kayıt `vN` klasörünün **içine değil yanına** yazılıyor — bilerek:
  `YanindaCocukVarMi` "vN'de birden çok dosya var mı" diye bakıp eski (çocuklu)
  arşivleri tanıyor; içeri koymak her yeni versiyonu eski düzen sandırırdı.

### Açarken sorulan soru

Bileşim kaydı olan bir versiyon çift tıklanınca:

| Seçenek | Ne yapar |
|---|---|
| **Evet** | O GÜNKÜ parçalarla açar — versiyonun gerçek hâli |
| **Hayır** | BUGÜNKÜ parçalarla açar |
| **İptal** | Açmaz |

Kayıt yoksa soru da yok (seçenek zaten tek). Eski, bileşimsiz versiyonlar
aynen çalışmaya devam ediyor.

Durum çubuğu **ne olduğunu söylüyor**: `12 parça yanına dizildi — O GÜNKÜ
hâlleriyle`. O günkü kopyası elle silinmiş bir parça varsa `· 2 tanesi bugünkü
hâliyle (o günkü kopyası yok)` diye ayrıca yazıyor — "o günkü" deyip bugünküyü
dizmek sessiz bir yalan olurdu.

---

## Ölçüm

`SwPdm.Cekirdek` derlendi (0 uyarı, 0 hata) ve mantık **gerçek dosyalarla**
sınandı — 30 doğrulamanın hepsi geçti:

- çocuklar kendi yuvalarında sürümlendi, montajın `vN` klasöründe **tek dosya**
- değişmeyen parça **ikinci kez kopyalanmadı**
- `v0` bileşiminde çocuk `v0`, `v1` bileşiminde değişen çocuk `v1`, değişmeyen `v0`
- sahne `v0`'ı açtı ve parçayı **eski hâliyle** dizdi (alt klasör dahil)
- **silinmiş** parça bile arşivden geri geldi
- bileşimsiz eski versiyon "yok" dedi, patlamadı ("okunamadı" demedi)
- kök dışı çocuk atlandı ve **sebebi söylendi**

Aynı sınavlar `testler/SwPdm.Cekirdek.Testler/SurumlerTestleri.Bilesim.cs`
içine 8 xunit testi olarak yazıldı; `DERLE.bat` bunları da koşturur.

---

## Kalan tek adım — DERLE.bat

Uygulamanın kendisi (`SwPdm.Arayuz`) **bu makinede derlenemiyor**: bulut
kabındaki .NET'te WinForms hedef paketi yok ve NuGet kapalı. Söz dizimi ve
`SwPdm.Cekirdek`'e yapılan bütün çağrılar Roslyn ile ayrıca denetlendi —
kalan hataların hepsi yalnızca "WinForms tipi bulunamadı" cinsinden.

`DERLE.bat`'ı çift tıklayamıyorum: **Dosya Gezgini "reddedilenler" listesinde**
(Ayarlar → Masaüstü uygulaması → Bilgisayar Kullanımı → Reddedilen
uygulamalar), Komut İstemi ise yalnız-tıklama izniyle açık — yazı yazamıyorum.

**Sen `DERLE.bat`'ı bir kez çift tıkla**, gerisini uygulamada test ederim.
(Gezgin'i o listeden çıkarırsan bundan sonra kendim derlerim.)

## Değişen dosyalar

| Dosya | Ne |
|---|---|
| `src/SwPdm.Cekirdek/Surum/SurumSahnesi.cs` | sahne; iki kip (bugünkü / o günkü), tek dizme kodu |
| `src/SwPdm.Cekirdek/Surum/Surumler.Bilesim.cs` | **yeni** — bileşim yaz/oku, çocuk sürümleme, içerik kıyası |
| `src/SwPdm.Arayuz/Gorunum/Islemler/SurumOlusturIslemi.cs` | versiyon oluştururken bileşimi de yazar; kutuda kaç parça olduğu yazılı |
| `src/SwPdm.Arayuz/Gorunum/Referans/SurumBolumu.cs` | açarken "o günkü mü, bugünkü mü" sorusu ve dürüst durum cümlesi |
| `testler/.../SurumlerTestleri.Bilesim.cs` | **yeni** — 8 test |

## Sıradaki (yapılmadı, karar senin)

- **"Bu versiyona dön" çocukları da döndürsün mü?** Bugün yalnız dosyanın
  kendisi dönüyor. Bileşim kaydı artık hangi çocuğun hangi versiyonda olduğunu
  biliyor; istenirse dönüş kutusu onları da listeleyebilir. Bu senin **canlı**
  dosyalarını değiştireceği için sormadan yapmadım.
- Park edilenler (senin kararın): `akış yuvaya sığmıyor` taşması ve klasör
  yeniden adlandırmadan sonra Ctrl+Z zincirinin kopması.

---

# Ek — taşınan/adı değişen parça (02.09.2026, senin klasöründe ölçüldü)

Sen derleyip versiyonları oluşturduktan sonra arşivi denetledim. **15 bileşim
satırının 3'ü bayattı.**

Sebep: bileşim satırı çocuğu **köke göre yoluyla** yazıyor. Çocuk sonradan
taşınır ya da adı değişirse o yol artık onu göstermiyor. Arşiv kopyaları
**duruyordu** (yuva dosyayla birlikte taşınıyor) ama kayıttaki yol bulamıyordu —
yani "versiyonlar da bozulmasın" isteğinin tam ortasından vuruluyordu.

Senin klasöründeki üç satır:

| parça | ne olmuş |
|---|---|
| `1-YMB.00902…GÖVDESİ.SLDPRT` | `Yeni klasör`'e taşınmış **ve** `2-…` diye yeniden adlandırılmış |
| `1-YMB.00903…KULPU.SLDPRT` | `Yeni klasör`'e taşınmış |

## Düzeltme

Kanca, arşiv yuvasının taşınmasıyla **aynı yerde**: `Surumler.Tasindi` — yani
*adlandır · taşı · geri al · ileri al* yollarının hepsinin altından geçtiği tek
nokta. Bir dosya taşınınca artık bütün `*.cocuklar.txt` kayıtları da taranıp
tazeleniyor. **Klasör taşıma da kapsanıyor**: satırın yolu taşınan klasörle
başlıyorsa önek değişiyor, yani tek `Directory.Move` ile yeri değişen bütün
çocuklar tek geçişte onarılıyor. İlgisiz bir taşıma hiçbir kayda dokunmuyor.

3 yeni test eklendi (yeniden adlandırma · klasör taşıma · ilgisiz taşıma);
toplam 11 bileşim testi. Mantık ayrıca gerçek dosyalarla 40 doğrulamayla
sınandı, hepsi geçti.

## Senin arşivin onarıldı

Bayat 3 satırı diskte düzelttim; her kaydın yanına `.yedek` kopyası bırakıldı.
Şimdi 15 satırın 15'i de doğru arşiv kopyasını buluyor — eski versiyonlarını
yeniden oluşturman gerekmiyor.

**Yapılacak:** `DERLE.bat`'ı bir kez daha çalıştır (düzeltme kaynakta, derlenmiş
sürümde değil).


---

# Ek 2 — parçaların versiyon listesi kirlenmesin (senin itirazın)

> *"montajın versiyonunu oluştur dediğimde içindeki tüm parçaların versiyonunu
> oluşturuyor."*

Haklıydın. İlk tasarımda her çocuk **kendi arşivinde** sürümleniyordu; parçanın
VERSİYONLAR listesinde senin oluşturmadığın `(otomatik — … ile)` satırları
çıkıyordu ve 01.09'daki **"versiyon = yalnız o dosya"** kuralı görünürde
bozuluyordu.

## Yeni tasarım — gizli, içerik-adresli depo

Parçaların o günkü içeriği artık şuraya yazılıyor:

```
kök\.SwPdmSurum\.icerik\<ilk iki hane>\<sha256>
```

Bileşim kaydının satırı da `göreli yol <TAB> sha256` oldu.

| | |
|---|---|
| Parçanın VERSİYONLAR listesi | **yalnızca senin oluşturduklarını** gösterir |
| Aynı içerik | diskte **tek kopya** (anahtar içeriğin kendisi) |
| Parça taşınsa / adı değişse / silinse | içerik yine **bulunur** — arama yoldan bağımsız |
| Depo dosyaları | salt-okunur; yarım kopya olmasın diye geçici ada yazılıp yerine konuyor |
| Eski (numaralı) kayıtlar | **okunmaya devam ediyor** |

## Bir hatayı daha kapattı

Sabah eklediğim "taşınınca bileşim kaydındaki yolu güncelle" davranışını
**geri aldım — yanlıştı.** Arşivdeki montaj salt-okunur, yani içindeki yollar
hiç onarılmıyor; parçaları **o günkü** yerlerinde arıyor. Sahne de o günkü
yerleşimi kurmak zorunda. Artık kayıttaki yol donmuş duruyor, içerik ise
karmayla bulunuyor — ikisi birden doğru.

Ayrıca bir versiyon silinince `vN.cocuklar.txt` de siliniyor. İçerik deposuna
dokunulmuyor: aynı içeriği başka versiyonlar gösteriyor olabilir, yanlış bir
silme geçmişi geri getirilemez biçimde yok ederdi.

## Ölçüm

30 doğrulama gerçek dosyalarla geçti; 11 xunit testi yazıldı. Öne çıkanlar:
parçanın listesi boş kalıyor · aynı içerik ikinci kez yazılmıyor · taşınan, adı
değişen ve **silinen** parça o günkü yerine o günkü içerikle diziliyor · eski
numaralı kayıt hâlâ okunuyor · depodaki içerik silinmişse bu **sayılıp
söyleniyor**.

## Senin arşivin taşındı

İki bileşim kaydındaki 14 satırın hepsi içerik deposuna taşındı — **7 içerik**,
yani 14 satır 7 dosyayla karşılanıyor (aynı parçalar iki versiyonda da
değişmemiş). Hiçbir şey silinmedi.

**Kalan tek artık:** parçaların listesinde ilk turdan kalma **7 adet
`(otomatik — …)`** versiyonu duruyor. Artık hiçbir yerden kullanılmıyorlar.
Senin oluşturduğun 3 versiyon (`1-GİZLİ MANİVELA KOLU.SLDASM` v0/v1 ve
`…GÖBEĞİ.SLDPRT` v1) elbette kalıyor. İstersen o 7 tanesini temizlerim.

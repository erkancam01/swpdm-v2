# SW PDM — elle kullanım testi raporu

**Ortam:** Windows, SOLIDWORKS 2022 + eDrawings kurulu · **Tarih:** 01.09.2026
**Kök:** `…\swpdm-v2\1111111111111111111` (10 dosyalık gerçek manivela kolu takımı)
**Yöntem:** uygulama `dotnet run` ile açıldı, her şey **ekranda elle** denendi.
Hiçbir madde belgeden okunmadı; her biri tekrar edilerek doğrulandı.

---

# A. AĞIR — düzeltilmeden sürüm çıkmamalı

## A1. Tam ekranda KIRIK ve VERSİYONLAR sekmeleri TIKLANMIYOR

**Tekrarı:** pencereyi tam ekran yap (referans şeridi tek satıra sığar) →
**KIRIK** ya da **VERSİYONLAR** yazısına tıkla. Hiçbir şey olmuyor.
İÇİNDEKİLER ve KULLANILDIĞI YERLER aynı şeritte sorunsuz açılıyor.
x = 1035…1210, y = 462…480 arası onlarca nokta denendi.

**Çalıştığı yer:** pencere küçültülüp şerit **iki satıra sarınca** dördü de
açılıyor. Yani hata yalnız **tek satırlık** yerleşimde.

**Nerede aramalı:** `Gorunum/Referans/ReferansSeridi.cs` — `FlowLayoutPanel`
(`AutoSize` + `WrapContents`) içinde `AutoSize` düğmeler. `Sayilari()` düğme
yazılarını **sonradan** uzatıyor ("KIRIK" → "KIRIK  2 dosya"); tek satırda
düğmelerin tıklama dikdörtgeni çizilen yazıyla örtüşmüyor.

## A2. `Ctrl+Shift+E` (referans bölümünü ilerlet) HİÇ çalışmıyor

Ağaç odaktayken de, referans paneli odaktayken de 4'er kez denendi: bölüm
değişmiyor, durum çubuğuna bir şey yazılmıyor. Aynı kalıptaki `Ctrl+Shift+F`
**çalışıyor** — yani tuş uygulamaya ulaşıyor, `KeyDown` işleyicisi koşuyor.

**A1 + A2 birlikte: tam ekran çalışan kullanıcı KIRIK ve VERSİYONLAR
bölümlerine hiçbir yoldan ulaşamıyor.** Üstelik durum çubuğu her bölüm
değişiminde *"· Ctrl+Shift+E ile ilerlet"* diye çalışmayan tuşu duyuruyor.

## A3. `Ctrl+Shift+U` (yeni versiyon oluştur) HİÇ çalışmıyor

Parça seçili, ağaç odakta, sağ tık menüsünde madde **aktif** ve yanında
"Ctrl+Shift+U" yazıyor — kısayola basınca hiçbir şey olmuyor.
**Menüden tıklayınca çalışıyor.** VERSİYONLAR sekmesindeki
*"Versiyon yok — **Ctrl+Shift+U** ile başlat."* cümlesi kullanıcıyı
çalışmayan bir tuşa yönlendiriyor.

## A4. "Kırık referanslar"ın çoğu YANLIŞ ALARM — dosyanın kendi eski adı

Rapordaki 9 bulgunun **6'sı**, dosyanın **kendi** eski adı:

| Dosya | "Kırık" denen ad |
|---|---|
| `1-YMB.00900…GÖBEK YATAĞI.SLDPRT` | `YMB.00900…GÖBEK YATAĞI.SLDPRT` |
| `1-YMB.00902…GÖVDESİ.SLDPRT` | `YMB.00902…GÖVDESİ.SLDPRT` |
| `1-YMB.00905…GÖBEĞİ.SLDPRT` | `YMB.00905…GÖBEĞİ.SLDPRT` |
| `1-YMB.00924…ÜST KAPAK.SLDPRT` | `YMB.00924…ÜST KAPAK.SLDPRT` |
| `1-ND-MANİVELA KOLU HAREKETLİ PARÇA.SLDPRT` | `ND-MANİVELA KOLU HAREKETLİ PARÇA.SLDPRT` |
| `1-y1-manivela kolu yayı.SLDPRT` | `manivela kolu yayı.SLDPRT` |

Desen açık: bu dosyalara dışarıda **"1-" öneki** eklenmiş; SOLIDWORKS dosyası
kendi yolunu içinde saklıyor, çözücü bunu bir **çocuk referansı** sanıyor.

**Aynısını F2 ile canlı ürettim:** KULPU parçasının adını değiştirdim →
paneli *"Başka dosya kullanmıyor"*ken **"İÇİNDEKİLER: hepsi kırık · KIRIK 1"**
oldu, kırık satır parçanın kendi eski adıydı. `Ctrl+Z` ile geri alınca düzeldi.

**Bu bilgi zaten doğru yerde duruyor:** "Taşınmış dosyalar" raporu aynı 4
dosyayı *"Son kaydedildiği yer: …"* diye doğru şekilde listeliyor. KIRIK'teki
kopya tamamen gürültü — ve bu uygulamada yanlış "kırık" bilgisi **sağlam dosya
sildirir**. Dosyanın kendine yaptığı atıf İÇİNDEKİLER/KIRIK'te sayılmamalı.

---

# B. ORTA

## B1. Açılışta EN SON kök değil, EN ESKİ kök açılıyor

İki ayrı açılışta doğrulandı. "Klasör aç ▾" listesi doğru sırada
(1. sıradaki: test klasörü), ama uygulama listenin **son** sırasındaki eski
kökle açıldı. `OZELLIKLER.md` §1: *"en son kullanılan kök kendiliğinden açılır."*

## B2. Açılışta bölücü konumu pencereye göre sınırlanmıyor

**Her açılışta** referans paneli ~55 piksele sıkışmış geliyor: sekme yazıları
kesik ("İÇİNDEKİL", "KULLANIL", "VERSİYON"), liste okunmuyor. Elle sürükleyince
düzeliyor ve o oturumda bir daha bozulmuyor. Alt panele **en küçük genişlik**
gerekiyor.

## B3. 3B önizleme açıkken silinen dosya eDrawings hata kutusu çıkarıyor

Kopyalanan dosya seçiliyken `Ctrl+Z` → **eDrawings modal kutusu**:
*"…KULPU (2).SLDPRT bulunamadı."* Belgede *"her işlem başlamadan belge
kendiliğinden bırakılır"* yazıyor; bırakılmıyor. Kullanıcı uygulamanın değil,
eDrawings'in hatasını görüyor.

## B4. Aynı klasöre yapıştırmada çakışma kutusu çıkmıyor

Bir parçayı kopyalayıp **kendi klasörüne** yapıştırdım: çakışma kutusu
(Atla · İkisini de tut · Değiştir · Vazgeç) **hiç çıkmadı**, sessizce
`… KULPU (2).SLDPRT` oluştu.

## B5. Başarılı işlemlerden sonra durum çubuğu SESSİZ

- `F2` + onarım → **"N dosya onarıldı" cümlesi yok** (§12 söz veriyor).
- Taşıma başarılı → cümle yok. Versiyona dönüş → cümle yok. `Ctrl+Z` → cümle yok.
- Ama **iptal** ("Taşıma iptal edildi."), **kopyalama** ("1 öğe kopyalandı —
  hedef klasörü seçip Ctrl+V.") ve **tarama** cümle yazıyor.

Tutarsız: kullanıcı en çok teyide ihtiyaç duyduğu anda (dosyaların içine
yazıldığı an) hiçbir şey görmüyor.

## B6. `Ctrl+Z` sonrası durum çubuğunun solu bayat kalıyor

Ad geri alındıktan sonra sol tarafta hâlâ eski (değiştirilmiş) ad ve boyut
duruyordu. Aynısı arama sonucunda da oluyor: seçim boşaldığı hâlde önceki
dosyanın satırı kalıyor.

---

# C. HAFİF / belge–davranış farkı

- **Taşıma onay kutusu onarımdan söz etmiyor.** §12'ye göre *"kullandığı N dosya
  taşınmıyor; referansları onarılacak"* yazmalı; yalnız ne ve nereye taşınacağı yazıyor.
- **Adlandırma onayı YENİ adı yazmıyor** — "Adı değiştirilecek ve bu dosyalar
  onarılacak." `eski → yeni` yazsa daha güvenli.
- **Versiyon önizleme başlığı** `◂ v0.SLDPRT` değil, canlı dosyanın adını
  yazıyor (§11a). Kullanıcı versiyona mı bugüne mi baktığını yalnız `◂`den anlıyor.
- **3B kipte yüklenirken boş gri kutu** görünüyor; §10'un söz verdiği
  *"Önizleme yükleniyor…"* yazısı çıkmıyor.
- **"Süzgeç kaldı"** cümlesi ikircikli ("kaldırıldı" mı, "kaldı" mı).
- **Geri alma yığını**, çöp kutusundan elle geri yüklenen bir silmeyi hâlâ
  "Geri al: 1 öğenin silinmesi" diye tutuyor.

---

# D. ÖLÇEMEDİKLERİM (hata saymıyorum)

- **`Esc`** — uzaktan kumandanın klavye köprüsü Escape'i uygulamaya iletmiyor
  gibi: rapor penceresinde, ad kutusunda ve arama kutusunda hiç etki etmedi.
  **Senin elinle denemen gerekiyor.**
- **Uygulama bir kez kendiliğinden kapandı** (versiyon kutusunda Tamam'a
  basıldığı an uzak bağlantı da koptu). Çöktü mü, köprü mü kapattı ayıramadım.
  Versiyon arşivi o sırada **doğru yazılmıştı**.
- Ağ sürücüsü, 100 MB+ montaj, "Referansı elle bağla", "Bulunanları düzelt"
  (bayat yol bulunamadı — test verisinde 0 bulgu vardı).

---

# E. ÇALIŞTIĞINI GÖRDÜKLERİM — çoğu sağlam

**Versiyon sistemi uçtan uca doğru (diskten byte byte doğrulandı):**
oluşturma · not düzeltme (`F2`) · **versiyona dönüş** — dosyayı 285.982 bayttan
özgün 285.974 bayta geri yazdı, aradaki hâli `v2 — v0'a dönmeden önce` diye
kendiliğinden arşivledi, hiçbir içerik kaybolmadı · versiyon silme (kalıcı,
dosyaya dokunmadan). İçerik değişmemişse gereksiz versiyon **yazmıyor** —
doğru davranış.

**Referans onarımı:** `F2` sonrası montaj ve teknik resim yeni adı gösteriyor;
`Ctrl+X`/`Ctrl+V` taşımasından ve **sürükle-bırak**tan sonra da bağ sağlam
kalıyor; `Ctrl+Z` hem adı hem onarımları birlikte geri alıyor; sağ tık menüsü
"İleri al"da işlemin tam adını yazıyor.

**Silme onayı — uygulamanın en iyi ekranı:** *"DİKKAT: bunları 1 dosya
KULLANIYOR"* + dosya adı + *"Silerseniz o dosyalar bu parçayı bulamaz."*,
varsayılan düğme **Vazgeç**.

**Çöp kutusu:** silme anında (aynı disk), sütunlar, eski konum, çift tıkla geri
yükleme, ardından *"Diskte değişiklik görüldü — ağaç tazelendi."*

**Ayrıca:** kök açma ve son açılanlar listesi · ağaç ve gerçek SOLIDWORKS
simgeleri · 2B ve 3B (eDrawings) önizleme · `Ctrl+Shift+R` tarama (10 dosya,
0,0–0,1 sn) · referans sayıları ve *içinde / kullanan / BULUNAMADI* ayrımı ·
satıra tek tıkla komşu dosyanın önizlemesi ve `◂` ile dönüş · arama ·
`Ctrl+Shift+F` süzgeç · `Ctrl+Shift+D` altı rapor (özellikle **Taşınmış
dosyalar** doğru çalışıyor) · `Ctrl+Shift+N` yeni klasör · `Ctrl+Shift+Q`
klasör kilidi (ipucu metni çok iyi) · Ayarlar sekmesi ve 3B anahtarının anında
etkisi · otomatik tazeleme · **Wine'da hiç açılamayan sağ tık menüsü burada
sorunsuz açılıyor.**

---

**Not:** test klasöründe benim bıraktığım `ALT-KLASOR` (boş) ve çöp kutusunda
bir `… KULPU (2).SLDPRT` kopyası var; ikisi de silinebilir.

# SIRADAKİ — yeni bir oturum buradan devam eder

> Bu dosya **CLAUDE.md değildir.** Oradakiler ölçülmüş, bayatlamayan
> gerçekler; burası **bugünün açık işleri**. Bir iş bitince bu dosyadan
> silinir; hepsi bitince dosya silinir.
>
> Son durum: dal `claude/v2-pdm-start-u058ey`; `main` de aynı yerde
> (ileri sarma, 30.08.2026). **"Kök dışında" özelliği geri çekildi** —
> iki sürüm Erkan'ın makinesinde dondu, revert ile ca37316'nın davranışına
> dönüldü (ders CLAUDE.md §4'te).
> 313 test · 308 geçti · 5 atlandı (Windows'a özel) · **BEŞ** kapı TEMİZ
> (harita + boyut + derleme + test + çalıştırma **17** ölçüm).
> Yeni (30.08.2026): **3B önizleme (eDrawings)** — Ayarlar'dan seçilir,
> varsayılan 2B; Erkan denedi: *"harika çalıştı."*
> Aynı gün **özellik tarafı GERİ ÇEKİLDİ** (arama + panel gösterimi) —
> Erkan gerçek veride *"hiç kullanışlı değil"* dedi; C listesinde yerine
> ne konacağı duruyor.
> Aynı gün **panel sadeleşti**: önizlemenin altındaki bilgi bloğu kalktı,
> üstteki başlık artık **önizlenen** dosyanın adı, bölüm başlıkları
> `▼ İÇİNDEKİLER` / `▲ KULLANILDIĞI YERLER` oldu (Erkan'ın kararı).
> Aynı gün **referans panelinde sağ tık** geldi: menü ağaçtakinin aynısı,
> hedef o satırdaki dosya (`ReferansMenusu.cs`).
> Aynı gün panel **üç sekme** oldu: İÇİNDEKİLER · KULLANILDIĞI YERLER ·
> **KIRIK** (`ReferansSeridi.cs`); `Ctrl+Shift+E` ile gezilir. Sayılar
> aynı gün sadeleşti: sekmedeki sayı = o sekmedeki satır, "· eksik" kalktı.
> 31.08.2026: **VERSİYONLAR** geldi (Aşama 1) — `Surumler.cs`, dördüncü
> sekme, `Ctrl+Shift+U`, Enter = dön. Aşama 2-3 aşağıda AÇIK İŞ.
> Erkan §1b düzen turundan sonra denedi (29.08.2026): *"her şey çalışıyor."*
> Ctrl+Y, uzantı kilidi, yeni klasör adı sorma — hepsi onun makinesinde
> doğrulandı.
>
> **Kullanım kılavuzu artık var:** `OZELLIKLER.md` (pakete `OZELLIKLER.txt`
> olarak giriyor). Bir düğme/kısayol değişirse orası da değişmeli.

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
- **`Ctrl` ile sürükleyip kopyalama.** Wine'da sürükle-bırak ölçülmüyor;
  kod yolu "Yapıştır (kopyala)" ile aynı motora giriyor ama fare hareketi
  denenmedi.
- **Ad kutusundaki uzantı kilidi.** Wine'da kutu açılıyor ve Enter ile
  kapanıyor (kapı 9. ölçüm), ama "Uzantıyı da değiştir" kutusunun kendisi
  tıklanarak ölçülmedi.
- **`Ctrl+Y` (ileri alma).** Birim testi yazılamıyor (arayüz katmanı);
  kapıya da girmedi — çalıştırma kapısının 9. ölçümü `Ctrl+Z`'yi ölçüyor,
  `Ctrl+Y` eklenirse aynı ölçüm 8 → 9 → 8 → 9 olmalı.
- **`Esc` ile iş iptali.** Wine'da uzun bir iş üretip Esc'e basmak
  ölçülmedi; kod yolu düğmenin `PerformClick`'i, yani düğme çalışıyorsa
  bu da çalışır.
- **Yol çubuğundaki `…` tıklaması.** Dar pencerede kırpma oluşması gerekiyor;
  kapıdaki pencere boyutunda kırpma çıkmıyor.
- **Dolu listede tarama eksikliği artık sekmede YAZMIYOR** (Erkan'ın
  kararı, riski söylendi): kökteki bazı dosyalar okunamadıysa
  "KULLANILDIĞI YERLER 4 dosya" der ama gerçekte 5. bir kullanan olabilir.
  Tek işaret durum çubuğundaki tarama cümlesi. Erkan'da rahatsızlık
  yaratırsa dönülecek yer: `YukariMetni` (tek metot).

- **KIRIK sekmesinin GERÇEK veriyle hâli.** Örnek klasörde kırık referans
  yok; burada sekme hep "yok" diyor. Erkan'ın 67 referanslı montajında
  ölçülecek: sayı doğru mu, `BULUNAMADI` ile `yol BAYAT` ayrımı okunur mu,
  liste kalabalıklaşınca kullanışlı mı.

- **Referans panelinin SAĞ TIK MENÜSÜ.** Wine'da açılan her
  `ToolStripDropDown` uygulamayı çökertiyor (CLAUDE.md §11), yani menünün
  kendisi burada hiç açılamadı. Ölçülen tek şey, menüyle **aynı kodu**
  çağıran kısayol yolu (kapının 16. ölçümü: panelden `F2` satırın
  dosyasını adlandırıyor mu — diskten bakılıyor). Erkan'da bakılacak:
  menü açılıyor mu · en üstte doğru ad yazıyor mu · Sil/Kes/Yapıştır
  doğru dosyaya gidiyor mu · gri öğenin sebebi görünüyor mu.

- **3B önizlemenin TAMAMI.** eDrawings burada yok; Wine'da ölçülen tek şey
  "eDrawings'siz çökmüyor + sebep yazıp 2B'ye düşüyor" (16. ölçüm).
  Erkan'da ölçülecek: 3B görünüm geliyor mu · döndürme · büyük montajda
  ilk açılış süresi · 3B açıkken taşıma/ad değiştirme (belge kilidi işlem
  öncesi bırakılıyor; `OpenDoc` imzası bir sürümde farklıysa durum
  çubuğundaki hata metni gelsin, ikinci turda düzeltilir).

## A2 — VERSİYON: AÇIK İŞLER (kurgu Erkan'la kuruldu, 31.08.2026)

- **Aşama 2 — "üzerine yaz / yeni versiyon" sorusu (Erkan'ın 2. beklentisi).**
  Kurgu: uygulamadan açılan (ya da `~$` kilidi beliren) SW dosyasının o anki
  hâli beklemeye alınır; kilit KALKINCA içerik değiştiyse TEK soru: "Üzerine
  yazıldı (versiyon artmasın) / Yeni versiyon". Her Ctrl+S'te soru YOK —
  SOLIDWORKS kullanıcısı sık kaydeder, kutu kullanılmaz olurdu (§6).
  Parçalar hazır: `DiskIzleyici` (değişikliği görür) + `Kilit` (açık/kapalı).
- **Aşama 3 — montaj versiyonu çocukları NOT eder, dönerken sorar.**
  `kayit.txt`'e çocukların o günkü versiyon numaraları yazılır (referans
  indeksi biliyor, kopya değil ucuz); dönüşte "yalnız montaj / o günkü
  çocuk versiyonlarıyla" seçimi. Karar verildi, kurulmadı.
- **KLASÖR adı değişince dışarıdaki ebeveynler ONARILMIYOR (Erkan, 31.08.2026:
  "dosya ve klasör ismi değiştirmede referanslar kayboluyor").** Dosya adı
  tarafı bu turda onarıldı ve testle kilitlendi; klasör tarafı açık: klasörün
  İÇİNDEKİ referanslar komşuluk kuralıyla sağlam kalıyor (§5'te ölçüldü), ama
  dışarıdan gelen ebeveyn eski klasör adını yazmaya devam ediyor. Taşıma
  motorunun `TasimaPlanlari` makinesi bunu zaten yapabiliyor — klasör altındaki
  SOLIDWORKS dosyaları için (eski yol → yeni yol) çiftleri üretilip aynı
  "hepsi ya da hiçbiri" akışına verilmeli.
- **Ad değiştirme/taşıma arşivi TAŞIMIYOR (bilinen borç, §3).** Dosya bizim
  uygulamadan adlanınca/taşınınca `.SwPdmSurum` yuvası eski adda kalıyor ve
  versiyon listesi "yok"a döner (kayıt kaybolmaz, yuva diskte durur; elle
  yuva klasörünü yeniden adlandırmak geri getirir). Aşama 2'yle birlikte
  `DosyaIslemleri`/`TasiIslemi` kancasına bağlanacak.
- **Versiyon silme/not düzenleme GELDİ (31.08.2026).** `F2` notu düzeltir,
  `Delete` versiyonu kalıcı siler (çöp kutusuna GİTMEZ — çöpten dönen kopya
  kayıtsız kalır ve listede görünmez; "geri alınabilir" demek orada yalan
  olurdu). Kalan borç: **silme `Ctrl+Z` yığınına girmiyor** — geri alma tek
  savunma değil, onay kutusu var; yine de eklenebilir.
- **v4 ayrışmasının KÖK SEBEBİ Erkan'da henüz ölçülemedi.** Üretebilen bir
  mekanizma bulundu ve kapatıldı (görünenlerin en büyüğünden numara türetme
  → çift No). Ama onun makinesindeki ilk tetikleyici doğrulanmadı; yeni
  sürümdeki "kayıt ... diyordu" durum notu izi gösterecek.
- **Eski arşiv kopyaları salt-okunur değil.** Koruma 31.08.2026'da geldi
  ve yalnız YENİ kopyalara konuyor; öncesinde oluşan kopyalar yazılabilir.
  İşlev aynı, koruma eksik — gerekirse Listele sırasında öznitelik
  tamamlanabilir (yazılmadı: eski kopya sayısı bir elin parmağı).
- **Silinen dosyanın arşivi yerinde kalıyor** — çöpten geri gelince
  versiyonları da geri gelir (iyi); dosya çöpten kalıcı silinirse arşiv
  öksüz kalır, temizliği düşünülecek.

## B — KAPI BORCU (bilerek eksik bırakıldı, sebebiyle)

- **Dikdörtgenle seçim yok.** `SecimliAgac` satırın sağını uzun süre
  "dikdörtgen başlar" diye ayırmıştı ama özellik hiç yazılmadı; 29.08.2026'da
  satırın tamamı tıklanabilir yapıldı. Dikdörtgen seçim yazılırsa
  `MetneVuranDugum` yeniden ayrılmalı. Wine'da `DrawReversibleFrame`
  çalışıyor (CLAUDE.md §11), yani ölçülebilir.
- **İleri alma "Değiştir" sonrası YOK.** Üzerine yazılan dosya geri alma
  sırasında çöpten geri geldi; ileri alırken onu yeniden çöpe göndermek
  gerekirdi ve o dosya bu arada değişmiş olabilir. `Ctrl+Y` sebebini
  söylüyor. Çözüm: çöpteki öğenin kimliğini (`CopOgesi.No`) adımda taşımak.
- **Çöp kutusu penceresinde toplu iş İPTAL EDİLEMİYOR.** Pencere modal ve iş
  arayüz iş parçacığında koşuyor; ilerleme yazılıyor ama tıklanamayacak bir
  İptal düğmesi konmadı (§3: çalışmayan düğme koymaktansa yok). Doğru çözüm
  işi `IIlerlemeYuzeyi`'ne taşımak — pencerenin tamamını değiştirir.
- **`Ctrl+Z` (geri alma) arayüz iş parçacığında.** 20 öğelik bir taşımanın
  ağ sürücüsünde geri alınması uygulamayı dondurur. İleri yön (`Aktar`) arka
  planda + ilerleme + iptal ile koşuyor; geri yön değil.
- **Önizlemede zaman aşımı yok.** Takılan bir kabuk sağlayıcısı bütün
  önizleme kuyruğunu kilitleyebilir. Wine'da ölçülemez (§11), Erkan'ın
  makinesinde belirti görülmedi — o yüzden yazılmadı.

- **Elle bağlamanın kapıda kalıcı ölçümü YOK.** Wine'da uçtan uca bir kez
  ölçüldü (pencere açıldı · kendi ağacımızdan dosya seçildi · yama yazıldı),
  ama kapıya girmedi: çalıştırma kapısının bazı ölçümleri örnek klasördeki
  **mutlak satır numaralarına** bağlı ve yeni dosya eklemek onları kaydırıyor
  (11. ölçüm "satır 13 = Parça1.SLDPRT" diyor). Eklenecekse önce o ölçümler
  satır numarasından kurtarılmalı.

## B2 — §1b denetiminden kalan bilinçli borçlar (29.08.2026)

- **`Ayarlar.cs`'te yeni ayar = aynı dosyada 3 nokta** (özellik + Oku + Yaz);
  anahtar adı iki yerde elle, kayarsa hata sessiz. Tek dosyada olduğu için
  tolere edildi; sözlük tabanlı türetme yazılırsa kapanır.
- **`TasinmisDosyalar` raporunun adıyla testi yok** — yalnızca RaporListesi
  döngüsünden geçiyor; asıl karşılaştırma satırı ölçülmüyor.
- **`AyarlarSayfasi`'nın ~%70'i çöp kutusu ayarı** — bölünmedi (tek dosya,
  davranış doğru); çöp ayarları büyürse kendi sayfa dosyasına çıkar.

## C — Bekleyen fikirler (Erkan'a sunuldu, seçilmedi)

- **"Kök dışında" ayrımı — İKİ KEZ DENENDİ, GERİ ÇEKİLDİ (30.08.2026).**
  Fikir doğru (diskte var + kök dışında ≠ kayıp) ama `File.Exists`
  erişilemeyen yolda uzun süre bloklıyor ve Erkan'ın 2341 dosyalık
  ağacında iki deneme de dondurdu: çözüm anında sormak SEÇİMİ, taramanın
  sonunda toplu sormak TARAMAYI (2300/2341'de asılı; Erkan geri çekilmesini
  istedi). Ayrıntı CLAUDE.md §4'te; kod `git revert` edilen 58b6ecb +
  b123617 commit'lerinde duruyor, oradan geri alınabilir. Üçüncü deneme
  ancak BEKLEMEYEN yoklamayla: yol başına kısa zaman aşımı (ayrı iş
  parçacığı, süresinde dönmezse "bilinmiyor"), sunucu adına "ölü" damgası,
  ve/veya yalnız seçili dosyanın yolları istek üzerine.

- **ÖZELLİK TARAFI — YAZILDI, DENENDİ, GERİ ÇEKİLDİ (30.08.2026).**
  Erkan gerçek veride: *"bu şekilde hiç kullanışlı değil."* Kaldırılanlar:
  arama kutusuna `malzeme: pirinç` sözdizimi (indeksten) **ve** önizleme
  panelindeki 3 satırlık özellik gösterimi. Kod `git revert` edilen
  `0886b6c`'de duruyor; çekirdek okuyucu `SwBelgeBilgisi.cs` yerinde
  (yazıcı regresyon kapısı).
  **Neden kullanışsız çıktı — ölçülmüş gerekçe:** (1) anahtarı EZBERE
  yazmak gerekiyordu; hangi özellik var, hangi değerler var hiçbir yerde
  görünmüyordu. (2) Gösterim yalnızca **tek** seçili dosyada ve 3 satıra
  kırpılmıştı; "hangi parçanın malzemesi ne" sorusu toplu cevaplanamıyordu.
  Bir sonraki deneme bu ikisini çözmeli. Erkan'a sunulan seçenekler:
  · **sütunlu tablo görünümü** (Ad · Malzeme · Ağırlık · Revizyon; başlığa
    tıkla-sırala) — "toplu görme" sorununu çözer
  · **tıkla-süz**: süzgeç şeridinde "Malzeme ▾" → indeksten gelen GERÇEK
    değerler ve her birinin kaç dosyada olduğu; yazmak yok
  · **Excel/CSV parça listesi** (BOM/teklif için doğrudan kullanılır)
  · **özellik DÜZENLEME**: seçili dosyaların özelliğini toplu değiştirmek
    (dosyaya yazma altyapısı `SwYazici` ile hazır)
- **Bilgi bloğunun yerine ne konacak — AÇIK SORU.** Blok kalktı (Erkan:
  *"gerek yok"*), ama Tür/Boyut/Tarih artık yalnızca durum çubuğunda ve
  ağaçtaki sütunlarda. Gerçek veride bir eksiklik hissedilirse seçenek:
  önizleme başlığının sağına **tek satır** özet (boyut · tarih) koymak —
  panelin sadeliğini bozmadan. Erkan söylemeden yapılmayacak.

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

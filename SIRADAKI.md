# SIRADAKİ — yeni bir oturum buradan devam eder

> Bu dosya **CLAUDE.md değildir.** Oradakiler ölçülmüş, bayatlamayan
> gerçekler; burası **bugünün açık işleri**. Bir iş bitince bu dosyadan
> silinir; hepsi bitince dosya silinir.
>
> Son durum: dal `claude/v2-pdm-start-u058ey`; `main` de aynı yerde
> (ileri sarma, 30.08.2026). **"Kök dışında" özelliği geri çekildi** —
> iki sürüm Erkan'ın makinesinde dondu, revert ile ca37316'nın davranışına
> dönüldü (ders CLAUDE.md §4'te).
> 392 test geçti · 5 atlandı (Windows'a özel) · **YEDİ** kapı TEMİZ
> (harita + kılavuz + kısayol + boyut + derleme + test + çalıştırma
> **27** ölçüm) — **YEDİ** kapı.
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
> 31.08.2026: **PARÇA LİSTESİ (BOM) geldi ve AYNI GÜN KALDIRILDI** —
> Erkan: *"solidworkun içinde var zaten."* Dört dosya silindi, bir satır
> kesildi (§1b'nin sınavı geçti). Ondan **kalanlar**: ağaç yürüyüşü
> `BelgeAgaci.cs`'te (versiyon arşivinin çekirdeği) ve kapısının yakaladığı
> **gerçek hata** — alt klasördeki parça hiç bulunamıyordu, arşive girmiyordu
> ve montajın versiyonu SOLIDWORKS'te açılmıyordu; onarıldı ve testle kilitli.
> 31.08.2026: **KLASÖR DÜZENİ** — çekirdek beş klasöre ayrıldı
> (`Ortak` · `Gezgin` · `SwDosyasi` · `Referans` · `Surum`), arayüzde kutular
> `Islemler/Kutular/`e çıktı. **Yalnız `git mv`**: 54 rename, sıfır kod
> değişikliği, 374 test aynı. Harita kapısına **yol denetimi** eklendi —
> önceden yalnız dosya adına bakıyordu ve taşınan yolu göremezdi.
> 01.09.2026: **PARÇANIN VERSİYONU YALNIZ O PARÇA** — in-context bir parça
> kendi montajını referans veriyor ve yürüyüş oradan aşağı inip bütün ürün
> ağacını parçanın arşivine dolduruyordu (Erkan'da **162 dosya**).
> `BelgeAgaci.IcerikMi`: parça → montaj bağı izlenmez (CLAUDE.md §5).
> 31.08.2026: **KLASÖR KİLİDİ** geldi — `Ctrl+Shift+Q`, bitmiş işler
> açılmaz (`KlasorKilidi.cs`). Kaza koruması: Gezgin'i bağlamaz, referans
> taraması/onarımı bilerek devam eder. Yol üstünde gerçek bir kusur da
> kapandı: **arama** kendi klasörlerimize (çöp + arşiv) giriyordu.
> Gizli klasör adları artık `GizliKlasorler.Tumu`'nden türetiliyor.
> 31.08.2026: **dön kutusu "kimler etkilenir" diyor** — Erkan'ın sorusunun
> cevabı: dönüş dosyanın kendi yoluna yazdığı için montajlar zaten dönülen
> içeriği görüyor; eksik olan söylemekti. Kutu artık iki yönü de gösteriyor,
> taranmamışsa sayı vermiyor.
> 31.08.2026: **sağ tık → "Dosya ağacında göster"** (`Ctrl+Shift+G`) —
> Erkan: *"dosyanın konumunu bilmiyorum."* Gitme yeteneği vardı (panelde
> `Enter`), **menüde yoktu**; artık menüde ve gidilen klasörü durum
> çubuğuna yazıyor. İşlem ağacı kendi sürmüyor: `IslemBaglami.AgactaGoster`
> (`AgaciKapat`'ın kardeşi) — `Tazele` ile aynı şey **değil**, ölçüldü.
> 31.08.2026: **dört istek** — sağ tık **"Aç"** · panelde **çift tık AÇAR**
> (git artık `Enter`) · **versiyon SATIRIN dosyasına** · tür süzgeci tuzağı.
> Son ikisi **gerçek hataydı**: versiyon panelden alınınca ağaçtaki dosyaya
> gidiyordu (sürüm notunda açıkça yazılı — Erkan'ın arşivinde yanlış yuva
> olabilir), ve süzgeç açıkken panelden gidilemeyen dosya için **yanlış
> sebep** yazılıyordu ("kökün dışında"). Kapı iki yeni ölçüm aldı (23, 24) ve
> yolda üçüncü bir hatayı da yakaladı: `Keys.Enter` menü kısayolu olarak
> kaydedilince uygulama **hiç açılmıyordu** (derleme temizdi) — ders
> CLAUDE.md §6'da.
> Erkan §1b düzen turundan sonra denedi (29.08.2026): *"her şey çalışıyor."*
> Ctrl+Y, uzantı kilidi, yeni klasör adı sorma — hepsi onun makinesinde
> doğrulandı.
>
> **Kullanım kılavuzu artık var:** `OZELLIKLER.md` (pakete `OZELLIKLER.txt`
> olarak giriyor). Bir düğme/kısayol değişirse orası da değişmeli.
> **01.09.2026: VERSİYON = YALNIZ O DOSYA.** Erkan iki turda bildirdi
> (*"ne alaka dosyaları arşivleme"*, sonra *"aynı hata teknik resim ve
> montajda devam ediyor"*). Önce parçanın gerçek hatası onarıldı (in-context
> montaj referansı izleniyordu: 162 dosya), sonra karar sorulup
> **"yalnız o dosya"** seçildi → çocuk toplama tümden kalktı,
> `BelgeAgaci.cs` + `Surumler.Cocuklar.cs` **silindi**. Eski çocuklu
> arşivler bozulmadı (on birim testi kilitliyor). Kabul edilen bedel:
> arşivdeki montaj/teknik resim bugünkü parçalarla açılır — durum çubuğu
> **söylüyor**.

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
- **Arşiv artık dosyayla birlikte TAŞINIYOR (31.08.2026).** Kanca çekirdekte
  (`DosyaIslemleri.YenidenAdlandir` · `.Tasi` · `ReferansOnarimi`'nin ad
  değiştirmesi), yani adlandırma · taşıma · sürükle-bırak · geri/ileri alma
  hepsi kapsanıyor. Kalan ölçülmüş sınır: **hedefte zaten bir arşiv varsa**
  (çöpe gitmiş eski bir dosyadan kalmış olabilir) taşıma yapılmaz — ikisi de
  yerinde bırakılır ve sebep yazılır. Birleştirme kurgusu yok; gerekirse
  kullanıcı `.SwPdmSurum` altında elle çözer.
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

- **TAŞINAN DOSYANIN KENDİ YOLLARI ONARILMIYOR (01.09.2026, Erkan buldu).**
  `ReferansOnarimi.TasimaPlani` yalnızca **dışarıda kalan ebeveynleri**
  onarıyor — taşınan dosyanın *içinde yazan* yollar bayat kalıyor ve satır
  KIRIK'e düşüyor (ölçüldü: `TasinanDosyaninKendiYoluTestleri`).
  **Neden otomatikleştirilmedi:** `Ctrl+Z` ile geri alma. Yeni klasöre göre
  yazılan yol, dosya eski yerine döndüğünde bu sefer **orada** bayat olurdu
  (§1a). Doğru çözüm, öz-onarımı `OnarimPlani`'na katıp `GeriOnar`/
  `YenidenOnar` yoluna sokmak — plan kaydı ikinci bir iş türü taşımalı.
  **Kullanıcının bugünkü yolu var ve kılavuzda yazılı:** `Ctrl+Shift+D` →
  "Bayat yollar" → "Bulunanları düzelt".

- **"Aç" işlemi ÖLÇÜLEMİYOR — kabul edilen kör nokta (01.09.2026).**
  `AcIslemi.Kisayol` **olamaz**: tek başına `Enter`'ı `ShortcutKeys`'e
  yazmak uygulamayı hiç açtırmıyor (§6, bedeli ödendi). Kısayolsuz işlem
  de `AgacMenusu.TusaBasildi`'de atlanıyor, yani gövdesine **yalnız menü
  tıklamasıyla** girilebiliyor — ve Wine'da her `ToolStripDropDown`
  uygulamayı çökertiyor (§11). Ölçülebilen tek yol menüyü açmaktan geçiyor
  ve o yol burada kapalı. Gövdesi zaten tek satır (`DosyaAcici.Ac`) ve o
  çağrı **panelde çift tıkla ölçülüyor** (16. ölçüm), yani risk sınırlı.
  Açılırsa: Wine'ın menü çökmesi çözülürse ya da işlem ikinci bir
  modifiyeli kısayol alırsa ölçülebilir olur.
- **Pano (`Ctrl+X`/`Ctrl+V`) ve sürükle-bırak için kapı ölçümü yok.**
  Silme 21. ölçümle kapandı; bu ikisi aynı sınıfta ama daha pahalı:
  sürükle-bırak `xdotool` ile basılı-tut/sürükle gerektiriyor ve ölçümün
  kendisi kırılgan. Çekirdek tarafı `DosyaIslemleriTestleri` ile testli;
  ölçülmeyen, arayüz zinciri.

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
  · ~~**Excel/CSV parça listesi (BOM)**~~ — **REDDEDİLDİ (31.08.2026).**
    Yazıldı, denendi, aynı gün kaldırıldı; Erkan: *"solidworkun içinde var
    zaten."* Bir daha önerilmeyecek.
  · **özellik DÜZENLEME**: seçili dosyaların özelliğini toplu değiştirmek
    (dosyaya yazma altyapısı `SwYazici` ile hazır)
- **Bilgi bloğunun yerine ne konacak — AÇIK SORU.** Blok kalktı (Erkan:
  *"gerek yok"*), ama Tür/Boyut/Tarih artık yalnızca durum çubuğunda ve
  ağaçtaki sütunlarda. Gerçek veride bir eksiklik hissedilirse seçenek:
  önizleme başlığının sağına **tek satır** özet (boyut · tarih) koymak —
  panelin sadeliğini bozmadan. Erkan söylemeden yapılmayacak.

- **ÇOKLU KULLANICI — ERTELENDİ (31.08.2026), gerekçesiyle.** Erkan: *"burda
  çok iş var, mevcut özellikleri ve arayüzü iyice oturtup ondan sonra mı
  baksak"* — ve haklı: temel bilinmeyeni (**ağ paylaşımında dosya kilidi
  atomik mi**) burada ölçülemiyor, üstelik tek kullanıcının akışı henüz
  Erkan'ın makinesinde tam doğrulanmadı. Yerine **klasör kilidi** yazıldı
  (kaza koruması, tek kullanıcı). Çoklu kullanıcı bir gün gelirse sırası:
  (1) ağ ölçüm aracı — iki süreç tek paylaşımda `FileMode.CreateNew` için
  yarışır, SMB önbelleği kaç saniye gecikiyor, saat farkı ne;
  (2) kilit çekirdeği + kim/ne zaman; (3) işlemlerin kilide bakması —
  en kritik yer `ReferansOnarimi`'nin **yazacağı montajlar**;
  (4) günlük + "kimde ne var" penceresi. Kilit listesi zaten kökün içinde
  (`.SwPdmKilit`), yani oraya doğru büyüyebilir.
- **Klasör düzeni turu.** Gezgin / Referans / Versiyon / Paylaşım ayrı
  klasörlere (yalnız `git mv`, sıfır kod değişikliği — bu depoda klasör ad
  alanı değil). Erkan'ın isteği; alan **üç değil dört**, dördüncüsü ve en
  değerlisi **referans koruma**.
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

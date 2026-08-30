# SIRADAKİ — yeni bir oturum buradan devam eder

> Bu dosya **CLAUDE.md değildir.** Oradakiler ölçülmüş, bayatlamayan
> gerçekler; burası **bugünün açık işleri**. Bir iş bitince bu dosyadan
> silinir; hepsi bitince dosya silinir.
>
> Son durum: dal `claude/v2-pdm-start-u058ey` (bu dosyayı taşıyan commit'in
> kendisi); `main` de aynı yerde (ileri sarma, 30.08.2026).
> 320 test · 315 geçti · 5 atlandı (Windows'a özel) · **BEŞ** kapı TEMİZ
> (harita + boyut + derleme + test + çalıştırma 15 ölçüm).
> Erkan §1b düzen turundan sonra denedi (29.08.2026): *"her şey çalışıyor."*
> Ctrl+Y, uzantı kilidi, yeni klasör adı sorma — hepsi onun makinesinde
> doğrulandı.
>
> **Kullanım kılavuzu artık var:** `OZELLIKLER.md` (pakete `OZELLIKLER.txt`
> olarak giriyor). Bir düğme/kısayol değişirse orası da değişmeli.

---

## Bitmiş olan (yeniden yapılmayacak)

Dosya yöneticisi tarafı **ve** PDM tarafı çalışıyor: referans indeksi
(kim kimi kullanıyor), yedi rapor, SOLIDWORKS'süz önizleme, belge ve özel
özellikler, silmeden önce uyarı, referansa çift tıklayıp gitme, taşırken
bağımlıları da götürme, "kök dışında" ayrımı (30.08.2026: diskte var ama
kök dışında olan referans artık "bulunamadı" değil; gizlenmiyor,
önizleniyor, kendi raporu var).

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
- **"Kök dışında" satırları Erkan'ın gerçek verisinde.** Buradaki ölçüm
  yapay dosyayla (Wine'da `C:\Users\PC\...` yaşamıyor). Onun 43 referanslı
  montajında gizlenen satırların kaçının "kök dışında"ya dönüştüğü ve
  panelin okunur kalıp kalmadığı ancak orada görülür; kalabalıksa tek
  dosyalık iş — gizlemeye çevrilir.
  **İlk sürüm onun makinesinde DONDU** (30.08.2026): `File.Exists` seçim
  anında, arayüz iş parçacığında koşuyordu (CLAUDE.md §4'e yazıldı).
  Yoklama taramaya taşındı; **donmanın gittiği ve ilk taramanın süresi**
  onun makinesinde ölçülecek — tarama cümlesi süreyi zaten yazıyor.

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

# SW PDM — TÜM ÖZELLİKLER

Her madde: **ne yapılır → ne olur.** Hepsi koddan çıkarıldı; karşılığı
olmayan hiçbir şey buraya yazılmadı.

---

## 1. Genel

- **Uygulama** → SOLIDWORKS dosyalarını taşırken/adlandırırken montaj ve teknik resim referanslarını koruyan dosya yöneticisi.
- **Pencere** → iki sekme: **Dosyalar** ve **Ayarlar**.
- **Kök klasör** → uygulamanın çalıştığı ağacın tepesi; her şey (arama, tarama, çöp kutusu, raporlar) bu kökün içinde geçer.
- **Açılış** → en son kullanılan kök kendiliğinden açılır; klasör artık yoksa sebebi yazılır ve o kök geçmişten düşer.
- **Komut satırı** → `SwPdm.exe --klasor <yol>` ile doğrudan o kökle açılır; "son açılanlar" listesi yine yüklenir.
- **Beklenmedik hata** → kırmızı ikonlu bir kutuda sebebiyle gösterilir, sessizce kapanmaz.

---

## 2. Ağaç — fare

- **Sol tık (satırın herhangi bir yeri)** → yalnız o öğe seçilir; adın sağı da satıra dahildir.
- **Sol tık (soldaki girinti)** → seçim boşalır.
- **Sol tık (+ / − kutusu)** → dal açılır/kapanır, seçim değişmez.
- **Ctrl + tık** → o öğeyi seçime ekler ya da seçimden çıkarır.
- **Shift + tık** → son tıklanandan buraya kadar olan satırları seçer.
- **Sağ tık (seçili öğede)** → çoklu seçim bozulmaz, menü açılır.
- **Sağ tık (herhangi bir yer)** → işlem menüsü açılır.
- **Çift tık (dosya)** → dosyayı Windows'un varsayılan uygulamasıyla açar.
- **Çift tık (klasör)** → dalı açar/kapatır.
- **Sürükle-bırak (klasör üstüne)** → seçilenleri o klasöre **taşır**; onay kutusu çıkar.
- **Ctrl + sürükle-bırak** → taşımaz, **kopyalar**; sürüklerken imleç de bunu gösterir.
- **Sürükle-bırak (dosya ya da boşluk üstüne)** → kabul edilmez, hiçbir şey olmaz.
- **Sürüklerken hedef klasör** → sarı zeminle işaretlenir; kapalı bir klasörün üstünde ~1 saniye beklersen dal açılır.

> **Dikdörtgenle (kutu) seçim YOK** — kodda böyle bir şey bulunmuyor.

---

## 3. Ağaç — klavye

- **Ok tuşları** → satırlar arasında gezinir, seçim tek öğeye iner.
- **Shift + Yukarı/Aşağı/Home/End** → aralık seçer.
- **Ctrl+A** → **yalnızca içinde bulunulan klasörün** doğrudan çocuklarını seçer (kökün tamamını değil); klasör kapalıysa önce açar.
- **Enter (dosya)** → dosyayı açar.
- **Enter (klasör)** → dalı açar/kapatır.
- **Enter (seçim yok)** → "Önce bir dosya seçin." yazar.
- **Backspace** → bir üst klasöre çıkar; köktesen "Zaten en üst klasördesiniz." der.
- **Esc** → önce **süren işi iptal eder**; iş yoksa aramadan çıkar (arama kutusu odaktayken de çalışır).
- **Ctrl+A (boş klasörde / kök yokken)** → seçim bozulmaz ve **sebebi yazılır**.

---

## 4. Sağ tık menüsü — 14 işlem

| İşlem | Kısayol | Ne olur | Ne zaman gri kalır |
|---|---|---|---|
| **Yeni klasör** | `Ctrl+Shift+N` | **Adı sorar** (çakışmayan bir ad dolu gelir), sonra klasörü açar ve seçili gelir | Arama sonucundayken · kök yokken |
| **Yeniden adlandır** | `F2` | Ad kutusu açar; SOLIDWORKS dosyasıysa onu kullananları da onarır | Seçim yokken · birden çok öğe seçiliyken |
| **Sil** | `Delete` | Seçilenleri **çöp kutusuna taşır** (kalıcı silmez) | Seçim yokken · kök yokken |
| **Kes** | `Ctrl+X` | Seçilenleri panoya "taşınacak" olarak koyar | Seçim yokken |
| **Kopyala** | `Ctrl+C` | Seçilenleri panoya "kopyalanacak" olarak koyar | Seçim yokken |
| **Yapıştır** | `Ctrl+V` | Panodakileri etkin klasöre taşır/kopyalar; menüde kaç öğe ve hangi iş olduğu yazar | Pano boşken · arama sonucundayken · hedef yokken · **kesilenler zaten o klasördeyken** |
| **Geri al** | `Ctrl+Z` | Son dosya işlemini geri alır (menüde adı yazar); "Değiştir" ile çöpe giden dosyayı da geri yükler, ad değiştiyse söyler. **En fazla 20 adım** tutulur | Geri alınacak iş yokken |
| **İleri al** | `Ctrl+Y` | Geri alınan işlemi **yeniden yapar** (menüde adı yazar) | İleri alınacak iş yokken · adımın tersi yoksa |
| **Boyutu hesapla** | `Ctrl+Shift+B` | Seçili klasörlerin toplam boyutunu hesaplar, durum çubuğuna yazar | Hesaplama sürerken · seçimde klasör yokken |
| **Referansları tara** | `Ctrl+Shift+R` | "Kim kimi kullanıyor" indeksini kurar (artımlı) | Tarama sürerken · kök yokken |
| **Referansı elle bağla…** | `Ctrl+Shift+L` | Çözülemeyen bir referansı, senin seçtiğin dosyaya bağlar | Tek dosya seçili değilken · tür referans taşımıyorken |
| **Referans raporları** | `Ctrl+Shift+D` | Rapor penceresini açar | Kök yokken |
| **Yenile** | `F5` | Ağacı diskten tazeler, açık dallar korunur | Kök yokken |
| **Ağacı kapat** | `Ctrl+Shift+K` | Bütün dalları kapatır, köke döner | Kök yokken |

- **Gri öğe** → gizlenmez; sebebi ipucunda **ve** fareyle üstüne gelince durum çubuğunda yazar.
- **Kısayolla gri bir işlem denenirse** → sebep doğrudan durum çubuğuna düşer.

---

## 5. Araç çubuğu

- **Klasör aç (klasör simgesi)** → kök klasör seçme kutusunu açar (`Ctrl+O`).
- **Klasör aç → ok kısmı** → daha önce açılan kökleri listeler, birine tıklayınca o kök açılır.
- **Çöp kutusu (N)** → çöp kutusu penceresini açar; parantezdeki sayı içindeki öğe adedidir.
- **Çöp kutusu (kök yokken)** → gri; ipucu "Çöp kutusu — önce bir klasör açın".
- **Çöp kutusu (?)** → çöp kaydı okunamadı demektir; sayı YAZILMAZ (okunamayan bir kutu "boş" gibi gösterilmez), ipucunda sebebi yazar.
- **Geri al** → `Ctrl+Z` ile aynı işi yapar; ipucunda geri alınacak işlemin adı yazar.
- **Ara kutusu** → yazdıkça kök içinde arar; içinde "Ara..." yer tutucusu yazar.

---

## 6. Başlık şeridi

- **Raptiye düğmesi** → pencereyi hep üstte tutmayı açar/kapatır; açıkken düğme vurgulu, durum çubuğunda yazar.
- **Dişli düğmesi** → Ayarlar sekmesine geçer.

---

## 7. Tür süzgeci ve sıralama

- **Tümü** → süzgeci kaldırır, her şey görünür.
- **Montaj / Parça / Teknik resim / PDF** → ağaçta yalnız o türü bırakır.
- **Sıralama düğmesi** (şeridin sağında, üstünde "Ad ↑" gibi yazar) → ölçüt menüsünü açar: **Ad · Tür · Boyut · Tarih**.
- **Süzgeç ya da sıralama değişince** → durum çubuğuna ne seçildiği **ve kısayolu** yazılır (`Ctrl+Shift+F` · `Ctrl+Shift+S`). Bu şeritteki düğmelerde ipucu **yok**: ipucu penceresi tıklamayı yiyor (ölçüldü).
- **Aynı ölçüte tekrar basmak** → yönü çevirir (artan ↔ azalan).
- **`Ctrl+Shift+S`** → sekiz hâl (4 ölçüt × 2 yön) arasında sırayla ilerler.
- **`Ctrl+Shift+F`** → tür süzgecini ilerletir (Tümü → Montaj → Parça → Teknik resim → PDF → Tümü); süzgeç düğmeleri fare istemesin diye.
- **Sıralama ve süzgeç** → kalıcıdır, uygulama kapanıp açılınca geri gelir.

---

## 8. Yol çubuğu (ağacın üstü)

- **Yol parçaları** → seçili klasörün yolunu `›` ile ayrılmış gösterir.
- **Kök altındaki bir parçaya tık** → ağaç oraya gider ve orayı seçer.
- **Arama sonucundayken tık** → çalışmaz ve **sebebi yazılır** ("önce aramayı temizleyin"); ağaçta o an yalnızca eşleşmeler var.
- **Kökün üstündeki parçalar** → soluk ve tıklanamaz; oraya gitmek için "Klasör aç".
- **Yol sığmazsa** → soldan kırpılır, başına `…` konur; tam yol ipucunda.
- **`…`'ya tık** → bir üst klasöre gider; her tık bir kademe yukarı, yani gizlenen parçaların hepsine ulaşılır.

---

## 9. Arama

- **Kutuya yazmak** → son tuştan 350 ms sonra arama başlar.
- **Enter** → beklemeden hemen arar.
- **Ne aranır** → yalnızca **dosya adı** (büyük/küçük harf ayrımı yok, "içeriyor" eşleşmesi).
- **Sonuç** → ağaç arama kipine geçer: kökte `— "metin": N eşleşme`, altında klasör klasör gruplanmış eşleşmeler.
- **Süzgeç açıkken** → sonuç da süzülür, özet "N / M eşleşme (süzgeç açık)" der.
- **Sınır** → 2000 eşleşme; sınıra ulaşılırsa durum çubuğunda söylenir.
- **Arama sürerken** → ağaç geçici olarak kilitlenir, durum çubuğunda ilerleme yazar.
- **Esc ya da kutuyu boşaltmak** → gezinmeye döner, arama öncesi açık dallar geri gelir.
- **Arama sonucundayken dosya işlemi** → sonuç yeniden üretilir, arama kipi düşmez.

---

## 10. Önizleme paneli (sol alt)

- **Üstteki ad** → **o an önizlenen dosyanın adı.** Referans satırına tıklayıp komşu bir dosyaya bakarken başa `◂` gelir ve **tıklayınca ağaçta seçili dosyaya döner**. Panelde başka bilgi satırı yok (30.08.2026: gerek görülmedi) — tür, boyut, tarih durum çubuğunda; referans sayıları sağdaki bölüm başlıklarında.
- **Resim (2B — varsayılan)** → sırayla üç kaynaktan denenir: Windows kabuğu (Gezgin ne gösteriyorsa) → SOLIDWORKS dosyasının içindeki önizleme → eski sürümlerin gömülü önizlemesi.
- **3B kip (Ayarlar'dan açılır)** → SOLIDWORKS dosyaları **eDrawings** ile açılır: döndür, yakınlaş, kaydır. PDF ve öteki türler yine 2B yoldan gösterilir. eDrawings kurulu değilse ya da dosyayı açamazsa **sebep durum çubuğuna yazılır** ve 2B'ye dönülür.
- **3B kipte hız** → dosya gerçekten açılır: parça hızlı, büyük montaj bekletebilir (eDrawings kendi ilerlemesini gösterir).
- **3B kipte dosya kilidi** → eDrawings açık belgeyi tutabilir; her işlem (taşıma, ad değiştirme, silme…) başlamadan belge kendiliğinden bırakılır.
- **Yüklenirken** → "Önizleme yükleniyor…" yazar (boş kutu bırakılmaz).
- **Hiçbiri yoksa** → "Önizleme yok" ya da okunamama sebebi yazar.
- **PDF'te önizleme yoksa** → PDF okuyucunun Gezgin küçük resim ayarını açmayı anlatan yönlendirme çıkar.
- **Klasör seçilince** → "Klasör" yazar.
- **Çoklu seçimde** → "N öğe seçildi" yazar.

---

## 11. Referans paneli (sağ alt)

Üstte **dört sekmelik bir şerit**, altında o sekmenin listesi. Şerit sabittir —
liste kaydırılınca kaybolmaz, yani "hangi yöne bakıyorum" sorusu her an cevaplı.

- **İÇİNDEKİLER** (varsayılan) → bu dosyanın **içinde** kullandığı dosyalar (aşağı yön).
- **KULLANILDIĞI YERLER** → bu dosyayı kullanan dosyalar (yukarı yön).
- **KIRIK** → SOLIDWORKS'ün açamayacağı referanslar: `BULUNAMADI` (bu adda dosya taranan ağaçta yok) ve `yol BAYAT` (dosya duruyor ama belgedeki yol başka yeri gösteriyor).
- **VERSİYONLAR** → dosyanın arşivlenmiş versiyonları; ayrıntısı aşağıda (§11a).
- **Her sekmede SAYI yazar** → "İÇİNDEKİLER 14" · "KULLANILDIĞI YERLER 4 dosya" · "KIRIK 29 dosya". Sekmeyi açmadan da durumu görürsün; "yok" ile "taranmadı" asla aynı kelimeyle yazılmaz.
- **Sayı, o sekmede GERÇEKTEN duran satır kadardır.** İÇİNDEKİLER'in sayısı kırıkları **içermez** — onlar KIRIK sekmesinde sayılır. (Önceden toplam yazıyordu: sekme "43" derken listede 14 satır vardı.)
- **Ctrl+Shift+E** → sıradaki bölüme geçer, sonunda başa döner.
- Şerit dar pencerede **alt satıra sarar**; hiçbir sekme gizlenmez.
- **`içinde`** → referans çözüldü, dosya bulundu.
- **`içinde? N aday`** → aynı adda birden çok dosya var, hangisi olduğu kesin değil (uydurulmuyor).
- **`yol BAYAT`** → dosya duruyor ama belgenin içindeki yol başka yeri gösteriyor → SOLIDWORKS açamaz.
- **`kullanan`** → KULLANILDIĞI YERLER bölümünde, bu dosyayı kullanan belge.
- **Kırık referanslar İÇİNDEKİLER'de görünmez** → hepsi KIRIK sekmesinde ve sayısı orada yazıyor; ayrıca `Ctrl+Shift+D` raporlarında da duruyorlar.
- **İpucu (fareyle üstüne gel)** → çözülen satırda dosyanın **tam yolu**, çözülemeyende dosyanın **içinde yazan yol**.
- **Tek tık (ya da ok tuşuyla gezinme)** → soldaki önizleme **o satırdaki dosyaya** döner (üstteki başlık da o dosyanın adını yazar); ağaçtaki seçim BOZULMAZ. Üstteki `◂` adına tıklayınca seçili dosyaya dönülür.
- **Çift tık / Enter** → o dosyaya gider, ağaçta açıp seçer; gidilemiyorsa sebebi yazar.
- **Ctrl+C (panel odaktayken)** → satırın yolunu panoya kopyalar ve ne kopyalandığını yazar.
- **SAĞ TIK** → ağaçtakinin **aynı menüsü**, ama işlem **o satırdaki dosyaya** uygulanır; ağaçtaki seçim ve okunan liste yerinde kalır. Sağ tık önce o satırı seçer ve menünün en üstünde **hedefin adı** yazar — hangi dosyaya uygulanacağı görünmeden hiçbir işlem çalışmaz.
- **Kısayollar da aynı hedefe gider** (panel odaktayken): `F2` · `Delete` · `Ctrl+X` · `Ctrl+V` … satırın dosyasına uygulanır.
- **Menüde "Kopyala" yanında kısayol yazmaz** → `Ctrl+C` bu panelde "yolu kopyala"dır; iki ayrı iş, ikisi de duruyor.
- **Ctrl+Shift+L (panel odaktayken)** → **satıra değil**, panelin gösterdiği (ağaçta seçili) dosyaya uygulanır: düzeltilecek yazı onun içinde durur. Çözülemeyen bir satıra bakarken de çalışması bu yüzden.
- **Satırda dosya yoksa** (açıklama satırı, "BULUNAMADI", kök dışında kalan yol) → menü yine açılır ama dosya işlemleri **gri durur ve sebebini söyler**; sessizce başka bir dosyaya uygulanmaz. KIRIK bölümündeki `yol BAYAT` satırlarının hedefi ise **gerçek dosyadır**, işlem ona gider.
- **Boş bölüm** → sebebini yazar ("Başka dosya kullanmıyor." / "Hepsi kırık — KIRIK bölümünde." / "Bunu kullanan dosya yok." / "Kırık referans yok." / "Bu kök henüz taranmadı."). Liste **doluyken** ayrıca uyarı satırı çıkmaz.
- **Tarama yarım kaldıysa** → sekmede fazladan bir kelime yazmaz; ama liste **boşsa** sayı "yok" değil **"taranmadı"** olur ve bölümün içindeki satır sebebini yazar. Ayrıntı ("EKSİK — 15 dosya okunamadı") durum çubuğundaki tarama cümlesindedir.
- **Klasör, çoklu seçim ya da SOLIDWORKS olmayan dosya** → panel boş kalmaz, **neden boş olduğunu** yazar ("SOLIDWORKS dosyası değil" · "Seçim yok" · "taranmadı").

---

## 11a. Versiyonlar

Model: **aynı ad, tek dosya + gizli arşiv.** Dosya hep aynı adla yerinde durur;
eski içerikler kökün içindeki gizli `.SwPdmSurum` klasöründe tam kopya olarak
saklanır (çöp kutusu gibi: aynı disk, ağaçta görünmez, taranmaz). Parçayı eski
versiyona döndürmek montaj dosyasına **hiç dokunmaz** — ad değişmediği için
referanslar kendiliğinden sağlam kalır.

- **Her dosya v0 doğar.** Hazırlık gerekmez; ilk "Yeni versiyon oluştur" o anki içeriği **v0** olarak arşivler, sonrakiler v1, v2…
- **`Ctrl+Shift+U` (ya da sağ tık → "Yeni versiyon oluştur…")** → kısa bir not sorar (boş geçilebilir), o anki içeriği arşivler. Dosya yerinde kalır, çalışmaya devam edersin. Yalnız SOLIDWORKS dosyaları.
- **VERSİYONLAR sekmesi** → v0…vN listesi (en yeni üstte): solda `v3 — not`, sağda tarih. Fareyle üstüne gelince (ve `Ctrl+C` ile) **arşiv kopyasının tam yolu**.
- **Tek tık** → soldaki önizleme **o versiyonun** resmine döner; başlıkta `◂ v3.SLDPRT` yazar, başlığa tıklayınca bugünkü dosyaya dönülür. (3B kip açıksa versiyon eDrawings'te döndürülerek de incelenir.)
- **Çift tık** → o versiyonun arşiv kopyasını **açar**. Kopyalar diskte **salt-okunur** durur; SOLIDWORKS `[Read-Only]` açar, geçmişin üstüne kaza ile kaydedilemez.
- **Versiyon satırına sağ tık** → dosya işlemleri uygulanmaz (gri + sebep): F2/Sil arşive gitseydi kayıt ile kopya eşleşmesi kırılırdı.
- **Bu versiyona dön** → sekmede satırı seç, **Enter**. Onay kutusu iki şeyi açıkça söyler: **bugünkü hâl önce otomatik arşivlenir** (dönüş de bir versiyondur — hiçbir içerik hiçbir işlemle kaybolmaz) ve bu dosyayı kullanan **bütün montajlar** dönülen içeriği görür.
- **SOLIDWORKS'te açık dosyaya dönülmez** → `~$` kilidi varken işlem reddedilir ve sebebi yazılır; belgeyi kapatıp yeniden dene.
- **Açık sınır** → uygulama kapalıyken yaptığın bir değişikliğin *önceki* hâli, daha önce versiyonlanmadıysa **kurtarılamaz**. Alışkanlık: düzenlemeye başlamadan `Ctrl+Shift+U`. (Belge kapanınca kendiliğinden soran akış sonraki sürümde geliyor.)
- **Açık sınır 2** → ad değiştirme/taşıma arşivi henüz **taşımıyor**: versiyonlu dosyayı adlandırırsan liste "yok" görünür (kayıp değil — arşiv eski adla diskte durur). Şimdilik versiyonlu dosyanın adını değiştirme; sonraki sürümde kendiliğinden taşınacak.
- Bozuk ya da arşivi kayıp kayıtlar gizlenmez; listenin altında sayısıyla söylenir.

---

## 12. Referans onarımı (uygulamanın varlık sebebi)

- **Yeniden adlandırma (F2)** → adı değişen dosyayı kullanan belgelerin **içine** yeni ad yazılır.
- **Taşıma (sürükle / Ctrl+X-V)** → taşınan dosyayı kullanan belgelerin içindeki yol yeni konuma çevrilir.
- **Birlikte taşınan aile** → onarılmaz, gerekmez: SOLIDWORKS önce ebeveynin yanına bakar.
- **Kopyalama** → onarım yapılmaz; kopya, özgün parçaları göstermeye devam eder (onay kutusunda yazar).
- **Toplu onarım** → raporlardaki "Bayat yollar" sekmesinde **"Bulunanları düzelt"** düğmesi; geçmişte kırılmış bağları toparlar.
- **Elle bağlama (`Ctrl+Shift+L`)** → otomatik bulamıyorsa hedefi sen seçersin (ör. dosyanın adı dışarıda değişmişse).
- **Her onarım** → KOPYALA → YAMA → DOĞRULA → DEĞİŞTİR; tutmazsa asıl dosya **hiç değişmez** ve sebep söylenir.
- **SOLIDWORKS'te açık dosya** (yanında `~$` kilidi) → yazılmaz, atlanır ve söylenir.

---

## 13. Raporlar (`Ctrl+Shift+D`) — 6 rapor

- **Kırık referanslar** → içinde yazan bir dosyanın karşılığı taranan ağaçta bulunamadı.
- **Bayat yollar** → dosya duruyor ama belgedeki yol başka yeri gösteriyor; **düzeltilebilir**.
- **Yetim parçalar** → taranan ağaçta hiçbir montajın/teknik resmin kullanmadığı parçalar.
- **Teknik resmi olmayanlar** → hiçbir teknik resmin baz almadığı parça ve montajlar.
- **Taşınmış dosyalar** → son kaydedildiği yer ile şimdiki yeri farklı olan dosyalar.
- **Okunamayan dosyalar** → referansları çıkarılamayan dosyalar (öteki raporlar bu kadar eksik).
- **Sekme başlığındaki sayı** → bulgu adedi; tarama güvenilir değilse sayı yerine `(?)` yazar.
- **Satıra çift tık / Enter** → pencere kapanır ve o dosya ağaçta seçilir.
- **"Bulunanları düzelt (N)"** → yalnızca "Bayat yollar"da çıkar; onay ister, sonra pencereyi kapatır (listeler bayatladığı için). Düzeltilecek bir şey çıkmazsa düğme geri açılır ve sebebi durum çubuğuna yazılır.
- **Kapat / Esc** → pencereyi kapatır.

---

## 14. Çöp kutusu

- **Yeri** → kökün içinde `.SwPdmCop` klasörü (Ayarlar'dan başka bir yere alınabilir); ağaçta görünmez.
- **Neden Windows Çöp Kutusu değil** → ağ sürücüsünden silinen dosya oraya gitmez, kalıcı gider.
- **Silme** → aynı diskte **taşımadır**, yani 1 GB'lık montaj bile anında gider.
- **Sütunlar** → Ad · Eski konum · Silinme · Boyut (klasörde "klasör" yazar).
- **Geri yükle** → seçilenleri eski yerine koyar. **Aynı adda bir şey varsa çakışma kutusu çıkar** (Atla · İkisini de tut · Değiştir — eskisi çöpe gider · Vazgeç); numaralanan adlar sonunda tek tek raporlanır.
- **Üst satır** → "N öğe.   Yeri: <yol>"; kayıt okunamıyorsa **sebebi** yazar — "boş" DEMEZ.
- **Okunamayan çöp kutusu** → "Tümünü boşalt" çalışmaz; elimizdeki liste eksik olabileceği için sebebi kutuda söylenir.
- **Çift tık / Enter** → seçileni geri yükler.
- **`Delete`** → seçileni kalıcı siler (onay ister).
- **Sütun başlığına tık** → o sütuna göre sıralar; aynı başlığa ikinci tık yönü çevirir.
- **Kalıcı sil** → seçilenleri geri dönüşsüz siler; **onay ister**, varsayılan düğme Vazgeç.
- **Tümünü boşalt** → çöpteki her şeyi geri dönüşsüz siler; **onay ister**, varsayılan düğme Vazgeç.
- **Kapat / Esc** → pencereyi kapatır, ağaç tazelenir.
- **Toplu işlerde ilerleme** → üst satırda "N/M" yazar. **İptal yok**: pencere modal ve iş arayüz iş parçacığında koşuyor, tıklanamayacak bir İptal düğmesi konmadı.
- **Kendiliğinden temizlik YOK** → çöp yalnızca sen boşaltınca boşalır.

---

## 15. Ayarlar sekmesi

- **Çöp kutusu yolu (salt okunur kutu)** → çöp kutusunun o anki tam yerini gösterir.
- **Değiştir…** → çöp kutusunun konacağı üst klasörü seçtirir; başka diskse uyarır (silme kopyalamaya döner).
- **Varsayılana dön** → çöp yine kökün içine alınır; zaten öyleyse bunu söyler.
- **Çöp yeri değişirken eskisinde öğe varsa** → uyarı çıkar ve eski yol yazılır. **Taşınmaz** (başka diske kopyalamaya döner, dakikalar sürebilir); geri yüklemek için eski klasörü tekrar seçmek gerekir.
- **"Diskte bir şey değişince ağacı kendiliğinden tazele"** → disk izleyicisini açar/kapatır (varsayılan açık); açıp kapattığında **durum çubuğuna ne olduğunu yazar**.
- **"3B önizleme (eDrawings)"** → önizlemeyi 3B kipe alır (varsayılan kapalı = hızlı 2B); değiştirince açık seçim hemen yeni kiple çizilir ve durum çubuğuna yazılır.
- **Ayarlar: <yol>** → ayar dosyasının yerini gösterir (`%APPDATA%\SwPdm\ayarlar.txt`).
- **Ayar yazılamazsa** → "Ayar bu oturumda geçerli ama diske YAZILAMADI" uyarısı çıkar.

**Saklanan ayarlar:** son açılan kökler (en fazla 10) · çöp üst klasörü · sıralama · otomatik tazeleme · pencere boyutu · iki bölücünün yeri · son tür süzgeci · 3B önizleme seçimi.
**Saklanmayan:** pencere **konumu** (bilerek: ikinci ekran çıkarılınca pencere görünmez bir yerde açılabilirdi).

---

## 16. Kutular

- **Onay kutusu** → düğmeleri hep **Evet / Vazgeç**; tehlikeli işlemlerde varsayılan **Vazgeç**.
- **Kutu ne zaman çıkar** → yalnızca (1) geri alması zor bir işlemin onayı, (2) görmeden geçilmemesi gereken bir hata. Bilgi durum çubuğuna yazılır.
- **Ad kutusu (F2 · Ctrl+Shift+N)** → ad dolu ve seçili gelir; geçersiz adda sebep anında kırmızı yazar ve Tamam gri kalır.
- **Uzantı ayrı kutuda ve KİLİTLİ** → dosya adının uzantısı kazayla değişemez.
- **"Uzantıyı da değiştir"** → işaretlenirse uzantı kutusu açılır ve "DİKKAT: uzantı değişiyor" uyarısı anında görünür; işaret kalkınca eski uzantı geri gelir.
- **Aynı adda öğe varsa** → kutuda anında söylenir, Tamam gri kalır (sessizce "(2)" eklenmez).
- **Ad çok uzunsa** → 255 karakteri ya da tam yolda 259 karakteri aşan ad kabul edilmez, sebebi yazılır.
- **Onarım kutusu** → adı değişen dosyayı kullananları (en fazla 12 ad) listeler; tarama yapılmamışsa "kimin kullandığını bilmiyoruz" der ve varsayılan Vazgeç olur.
- **Çakışma kutusu** → hedefte aynı ad varsa; iki tarafı boyut ve tarihle karşılaştırır.
  - **İkisini de tut** → yeni gelen "(2)" ekiyle konur (varsayılan).
  - **Atla** → bu öğe olduğu yerde kalır.
  - **Değiştir — eskisi çöp kutusuna gider** → üzerine yazılan dosya yok edilmez, çöpe taşınır (klasörlerde bu seçenek yok).
  - **Vazgeç** → işlemin tamamı iptal.
  - **"Kalan bütün çakışmalara da uygula"** → aynı karar kalanlara sorulmadan uygulanır.
- **Silme onayı ("Çöp kutusuna gönder")** → ne silineceğini sayar (en fazla 10 ad), "çöp kutusundan geri yüklenebilir" der, varsayılan düğme **Vazgeç**.
  - **Referans uyarısı** → tarama yoksa "kimin kullandığını BİLMİYORUZ", tarama varsa "bunları N dosya KULLANIYOR" (en fazla 8 ad) yazar. **Uyarır, engellemez.**
  - **Açık dosya uyarısı** → SOLIDWORKS'te açık görünen (yanında `~$` kilidi olan) öğeler sayılır ve adlanır. Silme ve taşıma/kopyalama onaylarının ikisinde de çıkar. **Uyarır, engellemez.**
- **Taşı/Kopyala onayı** → ne taşınacağını ve hedef klasörü yazar; taşımada "kullandığı N dosya taşınmıyor; referansları onarılacak" der.
- **Elle bağlama** → (1) "Hangi referans bağlanacak?" (tek aday varsa atlanır), (2) "Dosyayı seç" — **kendi ağacımız**, Windows kutusu yok, (3) onay: eski ve yeni yol tam yazar, **geri alınamaz** olduğu söylenir.

---

## 17. Durum çubuğu

- **Sol taraf** → nerede olduğun: seçili dosyanın adı · boyutu · tarihi, klasörde yol, çoklu seçimde özet.
- **Sağ taraf** → ne olduğu: işlem sonuçları, tarama özeti, arama ilerlemesi, hata sebepleri.
- **İlerleme çubuğu + sayaç + İptal** → yalnızca uzun bir iş sürerken görünür (tarama, taşıma, boyut hesabı).
- **İptal** → iş öğeler arasında durur; yarım dosya bırakılmaz.

---

## 18. Otomatik tazeleme

- **Diskte değişiklik** → ağaç kendiliğinden tazelenir (seçim ve açık dallar korunur).
- **Kendi işlemlerimiz** → izleme kısa süre susturulur, "başkası yaptı" gibi görünmez.
- **İzleme kurulamazsa/koparsa** → sebep yazılır, `F5` ile elle yenileme her zaman var.
- **Kapatmak** → Ayarlar sekmesindeki onay kutusundan.

---

## 19. Kısayollar — tek bakışta

| Tuş | İş |
|---|---|
| `Ctrl+O` | Klasör aç (kök seç) |
| `F5` | Ağacı yenile |
| `Ctrl+Shift+K` | Ağacı kapat |
| `Ctrl+Shift+N` | Yeni klasör |
| `F2` | Yeniden adlandır |
| `Delete` | Sil (çöp kutusuna) |
| `Ctrl+X` / `Ctrl+C` / `Ctrl+V` | Kes / Kopyala / Yapıştır |
| `Ctrl+Z` | Geri al (en fazla 20 adım) |
| `Ctrl+Y` | İleri al |
| `Ctrl+A` | İçinde bulunulan klasörü seç |
| `Shift+ok`, `Shift+Home/End` | Aralık seç |
| `Enter` | Dosyayı aç / klasörü aç-kapat |
| `Enter` (referans panelinde) | Seçili referansın dosyasına git |
| `Enter` (arama kutusunda) | Beklemeden hemen ara |
| `Backspace` | Üst klasör |
| `Esc` | Süren işi iptal et; iş yoksa aramadan çık |
| `Ctrl+Shift+S` | Sıralamayı ilerlet |
| `Ctrl+Shift+F` | Tür süzgecini ilerlet (Tümü → Montaj → … → Tümü) |
| `Ctrl+Shift+E` | Referans bölümünü ilerlet (İÇİNDEKİLER → KULLANILDIĞI YERLER → KIRIK → VERSİYONLAR → …) |
| `Ctrl+Shift+U` | Yeni versiyon oluştur (seçili dosyanın o anki hâli arşive) |
| `Enter` (VERSİYONLAR sekmesinde) | Seçili versiyona dön (önce bugünkü hâl otomatik arşivlenir) |
| `Ctrl+Shift+B` | Klasör boyutunu hesapla |
| `Ctrl+Shift+R` | Referansları tara |
| `Ctrl+Shift+L` | Referansı elle bağla |
| `Ctrl+Shift+D` | Referans raporları |
| `Ctrl+C` (referans panelinde) | Satırın yolunu panoya kopyala |

> İşlem kısayolları yalnızca **ağaç** ya da **referans paneli** odaktayken çalışır. `Ctrl+O`, `Ctrl+Shift+S`, `Ctrl+Shift+F`, `Ctrl+Shift+E` ve `Esc` her yerde çalışır.

---

## 20. Bilinen sınırlar (dürüstçe)

- **Kök dışındaki referanslar** → çözücü yalnızca açık kökü bilir; dosya diskte dursa bile kök dışındaysa "bulunamadı" sayılır ve panelde gizlenir. Üst klasörü kök yaparsan çoğu çözülür.
- **254 karakterden uzun yollar** → dosyanın içindeki bu yollar atlanır ve sonucun eksik olduğu söylenir.
- **SOLIDWORKS 2022 dışındaki sürümler** → dosya biçimi 2022 ile ölçüldü; eski sürümlerde referans okuma çalışmayabilir.
- **Toolbox / kütüphane parçaları** → ayrı bir işleme yok; kök dışındaysalar yukarıdaki maddeye girerler.
- **İleri alma her işlemde yok** → "Değiştir — eskisi çöpe gider" seçilmiş bir taşıma/kopyalama geri alındıysa `Ctrl+Y` çalışmaz ve **sebebini söyler**: üzerine yazılan dosya bu arada değişmiş olabilir, tahmin edilmez.
- **`Ctrl+Z` uzun sürerse pencere donar** → geri alma arayüz iş parçacığında koşuyor; ileri yön (taşıma/kopyalama) arka planda ve iptal edilebilir, geri yön değil.
- **Çöp kutusunda iptal yok** → toplu geri yükleme/silme başlayınca pencere iş bitene kadar cevap vermez; ilerleme yazılır.
- **Kök değişince pano ve geri alma listesi boşalır** → eski kökün yolları yeni ağaçta yanlış yere dokunurdu.
- **3B önizleme yalnızca eDrawings kuruluysa çalışır** → kurulu değilse sebep yazılır, 2B devam eder. eDrawings büyük montajı geç açar ve açık belgeyi kilitli tutabilir (işlem başlarken bırakılır). Bu özellik geliştirme ortamında ölçülemedi (eDrawings yok); ilk gerçek ölçüm senin makinende.
- **Boş liste asla "yok" demek değildir** → tarama yapılmadıysa panel sayı yerine "taranmadı" yazar; bu bilerek böyle.

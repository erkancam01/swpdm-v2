# Taşınan dosyanın referansları — düzeltildi

**Şikâyet (Erkan, 02.09.2026):** *"SOLIDWORKS dosyalarını farklı klasöre
taşıdığımda içindekiler ve kullananlar kısmı yok diyor, kırık diyor."*

**Durum: DÜZELTİLDİ ve ölçüldü.**

---

## Kök sebep — ilk kurguladığım akış yanlıştı

`ReferansIndeksi.BayatMi` üç şarta birden bakıyor:

1. dosya bulundu,
2. **ebeveynin yanında değil**,
3. yazılı yol gerçek dosyayı göstermiyor.

İkinci şart kritik: SOLIDWORKS bir çocuğu **önce ebeveynin yanında** arar,
bulamazsa dosyanın içinde **yazılı yola** bakar.

Senin dosyaların bir yerden **kopyalanarak** gelmiş — içlerinde yazan yollar
hâlâ `C:\Users\PC\Desktop\…` gösteriyor. `Taşınmış dosyalar` raporu bunu
zaten söylüyordu: **10 dosyanın 10'u**. Hepsi aynı klasörde durduğu sürece
**komşuluk kuralı** bunu gizliyor; hiçbir şey bozuk görünmüyor.

**Bir dosyayı başka klasöre taşıdığın an o koruma kalkıyor** ve içindeki eski
yol açığa çıkıyor:

- panel → *"İÇİNDEKİLER: hepsi kırık"*
- ve SOLIDWORKS de gerçekten bulamaz — panel doğru söylüyordu.

Yani taşıma yeni bir kırık **üretmiyordu**, var olanı **görünür kılıyordu**.
Ama kullanıcı açısından sonuç aynı: dosya açılmıyor.

**Uygulamadaki eksik adım:** taşıma, *taşınan dosyayı kullananları* onarıyordu
(bu zaten doğru çalışıyor) ama **taşınan dosyanın kendi içindeki yolları**
onarmıyordu. Oysa taşıma tam da o yolların dayandığı komşuluğu bozuyor.

## Düzeltme

`ReferansOnarimi.TasimaPlanlari` içine yeni bir adım: **`KendiYollariPlanlari`**.

Taşınan her dosya için, o dosyanın kendi yazılı yollarından
**taşımadan önce sağlam olup taşımadan sonra çözülemeyecek** olanlar,
çocuğun gerçek konumuna yeniden yazılıyor.

Kapsam bilerek dar (CLAUDE.md 1a):

- taşımadan **önce de** bayat olana dokunulmaz — o bu taşımanın işi değil,
  *"Bulunanları düzelt"*in işi;
- **birlikte taşınan** çocuğa dokunulmaz — komşuluğu bozulmadı;
- **bulunamayan** çocuğa dokunulmaz — yazılacak hedef yok.

Planlar olağan `OnarimPlani`; yani `Ctrl+Z` ve `Ctrl+Y` bedavaya geliyor.

**Yan kazanç:** yazıcı zaten **önce göreli yol** deniyor (`..\parça.SLDPRT`).
Göreli yol kısa olduğu için, önceki turda çıkan *"akış yuvaya sığmıyor
(152 > 150 bayt)"* hatası bu senaryoda **hiç çıkmadı**.

---

## ÖLÇÜM — aynı sınav, düzeltmeden önce ve sonra

Sınav: montaj + teknik resmi **birlikte** alt klasöre taşı (parçalar yerinde kalıyor).

| | ÖNCE | SONRA |
|---|---|---|
| Taşıma sonrası uyarı kutusu | *"Bazı işlemler tamamlanmadı — 1 dosyanın referansı onarılamadı"* | **yok** |
| Bayat yollar | **8** | **0** |
| Taşınan montajın İÇİNDEKİLER'i | *"hepsi kırık"* | **7 dosya, hepsi "içinde"** |
| KULLANILDIĞI YERLER | — | 3 dosya |
| Kırık referanslar | 3 | **3** (aynı üç gerçek bulgu) |

**Geri dönüş de ölçüldü:** aynı iki dosya ana klasöre geri taşındı →
Bayat **0**, İÇİNDEKİLER **7**, Kırık **3**. Yani iki yönde de temiz.

**Kapılar:** derleme `-warnaserror` TEMİZ · testler **392 başarılı, 0 başarısız, 1 atlandı**

---

## Bunun senin için pratik anlamı

- Artık bir montajı/parçayı/teknik resmi başka klasöre taşıdığında, o dosyanın
  içindeki yollar da **gerçek konuma** yazılıyor. Komşuluk kuralına bağımlılık
  bitiyor.
- Dosyaların "kopyalanmış" geçmişi taşıma anında kendiliğinden temizleniyor.
- Hâlâ elle bir toplu temizlik istersen: `Ctrl+Shift+D` → **Bayat yollar** →
  *Bulunanları düzelt*.

## Değişen dosya

`src/SwPdm.Cekirdek/Referans/ReferansOnarimi.cs` — bir yeni metot
(`KendiYollariPlanlari`) ve `TasimaPlanlari` içinde onu çağıran bir satır.

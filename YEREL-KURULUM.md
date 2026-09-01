# YEREL KURULUM — projeyi kendi makinende sürdürmek

> Bu dosya **yeni bir Claude Code oturumunun buradaki oturumla aynı
> davranması** için var. `CLAUDE.md` çalışma kurallarını, `SIRADAKI.md`
> açık işleri anlatıyor; burası **ortamı** anlatıyor.
>
> `CLAUDE.md` §11 bulut kapsayıcısını (Linux + Wine) varsayarak yazıldı.
> Windows'ta bazı şeyler **daha iyi**, bazıları **hiç çalışmıyor**. Aşağıdaki
> ayrım ölçülmüş ile ölçülmemişi karıştırmıyor (§2).

---

## 1. Ne kurulu olmalı (Windows)

| araç | niçin | zorunlu mu |
|---|---|---|
| **Git for Windows** (Git Bash) | `araclar/*.sh` bash betikleri | **evet** |
| **.NET 8 SDK** | derleme, test, çalıştırma | **evet** |
| **Python 3** | yalnız `kapi_ozellikler.sh` | kılavuz kapısı için |
| SOLIDWORKS 2022 | gerçek simge, önizleme, açma | ölçüm için |
| eDrawings | 3B önizleme | isteğe bağlı |

`zip` Git Bash'te **yoktur** → `araclar/paket.sh` yerelde çalışmaz. Gerek de
yok: pakete ihtiyaç bulutta uzaktan deneme içindi, sen doğrudan
çalıştıracaksın.

**Windows'ta bir şey KURMANA GEREK YOK ki bu önemli:** `CLAUDE.md` §11'deki
`EnableWindowsTargeting` + `FrameworkReference` numarası, Ubuntu'da
`Microsoft.NET.Sdk.WindowsDesktop` **olmadığı** için vardı. Windows'un
gerçek SDK'sinde o bileşen zaten var; aynı proje dosyası **iki yerde de**
derleniyor (CI'ın `derleme-windows` işi bunu her push'ta ölçüyor).

## 2. Klonlama

```bash
git clone https://github.com/erkancam01/swpdm-v2.git
cd swpdm-v2
```

Dal: **`main`** (§1a). `--force`, `rebase`, geçmişi yeniden yazma **YOK**.
Commit mesajları Türkçe ve **neden** değiştiğini yazar.

## 3. Uygulamayı çalıştır

```bash
dotnet run --project src/SwPdm.Arayuz
```

## 4. Kapılar — hangisi nerede koşar

**ÖLÇÜLDÜ (CI'ın `derleme-windows` işi, her push'ta):**

| kapı | Windows |
|---|---|
| `kapi_derleme.sh` | **koşuyor** |
| `kapi_test.sh` | **koşuyor** — ve burada FAZLASI var: `WindowsYolu`, Windows'ta gerçek `System.IO.Path`'in kendisiyle karşılaştırılıyor. Linux'ta atlanan 5 test **burada koşar.** |

**ÖLÇÜLMEDİ ama bağımlılıkları Git Bash'te var** (`find`/`grep`/`sed`):
`kapi_harita.sh` · `kapi_kisayol.sh` · `kapi_boyut.sh` ·
`kapi_ozellikler.sh` (+ `python3`).

**KOŞMAZ:** `kapi_calistir.sh` — Xvfb + Wine + `xdotool` + ImageMagick
istiyor. Windows'ta bunlar yok ve **olmasına gerek de yok**: o kapı
uygulamayı Wine'da açıp taklit tıklamalarla ölçüyordu; sende gerçeğin
kendisi var.

> **KAPI ATLAMAZ, HATA VERİR** (§9: *"kurulu olmayan bir kapı 'geçti'
> sayılmaz"*). Bu yüzden `araclar/kapilar.sh` Windows'ta **kırık** der ve bu
> doğru davranıştır. Yerelde altısını koş:

```bash
for k in harita ozellikler kisayol boyut derleme test; do
  bash araclar/kapi_$k.sh || { echo "### KIRIK: $k"; break; }
done
```

Yedincinin yerine: **uygulamayı aç ve değiştirdiğin şeyi elle dene.**
Yeşil derleme çalışıyor demek değildir (§11) — bu kural Windows'ta da aynen
geçerli; 27.08.2026'da uygulama Windows'ta hiç açılmamıştı ve derleme
"0 uyarı 0 hata" diyordu.

## 5. Oturum başlangıcı kancası — yerelde KENDİNİ KAPATIYOR

`.claude/hooks/session-start.sh` ilk satırlarında
`CLAUDE_CODE_REMOTE != true` ise **çıkıyor**. Yani senin makinene `apt-get`
çalıştırmaz, hiçbir şey kurmaz. Dokunmana gerek yok.

## 6. YERELDE AÇILAN ÖLÇÜMLER — asıl kazanç

`SIRADAKI.md`'deki kör noktaların çoğu Wine'ın eksikleriydi. Sende
**ölçülebilir** hale geliyorlar:

- **Sağ tık menüleri.** Wine'da her `ToolStripDropDown` uygulamayı
  çökertiyor (§11), yani menülerin hiçbiri hiç açılamadı. Sende açılıyor.
- **3B önizleme (eDrawings).** Burada `REGDB_E_CLASSNOTREG`'den öteye
  gidilemiyor.
- **Pano (`Ctrl+X`/`Ctrl+V`) ve sürükle-bırak.** Kapıda ölçümü yok.
- **Gerçek SOLIDWORKS simgeleri.** Kabuk kaydı olmayınca üç tür aynı boş
  sayfa simgesini veriyor (§11).
- **Ağ sürücüsü hızı, 100 MB+ montaj, Segoe UI ile gerçek yerleşim.**
- **Türkçe dosya adları.** Wine + ASCII yerel ayar `Parça1` → `ParC'a1`
  yapıyordu (§11); Windows'ta adlar UTF-16, bu çevrim hiç yok.

## 7. Yeni oturum ilk ne yapmalı

1. `CLAUDE.md` — çalışma kuralları ve ölçülmüş gerçekler. **§1b, §1c, §1d
   Erkan'ın koyduğu kalıcı varsayılanlardır**, ayrıca sorulmaz.
2. `SIRADAKI.md` — bugünün açık işleri (biten silinir).
3. `OZELLIKLER.md` — kullanıcı kılavuzu; her düğmenin ne yaptığı.
4. Bu dosya — ortam.

Sonra: **ölç, tahmin etme** (§2). Sayıyı belgeden okuma — çalıştır.

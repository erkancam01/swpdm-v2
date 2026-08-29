#!/usr/bin/env bash
#
# KAPI: calistirma
#
# Uygulamayi GERCEKTEN acar ve ana penceresinin dogdugunu DOGRULAR.
#
# NEDEN VAR:
#   27.08.2026'da uygulama Windows'ta hic acilmadi (kurucudan cagrilan
#   OnResize, henuz atanmamis alanlara dokunuyordu). O anda derleme kapisi
#   "0 uyari 0 hata" diyordu. Yani derleme kapisi bu hata SINIFINI GORMUYOR.
#   Bu kapi tam olarak onun icin var.
#
# NASIL:
#   Linux'ta win-x64 kendi kendine yeten yayin uretilir (gercek PE .exe),
#   Xvfb sanal ekraninda Wine ile acilir, pencereler okunur, goruntu alinir.
#
# CLAUDE.md 9:
#   - Kapsam ADLARA degil AGACA bagli: WinExe projesi agactan bulunur.
#   - Kurulu olmayan bir kapi GECTI sayilmaz: eksik arac varsa ATLAMAZ,
#     hata verir. --kur verilirse eksikleri kurar.
#
# ORNEK KLASOR: uygulama "--klasor <yol>" ile aciliyor. Sebep: bos bir pencere
# olcmek az sey soyler. Kapi gecici bir klasor kurar (SOLIDWORKS uzantili
# dosyalar, alt klasorler, okunamayan bir yol, tanimadigimiz uzantilar) ve
# uygulamayi onunla acar; ekran goruntusunde DOLU agac gorunur.
#
# COKLU SECIM: pencere acildiktan sonra xdotool ile GERCEK tik atilir ve
# secili satir sayisi EKRAN GORUNTUSUNDEN sayilir. Sebep: coklu secim
# WinForms TreeView'de yok, elle yazildi; birim testi mumkun degil, tek
# olcum yolu bu. CLAUDE.md 9: depoda ve CI'da olmayan denetim, denetim
# degildir.
#
# DONUS: 0 = pencere acildi, hata yok, coklu secim / Ctrl+A kapsami /
# siralama / tur suzgeci / geri alma calisiyor.

set -uo pipefail

KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CALISMA="$KOK/.kapi"
EKRAN_NO="${KAPI_EKRAN:-99}"
BEKLE="${KAPI_BEKLE:-25}"
KUR=0
[ "${1:-}" = "--kur" ] && KUR=1

# ============================ YERLESIM OLCULERI ============================
# Pencere ICI koordinatlar. Arayuzun yerlesimi degisirse YALNIZCA burasi
# degisir - asagida hicbir yerde ciplak sayi yok.
#
# Bunlar OLCULDU (xwininfo + ekran goruntusu), tahmin degil:
#   baslik seridi 32 · sekme baslıklari ~22 · arac cubugu ~25 ·
#   suzgec seridi 28 · YOL CUBUGU 26  -> agacin ilk satiri
AGAC_ILK_SATIR="${KAPI_AGAC_ILK_SATIR:-142}"   # ilk agac satirinin y'si
AGAC_SATIR_YUKSEKLIGI=18
AGAC_TIK_X=105                                  # dugum metnine denk gelen x
SUZGEC_Y=95                                     # suzgec seridinin y'si
SUZGEC_PARCA_X=171                              # "Parca" dugmesinin x'i
SUZGEC_TUMU_X=38                                # "Tumu" dugmesinin x'i
ARAMA_X=235                                     # arama kutusunun ortasi
ARAMA_Y=68                                      # arama kutusunun y'si
# Agac alani. AGAC_ILK_SATIR'dan TURETILIYOR, ikinci kez yazilmiyor:
# yerlesim degisince tek bir sayi degisiyor (CLAUDE.md 8).
AGAC_KIRP="560x250+5+$(( AGAC_ILK_SATIR - 7 ))"
# Alt panel: solda onizleme kutusu, sagda referans listesi.
ONIZLEME_KIRP="250x280+15+458"                  # pencere ici: genislikxyukseklik+x+y
REFERANS_KIRP="260x150+295+458"
BOLUM_ZEMIN="#E4EAF1"                           # Renkler.ReferansBolumZemin
ACIK_DOSYA="#FFE3C8"                            # Renkler.AcikDosyaZemin
# ==========================================================================

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export LANG=C.UTF-8 LC_ALL=C.UTF-8

echo "== KAPI: calistirma =="

# ---------------------------------------------------------------- gereksinimler
wine_yolu() {
  for y in /usr/lib/wine/wine64 /usr/lib/wine/wine "$(command -v wine64 2>/dev/null)" "$(command -v wine 2>/dev/null)"; do
    [ -n "$y" ] && [ -x "$y" ] && { echo "$y"; return 0; }
  done
  return 1
}

EKSIK=()
command -v dotnet   > /dev/null 2>&1 || EKSIK+=("dotnet-sdk-8.0")
wine_yolu           > /dev/null 2>&1 || EKSIK+=("wine64")
command -v Xvfb     > /dev/null 2>&1 || EKSIK+=("xvfb")
command -v xwininfo > /dev/null 2>&1 || EKSIK+=("x11-utils")
command -v import   > /dev/null 2>&1 || EKSIK+=("imagemagick")
command -v xdotool  > /dev/null 2>&1 || EKSIK+=("xdotool")

if [ "${#EKSIK[@]}" -gt 0 ]; then
  if [ "$KUR" -eq 1 ]; then
    echo "   eksik kuruluyor: ${EKSIK[*]}"
    SUDO=""
    [ "$(id -u)" -ne 0 ] && SUDO="sudo"
    $SUDO apt-get update > /dev/null 2>&1
    DEBIAN_FRONTEND=noninteractive $SUDO apt-get install -y --no-install-recommends "${EKSIK[@]}" > /dev/null 2>&1 \
      || { echo "KAPI KURULU DEGIL: kurulum basarisiz (${EKSIK[*]})"; exit 1; }
  else
    echo "KAPI KURULU DEGIL. Eksik: ${EKSIK[*]}"
    echo "  sudo apt-get install -y ${EKSIK[*]}"
    echo "  ya da: $0 --kur"
    echo "  Kurulu olmayan bir kapi GECTI sayilmaz."
    exit 1
  fi
fi
WINE="$(wine_yolu)"
WINESERVER="$(dirname "$WINE")/wineserver"
[ -x "$WINESERVER" ] || WINESERVER="$(command -v wineserver 2>/dev/null)"
echo "   wine: $($WINE --version 2>/dev/null)"

# ------------------------------------------------------- WinExe projesini bul
mapfile -t ADAYLAR < <(grep -rl "<OutputType>WinExe</OutputType>" "$KOK" \
  --include="*.csproj" 2>/dev/null | sort)

if [ "${#ADAYLAR[@]}" -ne 1 ]; then
  echo "KAPI KIRIK: agacta tam olarak bir WinExe projesi bekleniyordu, ${#ADAYLAR[@]} bulundu."
  printf '   %s\n' "${ADAYLAR[@]}"
  exit 1
fi
PROJE="${ADAYLAR[0]}"
echo "   proje: ${PROJE#"$KOK"/}"

# ------------------------------------------------------------------- yayinla
YAYIN="$CALISMA/yayin"
rm -rf "$YAYIN"; mkdir -p "$YAYIN"
echo "   yayinlaniyor (win-x64, kendi kendine yeten)..."
if ! dotnet publish "$PROJE" -c Release -r win-x64 --self-contained true \
     -p:UseAppHost=true -o "$YAYIN" > "$CALISMA/yayin.log" 2>&1; then
  echo "KAPI KIRIK: yayin basarisiz."
  grep -E "error" "$CALISMA/yayin.log" | head -10 | sed 's/^/     /'
  exit 1
fi

# Uygulama adi runtimeconfig'ten okunur; hicbir yere ad YAZILMAZ.
RC="$(find "$YAYIN" -maxdepth 1 -name "*.runtimeconfig.json" | head -1)"
[ -z "$RC" ] && { echo "KAPI KIRIK: runtimeconfig bulunamadi."; exit 1; }
AD="$(basename "$RC" .runtimeconfig.json)"
EXE="$YAYIN/$AD.exe"
[ -x "$EXE" ] || [ -f "$EXE" ] || { echo "KAPI KIRIK: $AD.exe uretilmemis."; exit 1; }
echo "   uretilen: $AD.exe ($(stat -c%s "$EXE") bayt)"

# --------------------------------------------------------------------- calistir
# OLCULMUS TUZAK (27.08.2026): WINEDLLOVERRIDES icinde mscoree'yi KAPATMA.
# Wine'in "Mono kurayim mi" penceresini engellemek icin "mscoree,mshtml="
# yazmistim. Sonuc: uygulama kendi klasorundeki System.Runtime.dll'i bile
# "Module not found" ile reddetti. Belirti tamamen yaniltici - dosya oradaydi,
# yol dogruydu, ayni yayin bu degisken olmadan SORUNSUZ aciliyordu.
# Korelasyon birebir: bu degiskenin oldugu HER kosu kirildi, olmadigi HER
# kosu calisti. Yalnizca mshtml (Gecko) kapatiliyor.
# OLCULDU (28.08.2026) - LOCALE ASCII ISE WINE TURKCE DOSYA ADINI BOZUYOR.
# Kapsayicinin varsayilani LC_CTYPE=POSIX. Wine dosya adlarini o kod
# sayfasina cevirmeye calisiyor ve "Parça1.SLDPRT" uygulamaya
# "ParC\'a1.SLDPRT" olarak geliyor. Belirti SESSIZ ve YANILTICI: dosya
# agacta gorunuyor, ama dosyanin ICINDE yazan "Parça1.SLDPRT" ile eslesmiyor
# ve referans "BULUNAMADI" cikiyor - yani kapi, uygulamada olmayan bir hatayi
# olcmus olur (gercek Windows'ta adlar UTF-16, boyle bir cevrim yok).
# Ilk gorulusu: elle baglama penceresi olculurken. Duzeltmesi tek satir.
export LANG=C.UTF-8 LC_ALL=C.UTF-8
export WINEPREFIX="$CALISMA/wine" WINEDEBUG=-all WINEDLLOVERRIDES="mshtml="
export DISPLAY=":$EKRAN_NO"
UYGULAMA_LOG="$CALISMA/uygulama.log"
GORUNTU="$CALISMA/ekran.png"
: > "$UYGULAMA_LOG"

# =========================== OLCULMUS HATA ===========================
# "pkill -f" KOMUT SATIRININ TAMAMINA bakiyor - yalnizca surec adina degil.
# Burada "pkill -f SwPdm.exe" yaziyordu ve bir gun cagiran kabugu OLDURDU:
# o kabugun komut satirinda (uzun bir commit mesajinda) "SwPdm.exe" gecıyordu.
# Belirti tamamen sessizdi: komut exit 144 ile dustu, hicbir hata yazmadi.
#
# Olculdu: pgrep -f <desen>  -> cagiran kabugu ESLIYOR
#          pgrep -x <desen>  -> ESLEMIYOR (yalnizca surec ADI)
# Bu yuzden eski surecler ADA gore bulunuyor, komut satirina gore degil;
# ayrica kendi surecimiz ve atalarimiz elenmis oluyor.
# =====================================================================
eskileri_oldur() {
  local surec_adi="$1" istenen_desen="${2:-}" pid
  for pid in $(pgrep -x "$surec_adi" 2>/dev/null); do
    [ "$pid" = "$$" ] && continue
    if [ -z "$istenen_desen" ] || grep -qa -- "$istenen_desen" "/proc/$pid/cmdline" 2>/dev/null; then
      kill "$pid" > /dev/null 2>&1
    fi
  done
}

eskileri_oldur Xvfb ":$EKRAN_NO"
eskileri_oldur "$AD.exe"
sleep 1

# KAPI YINELENEBILIR OLMALI - ve degildi. Uygulama ayarlarini ve referans
# indeksini %APPDATA%'ya yaziyor; onceki kosudan kalan indeks yuzunden
# ikinci kosu daha basindan "taranmis" haliyle aciliyordu ve "tarama
# oncesi / sonrasi" olcumu ayni seyi iki kez olcuyordu. Belirti sinsiydi:
# kapi ilk kosuda dogru, ikincide yanlis sonuc veriyordu.
find "$WINEPREFIX" -type d -name "SwPdm" -prune -exec rm -rf {} + 2>/dev/null

# Onyuklemenin bittigini beklemek. DURUST NOT: bu bekleme, yukaridaki
# mscoree hatasi aranirken "yarim kurulmus on ek" hipoteziyle eklendi ve o
# hipotez YANLIS cikti - sebep mscoree'ydi. Bekleme yine de duruyor cunku
# dogru olan bu: wineserver -w onyuklemenin bittigini GARANTI eder, biz de
# tahmini bir "sleep" ile is gormus gibi yapmayiz. Ama bir hatayi cozdugu
# OLCULMEDI; oyle oldugunu iddia etmiyoruz.
if [ ! -d "$WINEPREFIX" ]; then
  echo "   wine on eki ilk kez kuruluyor..."
  "$WINE" wineboot -i > "$CALISMA/wineboot.log" 2>&1
  if [ -x "$WINESERVER" ]; then
    "$WINESERVER" -w
  else
    echo "KAPI KIRIK: wineserver bulunamadi, onyuklemenin bittigi DOGRULANAMIYOR."
    exit 1
  fi
  echo "   on ek hazir."
fi

Xvfb ":$EKRAN_NO" -screen 0 1200x1100x24 -nolisten tcp > "$CALISMA/xvfb.log" 2>&1 &
XVFB_PID=$!
sleep 4

temizle() {
  kill "$UYG_PID"  > /dev/null 2>&1
  kill "$XVFB_PID" > /dev/null 2>&1
  eskileri_oldur winedbg.exe
}
trap temizle EXIT

# ---------------------------------------------------------- ornek klasor
ORNEK="$CALISMA/ornek-klasor/ORJINAL"
rm -rf "$CALISMA/ornek-klasor"
mkdir -p "$ORNEK"/{1,2,33,222,"alt klasor"}
# Kokte de dosya olsun: ekran goruntusunde simgeler ve adlar gorunsun.
: > "$ORNEK/Govde.SLDASM"
: > "$ORNEK/Kapak.SLDPRT"
# 13. OLCUM ICIN: kokte bir kilit cifti. Ozellik yokken bu satir agaca
# BIR SATIR EKLERDI; ozellik varken hic eklemiyor - yani kokteki satir
# sayisi degismiyor ve 5/6/8/9. olcumlerin tabanlari KAYMIYOR.
: > "$ORNEK/~\$Kapak.SLDPRT"
# Icinde GERCEK gomulu onizleme olan bilesik belge. Wine'da kabuk onizleme
# saglayicisi yok; bu dosya yedek yolu (dosyanin icindeki onizleme) gorunur
# kilar - yoksa kapi yalnizca bos bir kutu olcerdi.
cp "$KOK/araclar/ornek-veri/ornek.sldprt" "$ORNEK/Onizlemeli.SLDPRT"
: > "$ORNEK/Kapak.SLDDRW"
# GERCEK, tek sayfalik PDF (araclar/ornek-veri/pdf_uret.py ile uretildi).
# Wine WinRT tasimiyor, yani PDF yolu burada CIZEMEZ - olculebilen tek sey
# COKMEDIGI ve sebebini soyledigi.
cp "$KOK/araclar/ornek-veri/ornek.pdf" "$ORNEK/katalog.pdf"
: > "$ORNEK/okubeni.txt"
: > "$ORNEK/1/Parca3.SLDPRT"
: > "$ORNEK/1/Montaj1.SLDASM"
: > "$ORNEK/2/Parca1.SLDPRT"
: > "$ORNEK/33/Montaj2.SLDASM"
: > "$ORNEK/33/Parca2.SLDDRW"
: > "$ORNEK/33/Parca2.SLDPRT"
: > "$ORNEK/222/asaParcaa1.SLDPRT"
# KILIT DOSYALARI - iki hal, iki ayri beklenti (bkz. Cekirdek/Kilit.cs):
#   sahibi VAR  -> kilit satiri GIZLENIR, sahibi "acik" diye isaretlenir
#   sahibi YOK  -> kilit GORUNUR kalir; klasor silmeyi engelleyen sey odur
: > "$ORNEK/222/~\$asaParcaa1.SLDPRT"      # sahibi var -> gizlenir
: > "$ORNEK/222/~\$kayipsahip.SLDPRT"      # sahibi YOK -> gorunur kalir
: > "$ORNEK/alt klasor/olcum.pdf"
: > "$ORNEK/alt klasor/notlar.txt"          # tanimadigimiz uzanti: GORUNMELI
mkdir -p "$ORNEK/33/derin/daha-derin"
: > "$ORNEK/33/derin/daha-derin/Parca9.SLDPRT"
head -c 83000 /dev/zero > "$ORNEK/33/Parca2.SLDDRW"

# GERCEK SOLIDWORKS 2022 dosyalari. Yukaridaki bos dosyalar agacin
# gorunusunu olcmeye yetiyor ama ONIZLEME ve REFERANS olcumu icin gercek
# icerik sart: bos bir dosyada gosterilecek onizleme de, cozulecek referans
# da yok. Ikisi ayni kumeden: teknik resim parcayi baz aliyor.
cp "$KOK/araclar/ornek-veri/tertemiz/Parça1.SLDPRT" "$ORNEK/Parça1.SLDPRT"
cp "$KOK/araclar/ornek-veri/tertemiz/Parça1.SLDDRW" "$ORNEK/Parça1.SLDDRW"

# Wine "Z:" surucusunu koke esliyor; yolu Windows bicimine ceviriyoruz.
ORNEK_WIN="Z:$(echo "$ORNEK" | tr '/' '\\')"
echo "   ornek klasor: $ORNEK_WIN"

echo "   aciliyor, $BEKLE saniye izleniyor..."
( cd "$YAYIN" && "$WINE" "./$AD.exe" --klasor "$ORNEK_WIN" >> "$UYGULAMA_LOG" 2>&1 ) &
UYG_PID=$!
sleep "$BEKLE"

# ---------------------------------------------------------------- olcumler
# Secili satir sayisi: secim rengi (#3399FF = Renkler.SecimArkaPlan) tasiyan
# piksellerin y degerleri kac ayri BANT olusturuyor. Piksel SAYISI ise
# yaramaz - satir genisligi dosya adinin uzunluguna gore degisiyor.
secili_satir_say() {
  convert "$1" -crop "560x320+$(( $2 + 5 ))+$(( $3 + AGAC_ILK_SATIR - 7 ))" +repage txt:- 2>/dev/null \
    | grep -o '^[0-9]*,[0-9]*:.*#3399FF' \
    | cut -d, -f2 | cut -d: -f1 | sort -n | uniq \
    | awk 'NR==1{bant=1; onceki=$1; next} {if ($1-onceki>1) bant++; onceki=$1} END{print bant+0}'
}

# Agactaki GORUNUR satir sayisi: beyaz olmayan piksel iceren yatay bantlar.
# Suzgec uygulaninca satir sayisi AZALMALI.
#
# Kirpma AGACIN ICINDE kalmali: altta bolen cizgisi ve "Onizleme ve
# Referanslar" basligi var, onlar da bant sayilir ve sayiyi sisirir.
# (Olculdu: 250 px yukseklik agac alaninin icinde kaliyor.)
#
# BANT EN AZ 3 PIKSEL OLMALI - ve bu sart OLCULEREK eklendi (27.08.2026).
# TreeView'in NOKTALI baglanti cizgileri iki pikselde bir nokta koyuyor;
# aradaki bosluk 1'den buyuk oldugu icin her nokta AYRI BANT sayiliyordu ve
# ayni agac 12 yerine 17 satir gorunuyordu. Hata o zamana kadar gorunmedi
# cunku olcum hep Ctrl+A'dan SONRA aliniyordu: secim boyasi noktali
# cizgileri ortuyordu. Yani sayi dogru degildi, sadece dogru gorunuyordu.
# Metin satirlari ~9 piksel; noktalar 1 piksel - esik ikisini ayiriyor.
agac_satir_say() {
  local boyut="${AGAC_KIRP%%+*}" kalan="${AGAC_KIRP#*+}"
  local kx="${kalan%%+*}" ky="${kalan#*+}"
  convert "$1" -crop "${boyut}+$(( $2 + kx ))+$(( $3 + ky ))" +repage txt:- 2>/dev/null \
    | grep -v '#FFFFFF' | grep -o '^[0-9]*,[0-9]*:' \
    | cut -d, -f2 | cut -d: -f1 | sort -n | uniq \
    | awk 'NR==1{bas=$1; onceki=$1; next}
           {if ($1-onceki>1) {if (onceki-bas>=3) bant++; bas=$1} onceki=$1}
           END{if (NR>0 && onceki-bas>=3) bant++; print bant+0}'
}

# DOSYA SATIRLARININ PARMAK IZI. Siralama satir SAYISINI degistirmiyor,
# SIRASINI degistiriyor; sayan bir olcum bunu goremez. Ayni kirpmanin ozeti
# aliniyor: sira degisirse ozet degisir, degismezse aynen kalir.
#
# NEDEN YALNIZCA DOSYALAR: butun agac kirpilinca kapi DUYARSIZ kaliyordu ve
# bu OLCULDU - dosya karsilastiricisi bilerek bozulup kapi kosuldu, kapi
# "TEMIZ" dedi. Sebep: klasorler ayri bir yoldan siralaniyor ve onlar hala
# ters donuyordu, yani iz yine degisiyordu. Kirpma dosya satirlarina
# indirilince ayni bozuk yapi YAKALANDI.
# Ornek klasorde kok + 5 klasor var, dosyalar 7. satirdan basliyor.
ILK_DOSYA_SATIRI=6           # kok + 5 klasor
DOSYA_SATIR_SAYISI=6
agac_izi() {
  convert "$1" -crop \
      "560x$(( DOSYA_SATIR_SAYISI * AGAC_SATIR_YUKSEKLIGI + 6 ))+$(( $2 + 5 ))+$(( $3 + AGAC_ILK_SATIR + ILK_DOSYA_SATIRI * AGAC_SATIR_YUKSEKLIGI - 7 ))" \
      +repage -depth 8 rgb:- 2>/dev/null | md5sum | cut -d' ' -f1
}

# Pencere ICI kirpma: "GxY+x+y" olcusunu ekran koordinatina cevirip beyaz
# olmayan piksel sayar. Bos bir kutu ~0 verir.
beyaz_olmayan() {
  local goruntu="$1" olcu="$2" px="$3" py="$4"
  local boyut="${olcu%%+*}" kalan="${olcu#*+}"
  local x="${kalan%%+*}" y="${kalan#*+}"
  convert "$goruntu" -crop "${boyut}+$(( px + x ))+$(( py + y ))" +repage txt:- 2>/dev/null \
    | grep -vc '#FFFFFF'
}

# Ayni kirpmanin ozeti. Icerik degistiyse ozet degisir.
kirpma_izi() {
  local goruntu="$1" olcu="$2" px="$3" py="$4"
  local boyut="${olcu%%+*}" kalan="${olcu#*+}"
  local x="${kalan%%+*}" y="${kalan#*+}"
  convert "$goruntu" -crop "${boyut}+$(( px + x ))+$(( py + y ))" +repage \
    -depth 8 rgb:- 2>/dev/null | md5sum | cut -d' ' -f1
}

# BELLI BIR RENGIN KAC AYRI YATAY BANT olusturdugu. Coklu secim olcumundeki
# teknigin aynisi; piksel SAYISI ise yaramaz cunku satir genisligi metne
# gore degisiyor, BANT SAYISI degismiyor.
renk_bant_say() {
  local goruntu="$1" olcu="$2" px="$3" py="$4" renk="$5"
  local boyut="${olcu%%+*}" kalan="${olcu#*+}"
  local x="${kalan%%+*}" y="${kalan#*+}"
  convert "$goruntu" -crop "${boyut}+$(( px + x ))+$(( py + y ))" +repage txt:- 2>/dev/null \
    | grep -o "^[0-9]*,[0-9]*:.*${renk}" \
    | cut -d, -f2 | cut -d: -f1 | sort -n | uniq \
    | awk 'NR==1{bant=1; onceki=$1; next} {if ($1-onceki>1) bant++; onceki=$1} END{print bant+0}'
}

SORUN=0

# 1) surec ayakta mi
if kill -0 "$UYG_PID" > /dev/null 2>&1; then
  echo "   [1/14] surec ayakta ............ EVET"
else
  echo "   [1/14] surec ayakta ............ HAYIR (uygulama oldu)"
  SORUN=1
fi

# 2) hata akisa dustu mu (Program.cs hem kutuya hem akisa yaziyor)
if grep -qaE "Unhandled exception|Exception:" "$UYGULAMA_LOG" 2>/dev/null; then
  echo "   [2/14] hata akisi temiz ........ HAYIR"
  grep -aE "Unhandled exception|Exception:" "$UYGULAMA_LOG" | head -3 | sed 's/^/           /'
  SORUN=1
else
  echo "   [2/14] hata akisi temiz ........ EVET"
fi

# 3) Wine'in cokme penceresi acildi mi
PENCERELER="$(xwininfo -root -children 2>/dev/null)"
if echo "$PENCERELER" | grep -qi "winedbg"; then
  echo "   [3/14] cokme penceresi yok ..... HAYIR (winedbg acilmis)"
  SORUN=1
else
  echo "   [3/14] cokme penceresi yok ..... EVET"
fi

# 4) ana pencere dogdu mu: uygulamaya ait, 400x400'den buyuk bir ust pencere
# OLCULMUS TUZAK: Wine her uygulama icin bir suru 1x1 YARDIMCI pencere
# aciyor (IME, BroadcastEventWindow...). "head -1" bunlardan birini secip
# +0+0 dondurdu ve tiklama pencerenin DISINA gitti; belirti "hicbir sey
# secili degil" idi, sebebi degil. Boyut ve KONUM ayni satirdan okunur.
ANA_KAYIT="$(echo "$PENCERELER" | grep -i "(\"${AD,,}.exe\"" \
      | grep -oE '[0-9]+x[0-9]+\+[-0-9]+\+[-0-9]+' \
      | awk -F'[x+]' '$1 >= 400 && $2 >= 400 {print $1" "$2" "$3" "$4; exit}')"
ANA=""
PENCERE_X=""
PENCERE_Y=""
if [ -n "$ANA_KAYIT" ]; then
  # shellcheck disable=SC2086
  set -- $ANA_KAYIT
  ANA="$1x$2"
  PENCERE_X="$3"
  PENCERE_Y="$4"
fi
if [ -n "$ANA" ]; then
  echo "   [4/14] ana pencere dogdu ....... EVET ($ANA)"
  # HICBIR SEY SECILI DEGILKEN bir goruntu: 13. olcum satir rengine bakiyor
  # ve secim boyasi rengi ORTERDI. Sonraki olcumler tiklamaya basliyor.
  import -window root "$CALISMA/ilk.png" > /dev/null 2>&1
else
  echo "   [4/14] ana pencere dogdu ....... HAYIR (400x400'den buyuk pencere yok)"
  echo "$PENCERELER" | grep -i "${AD,,}.exe" | head -5 | sed 's/^/           /'
  SORUN=1
fi

# 5) coklu secim: Ctrl ile iki dosya secilebiliyor mu
# Agactaki satirlar 18 px; ilk satir ornek klasorun kokudur. Iki KOK
# seviyesindeki dosyaya Ctrl ile tiklaniyor ve secili satir sayiliyor.
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  # Pencere ici koordinatlar: agac ilk satiri y=116, satir yuksekligi 18.
  # Kok seviyesindeki ilk iki dosya 6. ve 7. satirlarda (5 alt klasor var).
  TIK_X=$(( PENCERE_X + AGAC_TIK_X ))
  SATIR1=$(( PENCERE_Y + AGAC_ILK_SATIR + AGAC_SATIR_YUKSEKLIGI * 6 ))
  SATIR2=$(( PENCERE_Y + AGAC_ILK_SATIR + AGAC_SATIR_YUKSEKLIGI * 9 ))

  xdotool mousemove "$TIK_X" "$SATIR1" click 1 > /dev/null 2>&1
  sleep 2
  xdotool keydown ctrl > /dev/null 2>&1
  xdotool mousemove "$TIK_X" "$SATIR2" click 1 > /dev/null 2>&1
  xdotool keyup ctrl > /dev/null 2>&1
  sleep 2

  import -window root "$CALISMA/secim.png" > /dev/null 2>&1
  SECILI="$(secili_satir_say "$CALISMA/secim.png" "$PENCERE_X" "$PENCERE_Y")"
  if [ "${SECILI:-0}" -eq 2 ]; then
    echo "   [5/14] coklu secim ............. EVET (Ctrl ile 2 satir)"
  else
    echo "   [5/14] coklu secim ............. HAYIR (2 bekleniyordu, $SECILI secili)"
    SORUN=1
  fi
else
  echo "   [5/14] coklu secim ............. OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 6) Ctrl+A KAPSAMI: butun agaci degil, icinde bulunulan klasoru secmeli
#
# NEDEN VAR: once butun agaci seciyordu ve bu bir rahatsizlik degil
# TEHLIKEYDI - Ctrl+A'dan sonra Delete, kullanicinin bir klasoru
# temizledigini sanirken KOKUN TAMAMINI cope atardi.
#
# Olcum: koke tiklanir, Ctrl+A basilir. Kokun KENDISI secime girmemeli,
# yani secili satir = gorunen satir - 1. Eski davranis gorunen sayinin
# KENDISINI verirdi.
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  xdotool mousemove "$(( PENCERE_X + AGAC_TIK_X ))" \
                    "$(( PENCERE_Y + AGAC_ILK_SATIR ))" click 1 > /dev/null 2>&1
  sleep 1
  xdotool key --clearmodifiers ctrl+a > /dev/null 2>&1
  sleep 2

  import -window root "$CALISMA/ctrla.png" > /dev/null 2>&1
  GORUNEN="$(agac_satir_say "$CALISMA/ctrla.png" "$PENCERE_X" "$PENCERE_Y")"
  ICERDEKI="$(secili_satir_say "$CALISMA/ctrla.png" "$PENCERE_X" "$PENCERE_Y")"
  BEKLENEN=$(( GORUNEN - 1 ))

  if [ "${GORUNEN:-0}" -gt 1 ] && [ "${ICERDEKI:-0}" -eq "$BEKLENEN" ]; then
    echo "   [6/14] Ctrl+A kapsami .......... EVET ($ICERDEKI/$GORUNEN - kok secili degil)"
  else
    echo "   [6/14] Ctrl+A kapsami .......... HAYIR ($BEKLENEN bekleniyordu, $ICERDEKI secili)"
    SORUN=1
  fi
else
  echo "   [6/14] Ctrl+A kapsami .......... OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 7) SIRALAMA: Ctrl+Shift+S sirayi gercekten degistiriyor mu
#
# NEDEN VAR: siralama menusu bir ContextMenuStrip ve Wine'da ToolStrip acmak
# uygulamayi COKERTIYOR (CLAUDE.md 11) - yani menu burada OLCULEMEZ. Ayni
# kodu cagiran kisayol olculuyor.
#
# Olcum SAYIYLA yapilamaz: siralama satir sayisini degistirmez, SIRASINI
# degistirir. Onun icin agac alaninin parmak izi aliniyor:
#   Ad artan -> bir kez bas (Ad azalan): iz DEGISMELI
#   yedi kez daha bas (dort olcut x iki yon = sekiz hal, basa doner):
#   iz ILK IZE ESIT olmali.
# Ikinci kosul onemli: yalnizca "degisti" demek, dugmenin etiketi degistigi
# icin de saglanabilirdi - donguyu kapatmak siranin GERCEKTEN uygulandigini
# gosterir.
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  # Ctrl+A'nin coklu secimi kalkmali: tek tik yalnizca koku secer, yoksa
  # secim vurgusu izi kirletir.
  xdotool mousemove "$(( PENCERE_X + AGAC_TIK_X ))" \
                    "$(( PENCERE_Y + AGAC_ILK_SATIR ))" click 1 > /dev/null 2>&1
  sleep 2
  import -window root "$CALISMA/sira0.png" > /dev/null 2>&1
  IZ_BAS="$(agac_izi "$CALISMA/sira0.png" "$PENCERE_X" "$PENCERE_Y")"

  xdotool key --clearmodifiers ctrl+shift+s > /dev/null 2>&1
  sleep 2
  import -window root "$CALISMA/sira1.png" > /dev/null 2>&1
  IZ_TERS="$(agac_izi "$CALISMA/sira1.png" "$PENCERE_X" "$PENCERE_Y")"

  for _ in 1 2 3 4 5 6 7; do
    xdotool key --clearmodifiers ctrl+shift+s > /dev/null 2>&1
    sleep 1
  done
  sleep 1
  import -window root "$CALISMA/sira8.png" > /dev/null 2>&1
  IZ_DONUS="$(agac_izi "$CALISMA/sira8.png" "$PENCERE_X" "$PENCERE_Y")"

  if [ "$IZ_BAS" != "$IZ_TERS" ] && [ "$IZ_BAS" = "$IZ_DONUS" ]; then
    echo "   [7/14] siralama (Ctrl+Shift+S) . EVET (ters cevirdi, sekiz halde basa dondu)"
  else
    echo "   [7/14] siralama (Ctrl+Shift+S) . HAYIR (bas=${IZ_BAS:0:8} ters=${IZ_TERS:0:8} donus=${IZ_DONUS:0:8})"
    SORUN=1
  fi
else
  echo "   [7/14] siralama (Ctrl+Shift+S) . OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 8) tur suzgeci: "Parca" dugmesine tiklaninca agac gercekten suzuluyor mu
#
# NEDEN VAR: bu kapinin olmadigi bir turda suzgec dugmesinin Click baglantisi
# SILINDI ve kimse gormeden pakete girdi; Erkan bildirdi. Dugmeler ciziliyor,
# odagi aliyor, uzerine gelince renk degistiriyor - ama hicbir sey yapmiyordu.
# Derleme de testler de TEMIZ diyordu (CLAUDE.md 9).
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  ONCE="$(agac_satir_say "$CALISMA/sira8.png" "$PENCERE_X" "$PENCERE_Y")"

  xdotool mousemove "$(( PENCERE_X + SUZGEC_PARCA_X ))" \
                    "$(( PENCERE_Y + SUZGEC_Y ))" > /dev/null 2>&1
  sleep 1
  xdotool click 1 > /dev/null 2>&1
  sleep 2

  import -window root "$CALISMA/suzgec.png" > /dev/null 2>&1
  SONRA="$(agac_satir_say "$CALISMA/suzgec.png" "$PENCERE_X" "$PENCERE_Y")"

  if [ "${SONRA:-0}" -gt 0 ] && [ "${SONRA:-0}" -lt "${ONCE:-0}" ]; then
    echo "   [8/14] tur suzgeci .............. EVET ($ONCE -> $SONRA satir)"
  else
    echo "   [8/14] tur suzgeci .............. HAYIR (once $ONCE, sonra $SONRA - suzulmedi)"
    SORUN=1
  fi
else
  echo "   [8/14] tur suzgeci .............. OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 9) GERI AL: yeni klasor acilir, Ctrl+Z ile geri alinir
#
# NEDEN VAR: geri alma DOSYA SILIYOR. Sessizce bozulursa kullanici "geri
# aldim" sanip devam eder. Olcum: Ctrl+Shift+N agaca bir satir EKLER,
# Ctrl+Z o satiri GERI ALIR. Ikisi de sayilarak dogrulanir.
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  xdotool mousemove "$(( PENCERE_X + AGAC_TIK_X ))" \
                    "$(( PENCERE_Y + AGAC_ILK_SATIR ))" click 1 > /dev/null 2>&1
  sleep 1
  ONCEKI="$(agac_satir_say "$CALISMA/suzgec.png" "$PENCERE_X" "$PENCERE_Y")"

  xdotool key --clearmodifiers ctrl+shift+n > /dev/null 2>&1
  sleep 2

  # AD KUTUSU (29.08.2026): "Yeni klasör" artik adi SORUYOR. Kutu cakismayan
  # bir adla DOLU geliyor, yani Enter eski davranisin aynisi. Bu satir
  # olmadan kapi dogru sekilde HAYIR der - olculen sey degisti, kod degil.
  xdotool key --clearmodifiers Return > /dev/null 2>&1
  sleep 2
  import -window root "$CALISMA/klasor.png" > /dev/null 2>&1
  EKLENDI="$(agac_satir_say "$CALISMA/klasor.png" "$PENCERE_X" "$PENCERE_Y")"

  xdotool key --clearmodifiers ctrl+z > /dev/null 2>&1
  sleep 2
  import -window root "$CALISMA/gerial.png" > /dev/null 2>&1
  GERIALINDI="$(agac_satir_say "$CALISMA/gerial.png" "$PENCERE_X" "$PENCERE_Y")"

  if [ "${EKLENDI:-0}" -gt "${ONCEKI:-0}" ] && [ "${GERIALINDI:-0}" -eq "${ONCEKI:-0}" ]; then
    echo "   [9/14] geri al (Ctrl+Z) ........ EVET ($ONCEKI -> $EKLENDI -> $GERIALINDI)"
  else
    echo "   [9/14] geri al (Ctrl+Z) ........ HAYIR ($ONCEKI -> $EKLENDI -> $GERIALINDI)"
    SORUN=1
  fi
else
  echo "   [9/14] geri al (Ctrl+Z) ........ OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 10) ONIZLEME: dosyanin ICINDEKI onizleme cikiyor mu
#
# NEDEN VAR: bu alan BUGUNE KADAR HIC OLCULEMEDI. Onizleme yalnizca Windows
# kabugundan geliyordu; Wine'da kabuk saglayicisi YOK, SOLIDWORKS kurulu
# olmayan Windows'ta da .SLDPRT icin resim gelmiyor. Yani "onizleme bos"
# hatasi sessizce pakete girebilirdi - bos bir kutu, hicbir sebep.
# Artik dosyanin kendi "PreviewPNG" akisi okunuyor ve BURADA olculebiliyor.
#
# Olcum: gercek bir .SLDPRT secilir, onizleme kutusundaki BEYAZ OLMAYAN
# piksel sayilir. Bos kutu ~0 verir; gercek onizleme binlerce.
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  # "Tumu" suzgecine don, yoksa .SLDPRT gorunmeyebilir
  xdotool mousemove "$(( PENCERE_X + SUZGEC_TUMU_X ))" "$(( PENCERE_Y + SUZGEC_Y ))" click 1 > /dev/null 2>&1
  sleep 2
  SON_SATIR=$(( PENCERE_Y + AGAC_ILK_SATIR + AGAC_SATIR_YUKSEKLIGI * 13 ))
  xdotool mousemove "$(( PENCERE_X + AGAC_TIK_X ))" "$SON_SATIR" click 1 > /dev/null 2>&1
  sleep 5
  import -window root "$CALISMA/onizleme.png" > /dev/null 2>&1
  PIKSEL="$(beyaz_olmayan "$CALISMA/onizleme.png" "$ONIZLEME_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  # ESIK OLCULEREK SECILDI: gercek onizleme 12665 piksel, "Önizleme yok"
  # yazisi 612. Ilk halinde esik 500'du ve kaynak devre disi birakildiginda
  # kapi YAKALAMADI - yazinin kendisi esigi geciyordu. Renk sayisi da
  # ayirt etmedi (yazi kenar yumusatmayla 62 renk uretiyor). Ayiran tek sey
  # piksel sayisinin YIRMI KATLIK farki.
  if [ "${PIKSEL:-0}" -gt 3000 ]; then
    echo "   [10/14] onizleme (dosyadan) ... EVET ($PIKSEL piksel)"
  else
    echo "   [10/14] onizleme (dosyadan) ... HAYIR ($PIKSEL piksel - kutu bos)"
    SORUN=1
  fi
else
  echo "   [10/14] onizleme (dosyadan) ... OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 11) REFERANS LISTESI: gercek referanslar gorunuyor mu
#
# NEDEN VAR: bu uygulamanin VARLIK SEBEBI. Liste sessizce bos kalirsa
# kullanici "bu parcayi kimse kullanmiyor" sanip SILER (CLAUDE.md 3).
#
# OLCUM DEGISTI (28.08.2026) - VE BUNU KAPI KENDISI SOYLEDI.
# Eskiden "Ctrl+Shift+R'dan ONCE ve SONRA iz degisti mi" olculuyordu.
# Tarama artik HER ISLEMDEN ONCE kendiliginden kostugu icin indeks
# Ctrl+Shift+R'a basilmadan ONCE doluyor; iz degismiyor ve kapi HAYIR
# dedi. Kapi dogru davrandi: olcumun VARSAYIMI bayatlamisti.
#
# Yeni olcum varsayimsiz: REFERANSI OLAN bir dosya ile REFERANSI OLMAYAN
# bir dosya secildiginde listenin ICERIGI FARKLI olmali.
#   satir 13 = Parça1.SLDPRT  (teknik resim onu kullaniyor)
#   satir 10 = okubeni.txt    (referans tasimayan tur - liste bos)
# Liste hic dolmazsa ikisi de ayni (bos) cikar ve kapi YAKALAR.
TXT_SATIR=$(( PENCERE_Y + AGAC_ILK_SATIR + AGAC_SATIR_YUKSEKLIGI * 10 ))
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  xdotool key --clearmodifiers ctrl+shift+r > /dev/null 2>&1
  sleep 8

  xdotool mousemove "$(( PENCERE_X + AGAC_TIK_X ))" "$SON_SATIR" click 1 > /dev/null 2>&1
  sleep 4
  import -window root "$CALISMA/referans.png" > /dev/null 2>&1
  IZ_SW="$(kirpma_izi "$CALISMA/referans.png" "$REFERANS_KIRP" "$PENCERE_X" "$PENCERE_Y")"
  SW_L="$(beyaz_olmayan "$CALISMA/referans.png" "$REFERANS_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  xdotool mousemove "$(( PENCERE_X + AGAC_TIK_X ))" "$TXT_SATIR" click 1 > /dev/null 2>&1
  sleep 3
  import -window root "$CALISMA/referans-txt.png" > /dev/null 2>&1
  IZ_TXT="$(kirpma_izi "$CALISMA/referans-txt.png" "$REFERANS_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  if [ "$IZ_SW" != "$IZ_TXT" ] && [ "${SW_L:-0}" -gt 50 ]; then
    echo "   [11/14] referans listesi ...... EVET (referansli/referanssiz ayrisiyor, $SW_L piksel)"
  else
    echo "   [11/14] referans listesi ...... HAYIR (iz ${IZ_SW:0:8} / ${IZ_TXT:0:8}, $SW_L piksel)"
    SORUN=1
  fi

  # 12. olcum bu goruntuye bakiyor: SOLIDWORKS dosyasi secili olan.
  cp "$CALISMA/referans.png" "$CALISMA/referans-son.png" 2>/dev/null
else
  echo "   [11/14] referans listesi ...... OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 12) YON AYRIMI: referans listesinde IKI BOLUM BASLIGI var mi
#
# NEDEN VAR: liste hem "bu dosyanin kullandiklari" hem "bu dosyayi
# kullananlar" satirlarini tasiyor. Once ikisini ayiran tek sey rol
# sutunundaki BIR OK ISARETIYDI ve okunmuyordu; ayni ad iki bolumde birden
# cikabiliyor (montaj baglaminda yapilmis parca) ve hangi yonun hangisi
# oldugu anlasilmiyordu. Yon karistirmak bu uygulamada tehlikeli: "beni
# kimse kullanmiyor" diye okunan bir satir dosya sildirir (CLAUDE.md 3).
#
# Olcum SAYIYLA olmaz - bolum eklemek satir sayisini da parmak izini de
# zaten degistirir, yani 11. olcum bunu YAKALAMAZ. Ayirt eden sey basligin
# ZEMIN RENGI (#E4EAF1 = Renkler.ReferansBolumZemin): o panelde baska
# hicbir sey bu rengi kullanmiyor. Iki AYRI bant olmali.
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  BASLIK_BANT="$(renk_bant_say "$CALISMA/referans.png" "$REFERANS_KIRP" \
    "$PENCERE_X" "$PENCERE_Y" "$BOLUM_ZEMIN")"
  if [ "${BASLIK_BANT:-0}" -ge 2 ]; then
    echo "   [12/14] yon ayrimi ............ EVET ($BASLIK_BANT bolum basligi)"
  else
    echo "   [12/14] yon ayrimi ............ HAYIR ($BASLIK_BANT bolum basligi, 2 bekleniyordu)"
    SORUN=1
  fi
else
  echo "   [12/14] yon ayrimi ............ OLCULEMEDI (pencere yok)"
  SORUN=1
fi


# 13) KILIT DOSYALARI: "~$" gizlendi mi, sahibi isaretlendi mi
#
# NEDEN VAR: SOLIDWORKS her actigi belge icin klasore gizli bir "~$<ad>"
# dosyasi yaziyor (CLAUDE.md 5) ve bunlar agacta gercek dosyalarla yan yana
# duruyordu. Ama KORLEMESINE GIZLEMEK yanlis olurdu: Windows bir klasoru
# sildirmiyorsa sebep cogu zaman tam da o gorunmeyen dosyadir (4). Kural
# bu yuzden iki yanli - sahibi VARSA gizlenir ve sahibi "acik" isaretlenir,
# sahibi YOKSA gorunur kalir.
#
# Olcum satir SAYISIYLA olmaz: ornek klasordeki kilit zaten gizli oldugu
# icin sayi degismiyor (tabanlarin kaymamasi bilerek boyle). Ayirt eden sey
# isaretli satirin ZEMIN RENGI (#FFE3C8 = Renkler.AcikDosyaZemin) - agacta
# baska hicbir seyde yok. Bir bant bekleniyor: kokte tek bir kilit cifti var.
#
# NEDEN ZEMIN, YAZI RENGI DEGIL - OLCULDU: once yazi rengi (#A64B00)
# arandi ve SIFIR bulundu, oysa yazi ekranda turuncuydu. Sebep ClearType:
# alt-piksel cizimde metnin hicbir pikseli saf renge esit cikmiyor. Dolu
# dikdortgen tam renk veriyor.
#
# GORUNTU "ilk.png": hicbir sey secili degilken alindi. Secim boyasi satirin
# rengini orter ve olcum bos donerdi.
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  KILIT_BANT="$(renk_bant_say "$CALISMA/ilk.png" "$AGAC_KIRP" \
    "$PENCERE_X" "$PENCERE_Y" "$ACIK_DOSYA")"
  if [ "${KILIT_BANT:-0}" -ge 1 ]; then
    echo "   [13/14] kilit dosyalari ....... EVET (kilit gizlendi, sahibi isaretli)"
  else
    echo "   [13/14] kilit dosyalari ....... HAYIR ($KILIT_BANT isaret, 1 bekleniyordu)"
    SORUN=1
  fi
else
  echo "   [13/14] kilit dosyalari ....... OLCULEMEDI (pencere yok)"
  SORUN=1
fi

import -window root "$GORUNTU" > /dev/null 2>&1 && # 14) ESC: ARAMADAN CIKIS
#
# NEDEN VAR: Esc bu uygulamada BAGLI DEGILDI ve aramadan cikmanin tek yolu
# kutuyu elle bosaltmakti (olculdu, 28.08.2026). Esc eklendi; eklenen sey
# olculmezse yarin sessizce kirilir - v1'in suzgec dugmesi tam boyle
# kirilmisti (CLAUDE.md 8: tani temizliginde Click baglantisi silindi).
#
# OLCUM SAYIYLA: arama sonucu agaci DEGISTIRIR (satir sayisi baska olur),
# Esc gezinmeye DONDURUR (sayi tabana geri gelir). "asa" araniyor cunku
# ornek klasorde yalnizca "asaParcaa1.SLDPRT" esliyor - sonuc sayisi
# tabandan kesin farkli.
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  import -window root "$CALISMA/esc-once.png" > /dev/null 2>&1
  ESC_TABAN="$(agac_satir_say "$CALISMA/esc-once.png" "$PENCERE_X" "$PENCERE_Y")"

  xdotool mousemove "$(( PENCERE_X + ARAMA_X ))" "$(( PENCERE_Y + ARAMA_Y ))" \
    click 1 > /dev/null 2>&1
  sleep 1
  xdotool type --clearmodifiers "asa" > /dev/null 2>&1
  sleep 4
  import -window root "$CALISMA/esc-arama.png" > /dev/null 2>&1
  ESC_ARAMA="$(agac_satir_say "$CALISMA/esc-arama.png" "$PENCERE_X" "$PENCERE_Y")"

  xdotool key --clearmodifiers Escape > /dev/null 2>&1
  sleep 3
  import -window root "$CALISMA/esc-sonra.png" > /dev/null 2>&1
  ESC_SONRA="$(agac_satir_say "$CALISMA/esc-sonra.png" "$PENCERE_X" "$PENCERE_Y")"

  if [ "${ESC_ARAMA:-0}" -ne "${ESC_TABAN:-0}" ] \
     && [ "${ESC_SONRA:-0}" -eq "${ESC_TABAN:-0}" ]; then
    echo "   [14/14] Esc ile aramadan cikis  EVET ($ESC_TABAN -> $ESC_ARAMA -> $ESC_SONRA)"
  else
    echo "   [14/14] Esc ile aramadan cikis  HAYIR ($ESC_TABAN -> $ESC_ARAMA -> $ESC_SONRA)"
    SORUN=1
  fi
else
  echo "   [14/14] Esc ile aramadan cikis  OLCULEMEDI (pencere yok)"
  SORUN=1
fi

echo "   goruntu: $GORUNTU"

if [ "$SORUN" -ne 0 ]; then
  echo "== KAPI KIRIK =="
  exit 1
fi
echo "== KAPI TEMIZ =="

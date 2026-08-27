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
# DONUS: 0 = pencere acildi, hata yok, coklu secim / Ctrl+A kapsami / tur
# suzgeci calisiyor.

set -uo pipefail

KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CALISMA="$KOK/.kapi"
EKRAN_NO="${KAPI_EKRAN:-99}"
BEKLE="${KAPI_BEKLE:-25}"
KUR=0
[ "${1:-}" = "--kur" ] && KUR=1

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
: > "$ORNEK/222/~\$asaParcaa1.SLDPRT"     # SOLIDWORKS kilit dosyasi: GIZLENMEMELI
: > "$ORNEK/alt klasor/olcum.pdf"
: > "$ORNEK/alt klasor/notlar.txt"          # tanimadigimiz uzanti: GORUNMELI
mkdir -p "$ORNEK/33/derin/daha-derin"
: > "$ORNEK/33/derin/daha-derin/Parca9.SLDPRT"
head -c 83000 /dev/zero > "$ORNEK/33/Parca2.SLDDRW"

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
  convert "$1" -crop "560x320+$(( $2 + 5 ))+$(( $3 + 109 ))" +repage txt:- 2>/dev/null \
    | grep -o '^[0-9]*,[0-9]*:.*#3399FF' \
    | cut -d, -f2 | cut -d: -f1 | sort -n | uniq \
    | awk 'NR==1{bant=1; onceki=$1; next} {if ($1-onceki>1) bant++; onceki=$1} END{print bant+0}'
}

# Agactaki GORUNUR satir sayisi: beyaz olmayan piksel iceren yatay bantlar.
# Suzgec uygulaninca satir sayisi AZALMALI.
agac_satir_say() {
  convert "$1" -crop "560x300+$(( $2 + 5 ))+$(( $3 + 112 ))" +repage txt:- 2>/dev/null \
    | grep -v '#FFFFFF' | grep -o '^[0-9]*,[0-9]*:' \
    | cut -d, -f2 | cut -d: -f1 | sort -n | uniq \
    | awk 'NR==1{bant=1; onceki=$1; next} {if ($1-onceki>1) bant++; onceki=$1} END{print bant+0}'
}

SORUN=0

# 1) surec ayakta mi
if kill -0 "$UYG_PID" > /dev/null 2>&1; then
  echo "   [1/8] surec ayakta ............ EVET"
else
  echo "   [1/8] surec ayakta ............ HAYIR (uygulama oldu)"
  SORUN=1
fi

# 2) hata akisa dustu mu (Program.cs hem kutuya hem akisa yaziyor)
if grep -qaE "Unhandled exception|Exception:" "$UYGULAMA_LOG" 2>/dev/null; then
  echo "   [2/8] hata akisi temiz ........ HAYIR"
  grep -aE "Unhandled exception|Exception:" "$UYGULAMA_LOG" | head -3 | sed 's/^/           /'
  SORUN=1
else
  echo "   [2/8] hata akisi temiz ........ EVET"
fi

# 3) Wine'in cokme penceresi acildi mi
PENCERELER="$(xwininfo -root -children 2>/dev/null)"
if echo "$PENCERELER" | grep -qi "winedbg"; then
  echo "   [3/8] cokme penceresi yok ..... HAYIR (winedbg acilmis)"
  SORUN=1
else
  echo "   [3/8] cokme penceresi yok ..... EVET"
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
  echo "   [4/8] ana pencere dogdu ....... EVET ($ANA)"
else
  echo "   [4/8] ana pencere dogdu ....... HAYIR (400x400'den buyuk pencere yok)"
  echo "$PENCERELER" | grep -i "${AD,,}.exe" | head -5 | sed 's/^/           /'
  SORUN=1
fi

# 5) coklu secim: Ctrl ile iki dosya secilebiliyor mu
# Agactaki satirlar 18 px; ilk satir ornek klasorun kokudur. Iki KOK
# seviyesindeki dosyaya Ctrl ile tiklaniyor ve secili satir sayiliyor.
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  # Pencere ici koordinatlar: agac ilk satiri y=116, satir yuksekligi 18.
  # Kok seviyesindeki ilk iki dosya 6. ve 7. satirlarda (5 alt klasor var).
  TIK_X=$(( PENCERE_X + 105 ))
  SATIR1=$(( PENCERE_Y + 116 + 18 * 6 ))
  SATIR2=$(( PENCERE_Y + 116 + 18 * 9 ))

  xdotool mousemove "$TIK_X" "$SATIR1" click 1 > /dev/null 2>&1
  sleep 2
  xdotool keydown ctrl > /dev/null 2>&1
  xdotool mousemove "$TIK_X" "$SATIR2" click 1 > /dev/null 2>&1
  xdotool keyup ctrl > /dev/null 2>&1
  sleep 2

  import -window root "$CALISMA/secim.png" > /dev/null 2>&1
  SECILI="$(secili_satir_say "$CALISMA/secim.png" "$PENCERE_X" "$PENCERE_Y")"
  if [ "${SECILI:-0}" -eq 2 ]; then
    echo "   [5/8] coklu secim ............. EVET (Ctrl ile 2 satir)"
  else
    echo "   [5/8] coklu secim ............. HAYIR (2 bekleniyordu, $SECILI secili)"
    SORUN=1
  fi
else
  echo "   [5/8] coklu secim ............. OLCULEMEDI (pencere yok)"
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
  # Kok dugum: agacin ilk satiri (pencere ici y=116).
  xdotool mousemove "$(( PENCERE_X + 105 ))" "$(( PENCERE_Y + 116 ))" click 1 > /dev/null 2>&1
  sleep 1
  xdotool key --clearmodifiers ctrl+a > /dev/null 2>&1
  sleep 2

  import -window root "$CALISMA/ctrla.png" > /dev/null 2>&1
  GORUNEN="$(agac_satir_say "$CALISMA/ctrla.png" "$PENCERE_X" "$PENCERE_Y")"
  ICERDEKI="$(secili_satir_say "$CALISMA/ctrla.png" "$PENCERE_X" "$PENCERE_Y")"
  BEKLENEN=$(( GORUNEN - 1 ))

  if [ "${GORUNEN:-0}" -gt 1 ] && [ "${ICERDEKI:-0}" -eq "$BEKLENEN" ]; then
    echo "   [6/8] Ctrl+A kapsami .......... EVET ($ICERDEKI/$GORUNEN - kok secili degil)"
  else
    echo "   [6/8] Ctrl+A kapsami .......... HAYIR ($BEKLENEN bekleniyordu, $ICERDEKI secili)"
    SORUN=1
  fi
else
  echo "   [6/8] Ctrl+A kapsami .......... OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 7) tur suzgeci: "Parca" dugmesine tiklaninca agac gercekten suzuluyor mu
#
# NEDEN VAR: bu kapinin olmadigi bir turda suzgec dugmesinin Click baglantisi
# SILINDI ve kimse gormeden pakete girdi; Erkan bildirdi. Dugmeler ciziliyor,
# odagi aliyor, uzerine gelince renk degistiriyor - ama hicbir sey yapmiyordu.
# Derleme de testler de TEMIZ diyordu (CLAUDE.md 9).
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  ONCE="$(agac_satir_say "$CALISMA/ctrla.png" "$PENCERE_X" "$PENCERE_Y")"

  # Suzgec seridi: pencere ici y=95. "Parca" ucuncu dugme, x=171.
  xdotool mousemove "$(( PENCERE_X + 171 ))" "$(( PENCERE_Y + 95 ))" > /dev/null 2>&1
  sleep 1
  xdotool click 1 > /dev/null 2>&1
  sleep 2

  import -window root "$CALISMA/suzgec.png" > /dev/null 2>&1
  SONRA="$(agac_satir_say "$CALISMA/suzgec.png" "$PENCERE_X" "$PENCERE_Y")"

  if [ "${SONRA:-0}" -gt 0 ] && [ "${SONRA:-0}" -lt "${ONCE:-0}" ]; then
    echo "   [7/8] tur suzgeci .............. EVET ($ONCE -> $SONRA satir)"
  else
    echo "   [7/8] tur suzgeci .............. HAYIR (once $ONCE, sonra $SONRA - suzulmedi)"
    SORUN=1
  fi
else
  echo "   [7/8] tur suzgeci .............. OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 8) GERI AL: yeni klasor acilir, Ctrl+Z ile geri alinir
#
# NEDEN VAR: geri alma DOSYA SILIYOR. Sessizce bozulursa kullanici "geri
# aldim" sanip devam eder. Olcum: Ctrl+Shift+N agaca bir satir EKLER,
# Ctrl+Z o satiri GERI ALIR. Ikisi de sayilarak dogrulanir.
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  xdotool mousemove "$(( PENCERE_X + 105 ))" "$(( PENCERE_Y + 116 ))" click 1 > /dev/null 2>&1
  sleep 1
  ONCEKI="$(agac_satir_say "$CALISMA/suzgec.png" "$PENCERE_X" "$PENCERE_Y")"

  xdotool key --clearmodifiers ctrl+shift+n > /dev/null 2>&1
  sleep 2
  import -window root "$CALISMA/klasor.png" > /dev/null 2>&1
  EKLENDI="$(agac_satir_say "$CALISMA/klasor.png" "$PENCERE_X" "$PENCERE_Y")"

  xdotool key --clearmodifiers ctrl+z > /dev/null 2>&1
  sleep 2
  import -window root "$CALISMA/gerial.png" > /dev/null 2>&1
  GERIALINDI="$(agac_satir_say "$CALISMA/gerial.png" "$PENCERE_X" "$PENCERE_Y")"

  if [ "${EKLENDI:-0}" -gt "${ONCEKI:-0}" ] && [ "${GERIALINDI:-0}" -eq "${ONCEKI:-0}" ]; then
    echo "   [8/8] geri al (Ctrl+Z) ........ EVET ($ONCEKI -> $EKLENDI -> $GERIALINDI)"
  else
    echo "   [8/8] geri al (Ctrl+Z) ........ HAYIR ($ONCEKI -> $EKLENDI -> $GERIALINDI)"
    SORUN=1
  fi
else
  echo "   [8/8] geri al (Ctrl+Z) ........ OLCULEMEDI (pencere yok)"
  SORUN=1
fi

import -window root "$GORUNTU" > /dev/null 2>&1 && echo "   goruntu: $GORUNTU"

if [ "$SORUN" -ne 0 ]; then
  echo "== KAPI KIRIK =="
  exit 1
fi
echo "== KAPI TEMIZ =="

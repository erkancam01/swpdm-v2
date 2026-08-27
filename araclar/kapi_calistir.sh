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
# DONUS: 0 = pencere acildi ve hata yok. Diger her sey = KIRIK.

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

pkill -f "Xvfb :$EKRAN_NO" > /dev/null 2>&1
pkill -f "$AD.exe"         > /dev/null 2>&1
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
  pkill -f winedbg > /dev/null 2>&1
}
trap temizle EXIT

# ---------------------------------------------------------- ornek klasor
ORNEK="$CALISMA/ornek-klasor/ORJINAL"
rm -rf "$CALISMA/ornek-klasor"
mkdir -p "$ORNEK"/{1,2,33,222,"alt klasor"}
# Kokte de dosya olsun: ekran goruntusunde simgeler ve adlar gorunsun.
: > "$ORNEK/Govde.SLDASM"
: > "$ORNEK/Kapak.SLDPRT"
: > "$ORNEK/Kapak.SLDDRW"
: > "$ORNEK/katalog.pdf"
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
SORUN=0

# 1) surec ayakta mi
if kill -0 "$UYG_PID" > /dev/null 2>&1; then
  echo "   [1/4] surec ayakta ............ EVET"
else
  echo "   [1/4] surec ayakta ............ HAYIR (uygulama oldu)"
  SORUN=1
fi

# 2) hata akisa dustu mu (Program.cs hem kutuya hem akisa yaziyor)
if grep -qaE "Unhandled exception|Exception:" "$UYGULAMA_LOG" 2>/dev/null; then
  echo "   [2/4] hata akisi temiz ........ HAYIR"
  grep -aE "Unhandled exception|Exception:" "$UYGULAMA_LOG" | head -3 | sed 's/^/           /'
  SORUN=1
else
  echo "   [2/4] hata akisi temiz ........ EVET"
fi

# 3) Wine'in cokme penceresi acildi mi
PENCERELER="$(xwininfo -root -children 2>/dev/null)"
if echo "$PENCERELER" | grep -qi "winedbg"; then
  echo "   [3/4] cokme penceresi yok ..... HAYIR (winedbg acilmis)"
  SORUN=1
else
  echo "   [3/4] cokme penceresi yok ..... EVET"
fi

# 4) ana pencere dogdu mu: uygulamaya ait, 400x400'den buyuk bir ust pencere
ANA="$(echo "$PENCERELER" | grep -i "(\"${AD,,}.exe\"" \
      | grep -oE '[0-9]+x[0-9]+\+[-0-9]+\+[-0-9]+' \
      | awk -F'[x+]' '$1 >= 400 && $2 >= 400 {print $1"x"$2; exit}')"
if [ -n "$ANA" ]; then
  echo "   [4/4] ana pencere dogdu ....... EVET ($ANA)"
else
  echo "   [4/4] ana pencere dogdu ....... HAYIR (400x400'den buyuk pencere yok)"
  echo "$PENCERELER" | grep -i "${AD,,}.exe" | head -5 | sed 's/^/           /'
  SORUN=1
fi

import -window root "$GORUNTU" > /dev/null 2>&1 && echo "   goruntu: $GORUNTU"

if [ "$SORUN" -ne 0 ]; then
  echo "== KAPI KIRIK =="
  exit 1
fi
echo "== KAPI TEMIZ =="

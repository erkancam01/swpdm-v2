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
# Alt panel: solda onizleme kutusu, sagda referans listesi.
ONIZLEME_KIRP="250x280+15+458"                  # pencere ici: genislikxyukseklik+x+y
REFERANS_KIRP="260x150+295+458"
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
  convert "$1" -crop "560x250+$(( $2 + 5 ))+$(( $3 + AGAC_ILK_SATIR - 7 ))" +repage txt:- 2>/dev/null \
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

SORUN=0

# 1) surec ayakta mi
if kill -0 "$UYG_PID" > /dev/null 2>&1; then
  echo "   [1/11] surec ayakta ............ EVET"
else
  echo "   [1/11] surec ayakta ............ HAYIR (uygulama oldu)"
  SORUN=1
fi

# 2) hata akisa dustu mu (Program.cs hem kutuya hem akisa yaziyor)
if grep -qaE "Unhandled exception|Exception:" "$UYGULAMA_LOG" 2>/dev/null; then
  echo "   [2/11] hata akisi temiz ........ HAYIR"
  grep -aE "Unhandled exception|Exception:" "$UYGULAMA_LOG" | head -3 | sed 's/^/           /'
  SORUN=1
else
  echo "   [2/11] hata akisi temiz ........ EVET"
fi

# 3) Wine'in cokme penceresi acildi mi
PENCERELER="$(xwininfo -root -children 2>/dev/null)"
if echo "$PENCERELER" | grep -qi "winedbg"; then
  echo "   [3/11] cokme penceresi yok ..... HAYIR (winedbg acilmis)"
  SORUN=1
else
  echo "   [3/11] cokme penceresi yok ..... EVET"
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
  echo "   [4/11] ana pencere dogdu ....... EVET ($ANA)"
else
  echo "   [4/11] ana pencere dogdu ....... HAYIR (400x400'den buyuk pencere yok)"
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
    echo "   [5/11] coklu secim ............. EVET (Ctrl ile 2 satir)"
  else
    echo "   [5/11] coklu secim ............. HAYIR (2 bekleniyordu, $SECILI secili)"
    SORUN=1
  fi
else
  echo "   [5/11] coklu secim ............. OLCULEMEDI (pencere yok)"
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
    echo "   [6/11] Ctrl+A kapsami .......... EVET ($ICERDEKI/$GORUNEN - kok secili degil)"
  else
    echo "   [6/11] Ctrl+A kapsami .......... HAYIR ($BEKLENEN bekleniyordu, $ICERDEKI secili)"
    SORUN=1
  fi
else
  echo "   [6/11] Ctrl+A kapsami .......... OLCULEMEDI (pencere yok)"
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
    echo "   [7/11] siralama (Ctrl+Shift+S) . EVET (ters cevirdi, sekiz halde basa dondu)"
  else
    echo "   [7/11] siralama (Ctrl+Shift+S) . HAYIR (bas=${IZ_BAS:0:8} ters=${IZ_TERS:0:8} donus=${IZ_DONUS:0:8})"
    SORUN=1
  fi
else
  echo "   [7/11] siralama (Ctrl+Shift+S) . OLCULEMEDI (pencere yok)"
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
    echo "   [8/11] tur suzgeci .............. EVET ($ONCE -> $SONRA satir)"
  else
    echo "   [8/11] tur suzgeci .............. HAYIR (once $ONCE, sonra $SONRA - suzulmedi)"
    SORUN=1
  fi
else
  echo "   [8/11] tur suzgeci .............. OLCULEMEDI (pencere yok)"
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
  import -window root "$CALISMA/klasor.png" > /dev/null 2>&1
  EKLENDI="$(agac_satir_say "$CALISMA/klasor.png" "$PENCERE_X" "$PENCERE_Y")"

  xdotool key --clearmodifiers ctrl+z > /dev/null 2>&1
  sleep 2
  import -window root "$CALISMA/gerial.png" > /dev/null 2>&1
  GERIALINDI="$(agac_satir_say "$CALISMA/gerial.png" "$PENCERE_X" "$PENCERE_Y")"

  if [ "${EKLENDI:-0}" -gt "${ONCEKI:-0}" ] && [ "${GERIALINDI:-0}" -eq "${ONCEKI:-0}" ]; then
    echo "   [9/11] geri al (Ctrl+Z) ........ EVET ($ONCEKI -> $EKLENDI -> $GERIALINDI)"
  else
    echo "   [9/11] geri al (Ctrl+Z) ........ HAYIR ($ONCEKI -> $EKLENDI -> $GERIALINDI)"
    SORUN=1
  fi
else
  echo "   [9/11] geri al (Ctrl+Z) ........ OLCULEMEDI (pencere yok)"
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
    echo "   [10/11] onizleme (dosyadan) ... EVET ($PIKSEL piksel)"
  else
    echo "   [10/11] onizleme (dosyadan) ... HAYIR ($PIKSEL piksel - kutu bos)"
    SORUN=1
  fi
else
  echo "   [10/11] onizleme (dosyadan) ... OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 11) REFERANS LISTESI: tarama sonrasi "kim kullaniyor" doluyor mu
#
# NEDEN VAR: bu uygulamanin VARLIK SEBEBI. Liste sessizce bos kalirsa
# kullanici "bu parcayi kimse kullanmiyor" sanip SILER (CLAUDE.md 3).
# Olcum: Ctrl+Shift+R ile taranir, parca secilir, sag alt listedeki
# beyaz olmayan piksel sayilir. Tarama ONCESI de bakiliyor - liste
# taramadan once BOS olmali, sonra DOLMALI; ikisi birden olcum.
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  # SAYI DEGIL PARMAK IZI: liste tarama ONCESI de bos degil - icinde
  # "Bilinmiyor / taranmadı" satiri duruyor (CLAUDE.md 3: bos birakmak
  # "referansi yok" diye okunurdu). Yani "doldu mu" sorusu ayirt etmiyor;
  # ayirt eden sey ICERIGIN DEGISMESI.
  IZ_ONCE="$(kirpma_izi "$CALISMA/onizleme.png" "$REFERANS_KIRP" "$PENCERE_X" "$PENCERE_Y")"
  ONCE_L="$(beyaz_olmayan "$CALISMA/onizleme.png" "$REFERANS_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  xdotool key --clearmodifiers ctrl+shift+r > /dev/null 2>&1
  sleep 8
  xdotool mousemove "$(( PENCERE_X + AGAC_TIK_X ))" "$SON_SATIR" click 1 > /dev/null 2>&1
  sleep 4
  import -window root "$CALISMA/referans.png" > /dev/null 2>&1
  IZ_SONRA="$(kirpma_izi "$CALISMA/referans.png" "$REFERANS_KIRP" "$PENCERE_X" "$PENCERE_Y")"
  SONRA_L="$(beyaz_olmayan "$CALISMA/referans.png" "$REFERANS_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  if [ "$IZ_ONCE" != "$IZ_SONRA" ] && [ "${SONRA_L:-0}" -gt 50 ]; then
    echo "   [11/11] referans listesi ...... EVET (tarama sonrasi icerik degisti, $SONRA_L piksel)"
  else
    echo "   [11/11] referans listesi ...... HAYIR (iz ${IZ_ONCE:0:8} -> ${IZ_SONRA:0:8}, $SONRA_L piksel)"
    SORUN=1
  fi
else
  echo "   [11/11] referans listesi ...... OLCULEMEDI (pencere yok)"
  SORUN=1
fi

import -window root "$GORUNTU" > /dev/null 2>&1 && echo "   goruntu: $GORUNTU"

if [ "$SORUN" -ne 0 ]; then
  echo "== KAPI KIRIK =="
  exit 1
fi
echo "== KAPI TEMIZ =="

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

# ---- olcum numaralama -------------------------------------------------------
# NEDEN SAYAC (29.08.2026 denetimi): "[N/14]" etiketi 38 yerde ELLE yaziliydi.
# Bir olcum eklemek/cikarmak hepsini kaydiriyordu - ve gecmiste tam boyle
# oldu: siralama araya girince suzgec 8., geri alma 9. oldu ve CLAUDE.md'de
# numaralar elle duzeltildi. Simdi numara KOSARKEN sayiliyor; elle numara
# kalmadi, kaymasi imkansiz.
OLCUM_TOPLAM=21
OLCUM_NO=0
olcum() {
  # olcum "<ad ....>" "<EVET/HAYIR/OLCULEMEDI ...>"
  OLCUM_NO=$((OLCUM_NO + 1))
  echo "   [$OLCUM_NO/$OLCUM_TOPLAM] $1 $2"
}

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
# REFERANS PANELI: ustte SEKME SERIDI (30.08.2026), altinda liste. Serit
# SARMALI - bu dar pencerede uc satir ediyor ve listeyi ~77 piksel asagi
# itiyor; asagidaki iki sayi OLCULDU (ekran goruntusunden), tahmin degil.
REFERANS_KIRP="260x120+295+535"                 # SERIDIN ALTINDAKI liste alani
REF_SATIR_X=360                                 # referans panelinde tiklanacak x
REF_ILK_SATIR_Y=543                             # listenin ILK veri satiri (y)
ONIZLEME_BASLIK_X=60                             # onizleme panelinin ustundeki ad (x)
ONIZLEME_BASLIK_Y=463
ONIZLEME_BASLIK_KIRP="200x16+15+456"            # basligin kendisi (iz icin)
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
  olcum "surec ayakta ............" "EVET"
else
  olcum "surec ayakta ............" "HAYIR (uygulama oldu)"
  SORUN=1
fi

# 2) hata akisa dustu mu (Program.cs hem kutuya hem akisa yaziyor)
if grep -qaE "Unhandled exception|Exception:" "$UYGULAMA_LOG" 2>/dev/null; then
  olcum "hata akisi temiz ........" "HAYIR"
  grep -aE "Unhandled exception|Exception:" "$UYGULAMA_LOG" | head -3 | sed 's/^/           /'
  SORUN=1
else
  olcum "hata akisi temiz ........" "EVET"
fi

# 3) Wine'in cokme penceresi acildi mi
PENCERELER="$(xwininfo -root -children 2>/dev/null)"
if echo "$PENCERELER" | grep -qi "winedbg"; then
  olcum "cokme penceresi yok ....." "HAYIR (winedbg acilmis)"
  SORUN=1
else
  olcum "cokme penceresi yok ....." "EVET"
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
  olcum "ana pencere dogdu ......." "EVET ($ANA)"
  # HICBIR SEY SECILI DEGILKEN bir goruntu: 13. olcum satir rengine bakiyor
  # ve secim boyasi rengi ORTERDI. Sonraki olcumler tiklamaya basliyor.
  import -window root "$CALISMA/ilk.png" > /dev/null 2>&1
else
  olcum "ana pencere dogdu ......." "HAYIR (400x400'den buyuk pencere yok)"
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
    olcum "coklu secim ............." "EVET (Ctrl ile 2 satir)"
  else
    olcum "coklu secim ............." "HAYIR (2 bekleniyordu, $SECILI secili)"
    SORUN=1
  fi
else
  olcum "coklu secim ............." "OLCULEMEDI (pencere yok)"
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
    olcum "Ctrl+A kapsami .........." "EVET ($ICERDEKI/$GORUNEN - kok secili degil)"
  else
    olcum "Ctrl+A kapsami .........." "HAYIR ($BEKLENEN bekleniyordu, $ICERDEKI secili)"
    SORUN=1
  fi
else
  olcum "Ctrl+A kapsami .........." "OLCULEMEDI (pencere yok)"
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
    olcum "siralama (Ctrl+Shift+S) ." "EVET (ters cevirdi, sekiz halde basa dondu)"
  else
    olcum "siralama (Ctrl+Shift+S) ." "HAYIR (bas=${IZ_BAS:0:8} ters=${IZ_TERS:0:8} donus=${IZ_DONUS:0:8})"
    SORUN=1
  fi
else
  olcum "siralama (Ctrl+Shift+S) ." "OLCULEMEDI (pencere yok)"
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
    olcum "tur suzgeci .............." "EVET ($ONCE -> $SONRA satir)"
  else
    olcum "tur suzgeci .............." "HAYIR (once $ONCE, sonra $SONRA - suzulmedi)"
    SORUN=1
  fi
else
  olcum "tur suzgeci .............." "OLCULEMEDI (pencere yok)"
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
    olcum "geri al (Ctrl+Z) ........" "EVET ($ONCEKI -> $EKLENDI -> $GERIALINDI)"
  else
    olcum "geri al (Ctrl+Z) ........" "HAYIR ($ONCEKI -> $EKLENDI -> $GERIALINDI)"
    SORUN=1
  fi
else
  olcum "geri al (Ctrl+Z) ........" "OLCULEMEDI (pencere yok)"
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
    olcum "onizleme (dosyadan) ..." "EVET ($PIKSEL piksel)"
  else
    olcum "onizleme (dosyadan) ..." "HAYIR ($PIKSEL piksel - kutu bos)"
    SORUN=1
  fi
else
  olcum "onizleme (dosyadan) ..." "OLCULEMEDI (pencere yok)"
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
    olcum "referans listesi ......" "EVET (referansli/referanssiz ayrisiyor, $SW_L piksel)"
  else
    olcum "referans listesi ......" "HAYIR (iz ${IZ_SW:0:8} / ${IZ_TXT:0:8}, $SW_L piksel)"
    SORUN=1
  fi

  # 12. olcum bu goruntuye bakiyor: SOLIDWORKS dosyasi secili olan.
  cp "$CALISMA/referans.png" "$CALISMA/referans-son.png" 2>/dev/null
else
  olcum "referans listesi ......" "OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 12) YON AYRIMI: dort bolum seridi gercekten AYRI listeler gosteriyor mu
#
# NEDEN VAR: liste uc ayri soruya cevap veriyor - "bu dosya neyi kullaniyor"
# (ICINDEKILER), "bu dosyayi kim kullaniyor" (KULLANILDIGI YERLER) ve
# "hangileri kirik". Yon karistirmak bu uygulamada tehlikeli: "beni kimse
# kullanmiyor" diye okunan bir satir SAGLAM DOSYA SILDIRIR (CLAUDE.md 3).
#
# OLCUM DEGISTI (30.08.2026): once bolum basliklarinin ZEMIN RENGI sayiliyordu
# (iki bant). Basliklar kalkti - islerini serit yapiyor - ve o olcum
# ANLAMSIZ kaldi. Yerine ayni tehlikeyi olcen sey kondu: bolumlerin listesi
# birbirinden FARKLI olmali. (31.08.2026: VERSIYONLAR eklendi, dongu DORT
# bolum oldu - olcum de dorde cikti; uc basista basa donmesini bekleyen
# eski hali tam da bu yuzden HAYIR dedi, sayaci kapinin kendisi yakaladi.)
#
# SERIT WINE'DA TIKLANMIYOR degil ama koordinati sarmaya bagli; ayni kodu
# cagiran Ctrl+Shift+E olculuyor (CLAUDE.md 11: menusuz/faresiz kalan ozellik
# kor noktadir).
#
# IKI SART: uc iz de birbirinden farkli OLMALI, VE ucuncu basista basa
# donmeli. Tek basina "degisti" demek yetmez - dugme etiketi degistigi icin
# de saglanabilirdi (siralama olcumundeki dersin aynisi).
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  xdotool mousemove "$(( PENCERE_X + AGAC_TIK_X ))" "$SON_SATIR" click 1 > /dev/null 2>&1
  sleep 4
  import -window root "$CALISMA/bolum1.png" > /dev/null 2>&1
  IZ_B1="$(kirpma_izi "$CALISMA/bolum1.png" "$REFERANS_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  xdotool key --clearmodifiers ctrl+shift+e > /dev/null 2>&1
  sleep 3
  import -window root "$CALISMA/bolum2.png" > /dev/null 2>&1
  IZ_B2="$(kirpma_izi "$CALISMA/bolum2.png" "$REFERANS_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  xdotool key --clearmodifiers ctrl+shift+e > /dev/null 2>&1
  sleep 3
  import -window root "$CALISMA/bolum3.png" > /dev/null 2>&1
  IZ_B3="$(kirpma_izi "$CALISMA/bolum3.png" "$REFERANS_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  xdotool key --clearmodifiers ctrl+shift+e > /dev/null 2>&1
  sleep 3
  import -window root "$CALISMA/bolum4.png" > /dev/null 2>&1
  IZ_B4="$(kirpma_izi "$CALISMA/bolum4.png" "$REFERANS_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  xdotool key --clearmodifiers ctrl+shift+e > /dev/null 2>&1
  sleep 3
  import -window root "$CALISMA/bolum5.png" > /dev/null 2>&1
  IZ_B5="$(kirpma_izi "$CALISMA/bolum5.png" "$REFERANS_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  if [ "$IZ_B1" != "$IZ_B2" ] && [ "$IZ_B1" != "$IZ_B3" ] && [ "$IZ_B1" != "$IZ_B4" ] \
     && [ "$IZ_B2" != "$IZ_B3" ] && [ "$IZ_B2" != "$IZ_B4" ] && [ "$IZ_B3" != "$IZ_B4" ] \
     && [ "$IZ_B5" = "$IZ_B1" ]; then
    olcum "yon ayrimi (dort bolum)" "EVET (dort liste farkli, basa dondu)"
  else
    olcum "yon ayrimi (dort bolum)" \
      "HAYIR (iz ${IZ_B1:0:6}/${IZ_B2:0:6}/${IZ_B3:0:6}/${IZ_B4:0:6}/${IZ_B5:0:6})"
    SORUN=1
  fi
else
  olcum "yon ayrimi (dort bolum)" "OLCULEMEDI (pencere yok)"
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
    olcum "kilit dosyalari ......." "EVET (kilit gizlendi, sahibi isaretli)"
  else
    olcum "kilit dosyalari ......." "HAYIR ($KILIT_BANT isaret, 1 bekleniyordu)"
    SORUN=1
  fi
else
  olcum "kilit dosyalari ......." "OLCULEMEDI (pencere yok)"
  SORUN=1
fi

import -window root "$GORUNTU" > /dev/null 2>&1

# 14) ESC: ARAMADAN CIKIS
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
    olcum "Esc ile aramadan cikis" "EVET ($ESC_TABAN -> $ESC_ARAMA -> $ESC_SONRA)"
  else
    olcum "Esc ile aramadan cikis" "HAYIR ($ESC_TABAN -> $ESC_ARAMA -> $ESC_SONRA)"
    SORUN=1
  fi
else
  olcum "Esc ile aramadan cikis" "OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 15) KOMSU ONIZLEME: referans satirina tiklayinca onizleme O dosyaya doner,
#     ustteki ada tiklayinca CIPAYA (agacta seciliye) geri gelir
#
# NEDEN VAR (Erkan, 29.08.2026): "kullananlar listesindeki dosyalarin
# resmine yerinden kipirdamadan bakayim." Tek tikin baglantisi bir tani
# temizliginde sessizce silinirse (CLAUDE.md 8'in suzgec vakasi) bunu
# yalnizca bu olcum gorur.
#
# Olcum PARMAK IZIYLE: once .SLDPRT secilir (cipa) ve onizleme kirpmasinin
# izi alinir; referans satirina tiklaninca iz DEGISMELI (baslik "◂ ..."
# olur, bilgiler komsuya doner); usteki ada tiklaninca ilk ize DONMELI.
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  xdotool mousemove "$(( PENCERE_X + AGAC_TIK_X ))" "$SON_SATIR" click 1 > /dev/null 2>&1
  sleep 5
  import -window root "$CALISMA/komsu-once.png" > /dev/null 2>&1
  IZ_CIPA="$(kirpma_izi "$CALISMA/komsu-once.png" "$ONIZLEME_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  # KOMSU, "KULLANILDIGI YERLER" bolumundedir (Parça1.SLDDRW). Serit
  # ICINDEKILER'de duruyor - once oraya gecilir (12. olcum basa dondurmustu).
  xdotool key --clearmodifiers ctrl+shift+e > /dev/null 2>&1
  sleep 3
  xdotool mousemove "$(( PENCERE_X + REF_SATIR_X ))" "$(( PENCERE_Y + REF_ILK_SATIR_Y ))" \
    click 1 > /dev/null 2>&1
  sleep 5
  import -window root "$CALISMA/komsu-sonra.png" > /dev/null 2>&1
  IZ_KOMSU="$(kirpma_izi "$CALISMA/komsu-sonra.png" "$ONIZLEME_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  xdotool mousemove "$(( PENCERE_X + ONIZLEME_BASLIK_X ))" "$(( PENCERE_Y + ONIZLEME_BASLIK_Y ))" \
    click 1 > /dev/null 2>&1
  sleep 5
  import -window root "$CALISMA/komsu-geri.png" > /dev/null 2>&1
  IZ_GERI="$(kirpma_izi "$CALISMA/komsu-geri.png" "$ONIZLEME_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  if [ "$IZ_CIPA" != "$IZ_KOMSU" ] && [ "$IZ_GERI" = "$IZ_CIPA" ]; then
    olcum "komsu onizleme ........" "EVET (degisti ve cipaya dondu)"
  else
    olcum "komsu onizleme ........" "HAYIR (iz ${IZ_CIPA:0:6}/${IZ_KOMSU:0:6}/${IZ_GERI:0:6})"
    SORUN=1
  fi
else
  olcum "komsu onizleme ........" "OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 16) PANELDEN ISLEM: referans satirindaki dosyaya uygulanmali (agactakine DEGIL)
#
# NEDEN VAR: sag tik menusunun kendisi Wine'da OLCULEMEZ - acilan her
# ToolStripDropDown uygulamayi cokertiyor (CLAUDE.md 11). Menuyle AYNI kodu
# cagiran kisayol yolu olculuyor.
#
# NEDEN TAM BU: bu ozelligin tek gercek tehlikesi hedef karisikligi. Agacta
# "Parça1.SLDPRT" secili, panelde "Parça1.SLDDRW" satirina tiklanmis; F2
# YANLIS dosyaya giderse kullanici parcayi adlandirdigini bilmeden montaji
# adlandirir (CLAUDE.md 3). Olcum EKRANDAN degil DISKTEN yapiliyor - uzanti
# hangi dosyanin adlandigini tek basina soyluyor.
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  # agacta Parça1.SLDPRT; panelde onu KULLANAN Parça1.SLDDRW satiri
  xdotool mousemove "$(( PENCERE_X + AGAC_TIK_X ))" "$SON_SATIR" click 1 > /dev/null 2>&1
  sleep 5
  # Serit 15. olcumden beri "KULLANILDIGI YERLER"de; satir Parça1.SLDDRW.
  xdotool mousemove "$(( PENCERE_X + REF_SATIR_X ))" "$(( PENCERE_Y + REF_ILK_SATIR_Y ))" \
    click 1 > /dev/null 2>&1
  sleep 3

  xdotool key F2 > /dev/null 2>&1
  sleep 3
  xdotool type --delay 60 "PanelAdi" > /dev/null 2>&1
  sleep 1
  xdotool key Return > /dev/null 2>&1
  sleep 4

  # ONARIM KUTUSU: ad kutusundan sonra "kimin kullandigini bilmiyoruz"
  # uyarisi cikiyor ve odak "Vazgeç"te. Sol ok "Evet"e gecirir.
  # (Kutunun CIKMASI bu olcumun ilk kosusunda GORULDU - tahmin degil;
  # ustelik metni "Parça1.SLDDRW" yaziyordu, yani hedef daha o an dogruydu.)
  xdotool key Left > /dev/null 2>&1
  sleep 1
  xdotool key Return > /dev/null 2>&1
  sleep 6
  import -window root "$CALISMA/panel-islem.png" > /dev/null 2>&1

  # Ad kutusu tabani secili acabilir de acmayabilir de; ikisinde de yeni ad
  # "PanelAdi" ile BASLAR. Ayirt edici olan UZANTI.
  YENI_DRW="$(find "$ORNEK" -maxdepth 1 -iname "PanelAdi*.SLDDRW" | wc -l)"
  YENI_PRT="$(find "$ORNEK" -maxdepth 1 -iname "PanelAdi*.SLDPRT" | wc -l)"

  if [ "$YENI_DRW" -eq 1 ] && [ "$YENI_PRT" -eq 0 ]; then
    olcum "panelden islem ........" "EVET (satirin dosyasi adlandi)"
  elif [ "$YENI_PRT" -gt 0 ]; then
    olcum "panelden islem ........" "HAYIR (AGACTAKI dosya adlandi - yanlis hedef)"
    SORUN=1
  else
    olcum "panelden islem ........" "HAYIR (hicbir sey adlanmadi)"
    SORUN=1
  fi
else
  olcum "panelden islem ........" "OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 17) VERSIYON OLUSTUR: Ctrl+Shift+U o anki icerigi v0 olarak ARSIVLEMELI
#
# NEDEN VAR: versiyon arsivi dosya KOPYALAYAN bir ozellik; kopya sessizce
# olusmazsa kullanici "versiyonladim" sanip dosyanin ustune yazar ve eski
# hal GERI GELMEZ (CLAUDE.md 1a/3). Cekirdek birim testli (Linux'ta 11
# test); burada olculen, KISAYOL -> NOT KUTUSU -> DISK zinciri.
#
# Olcum EKRANDAN degil DISKTEN: arsiv kopyasi olustu mu ve icerigi asilla
# BIREBIR ayni mi (cmp). Ikinci sart: agactaki satir sayisi DEGISMEMELI -
# .SwPdmSurum gizli kalmali; gorunse kullanici onu dosya sanip tasir.
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  xdotool mousemove "$(( PENCERE_X + AGAC_TIK_X ))" "$SON_SATIR" click 1 > /dev/null 2>&1
  sleep 3
  import -window root "$CALISMA/surum-once.png" > /dev/null 2>&1
  SURUM_ONCE="$(agac_satir_say "$CALISMA/surum-once.png" "$PENCERE_X" "$PENCERE_Y")"

  xdotool key --clearmodifiers ctrl+shift+u > /dev/null 2>&1
  sleep 3
  xdotool key Return > /dev/null 2>&1          # not bos gecilir, Enter = Tamam
  sleep 4
  import -window root "$CALISMA/surum-sonra.png" > /dev/null 2>&1
  SURUM_SONRA="$(agac_satir_say "$CALISMA/surum-sonra.png" "$PENCERE_X" "$PENCERE_Y")"

  # VERSIYON SATIRINA TEK TIK = O VERSIYONUN ONIZLEMESI (Erkan, 31.08.2026).
  # Icerik iziyle olculemez: v0 bugunku dosyayla birebir ayni, resim
  # degismez. Ayiran sey BASLIK: "Parça1.SLDPRT" -> "◂ v0.SLDPRT".
  # Serit 16'dan beri KULLANILDIGI YERLER'de; iki ilerletme = VERSIYONLAR.
  xdotool key --clearmodifiers ctrl+shift+e > /dev/null 2>&1
  sleep 2
  xdotool key --clearmodifiers ctrl+shift+e > /dev/null 2>&1
  sleep 2
  import -window root "$CALISMA/surum-panel0.png" > /dev/null 2>&1
  IZ_S0="$(kirpma_izi "$CALISMA/surum-panel0.png" "$ONIZLEME_BASLIK_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  xdotool mousemove "$(( PENCERE_X + REF_SATIR_X ))" "$(( PENCERE_Y + REF_ILK_SATIR_Y ))" \
    click 1 > /dev/null 2>&1
  sleep 4
  import -window root "$CALISMA/surum-panel1.png" > /dev/null 2>&1
  IZ_S1="$(kirpma_izi "$CALISMA/surum-panel1.png" "$ONIZLEME_BASLIK_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  xdotool mousemove "$(( PENCERE_X + ONIZLEME_BASLIK_X ))" "$(( PENCERE_Y + ONIZLEME_BASLIK_Y ))" \
    click 1 > /dev/null 2>&1
  sleep 3
  import -window root "$CALISMA/surum-panel2.png" > /dev/null 2>&1
  IZ_S2="$(kirpma_izi "$CALISMA/surum-panel2.png" "$ONIZLEME_BASLIK_KIRP" "$PENCERE_X" "$PENCERE_Y")"

  # ARSIV ARTIK KLASOR: "v0/<gercek ad>" - versiyon kendi kendine yetiyor
  # (montajin cocuklari da yaninda arsivleniyor, 31.08.2026).
  SURUM_ARSIV="$ORNEK/.SwPdmSurum/Parça1.SLDPRT/v0/Parça1.SLDPRT"
  if [ -f "$SURUM_ARSIV" ] && cmp -s "$SURUM_ARSIV" "$ORNEK/Parça1.SLDPRT" \
     && [ "$SURUM_SONRA" = "$SURUM_ONCE" ] \
     && [ "$IZ_S1" != "$IZ_S0" ] && [ "$IZ_S2" = "$IZ_S0" ]; then
    olcum "versiyon olustur ......" "EVET (v0 arsivde birebir, agac degismedi, onizleme v0'a gecip dondu)"
  elif [ -f "$SURUM_ARSIV" ] && { [ "$IZ_S1" = "$IZ_S0" ] || [ "$IZ_S2" != "$IZ_S0" ]; }; then
    olcum "versiyon olustur ......" \
      "HAYIR (onizleme basligi: ${IZ_S0:0:6}/${IZ_S1:0:6}/${IZ_S2:0:6} - satira tik onizlemeyi degistirmedi)"
    SORUN=1
  elif [ ! -f "$SURUM_ARSIV" ]; then
    olcum "versiyon olustur ......" "HAYIR (arsiv kopyasi olusmadi)"
    SORUN=1
  elif ! cmp -s "$SURUM_ARSIV" "$ORNEK/Parça1.SLDPRT"; then
    olcum "versiyon olustur ......" "HAYIR (kopya asildan FARKLI)"
    SORUN=1
  else
    olcum "versiyon olustur ......" "HAYIR (agac $SURUM_ONCE -> $SURUM_SONRA - arsiv gorunur oldu)"
    SORUN=1
  fi
else
  olcum "versiyon olustur ......" "OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 18) ARSIV ADLA BIRLIKTE TASINIR: ad degisince VERSIYONLAR kaybolmamali
#
# NEDEN VAR (Erkan, 31.08.2026: "parçanın adını veya bağlı bulunduğu
# klasörün adını değiştirince versiyonlar gözükmüyor, versiyon yok diyor"):
# arsiv yuvasi dosyanin YOLUNDAN turetiliyor; ad degisince yuva OKSUZ kalir
# ve panel "Versiyon yok" der. Arsiv diskte durur ama kullanici KAYBOLDUGUNU
# sanir - "versiyonladim" deyip dosyanin ustune yazar (CLAUDE.md 3).
#
# Olcum EKRANDAN degil DISKTEN: yeni adin yuvasinda v0 var mi, eski yuva
# kalkti mi. 17. olcumden devraliyor (v0 orada uretildi).
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  # Agaca don ve Parça1.SLDPRT'yi sec (17 en son ONIZLEME BASLIGINA tikladi).
  xdotool mousemove "$(( PENCERE_X + AGAC_TIK_X ))" "$SON_SATIR" click 1 > /dev/null 2>&1
  sleep 4

  # AD KUTUSU GEC ACILIYOR: agactan F2, islemden ONCE referans taramasi
  # kosturuyor (ReferansTazeleme.Once). Ilk kosuda 3 saniye YETMEDI - yazi
  # hicbir yere gitmedi ve ad DEGISMEDI; kapi bunu yakaladi ve sebep
  # ekran goruntusunden okundu.
  xdotool key F2 > /dev/null 2>&1
  sleep 9
  import -window root "$CALISMA/ad-kutusu.png" > /dev/null 2>&1
  xdotool type --delay 60 "VParca1" > /dev/null 2>&1      # uzanti kutuda kilitli
  sleep 2
  xdotool key Return > /dev/null 2>&1
  sleep 5

  # ONARIM KUTUSU - 16. OLCUMDEKI KALIP BURADA YANLIS, OLCULDU (31.08.2026):
  # oradaki kutu "kimin kullandigi bilinmiyor" (tehlikeli: true) ve odak
  # "Vazgeç"te oldugu icin sol ok gerekiyordu. BURADA plan guvenilir ve
  # ebeveynler biliniyor -> duz onay kutusu cikiyor, odak zaten "Evet"te.
  # Sol ok basmak odagi "Vazgeç"e kaydirip ADLANDIRMAYI IPTAL ETTIRDI;
  # kapi "arsiv eski adda kaldi" dedi, ekran goruntusu (ad-kutusu.png)
  # kutunun acik oldugunu gosterdi ve sebep boyle bulundu.
  xdotool key Return > /dev/null 2>&1
  sleep 8

  YENI_YUVA="$ORNEK/.SwPdmSurum/VParca1.SLDPRT"
  ESKI_YUVA="$ORNEK/.SwPdmSurum/Parça1.SLDPRT"

  # Arsivdeki kopya ARSIVLENDIGI GUNKU adiyla durur; yuva yeni ada tasinir.
  YENI_ARSIV="$(find "$YENI_YUVA/v0" -maxdepth 1 -name "*.SLDPRT" 2>/dev/null | head -1)"

  if [ -n "$YENI_ARSIV" ] && [ ! -d "$ESKI_YUVA" ]; then
    olcum "arsiv adla tasindi ..." "EVET (yuva yeni adda, eski yuva kalmadi)"
  elif [ -n "$YENI_ARSIV" ]; then
    olcum "arsiv adla tasindi ..." "HAYIR (yeni yuva var ama ESKISI de duruyor)"
    SORUN=1
  elif [ -d "$ESKI_YUVA" ]; then
    olcum "arsiv adla tasindi ..." "HAYIR (arsiv eski adda kaldi - versiyonlar kayboldu)"
    SORUN=1
  else
    olcum "arsiv adla tasindi ..." "HAYIR (ad degismedi ya da arsiv yok)"
    SORUN=1
  fi

  # SONRAKI OLCUM YENI ADI KULLANIR.
  SURUM_ARSIV="$YENI_YUVA/v0"          # 19. olcum KLASORE bakar
else
  olcum "arsiv adla tasindi ..." "OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 19) VERSIYON BAKIMI: F2 NOTU YAZMALI, Delete VERSIYONU SILMELI
#
# NEDEN VAR: silme GERI ALINAMAZ ve cop kutusuna gitmez (Surumler.Sil).
# Sessizce YANLIS satiri silerse kullanici bunu ancak o versiyona donmek
# isteyince anlar - o an da is islemistir (CLAUDE.md 1a). Ikinci tehlike
# ters yonde: silme hic olmazsa kullanici "temizledim" sanip cop kayitlarla
# devam eder (Erkan'in elindeki v5/v6/v7'nin sebebi buydu).
#
# Olcum EKRANDAN DEGIL DISKTEN, ve iki asamali: once F2 ile yazilan not
# kayit dosyasinda GORULMELI (yazma yolu calisiyor), sonra Delete + onay
# hem arsiv kopyasini hem O SATIRI kaldirmali. Not once yaziliyor cunku
# satirin dogru KAYDA baglandigini tek basina kanitlayan sey o: yanlis
# kayda yazilsaydi silinen satir da baska olurdu.
#
# 17. olcumden devraliyor: VERSIYONLAR sekmesi acik, listede tek satir (v0).
# Satira yeniden tiklaniyor cunku tuslar YALNIZ panel odaktayken yonleniyor
# (AnaForm.Kisayollar) ve 17 en son ONIZLEME BASLIGINA tiklamisti.
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  SURUM_KAYIT="$ORNEK/.SwPdmSurum/VParca1.SLDPRT/kayit.txt"   # 18'de adlandi

  xdotool mousemove "$(( PENCERE_X + REF_SATIR_X ))" "$(( PENCERE_Y + REF_ILK_SATIR_Y ))" \
    click 1 > /dev/null 2>&1
  sleep 3

  xdotool key --clearmodifiers F2 > /dev/null 2>&1
  sleep 3
  xdotool type --delay 40 "kapi notu" > /dev/null 2>&1
  sleep 1
  xdotool key Return > /dev/null 2>&1              # Enter = Tamam
  sleep 3

  NOT_YAZILDI=0
  if [ -f "$SURUM_KAYIT" ] && grep -q "kapi notu" "$SURUM_KAYIT"; then
    NOT_YAZILDI=1
  fi

  # SATIRA YENIDEN TIK - OLCULDU (31.08.2026): not yazildiktan sonra panel
  # yeniden doluyor ve secim DUSUYOR; ilk kosuda Delete bu yuzden hicbir sey
  # yapmadi. Secimin dusmesi bilincli: silme geri alinamaz ve satirlar
  # tazelemeden sonra KAYABILIR - kullanicinin gozuyle secmedigi bir satira
  # Delete gitmemeli (CLAUDE.md 11'deki "yanlis hedef" tehlikesi).
  xdotool mousemove "$(( PENCERE_X + REF_SATIR_X ))" "$(( PENCERE_Y + REF_ILK_SATIR_Y ))" \
    click 1 > /dev/null 2>&1
  sleep 3

  xdotool key --clearmodifiers Delete > /dev/null 2>&1
  sleep 3
  import -window root "$CALISMA/surum-sil-onay.png" > /dev/null 2>&1
  xdotool key Left > /dev/null 2>&1                # onay "tehlikeli": odak Vazgec'te
  sleep 1
  xdotool key Return > /dev/null 2>&1
  sleep 3

  KOPYA_GITTI=0
  [ -d "$SURUM_ARSIV" ] || KOPYA_GITTI=1   # klasorun TAMAMI gitmeli

  SATIR_GITTI=0
  if [ ! -f "$SURUM_KAYIT" ] || ! grep -q "kapi notu" "$SURUM_KAYIT"; then
    SATIR_GITTI=1
  fi

  if [ "$NOT_YAZILDI" = "1" ] && [ "$KOPYA_GITTI" = "1" ] && [ "$SATIR_GITTI" = "1" ]; then
    olcum "versiyon bakimi ......." "EVET (F2 notu yazdi, Delete kopyayi ve satiri sildi)"
  elif [ "$NOT_YAZILDI" != "1" ]; then
    olcum "versiyon bakimi ......." "HAYIR (F2 notu kayda yazmadi)"
    SORUN=1
  elif [ "$KOPYA_GITTI" != "1" ]; then
    olcum "versiyon bakimi ......." "HAYIR (Delete arsiv kopyasini silmedi)"
    SORUN=1
  else
    olcum "versiyon bakimi ......." "HAYIR (kopya silindi ama kayit satiri kaldi)"
    SORUN=1
  fi
else
  olcum "versiyon bakimi ......." "OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 20) BU VERSIYONA DON: dosya GERCEKTEN eski icerige donmeli
#
# NEDEN VAR: "don" bugune kadar HIC olculmuyordu - oysa dosyanin UZERINE
# yazan tek islem o. Sessizce yanlis yazarsa kullanici "eski versiyona
# dondum" sanip calismaya devam eder ve bugunku hali kaybeder (CLAUDE.md 1a).
# Cekirdek birim testli; burada olculen KISAYOL -> KUTU -> DISK zinciri.
#
# Olcum EKRANDAN degil DISKTEN: dosya bozulur (icerigi degistirilir), sonra
# v0'a donulur ve dosya v0 kopyasiyla BIREBIR ayni olmali (cmp).
if [ -n "$ANA" ] && [ -n "$PENCERE_X" ] && [ -n "$PENCERE_Y" ]; then
  # 19'dan devir: VERSIYONLAR sekmesi acik, ama v0 SILINDI. Yeni bir
  # versiyon uretilir (agacta secili dosya hala ayni).
  xdotool mousemove "$(( PENCERE_X + AGAC_TIK_X ))" "$SON_SATIR" click 1 > /dev/null 2>&1
  sleep 3
  xdotool key --clearmodifiers ctrl+shift+u > /dev/null 2>&1
  sleep 3
  xdotool key Return > /dev/null 2>&1
  sleep 4

  DON_DOSYA="$(find "$ORNEK" -maxdepth 1 -name "VParca1*.SLDPRT" | head -1)"
  DON_ARSIV="$(find "$ORNEK/.SwPdmSurum" -path "*VParca1*/v0/*" -name "*.SLDPRT" | head -1)"

  if [ -n "$DON_DOSYA" ] && [ -n "$DON_ARSIV" ]; then
    # Dosyayi BOZ: donusun gercekten yazdigini boyle olcebiliyoruz.
    printf 'BOZULDU' >> "$DON_DOSYA"

    # DISK IZLEYICININ TAZELEMESI BEKLENIR - OLCULDU (31.08.2026): dosyayi
    # disaridan bozmak DiskIzleyici'yi tetikliyor, panel yeniden doluyor ve
    # SATIR SECIMI DUSUYOR. Ilk kosuda Enter bu yuzden "bu satirda gidilecek
    # bir dosya yok" dedi (durum cubugundan okundu, tahmin degil).
    sleep 7

    # Panele don ve satira tikla, Enter = "bu versiyona don".
    xdotool mousemove "$(( PENCERE_X + REF_SATIR_X ))" "$(( PENCERE_Y + REF_ILK_SATIR_Y ))" \
      click 1 > /dev/null 2>&1
    sleep 3
    import -window root "$CALISMA/don-satir.png" > /dev/null 2>&1
    xdotool key --clearmodifiers Return > /dev/null 2>&1
    sleep 4
    import -window root "$CALISMA/don-kutu.png" > /dev/null 2>&1
    xdotool key Return > /dev/null 2>&1          # kutuda Evet (odak Evet'te)
    sleep 6

    if cmp -s "$DON_DOSYA" "$DON_ARSIV"; then
      olcum "versiyona don ........" "EVET (dosya v0 icerigine dondu, diskten dogrulandi)"
    else
      olcum "versiyona don ........" "HAYIR (dosya v0 kopyasiyla ayni degil)"
      SORUN=1
    fi
  else
    olcum "versiyona don ........" "HAYIR (versiyon uretilemedi)"
    SORUN=1
  fi
else
  olcum "versiyona don ........" "OLCULEMEDI (pencere yok)"
  SORUN=1
fi

# 21) 3B AYARIYLA ACILIS: eDrawings YOKKEN cokmemeli, sebep yazilmali
#
# NEDEN VAR: 3B onizleme (Ayarlar) eDrawings'i kullaniyor; Wine'da ve
# SOLIDWORKS'suz Windows'ta eDrawings YOK. Burada olculebilen tek sey
# CLAUDE.md 11'deki WinRT kalibiyla ayni: COKMEDIGI ve sebep yazip 2B'ye
# dustugu. Uygulama 3B ayari ACIK olarak yeniden baslatilir ve bir dosya
# secilir; surec ayakta kalmali, hata akisina tek satir dusmemeli
# (ele alinan hatalar da akisa "SW PDM — hata:" olarak yazilir -
# Program.Bildir; yani sizinti buradan gorunur).
UYG2_LOG="$CALISMA/uygulama-3b.log"
: > "$UYG2_LOG"
kill "$UYG_PID" > /dev/null 2>&1
sleep 2

AYAR_KLASORU="$(find "$WINEPREFIX/drive_c/users" -maxdepth 4 -type d -name "Roaming" 2>/dev/null | head -1)"
if [ -n "$AYAR_KLASORU" ]; then
  mkdir -p "$AYAR_KLASORU/SwPdm"
  printf 'onizleme3b=evet\r\n' > "$AYAR_KLASORU/SwPdm/ayarlar.txt"

  ( cd "$YAYIN" && "$WINE" "./$AD.exe" --klasor "$ORNEK_WIN" >> "$UYG2_LOG" 2>&1 ) &
  UYG_PID=$!
  sleep 18

  P2="$(xwininfo -root -children 2>/dev/null)"
  K2="$(echo "$P2" | grep -i "(\"${AD,,}.exe\"" \
        | grep -oE '[0-9]+x[0-9]+\+[-0-9]+\+[-0-9]+' \
        | awk -F'[x+]' '$1 >= 400 && $2 >= 400 {print $3" "$4; exit}')"

  if [ -n "$K2" ]; then
    # shellcheck disable=SC2086
    set -- $K2
    # 3B dallanmasi SECIMDE kosuyor; kok seviyesindeki bir dosyaya tiklanir
    # (5. olcumun ilk satiri) ve uygulamanin ayakta kaldigina bakilir.
    xdotool mousemove "$(( $1 + AGAC_TIK_X ))" \
      "$(( $2 + AGAC_ILK_SATIR + AGAC_SATIR_YUKSEKLIGI * 6 ))" click 1 > /dev/null 2>&1
    sleep 5

    if ! kill -0 "$UYG_PID" > /dev/null 2>&1; then
      olcum "3B ayariyla acilis ...." "HAYIR (uygulama oldu)"
      SORUN=1
    elif grep -qaE "Unhandled exception|Exception:|SW PDM — hata" "$UYG2_LOG" 2>/dev/null; then
      olcum "3B ayariyla acilis ...." "HAYIR (hata akisina dusen var)"
      grep -aE "Unhandled exception|Exception:|SW PDM — hata" "$UYG2_LOG" | head -3 | sed 's/^/           /'
      SORUN=1
    else
      olcum "3B ayariyla acilis ...." "EVET (eDrawings'siz cokmedi)"
    fi
  else
    olcum "3B ayariyla acilis ...." "HAYIR (3B ayariyla pencere dogmadi)"
    SORUN=1
  fi
else
  olcum "3B ayariyla acilis ...." "OLCULEMEDI (wine kullanici klasoru yok)"
  SORUN=1
fi

echo "   goruntu: $GORUNTU"

if [ "$SORUN" -ne 0 ]; then
  echo "== KAPI KIRIK =="
  exit 1
fi
echo "== KAPI TEMIZ =="

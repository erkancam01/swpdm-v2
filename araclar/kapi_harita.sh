#!/usr/bin/env bash
#
# KAPI: harita - CLAUDE.md §11 tablosu ile gercek agac esit mi.
#
# NEDEN VAR (29.08.2026 denetimi): tablo 23 dosyayi hic saymiyordu
# (AnaForm.cs'in kendisi dahil) ve daha once silinmis bir dosyayi
# (BagimlilariEkle.cs) gosterdigi de olmustu. Haritasi bayat olan bir kod
# tabaninda "neye dokunacagim" sorusu yeniden koda dusuyor - Erkan'in
# "tek sayfaya dokunayim" istegi tam da bu haritayla ayakta duruyor.
#
# OLCTUGU UC YON:
#   1. src/ altindaki her .cs dosyasinin ADI CLAUDE.md'de geciyor mu
#      (tabloda ya da metinde - bulunabilir olmak yeterli).
#   2. CLAUDE.md'nin tablo satirlarinda gecen her .cs ADI agacta var mi
#      (bayat isaretci: silinen dosyayi gosteren satir).
#   3. Belgede yazan YOL, dosyanin GERCEK konumu mu.
#
# UCUNCU YON 31.08.2026'DA EKLENDI - ve tam da ihtiyac aninda: o gun 54
# dosya klasorlere tasindi. Kapi o zamana kadar yalnizca ADA bakiyordu,
# yani tasinan bir dosyanin tablodaki YOLU bayatlar ve kapi bunu GORMEZDI.
# "Nereye dokunacagim" sorusunun cevabi yanlis klasoru gosterirdi; kapi
# kendi korudugu seyi koruyamaz halde kalirdi.
#
# Kapsam ADLARA degil AGACA bagli (CLAUDE.md 9): dosya listesi find ile
# cikar; hicbir proje adi elle yazilmadi. Testler ve araclar kapsam disi -
# tablo "urun kodunun haritasi".

set -uo pipefail

KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BELGE="$KOK/CLAUDE.md"

echo "== KAPI: harita (CLAUDE.md §11 <-> agac) =="

if [ ! -f "$BELGE" ]; then
  echo "KAPI KIRIK: CLAUDE.md yok."
  exit 1
fi

SORUN=0

# --- 1. yon: agactaki her .cs, belgede geciyor mu -------------------------
EKSIK=0
BAKILAN=0
while IFS= read -r dosya; do
  BAKILAN=$((BAKILAN + 1))
  ad="$(basename "$dosya")"
  if ! grep -qF "$ad" "$BELGE"; then
    echo "   HARITADA YOK: $dosya"
    EKSIK=$((EKSIK + 1))
  fi
done < <(find "$KOK/src" -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*" | sort)

# --- 2. yon: tablo satirlarindaki her .cs adi agacta var mi ---------------
BAYAT=0
while IFS= read -r ad; do
  if ! find "$KOK/src" -name "$ad" -not -path "*/obj/*" -not -path "*/bin/*" | grep -q .; then
    echo "   BAYAT ISARETCI: tablo '$ad' diyor, agacta yok"
    BAYAT=$((BAYAT + 1))
  fi
done < <(grep -E '^\|' "$BELGE" | grep -oE '[A-Za-z0-9_.]+\.cs' | sort -u)

# --- 3. yon: belgede yazan YOL gercek konum mu ----------------------------
# Belgedeki yol "Cekirdek/..." / "Arayuz/..." bicimindedir; gercek yol
# "src/SwPdm." onekiyle. Karsilastirma o oneki ekleyerek yapiliyor.
YANLIS=0
while IFS= read -r yol; do
  if [ ! -f "$KOK/src/SwPdm.$yol" ]; then
    ad="$(basename "$yol")"
    gercek="$(find "$KOK/src" -name "$ad" -not -path "*/obj/*" -not -path "*/bin/*" \
              | head -1 | sed "s|^$KOK/src/SwPdm\.||")"
    if [ -n "$gercek" ]; then
      echo "   YANLIS YOL: belge '$yol' diyor, gercegi '$gercek'"
      YANLIS=$((YANLIS + 1))
    fi
  fi
done < <(grep -oE '`(Cekirdek|Arayuz)/[A-Za-z0-9_./-]+\.cs`' "$BELGE" \
         | tr -d '`' | sort -u)

echo "   bakilan dosya: $BAKILAN   haritada olmayan: $EKSIK   bayat isaretci: $BAYAT   yanlis yol: $YANLIS"

if [ "$EKSIK" -gt 0 ] || [ "$BAYAT" -gt 0 ] || [ "$YANLIS" -gt 0 ]; then
  echo "KAPI KIRIK: harita ile agac ayristi."
  echo "  Yeni dosya eklediysen CLAUDE.md §11 tablosuna bir satir ekle;"
  echo "  dosya sildiysen tablodan satirini dusur; dosyayi TASIDIYSAN"
  echo "  tablodaki yolu duzelt. Harita bayatlarsa 'neye dokunacagim'"
  echo "  sorusu yeniden koda duser."
  echo "== KAPI KIRIK =="
  exit 1
fi

echo "== KAPI TEMIZ =="

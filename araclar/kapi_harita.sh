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
# OLCTUGU IKI YON:
#   1. src/ altindaki her .cs dosyasinin ADI CLAUDE.md'de geciyor mu
#      (tabloda ya da metinde - bulunabilir olmak yeterli).
#   2. CLAUDE.md'nin tablo satirlarinda gecen her .cs ADI agacta var mi
#      (bayat isaretci: silinen dosyayi gosteren satir).
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

echo "   bakilan dosya: $BAKILAN   haritada olmayan: $EKSIK   bayat isaretci: $BAYAT"

if [ "$EKSIK" -gt 0 ] || [ "$BAYAT" -gt 0 ]; then
  echo "KAPI KIRIK: harita ile agac ayristi."
  echo "  Yeni dosya eklediysen CLAUDE.md §11 tablosuna bir satir ekle;"
  echo "  dosya sildiysen tablodan satirini dusur. Harita bayatlarsa"
  echo "  'neye dokunacagim' sorusu yeniden koda duser."
  echo "== KAPI KIRIK =="
  exit 1
fi

echo "== KAPI TEMIZ =="

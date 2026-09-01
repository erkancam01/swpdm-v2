#!/usr/bin/env bash
#
# KAPI: kilavuz - OZELLIKLER.md ile gercek menu esit mi.
#
# NEDEN VAR (01.09.2026 denetimi): kilavuz PAKETE giriyor (OZELLIKLER.txt)
# ve Erkan'in okudugu dosya o. Hicbir sey onu koda karsi denetlemiyordu ve
# denetim sirasinda ZATEN BAYATLAMISTI: menude 18 islem varken kilavuz
# "17 islem" diyordu ve "Yeni versiyon oluştur…" satiri hic yoktu - yani
# arsive YAZAN islem kilavuzda gorunmuyordu.
#
# Harita kapisi (CLAUDE.md <-> agac) ayni isi GELISTIRICI belgesi icin
# yapiyor; bu, KULLANICI belgesi icin yapiyor.
#
# OLCTUGU UC YON:
#   1. Her IAgacIslemi'nin ADI, kilavuzun MENU TABLOSUNDA geciyor mu.
#      TABLO ile sinirli, bilerek: adin belgenin bir yerinde gecmesi
#      YETMEZ - tam da o yuzden bayatlamisti ("Yeni versiyon oluştur…"
#      baska bolumlerde anlatiliyordu ama menu tablosunda satiri yoktu ve
#      tablo "menunun tamami" diye sunuluyor.
#   2. Tablo basligindaki SAYI gercek islem sayisina esit mi.
#   3. Her islemin KISAYOLU kilavuzda geciyor mu (Ctrl+Shift+N gibi).
#
# NEDEN TERSI YON YOK: kilavuzda menude olmayan bir sey yazmasi tek basina
# hata degil - orada tuslar, kutular ve akislar da anlatiliyor.
#
# KAPSAM ADLARA DEGIL AGACA BAGLI (CLAUDE.md 9): islem dosyalari find ile
# bulunuyor, hicbir dosya adi elle yazilmadi.

set -uo pipefail

KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
KILAVUZ="$KOK/OZELLIKLER.md"

echo "== KAPI: kilavuz (OZELLIKLER.md <-> menu) =="

if [ ! -f "$KILAVUZ" ]; then
  echo "KAPI KIRIK: OZELLIKLER.md yok."
  exit 1
fi

if ! command -v python3 > /dev/null 2>&1; then
  echo "KAPI KURULU DEGIL: python3 yok. Kurulu olmayan bir kapi GECTI sayilmaz."
  exit 1
fi

python3 - "$KOK" "$KILAVUZ" <<'PY'
import glob
import os
import re
import sys

kok, kilavuz_yolu = sys.argv[1], sys.argv[2]
kilavuz = open(kilavuz_yolu, encoding="utf-8").read()

# MENU TABLOSU: "## 4. ..." basligi ile bir sonraki "## " arasi.
tablo_eslesme = re.search(r"^## 4\..*?$(.*?)^## ", kilavuz, re.S | re.M)
if tablo_eslesme is None:
    print("   MENU TABLOSU BULUNAMADI (## 4. ... basligi)")
    sys.exit(1)

tablo = tablo_eslesme.group(1)
baslik = re.search(r"^## 4\..*?(\d+) işlem", kilavuz, re.M)

# Keys.Control | Keys.Shift | Keys.N  ->  Ctrl+Shift+N
def tus_metni(ifade):
    parcalar = re.findall(r"Keys\.(\w+)", ifade)
    if not parcalar or parcalar == ["None"]:
        return None
    ad = {"Control": "Ctrl", "Shift": "Shift", "Alt": "Alt"}
    return "+".join(ad.get(p, p) for p in parcalar)

sorun = 0
bakilan = 0

for yol in sorted(glob.glob(os.path.join(kok, "src", "**", "*.cs"), recursive=True)):
    if f"{os.sep}obj{os.sep}" in yol or f"{os.sep}bin{os.sep}" in yol:
        continue

    metin = open(yol, encoding="utf-8").read()
    if ": IAgacIslemi" not in metin:
        continue

    goreli = os.path.relpath(yol, kok)

    # --- 1. yon: ADI kilavuzda geciyor mu.
    # Ad hesaplanmis olabilir ("Geri al: " + ad); o yuzden ifadedeki
    # BUTUN metinlere bakiliyor, biri gecerse yeter.
    for ad_ifadesi in re.findall(r"string Ad\s*=>\s*(.+?);", metin, re.S):
        bakilan += 1
        adlar = re.findall(r'"([^"]+)"', ad_ifadesi)
        if not adlar:
            print(f"   ADI OKUNAMADI: {goreli}")
            sorun += 1
            continue

        if not any(a.strip().strip(":").strip() in tablo for a in adlar):
            print(f"   MENU TABLOSUNDA YOK: {adlar[0]}  <- {goreli}")
            sorun += 1

    # --- 3. yon: KISAYOLU kilavuzda geciyor mu (belgenin herhangi bir yeri).
    for tus_ifadesi in re.findall(r"Keys (?:Kisayol|YazilanTus)\s*=>\s*(.+?);", metin):
        tus = tus_metni(tus_ifadesi)
        if tus is None:
            continue

        bakilan += 1
        if tus not in kilavuz:
            print(f"   KILAVUZDA YOK (kisayol): {tus}  <- {goreli}")
            sorun += 1

# --- 2. yon: baslıktaki SAYI gercek mi (bayat sayi tek basina bir bulgu).
islem_sayisi = len(re.findall(r"string Ad\s*=>", "".join(
    open(y, encoding="utf-8").read()
    for y in sorted(glob.glob(os.path.join(kok, "src", "**", "*.cs"), recursive=True))
    if f"{os.sep}obj{os.sep}" not in y and f"{os.sep}bin{os.sep}" not in y
    and ": IAgacIslemi" in open(y, encoding="utf-8").read())))

if baslik is None:
    print("   BASLIKTA SAYI YOK: '## 4. ... N işlem' bekleniyordu")
    sorun += 1
elif int(baslik.group(1)) != islem_sayisi:
    print(f"   BASLIKTAKI SAYI BAYAT: kilavuz {baslik.group(1)}, gercek {islem_sayisi}")
    sorun += 1

print(f"   bakilan: {bakilan} · menude {islem_sayisi} islem")
sys.exit(1 if sorun else 0)
PY

if [ "$?" -ne 0 ]; then
  echo "== KAPI KIRIK =="
  exit 1
fi

echo "== KAPI TEMIZ =="

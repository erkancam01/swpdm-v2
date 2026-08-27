#!/usr/bin/env bash
#
# KAPI: dosya boyutu
#
# v1'in EN PAHALI hatasi tek bir arayuz sinifinin 9.918 satira cikmasiydi
# (urun kodunun %38'i) ve o dosya artik BOLUNEMIYORDU. Kimse zamaninda
# gormedi - cunku bakan bir sey yoktu. Bu kapi bakar.
#
# CLAUDE.md 9:
#   - Kapsam ADLARA degil AGACA bagli: agactaki HER .cs, proje/klasor adi
#     yazilmadan. Yarin eklenen bir proje kendiliginden kapsama girer.
#   - Kurulu olmayan bir kapi GECTI sayilmaz.
#   - Hicbir dosya bulunamamasi "TEMIZ" degildir: kapi inert demektir.
#
# CLAUDE.md 3: sinir asilirsa dosya ADI ve SATIR SAYISI yazilir - "bir dosya
# buyuk" demek kullaniciya hicbir sey soylemez.

set -uo pipefail

KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Sinir BUGUNUN OLCUMUYLE secildi: 27.08.2026'da agactaki en buyuk dosya
# 536 satir. 600, bugunku hicbir dosyayi kirmadan v1'in hastaligini
# yakalayacak kadar dar. Belgeden degil, calistirmadan gelen bir sayi.
SINIR="${KAPI_BOYUT_SINIRI:-600}"

echo "== KAPI: dosya boyutu (sinir: $SINIR satir) =="

mapfile -t DOSYALAR < <(find "$KOK" -name '*.cs' \
  -not -path '*/bin/*' -not -path '*/obj/*' -not -path '*/.git/*' \
  | sort)

if [ "${#DOSYALAR[@]}" -eq 0 ]; then
  echo "KAPI KIRIK: agacta hic .cs dosyasi yok. Kapinin bakacagi bir sey yok."
  exit 1
fi

KIRIK=0
ENBUYUK=0
ENBUYUK_AD=""

for DOSYA in "${DOSYALAR[@]}"; do
  SATIR="$(wc -l < "$DOSYA" | tr -d ' ')"
  GORECELI="${DOSYA#"$KOK"/}"

  if [ "$SATIR" -gt "$ENBUYUK" ]; then
    ENBUYUK="$SATIR"
    ENBUYUK_AD="$GORECELI"
  fi

  if [ "$SATIR" -gt "$SINIR" ]; then
    echo "   ASILDI: $GORECELI  ->  $SATIR satir (sinir $SINIR)"
    KIRIK=1
  fi
done

echo "   bakilan dosya: ${#DOSYALAR[@]}   en buyuk: $ENBUYUK_AD ($ENBUYUK satir)"

if [ "$KIRIK" -ne 0 ]; then
  echo "KAPI KIRIK: sinirin ustunde dosya var."
  echo "  Bu bir bicim kurali degil: v1'de bolunemeyen dosya BIR GUNDE"
  echo "  olusmadi, kimse bakmadigi icin buyudu. Dosyayi konusuna gore bol."
  echo "== KAPI KIRIK =="
  exit 1
fi

echo "== KAPI TEMIZ =="

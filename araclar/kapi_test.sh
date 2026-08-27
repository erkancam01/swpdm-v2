#!/usr/bin/env bash
#
# KAPI: testler
#
# Agactaki HER test projesini kosar.
#
# CLAUDE.md 9:
#   - Kapsam ADLARA degil AGACA bagli: test projesi ICERIKTEN bulunur
#     (Microsoft.NET.Test.Sdk paketine bakilir), ad kalibina degil. Yarin
#     "Denemeler" adinda bir proje eklenirse yine kapsama girer.
#   - Kurulu olmayan bir kapi GECTI sayilmaz: dotnet yoksa hata verir.
#   - Sifir test "gecti" DEGILDIR: hic test kosmadiysa kapi KIRIK der.
#     v1'de bir kapi yazildigi anda inert'ti ve hep "TEMIZ" diyordu.

set -uo pipefail

KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

echo "== KAPI: testler =="

if ! command -v dotnet > /dev/null 2>&1; then
  echo "KAPI KURULU DEGIL: 'dotnet' bulunamadi."
  echo "  Ubuntu: sudo apt-get install -y dotnet-sdk-8.0"
  echo "  Kurulu olmayan bir kapi GECTI sayilmaz."
  exit 1
fi

mapfile -t TESTLER < <(grep -rl "Microsoft.NET.Test.Sdk" "$KOK" \
  --include="*.csproj" 2>/dev/null | sort)

if [ "${#TESTLER[@]}" -eq 0 ]; then
  echo "KAPI KIRIK: agacta hic test projesi yok. Kapinin kosacagi bir sey yok."
  exit 1
fi

echo "   agacta bulunan test projesi: ${#TESTLER[@]}"
KIRIK=0
TOPLAM_GECEN=0

for PROJE in "${TESTLER[@]}"; do
  GORECELI="${PROJE#"$KOK"/}"
  echo "   -> $GORECELI"
  CIKTI="$(dotnet test "$PROJE" --nologo -v q 2>&1)"
  DURUM=$?
  OZET="$(echo "$CIKTI" | grep -E "^(Passed!|Failed!)" | tail -1)"
  [ -n "$OZET" ] && echo "      $OZET"

  if [ "$DURUM" -ne 0 ]; then
    echo "$CIKTI" | grep -E "error|Assert|\[FAIL\]" | head -20 | sed 's/^/        /'
    KIRIK=1
    continue
  fi

  GECEN="$(echo "$OZET" | grep -oE "Passed:[[:space:]]+[0-9]+" | grep -oE "[0-9]+" | head -1)"
  TOPLAM_GECEN=$(( TOPLAM_GECEN + ${GECEN:-0} ))
done

# Sifir test "gecti" degildir.
if [ "$KIRIK" -eq 0 ] && [ "$TOPLAM_GECEN" -eq 0 ]; then
  echo "KAPI KIRIK: hicbir test kosmadi. Sifir test GECTI sayilmaz."
  exit 1
fi

if [ "$KIRIK" -ne 0 ]; then
  echo "== KAPI KIRIK =="
  exit 1
fi

echo "   toplam gecen: $TOPLAM_GECEN"
echo "== KAPI TEMIZ =="

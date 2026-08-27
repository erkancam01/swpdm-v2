#!/usr/bin/env bash
#
# KAPI: derleme
#
# Agactaki HER .csproj'u uyarilar hata sayilarak derler.
#
# CLAUDE.md 9 geregi:
#   - Kapsam ADLARA degil AGACA bagli. Yarin eklenen bir proje kendiliginden
#     kapsama girer; v1'de bir kapi iki proje ADINA bakiyordu ve ucuncu bir
#     proje eklenseydi SESSIZCE atlanirdi.
#   - Kurulu olmayan bir kapi "gecti" sayilmaz: dotnet yoksa ATLAMAZ, hata verir.
#   - -warnaserror burada da veriliyor; kapinin gucu tek tek csproj'larin
#     dogru yazilmis olmasina BAGLI OLMAMALI.
#
# Yakalayabildigi: derleyicinin gordugu her sey.
# Yakalayamadigi: yalnizca CALISTIRINCA gorunen hatalar -> kapi_calistir.sh

set -uo pipefail

KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

echo "== KAPI: derleme =="
echo "   kok: $KOK"

if ! command -v dotnet > /dev/null 2>&1; then
  echo "KAPI KURULU DEGIL: 'dotnet' bulunamadi."
  echo "  Ubuntu: sudo apt-get install -y dotnet-sdk-8.0"
  echo "  Kurulu olmayan bir kapi GECTI sayilmaz."
  exit 1
fi
echo "   dotnet: $(dotnet --version)"

mapfile -t PROJELER < <(find "$KOK" -name "*.csproj" \
  -not -path "*/bin/*" -not -path "*/obj/*" -not -path "*/.git/*" | sort)

if [ "${#PROJELER[@]}" -eq 0 ]; then
  echo "KAPI KIRIK: agacta hic .csproj bulunamadi. Kapinin bakacagi bir sey yok."
  exit 1
fi

echo "   agacta bulunan proje: ${#PROJELER[@]}"
KIRIK=0
for PROJE in "${PROJELER[@]}"; do
  GORECELI="${PROJE#"$KOK"/}"
  printf '   -> %-44s ' "$GORECELI"
  if CIKTI="$(dotnet build "$PROJE" -v q --nologo -warnaserror 2>&1)"; then
    echo "TEMIZ"
  else
    echo "KIRIK"
    echo "$CIKTI" | grep -E "error|warning" | head -20 | sed 's/^/        /'
    KIRIK=1
  fi
done

if [ "$KIRIK" -ne 0 ]; then
  echo "== KAPI KIRIK =="
  exit 1
fi

echo "== KAPI TEMIZ =="

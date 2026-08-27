#!/usr/bin/env bash
#
# Butun kapilari sirayla kosar. CI de ayni betikleri kosuyor; burada ikinci
# bir kopya YOK (CLAUDE.md 8: ayni mantigin ikinci kopyasini yazma).
#
# Kullanim:  araclar/kapilar.sh [--kur]
#   --kur : calistirma kapisinin eksik araclarini kurmasina izin ver

set -uo pipefail
KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
KIRIK=0

"$KOK/araclar/kapi_derleme.sh"        || KIRIK=1
echo
"$KOK/araclar/kapi_test.sh"           || KIRIK=1
echo
"$KOK/araclar/kapi_calistir.sh" "$@"  || KIRIK=1

echo
if [ "$KIRIK" -ne 0 ]; then
  echo "########## KAPILAR: KIRIK ##########"
  exit 1
fi
echo "########## KAPILAR: TEMIZ ##########"

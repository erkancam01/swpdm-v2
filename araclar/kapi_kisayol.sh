#!/usr/bin/env bash
#
# KAPI: kisayol - menuye KAYDEDILEN tus gecerli mi.
#
# NEDEN VAR - BEDELI ODENDI (31.08.2026): "Aç" islemi kisayol olarak
# Keys.Enter dedi. Tek basina Enter GECERLI BIR MENU KISAYOLU DEGIL;
# ToolStripMenuItem.ShortcutKeys'e yazilinca InvalidEnumArgumentException
# atiyor ve menu ogesi KURUCUDA uretildigi icin istisna ACILISTA cikiyor:
# uygulama HIC ACILMADI. Derleme "0 uyari 0 hata" dedi; goren tek sey
# calistirma kapisi oldu - yani hatanin bedeli tam bir Wine kosusu.
#
# Bugun agac TEMIZ (01.09.2026 denetimi tek tek baktir): modifiyesiz tus
# kullanan islem yok. Ama bunu koruyan bir sey de yoktu; bu kapi o yuzden
# var - ONARIM degil, KORUMA.
#
# KURAL: Kisayol ya modifiyeli (Ctrl/Shift/Alt) olacak ya da su beyaz
# listeden olacak - bunlar WinForms'un tek basina kabul ettigi tuslar:
#   F1..F24 · Delete · Insert · Back · None
# Enter, Escape, Space, Tab ve harf/rakam tuslari REDDEDILIR.
#
# YazilanTus BU KAPININ DISINDA, bilerek: o zaten KAYDEDILMEYEN tus -
# Enter'in orada durmasi kuralin ta kendisi.
#
# Kapsam ADLARA degil AGACA bagli (CLAUDE.md 9).

set -uo pipefail

KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "== KAPI: kisayol (menuye kaydedilen tus gecerli mi) =="

SORUN=0
BAKILAN=0

while IFS= read -r dosya; do
  grep -q ": IAgacIslemi" "$dosya" || continue

  # "public Keys Kisayol => Keys.Control | Keys.Shift | Keys.N;"
  while IFS= read -r ifade; do
    BAKILAN=$((BAKILAN + 1))

    # Modifiyeli ise sorun yok.
    case "$ifade" in
      *Keys.Control*|*Keys.Shift*|*Keys.Alt*) continue ;;
    esac

    # Tek tus: yalnizca beyaz liste.
    TUS="$(echo "$ifade" | grep -oE 'Keys\.[A-Za-z0-9]+' | head -1 | cut -d. -f2)"
    case "$TUS" in
      None|Delete|Insert|Back|F1|F2|F3|F4|F5|F6|F7|F8|F9|F10|F11|F12) continue ;;
    esac

    echo "   GECERSIZ KISAYOL: Keys.$TUS  <- ${dosya#"$KOK/"}"
    echo "      tek basina bu tus ShortcutKeys'e yazilamaz; uygulama ACILMAZ."
    SORUN=$((SORUN + 1))
  done < <(grep -hoE 'Keys +Kisayol *=> *[^;]+' "$dosya")
done < <(find "$KOK/src" -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*" | sort)

echo "   bakilan kisayol: $BAKILAN"

if [ "$SORUN" -ne 0 ]; then
  echo "== KAPI KIRIK =="
  exit 1
fi

echo "== KAPI TEMIZ =="

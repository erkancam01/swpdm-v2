#!/usr/bin/env bash
#
# PAKET: Erkan'in deneyebilecegi calistirilabilir zip.
#
# Erkan: "her yaptigin calismanin sonunda zip olarak ver, bende deneyeyim."
#
# Cerceve bagimli yayin (win-x64): ~120 KB. Kendi kendine yeten surum 67 MB
# ve gereksiz - hedef makinede .NET 8 Desktop Runtime zaten var.
#
# Surum notu SURUM-NOTU.txt'ten alinir; basina commit ve tarih DAMGALANIR ki
# eski bir not yeni bir pakete yapissa bile fark edilsin.

set -uo pipefail

KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

echo "== PAKET =="

for ARAC in dotnet zip; do
  if ! command -v "$ARAC" > /dev/null 2>&1; then
    echo "PAKET URETILEMEDI: '$ARAC' bulunamadi."
    exit 1
  fi
done

mapfile -t ADAYLAR < <(grep -rl "<OutputType>WinExe</OutputType>" "$KOK" --include="*.csproj" 2>/dev/null | sort)
if [ "${#ADAYLAR[@]}" -ne 1 ]; then
  echo "PAKET URETILEMEDI: tam olarak bir WinExe projesi bekleniyordu, ${#ADAYLAR[@]} bulundu."
  exit 1
fi

COMMIT="$(git -C "$KOK" rev-parse --short HEAD 2>/dev/null || echo bilinmiyor)"
KIRLI=""
git -C "$KOK" diff --quiet 2>/dev/null || KIRLI=" (calisma agacinda kaydedilmemis degisiklik var)"
TARIH="$(date '+%d.%m.%Y %H:%M')"

CIKTI="$KOK/.kapi/paket"
ICERI="$CIKTI/swpdm"
rm -rf "$CIKTI"; mkdir -p "$ICERI"

echo "   yayinlaniyor (win-x64, cerceve bagimli)..."
if ! dotnet publish "${ADAYLAR[0]}" -c Release -r win-x64 --self-contained false \
     -p:UseAppHost=true -o "$ICERI" > "$CIKTI/yayin.log" 2>&1; then
  echo "PAKET URETILEMEDI: yayin basarisiz."
  grep -E "error" "$CIKTI/yayin.log" | head -10 | sed 's/^/     /'
  exit 1
fi

# OKU-BENI: uretilen damga + elle yazilan surum notu.
# CRLF sart - Not Defteri'nde tek satir gorunmesin (CLAUDE.md 4 kalibinin
# zararsiz akrabasi).
{
  printf 'SW PDM v2 - Dosya Yoneticisi (referans korumali)\r\n'
  printf 'Surum: %s%s\r\n' "$COMMIT" "$KIRLI"
  printf 'Paketleme: %s\r\n' "$TARIH"
  printf '\r\n'
  printf 'CALISTIRMAK ICIN\r\n'
  printf '  SwPdm.exe\r\n'
  printf '\r\n'
  printf '  .NET 8 Desktop Runtime gerekiyor:\r\n'
  printf '  https://dotnet.microsoft.com/download/dotnet/8.0\r\n'
  printf '\r\n'
  if [ -f "$KOK/SURUM-NOTU.txt" ]; then
    sed 's/$/\r/' "$KOK/SURUM-NOTU.txt"
  else
    printf 'SURUM-NOTU.txt yok - bu pakette neyin calistigi YAZILMAMIS.\r\n'
  fi
} > "$ICERI/OKU-BENI.txt"

# KULLANIM KILAVUZU DA PAKETE GIRER: "bu dugme ne yapiyor" sorusunun cevabi
# zip'in icinde olsun. CRLF sart - Not Defteri LF'li dosyayi tek satir
# gosteriyor (CLAUDE.md 4'teki .bat tuzaginin zararsiz akrabasi).
if [ -f "$KOK/OZELLIKLER.md" ]; then
  sed 's/$/\r/' "$KOK/OZELLIKLER.md" > "$ICERI/OZELLIKLER.txt"
else
  echo "   UYARI: OZELLIKLER.md yok - kilavuz pakete girmedi."
fi

ZIP="$CIKTI/swpdm-$COMMIT.zip"
( cd "$CIKTI" && zip -qr "$ZIP" swpdm ) || { echo "PAKET URETILEMEDI: zip basarisiz."; exit 1; }

echo "   $ZIP  ($(stat -c%s "$ZIP") bayt)"
echo "== PAKET HAZIR =="

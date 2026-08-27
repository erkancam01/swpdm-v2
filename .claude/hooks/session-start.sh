#!/usr/bin/env bash
#
# Claude Code (web) oturum baslangici.
#
# NEDEN VAR: kap gecici. Her yeni oturum .NET SDK'siz aciliyor ve o zaman
# hicbir sey OLCULEMIYOR - v1'in en pahali kalemi tam olarak buydu.
# Bu betik kapilarin ihtiyaci olan her seyi kurar.
#
# Idempotent: kurulu olani atlar, tekrar tekrar kosulabilir.

set -uo pipefail

# Yalnizca uzak (web) ortamda kos; gelistiricinin kendi makinesine dokunma.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

SUDO=""
[ "$(id -u)" -ne 0 ] && SUDO="sudo"

EKSIK=()
command -v dotnet   > /dev/null 2>&1 || EKSIK+=("dotnet-sdk-8.0")
command -v Xvfb     > /dev/null 2>&1 || EKSIK+=("xvfb")
command -v xwininfo > /dev/null 2>&1 || EKSIK+=("x11-utils")
command -v import   > /dev/null 2>&1 || EKSIK+=("imagemagick")
command -v zip      > /dev/null 2>&1 || EKSIK+=("zip")          # araclar/paket.sh
if [ ! -x /usr/lib/wine/wine64 ] && ! command -v wine64 > /dev/null 2>&1; then
  EKSIK+=("wine64")
fi

if [ "${#EKSIK[@]}" -eq 0 ]; then
  echo "oturum: gerekli her sey zaten kurulu ($(dotnet --version))"
  exit 0
fi

echo "oturum: kuruluyor -> ${EKSIK[*]}"
$SUDO apt-get update -o Acquire::Retries=2 > /dev/null 2>&1 || true

if ! DEBIAN_FRONTEND=noninteractive $SUDO apt-get install -y --no-install-recommends "${EKSIK[@]}" > /tmp/oturum-kurulum.log 2>&1; then
  echo "oturum: KURULUM BASARISIZ. Ayrinti: /tmp/oturum-kurulum.log"
  tail -5 /tmp/oturum-kurulum.log
  # Sessiz basarisizlik YASAK (CLAUDE.md 3): sebep yaziliyor, ama oturum
  # yine de acilsin - kapilar zaten "kurulu degil" deyip hata verecek.
  exit 0
fi

echo "oturum: hazir ($(dotnet --version 2>/dev/null || echo 'dotnet YOK'))"

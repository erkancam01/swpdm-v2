#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Windows .bat kapisi - cmd.exe'nin SESSIZ olum sebepleri.

NEDEN BU BETIK VAR:
Bu depoda .bat dosyalari IKI KEZ sessizce oldu ve ikisinde de kullanicinin
gordugu sey ayniydi: "pencere aciliyor ve hemen kapaniyor, hicbir sey
yazmiyor". Hata mesaji YOK, gunluk YOK, cikis kodu bile gorulemiyor.

  1) LF satir sonu. cmd.exe LF'e dusmus bir .bat'i yarida birakiyor.
     (.gitattributes'ta "*.bat -text" ve paketle.py'de newline="" bu yuzden.)

  2) Blok icinde KACISSIZ parantez. Bir "if ... ( ... )" ya da "for ... ( ... )"
     blogunun ICINDE gecen ( ve ) karakterleri, tirnak icinde olsalar bile
     cmd'nin blok ayristiricisini yaniltabiliyor. En sik hali su:

         if ... (
             echo    derle.bat "C:\\...\\SOLIDWORKS (2)"
         )

     Dogrusu ^( ve ^) yazmak - ya da hic blok kullanmamak.

BU KAPI DERLEYICI DEGIL: cmd.exe'nin ayristiricisini birebir taklit etmiyor
ve etmeye de calismiyor. Yaptigi sey, iki bilinen olum sebebini yasaklamak.
Kacis eklemenin maliyeti sifir; sessizce olen bir betigin teshisi ise saatler.

YANLIS ALARM RISKI: blok DISINDA parantez serbest - orada zararsiz ve
sik gerekiyor (for ... in (...) gibi). Yalnizca blok ICI denetleniyor.
"""

import io
import os
import re
import sys

KOK = os.path.dirname(os.path.abspath(__file__))
DEPO = os.path.join(KOK, "..")

# Blok ACAN satir: sonu "(" ile bitiyor VE oncesinde bosluk var:
#     if ... (        ) else (        for ... do (
#
# Bosluk sarti onemli - kapinin ilk surumu bunu aramiyordu ve "echo(" satirini
# blok acici sandi. Sonuc: derinlik bir daha sifira donmedi ve dosyanin geri
# kalanindaki HER parantez yanlis alarm verdi. Yanlis alarm veren bir kapi
# kapi olmaktan cikar - bu depoda ayni ders iki kez odendi.
BLOK_AC = re.compile(r"(?:^|\s)\($")

# Blok KAPATAN satir: yalnizca ")" ya da ") else ("
BLOK_KAPA = re.compile(r"^\s*\)")

# Kacisli parantezler - denetimden once temizleniyor.
KACISLI = re.compile(r"\^[()]")


def bat_dosyalari():
    for ad in sorted(os.listdir(DEPO)):
        if ad.lower().endswith(".bat"):
            yield os.path.join(DEPO, ad)


def denetle(yol):
    """(satir_sonu_bulgulari, parantez_bulgulari)"""
    ham = io.open(yol, "rb").read()

    satir_sonu = []
    crlf = ham.count(b"\r\n")
    yalniz_lf = ham.count(b"\n") - crlf
    if yalniz_lf > 0:
        satir_sonu.append(
            "%d satir LF ile bitiyor (CRLF olmali). cmd.exe boyle bir dosyayi "
            "yarida birakiyor." % yalniz_lf)

    parantez = []
    derinlik = 0

    metin = ham.decode("utf-8", "replace")
    for no, satir in enumerate(metin.replace("\r\n", "\n").split("\n"), 1):
        sade = satir.rstrip()
        if not sade:
            continue

        kapatiyor = bool(BLOK_KAPA.match(sade))
        aciyor = bool(BLOK_AC.search(sade))

        # Blok sinirlarini olusturan parantezleri sayimdan cikar.
        govde = sade
        if kapatiyor:
            govde = BLOK_KAPA.sub("", govde, count=1)
        if aciyor:
            govde = govde[:-1]

        # Kalan parantezler govdenin ICINDE demektir. Blok icindeysek bunlar
        # kacisli olmali.
        govde = KACISLI.sub("", govde)
        if derinlik > 0 and ("(" in govde or ")" in govde):
            parantez.append((no, sade.strip()))

        if kapatiyor:
            derinlik = max(0, derinlik - 1)
        if aciyor:
            derinlik += 1

    return satir_sonu, parantez


def main():
    bulgu_var = False
    sayi = 0

    for yol in bat_dosyalari():
        sayi += 1
        ad = os.path.basename(yol)
        satir_sonu, parantez = denetle(yol)

        for mesaj in satir_sonu:
            bulgu_var = True
            print("  %s: %s" % (ad, mesaj))

        for no, satir in parantez:
            bulgu_var = True
            print("  %s:%d  blok icinde KACISSIZ parantez" % (ad, no))
            print("      %s" % satir[:100])
            print("      Cozum: ( ve ) yerine ^( ve ^) yazin, ya da blok yerine goto kullanin.")

    if bulgu_var:
        print()
        print("BAT KAPISI: bulgu var. Bu hatalar cmd.exe'de SESSIZ olume yol aciyor -")
        print("pencere acilir, hicbir sey yazmadan kapanir.")
        return 1

    print("Bat kapisi: TEMIZ (%d dosya)" % sayi)
    return 0


if __name__ == "__main__":
    sys.exit(main())

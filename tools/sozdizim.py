#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""C# SOZDIZIMI denetimi - gercek bir ayristiriciyla (tree-sitter).

NEDEN BU BETIK VAR:
Bu depoda SwPdm.Masaustu Linux'ta DERLENEMIYOR (SOLIDWORKS
interop DLL'leri yok). Yani CI o dosyalari hic gormuyor ve bir sozdizimi
hatasi ancak Erkan'in makinesinde, paket indirildikten SONRA ortaya cikiyor.
csdenge.py bu boslugun bir kismini kapatiyor ama o bir SEZGISEL: suslu
parantez dengesi, cift bildirim, bilinen tuzaklar. Yapinin kendisini
dogrulamiyor.

Bu betik gercek bir C# ayristiricisi kullaniyor. Yakaladigi sey dar ama
KESIN: dosya gecerli C# olarak ayristirilabiliyor mu. Tur denetimi,
ad cozumlemesi, eksik using YOK - onlar icin derleyici gerekir.

DOGDUGU AN: 26.08.2026'da PdmPaneli.cs'ten 2546 satir silindi (revizyon
arayuzu eklentiye tasindi). Silme sabit-nokta erisilebilirlik analiziyle
yapildi ve csdenge TEMIZ dedi - ama "TEMIZ" burada yalnizca "parantezler
dengeli" demekti. Yarim kesilmis bir metot govdesi ya da bozulmus bir
bildirim o denetimden GECEBILIRDI. Bu betik tam o riski kapatiyor.

BAGIMLILIK:
    pip install tree_sitter tree_sitter_c_sharp
Kurulu degilse betik SESSIZCE GECMIYOR - hata koduyla cikiyor ve kurulum
komutunu yaziyor. "Atlandi = gecti" bu depoda tam olarak yasaklanan sey.
"""

import glob
import io
import os
import sys

KOK = os.path.dirname(os.path.abspath(__file__))

TARANANLAR = [
    os.path.join(KOK, "..", "src"),
    os.path.join(KOK, "..", "tests"),
]


def ayristirici():
    try:
        from tree_sitter import Language, Parser
        import tree_sitter_c_sharp as tscs
    except ImportError:
        print("C# sozdizimi denetimi: AYRISTIRICI YOK")
        print()
        print("  pip install tree_sitter tree_sitter_c_sharp")
        print()
        print("Denetim ATLANMADI, BASARISIZ sayildi: kurulu olmayan bir kapiyi")
        print("'gecti' saymak, kapinin kendisini anlamsizlastirir.")
        sys.exit(2)

    return Parser(Language(tscs.language()))


def hatalar(kok, kaynak):
    """ERROR ve MISSING dugumleri (satir, tur, baglam)."""
    bulunan = []
    yigin = [kok]
    while yigin:
        n = yigin.pop()
        if n.type == "ERROR" or n.is_missing:
            metin = kaynak[n.start_byte:n.start_byte + 90]
            bulunan.append((
                n.start_point[0] + 1,
                "MISSING" if n.is_missing else "ERROR",
                metin.decode("utf-8", "replace").replace("\n", " ").strip(),
            ))
        yigin.extend(n.children)
    return bulunan


def main():
    p = ayristirici()

    dosyalar = []
    for kok in TARANANLAR:
        if os.path.isdir(kok):
            dosyalar.extend(glob.glob(os.path.join(kok, "**", "*.cs"), recursive=True))

    toplam = 0
    for yol in sorted(dosyalar):
        ham = io.open(yol, "rb").read()
        h = hatalar(p.parse(ham).root_node, ham)
        if not h:
            continue

        print("HATA  " + os.path.relpath(yol, os.path.join(KOK, "..")))
        for satir, tur, metin in sorted(h)[:6]:
            print("   satir %-6d %-8s %s" % (satir, tur, metin))
        if len(h) > 6:
            print("   ... ve %d tane daha" % (len(h) - 6))
        toplam += len(h)

    if toplam:
        print()
        print("C# sozdizimi denetimi: %d HATA (%d dosya tarandi)" % (toplam, len(dosyalar)))
        return 1

    print("C# sozdizimi denetimi: TEMIZ (%d dosya)" % len(dosyalar))
    return 0


if __name__ == "__main__":
    sys.exit(main())

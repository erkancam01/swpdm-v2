#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""SOLIDWORKS interop ad cakismasi denetimi (CS0104).

NEDEN BU BETIK VAR:
SolidWorks.Interop.sldworks, System ile AYNI ADLARI tasiyan tipler
tanimliyor - en sik carpisani Environment. Bir dosya hem "using System"
hem "using SolidWorks.Interop.sldworks" iceriyorsa, ciplak "Environment."
yazmak derleyiciyi ikilemde birakiyor: CS0104.

Bu hata bu projede AYLARCA tekrarladi. Sebebi yapisal: CI Linux'ta kosuyor
ve SwPdm.Masaustu derlenmiyor (SOLIDWORKS interop DLL'leri yok). Yani
derleyici bu dosyalari hic gormuyor ve hata ancak paket indirildikten SONRA,
kullanicinin makinesinde ortaya cikiyor. Kullanici her seferinde elle
duzeltiyordu.

Derleyemedigimiz icin STATIK denetliyoruz: interop iceren dosyalarda
cakisan adlarin ciplak kullanimini ariyoruz. Dosyada o ad icin takma ad
(using X = System.X;) varsa ya da tam nitelenmisse (System.X.) sorun yok.

AD LISTESI BILEREK GENIS: fazladan bir takma ad zararsiz, eksik kalan bir ad
KIRIK DERLEME demek.

IKINCI HATA SINIFI - COM'da yansima (bkz. COM_YANSIMASI):
Bir COM sarmalayicisinda nesne.GetType() System.__ComObject donduruyor ve o
tipte arayuzun HICBIR uyesi yok. Yani ".GetType().GetMethod(...)" derlense
bile HER ZAMAN null verir. Bu, derlemeyi kirmadigi icin CI'dan da elle
gozden de kaciyor; sonucu sessizce yanlis calisan kod ("uye bulunamadi"
raporlayip hic denemeyen bir dogrulama kapisi). Bu hata bu projede iki tur
ust uste yasandi. Dogrusu arayuz tipine yansimak: typeof(IX).GetMethod(...)
ya da typeof(T) - cagrinin hedefi yine COM nesnesi.
"""

import io
import os
import re
import sys

KOK = os.path.dirname(os.path.abspath(__file__))

# SOLIDWORKS interop kullanan HER proje. Bugun tek proje var; yenisi
# eklenirse LISTEYE YAZILMALI - unutmak, denetimi o proje icin sessizce
# kapatmak demek.
TARANANLAR = [
    # v2: proje ADLARINA bagli DEGIL - src/ altinin tamami taraniyor.
    # v1'de burada iki proje adi yaziliydi ve yeni bir proje eklendiginde
    # kapi onu SESSIZCE atlardi; kapinin kapsami adlara degil AGACA bagli.
    os.path.join(KOK, "..", "src"),
]

# System* ile SolidWorks.Interop.sldworks arasinda cakisan (ya da cakisma
# ihtimali olan) tip adlari.
CAKISANLAR = [
    "Environment", "View", "Application", "Color", "Point", "Component",
    "Attribute", "Feature", "Dimension", "Body", "Sketch", "Configuration",
    "Annotation", "Table", "Layer", "Note", "Curve", "Surface",
]

# Adin gercek System ad alani - oneri metni DOGRU olsun diye.
# "using Color = System.Color;" yazan bir oneri, uyariyi okuyani ikinci bir
# derleme hatasina goturur (Color System.Drawing'de). Listede olmayan adlar
# icin duz "System." kullaniliyor: onlarin System karsiligi belirsiz, oneri
# de zaten "tam nitele" demekten ibaret.
AD_ALANI = {
    "Application": "System.Windows.Forms",
    "View": "System.Windows.Forms",
    "Color": "System.Drawing",
    "Point": "System.Drawing",
    "Component": "System.ComponentModel",
    "Configuration": "System.Configuration",
}


def tam_ad(ad):
    return AD_ALANI.get(ad, "System") + "." + ad


def kod(kaynak):
    """Yorumlari ve dizeleri atar - yalnizca gercek kod kalir."""
    cikti = []
    i, n = 0, len(kaynak)
    while i < n:
        c = kaynak[i]
        if c == "/" and i + 1 < n and kaynak[i + 1] == "/":
            while i < n and kaynak[i] != "\n":
                i += 1
        elif c == "/" and i + 1 < n and kaynak[i + 1] == "*":
            i += 2
            while i + 1 < n and not (kaynak[i] == "*" and kaynak[i + 1] == "/"):
                if kaynak[i] == "\n":
                    cikti.append("\n")
                i += 1
            i += 2
        elif c == "@" and i + 1 < n and kaynak[i + 1] == '"':
            i += 2
            while i < n:
                if kaynak[i] == '"':
                    if i + 1 < n and kaynak[i + 1] == '"':
                        i += 2
                        continue
                    i += 1
                    break
                if kaynak[i] == "\n":
                    cikti.append("\n")
                i += 1
        elif c == '"':
            i += 1
            while i < n:
                if kaynak[i] == "\\":
                    i += 2
                    continue
                if kaynak[i] == '"':
                    i += 1
                    break
                i += 1
        else:
            cikti.append(c)
            i += 1
    return "".join(cikti)


# Interop DISINDAKI cakismalar: iki System ad alani ayni adi tasiyor.
# Bicim: (gerekli using'ler, cakisan ad, onerilen tam ad)
#
# Timer: System.Threading.Timer ile System.Windows.Forms.Timer. AnaPencere
# ikisini de iceriyor ve arama kutusunun gecikme zamanlayicisi eklenirken bu
# tuzaga dusuldu. Interop kadar gorunmez bir hata: CI Linux'ta WinForms
# projesini derlemedigi icin yine ancak kullanicinin makinesinde cikardi.
IKILI_CAKISANLAR = [
    (["System.Threading", "System.Windows.Forms"], "Timer", "System.Windows.Forms.Timer"),
]

# COM nesnesi uzerinde CALISMA-ZAMANI tipine yansima. Bunlar derlenir ama
# calisma aninda HER ZAMAN bos doner (System.__ComObject'te arayuz uyesi yok).
#
# NEDEN SADECE BU UYELER: yakalanmasi gereken sey "GetType()'in donusu
# uzerinde yansima yapmak". ".GetType().Name" ve ".GetType().FullName" bu
# kalibin disinda ve mesru - olculdu: depoda 12 yerde geciyor ve hepsi
# Exception uzerinde. Yani kural AYIRT EDIYOR; ayirt etmeyen bir kural
# eklenmezdi (yanlis alarm veren bir CI kapisi, kapi olmaktan cikar).
COM_YANSIMASI = [
    "GetMethod", "GetMethods", "GetProperty", "GetProperties",
    "GetField", "GetFields", "GetEvent", "GetEvents", "GetMember", "GetMembers",
    "InvokeMember",
]


def ad_bul(temiz, ad):
    """
    Adin BAGIMSIZ her gecisi (nokta ile nitelenmemis).

    NEDEN BU KADAR GENIS: ilk surum yalnizca "Ad." ve "new Ad(" ariyordu ve
    tam da yakalamasi gereken iki kullanimi KACIRDI - alan tanimi
    ("private Timer _x;") ve parantezsiz nesne baslatici ("new Timer { ... }").
    Denetim, kendisini sinadigimda "TEMIZ" dedi; oysa dosyada ciplak Timer
    vardi. Ciplak bir ad nerede gecerse gecsin belirsiz - bagimsiz gecisi
    aramak hem daha basit hem dogru.
    """
    satirlar = []
    for m in re.finditer(r"(?<![\w.])" + ad + r"(?![\w])", temiz):
        satirlar.append(temiz.count("\n", 0, m.start()) + 1)
    return satirlar


def takma_ad_var(temiz, ad):
    return re.search(r"using\s+" + ad + r"\s*=\s*[\w.]+\s*;", temiz) is not None


def denetle(yol):
    ham = io.open(yol, encoding="utf-8").read()
    temiz = kod(ham)
    bulgular = []
    yansimalar = []

    # TEMIZ kaynaga bakiliyor, HAM'a degil. Bir YORUMDA "using
    # SolidWorks.Interop" gecmesi o dosyanin interop'u ice aktardigi anlamina
    # gelmez. Ilk surum ham metne bakiyordu ve bir yorum yuzunden interop
    # kullanmayan 5000 satirlik bir dosyayi taramaya alip 150'den fazla
    # SAHTE uyari uretti. Ayni sinif hata (ham vs temiz) csdenge.py'de de
    # yasandi - denetim araclarinin kendisi de olculmeli.
    if "using SolidWorks.Interop" in temiz:
        for uye in COM_YANSIMASI:
            for m in re.finditer(r"\.\s*GetType\s*\(\s*\)\s*\.\s*" + uye + r"\s*\(", temiz):
                satir = temiz.count("\n", 0, m.start()) + 1
                yansimalar.append((satir, uye))

        for ad in CAKISANLAR:
            # Takma ad varsa dosya guvende.
            if takma_ad_var(temiz, ad):
                continue

            # Ciplak kullanim: onunde nokta/harf OLMAYAN "Ad." dizilimi.
            # "System.Environment." ve "x.Environment." elenir.
            for m in re.finditer(r"(?<![\w.])" + ad + r"\s*\.", temiz):
                satir = temiz.count("\n", 0, m.start()) + 1
                bulgular.append((satir, ad, tam_ad(ad)))

    for gerekli, ad, tam in IKILI_CAKISANLAR:
        if not all(("using " + g + ";") in temiz for g in gerekli):
            continue
        if takma_ad_var(temiz, ad):
            continue

        for satir in ad_bul(temiz, ad):
            bulgular.append((satir, ad, tam))

    return bulgular, yansimalar


def main():
    hata = 0
    for taranan in TARANANLAR:
        if not os.path.isdir(taranan):
            continue

        for dizin, _, dosyalar in os.walk(taranan):
            for d in sorted(dosyalar):
                if not d.endswith(".cs"):
                    continue

                yol = os.path.join(dizin, d)
                bulgular, yansimalar = denetle(yol)
                goreli = os.path.relpath(yol, os.path.join(KOK, ".."))

                for satir, ad, tam in bulgular:
                    print("CS0104 RISKI: %s:%d  ciplak '%s' - "
                          "dosyaya 'using %s = %s;' ekleyin"
                          % (goreli, satir, ad, ad, tam))
                    hata += 1

                for satir, uye in yansimalar:
                    print("COM YANSIMASI: %s:%d  .GetType().%s(...) - COM'da "
                          "GetType() System.__ComObject verir, bu cagri HER "
                          "ZAMAN bos doner. Arayuz tipine yansiyin: "
                          "typeof(IArayuz) ya da jenerik typeof(T)."
                          % (goreli, satir, uye))
                    hata += 1

    if hata:
        print("\n%d bulgu. Cakisan tipler takma adla netlestirilmeli; "
              "COM yansimalari arayuz tipine cevrilmeli." % hata)
        return 1

    print("Interop denetimi (CS0104 + COM yansimasi): TEMIZ")
    return 0


if __name__ == "__main__":
    sys.exit(main())

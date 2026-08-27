#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""C# kaba yapi denetimi: denge, cift yerel tanim, cift bildirim, CS0111, tekrar.

NEDEN BU BETIK VAR:
CI Linux'ta kosuyor ve SwPdm.Masaustu DERLENMIYOR - SOLIDWORKS interop
DLL'leri yok. Yani o projedeki sozdizimi ve bildirim hatalari ancak paket
indirildikten SONRA, Erkan'in makinesinde ortaya cikiyor.
interop_denetim.py bu boslugun bir parcasini (CS0104) kapatiyor; bu betik
geri kalanini kapatiyor.

NEDEN DEPOYA ALINDI (yasanmis hata):
Bu betigin ilk hali gelistirme sirasinda gecici bir dosya olarak duruyordu -
depoda degildi, CI'da hic kosmuyordu. PdmPaneli.cs'te bir bildirim satiri
AYNI SATIRDA iki kez yazildi (python metin degistirme kazasi) ve bu hata UC
COMMIT boyunca fark edilmeden yayinlandi. Erkan derlemeye calistiginda yedi
derleme hatasi aldi; hepsi o tek satirdandi. Denetim ancak depoda ve CI'da
ise denetimdir.

NEDEN DURUM MAKINESI (temizle): onceki bir surum once string'leri, sonra
yorumlari siliyordu. Yorum ICINDE tirnak gecen bir dosyada string silici
yorumun icinden baslayip dosyanin geri kalanini kaydiriyor ve SAHTE
dengesizlik uretiyordu. Tek gecisli tarayici hem yorumu hem string'i dogru
sirada tuketiyor.
"""

import io
import os
import re
import sys

KOK = os.path.dirname(os.path.abspath(__file__))

# Taranan agaclar. interop_denetim.py ile ayni desen: yeni bir proje
# eklendiginde listeye yazilmazsa denetim o proje icin SESSIZCE kapanir.
TARANANLAR = [
    os.path.join(KOK, "..", "src"),
    os.path.join(KOK, "..", "tests"),
]

# Uye bildirimi basligi: erisim belirleyiciyle baslayan, ';' ya da '=' icermeyen,
# ')' ile biten satir. Govdesi bir sonraki satirdaki '{'.
BASLIK = re.compile(r'^\s+(?:private|internal|public|protected)[^;=]*\)\s*$')
ERISIM = re.compile(r'\b(?:private|public|internal|protected)\b')
TIP = re.compile(r'\b(?:class|struct|interface)\s+(\w+)')

# Govde icinde yerel degisken tanimi: "var x =" ya da "Tur x ="
YEREL = re.compile(r'\s{12}(?:var|[A-Z][\w<>\[\],\.\?]*)\s+(\w+)\s*=')

# Core'da TEK kaynagi olan mantigin elle yeniden yazilmasi.
#
# NEDEN BU KURAL VAR - OLCULDU: "yolun son parcasi" mantigi depoda DOKUZ
# ayri yerde elle yazilmisti ve uc ayri boyutta ayrismisti (bos girdi,
# sondaki ayirici, '/' taninmasi). Boyut bicimlendirmesi de UC yerdeydi ve
# biri otekilerden farkli sayi gosteriyordu. Ikisi de tek sinifa toplandi;
# bu kural ONUNCUSUNUN eklenmesini engelliyor.
#
# Desenler BILEREK dar: yanlis alarm veren bir CI kapisi kapi olmaktan
# cikar. Eklendiginde depo genelinde SIFIR bulgu vardi.
TEKRAR = [
    (re.compile(r"LastIndexOfAny\(\s*new\[\]\s*\{\s*'\\\\'\s*,\s*'/'\s*\}\s*\)"),
     "Yol.Ad / Yol.TabanAd / Yol.Uzanti / Yol.Klasor"),
    (re.compile(r"LastIndexOf\('\\\\'\)"),
     "Yol.Ad / Yol.Klasor"),
    (re.compile(r"1024\.0"),
     "Boyut.Metin"),
]

# Kuralin kendisini uygulayan dosyalar dogal olarak muaf.
TEKRAR_MUAF = {"Yol.cs", "Boyut.cs", "PathKey.cs"}

# YALNIZCA src/SwPdm.Core icin: Path'in yol parcalama uyeleri yasak.
#
# NEDEN SADECE CORE - OLCULDU (net8, Linux):
#   Path.GetExtension(@"C:\Proje 2.0\parca")  ->  ".0\parca"
#   Path.GetFileName(@"C:\a\b.SLDPRT")        ->  "C:\a\b.SLDPRT"  (TAMAMI)
#   Path.GetDirectoryName(@"C:\a\b.SLDPRT")   ->  ""
# Linux'ta '\' ayirici SAYILMIYOR. Core'un testleri CI'da Linux'ta kosuyor;
# yani bu uyeler orada Windows yolunu yanlis parcaliyor ve test yanlis
# sonucu DOGRULUYOR - hata Erkan'in makinesine kaliyor (CLAUDE.md 6).
#
# Masaustu ve AddIn KAPSAM DISI ve bu bilerek: ikisi de yalnizca Windows'ta
# kosuyor, orada Path dogru calisiyor ve Yol.Klasor ondan SURUCU KOKUNDE
# ayriliyor - Path.GetDirectoryName(@"C:\a.SLDPRT") "C:\" derken Yol.Klasor
# "C:" diyor, Path.Combine("C:", "x") ise "C:x" (surucuye goreli) uretiyor.
# Calisan bir seyi olculebilir kazanc olmadan degistirmek CLAUDE.md 0'a
# aykiri olurdu.
CORE_YASAK = re.compile(
    r"\bPath\.(GetFileName|GetFileNameWithoutExtension|GetExtension|GetDirectoryName)\b")
CORE_YERINE = {
    "GetFileName": "Yol.Ad",
    "GetFileNameWithoutExtension": "Yol.TabanAd",
    "GetExtension": "Yol.Uzanti",
    "GetDirectoryName": "Yol.Klasor",
}
# v2: tek bir proje ADINA degil KURALA bagli - src/ altinda adi ".Core"
# ile biten her proje. Yasak, "SolidWorks'e ve Windows'a bagimli OLMAYAN,
# testleri Linux CI'da kosan katman" icin gecerli; onu belirleyen sey adi
# degil o ozelligi, ama ad kurali onu gorunur kiliyor.
CORE_SONEKI = ".Core"


def CoreDosyasiMi(yol):
    """
    Dosya, src/ altinda adi CORE_SONEKI ile biten bir projenin icinde mi.

    YOL PARCALARINA BAKIYOR, metin aramasi YAPMIYOR: "src/X.Core.Tests/..."
    gibi bir yol duz bir find() ile de eslesirdi ve TESTLER Core sayilirdi -
    oysa testler net8'de kosuyor ve orada Path yasagi anlamsiz.
    """
    parcalar = os.path.normpath(yol).replace("\\", "/").split("/")
    return any(p.endswith(CORE_SONEKI) for p in parcalar)


def temizle(kaynak, dizeleri_de_at=True):
    """Yorumlari atar; dizeleri_de_at ise dize/karakter sabitlerini de atar.

    NEDEN SECENEK: yapi denetimleri (denge, CS0111, CS0128) dizelerin
    ICINDEKI suslu parantezden etkilenmemeli, o yuzden onlar icin hepsi
    atiliyor. Ama "tekrar" denetimi tam da karakter sabitlerine bakiyor
    ('\\' ve '/'); onlari atarsak LastIndexOf('\\') ile LastIndexOf('.')
    ayirt edilemez hale geliyor. O denetim yalnizca YORUMLARI attiriyor -
    boylece bir aciklama metnindeki ornek kod da yanlis alarm uretmiyor.
    """
    cikti = []
    i, n = 0, len(kaynak)
    while i < n:
        c = kaynak[i]
        if c == '/' and i + 1 < n and kaynak[i + 1] == '/':
            while i < n and kaynak[i] != '\n':
                i += 1
        elif c == '/' and i + 1 < n and kaynak[i + 1] == '*':
            i += 2
            while i + 1 < n and not (kaynak[i] == '*' and kaynak[i + 1] == '/'):
                if kaynak[i] == '\n':
                    cikti.append('\n')      # satir numaralari kaysin istemiyoruz
                i += 1
            i += 2
        elif not dizeleri_de_at and (c == '"' or c == "'" or
                                     (c == '@' and i + 1 < n and kaynak[i + 1] == '"')):
            # Dize/karakter sabiti KORUNUYOR - ama icindeki kacislari dogru
            # tuketmek zorundayiz, yoksa kapanis tirnagini kacirir ve
            # dosyanin geri kalanini yanlis okuruz.
            tirnak = '"' if c != "'" else "'"
            if c == '@':
                cikti.append(kaynak[i]); i += 1          # '@'
            cikti.append(kaynak[i]); i += 1              # acilis tirnagi
            while i < n:
                if kaynak[i] == '\\' and c != '@':
                    cikti.append(kaynak[i]); i += 1
                    if i < n:
                        cikti.append(kaynak[i]); i += 1
                    continue
                cikti.append(kaynak[i])
                if kaynak[i] == tirnak:
                    i += 1
                    break
                i += 1
        elif c == '@' and i + 1 < n and kaynak[i + 1] == '"':
            i += 2
            while i < n:
                if kaynak[i] == '"':
                    if i + 1 < n and kaynak[i + 1] == '"':
                        i += 2
                        continue
                    i += 1
                    break
                if kaynak[i] == '\n':
                    cikti.append('\n')
                i += 1
        elif c == '"':
            i += 1
            while i < n:
                if kaynak[i] == '\\':
                    i += 2
                    continue
                if kaynak[i] == '"':
                    i += 1
                    break
                i += 1
        elif c == "'":
            i += 1
            while i < n:
                if kaynak[i] == '\\':
                    i += 2
                    continue
                if kaynak[i] == "'":
                    i += 1
                    break
                i += 1
        else:
            cikti.append(c)
            i += 1
    return "".join(cikti)


def bildirimler(satirlar):
    """(satir_no, icinde_bulundugu_tip, baslik_metni) uretir.

    TIP TAKIBINDE IKI HATA YASANDI, ikisi de gercek dosyalara karsi olculerek
    bulundu - ayni tuzaga dusmemek icin yazili:

    1. Tip yigini bildirimin gorulduğu SATIRDA push edilip ayni satirda pop
       ediliyordu; sonuc olarak her uye "kok" gorunuyordu ve sinif ayrimi hic
       calismiyordu. SwBaglanti.cs'teki iki ayri Dispose() (biri ic sinif
       GorunmezlukKapsami'nde) sahte CS0111 verdi. Cozum: tipi, govdesini acan
       '{' gorulunce push et.

    2. '{ get; set; }' otomatik ozellikleri ac/kapa'yi AYNI SATIRDA yapiyor.
       Acilis ve kapanisi ayri ayri islemek derinligi bir an dusuruyor ve
       yigini yanlislikla bosaltiyordu; Referans.cs'te iki farkli sinifin
       ToString()'i sahte CS0111 verdi. Cozum: satir basina NET derinlik.
    """
    derinlik = 0
    yigin = []        # (govde_derinligi, tip_adi)
    bekleyen = None   # tip bildirimini gorduk, govdesini acan '{' bekliyoruz

    for i, s in enumerate(satirlar, 1):
        m = TIP.search(s)
        if m:
            bekleyen = m.group(1)

        if BASLIK.match(s):
            yield i, (yigin[-1][1] if yigin else "<kok>"), s

        ac = s.count("{")
        derinlik += ac - s.count("}")

        while yigin and derinlik < yigin[-1][0]:
            yigin.pop()

        if bekleyen is not None and ac > 0:
            yigin.append((derinlik, bekleyen))
            bekleyen = None


def govdeler(satirlar):
    """(baslik, govde_satirlari) uretir - CS0128 taramasi icin."""
    i = 0
    while i < len(satirlar):
        s = satirlar[i]
        if BASLIK.match(s) and i + 1 < len(satirlar) and satirlar[i + 1].strip() == "{":
            d, j, g = 0, i + 1, []
            while j < len(satirlar):
                d += satirlar[j].count("{") - satirlar[j].count("}")
                g.append(satirlar[j])
                if d == 0:
                    break
                j += 1
            yield s.strip(), g
            i = j
        i += 1


def denetle(yol):
    """Bu dosyadaki bulgulari (aciklama metinleri) dondurur."""
    bulgular = []
    temiz = temizle(io.open(yol, encoding="utf-8").read())
    satirlar = temiz.split("\n")

    # 1. Kaba denge
    for ac, kapa, ad in [('{', '}', '{}'), ('(', ')', '()'), ('[', ']', '[]')]:
        fark = temiz.count(ac) - temiz.count(kapa)
        if fark:
            bulgular.append("DENGESIZ %s: %s %+d" % (ad, yol, fark))

    # 2. Bildirim basliklari
    gorulen = {}
    for satir, tip, s in bildirimler(satirlar):
        # 2a. Ayni satirda IKI bildirim.
        # Tam olarak yasanan hata: metin degistirme kazasi bildirimi satir
        # icinde ikizliyor. Derleyici bunu CS0111 + CS0501 + CS1002 olarak
        # bildiriyor; eski denetim ise satiri sorunsuz tek bir metot saniyordu
        # cunku yalnizca "baslik gibi mi gorunuyor" diye bakiyordu.
        if len(ERISIM.findall(s)) >= 2:
            bulgular.append("CS0111/CS0501 RISKI: %s:%d  ayni satirda IKI bildirim\n"
                            "    %s" % (yol, satir, s.strip()[:110]))

        # 2b. Ayni TIP icinde birebir ayni imza iki kez (metot bastan yapistirilmis).
        imza = " ".join(s.split())
        anahtar = (tip, imza)
        if anahtar in gorulen:
            bulgular.append("CS0111 RISKI: %s:%d  '%s' icinde ayni imza %d. satirda da var\n"
                            "    %s" % (yol, satir, tip, gorulen[anahtar], imza[:110]))
        gorulen[anahtar] = satir

    # 3. Govde icinde ayni yerel adin iki kez tanimlanmasi (CS0128)
    for imza, govde in govdeler(satirlar):
        adlar = {}
        for satir in govde:
            m = YEREL.match(satir)
            if m:
                adlar[m.group(1)] = adlar.get(m.group(1), 0) + 1
        for ad, kac in adlar.items():
            if kac > 1:
                bulgular.append("CS0128 RISKI: %s | %s -> %s x%d"
                                % (yol, imza[:60], ad, kac))

    # 4. Core'da tek kaynagi olan mantigin elle yeniden yazilmasi.
    #    YORUMLARI atilmis ama DIZELERI duran kaynak uzerinde: desenler tam
    #    da karakter sabitlerine bakiyor ('\\' ve '/'), tam temizlikte
    #    LastIndexOf('\\') ile LastIndexOf('.') ayirt edilemez olurdu.
    if os.path.basename(yol) not in TEKRAR_MUAF:
        kodlu = temizle(io.open(yol, encoding="utf-8").read(), dizeleri_de_at=False)
        for satir_no, satir in enumerate(kodlu.split("\n"), 1):
            for desen, yerine in TEKRAR:
                if desen.search(satir):
                    bulgular.append(
                        "TEKRAR: %s:%d  bu mantigin Core'da tek kaynagi var -> %s\n"
                        "    %s" % (yol, satir_no, yerine, satir.strip()[:110]))

    # 6. UST USTE IKI <summary> - SARKAN BELGE YORUMU.
    #
    # NEDEN BU KURAL VAR - OLCULDU: depoda 18 tane vardi. Hepsinin sebebi
    # ayni kaza: bir uye silinince ya da araya yeni bir uye eklenince, eski
    # uyenin belge yorumu YERINDE KALIYOR ve artik ALAKASIZ bir uyenin
    # ustunde duruyor. Derleyici sesini cikarmiyor (ikinci <summary> sessizce
    # yok sayiliyor), yani hatayi yalnizca okuyan insan yiyor - ve bu depoda
    # "bu kod neden boyle" sorusunun tek cevabi o yorumlar.
    #
    # 18'in HICBIRI olu degildi: her biri hala YASAYAN bir uyeyi anlatiyordu,
    # yalnizca yanlis uyenin ustundeydi. Yani silinecek degil TASINACAK
    # bilgiydi.
    #
    # Yanlis alarm YOK: C#'ta bir uyenin tek bir <summary>'si olur.
    #
    # HAM kaynak uzerinde - temizle() yorumlari (/// dahil) atiyor; bu kural
    # onlara BAKIYOR. Ilk yazilisinda temiz metne bakiyordu ve kapi HER ZAMAN
    # "TEMIZ" diyordu; olculdugu icin yakalandi (CLAUDE.md 4).
    ham = io.open(yol, encoding="utf-8").read().split("\n")
    for satir_no in range(len(ham) - 1):
        if ham[satir_no].strip() == "/// </summary>" and \
           ham[satir_no + 1].strip().startswith("/// <summary>"):
            bulgular.append(
                "SARKAN BELGE: %s:%d  ust uste IKI <summary> - ustteki, "
                "artik burada olmayan bir uyeyi anlatiyor" % (yol, satir_no + 1))

    # 5. Core'da Path'in yol parcalama uyeleri (bkz. CORE_YASAK).
    #    Yorumlar ATILMIS kaynak uzerinde: bu dosyalarin YARISI kurali kendi
    #    yorumunda anlatiyor ve ham metinde arayan bir kapi onlari yakalardi.
    if CoreDosyasiMi(yol):
        for satir_no, satir in enumerate(temiz.split("\n"), 1):
            for esleme in CORE_YASAK.finditer(satir):
                bulgular.append(
                    "CORE/PATH: %s:%d  Linux'ta '\\' ayirici sayilmiyor -> %s\n"
                    "    %s" % (yol, satir_no, CORE_YERINE[esleme.group(1)],
                                satir.strip()[:110]))

    return bulgular


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
                goreli = os.path.relpath(yol, os.path.join(KOK, ".."))
                for bulgu in denetle(yol):
                    print(bulgu.replace(yol, goreli))
                    hata += 1

    if hata:
        print("\n%d bulgu.\n"
              "  DENGESIZ / CS0111 / TEKRAR : Linux'ta DERLENMEYEN projelerde de "
              "derlemeyi kirar.\n"
              "  CORE/PATH                  : derlemeyi kirmaz, CI'da (Linux) "
              "YANLIS olcer.\n"
              "  SARKAN BELGE               : derlemeyi kirmaz, OKUYANI yanlis "
              "yonlendirir." % hata)
        return 1

    print("C# kaba yapi denetimi: TEMIZ")
    return 0


if __name__ == "__main__":
    sys.exit(main())

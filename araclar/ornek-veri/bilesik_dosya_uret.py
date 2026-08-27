import struct, io

SEKTOR = 512
FREESECT, ENDOFCHAIN, FATSECT = 0xFFFFFFFF, 0xFFFFFFFE, 0xFFFFFFFD

def dizin_girdisi(ad, tur, renk, sol, sag, cocuk, baslangic, boyut):
    b = bytearray(128)
    kodlu = ad.encode("utf-16-le") + b"\x00\x00"
    b[0:len(kodlu)] = kodlu
    struct.pack_into("<H", b, 0x40, len(kodlu))
    b[0x42] = tur; b[0x43] = renk
    struct.pack_into("<III", b, 0x44, sol, sag, cocuk)
    struct.pack_into("<I", b, 0x74, baslangic)
    struct.pack_into("<Q", b, 0x78, boyut)
    return bytes(b)

def yaz(cikti, kucuk_ad, kucuk_veri, buyuk_ad, buyuk_veri):
    mini_sayisi = (len(kucuk_veri) + 63) // 64
    mini_kap_bayt = mini_sayisi * 64
    mini_kap_sektor = max(1, (mini_kap_bayt + SEKTOR - 1) // SEKTOR)
    buyuk_sektor = (len(buyuk_veri) + SEKTOR - 1) // SEKTOR

    # 0=FAT, 1=dizin, 2=miniFAT, 3..=mini kap, sonra buyuk akis
    mini_bas = 3
    buyuk_bas = mini_bas + mini_kap_sektor
    toplam = buyuk_bas + buyuk_sektor

    fat = [FREESECT] * 128
    fat[0] = FATSECT
    fat[1] = ENDOFCHAIN
    fat[2] = ENDOFCHAIN
    for i in range(mini_kap_sektor):
        fat[mini_bas + i] = ENDOFCHAIN if i == mini_kap_sektor - 1 else mini_bas + i + 1
    for i in range(buyuk_sektor):
        fat[buyuk_bas + i] = ENDOFCHAIN if i == buyuk_sektor - 1 else buyuk_bas + i + 1

    minifat = [FREESECT] * 128
    for i in range(mini_sayisi):
        minifat[i] = ENDOFCHAIN if i == mini_sayisi - 1 else i + 1

    basl = bytearray(SEKTOR)
    basl[0:8] = bytes.fromhex("D0CF11E0A1B11AE1")
    struct.pack_into("<HHH", basl, 0x18, 0x3E, 3, 0xFFFE)
    struct.pack_into("<HH", basl, 0x1E, 9, 6)
    struct.pack_into("<I", basl, 0x2C, 1)          # FAT sektor sayisi
    struct.pack_into("<I", basl, 0x30, 1)          # ilk dizin sektoru
    struct.pack_into("<I", basl, 0x38, 4096)       # mini akis esigi
    struct.pack_into("<I", basl, 0x3C, 2)          # ilk miniFAT
    struct.pack_into("<I", basl, 0x40, 1)          # miniFAT sayisi
    struct.pack_into("<I", basl, 0x44, FREESECT)   # DIFAT yok
    for i in range(109):
        struct.pack_into("<I", basl, 0x4C + i * 4, 0 if i == 0 else FREESECT)

    dizin = bytearray()
    dizin += dizin_girdisi("Root Entry", 5, 0, FREESECT, FREESECT, 1, mini_bas, mini_kap_bayt)
    dizin += dizin_girdisi(kucuk_ad, 2, 1, FREESECT, 2, FREESECT, 0, len(kucuk_veri))
    dizin += dizin_girdisi(buyuk_ad, 2, 1, FREESECT, FREESECT, FREESECT, buyuk_bas, len(buyuk_veri))
    dizin += bytes(128)

    d = io.BytesIO()
    d.write(basl)
    d.write(b"".join(struct.pack("<I", x) for x in fat))
    d.write(dizin.ljust(SEKTOR, b"\x00"))
    d.write(b"".join(struct.pack("<I", x) for x in minifat))
    d.write(kucuk_veri.ljust(mini_kap_sektor * SEKTOR, b"\x00"))
    d.write(buyuk_veri.ljust(buyuk_sektor * SEKTOR, b"\x00"))
    open(cikti, "wb").write(d.getvalue())
    return toplam

png = open("onizleme.png", "rb").read()
buyuk = bytes(range(256)) * 24     # 6144 bayt -> normal sektor yolu
n = yaz("ornek.sldprt", "PreviewPNG", png, "BuyukAkis", buyuk)
print(f"yazildi: ornek.sldprt  ({n} sektor, PreviewPNG {len(png)} bayt, BuyukAkis {len(buyuk)} bayt)")

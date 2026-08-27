#!/usr/bin/env python3
"""Gecerli, tek sayfalik kucuk bir PDF uretir.

Calistirma kapisinin ornek klasorune giriyor. Elle yaziliyor cunku ureteci
bagimliliksiz ve gozden gecirilebilir olsun istiyoruz - depoya giren ikili
dosyanin nasil olustugu gorunur kalmali.
"""
import sys

icerik = b"""q
1 w 0.18 0.43 0.64 RG
50 60 495 720 re S
0.95 0.95 0.95 rg
90 560 415 180 re f
0.18 0.43 0.64 RG
90 560 415 180 re S
BT /F1 28 Tf 120 640 Td (ORNEK TEKNIK RESIM) Tj ET
BT /F1 14 Tf 120 600 Td (SW PDM v2 - PDF onizleme denemesi) Tj ET
2 w
120 200 m 300 480 l S
300 480 m 480 200 l S
480 200 m 120 200 l S
Q"""

nesneler = [
    b"<< /Type /Catalog /Pages 2 0 R >>",
    b"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
    b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] "
    b"/Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
    b"<< /Length " + str(len(icerik)).encode() + b" >>\nstream\n" + icerik + b"\nendstream",
    b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
]

govde = bytearray(b"%PDF-1.4\n")
konumlar = []
for i, n in enumerate(nesneler, start=1):
    konumlar.append(len(govde))
    govde += str(i).encode() + b" 0 obj\n" + n + b"\nendobj\n"

xref = len(govde)
govde += b"xref\n0 " + str(len(nesneler) + 1).encode() + b"\n"
govde += b"0000000000 65535 f \n"
for k in konumlar:
    govde += ("%010d 00000 n \n" % k).encode()
govde += (b"trailer\n<< /Size " + str(len(nesneler) + 1).encode()
          + b" /Root 1 0 R >>\nstartxref\n" + str(xref).encode() + b"\n%%EOF\n")

hedef = sys.argv[1] if len(sys.argv) > 1 else "ornek.pdf"
open(hedef, "wb").write(bytes(govde))
print(f"yazildi: {hedef} ({len(govde)} bayt)")

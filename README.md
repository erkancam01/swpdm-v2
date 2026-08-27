# SW PDM v2

SOLIDWORKS dosyalarını taşırken ve adlandırırken **montaj ve teknik resim
referanslarını koruyan** bağımsız masaüstü uygulaması.

## Durum

**Kapsam belirleniyor.** Bu depoda henüz ürün kodu yok — yalnızca çalışma
kuralları (`CLAUDE.md`) ve CI kapıları (`tools/`) var.

Karara bağlanmış tek şey: **SOLIDWORKS eklentisi (Görev Bölmesi paneli)
şimdilik YOK.** Gerekçesi ölçüsüyle birlikte `CLAUDE.md` §4.3'te.

## v1

v1 `erkancam01/swpdm` deposunda ve **bitti**. Orada 160+ commit'lik geçmiş ve
kendi `CLAUDE.md`'si duruyor; bu depodaki kurallar oradan **ölçülerek**
çıkarıldı.

## Kapılar

```bash
python3 tools/csdenge.py
python3 tools/interop_denetim.py
python3 tools/sozdizim.py      # pip install tree_sitter tree_sitter_c_sharp
python3 tools/bat_kapisi.py
```

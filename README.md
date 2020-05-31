# AML_Arciver Unpacker (imageArcive.arc) to PNG
`imageArcive.arc` contains most of the images from **True Remembrance** [Nintendo 3DS].

You can use C # or Python version.
## Usage
Use `ctrtool` to extract romfs.

.3ds
```
ctrtool --romfsdir=romfs game.3ds
```
.cia
```
ctrtool --contents=contents game.cia
ctrtool --romfsdir=romfs contents.0000.00000000
```
imageArcive.arc inside romfs/data/.
### C# (faster)
1. Install .NET Framework 4+. Windows 8 and above have it.
2. Unpack:
```
AMLUnpacker.exe imageArcive.arc images_dir
```
Download `AMLUnpacker.exe` in releases.
### Python 3
1. Install Python 3.
2. Install PIL:
```
pip install -U pillow
```
3. Unpack:
```
AMLUnpacker.py imageArcive.arc -o images_dir
```

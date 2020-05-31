#!/usr/bin/env python3
__version__ = "1.0.0"
__author__  = "infval"


from pathlib import Path
from struct import unpack
from os import system

from PIL import Image

import etc1decoder


class CTPKTexInfo:
    """ https://www.3dbrew.org/wiki/CTPK#Texture_Info_Entry
    """
    ATTR_NAMES = (
          "Magic",                 # CTPK
          "FilePathOffset ",       # -
          "TextureDataSize",
          "TextureDataOffset",     # -
          "TextureFormat",         # 0xC - ETC1, 0xD - ETC1A4 (ETC1 + Alpha)
          "Width",
          "Height",
          "MipLevel",
          "Type",                  # 0: Cube Map, 1: 1D, 2: 2D
          "CubeMapRelated",        # -
          "BitmapSizeArrayOffset", # -
          "UnixTimestamp"
    )
    def __init__(self, data):
        values = unpack("<IIIIIHHBBHII", data)
        for key, value in zip(self.ATTR_NAMES, values):
            setattr(self, key, value)

    def __str__(self):
        s = ""
        for key in self.ATTR_NAMES:
            s += "{}: {}\n".format(key, getattr(self, key))
        return s


def decompress(b):
    bout = bytearray()
    marker = b[0]

    i = 1
    while i < len(b):
        if b[i] == marker and b[i + 1] == marker:
            bout.append(b[i])
            i += 1
        elif b[i] == marker:
            i += 1
            offset = b[i]
            #if offset == 0:
            #    print("OFFSET 0")
            if offset > marker:
                offset -= 1
            i += 1
            count = b[i]
            #if count < 4:
            #    print("COUNT", count)

            index = len(bout) - offset
            #if index < 0:
            #    print("INDEX", offset, count, len(bout))
            #    index = 0
            bout.extend(bout[index: index + count])
            #for j in range(count):
            #    bout.append(bout[index + j])
        else:
            bout.append(b[i])
        i += 1

    return bout


def get_str(b):
    end = b.find(b"\x00")
    s = b[:end].decode("latin_1")
    return s


def rbga_to_png(path, data, width, height):
    pix_data = []
    for i in range(0, len(data), 4):
        pix_data.append((data[i+0], data[i+1], data[i+2], data[i+3]))
    im = Image.new("RGBA", (width, height))
    im.putdata(pix_data)
    im.save(path)
    im.close()


def aml_unpack(path, outdir):
    with open(path, "rb") as f:
        magic = f.read(11)
        if magic != b"AML_Arciver":
            print("Error: not AML_Arciver -", path)
            return

        f.seek(0x80)
        filecount, = unpack("<I", f.read(4))
        print("File count:", filecount)

        for i in range(filecount):
            filename = get_str(f.read(0x40))
            print(filename)
            filename = filename.replace("/", "_")

            p_output = Path(outdir) / (filename + ".png")
            print(">", p_output)

            filesize, = unpack("<I", f.read(4))

            data = decompress(f.read(filesize))
            texinfo = CTPKTexInfo(data[:36])
            if texinfo.TextureDataSize != len(data) - 36:
                print("Error: texinfo.TextureDataSize != len(data) - 36")

            # ETC1A4 to PNG
            if texinfo.TextureFormat == 0xD:
                data = etc1decoder.decode(data[36:], texinfo.Width, texinfo.Height, True)
                rbga_to_png(p_output, data, texinfo.Width, texinfo.Height)
            else:
                print("Error: texinfo.TextureFormat != 0xD (ETC1A4)")


def get_argparser():
    import argparse
    parser = argparse.ArgumentParser(description='AML_Arciver Unpacker - [3DS] True Remembrance || v' + __version__)
    parser.add_argument('--version', action='version', version='%(prog)s ' + __version__)
    parser.add_argument('input_path', metavar='infile', help='AML_Arciver (imageArcive.arc)')
    parser.add_argument('-o', '--output', metavar='outdir', help='output directory (default: %%infile%%_unpack)')
    return parser


if __name__ == '__main__':
    parser = get_argparser()
    args = parser.parse_args()

    p_input = Path(args.input_path)
    outdir = args.input_path + "_unpack"
    if args.output:
        outdir = args.output
    p_output = Path(outdir)

    p_output.mkdir(exist_ok=True)
    aml_unpack(p_input, p_output)

#!/usr/bin/env python3
""" Original code: https://github.com/gdkchan/Ohana3DS-Rebirth/
"""

from pathlib import Path
from struct import unpack

etc1LUT = [
    [2 , 8  , -2 , -8  ],
    [5 , 17 , -5 , -17 ],
    [9 , 29 , -9 , -29 ],
    [13, 42 , -13, -42 ],
    [18, 60 , -18, -60 ],
    [24, 80 , -24, -80 ],
    [33, 106, -33, -106],
    [47, 183, -47, -183]
]

def int_to_s8(n):
    """ 0x80 -> -128, 0xFF -> -1, 127 -> 127 """
    n &= 0xFF
    if n & 0x80:
        n -= 0x100
    return n

def decode(data, width, height, alpha=False):
    output = bytearray(width * height * 4)

    decodedData = etc1Decode(data, width, height, alpha)
    etc1Order = etc1Scramble(width, height)

    i = 0
    for tY in range(height // 4):
        for tX in range(width // 4):
            TX = etc1Order[i] % (width // 4)
            TY = (etc1Order[i] - TX) // (width // 4)
            for y in range(4):
                for x in range(4):
                    dataOffset   = ((TX * 4) + x + ((TY * 4 + y) * width)) * 4
                    outputOffset = ((tX * 4) + x + ((tY * 4 + y) * width)) * 4

                    output[outputOffset: outputOffset + 4] = decodedData[dataOffset: dataOffset + 4]
            i += 1

    return output

def etc1Decode(input, width, height, alpha):
    output = bytearray(width * height * 4)
    offset = 0

    for y in range(height // 4):
        for x in range(width // 4):
            colorBlock = bytearray(8)
            alphaBlock = bytearray(8)
            if alpha:
                for i in range(8):
                    colorBlock[7 - i] = input[offset + 8 + i]
                    alphaBlock[i    ] = input[offset + i]
                offset += 16
            else:
                for i in range(8):
                    colorBlock[7 - i] = input[offset + i]
                    alphaBlock[i    ] = 0xff
                offset += 8

            colorBlock = etc1DecodeBlock(colorBlock)

            toggle = False
            alphaOffset = 0
            for tX in range(4):
                for tY in range(4):
                    outputOffset = (x * 4 + tX + ((y * 4 + tY) * width)) * 4
                    blockOffset = (tX + (tY * 4)) * 4
                    output[outputOffset: outputOffset + 3] = colorBlock[blockOffset: blockOffset + 3]

                    if toggle:
                        a = (alphaBlock[alphaOffset] & 0xf0) >> 4
                        alphaOffset += 1
                    else:
                        a = (alphaBlock[alphaOffset] & 0x0f)
                    output[outputOffset + 3] = (a << 4) | a
                    toggle = not toggle

    return output

def etc1DecodeBlock(data):
    blockTop,    = unpack("<I", data[:4])
    blockBottom, = unpack("<I", data[4:])

    flip       = (blockTop & 0x1000000) > 0
    difference = (blockTop & 0x2000000) > 0

    r1 = g1 = b1 = 0
    r2 = g2 = b2 = 0

    if difference:
        r1 = (blockTop & 0x0000f8)
        g1 = (blockTop & 0x00f800) >> 8
        b1 = (blockTop & 0xf80000) >> 16

        r2 = (r1 >> 3) + (int_to_s8((blockTop & 0x00007) <<  5) >> 5)
        g2 = (g1 >> 3) + (int_to_s8((blockTop & 0x00700) >>  3) >> 5)
        b2 = (b1 >> 3) + (int_to_s8((blockTop & 0x70000) >> 11) >> 5)

        r1 |= r1 >> 5
        g1 |= g1 >> 5
        b1 |= b1 >> 5

        r2 = (r2 << 3) | (r2 >> 2)
        g2 = (g2 << 3) | (g2 >> 2)
        b2 = (b2 << 3) | (b2 >> 2)
    else:
        r1 = (blockTop & 0x0000f0)
        g1 = (blockTop & 0x00f000) >> 8
        b1 = (blockTop & 0xf00000) >> 16

        r2 = (blockTop & 0x00000f) << 4
        g2 = (blockTop & 0x000f00) >> 4
        b2 = (blockTop & 0x0f0000) >> 12

        r1 |= r1 >> 4
        g1 |= g1 >> 4
        b1 |= b1 >> 4

        r2 |= r2 >> 4
        g2 |= g2 >> 4
        b2 |= b2 >> 4

    table1 = (blockTop >> 29) & 7
    table2 = (blockTop >> 26) & 7

    output = bytearray(4 * 4 * 4)
    if not flip:
        for y in range(4):
            for x in range(2):
                color1 = etc1Pixel(r1, g1, b1, x    , y, blockBottom, table1)
                color2 = etc1Pixel(r2, g2, b2, x + 2, y, blockBottom, table2)

                offset = (y * 4 + x) * 4
                output[offset: offset + 3] = color1

                offset = (y * 4 + x + 2) * 4
                output[offset: offset + 3] = color2
    else:
        for y in range(2):
            for x in range(4):
                color1 = etc1Pixel(r1, g1, b1, x, y    , blockBottom, table1)
                color2 = etc1Pixel(r2, g2, b2, x, y + 2, blockBottom, table2)

                offset = (y * 4 + x) * 4
                output[offset: offset + 3] = color1

                offset = ((y + 2) * 4 + x) * 4
                output[offset: offset + 3] = color2

    return output

def etc1Pixel(r, g, b, x, y, block, table):
    index = x * 4 + y
    MSB = block << 1

    if index < 8:
        pixel = etc1LUT[table][((block >> (index + 24)) & 1) + ((MSB >> (index + 8)) & 2)]
    else:
        pixel = etc1LUT[table][((block >> (index +  8)) & 1) + ((MSB >> (index - 8)) & 2)]

    r = saturate(r + pixel)
    g = saturate(g + pixel)
    b = saturate(b + pixel)

    return (r, g, b) # Byte order: R G B A

def saturate(value):
    if value > 0xff:
        return 0xff
    if value < 0:
        return 0
    return value

def etc1Scramble(width, height):
    tileScramble = [0] * ((width // 4) * (height // 4))
    baseAccumulator = 0
    rowAccumulator = 0
    baseNumber = 0
    rowNumber = 0

    for tile in range(len(tileScramble)):
        if (tile % (width // 4) == 0) and tile > 0:
            if rowAccumulator < 1:
                rowAccumulator += 1
                rowNumber += 2
                baseNumber = rowNumber
            else:
                rowAccumulator = 0
                baseNumber -= 2
                rowNumber = baseNumber

        tileScramble[tile] = baseNumber

        if baseAccumulator < 1:
            baseAccumulator += 1
            baseNumber += 1
        else:
            baseAccumulator = 0
            baseNumber += 3

    return tileScramble

def etc1_to_png(path, outfile, width, height, alpha=False):
    from PIL import Image

    b = Path(path).read_bytes()
    b = decode(b, width, height, alpha)

    pix_data = []
    for i in range(0, len(b), 4):
        pix_data.append((b[i+0], b[i+1], b[i+2], b[i+3]))
    im = Image.new("RGBA", (width, height))
    im.putdata(pix_data)
    im.save(outfile, "PNG")
    im.close()

def get_argparser():
    import argparse
    parser = argparse.ArgumentParser(description='ETC1/ETC1A4 decoder [3DS]')
    parser.add_argument('input_path', metavar='infile', help='raw file')
    parser.add_argument('width')
    parser.add_argument('height')
    parser.add_argument('-a', '--alpha', action='store_true', help='ETC1A4')
    parser.add_argument('-o', '--output', metavar='outfile', help='output PNG file (default: %%infile%%.png)')
    return parser

if __name__ == '__main__':
    parser = get_argparser()
    args = parser.parse_args()

    p_input = Path(args.input_path)
    outfile = args.input_path + ".png"
    if args.output:
        outfile = args.output
    p_output = Path(outfile)

    width = int(args.width)
    height = int(args.height)

    etc1_to_png(p_input, p_output, width, height, args.alpha)

// From https://github.com/gdkchan/Ohana3DS-Rebirth/
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace AMLUnpacker
{
    class ETC1
    {
        private static int[,] etc1LUT = {
            { 2,  8,   -2,  -8   },
            { 5,  17,  -5,  -17  },
            { 9,  29,  -9,  -29  },
            { 13, 42,  -13, -42  },
            { 18, 60,  -18, -60  },
            { 24, 80,  -24, -80  },
            { 33, 106, -33, -106 },
            { 47, 183, -47, -183 }
        };

        /// <summary>
        ///     Decodes a ETC1/ETC1A4 Texture.
        /// </summary>
        /// <param name="data">Buffer with the Texture</param>
        /// <param name="width">Width of the Texture</param>
        /// <param name="height">Height of the Texture</param>
        /// <param name="format">Pixel Format of the Texture</param>
        /// <returns></returns>
        public static Bitmap decode(byte[] data, int width, int height, bool alpha = false)
        {
            byte[] output = new byte[width * height * 4];
            long dataOffset = 0;

            byte[] decodedData = etc1Decode(data, width, height, alpha);
            int[] etc1Order = etc1Scramble(width, height);

            int i = 0;
            for (int tY = 0; tY < height / 4; tY++)
            {
                for (int tX = 0; tX < width / 4; tX++)
                {
                    int TX = etc1Order[i] % (width / 4);
                    int TY = (etc1Order[i] - TX) / (width / 4);
                    for (int y = 0; y < 4; y++)
                    {
                        for (int x = 0; x < 4; x++)
                        {
                            dataOffset = ((TX * 4) + x + (((TY * 4) + y) * width)) * 4;
                            long outputOffset = ((tX * 4) + x + (((tY * 4 + y)) * width)) * 4;

                            Buffer.BlockCopy(decodedData, (int)dataOffset, output, (int)outputOffset, 4);
                        }
                    }
                    i += 1;
                }
            }

            return TextureUtils.getBitmap(output, width, height);
        }

        #region "ETC1"
        private static byte[] etc1Decode(byte[] input, int width, int height, bool alpha)
        {
            byte[] output = new byte[(width * height * 4)];
            long offset = 0;

            for (int y = 0; y < height / 4; y++)
            {
                for (int x = 0; x < width / 4; x++)
                {
                    byte[] colorBlock = new byte[8];
                    byte[] alphaBlock = new byte[8];
                    if (alpha)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            colorBlock[7 - i] = input[offset + 8 + i];
                            alphaBlock[i] = input[offset + i];
                        }
                        offset += 16;
                    }
                    else
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            colorBlock[7 - i] = input[offset + i];
                            alphaBlock[i] = 0xff;
                        }
                        offset += 8;
                    }

                    colorBlock = etc1DecodeBlock(colorBlock);

                    bool toggle = false;
                    long alphaOffset = 0;
                    for (int tX = 0; tX < 4; tX++)
                    {
                        for (int tY = 0; tY < 4; tY++)
                        {
                            int outputOffset = (x * 4 + tX + ((y * 4 + tY) * width)) * 4;
                            int blockOffset = (tX + (tY * 4)) * 4;
                            Buffer.BlockCopy(colorBlock, blockOffset, output, outputOffset, 3);

                            byte a = toggle ? (byte)((alphaBlock[alphaOffset++] & 0xf0) >> 4) : (byte)(alphaBlock[alphaOffset] & 0xf);
                            output[outputOffset + 3] = (byte)((a << 4) | a);
                            toggle = !toggle;
                        }
                    }
                }
            }

            return output;
        }

        private static byte[] etc1DecodeBlock(byte[] data)
        {
            uint blockTop = BitConverter.ToUInt32(data, 0);
            uint blockBottom = BitConverter.ToUInt32(data, 4);

            bool flip = (blockTop & 0x1000000) > 0;
            bool difference = (blockTop & 0x2000000) > 0;

            uint r1, g1, b1;
            uint r2, g2, b2;

            if (difference)
            {
                r1 = blockTop & 0xf8;
                g1 = (blockTop & 0xf800) >> 8;
                b1 = (blockTop & 0xf80000) >> 16;

                r2 = (uint)((sbyte)(r1 >> 3) + ((sbyte)((blockTop & 7) << 5) >> 5));
                g2 = (uint)((sbyte)(g1 >> 3) + ((sbyte)((blockTop & 0x700) >> 3) >> 5));
                b2 = (uint)((sbyte)(b1 >> 3) + ((sbyte)((blockTop & 0x70000) >> 11) >> 5));

                r1 |= r1 >> 5;
                g1 |= g1 >> 5;
                b1 |= b1 >> 5;

                r2 = (r2 << 3) | (r2 >> 2);
                g2 = (g2 << 3) | (g2 >> 2);
                b2 = (b2 << 3) | (b2 >> 2);
            }
            else
            {
                r1 = blockTop & 0xf0;
                g1 = (blockTop & 0xf000) >> 8;
                b1 = (blockTop & 0xf00000) >> 16;

                r2 = (blockTop & 0xf) << 4;
                g2 = (blockTop & 0xf00) >> 4;
                b2 = (blockTop & 0xf0000) >> 12;

                r1 |= r1 >> 4;
                g1 |= g1 >> 4;
                b1 |= b1 >> 4;

                r2 |= r2 >> 4;
                g2 |= g2 >> 4;
                b2 |= b2 >> 4;
            }

            uint table1 = (blockTop >> 29) & 7;
            uint table2 = (blockTop >> 26) & 7;

            byte[] output = new byte[(4 * 4 * 4)];
            if (!flip)
            {
                for (int y = 0; y <= 3; y++)
                {
                    for (int x = 0; x <= 1; x++)
                    {
                        Color color1 = etc1Pixel(r1, g1, b1, x, y, blockBottom, table1);
                        Color color2 = etc1Pixel(r2, g2, b2, x + 2, y, blockBottom, table2);

                        int offset1 = (y * 4 + x) * 4;
                        output[offset1] = color1.B;
                        output[offset1 + 1] = color1.G;
                        output[offset1 + 2] = color1.R;

                        int offset2 = (y * 4 + x + 2) * 4;
                        output[offset2] = color2.B;
                        output[offset2 + 1] = color2.G;
                        output[offset2 + 2] = color2.R;
                    }
                }
            }
            else
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int x = 0; x <= 3; x++)
                    {
                        Color color1 = etc1Pixel(r1, g1, b1, x, y, blockBottom, table1);
                        Color color2 = etc1Pixel(r2, g2, b2, x, y + 2, blockBottom, table2);

                        int offset1 = (y * 4 + x) * 4;
                        output[offset1] = color1.B;
                        output[offset1 + 1] = color1.G;
                        output[offset1 + 2] = color1.R;

                        int offset2 = ((y + 2) * 4 + x) * 4;
                        output[offset2] = color2.B;
                        output[offset2 + 1] = color2.G;
                        output[offset2 + 2] = color2.R;
                    }
                }
            }

            return output;
        }

        private static Color etc1Pixel(uint r, uint g, uint b, int x, int y, uint block, uint table)
        {
            int index = x * 4 + y;
            uint MSB = block << 1;

            int pixel = index < 8
                ? etc1LUT[table, ((block >> (index + 24)) & 1) + ((MSB >> (index + 8)) & 2)]
                : etc1LUT[table, ((block >> (index + 8)) & 1) + ((MSB >> (index - 8)) & 2)];

            r = saturate((int)(r + pixel));
            g = saturate((int)(g + pixel));
            b = saturate((int)(b + pixel));

            return Color.FromArgb((int)r, (int)g, (int)b);
        }

        private static byte saturate(int value)
        {
            if (value > 0xff) return 0xff;
            if (value < 0) return 0;
            return (byte)(value & 0xff);
        }

        private static int[] etc1Scramble(int width, int height)
        {
            //Maybe theres a better way to do this?
            int[] tileScramble = new int[((width / 4) * (height / 4))];
            int baseAccumulator = 0;
            int rowAccumulator = 0;
            int baseNumber = 0;
            int rowNumber = 0;

            for (int tile = 0; tile < tileScramble.Length; tile++)
            {
                if ((tile % (width / 4) == 0) && tile > 0)
                {
                    if (rowAccumulator < 1)
                    {
                        rowAccumulator += 1;
                        rowNumber += 2;
                        baseNumber = rowNumber;
                    }
                    else
                    {
                        rowAccumulator = 0;
                        baseNumber -= 2;
                        rowNumber = baseNumber;
                    }
                }

                tileScramble[tile] = baseNumber;

                if (baseAccumulator < 1)
                {
                    baseAccumulator++;
                    baseNumber++;
                }
                else
                {
                    baseAccumulator = 0;
                    baseNumber += 3;
                }
            }

            return tileScramble;
        }
        #endregion
    }

    class TextureUtils
    {
        /// <summary>
        ///     Gets a Bitmap from a RGBA8 Texture buffer.
        /// </summary>
        /// <param name="array">The Buffer</param>
        /// <param name="width">Width of the Texture</param>
        /// <param name="height">Height of the Texture</param>
        /// <returns></returns>
        public static Bitmap getBitmap(byte[] array, int width, int height)
        {
            Bitmap img = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData imgData = img.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(array, 0, imgData.Scan0, array.Length);
            img.UnlockBits(imgData);
            return img;
        }

        /// <summary>
        ///     Gets a RGBA8 Texture Buffer from a Bitmap.
        /// </summary>
        /// <param name="img">The Bitmap</param>
        /// <returns></returns>
        public static byte[] getArray(Bitmap img)
        {
            BitmapData imgData = img.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] array = new byte[imgData.Stride * img.Height];
            Marshal.Copy(imgData.Scan0, array, 0, array.Length);
            img.UnlockBits(imgData);
            return array;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace AMLUnpacker
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1 || args.Length > 2)
            {
                Console.WriteLine("AML_Arciver Unpacker - [3DS] True Remembrance || v1.0.0");
                Console.WriteLine("usage: AMLUnpacker infile [outdir]");
                Console.WriteLine("outdir - default '%infile%_unpack'");
                return;
            }

            string infile = args[0];
            if (!File.Exists(infile))
            {
                Console.WriteLine("File Not Found: {0}", infile);
                return;
            }

            string outdir = args[0] + "_unpack";
            if (args.Length == 2)
            {
                outdir = args[1];
            }
            Directory.CreateDirectory(outdir);

            AMLUnpack(infile, outdir);
        }

        public static void AMLUnpack(string path, string outdir)
        {
            using (BinaryReader r = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                byte[] magic = r.ReadBytes(11);
                if (!magic.SequenceEqual(ASCIIEncoding.ASCII.GetBytes("AML_Arciver")))
                {
                    Console.WriteLine("Error: not AML_Arciver - {0}", path);
                    return;
                }

                r.BaseStream.Seek(0x80, SeekOrigin.Begin);
                uint filecount = r.ReadUInt32();
                Console.WriteLine("File count: {0}", filecount);

                for (uint i = 0; i < filecount; i++)
                {
                    string filename = CstrToString(r.ReadBytes(0x40));
                    Console.WriteLine(filename);
                    filename = filename.Replace('/', '_');

                    string outpath = Path.Combine(outdir, filename) + ".png";
                    Console.WriteLine("> " + outpath);

                    int filesize = (int)r.ReadUInt32();

                    byte[] data = Decompress(r.ReadBytes(filesize));
                    // https://www.3dbrew.org/wiki/CTPK#Texture_Info_Entry
                    int texDataSize = (int)BitConverter.ToUInt32(data, 2 * 4);
                    int width       =      BitConverter.ToUInt16(data, 5 * 4);
                    int height      =      BitConverter.ToUInt16(data, 5 * 4 + 1 * 2);

                    byte[] texData = new byte[texDataSize];
                    Buffer.BlockCopy(data, 36, texData, 0, texDataSize);
                    data = texData;

                    Bitmap bitmap = ETC1.decode(data, width, height, true);
                    bitmap.Save(outpath);
                }
            }
        }

        public static string CstrToString(byte[] data)
        {
            int end = Array.IndexOf<byte>(data, 0x00);
            if (end == -1) end = data.Length;
            return ASCIIEncoding.ASCII.GetString(data, 0, end);
        }

        public static byte[] Decompress(byte[] data)
        {
            List<byte> bout = new List<byte>();
            byte marker = data[0];

            int i = 1;
            while (i < data.Length)
            {
                if (data[i] == marker && data[i + 1] == marker)
                {
                    bout.Add(data[i]);
                    i++;
                }
                else if (data[i] == marker)
                {
                    i++;
                    int offset = data[i];
                    if (offset > marker)
                    {
                        offset--;
                    }
                    i++;
                    int count = data[i];
                    int index = bout.Count - offset;

                    for (int j = index; j < index + count; j++)
                    {
                        bout.Add(bout[j]);
                    }
                }
                else
                {
                    bout.Add(data[i]);
                }
                i++;
            }

            return bout.ToArray();
        }
    }
}

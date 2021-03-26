using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Labb3_Console
{
    public class PNG_BMP_Handler
    {
        public static bool FindCorrectFormat(ref string path)
        {
            string pngCorrection;
            string bmpCorrection;

            try
            {
                if (Path.GetExtension(path) == string.Empty)
                {
                    pngCorrection = path + ".png";
                    bmpCorrection = path + ".bmp";
                }
                else
                {
                    pngCorrection = path.Replace(Path.GetExtension(path), ".png");
                    bmpCorrection = path.Replace(Path.GetExtension(path), ".bmp");
                }
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (!File.Exists(pngCorrection) && !File.Exists(bmpCorrection))
            {
                return false;   
            }
            else
            {
                if (File.Exists(pngCorrection))
                    path = pngCorrection;
                else if (File.Exists(bmpCorrection))
                    path = bmpCorrection;

                return true;
            }
        }

        public static string GetPngResolution(FileStream fs)
        {
            byte[] buffer = new byte[8];

            fs.Read(buffer, 0, 8);

            // 8-byte PNG file signature in decimal values: 137 80 78 71 13 10 26 10
            if (buffer[0] != 137 || buffer[1] != 80 || buffer[2] != 78 || buffer[3] != 71 ||
                buffer[4] != 13 || buffer[5] != 10 || buffer[6] != 26 || buffer[7] != 10)
                return null;

            // Names: Width & Height => Size: 8 bytes => Offset: 0x10
            fs.Position = 0x10;
            fs.Read(buffer, 0, 8);

            // Byte Order: Big-endian
            int width = BitConverter.ToInt32(new byte[] { buffer[3], buffer[2], buffer[1], buffer[0] });
            int height = BitConverter.ToInt32(new byte[] { buffer[7], buffer[6], buffer[5], buffer[4] });

            return $"{width}x{height}";
        }

        public static string GetBmpResolution(FileStream fs)
        {
            byte[] buffer = new byte[8];

            fs.Read(buffer, 0, 2);

            // 2-byte BMP file signature in decimal values: 66 77
            if (buffer[0] != 66 || buffer[1] != 77)
                return null;

            // Names: Width & Height => Size: 8 bytes => Offset: 0x12
            fs.Position = 0x12;
            fs.Read(buffer, 0, 8);

            // Byte Order: Little-endian
            int width = BitConverter.ToInt32(buffer[..4]);
            int height = BitConverter.ToInt32(buffer[4..]);

            return $"{width}x{height}";
        }

        public static List<PNG_Chunk> FindPNGChunks(FileStream fs)
        {
            fs.Position = 0;

            byte[] bytes = new byte[fs.Length];

            fs.Read(bytes, 0, (int)fs.Length);

            List<PNG_Chunk> chunks = new List<PNG_Chunk>()
               {
                   new PNG_Chunk("IHDR"), new PNG_Chunk("PLTE"),new PNG_Chunk("IEND"),new PNG_Chunk("cHRM"),new PNG_Chunk("gAMA"),new PNG_Chunk("iCCP"),
                   new PNG_Chunk("sBIT"),new PNG_Chunk("sRGB"),new PNG_Chunk("bKGD"),new PNG_Chunk("hIST"),new PNG_Chunk("tRNS"),new PNG_Chunk("pHYs"),
                   new PNG_Chunk("sPLT"),new PNG_Chunk("tIME"),new PNG_Chunk("iTXt"),new PNG_Chunk("tEXt"),new PNG_Chunk("zTXt")
               };

            FindAllSingleOccuranceChunks(bytes, chunks);

            FindAllIDATChunks(bytes, chunks);

            SortChunks(chunks);

            string[] errorMessages;

            if (!ContainsCriticalChunks(chunks, out errorMessages))
            {
                Console.WriteLine();
                foreach (var msg in errorMessages)
                {
                    if (msg != null)
                        Console.WriteLine(msg);
                }
                return null;
            }

            ComputeChunkLengths(bytes, chunks);

            return chunks; 
        }

        static void FindAllIDATChunks(byte[] bytes, List<PNG_Chunk> chunks)
        {
            byte count = 0;

            for (int i = 0; i < bytes.Length; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    if (bytes[i + j] == "IDAT"[j])
                        count++;
                    else
                    {
                        count = 0;
                        break;
                    }
                }
                if (count == 4)
                {
                    chunks.Add(new PNG_Chunk("IDAT", i));
                    count = 0;
                }
            }
        }

        static void FindAllSingleOccuranceChunks(byte[] bytes, List<PNG_Chunk> chunks)
        {
            byte count = 0;

            for (int k = 0; k < chunks.Count; k++)
            {
                var chunk = chunks[k];
                for (int i = 0; i < bytes.Length; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        if (bytes[i + j] == chunk.FieldType[j])
                            count++;
                        else
                        {
                            count = 0;
                            break;
                        }
                    }
                    if (count == 4)
                    {
                        chunk.StartIndex = i;
                        break;
                    }
                }

                if (count < 4)
                    chunks[k] = null;

                count = 0;
            }
            chunks.RemoveAll(c => c == null);
        }

        static bool ContainsCriticalChunks(List<PNG_Chunk> chunks, out string[] errorMessages)
        {
            bool containsIHDR = false;
            bool containsIDAT = false;
            bool containsIEND = false;
            bool misplacedIHDR = false;
            bool misplacedIEND = false;

            errorMessages = new string[6];

            if (chunks[0].FieldType != "IHDR")
                misplacedIHDR = true;
            if (chunks[chunks.Count - 1].FieldType != "IEND")
                misplacedIEND = true;

            foreach (var chunk in chunks)
            {
                string fType = chunk.FieldType;

                if (fType == "IHDR")
                    containsIHDR = true;
                else if (fType == "IDAT")
                    containsIDAT = true;
                else if (fType == "IEND")
                    containsIEND = true;
            }

            if (!misplacedIHDR && !misplacedIEND && containsIHDR && containsIDAT && containsIEND)
                return true;

            errorMessages[0] = "Error: Invalid PNG file:";
            if (!containsIHDR)
                errorMessages[1] = "Missing critical IHDR chunk";
            if (!containsIDAT)
                errorMessages[2] = "Missing critical IDAT chunk";
            if (!containsIEND)
                errorMessages[3] = "Missing critical IEND chunk";
            if (misplacedIHDR)
                errorMessages[4] = "IHDR chunk does not appear first";
            if (misplacedIEND)
                errorMessages[5] = "IEND chunk does not appear last";

            return false;
        }

        static void SortChunks(List<PNG_Chunk> chunks)
        {
            // Bubble sort
            for (int j = 0; j <= chunks.Count - 2; j++)
            {
                for (int i = 0; i <= chunks.Count - 2; i++)
                {
                    if (chunks[i].StartIndex > chunks[i + 1].StartIndex)
                    {
                        PNG_Chunk tempChunk = chunks[i + 1];
                        chunks[i + 1] = chunks[i];
                        chunks[i] = tempChunk;
                    }
                }
            }
        }

        static void ComputeChunkLengths(byte[] bytes, List<PNG_Chunk> chunks)
        {
            foreach (var chunk in chunks)
            {
                var b1 = bytes[chunk.StartIndex - 4];
                var b2 = bytes[chunk.StartIndex - 3];
                var b3 = bytes[chunk.StartIndex - 2];
                var b4 = bytes[chunk.StartIndex - 1];
                int length = BitConverter.ToInt32(new byte[] { b4, b3, b2, b1 });
                chunk.Length = length;
            }
        }
    }
}

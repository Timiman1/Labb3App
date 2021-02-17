using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Labb3_Console
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Please enter a valid file path with the extension .png or .bmp:");
            Console.WriteLine("Defualt/preset images : kling&klang.png, polskKorv.png, faultyPNG.png, pinguin.bmp, rulltarta.bmp");

            string path = Console.ReadLine();

            Console.WriteLine();

            if ((!File.Exists(path)) || (Path.GetExtension(path) != ".png" && Path.GetExtension(path) != ".bmp"))
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
                    Console.WriteLine("File not found!");

                    Console.WriteLine("\nPress any key to continue . . .");
                    Console.ReadKey();
                    return;
                }

                if (!File.Exists(pngCorrection) && !File.Exists(bmpCorrection))
                {
                    Console.WriteLine("File not found!");

                    Console.WriteLine("\nPress any key to continue . . .");
                    Console.ReadKey();
                    return;
                }
                else
                {
                    if (File.Exists(pngCorrection))
                        path = pngCorrection;
                    else if (File.Exists(bmpCorrection))
                        path = bmpCorrection;    
                }
            }

            using (FileStream fs = File.OpenRead(path))
            {
                if (Path.GetExtension(path) == ".png")
                {
                    string resolution = GetPngResolution(fs);

                    if (resolution == null)
                    {
                        Console.WriteLine("This is not a valid .png file!");
                        return;
                    }

                    Console.WriteLine("This is a .png image. Resolution: {0} pixels.", resolution);

                    ComputeAndPrintPNGChunkData(fs);
                }
                else
                {
                    string resolution = GetBmpResolution(fs);

                    if (resolution == null)
                    {
                        Console.WriteLine("This is not a valid .bmp file!");
                        return;
                    }

                    Console.WriteLine("This is a .bmp image. Resolution: {0} pixels.", resolution);
                }
            }

            Console.WriteLine("\nPress any key to continue . . .");
            Console.ReadKey();
        }

        static void ComputeAndPrintPNGChunkData(FileStream fs)
        {
            fs.Position = 0;

            byte[] bytes = new byte[fs.Length];

            fs.Read(bytes, 0, (int)fs.Length);

            List<int[]> idatChunkData = new List<int[]>();

            //Dictionary<string: Field type, { int: StartIndex, int: Chunk Size}>
            Dictionary<string, int[]> chunks = new Dictionary<string, int[]>()
            { 
                {"IHDR", new int[] { -1, -1 } }, {"PLTE", new int[] { -1, -1 } }, 
                {"IEND", new int[] { -1, -1 } }, {"cHRM", new int[] { -1, -1 } },
                { "gAMA", new int[] { -1, -1 } }, {"iCCP", new int[] { -1, -1 } }, {"sBIT", new int[] { -1, -1 } },
                {"sRGB", new int[] { -1, -1 } }, {"bKGD", new int[] { -1, -1 } }, {"hIST", new int[] { -1, -1 } },
                { "tRNS", new int[] { -1, -1 } }, {"pHYs", new int[] { -1, -1 } }, {"sPLT", new int[] { -1, -1 } }, 
                {"tIME", new int[] { -1, -1 } }, {"iTXt", new int[] { -1, -1 } }, {"tEXt", new int[] { -1, -1 } },
                { "zTXt",new int[] { -1, -1 } } 
            };

            foreach (var chunkType in chunks.Keys.ToArray())
                CalculateAndSetChunkStartIndex(chunkType, bytes, chunks);

            CalculateAndSetIDATChunksStartIndices(bytes, idatChunkData);

            var tempfieldList = chunks.Keys.ToList();
            var tempdataList = chunks.Values.ToList();

            foreach (var data in idatChunkData)
            {
                tempfieldList.Add("IDAT");
                tempdataList.Add(data);
            }

            var chunkData = tempdataList.ToArray();
            var fieldTypes = tempfieldList.ToArray();

            SortChunkDataAndFieldTypes(chunkData, fieldTypes);

            string[] errorMessages;

            if (!PNGContainsCriticalChunks(fieldTypes, out errorMessages))
            {
                Console.WriteLine();
                foreach (var msg in errorMessages)
                {
                    if (msg != null)
                        Console.WriteLine(msg);
                }
                return;
            }

            CalculateAndSetAllChunkDataSizes(bytes, chunkData); 

            Console.WriteLine("\nFound chunks: ");

            for (int i = 0; i < chunkData.Length; i++)
                Console.WriteLine("Field type: {0}    Size = {2} bytes", fieldTypes[i], chunkData[i][0], chunkData[i][1]);
        }

        static void CalculateAndSetIDATChunksStartIndices(byte[] bytes, List<int[]> idatChunks)
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
                    idatChunks.Add(new int[] { i, -1 });
                }
            }
        }

        static void CalculateAndSetChunkStartIndex(string chunkType, byte[] bytes, Dictionary<string, int[]> chunks)
        {
            byte count = 0;

            for (int i = 0; i < bytes.Length; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    if (bytes[i + j] == chunkType[j])
                        count++;
                    else
                    {
                        count = 0;
                        break;
                    }
                }
                if (count == 4)
                {
                    chunks[chunkType][0] = i;
                    return;
                }
            }
            chunks.Remove(chunkType);
        }

        static bool PNGContainsCriticalChunks(string[] fieldTypes, out string[] errorMessages)
        {
            bool containsIHDR = false;
            bool containsIDAT = false;
            bool containsIEND = false;
            bool misplacedIHDR = false;
            bool misplacedIEND = false;

            errorMessages = new string[6];

            if (fieldTypes[0] != "IHDR")
                misplacedIHDR = true;
            if (fieldTypes[fieldTypes.Length - 1] != "IEND")
                misplacedIEND = true;

            foreach (var fType in fieldTypes)
            {
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
            if(!containsIDAT)
                errorMessages[2] = "Missing critical IDAT chunk";
            if (!containsIEND)
                errorMessages[3] = "Missing critical IEND chunk";
            if (misplacedIHDR)
                errorMessages[4] = "IHDR chunk does not appear first";
            if (misplacedIEND)
                errorMessages[5] = "IEND chunk does not appear last";

            return false;
        }

        static void SortChunkDataAndFieldTypes(int[][] chunkData, string[] fieldTypes)
        {
            // Bubble sort
            for (int j = 0; j <= chunkData.Length - 2; j++)
            {
                for (int i = 0; i <= chunkData.Length - 2; i++)
                {
                    if (chunkData[i][0] > chunkData[i + 1][0])
                    {
                        int[] tempData = chunkData[i + 1];
                        string tempType = fieldTypes[i + 1];

                        chunkData[i + 1] = chunkData[i];
                        fieldTypes[i + 1] = fieldTypes[i];

                        chunkData[i] = tempData;
                        fieldTypes[i] = tempType;
                    }
                }
            }
        }

        static void CalculateAndSetAllChunkDataSizes(byte[] bytes, int[][] chunkData)
        {

            for (int i = 0; i < chunkData.Length; i++)
            {
                var b1 = bytes[chunkData[i][0] - 4];
                var b2 = bytes[chunkData[i][0] - 3];
                var b3 = bytes[chunkData[i][0] - 2];
                var b4 = bytes[chunkData[i][0] - 1];
                int length = BitConverter.ToInt32(new byte[] { b4, b3, b2, b1 });
                chunkData[i][1] = length;
            }
        }

        static string GetPngResolution(FileStream fs)
        {
            byte[] bytes = new byte[8];

            fs.Read(bytes, 0, 8);

            // 8-byte PNG file signature in decimal values: 137 80 78 71 13 10 26 10
            if (bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71 ||
                bytes[4] != 13 || bytes[5] != 10 || bytes[6] != 26 || bytes[7] != 10)
                return null;

            // Names: Width & Height => Size: 8 bytes => Offset: 0x10
            fs.Position = 0x10;
            fs.Read(bytes, 0, 8);

            // Byte Order: Big-endian
            int width = BitConverter.ToInt32(new byte[] { bytes[3], bytes[2], bytes[1], bytes[0] });
            int height = BitConverter.ToInt32(new byte[] { bytes[7], bytes[6], bytes[5], bytes[4] });

            return $"{width}x{height}";
        }

        static string GetBmpResolution(FileStream fs)
        {
            byte[] bytes = new byte[8];

            fs.Read(bytes, 0, 2);

            // 2-byte BMP file signature in decimal values: 66 77
            if (bytes[0] != 66 || bytes[1] != 77)
                return null;
            
            // Names: Width & Height => Size: 8 bytes => Offset: 0x12
            fs.Position = 0x12;
            fs.Read(bytes, 0, 8);

            // Byte Order: Little-endian
            int width = BitConverter.ToInt32(bytes[..4]);
            int height = BitConverter.ToInt32(bytes[4..]);

            return $"{width}x{height}";
        }
    }
}

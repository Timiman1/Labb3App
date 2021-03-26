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
                if (!PNG_BMP_Handler.FindCorrectFormat(ref path))
                {
                    Console.WriteLine("File not found!\nPress any key to continue . . .");
                    Console.ReadKey();
                    return;
                }
            }


            using (FileStream fs = File.OpenRead(path))
            {
                if (Path.GetExtension(path) == ".png")
                {
                    string resolution = PNG_BMP_Handler.GetPngResolution(fs);

                    if (resolution == null)
                    {
                        Console.WriteLine("This is not a valid .png file!");
                        return;
                    }

                    Console.WriteLine("This is a .png image. Resolution: {0} pixels.", resolution);

                    List<PNG_Chunk> chunks = PNG_BMP_Handler.FindPNGChunks(fs);

                    if (chunks != null)
                    {
                        Console.WriteLine("\nFound chunks: ");
                        foreach (var chunk in chunks)
                        {
                            Console.WriteLine("Field type: {0}    Length = {1} bytes", chunk.FieldType, chunk.Length);
                        }
                    }
                }
                else
                {
                    string resolution = PNG_BMP_Handler.GetBmpResolution(fs);

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
    }
}

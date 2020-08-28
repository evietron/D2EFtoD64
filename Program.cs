﻿using System;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace D2EFtoD64
{
    class Program
    {
        static byte[] banks;

        static void scanBanks(FileStream fs)
        {
            int maxBank = 0;
            byte[] header = new byte[0x10];
            fs.Seek(0x40, SeekOrigin.Begin);
            while (fs.Position != fs.Length)
            {
                fs.Read(header);
                int bank = header[11];
                int len = header[15] + (header[14] << 8);
                fs.Seek(len, SeekOrigin.Current);
                maxBank = Math.Max(maxBank, bank + 1);
            }
            banks = new byte[maxBank * 0x4000];
        }

        static void readBanks(FileStream fs)
        {
            byte[] header = new byte[0x10];
            fs.Seek(0x40, SeekOrigin.Begin);
            while (fs.Position != fs.Length)
            {
                fs.Read(header);
                bool high = header[12] != 0x80;
                int bank = header[11];
                int len = header[15] + (header[14] << 8);
                fs.Read(banks, (bank * 0x4000) + (high ? 0x2000 : 0), len);
            }
        }

        static int search(byte[] src, byte[] pattern)
        {
            int c = src.Length - pattern.Length + 1;
            int j;
            for (int i = 0; i < c; i++)
            {
                if (src[i] != pattern[0]) continue;
                for (j = pattern.Length - 1; j >= 1 && src[i + j] == pattern[j]; j--) ;
                if (j == 0) return i;
            }
            return -1;
        }

        static void parse(string filename)
        {
            string prefix = filename.Substring(0, filename.LastIndexOf('.'));
            FileStream fs = File.Open(filename, FileMode.Open);
            string suffix = "d64";
            if (fs.Length > 664 * 254)
            {
                suffix = "d81";
            }
            if (fs.Length > 3160 * 254)
            {
                throw new Exception("Too big!");
            }

            ProcessStartInfo psi = new ProcessStartInfo("c1541", "-format d2ef,df " + suffix + " \"" + prefix + "." + suffix + "\"");
            psi.RedirectStandardOutput = true;
            Process p = Process.Start(psi);
            p.WaitForExit();

            scanBanks(fs);
            readBanks(fs);

            fs.Close();

            int bankOffset = 0x4362;
            int fileCount = 0;
            while (banks[bankOffset + 0] != '\0')
            {
                if (banks[bankOffset + 0] != '$')
                {
                    int len = 0;
                    while (len < 16 && banks[bankOffset + len] != '\0')
                    {
                        len++;
                    }
                    byte[] bytename = new byte[16];
                    Array.Copy(banks, bankOffset, bytename, 0, 16);
                    bool foundEnd = false;
                    for (int i = 0; i < 16; i++)
                    {
                        if (bytename[i] == '\0')
                        {
                            foundEnd = true;
                        }
                        if (foundEnd)
                        {
                            bytename[i] = 0xa0;
                        }
                    }
                    string name = Encoding.UTF8.GetString(banks, bankOffset, len);
                    int bank = banks[bankOffset + 16] + (banks[bankOffset + 17] << 8) + 1;
                    int offset = banks[bankOffset + 18] + (banks[bankOffset + 19] << 8);
                    int addr = banks[bankOffset + 20] + (banks[bankOffset + 21] << 8);
                    int size = banks[bankOffset + 22] + (banks[bankOffset + 23] << 8);

                    string converted = "c0nv3r4ted1x2x3x";
                    FileStream fsw = File.OpenWrite(converted);
                    fsw.WriteByte((byte)addr);
                    fsw.WriteByte((byte)(addr >> 8));
                    fsw.Write(banks, bank * 0x4000 + (offset & 0x3fff), size);
                    fsw.Close();

                    if (fileCount >= 296)
                    {
                        fileCount -= 296;
                        prefix += "2";
                        psi = new ProcessStartInfo("c1541", "-format d2ef,df " + suffix + " \"" + prefix + "." + suffix + "\"");
                        psi.RedirectStandardOutput = true;
                        p = Process.Start(psi);
                        p.WaitForExit();
                    }

                    psi = new ProcessStartInfo("c1541", "\"" + prefix + "." + suffix + "\" -write " + converted);
                    psi.RedirectStandardOutput = true;
                    p = Process.Start(psi);
                    
                    p.WaitForExit();

                    new FileInfo(converted).Delete();

                    FileStream fsr = File.Open(prefix + "." + suffix, FileMode.Open);
                    byte[] dataread = new byte[fsr.Length];
                    fsr.Read(dataread);
                    int found = search(dataread, Encoding.UTF8.GetBytes(converted.ToUpper()));
                    fileCount++;

                    if (found != -1)
                    {
                        fsr.Seek(found, SeekOrigin.Begin);
                        fsr.Write(bytename);
                    }
                    else
                    {
                        throw new Exception("Can't find written file! (" + name + ")");
                    }
                    fsr.Close();
                }
                bankOffset += 24;
            }

            fs.Close();
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("D2EFtoD64 <filename.crt>");
            }
            else
            {
                try
                {
                    Console.WriteLine("CONVERTING: " + args[0]);
                    parse(args[0]);
                } catch (Exception e)
                {
                    Console.WriteLine("FAILED: " + e.Message);
                    new FileInfo("tempfile").Delete();
                    new FileInfo(args[0].Substring(0, args[0].LastIndexOf('.')) + ".d64").Delete();
                    new FileInfo(args[0].Substring(0, args[0].LastIndexOf('.')) + ".d81").Delete();
                }
            }
        }
    }
}

//cil : -a všechny v adr (vychozi)
//      -f konkretni soubor

//prodleva (v sekundách) : -d , jinak vychozich 60 sekund

//build with
// dotnet publish -p:PublishSingleFile=true -r win-x64 -c Release --self-contained false

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Log_Splitter
{
    internal class Program
    {
        enum Mode { SingleFile, AllFiles, NotFound, Help}

        const int DefaultDelay = 60;
        static int Main(string[] args)
        {
            (Mode mode, string filePath, int delay) = GetParams();

            switch (mode)
            {
                case Mode.Help:
                    PrintHelp();
                    return 0;
                case Mode.NotFound:
                    Console.WriteLine("File not found!");
                    return 1;
                case Mode.SingleFile:
                    SplitFile(filePath, delay);
                    break;
                default:
                    foreach (string file in Directory.GetFiles(Directory.GetCurrentDirectory(), "*.LOG"))
                        SplitFile(file, delay);
                    break;
                    
            }
            Console.WriteLine();
            Console.WriteLine("Splitting finished");
            return 0;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Safecast log splitter - splits LOG file into chunks on time gap greater than specified delay");
            Console.WriteLine();
            Console.WriteLine("-d : set splitting minimal delay (in seconds), default is 60");
            Console.WriteLine("-f : specify file to split");
            Console.WriteLine("     if not specified, all *.LOG files in current directory will be processed");
            Console.WriteLine("-h : print this message");
        }

        static void SplitFile(string fullPath, int maxDelay)
        {
            Console.WriteLine($"Processing file \"{fullPath}\"");
            Console.WriteLine();
            try
            {
                string dir = Path.GetDirectoryName(fullPath);
                string inFileName = Path.GetFileName(fullPath);
                
                
                int counter = 1;

                DateTime last = DateTime.MaxValue;
                using (StreamReader sr = new StreamReader(fullPath))
                {
                    StreamWriter sw = MakeNewOutfile(inFileName, dir, counter);

                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        //Regex rx = new Regex(
                        //    @"\$BNRDD,\d{4},(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z)",
                        //    RegexOptions.Compiled | RegexOptions.IgnoreCase
                        //);
                        //Match match = rx.Match(line);

                        //if (!match.Success)
                        //    continue;
                        if (line.Length < 31)
                            continue;

                        string isodate = line.Substring(11, 20);

                        DateTime current;
                        if (
                            //!DateTime.TryParse(match.Groups[1].Value, out current) 
                            !DateTime.TryParse(isodate, out current)
                            || current > DateTime.Now
                            )
                            continue;
                        

                        TimeSpan delay = last == DateTime.MaxValue ? TimeSpan.Zero : current - last;

                        if (delay <TimeSpan.Zero)
                        {
                            continue;
                        }
                        else if (delay.TotalSeconds < maxDelay)
                        {
                            sw.WriteLine(line);                            
                        }
                        else
                        {
                            CloseLogFile(last, sw);
                            counter++;
                            sw = MakeNewOutfile(inFileName, dir, counter);
                        }

                        last = current;
                    }
                    CloseLogFile(last, sw);
                }
            }
            catch (Exception e)
            {
                // Let the user know what went wrong.
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
            }
        }

        private static void CloseLogFile(DateTime last, StreamWriter sw)
        {
            string name = ((FileStream)sw.BaseStream).Name;
            sw.Close();
            sw.Dispose();
            File.SetLastWriteTime(name, last);
        }

        static string GetHeader()
        {
            return "# NEW LOG\n# format=1.3.5nano\n# deadtime=on";
        }
        static string GetOutFileName(string filename, int counter)
        {
            string nameOnly = Path.GetFileNameWithoutExtension(filename);
            string ext = Path.GetExtension(filename);
            return $"{nameOnly}-{counter:00}{ext}";
        }
        static StreamWriter MakeNewOutfile(string inFileName, string dir, int counter)
        {
            string outFileName = GetOutFileName(inFileName, counter);
            string outFileFullPath = Path.Combine(dir, outFileName);
            StreamWriter sw = new StreamWriter(outFileFullPath);
            sw.WriteLine(GetHeader());
            Console.WriteLine($"Creating file \"{outFileFullPath}\"");
            return sw;
        }

        static (Mode Mode, string FilePath, int Delay) GetParams()
        {
            string[] arguments = Environment.GetCommandLineArgs();

            Mode mode;
            int delay;
            string filePath;

            if (Array.IndexOf(arguments, "-h") != -1)
            {
                return (Mode.Help, String.Empty, 0);
            }


            int delayIdx = Array.IndexOf(arguments, "-d");
            if (delayIdx == -1)
            { 
                delay = DefaultDelay;
            }
            else
            {
                string delayStr =   arguments.Length >= delayIdx + 1
                                    ? arguments[delayIdx + 1] 
                                    : String.Empty;

                if (delayStr == String.Empty || !int.TryParse(delayStr, out delay))
                {
                    Console.WriteLine("Invalid delay value" + delayStr + "using dafult value instead");
                    delay = DefaultDelay;
                }
            }


            int fileIdx = Array.IndexOf(arguments, "-f");
            if (fileIdx == -1)
            {
                mode = Mode.AllFiles;
                filePath = String.Empty;
            }
            else
            {
                if (arguments.Length <= fileIdx + 1)
                {
                    mode = Mode.NotFound;
                    filePath = String.Empty;
                }
                else
                {
                    string rawFilename = arguments[fileIdx + 1];

                    string fullPath;
                    if (Path.IsPathFullyQualified(rawFilename))
                    {
                        fullPath = rawFilename;

                    }
                    else
                    {
                        fullPath = Path.Combine(Directory.GetCurrentDirectory(), rawFilename);
                    }

                    if (File.Exists(fullPath))
                    {
                        filePath = fullPath;
                        mode = Mode.SingleFile;
                    }
                    else
                    {
                        filePath = String.Empty;
                        mode = Mode.NotFound;
                    }
                }
            }

            return (mode, filePath, delay);

        }
    }
}


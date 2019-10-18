using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace RespaldoMensualSQLPCI
{

    class Program
    {
        static List<String> SOURCE_DBS = new List<string>();
        static String TARGET_DIRECTORY = @"C:\COPIED FILES\";        

        static readonly String BKP_ARGUMENTS = "SqlCmd /E /Q \"BACKUP DATABASE [db-name] TO DISK ='X:\\[target-directory]\\[db-name].bak'";
        static readonly String ZIP_ARGUMENTS = "PowerShell Compress-Archive -LiteralPath [source-files] -DestinationPath [destiny-file].zip";
        static readonly String AWS_ARGUMENTS_SDK = "aws-encryption-cli --encrypt --input [input] --master-keys key=[key-id] --metadata-output ../metadata --output [output]";
        static readonly String AWS_ARGUMENTS_S3 = "aws s3 cp [file] s3://respaldo-mensual/srvfile/";
        static readonly DateTime TODAY = DateTime.Today;
        static List<String> targetDirectories = new List<string>();
        static String TARGET_NAME = "";
        static readonly String LOG_FILE = @"log.txt";
        static string PARAMETERS_FILE;

        static void Main(string[] args)
        {
            try
            {
                Log(TODAY.ToString());

                if (args.Length == 0)
                {
                    System.Console.WriteLine("Ingrese la ruta del archivo de parámetros");
                }

                PARAMETERS_FILE = args[0];

                readParameters();

                // Copiar archivos a disco externo
                foreach (String sourceDB in SOURCE_DBS)
                {                    
                    String targetDirectory = TARGET_DIRECTORY + sourceDB;
                    targetDirectories.Add(targetDirectory);
                    Backup(sourceDB);
                }
                CompressFiles();
                EncryptFile();
                UploadFile();
            }
            catch (Exception e)
            {
                Log(e.Message, EventLogEntryType.Error);
                Log(e.Message);
                throw e;
            }
        }

        static void readParameters()
        {
            try
            {
                FileStream fileStream = new FileStream(PARAMETERS_FILE, FileMode.Open);

                using (StreamReader reader = new StreamReader(fileStream))
                {
                    while (reader.Peek() >= 0)
                    {
                        string line = reader.ReadLine();

                        string[] tokens = line.Split('=');

                        if (tokens.Length != 2)
                        {
                            String msg = "Formato no válido. Los parámetros deben ser especificados en la forma: [NOMBRE] = [VALOR]";
                            Log(msg, EventLogEntryType.Error);
                            Log(msg);
                            throw new System.ApplicationException(msg);
                        }

                        switch (tokens[0])
                        {                            
                            case "TARGET_DIRECTORY":
                                TARGET_DIRECTORY = tokens[1];
                                string year = TODAY.Year.ToString().Remove(0, 2);
                                string month = (TODAY.Month - 1).ToString();
                                if (TODAY.Month - 1 < 10)
                                {
                                    month = "0" + month;
                                }
                                TARGET_NAME = " \"" + TARGET_DIRECTORY + year + month + ".zip" + "\" ";
                                break;
                            case "SOURCE_DBS":
                                string[] tokens2 = tokens[1].Split(';');
                                foreach (String path in tokens2)
                                {
                                    if (path.EndsWith("\\"))
                                    {
                                        SOURCE_DBS.Add(path.TrimEnd('\\'));
                                    }
                                    else
                                    {
                                        SOURCE_DBS.Add(path);
                                    }
                                }
                                break;
                            default:
                                String msg = "Parámetro no válido. Valores aceptados: SOURCE_DIRECTORIES, TARGET_DIRECTORY";
                                Log(msg, EventLogEntryType.Error);
                                Log(msg);
                                throw new System.ApplicationException(msg);
                        }
                    }
                }
            }
            catch (FileNotFoundException e)
            {
                String msg = "El archivo de parámetros 'parameters.txt' no existe. Debe crear este archivo en la ruta donde se encuentra el ejecutable del aplicativo.";
                Log(msg, EventLogEntryType.Error);
                Log(msg);
                throw new System.ApplicationException(msg);
            }
            catch (FormatException e2)
            {
                String msg = "Formato no válido. Los parámetros deben ser especificados en la forma: [NOMBRE] = [VALOR]";
                Log(msg, EventLogEntryType.Error);
                Log(msg);
                throw new System.ApplicationException(msg);
            }
        }

        static void Backup(string db)
        {
            // Use ProcessStartInfo class
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = false;

            startInfo.CreateNoWindow = false; //not diplay a windows
            startInfo.WindowStyle = ProcessWindowStyle.Normal; //not diplay a windows            

            startInfo.Verb = "runas";
            //Give the name as Xcopy
            startInfo.FileName = "xcopy";

            //Send the Source and destination as Arguments to the process            
            String commands = BKP_ARGUMENTS.Replace("[db-name]", db).Replace("[target-directory]", TARGET_DIRECTORY);

            ProcessStartInfo commandsToRun = new ProcessStartInfo("cmd.exe", @"/c " + commands);

            Process process2 = new Process();

            commandsToRun.UseShellExecute = false;
            commandsToRun.CreateNoWindow = false; //not diplay a windows
            commandsToRun.WindowStyle = ProcessWindowStyle.Normal; //not diplay a windows
            commandsToRun.RedirectStandardError = true;
            commandsToRun.RedirectStandardOutput = true;

            process2.StartInfo = commandsToRun;
            process2.Start();
            process2.WaitForExit();

            String msg = "BD " + db + " copiada exitosamente";
            Log(msg, EventLogEntryType.Information);
            Log(msg);

            string stdoutx = process2.StandardOutput.ReadToEnd();
            string stderrx = process2.StandardError.ReadToEnd();

            Console.WriteLine("Exit code : {0}", process2.ExitCode);
            Console.WriteLine("Stdout : {0}", stdoutx);
            Console.WriteLine("Stderr : {0}", stderrx);

        }

        static void CompressFiles()
        {
            string sourceName = "";
            foreach (String targetDirectory in targetDirectories)
            {
                sourceName += " \"" + targetDirectory + "\" ";
            }

            String commands = ZIP_ARGUMENTS.Replace("[source-files]", sourceName).Replace("[destiny-file]", TARGET_NAME);

            ProcessStartInfo commandsToRun = new ProcessStartInfo("cmd.exe", @"/c " + commands);

            Process process2 = new Process();

            commandsToRun.UseShellExecute = false;
            commandsToRun.CreateNoWindow = false; //not diplay a windows
            commandsToRun.WindowStyle = ProcessWindowStyle.Normal; //not diplay a windows
            commandsToRun.RedirectStandardError = true;
            commandsToRun.RedirectStandardOutput = true;

            process2.StartInfo = commandsToRun;
            process2.Start();
            process2.WaitForExit();

            String msg = "Archivos " + sourceName + " comprimidos exitosamente";
            Log(msg, EventLogEntryType.Information);
            Log(msg);

            string stdoutx = process2.StandardOutput.ReadToEnd();
            string stderrx = process2.StandardError.ReadToEnd();

            Console.WriteLine("Exit code : {0}", process2.ExitCode);
            Console.WriteLine("Stdout : {0}", stdoutx);
            Console.WriteLine("Stderr : {0}", stderrx);
        }

        static void EncryptFile()
        {
            ProcessStartInfo process = new ProcessStartInfo();
            process.UseShellExecute = false;
            process.CreateNoWindow = true; //not diplay a windows
            process.Verb = "runas";

            String commands = AWS_ARGUMENTS_SDK.Replace("[input]", TARGET_NAME).Replace("[key-id]", "0d371f34-4d7c-4579-88b5-b1a1b6bfc56f").Replace("[output]", TARGET_NAME.Replace(".zip", ".zip.encrypted"));

            ProcessStartInfo commandsToRun = new ProcessStartInfo("cmd.exe", @"/c " + commands);

            Process process2 = new Process();

            commandsToRun.UseShellExecute = false;
            commandsToRun.CreateNoWindow = false; //not diplay a windows
            commandsToRun.WindowStyle = ProcessWindowStyle.Normal; //not diplay a windows
            commandsToRun.RedirectStandardError = true;
            commandsToRun.RedirectStandardOutput = true;

            process2.StartInfo = commandsToRun;
            process2.Start();
            process2.WaitForExit();

            String msg = "Archivo " + TARGET_NAME.Replace(".zip", ".zip.encrypted") + " encriptado exitosamente";
            Log(msg, EventLogEntryType.Information);
            Log(msg);

            string stdoutx = process2.StandardOutput.ReadToEnd();
            string stderrx = process2.StandardError.ReadToEnd();

            Console.WriteLine("Exit code : {0}", process2.ExitCode);
            Console.WriteLine("Stdout : {0}", stdoutx);
            Console.WriteLine("Stderr : {0}", stderrx);
        }

        static void UploadFile()
        {
            ProcessStartInfo process = new ProcessStartInfo();
            process.UseShellExecute = false;
            process.CreateNoWindow = true; //not diplay a windows
            process.Verb = "runas";

            String commands = AWS_ARGUMENTS_S3.Replace("[file]", TARGET_NAME.Replace(".zip", ".zip.encrypted"));

            ProcessStartInfo commandsToRun = new ProcessStartInfo("cmd.exe", @"/c " + commands);

            Process process2 = new Process();

            commandsToRun.UseShellExecute = false;
            commandsToRun.CreateNoWindow = false; //not diplay a windows
            commandsToRun.WindowStyle = ProcessWindowStyle.Normal; //not diplay a windows
            commandsToRun.RedirectStandardError = true;
            commandsToRun.RedirectStandardOutput = true;

            process2.StartInfo = commandsToRun;
            process2.Start();
            process2.WaitForExit();

            String msg = "Archivo " + TARGET_NAME.Replace(".zip", ".zip.encrypted") + " subido exitosamente";
            Log(msg, EventLogEntryType.Information);
            Log(msg);

            string stdoutx = process2.StandardOutput.ReadToEnd();
            string stderrx = process2.StandardError.ReadToEnd();

            Console.WriteLine("Exit code : {0}", process2.ExitCode);
            Console.WriteLine("Stdout : {0}", stdoutx);
            Console.WriteLine("Stderr : {0}", stderrx);
        }

        static void Log(String message, EventLogEntryType level)
        {
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = "Trends";
                eventLog.WriteEntry(message, level, 9997, 19 /*Archive Task*/);
            }
        }

        static void Log(String message)
        {
            using (StreamWriter sw = File.AppendText(LOG_FILE))
            {
                sw.WriteLine(message);
            }
        }

    }

}

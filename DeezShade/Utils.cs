using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DeezShade {
    internal static class Utils {
        internal static string ProgramPath => Directory.GetParent(AppContext.BaseDirectory).FullName;

        internal static void ExitWithCode(ExitCode exitCode) {
            Services.SubProgram?.Dispose();
            Environment.Exit((int)exitCode);
        }

        internal static void WriteErrorAndExit(Exception exception) {
            Services.ErrorWriter.WriteLine(exception.Message);
            Services.ErrorWriter.WriteLine(exception.StackTrace);
            ExitWithCode(ExitCode.Error);
        }

        internal static void WriteErrorAndExit(params string[] exceptionMessages) {
            foreach (string message in exceptionMessages) {
                Services.ErrorWriter.WriteLine(message);
            }
            ExitWithCode(ExitCode.Error);
        }

        internal static void ParseCommandLineArguments(string[] args) {
            if (args.Length == 0 || args.ToList().FindIndex(x => x.StartsWith("--path")) == -1) {
                Console.Write("Enter the path to your game install: ");
                Services.Settings.GameInstall = Services.InReader.ReadLine();
            } else {
                var index = args.ToList().FindIndex(x => x.StartsWith("--path="));
                if (index != -1) {
                    Services.Settings.GameInstall = args[index].Replace("--path=", "");
                } else {
                    index = args.ToList().FindIndex(x => x == "--path");
                    Services.Settings.GameInstall = args[index + 1].Replace("\"", "");
                }
            }
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using HarmonyLib;

namespace DeezShade {
    public class Program {
        public static void Main(string[] args) {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Console.WriteLine($"DeezShade v{version}, by NotNet and friends");
            Console.WriteLine("Built for GShade v4.1.1.");

            Console.Write("Enter the path to your game install: ");
            var gameInstall = Console.ReadLine();

            // if gameInstall is the exe, get the directory it's in
            if (File.Exists(gameInstall)) {
                gameInstall = Path.GetDirectoryName(gameInstall);
            }

            // if we're in the root, go to game
            if (File.Exists(Path.Combine(gameInstall, "game", "ffxiv_dx11.exe"))) {
                gameInstall = Path.Combine(gameInstall, "game");
            }

            var tempPath = Path.GetTempPath() + "DeezShade/";

            Console.WriteLine("Setting up temporary directory...");
            if (!Directory.Exists(tempPath)) {
                Directory.CreateDirectory(tempPath);
            } else {
                foreach (var file in Directory.GetFiles(tempPath)) {
                    File.Delete(file);
                }
            }

            var installerUrl =
                "https://github.com/Mortalitas/GShade/releases/latest/download/GShade.Latest.Installer.exe";
            var zipUrl = "https://github.com/Mortalitas/GShade/releases/latest/download/GShade.Latest.zip";

            var exePath = tempPath + "GShade.Latest.Installer.exe";
            var zipPath = tempPath + "GShade.Latest.zip";

            Console.WriteLine("Downloading GShade installer...");
            using (var client = new WebClient()) {
                client.DownloadFile(installerUrl, exePath);
                client.DownloadFile(zipUrl, zipPath);
            }

            // I'm using the official GShade installer, am I not? :^
            var assembly = Assembly.LoadFile(exePath);
            var type = assembly.GetType("GShadeInstaller.App");

            // get presets & shaders
            type.GetField("_gsTempPath").SetValue(null, tempPath);
            type.GetField("_exeParentPath").SetValue(null, gameInstall);

            // Patch GShade from shutting off your computer (LMAO)
            Console.WriteLine("Patching GShade malware...");
            type.GetField("_instReady").SetValue(null, true); // wp
            var harmony = new Harmony("com.notnite.thanks-marot");
            var lolMethod = type.GetMethod("www", BindingFlags.Static | BindingFlags.NonPublic);
            harmony.Patch(lolMethod, new HarmonyMethod(typeof(Program).GetMethod(nameof(LolDetour))));

            Console.WriteLine("Requesting new files through GShade installer...");
            type.GetMethod("CopyZipDeployProcess").Invoke(null, null);
            type.GetMethod("PresetDownloadProcess").Invoke(null, null);
            type.GetMethod("PresetInstallProcess").Invoke(null, null);

            // File.Copy gives an access denied error, so let's make it ourself
            var src = tempPath + "gshade-shaders";
            var dst = Path.Combine(gameInstall, "gshade-shaders");

            void RecursiveClone(string path) {
                var path2 = Path.Combine(src, path);

                foreach (var file in Directory.GetFiles(path2)) {
                    var fileName = Path.GetFileName(file);

                    var srcFile = Path.Combine(path2, fileName);
                    var dstFile = Path.Combine(dst, path, fileName);
                    File.Copy(srcFile, dstFile, true);
                }

                foreach (var dir in Directory.GetDirectories(path2)) {
                    var dirName = Path.GetFileName(dir);
                    var lol = Path.Combine(dst, path, dirName);
                    Directory.CreateDirectory(lol);
                    RecursiveClone(Path.Combine(path, dirName));
                }
            }

            Console.WriteLine("Moving shaders to game directory...");
            RecursiveClone("");
            Directory.CreateDirectory(Path.Combine(gameInstall, "gshade-addons"));

            Console.Write("Use ReShade (y/n)? ");
            var useReShade = Console.ReadLine().ToLower() == "y";

            if (useReShade) {
                Console.WriteLine("Downloading ReShade...");
                var reshadeUrl = "http://static.reshade.me/downloads/ReShade_Setup_5.6.0_Addon.exe";
                var reshadePath = tempPath + "ReShade_Setup_5.6.0_Addon.exe";
                using (var client = new WebClient()) {
                    client.DownloadFile(reshadeUrl, reshadePath);
                }

                Console.WriteLine("Installing ReShade...");
                if (File.Exists(Path.Combine(gameInstall, "dxgi.dll"))) {
                    File.Move(Path.Combine(gameInstall, "dxgi.dll"), Path.Combine(gameInstall, "dxgi.dll.old"));
                }

                var reshadeProcess = new Process();
                reshadeProcess.StartInfo.FileName = reshadePath;
                reshadeProcess.StartInfo.Arguments =
                    $"\"{Path.Combine(gameInstall, "ffxiv_dx11.exe")}\" --api dxgi --headless";
                reshadeProcess.Start();

                var configPath = Path.Combine(gameInstall, "ReShade.ini");

                if (!File.Exists(configPath)) {
                    Console.WriteLine("Writing ReShade config...");
                    var configText = @"[GENERAL]
[GENERAL]
EffectSearchPaths=.\gshade-shaders\Shaders\**
TextureSearchPaths=.\gshade-shaders\Textures\**
".Trim();

                    File.WriteAllText(configPath, configText);
                }
            } else {
                Console.WriteLine("Extracting DLL and config...");
                var zip = ZipFile.OpenRead(zipPath);

                if (File.Exists(Path.Combine(gameInstall, "dxgi.dll"))) {
                    File.Move(Path.Combine(gameInstall, "dxgi.dll"), Path.Combine(gameInstall, "dxgi.dll.old"));
                }

                zip.GetEntry("GShade64.dll").ExtractToFile(Path.Combine(gameInstall, "dxgi.dll"), true);
                zip.GetEntry("GShade.ini").ExtractToFile(Path.Combine(gameInstall, "GShade.ini"), true);
            }

            Console.WriteLine("Done!\nSupport FOSS, and thank you for using DeezShade!\nPress any key to continue.");
            Console.ReadKey();
        }

        public static bool LolDetour() {
            // thank you for writing malware marot
            return false;
        }
    }
}

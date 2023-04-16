using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using HarmonyLib;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Documents;
using System.Collections.Generic;

namespace DeezShade {
    [Serializable]
    internal class Tag {
        internal string Name { get; set; }
    }

    [Serializable]
    internal class Tags {
        internal List<Tag> Items { get; set; }
    }

    public static class Program {
        public static void Main(string[] args) {
            TextWriter errorWriter = Console.Error;
            TextWriter outWriter = Console.Out;
            TextReader inWriter = Console.In;
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Console.WriteLine($"DeezShade v{version}, by NotNet and friends");
            Console.WriteLine("Built for GShade v4.1.1.");

            var gameInstall = "";

            if (args.Length == 0 || args.ToList().FindIndex(x => x.StartsWith("--path")) == -1) {
                Console.Write("Enter the path to your game install: ");
                gameInstall = inWriter.ReadLine();
            } else {
                var index = args.ToList().FindIndex(x => x.StartsWith("--path="));
                if (index != -1) {
                    gameInstall = args[index].Replace("--path=", "");
                } else {
                    index = args.ToList().FindIndex(x => x == "--path");
                    gameInstall = args[index + 1].Replace("\"", "");
                }
            }

            // if gameInstall is the exe, get the directory it's in
            if (File.Exists(gameInstall)) {
                gameInstall = Path.GetDirectoryName(gameInstall);
            }

            // if we're in the root, go to game
            if (File.Exists(Path.Combine(gameInstall, "game", "ffxiv_dx11.exe"))) {
                gameInstall = Path.Combine(gameInstall, "game");
            }

            var tempPath = $"{Path.GetTempPath()}DeezShade{Path.DirectorySeparatorChar}";

            #if !DEBUG
              outWriter.WriteLine("Setting up temporary directory...");
              if (!Directory.Exists(tempPath)) {
                  _ = Directory.CreateDirectory(tempPath);
              } else {
                  foreach (var file in Directory.GetFiles(tempPath)) {
                      File.Delete(file);
                  }
              }
            #endif

            string GenerateGShadeUrl() {
                const string domain = "gshade.org";
                var tree = new string[] { "releases", "GShade.Latest.Installer.exe" };
                return $"https://{domain}/{tree[0]}/{tree[1]}";
            }

            var installerUrl = GenerateGShadeUrl();
            var exePath = tempPath + "GShade.Latest.Installer.exe";

#if DEBUG
            if (!File.Exists(exePath)) {
                outWriter.WriteLine($"Downloading GShade installer from {installerUrl}...");
                using (var client = new WebClient()) {
                    client.DownloadFile(installerUrl, exePath);
                }
            }
#else
            outWriter.WriteLine("Downloading GShade installer...");
            using (var client = new WebClient()) {
                client.DownloadFile(installerUrl, exePath);
            }

            // I'm using the official GShade installer, am I not? :^
            var assembly = Assembly.LoadFile(exePath);
            var type = assembly.GetType("GShadeInstaller.App");

            // get presets & shaders
            type.GetField("_gsTempPath").SetValue(null, tempPath);
            type.GetField("_exeParentPath").SetValue(null, gameInstall);

            // Patch GShade from shutting off your computer (LMAO)
            outWriter.WriteLine("Patching GShade malware...");
            var harmony = new Harmony("com.notnite.thanks-marot");

            var getProcessesByName = typeof(Process).GetMethod("GetProcessesByName", new[] { typeof(string) });
            _ = harmony.Patch(getProcessesByName, new HarmonyMethod(typeof(Program).GetMethod(nameof(ProcessDetour))));

            outWriter.WriteLine("Requesting new files through GShade installer...");
            _ = type.GetMethod("CopyZipDeployProcess").Invoke(null, null);
            _ = type.GetMethod("PresetDownloadProcess").Invoke(null, null);
            _ = type.GetMethod("PresetInstallProcess").Invoke(null, null);
#endif
            // File.Copy gives an access denied error, so let's make it ourself
            var src = tempPath + "gshade-shaders";
            var dst = Path.Combine(gameInstall, "reshade-shaders");

            void RecursiveClone(string source, string destination) {
#if DEBUG
                outWriter.WriteLine($"destination: {source}");
                outWriter.WriteLine($"destination: {destination}");
#endif

                foreach (var file in Directory.GetFiles(source)) {
                    var fileName = Path.GetFileName(file);

                    var srcFile = Path.Combine(source, fileName);
                    var dstFile = Path.Combine(destination, fileName);
#if DEBUG
                    outWriter.WriteLine($"file: {file}");
                    outWriter.WriteLine($"fileName: {fileName}");
                    outWriter.WriteLine($"srcFile: {srcFile}");
                    outWriter.WriteLine($"dstFile: {dstFile}");
#endif
                    File.Copy(srcFile, dstFile, true);
                }

                foreach (var dir in Directory.GetDirectories(source)) {
                    var dirName = Path.GetFileName(dir);
                    var newDestination = Path.Combine(destination, dirName);
#if DEBUG
                    outWriter.WriteLine($"dir: {dir}");
                    outWriter.WriteLine($"dirName: {dirName}");
                    outWriter.WriteLine($"newDestination: {newDestination}");
                    outWriter.WriteLine($"Exists(newDestination): {Directory.Exists(newDestination)}");
#endif
                    if (!Directory.Exists(newDestination)) {
                        _ = Directory.CreateDirectory(newDestination);
                    }
                    RecursiveClone(dir, newDestination);
                }
            }

            outWriter.WriteLine("Moving shaders to game directory...");
            RecursiveClone(src,  dst);
            _ = Directory.CreateDirectory(Path.Combine(gameInstall, "reshade-addons"));

            RecursiveClone(Path.Combine(gameInstall, "gshade-presets"), Path.Combine(gameInstall, "reshade-presets"));

            string GetGitHubTagsUrl() {
                const string subdomain = "api";
                const string domain = "github.com";
                var tree = new string[] { "repos", "crosire", "reshade", "tags" };
                return $"https://{subdomain}.{domain}/{tree[0]}/{tree[1]}/{tree[2]}/{tree[3]}";
            }
            string GetReShadeLatestVersion() {
                var fallback = "5.7.0";
                try {
                    using (var client = new WebClient()) {
                        var tagsUri = GetGitHubTagsUrl();
#if DEBUG
                        outWriter.WriteLine($"tagsUri: {tagsUri}");
#endif
                        var jsonString = client.DownloadString(tagsUri);
                        var json =  JsonSerializer.Deserialize<Tags>(jsonString);
                        fallback = json.Items[0].Name.Replace("v", "");
                    }
                } catch (WebException exception) {
                    if ((exception.Response as HttpWebResponse)?.StatusCode == (HttpStatusCode)403) {
                        errorWriter.WriteLine($"{exception.Message} This is likely due to a rate limit.");
                    } else {
                        errorWriter.WriteLine(exception.Message);
                        errorWriter.WriteLine(exception.StackTrace);
                    }
                }
                return fallback;
            }
            string reshadeLatest = GetReShadeLatestVersion();
            string GenerateReShadeUrl() {
                const string domain = "reshade.me";
                var tree = new string[] { "downloads", "ReShade_Setup_", reshadeLatest, "_Addon.exe" };
                return $"http://{domain}/{tree[0]}/{tree[1]}{tree[2]}{tree[3]}";
            }

#if DEBUG
            var reshadePath = $"{tempPath}ReShade_Setup_{reshadeLatest}_Addon.exe";
            var reshadeUrl = GenerateReShadeUrl();
            if (!File.Exists(reshadePath)) {
                outWriter.WriteLine($"Downloading ReShade from {reshadeUrl}...");
                using (var client = new WebClient()) {
                    client.DownloadFile(reshadeUrl, reshadePath);
                }
            }
#else
            outWriter.WriteLine($"Downloading ReShade...");
            var reshadePath = tempPath + "ReShade_Setup_5.7.0_Addon.exe";
            var reshadeUrl = GenerateReShadeUrl();
            using (var client = new WebClient()) {
                client.DownloadFile(reshadeUrl, reshadePath);
            }
#endif

            outWriter.WriteLine("Installing ReShade...");
            if (File.Exists(Path.Combine(gameInstall, "dxgi.dll"))) {
                if (File.Exists(Path.Combine(gameInstall, "dxgi.dll.old"))) {
                    File.Delete(Path.Combine(gameInstall, "dxgi.dll"));
                } else {
                    File.Move(Path.Combine(gameInstall, "dxgi.dll"), Path.Combine(gameInstall, "dxgi.dll.old"));
                }
            }

            var reshadeProcess = new Process();
            reshadeProcess.StartInfo.FileName = reshadePath;
            reshadeProcess.StartInfo.Arguments =
                $"\"{Path.Combine(gameInstall, "ffxiv_dx11.exe")}\" --api dxgi --headless";
            _ = reshadeProcess.Start();

            var configPath = Path.Combine(gameInstall, "ReShade.ini");

            if (!File.Exists(configPath)) {
                outWriter.WriteLine("Writing ReShade config...");
                var configText = $"[GENERAL]{Environment.NewLine}[GENERAL]{Environment.NewLine}EffectSearchPaths=.\\reshade-shaders\\Shaders\\**{Environment.NewLine}TextureSearchPaths=.\\reshade-shaders\\Textures\\**{Environment.NewLine}".Trim();

                File.WriteAllText(configPath, configText);
            }

            outWriter.WriteLine("Done!\nSupport FOSS, and thank you for using DeezShade!\nPress any key to continue.");
            _ = Console.ReadKey();
        }

        public static bool ProcessDetour(ref Process[] __result, string processName) {
            if (processName == "GShade.Installer") {
                __result = Array.Empty<Process>();
                return false;
            }

            return true;
        }
    }
}

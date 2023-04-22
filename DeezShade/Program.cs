using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
#if !DEBUG
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
#endif
using System.Reflection;
using HarmonyLib;
using System.Text.RegularExpressions;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;

namespace DeezShade {
    [Serializable]
    public class Tag {
        public string Name { get; set; } = "";
    }

    enum ExitCode : int {
        Success = 0,
        Error = 1
    }

    public static class Program {
        private static readonly TextWriter errorWriter = Console.Error;
        private static readonly TextWriter outWriter = Console.Out;
        private static readonly TextReader inReader = Console.In;

        public static void Main(string[] args) {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var asseblyPath = Assembly.GetExecutingAssembly().Location;
            Console.WriteLine($"DeezShade v{version}, by NotNet and friends");
            Console.WriteLine("Built for GShade v4.1.1.");

            var gameInstall = "";

            // if gameInstall is the exe, get the directory it's in
            if (File.Exists(gameInstall)) {
                gameInstall = Path.GetDirectoryName(gameInstall);
            }

            // if we're in the root, go to game
            if (File.Exists(Path.Combine(gameInstall, "game", "ffxiv_dx11.exe"))) {
                gameInstall = Path.Combine(gameInstall, "game");
            }

            var tempPath = $"{Path.GetTempPath()}DeezShade{Path.DirectorySeparatorChar}";
            string programPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            responseFile = Path.Combine(programPath, "respose.txt");

            outWriter.WriteLine("Setting up temporary directory...");
            if (!Directory.Exists(tempPath)) {
                _ = Directory.CreateDirectory(tempPath);
            } else {
#if RELEASE
                foreach (var file in Directory.GetFiles(tempPath)) {
                    File.Delete(file);
                }
#endif
            }

            string GenerateGShadeUrl() {
                const string domain = "gshade.org";
                var tree = string.Join("/", "releases", "GShade.Latest.Installer.exe");
                return $"https://{domain}/{tree}";
            }

            var installerUrl = GenerateGShadeUrl();
            var exePath = tempPath + "GShade.Latest.Installer.exe";

#if RELEASE
            outWriter.WriteLine("Downloading GShade installer...");
            Task.Run(async () => await DownloadFileAsync(installerUrl, exePath)).Wait();
#endif

#if DEBUG
            if (!Directory.Exists(tempPath + Path.Combine("GShade-C-Shaders-main", "gshade-shaders"))) {
#endif
                SubProgram = new SubProgram(errorWriter, outWriter, inReader);
                // I'm using the official GShade installer, am I not? :^
                var gshadeInstaller = SubProgram.RunGShade(asseblyPath, exePath, tempPath, gameInstall);

                if (gshadeInstaller == 1) {
                    WriteErrorAndExit("Failed to run method \"CopyZipDeployProcess\" from Assembly of type GShadeInstaller.App.");
                } else if (gshadeInstaller == 99) {
                    WriteErrorAndExit($"Failed to load assembly at \"{exePath}\"");
                }
#if DEBUG
            }
#endif

            // File.Copy gives an access denied error, so let's make it ourself
            var src = tempPath + Path.Combine("GShade-C-Shaders-main", "gshade-shaders");
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
                    //outWriter.WriteLine($"file: {file}");
                    //outWriter.WriteLine($"fileName: {fileName}");
                    //outWriter.WriteLine($"srcFile: {srcFile}");
                    //outWriter.WriteLine($"dstFile: {dstFile}");
#endif
                    File.Copy(srcFile, dstFile, true);
                }

                foreach (var dir in Directory.GetDirectories(source)) {
                    var dirName = Path.GetFileName(dir);
                    var newDestination = Path.Combine(destination, dirName);
#if DEBUG
                    //outWriter.WriteLine($"dir: {dir}");
                    //outWriter.WriteLine($"dirName: {dirName}");
                    //outWriter.WriteLine($"newDestination: {newDestination}");
                    //outWriter.WriteLine($"Exists(newDestination): {Directory.Exists(newDestination)}");
#endif
                    if (!Directory.Exists(newDestination)) {
                        _ = Directory.CreateDirectory(newDestination);
                    }
                    RecursiveClone(dir, newDestination);
                }
            }

            outWriter.WriteLine("Moving shaders to game directory...");
            RecursiveClone(src, dst);
            _ = Directory.CreateDirectory(Path.Combine(gameInstall, "reshade-addons"));

            RecursiveClone(Path.Combine(gameInstall, "gshade-presets"), Path.Combine(gameInstall, "reshade-presets"));

#if RELEASE
            string GetGitHubKey() {
                const string gitHubKeyFile = ".env";
                string combinedPath = Path.Combine(programPath, gitHubKeyFile);
                if (File.Exists(combinedPath)) {
                    foreach (string line in File.ReadAllLines(combinedPath)) {
                        if (line.StartsWith("GitHubKey=\"")) {
                            var result = Regex.Match(line, "GitHubKey=\"([^\"]*)\"");
                            if (result.Captures.Count > 0 && result.Groups.Count > 1) {
                                return result.Groups[1].Value;
                            }
                        }
                    }
                }
                return "";
            }
            string GetGitHubTagsUrl() {
                const string subdomain = "api";
                const string domain = "github.com";
                var tree = string.Join("/", "repos", "crosire", "reshade", "tags" );
                return $"https://{subdomain}.{domain}/{tree}";
            }
            long GetLastCheckTime() {
                const string gitHubKeyFile = ".env";
                string combinedPath = Path.Combine(programPath, gitHubKeyFile);
                if (File.Exists(combinedPath)) {
                    foreach (string line in File.ReadAllLines(combinedPath)) {
                        if (line.StartsWith("LastCheckTime=")) {
                            var result = Regex.Match(line, @"LastCheckTime=(\d+)");
                            if (result.Captures.Count > 0 && result.Groups.Count > 1 && long.TryParse(result.Groups[1].Value, out long timestamp)) {
                                return timestamp;
                            }
                        }
                    }
                }
                return 0;
            }
            void WriteLastCheckTime(long timestamp) {
                const string gitHubKeyFile = ".env";
                string combinedPath = Path.Combine(programPath, gitHubKeyFile);
                try {
                    if (!File.Exists(combinedPath)) {
                        File.Create(combinedPath);
                    }
                    string text = File.ReadAllText(combinedPath);
                    long lastCheckTime = GetLastCheckTime();
                    if (lastCheckTime > 0) {
                        File.WriteAllText(combinedPath, Regex.Replace(text, @"^(LastCheckTime)=\d+$", $"$1={timestamp}"));
                    } else {
                        File.WriteAllText(combinedPath, $"{text}{Environment.NewLine}LastCheckTime={timestamp}");
                    }
                } catch (Exception exception) {
                    WriteErrorAndExit(exception);
                }
            }
#endif
            string GetLastVersion(string fallback = "") {
                const string gitHubKeyFile = ".env";
                string combinedPath = Path.Combine(programPath, gitHubKeyFile);
                if (File.Exists(combinedPath)) {
                    foreach (string line in File.ReadAllLines(combinedPath)) {
                        if (line.StartsWith("LastVersion=")) {
                            var result = Regex.Match(line, "LastVersion=\"([^\"]*)\"");
                            if (result.Captures.Count > 0 && result.Groups.Count > 1) {
                                return result.Groups[1].Value;
                            }
                        }
                    }
                }
                return fallback;
            }
            void WriteLastVersion(string version) {
                const string gitHubKeyFile = ".env";
                string combinedPath = Path.Combine(programPath, gitHubKeyFile);
                try {
                    if (!File.Exists(combinedPath)) {
                        File.Create(combinedPath);
                    }
                    string text = File.ReadAllText(combinedPath);
                    string lastVersion = GetLastVersion();
                    if (lastVersion != "") {
                        File.WriteAllText(combinedPath, Regex.Replace(text, "(LastVersion=)\"[^\"]*\"", $"$1\"{version}\""));
                    } else {
                        File.WriteAllText(combinedPath, $"{text}{Environment.NewLine}LastVersion=\"{version}\"");
                    }
                } catch (Exception exception) {
                    WriteErrorAndExit(exception);
                }
            }
            string GetReShadeLatestVersion() {
                string fallback = "5.8.0";
#nullable enable
#if RELEASE
                Task<string?> task = null!;
                try {
                    var tagsUri = GetGitHubTagsUrl();

                    task = Task.Run(async() => await GetHttpRequestAsync(tagsUri, GetGitHubKey(), GetLastCheckTime(), true));
                    task.Wait(10 * 1000);
                    if (task.IsCompleted && task.Result is not null) {
                        WriteLastCheckTime(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                        var json = JsonArray.Parse(task.Result);
                        fallback = (((string?)json?.AsArray()[0]?["name"]) ?? $"v{fallback}").Replace("v", "");
                    }
                } catch (Exception exception) {
                    JsonNode? json = JsonObject.Parse(task.Result ?? "{}");
                    if (json?.AsObject().ContainsKey("message") == true) {
                        WriteErrorAndExit($"GitHub returned: {json.AsObject()["message"]}, {json.AsObject()["documentation_url"]}");
                    }
                    WriteErrorAndExit(exception.Message);
                }
#else
                try {
                    var files = Directory.GetFiles(tempPath, "ReShade_Setup_*_Addon.exe");
                    if (files.Length > 0) {
                        var result = Regex.Match(files[0], "ReShade_Setup_([^_]*)_Addon.exe");
                        if (result.Captures.Count > 0 && result.Groups.Count > 1) {
                            fallback = result.Groups[1].Value;
                        }
                    }
                } catch (Exception exception) {
                    WriteErrorAndExit(exception.Message);
                }
#endif
                if (fallback == "NotModified") {
                    fallback = GetLastVersion(fallback);
                } else {
                    WriteLastVersion(fallback);
                }
                return fallback;
            }
#nullable restore
            string reshadeLatest = GetReShadeLatestVersion();
#if RELEASE
            string GenerateReShadeUrl() {
                const string domain = "reshade.me";
                var tree = string.Join("/", "downloads", string.Concat("ReShade_Setup_", reshadeLatest, "_Addon.exe"));
                return $"http://{domain}/{tree}";
            }

            outWriter.WriteLine("Downloading ReShade...");
#endif
            var reshadePath = tempPath + $"ReShade_Setup_{reshadeLatest}_Addon.exe";
#if RELEASE
            var reshadeUrl = GenerateReShadeUrl();
            if (!File.Exists(reshadePath)) {
                outWriter.WriteLine($"Downloading ReShade from {reshadeUrl}...");
                Task.Run(async () => await DownloadFileAsync(reshadeUrl, reshadePath)).Wait();
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
            reshadeProcess.StartInfo.Arguments = $"\"{Path.Combine(gameInstall, "ffxiv_dx11.exe")}\" --api dxgi --headless";
            reshadeProcess.StartInfo.RedirectStandardOutput = true;
            reshadeProcess.StartInfo.RedirectStandardError = true;
            var reshadeReturn = reshadeProcess.Start();

            if (!reshadeReturn) {
                outWriter.Write(reshadeProcess.StandardOutput.ReadToEnd());
                errorWriter.Write(reshadeProcess.StandardError.ReadToEnd());
            }

            reshadeProcess.WaitForExit();

            var configPath = Path.Combine(gameInstall, "ReShade.ini");

            if (!File.Exists(configPath)) {
                outWriter.WriteLine("Writing ReShade config...");
                var configText = $"[GENERAL]{Environment.NewLine}[GENERAL]{Environment.NewLine}EffectSearchPaths=.\\reshade-shaders\\Shaders\\**{Environment.NewLine}TextureSearchPaths=.\\reshade-shaders\\Textures\\**{Environment.NewLine}".Trim();

                File.WriteAllText(configPath, configText);
            }

            SubProgram.Dispose();

#if RELEASE
            if (Directory.Exists(tempPath)) {
                outWriter.WriteLine("Cleaning up...");
                try {
                    Directory.Delete(tempPath, true);
                } catch (Exception exception) {
                    WriteErrorAndExit(exception);
                }
            }
#endif

            outWriter.WriteLine("Done!\nSupport FOSS, and thank you for using DeezShade!\nPress any key to continue.");
            _ = Console.ReadKey();

            ExitWithCode(ExitCode.Success);
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

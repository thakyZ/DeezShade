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
    internal class TestAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public TestAssemblyLoadContext(string mainAssemblyToLoadPath) : base(isCollectible: true) => _resolver = new AssemblyDependencyResolver(mainAssemblyToLoadPath);

        protected override Assembly Load(AssemblyName name)
        {
            string assemblyPath = _resolver.ResolveAssemblyToPath(name);
            return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
        }
    }

    public static class Program {
#if !DEBUG
        private static readonly HttpClient client = new();
#endif
        private static readonly TextWriter errorWriter = Console.Error;
        private static readonly TextWriter outWriter = Console.Out;
        private static readonly TextReader inReader = Console.In;
        private static string responseFile = "";

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int ExecuteAndUnload(string asseblyName, string assemblyPath, string tempPath, string gameInstall, out WeakReference weakRef, out TestAssemblyLoadContext assemblyLoadContext)
        {
            assemblyLoadContext = new TestAssemblyLoadContext(asseblyName);

            weakRef = new WeakReference(assemblyLoadContext, trackResurrection: true);

            Assembly assembly = assemblyLoadContext.LoadFromAssemblyPath(assemblyPath);
            if (assembly == null)
            {
                Console.WriteLine($"Loading the assembly at, `{assemblyPath}` failed.");
                return 99;
            }

            var type = assembly.GetType("GShadeInstaller.App");

            type.GetField("_gsTempPath").SetValue(null, tempPath);
            type.GetField("_exeParentPath").SetValue(null, gameInstall);

            // Patch GShade from shutting off your computer (LMAO)
            outWriter.WriteLine("Patching GShade malware...");
            var harmony = new Harmony("com.notnite.thanks-marot");

            var getProcessesByName = typeof(Process).GetMethod("GetProcessesByName", new[] { typeof(string) });
            _ = harmony.Patch(getProcessesByName, new HarmonyMethod(typeof(Program).GetMethod(nameof(ProcessDetour))));

            outWriter.WriteLine("Requesting new files through GShade installer...");
            _ = type.GetMethod("InitLog").Invoke(null, null);
            var complete = type.GetMethod("CopyZipDeployProcess").Invoke(null, null);
            _ = type.GetMethod("PresetDownloadProcess").Invoke(null, null);
            _ = type.GetMethod("PresetInstallProcess").Invoke(null, null);

            assemblyLoadContext.Unload();

            return complete is bool x2 && !x2 ? 1 : 0;
        }
        private static void ExitWithCode(ExitCode exitCode) => Environment.Exit((int)exitCode);

        private static void WriteErrorAndExit(Exception exception) {
            errorWriter.WriteLine(exception.Message);
            errorWriter.WriteLine(exception.StackTrace);
            ExitWithCode(ExitCode.Error);
        }

        private static void WriteErrorAndExit(params string[] exceptionMessages) {
            foreach (string message in exceptionMessages) {
                errorWriter.WriteLine(message);
            }
            ExitWithCode(ExitCode.Error);
        }

#if !DEBUG
        private static async Task DownloadFileAsync(string url, string destination) {
            try {
                if (!File.Exists(destination)) {
                    using HttpResponseMessage response = await client.GetAsync(url);
                    _ = response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    using (var fs = new FileStream(destination, FileMode.CreateNew)) {
                        await response.Content.CopyToAsync(fs);
                    }
                }
            } catch (Exception exception) {
                WriteErrorAndExit(exception);
            }
        }

#nullable enable
        private static void WriteResposeFile(string contents) {
            FileStream? stream = null;
            try {
                if (File.Exists(responseFile)) {
                    File.Delete(responseFile);
                }
                var bytes = Encoding.UTF8.GetBytes(contents);
                using (stream = File.Create(responseFile)) {
                    stream.Write(bytes, 0, bytes.Length);
                }
                errorWriter.WriteLine($"Wrote response to: {responseFile}");
            } catch (Exception exception) {
                WriteErrorAndExit(exception);
            } finally {
                stream?.Dispose();
            }
        }

        private static async Task<string?> GetHttpRequestAsync(string url, string key = "", long lastCheckTime = 0, bool isGithub = false) {
            try {
                HttpResponseMessage response;

                using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, url)) {
                    if (isGithub) {
                        requestMessage.Headers.Add("User-Agent", "NekoBoiNick.DeezShade");
                        requestMessage.Headers.Add("Accept", "application/vnd.github+json");
                        if (key != "") {
                            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                        }
                        requestMessage.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                        if (lastCheckTime > 0) {
                            requestMessage.Headers.Add("If-Modified-Since", $"{DateTimeOffset.FromUnixTimeSeconds(lastCheckTime):ddd, dd MMM yyyy HH:mm:ss} GMT");
                        }
                    }
                    response = await client.SendAsync(requestMessage);
                }

                string content = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == HttpStatusCode.NotModified) {
                    return "[{\"name\":\"vNotModified\"}]";
                }

                try {
                    _ = JsonValue.Parse(content);
                } catch (JsonException exception) {
                    WriteResposeFile(content);
                    WriteErrorAndExit((Exception)exception);
                    return null;
                } catch (Exception exception) {
                    WriteErrorAndExit(exception);
                    return null;
                }

                return content;
            } catch (Exception exception) {
                if (isGithub && exception is HttpRequestException { StatusCode: (HttpStatusCode)403 }) {
                    WriteErrorAndExit($"{exception.Message} This is likely due to a rate limit.");
                } else {
                    WriteErrorAndExit(exception);
                }
                return null;
            }
        }
#nullable restore
#endif

        public static void Main(string[] args) {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var asseblyPath = Assembly.GetExecutingAssembly().Location;
            Console.WriteLine($"DeezShade v{version}, by NotNet and friends");
            Console.WriteLine("Built for GShade v4.1.1.");

            var gameInstall = "";

            if (args.Length == 0 || args.ToList().FindIndex(x => x.StartsWith("--path")) == -1) {
                Console.Write("Enter the path to your game install: ");
                gameInstall = inReader.ReadLine();
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
            string programPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            responseFile = Path.Combine(programPath, "respose.txt");

            outWriter.WriteLine("Setting up temporary directory...");
            if (!Directory.Exists(tempPath)) {
                _ = Directory.CreateDirectory(tempPath);
            } else {
#if !DEBUG
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

#if !DEBUG
            outWriter.WriteLine("Downloading GShade installer...");
            Task.Run(async () => await DownloadFileAsync(installerUrl, exePath)).Wait();
#endif

#if DEBUG
            if (!Directory.Exists(tempPath + Path.Combine("GShade-C-Shaders-main", "gshade-shaders"))) {
#endif
                // I'm using the official GShade installer, am I not? :^
                var gshadeInstaller = ExecuteAndUnload(asseblyPath, exePath, tempPath, gameInstall, out WeakReference weakRef, out TestAssemblyLoadContext assemblyLoadContext);

                for (int i = 0; weakRef.IsAlive && (i < 10); i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                assemblyLoadContext.Unloading += (AssemblyLoadContext _) => outWriter.WriteLine("Unloading...");
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

#if !DEBUG
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
#if !DEBUG
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
#if !DEBUG
            string GenerateReShadeUrl() {
                const string domain = "reshade.me";
                var tree = string.Join("/", "downloads", string.Concat("ReShade_Setup_", reshadeLatest, "_Addon.exe"));
                return $"http://{domain}/{tree}";
            }
#endif

            outWriter.WriteLine("Downloading ReShade...");
            var reshadePath = tempPath + $"ReShade_Setup_{reshadeLatest}_Addon.exe";
#if !DEBUG
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

            GC.SuppressFinalize(weakRef);

#if !DEBUG
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

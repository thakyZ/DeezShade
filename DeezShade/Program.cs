﻿using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;

namespace DeezShade {
    public class Program {
        public static void Main(string[] args) {
            Console.WriteLine("DeezShade v1.0.2, by NotNet and friends");
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
            Console.WriteLine("Requesting new files through GShade installer...");
            var assembly = Assembly.LoadFile(exePath);
            var type = assembly.GetType("GShadeInstaller.App");

            // get presets & shaders
            type.GetField("_gsTempPath").SetValue(null, tempPath);
            type.GetField("_exeParentPath").SetValue(null, gameInstall);
            type.GetField("_instReady").SetValue(null, true); // wp
            
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
            Directory.CreateDirectory(gameInstall + "gshade-addons");

            Console.WriteLine("Extracting DLL and config...");
            var zip = ZipFile.OpenRead(zipPath);
            
            if (File.Exists(Path.Combine(gameInstall, "dxgi.dll"))) {
                File.Move(Path.Combine(gameInstall, "dxgi.dll"), Path.Combine(gameInstall, "dxgi.dll.old"));
            }

            zip.GetEntry("GShade64.dll").ExtractToFile(gameInstall + "dxgi.dll", true);
            zip.GetEntry("GShade.ini").ExtractToFile(gameInstall + "GShade.ini", true);
            
            Console.WriteLine("Done!\nSupport FOSS, and thank you for using DeezShade!\nPress any key to continue.");
            Console.ReadKey();
        }
    }
}

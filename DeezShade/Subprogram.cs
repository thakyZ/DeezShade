using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shell;

using HarmonyLib;

namespace DeezShade {
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

    internal class SubProgram : IDisposable {
        private TextWriter ErrorWriter { get; }
        private TextWriter OutWriter { get; }
        private TextReader InReader { get; }
        private WeakReference WeakRef { get; set; }
        private TestAssemblyLoadContext AssemblyLoadContext { get; set; }

        public SubProgram(TextWriter errorWriter, TextWriter outWriter, TextReader inReader) {
            ErrorWriter = errorWriter;
            OutWriter = outWriter;
            InReader = inReader;
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        private int ExecuteAndUnloadGShade(string assemblyName, string assemblyPath, string tempPath, string gameInstall)
        {
            AssemblyLoadContext = new TestAssemblyLoadContext(assemblyName);

            WeakRef = new WeakReference(AssemblyLoadContext, trackResurrection: true);

            Assembly assembly = AssemblyLoadContext.LoadFromAssemblyPath(assemblyPath);
            if (assembly == null)
            {
                Console.WriteLine($"Loading the assembly at, `{assemblyPath}` failed.");
                return 99;
            }

            // I'm using the official GShade installer, am I not? :^
            var type = assembly.GetType("GShadeInstaller.App");

            type.GetField("_gsTempPath").SetValue(null, tempPath);
            type.GetField("_exeParentPath").SetValue(null, gameInstall);

            // Patch GShade from shutting off your computer (LMAO)
            OutWriter.WriteLine("Patching GShade malware...");
            var harmony = new Harmony("com.notnite.thanks-marot");

            var getProcessesByName = typeof(Process).GetMethod("GetProcessesByName", new[] { typeof(string) });
            _ = harmony.Patch(getProcessesByName, new HarmonyMethod(typeof(Program).GetMethod(nameof(Program.ProcessDetour))));

            OutWriter.WriteLine("Requesting new files through GShade installer...");
            _ = type.GetMethod("InitLog").Invoke(null, null);
            var complete = type.GetMethod("CopyZipDeployProcess").Invoke(null, null);
            _ = type.GetMethod("PresetDownloadProcess").Invoke(null, null);
            _ = type.GetMethod("PresetInstallProcess").Invoke(null, null);

            AssemblyLoadContext.Unload();

            return complete is bool x2 && !x2 ? 1 : 0;
        }

        public int RunGShade(string assemblyPath, string exePath, string tempPath, string gameInstall) {
            var gshadeInstaller = ExecuteAndUnloadGShade(assemblyPath, exePath, tempPath, gameInstall);

            for (int i = 0; WeakRef.IsAlive && (i < 10); i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            AssemblyLoadContext.Unloading += (AssemblyLoadContext _) => OutWriter.WriteLine("Unloading...");
            return gshadeInstaller;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool _isDisposed;

        protected virtual void Dispose(bool disposing) {
            if (disposing && !_isDisposed) {
                if (WeakRef is not null) {
                    #pragma warning disable CA1816
                    GC.SuppressFinalize(WeakRef);
                    #pragma warning restore CA1816
                }
                _isDisposed = true;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeezShade {
    internal static class Services {
        internal static SubProgram SubProgram { get; set; } = null!;
        internal static Configuration Settings { get; set; } = null!;
        internal static TextWriter ErrorWriter { get; } = Console.Error;
        internal static TextWriter OutWriter { get; } = Console.Out;
        internal static TextReader InReader { get; } = Console.In;
    }
}

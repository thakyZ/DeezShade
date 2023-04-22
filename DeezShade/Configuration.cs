using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DeezShade {
    [Serializable]
    internal class VersionCheck {
        [JsonPropertyName("last_check_time")]
        internal long LastCheckTime { get; set; }
        [JsonPropertyName("last_version")]
        internal string LastVersion { get; set; }
    }

    [Serializable]
    internal class GitHubChecks {
        [JsonPropertyName("reshade_version")]
        internal VersionCheck ReshadeVersion { get; set; } = new();
        [JsonPropertyName("additional_addon_versions")]
        internal List<VersionCheck> AdditionalAddonVersions { get; set; } = new();
    }

    [Serializable]
    internal class ReShadeFolders {
        [JsonPropertyName("addons")]
        internal string Addons { get; set; }
        [JsonPropertyName("presets")]
        internal string Presets { get; set; }
        [JsonPropertyName("shaders")]
        internal string Shaders { get; set; }
    }

    [Serializable]
    internal class AdditionalAddonGithub : AdditionalAddonBase {
        [JsonPropertyName("user")]
        internal string User { get; set; }
        [JsonPropertyName("repo")]
        internal string Repo { get; set; }
        [JsonPropertyName("branch")]
        internal string Branch { get; set; }
        [JsonPropertyName("file_name")]
        internal string FileName { get; set; }
    }

    [Serializable]
    internal class AdditionalAddonUrl : AdditionalAddonBase {
        [JsonPropertyName("ini_url")]
        internal string IniUrl { get; set; }
        [JsonPropertyName("addon_url")]
        internal string AddonUrl { get; set; }
    }

    [Serializable]
    internal abstract class AdditionalAddonBase {
        [JsonPropertyName("site")]
        internal string Site { get; set; }
    }


    [Serializable]
    internal class Configuration {
        [JsonPropertyName("game_path")]
        internal string GameInstall { get; set; }
        [JsonPropertyName("install_path")]
        internal string CustomInstallPath { get; set; }
        [JsonPropertyName("github_key")]
        internal string GitHubKey { get; set; }
        [JsonPropertyName("reshade_folders")]
        internal ReShadeFolders ReShadeFolders { get; set; } = new();
        [JsonPropertyName("additional_addons")]
        internal List<AdditionalAddonBase> AdditionalAddons { get; set; } = new();
        [JsonPropertyName("github_checks")]
        internal GitHubChecks GitHubChecks { get; set; } = new();

        internal static bool Load() {
            Services.Configuration
        }
        internal static bool Save() {

        }
    }
}

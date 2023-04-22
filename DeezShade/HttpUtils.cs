#if RELEASE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Windows.Media.Protection.PlayReady;

namespace DeezShade {
    internal static class HttpUtils {
        private static readonly HttpClient client = new();
        private static string responseFile = "";

#nullable enable
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
    }
}
#endif

using System.IO.Compression;

namespace TradeScout.API.Services;

/// <summary>
/// Creates a Chrome extension for proxy authentication
/// </summary>
public class ProxyExtensionBuilder
{
    public static string CreateProxyExtension(string proxyHost, int proxyPort, string username, string password)
    {
        var extensionDir = Path.Combine(Path.GetTempPath(), $"proxy_auth_{Guid.NewGuid()}");
        Directory.CreateDirectory(extensionDir);

        // manifest.json
        var manifest = @"{
  ""version"": ""1.0.0"",
  ""manifest_version"": 2,
  ""name"": ""Proxy Auth"",
  ""permissions"": [
    ""proxy"",
    ""tabs"",
    ""unlimitedStorage"",
    ""storage"",
    ""<all_urls>"",
    ""webRequest"",
    ""webRequestBlocking""
  ],
  ""background"": {
    ""scripts"": [""background.js""]
  },
  ""minimum_chrome_version"": ""22.0.0""
}";

        // background.js
        var background = $@"
var config = {{
    mode: ""fixed_servers"",
    rules: {{
        singleProxy: {{
            scheme: ""http"",
            host: ""{proxyHost}"",
            port: parseInt({proxyPort})
        }},
        bypassList: [""localhost""]
    }}
}};

chrome.proxy.settings.set({{value: config, scope: ""regular""}}, function() {{}});

function callbackFn(details) {{
    return {{
        authCredentials: {{
            username: ""{username}"",
            password: ""{password}""
        }}
    }};
}}

chrome.webRequest.onAuthRequired.addListener(
    callbackFn,
    {{urls: [""<all_urls>""]}},
    ['blocking']
);
";

        File.WriteAllText(Path.Combine(extensionDir, "manifest.json"), manifest);
        File.WriteAllText(Path.Combine(extensionDir, "background.js"), background);

        return extensionDir;
    }

    public static void CleanupExtension(string extensionDir)
    {
        try
        {
            if (Directory.Exists(extensionDir))
            {
                Directory.Delete(extensionDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

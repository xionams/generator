// Copyright 2024 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License"):
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Newtonsoft.Json;
using System.Diagnostics;

namespace MakeItSo;

internal class BuildCommand : ICommand
{
    private readonly string _api;
    private readonly string _outputRoot;
    private readonly UnknownApiBehavior _unknownApiBehavior;

    public BuildCommand(string api, string outputRoot, UnknownApiBehavior unknownApiBehavior)
    {
        _api = api;
        _outputRoot = outputRoot;
        _unknownApiBehavior = unknownApiBehavior;
    }

    public void Execute()
    {
        // This is within google-cloud-dotnet (for now). It should be within the container.
        var processArguments = new List<string> { "./build.sh" };

        // The magic string "all" is used to say "just build all known APIs" which is
        // done by just running build.sh without any other arguments.
        // Otherwise, we need to find the API corresponding to the specified directory.
        if (_api != "all")
        {
            var apiCatalogJson = File.ReadAllText(Path.Combine(_outputRoot, "apis", "apis.json"));
            var apiCatalog = JsonConvert.DeserializeObject<ApiCatalog>(apiCatalogJson)!;
            var api = apiCatalog.Apis.FirstOrDefault(api => api.ProtoPath == _api);
            if (api is null)
            {
                switch (_unknownApiBehavior)
                {
                    case UnknownApiBehavior.Create:
                        throw new InvalidOperationException($"Create for unknown API {_api} is not supported by the build command");
                    case UnknownApiBehavior.Error:
                        throw new InvalidOperationException($"No API configured with proto path {_api}, and unknown API behavior is 'error'");
                    case UnknownApiBehavior.Ignore:
                        Console.WriteLine($"Ignoring unknown API {_api}");
                        return;
                    default:
                        throw new InvalidOperationException($"Unsupported unknown API behavior: {_unknownApiBehavior}");
                }
            }
            processArguments.Add(api.Id);
        }

        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            WorkingDirectory = _outputRoot
        };
        processArguments.ForEach(psi.ArgumentList.Add);

        var process = Process.Start(psi)!;
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new Exception($"Generation ended with exit code {process.ExitCode}");
        }
    }
}

using System;
using System.IO;
using System.Net.Http;

try
{
    if (args.Length != 3)
    {
        WriteHelp();
        return;
    }

    bool upload;
    
    switch (args[0])
    {
        case "u":
            upload = true;
            break;

        case "d":
            upload = false;
            break;

        default:
            WriteError($"Unknown action '{args[0]}'.");
            WriteHelp();
            return;
    };

    var url = args[1].TrimEnd('/') + "/file";
    var path = args[2];

    if (upload && !File.Exists(path))
    {
        WriteError($"File '{path}' does not exist.");
        return;
    }

    if (!upload && File.Exists(path))
    {
        WriteError($"File '{path}' already exists.");
        return;
    }

    Console.WriteLine($"Please enter the {(upload ? "upload" : "download")} password:");
    var password = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(password))
    {
        return;
    }

    var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("Authorization", Uri.EscapeDataString(password));
    HttpResponseMessage response;

    if (upload)
    {
        using var stream = File.OpenRead(path);
        response = await httpClient.PutAsync(url, new StreamContent(stream));
    }
    else
    {
        response = await httpClient.GetAsync(url);
    }

    if (!response.IsSuccessStatusCode)
    {
        string? message = null;

        try
        {
            message = await response.Content.ReadAsStringAsync();
        }
        catch { }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = "(none)";
        }

        WriteError($"Server returned {(int)response.StatusCode} and message: {message}");
        return;
    }

    if (upload)
    {
        Console.WriteLine($"Uploaded file '{path}'.");
    }
    else
    {
        using var stream = File.OpenWrite(path);
        await response.Content.CopyToAsync(stream);
        Console.WriteLine($"Created file '{path}'.");
    }
}
catch (Exception ex)
{
    WriteError(ex);
}

static void WriteHelp()
{
    Console.WriteLine();
    Console.WriteLine("Example usage");
    Console.WriteLine("protected-storage-client u http://localhost:5000 README.md     Upload 'README.md'.");
    Console.WriteLine("protected-storage-client d http://localhost:5000 README-1.md   Download the file to 'README-1.md'.");
}

static void WriteError(object message) => Console.Error.WriteLine($"Error: {message}");
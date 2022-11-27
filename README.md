# Protected Storage
A password-protected REST API that serves a single file.

## Quick start
- Install [.NET 7.0 SDK](https://dotnet.microsoft.com/download/dotnet/7.0)
- Start the API: `dotnet run -p ProtectedStorage.Server`
- Upload a file: `dotnet run -p ProtectedStorage.Client -- u http://localhost:5000 README.md`. The default upload password is "upload".
- Download the uploaded file: `dotnet run -p ProtectedStorage.Client -- d http://localhost:5000 README-1.md`. The default download password is "download".

##  Features
- Stores a single file
- Supports a separate upload and download password
- After submitting an invalid password no requests are allowed for the next five minutes (as long as there is no restart)
- Optionally sends [Slack](https://slack.com/) notifications

## Endpoints
| Endpoint | Description |
| --- | --- |
| PUT `/file` | Upload a file. The contents of the file are read from the request body. |
| GET `/file` | Download the uploaded file. The response body contains the contents of the file. |

## Authentication
The value of the `Authorization` header should be equal to the upload or download password.

## Configuration
There are four values that can be configured: `UploadPassword`, `DownloadPassword`, `FilePath` and `SlackWebhookUrls`. Edit `appsettings.json`, `appsettings.Development.json`, or add an environment variable like `DOTNET_UploadPassword`.

## Security
The download and upload passwords are stored in plain text on the server. Therefore, construct these passwords by using an application like [key-stretcher](https://github.com/rdragon/key-stretcher).

The uploaded file is stored unencrypted on the server. Consider using an application like [file-encrypter](https://github.com/rdragon/file-encrypter) to encrypt the file before it's uploaded.
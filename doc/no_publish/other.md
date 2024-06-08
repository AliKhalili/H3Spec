### Run Local HTTP/3 Kestrel Server via Docker

```bash
dotnet dev-certs https -ep ./certs/certificate.pfx -p 1234567 --trust
dotnet dev-certs https --check --trust
.\build_and_run_h3server.bat
"C:\Program Files\Google\Chrome\Application\chrome.exe" --origin-to-force-quic-on=localhost:6001 https://localhost:6001
```

## Release

Publish single executable file:

```bash
dotnet publish -r win-x64 -p:PublishSingleFile=true
dotnet publish -r linux-x64 -p:PublishSingleFile=true
```

## Console

### Configuring the Windows Terminal For Unicode and Emoji Support

For PowerShell, the following command will enable Unicode and Emoji support. You can add this to your profile.ps1 file:

```bash
[console]::InputEncoding = [console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
```

[Link](https://spectreconsole.net/best-practices)

function Assert-SdkProjectionPayload {
    param([Parameter(Mandatory)][string]$Directory,[Parameter(Mandatory)][string]$PackageDirectory)
    $ErrorActionPreference='Stop'
    foreach ($name in @('Microsoft.Windows.SDK.NET.dll','WinRT.Runtime.dll')) {
        $source=Join-Path $PackageDirectory ('lib/net8.0/'+$name)
        $payload=Join-Path $Directory $name
        if (-not (Test-Path -LiteralPath $source -PathType Leaf) -or -not (Test-Path -LiteralPath $payload -PathType Leaf)) {
            throw "Missing SDK projection file: $name"
        }
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -cne (Get-FileHash -LiteralPath $payload -Algorithm SHA256).Hash) {
            throw "SDK projection file differs from restored package: $name"
        }
    }
}

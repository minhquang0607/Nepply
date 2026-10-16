# Các helper thuần cho ba nhóm đầu ra phát hành; không tự chạy build hoặc sửa repo.
function Get-NepplyReleaseVersion {
    param([Parameter(Mandatory)][string]$Repository)
    [xml]$project = Get-Content -LiteralPath (Join-Path $Repository 'src/Nepply/Nepply.csproj') -Raw
    $version = [string]$project.Project.PropertyGroup.Version
    $fileVersion = [string]$project.Project.PropertyGroup.FileVersion
    if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$' -or $fileVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw 'Version/FileVersion không hợp lệ trong Nepply.csproj.'
    }
    [pscustomobject]@{ Version=$version; FileVersion=$fileVersion }
}

function New-NepplyArtifactDirectory {
    param([Parameter(Mandatory)][string]$Repository, [Parameter(Mandatory)][string]$Path)
    $repo = [IO.Path]::GetFullPath($Repository).TrimEnd('\','/')
    $root = Join-Path $repo 'artifacts'
    $target = [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path $repo $Path }))
    if (-not $target.StartsWith($root + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
        throw 'Đầu ra phải là thư mục con mới trong artifacts.'
    }
    if (Test-Path -LiteralPath $target) { throw 'Đầu ra đã tồn tại; chọn tên mới để giữ bằng chứng cũ.' }
    $ancestor = Split-Path $target -Parent
    while ($ancestor -and $ancestor.Length -ge $repo.Length) {
        if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Không ghi artifact qua symlink/junction.'
        }
        $ancestor = Split-Path $ancestor -Parent
    }
    (New-Item -ItemType Directory -Path $target -ErrorAction Stop).FullName
}

function Copy-NepplySourceTree {
    param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$Destination)
    $item = Get-Item -LiteralPath $Source -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Source chứa link: $Source" }
    if ($item.PSIsContainer) {
        if ($item.Name -in @('bin','obj','artifacts','publish','.git','.vs','.idea','TestResults')) { return }
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
        foreach ($child in Get-ChildItem -LiteralPath $Source -Force) {
            Copy-NepplySourceTree -Source $child.FullName -Destination (Join-Path $Destination $child.Name)
        }
    } else {
        if ($item.Extension -in @('.pfx','.p12','.key','.dmp','.log','.user','.suo') -or
            ($item.Name -like '.env*' -and $item.Name -ne '.env.example')) {
            throw "Phát hiện file riêng tư trong source bundle: $($item.Name). Cần rà soát, không tự đưa vào ZIP."
        }
        if ($item.Name -eq 'dump.txt' -and $item.Directory.Name -eq 'SensorDumper') { return }
        Copy-Item -LiteralPath $item.FullName -Destination $Destination
    }
}

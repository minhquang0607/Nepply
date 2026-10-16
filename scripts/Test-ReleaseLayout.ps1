$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Release-Layout.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$root = New-NepplyArtifactDirectory -Repository $repo -Path ('artifacts/release-layout-tests-' + [Guid]::NewGuid().ToString('N'))
$passed = 0
function Check([string]$Name,[bool]$Condition) {
    if (-not $Condition) { throw "FAIL $Name" }
    $script:passed++
    Write-Output "PASS $Name"
}
function Reject([string]$Name,[scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected=$true }
    Check $Name $rejected
}
$version = Get-NepplyReleaseVersion -Repository $repo
Check 'Một nguồn version có semver và file version' ($version.Version -match '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$' -and $version.FileVersion -match '^\d+\.\d+\.\d+\.\d+$')
Reject 'Không xuất vào root artifacts' { New-NepplyArtifactDirectory -Repository $repo -Path 'artifacts' }
Reject 'Không traversal ra ngoài artifacts' { New-NepplyArtifactDirectory -Repository $repo -Path 'artifacts/../../outside-nepply' }
Reject 'Không ghi đè đầu ra trước' { New-NepplyArtifactDirectory -Repository $repo -Path $root }
$absolute = New-NepplyArtifactDirectory -Repository $repo -Path (Join-Path $root 'absolute')
Check 'Nhận absolute path đã giới hạn' ($absolute -eq (Join-Path $root 'absolute'))
$fixture = Join-Path $root 'fixture'
New-Item -ItemType Directory -Path (Join-Path $fixture '.github/workflows'),(Join-Path $fixture 'bin'),(Join-Path $fixture 'obj') | Out-Null
[IO.File]::WriteAllText((Join-Path $fixture 'app.cs'),'// fixture')
[IO.File]::WriteAllText((Join-Path $fixture '.github/workflows/test.yml'),'name: fixture')
[IO.File]::WriteAllText((Join-Path $fixture 'bin/generated.txt'),'fixture')
[IO.File]::WriteAllText((Join-Path $fixture 'obj/generated.txt'),'fixture')
$copy = Join-Path $root 'copy'
Copy-NepplySourceTree -Source $fixture -Destination $copy
Check 'Giữ mã nguồn' (Test-Path -LiteralPath (Join-Path $copy 'app.cs'))
Check 'Giữ workflow ẩn trong source' (Test-Path -LiteralPath (Join-Path $copy '.github/workflows/test.yml'))
Check 'Không mang bin/obj' (-not (Test-Path -LiteralPath (Join-Path $copy 'bin')) -and -not (Test-Path -LiteralPath (Join-Path $copy 'obj')))
[IO.File]::WriteAllText((Join-Path $fixture 'fixture.key'),'fake-key-test-only')
Reject 'Không mang file khóa vào source bundle' { Copy-NepplySourceTree -Source $fixture -Destination (Join-Path $root 'rejected-copy') }
[xml]$manifest = Get-Content -LiteralPath (Join-Path $repo 'packaging/msix/AppxManifest.xml') -Raw
Check 'MSIX QA dùng identity riêng' ($manifest.Package.Identity.Name -ceq 'Nepply.Internal.Preview')
Check 'MSIX chạy executable đúng đường dẫn' ($manifest.Package.Applications.Application.Executable -ceq 'App\Nepply.exe')
Check 'MSIX khai báo startup nhưng không tự bật' ($manifest.Package.Applications.Application.Extensions.Extension.StartupTask.Enabled -ceq 'false')
Check 'MSIX không xin admin' ($manifest.Package.Capabilities.Capability.Name -ceq 'runFullTrust')
$inno=Get-Content -LiteralPath (Join-Path $repo 'packaging/inno/Nepply.iss') -Raw
Check 'Inno lấy version qua build thay vì hardcode beta' ($inno.Contains('AppVersion={#ReleaseVersion}') -and $inno.Contains('VersionInfoVersion={#ReleaseFileVersion}') -and $inno -notmatch '2\.5\.0-beta')
Write-Output "RESULT: $passed kiểm tra release layout đạt. Không phải nghiệm thu cài/gỡ hoặc Store."

param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference='Stop'
$executablePath=(Resolve-Path -LiteralPath $Executable).Path
if([IO.Path]::GetFileName($executablePath) -cne 'Nepply.DesktopGuardian.exe'){throw 'Unexpected executable name.'}
$qaSelf=[Diagnostics.Process]::GetCurrentProcess()
function Start-Guardian([string[]]$Arguments) {
    $start=[Diagnostics.ProcessStartInfo]::new($executablePath)
    $start.UseShellExecute=$false
    $start.CreateNoWindow=$true
    foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
    return [Diagnostics.Process]::Start($start)
}
try {
    foreach($invalid in @('bad-generation','00000000000000000000000000000000')){
        $child=Start-Guardian @($invalid,[string]$PID,[string]$qaSelf.StartTime.ToUniversalTime().Ticks)
        try {if(-not $child.WaitForExit(10000) -or $child.ExitCode -ne 2){throw 'Malformed arguments were not rejected.'}}
        finally {if(-not $child.HasExited){$child.Kill();[void]$child.WaitForExit(5000)};$child.Dispose()}
    }
    'PASS: malformed and empty generation rejected'
    foreach($scenario in @('commands','idle-timeout')) {
        $generation=[Guid]::NewGuid().ToString('N')
        $options=[IO.Pipes.PipeOptions]::Asynchronous -bor [IO.Pipes.PipeOptions]::CurrentUserOnly
        $pipe=[IO.Pipes.NamedPipeServerStream]::new(('Nepply-guardian-'+$generation),[IO.Pipes.PipeDirection]::InOut,1,[IO.Pipes.PipeTransmissionMode]::Byte,$options)
        $timeout=[Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(20))
        $child=$null
        try {
            $connection=$pipe.WaitForConnectionAsync($timeout.Token)
            $child=Start-Guardian @($generation,[string]$PID,[string]$qaSelf.StartTime.ToUniversalTime().Ticks)
            $connection.GetAwaiter().GetResult()
            $buffer=[byte[]]::new(1)
            $read=$pipe.ReadAsync($buffer,0,1,$timeout.Token).GetAwaiter().GetResult()
            if($read -ne 1 -or $buffer[0] -ne 128){throw 'Authenticated ready handshake missing.'}
            if($scenario -eq 'commands'){
                # Never send Arm (1): this test must not claim or modify the real Desktop.
                foreach($command in @(2,3)){
                    $pipe.WriteByte([byte]$command)
                    $read=$pipe.ReadAsync($buffer,0,1,$timeout.Token).GetAwaiter().GetResult()
                    if($read -ne 1 -or $buffer[0] -ne 130){throw 'Unarmed command unexpectedly accepted.'}
                }
                $pipe.WriteByte(255)
                if(-not $child.WaitForExit(10000) -or $child.ExitCode -ne 4){throw 'Unknown command not rejected.'}
            } else {
                if(-not $child.WaitForExit(15000) -or $child.ExitCode -ne 3){throw 'Unused guardian did not time out.'}
            }
            "PASS: $scenario; no Desktop arm/hide/restore"
        } finally {
            $timeout.Cancel();$pipe.Dispose();$timeout.Dispose()
            if($child){if(-not $child.HasExited){$child.Kill();[void]$child.WaitForExit(5000)};$child.Dispose()}
        }
    }
    foreach($releaseMode in @('normal','abandoned')){
        $pipes=@();$children=@();$timeout=[Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(20))
        try {
            for($index=0;$index -lt 2;$index++){
                $generation=[Guid]::NewGuid().ToString('N')
                $pipe=[IO.Pipes.NamedPipeServerStream]::new(('Nepply-guardian-'+$generation),[IO.Pipes.PipeDirection]::InOut,1,[IO.Pipes.PipeTransmissionMode]::Byte,$options)
                $pipes+=$pipe
                $connection=$pipe.WaitForConnectionAsync($timeout.Token)
                $children+=Start-Guardian @($generation,[string]$PID,[string]$qaSelf.StartTime.ToUniversalTime().Ticks)
                $connection.GetAwaiter().GetResult()
                if($index -eq 0){
                    $firstBuffer=[byte[]]::new(1)
                    $count=$pipe.ReadAsync($firstBuffer,0,1,$timeout.Token).GetAwaiter().GetResult()
                    if($count -ne 1 -or $firstBuffer[0] -ne 128){throw 'First guardian not ready.'}
                }
            }
            $secondBuffer=[byte[]]::new(1)
            $secondReady=$pipes[1].ReadAsync($secondBuffer,0,1,$timeout.Token)
            Start-Sleep -Milliseconds 300
            if($secondReady.IsCompleted){throw 'Second guardian became ready while first still owns fence.'}
            if($releaseMode -eq 'abandoned'){$children[0].Kill()}else{$pipes[0].WriteByte(255)}
            if(-not $children[0].WaitForExit(5000)){throw 'First guardian did not exit.'}
            $count=$secondReady.GetAwaiter().GetResult()
            if($count -ne 1 -or $secondBuffer[0] -ne 128){throw 'Second guardian did not acquire released fence.'}
            $pipes[1].WriteByte(255)
            if(-not $children[1].WaitForExit(5000) -or $children[1].ExitCode -ne 4){throw 'Second guardian did not finish.'}
            "PASS: two-process generation fence ($releaseMode); no Desktop arm/hide/restore"
        } finally {
            $timeout.Cancel()
            foreach($pipe in $pipes){$pipe.Dispose()}
            foreach($child in $children){if(-not $child.HasExited){$child.Kill();[void]$child.WaitForExit(5000)};$child.Dispose()}
            $timeout.Dispose()
        }
    }
} finally {$qaSelf.Dispose()}

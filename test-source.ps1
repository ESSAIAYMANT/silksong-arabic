param([string]$OutputRoot)
$ErrorActionPreference='Stop'
if(!$OutputRoot){$OutputRoot=Join-Path $PSScriptRoot ('test-output/'+[Guid]::NewGuid().ToString('N'))}
if(Test-Path -LiteralPath $OutputRoot){throw 'Use a new output directory so old fixtures cannot affect this test run.'}
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if(!(Test-Path -LiteralPath $taskCompiler)){throw 'The .NET Framework C# compiler is missing.'}
$taskSources=@('Engine.cs','Toggle.cs','SupportReport.cs','Tests.cs') | ForEach-Object { Join-Path $PSScriptRoot ('installer/src/'+$_) }
$taskExe=Join-Path $OutputRoot 'InstallerTests.exe'
& $taskCompiler /nologo /codepage:65001 /optimize+ /platform:x64 /target:exe /r:System.dll /r:System.Core.dll /r:System.Xml.dll /r:System.Xml.Linq.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ('/out:'+$taskExe) @taskSources
if($LASTEXITCODE -ne 0){throw 'Test compilation failed.'}
$taskLog=Join-Path $OutputRoot 'tests.txt'
& $taskExe (Join-Path $OutputRoot 'fixtures') | Tee-Object -FilePath $taskLog
if($LASTEXITCODE -ne 0){throw 'Source regression tests failed.'}
if(!(Select-String -LiteralPath $taskLog -SimpleMatch 'TOTAL PASS: 45' -Quiet)){throw 'Expected 45 synthetic cases.'}

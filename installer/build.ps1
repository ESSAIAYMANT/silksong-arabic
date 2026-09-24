param([switch]$Tests,[string]$InputDirectory,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$taskHere=$PSScriptRoot
if(!$InputDirectory){$InputDirectory=Join-Path $taskHere 'build'}
if(!$OutputDirectory){$OutputDirectory=Join-Path $taskHere 'candidate'}
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if(!(Test-Path -LiteralPath $taskCompiler)){throw 'The .NET Framework C# compiler is missing.'}
$taskReferences=@('/r:System.dll','/r:System.Core.dll','/r:System.Xml.dll','/r:System.Xml.Linq.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll')
$taskCommon=@('/nologo','/codepage:65001','/optimize+','/platform:x64')+$taskReferences
$taskResources=@(('/resource:'+(Join-Path $InputDirectory 'payload.zip')+',payload.zip'),('/resource:'+(Join-Path $InputDirectory 'package.xml')+',package.xml'))
$taskCandidate=$OutputDirectory
New-Item -ItemType Directory -Path $taskCandidate -Force | Out-Null
$taskTarget=Join-Path $taskCandidate 'Silksong-Arabic-Setup-0.3.0-RC3.exe'
$taskSources=@('Engine.cs','Toggle.cs','Program.cs','ModernForm.cs','SupportReport.cs','AssemblyInfo.cs') | ForEach-Object { Join-Path $taskHere ('src/'+$_) }
& $taskCompiler @taskCommon /target:winexe /r:System.Windows.Forms.dll /r:System.Drawing.dll ('/win32icon:'+(Join-Path $taskHere 'assets/setup.ico')) ('/win32manifest:'+(Join-Path $taskHere 'src/app.manifest')) ('/out:'+$taskTarget) @taskResources @taskSources
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed.'}
if($Tests){
 & $taskCompiler @taskCommon /target:exe ('/out:'+(Join-Path $InputDirectory 'InstallerTests.exe')) @taskResources (Join-Path $taskHere 'src/Engine.cs') (Join-Path $taskHere 'src/Toggle.cs') (Join-Path $taskHere 'src/SupportReport.cs') (Join-Path $taskHere 'src/Tests.cs')
 if($LASTEXITCODE -ne 0){throw 'Test compilation failed.'}
}
Get-FileHash -LiteralPath $taskTarget

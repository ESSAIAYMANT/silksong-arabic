param(
    [Parameter(Mandatory=$true)][string]$Installer,
    [Parameter(Mandatory=$true)][string]$AcceptanceReport
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$taskSig=Get-AuthenticodeSignature -LiteralPath $Installer
if($taskSig.Status -ne 'Valid' -or !$taskSig.TimeStamperCertificate){throw 'RELEASE BLOCKED: trusted, timestamped code signature required.'}
if($taskSig.SignerCertificate.PublicKey.Oid.Value -ne '1.2.840.113549.1.1.1'){throw 'RELEASE BLOCKED: RSA signing certificate required.'}
$taskReport=Get-Content -LiteralPath $AcceptanceReport -Raw | ConvertFrom-Json
$taskHash=(Get-FileHash -LiteralPath $Installer -Algorithm SHA256).Hash.ToLowerInvariant()
if($taskReport.installer_sha256 -ne $taskHash){throw 'RELEASE BLOCKED: acceptance report belongs to another binary.'}
if($taskReport.engine_tests_passed -lt 46){throw 'RELEASE BLOCKED: engine regression tests not recorded.'}
foreach($taskCheck in @('smart_app_control_launch_ok','gui_install_ok','gui_repair_ok','gui_remove_ok','gui_disable_enable_ok','supported_clean_gameplay_ok','payload_binary_signatures_ok')){
    if($taskReport.$taskCheck -isnot [bool] -or !$taskReport.$taskCheck){throw "RELEASE BLOCKED: missing evidence for $taskCheck"}
}
Write-Output ('RELEASE CHECK PASSED for SHA256 '+$taskHash)
Write-Output 'This checks the recorded acceptance evidence. It does not itself run Windows UI or gameplay tests.'

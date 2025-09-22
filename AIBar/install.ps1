$downloadSize = "5GB"
$promptMessage = "$downloadSize of additional content will be downloaded. Do you accept? [Y/n]"
Write-Host $promptMessage
$userResponse = Read-Host
if ($userResponse -eq "Y" -or $userResponse -eq "") {
    Write-Host "Download initiated..."
} else {
    Write-Host "Download cancelled."
    exit
}
winget install Ollama.Ollama
Stop-Process -Name "ollama app" 2> $null
$ollama = "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe"
& $ollama pull gemma3:4b
& $ollama pull gemma3:1b
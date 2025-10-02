winget install llama.cpp
try {
    pip -V
} catch {
    Write-Host "pip not found, installing python"
    winget install Python.Python.3
}
pip install -U "huggingface_hub[cli]"
hf download Qwen/Qwen3-1.7B-GGUF

$dir = $args[0]

try {
    mkdir "$dir"
} catch {
    Write-Host "Folder $dir already exists"
}
Copy-Item .\App\** "$dir" -Recurse > $null

$UninstallBat = "
@echo off
set `"FOLDER=$dir`"
rmdir /s /q `"%FOLDER%`"
echo Folder deleted successfully.
"

$UninstallContent | Out-File "$dir\uninstall.bat" -Encoding UTF8
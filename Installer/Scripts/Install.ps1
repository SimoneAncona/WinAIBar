winget install llama.cpp
try {
    pip -V
} catch {
    Write-Host "pip not found, installing python"
    winget install Python.Python.3
}
pip install -U "huggingface_hub[cli]"
hf download Qwen/Qwen3-1.7B-GGUF

try {
    mkdir "$args[0]"
} catch {
    Write-Host "Folder $args[0] already exists"
}
Copy-Item .\App\** "$args[0]" -Recurse

$UninstallBat = "
@echo off
set `"FOLDER=$args[0]`"
rmdir /s /q `"%FOLDER%`"
echo Folder deleted successfully.
"

$UninstallContent | Out-File "$args[0]\uninstall.bat" -Encoding UTF8
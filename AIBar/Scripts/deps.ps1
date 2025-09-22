winget install llama.cpp
try {
    pip -V
} catch {
    Write-Host "pip not found, installing python"
    winget install Python.Python.3
}
pip install -U "huggingface_hub[cli]"
hf download Qwen/Qwen3-1.7B-GGUF

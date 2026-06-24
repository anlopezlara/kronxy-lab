param(
    [string]$Root = "E:\dev\kronxy-lab",
    [string]$OutFile = "",
    [int]$MaxLinesPerFile = 220,
    [int]$MaxFileSizeKB = 256,
    [switch]$IncludeFullSource,
    [switch]$OpenAfter
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path $Root)) {
    throw "No existe la ruta Root: $Root"
}

$Root = (Resolve-Path $Root).Path

if ([string]::IsNullOrWhiteSpace($OutFile)) {
    $stamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $OutFile = Join-Path $Root "PROJECT_CONTEXT_$stamp.md"
}

$ExcludeDirs = @(
    ".git",".vs",".idea",".vscode",
    "bin","obj","node_modules","dist","build","target",
    ".gradle",".mvn","__pycache__",".pytest_cache",
    "packages",".nuget","coverage","logs","tmp","temp"
)

$IncludeExt = @(
    ".sln",".csproj",".props",".targets",
    ".cs",".json",".xml",".config",".yml",".yaml",
    ".md",".txt",".ps1",".bat",".cmd",
    ".sql",".js",".ts",".html",".css",
    ".java",".properties",".env.example"
)

$PriorityPatterns = @(
    "*.sln","*.csproj","*.props","*.targets",
    "Program.cs","Startup.cs","appsettings*.json",
    "README*","*.md",
    "Dockerfile","docker-compose*.yml",
    "*.ps1","*.sql"
)

function Test-IsExcludedPath {
    param([string]$Path)

    $normalized = $Path.Replace("/", "\")
    foreach ($d in $ExcludeDirs) {
        if ($normalized -like "*\$d\*" -or $normalized -like "*\$d") {
            return $true
        }
    }
    return $false
}

function Get-RelativePathSafe {
    param([string]$FullName)
    return $FullName.Substring($Root.Length).TrimStart("\","/")
}

function Add-Line {
    param([string]$Text = "")
    Add-Content -Path $OutFile -Value $Text -Encoding UTF8
}

function Add-CommandBlock {
    param(
        [string]$Title,
        [scriptblock]$Command
    )

    Add-Line ""
    Add-Line "## $Title"
    Add-Line ""
    Add-Line '```text'
    try {
        $result = & $Command 2>&1 | Out-String
        if ([string]::IsNullOrWhiteSpace($result)) {
            Add-Line "[sin salida]"
        } else {
            Add-Line ($result.TrimEnd())
        }
    } catch {
        Add-Line "[ERROR] $($_.Exception.Message)"
    }
    Add-Line '```'
}

function Get-TextPreview {
    param(
        [string]$Path,
        [int]$MaxLines = 220
    )

    try {
        $lines = Get-Content -Path $Path -TotalCount ($MaxLines + 1) -ErrorAction Stop
        if ($lines.Count -gt $MaxLines) {
            return (($lines | Select-Object -First $MaxLines) -join [Environment]::NewLine) +
                [Environment]::NewLine +
                "... [TRUNCADO: más de $MaxLines líneas]"
        }
        return ($lines -join [Environment]::NewLine)
    } catch {
        return "[ERROR leyendo archivo: $($_.Exception.Message)]"
    }
}

$allItems = Get-ChildItem -Path $Root -Recurse -Force |
    Where-Object { -not (Test-IsExcludedPath $_.FullName) }

$files = $allItems |
    Where-Object { -not $_.PSIsContainer } |
    Sort-Object FullName

$includedFiles = $files |
    Where-Object {
        $ext = $_.Extension.ToLower()
        ($IncludeExt -contains $ext -or $_.Name -in @("Dockerfile","Makefile",".env.example")) -and
        ($_.Length / 1KB) -le $MaxFileSizeKB
    } |
    Sort-Object FullName

$priorityFilesRaw = foreach ($pat in $PriorityPatterns) {
    Get-ChildItem -Path $Root -Recurse -Force -File -Filter $pat -ErrorAction SilentlyContinue |
        Where-Object {
            -not (Test-IsExcludedPath $_.FullName) -and
            ($_.Length / 1KB) -le $MaxFileSizeKB
        }
}

$priorityFiles = $priorityFilesRaw | Sort-Object FullName -Unique

Set-Content -Path $OutFile -Value "# PROJECT CONTEXT - kronxy-lab" -Encoding UTF8
Add-Line ""
Add-Line "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Add-Line "Root: $Root"
Add-Line ""
Add-Line "Uso recomendado: pega este archivo al inicio del prompt cuando quieras que ChatGPT retome el proyecto."
Add-Line "Modo: $($(if ($IncludeFullSource) { 'FULL SOURCE' } else { 'COMPACTO' }))"
Add-Line ""

Add-Line "## Instrucciones para ChatGPT"
Add-Line ""
Add-Line "- Este archivo es contexto del proyecto local kronxy-lab."
Add-Line "- Prioriza este contexto sobre suposiciones."
Add-Line "- Si falta un archivo específico, pedirlo explícitamente."
Add-Line "- Responder en español, técnico y conciso."
Add-Line "- Cuando propongas cambios, indicar ruta exacta del archivo."
Add-Line ""

Add-Line "## Resumen automático"
Add-Line ""
Add-Line "- Total archivos considerados: $($files.Count)"
Add-Line "- Archivos incluidos por extensión/tamaño: $($includedFiles.Count)"
Add-Line "- Límite por archivo: $MaxLinesPerFile líneas"
Add-Line "- Tamaño máximo por archivo: $MaxFileSizeKB KB"
Add-Line ""

Add-CommandBlock "Git status" {
    Push-Location $Root
    try { git status --short --branch } finally { Pop-Location }
}

Add-CommandBlock "Git últimos commits" {
    Push-Location $Root
    try { git log --oneline -8 } finally { Pop-Location }
}

Add-Line ""
Add-Line "## Soluciones / proyectos detectados"
Add-Line ""
Add-Line '```text'
$proj = $files | Where-Object { $_.Extension -in @(".sln",".csproj",".fsproj",".vbproj",".sqlproj") }
if ($proj.Count -eq 0) {
    Add-Line "[no detectado]"
} else {
    foreach ($f in $proj) { Add-Line (Get-RelativePathSafe $f.FullName) }
}
Add-Line '```'

Add-Line ""
Add-Line "## Estructura del proyecto"
Add-Line ""
Add-Line '```text'
foreach ($item in ($allItems | Sort-Object FullName)) {
    $rel = Get-RelativePathSafe $item.FullName
    if ([string]::IsNullOrWhiteSpace($rel)) { continue }

    $depth = ($rel -split '[\\/]').Count - 1
    if ($depth -gt 5) { continue }

    if ($item.PSIsContainer) {
        Add-Line ("[DIR]  " + $rel)
    } else {
        $sizeKb = [Math]::Round($item.Length / 1KB, 1)
        Add-Line ("[FILE] " + $rel + " (" + $sizeKb + " KB)")
    }
}
Add-Line '```'

Add-Line ""
Add-Line "## Archivos prioritarios"
Add-Line ""
if ($priorityFiles.Count -eq 0) {
    Add-Line "[no detectados]"
} else {
    foreach ($f in $priorityFiles) {
        $rel = Get-RelativePathSafe $f.FullName
        Add-Line ""
        Add-Line "### $rel"
        Add-Line ""
        Add-Line '```text'
        Add-Line (Get-TextPreview -Path $f.FullName -MaxLines $MaxLinesPerFile)
        Add-Line '```'
    }
}

if ($IncludeFullSource) {
    Add-Line ""
    Add-Line "## Contenido adicional"
    foreach ($f in $includedFiles) {
        if ($priorityFiles.FullName -contains $f.FullName) { continue }

        $rel = Get-RelativePathSafe $f.FullName
        Add-Line ""
        Add-Line "### $rel"
        Add-Line ""
        Add-Line '```text'
        Add-Line (Get-TextPreview -Path $f.FullName -MaxLines $MaxLinesPerFile)
        Add-Line '```'
    }
} else {
    Add-Line ""
    Add-Line "## Inventario de archivos incluidos"
    Add-Line ""
    Add-Line '```text'
    foreach ($f in $includedFiles) {
        $rel = Get-RelativePathSafe $f.FullName
        $sizeKb = [Math]::Round($f.Length / 1KB, 1)
        Add-Line "$rel ($sizeKb KB)"
    }
    Add-Line '```'
}

Add-Line ""
Add-Line "## Comando sugerido para actualizar contexto"
Add-Line ""
Add-Line '```powershell'
Add-Line '.\export_context.ps1'
Add-Line '# o con más detalle:'
Add-Line '.\export_context.ps1 -IncludeFullSource'
Add-Line '```'

Write-Host "OK generado: $OutFile"

if ($OpenAfter) {
    Invoke-Item $OutFile
}

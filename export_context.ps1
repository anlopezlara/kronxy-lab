$Root = "E:\dev\kronxy-lab"
$Out  = Join-Path $Root "PROJECT_CONTEXT.md"

$ExcludeDirs = @(".git",".vs",".idea","bin","obj","node_modules","dist","build","target",".gradle",".mvn","__pycache__")
$IncludeExt = @(".cs",".csproj",".sln",".json",".xml",".config",".yml",".yaml",".md",".txt",".ps1",".bat",".cmd",".sql",".js",".ts",".html",".css",".java",".properties")

function IsExcluded($Path) {
    foreach ($d in $ExcludeDirs) {
        if ($Path -like "*\$d\*" -or $Path -like "*\$d") {
            return $true
        }
    }
    return $false
}

Set-Content -Path $Out -Value "# PROJECT CONTEXT - kronxy-lab" -Encoding UTF8
Add-Content -Path $Out -Value ""
Add-Content -Path $Out -Value "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Add-Content -Path $Out -Value "Root: $Root"
Add-Content -Path $Out -Value ""
Add-Content -Path $Out -Value "## TREE"
Add-Content -Path $Out -Value ""

Get-ChildItem $Root -Recurse -Force |
    Where-Object { -not (IsExcluded $_.FullName) } |
    ForEach-Object {
        $rel = $_.FullName.Replace($Root, "").TrimStart("\")
        if ($_.PSIsContainer) {
            Add-Content -Path $Out -Value "[DIR]  $rel"
        } else {
            Add-Content -Path $Out -Value "[FILE] $rel"
        }
    }

Add-Content -Path $Out -Value ""
Add-Content -Path $Out -Value "## FILE CONTENTS"
Add-Content -Path $Out -Value ""

Get-ChildItem $Root -Recurse -File -Force |
    Where-Object {
        -not (IsExcluded $_.FullName) -and
        $IncludeExt -contains $_.Extension.ToLower()
    } |
    Sort-Object FullName |
    ForEach-Object {
        $rel = $_.FullName.Replace($Root, "").TrimStart("\")

        Add-Content -Path $Out -Value ""
        Add-Content -Path $Out -Value "---"
        Add-Content -Path $Out -Value ""
        Add-Content -Path $Out -Value "### FILE: $rel"
        Add-Content -Path $Out -Value ""
        Add-Content -Path $Out -Value "~~~text"

        try {
            $content = Get-Content $_.FullName -Raw -ErrorAction Stop
            Add-Content -Path $Out -Value $content
        } catch {
            Add-Content -Path $Out -Value "[ERROR leyendo archivo]"
        }

        Add-Content -Path $Out -Value "~~~"
    }

Write-Host ("OK generado: " + $Out)
$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $PSScriptRoot 'ES_README.md'
$targetPath = Join-Path $PSScriptRoot 'README.md'
$sourceLines = [System.IO.File]::ReadAllLines($sourcePath, [System.Text.Encoding]::UTF8)
$translatedLines = [System.Collections.Generic.List[string]]::new()
$insideFence = $false
$cache = @{}

foreach ($line in $sourceLines) {
    if ($line.TrimStart().StartsWith('```')) {
        $insideFence = -not $insideFence
        $translatedLines.Add($line)
        continue
    }

    if ($insideFence -or [string]::IsNullOrWhiteSpace($line) -or $line -match '^\s*\|?\s*[-:]+(?:\s*\|\s*[-:]+)+\s*\|?\s*$') {
        $translatedLines.Add($line)
        continue
    }

    $protected = @{}
    $index = 0
    $query = [regex]::Replace($line, '`[^`]+`|https?://[^\s)>]+', {
        param($match)
        $token = "ZXQ$index" + 'XZ'
        $protected[$token] = $match.Value
        $script:index++
        return $token
    })

    if (-not $cache.ContainsKey($query)) {
        $uri = 'https://api.mymemory.translated.net/get?q=' + [uri]::EscapeDataString($query) + '&langpair=es|en'
        $response = Invoke-RestMethod -Uri $uri -Method Get
        if ([int]$response.responseStatus -ne 200 -or [string]::IsNullOrWhiteSpace($response.responseData.translatedText)) {
            throw "Translation failed for: $line"
        }
        $cache[$query] = [System.Net.WebUtility]::HtmlDecode([string]$response.responseData.translatedText)
        Start-Sleep -Milliseconds 80
    }

    $translated = $cache[$query]
    foreach ($entry in $protected.GetEnumerator()) {
        $translated = $translated.Replace($entry.Key, $entry.Value)
    }
    $translatedLines.Add($translated)
}

if ($insideFence) { throw 'The source document contains an unclosed code fence.' }
[System.IO.File]::WriteAllLines($targetPath, $translatedLines, [System.Text.UTF8Encoding]::new($false))
Write-Output "Translated $($sourceLines.Count) lines."

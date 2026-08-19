[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName System.IO.Compression.FileSystem

$files = Get-ChildItem -Path "D:\Yandex.Disk\Revit\Plugins\BimboClub" -Filter "*.docx"
$out = @()

foreach ($file in $files) {
    $out += "=========================================="
    $out += "FILE: " + $file.Name
    $out += "=========================================="
    
    $zip = [System.IO.Compression.ZipFile]::OpenRead($file.FullName)
    $entry = $zip.Entries | Where-Object { $_.FullName -eq 'word/document.xml' }
    if ($entry) {
        $stream = $entry.Open()
        $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8)
        $xmlText = $reader.ReadToEnd()
        $reader.Close()
        $stream.Close()
        
        $xml = [xml]$xmlText
        $paragraphs = $xml.SelectNodes('//*[local-name()="p"]')
        $count = 1
        foreach ($p in $paragraphs) {
            $texts = $p.SelectNodes('.//*[local-name()="t"]')
            $line = ($texts | ForEach-Object { $_.InnerText }) -join ""
            if ($line.Trim() -ne "") {
                $out += "${count}. ${line}"
                $count++
            }
        }
    }
    $zip.Dispose()
}

$out | Out-File -FilePath "TZ_extracted.txt" -Encoding utf8

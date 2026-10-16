# Removes the Explorer context menu entries of 물뷰 (current user only).
$exts = @('.zip','.rar','.7z','.cbz','.cbr','.cb7','.lzh','.pdf','.jpg','.jpeg','.png','.gif','.webp','.bmp','.tif','.tiff','.avif','.jxl','.heic')
foreach ($e in $exts) {
    foreach ($name in @('WaterView', 'OpenInNeeView')) {
        Remove-Item -LiteralPath "HKCU:\Software\Classes\SystemFileAssociations\$e\shell\$name" -Recurse -ErrorAction SilentlyContinue
    }
}
foreach ($name in @('WaterView', 'OpenInNeeView')) {
    Remove-Item -LiteralPath "HKCU:\Software\Classes\Directory\shell\$name" -Recurse -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath "HKCU:\Software\Classes\Directory\Background\shell\$name" -Recurse -ErrorAction SilentlyContinue
}
Write-Host "Removed."

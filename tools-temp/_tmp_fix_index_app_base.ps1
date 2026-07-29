$marketHtml = 'C:\xampp\htdocs\market\index.html'
$rootApp = 'C:\xampp\htdocs\index.app'
$html = Get-Content -Raw $marketHtml
if ($html -notmatch '<base\s') {
  $html = $html.Replace('<head>', "<head>`r`n  <base href=`"/market/`">")
}
Set-Content -Path $rootApp -Value $html -Encoding UTF8
Select-String -Path $rootApp -Pattern 'base href' | Select-Object -First 1

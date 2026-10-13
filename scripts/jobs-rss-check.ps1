$ErrorActionPreference='Stop'
# Published links from the official Jobs.ps RSS index; no guessed endpoints.
$feeds=@(
 @{Name='جفتلك';Slug='jiftlik'},@{Name='بدو';Slug='bidu-jobs'},@{Name='حزما';Slug='hizma'},@{Name='عناتا';Slug='anata'},@{Name='كفر عقب';Slug='kafr-aqab'},@{Name='أبو ديس';Slug='abu-dis'},@{Name='العيزرية';Slug='al-eizariya'},@{Name='الرام';Slug='al-ram'},@{Name='بيرزيت';Slug='birzeit'},@{Name='نابلس';Slug='nablus-jobs'},@{Name='قطاع غزة';Slug='gaza-jobs'},@{Name='القدس';Slug='jerusalem-jobs'},@{Name='طوباس';Slug='tubas-jobs'},@{Name='قلقيلية';Slug='qalqilya-jobs'},@{Name='طولكرم';Slug='tulkarm-jobs'},@{Name='جنين';Slug='jenin-jobs'},@{Name='سلفيت وبديا';Slug='salfit-jobs'},@{Name='أريحا';Slug='jericho-jobs'},@{Name='الخليل';Slug='hebron-jobs'},@{Name='بيت لحم';Slug='bethlehem-jobs'},@{Name='رام الله والبيرة';Slug='ramallah-jobs'},@{Name='روابي';Slug='rawabi-jobs'}
)
$results=@($feeds|ForEach-Object -Parallel {
 $entry=$_;$url='https://www.jobs.ps/rss/jobs/'+$entry.Slug
 try{
  $r=Invoke-WebRequest $url -TimeoutSec 25 -MaximumRedirection 0
  if($r.Content.Length -gt 4MB){throw 'Feed exceeds limit'}
  $settings=[Xml.XmlReaderSettings]::new();$settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$settings.XmlResolver=$null;$settings.MaxCharactersInDocument=4MB
  $reader=[Xml.XmlReader]::Create([IO.StringReader]::new($r.Content),$settings)
  try{$doc=[Xml.XmlDocument]::new();$doc.XmlResolver=$null;$doc.Load($reader)}finally{$reader.Dispose()}
  if($doc.DocumentElement.Name -ne 'rss'){throw 'Not RSS'}
  $items=@($doc.SelectNodes('/rss/channel/item'))
  $links=@($items|ForEach-Object {$_.SelectSingleNode('link').InnerText})
  $lengths=@($items|ForEach-Object {[Net.WebUtility]::HtmlDecode([regex]::Replace($_.SelectSingleNode('description').InnerText,'<[^>]+>',' ')).Trim().Length})
  [pscustomobject]@{Name=$entry.Name;Url=$url;Status=[int]$r.StatusCode;Items=$items.Count;Readable=@($lengths|Where-Object {$_ -ge 80}).Count;MinChars=if($lengths.Count){($lengths|Measure-Object -Minimum).Minimum}else{0};Links=$links;Error=''}
 }catch{[pscustomobject]@{Name=$entry.Name;Url=$url;Status=if($_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{0};Items=0;Readable=0;MinChars=0;Links=@();Error=$_.Exception.Message}}
} -ThrottleLimit 2)
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$results|Sort-Object Name|ConvertTo-Json -Depth 5|Set-Content (Join-Path $root 'artifacts/jobs-rss-check.json')
$results|Sort-Object Name|Select-Object Name,Status,Items,Readable,MinChars,Error|Format-Table -AutoSize
$total=($results|Measure-Object Items -Sum).Sum;$unique=@($results.Links|Sort-Object -Unique).Count
Write-Output ('Feeds='+$results.Count+'; HTTP200='+@($results|Where-Object Status -eq 200).Count+'; Entries='+$total+'; UniqueLinks='+$unique)

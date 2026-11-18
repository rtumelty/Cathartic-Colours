# roadmap-import.ps1
# Imports docs/wiki/roadmap.md into GitHub Issues and the Cathartic Colours project.
# Dry run by default. Pass -Apply to write. Targets Windows PowerShell 5.1.
#
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\roadmap-import.ps1 [-Ids CC-006,CC-020] [-ShowBodies] [-Apply]
#
# One-time import, run for CC-034.2 on 2026-11-18. Since CC-034.3, roadmap.md is a strategy page
# without the item table and detail sections this script reads, so it finds no items and does
# nothing. Track items with gh directly (project CLAUDE.md §6, ADR-002).

param(
    [switch]$Apply,
    [string[]]$Ids,
    [switch]$ShowBodies
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch { }

# ------------------------------------------------------------------ constants
$Owner = 'rtumelty'
$Repo = 'Cathartic-Colours'
$ProjectNumber = 1
$RoadmapPath = 'docs/wiki/roadmap.md'
$WikiBase = 'https://github.com/rtumelty/Cathartic-Colours/blob/develop/docs/wiki/'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Footer = '_Imported from `docs/wiki/roadmap.md` by `tools/roadmap-import.ps1`._'
$Dash = [string][char]0x2013
$KnownPhases = @('Foundations', 'Core refactor', 'Quick fixes', 'Gameplay', 'Platform and monetisation', 'Polish')
$DefaultPhase = 'Quick fixes'

$QMeta = 'query($login: String!, $number: Int!) { user(login: $login) { projectV2(number: $number) { id fields(first: 100) { nodes { ... on ProjectV2SingleSelectField { id name options { id name } } } } } } }'
$QItems = 'query($login: String!, $number: Int!, $cursor: String) { user(login: $login) { projectV2(number: $number) { items(first: 100, after: $cursor) { pageInfo { hasNextPage endCursor } nodes { id content { ... on Issue { number } } fieldValues(first: 50) { nodes { ... on ProjectV2ItemFieldSingleSelectValue { name field { ... on ProjectV2FieldCommon { name } } } } } } } } } }'
$QSubs = 'query($owner: String!, $name: String!, $num: Int!) { repository(owner: $owner, name: $name) { issue(number: $num) { subIssues(first: 50) { nodes { number } } } } }'
$QAddItem = 'mutation($project: ID!, $content: ID!) { addProjectV2ItemById(input: { projectId: $project, contentId: $content }) { item { id } } }'
$QSetField = 'mutation($project: ID!, $item: ID!, $field: ID!, $option: String!) { updateProjectV2ItemFieldValue(input: { projectId: $project, itemId: $item, fieldId: $field, value: { singleSelectOptionId: $option } }) { projectV2Item { id } } }'
$QAddSub = 'mutation($parent: ID!, $child: ID!) { addSubIssue(input: { issueId: $parent, subIssueId: $child }) { issue { id } } }'

# ------------------------------------------------------------------ state
$script:Gh = ''
$script:Warnings = New-Object System.Collections.ArrayList
$script:Items = @{}
$script:ItemIds = @()
$script:ItemOrder = @()
$script:Existing = @{}
$script:SubsOf = @{}
$script:ProjId = ''
$script:Fields = @{}
$script:ProjByNumber = @{}
$script:RepoLabels = @()
$script:Recs = @{}
$script:Order = New-Object System.Collections.ArrayList
$script:Counts = @{ create = 0; close = 0; reopen = 0; projectAdd = 0; fieldSet = 0; link = 0 }

# ------------------------------------------------------------------ small helpers
function Add-Warning([string]$Message) {
    Write-Host $Message
    [void]$script:Warnings.Add($Message)
}

function Format-Line([string]$Id, [string]$Action, [string]$Text) {
    return ($Id.PadRight(8) + ' ' + $Action.PadRight(7) + ' ' + $Text)
}

function Get-Prop($Obj, [string]$Name) {
    if ($null -eq $Obj) { return $null }
    $p = $Obj.PSObject.Properties[$Name]
    if ($null -eq $p) { return $null }
    return $p.Value
}

function ConvertFrom-JsonArray([string]$Text) {
    $v = $Text | ConvertFrom-Json
    $list = New-Object System.Collections.ArrayList
    foreach ($e in @($v)) { [void]$list.Add($e) }
    return ,$list.ToArray()
}

function Get-NormText($Value) {
    if ($null -eq $Value) { return '' }
    return (([string]$Value) -replace "`r`n", "`n").Trim()
}

function Test-Managed([string]$Name) {
    return ($Name.StartsWith('type:') -or $Name.StartsWith('severity:') -or $Name.StartsWith('size:'))
}

function Expand-CcIds([string]$Text) {
    $result = New-Object System.Collections.ArrayList
    $pattern = 'CC-(\d{3})(?:\s*[-' + $Dash + ']\s*CC-(\d{3}))?'
    foreach ($m in [regex]::Matches($Text, $pattern)) {
        $a = [int]$m.Groups[1].Value
        if ($m.Groups[2].Success) {
            $b = [int]$m.Groups[2].Value
            if ($b -lt $a) { $t = $a; $a = $b; $b = $t }
            for ($n = $a; $n -le $b; $n++) { [void]$result.Add('CC-' + $n.ToString('000')) }
        }
        else {
            [void]$result.Add('CC-' + $m.Groups[1].Value)
        }
    }
    return ,$result.ToArray()
}

function Convert-Links([string]$Text) {
    # Rewrites relative markdown link targets to absolute wiki URLs. Link text is untouched.
    $sb = New-Object System.Text.StringBuilder
    $last = 0
    foreach ($m in [regex]::Matches($Text, '\]\(([^)]+)\)')) {
        $target = $m.Groups[1].Value
        if ($target -match '^(https?:|mailto:)') { $new = $target }
        elseif ($target.StartsWith('#')) { $new = $WikiBase + 'roadmap.md' + $target }
        else { $new = $WikiBase + $target }
        [void]$sb.Append($Text.Substring($last, $m.Index - $last))
        [void]$sb.Append('](' + $new + ')')
        $last = $m.Index + $m.Length
    }
    [void]$sb.Append($Text.Substring($last))
    return $sb.ToString()
}

function Test-Option([string]$Field, [string]$Value) {
    return ($script:Fields.ContainsKey($Field) -and $script:Fields[$Field]['Options'].ContainsKey($Value))
}

function Assert-Option([string]$Field, [string]$Value, [string]$Where) {
    if (-not (Test-Option $Field $Value)) {
        throw ($Where + ": '" + $Value + "' is not an option of project field " + $Field)
    }
}

function Get-OptionId([string]$Field, [string]$Value) {
    Assert-Option $Field $Value $Field
    return [string]$script:Fields[$Field]['Options'][$Value]
}

function Get-Ref([string]$Id) {
    $r = $script:Recs[$Id]
    if ($null -ne $r -and $null -ne $r['Number']) { return ('#' + [string]$r['Number']) }
    return ('#?' + $Id)
}

function Get-Body($Rec) {
    $parts = New-Object System.Collections.ArrayList
    $bold = New-Object System.Collections.ArrayList
    if ($Rec['IsSub']) {
        [void]$bold.Add('**Risk:** ' + [string]$Rec['Risk'])
        [void]$bold.Add('**Worker:** ' + [string]$Rec['Worker'])
    }
    else {
        if ([bool]$Rec['HasLinks']) { [void]$bold.Add('**Links:** ' + [string]$Rec['LinksText']) }
        $deps = @($Rec['Deps'])
        if ($deps.Count -gt 0) {
            $refs = @()
            foreach ($d in $deps) { $refs += (Get-Ref ([string]$d)) }
            [void]$bold.Add('**Depends on:** ' + ($refs -join ', '))
        }
        if ($null -ne $Rec['PartOf']) { [void]$bold.Add('**Part of:** ' + (Get-Ref ([string]$Rec['PartOf']))) }
    }
    if (([string]$Rec['Description']) -ne '') { [void]$parts.Add([string]$Rec['Description']) }
    if ($bold.Count -gt 0) { [void]$parts.Add(($bold -join "`n")) }
    [void]$parts.Add($Footer)
    return (($parts -join "`n`n").Trim())
}

function Get-LabelPlan($Current, $Wanted) {
    $cur = @($Current)
    $want = @($Wanted)
    $curManaged = @($cur | Where-Object { Test-Managed ([string]$_) })
    $keep = @($cur | Where-Object { -not (Test-Managed ([string]$_)) })
    $plus = @($want | Where-Object { $curManaged -notcontains $_ })
    $minus = @($curManaged | Where-Object { $want -notcontains $_ })
    return @{ Plus = $plus; Minus = $minus; Full = @($keep + $want) }
}

# ------------------------------------------------------------------ gh access
function Invoke-Gh {
    param([string[]]$GhArgs)
    $raw = & $script:Gh @GhArgs
    $code = $LASTEXITCODE
    $text = (@($raw | ForEach-Object { [string]$_ })) -join "`n"
    if ($code -ne 0) {
        throw ('gh ' + ($GhArgs -join ' ') + ' failed with exit code ' + $code + ': ' + $text)
    }
    return $text
}

function Invoke-GhApi {
    param([string]$Method, [string]$Path, $Body = $null)
    if ($null -eq $Body) {
        $text = Invoke-Gh @('api', '--method', $Method, $Path)
    }
    else {
        $json = ConvertTo-Json -InputObject $Body -Depth 20
        $tmp = Join-Path ([IO.Path]::GetTempPath()) ('roadmap-import-' + [guid]::NewGuid().ToString('N') + '.json')
        try {
            [IO.File]::WriteAllText($tmp, $json, (New-Object Text.UTF8Encoding $false))
            $text = Invoke-Gh @('api', '--method', $Method, $Path, '--input', $tmp)
        }
        finally {
            if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force }
        }
    }
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    return ($text | ConvertFrom-Json)
}

function Invoke-GhGraphQL {
    param([string]$Query, $Variables = @{})
    $resp = Invoke-GhApi -Method 'POST' -Path 'graphql' -Body @{ query = $Query; variables = $Variables }
    $errors = Get-Prop $resp 'errors'
    if ($null -ne $errors) {
        throw ('GraphQL error: ' + (ConvertTo-Json -InputObject $errors -Depth 10))
    }
    return (Get-Prop $resp 'data')
}

# Every write (REST POST/PATCH or GraphQL mutation) goes through here.
# Dry run: prints the description and returns $null. -Apply: prints it and runs the action.
function Invoke-Write {
    param([string]$Description, [scriptblock]$Action, [object[]]$ArgList = @())
    Write-Host $Description
    if ($Apply) {
        $result = & $Action @ArgList
        # Pace writes to stay under GitHub's secondary rate limit for content creation.
        Start-Sleep -Milliseconds 1000
        return $result
    }
    return $null
}

# ------------------------------------------------------------------ roadmap parsing
function Read-Detail($Item, $Section) {
    $seenHeader = $false
    $desc = New-Object System.Collections.ArrayList
    foreach ($raw in $Section) {
        $t = ([string]$raw).Trim()
        if ($t -eq '') { continue }
        if (-not $seenHeader) { $seenHeader = $true; continue }
        if ($t -match '^Links:\s*(.*)$') {
            $Item['HasLinks'] = $true
            $Item['LinksText'] = $matches[1].Trim()
            continue
        }
        if ($t -match '^\|\s*(CC-\d{3}\.\d+)\s*\|') {
            $dvId = $matches[1]
            $cells = @($t.Split('|'))
            if ($cells.Count -lt 7) { throw ('Deliverable row ' + $dvId + ' has ' + $cells.Count + ' cells') }
            $dv = @{
                Id = $dvId
                ParentId = [string]$Item['Id']
                Title = $cells[2].Trim()
                Risk = $cells[3].Trim()
                Worker = $cells[4].Trim()
                Status = $cells[5].Trim()
            }
            [void]$Item['Deliverables'].Add($dv)
            continue
        }
        if ($t.StartsWith('|')) { continue }
        [void]$desc.Add($t)
    }
    $Item['Description'] = ($desc -join "`n")
}

function Read-Roadmap {
    $full = Join-Path $RepoRoot $RoadmapPath
    $lines = @(Get-Content -LiteralPath $full -Encoding UTF8)
    $idList = New-Object System.Collections.ArrayList

    # 1. Summary table
    foreach ($line in $lines) {
        if ($line -match '^\|\s*\[(CC-\d{3})\]') {
            $id = $matches[1]
            $cells = @($line.Split('|'))
            if ($cells.Count -lt 9) { throw ('Summary row ' + $id + ' has ' + $cells.Count + ' cells') }
            if ($script:Items.ContainsKey($id)) { throw ('Duplicate summary row for ' + $id) }
            $script:Items[$id] = @{
                Id = $id
                Title = $cells[2].Trim()
                Type = $cells[3].Trim()
                Severity = $cells[4].Trim()
                Size = $cells[5].Trim()
                Status = $cells[6].Trim()
                DependsRaw = $cells[7].Trim()
                Phase = ''
                Heading = ''
                Description = ''
                HasLinks = $false
                LinksText = ''
                Deps = @()
                PartOf = $null
                Deliverables = (New-Object System.Collections.ArrayList)
            }
            [void]$idList.Add($id)
        }
    }

    # 2. Phase per item from ### Suggested order (first phase that mentions an id wins)
    $phaseOf = @{}
    $inOrder = $false
    foreach ($line in $lines) {
        if ($line -match '^### Suggested order') { $inOrder = $true; continue }
        if ($inOrder -and ($line -match '^#{1,3} ' -or $line -match '^---')) { $inOrder = $false }
        if ($inOrder -and $line -match '^\d+\.\s+\*\*(.+?)\*\*') {
            # "Quick fixes, any time:" -> "Quick fixes"
            $phaseName = ($matches[1].Trim().TrimEnd(':').Split(',')[0]).Trim()
            foreach ($cid in (Expand-CcIds $line)) {
                if (-not $phaseOf.ContainsKey($cid)) { $phaseOf[$cid] = $phaseName }
            }
        }
    }

    # 3. Detail sections (### CC-nnn) and the ## heading they sit under
    $h2 = ''
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match '^## (.+?)\s*$') { $h2 = $matches[1]; continue }
        if ($line -match '^### (CC-\d{3})\s*$') {
            $id = $matches[1]
            if (-not $script:Items.ContainsKey($id)) { throw ('Detail section ' + $id + ' has no summary row') }
            $script:Items[$id]['Heading'] = $h2
            $section = New-Object System.Collections.ArrayList
            $j = $i + 1
            while ($j -lt $lines.Count) {
                $l = $lines[$j]
                if ($l -match '^### ' -or $l -match '^## ' -or $l -match '^---' -or $l -match '^_CC-\d{3} to CC-\d{3}') { break }
                [void]$section.Add($l)
                $j++
            }
            Read-Detail ($script:Items[$id]) $section
            $i = $j - 1
        }
    }

    # 4. Phase fallback, link rewriting, dependencies
    foreach ($id in $idList) {
        $it = $script:Items[$id]
        # The item's own ## heading wins; the suggested order is the fallback
        # (it mentions some ids in other phases, e.g. "CC-031 before CC-013").
        if ($KnownPhases -contains $it['Heading']) {
            $it['Phase'] = $it['Heading']
        }
        elseif ($phaseOf.ContainsKey($id)) {
            $it['Phase'] = $phaseOf[$id]
        }
        else {
            $it['Phase'] = $DefaultPhase
            Add-Warning ('WARN ' + $id + ': no phase in suggested order; defaulting to Quick fixes')
        }
        $it['Description'] = Convert-Links ([string]$it['Description'])
        $it['LinksText'] = Convert-Links ([string]$it['LinksText'])
        $raw = [string]$it['DependsRaw']
        if ($raw -match '^in\s+(CC-\d{3})') {
            $it['PartOf'] = $matches[1]
        }
        else {
            $it['Deps'] = @([regex]::Matches($raw, 'CC-\d{3}') | ForEach-Object { $_.Value })
        }
    }

    $script:ItemIds = @($idList | Sort-Object)
}

# ------------------------------------------------------------------ GitHub state (reads only)
function Read-GitHubState {
    Write-Host ('Reading issues from ' + $Owner + '/' + $Repo)
    $issueText = Invoke-Gh @('issue', 'list', '--repo', ($Owner + '/' + $Repo), '--state', 'all', '--limit', '1000', '--json', 'number,id,title,body,state,labels,url')
    $issues = ConvertFrom-JsonArray $issueText
    foreach ($iss in $issues) {
        $t = [string](Get-Prop $iss 'title')
        if (-not ($t -match '^(CC-\d{3}(\.\d+)?):')) { continue }
        $key = $matches[1]
        if ($script:Existing.ContainsKey($key)) { throw ('Two issues match ' + $key) }
        $names = @()
        foreach ($lb in @(Get-Prop $iss 'labels')) { $names += [string](Get-Prop $lb 'name') }
        $script:Existing[$key] = @{
            Number = [int](Get-Prop $iss 'number')
            NodeId = [string](Get-Prop $iss 'id')
            Title = $t
            State = [string](Get-Prop $iss 'state')
            Labels = $names
            Body = (Get-NormText (Get-Prop $iss 'body'))
        }
    }

    # Sub-issues of each existing item
    foreach ($key in @($script:Existing.Keys)) {
        if ($key -match '\.') { continue }
        $num = $script:Existing[$key]['Number']
        $data = Invoke-GhGraphQL -Query $QSubs -Variables @{ owner = $Owner; name = $Repo; num = $num }
        $issueObj = Get-Prop (Get-Prop $data 'repository') 'issue'
        $childNodes = @(Get-Prop (Get-Prop $issueObj 'subIssues') 'nodes')
        $nums = New-Object System.Collections.ArrayList
        foreach ($cn in $childNodes) { [void]$nums.Add([int](Get-Prop $cn 'number')) }
        $script:SubsOf[$key] = $nums.ToArray()
    }

    Write-Host ('Reading project ' + $ProjectNumber + ' for ' + $Owner)
    $meta = Invoke-GhGraphQL -Query $QMeta -Variables @{ login = $Owner; number = $ProjectNumber }
    $pv = Get-Prop (Get-Prop $meta 'user') 'projectV2'
    if ($null -eq $pv) { throw ('Project ' + $ProjectNumber + ' not found for ' + $Owner) }
    $script:ProjId = [string](Get-Prop $pv 'id')
    foreach ($f in @(Get-Prop (Get-Prop $pv 'fields') 'nodes')) {
        $fname = Get-Prop $f 'name'
        if ($null -eq $fname) { continue }
        $opts = @{}
        foreach ($o in @(Get-Prop $f 'options')) { $opts[[string](Get-Prop $o 'name')] = [string](Get-Prop $o 'id') }
        $script:Fields[[string]$fname] = @{ Id = [string](Get-Prop $f 'id'); Options = $opts }
    }
    foreach ($req in @('Status', 'Severity', 'Size', 'Phase')) {
        if (-not $script:Fields.ContainsKey($req)) { throw ('Project field ' + $req + ' not found') }
    }

    $cursor = $null
    $hasNext = $true
    while ($hasNext) {
        $data = Invoke-GhGraphQL -Query $QItems -Variables @{ login = $Owner; number = $ProjectNumber; cursor = $cursor }
        $page = Get-Prop (Get-Prop (Get-Prop $data 'user') 'projectV2') 'items'
        foreach ($nd in @(Get-Prop $page 'nodes')) {
            $num = Get-Prop (Get-Prop $nd 'content') 'number'
            if ($null -eq $num) { continue }
            $entry = @{ ItemId = [string](Get-Prop $nd 'id'); Fields = @{} }
            foreach ($fv in @(Get-Prop (Get-Prop $nd 'fieldValues') 'nodes')) {
                $fname = Get-Prop (Get-Prop $fv 'field') 'name'
                $fval = Get-Prop $fv 'name'
                if ($null -ne $fname -and $null -ne $fval) { $entry['Fields'][[string]$fname] = [string]$fval }
            }
            $script:ProjByNumber[[string]$num] = $entry
        }
        $pi = Get-Prop $page 'pageInfo'
        $hasNext = [bool](Get-Prop $pi 'hasNextPage')
        $cursor = Get-Prop $pi 'endCursor'
    }

    $labelText = Invoke-Gh @('label', 'list', '--repo', ($Owner + '/' + $Repo), '--limit', '200', '--json', 'name')
    $labelObjs = ConvertFrom-JsonArray $labelText
    $script:RepoLabels = @()
    foreach ($lo in $labelObjs) { $script:RepoLabels += [string](Get-Prop $lo 'name') }
}

function Attach-Existing($Rec) {
    $key = [string]$Rec['Id']
    if (-not $script:Existing.ContainsKey($key)) { return }
    $e = $script:Existing[$key]
    $Rec['Exists'] = $true
    $Rec['Number'] = $e['Number']
    $Rec['NodeId'] = $e['NodeId']
    $Rec['CurTitle'] = $e['Title']
    $Rec['CurLabels'] = $e['Labels']
    $Rec['CurState'] = $e['State']
    $Rec['CurBody'] = $e['Body']
    $pe = $script:ProjByNumber[[string]$e['Number']]
    if ($null -ne $pe) {
        $Rec['ProjectItemId'] = $pe['ItemId']
        $Rec['ProjectFields'] = $pe['Fields']
    }
}

# ------------------------------------------------------------------ records (wanted state)
function Build-Records {
    foreach ($id in $script:ItemIds) {
        $it = $script:Items[$id]

        $phase = [string]$it['Phase']
        if (-not (Test-Option 'Phase' $phase)) {
            $base = ($phase.Split(',')[0]).Trim()
            if (Test-Option 'Phase' $base) {
                Add-Warning ('WARN ' + $id + ": phase '" + $phase + "' is not a Phase option; using '" + $base + "'")
                $phase = $base
            }
            else {
                throw ($id + ": phase '" + $phase + "' is not a Phase option")
            }
        }
        $it['Phase'] = $phase
        Assert-Option 'Status' ([string]$it['Status']) $id
        Assert-Option 'Severity' ([string]$it['Severity']) $id
        Assert-Option 'Size' ([string]$it['Size']) $id

        $type = ([string]$it['Type']).ToLowerInvariant()
        $sev = ([string]$it['Severity']).ToLowerInvariant()
        $labels = @(('type:' + $type), ('severity:' + $sev), ('size:' + [string]$it['Size']))
        foreach ($l in $labels) {
            if ($script:RepoLabels -notcontains $l) { Add-Warning ('WARN ' + $id + ': label ' + $l + ' does not exist in the repo yet') }
        }

        $wantFields = [ordered]@{
            Status = [string]$it['Status']
            Severity = [string]$it['Severity']
            Size = [string]$it['Size']
            Phase = $phase
        }
        $rec = @{
            Id = $id; IsSub = $false; ParentId = ''
            WantTitle = ($id + ': ' + [string]$it['Title'])
            WantLabels = $labels
            WantOpen = ([string]$it['Status'] -ne 'Done')
            WantFields = $wantFields
            Description = [string]$it['Description']
            HasLinks = [bool]$it['HasLinks']
            LinksText = [string]$it['LinksText']
            Deps = @($it['Deps'])
            PartOf = $it['PartOf']
            Risk = ''; Worker = ''
            Exists = $false; Number = $null; NodeId = ''
            CurTitle = ''; CurLabels = @(); CurState = ''; CurBody = ''
            ProjectItemId = $null; ProjectFields = @{}
            Acts = 0; Touched = $false; Updated = $false; Created = $false
        }
        Attach-Existing $rec
        $script:Recs[$id] = $rec

        foreach ($dv in @($it['Deliverables'])) {
            $dvId = [string]$dv['Id']
            Assert-Option 'Status' ([string]$dv['Status']) $dvId
            $drec = @{
                Id = $dvId; IsSub = $true; ParentId = $id
                WantTitle = ($dvId + ': ' + [string]$dv['Title'])
                WantLabels = @()
                WantOpen = ([string]$dv['Status'] -ne 'Done')
                WantFields = [ordered]@{ Status = [string]$dv['Status'] }
                Description = ''; HasLinks = $false; LinksText = ''; Deps = @(); PartOf = $null
                Risk = [string]$dv['Risk']; Worker = [string]$dv['Worker']
                Exists = $false; Number = $null; NodeId = ''
                CurTitle = ''; CurLabels = @(); CurState = ''; CurBody = ''
                ProjectItemId = $null; ProjectFields = @{}
                Acts = 0; Touched = $false; Updated = $false; Created = $false
            }
            Attach-Existing $drec
            $script:Recs[$dvId] = $drec
        }
    }
}

# ------------------------------------------------------------------ actions
function Update-Body($Rec, [string]$Want) {
    $desc = Format-Line ([string]$Rec['Id']) 'UPDATE' 'body'
    [void](Invoke-Write -Description $desc -Action { param($n, $b) Invoke-GhApi -Method 'PATCH' -Path ("repos/$Owner/$Repo/issues/$n") -Body @{ body = $b } } -ArgList @($Rec['Number'], $Want))
    $Rec['CurBody'] = $Want
    $Rec['Touched'] = $true
    $Rec['Updated'] = $true
    $Rec['Acts']++
}

function Sync-Record($Rec) {
    $id = [string]$Rec['Id']
    $title = [string]$Rec['WantTitle']
    $labels = @($Rec['WantLabels'])
    $wantBody = Get-Body $Rec

    if (-not $Rec['Exists']) {
        $kind = 'issue'
        if ($Rec['IsSub']) { $kind = 'sub-issue' }
        $labelText = ''
        if ($labels.Count -gt 0) { $labelText = '  [' + ($labels -join ', ') + ']' }
        $desc = Format-Line $id 'CREATE' ($kind + ' "' + $title + '"' + $labelText)
        $resp = Invoke-Write -Description $desc -Action { param($t, $b, $l) Invoke-GhApi -Method 'POST' -Path ("repos/$Owner/$Repo/issues") -Body @{ title = $t; body = $b; labels = @($l) } } -ArgList @($title, $wantBody, $labels)
        $Rec['Exists'] = $true
        $Rec['CurTitle'] = $title
        $Rec['CurLabels'] = $labels
        $Rec['CurState'] = 'OPEN'
        $Rec['CurBody'] = $wantBody
        $Rec['Created'] = $true
        $Rec['Touched'] = $true
        $Rec['Acts']++
        $script:Counts['create']++
        if ($Apply) {
            $Rec['Number'] = [int](Get-Prop $resp 'number')
            $Rec['NodeId'] = [string](Get-Prop $resp 'node_id')
        }
    }
    else {
        if ([string]$Rec['CurTitle'] -cne $title) {
            $desc = Format-Line $id 'UPDATE' ('title: "' + [string]$Rec['CurTitle'] + '" -> "' + $title + '"')
            [void](Invoke-Write -Description $desc -Action { param($n, $t) Invoke-GhApi -Method 'PATCH' -Path ("repos/$Owner/$Repo/issues/$n") -Body @{ title = $t } } -ArgList @($Rec['Number'], $title))
            $Rec['CurTitle'] = $title
            $Rec['Touched'] = $true
            $Rec['Updated'] = $true
            $Rec['Acts']++
        }

        $plan = Get-LabelPlan ($Rec['CurLabels']) $labels
        if (@($plan['Plus']).Count -gt 0 -or @($plan['Minus']).Count -gt 0) {
            $parts = @()
            foreach ($a in @($plan['Plus'])) { $parts += ('+' + $a) }
            foreach ($r in @($plan['Minus'])) { $parts += ('-' + $r) }
            $desc = Format-Line $id 'UPDATE' ('labels: ' + ($parts -join ' '))
            [void](Invoke-Write -Description $desc -Action { param($n, $l) Invoke-GhApi -Method 'PATCH' -Path ("repos/$Owner/$Repo/issues/$n") -Body @{ labels = @($l) } } -ArgList @($Rec['Number'], $plan['Full']))
            $Rec['CurLabels'] = $plan['Full']
            $Rec['Touched'] = $true
            $Rec['Updated'] = $true
            $Rec['Acts']++
        }

        # Items whose body still has #?CC placeholders are compared in pass 2 instead.
        if (-not $wantBody.Contains('#?') -and ($wantBody -cne [string]$Rec['CurBody'])) {
            Update-Body $Rec $wantBody
        }
    }

    $wantOpen = [bool]$Rec['WantOpen']
    if ($wantOpen -and [string]$Rec['CurState'] -eq 'CLOSED') {
        $desc = Format-Line $id 'REOPEN' ('(status ' + [string]$Rec['WantFields']['Status'] + ')')
        [void](Invoke-Write -Description $desc -Action { param($n) Invoke-GhApi -Method 'PATCH' -Path ("repos/$Owner/$Repo/issues/$n") -Body @{ state = 'open' } } -ArgList @($Rec['Number']))
        $Rec['CurState'] = 'OPEN'
        $Rec['Acts']++
        $script:Counts['reopen']++
    }
    elseif (-not $wantOpen -and [string]$Rec['CurState'] -eq 'OPEN') {
        $desc = Format-Line $id 'CLOSE' '(Done)'
        [void](Invoke-Write -Description $desc -Action { param($n) Invoke-GhApi -Method 'PATCH' -Path ("repos/$Owner/$Repo/issues/$n") -Body @{ state = 'closed'; state_reason = 'completed' } } -ArgList @($Rec['Number']))
        $Rec['CurState'] = 'CLOSED'
        $Rec['Acts']++
        $script:Counts['close']++
    }
}

function Sync-Project($Rec) {
    $id = [string]$Rec['Id']
    $want = $Rec['WantFields']

    if ($null -eq $Rec['ProjectItemId']) {
        $texts = @()
        $plan = New-Object System.Collections.ArrayList
        foreach ($name in $want.Keys) {
            $texts += ([string]$name + '=' + [string]$want[$name])
            [void]$plan.Add(@{ FieldId = [string]$script:Fields[[string]$name]['Id']; OptionId = (Get-OptionId ([string]$name) ([string]$want[$name])) })
        }
        $planArr = $plan.ToArray()
        $desc = Format-Line $id 'PROJECT' ('add; ' + ($texts -join ' '))
        $itemId = Invoke-Write -Description $desc -Action {
            param($content, $fieldPlan)
            $added = Invoke-GhGraphQL -Query $QAddItem -Variables @{ project = $script:ProjId; content = $content }
            $newItem = [string](Get-Prop (Get-Prop (Get-Prop $added 'addProjectV2ItemById') 'item') 'id')
            foreach ($fp in $fieldPlan) {
                [void](Invoke-GhGraphQL -Query $QSetField -Variables @{ project = $script:ProjId; item = $newItem; field = $fp['FieldId']; option = $fp['OptionId'] })
            }
            return $newItem
        } -ArgList @($Rec['NodeId'], $planArr)
        $Rec['ProjectFields'] = @{}
        foreach ($name in $want.Keys) { $Rec['ProjectFields'][[string]$name] = $want[$name] }
        if ($Apply) { $Rec['ProjectItemId'] = [string]$itemId } else { $Rec['ProjectItemId'] = 'dry-run' }
        $Rec['Acts']++
        $script:Counts['projectAdd']++
        $script:Counts['fieldSet'] += $planArr.Count
        return
    }

    foreach ($name in $want.Keys) {
        $cur = $null
        if ($Rec['ProjectFields'].ContainsKey([string]$name)) { $cur = $Rec['ProjectFields'][[string]$name] }
        if ($cur -cne $want[$name]) {
            $curText = '(none)'
            if ($null -ne $cur) { $curText = [string]$cur }
            $desc = Format-Line $id 'PROJECT' ('SET ' + [string]$name + ': ' + $curText + ' -> ' + [string]$want[$name])
            $optId = Get-OptionId ([string]$name) ([string]$want[$name])
            $fieldId = [string]$script:Fields[[string]$name]['Id']
            [void](Invoke-Write -Description $desc -Action { param($item, $field, $option) Invoke-GhGraphQL -Query $QSetField -Variables @{ project = $script:ProjId; item = $item; field = $field; option = $option } } -ArgList @($Rec['ProjectItemId'], $fieldId, $optId))
            $Rec['ProjectFields'][[string]$name] = $want[$name]
            $Rec['Acts']++
            $script:Counts['fieldSet']++
        }
    }
}

function Link-Sub($Rec) {
    $parentId = [string]$Rec['ParentId']
    $parent = $script:Recs[$parentId]
    $linked = $false
    if ($null -ne $Rec['Number'] -and $script:SubsOf.ContainsKey($parentId)) {
        $linked = (@($script:SubsOf[$parentId]) -contains $Rec['Number'])
    }
    if ($linked) { return }
    $ref = 'sub-issue of ' + $parentId
    if ($null -eq $parent['Number']) { $ref = $ref + ' (new)' }
    $desc = Format-Line ([string]$Rec['Id']) 'LINK' $ref
    [void](Invoke-Write -Description $desc -Action { param($p, $c) Invoke-GhGraphQL -Query $QAddSub -Variables @{ parent = $p; child = $c } } -ArgList @($parent['NodeId'], $Rec['NodeId']))
    if ($Apply) {
        if (-not $script:SubsOf.ContainsKey($parentId)) { $script:SubsOf[$parentId] = @() }
        $script:SubsOf[$parentId] = @($script:SubsOf[$parentId]) + @([int]$Rec['Number'])
    }
    $Rec['Acts']++
    $script:Counts['link']++
}

function Report-Unchanged($Rec) {
    if ([int]$Rec['Acts'] -eq 0) { Write-Host (Format-Line ([string]$Rec['Id']) 'OK' 'unchanged') }
}

# ================================================================== main
Read-Roadmap

$cmd = Get-Command gh -ErrorAction SilentlyContinue
if ($null -ne $cmd) {
    $script:Gh = [string]$cmd.Source
}
elseif (Test-Path -LiteralPath 'C:\Program Files\GitHub CLI\gh.exe') {
    $script:Gh = 'C:\Program Files\GitHub CLI\gh.exe'
}
else {
    throw 'gh.exe not found on PATH or at C:\Program Files\GitHub CLI\gh.exe'
}

$filter = @()
if ($null -ne $Ids) {
    $filter = @($Ids | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToUpperInvariant() } | Where-Object { $_ -ne '' })
}
foreach ($f in $filter) {
    if (-not $script:Items.ContainsKey($f)) { throw ('Unknown roadmap item ' + $f) }
}
if ($filter.Count -gt 0) {
    $script:ItemOrder = @($script:ItemIds | Where-Object { $filter -contains $_ })
}
else {
    $script:ItemOrder = @($script:ItemIds)
}

Read-GitHubState
Build-Records

$mode = 'DRY RUN'
if ($Apply) { $mode = 'APPLIED' }
Write-Host ('Mode: ' + $mode + $(if ($Apply) { '' } else { ' (no writes; -Apply to write)' }))

# Pass 1: items, then their deliverables
foreach ($id in $script:ItemOrder) {
    $rec = $script:Recs[$id]
    [void]$script:Order.Add($id)
    Sync-Record $rec
    Sync-Project $rec
    Report-Unchanged $rec
    foreach ($dv in @($script:Items[$id]['Deliverables'])) {
        $dvId = [string]$dv['Id']
        $drec = $script:Recs[$dvId]
        [void]$script:Order.Add($dvId)
        Sync-Record $drec
        Sync-Project $drec
        Link-Sub $drec
        Report-Unchanged $drec
    }
}

# Pass 2: rebuild item bodies now that every issue number is known
foreach ($id in $script:Order) {
    $rec = $script:Recs[$id]
    if ($rec['IsSub']) { continue }
    if ($null -eq $rec['Number']) { continue }
    $want = Get-Body $rec
    if ($want -cne [string]$rec['CurBody']) { Update-Body $rec $want }
}

if ($ShowBodies) {
    Write-Host ''
    Write-Host '=== Computed bodies for issues created or updated ==='
    foreach ($id in $script:Order) {
        $rec = $script:Recs[$id]
        if (-not $rec['Touched']) { continue }
        Write-Host ('--- ' + $id + ' body ---')
        Write-Host (Get-Body $rec)
        Write-Host '--- end ---'
    }
}

$nItems = 0
$nSubs = 0
$nUnchanged = 0
$nUpdated = 0
foreach ($id in $script:Order) {
    $rec = $script:Recs[$id]
    if ($rec['IsSub']) { $nSubs++ } else { $nItems++ }
    if ([int]$rec['Acts'] -eq 0) { $nUnchanged++ }
    if ($rec['Updated']) { $nUpdated++ }
}

foreach ($w in $script:Warnings) { Write-Host $w }
Write-Host ('Summary (' + $mode + '): ' + $nItems + ' items, ' + $nSubs + ' sub-issues; create ' + $script:Counts['create'] + ', update ' + $nUpdated + ', close ' + $script:Counts['close'] + ', reopen ' + $script:Counts['reopen'] + ', project-add ' + $script:Counts['projectAdd'] + ', field-set ' + $script:Counts['fieldSet'] + ', link ' + $script:Counts['link'] + ', unchanged ' + $nUnchanged)

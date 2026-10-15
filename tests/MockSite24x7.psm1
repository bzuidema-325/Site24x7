<#
    A tiny in-process HTTP server that stands in for both the Site24x7 API and Zoho Accounts,
    so the module can be tested without a real account. It records every request and answers
    from a route table the tests fill in.
#>

$script:State = $null

function Start-MockSite24x7 {
    [CmdletBinding()]
    param()

    $port = Get-Random -Minimum 20000 -Maximum 60000
    $prefix = "http://localhost:$port/"
    $listener = [System.Net.HttpListener]::new()
    $listener.Prefixes.Add($prefix)
    $listener.Start()

    $state = [hashtable]::Synchronized(@{
        Listener = $listener
        BaseUri  = $prefix
        Requests = [System.Collections.ArrayList]::Synchronized([System.Collections.ArrayList]::new())
        Routes   = [hashtable]::Synchronized(@{})
        Queued   = [hashtable]::Synchronized(@{})
    })

    $ps = [powershell]::Create()
    [void]$ps.AddScript({
        param($state)
        while ($state.Listener.IsListening) {
            try { $ctx = $state.Listener.GetContext() } catch { break }
            $req = $ctx.Request
            $reader = [System.IO.StreamReader]::new($req.InputStream, [System.Text.Encoding]::UTF8)
            $body = $reader.ReadToEnd()
            $path = $req.Url.AbsolutePath
            $headers = @{}
            foreach ($k in $req.Headers.AllKeys) { $headers[$k] = $req.Headers[$k] }
            [void]$state.Requests.Add([pscustomobject]@{
                Method  = $req.HttpMethod
                Path    = $path
                Query   = $req.Url.Query.TrimStart('?')
                Body    = $body
                Headers = $headers
            })

            $key = "$($req.HttpMethod) $path"
            $route = $null
            if ($state.Queued.ContainsKey($key) -and $state.Queued[$key].Count -gt 0) {
                $route = $state.Queued[$key].Dequeue()
            } elseif ($state.Routes.ContainsKey($key)) {
                $route = $state.Routes[$key]
            } else {
                $route = @{ Status = 200; Body = '{"code":0,"message":"success","data":{}}' }
            }

            $bytes = [System.Text.Encoding]::UTF8.GetBytes([string]$route.Body)
            $ctx.Response.StatusCode = $route.Status
            $ctx.Response.ContentType = 'application/json;charset=UTF-8'
            $ctx.Response.ContentLength64 = $bytes.Length
            $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
            $ctx.Response.Close()
        }
    }).AddArgument($state)
    $state.Handle = $ps.BeginInvoke()
    $state.PowerShell = $ps
    $script:State = $state
    $state
}

function Stop-MockSite24x7 {
    if ($script:State) {
        $script:State.Listener.Stop()
        $script:State.Listener.Close()
        $script:State.PowerShell.Dispose()
        $script:State = $null
    }
}

function Set-MockRoute {
    <# Answers METHOD /path with the given status and body (an object is converted to JSON). #>
    param(
        [Parameter(Mandatory)] [string] $Method,
        [Parameter(Mandatory)] [string] $Path,
        [object] $Body = @{ code = 0; message = 'success'; data = @{} },
        [int] $Status = 200,
        [switch] $Once
    )
    $text = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 20 -Compress }
    $key = "$($Method.ToUpperInvariant()) $Path"
    $route = @{ Status = $Status; Body = $text }
    if ($Once) {
        if (-not $script:State.Queued.ContainsKey($key)) { $script:State.Queued[$key] = [System.Collections.Queue]::Synchronized([System.Collections.Queue]::new()) }
        $script:State.Queued[$key].Enqueue($route)
    } else {
        $script:State.Routes[$key] = $route
    }
}

function Get-MockRequest {
    <# The requests the mock has received, newest last. #>
    param([string] $Method, [string] $Path)
    $script:State.Requests | Where-Object {
        (-not $Method -or $_.Method -eq $Method) -and (-not $Path -or $_.Path -eq $Path)
    }
}

function Clear-MockSite24x7 {
    $script:State.Requests.Clear()
    $script:State.Routes.Clear()
    $script:State.Queued.Clear()
}

Export-ModuleMember -Function Start-MockSite24x7, Stop-MockSite24x7, Set-MockRoute, Get-MockRequest, Clear-MockSite24x7

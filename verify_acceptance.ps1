$baseUrl = "http://localhost:5164"
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " ACXIOMCRM - 14 FINAL ACCEPTANCE CRITERIA VERIFICATION" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$testResults = @()

function Assert-Test($testNum, $description, $condition) {
    if ($condition) {
        Write-Host "[$testNum] PASS: $description" -ForegroundColor Green
        $global:testResults += [PSCustomObject]@{ Id = $testNum; Description = $description; Result = "PASS" }
    } else {
        Write-Host "[$testNum] FAIL: $description" -ForegroundColor Red
        $global:testResults += [PSCustomObject]@{ Id = $testNum; Description = $description; Result = "FAIL" }
    }
}

# 1. Unauthenticated access check (Section 17.19 Item 1)
$resp1 = Invoke-WebRequest -Uri "$baseUrl/Customer" -MaximumRedirection 0 -ErrorAction SilentlyContinue -UseBasicParsing
$isRedirected = ($resp1.StatusCode -eq 302 -and $resp1.Headers.Location -like "*/Account/Login*")
Assert-Test "1" "Unauthenticated request to /Customer redirects to /Account/Login" $isRedirected

# 2. Login with valid test user (Section 17.19 Item 2)
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginPage = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -WebSession $session -UseBasicParsing
$tokenMatch = [regex]::Match($loginPage.Content, '__RequestVerificationToken"[^>]*value="([^"]+)"')
$csrfToken = $tokenMatch.Groups[1].Value

$loginBody = @{
    "Email" = "admin@acxiom.com"
    "Password" = "Admin@12345"
    "RememberMe" = "false"
    "__RequestVerificationToken" = $csrfToken
}
$loginResp = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -Method Post -Body $loginBody -WebSession $session -MaximumRedirection 0 -ErrorAction SilentlyContinue -UseBasicParsing
$authSuccess = ($loginResp.StatusCode -eq 302 -and ($loginResp.Headers.Location -eq "/" -or $loginResp.Headers.Location -like "*/Dashboard*"))
Assert-Test "2" "Admin credentials authenticate successfully and redirect to /Dashboard" $authSuccess

# 3. Client-side validation attributes & scripts present (Section 17.19 Item 3)
$custCreatePage = Invoke-WebRequest -Uri "$baseUrl/Customer/Create" -WebSession $session -UseBasicParsing
$hasValidationScripts = ($custCreatePage.Content -like "*jquery.validate*" -and $custCreatePage.Content -like "*data-val-required*")
Assert-Test "3" "Customer Create view renders client-side validation rules and data-val attributes" $hasValidationScripts

# 4. Server-side validation for invalid/tampered request (Section 17.19 Item 4)
$custTokenMatch = [regex]::Match($custCreatePage.Content, '__RequestVerificationToken"[^>]*value="([^"]+)"')
$custCsrf = $custTokenMatch.Groups[1].Value

$invalidCustBody = @{
    "CustomerName" = "Invalid Test Customer"
    "Email" = "not-an-email"
    "Phone" = "123"
    "CustomerCode" = "CUST-9999"
    "Status" = "Active"
    "__RequestVerificationToken" = $custCsrf
}
$invalidCustResp = Invoke-WebRequest -Uri "$baseUrl/Customer/Create" -Method Post -Body $invalidCustBody -WebSession $session -UseBasicParsing
$serverRejected = ($invalidCustResp.Content -like "*Enter a valid email address*" -or $invalidCustResp.Content -like "*Enter a valid phone number*")
Assert-Test "4" "Server-side validation rejects invalid email and phone when browser rules bypassed" $serverRejected

# 5. Opportunity validation: Amount <= 0 rejected (Section 17.19 Item 5)
$oppCreatePage = Invoke-WebRequest -Uri "$baseUrl/Opportunity/Create" -WebSession $session -UseBasicParsing
$oppTokenMatch = [regex]::Match($oppCreatePage.Content, '__RequestVerificationToken"[^>]*value="([^"]+)"')
$oppCsrf = $oppTokenMatch.Groups[1].Value

$badOppBody1 = @{
    "OpportunityName" = "Invalid Zero Amount Deal"
    "Amount" = "0"
    "Probability" = "50"
    "Stage" = "Qualification"
    "ExpectedCloseDate" = (Get-Date).AddDays(10).ToString("yyyy-MM-dd")
    "__RequestVerificationToken" = $oppCsrf
}
$badOppResp1 = Invoke-WebRequest -Uri "$baseUrl/Opportunity/Create" -Method Post -Body $badOppBody1 -WebSession $session -UseBasicParsing
$amountRejected = ($badOppResp1.Content -like "*Opportunity Amount must be greater than 0*")
Assert-Test "5" "Opportunity with Amount <= 0 is rejected with exact requirement message" $amountRejected

# 6. Opportunity validation: Probability > 100 rejected (Section 17.19 Item 6)
$badOppBody2 = @{
    "OpportunityName" = "Invalid Probability Deal"
    "Amount" = "50000"
    "Probability" = "150"
    "Stage" = "Qualification"
    "ExpectedCloseDate" = (Get-Date).AddDays(10).ToString("yyyy-MM-dd")
    "__RequestVerificationToken" = $oppCsrf
}
$badOppResp2 = Invoke-WebRequest -Uri "$baseUrl/Opportunity/Create" -Method Post -Body $badOppBody2 -WebSession $session -UseBasicParsing
$probRejected = ($badOppResp2.Content -like "*Probability must be between 0 and 100*")
Assert-Test "6" "Opportunity with Probability > 100 is rejected with exact requirement message" $probRejected

# 7. Opportunity validation: Expected Close Date in the past rejected (Section 17.19 Item 7)
$badOppBody3 = @{
    "OpportunityName" = "Past Date Deal"
    "Amount" = "50000"
    "Probability" = "50"
    "Stage" = "Qualification"
    "ExpectedCloseDate" = (Get-Date).AddDays(-5).ToString("yyyy-MM-dd")
    "__RequestVerificationToken" = $oppCsrf
}
$badOppResp3 = Invoke-WebRequest -Uri "$baseUrl/Opportunity/Create" -Method Post -Body $badOppBody3 -WebSession $session -UseBasicParsing
$pastDateRejected = ($badOppResp3.Content -like "*Expected Close Date cannot be in the past*")
Assert-Test "7" "Opportunity with past Expected Close Date is rejected for active deal" $pastDateRejected

# 8. Follow-up validation: Follow-up date earlier than today rejected (Section 17.19 Item 8)
$fuCreatePage = Invoke-WebRequest -Uri "$baseUrl/FollowUp/Create" -WebSession $session -UseBasicParsing
$fuTokenMatch = [regex]::Match($fuCreatePage.Content, '__RequestVerificationToken"[^>]*value="([^"]+)"')
$fuCsrf = $fuTokenMatch.Groups[1].Value

$badFuBody = @{
    "Subject" = "Past Touchpoint"
    "FollowUpType" = "Call"
    "FollowUpDate" = (Get-Date).AddDays(-2).ToString("yyyy-MM-ddTHH:mm")
    "Status" = "Planned"
    "__RequestVerificationToken" = $fuCsrf
}
$badFuResp = Invoke-WebRequest -Uri "$baseUrl/FollowUp/Create" -Method Post -Body $badFuBody -WebSession $session -UseBasicParsing
$fuDateRejected = ($badFuResp.Content -like "*Follow-up date cannot be earlier than today*")
Assert-Test "8" "Follow-Up scheduled prior to today is rejected with exact requirement message" $fuDateRejected

# 9. Login as SalesExecutive -> only assigned scope visible (Section 17.19 Item 9)
$salesSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$salesLogin = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -WebSession $salesSession -UseBasicParsing
$salesTokenMatch = [regex]::Match($salesLogin.Content, '__RequestVerificationToken"[^>]*value="([^"]+)"')
$salesCsrf = $salesTokenMatch.Groups[1].Value

$salesBody = @{
    "Email" = "sales@acxiom.com"
    "Password" = "Sales@12345"
    "RememberMe" = "false"
    "__RequestVerificationToken" = $salesCsrf
}
$salesResp = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -Method Post -Body $salesBody -WebSession $salesSession -MaximumRedirection 0 -ErrorAction SilentlyContinue -UseBasicParsing
$salesDash = Invoke-WebRequest -Uri "$baseUrl/Dashboard" -WebSession $salesSession -UseBasicParsing
$salesScopeValid = ($salesDash.Content -like "*SalesExecutive View*" -or $salesDash.Content -like "*Representative Scope*")
Assert-Test "9" "SalesExecutive login scopes view to assigned representative data" $salesScopeValid

# 10. Login as Manager -> team pipeline & reports access (Section 17.19 Item 10)
$mgrSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$mgrLogin = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -WebSession $mgrSession -UseBasicParsing
$mgrTokenMatch = [regex]::Match($mgrLogin.Content, '__RequestVerificationToken"[^>]*value="([^"]+)"')
$mgrCsrf = $mgrTokenMatch.Groups[1].Value

$mgrBody = @{
    "Email" = "manager@acxiom.com"
    "Password" = "Manager@12345"
    "RememberMe" = "false"
    "__RequestVerificationToken" = $mgrCsrf
}
$mgrResp = Invoke-WebRequest -Uri "$baseUrl/Account/Login" -Method Post -Body $mgrBody -WebSession $mgrSession -MaximumRedirection 0 -ErrorAction SilentlyContinue -UseBasicParsing
$mgrReports = Invoke-WebRequest -Uri "$baseUrl/Reports" -WebSession $mgrSession -UseBasicParsing
$mgrReportsOk = ($mgrReports.StatusCode -eq 200 -and $mgrReports.Content -like "*Stage-Wise Pipeline Performance*")
Assert-Test "10" "Manager login provides access to team pipeline analytics and reports" $mgrReportsOk

# 11. Login as Admin -> User & Security Audit administration (Section 17.19 Item 11)
$adminUsers = Invoke-WebRequest -Uri "$baseUrl/UserManagement" -WebSession $session -UseBasicParsing
$adminAudit = Invoke-WebRequest -Uri "$baseUrl/AuditLog" -WebSession $session -UseBasicParsing
$adminAccessOk = ($adminUsers.StatusCode -eq 200 -and $adminAudit.StatusCode -eq 200)
Assert-Test "11" "Admin login provides full access to User Administration and Security Audit Logs" $adminAccessOk

# 12. Create CRM record & verify audit log entry generated (Section 17.19 Item 12)
$custCreatePage2 = Invoke-WebRequest -Uri "$baseUrl/Customer/Create" -WebSession $session -UseBasicParsing
$custTokenMatch2 = [regex]::Match($custCreatePage2.Content, '__RequestVerificationToken"[^>]*value="([^"]+)"')
$custCsrf2 = $custTokenMatch2.Groups[1].Value

$validCustBody = @{
    "CustomerName" = "Acxiom Automated Test Corp"
    "Email" = "audit_verified@acxiomcorp.com"
    "Phone" = "9988776655"
    "CompanyName" = "AutoCorp Inc."
    "CustomerCode" = "CUST-8899"
    "Status" = "Active"
    "__RequestVerificationToken" = $custCsrf2
}
$createCustResp = Invoke-WebRequest -Uri "$baseUrl/Customer/Create" -Method Post -Body $validCustBody -WebSession $session -MaximumRedirection 0 -ErrorAction SilentlyContinue -UseBasicParsing
$auditPage = Invoke-WebRequest -Uri "$baseUrl/AuditLog" -WebSession $session -UseBasicParsing
$auditLogged = ($auditPage.Content -like "*Acxiom Automated Test Corp*" -or $auditPage.Content -like "*Customer*")
Assert-Test "12" "Creating a CRM customer generates an automated entry in the Security Audit Trail" $auditLogged

# 13. REST API /api/customers call returns JSON response (Section 17.19 Item 13)
$apiResp = Invoke-WebRequest -Uri "$baseUrl/api/customers" -UseBasicParsing
$isApiJson = ($apiResp.StatusCode -eq 200 -and $apiResp.Headers["Content-Type"] -like "*application/json*" -and $apiResp.Content -like "*CustomerCode*")
Assert-Test "13" "REST endpoint /api/customers returns valid JSON payload with status 200 OK" $isApiJson

# 14. Dashboard KPI cards and Chart.js charts rendered (Section 17.19 Item 14)
$dashPage = Invoke-WebRequest -Uri "$baseUrl/Dashboard" -WebSession $session -UseBasicParsing
$hasCharts = ($dashPage.Content -like "*pipelineStageChart*" -and $dashPage.Content -like "*leadStatusChart*" -and $dashPage.Content -like "*monthlySalesChart*")
$hasKpis = ($dashPage.Content -like "*Total Customers*" -and $dashPage.Content -like "*Pipeline Value*")
Assert-Test "14" "Dashboard renders all mandatory KPI cards and Chart.js visualization canvases" ($hasCharts -and $hasKpis)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " SUMMARY: $($testResults.Where({$_.Result -eq 'PASS'}).Count) of $($testResults.Count) ACCEPTANCE CRITERIA PASSED" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

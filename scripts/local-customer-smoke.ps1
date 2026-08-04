param(
    [string]$ApiBaseUrl = "http://localhost:5282/api",
    [Parameter(Mandatory = $true)][string]$EmailOrUserName,
    [Parameter(Mandatory = $true)][string]$Password
)

$ErrorActionPreference = "Stop"

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$origin = $ApiBaseUrl -replace "/api/?$", ""
$ready = Invoke-WebRequest -UseBasicParsing "$origin/health/ready" -TimeoutSec 20
Assert-True ($ready.StatusCode -eq 200) "Readiness check failed."

$login = Invoke-RestMethod -Method Post "$ApiBaseUrl/auth/login" -ContentType "application/json" `
    -Body (@{ emailOrUserName = $EmailOrUserName; password = $Password } | ConvertTo-Json)
$token = $login.data.accessToken
Assert-True (-not [string]::IsNullOrWhiteSpace($token)) "Login did not return an access token."
$headers = @{ Authorization = "Bearer $token" }

$checks = [ordered]@{}
$checks.health = $ready.StatusCode
$checks.profile = (Invoke-WebRequest -UseBasicParsing -Headers $headers "$ApiBaseUrl/account").StatusCode
$checks.preferences = (Invoke-WebRequest -UseBasicParsing -Headers $headers "$ApiBaseUrl/customers/me/preferences").StatusCode
$checks.cart = (Invoke-WebRequest -UseBasicParsing -Headers $headers "$ApiBaseUrl/cart?page=1&pageSize=20").StatusCode
$checks.orders = (Invoke-WebRequest -UseBasicParsing -Headers $headers "$ApiBaseUrl/customer/orders?page=1&pageSize=20").StatusCode
$checks.reviews = (Invoke-WebRequest -UseBasicParsing -Headers $headers "$ApiBaseUrl/reviews/mine?page=1&pageSize=20").StatusCode
$checks.complaints = (Invoke-WebRequest -UseBasicParsing -Headers $headers "$ApiBaseUrl/complaints/mine?page=1&pageSize=20").StatusCode
$checks.notifications = (Invoke-WebRequest -UseBasicParsing -Headers $headers "$ApiBaseUrl/notifications?page=1&pageSize=20").StatusCode
$checks.chats = (Invoke-WebRequest -UseBasicParsing -Headers $headers "$ApiBaseUrl/chats?page=1&pageSize=20").StatusCode
$checks.ai = (Invoke-WebRequest -UseBasicParsing -Headers $headers "$ApiBaseUrl/ai/recommendations/me" -TimeoutSec 60).StatusCode

$tags = (Invoke-RestMethod "$ApiBaseUrl/food-tags?page=1&pageSize=100").data
$categories = (Invoke-RestMethod "$ApiBaseUrl/food-categories?page=1&pageSize=100").data
$markets = (Invoke-RestMethod "$ApiBaseUrl/night-markets?page=1&pageSize=20").data
$foods = (Invoke-RestMethod "$ApiBaseUrl/foods?page=1&pageSize=20").data
Assert-True ($tags.total -gt 0) "Food-tag seed is empty."
Assert-True ($categories.total -gt 0) "Food-category seed is empty."
Assert-True ($markets.total -gt 0) "Market seed is empty."
Assert-True ($foods.total -gt 0) "Food seed is empty."

[pscustomobject]@{
    checks = $checks
    foodTags = $tags.total
    foodCategories = $categories.total
    markets = $markets.total
    foods = $foods.total
} | ConvertTo-Json -Depth 5

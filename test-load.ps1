$baseUrl = "http://localhost:5252/api/carts"

Write-Host "Starting load test: Sending 100 cart flows..." -ForegroundColor Cyan

for ($i = 1; $i -le 100; $i++) {
    $userId = [guid]::NewGuid().ToString()
    $productId = [guid]::NewGuid().ToString()
    $idempotencyKey = [guid]::NewGuid().ToString()
    
    $productName = "Product Number $i"
    $price = Get-Random -Minimum 10 -Maximum 5000
    $quantity = Get-Random -Minimum 1 -Maximum 5

    Write-Host "[$i/100] Processing Cart for User: $userId" -ForegroundColor Yellow

    # 1. Add Item to Cart
    $addItemBody = @{
        productId = $productId
        productName = $productName
        category = "TestCategory"
        quantity = $quantity
        price = $price
    } | ConvertTo-Json

    try {
        Invoke-RestMethod -Uri "$baseUrl/$userId/items" `
                          -Method Post `
                          -Body $addItemBody `
                          -ContentType "application/json" | Out-Null
        
        Write-Host "  - Item added: $productName ($quantity x $price TL)" -ForegroundColor Green

        # 2. Confirm Cart
        Invoke-RestMethod -Uri "$baseUrl/$userId/confirm" `
                          -Method Post `
                          -Headers @{ "X-Idempotency-Key" = $idempotencyKey } `
                          -ContentType "application/json" | Out-Null
                          
        Write-Host "  - Cart confirmed successfully! Integration Event dispatched." -ForegroundColor Green
    }
    catch {
        Write-Host "  - Error processing cart $i : $_" -ForegroundColor Red
    }
    
    # Optional: slight delay so we can watch the console output stream smoothly
    Start-Sleep -Milliseconds 50
}

Write-Host "Load test completed! Check RabbitMQ Management UI and Service Logs." -ForegroundColor Cyan

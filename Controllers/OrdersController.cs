[HttpPost]
public async Task<ActionResult> PlaceOrder(Order incomingOrder)
{
    try 
    {
        incomingOrder.Status = "Received";
        _context.Orders.Add(incomingOrder);
        
        // 1. Try to save to MongoDB
        await _context.SaveChangesAsync();

        // 2. Try to ping the Kitchen Dashboard
        await _hubContext.Clients.All.SendAsync("ReceiveNewOrder", incomingOrder);
        
        // 3. TEMPORARILY DISABLE EMAIL (Comment this out for now!)
        // If your SMTP password isn't set up perfectly, this line is what causes the Network Error.
        // SendReceiptEmail(incomingOrder); 

        return Ok(incomingOrder);
    }
    catch (Exception ex)
    {
        // If anything crashes, this catches it, keeps CORS working, and prints the real error to Render!
        Console.WriteLine($"🚨 CRITICAL ORDER CRASH: {ex.Message}");
        if (ex.InnerException != null) 
        {
            Console.WriteLine($"🚨 INNER EXCEPTION: {ex.InnerException.Message}");
        }
        
        return StatusCode(500, new { error = "Backend crashed!", message = ex.Message });
    }
}